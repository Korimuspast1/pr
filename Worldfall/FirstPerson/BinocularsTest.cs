using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BinocularsTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
			bool screens = text.Contains("shots");
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
			Settings s = _mod.Settings;
			int savedTime = s.TimeMode;
			int savedRes = s.Resolution;
			float savedFov = s.FieldOfView;
			float savedDetail = s.DetailDistance;
			float savedDraw = s.DrawDistance;
			s.TimeMode = 1;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			s.FieldOfView = 75f;
			s.DetailDistance = 80f;
			s.DrawDistance = 270f;
			Actor king = null;
			WorldTile village = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 8, 2);
				if (king != null)
				{
					village = item;
					break;
				}
			}
			if (king == null)
			{
				Log.Error("BINO could not found a village");
				Finish(quit, s, savedTime, savedRes, savedFov, savedDetail, savedDraw);
				yield break;
			}
			yield return new WaitForSecondsRealtime(4f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("BINO first person did not start");
				Finish(quit, s, savedTime, savedRes, savedFov, savedDetail, savedDraw);
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
			Vector2 vector = new Vector2((float)village.x + 0.5f, (float)village.y + 0.5f);
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector2 vector2 = vector + new Vector2(90f, 0f);
			float num = float.MaxValue;
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector2 vector3 = vector + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 90f;
				if (vector3.x < 4f || vector3.y < 4f || vector3.x > (float)(MapBox.width - 5) || vector3.y > (float)(MapBox.height - 5))
				{
					continue;
				}
				WorldTile tile = World.world.GetTile((int)vector3.x, (int)vector3.y);
				if (tile != null && tile.main_type != null && !tile.main_type.liquid)
				{
					float a = terrainForTest.SampleHeight(vector3.x, vector3.y) + 5f;
					float b = terrainForTest.SampleHeight(vector.x, vector.y) + 1f;
					float num2 = -99f;
					for (int j = 1; j < 30; j++)
					{
						float t2 = (float)j / 30f;
						Vector2 vector4 = Vector2.Lerp(vector3, vector, t2);
						num2 = Mathf.Max(num2, terrainForTest.SampleHeight(vector4.x, vector4.y) - Mathf.Lerp(a, b, t2));
					}
					if (num2 < num)
					{
						num = num2;
						vector2 = vector3;
					}
				}
			}
			Vector2 normalized = (vector - vector2).normalized;
			_mod.DebugEye = vector2;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 5f;
			_mod.SetYaw(Mathf.Atan2(normalized.y, normalized.x));
			float y = terrainForTest.SampleHeight(vector2.x, vector2.y) + 5f - terrainForTest.SampleHeight(vector.x, vector.y);
			_mod.SetPitchDegrees((0f - Mathf.Atan2(y, 90f)) * 57.29578f);
			Log.Info($"BINO eye {vector2.x:F0},{vector2.y:F0} looking at the village {vector.x:F0},{vector.y:F0} (90 tiles), the land rising {num:F1} over the sight line at worst");
			Vector2 vector5 = new Vector2(0f - normalized.y, normalized.x);
			string[] array = new string[4] { "orc", "wolf", "sheep", "skeleton" };
			for (int k = 0; k < array.Length; k++)
			{
				WorldTile tile2 = World.world.GetTile((int)(vector.x - normalized.x * 6f + vector5.x * ((float)k - 1.5f) * 3f), (int)(vector.y - normalized.y * 6f + vector5.y * ((float)k - 1.5f) * 3f));
				if (tile2 != null && tile2.main_type != null && !tile2.main_type.liquid)
				{
					try
					{
						World.world.units.spawnNewUnit(array[k], tile2, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
					}
					catch
					{
					}
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Config.paused = true;
			for (int l = 0; l < 20; l++)
			{
				yield return null;
			}
			Binoculars b2 = _mod.Binoculars;
			Check("put away at first", !b2.Active && Models.LodZoom == 1f && _mod.Renderer.DetailZoom == 1f);
			Snapshot("1x", out var specks1, out var _, out var whole1);
			if (screens)
			{
				yield return Screen("1x");
			}
			b2.Press();
			yield return null;
			b2.Release();
			yield return Hands("raising");
			yield return new WaitForSecondsRealtime(0.75f);
			Check("a tap keeps them up", b2.Up && b2.Raise >= 1f);
			Check("4x by default", Mathf.Abs(b2.Magnification - 4f) < 0.01f, $"x{b2.Magnification:F2}");
			float num3 = _mod.Shown.View.Fov * 57.29578f;
			float num4 = Binoculars.Fov(1.3089969f, 4f) * 57.29578f;
			Check("the view narrows", Mathf.Abs(num3 - num4) < 0.5f, $"{num3:F1} deg, want {num4:F1}");
			Check("detail follows the zoom", Mathf.Abs(_mod.Renderer.DetailZoom - 4f) < 0.01f && Mathf.Abs(Models.LodZoom - 4f) < 0.01f);
			Check("things stay whole further", _mod.Renderer.BillboardDistance > 312f || _mod.Renderer.BillboardDistance >= _mod.Renderer.MaxDistance - 0.01f, $"detail reach {_mod.Renderer.BillboardDistance:F0} of {_mod.Renderer.MaxDistance:F0}");
			Check("no hands at the eyes", _mod.Renderer.OverlayMeshes.Count == 0, _mod.Renderer.OverlayMeshes.Count + " overlay meshes");
			Check("blows wait", PossessionHooks.BlockAttack);
			Check("a range under the crosshair", _mod.BinocularsRange > 0f, string.Format("{0:F1} tiles, on {1}", _mod.BinocularsRange, (_mod.Picked != null) ? _mod.Picked.GetType().Name : "nothing"));
			Snapshot("4x", out var specks2, out var _, out var whole2);
			Check("fewer specks at 4x", specks2 < specks1 || specks1 == 0, $"{specks1} -> {specks2}");
			Check("more whole models at 4x", whole2 > whole1, $"{whole1} -> {whole2}");
			if (screens)
			{
				yield return Screen("4x");
			}
			b2.Step(10);
			yield return new WaitForSecondsRealtime(0.6f);
			Check("the wheel steps to 12x", Mathf.Abs(b2.Magnification - 12f) < 0.05f, $"x{b2.Magnification:F2}");
			Snapshot("12x", out var _, out var _, out var _);
			if (screens)
			{
				yield return Screen("12x");
			}
			b2.Step(-10);
			yield return new WaitForSecondsRealtime(0.6f);
			Check("and back to 2x", Mathf.Abs(b2.Magnification - 2f) < 0.05f, $"x{b2.Magnification:F2}");
			b2.Step(2);
			Config.paused = false;
			yield return Rate("4x");
			b2.Step(10);
			yield return new WaitForSecondsRealtime(0.5f);
			yield return Rate("12x");
			b2.Step(2 - Binoculars.Powers.Length);
			b2.Step(2);
			b2.Press();
			yield return null;
			b2.Release();
			yield return Hands("lowering");
			yield return new WaitForSecondsRealtime(0.6f);
			Check("a second tap puts them down", !b2.Up && !b2.Active);
			Check("the plain view again", Mathf.Abs(_mod.Shown.View.Fov * 57.29578f - 75f) < 1.5f && Models.LodZoom == 1f && _mod.Renderer.DetailZoom == 1f, $"{_mod.Shown.View.Fov * 57.29578f:F1} deg");
			yield return Rate("1x");
			Config.paused = true;
			b2.Press();
			yield return new WaitForSecondsRealtime(0.65f);
			Check("held: up", b2.Up && b2.Raise >= 1f);
			b2.Release();
			yield return new WaitForSecondsRealtime(0.5f);
			Check("let go after a hold: down", !b2.Active);
			b2.Press();
			yield return null;
			b2.Release();
			yield return new WaitForSecondsRealtime(0.65f);
			Config.paused = false;
			_mod.DebugEyeSet = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Check("leaving puts them away", !b2.Active && Models.LodZoom == 1f && _mod.Renderer.DetailZoom == 1f);
			Log.Info($"BINO {_pass} passed, {_fail} failed");
			Finish(quit, s, savedTime, savedRes, savedFov, savedDetail, savedDraw);
		}

		private void Snapshot(string name, out int specks, out int boxes, out int whole)
		{
			_mod.SaveFrame("bino_" + name + ".png");
			FrameRenderer renderer = _mod.Renderer;
			specks = renderer.FarProps.Count;
			boxes = (whole = 0);
			foreach (MeshInstance mesh in renderer.Meshes)
			{
				if (mesh != null && mesh.Model != null && mesh.Model.Name != null)
				{
					if (Models.IsDistant(mesh.Model))
					{
						boxes++;
					}
					else if (!mesh.Model.Name.EndsWith("_f", StringComparison.Ordinal))
					{
						whole++;
					}
				}
			}
			string text = Describe(_mod.Picked);
			Log.Info($"BINO {name}: fov {_mod.Shown.View.Fov * 57.29578f:F1} deg, detail x{renderer.DetailZoom:F1}, detail reach {renderer.BillboardDistance:F0}, {specks} far specks, {boxes} creatures as boxes, {whole} models whole, {renderer.Meshes.Count} models, {renderer.MeshTriangles} triangles, land {renderer.TerrainMs:F1} ms, models {renderer.MeshMs:F1} ms, range {_mod.BinocularsRange:F1}, on {text}");
		}

		private static string Describe(BaseSimObject o)
		{
			try
			{
				if (o is Actor a)
				{
					return WorldBoxMod.Describe(a);
				}
				if (o is Building building)
				{
					return (building.asset != null) ? building.asset.id : "a building";
				}
			}
			catch
			{
			}
			return "nothing";
		}

		private IEnumerator Hands(string name)
		{
			bool saved = false;
			for (int i = 0; i < 40; i++)
			{
				yield return null;
				Binoculars binoculars = _mod.Binoculars;
				if (!saved && binoculars.HandsShown && binoculars.Lift > 0.4f)
				{
					_mod.SaveFrame("bino_hands_" + name + ".png");
					Log.Info($"BINO hands {name}: lift {binoculars.Lift:F2}, {_mod.Renderer.OverlayMeshes.Count} overlay meshes");
					Check("hands hold them " + name, _mod.Renderer.OverlayMeshes.Count >= 3);
					saved = true;
				}
			}
		}

		private IEnumerator Screen(string name)
		{
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "bino_" + name + "_screen.png"));
			yield return null;
			yield return null;
		}

		private IEnumerator Rate(string name)
		{
			yield return new WaitForSecondsRealtime(0.7f);
			double frame = 0.0;
			double land = 0.0;
			double models = 0.0;
			int frames = 0;
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 4f)
			{
				yield return null;
				frame += (double)Time.unscaledDeltaTime * 1000.0;
				land += (double)_mod.Renderer.TerrainMs;
				models += (double)_mod.Renderer.MeshMs;
				frames++;
			}
			double num = 1.0 / (double)Math.Max(1, frames);
			Log.Info($"BINO rate {name}: {1000.0 / (frame * num):F1} fps, frame {frame * num:F1} ms, land {land * num:F1} ms, models {models * num:F1} ms, {_mod.Renderer.MeshTriangles} triangles");
		}

		private void Check(string what, bool ok, string detail = null)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("BINO " + (ok ? "PASS" : "FAIL") + " " + what + ((detail != null) ? (": " + detail) : ""));
		}

		private void Finish(bool quit, Settings s, int time, int res, float fov, float detail, float draw)
		{
			Config.paused = false;
			s.TimeMode = time;
			s.Resolution = res;
			s.FieldOfView = fov;
			s.DetailDistance = detail;
			s.DrawDistance = draw;
			if (_mod != null)
			{
				_mod.DebugEyeSet = false;
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("BINO DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
