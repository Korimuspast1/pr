using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RtsOrdersTest : CivilTest
	{
		private readonly Queue<Event> _events = new Queue<Event>();

		private Actor _king;

		private Kingdom _home;

		private Kingdom _foeK;

		private City _foeCity;

		private List<Actor> _bandA;

		private List<Actor> _bandB;

		private List<Actor> _orcs;

		protected override string Prefix => "RTS";

		protected override IEnumerator Body()
		{
			_king = Village("human", 21, 0);
			if (!Check("our village", _king != null))
			{
				yield break;
			}
			foreach (Actor item in new List<Actor>(Founded))
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			WorldTile current_tile = _king.current_tile;
			Actor actor = Village("orc", 12, 4, current_tile, 34f) ?? Village("orc", 12, 4);
			if (!Check("an orc village", actor != null))
			{
				yield break;
			}
			_orcs = new List<Actor>(Founded);
			_foeK = actor.kingdom;
			try
			{
				_foeCity = actor.city;
			}
			catch
			{
			}
			yield return Possess(_king);
			if (!Check("the king in first person", InControl(_king) && Royal.IsKing(_king)))
			{
				yield break;
			}
			_home = _king.kingdom;
			Mod.Royal.DeclareWar(_king, _foeK, Mod.Toast, delegate
			{
			});
			Check("at war with the orcs", Royal.AtWar(_home, _foeK));
			yield return CivilTest.Wait(2f);
			List<Actor> list = new List<Actor>();
			try
			{
				foreach (Actor unit in _home.getUnits())
				{
					if (unit != null && unit != _king && unit.isAlive() && unit.isWarrior())
					{
						list.Add(unit);
					}
				}
			}
			catch
			{
			}
			if (!Check("warriors to command", list.Count >= 6, list.Count + " warriors"))
			{
				yield break;
			}
			_bandA = list.GetRange(0, list.Count / 2);
			_bandB = list.GetRange(list.Count / 2, list.Count - list.Count / 2);
			try
			{
				if (!Court.Open(_home, Seat.Marshal))
				{
					Court.Appoint(_home, Seat.Marshal, list[0]);
				}
			}
			catch (Exception ex)
			{
				Note("marshal: " + ex.Message);
			}
			yield return Part("windows", Windows());
			yield return Part("flag pins", Pins());
			yield return Part("groups and keys", Groups());
			yield return Part("flags and right clicks", Orders());
			yield return Part("the town card", TownCard());
			yield return Part("kill, pillage, destroy", Kinds());
			yield return Part("the scene", Scene());
			Mod.WarMap.ClearFlags();
			Mod.CloseMapWindow();
		}

		private IEnumerator Windows()
		{
			WarMap map = Mod.WarMap;
			Mod.CloseMapWindow();
			Mod.Royal.Toggle(_king, Mod.Toast);
			yield return CivilTest.Wait(0.6f);
			if (Check("K opens the council", Mod.Royal.Open))
			{
				yield return Press(Mod.HudForTest.WarMapLinkRectForTest.center);
				yield return CivilTest.Wait(0.4f);
				Check("the council's War map button swaps to the war map", map.Open && !Mod.Royal.Open, $"map {map.Open}, council {Mod.Royal.Open}");
				Check("no talk from E while a window is up", Mod.Talk.Blocked);
				yield return CivilTest.Wait(0.6f);
				yield return Press(Mod.HudForTest.CouncilLinkRectForTest.center);
				yield return CivilTest.Wait(0.4f);
				Check("the war map's Council button swaps back", Mod.Royal.Open && !map.Open, $"map {map.Open}, council {Mod.Royal.Open}");
				Mod.Royal.Open = false;
			}
		}

		private IEnumerator Pins()
		{
			WarMap map = Mod.WarMap;
			map.ClearFlags();
			Vector2 me = _king.current_position;
			WorldTile near = null;
			WorldTile far = null;
			float num = 0f;
			for (int i = 0; i < 16; i++)
			{
				if (near != null && far != null)
				{
					break;
				}
				num = (float)i * MathF.PI / 8f;
				near = Spot(_king.current_tile, num, 12f);
				far = Spot(_king.current_tile, num, 75f);
			}
			if (Check("ground near and far the same way", near != null && far != null))
			{
				map.Plant(near, _home);
				map.Plant(far, _home);
				Mod.SetYaw(num);
				Mod.SetPitchDegrees(-6f);
				yield return CivilTest.Wait(1.5f);
				Hud hud = Mod.HudForTest;
				Check("the near flag stands in first person, the far one doesn't", hud.PinsDrawnForTest == 1, $"{hud.PinsDrawnForTest} drawn, near {(P(near) - me).magnitude:F0} tiles, far {(P(far) - me).magnitude:F0} tiles");
				yield return Capture("rts_pins.png");
				Mod.Royal.Toggle(_king, Mod.Toast);
				yield return CivilTest.Wait(0.5f);
				int frame = hud.PinsFrameForTest;
				yield return CivilTest.Wait(0.6f);
				Check("no flags drawn over the council", hud.PinsFrameForTest == frame && Mod.Royal.Open, $"last drawn at frame {hud.PinsFrameForTest}, {frame} before, now {Time.frameCount}");
				Mod.Royal.Open = false;
				map.ClearFlags();
			}
		}

		private IEnumerator Groups()
		{
			WarMap map = Mod.WarMap;
			map.ClearFlags();
			map.NextOrder = WarOrder.Attack;
			Mod.ShowMapPage(MapTabs.Page.War);
			yield return CivilTest.Wait(1f);
			if (Check("the war map opens", map.Open))
			{
				yield return View(map, _king.current_position, 70f);
				Mark(map, _bandA);
				yield return Key(KeyCode.Alpha1, EventModifiers.Control);
				Mark(map, _bandB);
				yield return Key(KeyCode.Alpha2, EventModifiers.Control);
				Check("Ctrl+1 and Ctrl+2 save the groups", map.GroupMembers(1).Count == _bandA.Count && map.GroupMembers(2).Count == _bandB.Count, $"group 1 {map.GroupMembers(1).Count} of {_bandA.Count}, group 2 {map.GroupMembers(2).Count} of {_bandB.Count}");
				map.Marked.Clear();
				map.Centre = _king.current_position + new Vector2(40f, 40f);
				yield return Key(KeyCode.Alpha1);
				Check("1 selects group 1", map.Marked.SetEquals(_bandA), map.Marked.Count + " selected");
				Check("...and the map goes to them", (map.Centre - Middle(_bandA)).magnitude < 3f, $"centre {map.Centre}, group {Middle(_bandA)}");
				yield return Key(KeyCode.Alpha2);
				Check("2 selects group 2", map.Marked.SetEquals(_bandB), map.Marked.Count + " selected");
				yield return Key(KeyCode.E);
				Check("E chooses Wait for the next flag", map.NextOrder == WarOrder.Wait && map.CurrentOrder == WarOrder.Wait);
				yield return Key(KeyCode.Q);
				Check("Q chooses Attack", map.NextOrder == WarOrder.Attack);
				yield return Capture("rts_bar.png");
			}
		}

		private IEnumerator Orders()
		{
			WarMap map = Mod.WarMap;
			WorldTile east = null;
			WorldTile west = null;
			for (int i = 0; i < 16; i++)
			{
				if (west != null)
				{
					break;
				}
				float num = (float)i * MathF.PI / 8f;
				WorldTile worldTile = Spot(_king.current_tile, num, 11f);
				if (worldTile == null)
				{
					continue;
				}
				for (int j = 4; j <= 12; j++)
				{
					if (west != null)
					{
						break;
					}
					WorldTile worldTile2 = Spot(_king.current_tile, num + (float)j * MathF.PI / 8f, 11f);
					if (worldTile2 != null && (P(worldTile2) - P(worldTile)).magnitude > 12f)
					{
						east = worldTile;
						west = worldTile2;
					}
				}
			}
			if (!Check("open ground on two sides", east != null && west != null))
			{
				yield break;
			}
			yield return Key(KeyCode.Alpha1);
			yield return View(map, (P(east) + P(west)) * 0.5f, 60f);
			Vector2 vector = Screen(map, P(east));
			Vector2 to = Screen(map, P(east) + new Vector2(10f, 0f));
			yield return Drag(1, vector, to, shift: true);
			WarFlag f1 = map.FlagNumbered(1);
			Check("Shift+right drag plants group 1's flag", f1 != null && f1.Band.Count == _bandA.Count && map.Flags.Count == 1, (f1 != null) ? $"flag 1 band {f1.Band.Count}, area {f1.Radius:F1}" : ("no flag; " + Mod.ToastText));
			if (f1 == null)
			{
				yield break;
			}
			Check("...its area as dragged", Mathf.Abs(f1.Radius - 10f) < 2f, $"{f1.Radius:F1} tiles");
			Check("...with the bar's order", f1.Order == WarOrder.Attack, f1.Order.ToString());
			Check("...and picked", map.SelectedFlag == f1);
			yield return Key(KeyCode.E);
			Check("E with the flag picked turns it to Wait", f1.Order == WarOrder.Wait, f1.Order.ToString());
			yield return Capture("rts_flag.png", 0.8f);
			yield return Key(KeyCode.Alpha2);
			yield return View(map, (P(east) + P(west)) * 0.5f, 60f);
			yield return Click(1, Screen(map, P(west)));
			WarFlag post = ((map.Posts.Count > 0) ? map.Posts[map.Posts.Count - 1] : null);
			Check("a right click gives an order with no flag", post != null && map.Flags.Count == 1 && post.Band.Count == _bandB.Count, $"posts {map.Posts.Count}, flags {map.Flags.Count}, band {post?.Band.Count ?? 0}; {Mod.ToastText}");
			Check("...the selection stays", map.Marked.SetEquals(_bandB), map.Marked.Count + " selected");
			if (post == null)
			{
				yield break;
			}
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 40f && Share(_bandB, post.P, post.Radius + 3f) < 0.7f)
			{
				yield return CivilTest.Wait(1f);
			}
			Check("group 2 goes there and holds", Share(_bandB, post.P, post.Radius + 3f) >= 0.7f, $"{Share(_bandB, post.P, post.Radius + 3f) * 100f:F0}% within {post.Radius + 3f:F0} tiles after {Time.realtimeSinceStartup - t0:F0} s");
			yield return CivilTest.Wait(8f);
			Check("...and is still there", Share(_bandB, post.P, post.Radius + 3f) >= 0.7f && map.Posts.Contains(post), $"{Share(_bandB, post.P, post.Radius + 3f) * 100f:F0}%");
			Actor foe = _orcs.Find((Actor o) => o != null && o.isAlive() && o.isWarrior());
			if (!Check("an orc warrior", foe != null))
			{
				yield break;
			}
			WorldTile worldTile3 = Spot(World.world.GetTile((int)Middle(_bandB).x, (int)Middle(_bandB).y), 0f, 16f) ?? Spot(World.world.GetTile((int)Middle(_bandB).x, (int)Middle(_bandB).y), MathF.PI, 16f);
			if (worldTile3 != null)
			{
				try
				{
					foe.setCurrentTilePosition(worldTile3);
				}
				catch
				{
				}
			}
			yield return CivilTest.Wait(0.5f);
			yield return View(map, foe.current_position, 50f);
			yield return Click(1, Screen(map, foe.current_position));
			WarFlag warFlag = map.CommandOf(_bandB[0]);
			Actor actor = ((warFlag != null) ? (warFlag.Target as Actor) : null);
			Check("a right click on a foe sends them after him", actor != null && actor.kingdom == _foeK, (actor != null) ? WorldBoxMod.Describe(actor) : ("no target; " + Mod.ToastText));
			if (actor == null)
			{
				yield break;
			}
			foe = actor;
			t0 = Time.realtimeSinceStartup;
			bool flag = false;
			while (Time.realtimeSinceStartup - t0 < 50f && !flag)
			{
				yield return CivilTest.Wait(1f);
				try
				{
					flag = !foe.isAlive() || _bandB.Exists((Actor a) => a.isAlive() && a.has_attack_target && a.attack_target == foe);
				}
				catch
				{
					flag = true;
				}
			}
			Check("...and close with him", flag, string.Format("after {0:F0} s, {1}, nearest {2:F1} tiles", Time.realtimeSinceStartup - t0, foe.isAlive() ? "alive" : "dead", Nearest(_bandB, foe.current_position)));
		}

		private IEnumerator TownCard()
		{
			WarMap map = Mod.WarMap;
			if (Check("the orc town", _foeCity != null && _foeCity.isAlive()))
			{
				Vector2 town = map.TownPoint(_foeCity);
				yield return View(map, town, 50f);
				yield return Click(0, Screen(map, town) + new Vector2(0f, -6f) * GameArt.P);
				Check("a click on the orc town brings up its card", Mod.HudForTest.CardCityForTest == _foeCity, (Mod.HudForTest.CardCityForTest != null) ? Mod.HudForTest.CardCityForTest.name : "no card");
				yield return CivilTest.Wait(0.5f);
				yield return Capture("rts_card.png");
				Rect rect = Mod.HudForTest.TownCardSharesForTest[1];
				if (Check("its Send army buttons", rect.width > 0f))
				{
					yield return Press(rect.center);
					Royal.Campaign active = Mod.Royal.Active;
					Check("Send army aims the campaign at that town", active != null && active.Objective == _foeCity, (active != null) ? ("on " + ((active.Objective != null) ? active.Objective.name : "?")) : ("no campaign; " + Mod.ToastText));
					Mod.Royal.Recall(null);
				}
			}
		}

		private IEnumerator Kinds()
		{
			WarMap warMap = Mod.WarMap;
			WarFlag f1 = warMap.FlagNumbered(1);
			if (!Check("flag 1 still up", f1 != null && _foeCity != null))
			{
				yield break;
			}
			Vector2 town = warMap.TownPoint(_foeCity);
			WorldTile worldTile = WarMap.GroundFor(World.world.GetTile((int)town.x, (int)town.y));
			WorldTile worldTile2 = Spot(worldTile, 0f, 14f) ?? Spot(worldTile, MathF.PI / 2f, 14f) ?? Spot(worldTile, MathF.PI, 14f) ?? Spot(worldTile, -MathF.PI / 2f, 14f);
			if (worldTile2 != null)
			{
				foreach (Actor item in f1.Band)
				{
					try
					{
						item.setCurrentTilePosition(worldTile2);
					}
					catch
					{
					}
				}
			}
			warMap.Move(f1, worldTile);
			f1.Radius = 14f;
			warMap.SelectedFlag = f1;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 70f && Share(_bandA, town, 18f) < 0.5f)
			{
				yield return CivilTest.Wait(1f);
			}
			Note($"band A at the orc town after {Time.realtimeSinceStartup - t0:F0} s: {Share(_bandA, town, 18f) * 100f:F0}% within 18 tiles");
			yield return Key(KeyCode.T);
			Check("T turns flag 1 to Kill", f1.Order == WarOrder.Kill);
			int fights = 0;
			int onVillagers = 0;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 30f)
			{
				yield return CivilTest.Wait(0.5f);
				foreach (Actor item2 in f1.Band)
				{
					try
					{
						if (item2.isAlive() && item2.has_attack_target && item2.attack_target is Actor actor && actor.kingdom == _foeK)
						{
							fights++;
							if (!actor.isWarrior() && !actor.isKing() && (!actor.has_attack_target || actor.attack_target != item2))
							{
								onVillagers++;
							}
						}
					}
					catch
					{
					}
				}
			}
			Note($"Kill: {fights} fight samples, {onVillagers} on villagers, band {f1.Band.Count}, {f1.Fighting} fighting");
			Check("Kill leaves the villagers be", fights == 0 || onVillagers <= fights / 5, $"{onVillagers} of {fights} samples on villagers");
			int num = 0;
			for (int i = 0; i < 5; i++)
			{
				if (_foeCity == null)
				{
					break;
				}
				if (!_foeCity.isAlive())
				{
					break;
				}
				if (_foeCity.kingdom != _foeK)
				{
					break;
				}
				Actor actor2 = CivilTest.SpawnNear("orc", town, 2f);
				if (actor2 == null)
				{
					continue;
				}
				try
				{
					if (actor2.city != _foeCity)
					{
						actor2.joinCity(_foeCity);
					}
					_foeCity.makeWarrior(actor2);
					num++;
				}
				catch
				{
				}
			}
			Note($"{num} fresh defenders for the orc town");
			int num2 = 0;
			for (int j = 0; j < 2; j++)
			{
				if (_foeCity == null)
				{
					break;
				}
				if (!_foeCity.isAlive())
				{
					break;
				}
				if (_foeCity.kingdom != _foeK)
				{
					break;
				}
				if (AddHouse("house_orc_0", _foeCity) != null)
				{
					num2++;
				}
			}
			Note($"{num2} houses built in the orc town");
			yield return Key(KeyCode.Y);
			Check("Y turns flag 1 to Pillage", f1.Order == WarOrder.Pillage);
			int num3 = 0;
			try
			{
				num3 = _foeCity.countUnits();
			}
			catch
			{
			}
			Note($"Pillage: {num3} live in the town, band {f1.Band.Count}");
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 45f && f1.Burned == 0)
			{
				yield return CivilTest.Wait(1f);
			}
			Check("Pillage sets houses alight", f1.Burned > 0, $"{f1.Burned} set alight after {Time.realtimeSinceStartup - t0:F0} s");
			yield return Capture("rts_pillage.png");
			yield return Key(KeyCode.R);
			Check("R turns flag 1 to Destroy", f1.Order == WarOrder.Destroy);
			bool hitting = false;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 30f && !hitting)
			{
				yield return CivilTest.Wait(0.5f);
				foreach (Actor item3 in f1.Band)
				{
					try
					{
						if (item3.isAlive() && item3.has_attack_target && item3.attack_target != null && item3.attack_target.isBuilding())
						{
							hitting = true;
						}
					}
					catch
					{
					}
				}
			}
			Check("Destroy goes for buildings", hitting, $"after {Time.realtimeSinceStartup - t0:F0} s");
		}

		private IEnumerator Scene()
		{
			WarMap map = Mod.WarMap;
			if (!map.Open)
			{
				Mod.ShowMapPage(MapTabs.Page.War);
				yield return CivilTest.Wait(1f);
			}
			if (Check("into the scene", Mod.EnterScene(MapTabs.Page.War, Middle(_bandA))))
			{
				yield return CivilTest.Wait(2f);
				Check("the order bar in the scene", Mod.HudForTest.OrderBarRectForTest.width > 0f);
				yield return Key(KeyCode.Alpha2);
				Check("2 jumps the camera to group 2", (Mod.Scene.At - Middle(_bandB)).magnitude < 4f, $"at {Mod.Scene.At}, group 2 {Middle(_bandB)}");
				yield return CivilTest.Wait(1.5f);
				WorldTile worldTile = Spot(World.world.GetTile((int)Mod.Scene.At.x, (int)Mod.Scene.At.y), 0f, 6f) ?? Spot(World.world.GetTile((int)Mod.Scene.At.x, (int)Mod.Scene.At.y), MathF.PI, 6f);
				if (Check("ground in the scene", worldTile != null) && Mod.SceneProject(new Vector3((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f, Mod.GroundAt(P(worldTile))), out var gui))
				{
					yield return Click(1, gui, shift: true);
					WarFlag warFlag = map.FlagNumbered(2);
					Check("Shift+right click in the scene plants group 2's flag", warFlag != null && warFlag.Band.Count == _bandB.Count, (warFlag != null) ? $"flag 2 band {warFlag.Band.Count} of {_bandB.Count}" : ("none; " + Mod.ToastText));
					yield return Capture("rts_scene.png", 1.2f);
				}
				Mod.LeaveScene(toMap: false);
			}
		}

		private static Vector2 P(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private static Vector2 Middle(List<Actor> who)
		{
			Vector2 zero = Vector2.zero;
			int num = 0;
			foreach (Actor item in who)
			{
				try
				{
					if (item.isAlive())
					{
						zero += item.current_position;
						num++;
					}
				}
				catch
				{
				}
			}
			if (num <= 0)
			{
				return Vector2.zero;
			}
			return zero / num;
		}

		private static float Share(List<Actor> who, Vector2 at, float within)
		{
			int num = 0;
			int num2 = 0;
			foreach (Actor item in who)
			{
				try
				{
					if (item.isAlive())
					{
						num2++;
						if ((item.current_position - at).magnitude <= within)
						{
							num++;
						}
					}
				}
				catch
				{
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)num / (float)num2;
		}

		private static float Nearest(List<Actor> who, Vector2 at)
		{
			float num = float.MaxValue;
			foreach (Actor item in who)
			{
				try
				{
					if (item.isAlive())
					{
						num = Mathf.Min(num, (item.current_position - at).magnitude);
					}
				}
				catch
				{
				}
			}
			return num;
		}

		private static WorldTile Spot(WorldTile from, float angle, float distance)
		{
			if (from == null)
			{
				return null;
			}
			for (float num = distance; num < distance + 12f; num += 1.5f)
			{
				for (int i = 0; i < 9; i++)
				{
					float f = angle + (float)(((i % 2 == 0) ? 1 : (-1)) * ((i + 1) / 2)) * 0.17f;
					WorldTile tile = World.world.GetTile(from.x + Mathf.RoundToInt(Mathf.Cos(f) * num), from.y + Mathf.RoundToInt(Mathf.Sin(f) * num));
					if (tile == null || !Royal.Standable(tile))
					{
						continue;
					}
					try
					{
						if (!tile.isSameIsland(from))
						{
							continue;
						}
					}
					catch
					{
						continue;
					}
					bool flag = true;
					for (int j = -2; j <= 2 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							flag = Royal.Standable(World.world.GetTile(tile.x + k, tile.y + j));
						}
					}
					if (flag)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private void Mark(WarMap map, List<Actor> who)
		{
			map.Marked.Clear();
			foreach (Actor item in who)
			{
				map.Marked.Add(item);
			}
		}

		private IEnumerator View(WarMap map, Vector2 at, float across)
		{
			map.Centre = at;
			map.Zoom = Mathf.Clamp(map.Span / across, map.ZoomMin, map.ZoomMax);
			yield return null;
			yield return null;
			yield return CivilTest.Wait(0.3f);
		}

		private static Vector2 Screen(WarMap map, Vector2 world)
		{
			return WorldBoxMod.Instance.HudForTest.WarMapToScreenForTest?.Invoke(world) ?? Vector2.zero;
		}

		private IEnumerator Capture(string name, float wait = 0.5f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Note("captured " + name);
		}

		private IEnumerator Key(KeyCode key, EventModifiers mods = EventModifiers.None)
		{
			_events.Enqueue(new Event
			{
				type = EventType.KeyDown,
				keyCode = key,
				modifiers = mods
			});
			yield return Flush();
		}

		private IEnumerator Press(Vector2 at)
		{
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = 0,
				mousePosition = at,
				clickCount = 1
			});
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = 0,
				mousePosition = at,
				clickCount = 1
			});
			yield return Flush();
		}

		private IEnumerator Click(int button, Vector2 at, bool shift = false)
		{
			return Drag(button, at, at, shift);
		}

		private IEnumerator Drag(int button, Vector2 from, Vector2 to, bool shift = false)
		{
			EventModifiers modifiers = (shift ? EventModifiers.Shift : EventModifiers.None);
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = button,
				mousePosition = from,
				clickCount = 1,
				modifiers = modifiers
			});
			if (to != from)
			{
				for (int i = 1; i <= 10; i++)
				{
					_events.Enqueue(new Event
					{
						type = EventType.MouseDrag,
						button = button,
						mousePosition = Vector2.Lerp(from, to, (float)i / 10f),
						modifiers = modifiers
					});
				}
			}
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = button,
				mousePosition = to,
				clickCount = 1,
				modifiers = modifiers
			});
			yield return Flush();
		}

		private IEnumerator Flush()
		{
			for (int i = 0; i < 300; i++)
			{
				if (_events.Count <= 0)
				{
					break;
				}
				yield return null;
			}
			yield return null;
			yield return null;
		}

		private void OnGUI()
		{
			if (Mod == null || _events.Count == 0)
			{
				return;
			}
			Event current = Event.current;
			try
			{
				Event.current = _events.Dequeue();
				if (Mod.Scene.On)
				{
					Mod.HudForTest.DrawScene();
				}
				else if (Mod.Royal.Open)
				{
					Mod.HudForTest.DrawCouncil();
				}
				else if (Mod.WarMap.Open)
				{
					Mod.HudForTest.DrawWarMap();
				}
			}
			catch (Exception ex)
			{
				Log.Error("RTS events: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}
	}
}
