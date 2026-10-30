using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class VrHandsTest : CivilTest
	{
		private Vector2 _home;

		private float _yaw;

		private float _fastest;

		protected override string Prefix => "VRHANDS";

		private VrSystem Vr => Mod.VrForTest;

		private Hands H => Mod.HandsForTest;

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
			if (!Check("first person came up", InControl(me)))
			{
				yield break;
			}
			Settings s = Mod.Settings;
			bool savedVr = s.Vr;
			int savedHands = s.VrHands;
			WorldBoxMod.VrSimulate = true;
			Mod.VrResetForTest();
			Mod.MouseLookOff = true;
			s.Vr = true;
			s.VrHands = 0;
			try
			{
				yield return CivilTest.Wait(1f);
				if (Check("the pretend headset runs", Vr != null && Vr.Simulated && Mod.VrRunning, Vr?.Status))
				{
					Vr.SimHead = Xr.Pose.Identity;
					yield return Part("a sword", Sword(me));
					yield return Part("a spear", Spear(me));
					yield return Part("the bow", Bow(me));
					yield return Part("swings", Swings(me, king));
				}
			}
			finally
			{
				VrHandsTest vrHandsTest = this;
				s.Vr = savedVr;
				s.VrHands = savedHands;
				if (vrHandsTest.Vr != null)
				{
					vrHandsTest.Vr.SimHead = Xr.Pose.Identity;
					vrHandsTest.Vr.SimAttack = 0f;
					vrHandsTest.Vr.SimHandValid[0] = (vrHandsTest.Vr.SimHandValid[1] = false);
				}
				WorldBoxMod.VrSimulate = false;
				vrHandsTest.Mod.MouseLookOff = false;
				vrHandsTest.Mod.VrResetForTest();
				s.Save();
			}
		}

		private void Hold(int hand, Vector3 grip, Vector3 dir)
		{
			Quaternion quaternion = Quaternion.LookRotation(-dir.normalized, Vector3.up);
			Xr.Quat orientation = new Xr.Quat
			{
				X = quaternion.x,
				Y = quaternion.y,
				Z = quaternion.z,
				W = quaternion.w
			};
			Vector3 vector = grip - quaternion * new Vector3(0f, -0.02f, 0.07f);
			Vr.SimHands[hand] = new Xr.Pose
			{
				Orientation = orientation,
				Position = new Xr.Vec3
				{
					X = vector.x,
					Y = vector.y,
					Z = vector.z
				}
			};
			Vr.SimHandValid[hand] = true;
		}

		private Vector3 World(Vector3 room)
		{
			return Mod.VrRoomToWorldForTest(new Xr.Vec3
			{
				X = room.x,
				Y = room.y,
				Z = room.z
			});
		}

		private Vector3 WorldDir(Vector3 room)
		{
			return (World(room) - World(Vector3.zero)).normalized;
		}

		private Vector3 Room(Vec3 w)
		{
			Xr.Vec3 vec = Mod.VrWorldToRoomForTest(new Vector3(w.X, w.Y, w.Z));
			return new Vector3(vec.X, vec.Y, vec.Z);
		}

		private static Vector3 V(Vec3 v)
		{
			return new Vector3(v.X, v.Y, v.Z);
		}

		private static Xr.Quat Axis(float x, float y, float z, float degrees)
		{
			float f = degrees * 0.5f * (MathF.PI / 180f);
			float num = Mathf.Sin(f);
			return new Xr.Quat
			{
				X = x * num,
				Y = y * num,
				Z = z * num,
				W = Mathf.Cos(f)
			};
		}

		private void SaveEyes(string name)
		{
			RenderTexture renderTexture = Mod.VrEyeForTest(0);
			RenderTexture renderTexture2 = Mod.VrEyeForTest(1);
			if (renderTexture == null || renderTexture2 == null)
			{
				Note("no eye pictures to save for " + name);
				return;
			}
			Texture2D texture2D = Read(renderTexture);
			Texture2D texture2D2 = Read(renderTexture2);
			int num = texture2D.width + texture2D2.width + 8;
			int num2 = Mathf.Max(texture2D.height, texture2D2.height);
			Texture2D texture2D3 = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false);
			Color32[] array = new Color32[num * num2];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new Color32(20, 20, 20, byte.MaxValue);
			}
			texture2D3.SetPixels32(array);
			texture2D3.SetPixels32(0, 0, texture2D.width, texture2D.height, texture2D.GetPixels32());
			texture2D3.SetPixels32(texture2D.width + 8, 0, texture2D2.width, texture2D2.height, texture2D2.GetPixels32());
			texture2D3.Apply(updateMipmaps: false);
			File.WriteAllBytes(Path.Combine(Log.Folder, name + ".png"), texture2D3.EncodeToPNG());
			Note("saved " + name + ".png");
			UnityEngine.Object.Destroy(texture2D);
			UnityEngine.Object.Destroy(texture2D2);
			UnityEngine.Object.Destroy(texture2D3);
		}

		private static Texture2D Read(RenderTexture rt)
		{
			Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, mipChain: false);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			texture2D.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
			texture2D.Apply(updateMipmaps: false);
			RenderTexture.active = active;
			Color32[] pixels = texture2D.GetPixels32();
			Color32[] array = new Color32[pixels.Length];
			for (int i = 0; i < rt.height; i++)
			{
				Array.Copy(pixels, i * rt.width, array, (rt.height - 1 - i) * rt.width, rt.width);
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			return texture2D;
		}

		private static bool Equip(Actor unit, string id)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = global::World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
				slot.setItem(pItem, unit);
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("VRHANDS equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private IEnumerator Sword(Actor me)
		{
			Check("a sword in hand", Equip(me, "sword_iron"));
			Vector3 ahead = new Vector3(0f, 0f, -1f);
			Vector3 grip = new Vector3(0.2f, -0.3f, -0.35f);
			Hold(1, grip, ahead);
			Hold(0, new Vector3(-0.22f, -0.32f, -0.3f), ahead);
			yield return CivilTest.Wait(0.8f);
			Check("the arms follow the controllers", Mod.VrHandsOnForTest && H.TrackAimValid, $"on {Mod.VrHandsOnForTest}, aim {H.TrackAimValid}");
			float scale = Mod.VrScaleForTest;
			float magnitude = (V(H.RightFistWorld) - World(grip)).magnitude;
			Check("the right fist is where the controller is held", magnitude < 0.02f * scale + 0.005f, $"{magnitude:F3} tiles off ({scale:F2} tiles a metre)");
			float num = Vector3.Dot(V(H.RightForearmWorld), WorldDir(ahead));
			Check("the forearm runs the way the controller points", num > 0.99f, $"{num:F3}");
			Check("the left fist is where the left controller is", (V(H.LeftFistWorld) - World(new Vector3(-0.22f, -0.32f, -0.3f))).magnitude < 0.02f * scale + 0.005f);
			Vr.SimHead = new Xr.Pose
			{
				Orientation = Axis(1f, 0f, 0f, -25f)
			};
			yield return CivilTest.Wait(0.4f);
			SaveEyes("vrhands_sword");
			Vr.SimHead = Xr.Pose.Identity;
			yield return CivilTest.Wait(0.2f);
			Vec3 was = H.RightFistWorld;
			Hold(1, grip + new Vector3(0.3f, 0f, 0f), ahead);
			yield return CivilTest.Wait(0.3f);
			Vector3 vector = V(H.RightFistWorld) - V(was);
			Check("the fist moves with the hand at the world's scale", Mathf.Abs(vector.magnitude - 0.3f * scale) < 0.03f * scale && Vector3.Dot(vector.normalized, WorldDir(Vector3.right)) > 0.98f, $"{vector.magnitude:F3} tiles, want {0.3f * scale:F3}");
			Vector3 up = new Vector3(0f, Mathf.Sin(0.6981317f), 0f - Mathf.Cos(0.6981317f));
			Hold(1, grip, up);
			yield return CivilTest.Wait(0.3f);
			num = Vector3.Dot(V(H.RightForearmWorld), WorldDir(up));
			Check("the forearm turns up with the controller", num > 0.99f, $"{num:F3}");
			Hold(1, grip, ahead);
			yield return CivilTest.Wait(0.3f);
			Vector3 at = World(grip);
			int attacks = Mod.Feedback.Attacks;
			float worst = 0f;
			Vr.SimAttack = 1f;
			for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
			{
				if (t > 0.15f)
				{
					Vr.SimAttack = 0f;
				}
				worst = Mathf.Max(worst, (V(H.RightFistWorld) - at).magnitude);
				yield return null;
			}
			Vr.SimAttack = 0f;
			Note($"swings {Mod.Feedback.Attacks - attacks}");
			Check("a blow leaves the weapon where the hand is", worst < 0.02f * scale + 0.005f, $"{worst:F3} tiles at most from the hand");
			Vr.SimHandValid[1] = false;
			yield return CivilTest.Wait(0.3f);
			Check("a controller lost, the arms are held as on the screen", !H.TrackAimValid);
			Hold(1, grip, ahead);
			Mod.Settings.VrHands = 1;
			yield return CivilTest.Wait(0.3f);
			Check("set to Fixed, the arms are held as on the screen", !Mod.VrHandsOnForTest && !H.TrackAimValid);
			Mod.Settings.VrHands = 0;
			yield return CivilTest.Wait(0.3f);
			Check("set back to the controllers, they follow again", Mod.VrHandsOnForTest && H.TrackAimValid);
		}

		private IEnumerator Spear(Actor me)
		{
			if (Check("a spear in hand", Equip(me, "spear_steel")))
			{
				Vector3 ahead = new Vector3(0f, 0f, -1f);
				Vector3 grip = new Vector3(0.18f, -0.3f, -0.25f);
				Hold(1, grip, ahead);
				Hold(0, new Vector3(-0.35f, -0.45f, -0.1f), ahead);
				yield return CivilTest.Wait(0.8f);
				Check("the left hand away from the shaft is free", !H.TrackLeftOnWeapon && (V(H.LeftFistWorld) - World(new Vector3(-0.35f, -0.45f, -0.1f))).magnitude < 0.03f * Mod.VrScaleForTest + 0.005f);
				Vector3 hold = Room(H.TrackLeftHoldWorld);
				Hold(0, hold, ahead);
				yield return CivilTest.Wait(0.3f);
				float magnitude = (V(H.LeftFistWorld) - V(H.TrackLeftHoldWorld)).magnitude;
				Check("brought to the shaft, the left hand takes hold of it", H.TrackLeftOnWeapon && magnitude < 0.01f, $"on {H.TrackLeftOnWeapon}, {magnitude:F3} tiles from the shaft");
				Hold(0, hold + new Vector3(-0.3f, -0.1f, 0.1f), ahead);
				yield return CivilTest.Wait(0.3f);
				Check("taken away, it lets go", !H.TrackLeftOnWeapon);
			}
		}

		private void Stay(Actor me)
		{
			if (me != null && me.isAlive())
			{
				me.current_position = _home;
				me.dirty_current_tile = true;
				Mod.SetYaw(_yaw);
			}
		}

		private static void Keep(Actor a, Vector2 at)
		{
			if (a == null || !a.isAlive())
			{
				return;
			}
			a.current_position = at;
			try
			{
				a.cancelAllBeh();
				a.makeWait(2f);
			}
			catch
			{
			}
		}

		private IEnumerator Sweep(Actor me, Vector3 from, Vector3 to, Vector3 dir, float seconds, Actor[] kept, Vector2[] at)
		{
			_fastest = 0f;
			for (float t = 0f; t <= seconds; t += Time.unscaledDeltaTime)
			{
				Hold(1, Vector3.Lerp(from, to, Mathf.Clamp01(t / seconds)), dir);
				_fastest = Mathf.Max(_fastest, Mod.VrSwingSpeedForTest);
				Stay(me);
				for (int i = 0; i < kept.Length; i++)
				{
					Keep(kept[i], at[i]);
				}
				yield return null;
			}
			Hold(1, to, dir);
			for (int j = 0; j < 12; j++)
			{
				Stay(me);
				for (int k = 0; k < kept.Length; k++)
				{
					Keep(kept[k], at[k]);
				}
				yield return null;
			}
		}

		private IEnumerator BladeReach(Actor me, Vector3 grip, Vector3 dir, float[] reach, float high = 0.55f)
		{
			Hold(1, grip, dir);
			for (int i = 0; i < 20; i++)
			{
				Stay(me);
				yield return null;
			}
			HandStrike strikeR = H.StrikeR;
			Vector2 rhs = new Vector2(Mathf.Cos(_yaw), Mathf.Sin(_yaw));
			float num = float.MaxValue;
			for (int j = 0; j <= 20; j++)
			{
				Vec3 vec = strikeR.A + (strikeR.B - strikeR.A) * ((float)j / 20f);
				float num2 = vec.Z - Mod.GroundAt(new Vector2(vec.X, vec.Y));
				if (!(Mathf.Abs(num2 - high) >= num))
				{
					num = Mathf.Abs(num2 - high);
					reach[0] = Vector2.Dot(new Vector2(vec.X, vec.Y) - _home, rhs);
					reach[1] = num2;
				}
			}
		}

		private IEnumerator Swings(Actor me, Actor king)
		{
			Settings s = Mod.Settings;
			bool savedFf = s.FriendlyFire;
			s.FriendlyFire = false;
			try
			{
				if (!Check("a sword in hand", Equip(me, "sword_iron")))
				{
					yield break;
				}
				_home = me.current_position;
				_yaw = Mod.ViewYaw;
				Vr.SimHead = Xr.Pose.Identity;
				Vector3 ahead = new Vector3(0f, 0f, -1f);
				Hold(0, new Vector3(-0.3f, -0.45f, -0.15f), ahead);
				float[] reach = new float[2];
				yield return BladeReach(me, new Vector3(0f, -0.45f, -0.35f), ahead, reach);
				Note($"the blade at a body's chest: {reach[0]:F2} tiles ahead, {reach[1]:F2} over the ground");
				Vector2 fwd = new Vector2(Mathf.Cos(_yaw), Mathf.Sin(_yaw));
				Vector2 side = new Vector2(Mathf.Sin(_yaw), 0f - Mathf.Cos(_yaw));
				Vector2 spot = _home + fwd * Mathf.Max(0.3f, reach[0]);
				Actor bandit = CivilTest.SpawnNear("bandit", spot);
				if (!Check("a bandit to swing at", bandit != null))
				{
					yield break;
				}
				Actor[] one = new Actor[1] { bandit };
				Vector2[] oneAt = new Vector2[1] { spot };
				Vector3 right = new Vector3(0.7f, -0.45f, -0.35f);
				Vector3 left = new Vector3(-0.7f, -0.45f, -0.35f);
				yield return Sweep(me, right, right, ahead, 0.4f, one, oneAt);
				int hits = Mod.VrSwingHitsForTest;
				bandit.getHealth();
				yield return Sweep(me, right, left, ahead, 3.6f, one, oneAt);
				Check("moved slowly through a body, the sword doesn't hit", Mod.VrSwingHitsForTest == hits, $"hits {Mod.VrSwingHitsForTest - hits}, fastest {_fastest:F1} m/s");
				int health = bandit.getHealth();
				yield return Sweep(me, left, right, ahead, 0.22f, one, oneAt);
				Check("swung fast through it, the sword hits once", Mod.VrSwingHitsForTest == hits + 1 && (bandit.getHealth() < health || !bandit.isAlive()), $"hits {Mod.VrSwingHitsForTest - hits}, health {health} -> {bandit.getHealth()}, fastest {_fastest:F1} m/s");
				Actor second = CivilTest.SpawnNear("bandit", spot);
				if (second != null)
				{
					Actor[] two = new Actor[2] { bandit, second };
					Vector2[] twoAt = new Vector2[2]
					{
						spot - side * 0.2f,
						spot + side * 0.2f
					};
					if (!bandit.isAlive())
					{
						two[0] = CivilTest.SpawnNear("bandit", spot);
					}
					yield return Sweep(me, left, left, ahead, 0.4f, two, twoAt);
					hits = Mod.VrSwingHitsForTest;
					int h0 = ((two[0] != null) ? two[0].getHealth() : (-1));
					int h1 = two[1].getHealth();
					Hitbox box;
					string where = $"{Vector2.Dot(two[0].current_position - _home, side):F2} and {Vector2.Dot(two[1].current_position - _home, side):F2} across, " + $"boxes {Hitboxes.Get(two[0], out box)}/{Hitboxes.Get(two[1], out box)}";
					yield return Sweep(me, left, right, ahead, 0.22f, two, twoAt);
					Check("one swing through two bodies hits both", Mod.VrSwingHitsForTest - hits == 2, $"hits {Mod.VrSwingHitsForTest - hits}, health {h0} -> {two[0]?.getHealth()}, {h1} -> {two[1].getHealth()}; {where}, fastest {_fastest:F1} m/s");
					CivilTest.Kill(second);
				}
				CivilTest.Kill(bandit);
				Actor friend = null;
				foreach (Actor item in Founded)
				{
					if (item != null && item.isAlive() && item != me && item != king)
					{
						friend = item;
						break;
					}
				}
				if (friend != null)
				{
					Actor[] two = new Actor[1] { friend };
					yield return Sweep(me, right, right, ahead, 0.4f, two, oneAt);
					hits = Mod.VrSwingHitsForTest;
					health = friend.getHealth();
					yield return Sweep(me, right, left, ahead, 0.22f, two, oneAt);
					Check("one of your own isn't hit (friendly fire off)", Mod.VrSwingHitsForTest == hits && friend.getHealth() == health, $"hits {Mod.VrSwingHitsForTest - hits}, health {health} -> {friend.getHealth()}");
				}
				Actor target = CivilTest.SpawnNear("bandit", spot);
				if (target != null)
				{
					(new Actor[1])[0] = target;
					Vector2 near = _home + fwd * 0.45f;
					(new Vector2[1])[0] = near;
					Hold(1, new Vector3(0.35f, -0.5f, 0.05f), ahead);
					for (int h1 = 0; h1 < 30; h1++)
					{
						Stay(me);
						Keep(target, near);
						yield return null;
					}
					hits = Mod.VrSwingHitsForTest;
					health = target.getHealth();
					for (float t = 0f; t <= 0.18f; t += Time.unscaledDeltaTime)
					{
						Hold(0, Vector3.Lerp(new Vector3(-0.15f, -0.15f, -0.05f), new Vector3(-0.05f, -0.15f, -0.75f), t / 0.18f), ahead);
						Stay(me);
						Keep(target, near);
						yield return null;
					}
					for (int h1 = 0; h1 < 12; h1++)
					{
						Stay(me);
						Keep(target, near);
						yield return null;
					}
					Check("the empty left fist punches", Mod.VrSwingHitsForTest == hits + 1 && (target.getHealth() < health || !target.isAlive()), $"hits {Mod.VrSwingHitsForTest - hits}, health {health} -> {target.getHealth()}");
					CivilTest.Kill(target);
					Hold(0, new Vector3(-0.3f, -0.45f, -0.15f), ahead);
				}
				if (Check("an axe in hand", Equip(me, "axe_iron")))
				{
					yield return BladeReach(me, new Vector3(0f, -0.45f, -0.35f), ahead, reach, 0.8f);
					WorldTile tile = global::World.world.GetTile(Mathf.FloorToInt(_home.x + fwd.x * 3f), Mathf.FloorToInt(_home.y + fwd.y * 3f));
					Building tree = PutTree(tile);
					if (Check("a tree to chop", tree != null))
					{
						_home = Work.PlaceOf(tree) - fwd * Mathf.Max(0.5f, reach[0]);
						int h1 = Mod.VrSwingWorkForTest;
						Actor[] two = new Actor[0];
						Vector2[] twoAt = new Vector2[0];
						yield return Sweep(me, right, right, ahead, 0.4f, two, twoAt);
						yield return Sweep(me, right, left, ahead, 0.22f, two, twoAt);
						Check("an axe swung through a tree chops it", Mod.VrSwingWorkForTest == h1 + 1, $"blows {Mod.VrSwingWorkForTest - h1}, reach {reach[0]:F2}");
						try
						{
							if (tree.isAlive())
							{
								tree.removeBuildingFinal();
							}
						}
						catch
						{
						}
					}
				}
				if (Butchery.CanCut(me) && Equip(me, "sword_iron"))
				{
					_home = me.current_position;
					Vector3 down = new Vector3(0f, -0.85f, -0.5f).normalized;
					yield return BladeReach(me, new Vector3(0f, -0.95f, -0.3f), down, reach, 0.2f);
					Vector2 at = _home + fwd * Mathf.Max(0.4f, reach[0]);
					Butchery.Carcass carcass = new Butchery.Carcass
					{
						Id = -4242L,
						Kind = "sheep",
						Name = "sheep",
						At = at,
						Total = 3
					};
					carcass.Parts.Add(new KeyValuePair<string, int>("meat", 3));
					Butchery.All.Add(carcass);
					int h1 = Mod.VrSwingCutsForTest;
					Actor[] two = new Actor[0];
					Vector2[] twoAt = new Vector2[0];
					Vector3 lowRight = new Vector3(0.7f, -0.95f, -0.3f);
					Vector3 lowLeft = new Vector3(-0.7f, -0.95f, -0.3f);
					yield return Sweep(me, lowRight, lowRight, down, 0.4f, two, twoAt);
					yield return Sweep(me, lowRight, lowLeft, down, 0.22f, two, twoAt);
					Check("a sword swung low through a carcass cuts from it", Mod.VrSwingCutsForTest == h1 + 1 && carcass.Left < 3, $"cuts {Mod.VrSwingCutsForTest - h1}, left {carcass.Left}, blade {reach[0]:F2} ahead {reach[1]:F2} up");
					Butchery.All.Remove(carcass);
				}
			}
			finally
			{
				VrHandsTest vrHandsTest = this;
				s.FriendlyFire = savedFf;
				vrHandsTest.Hold(1, new Vector3(0.2f, -0.3f, -0.35f), new Vector3(0f, 0f, -1f));
			}
		}

		private Building PutTree(WorldTile t)
		{
			if (t == null)
			{
				return null;
			}
			string text = ((AssetManager.buildings.get("oak_tree") != null) ? "oak_tree" : null);
			if (text == null)
			{
				foreach (BuildingAsset item in AssetManager.buildings.list)
				{
					if (item.building_type == BuildingType.Building_Tree && item.resources_given != null && item.resources_given.Count > 0)
					{
						text = item.id;
						break;
					}
				}
			}
			if (text == null)
			{
				return null;
			}
			try
			{
				if (t.building != null)
				{
					t.building.removeBuildingFinal();
				}
				return global::World.world.buildings.addBuilding(text, t, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			}
			catch (Exception ex)
			{
				Log.Error("VRHANDS put tree: " + ex.Message);
				return null;
			}
		}

		private IEnumerator Bow(Actor me)
		{
			if (!Check("a bow in hand", Equip(me, "bow_wood")))
			{
				yield break;
			}
			for (int i = 0; i < 60; i++)
			{
				if (Mod.Bow.InHand)
				{
					break;
				}
				yield return null;
			}
			if (!Check("the bow is drawn by hand", Mod.Bow.InHand && Mod.Bow.Tracked))
			{
				yield break;
			}
			float f = MathF.PI / 6f;
			Vector3 dir = new Vector3(Mathf.Sin(f), 0f, 0f - Mathf.Cos(f));
			Vector3 grip = new Vector3(-0.05f, -0.15f, 0f) + dir * 0.55f;
			Hold(0, grip, dir);
			Hold(1, new Vector3(0.25f, -0.4f, -0.2f), new Vector3(0f, 0f, -1f));
			yield return CivilTest.Wait(0.8f);
			Check("away from the string the right hand can't take it", !H.TrackNockNear);
			Vector3 nock = Room(H.TrackNockWorld);
			Hold(1, nock, dir);
			yield return CivilTest.Wait(0.3f);
			Check("at the nock the right hand can take the string", H.TrackNockNear, $"nock at {nock.x:F2},{nock.y:F2},{nock.z:F2}");
			Vr.SimAttack = 1f;
			yield return CivilTest.Wait(0.25f);
			Check("the trigger takes the string", Mod.Bow.OnString && (Mod.Bow.State.State == BowState.Phase.Drawing || Mod.Bow.State.State == BowState.Phase.Held), $"on {Mod.Bow.OnString}, {Mod.Bow.State.State}");
			float back = 0f;
			while (back < 0.8f && H.TrackPull < 1.02f)
			{
				back += 0.02f;
				Hold(1, nock - dir * back, dir);
				yield return null;
			}
			yield return CivilTest.Wait(0.2f);
			Check("pulled back, the bow draws", H.TrackPull >= 1f && Mod.Bow.State.Draw >= 0.98f, $"pull {H.TrackPull:F2} at {back:F2} m, draw {Mod.Bow.State.Draw:F2}");
			Note($"a full draw is {back:F2} m of pull");
			Vector3 aimDir = V(H.TrackAimDir).normalized;
			Vector3 rhs = WorldDir(dir);
			Check("the arrow lies from the hand through the bow", Vector3.Dot(aimDir, rhs) > 0.99f, $"{Vector3.Dot(aimDir, rhs):F4}");
			Vr.SimHead = new Xr.Pose
			{
				Orientation = Axis(0f, 1f, 0f, -25f)
			};
			yield return CivilTest.Wait(0.3f);
			SaveEyes("vrhands_bow");
			Vr.SimHead = Xr.Pose.Identity;
			yield return CivilTest.Wait(0.3f);
			float aimYaw = Mathf.Atan2(aimDir.y, aimDir.x);
			float viewYaw = Mod.View.Yaw;
			float num = Mathf.Abs(Mathf.DeltaAngle(aimYaw * 57.29578f, viewYaw * 57.29578f));
			Check("...30 degrees right of where you look", Mathf.Abs(num - 30f) < 3f, $"{num:F1} deg");
			Check("the shots go along the hand", FirstPersonAim.HandAim);
			Vec3 tip = H.TrackAimFrom;
			int launched = FirstPersonShots.Launched;
			Vr.SimAttack = 0f;
			for (float t = 0f; t < 1.5f; t += 0.05f)
			{
				if (FirstPersonShots.Launched != launched)
				{
					break;
				}
				yield return CivilTest.Wait(0.05f);
			}
			if (Check("let go, the arrow is loosed", FirstPersonShots.Launched > launched, $"{FirstPersonShots.Launched - launched} shots"))
			{
				Vec3 lastFrom = FirstPersonShots.LastFrom;
				Vec3 lastAim = FirstPersonShots.LastAim;
				float magnitude = (V(lastFrom) - V(tip)).magnitude;
				Check("it leaves from the arrow's point", magnitude < 0.15f, $"{magnitude:F3} tiles from it");
				float num2 = Mathf.Atan2(lastAim.Y - lastFrom.Y, lastAim.X - lastFrom.X);
				float num3 = Mathf.Abs(Mathf.DeltaAngle(num2 * 57.29578f, aimYaw * 57.29578f));
				Check("it flies the way the arrow pointed, not where you look", num3 < 4f, $"{num3:F1} deg off the arrow, {Mathf.Abs(Mathf.DeltaAngle(num2 * 57.29578f, viewYaw * 57.29578f)):F1} off the view");
			}
			Check("the string is let go", !Mod.Bow.OnString);
			yield return CivilTest.Wait(1.4f);
			Check("the next arrow is nocked", Mod.Bow.State.ArrowNocked);
		}
	}
}
