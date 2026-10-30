using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TornadoTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private TornadoEffect _twister;

		private int _pass;

		private int _fail;

		private Vector2 _site;

		private float _angle = 4.2f;

		private float _meanMs;

		private float _worstMs;

		private Vector2 Foot
		{
			get
			{
				if (!(_twister != null))
				{
					return _site;
				}
				return _twister.transform.position;
			}
		}

		private void Check(bool ok, string what)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info((ok ? "TWISTER PASS " : "TWISTER FAIL ") + what);
		}

		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 3, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("TWISTER could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("TWISTER first person did not start");
				Finish(quit);
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			IEnumerator run = Run();
			while (true)
			{
				object current2;
				try
				{
					if (!run.MoveNext())
					{
						break;
					}
					current2 = run.Current;
					goto IL_0272;
				}
				catch (Exception ex)
				{
					Log.Error("TWISTER failed: " + ex);
				}
				break;
				IL_0272:
				yield return current2;
			}
			try
			{
				if (_twister != null && ((BaseEffect)_twister).active)
				{
					_twister.kill();
				}
			}
			catch
			{
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			_mod.SetPitchDegrees(0f);
			s.TimeMode = savedMode;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Finish(quit);
		}

		private void Finish(bool quit)
		{
			Log.Info($"TWISTER {_pass} passed, {_fail} failed");
			Log.Info("TWISTER DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private bool Site(float room, out Vector2 site)
		{
			site = Vector2.zero;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				return false;
			}
			float num = float.MaxValue;
			Vector2 current_position = _king.current_position;
			for (int i = (int)room + 4; (float)i < (float)terrainForTest.Height - room - 4f; i += 4)
			{
				for (int j = (int)room + 4; (float)j < (float)terrainForTest.Width - room - 4f; j += 4)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					int num5 = 0;
					for (int k = -(int)room; (float)k <= room; k += 4)
					{
						for (int l = -(int)room; (float)l <= room; l += 4)
						{
							int num6 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num6] & 3) != 0)
							{
								num4++;
								continue;
							}
							num2 = Mathf.Min(num2, terrainForTest.Ground[num6]);
							num3 = Mathf.Max(num3, terrainForTest.Ground[num6]);
							WorldTile tile = World.world.GetTile(j + l, i + k);
							if (tile != null && tile.building != null && AssetStyle.IsSolid(tile.building))
							{
								num5++;
							}
						}
					}
					if (num2 > num3)
					{
						continue;
					}
					float magnitude = (new Vector2(j, i) - current_position).magnitude;
					if (!(magnitude < room + 35f))
					{
						float num7 = (float)num4 * 4f + (float)num5 * 1.5f + (num3 - num2) * 2f + magnitude * 0.01f;
						if (num7 < num)
						{
							num = num7;
							site = new Vector2((float)j + 0.5f, (float)i + 0.5f);
						}
					}
				}
			}
			if (num < float.MaxValue)
			{
				Log.Info($"TWISTER site {site} (score {num:F1})");
			}
			return num < float.MaxValue;
		}

		private void PickAngle(float back, float height)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector2 foot = Foot;
			float num = float.MaxValue;
			for (int i = 0; i < 16; i++)
			{
				float num2 = (float)i * MathF.PI / 8f;
				Vector2 a = foot + new Vector2(Mathf.Cos(num2), Mathf.Sin(num2)) * back;
				if (a.x < 3f || a.y < 3f || a.x > (float)terrainForTest.Width - 4f || a.y > (float)terrainForTest.Height - 4f)
				{
					continue;
				}
				float num3 = terrainForTest.SampleHeight(a.x, a.y) + 1f + height;
				float num4 = terrainForTest.SampleHeight(foot.x, foot.y) + 4f;
				float num5 = 0f;
				int num6 = Mathf.CeilToInt(back);
				for (int j = 1; j < num6; j++)
				{
					float num7 = (float)j / (float)num6;
					Vector2 vector = Vector2.Lerp(a, foot, num7);
					float num8 = num3 + (num4 - num3) * num7;
					float num9 = terrainForTest.SampleHeight(vector.x, vector.y);
					if (num9 > num8)
					{
						num5 += 3f + (num9 - num8);
					}
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (tile != null && tile.building != null && AssetStyle.IsSolid(tile.building) && num9 + 3.5f > num8)
					{
						num5 += ((num7 < 0.3f) ? 2f : 1f);
					}
				}
				int num10 = (int)a.y * terrainForTest.Width + (int)a.x;
				if ((terrainForTest.Flags[num10] & 1) != 0)
				{
					num5 += 4f;
				}
				if (num5 < num)
				{
					num = num5;
					_angle = num2;
				}
			}
			Log.Info($"TWISTER watching {back:F0} tiles off from {_angle * 57.29578f:F0} deg ({num:F0} in the way)");
		}

		private void Watch(float back, float pitch, float height = 0f)
		{
			Vector2 foot = Foot;
			Vector2 debugEye = foot + new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * back;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			debugEye.x = Mathf.Clamp(debugEye.x, 2f, (float)terrainForTest.Width - 3f);
			debugEye.y = Mathf.Clamp(debugEye.y, 2f, (float)terrainForTest.Height - 3f);
			_mod.DebugEye = debugEye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = height;
			_mod.SetYaw(Mathf.Atan2(foot.y - debugEye.y, foot.x - debugEye.x));
			_mod.SetPitchDegrees(pitch);
		}

		private IEnumerator Hold(float back, float pitch, float seconds, float height = 0f)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Watch(back, pitch, height);
				yield return null;
			}
		}

		private static void Clear(Vector2 at, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					if (j * j + i * i > r * r)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile((int)at.x + j, (int)at.y + i);
					try
					{
						if (tile != null && tile.building != null)
						{
							tile.building.startDestroyBuilding();
						}
					}
					catch
					{
					}
				}
			}
		}

		private IEnumerator Clock(float back, float pitch, float seconds, float height = 0f)
		{
			float sum = 0f;
			float worst = 0f;
			int n = 0;
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Watch(back, pitch, height);
				if (t > 0.5f)
				{
					sum += _mod.FrameMs;
					worst = Mathf.Max(worst, _mod.FrameMs);
					n++;
				}
				yield return null;
			}
			_meanMs = sum / (float)Mathf.Max(1, n);
			_worstMs = worst;
		}

		private IEnumerator Run()
		{
			SceneCollector scene = _mod.SceneForTest;
			if (!Site(26f, out var site))
			{
				Log.Error("TWISTER no site");
				yield break;
			}
			_site = site;
			Clear(site, 7);
			WorldTile tile = World.world.GetTile((int)site.x, (int)site.y);
			PickAngle(42f, 5f);
			yield return Clock(42f, 10f, 4f, 5f);
			float calmMs = _meanMs;
			Log.Info($"TWISTER no tornado: frame {calmMs:F1} ms (worst {_worstMs:F1})");
			_mod.SaveFrame("twister_before.png");
			PickAngle(80f, 10f);
			Watch(80f, 4f, 10f);
			yield return new WaitForSecondsRealtime(1f);
			Check(Mathf.Abs(scene.Weather.Base - 15f) < 0.01f && scene.TwistersForTest == 0, $"no tornado: the clouds hang where they always do ({scene.Weather.Base:F1})");
			try
			{
				_twister = EffectsLibrary.spawnAtTile("fx_tornado", tile, 0.0833f) as TornadoEffect;
				if (_twister != null)
				{
					_twister.resizeTornado(0.5f);
				}
			}
			catch (Exception ex)
			{
				Log.Error("TWISTER spawn: " + ex.Message);
			}
			if (_twister == null)
			{
				Log.Error("TWISTER no tornado");
				yield break;
			}
			float baseAt1 = 0f;
			for (float t = 0f; t < 7f; t += Time.unscaledDeltaTime)
			{
				Watch(80f, 4f, 10f);
				if (baseAt1 == 0f && t > 1f)
				{
					baseAt1 = scene.Weather.Base;
					_mod.SaveFrame("twister_forming.png");
				}
				yield return null;
			}
			Log.Info($"TWISTER formed: scale {((BaseEffect)_twister).scale:F2}, cloud base {baseAt1:F1} after 1 s, {scene.Weather.Base:F1} now, {scene.TwisterStormsForTest} storms, {scene.TwisterPiecesForTest} pieces, frame {_mod.FrameMs:F1} ms");
			Check(scene.TwistersForTest == 1, "the tornado is followed");
			Check(scene.TwisterStormsForTest >= 1, "it hangs from a storm of its own");
			Check(scene.Weather.Base > 25f && baseAt1 < scene.Weather.Base, $"the cloud layer rises under its storm as it builds ({baseAt1:F1} -> {scene.Weather.Base:F1})");
			Check(scene.TwisterPiecesForTest >= 30, $"it is drawn far off ({scene.TwisterPiecesForTest} pieces)");
			_mod.SaveFrame("twister_far.png");
			(string, float, float, float)[] array = new(string, float, float, float)[3]
			{
				("mid", 42f, 10f, 5f),
				("near", 18f, 16f, 1.5f),
				("close", 8f, 22f, 0f)
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (name, t, pitch, height) = array[i];
				PickAngle(t, height);
				float worst = 0f;
				float sum = 0f;
				float shake = 0f;
				int n = 0;
				int arcs = 0;
				int bits = 0;
				for (float t2 = 0f; t2 < 3.5f; t2 += Time.unscaledDeltaTime)
				{
					Watch(t, pitch, height);
					if (t2 > 1f)
					{
						worst = Mathf.Max(worst, _mod.FrameMs);
						sum += _mod.FrameMs;
						n++;
					}
					if (t2 > 1f)
					{
						shake = Mathf.Max(shake, scene.TwisterShakeForTest);
					}
					arcs = Mathf.Max(arcs, scene.WindForTest.Arcs);
					bits = Mathf.Max(bits, scene.WindForTest.Bits);
					yield return null;
				}
				_mod.SaveFrame("twister_" + name + ".png");
				Gusts gusts = scene.Gusts;
				Log.Info($"TWISTER {name} ({t:F0} tiles): {scene.TwisterPiecesForTest} pieces, {_mod.BillboardCount} billboards, frame {sum / (float)Mathf.Max(1, n):F1} ms (worst {worst:F1}), " + $"wind {gusts.Strength:F2} fury {gusts.Fury:F2}, grass {GrassTufts.WindStrength:F2}, {arcs} arcs, {bits} bits, shake {scene.TwisterShakeForTest:F2} (most {shake:F2})");
				if (name == "near")
				{
					Check(gusts.Fury > 0.3f && gusts.Strength > 0.3f, $"near it its wind blows hard ({gusts.Fury:F2})");
					Vector2 vector = Foot - _mod.DebugEye;
					float num = (gusts.DirX * vector.x + gusts.DirY * vector.y) / Mathf.Max(0.01f, vector.magnitude);
					Check(num > -0.2f, $"the wind blows round and in towards it, not away ({num:F2})");
					Check(GrassTufts.WindStrength > 0.3f, "the grass bends in it");
					Check(arcs >= 3 && bits >= 20, $"streaks of wind circle it and it whirls what the wind carries ({arcs} arcs, {bits} bits)");
					scene.Weather.Cover(Foot.x, Foot.y, 0f, out var drops);
					scene.Weather.Cover(_mod.DebugEye.x, _mod.DebugEye.y, 0f, out var drops2);
					Check(drops != Precip.Rain && drops2 != Precip.Rain, $"no rain comes with it ({drops} at its foot, {drops2} here)");
				}
				if (name == "mid")
				{
					Log.Info($"TWISTER cost half way: {sum / (float)Mathf.Max(1, n) - calmMs:F1} ms over the frame without it ({calmMs:F1} ms)");
				}
				if (name == "close")
				{
					Check(shake > 0.05f, $"close to it the ground shakes ({shake:F2})");
				}
			}
			Settings s = _mod.Settings;
			PickAngle(36f, 4f);
			(int, string)[] array2 = new(int, string)[2]
			{
				(2, "golden"),
				(3, "night")
			};
			for (int i = 0; i < array2.Length; i++)
			{
				(int, string) tuple2 = array2[i];
				int item = tuple2.Item1;
				string name = tuple2.Item2;
				s.TimeMode = item;
				_mod.ForceRelight();
				yield return Hold(36f, 10f, 2.5f, 4f);
				_mod.SaveFrame("twister_" + name + ".png");
				Log.Info($"TWISTER {name}: frame {_mod.FrameMs:F1} ms");
			}
			s.TimeMode = 1;
			_mod.ForceRelight();
			bool savedGod = s.GodView;
			s.GodView = true;
			_mod.DebugEyeSet = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			float[] array3 = new float[2] { 30f, 15f };
			for (int i = 0; i < array3.Length; i++)
			{
				float height = array3[i];
				for (float pitch = 0f; pitch < 3f; pitch += Time.unscaledDeltaTime)
				{
					try
					{
						Camera camera = World.world.camera;
						Vector2 vector2 = Foot + new Vector2(0f, (0f - height) * 0.25f);
						camera.transform.position = new Vector3(vector2.x, vector2.y, camera.transform.position.z);
						camera.orthographicSize = height;
						if (World.world.move_camera != null)
						{
							World.world.move_camera.setTargetZoom(height);
						}
						World.world.setZoomOrthographic(height);
					}
					catch (Exception ex2)
					{
						Log.Error("TWISTER camera: " + ex2.Message);
						break;
					}
					yield return null;
				}
				_mod.SaveFrame($"twister_god{height:F0}.png");
				Log.Info($"TWISTER god view at zoom {height:F0}: {scene.TwisterPiecesForTest} pieces, frame {_mod.FrameMs:F1} ms");
			}
			s.GodView = savedGod;
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			yield return Hold(36f, 10f, 1f, 4f);
			try
			{
				_twister.die();
			}
			catch (Exception ex3)
			{
				Log.Error("TWISTER die: " + ex3.Message);
			}
			yield return Hold(36f, 10f, 0.6f, 4f);
			_mod.SaveFrame("twister_rope.png");
			for (float height = 0f; height < 4f; height += Time.unscaledDeltaTime)
			{
				if (!((BaseEffect)_twister).active)
				{
					break;
				}
				if (((BaseEffect)_twister).state == 3)
				{
					break;
				}
				Watch(36f, 10f, 4f);
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			int twisterStormsForTest = scene.TwisterStormsForTest;
			Log.Info($"TWISTER gone: {scene.TwistersForTest} followed, {twisterStormsForTest} storms lingering, cloud base {scene.Weather.Base:F1}");
			Check(scene.TwistersForTest == 0, "a dead tornado is let go");
			Check(twisterStormsForTest >= 1, "its storm lingers a while after it");
			_twister = null;
			float waited;
			for (waited = 0f; waited < 40f; waited += Time.unscaledDeltaTime)
			{
				if (scene.TwisterStormsForTest <= 0)
				{
					break;
				}
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Log.Info($"TWISTER after {waited:F1} s: {scene.TwisterStormsForTest} storms, cloud base {scene.Weather.Base:F1}, wind {scene.Gusts.Fury:F2}");
			Check(scene.TwisterStormsForTest == 0 && Mathf.Abs(scene.Weather.Base - 15f) < 0.01f, "then the storm is gone and the clouds settle back");
			Check(scene.Gusts.Fury < 0.01f, "and its wind has stopped");
		}
	}
}
