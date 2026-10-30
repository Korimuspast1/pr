using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class WonderLook
	{
		public enum Mote : byte
		{
			Spores,
			Bubbles,
			Embers,
			Sparkles,
			Drips,
			Snow,
			Notes,
			Sparks,
			Smoke
		}

		public struct Emitter
		{
			public Mote Kind;

			public Vec3 At;

			public float Radius;

			public uint Color;

			public int Count;
		}

		public enum LightMode : byte
		{
			Steady,
			Flicker,
			Pulse,
			Beat
		}

		public sealed class Fx
		{
			public int[] Look;

			public int[] Scan;

			public int[] Lids;

			public int[] Beats;

			public int[] Breathes;

			public int[] Sways;

			public int[] Hovers;

			public int[] Floats;

			public int[] Spins;

			public int[] Flames;

			public int[] Faces;

			public string[] Moods;

			public int[] PulseSlots;

			public int[] FlickerSlots;

			public int[] ShimmerSlots;

			public int[] BeatSlots;

			public int[] LedSlots;

			public float LookYaw;

			public float LookPitch;

			public float ScanYaw;

			public float ScanRange;

			public float Sway;

			public float HoverBob;

			public float HoverTurn;

			public float[] SwayOf;

			public float[] SpinRate;

			public Vec3[] LightAt;

			public uint[] LightColor;

			public float[] LightRadius;

			public LightMode[] LightMode;

			public Emitter[] Motes;

			public bool HasBurst;

			public Vec3 Burst;

			public uint BurstColor;

			public uint Mist;

			public string Sound;

			public string PulseSound;

			public bool Posed;

			public bool Coloured;

			public bool Any;
		}

		public sealed class State
		{
			public float[] Yaw;

			public float[] Pitch;

			public int Mood = -1;

			public float MoodUntil;

			public float BlinkAt = -1f;

			public float Last = float.NaN;

			public float PulseAt = -1E+09f;

			public bool Near;

			public uint Seed;
		}

		public const float BurstTime = 2.2f;

		private static readonly Dictionary<MeshModel, Fx> Cache = new Dictionary<MeshModel, Fx>();

		private static readonly SpriteImage[] Notes = new SpriteImage[2];

		public static Fx Of(MeshModel m)
		{
			if (m == null)
			{
				return null;
			}
			lock (Cache)
			{
				if (Cache.TryGetValue(m, out var value))
				{
					return value;
				}
				value = Read(m);
				if (Cache.Count > 400)
				{
					Cache.Clear();
				}
				Cache[m] = value;
				return value;
			}
		}

		private static Fx Read(MeshModel m)
		{
			Fx fx = new Fx();
			List<int> list = new List<int>();
			List<int> list2 = new List<int>();
			List<int> list3 = new List<int>();
			List<int> list4 = new List<int>();
			List<int> list5 = new List<int>();
			List<int> list6 = new List<int>();
			List<int> list7 = new List<int>();
			List<int> list8 = new List<int>();
			List<int> list9 = new List<int>();
			List<int> list10 = new List<int>();
			List<int> list11 = new List<int>();
			List<string> list12 = new List<string>();
			for (int i = 0; i < m.Parts.Length; i++)
			{
				string text = m.Parts[i].Name ?? "";
				if (text.StartsWith("look", StringComparison.Ordinal))
				{
					list.Add(i);
				}
				else if (text.StartsWith("scan", StringComparison.Ordinal))
				{
					list2.Add(i);
				}
				else if (text.StartsWith("lid", StringComparison.Ordinal))
				{
					list3.Add(i);
				}
				else if (text.StartsWith("beat", StringComparison.Ordinal))
				{
					list4.Add(i);
				}
				else if (text.StartsWith("breathe", StringComparison.Ordinal))
				{
					list5.Add(i);
				}
				else if (text.StartsWith("sway", StringComparison.Ordinal))
				{
					list6.Add(i);
				}
				else if (text.StartsWith("hover", StringComparison.Ordinal))
				{
					list7.Add(i);
				}
				else if (text.StartsWith("float", StringComparison.Ordinal))
				{
					list8.Add(i);
				}
				else if (text.StartsWith("spin", StringComparison.Ordinal))
				{
					list9.Add(i);
				}
				else if (text.StartsWith("flames", StringComparison.Ordinal))
				{
					list10.Add(i);
				}
				else if (text.StartsWith("face_", StringComparison.Ordinal))
				{
					list11.Add(i);
					list12.Add(text.Substring(5));
				}
			}
			fx.Look = list.ToArray();
			fx.Scan = list2.ToArray();
			fx.Lids = list3.ToArray();
			fx.Beats = list4.ToArray();
			fx.Breathes = list5.ToArray();
			fx.Sways = list6.ToArray();
			fx.Hovers = list7.ToArray();
			fx.Floats = list8.ToArray();
			fx.Spins = list9.ToArray();
			fx.Flames = list10.ToArray();
			fx.Faces = list11.ToArray();
			fx.Moods = list12.ToArray();
			fx.Posed = m.Parts.Length > 1 && list.Count + list2.Count + list3.Count + list4.Count + list5.Count + list6.Count + list7.Count + list8.Count + list9.Count + list10.Count + list11.Count > 0;
			List<int> list13 = new List<int>();
			List<int> list14 = new List<int>();
			List<int> list15 = new List<int>();
			List<int> list16 = new List<int>();
			List<int> list17 = new List<int>();
			for (int j = 0; j < m.SlotNames.Length; j++)
			{
				string text2 = m.SlotNames[j] ?? "";
				if (text2.StartsWith("pulse", StringComparison.Ordinal))
				{
					list13.Add(j);
				}
				else if (text2.StartsWith("flicker", StringComparison.Ordinal))
				{
					list14.Add(j);
				}
				else if (text2.StartsWith("shimmer", StringComparison.Ordinal))
				{
					list15.Add(j);
				}
				else if (text2.StartsWith("beatglow", StringComparison.Ordinal))
				{
					list16.Add(j);
				}
				else if (text2.StartsWith("led", StringComparison.Ordinal))
				{
					list17.Add(j);
				}
			}
			list15.Sort((int a, int b) => string.CompareOrdinal(m.SlotNames[a], m.SlotNames[b]));
			fx.PulseSlots = list13.ToArray();
			fx.FlickerSlots = list14.ToArray();
			fx.ShimmerSlots = list15.ToArray();
			fx.BeatSlots = list16.ToArray();
			fx.LedSlots = list17.ToArray();
			fx.Coloured = list13.Count + list14.Count + list15.Count + list16.Count + list17.Count > 0;
			fx.LookYaw = Deg(m.InfoFloat("look_yaw", 35f));
			fx.LookPitch = Deg(m.InfoFloat("look_pitch", 20f));
			fx.ScanYaw = Deg(m.InfoFloat("scan_yaw", 55f));
			fx.ScanRange = m.InfoFloat("scan_range", 14f);
			fx.Sway = m.InfoFloat("sway", 0.06f);
			fx.HoverBob = m.InfoFloat("hover_bob", 0.25f);
			fx.HoverTurn = m.InfoFloat("hover_turn", 0.2f);
			fx.SwayOf = new float[fx.Sways.Length];
			for (int num = 0; num < fx.Sways.Length; num++)
			{
				fx.SwayOf[num] = m.InfoFloat(Group(m.Parts[fx.Sways[num]].Name), fx.Sway);
			}
			fx.SpinRate = new float[fx.Spins.Length];
			for (int num2 = 0; num2 < fx.Spins.Length; num2++)
			{
				fx.SpinRate[num2] = m.InfoFloat(Group(m.Parts[fx.Spins[num2]].Name), (num2 % 2 == 0) ? 0.45f : (-0.3f));
			}
			List<Vec3> list18 = new List<Vec3>();
			List<uint> list19 = new List<uint>();
			List<float> list20 = new List<float>();
			List<LightMode> list21 = new List<LightMode>();
			foreach (string[] item in Fields(m.Info("light")))
			{
				if (item.Length >= 5)
				{
					list18.Add(new Vec3(F(item[0]), F(item[1]), F(item[2])));
					list19.Add(Hex(item[3]));
					list20.Add(F(item[4]));
					list21.Add((item.Length > 5) ? ((item[5] == "flicker") ? LightMode.Flicker : ((item[5] == "pulse") ? LightMode.Pulse : ((item[5] == "beat") ? LightMode.Beat : LightMode.Steady))) : LightMode.Steady);
				}
			}
			if (fx.Flames.Length != 0)
			{
				Vec3 vec = new Vec3(0f, 0f, 0f);
				int[] flames = fx.Flames;
				foreach (int num4 in flames)
				{
					vec += m.Parts[num4].Pivot;
				}
				vec *= 1f / (float)fx.Flames.Length;
				float num5 = 0f;
				flames = fx.Flames;
				foreach (int num6 in flames)
				{
					Vec3 vec2 = m.Parts[num6].Pivot - vec;
					num5 = Math.Max(num5, (float)Math.Sqrt(Vec3.Dot(vec2, vec2)));
				}
				list18.Add(vec + new Vec3(0f, 0f, 0.3f));
				list19.Add(TerrainCache.Rgb(255, 150, 60));
				list20.Add(Math.Min(9f, 4f + num5));
				list21.Add(LightMode.Flicker);
			}
			fx.LightAt = list18.ToArray();
			fx.LightColor = list19.ToArray();
			fx.LightRadius = list20.ToArray();
			fx.LightMode = list21.ToArray();
			List<Emitter> list22 = new List<Emitter>();
			foreach (string[] item2 in Fields(m.Info("motes")))
			{
				if (item2.Length >= 7 && Enum.TryParse<Mote>(item2[0], ignoreCase: true, out var result))
				{
					list22.Add(new Emitter
					{
						Kind = result,
						At = new Vec3(F(item2[1]), F(item2[2]), F(item2[3])),
						Radius = F(item2[4]),
						Color = Hex(item2[5]),
						Count = Math.Max(1, Math.Min(64, (int)F(item2[6])))
					});
				}
			}
			fx.Motes = list22.ToArray();
			foreach (string[] item3 in Fields(m.Info("burst")))
			{
				if (item3.Length >= 4)
				{
					fx.HasBurst = true;
					fx.Burst = new Vec3(F(item3[0]), F(item3[1]), F(item3[2]));
					fx.BurstColor = Hex(item3[3]);
					break;
				}
			}
			string text3 = m.Info("mist");
			fx.Mist = ((!string.IsNullOrEmpty(text3)) ? Hex(text3) : 0u);
			fx.Sound = m.Info("sound");
			fx.PulseSound = m.Info("pulse_sound");
			fx.Any = fx.Posed || fx.Coloured || fx.LightAt.Length != 0 || fx.Motes.Length != 0 || fx.HasBurst || fx.Mist != 0 || fx.Sound != null;
			return fx;
		}

		private static float Deg(float d)
		{
			return d * (MathF.PI / 180f);
		}

		private static string Group(string name)
		{
			int num = name.Length;
			while (num > 0 && (char.IsDigit(name[num - 1]) || name[num - 1] == '_'))
			{
				num--;
			}
			return name.Substring(0, num);
		}

		private static IEnumerable<string[]> Fields(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				yield break;
			}
			string[] array = s.Split('/');
			foreach (string text in array)
			{
				if (text.Length > 0)
				{
					yield return text.Split(',');
				}
			}
		}

		private static float F(string s)
		{
			if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return 0f;
			}
			return result;
		}

		private static uint Hex(string s)
		{
			s = (s ?? "").Trim().TrimStart('#');
			if (s.Length < 6 || !uint.TryParse(s.Substring(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
			{
				return TerrainCache.Rgb(255, 255, 255);
			}
			return TerrainCache.Rgb((int)((result >> 16) & 0xFF), (int)((result >> 8) & 0xFF), (int)(result & 0xFF));
		}

		public static float Heart(float t, float seed)
		{
			float u = Frac(t / 0.86f + seed);
			float num = Bump(u, 0f, 0.11f);
			float num2 = Bump(u, 0.24f, 0.1f);
			return Math.Min(1f, num + 0.6f * num2);
		}

		private static float Bump(float u, float at, float width)
		{
			float num = (u - at) / width;
			if (num < 0f || num > 1f)
			{
				return 0f;
			}
			return (float)Math.Sin((double)num * Math.PI);
		}

		public static float Candle(float t, float seed)
		{
			return 0.82f + 0.1f * (float)Math.Sin(t * 9.1f + seed) + 0.06f * (float)Math.Sin(t * 23.7f + seed * 2.3f) + 0.05f * (TerrainModel.ValueNoise(t * 6f, seed) - 0.5f) * 2f;
		}

		public static float BurstStrength(float age)
		{
			if (age < 0f || age > 2.2f)
			{
				return 0f;
			}
			if (age < 0.12f)
			{
				return age / 0.12f;
			}
			return (float)Math.Pow(1f - (age - 0.12f) / 2.0800002f, 1.600000023841858);
		}

		private static float Frac(float v)
		{
			return v - (float)Math.Floor(v);
		}

		private static float Hash01(uint seed, int k)
		{
			return (float)(TerrainModel.Hash((int)seed, k * 7919 + 13) & 0xFFFF) / 65535f;
		}

		public static void Apply(MeshInstance inst, State st, float t, float night, Vec3 eye)
		{
			MeshModel model = inst.Model;
			Fx fx = Of(model);
			if (fx == null || !fx.Any)
			{
				return;
			}
			float num = (float.IsNaN(st.Last) ? 1f : Math.Max(0f, Math.Min(0.5f, t - st.Last)));
			st.Last = t;
			float num2 = (float)(st.Seed & 0x3FF) * 0.37f;
			float num3 = t - st.PulseAt;
			float num4 = BurstStrength(num3);
			Vec3 target = ToModel(inst, eye);
			float num5 = ScaleOf(inst);
			float num6 = (float)Math.Sqrt(target.X * target.X + target.Y * target.Y) * num5;
			if (fx.Posed)
			{
				inst.EnsurePose();
				inst.ResetPose();
				int num7 = model.Parts.Length;
				if (st.Yaw == null || st.Yaw.Length != num7)
				{
					st.Yaw = new float[num7];
					st.Pitch = new float[num7];
				}
				float num8 = 1f - (float)Math.Exp((0f - num) * 3.2f);
				int[] look = fx.Look;
				foreach (int num9 in look)
				{
					Aim(model.Parts[num9].Pivot, target, out var yaw, out var pitch);
					yaw = Clamp(yaw, fx.LookYaw);
					pitch = Clamp(pitch, fx.LookPitch);
					if (num6 > 40f)
					{
						yaw = 0.15f * (float)Math.Sin(t * 0.3f + num2);
						pitch = 0f;
					}
					st.Yaw[num9] += (yaw - st.Yaw[num9]) * num8;
					st.Pitch[num9] += (pitch - st.Pitch[num9]) * num8;
					inst.PartRotation[num9] = Mat3.RotZ(st.Yaw[num9]) * Mat3.RotX(st.Pitch[num9]);
				}
				look = fx.Scan;
				foreach (int num10 in look)
				{
					float yaw2;
					float pitch2;
					if (num6 < fx.ScanRange)
					{
						Aim(model.Parts[num10].Pivot, target, out yaw2, out pitch2);
						pitch2 = Clamp(pitch2, 0.35f);
					}
					else
					{
						float value = (float)Math.Sin(t * 0.55f + num2 + (float)num10);
						yaw2 = fx.ScanYaw * (float)Math.Sign(value) * (float)Math.Pow(Math.Abs(value), 0.6000000238418579);
						pitch2 = -0.06f + 0.04f * (float)Math.Sin(t * 0.37f + num2);
					}
					float num11 = 1f - (float)Math.Exp((0f - num) * ((num6 < fx.ScanRange) ? 4.5f : 2f));
					st.Yaw[num10] += Wrap(yaw2 - st.Yaw[num10]) * num11;
					st.Pitch[num10] += (pitch2 - st.Pitch[num10]) * num11;
					inst.PartRotation[num10] = Mat3.RotZ(st.Yaw[num10]) * Mat3.RotX(st.Pitch[num10]);
				}
				for (int j = 0; j < fx.Lids.Length; j++)
				{
					uint num12 = TerrainModel.Hash((int)st.Seed + j * 31, 77);
					float num13 = 2.6f + (float)(num12 & 0x3FF) / 1023f * 3.5f;
					float num14 = Frac(t / num13 + (float)((num12 >> 10) & 0x3FF) / 1023f) * num13;
					if (!(num14 < 0.14f) && (!(num14 > 0.32f) || !(num14 < 0.42f) || (num12 & 0x100000) == 0))
					{
						inst.Hide(model.Parts[fx.Lids[j]].Name);
					}
					else
					{
						inst.Hide(model.Parts[fx.Lids[j]].Name, hide: false);
					}
				}
				float num15 = Heart(t, num2);
				look = fx.Beats;
				foreach (int num16 in look)
				{
					inst.PartScale[num16] = 1f + 0.055f * num15 + 0.05f * num4;
				}
				for (int k = 0; k < fx.Breathes.Length; k++)
				{
					inst.PartScale[fx.Breathes[k]] = 1f + 0.032f * (float)Math.Sin(t * 1.9f + num2 + (float)k * 1.37f);
				}
				for (int l = 0; l < fx.Sways.Length; l++)
				{
					float num17 = num2 + (float)l * 2.17f;
					float num18 = fx.SwayOf[l] * (1f + 0.6f * num4);
					inst.PartRotation[fx.Sways[l]] = Mat3.RotX(num18 * (float)Math.Sin(t * 0.83f + num17)) * Mat3.RotY(num18 * 0.8f * (float)Math.Sin(t * 0.61f + num17 * 1.3f));
				}
				for (int m = 0; m < fx.Hovers.Length; m++)
				{
					int num19 = fx.Hovers[m];
					inst.PartOffset[num19] = new Vec3(0f, 0f, fx.HoverBob * (float)Math.Sin(t * 0.8f + num2 + (float)m * 1.9f) + 0.6f * fx.HoverBob * num4);
					inst.PartRotation[num19] = Mat3.RotZ(t * fx.HoverTurn * ((m % 2 == 0) ? 1f : (-1.3f)) + num2);
				}
				for (int n = 0; n < fx.Floats.Length; n++)
				{
					inst.PartOffset[fx.Floats[n]] = new Vec3(0f, 0f, 0.14f * (float)Math.Sin(t * 1.1f + num2 + (float)n * 2.1f));
				}
				for (int num20 = 0; num20 < fx.Spins.Length; num20++)
				{
					inst.PartRotation[fx.Spins[num20]] = Mat3.RotZ((t * fx.SpinRate[num20] + num2 + (float)num20) * (1f + 2f * num4));
				}
				for (int num21 = 0; num21 < fx.Flames.Length; num21++)
				{
					int num22 = fx.Flames[num21];
					float num23 = num2 + (float)num21 * 1.7f;
					inst.PartScale[num22] = 0.9f + 0.1f * (float)Math.Sin(t * 9.3f + num23) + 0.06f * (float)Math.Sin(t * 14.1f + num23 * 2f);
					inst.PartRotation[num22] = Mat3.RotZ(t * 0.7f + num23) * Mat3.RotX(0.05f * (float)Math.Sin(t * 5.1f + num23));
				}
				if (fx.Faces.Length != 0)
				{
					Faces(inst, fx, st, t, night, num6, num3);
				}
			}
			if (fx.Coloured)
			{
				if (inst.Palette == null || inst.Palette.Length != model.SlotNames.Length)
				{
					inst.Palette = new uint[model.SlotNames.Length];
				}
				uint[] palette = inst.Palette;
				float num24 = 1f + 0.55f * num4;
				for (int num25 = 0; num25 < fx.PulseSlots.Length; num25++)
				{
					int num26 = fx.PulseSlots[num25];
					palette[num26] = TerrainCache.Scale(model.SlotColor[num26], (0.72f + 0.28f * (float)Math.Sin(t * 2.4f + num2 + (float)num25 * 0.9f)) * num24);
				}
				for (int num27 = 0; num27 < fx.FlickerSlots.Length; num27++)
				{
					int num28 = fx.FlickerSlots[num27];
					palette[num28] = TerrainCache.Scale(model.SlotColor[num28], Candle(t, num2 + (float)num27 * 3.1f) * num24);
				}
				int num29 = fx.ShimmerSlots.Length;
				for (int num30 = 0; num30 < num29; num30++)
				{
					int num31 = fx.ShimmerSlots[num30];
					float num32 = Frac(t * 0.23f + num2) * ((float)num29 + 6f) - 3f;
					float num33 = (float)Math.Exp((0f - ((float)num30 - num32)) * ((float)num30 - num32) * 0.5f);
					if (num3 >= 0f && num3 < 1.6f)
					{
						float num34 = ((num3 < 0.8f) ? (num3 / 0.8f * (float)(num29 - 1)) : ((1.6f - num3) / 0.8f * (float)(num29 - 1)));
						num33 = Math.Max(num33, (float)Math.Exp((0f - ((float)num30 - num34)) * ((float)num30 - num34) * 0.8f) * 1.2f);
					}
					palette[num31] = TerrainCache.Scale(model.SlotColor[num31], 0.62f + 0.5f * num33 + 0.2f * num4);
				}
				float num35 = Heart(t, num2);
				for (int num36 = 0; num36 < fx.BeatSlots.Length; num36++)
				{
					int num37 = fx.BeatSlots[num36];
					palette[num37] = TerrainCache.Scale(model.SlotColor[num37], (0.58f + 0.48f * num35) * num24);
				}
				for (int num38 = 0; num38 < fx.LedSlots.Length; num38++)
				{
					int num39 = fx.LedSlots[num38];
					float num40 = 0.7f + Hash01(st.Seed, num38) * 2.2f;
					bool flag = (TerrainModel.Hash((int)Math.Floor(t * num40) + num38 * 101, (int)st.Seed) & 3) != 0;
					palette[num39] = (flag ? TerrainCache.Scale(model.SlotColor[num39], num24) : TerrainCache.Scale(model.SlotColor[num39], 0.22f));
				}
			}
			inst.Flash = ((num4 > 0f && fx.HasBurst) ? ((fx.BurstColor & 0xFFFFFF) | ((uint)(num4 * 22f) << 24)) : 0u);
		}

		private static void Faces(MeshInstance inst, Fx fx, State st, float t, float night, float eyeDist, float pulseAge)
		{
			MeshModel model = inst.Model;
			bool flag = eyeDist < 9f;
			if (flag && !st.Near && Find("surprised") >= 0)
			{
				st.Mood = Find("surprised");
				st.MoodUntil = t + 1.1f;
			}
			st.Near = flag;
			if (pulseAge >= 0f && pulseAge < 0.1f && Find("angry") >= 0)
			{
				st.Mood = Find("angry");
				st.MoodUntil = t + 1.8f;
			}
			if (st.Mood < 0 || t >= st.MoodUntil)
			{
				uint num = TerrainModel.Hash((int)(t * 3f), (int)st.Seed);
				int num2 = Find("grin");
				int num3 = Find("happy");
				int num4 = Find("angry");
				int num5 = Find("sleep");
				if (night > 0.6f && !flag && num5 >= 0)
				{
					st.Mood = num5;
				}
				else
				{
					int num6 = (int)(num % 10);
					st.Mood = ((num6 < 5 && num2 >= 0) ? num2 : ((num6 < 8 && num3 >= 0) ? num3 : ((num4 >= 0) ? num4 : 0)));
				}
				if (st.Mood < 0)
				{
					st.Mood = 0;
				}
				st.MoodUntil = t + 4f + (float)((num >> 8) & 0x3FF) / 1023f * 6f;
			}
			if (st.Mood >= fx.Moods.Length)
			{
				st.Mood = 0;
			}
			int num7 = st.Mood;
			int num8 = Find("blink");
			if (num8 >= 0 && fx.Moods[num7] != "sleep" && Frac(t / 3.7f + (float)(st.Seed & 0xFF) / 255f) * 3.7f < 0.13f)
			{
				num7 = num8;
			}
			for (int i = 0; i < fx.Faces.Length; i++)
			{
				inst.Hide(model.Parts[fx.Faces[i]].Name, i != num7);
			}
			int Find(string mood)
			{
				for (int j = 0; j < fx.Moods.Length; j++)
				{
					if (fx.Moods[j] == mood)
					{
						return j;
					}
				}
				return -1;
			}
		}

		private static float Clamp(float a, float most)
		{
			if (!(a < 0f - most))
			{
				if (!(a > most))
				{
					return a;
				}
				return most;
			}
			return 0f - most;
		}

		private static float Wrap(float a)
		{
			while ((double)a > Math.PI)
			{
				a -= MathF.PI * 2f;
			}
			while ((double)a < -Math.PI)
			{
				a += MathF.PI * 2f;
			}
			return a;
		}

		private static void Aim(Vec3 pivot, Vec3 target, out float yaw, out float pitch)
		{
			float num = target.X - pivot.X;
			float num2 = target.Y - pivot.Y;
			float num3 = target.Z - pivot.Z;
			yaw = (float)Math.Atan2(num, 0f - num2);
			float val = (float)Math.Sqrt(num * num + num2 * num2);
			pitch = 0f - (float)Math.Atan2(num3, Math.Max(0.01f, val));
		}

		public static Vec3 ToModel(MeshInstance inst, Vec3 world)
		{
			Vec3 a = world - inst.Position;
			Mat3 basis = inst.Basis;
			float val = Vec3.Dot(basis.C0, basis.C0);
			float val2 = Vec3.Dot(basis.C1, basis.C1);
			float val3 = Vec3.Dot(basis.C2, basis.C2);
			return new Vec3(Vec3.Dot(a, basis.C0) / Math.Max(1E-06f, val), Vec3.Dot(a, basis.C1) / Math.Max(1E-06f, val2), Vec3.Dot(a, basis.C2) / Math.Max(1E-06f, val3));
		}

		public static float ScaleOf(MeshInstance inst)
		{
			return (float)Math.Sqrt(Vec3.Dot(inst.Basis.C2, inst.Basis.C2));
		}

		public static void Emit(MeshInstance inst, State st, float t, float night, float ground, bool near, List<Billboard> bbs, List<PointLight> lights)
		{
			Fx fx = Of(inst.Model);
			if (fx == null || !fx.Any)
			{
				return;
			}
			float num = ScaleOf(inst);
			float num2 = (float)(st.Seed & 0x3FF) * 0.37f;
			float age = t - st.PulseAt;
			float num3 = BurstStrength(age);
			float num4 = Heart(t, num2);
			for (int i = 0; i < fx.LightAt.Length; i++)
			{
				Vec3 vec = World(inst, fx.LightAt[i]);
				uint num5 = fx.LightColor[i];
				float num6 = 0.22f + 0.78f * night;
				switch (fx.LightMode[i])
				{
				case LightMode.Flicker:
					num6 *= Candle(t, num2 + (float)i * 1.3f);
					break;
				case LightMode.Pulse:
					num6 *= 0.7f + 0.3f * (float)Math.Sin(t * 2.4f + num2 + (float)i);
					break;
				case LightMode.Beat:
					num6 *= 0.6f + 0.5f * num4;
					break;
				}
				num6 *= 1f + 0.6f * num3;
				lights.Add(new PointLight
				{
					X = vec.X,
					Y = vec.Y,
					Z = vec.Z,
					Radius = Math.Max(1.5f, Math.Min(14f, fx.LightRadius[i] * num)),
					R = (float)(num5 & 0xFF) / 255f * num6,
					G = (float)((num5 >> 8) & 0xFF) / 255f * num6,
					B = (float)((num5 >> 16) & 0xFF) / 255f * num6,
					Glow = 0.06f * num6
				});
			}
			if (fx.HasBurst && num3 > 0f)
			{
				Burst(inst, fx, st, age, num3, ground, bbs, lights);
			}
			if (near)
			{
				for (int j = 0; j < fx.Motes.Length; j++)
				{
					Motes(inst, fx.Motes[j], st.Seed + (uint)(j * 7919), t, night, num, ground, bbs);
				}
				if (fx.Mist != 0)
				{
					Mist(inst, fx.Mist, st.Seed, t, night, num, ground, bbs);
				}
			}
		}

		public static Vec3 World(MeshInstance inst, Vec3 local)
		{
			return inst.Basis.Mul(local) + inst.Position;
		}

		private static void Burst(MeshInstance inst, Fx fx, State st, float age, float k, float ground, List<Billboard> bbs, List<PointLight> lights)
		{
			float num = ScaleOf(inst);
			Vec3 vec = World(inst, fx.Burst);
			uint burstColor = fx.BurstColor;
			float num2 = Math.Max(2f, inst.Model.Max.Z * num);
			lights.Add(new PointLight
			{
				X = vec.X,
				Y = vec.Y,
				Z = vec.Z + 0.5f,
				Radius = 4f + 6f * k,
				R = (float)(burstColor & 0xFF) / 255f * 0.5f * k,
				G = (float)((burstColor >> 8) & 0xFF) / 255f * 0.5f * k,
				B = (float)((burstColor >> 16) & 0xFF) / 255f * 0.5f * k,
				Glow = 0.4f * k
			});
			float width = (0.6f + 1.4f * k) * Math.Max(1f, num);
			bbs.Add(new Billboard
			{
				X = vec.X,
				Y = vec.Y,
				Z = vec.Z - 0.2f,
				GroundZ = ground,
				Width = width,
				Height = num2 * (1.3f + 0.8f * (1f - k)) + 6f,
				Image = BlastFx.GlowSprite,
				Kind = BillboardKind.Facing,
				Tag = -1,
				Emissive = true,
				Additive = true,
				Multiply = burstColor,
				Alpha = (byte)Math.Max(1, Math.Min(255, (int)(150f * k)))
			});
			bbs.Add(new Billboard
			{
				X = vec.X,
				Y = vec.Y,
				Z = vec.Z - 1.2f * (0.5f + k),
				GroundZ = ground,
				Width = 2.4f * (0.5f + k) * Math.Max(1f, num),
				Height = 2.4f * (0.5f + k) * Math.Max(1f, num),
				Image = BlastFx.GlowSprite,
				Kind = BillboardKind.Facing,
				Tag = -1,
				Emissive = true,
				Additive = true,
				Multiply = burstColor,
				Alpha = (byte)Math.Max(1, Math.Min(255, (int)(200f * k)))
			});
			float num3 = (1.2f + age * 5.5f) * Math.Max(1f, num * 0.8f);
			int num4 = 28;
			byte alpha = (byte)Math.Max(1, Math.Min(255, (int)(190f * k)));
			for (int i = 0; i < num4; i++)
			{
				float num5 = (float)i * (float)(Math.PI * 2.0 / (double)num4) + (float)st.Seed * 0.01f;
				float x = vec.X + (float)Math.Cos(num5) * num3;
				float y = vec.Y + (float)Math.Sin(num5) * num3;
				float num6 = 0.55f + 0.35f * Hash01(st.Seed, i);
				bbs.Add(new Billboard
				{
					X = x,
					Y = y,
					Z = ground + 0.05f,
					GroundZ = ground,
					Width = num6,
					Height = num6 * 0.7f,
					Image = BlastFx.GlowSprite,
					Kind = BillboardKind.Facing,
					Tag = -1,
					Emissive = true,
					Additive = true,
					Multiply = burstColor,
					Alpha = alpha
				});
			}
			for (int j = 0; j < 18; j++)
			{
				float num7 = Hash01(st.Seed + 5, j) * (MathF.PI * 2f);
				float num8 = 1.5f + 2.5f * Hash01(st.Seed + 9, j);
				float num9 = 3f + 4f * Hash01(st.Seed + 11, j);
				float num10 = Math.Min(age, 1.6f);
				float x2 = vec.X + (float)Math.Cos(num7) * num8 * num10;
				float y2 = vec.Y + (float)Math.Sin(num7) * num8 * num10;
				float num11 = vec.Z + num9 * num10 - 4.9f * num10 * num10 * 0.6f;
				if (!(num11 < ground))
				{
					bbs.Add(new Billboard
					{
						X = x2,
						Y = y2,
						Z = num11,
						GroundZ = ground,
						Width = 0.12f,
						Height = 0.12f,
						Image = BlastFx.Spark(2),
						Kind = BillboardKind.Facing,
						Tag = -1,
						Emissive = true,
						Multiply = TerrainCache.Lerp(burstColor, TerrainCache.Rgb(255, 255, 255), 0.5f)
					});
				}
			}
		}

		private static void Motes(MeshInstance inst, Emitter e, uint seed, float t, float night, float s, float ground, List<Billboard> bbs)
		{
			Mat3 basis = inst.Basis;
			for (int i = 0; i < e.Count; i++)
			{
				uint num = TerrainModel.Hash((int)seed + i * 613, i * 29 + (int)e.Kind);
				float num2 = (float)(num & 0x3FF) / 1023f;
				float num3 = (float)((num >> 10) & 0x3FF) / 1023f;
				float num4 = (float)((num >> 20) & 0x3FF) / 1023f;
				float num5 = num2 * (MathF.PI * 2f);
				float num6 = (float)Math.Sqrt(num3) * e.Radius;
				Vec3 vec = e.At + new Vec3((float)Math.Cos(num5) * num6, (float)Math.Sin(num5) * num6, 0f);
				bool emissive = false;
				bool additive = false;
				byte b = 0;
				uint color = e.Color;
				Vec3 v;
				float num11;
				SpriteImage image;
				switch (e.Kind)
				{
				case Mote.Spores:
				{
					float num7 = 4f + num4 * 3f;
					float num8 = Frac(t / num7 + num2) * num7;
					float num9 = num8 / num7;
					v = vec + new Vec3(0.35f * (float)Math.Sin(num8 * 1.1f + (float)i), 0.35f * (float)Math.Cos(num8 * 0.9f + (float)i * 1.7f), num8 * 0.32f);
					num11 = 0.09f + 0.07f * num4;
					image = BiomeArt.Dot(0);
					emissive = night > 0.3f;
					b = (byte)(200f * Fade(num9));
					break;
				}
				case Mote.Bubbles:
				{
					float num7 = 1.1f + num4 * 1.3f;
					float num8 = Frac(t / num7 + num2) * num7;
					float num9 = num8 / num7;
					v = vec + new Vec3(0.05f * (float)Math.Sin(num8 * 6f + (float)i), 0f, num8 * 0.42f);
					num11 = (0.12f + 0.16f * num4) * ((num9 > 0.88f) ? 1.35f : (0.6f + 0.4f * num9));
					image = ((num9 > 0.88f) ? BiomeArt.Sprinkle(i) : BiomeArt.Bubble);
					b = (byte)((num9 > 0.88f) ? 140u : 210u);
					break;
				}
				case Mote.Embers:
				{
					float num7 = 1.8f + num4 * 1.6f;
					float num8 = Frac(t / num7 + num2) * num7;
					float num9 = num8 / num7;
					v = vec + new Vec3(0.25f * (float)Math.Sin(num8 * 3.1f + (float)i), 0.25f * (float)Math.Cos(num8 * 2.3f + (float)i), num8 * 1.1f);
					num11 = 0.07f + 0.05f * num4;
					image = Flames.Ember;
					emissive = true;
					b = (byte)(230f * Fade(num9));
					break;
				}
				case Mote.Sparkles:
				{
					float num7 = 1.4f + num4 * 1.4f;
					float num8 = Frac(t / num7 + num2) * num7;
					float num9 = num8 / num7;
					v = vec + new Vec3(0f, 0f, (num4 - 0.5f) * e.Radius * 0.8f + num8 * 0.08f);
					num11 = (0.1f + 0.1f * num4) * (float)Math.Sin((double)num9 * Math.PI);
					image = BiomeArt.Star(i + (int)(num8 * 4f));
					emissive = true;
					additive = true;
					b = (byte)(220f * (float)Math.Sin((double)num9 * Math.PI));
					break;
				}
				case Mote.Drips:
				{
					float num7 = 1.6f + num4 * 2f;
					float num8 = Frac(t / num7 + num2) * num7;
					float num9 = num8 / num7;
					float num12 = ((num9 < 0.7f) ? 0f : ((num9 - 0.7f) / 0.3f));
					v = vec - new Vec3(0f, 0f, num12 * num12 * Math.Max(0.2f, e.At.Z));
					num11 = 0.07f + 0.06f * Math.Min(1f, num9 / 0.7f);
					image = BiomeArt.Dot(0);
					b = 230;
					emissive = night > 0.5f;
					break;
				}
				case Mote.Snow:
				{
					float num7 = 3f + num4 * 3f;
					float num8 = Frac(t / num7 + num2) * num7;
					float num9 = num8 / num7;
					v = vec + new Vec3(0.3f * (float)Math.Sin(num8 * 1.7f + (float)i), 0.3f * (float)Math.Cos(num8 * 1.3f + (float)i), (0f - num8) * 0.45f);
					num11 = 0.06f + 0.05f * num4;
					image = BiomeArt.Sprinkle(i);
					b = (byte)(230f * Fade(num9));
					break;
				}
				case Mote.Notes:
				{
					float num7 = 3.2f + num4 * 2f;
					float num8 = Frac(t / num7 + num2) * num7;
					float num9 = num8 / num7;
					v = vec + new Vec3(0.45f * (float)Math.Sin(num8 * 1.4f + (float)i), 0.2f * (float)Math.Cos(num8 * 1.1f + (float)i), num8 * 0.5f);
					num11 = 0.32f + 0.12f * num4;
					image = Note((int)((num >> 28) & 1));
					emissive = true;
					b = (byte)(230f * Fade(num9));
					break;
				}
				case Mote.Sparks:
				{
					float num13 = 1.6f + num4 * 2.6f;
					float num14 = Frac(t / num13 + num2) * num13;
					if (!(num14 > 0.45f))
					{
						for (int j = 0; j < 5; j++)
						{
							float num15 = Hash01(num, j) * (MathF.PI * 2f);
							float num16 = 0.8f + Hash01(num + 3, j) * 1.2f;
							Vec3 v2 = vec + new Vec3((float)Math.Cos(num15) * num16 * num14, (float)Math.Sin(num15) * num16 * num14, 1.4f * num14 - 4.9f * num14 * num14);
							Vec3 vec2 = basis.Mul(v2) + inst.Position;
							bbs.Add(new Billboard
							{
								X = vec2.X,
								Y = vec2.Y,
								Z = vec2.Z,
								GroundZ = ground,
								Width = 0.07f,
								Height = 0.07f,
								Image = BlastFx.Spark((j != 0) ? 1 : 2),
								Kind = BillboardKind.Facing,
								Tag = -1,
								Emissive = true,
								Multiply = color
							});
						}
					}
					continue;
				}
				default:
				{
					float num7 = 6f + num4 * 3f;
					float num8 = Frac(t / num7 + num2) * num7;
					float num9 = num8 / num7;
					float num10 = num8 * 0.4f;
					v = vec + new Vec3(num10 * num10 * 0.12f, 0f, num10);
					num11 = (0.5f + 1.2f * num9) * Math.Max(0.6f, e.Radius);
					image = FireField.SoftSmoke(Math.Min(5, (int)(num9 * 2f)), (int)(num & 3));
					b = (byte)Math.Max(1, (int)(110f * ((num9 < 0.1f) ? (num9 / 0.1f) : ((float)Math.Pow(1f - (num9 - 0.1f) / 0.9f, 1.399999976158142)))));
					break;
				}
				}
				if (b != 0 && !(num11 <= 0.005f))
				{
					Vec3 vec3 = basis.Mul(v) + inst.Position;
					float num17 = num11 * Math.Max(0.6f, s);
					bbs.Add(new Billboard
					{
						X = vec3.X,
						Y = vec3.Y,
						Z = vec3.Z - num17 * 0.5f,
						GroundZ = ground,
						Width = num17,
						Height = num17,
						Image = image,
						Kind = BillboardKind.Facing,
						Tag = -1,
						Emissive = emissive,
						Additive = additive,
						Multiply = color,
						Alpha = b,
						Flip = ((num & 0x40) != 0)
					});
				}
			}
		}

		private static float Fade(float u)
		{
			if (!(u < 0.15f))
			{
				if (!(u > 0.75f))
				{
					return 1f;
				}
				return (1f - u) / 0.25f;
			}
			return u / 0.15f;
		}

		private static void Mist(MeshInstance inst, uint tint, uint seed, float t, float night, float s, float ground, List<Billboard> bbs)
		{
			float num = Math.Max(1.5f, (inst.Model.Max.X - inst.Model.Min.X) * s);
			float x = inst.Position.X;
			float y = inst.Position.Y;
			for (int i = 0; i < 4; i++)
			{
				uint num2 = TerrainModel.Hash((int)seed + i * 4099, i * 13 + 7);
				float num3 = (float)i * (MathF.PI / 2f) + (float)(num2 & 0xFF) / 255f * 1.2f + t * (0.03f + (float)((num2 >> 8) & 0xF) * 0.004f);
				float num4 = num * (0.45f + 0.35f * (float)((num2 >> 12) & 0xFF) / 255f);
				float num5 = 0.75f + 0.25f * (float)Math.Sin(t * 0.37f + (float)i * 1.9f);
				float num6 = num * (0.9f + 0.5f * (float)((num2 >> 20) & 0xFF) / 255f);
				float num7 = num6 * 0.32f;
				bbs.Add(new Billboard
				{
					X = x + (float)Math.Cos(num3) * num4,
					Y = y + (float)Math.Sin(num3) * num4,
					Z = ground - num7 * 0.25f,
					GroundZ = ground,
					Width = num6,
					Height = num7,
					Image = FireField.SoftSmoke(5, (int)(num2 & 3)),
					Kind = BillboardKind.Facing,
					Flip = ((num2 & 8) != 0),
					Tag = -1,
					Multiply = tint,
					Emissive = (night > 0.5f),
					Alpha = (byte)Math.Max(1, Math.Min(255, (int)((22f + 58f * night) * num5)))
				});
			}
		}

		public static SpriteImage Note(int which)
		{
			which &= 1;
			if (Notes[which] != null)
			{
				return Notes[which];
			}
			string[] array = ((which != 0) ? new string[8] { "..XXXXX", "..XxxxX", "..X...X", "..X...X", "..X...X", "XXX.XXX", "XXX.XXX", ".x...x." } : new string[9] { "..XX..", "..XxX.", "..X.xX", "..X..x", "..X...", "..X...", "XXX...", "XXX...", ".x...." });
			int num = array.Length;
			int length = array[0].Length;
			uint[] array2 = new uint[length * num];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < length; j++)
				{
					char c = array[i][j];
					int num2 = (num - 1 - i) * length + j;
					int num3;
					switch (c)
					{
					default:
						num3 = 0;
						break;
					case 'x':
						num3 = -2697514;
						break;
					case 'X':
						num3 = -1;
						break;
					}
					array2[num2] = (uint)num3;
				}
			}
			SpriteImage spriteImage = new SpriteImage
			{
				Width = length,
				Height = num,
				Pixels = array2,
				PivotX = 0.5f,
				UnitWidth = 1f,
				UnitHeight = 1f
			};
			spriteImage.MeasureOpaqueRows();
			Notes[which] = spriteImage;
			return spriteImage;
		}
	}
}
