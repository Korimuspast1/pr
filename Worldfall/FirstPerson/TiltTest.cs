using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Gpu;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TiltTest : CivilTest
	{
		private static readonly float[] Looks = new float[4] { -40f, -20f, 0f, 25f };

		private static readonly float[] SteepLooks = new float[6] { -88f, -70f, -55f, 55f, 70f, 88f };

		private float _frameMs;

		private float _modMs;

		protected override string Prefix => "TILT";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 6, 0);
			if (king == null)
			{
				Check("a village was founded", ok: false);
				yield break;
			}
			yield return CivilTest.Wait(3f);
			Actor me = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != king && item.isAdult())
				{
					me = item;
					break;
				}
			}
			if (me == null)
			{
				me = king;
			}
			yield return Possess(me);
			if (Check("first person came up", InControl(me)))
			{
				Settings s = Mod.Settings;
				float savedFov = s.FieldOfView;
				bool savedGpu = s.GpuRender;
				s.FieldOfView = 110f;
				s.GpuRender = false;
				try
				{
					yield return Part("tilted on the CPU", Cpu(me));
					yield return Part("tilted on the graphics card", Gpu(me));
					yield return Part("right up and down on the graphics card", Steep());
					yield return Part("in a house", Room());
					yield return Part("swimming in lava", Lava(me));
					yield return Part("letting go while looking right down", RiseFromSteep());
				}
				finally
				{
					TiltTest tiltTest = this;
					s.FieldOfView = savedFov;
					s.GpuRender = savedGpu;
					tiltTest.Mod.SetPitchDegrees(0f);
				}
			}
		}

		private IEnumerator Cpu(Actor me)
		{
			Config.paused = true;
			float[] looks = Looks;
			foreach (float look in looks)
			{
				Mod.SetPitchDegrees(look);
				yield return CivilTest.Wait(0.6f);
				FrameRenderer renderer = Mod.Renderer;
				Lens frameLens = renderer.FrameLens;
				float num = frameLens.Tilt * 57.29578f;
				Check($"looking {look:0}: the camera tilts", Mathf.Abs(num - look) < 2.5f && Mathf.Abs(frameLens.Shift) < 0.05f, $"tilt {num:F1} deg, the rest slid {Mathf.Atan(frameLens.Shift) * 57.29578f:F2} deg");
				Check($"looking {look:0}: the land in strips when tilted", (look == 0f) ? (renderer.LandColumns == renderer.Width) : (renderer.LandColumns > renderer.Width), $"{renderer.LandColumns} land columns for {renderer.Width}");
				RoundTrip(renderer, $"looking {look:0}, the middle", renderer.Width / 2, renderer.Height / 2);
				RoundTrip(renderer, $"looking {look:0}, a bottom corner", renderer.Width / 8, renderer.Height * 7 / 8);
				RoundTrip(renderer, $"looking {look:0}, a top corner", renderer.Width * 7 / 8, renderer.Height / 8);
				Mod.SaveFrame($"tilt_cpu_{look:0}.png");
			}
			Config.paused = false;
		}

		private void RoundTrip(FrameRenderer r, string what, int c, int row)
		{
			float num = r.Depth[(r.Height - 1 - row) * r.Width + c];
			if (!(num < 1000000f))
			{
				Note(what + ": sky (no depth to take back)");
				return;
			}
			ViewState view = Mod.View;
			float num2 = (float)c + 0.5f;
			float num3 = (float)row + 0.5f;
			r.PixelRay(num2, num3, out var side, out var rise);
			float num4 = Mathf.Cos(view.Yaw);
			float num5 = Mathf.Sin(view.Yaw);
			float x = view.X + (num4 + num5 * side) * num;
			float y = view.Y + (num5 - num4 * side) * num;
			float z = view.Z + rise * num;
			float col;
			float row2;
			float depth;
			bool flag = r.Project(x, y, z, out col, out row2, out depth);
			float col2;
			float row3;
			float depth2;
			bool flag2 = Mod.Shown.Project(x, y, z, out col2, out row3, out depth2);
			float num6 = Mathf.Max(Mathf.Abs(col - num2), Mathf.Abs(row2 - num3));
			float num7 = Mathf.Max(Mathf.Abs(col2 - num2), Mathf.Abs(row3 - num3));
			Check(what + ": the point projects back onto its pixel", flag && flag2 && num6 < 0.05f && num7 < 0.05f && Mathf.Abs(depth - num) < 0.01f * num + 0.001f, $"t {num:F2}; renderer {col:F2},{row2:F2} (off {num6:F3}), HUD {col2:F2},{row3:F2} (off {num7:F3}) for {num2:F1},{num3:F1}");
		}

		private IEnumerator Gpu(Actor me)
		{
			Mod.Settings.GpuRender = true;
			yield return CivilTest.Wait(1.5f);
			GpuRenderer gpu = Mod.GpuRendererForTest;
			if (gpu == null || !gpu.Available)
			{
				Note("no graphics card renderer here: skipped");
				Mod.Settings.GpuRender = false;
				yield break;
			}
			Config.paused = true;
			float[] looks = Looks;
			foreach (float look in looks)
			{
				Mod.SetPitchDegrees(look);
				yield return CivilTest.Wait(0.6f);
				Lens frameLens = gpu.Projection.FrameLens;
				Check($"graphics card, looking {look:0}: the camera tilts", Mathf.Abs(frameLens.Tilt * 57.29578f - look) < 2.5f, string.Format("tilt {0:F1} deg, error {1}", frameLens.Tilt * 57.29578f, gpu.Error ?? "none"));
				Mod.SaveFrame($"tilt_gpu_{look:0}.png");
			}
			Config.paused = false;
			Mod.Settings.GpuRender = false;
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Steep()
		{
			Settings s = Mod.Settings;
			int savedRes = s.Resolution;
			s.GpuRender = true;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			yield return CivilTest.Wait(1.5f);
			GpuRenderer gpu = Mod.GpuRendererForTest;
			if (gpu == null || !gpu.Available)
			{
				Note("no graphics card renderer here: skipped");
				s.GpuRender = false;
				s.Resolution = savedRes;
				yield break;
			}
			Check("on the graphics card outdoors, the mouse looks 88 degrees up and down", Mathf.Abs(Mod.LookLimitForTest - 88f) < 0.01f, $"{Mod.LookLimitForTest:F1}");
			Check("1080p: the card draws the picture 1920 wide", gpu.Output != null && gpu.Output.width == 1920 && Mod.ShownFrame == gpu.Shown, string.Format("{0}, supersample {1}", (gpu.Output != null) ? (gpu.Output.width + "x" + gpu.Output.height) : "none", gpu.Supersample));
			Config.paused = true;
			TerrainCache cache = Mod.SamplerForTest.Cache;
			try
			{
				Mod.SetPitchDegrees(0f);
				yield return MeasureFrames(1.5f);
				Note($"1080p level, the world paused: {_frameMs:F1} ms a frame (the window {Screen.width}x{Screen.height})");
				gpu.ReadDepth = true;
				float[] steepLooks = SteepLooks;
				foreach (float look in steepLooks)
				{
					Mod.SetPitchDegrees(look);
					yield return CivilTest.Wait(0.8f);
					Lens frameLens = gpu.Projection.FrameLens;
					float num = frameLens.Tilt * 57.29578f;
					Check($"1080p, looking {look:0}: the lens tilts all the way, drawn as a general camera", Mathf.Abs(num - look) < 1.5f && Mathf.Abs(frameLens.Shift) < 0.01f && frameLens.Steep && gpu.Error == null, string.Format("tilt {0:F1} deg, slid {1:F2} deg, steep {2}, error {3}", num, Mathf.Atan(frameLens.Shift) * 57.29578f, frameLens.Steep, gpu.Error ?? "none"));
					int width = gpu.Width;
					int height = gpu.Height;
					SteepRoundTrip(gpu, $"looking {look:0}, the middle", width / 2, height / 2);
					SteepRoundTrip(gpu, $"looking {look:0}, a bottom corner", width / 8, height * 7 / 8);
					SteepRoundTrip(gpu, $"looking {look:0}, a top corner", width * 7 / 8, height / 8);
					if (look < 0f)
					{
						float pickDepth = Mod.Renderer.PickDepth;
						ViewState view = Mod.View;
						float num2 = view.X + Mathf.Cos(view.Yaw) * pickDepth;
						float num3 = view.Y + Mathf.Sin(view.Yaw) * pickDepth;
						float num4 = view.Z + view.Pitch * pickDepth;
						float num5 = ((pickDepth < 1000000f) ? Mathf.Max(cache.SampleHeight(num2, num3), 0f) : float.NaN);
						bool flag = Mod.Picked != null;
						Check($"looking {look:0}: what the crosshair is on lies on its ray, on the land below", pickDepth < 1000000f && (flag || Mathf.Abs(num4 - num5) < 0.3f), string.Format("{0:F3} tiles ahead along the ground, the point {1:F1},{2:F1} at {3:F2}, the land {4:F2}, on {5}", pickDepth, num2, num3, num4, num5, flag ? Mod.Picked.GetType().Name : "nothing"));
					}
					Mod.SaveFrame($"tilt_steep_{look:0}.png");
				}
				float edge = 0f - Lens.FullTiltDegrees(Mod.View.Fov, (float)gpu.Height / (float)gpu.Width);
				steepLooks = new float[2]
				{
					edge + 0.4f,
					edge - 0.4f
				};
				foreach (float look in steepLooks)
				{
					Mod.SetPitchDegrees(look);
					yield return CivilTest.Wait(0.8f);
					Note(string.Format("looking {0:0.0} ({1}): tilt_switch_{2}.png", look, gpu.Projection.FrameLens.Steep ? "general camera" : "the lens", (look > edge) ? "lens" : "eye"));
					Mod.SaveFrame("tilt_switch_" + ((look > edge) ? "lens" : "eye") + ".png");
				}
				s.Resolution = 1;
				steepLooks = new float[6] { 0f, -50f, -70f, -88f, 60f, 88f };
				foreach (float look in steepLooks)
				{
					Mod.SetPitchDegrees(look);
					yield return CivilTest.Wait(0.5f);
					yield return MeasureModMs(1.5f);
					Note(string.Format("360p, looking {0:0}: {1:F2} ms of the game's thread a frame ({2}), {3} meshes, {4} billboards, {5} far props", look, _modMs, gpu.Projection.FrameLens.Steep ? "general camera" : "the lens", Mod.SceneForTest.Meshes.Count, Mod.SceneForTest.Billboards.Count, Mod.Renderer.FarProps.Count));
				}
				Mod.SetPitchDegrees(0f);
				yield return CivilTest.Wait(0.6f);
				yield return MeasureFrames(1.5f);
				Note($"360p level, the world paused: {_frameMs:F1} ms a frame");
			}
			finally
			{
				Config.paused = false;
				gpu.ReadDepth = false;
			}
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			s.GpuRender = false;
			Mod.SetPitchDegrees(-88f);
			yield return CivilTest.Wait(1f);
			float lookLimitForTest = Mod.LookLimitForTest;
			float num6 = Mathf.Atan(Mod.View.Pitch) * 57.29578f;
			Check("1080p on the CPU: drawn no wider than 640", Mod.Renderer.Width == 640 && !Mod.Renderer.FrameLens.Steep, $"{Mod.Renderer.Width}x{Mod.Renderer.Height}");
			Check("on the CPU the look comes back within what it tilts", lookLimitForTest >= 45f && lookLimitForTest < 60f && num6 > 0f - lookLimitForTest - 1.5f, $"limit {lookLimitForTest:F1} deg, looking {num6:F1}, tilt {Mod.Renderer.FrameLens.Tilt * 57.29578f:F1}");
			Mod.SetPitchDegrees(0f);
			s.Resolution = savedRes;
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator MeasureModMs(float seconds)
		{
			float start = Time.realtimeSinceStartup;
			float sum = 0f;
			int frames = 0;
			while (Time.realtimeSinceStartup - start < seconds)
			{
				yield return null;
				sum += Mod.FrameMs;
				frames++;
			}
			_modMs = sum / (float)Mathf.Max(1, frames);
		}

		private IEnumerator MeasureFrames(float seconds)
		{
			float start = Time.realtimeSinceStartup;
			int frames = 0;
			while (Time.realtimeSinceStartup - start < seconds)
			{
				yield return null;
				frames++;
			}
			_frameMs = (Time.realtimeSinceStartup - start) * 1000f / (float)Mathf.Max(1, frames);
		}

		private void SteepRoundTrip(GpuRenderer gpu, string what, int c, int row)
		{
			int depthWidth = gpu.DepthWidth;
			int depthHeight = gpu.DepthHeight;
			GpuRenderer.DepthSizeFor(gpu.Width, gpu.Height, out var w, out var h);
			if (gpu.DepthFrame < 0 || depthWidth != w || depthHeight != h)
			{
				Check(what + ": the card's depths came back", ok: false, $"frame {gpu.DepthFrame}, {depthWidth}x{depthHeight} for {w}x{h}");
				return;
			}
			int num = Mathf.Clamp((int)((float)c * (float)depthWidth / (float)gpu.Width), 0, depthWidth - 1);
			int num2 = Mathf.Clamp((int)((float)row * (float)depthHeight / (float)gpu.Height), 0, depthHeight - 1);
			float num3 = gpu.DepthPixels[(depthHeight - 1 - num2) * depthWidth + num];
			if (!(num3 < 1000000f))
			{
				Note(what + ": sky (no depth to take back)");
				return;
			}
			FrameProjection projection = gpu.Projection;
			ViewState view = projection.View;
			Lens frameLens = projection.FrameLens;
			float num4 = Mathf.Cos(view.Yaw);
			float num5 = Mathf.Sin(view.Yaw);
			Vector3 vector = new Vector3(num4 * frameLens.Cq, num5 * frameLens.Cq, frameLens.Sq);
			Vector3 vector2 = new Vector3((0f - num4) * frameLens.Sq, (0f - num5) * frameLens.Sq, frameLens.Cq);
			Vector3 vector3 = new Vector3(num5, 0f - num4, 0f);
			float num6 = ((float)num + 0.5f) * (float)gpu.Width / (float)depthWidth;
			float num7 = ((float)num2 + 0.5f) * (float)gpu.Height / (float)depthHeight;
			float num8 = (num6 - (float)gpu.Width * 0.5f) / projection.Focal;
			float num9 = (projection.Horizon - num7) / projection.Focal;
			Vector3 vector4 = vector + num8 * vector3 + num9 * vector2;
			float num10 = num3 / Mathf.Max(vector4.x * num4 + vector4.y * num5, 0.05f);
			Vector3 vector5 = new Vector3(view.X, view.Y, view.Z) + num10 * vector4;
			float col;
			float row2;
			float depth;
			bool flag = Mod.Shown.Project(vector5.x, vector5.y, vector5.z, out col, out row2, out depth);
			float num11 = Mathf.Max(Mathf.Abs(col - num6), Mathf.Abs(row2 - num7));
			Check(what + ": the point projects back onto its pixel", flag && num11 < 0.6f, $"depth {num3:F2} ({num10:F2} along the eye), the point {vector5.x:F1},{vector5.y:F1},{vector5.z:F2}; HUD {col:F2},{row2:F2} (off {num11:F3}) for {num6:F1},{num7:F1}");
			float num12 = Mod.SamplerForTest.Cache.SampleHeight(vector5.x, vector5.y);
			Check(what + ": the drawn point lies on the land or on something standing on it", vector5.z >= Mathf.Max(num12, 0f) - 0.08f, $"the point at {vector5.z:F2}, the land there {num12:F2}");
		}

		private IEnumerator RiseFromSteep()
		{
			Mod.Settings.GpuRender = true;
			Mod.DebugEyeSet = false;
			Mod.ForceSwim = false;
			Mod.SetPitchDegrees(-80f);
			yield return CivilTest.Wait(1.5f);
			GpuRenderer gpu = Mod.GpuRendererForTest;
			if (gpu == null || !gpu.Available)
			{
				Note("no graphics card renderer here: skipped");
				yield break;
			}
			Lens frameLens = Mod.Shown.FrameLens;
			float was = frameLens.Tilt * 57.29578f;
			Check("before letting go: looking 80 down, drawn as a general camera", frameLens.Steep && Mathf.Abs(was + 80f) < 3f, $"tilt {was:F1}, steep {frameLens.Steep}");
			Mod.NoDive = false;
			try
			{
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch (Exception ex)
				{
					Note("letting go: " + ex.Message);
				}
				int seen = 0;
				int shown = Mod.FramesShown;
				float first = float.NaN;
				float worstJump = 0f;
				float last = was;
				for (float time = 0f; time < 8f; time += Time.unscaledDeltaTime)
				{
					yield return null;
					if (!Mod.DivingOutForTest)
					{
						break;
					}
					if (Mod.FramesShown != shown)
					{
						shown = Mod.FramesShown;
						float num = Mod.Shown.FrameLens.Tilt * 57.29578f;
						if (seen == 0)
						{
							first = num;
							Mod.SaveFrame("tilt_rise_first.png");
						}
						else
						{
							worstJump = Mathf.Max(worstJump, Mathf.Abs(num - last));
						}
						last = num;
						seen++;
					}
				}
				Check("letting go: the rise starts from the look as it was, not slid", seen > 0 && Mathf.Abs(first - was) < 4f, $"{seen} frames of the rise; first tilted {first:F1} after {was:F1}, the most it turned in a frame {worstJump:F1} deg");
				Check("letting go: the rise is drawn on the card to its end", !Mod.DivingOutForTest && gpu.Error == null, string.Format("still rising {0}, error {1}", Mod.DivingOutForTest, gpu.Error ?? "none"));
			}
			finally
			{
				Mod.NoDive = true;
			}
		}

		private IEnumerator Room()
		{
			Mod.EnterRoomForTest(new List<Actor>());
			yield return CivilTest.Wait(1f);
			if (Check("inside a house", Mod.Interior.Inside))
			{
				float[] array = new float[2] { -40f, 20f };
				foreach (float look in array)
				{
					Mod.SetPitchDegrees(look);
					yield return CivilTest.Wait(0.8f);
					FrameRenderer renderer = Mod.Renderer;
					Check($"in the house, looking {look:0}: the camera tilts", Mathf.Abs(renderer.FrameLens.Tilt * 57.29578f - look) < 2.5f, $"tilt {renderer.FrameLens.Tilt * 57.29578f:F1}");
					RoundTrip(renderer, $"in the house, looking {look:0}, the middle", renderer.Width / 2, renderer.Height / 2);
					RoundTrip(renderer, $"in the house, looking {look:0}, a bottom corner", renderer.Width / 8, renderer.Height * 7 / 8);
					Mod.SaveFrame($"tilt_room_{look:0}.png");
				}
				Mod.SetPitchDegrees(0f);
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(1f);
			}
		}

		private IEnumerator Lava(Actor me)
		{
			TerrainCache cache = Mod.SamplerForTest.Cache;
			WorldTile current_tile = me.current_tile;
			WorldTile spot = World.world.GetTile(current_tile.x + 5, current_tile.y) ?? World.world.GetTile(current_tile.x - 5, current_tile.y);
			if (spot == null)
			{
				Check("a spot for the lava", ok: false);
				yield break;
			}
			Note($"lava poured at {spot.x},{spot.y} on {spot.Type?.id}, land {cache.SampleHeight((float)spot.x + 0.5f, (float)spot.y + 0.5f):F2} high (the sea at {0f:F2})");
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					WorldTile tile = World.world.GetTile(spot.x + i, spot.y + j);
					if (tile != null)
					{
						try
						{
							World.world.drop_manager.spawn(tile, "lava", 2f, -1f, -1L);
						}
						catch (Exception ex)
						{
							Note("lava drop: " + ex.Message);
						}
					}
				}
			}
			Vector2 at = new Vector2((float)spot.x + 0.5f, (float)spot.y + 0.5f);
			bool flag = false;
			for (float time = 0f; time < 10f; time += 0.25f)
			{
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				flag = spot.Type != null && spot.Type.lava && (cache.Flags[spot.y * cache.Width + spot.x] & 2) != 0;
			}
			if (!Check("the lava came down", flag, "tile " + spot.Type?.id))
			{
				yield break;
			}
			Mod.DebugEye = at;
			Mod.DebugEyeSet = true;
			Mod.ForceSwim = true;
			Mod.SetPitchDegrees(-25f);
			float worstGap = float.MaxValue;
			float eyeSeen = 0f;
			for (float time = 0f; time < 2.5f; time += 0.1f)
			{
				yield return CivilTest.Wait(0.1f);
				if (!(time < 1.2f))
				{
					float num = Mod.View.Z - cache.LiquidSurface(at.x, at.y);
					if (num < worstGap)
					{
						worstGap = num;
						eyeSeen = Mod.View.Z;
					}
				}
			}
			float num2 = cache.LiquidSurface(at.x, at.y);
			float num3 = cache.SampleHeight(at.x, at.y);
			Mod.SaveFrame("tilt_lava.png");
			Check("swimming in lava, your eye stays over it, not under the land", Mod.Swimming && worstGap > 0.05f && worstGap < 0.6f, $"swimming {Mod.Swimming}; eye {eyeSeen:F2}, lava surface {num2:F2}, ground there {num3:F2}, sea {0f:F2}; lowest eye over the surface {worstGap:F2}");
			Mod.ForceSwim = false;
			Mod.SetPitchDegrees(0f);
		}
	}
}
