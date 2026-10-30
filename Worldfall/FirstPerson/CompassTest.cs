using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class CompassTest : CivilTest
	{
		private Actor _me;

		protected override string Prefix => "COMPASS";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 8, 0);
			if (!Check("a village", king != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(2f);
			Actor other = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != king && item.isAdult())
				{
					if (_me == null && !item.isWarrior())
					{
						_me = item;
					}
					else if (other == null)
					{
						other = item;
					}
				}
			}
			if (!Check("villagers", _me != null && other != null))
			{
				yield break;
			}
			foreach (Actor item2 in Founded)
			{
				try
				{
					((BaseSimObject)item2).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(_me);
			if (!Check("first person", InControl(_me)))
			{
				yield break;
			}
			Mod.HintUntil = 0f;
			Config.paused = true;
			yield return CivilTest.Wait(0.5f);
			CompassState c = Mod.Compass;
			Vector2 current_position = _me.current_position;
			Vector2 near = Clamp(current_position + new Vector2(6f, 18f));
			TerrainCache land = Mod.Land;
			Mod.Pings.List.Clear();
			Mod.Pings.Add(PingKind.Go, new Vector3(near.x, near.y, land.SampleHeight(near.x, near.y)), null, null, far: false);
			Vector2 vector = (other.current_position = LevelSpot(current_position, land));
			other.dirty_current_tile = true;
			try
			{
				WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
				if (tile != null)
				{
					other.setCurrentTile(tile);
				}
			}
			catch
			{
			}
			Mod.Converse.Marked = other;
			Mod.Converse.MarkedName = "Marked one";
			Check("put away at first", !c.Active && !c.Shown);
			Mod.SetYaw(MathF.PI / 2f);
			yield return null;
			WorldBoxMod.CompassKeyForTest = true;
			bool dipped = false;
			bool blocked = true;
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 0.8f)
			{
				yield return null;
				if (c.Dip > 0f && c.Raise <= 0f)
				{
					dipped = true;
				}
				blocked &= PossessionHooks.BlockAttack;
			}
			Check("O takes it out", c.Out && c.Raise >= 1f && c.Shown, $"raise {c.Raise:F2}, lid {c.Lid * 57.29578f:F0} deg");
			Check("what was held goes down first", dipped);
			Check("no blows while it comes out", blocked);
			Check("the lid sprang open", c.Lid > 1.6f && c.Lid < 2.25f, $"{c.Lid * 57.29578f:F0} deg");
			Check("in the hands", HasCompass(), Overlay());
			yield return CivilTest.Wait(2.5f);
			CompassTest compassTest = this;
			WorldBoxMod.CompassMark? compassOn = Mod.CompassOn;
			int ok;
			if (compassOn.HasValue)
			{
				WorldBoxMod.CompassMark valueOrDefault = compassOn.GetValueOrDefault();
				if (valueOrDefault.Key.StartsWith("g") && (valueOrDefault.At - near).sqrMagnitude < 1f)
				{
					ok = ((Mod.CompassIndex == 0) ? 1 : 0);
					goto IL_05c6;
				}
			}
			ok = 0;
			goto IL_05c6;
			IL_05c6:
			compassTest.Check("it leads to the nearest", (byte)ok != 0, On() + $", {Mod.CompassCount} places");
			Check("two places to lead to", Mod.CompassCount == 2, Mod.CompassCount.ToString());
			CheckNeedles("facing north", near);
			Mod.SaveFrame("compass_north.png");
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "compass_north_hud.png"));
			yield return CivilTest.Wait(0.3f);
			Check("the readout beside it", Mod.HudForTest.CompassReadoutShown);
			Check("no second mark over the ping's own badge", !Mod.HudForTest.CompassMarkShown);
			Mod.SetYaw(-1.2707963f);
			yield return CivilTest.Wait(2.5f);
			CheckNeedles("turned to the south", near);
			Mod.SaveFrame("compass_south.png");
			Mod.SetYaw(MathF.PI / 2f);
			yield return CivilTest.Wait(1.5f);
			WorldBoxMod.HotbarWheelForTest = 1f;
			yield return CivilTest.Wait(2.5f);
			CompassTest compassTest2 = this;
			int ok2;
			if (Mod.CompassIndex == 1)
			{
				compassOn = Mod.CompassOn;
				if (compassOn.HasValue)
				{
					ok2 = (compassOn.GetValueOrDefault().Key.StartsWith("m") ? 1 : 0);
					goto IL_07b0;
				}
			}
			ok2 = 0;
			goto IL_07b0;
			IL_0964:
			object compassTest3;
			int ok3;
			((CivilTest)compassTest3).Check("and round to the nearest", (byte)ok3 != 0, On());
			int slot = Hotbar.Chosen(_me);
			WorldBoxMod.HotbarWheelForTest = -1f;
			yield return CivilTest.Wait(0.6f);
			Check("the hotbar keeps still meanwhile", Hotbar.Chosen(_me) == slot);
			Check("the wheel back the other way", Mod.CompassIndex == 1, On());
			WorldBoxMod.HotbarWheelForTest = 1f;
			yield return CivilTest.Wait(0.6f);
			Check("and forward again: the nearest", Mod.CompassIndex == 0, On());
			int attacks = Mod.Feedback.Attacks;
			WorldBoxMod.CompassClickForTest = true;
			yield return null;
			yield return CivilTest.Wait(0.05f);
			Mod.SaveFrame("compass_shutting.png");
			yield return CivilTest.Wait(0.6f);
			Check("a click shuts it", !c.Out && !c.Active && !HasCompass(), $"raise {c.Raise:F2}, lid {c.Lid:F2}");
			Check("and is no blow", Mod.Feedback.Attacks == attacks, $"{attacks} -> {Mod.Feedback.Attacks}");
			Check("what was held is back", Mod.Renderer.OverlayMeshes.Count > 0, Overlay());
			Mod.SaveFrame("compass_after.png");
			WorldBoxMod.CompassKeyForTest = true;
			yield return CivilTest.Wait(1.5f);
			Check("O again", c.Reading, $"raise {c.Raise:F2}, lid {c.Lid * 57.29578f:F0} deg");
			Check("it remembers nothing picked: the nearest", Mod.CompassIndex == 0);
			WorldBoxMod.CompassKeyForTest = true;
			yield return CivilTest.Wait(0.6f);
			Check("O puts it away", !c.Active);
			Mod.Pings.List.Clear();
			Mod.Converse.Marked = null;
			WorldBoxMod.CompassKeyForTest = true;
			yield return CivilTest.Wait(1f);
			bool flag = Mod.CompassOn?.Key.StartsWith("h") ?? false;
			Check("nothing to do: home", flag || _me.city == null, On());
			WorldBoxMod.CompassKeyForTest = true;
			yield return CivilTest.Wait(0.6f);
			Config.paused = false;
			yield break;
			IL_07b0:
			compassTest2.Check("the wheel: the next nearest", (byte)ok2 != 0, On());
			CheckNeedles("on the marked one", other.current_position);
			Vector2 vector2 = other.current_position - _me.current_position;
			Mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
			yield return CivilTest.Wait(2.5f);
			CheckNeedles("facing the marked one", other.current_position);
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "compass_marked_hud.png"));
			yield return CivilTest.Wait(0.3f);
			Check("a star over the marked one", Mod.HudForTest.CompassMarkShown);
			Mod.SetYaw(MathF.PI / 2f);
			yield return CivilTest.Wait(0.5f);
			Mod.SaveFrame("compass_marked.png");
			WorldBoxMod.HotbarWheelForTest = 1f;
			yield return CivilTest.Wait(0.6f);
			compassTest3 = this;
			if (Mod.CompassIndex == 0)
			{
				compassOn = Mod.CompassOn;
				if (compassOn.HasValue)
				{
					ok3 = (((compassOn.GetValueOrDefault().At - near).sqrMagnitude < 1f) ? 1 : 0);
					goto IL_0964;
				}
			}
			ok3 = 0;
			goto IL_0964;
		}

		private void CheckNeedles(string when, Vector2 at)
		{
			CompassState compass = Mod.Compass;
			float viewYaw = Mod.ViewYaw;
			Vector2 vector = at - _me.current_position;
			float num = Mathf.DeltaAngle(compass.Card * 57.29578f, (MathF.PI / 2f - viewYaw) * 57.29578f);
			float num2 = Mathf.DeltaAngle(compass.Pointer * 57.29578f, (Mathf.Atan2(vector.y, vector.x) - viewYaw) * 57.29578f);
			Check("the card on north, " + when, Mathf.Abs(num) < 4f, $"{num:F1} deg off");
			Check("the pointer on the place, " + when, Mathf.Abs(num2) < 4f, $"{num2:F1} deg off");
			Check("settled, " + when, Mathf.Abs(compass.CardSpeed) < 0.3f && Mathf.Abs(compass.PointerSpeed) < 0.3f, $"card {compass.CardSpeed:F2} rad/s, pointer {compass.PointerSpeed:F2}");
		}

		private bool HasCompass()
		{
			foreach (MeshInstance overlayMesh in Mod.Renderer.OverlayMeshes)
			{
				if (overlayMesh != null && overlayMesh.Model != null && overlayMesh.Model.Name == "held_compass")
				{
					return true;
				}
			}
			return false;
		}

		private string Overlay()
		{
			List<string> list = new List<string>();
			foreach (MeshInstance overlayMesh in Mod.Renderer.OverlayMeshes)
			{
				if (overlayMesh != null && overlayMesh.Model != null)
				{
					list.Add(overlayMesh.Model.Name);
				}
			}
			return string.Join(", ", list);
		}

		private string On()
		{
			WorldBoxMod.CompassMark? compassOn = Mod.CompassOn;
			if (compassOn.HasValue)
			{
				WorldBoxMod.CompassMark valueOrDefault = compassOn.GetValueOrDefault();
				return $"{valueOrDefault.Key} \"{valueOrDefault.Name}\" {valueOrDefault.Distance:F0} tiles";
			}
			return "nothing";
		}

		private static Vector2 LevelSpot(Vector2 me, TerrainCache land)
		{
			float num = land.SampleHeight(me.x, me.y);
			Vector2 result = Clamp(me + new Vector2(-24f, 8f));
			float num2 = float.MaxValue;
			for (int i = 0; i < 24; i++)
			{
				float f = Mathf.Lerp(110f, 230f, (float)i / 23f) * (MathF.PI / 180f);
				Vector2 vector = Clamp(me + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 25f);
				WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
				if (tile != null && tile.main_type != null && !tile.main_type.liquid)
				{
					float num3 = 0f;
					for (int j = 1; j <= 10; j++)
					{
						Vector2 vector2 = Vector2.Lerp(me, vector, (float)j / 10f);
						num3 = Mathf.Max(num3, land.SampleHeight(vector2.x, vector2.y) - num);
					}
					if (num3 < num2)
					{
						num2 = num3;
						result = vector;
					}
				}
			}
			return result;
		}

		private static Vector2 Clamp(Vector2 v)
		{
			return new Vector2(Mathf.Clamp(v.x, 4f, (float)MapBox.width - 5f), Mathf.Clamp(v.y, 4f, (float)MapBox.height - 5f));
		}
	}
}
