using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using FirstPerson.Gpu;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class LooksTest : CivilTest
	{
		private Actor _you;

		private readonly List<Actor> _folk = new List<Actor>();

		private readonly Dictionary<long, ulong> _keys = new Dictionary<long, ulong>();

		private readonly Dictionary<long, ulong> _masks = new Dictionary<long, ulong>();

		private readonly Dictionary<long, Wardrobe.Look> _looks = new Dictionary<long, Wardrobe.Look>();

		private readonly Dictionary<long, Wardrobe.Who> _whos = new Dictionary<long, Wardrobe.Who>();

		private Actor _helmeted;

		protected override string Prefix => "LOOKS";

		protected override IEnumerator Body()
		{
			int slot = Slot(Options);
			yield return LoadSave(slot);
			if (Check("a town of one of the four peoples", Town(out var best, out var race, out var middle)))
			{
				Note($"town {best.name} ({race}), {_folk.Count} grown-ups chosen; possessing {WorldBoxMod.Describe(_you)}");
				try
				{
					((BaseSimObject)_you).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(_you, 3f);
				Settings s = Mod.Settings;
				float detail = s.DetailDistance;
				float creatures = s.CreatureDistance;
				int time = s.TimeMode;
				s.CreatureDistance = 1200f;
				s.TimeMode = 1;
				Mod.ForceRelight();
				try
				{
					yield return Part("drawn", Drawn(middle));
					yield return Part("distinct", Distinct());
					yield return Part("steady", Steady(middle));
					yield return Part("jobs", Jobs());
					yield return Part("helmet", Helmet());
					yield return Part("portraits", Portraits());
					yield return Part("frames", Frames(middle));
					yield return Part("reload", Reload(slot, middle));
				}
				finally
				{
					LooksTest looksTest = this;
					s.DetailDistance = detail;
					s.CreatureDistance = creatures;
					s.TimeMode = time;
					looksTest.Mod.ForceRelight();
					looksTest.Mod.DebugEyeSet = false;
				}
			}
		}

		private static int Slot(string options)
		{
			int num = options.IndexOf("map", StringComparison.Ordinal);
			if (num < 0)
			{
				return 32;
			}
			int num2 = 0;
			int num3 = num + 3;
			while (num3 < options.Length && char.IsDigit(options[num3]))
			{
				num2 = num2 * 10 + (options[num3++] - 48);
			}
			if (num2 <= 0)
			{
				return 32;
			}
			return num2;
		}

		private IEnumerator LoadSave(int slot)
		{
			string text = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
			if (!File.Exists(text))
			{
				Note("no save in slot " + slot + ": the starting map");
				yield break;
			}
			bool flag = false;
			try
			{
				SaveManager.loadMapFromBytes(File.ReadAllBytes(text));
				flag = true;
			}
			catch (Exception ex)
			{
				Note("could not load " + text + ": " + ex.Message);
			}
			if (!flag)
			{
				yield break;
			}
			for (float t = 0f; t < 90f; t += 0.5f)
			{
				yield return CivilTest.Wait(0.5f);
				bool flag2 = true;
				try
				{
					flag2 = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag2 && t > 3f)
				{
					break;
				}
			}
			Config.autosaves = false;
			Config.paused = false;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Note($"save {slot} loaded: {MapBox.width}x{MapBox.height}, {World.world.units.getSimpleList().Count} creatures");
			yield return CivilTest.Wait(4f);
		}

		private static bool OfFour(Actor a)
		{
			string text = ((a?.asset != null) ? a.asset.id : "");
			switch (text)
			{
			default:
				return text == "dwarf";
			case "human":
			case "elf":
			case "orc":
				return true;
			}
		}

		private bool Town(out City best, out string race, out Vector2 middle)
		{
			best = null;
			race = null;
			middle = default(Vector2);
			int num = 0;
			foreach (City item in World.world.cities.list)
			{
				if (item == null || !item.isAlive())
				{
					continue;
				}
				Dictionary<string, int> dictionary = new Dictionary<string, int>();
				foreach (Actor unit in item.units)
				{
					if (unit != null && unit.isAlive() && unit.isAdult() && OfFour(unit))
					{
						dictionary[unit.asset.id] = (dictionary.TryGetValue(unit.asset.id, out var value) ? value : 0) + 1;
					}
				}
				foreach (KeyValuePair<string, int> item2 in dictionary)
				{
					if (item2.Value > num)
					{
						num = item2.Value;
						best = item;
						race = item2.Key;
					}
				}
			}
			if (best == null || num < 8)
			{
				return false;
			}
			List<Actor> list = new List<Actor>();
			foreach (Actor unit2 in best.units)
			{
				if (unit2 != null && unit2.isAlive() && unit2.isAdult() && unit2.asset.id == race)
				{
					list.Add(unit2);
				}
			}
			list.Sort((Actor x, Actor y) => x.data.id.CompareTo(y.data.id));
			_you = list[0];
			for (int num2 = 1; num2 < list.Count; num2++)
			{
				if (_folk.Count >= 30)
				{
					break;
				}
				_folk.Add(list[num2]);
			}
			int num3 = -1;
			foreach (Actor item3 in _folk)
			{
				int num4 = 0;
				foreach (Actor item4 in _folk)
				{
					if ((item3.current_position - item4.current_position).sqrMagnitude < 100f)
					{
						num4++;
					}
				}
				if (num4 > num3)
				{
					num3 = num4;
					middle = item3.current_position;
				}
			}
			return _folk.Count >= 8;
		}

		private IEnumerator Over(Vector2 middle, float height = 30f)
		{
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = height;
			Mod.DebugEye = middle + new Vector2(0f, -18f);
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-55f);
			yield return CivilTest.Wait(1.5f);
		}

		private IEnumerator Tour(List<Actor> who)
		{
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = 1.6f;
			foreach (Actor a in who)
			{
				if (a == null || !a.isAlive())
				{
					continue;
				}
				try
				{
					if (a.is_inside_building)
					{
						a.exitBuilding();
					}
					a.makeWait(30f);
				}
				catch
				{
				}
				Wardrobe.Look look;
				ulong mask;
				for (float t = 0f; t < 1.5f && (t < 0.1f || !People.LookForTest(a, out look, out mask)); t += Time.unscaledDeltaTime)
				{
					Mod.DebugEye = a.current_position + new Vector2(0f, -2.5f);
					Mod.SetYaw(MathF.PI / 2f);
					Mod.SetPitchDegrees(-8f);
					yield return null;
				}
			}
		}

		private IEnumerator Near(Actor a, float most = 2f, bool stay = false)
		{
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = 1.6f;
			try
			{
				if (a.is_inside_building)
				{
					a.exitBuilding();
				}
				a.makeWait(20f);
			}
			catch
			{
			}
			for (float t = 0f; t < most; t += Time.unscaledDeltaTime)
			{
				Mod.DebugEye = a.current_position + new Vector2(0f, -2.5f);
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-8f);
				yield return null;
				bool female;
				ulong hidden;
				string text = People.DrawnModel(a, out female, out hidden);
				if (!stay && t > 0.3f && text != null && !text.EndsWith("_m", StringComparison.Ordinal) && !text.EndsWith("_f", StringComparison.Ordinal) && !text.EndsWith("_x", StringComparison.Ordinal))
				{
					break;
				}
			}
		}

		private IEnumerator Drawn(Vector2 middle)
		{
			yield return Tour(_folk);
			yield return Over(middle);
			int num = 0;
			int num2 = 0;
			List<Actor> list = new List<Actor>();
			int num3 = 0;
			bool female;
			ulong hidden;
			foreach (Actor item in _folk)
			{
				if (People.DrawnModel(item, out female, out hidden) == null)
				{
					try
					{
						Note($"{item.data.id} not drawn: in a building {item.is_inside_building}, indoors {Steering.HiddenLastFrame(item)}, on a boat {item.is_inside_boat}");
					}
					catch
					{
					}
				}
				else
				{
					list.Add(item);
					if (People.LookForTest(item, out var _, out hidden))
					{
						num3++;
					}
				}
			}
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple == null || !simple.isAlive() || !OfFour(simple) || simple.isAdult())
				{
					continue;
				}
				string text = People.DrawnModel(simple, out female, out hidden);
				if (text != null)
				{
					num++;
					if (text.Contains("_child"))
					{
						num2++;
					}
				}
			}
			Check("everyone drawn is drawn with a look", list.Count >= 12 && num3 == list.Count, $"{num3} of {list.Count} drawn ({_folk.Count - list.Count} indoors or out of sight)");
			_folk.Clear();
			_folk.AddRange(list);
			Check("children are drawn on children's bodies", num == 0 || num2 == num, $"{num2} of {num} children");
		}

		private IEnumerator Distinct()
		{
			Dictionary<ulong, Actor> dictionary = new Dictionary<ulong, Actor>();
			int num = 0;
			string text = null;
			foreach (Actor item in _folk)
			{
				if (!People.LookForTest(item, out var look, out var mask))
				{
					continue;
				}
				ulong num2 = look.Key();
				_keys[item.data.id] = num2;
				_looks[item.data.id] = look;
				if (People.LookWhoForTest(item, out var who))
				{
					_whos[item.data.id] = who;
				}
				_masks[item.data.id] = mask;
				if (dictionary.TryGetValue(num2, out var value))
				{
					num++;
					if (text == null)
					{
						text = $"{item.data.id} and {value.data.id}";
					}
				}
				else
				{
					dictionary[num2] = item;
				}
				Note($"{item.data.id} {PeopleMet.Looks(item)}: face {look.Face} hair {look.Hair} beard {look.Beard} hat {look.Hat} job {look.Job} cut {look.Cut} belt {look.Belt} skirt {look.Skirt} build {look.Build} tall {look.Tall:F2}");
			}
			Check("no two of them look alike", num == 0 && dictionary.Count >= 8, string.Format("{0} different, {1} alike{2}", dictionary.Count, num, (text != null) ? (" (" + text + ")") : ""));
			HashSet<string> hashSet = new HashSet<string>();
			foreach (Actor item2 in _folk)
			{
				hashSet.Add(PeopleMet.Looks(item2));
			}
			Check("their look words tell many apart", hashSet.Count >= Mathf.Max(5, _folk.Count * 2 / 5), $"{hashSet.Count} different of {_folk.Count}");
			yield break;
		}

		private static bool OwnSame(in Wardrobe.Look a, in Wardrobe.Look b)
		{
			if (a.Face == b.Face && a.Hair == b.Hair && a.Beard == b.Beard && a.Build == b.Build && Mathf.Abs(a.Tall - b.Tall) < 0.0001f && a.Shirt == b.Shirt && a.Pants == b.Pants)
			{
				return a.Cloth == b.Cloth;
			}
			return false;
		}

		private IEnumerator Steady(Vector2 middle)
		{
			int start = Time.frameCount;
			while (Time.frameCount - start < 300)
			{
				yield return null;
			}
			yield return Tour(_folk);
			int num = 0;
			int num2 = 0;
			foreach (Actor item in _folk)
			{
				if (_looks.TryGetValue(item.data.id, out var value) && People.LookForTest(item, out var look, out var _) && look.Key() != value.Key())
				{
					bool flag = !OwnSame(in look, in value);
					if (flag)
					{
						num++;
					}
					else
					{
						num2++;
					}
					Note(string.Format("{0} changed{1}: job {2}->{3} hat {4}->{5} face {6}->{7} hair {8}->{9} ", item.data.id, flag ? "" : " (work)", value.Job, look.Job, value.Hat, look.Hat, value.Face, look.Face, value.Hair, look.Hair) + $"build {value.Build}->{look.Build} aged {value.Aged}->{look.Aged} patch {value.Patch}->{look.Patch} scar {value.Scar}->{look.Scar}");
				}
			}
			Check("each looks the same 300 frames later", num == 0, string.Format("{0} changed{1}", num, (num2 > 0) ? $", {num2} in other work clothes" : ""));
		}

		private IEnumerator Jobs()
		{
			int jobs = 0;
			int dressed = 0;
			string missing = null;
			foreach (Actor a in _folk)
			{
				Wardrobe.Job job = People.JobOf(a);
				if (job == Wardrobe.Job.None || People.HasRole(a))
				{
					continue;
				}
				yield return Near(a);
				if (!People.LookForTest(a, out var look, out var mask))
				{
					continue;
				}
				if (look.Job != job)
				{
					Mod.DebugEyeSet = true;
					for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
					{
						if (look.Job == job)
						{
							break;
						}
						Mod.DebugEye = a.current_position + new Vector2(0f, -2.5f);
						yield return null;
						job = People.JobOf(a);
						People.LookForTest(a, out look, out mask);
					}
				}
				jobs++;
				bool female;
				ulong hidden;
				string text = People.DrawnModel(a, out female, out hidden);
				MeshModel meshModel = ((text != null) ? Models.Get(text) : null);
				bool flag = false;
				if (meshModel != null)
				{
					for (int i = 0; i < meshModel.Parts.Length && i < 64; i++)
					{
						if (meshModel.Parts[i].Name != null && meshModel.Parts[i].Name.StartsWith("job_" + job.ToString().ToLowerInvariant(), StringComparison.Ordinal) && (hidden & (ulong)(1L << i)) == 0L)
						{
							flag = true;
						}
					}
				}
				if (look.Job == job && flag)
				{
					dressed++;
				}
				else if (missing == null)
				{
					missing = $"{a.data.id} {job}: look {look.Job}, model {text}";
				}
			}
			Check("the working wear their job's clothes", jobs == 0 || dressed == jobs, string.Format("{0} of {1}{2}", dressed, jobs, (missing != null) ? ("; first not: " + missing) : ""));
		}

		private Actor Outside(int from, int step, Actor not = null)
		{
			for (int i = 0; i < _folk.Count; i++)
			{
				Actor actor = _folk[((from + i * step) % _folk.Count + _folk.Count) % _folk.Count];
				bool flag = false;
				try
				{
					flag = actor != not && actor.isAlive() && !actor.is_inside_building && !Steering.HiddenLastFrame(actor) && People.DrawnModel(actor, out var _, out var _) != null;
				}
				catch
				{
				}
				if (flag)
				{
					return actor;
				}
			}
			return _folk[from];
		}

		private IEnumerator Helmet()
		{
			Actor a = (_helmeted = Outside(_folk.Count / 2, 1));
			ArmorTestPut(a, "helmet_iron");
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.5f);
			}
			_you.current_position = a.current_position - new Vector2(1.3f, 0f);
			yield return Near(a, 3.5f, stay: true);
			bool female;
			ulong hidden;
			string text = People.DrawnModel(a, out female, out hidden);
			MeshModel meshModel = ((text != null) ? Models.Get(text) : null);
			int num = 0;
			if (meshModel != null)
			{
				for (int i = 0; i < meshModel.Parts.Length && i < 64; i++)
				{
					string text2 = meshModel.Parts[i].Name ?? "";
					if ((text2.StartsWith("hair_", StringComparison.Ordinal) || text2.StartsWith("hat_", StringComparison.Ordinal)) && (hidden & (ulong)(1L << i)) == 0L)
					{
						num++;
					}
				}
			}
			Check("a helmet hides their hair and hat", meshModel != null && num == 0, $"{text}: {num} hair or hat parts shown");
			Mod.SaveFrame("looks_helmet.png");
		}

		private static void ArmorTestPut(Actor a, string id)
		{
			try
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null || a.equipment == null)
				{
					return;
				}
				ActorEquipmentSlot slot = a.equipment.getSlot(equipmentAsset.equipment_type);
				Item item = World.world.items.generateItem(equipmentAsset, a.kingdom, a.getName(), 1, a, 0, pByPlayer: true);
				if (item != null)
				{
					if (slot.getItem() != null)
					{
						slot.takeAwayItem();
					}
					slot.setItem(item, a);
					a.setStatsDirty();
				}
			}
			catch (Exception ex)
			{
				Log.Error("LOOKS wear " + id + ": " + ex.Message);
			}
		}

		private IEnumerator Portraits()
		{
			for (int i = 0; i < 6 && i < _folk.Count; i++)
			{
				PeopleMet.Note(_you, _folk[i]);
			}
			Favorites.Tab = 1;
			Mod.DebugEyeSet = false;
			Mod.Craft.Open = true;
			int before = FirstPerson.Portraits.Drawn;
			yield return CivilTest.Wait(2.5f);
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "looks_contacts.png"));
			yield return CivilTest.Wait(0.6f);
			int num = 0;
			for (int j = 0; j < 6 && j < _folk.Count; j++)
			{
				Texture2D texture2D = FirstPerson.Portraits.ShotForTest(_folk[j]);
				if (!(texture2D == null))
				{
					num++;
					SavePortrait(texture2D, $"looks_portrait_{j}.png");
				}
			}
			Check("the contact cards show their headshots", num >= Mathf.Min(6, _folk.Count) - 1, $"{num} of 6, {FirstPerson.Portraits.Drawn - before} drawn");
			Mod.Craft.Open = false;
			yield return CivilTest.Wait(0.4f);
			Actor npc = Outside(_folk.Count - 1, -1, _helmeted);
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.5f);
			}
			_you.current_position = npc.current_position - new Vector2(1.3f, 0f);
			yield return CivilTest.Wait(0.6f);
			CivilTest.StandBefore(npc, _you, 1.4f);
			try
			{
				npc.makeWait(20f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.5f);
			bool talk = Mod.StartConversationForTest(npc);
			yield return CivilTest.Wait(2f);
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "looks_talk.png"));
			yield return CivilTest.Wait(0.6f);
			Texture2D texture2D2 = FirstPerson.Portraits.ShotForTest(npc);
			Check("the talk card shows their headshot", talk && texture2D2 != null, (!talk) ? "no talk" : ((texture2D2 != null) ? $"{texture2D2.width}x{texture2D2.height}" : "no headshot"));
			if (texture2D2 != null)
			{
				SavePortrait(texture2D2, "looks_portrait_talk.png");
			}
			Mod.EndConversation();
			yield return CivilTest.Wait(0.5f);
		}

		private static void SavePortrait(Texture2D t, string name)
		{
			Color32[] pixels = t.GetPixels32();
			int width = t.width;
			int height = t.height;
			Texture2D texture2D = new Texture2D(width * 6, height * 6, TextureFormat.RGB24, mipChain: false);
			Color32[] array = new Color32[width * 6 * height * 6];
			Color32 color = new Color32(70, 66, 62, byte.MaxValue);
			for (int i = 0; i < height * 6; i++)
			{
				for (int j = 0; j < width * 6; j++)
				{
					Color32 color2 = pixels[i / 6 * width + j / 6];
					float num = (float)(int)color2.a / 255f;
					array[i * width * 6 + j] = new Color32((byte)((float)(int)color2.r * num + (float)(int)color.r * (1f - num)), (byte)((float)(int)color2.g * num + (float)(int)color.g * (1f - num)), (byte)((float)(int)color2.b * num + (float)(int)color.b * (1f - num)), byte.MaxValue);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
		}

		private IEnumerator Frames(Vector2 middle)
		{
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = 1f;
			Mod.DebugEye = middle + new Vector2(0f, -7f);
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-4f);
			yield return CivilTest.Wait(2f);
			Mod.SaveFrame("looks_street.png");
			List<float> ms = new List<float>();
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				ms.Add(Mod.FrameMs);
				yield return null;
			}
			ms.Sort();
			Note($"in the street: mod frame {((ms.Count > 0) ? ms[ms.Count / 2] : 0f):F1} ms (middle of {ms.Count})");
			GpuRenderer gpu = Mod.GpuRendererForTest;
			if (gpu != null)
			{
				People.LooksOn = false;
				yield return CivilTest.Wait(0.6f);
				Note("looks off: " + gpu.Stats + "; " + gpu.PeopleDrawsForTest());
				People.LooksOn = true;
				yield return CivilTest.Wait(0.6f);
				Note("looks on: " + gpu.Stats + "; " + gpu.PeopleDrawsForTest());
				FrameRenderer cfg = Mod.Renderer;
				List<Billboard> bb = Mod.SceneForTest.Billboards;
				List<VoxelObject> vox = Mod.SceneForTest.Voxels;
				bool classic = Mod.Settings.GpuLook == 0;
				Config.paused = true;
				yield return CivilTest.Wait(0.3f);
				gpu.RedrawForTest(cfg, bb, vox, classic, skipUnseen: false);
				byte[] array = gpu.RedrawForTest(cfg, bb, vox, classic, skipUnseen: false);
				byte[] array2 = gpu.RedrawForTest(cfg, bb, vox, classic, skipUnseen: true);
				Config.paused = false;
				if (array != null && array2 != null && array.Length == array2.Length)
				{
					int width = gpu.Width;
					int height = gpu.Height;
					int num = width * height;
					int[] array3 = new int[3]
					{
						num * 8,
						num * 16,
						num * 4
					};
					string[] array4 = new string[3] { "colour", "data", "picture" };
					int num2 = 0;
					for (int i = 0; i < 3; i++)
					{
						int num3 = 0;
						int num4 = array3[i] / num;
						StringBuilder stringBuilder = new StringBuilder();
						for (int j = 0; j < array3[i]; j++)
						{
							if (array[num2 + j] != array2[num2 + j])
							{
								if (num3 < 6)
								{
									stringBuilder.Append($" ({j / num4 % width},{j / num4 / width} byte {j % num4}: {array[num2 + j]}->{array2[num2 + j]})");
								}
								num3++;
							}
						}
						Note($"culling check, {array4[i]}: {num3} bytes differ{stringBuilder}");
						num2 += array3[i];
					}
				}
			}
			Mod.DebugEye = middle + new Vector2(-6f, 4f);
			Mod.SetYaw(0f);
			yield return CivilTest.Wait(1.5f);
			Mod.SaveFrame("looks_street_2.png");
			for (int k = 0; k < 3 && k < _folk.Count; k++)
			{
				Vector2 current_position = _folk[k * 3 % _folk.Count].current_position;
				Mod.DebugEye = current_position + new Vector2(0.4f, -1.6f);
				Vector2 vector = current_position - Mod.DebugEye;
				Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
				Mod.SetPitchDegrees(2f);
				yield return CivilTest.Wait(1.2f);
				Mod.SaveFrame($"looks_close_{k}.png");
			}
			List<Actor> list = new List<Actor>();
			for (int l = 0; l < _folk.Count; l++)
			{
				if (list.Count >= 6)
				{
					break;
				}
				Actor item = Outside(l, 1);
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
			Vector2 current_position2 = _you.current_position;
			for (int m = 0; m < list.Count; m++)
			{
				list[m].current_position = current_position2 + new Vector2(((float)m - (float)(list.Count - 1) * 0.5f) * 0.85f, 3.4f);
				try
				{
					list[m].makeWait();
					list[m].cancelAllBeh();
					list[m].makeWait();
				}
				catch
				{
				}
			}
			Mod.DebugEyeHeight = 1.1f;
			Mod.DebugEye = current_position2;
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-3f);
			yield return CivilTest.Wait(1.5f);
			Mod.SaveFrame("looks_row.png");
			Mod.DebugEyeSet = false;
		}

		private IEnumerator Reload(int slot, Vector2 middle)
		{
			List<long> ids = new List<long>(_keys.Keys);
			long you = _you.data.id;
			yield return LoadSave(slot);
			Actor actor = World.world.units.get(you);
			if (actor != null && actor.isAlive())
			{
				yield return Possess(actor, 3f);
			}
			List<Actor> again = new List<Actor>();
			foreach (long item in ids)
			{
				Actor actor2 = World.world.units.get(item);
				if (actor2 != null)
				{
					again.Add(actor2);
				}
			}
			yield return CivilTest.Wait(4f);
			yield return Tour(again);
			Mod.DebugEyeSet = false;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (long item2 in ids)
			{
				Actor actor3 = World.world.units.get(item2);
				if (actor3 == null || !actor3.isAlive() || !_looks.TryGetValue(item2, out var b) || !People.LookForTest(actor3, out var a, out var _))
				{
					continue;
				}
				if (_whos.TryGetValue(item2, out var value) && People.LookWhoForTest(actor3, out var who) && who.Age != value.Age)
				{
					num4++;
					Note($"{item2} after the load: the game reads them as {who.Age}, not {value.Age}");
					continue;
				}
				num2++;
				if (OwnSame(in a, in b))
				{
					num++;
				}
				if (a.Key() != b.Key())
				{
					if (OwnSame(in a, in b))
					{
						num3++;
					}
					Note($"{item2} after the load: job {b.Job}->{a.Job} hat {b.Hat}->{a.Hat} face {b.Face}->{a.Face} hair {b.Hair}->{a.Hair} " + $"beard {b.Beard}->{a.Beard} build {b.Build}->{a.Build} aged {b.Aged}->{a.Aged} hurt {b.Patch}{b.Scar}{b.Burns}->{a.Patch}{a.Scar}{a.Burns}");
				}
			}
			Check("after the save is loaded again they look the same", num2 >= 5 && num == num2, string.Format("{0} of {1} the same{2}{3}", num, num2, (num3 > 0) ? $" ({num3} in other work clothes)" : "", (num4 > 0) ? $"; {num4} read as another age" : ""));
		}
	}
}
