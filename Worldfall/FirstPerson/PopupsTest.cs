using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PopupsTest : CivilTest
	{
		private Actor _host;

		private Vector2 _home;

		private float _yaw;

		private WorldTile _spot;

		protected override string Prefix => "POPS";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 7, worldTile.y - 7, worldTile.x + 7, worldTile.y + 7);
			_host = CivilTest.SpawnNear("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (Check("a villager", _host != null))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(_host);
				if (Check("first person in them", InControl(_host)))
				{
					_home = _host.current_position;
					_spot = World.world.GetTile((int)_home.x, (int)_home.y + 2);
					_yaw = Mathf.Atan2(_spot.posV3.y - _home.y, _spot.posV3.x - _home.x);
					Mod.Holstered = true;
					yield return Part("a tree's wood flying home", TreeWood());
					yield return Part("three kinds at once", Several());
					yield return Part("a haul, as it plays", Live());
					yield return Part("the numbers over what you hit", Numbers());
					Mod.Holstered = false;
					Mod.Work.ForceSwing = false;
				}
			}
		}

		private IEnumerator TreeWood()
		{
			Building tree = null;
			Vector2Int[] array = new Vector2Int[4]
			{
				new Vector2Int(0, 2),
				new Vector2Int(2, 0),
				new Vector2Int(0, -2),
				new Vector2Int(-2, 0)
			};
			Vector2Int[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Vector2Int vector2Int = array2[i];
				WorldTile t = World.world.GetTile((int)_home.x + vector2Int.x, (int)_home.y + vector2Int.y);
				if (t == null)
				{
					continue;
				}
				_spot = t;
				_yaw = Mathf.Atan2(t.posV3.y - _home.y, t.posV3.x - _home.x);
				yield return Hold(0.2f);
				tree = Put(TreeId(), t);
				if (tree != null)
				{
					yield return Aim(tree);
					if (Mod.Picked == tree)
					{
						break;
					}
					Note($"a tree at {t.x},{t.y} not in sight (crosshair on {Picked()}): another way");
					Remove(tree);
					tree = null;
				}
			}
			if (!Check("a tree two tiles off, the crosshair on it", tree != null && Mod.Picked == tree, Picked()))
			{
				yield break;
			}
			int bag = Mod.Work.BagCount;
			Check("an empty bag to start", bag == 0, $"{bag}");
			yield return Chop(tree);
			List<BagGain> gained = Mod.Work.Gained;
			if (Check("felling it sends what it gave flying to the bag (the first into an empty one too)", gained.Count > 0 && Mod.Work.BagCount > bag, $"{gained.Count} gains, bag {bag} -> {Mod.Work.BagCount}"))
			{
				BagGain g = gained[0];
				Vector2 vector = new Vector2(_spot.posV3.x, _spot.posV3.y);
				Check("...wood, out of the tree", g.Id == "wood" && (new Vector2(g.From.x, g.From.y) - vector).magnitude < 0.8f, $"{g.Id} x{g.Amount} from {g.From.x:F1},{g.From.y:F1} z {g.From.z:F2}, tree at {vector.x:F1},{vector.y:F1}");
				Hud hud = Mod.HudForTest;
				Check("...the bag's count waits for it to land", hud.BagShownForTest < Mod.Work.BagCount, $"shows {hud.BagShownForTest} of {Mod.Work.BagCount}");
				float[] ages = new float[22]
				{
					0.02f, 0.06f, 0.1f, 0.16f, 0.24f, 0.36f, 0.45f, 0.5f, 0.56f, 0.64f,
					0.72f, 0.8f, 0.88f, 0.94f, 0.98f, 1f, 1.04f, 1.1f, 1.2f, 1.35f,
					1.6f, 1.85f
				};
				for (int i = 0; i < ages.Length; i++)
				{
					yield return PoseGains(ages[i], $"pops_tree_{i:00}_{Mathf.RoundToInt(ages[i] * 100f):000}.png");
				}
				Pose(5f);
				yield return Hold(0.2f);
				Check("...landed, the count is the bag's again", hud.BagShownForTest == Mod.Work.BagCount && g.Landed, $"shows {hud.BagShownForTest} of {Mod.Work.BagCount}, landed {g.Landed}");
				Remove(tree);
			}
		}

		private IEnumerator Several()
		{
			yield return Hold(0.3f);
			Mod.Work.Gained.Clear();
			int before = Mod.Work.Gained.Count;
			try
			{
				_host.addToInventory("wood", 2);
				_host.addToInventory("stone", 3);
				_host.addToInventory("gold", 1);
			}
			catch (Exception ex)
			{
				Note("give: " + ex.Message);
			}
			yield return Hold(0.05f);
			List<BagGain> list = new List<BagGain>();
			for (int i = before; i < Mod.Work.Gained.Count; i++)
			{
				list.Add(Mod.Work.Gained[i]);
			}
			bool flag = list.Count == 3;
			for (int j = 0; j < list.Count; j++)
			{
				flag &= list[j].Order == j && list[j].Of == list.Count;
			}
			Check("three kinds handed over: three gains, one after another", flag, string.Join(", ", list.ConvertAll((BagGain x) => $"{x.Id} x{x.Amount} #{x.Order}/{x.Of}").ToArray()));
			float[] ages = new float[11]
			{
				0.05f, 0.15f, 0.3f, 0.5f, 0.66f, 0.82f, 0.94f, 1.04f, 1.12f, 1.25f,
				1.5f
			};
			for (int i2 = 0; i2 < ages.Length; i2++)
			{
				yield return PoseGains(ages[i2], $"pops_three_{i2:00}_{Mathf.RoundToInt(ages[i2] * 100f):000}.png");
			}
			Pose(5f);
			yield return Hold(0.2f);
		}

		private IEnumerator Live()
		{
			yield return Hold(0.3f);
			Mod.SetPitchDegrees(-12f);
			yield return Hold(0.2f);
			float num = 1.8f;
			Vector3 from = new Vector3(_home.x + Mathf.Cos(_yaw) * num, _home.y + Mathf.Sin(_yaw) * num, Mod.View.Z + Mathf.Tan(-MathF.PI / 15f) * num);
			int had = Mod.Work.Gained.Count;
			Mod.Work.YieldFromForTest(from);
			try
			{
				_host.addToInventory("wood", 4);
				_host.addToInventory("stone", 2);
			}
			catch (Exception ex)
			{
				Note("give: " + ex.Message);
			}
			yield return null;
			if (!Check("a haul out of a spot ahead, on its way", Mod.Work.Gained.Count == had + 2 && Mod.Work.Gained[had].From == from, $"{Mod.Work.Gained.Count - had} gains"))
			{
				yield break;
			}
			BagGain g = Mod.Work.Gained[Mod.Work.Gained.Count - 1];
			Hud hud = Mod.HudForTest;
			int frame = 0;
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 2.2f && frame < 40)
			{
				Stay();
				float num2 = Time.unscaledTime - g.Start;
				hud.CaptureHudForTest(Path.Combine(Log.Folder, $"pops_live_{frame:00}_{Mathf.RoundToInt(num2 * 100f):000}.png"));
				while (hud.CapturePending)
				{
					yield return null;
				}
				yield return null;
				frame++;
			}
			Check("...it landed as it played", g.Landed && hud.BagShownForTest == Mod.Work.BagCount, $"{frame} frames, shows {hud.BagShownForTest} of {Mod.Work.BagCount}");
		}

		private IEnumerator PoseGains(float age, string name)
		{
			Stay();
			Pose(age);
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			while (Mod.HudForTest.CapturePending)
			{
				yield return null;
				Stay();
				Pose(age);
			}
			yield return null;
		}

		private void Pose(float age)
		{
			foreach (BagGain item in Mod.Work.Gained)
			{
				item.Born = Time.unscaledTime - age;
			}
		}

		private IEnumerator Numbers()
		{
			Mod.Work.Gained.Clear();
			CivilTest.ClearBox((int)_home.x - 5, (int)_home.y - 5, (int)_home.x + 5, (int)_home.y + 5);
			Mod.SetPitchDegrees(-6f);
			Actor sheep = CivilTest.SpawnNear("sheep", _home + new Vector2(Mathf.Cos(_yaw), Mathf.Sin(_yaw)) * 2.4f);
			if (!Check("a sheep to hit", sheep != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)sheep).addStatusEffect("invincible", 200f, pColorEffect: false);
			}
			catch
			{
			}
			Vector2 at = _home + new Vector2(Mathf.Cos(_yaw), Mathf.Sin(_yaw)) * 2.4f;
			int max = 50;
			try
			{
				max = sheep.getMaxHealth();
			}
			catch
			{
			}
			Note($"sheep max health {max}");
			float ground = 0f;
			try
			{
				ground = Mod.TerrainForTest.SampleHeight(at.x, at.y);
			}
			catch
			{
			}
			Vector3 world = new Vector3(at.x, at.y, ground + 1.1f);
			sheep.current_position = at;
			yield return Hold(0.3f);
			Vector3 head;
			bool flag = Mod.StarHead(sheep, out head);
			Note(string.Format("sheep head {0}; eye {1:F2} over it", flag ? $"{head.z - ground:F2} over the ground" : "not found", Mod.View.Z - ground));
			float[] ages = new float[13]
			{
				0f, 0.03f, 0.065f, 0.1f, 0.13f, 0.18f, 0.26f, 0.4f, 0.6f, 0.75f,
				0.85f, 0.95f, 1.05f
			};
			yield return Film(sheep, at, world, "-" + Mathf.Max(1, max / 12), Mathf.Max(1, max / 12), ages, "hit");
			yield return Film(sheep, at, world, "-" + Mathf.Max(1, max / 2), Mathf.Max(1, max / 2), new float[4] { 0.03f, 0.13f, 0.3f, 0.7f }, "heavy");
			yield return Film(null, at, world, "KILL", 0, new float[6] { 0f, 0.05f, 0.1f, 0.2f, 0.45f, 0.9f }, "kill", new Color(1f, 0.25f, 0.2f));
			FloatingText t = new FloatingText
			{
				World = world,
				Text = "-3",
				Color = new Color(1f, 0.9f, 0.3f),
				Victim = sheep,
				Amount = 3,
				Life = 4f
			};
			Mod.Feedback.Texts.Add(t);
			yield return Hold(0.12f);
			t.Amount += 9;
			t.Text = "-" + t.Amount;
			t.Age = Mathf.Min(t.Age, 0.08f);
			Check("pellets of one shot add up on one number", Mod.Feedback.Texts.Contains(t) && t.Text == "-12");
			for (int i = 0; i < 4; i++)
			{
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, $"pops_pellets_{i:00}.png"));
				while (Mod.HudForTest.CapturePending)
				{
					yield return null;
				}
				yield return null;
			}
			Mod.Feedback.Texts.Clear();
			CivilTest.Kill(sheep);
		}

		private IEnumerator Film(Actor victim, Vector2 at, Vector3 world, string text, int amount, float[] ages, string name, Color? color = null)
		{
			Mod.Feedback.Texts.Clear();
			FloatingText t = new FloatingText
			{
				World = world,
				Text = text,
				Color = (color ?? new Color(1f, 0.9f, 0.3f)),
				Victim = victim,
				Amount = amount
			};
			Hud hud = Mod.HudForTest;
			for (int i = 0; i < ages.Length; i++)
			{
				hud.CaptureHudForTest(Path.Combine(Log.Folder, $"pops_{name}_{i:00}_{Mathf.RoundToInt(ages[i] * 100f):000}.png"));
				do
				{
					Stay();
					if (victim != null)
					{
						victim.current_position = at;
						victim.dirty_current_tile = true;
					}
					if (!Mod.Feedback.Texts.Contains(t))
					{
						Mod.Feedback.Texts.Add(t);
					}
					hud.PoseNumberForTest(t, ages[i]);
					yield return null;
				}
				while (hud.CapturePending);
			}
			Mod.Feedback.Texts.Clear();
			yield return null;
		}

		private IEnumerator Chop(Building b)
		{
			int had = Mod.Work.Gained.Count;
			Mod.Work.ForceSwing = true;
			float said = 0f;
			for (float t = 0f; t < 10f; t += Time.unscaledDeltaTime)
			{
				if (Mod.Work.Gained.Count != had)
				{
					break;
				}
				Stay();
				if (t >= said)
				{
					said += 1f;
					Note(string.Format("  chopping {0:F1} s: target {1}, progress {2:F2}, ", t, (Mod.Work.Target != null) ? Mod.Work.Target.asset.id : "none", Mod.Work.Progress) + $"picked {Picked()}, prompt '{Mod.Prompt}', needs '{Mod.Work.Needs}', bag {Mod.Work.BagCount}, alive {b.isAlive()} chopped {b.chopped}");
				}
				yield return null;
			}
			Mod.Work.ForceSwing = false;
		}

		private IEnumerator Aim(Building b)
		{
			for (float pitch = -2f; pitch > -45f; pitch -= 3f)
			{
				Mod.SetPitchDegrees(pitch);
				yield return Hold(0.12f);
				if (Mod.Picked == b)
				{
					yield return Hold(0.12f);
					break;
				}
			}
		}

		private IEnumerator Hold(float seconds)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Stay();
				yield return null;
			}
		}

		private void Stay()
		{
			if (_host != null && _host.isAlive())
			{
				_host.current_position = _home;
				_host.dirty_current_tile = true;
				Mod.SetYaw(_yaw);
			}
		}

		private string Picked()
		{
			object obj;
			if (!(Mod.Picked is Building building))
			{
				obj = Mod.Picked?.ToString();
				if (obj == null)
				{
					return "nothing";
				}
			}
			else
			{
				obj = building.asset.id;
			}
			return (string)obj;
		}

		private static string TreeId()
		{
			if (AssetManager.buildings.get("oak_tree") != null)
			{
				return "oak_tree";
			}
			foreach (BuildingAsset item in AssetManager.buildings.list)
			{
				if (item.building_type == BuildingType.Building_Tree && item.resources_given != null && item.resources_given.Count > 0)
				{
					return item.id;
				}
			}
			return null;
		}

		private Building Put(string id, WorldTile t)
		{
			if (id == null || t == null)
			{
				return null;
			}
			try
			{
				if (t.building != null)
				{
					t.building.removeBuildingFinal();
				}
				return World.world.buildings.addBuilding(id, t, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			}
			catch (Exception ex)
			{
				Log.Error(Prefix + " put " + id + ": " + ex.Message);
				return null;
			}
		}

		private static void Remove(Building b)
		{
			try
			{
				if (b != null && b.isAlive())
				{
					b.removeBuildingFinal();
				}
			}
			catch
			{
			}
		}
	}
}
