using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SliceTest : CivilTest
	{
		private Actor _host;

		private Vector2 _home;

		private readonly List<Actor> _hold = new List<Actor>();

		private readonly List<KeyValuePair<Vector3, float>> _halves = new List<KeyValuePair<Vector3, float>>();

		private readonly List<bool> _bodies = new List<bool>();

		protected override string Prefix => "SLICE";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			Actor king = ((worldTile != null) ? Village("human", 5, 0, worldTile) : Village("human", 5, 0));
			if (!Check("a village", king != null))
			{
				yield break;
			}
			_host = Founded.Find((Actor u) => u != null && u != king && u.isAdult());
			if (!Check("someone to be", _host != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_host);
			if (!Check("first person", InControl(_host)))
			{
				yield break;
			}
			_home = OpenGround(_host.current_position);
			_host.current_position = _home;
			CivilTest.ClearBox((int)_home.x - 6, (int)_home.y - 6, (int)_home.x + 6, (int)_home.y + 6);
			foreach (Actor item in Founded)
			{
				if (item != null && item != _host && item.isAlive())
				{
					item.current_position = _home + new Vector2(14f, 9f);
					try
					{
						item.makeWait(120f);
					}
					catch
					{
					}
				}
			}
			Check("adamantine is the top tier", Slicing.IsTopSword("sword_adamantine"));
			Check("iron is not", !Slicing.IsTopSword("sword_iron") && !Slicing.IsTopSword("sword_wood"));
			Note(string.Format("mythril {0}, flame sword {1}, an axe {2}", Slicing.IsTopSword("sword_mythril"), Slicing.IsTopSword("flame_sword"), Slicing.IsTopSword("axe_adamantine")));
			bool friendlyFire = Mod.Settings.FriendlyFire;
			Mod.Settings.FriendlyFire = true;
			yield return Part("a man cut in two", Cut("human", "slice_man", 0));
			yield return Part("a wolf cut in two", Cut("wolf", "slice_wolf", 1));
			yield return Part("an iron sword doesn't", Lesser());
			yield return Part("a blow that doesn't kill doesn't", Survives());
			Mod.Settings.FriendlyFire = friendlyFire;
		}

		private static Vector2 OpenGround(Vector2 from)
		{
			for (float num = 9f; num < 20f; num += 1.5f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					bool flag = true;
					for (int j = -3; j <= 3 && flag; j++)
					{
						for (int k = -3; k <= 3 && flag; k++)
						{
							WorldTile tile = World.world.GetTile((int)vector.x + k, (int)vector.y + j);
							if (tile == null || tile.main_type == null || tile.main_type.liquid || tile.main_type.block)
							{
								flag = false;
							}
							else if (tile.building != null && AssetStyle.IsSolid(tile.building) && tile.building.asset.building_type != BuildingType.Building_Tree && tile.building.asset.building_type != BuildingType.Building_Mineral && tile.building.asset.building_type != BuildingType.Building_Plant)
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
					}
				}
			}
			return from;
		}

		private bool Equip(string id)
		{
			try
			{
				ActorEquipmentSlot slot = _host.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, _host.kingdom, _host.getName(), 1, _host, 0, pByPlayer: true);
				slot.setItem(pItem, _host);
				_host.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Note("equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private void Hold()
		{
			_host.current_position = _home;
			foreach (Actor item in _hold)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						item.cancelAllBeh();
						item.makeWait(5f);
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator Settle()
		{
			for (int i = 0; i < 6; i++)
			{
				Hold();
				yield return null;
			}
		}

		private void Face(Actor target)
		{
			if (Hitboxes.Get(target, out var box))
			{
				Vector3 eyeForTest = Mod.EyeForTest;
				float num = box.Centre.X - eyeForTest.x;
				float num2 = box.Centre.Y - eyeForTest.y;
				float num3 = Mathf.Max(0.05f, Mathf.Sqrt(num * num + num2 * num2));
				Mod.SetYaw(Mathf.Atan2(num2, num));
				Mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan((box.Centre.Z - eyeForTest.z) / num3) * 57.29578f, -44f, 44f));
			}
		}

		private IEnumerator Victim(string kind, bool weak, Action<Actor> got)
		{
			Mod.DebugEyeSet = false;
			_host.current_position = _home;
			yield return Settle();
			Vector3 eyeForTest = Mod.EyeForTest;
			float viewYaw = Mod.ViewYaw;
			Actor a = CivilTest.Spawn(kind, new Vector2(eyeForTest.x + Mathf.Cos(viewYaw) * 1f, eyeForTest.y + Mathf.Sin(viewYaw) * 1f));
			if (a == null)
			{
				got(null);
				yield break;
			}
			_hold.Add(a);
			yield return CivilTest.Wait(0.4f);
			yield return Settle();
			Face(a);
			yield return Settle();
			if (weak)
			{
				try
				{
					a.data.health = 1;
				}
				catch
				{
				}
			}
			got(a);
		}

		private IEnumerator Strike(Actor target)
		{
			Hold();
			Face(target);
			yield return null;
			PossessionCombat.AttackForTest(_host);
			for (int i = 0; i < 4; i++)
			{
				Hold();
				yield return null;
			}
		}

		private void Watch(Vector2 at, float side)
		{
			Vector2 vector = (at - _home).normalized;
			if (vector.sqrMagnitude < 0.5f)
			{
				vector = Vector2.right;
			}
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x) * side;
			Mod.DebugEye = at + vector2 * 1.5f - vector * 0.3f;
			Mod.DebugEyeSet = true;
			Vector2 vector3 = at + vector * 0.3f - Mod.DebugEye;
			Mod.SetYaw(Mathf.Atan2(vector3.y, vector3.x));
			Mod.SetPitchDegrees(-22f);
		}

		private IEnumerator Cut(string kind, string shot, int which)
		{
			Mod.Holstered = false;
			if (!Check("the adamantine sword in hand", Equip("sword_adamantine")))
			{
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Actor target = null;
			yield return Victim(kind, weak: true, delegate(Actor a)
			{
				target = a;
			});
			if (!Check("a " + kind + " in front of you", target != null))
			{
				yield break;
			}
			int cuts = Slicing.Cuts;
			int blows = PossessionCombat.Blows;
			Vector2 at = target.current_position;
			for (int tries = 0; tries < 4; tries++)
			{
				if (!target.isAlive())
				{
					break;
				}
				if (tries > 0)
				{
					Note("dodged or blocked, again");
					try
					{
						target.data.health = 1;
					}
					catch
					{
					}
					yield return CivilTest.Wait(0.5f);
				}
				yield return Strike(target);
				for (float t = 0f; t < 1f; t += 0.05f)
				{
					if (Slicing.Cuts != cuts)
					{
						break;
					}
					yield return CivilTest.Wait(0.05f);
				}
			}
			bool flag = target == null || !target.isAlive();
			Check("the blow lands and kills the " + kind, PossessionCombat.Blows > blows && flag, string.Format("blows {0}->{1}, last at {2}", blows, PossessionCombat.Blows, (PossessionCombat.LastBlowAt == target) ? "it" : "someone else"));
			if (!Check("the " + kind + " is cut in two", Slicing.Cuts == cuts + 1, $"{Slicing.LastUpper} + {Slicing.LastLower} triangles"))
			{
				yield break;
			}
			Slicing.HalvesForTest(_halves, _bodies);
			int num = 0;
			foreach (bool body in _bodies)
			{
				if (body)
				{
					num++;
				}
			}
			Check("two halves", num >= 2, $"{_halves.Count} pieces");
			Mod.Holstered = true;
			Watch(at, (which == 0) ? 1f : (-1f));
			Vector3 upper0 = Upper();
			float apart0 = Apart();
			float t2 = Time.unscaledTime;
			float[] array = new float[4] { 0.15f, 0.45f, 0.9f, 2.5f };
			foreach (float t in array)
			{
				while (Time.unscaledTime - t2 < t)
				{
					yield return null;
				}
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, $"{shot}_{t:F2}.png"));
				Note($"picture at {Time.unscaledTime - t2:F2} s");
				yield return null;
			}
			yield return CivilTest.Wait(0.2f);
			Slicing.HalvesForTest(_halves, _bodies);
			Vector3 vector = Upper();
			float magnitude = new Vector2(vector.x - at.x, vector.y - at.y).magnitude;
			Note($"upper half: {upper0} -> {vector}, {magnitude:F2} tiles from where it stood");
			float num2 = Apart();
			Check((which == 0) ? "the upper half is thrown off" : "the halves come apart", (which == 0) ? (magnitude > 0.3f) : (num2 > 0.35f), (which == 0) ? magnitude.ToString("F2") : $"{apart0:F2} -> {num2:F2} tiles apart");
			float num3 = 0f;
			for (int num4 = _halves.Count - 1; num4 >= 0; num4--)
			{
				if (_bodies[num4] && _halves[num4].Key != vector)
				{
					num3 = _halves[num4].Value;
					break;
				}
			}
			Check("the lower half has toppled", num3 > 60f, num3.ToString("F0") + " deg");
			for (int num5 = 0; num5 < _halves.Count; num5++)
			{
				Note(string.Format("piece {0}: {1} at {2}, {3:F0} deg from upright", num5, _bodies[num5] ? "half" : "blade", _halves[num5].Key, _halves[num5].Value));
			}
			Mod.DebugEyeSet = false;
			yield return CivilTest.Wait(0.2f);
		}

		private float Apart()
		{
			Slicing.HalvesForTest(_halves, _bodies);
			List<Vector3> list = new List<Vector3>();
			int num = _halves.Count - 1;
			while (num >= 0 && list.Count < 2)
			{
				if (_bodies[num])
				{
					list.Add(_halves[num].Key);
				}
				num--;
			}
			if (list.Count >= 2)
			{
				return new Vector2(list[0].x - list[1].x, list[0].y - list[1].y).magnitude;
			}
			return 0f;
		}

		private Vector3 Upper()
		{
			Slicing.HalvesForTest(_halves, _bodies);
			Vector3 result = Vector3.zero;
			int num = 0;
			int num2 = _halves.Count - 1;
			while (num2 >= 0 && num < 2)
			{
				if (_bodies[num2])
				{
					num++;
					if (num == 2)
					{
						result = _halves[num2].Key;
					}
				}
				num2--;
			}
			return result;
		}

		private IEnumerator Lesser()
		{
			Mod.Holstered = false;
			Check("an iron sword in hand", Equip("sword_iron"));
			yield return CivilTest.Wait(0.3f);
			Actor target = null;
			yield return Victim("human", weak: true, delegate(Actor a)
			{
				target = a;
			});
			if (target == null)
			{
				yield break;
			}
			int cuts = Slicing.Cuts;
			int fallen = Corpses.Count;
			int blows = PossessionCombat.Blows;
			for (int tries = 0; tries < 6; tries++)
			{
				if (!target.isAlive())
				{
					break;
				}
				try
				{
					target.data.health = 1;
				}
				catch
				{
				}
				yield return Strike(target);
				yield return CivilTest.Wait(1.1f);
			}
			Note($"iron: {PossessionCombat.Blows - blows} blows");
			Check("an iron sword kills", !target.isAlive());
			Check("but the body falls whole", Slicing.Cuts == cuts, $"cuts {Slicing.Cuts - cuts}, fallen {fallen}->{Corpses.Count}");
		}

		private IEnumerator Survives()
		{
			Mod.Holstered = false;
			Equip("sword_adamantine");
			yield return CivilTest.Wait(0.3f);
			Actor target = null;
			yield return Victim("bear", weak: false, delegate(Actor a)
			{
				target = a;
			});
			if (target != null)
			{
				try
				{
					target.data.health = 100000;
				}
				catch
				{
				}
				int cuts = Slicing.Cuts;
				yield return Strike(target);
				yield return CivilTest.Wait(0.6f);
				Check("a blow that doesn't kill cuts nothing", target.isAlive() && Slicing.Cuts == cuts, $"alive {target.isAlive()}, cuts {Slicing.Cuts - cuts}");
				CivilTest.Kill(target);
			}
		}
	}
}
