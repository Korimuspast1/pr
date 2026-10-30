using System;
using System.Collections;
using System.Collections.Generic;
using FMOD;
using FMOD.Studio;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SpatialTest : CivilTest
	{
		private Actor _you;

		private float _frontDb = -999f;

		protected override string Prefix => "SPATIAL";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 8, worldTile.y - 8, worldTile.x + 8, worldTile.y + 8);
			_you = CivilTest.Spawn("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (!Check("a villager", _you != null))
			{
				yield break;
			}
			yield return Possess(_you);
			if (Check("first person", InControl(_you)))
			{
				Mod.DebugEye = _you.current_position;
				Mod.DebugEyeSet = true;
				Mod.SetYaw(0f);
				if (WorldBoxMod.QuietTestCopy)
				{
					WorldBoxMod.QuietLevelForTest = 1E-05f;
				}
				yield return CivilTest.Wait(1f);
				FirstPersonAudio audio = Mod.Audio;
				Check("3D sound is on", audio.Spatial && audio.ListenerOn, FirstPersonAudio.SpatialNote);
				Check("the game's sound calls are taken over", SpatialHooks.Installed);
				if (audio.Spatial)
				{
					yield return Part("our sound around you", Around(audio));
					yield return Part("turning your head turns the sound", Turning(audio));
					yield return Part("a game sound to your right", GameSound(audio));
					FirstPersonAudio.ChatterOffForTest = true;
					yield return Part("a villager's talk", Voices(audio));
					FirstPersonAudio.ChatterOffForTest = false;
					yield return Part("the lava's rumble", Lava(audio));
					yield return Part("a ridge muffles", Ridge(audio));
					yield return Part("the setting turns it off and on", Switch(audio));
					yield return Part("a crowd of game sounds", Crowd(audio));
					WorldBoxMod.QuietLevelForTest = -1f;
					Mod.DebugEyeSet = false;
				}
			}
		}

		private IEnumerator Listen(FirstPersonAudio audio, float seconds, float[] into)
		{
			double l = 0.0;
			double r = 0.0;
			int n = 0;
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (!(t < 0.1f) && audio.EarsForTest(out var left, out var right))
				{
					l += (double)(left * left);
					r += (double)(right * right);
					n++;
				}
			}
			into[0] = Db((n > 0) ? Mathf.Sqrt((float)(l / (double)n)) : 0f);
			into[1] = Db((n > 0) ? Mathf.Sqrt((float)(r / (double)n)) : 0f);
			into[2] = n;
		}

		private static float Db(float x)
		{
			return 20f * Mathf.Log10(Mathf.Max(x, 1E-07f));
		}

		private IEnumerator Around(FirstPersonAudio audio)
		{
			List<KeyValuePair<string, Vector2>> list = new List<KeyValuePair<string, Vector2>>
			{
				new KeyValuePair<string, Vector2>("front", new Vector2(6f, 0f)),
				new KeyValuePair<string, Vector2>("right", new Vector2(0f, -6f)),
				new KeyValuePair<string, Vector2>("back", new Vector2(-6f, 0f)),
				new KeyValuePair<string, Vector2>("left", new Vector2(0f, 6f))
			};
			Dictionary<string, float[]> heard = new Dictionary<string, float[]>();
			foreach (KeyValuePair<string, Vector2> place in list)
			{
				Vector3 earForTest = audio.EarForTest;
				Vector3 at = new Vector3(earForTest.x + place.Value.x, earForTest.y + place.Value.y, earForTest.z);
				Channel channel = audio.PlayAtForTest("rain", at, 0.5f);
				float[] ears = new float[3];
				yield return Listen(audio, 0.7f, ears);
				if (channel.hasHandle())
				{
					channel.stop();
				}
				heard[place.Key] = ears;
				Note($"{place.Key}: left {ears[0]:0.0} dB, right {ears[1]:0.0} dB ({ears[2]:0} readings)");
				yield return CivilTest.Wait(0.25f);
			}
			float[] array = heard["front"];
			float[] array2 = heard["right"];
			float[] array3 = heard["back"];
			float[] array4 = heard["left"];
			Check("heard at all", array[0] > -60f && array[2] > 3f, $"{array[0]:0.0} dB");
			Check("to the right: the right ear louder", array2[1] - array2[0] > 4f, $"{array2[1] - array2[0]:0.0} dB");
			Check("to the left: the left ear louder", array4[0] - array4[1] > 4f, $"{array4[0] - array4[1]:0.0} dB");
			Check("in front: both ears the same", Mathf.Abs(array[0] - array[1]) < 1.5f, $"{array[0] - array[1]:0.0} dB");
			Check("behind: both ears the same", Mathf.Abs(array3[0] - array3[1]) < 1.5f, $"{array3[0] - array3[1]:0.0} dB");
			Check("behind is quieter than in front", (array3[0] + array3[1]) * 0.5f < (array[0] + array[1]) * 0.5f - 1f, $"front {(array[0] + array[1]) * 0.5f:0.0}, behind {(array3[0] + array3[1]) * 0.5f:0.0} dB");
			_frontDb = (array[0] + array[1]) * 0.5f;
		}

		private IEnumerator Turning(FirstPersonAudio audio)
		{
			Vector3 earForTest = audio.EarForTest;
			Vector3 at = new Vector3(earForTest.x + 6f, earForTest.y, earForTest.z);
			Channel channel = audio.PlayAtForTest("rain", at, 0.5f);
			Mod.SetYaw(MathF.PI / 2f);
			float[] ears = new float[3];
			yield return Listen(audio, 0.7f, ears);
			if (channel.hasHandle())
			{
				channel.stop();
			}
			Mod.SetYaw(0f);
			Note($"east, facing north: left {ears[0]:0.0}, right {ears[1]:0.0} dB");
			Check("the sound that was ahead is on your right once you turn left", ears[1] - ears[0] > 4f, $"{ears[1] - ears[0]:0.0} dB");
			yield return CivilTest.Wait(0.25f);
		}

		private IEnumerator GameSound(FirstPersonAudio audio)
		{
			int before = audio.GameSoundsSpatial;
			int native = audio.GameSoundsNative;
			Vector3 ear = audio.EarForTest;
			double l = 0.0;
			double r = 0.0;
			int n = 0;
			for (int i = 0; i < 6; i++)
			{
				MusicBox.playSound("event:/SFX/HIT/HitSwordSword", ear.x, ear.y - 5f);
				for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
				{
					yield return null;
					if (audio.EarsForTest(out var left, out var right))
					{
						l += (double)(left * left);
						r += (double)(right * right);
						n++;
					}
				}
			}
			float num = Db(Mathf.Sqrt((float)(l / (double)Mathf.Max(1, n))));
			float num2 = Db(Mathf.Sqrt((float)(r / (double)Mathf.Max(1, n))));
			Note($"game sword clashes 5 to the right: left {num:0.0}, right {num2:0.0} dB; taken {audio.GameSoundsSpatial - before}, played the old way {audio.GameSoundsNative - native}");
			Check("the game's sounds are taken over", audio.GameSoundsSpatial - before >= 5, $"{audio.GameSoundsSpatial - before} of 6");
			Check("and heard", num2 > -70f, $"{num2:0.0} dB");
			Check("on the right", num2 - num > 3f, $"{num2 - num:0.0} dB");
			Check("Studio's thread kept off the event groups (gate)", StudioGate.Installed && StudioGate.StudioMisses == 0, $"studio misses {StudioGate.StudioMisses}, frame misses {StudioGate.FrameMisses}");
			yield return CivilTest.Wait(1.5f);
			if (audio.GameOneShotsNow > 0)
			{
				Note("still followed: " + audio.GameOneShotsForTest());
			}
			Check("none left held or hanging on", audio.GameOneShotsOlderThan(1f) == 0, $"{audio.GameOneShotsOlderThan(1f)} still followed after a second ({audio.GameOneShotsNow} in all), {audio.GameSoundsNow} with the beds");
			double num3;
			r = (num3 = 0.0);
			l = num3;
			n = 0;
			for (int i = 0; i < 4; i++)
			{
				MusicBox.playSound("event:/SFX/HIT/HitSwordSword", ear.x, ear.y);
				for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
				{
					yield return null;
					if (audio.EarsForTest(out var left2, out var right2))
					{
						l += (double)(left2 * left2);
						r += (double)(right2 * right2);
						n++;
					}
				}
			}
			num = Db(Mathf.Sqrt((float)(l / (double)Mathf.Max(1, n))));
			num2 = Db(Mathf.Sqrt((float)(r / (double)Mathf.Max(1, n))));
			Note($"at your feet: left {num:0.0}, right {num2:0.0} dB");
			Check("a game sound at your feet is heard", num2 > -70f, $"{num2:0.0} dB");
			Check("evenly in both ears", Mathf.Abs(num - num2) < 2f, $"{num - num2:0.0} dB");
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator Voices(FirstPersonAudio audio)
		{
			Check("the creatures' own sounds are followed", SpatialHooks.VoicesInstalled);
			Vector3 earForTest = audio.EarForTest;
			Actor near = CivilTest.Spawn("human", new Vector2(earForTest.x + 3f, earForTest.y + 0.3f));
			Actor far = CivilTest.Spawn("human", new Vector2(earForTest.x + 14f, earForTest.y - 0.4f));
			if (Check("two villagers", near != null && far != null))
			{
				try
				{
					near.makeWait(8f);
					far.makeWait(8f);
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.6f);
				Vector2 p = near.current_position;
				near.playIdleSound();
				yield return null;
				yield return null;
				Vector3 at;
				float nearCarry;
				Actor speaker;
				bool flag = audio.HeardForTest("human/idle", out at, out nearCarry, out speaker);
				float num = Vector2.Distance(new Vector2(at.x, at.y), near.current_position);
				float num2 = ((FirstPersonAudio.GroundAt != null) ? FirstPersonAudio.GroundAt(p.x, p.y) : 0f);
				Note(string.Format("talk 3 ahead: heard {0}, from ({1:0.00}, {2:0.00}) {3:0.00} over the ground, villager at ({4:0.00}, {5:0.00}); carries {6:0.000}; speaker {7}", flag, at.x, at.y, at.z - num2, near.current_position.x, near.current_position.y, nearCarry, (speaker == near) ? "them" : ((speaker == null) ? "none" : "someone else")));
				if (!Check("a villager's talk is taken", flag))
				{
					CivilTest.Kill(near);
					CivilTest.Kill(far);
					yield break;
				}
				Check("heard from the villager", speaker == near && num < 0.6f, $"{num:0.00} tiles off");
				Check("at their mouth, not their feet", at.z - num2 > 0.45f, $"{at.z - num2:0.00} over the ground");
				near.current_position = p + new Vector2(0f, 1.5f);
				yield return CivilTest.Wait(0.5f);
				audio.HeardForTest("human/idle", out var at2, out var _, out var _);
				Hitbox box;
				Vector2 b = (Hitboxes.Get(near, out box) ? new Vector2(box.Centre.X, box.Centre.Y) : near.current_position);
				float num3 = Vector2.Distance(new Vector2(at2.x, at2.y), b);
				float num4 = Vector2.Distance(new Vector2(at2.x, at2.y), new Vector2(at.x, at.y));
				Note($"half a second on: heard from ({at2.x:0.00}, {at2.y:0.00}), {num4:0.00} tiles on; body drawn at ({b.x:0.00}, {b.y:0.00}), the creature at ({near.current_position.x:0.00}, {near.current_position.y:0.00})");
				Check("and follows them as they move", num3 < 0.25f && num4 > 0.4f, $"{num3:0.00} tiles from the body, {num4:0.00} on");
				Check("talk is kept soft even close by", nearCarry > 0.05f && nearCarry <= 0.5f, $"{nearCarry:0.000}");
				yield return CivilTest.Wait(1.2f);
				far.playIdleSound();
				yield return null;
				yield return null;
				audio.HeardForTest("human/idle", out var at3, out var carry2, out var speaker3);
				Note($"talk 14 off: from ({at3.x:0.00}, {at3.y:0.00}), carries {carry2:0.000} ({20f * Mathf.Log10(Mathf.Max(carry2, 1E-05f) / Mathf.Max(nearCarry, 1E-05f)):0.0} dB against the near one)");
				Check("one 14 tiles off is much fainter", speaker3 == far && carry2 > 0f && carry2 < nearCarry * 0.4f, $"{carry2:0.000} against {nearCarry:0.000}");
				CivilTest.Kill(near);
				CivilTest.Kill(far);
				yield return CivilTest.Wait(1.2f);
			}
		}

		private IEnumerator Lava(FirstPersonAudio audio)
		{
			TileType tileType = AssetManager.tiles.get("lava3");
			if (!Check("lava", tileType != null))
			{
				yield break;
			}
			Vector3 earForTest = audio.EarForTest;
			bool wasPaused = Config.paused;
			Config.paused = true;
			List<KeyValuePair<WorldTile, KeyValuePair<TileType, TopTileType>>> saved = new List<KeyValuePair<WorldTile, KeyValuePair<TileType, TopTileType>>>();
			int num = Mathf.FloorToInt(earForTest.x) - 1;
			int num2 = Mathf.FloorToInt(earForTest.y) + 7;
			for (int i = num2; i < num2 + 3; i++)
			{
				for (int j = num; j < num + 3; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null)
					{
						saved.Add(new KeyValuePair<WorldTile, KeyValuePair<TileType, TopTileType>>(tile, new KeyValuePair<TileType, TopTileType>(tile.main_type, tile.top_type)));
						try
						{
							tile.setTileTypes(tileType, null);
						}
						catch (Exception ex)
						{
							Note("lava: " + ex.Message);
						}
					}
				}
			}
			EventInstance bed = default(EventInstance);
			try
			{
				Vector2 at;
				bool flag = audio.NearestLavaForTest(out at);
				float num3 = Vector2.Distance(at, new Vector2(earForTest.x, (float)num2 + 0.5f));
				Note($"lava made: {saved.Count} tiles at ({num}..{num + 2}, {num2}..{num2 + 2}); nearest found {flag} at ({at.x:0.0}, {at.y:0.0})");
				Check("the nearest lava is found", flag && num3 < 1.2f, $"{num3:0.00} tiles from the patch's near edge");
				bed = MusicBox.getNewInstance("event:/SFX/ENVIRONMENT/LavaEnvironment");
				MusicBox.setPan(bed, earForTest.x - 15f, earForTest.y - 10f);
				bed.start();
			}
			catch (Exception ex2)
			{
				Note("lava bed: " + ex2.Message);
			}
			yield return CivilTest.Wait(0.6f);
			Vector3 at2;
			float carry;
			Actor speaker;
			bool flag2 = audio.HeardForTest("LavaEnvironment", out at2, out carry, out speaker);
			audio.NearestLavaForTest(out var at3);
			float num4 = Vector2.Distance(new Vector2(at2.x, at2.y), at3);
			Note($"lava bed: taken {flag2}, heard from ({at2.x:0.0}, {at2.y:0.0}), {num4:0.00} tiles from the nearest lava; carries {carry:0.000}");
			Check("the lava's rumble is taken", flag2);
			Check("heard from the nearest lava, not where the game put it", flag2 && num4 < 0.8f, $"{num4:0.00} tiles off");
			Check("and kept well down", flag2 && carry > 0f && carry < 0.15f, $"{carry:0.000}");
			try
			{
				if (bed.isValid())
				{
					bed.stop(STOP_MODE.IMMEDIATE);
					bed.release();
				}
			}
			catch
			{
			}
			foreach (KeyValuePair<WorldTile, KeyValuePair<TileType, TopTileType>> item in saved)
			{
				try
				{
					item.Key.setTileTypes(item.Value.Key, item.Value.Value);
				}
				catch (Exception ex3)
				{
					Note("lava back: " + ex3.Message);
				}
			}
			Config.paused = wasPaused;
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Ridge(FirstPersonAudio audio)
		{
			Func<float, float, float> groundAt = FirstPersonAudio.GroundAt;
			Vector3 ear = audio.EarForTest;
			float flat = groundAt?.Invoke(ear.x, ear.y) ?? 0f;
			FirstPersonAudio.GroundAt = (float x, float y) => (!(Mathf.Abs(x - (ear.x + 5f)) < 1f)) ? flat : (flat + 4f);
			float num = audio.OcclusionForTest(new Vector3(ear.x + 10f, ear.y, flat + 0.6f));
			float num2 = audio.OcclusionForTest(new Vector3(ear.x, ear.y + 10f, flat + 0.6f));
			float num3 = audio.OcclusionForTest(new Vector3(ear.x + 10f, ear.y, flat + 12f));
			FirstPersonAudio.GroundAt = groundAt;
			Note($"occlusion: behind the ridge {num:0.00}, in the open {num2:0.00}, a dragon high over it {num3:0.00}");
			Check("a sound behind a ridge is muffled", num > 0.4f);
			Check("one in the open isn't", num2 == 0f);
			Check("one high over the ridge isn't", num3 == 0f);
			yield break;
		}

		private IEnumerator Switch(FirstPersonAudio audio)
		{
			Mod.Settings.SpatialSound = false;
			yield return CivilTest.Wait(0.3f);
			Check("off: no Resonance, the game's sounds left alone", !audio.Spatial && FirstPersonAudio.Live == null && !audio.ListenerOn && audio.GameSoundsNow == 0);
			int before = audio.GameSoundsSpatial;
			Vector3 ear = audio.EarForTest;
			MusicBox.playSound("event:/SFX/HIT/HitSwordSword", ear.x + 3f, ear.y);
			yield return CivilTest.Wait(0.3f);
			Check("off: a game sound plays the game's way", audio.GameSoundsSpatial == before && audio.GameSoundsNow == 0);
			Channel channel = audio.PlayAtForTest("rain", new Vector3(ear.x, ear.y - 6f, ear.z), 0.3f);
			Check("off: our sound still plays (panned)", channel.hasHandle() && audio.VoicesNow == 0);
			if (channel.hasHandle())
			{
				channel.stop();
			}
			Mod.Settings.SpatialSound = true;
			yield return CivilTest.Wait(0.3f);
			Check("on again", audio.Spatial && audio.ListenerOn && FirstPersonAudio.Live == audio);
		}

		private IEnumerator Crowd(FirstPersonAudio audio)
		{
			Vector3 ear = audio.EarForTest;
			System.Random rng = new System.Random(5);
			string[] kinds = new string[4] { "event:/SFX/HIT/HitSwordSword", "event:/SFX/HIT/HitFlesh", "event:/SFX/HIT/HitWood", "event:/SFX/HIT/HitMetal" };
			int most = 0;
			float worstMs = 0f;
			for (int frame = 0; frame < 30; frame++)
			{
				for (int i = 0; i < 2; i++)
				{
					float f = (float)(rng.NextDouble() * 3.1415927410125732 * 2.0);
					float num = 2f + (float)rng.NextDouble() * 25f;
					MusicBox.playSound(kinds[rng.Next(kinds.Length)], ear.x + Mathf.Cos(f) * num, ear.y + Mathf.Sin(f) * num);
				}
				yield return null;
				most = Mathf.Max(most, audio.GameSoundsNow);
				worstMs = Mathf.Max(worstMs, audio.SpatialMs);
			}
			float waited;
			for (waited = 0f; waited < 6f; waited += Time.unscaledDeltaTime)
			{
				if (audio.GameOneShotsNow <= 0)
				{
					break;
				}
				yield return null;
			}
			Note($"all over {waited:0.0} s after the last");
			Note($"60 game sounds over 30 frames: at most {most} followed at once, {audio.SpatialMs:0.000} ms a frame now, worst {worstMs:0.000} ms");
			Check("within the limit", most <= 96, most.ToString());
			Check("cheap", worstMs < 1.5f, $"{worstMs:0.000} ms");
			if (audio.GameOneShotsNow > 0)
			{
				Note("still followed: " + audio.GameOneShotsForTest());
			}
			Check("and gone once they're over", audio.GameOneShotsNow == 0, $"{audio.GameOneShotsNow} ({audio.GameSoundsNow} with the beds)");
		}
	}
}
