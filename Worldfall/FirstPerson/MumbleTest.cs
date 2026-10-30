using System;
using System.Collections;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MumbleTest : CivilTest
	{
		private Actor _you;

		protected override string Prefix => "MUMBLE";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 10, worldTile.y - 10, worldTile.x + 10, worldTile.y + 10);
			_you = CivilTest.Spawn("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (Check("a villager to be", _you != null))
			{
				yield return Possess(_you);
				if (Check("first person", InControl(_you)))
				{
					Mod.DebugEye = _you.current_position;
					Mod.DebugEyeSet = true;
					Mod.SetYaw(0f);
					Mod.Settings.VoicesVolume = 1f;
					Mod.Settings.VoiceStyle = 0;
					FirstPersonAudio audio = Mod.Audio;
					yield return Part("the game's clips", Clips());
					yield return Part("a talk is spoken", Talk(audio));
					yield return Part("people chatting", Chatter(audio));
					yield return Part("the settings", Settings(audio));
					yield return Part("other peoples", Peoples(audio));
					Mod.Settings.VoicesVolume = 1f;
					Mod.Settings.VoiceStyle = 0;
					Mod.DebugEyeSet = false;
				}
			}
		}

		private IEnumerator Clips()
		{
			float t0 = Time.realtimeSinceStartup;
			FirstPersonAudio.WantKindForTest("human");
			while (!FirstPersonAudio.KindTriedForTest("human") && Time.realtimeSinceStartup - t0 < 60f)
			{
				yield return CivilTest.Wait(0.25f);
			}
			Note($"human clips after {Time.realtimeSinceStartup - t0:0.0} s: {FirstPersonAudio.SpeechNote}");
			Check("the game's human talk clips are read and cut", FirstPersonAudio.KindReadyForTest("human"), FirstPersonAudio.SpeechNote);
			Dump();
		}

		private void Dump()
		{
			try
			{
				VoiceSet voiceSet = FirstPersonAudio.KindSetForTest("human");
				if (voiceSet == null)
				{
					return;
				}
				string text = Path.Combine(Log.Folder, "mumble");
				Directory.CreateDirectory(text);
				for (int i = 0; i < voiceSet.Clips.Count; i++)
				{
					Wav(Path.Combine(text, $"clip_{i:00}_{voiceSet.Clips[i].Name}.wav"), voiceSet.Clips[i].S);
				}
				(string, Mumble.Speaker)[] array = new(string, Mumble.Speaker)[6]
				{
					("female_grown", new Mumble.Speaker
					{
						Seed = 11,
						Female = true,
						Pace = 1f
					}),
					("female_young_happy", new Mumble.Speaker
					{
						Seed = 23,
						Female = true,
						Tone = Mumble.Tone.Happy,
						Pace = 1f
					}),
					("female_elder", new Mumble.Speaker
					{
						Seed = 37,
						Female = true,
						Elder = true,
						Pace = 1f
					}),
					("male_grown", new Mumble.Speaker
					{
						Seed = 11,
						Pace = 1f
					}),
					("male_elder", new Mumble.Speaker
					{
						Seed = 37,
						Elder = true,
						Pace = 1f
					}),
					("child", new Mumble.Speaker
					{
						Seed = 5,
						Child = true,
						Pace = 1f
					})
				};
				for (int j = 0; j < array.Length; j++)
				{
					(string, Mumble.Speaker) tuple = array[j];
					string item = tuple.Item1;
					Mumble.Speaker item2 = tuple.Item2;
					Mumble.Phrase phrase = Mumble.Line(voiceSet, item2, "Well met, traveller. The road is long, and the fire is warm. Will you stay a while?", 3.5f);
					if (phrase != null)
					{
						Wav(Path.Combine(text, "voice_" + item + ".wav"), phrase.S);
					}
				}
				Note("clips and voices written to " + text);
			}
			catch (Exception ex)
			{
				Note("dump failed: " + ex.Message);
			}
		}

		private static void Wav(string file, float[] s)
		{
			byte[] array = new byte[s.Length * 2];
			for (int i = 0; i < s.Length; i++)
			{
				short num = (short)Math.Round(((s[i] < -1f) ? (-1f) : ((s[i] > 1f) ? 1f : s[i])) * 32767f);
				array[2 * i] = (byte)(num & 0xFF);
				array[2 * i + 1] = (byte)((num >> 8) & 0xFF);
			}
			using (BinaryWriter binaryWriter = new BinaryWriter(File.Create(file)))
			{
				int num2 = 48000;
				binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
				binaryWriter.Write(36 + array.Length);
				binaryWriter.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
				binaryWriter.Write(16);
				binaryWriter.Write((short)1);
				binaryWriter.Write((short)1);
				binaryWriter.Write(num2);
				binaryWriter.Write(num2 * 2);
				binaryWriter.Write((short)2);
				binaryWriter.Write((short)16);
				binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
				binaryWriter.Write(array.Length);
				binaryWriter.Write(array);
			}
		}

		private IEnumerator Talk(FirstPersonAudio audio)
		{
			Vector2 current_position = _you.current_position;
			Actor npc = CivilTest.Spawn("human", current_position + new Vector2(1.6f, 0.1f));
			if (!Check("someone to talk with", npc != null))
			{
				yield break;
			}
			try
			{
				npc.makeWait(30f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.6f);
			Conversation c = Mod.Converse;
			int said = audio.SpeechSaid;
			if (!Check("a talk opens", Mod.StartConversationForTest(npc) && c.Open))
			{
				CivilTest.Kill(npc);
				yield break;
			}
			float started = -1f;
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				if (!(started < 0f))
				{
					break;
				}
				if (audio.SpeechSaid > said)
				{
					started = c.Age;
				}
				yield return null;
			}
			string text = c.Line ?? "";
			Note($"greeting \"{text}\" ({(float)text.Length / 55f:0.00} s to type): voiced {started >= 0f} at {started:0.00} s into it; " + $"{audio.LastSpeechSyllables} syllables, {audio.LastSpeechSeconds:0.00} s, made in {audio.LastSpeechMs} ms; {FirstPersonAudio.SpeechNote}");
			Check("the greeting is spoken as it types", started >= 0f && started <= 0.3f, $"{started:0.00} s after the text began");
			float typing = (float)text.Length / 55f;
			Check("about as long as its typing", audio.LastSpeechSeconds <= typing + 0.3f && audio.LastSpeechSeconds >= Mathf.Min(typing * 0.6f, 0.15f), $"{audio.LastSpeechSeconds:0.00} s for {typing:0.00} s");
			Vector3 vector = FirstPersonAudio.MouthOf(npc);
			Vector3 lastSpeechAt = audio.LastSpeechAt;
			float num = Vector2.Distance(new Vector2(lastSpeechAt.x, lastSpeechAt.y), new Vector2(vector.x, vector.y));
			Check("from their mouth", num < 0.4f && Mathf.Abs(lastSpeechAt.z - vector.z) < 0.3f, $"{num:0.00} tiles off, {lastSpeechAt.z - vector.z:+0.00;-0.00} in height");
			Check("they're heard", audio.TalkVolumeForTest() > 0f || !audio.TalkSpeaking, $"volume {audio.TalkVolumeForTest():0.000}");
			yield return CivilTest.Wait(0.5f);
			Check("the music and the world dip 3 dB in the talk", audio.DipNow < 0.75f, $"{audio.DipNow:0.000}");
			yield return CivilTest.Wait(Mathf.Max(0f, typing - 0.4f) + 0.4f);
			said = audio.SpeechSaid;
			c.Say("The king's men took the last of the grain. We'll starve before the thaw, mark me. And nobody up at the hall cares one bit.");
			float s1 = -1f;
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				if (!(s1 < 0f))
				{
					break;
				}
				if (audio.SpeechSaid > said)
				{
					s1 = c.Age;
				}
				yield return null;
			}
			Check("a new page, a new line", s1 >= 0f && s1 <= 0.3f, $"{s1:0.00} s, {audio.LastSpeechSyllables} syllables, {audio.LastSpeechSeconds:0.00} s for {(float)c.Line.Length / 55f:0.00} s typed, {audio.LastSpeechMs} ms to make");
			yield return CivilTest.Wait(0.5f);
			bool before = audio.TalkSpeaking;
			c.Next();
			yield return CivilTest.Wait(0.3f);
			Check("clicked through, the voice wraps up", before && !audio.TalkSpeaking, $"speaking before {before}, after {audio.TalkSpeaking}");
			said = audio.SpeechSaid;
			c.SayAsIs("*shrugs*");
			yield return CivilTest.Wait(0.8f);
			Check("a gesture is silent", audio.SpeechSaid == said);
			Mod.EndConversation();
			yield return CivilTest.Wait(1.2f);
			Check("the talk over, the voice stops and the dip lifts", !audio.TalkSpeaking && audio.DipNow > 0.97f, $"dip {audio.DipNow:0.000}");
			CivilTest.Kill(npc);
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Chatter(FirstPersonAudio audio)
		{
			Vector2 current_position = _you.current_position;
			Actor near = CivilTest.Spawn("human", current_position + new Vector2(4f, 0.5f));
			Actor sheep = CivilTest.Spawn("sheep", current_position + new Vector2(-4f, 0.5f));
			if (Check("a villager and a sheep", near != null && sheep != null))
			{
				try
				{
					near.makeWait();
					sheep.makeWait();
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.8f);
				int chat = audio.ChatterSaid;
				near.playIdleSound();
				yield return CivilTest.Wait(0.4f);
				Vector3 at;
				float carry;
				Actor speaker;
				bool flag = audio.Spatial && audio.HeardForTest("human/idle", out at, out carry, out speaker) && speaker == near;
				Check("a villager's chatter is in their own voice", audio.ChatterSaid == chat + 1, $"{audio.ChatterSaid - chat} said");
				Check("the game's own chatter isn't played over it", !flag);
				Vector3 vector = FirstPersonAudio.MouthOf(near);
				Vector3 lastSpeechAt = audio.LastSpeechAt;
				float num = Vector2.Distance(new Vector2(lastSpeechAt.x, lastSpeechAt.y), new Vector2(vector.x, vector.y));
				Check("from their mouth", num < 0.5f, $"{num:0.00} tiles off; {audio.LastSpeechSyllables} syllables, {audio.LastSpeechSeconds:0.00} s");
				Check("as short as the game's (1-3 syllables)", audio.LastSpeechSyllables >= 1 && audio.LastSpeechSyllables <= 3 && audio.LastSpeechSeconds < 1.2f);
				yield return CivilTest.Wait(1.2f);
				chat = audio.ChatterSaid;
				sheep.playIdleSound();
				yield return CivilTest.Wait(0.4f);
				Actor speaker2;
				bool flag2 = !audio.Spatial || (audio.HeardForTest("sheep", out at, out carry, out speaker2) && speaker2 == sheep);
				Check("an animal keeps the game's own call", audio.ChatterSaid == chat && flag2, $"said {audio.ChatterSaid - chat}, the game's heard {flag2}");
				CivilTest.Kill(near);
				CivilTest.Kill(sheep);
				yield return CivilTest.Wait(0.5f);
			}
		}

		private IEnumerator Settings(FirstPersonAudio audio)
		{
			Vector2 me = _you.current_position;
			Actor near = CivilTest.Spawn("human", me + new Vector2(3f, -0.5f));
			if (Check("a villager", near != null))
			{
				try
				{
					near.makeWait(20f);
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.8f);
				Mod.Settings.VoiceStyle = 2;
				yield return null;
				yield return null;
				int chat = audio.ChatterSaid;
				int said = audio.SpeechSaid;
				near.playIdleSound();
				yield return CivilTest.Wait(0.4f);
				Vector3 at;
				float carry;
				Actor speaker;
				bool flag = !audio.Spatial || (audio.HeardForTest("human/idle", out at, out carry, out speaker) && speaker == near);
				Check("off: the game's own chatter comes back", audio.ChatterSaid == chat && flag, $"said {audio.ChatterSaid - chat}, the game's heard {flag}");
				near.current_position = me + new Vector2(1.6f, -0.2f);
				yield return CivilTest.Wait(0.4f);
				Mod.StartConversationForTest(near);
				yield return CivilTest.Wait(1f);
				Check("off: a talk is silent, no dip", audio.SpeechSaid == said && audio.DipNow > 0.97f, $"said {audio.SpeechSaid - said}, dip {audio.DipNow:0.000}");
				Mod.EndConversation();
				yield return CivilTest.Wait(0.5f);
				Mod.Settings.VoiceStyle = 0;
				Mod.Settings.VoicesVolume = 0f;
				yield return null;
				yield return null;
				chat = audio.ChatterSaid;
				near.playIdleSound();
				yield return CivilTest.Wait(0.4f);
				Check("at no volume: no voices made", audio.ChatterSaid == chat);
				Mod.Settings.VoicesVolume = 1f;
				Mod.Settings.VoiceStyle = 1;
				yield return null;
				Mod.StartConversationForTest(near);
				yield return CivilTest.Wait(0.3f);
				said = audio.SpeechSaid;
				Mod.Converse.Say("You again. Well, sit down, then. The fire's warm and the ale is cheap, if you're paying, and you always are.");
				yield return CivilTest.Wait(0.8f);
				Check("opening words: a few syllables of a long line", audio.SpeechSaid == said + 1 && audio.LastSpeechSyllables <= 3 && audio.LastSpeechSeconds < 1f, $"{audio.LastSpeechSyllables} syllables, {audio.LastSpeechSeconds:0.00} s");
				Mod.EndConversation();
				Mod.Settings.VoiceStyle = 0;
				CivilTest.Kill(near);
				yield return CivilTest.Wait(0.5f);
			}
		}

		private IEnumerator Peoples(FirstPersonAudio audio)
		{
			string[] array = new string[4] { "elf", "orc", "dwarf", "civ_cat" };
			foreach (string kind in array)
			{
				Vector2 current_position = _you.current_position;
				Actor npc = CivilTest.Spawn(kind, current_position + new Vector2(1.6f, 0.2f));
				if (npc == null)
				{
					Note(kind + ": couldn't be made here");
					continue;
				}
				try
				{
					npc.makeWait(20f);
				}
				catch
				{
				}
				string idle = ((npc.asset != null) ? npc.asset.sound_idle : "");
				FirstPersonAudio.WantKindForTest(VoiceSets.KindOf(idle));
				float t0 = Time.realtimeSinceStartup;
				while (!FirstPersonAudio.KindTriedForTest(VoiceSets.KindOf(idle) ?? "") && Time.realtimeSinceStartup - t0 < 30f)
				{
					yield return CivilTest.Wait(0.25f);
				}
				yield return CivilTest.Wait(0.3f);
				int said = audio.SpeechSaid;
				Mod.StartConversationForTest(npc);
				yield return CivilTest.Wait(1.2f);
				Check(kind + " speaks in its kind's voice (" + VoiceSets.KindOf(idle) + ")", audio.SpeechSaid > said, $"{audio.LastSpeechSyllables} syllables, {audio.LastSpeechSeconds:0.00} s, {audio.LastSpeechMs} ms; {FirstPersonAudio.SpeechNote}");
				Mod.EndConversation();
				CivilTest.Kill(npc);
				yield return CivilTest.Wait(0.6f);
			}
		}
	}
}
