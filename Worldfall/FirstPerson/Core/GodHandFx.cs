using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public sealed class GodHandFx
	{
		private struct Ring
		{
			public float U;

			public float Y;

			public float Z;

			public float Hx;

			public float Hy;

			public float Bend;
		}

		private struct Wisp
		{
			public Vec3 Pos;

			public Vec3 Vel;

			public float Age;

			public float Life;

			public float Size;

			public int Variant;

			public float Shade;
		}

		public readonly MeshInstance Hand = new MeshInstance
		{
			NoShadow = true
		};

		private readonly CloudLook _cloud = new CloudLook();

		public Vec3 Lean;

		public float Curl;

		public float Presence = 1f;

		private float _shown = 1f;

		public readonly uint Seed;

		private float _sparkClock;

		private float _ringClock;

		private float _wispClock;

		private float _tipGlow;

		private Vec3 _lastWrist;

		private bool _seen;

		public const float HandLength = 14f;

		public int BillowsDrawn;

		public int WispsAlive;

		public int BankPuffs;

		private static readonly string[] Joints = new string[15]
		{
			"thumb1", "thumb2", "thumb3", "index1", "index2", "index3", "middle1", "middle2", "middle3", "ring1",
			"ring2", "ring3", "pinky1", "pinky2", "pinky3"
		};

		public static readonly float[] Point = new float[15]
		{
			6f, 30f, 30f, -8f, -10f, -6f, 74f, 88f, 52f, 78f,
			88f, 50f, 82f, 86f, 48f
		};

		public static readonly float[] Open = new float[15]
		{
			-6f, -4f, -4f, -6f, -6f, -4f, -4f, -6f, -4f, -2f,
			-4f, -2f, 0f, -2f, 0f
		};

		public static readonly float[] Grab = new float[15]
		{
			8f, 34f, 36f, 50f, 60f, 40f, 54f, 62f, 42f, 58f,
			62f, 42f, 62f, 62f, 40f
		};

		private const float PalmScale = 1.22f;

		private static readonly Dictionary<MeshModel, Ring[]> _rings = new Dictionary<MeshModel, Ring[]>();

		private readonly List<Wisp> _wisps = new List<Wisp>();

		private const float BankBase = 2.45f;

		private static SpriteImage[] _billows;

		public GodHandFx(uint seed)
		{
			Seed = seed;
			float num = BlastFx.Rand(seed, 3) * 6.283f;
			Lean = new Vec3((float)Math.Cos(num) * 0.38f, (float)Math.Sin(num) * 0.38f, 1f).Normalized();
			Hand.Cloud = _cloud;
		}

		public static Vec3 InfoPoint(MeshModel m, string key, Vec3 fallback)
		{
			string text = m.Info(key);
			if (text == null)
			{
				return fallback;
			}
			string[] array = text.Split(',');
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			if (array.Length == 3 && float.TryParse(array[0], NumberStyles.Float, invariantCulture, out var result) && float.TryParse(array[1], NumberStyles.Float, invariantCulture, out var result2) && float.TryParse(array[2], NumberStyles.Float, invariantCulture, out var result3))
			{
				return new Vec3(result, result2, result3);
			}
			return fallback;
		}

		private static void Pose(MeshInstance inst, MeshModel m, float[] a, float[] b, float k)
		{
			int num = m.Parts.Length;
			if (inst.PartRotation == null || inst.PartRotation.Length != num)
			{
				inst.PartRotation = new Mat3[num];
			}
			for (int i = 0; i < num; i++)
			{
				inst.PartRotation[i] = Mat3.Identity;
			}
			Vec3 axis = InfoPoint(m, "thumb_swing", new Vec3(-0.19f, -0.912f, 0.362f)).Normalized();
			Vec3 axis2 = InfoPoint(m, "thumb_flex", new Vec3(-0.734f, -0.584f, 0.347f)).Normalized();
			for (int j = 0; j < Joints.Length; j++)
			{
				int num2 = m.PartIndex(Joints[j]);
				if (num2 >= 0)
				{
					float num3 = (a[j] + (b[j] - a[j]) * k) * MathF.PI / 180f;
					inst.PartRotation[num2] = ((j == 0) ? Mat3.Rotation(axis, num3) : ((j < 3) ? Mat3.Rotation(axis2, num3) : Mat3.RotX(0f - num3)));
				}
			}
			int num4 = m.PartIndex("palm");
			if (inst.PartScale == null || inst.PartScale.Length != num)
			{
				inst.PartScale = new float[num];
			}
			for (int l = 0; l < num; l++)
			{
				inst.PartScale[l] = 1f;
			}
			if (num4 >= 0)
			{
				inst.PartScale[num4] = 1.22f;
			}
		}

		private static Vec3 Posed(MeshInstance inst, MeshModel m, int part, Vec3 rest)
		{
			if (part < 0)
			{
				return rest;
			}
			Vec3 vec = rest;
			for (int num = part; num >= 0; num = m.Parts[num].Parent)
			{
				Mat3 a = ((inst.PartRotation != null) ? inst.PartRotation[num] : Mat3.Identity);
				float num2 = ((inst.PartScale != null) ? inst.PartScale[num] : 1f);
				if (num2 != 1f)
				{
					a *= num2;
				}
				Vec3 pivot = m.Parts[num].Pivot;
				vec = a.Mul(vec - pivot) + pivot;
			}
			return vec;
		}

		private static Mat3 ArmBasis(Vec3 up, Vec3 palm, float scale)
		{
			Vec3 vec = up.Normalized();
			Vec3 vec2 = palm * -1f;
			vec2 -= vec * Vec3.Dot(vec2, vec);
			if (Vec3.Dot(vec2, vec2) < 0.0001f)
			{
				vec2 = ((Math.Abs(vec.X) < 0.9f) ? Vec3.Cross(vec, new Vec3(1f, 0f, 0f)) : Vec3.Cross(vec, new Vec3(0f, 1f, 0f)));
			}
			Vec3 vec3 = vec2.Normalized();
			return new Mat3(Vec3.Cross(vec3, vec) * scale, vec3 * scale, vec * scale);
		}

		public Vec3 Steer(float vx, float vy, float dt, float time)
		{
			float num = BlastFx.Rand(Seed, 3) * 6.283f;
			Vec3 vec = (new Vec3((float)Math.Cos(num) * 0.38f, (float)Math.Sin(num) * 0.38f, 1f) - new Vec3(vx, vy, 0f) * 0.06f).Normalized();
			Lean = (Lean + (vec - Lean) * (1f - (float)Math.Exp(-1.5f * dt))).Normalized();
			float num2 = (float)Math.Sin(time * 0.6f + (float)Seed) * 0.05f;
			return (Lean + new Vec3(num2, (0f - num2) * 0.7f, 0f)).Normalized();
		}

		private Vec3 Withdrawn(Vec3 up, float s)
		{
			_shown = BlastFx.Smooth(Presence);
			return up.Normalized() * ((1f - _shown) * s * 2.4f);
		}

		private void Look(MeshModel m, float night)
		{
			CloudLook cloud = _cloud;
			cloud.Opacity = 0.78f * _shown;
			cloud.HalfDepth = 0.055f;
			cloud.Grain = 0.18f;
			cloud.Mottle = 0.7f;
			cloud.Bump = 6.5f;
			cloud.Churn = new Vec3(0.012f, -0.008f, 0.05f);
			cloud.FadeFrom = m.InfoFloat("fade", 2.35f);
			cloud.FadeTo = cloud.FadeFrom + 0.7f;
			cloud.Wrap = 0.3f;
			cloud.Scatter = 0.22f;
			cloud.Ambient = 0.82f;
			cloud.SelfColor = TerrainCache.Rgb(226, 226, 250);
			cloud.SelfLight = 0.08f + 0.3f * night;
			cloud.GlowColor = TerrainCache.Rgb(255, 210, 120);
		}

		public void Finger(FxSink sink, FxParticles parts, MeshModel m, Vec3 tip, Vec3 up, Vec3 palm, bool drawing, float groundZ, float time, float dt, float night)
		{
			float num = 14f / Math.Max(0.2f, m.InfoFloat("hand", 1f));
			tip += Withdrawn(up, num);
			Mat3 basis = ArmBasis(up, palm, num);
			Pose(Hand, m, Point, Point, 0f);
			Vec3 vec = InfoPoint(m, "tip", new Vec3(-0.178f, -0.161f, -0.963f));
			Vec3 v = Posed(Hand, m, m.PartIndex("index3"), vec + new Vec3(0f, 0f, 0.02f));
			Hand.Model = m;
			Hand.Basis = basis;
			Hand.Position = tip - basis.Mul(v);
			Hand.Static = false;
			Hand.SortDepth = sink.Distance(tip.X, tip.Y);
			Look(m, night);
			_tipGlow += ((drawing ? 1f : 0.22f) - _tipGlow) * (1f - (float)Math.Exp(-6f * Math.Max(0f, dt)));
			if (dt <= 0f)
			{
				_tipGlow = (drawing ? 1f : 0.22f);
			}
			float num2 = (0.85f + 0.15f * (float)Math.Sin(time * 6f + (float)Seed)) * _shown;
			_cloud.Glow = _tipGlow * num2 * 0.95f;
			_cloud.GlowAt = tip;
			_cloud.GlowRadius = num * 0.36f;
			sink.Meshes.Add(Hand);
			Billows(sink, m, num, time, night);
			Wisps(sink, parts, m, num, time, dt, night);
			Sky(sink, m, num, time, night);
			sink.Glow(tip.X, tip.Y, tip.Z + 0.2f, 1.5f + 3.5f * _tipGlow, TerrainCache.Rgb(255, 222, 140), (0.25f + 0.6f * _tipGlow) * num2);
			sink.Light(tip.X, tip.Y, tip.Z + 1f, 5f + 8f * _tipGlow, TerrainCache.Rgb(255, 218, 150), (0.35f + 1.1f * _tipGlow) * num2, 0.4f);
			if (drawing && parts != null && !(dt <= 0f) && !(_shown < 0.9f))
			{
				_sparkClock += dt * 40f;
				while (_sparkClock > 1f)
				{
					_sparkClock -= 1f;
					int num3 = parts.List.Count * 7 + (int)(time * 333f);
					float num4 = BlastFx.Rand(Seed, num3) * 6.283f;
					float num5 = 0.5f + BlastFx.Rand(Seed, num3 + 1) * 2f;
					parts.Spark(tip + new Vec3(0f, 0f, 0.1f), new Vec3((float)Math.Cos(num4) * num5, (float)Math.Sin(num4) * num5, 1f + BlastFx.Rand(Seed, num3 + 2) * 2.5f), 1f + BlastFx.Rand(Seed, num3 + 3), 0.12f, TerrainCache.Rgb(255, 246, 200), TerrainCache.Rgb(255, 190, 90), 0.8f, 1.2f);
				}
				_ringClock += dt * 8f;
				while (_ringClock > 1f)
				{
					_ringClock -= 1f;
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(tip.X, tip.Y, groundZ + 0.1f),
						Life = 1.2f,
						Size = 1.2f,
						EndSize = 3.2f,
						Color = TerrainCache.Rgb(255, 230, 160),
						EndColor = TerrainCache.Rgb(255, 200, 120),
						Alpha = 120,
						Kind = FxParticles.Kind.Glow
					});
				}
			}
		}

		public void Grabbing(FxSink sink, MeshModel m, Vec3 held, Vec3 up, Vec3 palm, float time, float night, float dt = 0f)
		{
			float num = 11.900001f / Math.Max(0.2f, m.InfoFloat("hand", 1f));
			held += Withdrawn(up, num);
			Mat3 basis = ArmBasis(up, palm, num);
			Pose(Hand, m, Open, Grab, Curl);
			Vec3 vec = InfoPoint(m, "palm", new Vec3(0f, -0.088f, -0.28f));
			Vec3 vec2 = InfoPoint(m, "grip", vec);
			Vec3 v = (vec + (vec2 - vec) * Curl) * 1.22f;
			Hand.Model = m;
			Hand.Basis = basis;
			Hand.Position = held - basis.Mul(v);
			Hand.Static = false;
			Hand.SortDepth = sink.Distance(held.X, held.Y);
			Look(m, night);
			_cloud.Glow = 0f;
			sink.Meshes.Add(Hand);
			Billows(sink, m, num, time, night);
			Wisps(sink, null, m, num, time, dt, night);
			Sky(sink, m, num, time, night);
			sink.Glow(held.X, held.Y, held.Z, num * 0.7f, TerrainCache.Rgb(255, 236, 190), 0.14f * _shown);
		}

		private static Ring[] Rings(MeshModel m)
		{
			lock (_rings)
			{
				if (_rings.TryGetValue(m, out var value))
				{
					return value;
				}
				List<Ring> list = new List<Ring>();
				string text = m.Info("rings");
				CultureInfo invariantCulture = CultureInfo.InvariantCulture;
				if (text != null)
				{
					string[] array = text.Split('|');
					for (int i = 0; i < array.Length; i++)
					{
						string[] array2 = array[i].Split(',');
						if (array2.Length == 6)
						{
							float[] array3 = new float[6];
							bool flag = true;
							for (int j = 0; j < 6; j++)
							{
								flag &= float.TryParse(array2[j], NumberStyles.Float, invariantCulture, out array3[j]);
							}
							if (flag)
							{
								list.Add(new Ring
								{
									U = array3[0],
									Y = array3[1],
									Z = array3[2],
									Hx = array3[3],
									Hy = array3[4],
									Bend = array3[5] * MathF.PI / 180f
								});
							}
						}
					}
				}
				if (list.Count < 2)
				{
					float num = m.InfoFloat("arm", 3f);
					list.Clear();
					list.Add(new Ring
					{
						U = 0.2f,
						Z = 0.2f,
						Hx = 0.15f,
						Hy = 0.1f
					});
					list.Add(new Ring
					{
						U = num,
						Z = num,
						Hx = 0.3f,
						Hy = 0.28f
					});
				}
				return _rings[m] = list.ToArray();
			}
		}

		private static void ArmAt(Ring[] r, float u, out Vec3 centre, out Vec3 back, out float hx, out float hy)
		{
			int i;
			for (i = 0; i < r.Length - 2 && u > r[i + 1].U; i++)
			{
			}
			float num = (u - r[i].U) / Math.Max(0.0001f, r[i + 1].U - r[i].U);
			if (num < 0f && i == 0)
			{
				num = Math.Max(num, -1f);
			}
			float num2 = r[i].Bend + (r[i + 1].Bend - r[i].Bend) * num;
			centre = new Vec3(0f, r[i].Y + (r[i + 1].Y - r[i].Y) * num, r[i].Z + (r[i + 1].Z - r[i].Z) * num);
			back = new Vec3(0f, (float)Math.Cos(num2), 0f - (float)Math.Sin(num2));
			hx = r[i].Hx + (r[i + 1].Hx - r[i].Hx) * Math.Max(0f, num);
			hy = r[i].Hy + (r[i + 1].Hy - r[i].Hy) * Math.Max(0f, num);
		}

		private Vec3 OnArm(Ring[] r, float u, float theta, float out_, out float radius)
		{
			ArmAt(r, u, out var centre, out var back, out var hx, out var hy);
			radius = (hx + hy) * 0.5f;
			float num = (float)Math.Cos(theta);
			float num2 = (float)Math.Sin(theta);
			Vec3 v = centre + new Vec3(1f, 0f, 0f) * (num * (hx + radius * out_)) + back * (num2 * (hy + radius * out_));
			return Hand.Position + Hand.Basis.Mul(v);
		}

		private float FacingAngle(Ring[] r, float u, FxSink sink)
		{
			ArmAt(r, u, out var centre, out var back, out var _, out var _);
			Vec3 vec = Hand.Position + Hand.Basis.Mul(centre);
			Vec3 a = new Vec3(sink.EyeX - vec.X, sink.EyeY - vec.Y, sink.EyeZ - vec.Z);
			Vec3 c = Hand.Basis.C0;
			Vec3 vec2 = Hand.Basis.Mul(back);
			return (float)Math.Atan2(Vec3.Dot(a, vec2) / Math.Max(0.0001f, Vec3.Dot(vec2, vec2)), Vec3.Dot(a, c) / Math.Max(0.0001f, Vec3.Dot(c, c)));
		}

		private static uint CloudTone(float k, float night)
		{
			uint a = TerrainCache.Lerp(TerrainCache.Rgb(214, 218, 236), TerrainCache.Rgb(196, 200, 236), night);
			uint b = TerrainCache.Lerp(TerrainCache.Rgb(255, 255, 255), TerrainCache.Rgb(240, 240, 255), night);
			return TerrainCache.Lerp(a, b, BlastFx.Clamp01(k));
		}

		private void Billows(FxSink sink, MeshModel m, float s, float time, float night)
		{
			Ring[] r = Rings(m);
			float num = sink.Distance(Hand.Position.X, Hand.Position.Y);
			int num2 = ((num < 70f) ? 50 : ((num < 140f) ? 30 : 20));
			BillowsDrawn = 0;
			for (int i = 0; i < num2; i++)
			{
				float num3 = 3.2f + 3f * BlastFx.Rand(Seed, i * 5 + 900);
				float num4 = time / num3 + BlastFx.Rand(Seed, i * 5 + 901);
				int num5 = (int)Math.Floor(num4);
				float num6 = num4 - (float)num5;
				uint num7 = Seed + (uint)(num5 * 7919 + i * 131);
				bool flag = i % 10 < 7;
				float num8 = (flag ? (1.55f + 1.0500002f * (float)Math.Pow(BlastFx.Rand(num7, 1), 0.800000011920929)) : (0.55f + 1f * BlastFx.Rand(num7, 1)));
				num8 += num6 * 0.2f;
				float theta = (flag ? (BlastFx.Rand(num7, 2) * 6.283f) : (FacingAngle(r, num8, sink) + (((i & 1) == 0) ? 1.5708f : (-1.5708f)) + (BlastFx.Rand(num7, 2) - 0.5f) * 0.9f));
				float radius;
				Vec3 at = OnArm(r, num8, theta, flag ? (0.1f + 0.25f * num6) : (0.15f * num6), out radius);
				float num9 = radius * s * (flag ? ((0.9f + 0.7f * BlastFx.Rand(num7, 3)) * (0.75f + 0.45f * num6)) : ((0.55f + 0.35f * BlastFx.Rand(num7, 3)) * (0.8f + 0.4f * num6)));
				float num10 = (flag ? (0.5f + 0.4f * BlastFx.Smooth((num8 - 1.55f) / 0.9f)) : 0.32f);
				float num11 = (float)Math.Pow(Math.Sin(Math.PI * (double)num6), 0.8) * num10;
				if (!(num11 < 0.02f))
				{
					uint color = CloudTone(0.55f + 0.45f * BlastFx.Rand(num7, 4), night);
					Puff(sink, Billow((int)(num7 & 7)), at, num9 * 1.2f, num9, color, num11 * 0.9f * _shown, (num7 & 8) != 0, night);
					BillowsDrawn++;
				}
			}
		}

		private void Wisps(FxSink sink, FxParticles parts, MeshModel m, float s, float time, float dt, float night)
		{
			Ring[] r = Rings(m);
			Vec3 position = Hand.Position;
			if (dt > 0f)
			{
				Vec3 vec = position - _lastWrist;
				float num = (_seen ? ((float)Math.Sqrt(Vec3.Dot(vec, vec))) : 0f);
				if (num > s * 2f)
				{
					num = 0f;
				}
				_wispClock += num / (s * 0.07f);
				if (_wispClock > 8f)
				{
					_wispClock = 8f;
				}
				int num2 = 0;
				while (_wispClock > 1f && _wisps.Count < 60)
				{
					_wispClock -= 1f;
					int num3 = (int)(time * 97f) + num2++ * 13 + _wisps.Count;
					float u = 0.3f + 2.6f * BlastFx.Rand(Seed, num3 + 1);
					float theta = BlastFx.Rand(Seed, num3 + 2) * 6.283f;
					float radius;
					Vec3 pos = OnArm(r, u, theta, 0.3f, out radius);
					_wisps.Add(new Wisp
					{
						Pos = pos,
						Vel = new Vec3((BlastFx.Rand(Seed, num3 + 3) - 0.5f) * 0.6f, (BlastFx.Rand(Seed, num3 + 4) - 0.5f) * 0.6f, 0.3f + 0.4f * BlastFx.Rand(Seed, num3 + 5)),
						Life = 1.4f + 1.2f * BlastFx.Rand(Seed, num3 + 6),
						Size = radius * s * (1.2f + 0.8f * BlastFx.Rand(Seed, num3 + 7)),
						Variant = (num3 & 7),
						Shade = BlastFx.Rand(Seed, num3 + 8)
					});
				}
				for (int num4 = _wisps.Count - 1; num4 >= 0; num4--)
				{
					Wisp value = _wisps[num4];
					value.Age += dt;
					if (value.Age >= value.Life)
					{
						_wisps.RemoveAt(num4);
					}
					else
					{
						value.Pos += value.Vel * dt;
						_wisps[num4] = value;
					}
				}
			}
			_lastWrist = position;
			_seen = true;
			WispsAlive = _wisps.Count;
			foreach (Wisp wisp in _wisps)
			{
				float num5 = wisp.Age / wisp.Life;
				float num6 = (1f - num5) * (1f - num5) * Math.Min(1f, wisp.Age * 6f) * 0.6f;
				if (!(num6 < 0.02f))
				{
					float num7 = wisp.Size * (1f + 1.2f * num5);
					Puff(sink, Billow(wisp.Variant), wisp.Pos, num7 * 1.3f, num7 * (1f - 0.3f * num5), CloudTone(0.5f + 0.5f * wisp.Shade, night), num6 * _shown, (wisp.Variant & 1) != 0, night);
				}
			}
		}

		private void Sky(FxSink sink, MeshModel m, float s, float time, float night)
		{
			ArmAt(Rings(m), 2.45f, out var centre, out var _, out var _, out var _);
			Vec3 v = InfoPoint(m, "shoulder", new Vec3(0f, 0.6f, 2.9f));
			Vec3 vec = Hand.Position + Hand.Basis.Mul(centre);
			Vec3 vec2 = Hand.Position + Hand.Basis.Mul(v);
			Vec3 vec3 = new Vec3((vec.X + vec2.X) * 0.5f, (vec.Y + vec2.Y) * 0.5f, vec.Z);
			int num = ((sink.Distance(vec3.X, vec3.Y) < 120f) ? 34 : 20);
			uint seed = Seed;
			float num2 = time * 0.01f;
			BankPuffs = 0;
			for (int i = 0; i < num; i++)
			{
				float num3 = (float)i / (float)num;
				float num4 = BlastFx.Rand(seed, i + 300) * 6.283f + num2 * (0.6f + 0.8f * BlastFx.Rand(seed, i + 310));
				float num5 = ((num3 < 0.45f) ? 1.35f : ((num3 < 0.8f) ? 1.05f : 0.6f));
				float num6 = s * num5 * (0.25f + 0.75f * (float)Math.Sqrt(BlastFx.Rand(seed, i + 340)));
				float num7 = ((num3 < 0.45f) ? (s * 0.06f * BlastFx.Rand(seed, i + 380)) : ((num3 < 0.8f) ? (s * (0.3f + 0.35f * BlastFx.Rand(seed, i + 380))) : (s * (0.7f + 0.45f * BlastFx.Rand(seed, i + 380)))));
				num7 -= num6 / (s * 1.35f) * (num6 / (s * 1.35f)) * s * 0.35f * num3;
				float num8 = (float)Math.Sin(time * (0.15f + 0.1f * BlastFx.Rand(seed, i + 390)) + (float)i) * s * 0.04f;
				Vec3 at = new Vec3(vec3.X + (float)Math.Cos(num4) * num6, vec3.Y + (float)Math.Sin(num4) * num6, vec3.Z + num7 + num8);
				float num9 = s * ((num3 < 0.45f) ? (0.95f + 0.55f * BlastFx.Rand(seed, i + 420)) : ((num3 < 0.8f) ? (0.85f + 0.45f * BlastFx.Rand(seed, i + 420)) : (0.65f + 0.35f * BlastFx.Rand(seed, i + 420))));
				float num10 = ((num3 < 0.45f) ? 1.75f : 1.3f);
				uint color = CloudTone((num3 < 0.45f) ? (0.1f + 0.25f * BlastFx.Rand(seed, i + 460)) : ((num3 < 0.8f) ? (0.45f + 0.3f * BlastFx.Rand(seed, i + 460)) : (0.8f + 0.2f * BlastFx.Rand(seed, i + 460))), night);
				Puff(sink, Billow(i + 3), at, num9 * num10, num9, color, (0.82f + 0.13f * BlastFx.Rand(seed, i + 470)) * _shown, (i & 1) != 0, night);
				BankPuffs++;
			}
			sink.Glow(vec3.X, vec3.Y, vec3.Z + s * 0.3f, s * 3.2f, TerrainCache.Rgb(255, 222, 160), (0.14f + 0.04f * (float)Math.Sin(time * 1.3f + (float)seed)) * _shown);
		}

		private static void Puff(FxSink sink, SpriteImage img, Vec3 at, float w, float h, uint color, float alpha, bool flip, float night)
		{
			int num = (int)(alpha * 255f);
			if (num >= 2 && !(w <= 0f) && !(h <= 0f))
			{
				float num2 = at.X - sink.EyeX;
				float num3 = at.Y - sink.EyeY;
				uint num4 = (uint)((int)(night * 0.36f * 255f) << 24) | (TerrainCache.Rgb(164, 166, 204) & 0xFFFFFF);
				sink.Billboards.Add(new Billboard
				{
					X = at.X,
					Y = at.Y,
					Z = at.Z - h * 0.5f,
					GroundZ = at.Z - h * 0.5f,
					Width = w,
					Height = h,
					Image = img,
					Kind = BillboardKind.Facing,
					Tag = -1,
					Multiply = color,
					Alpha = (byte)Math.Min(255, num),
					Flip = flip,
					Flash = ((night > 0.02f) ? num4 : 0u),
					Far = (num2 * num2 + num3 * num3 > 900f || w > 6f)
				});
			}
		}

		public static SpriteImage Billow(int variant)
		{
			if (_billows == null)
			{
				SpriteImage[] array = new SpriteImage[8];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = MakeBillow(i);
				}
				_billows = array;
			}
			return _billows[(variant % 8 + 8) % 8];
		}

		private static SpriteImage MakeBillow(int v)
		{
			Random random = new Random(v * 7919 + 101);
			int num = 7 + v % 4;
			float[] array = new float[num];
			float[] array2 = new float[num];
			float[] array3 = new float[num];
			array[0] = 32f;
			array2[0] = 28f;
			array3[0] = 12f + (float)random.NextDouble() * 2.5f;
			for (int i = 1; i < num; i++)
			{
				double num2 = Math.PI * (0.02 + 0.96 * random.NextDouble());
				if (i % 4 == 0)
				{
					num2 = (0.0 - num2) * 0.2;
				}
				float num3 = 6.5f + (float)random.NextDouble() * 6f;
				float num4 = 9f + (float)random.NextDouble() * 9f;
				array[i] = Math.Max(num3 + 3f, Math.Min(64f - num3 - 3f, 32f + (float)Math.Cos(num2) * num4 * 1.2f));
				array2[i] = Math.Max(num3 + 3f, Math.Min(64f - num3 - 3f, 28f + (float)Math.Sin(num2) * num4 * 0.95f));
				array3[i] = num3;
			}
			float[] array4 = new float[4096];
			for (int j = 0; j < 64; j++)
			{
				for (int k = 0; k < 64; k++)
				{
					float num5 = (float)k + 0.5f;
					float num6 = (float)j + 0.5f;
					float num7 = 0f;
					for (int l = 0; l < num; l++)
					{
						float num8 = (num5 - array[l]) / array3[l];
						float num9 = (num6 - array2[l]) / array3[l];
						float num10 = num8 * num8 + num9 * num9;
						if (num10 < 4f)
						{
							num7 += (float)Math.Exp((0f - num10) * 1.6f);
						}
					}
					num7 += (TerrainModel.ValueNoise(num5 * 0.22f + (float)v * 13f, num6 * 0.22f) - 0.5f) * 0.18f + (TerrainModel.ValueNoise(num5 * 0.5f + (float)v * 7f, num6 * 0.5f + 31f) - 0.5f) * 0.08f;
					array4[j * 64 + k] = num7;
				}
			}
			uint[] array5 = new uint[4096];
			for (int m = 0; m < 64; m++)
			{
				for (int n = 0; n < 64; n++)
				{
					float num11 = BlastFx.Smooth((array4[m * 64 + n] - 0.16f) / 0.42f);
					if (!(num11 <= 0.01f))
					{
						float num12 = array4[m * 64 + Math.Max(0, n - 1)];
						float num13 = array4[m * 64 + Math.Min(63, n + 1)];
						float num14 = array4[Math.Max(0, m - 1) * 64 + n];
						float num15 = array4[Math.Min(63, m + 1) * 64 + n];
						float num16 = (0f - (num13 - num12)) * 2.4f;
						float num17 = (0f - (num15 - num14)) * 2.4f;
						float num18 = 1f;
						float num19 = 1f / (float)Math.Sqrt(num16 * num16 + num17 * num17 + num18 * num18);
						float num20 = (num16 * -0.32f + num17 * 0.7f + num18 * 0.64f) * num19;
						num20 = BlastFx.Clamp01((num20 + 0.35f) / 1.35f);
						float num21 = ((float)m + 0.5f) / 64f;
						float num22 = BlastFx.Clamp01(0.62f + 0.36f * num20 + 0.12f * (num21 - 0.45f));
						int val = (int)(255f * (0.78f + 0.22f * num22) * num22 + 0.5f);
						int val2 = (int)(255f * (0.82f + 0.18f * num22) * num22 + 0.5f);
						int val3 = (int)(255f * Math.Min(1f, num22 * 1.04f + 0.02f));
						array5[m * 64 + n] = (TerrainCache.Rgb(Math.Min(255, val), Math.Min(255, val2), Math.Min(255, val3)) & 0xFFFFFF) | ((uint)(num11 * 255f) << 24);
					}
				}
			}
			SpriteImage spriteImage = new SpriteImage();
			spriteImage.Width = 64;
			spriteImage.Height = 64;
			spriteImage.Pixels = array5;
			spriteImage.PivotX = 0.5f;
			spriteImage.UnitWidth = 1f;
			spriteImage.UnitHeight = 1f;
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}
	}
}
