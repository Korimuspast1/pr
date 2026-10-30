using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public sealed class DragonRig
	{
		public readonly MeshModel Model;

		private readonly int _body;

		private readonly int _head;

		private readonly int _jaw;

		private readonly int _glow;

		private readonly int[] _neck = new int[4];

		private readonly int[] _tail = new int[6];

		private readonly int _legFL;

		private readonly int _legFL2;

		private readonly int _legFR;

		private readonly int _legFR2;

		private readonly int _legBL;

		private readonly int _legBL2;

		private readonly int _legBL3;

		private readonly int _legBR;

		private readonly int _legBR2;

		private readonly int _legBR3;

		private readonly int[] _arm = new int[2];

		private readonly int[] _fore = new int[2];

		private readonly int[] _hand = new int[2];

		private readonly int[,] _finger = new int[2, 4];

		private readonly Vec3[] _shoulder = new Vec3[2];

		private readonly Vec3[] _elbow = new Vec3[2];

		private readonly Vec3[] _wrist = new Vec3[2];

		private readonly Vec3[] _hip = new Vec3[2];

		private readonly Vec3[] _flank = new Vec3[2];

		private readonly Vec3[,] _tip = new Vec3[2, 4];

		private readonly float[,] _foldTo = new float[2, 4];

		private readonly float[] _foldElbow = new float[2];

		public readonly Vec3 MouthRest;

		public readonly Vec3 MawRest;

		public readonly float Height;

		public readonly float Length;

		public readonly float Wingspan;

		public readonly ulong GlowMask;

		private const int InU = 8;

		private const int InV = 5;

		private const int FinU = 6;

		private const int FinV = 4;

		private const int VertsPerSide = 112;

		private readonly int[] _skinTri;

		private Mat3[] _pm = new Mat3[64];

		private Vec3[] _pt = new Vec3[64];

		private readonly Vec3[] _lead = new Vec3[8];

		private readonly Vec3[] _trail = new Vec3[8];

		private readonly Vec3[] _bodyEdge = new Vec3[5];

		private readonly Vec3[] _outer = new Vec3[5];

		private readonly Vec3[] _fa = new Vec3[6];

		private readonly Vec3[] _fb = new Vec3[6];

		private readonly Vec3[] _edge = new Vec3[4];

		private readonly Vec3[] _grid = new Vec3[112];

		private readonly Vec3[] _tipNow = new Vec3[4];

		private readonly Vec3[,] _tipNow0 = new Vec3[2, 4];

		public int HeadPart => _head;

		public int JawPart => _jaw;

		public int GlowPart => _glow;

		public int BodyPart => _body;

		public Vec3 Mouth => At(_head, MouthRest);

		public Vec3 Maw => At(_head, MawRest);

		public Vec3 Snout => (At(_head, MouthRest) - At(_head, MouthRest + new Vec3(0f, 1f, 0f))).Normalized();

		public int SkinPart(int side)
		{
			return Model.Parts.Length + side;
		}

		public DragonRig(MeshModel m)
		{
			Model = m;
			_body = m.PartIndex("body");
			_head = m.PartIndex("head");
			_jaw = m.PartIndex("jaw");
			_glow = m.PartIndex("glow");
			for (int i = 0; i < 4; i++)
			{
				_neck[i] = m.PartIndex("neck_" + (i + 1));
			}
			for (int j = 0; j < 6; j++)
			{
				_tail[j] = m.PartIndex("tail_" + (j + 1));
			}
			_legFL = m.PartIndex("leg_fl");
			_legFL2 = m.PartIndex("leg_fl2");
			_legFR = m.PartIndex("leg_fr");
			_legFR2 = m.PartIndex("leg_fr2");
			_legBL = m.PartIndex("leg_bl");
			_legBL2 = m.PartIndex("leg_bl2");
			_legBL3 = m.PartIndex("leg_bl3");
			_legBR = m.PartIndex("leg_br");
			_legBR2 = m.PartIndex("leg_br2");
			_legBR3 = m.PartIndex("leg_br3");
			if (_glow >= 0)
			{
				GlowMask = (ulong)(1L << _glow);
			}
			Height = m.InfoFloat("height", m.Max.Z);
			Length = m.InfoFloat("length", m.Max.Y - m.Min.Y);
			Wingspan = m.InfoFloat("wingspan", m.Max.X - m.Min.X);
			MouthRest = Info(m, "mouth", new Vec3(0f, m.Min.Y, m.Max.Z * 0.7f));
			MawRest = Info(m, "maw", MouthRest);
			for (int k = 0; k < 2; k++)
			{
				string text = ((k == 0) ? "l" : "r");
				_arm[k] = m.PartIndex("wing_" + text);
				_fore[k] = m.PartIndex("wing_" + text + "_fore");
				_hand[k] = m.PartIndex("wing_" + text + "_hand");
				for (int l = 0; l < 4; l++)
				{
					_finger[k, l] = m.PartIndex("wing_" + text + "_f" + (l + 1));
				}
				if (_arm[k] < 0 || _fore[k] < 0 || _hand[k] < 0)
				{
					continue;
				}
				_shoulder[k] = m.Parts[_arm[k]].Pivot;
				_elbow[k] = m.Parts[_fore[k]].Pivot;
				_wrist[k] = m.Parts[_hand[k]].Pivot;
				_hip[k] = Info(m, "hip_" + text, _shoulder[k] + new Vec3(0f, 4f, -0.5f));
				_flank[k] = Info(m, "flank_" + text, (_shoulder[k] + _hip[k]) * 0.5f);
				float num = ((k == 0) ? 1f : (-1f));
				Vec3 vec = _elbow[k] - _wrist[k];
				float num2 = (float)Math.Atan2(vec.Y, vec.X * num);
				for (int n = 0; n < 4; n++)
				{
					_tip[k, n] = Info(m, "tip_" + text + (n + 1), _wrist[k] + new Vec3(num * 4f, (float)n * 1.5f, 0f));
					Vec3 vec2 = _tip[k, n] - _wrist[k];
					float num3 = (float)Math.Atan2(vec2.Y, vec2.X * num);
					float num4 = num2 - num3 - (0.1f - (float)n * 0.04f);
					if (num4 < 0f)
					{
						num4 += MathF.PI * 2f * (float)Math.Ceiling((double)(0f - num4) / (Math.PI * 2.0));
					}
					_foldTo[k, n] = num4;
				}
				Vec3 vec3 = _elbow[k] - _shoulder[k];
				Vec3 vec4 = _wrist[k] - _elbow[k];
				float num5 = (float)Math.Atan2(vec3.Y, vec3.X * num);
				float num6 = (float)Math.Atan2(vec4.Y, vec4.X * num);
				_foldElbow[k] = 2.9815927f - (num5 - num6);
			}
			_skinTri = BuildSkinTriangles();
		}

		private static Vec3 Info(MeshModel m, string key, Vec3 fallback)
		{
			string text = m.Info(key);
			if (text == null)
			{
				return fallback;
			}
			string[] array = text.Split(',');
			if (array.Length != 3)
			{
				return fallback;
			}
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			return new Vec3(float.Parse(array[0], invariantCulture), float.Parse(array[1], invariantCulture), float.Parse(array[2], invariantCulture));
		}

		public MeshModel NewModel()
		{
			int num = Model.Parts.Length;
			MeshPart[] array = new MeshPart[num + 2];
			Array.Copy(Model.Parts, array, num);
			int num2 = Math.Max(0, Model.SlotIndex("membrane"));
			int num3 = Model.SlotIndex("membrane_dark");
			if (num3 < 0)
			{
				num3 = num2;
			}
			for (int i = 0; i < 2; i++)
			{
				int num4 = _skinTri.Length / 3;
				MeshPart meshPart = new MeshPart
				{
					Name = ((i == 0) ? "skin_l" : "skin_r"),
					Parent = -1,
					Pivot = new Vec3(0f, 0f, 0f),
					Pos = new float[672],
					Nrm = new float[672],
					Tri = new int[_skinTri.Length * 2],
					Slot = new byte[num4 * 2],
					Axis = new byte[num4 * 2]
				};
				for (int j = 0; j < _skinTri.Length; j += 3)
				{
					meshPart.Tri[j] = _skinTri[j];
					meshPart.Tri[j + 1] = _skinTri[j + 1];
					meshPart.Tri[j + 2] = _skinTri[j + 2];
					int num5 = _skinTri.Length + j;
					meshPart.Tri[num5] = _skinTri[j] + 112;
					meshPart.Tri[num5 + 1] = _skinTri[j + 2] + 112;
					meshPart.Tri[num5 + 2] = _skinTri[j + 1] + 112;
				}
				for (int k = 0; k < num4 * 2; k++)
				{
					meshPart.Slot[k] = (byte)((k < num4) ? num2 : num3);
					meshPart.Axis[k] = 2;
				}
				if (i == 1)
				{
					for (int l = 0; l < meshPart.Tri.Length; l += 3)
					{
						int num6 = meshPart.Tri[l + 1];
						meshPart.Tri[l + 1] = meshPart.Tri[l + 2];
						meshPart.Tri[l + 2] = num6;
					}
				}
				array[num + i] = meshPart;
			}
			return Model.Derive(Model.Name, array);
		}

		private int[] BuildSkinTriangles()
		{
			List<int> tri = new List<int>();
			Grid(0, 8, 5);
			for (int i = 0; i < 3; i++)
			{
				Grid(40 + i * 6 * 4, 6, 4);
			}
			return tri.ToArray();
			void Grid(int start, int nu, int nv)
			{
				for (int j = 0; j < nu - 1; j++)
				{
					for (int k = 0; k < nv - 1; k++)
					{
						int num = start + j * nv + k;
						int num2 = num + nv;
						int item = num2 + 1;
						int item2 = num + 1;
						tri.Add(num);
						tri.Add(num2);
						tri.Add(item);
						tri.Add(num);
						tri.Add(item);
						tri.Add(item2);
					}
				}
			}
		}

		private static void Rot(MeshInstance m, int part, Mat3 r)
		{
			if (part >= 0)
			{
				m.PartRotation[part] = r;
			}
		}

		private static Mat3 RotX(float a)
		{
			return Mat3.RotX(a);
		}

		private static Mat3 RotY(float a)
		{
			return Mat3.RotY(a);
		}

		private static Mat3 RotZ(float a)
		{
			return Mat3.RotZ(a);
		}

		public void Apply(MeshInstance m, DragonPose p, float time)
		{
			m.EnsurePose();
			m.ResetPose();
			if (_body >= 0)
			{
				m.PartOffset[_body] = new Vec3(0f, 0f, p.Bob - p.Crouch * 1.15f);
				m.PartScale[_body] = 1f + 0.025f * p.Chest;
			}
			float[] array = new float[4] { 0.34f, 0.28f, 0.22f, 0.16f };
			float[] array2 = new float[4] { 0.55f, 0.2f, -0.35f, -0.55f };
			float[] array3 = new float[4] { 0.18f, 0.27f, 0.3f, 0.25f };
			float num = 0f;
			float num2 = 0f;
			for (int i = 0; i < 4; i++)
			{
				float num3 = p.NeckPitch * array[i] + p.NeckCurl * array2[i];
				float num4 = p.NeckYaw * array3[i];
				num += num3;
				num2 += num4;
				Rot(m, _neck[i], RotZ(num4) * RotX(0f - num3));
			}
			float num5 = p.HeadPitch - num - p.BodyPitch;
			float a = p.HeadYaw - num2;
			Rot(m, _head, RotZ(a) * RotX(0f - num5) * RotY(p.HeadRoll - p.BodyRoll * 0.6f));
			Rot(m, _jaw, RotX(0.85f * p.Jaw));
			for (int j = 0; j < 6; j++)
			{
				float num6 = 0.3f + 0.14f * (float)j;
				float num7 = p.TailSway * num6 * (float)Math.Sin(p.TailWave - (float)j * 0.75f);
				float num8 = p.TailLift * num6 * (float)Math.Sin(p.TailWave - (float)j * 0.75f - 1.2f);
				float a2 = p.TailPitch * ((j < 3) ? 0.3f : 0.1f) + num8;
				Rot(m, _tail[j], RotZ(p.TailYaw * ((j < 4) ? 0.22f : 0.06f) + num7) * RotX(a2));
			}
			Legs(m, p);
			for (int k = 0; k < 2; k++)
			{
				Wing(m, p, k);
			}
			Skins(m, p, time);
		}

		private void Legs(MeshInstance m, DragonPose p)
		{
			float tuck = p.Tuck;
			float reach = p.Reach;
			float crouch = p.Crouch;
			for (int i = 0; i < 2; i++)
			{
				float num = p.WalkPhase + ((i == 0) ? 0f : MathF.PI);
				float num2 = (float)Math.Sin(num);
				float num3 = Math.Max(0f, (float)Math.Cos(num));
				float num4 = (float)Math.Sin((double)num + Math.PI);
				float num5 = Math.Max(0f, (float)Math.Cos((double)num + Math.PI));
				float stride = p.Stride;
				float a = 0.45f * tuck - 0.55f * reach - 0.2f * crouch + 0.38f * stride * num2;
				float a2 = -0.8f * tuck + 0.25f * reach + 0.55f * crouch + 0.55f * stride * num3;
				Rot(m, (i == 0) ? _legFL : _legFR, RotX(a));
				Rot(m, (i == 0) ? _legFL2 : _legFR2, RotX(a2));
				float a3 = 1.05f * tuck - 0.7f * reach - 0.45f * crouch + 0.34f * stride * num4;
				float a4 = -0.2f * tuck + 0.45f * reach + 0.9f * crouch - 0.45f * stride * num5;
				float a5 = 1.7f * tuck - 0.35f * reach - 0.45f * crouch + 0.3f * stride * num5;
				Rot(m, (i == 0) ? _legBL : _legBR, RotX(a3));
				Rot(m, (i == 0) ? _legBL2 : _legBR2, RotX(a4));
				Rot(m, (i == 0) ? _legBL3 : _legBR3, RotX(a5));
			}
		}

		private void Wing(MeshInstance m, DragonPose p, int s)
		{
			if (_arm[s] >= 0)
			{
				float num = ((s == 0) ? 1f : (-1f));
				Mat3 r = RotY((0f - num) * p.Elev[s]) * RotX(0f - p.Twist[s]) * RotZ(num * p.Sweep[s]);
				Rot(m, _arm[s], r);
				Rot(m, _fore[s], RotZ((0f - num) * p.Elbow[s] * _foldElbow[s]));
				float num2 = p.Wrist[s];
				Rot(m, _hand[s], RotZ(num * num2 * 0.5f));
				float num3 = p.Spread[s];
				for (int i = 0; i < 4; i++)
				{
					float num4 = (1f - num3) * (_foldTo[s, 0] - _foldTo[s, i]) * -1f;
					float num5 = num2 * _foldTo[s, i] + (1f - num2) * num4;
					Rot(m, _finger[s, i], RotZ(num * num5));
				}
			}
		}

		private void Transforms(MeshInstance m)
		{
			MeshModel model = m.Model;
			int num = Model.Parts.Length;
			if (_pm.Length < num)
			{
				_pm = new Mat3[num];
				_pt = new Vec3[num];
			}
			for (int i = 0; i < num; i++)
			{
				MeshPart meshPart = model.Parts[i];
				Mat3 a = m.PartRotation[i];
				float num2 = m.PartScale[i];
				if (num2 != 1f)
				{
					a *= num2;
				}
				Vec3 vec = meshPart.Pivot - a.Mul(meshPart.Pivot) + m.PartOffset[i];
				if (meshPart.Parent >= 0)
				{
					_pm[i] = _pm[meshPart.Parent] * a;
					_pt[i] = _pm[meshPart.Parent].Mul(vec) + _pt[meshPart.Parent];
				}
				else
				{
					_pm[i] = a;
					_pt[i] = vec;
				}
			}
		}

		private Vec3 At(int part, Vec3 rest)
		{
			if (part < 0)
			{
				return rest;
			}
			return _pm[part].Mul(rest) + _pt[part];
		}

		public Vec3 Posed(int part, Vec3 rest)
		{
			return At(part, rest);
		}

		private Vec3 FingerPoint(int s, int k, float u)
		{
			Vec3 vec = _wrist[s];
			Vec3 vec2 = _tip[s, k] - vec;
			float num = ((s == 0) ? 1f : (-1f));
			Vec3 vec3 = Vec3.Cross(vec2, new Vec3(0f, 0f, 1f)).Normalized() * (Len(vec2) * 0.03f * num);
			float num2 = 4f * u * (1f - u) * 0.95f;
			Vec3 rest = vec + vec2 * u - vec3 * num2;
			return At(_finger[s, k], rest);
		}

		private static float Len(Vec3 v)
		{
			return (float)Math.Sqrt(Vec3.Dot(v, v));
		}

		private void Skins(MeshInstance m, DragonPose p, float time)
		{
			Transforms(m);
			int num = Model.Parts.Length;
			for (int i = 0; i < 2; i++)
			{
				MeshPart meshPart = ((m.Model.Parts.Length > num + i) ? m.Model.Parts[num + i] : null);
				if (meshPart == null || _arm[i] < 0)
				{
					continue;
				}
				float num2 = ((i == 0) ? 1f : (-1f));
				Vec3 vec = At(_arm[i], _shoulder[i]);
				Vec3 vec2 = At(_fore[i], _elbow[i]);
				Vec3 vec3 = At(_hand[i], _wrist[i]);
				Vec3 vec4 = At(_body, _hip[i]);
				Vec3 vec5 = At(_body, _flank[i]);
				for (int j = 0; j < 4; j++)
				{
					_tipNow0[i, j] = (_tipNow[j] = FingerPoint(i, j, 1f));
				}
				float val = Len(_tip[i, 3] - _hip[i]);
				float num3 = Len(_tipNow[3] - vec4);
				float num4 = Clamp01(1f - num3 / Math.Max(0.01f, val));
				float num5 = p.Billow[i];
				float num6 = 0.16f + 0.25f * num4;
				for (int k = 0; k < 8; k++)
				{
					float num7 = (float)k / 7f;
					_lead[k] = ((num7 <= 0.42f) ? At(_arm[i], Lerp(_shoulder[i], _elbow[i], num7 / 0.42f)) : At(_fore[i], Lerp(_elbow[i], _wrist[i], (num7 - 0.42f) / 0.58000004f)));
				}
				for (int l = 0; l < 5; l++)
				{
					float num8 = (float)l / 4f;
					Vec3 vec6 = vec5 * 2f - (vec + vec4) * 0.5f;
					_bodyEdge[l] = vec * ((1f - num8) * (1f - num8)) + vec6 * (2f * num8 * (1f - num8)) + vec4 * (num8 * num8);
					_outer[l] = FingerPoint(i, 3, num8);
				}
				_outer[0] = vec3;
				for (int n = 0; n < 8; n++)
				{
					float num9 = (float)n / 7f;
					Vec3 vec7 = Lerp(vec4, _tipNow[3], num9);
					_trail[n] = vec7 + (_lead[n] - vec7) * (num6 * (float)Math.Sin(Math.PI * (double)num9));
				}
				_trail[0] = vec4;
				_trail[7] = _tipNow[3];
				for (int num10 = 0; num10 < 8; num10++)
				{
					float t = (float)num10 / 7f;
					for (int num11 = 0; num11 < 5; num11++)
					{
						float t2 = (float)num11 / 4f;
						Vec3 vec8 = Lerp(_lead[num10], _trail[num10], t2);
						Vec3 vec9 = Lerp(_bodyEdge[num11], _outer[num11], t);
						Vec3 vec10 = Lerp(Lerp(vec, vec4, t2), Lerp(vec3, _tipNow[3], t2), t);
						_grid[num10 * 5 + num11] = vec8 + vec9 - vec10;
					}
				}
				int num12 = 40;
				for (int num13 = 0; num13 < 3; num13++)
				{
					for (int num14 = 0; num14 < 6; num14++)
					{
						float u = (float)num14 / 5f;
						_fa[num14] = FingerPoint(i, num13, u);
						_fb[num14] = FingerPoint(i, num13 + 1, u);
					}
					for (int num15 = 0; num15 < 4; num15++)
					{
						float num16 = (float)num15 / 3f;
						Vec3 vec11 = Lerp(_tipNow[num13], _tipNow[num13 + 1], num16);
						_edge[num15] = vec11 + (vec3 - vec11) * (num6 * 0.55f * (float)Math.Sin(Math.PI * (double)num16));
					}
					for (int num17 = 0; num17 < 6; num17++)
					{
						float t3 = (float)num17 / 5f;
						for (int num18 = 0; num18 < 4; num18++)
						{
							float t4 = (float)num18 / 3f;
							Vec3 vec12 = Lerp(_fa[num17], _fb[num17], t4);
							Vec3 vec13 = Lerp(vec3, _edge[num18], t3);
							Vec3 vec14 = Lerp(Lerp(vec3, vec3, t4), Lerp(_tipNow[num13], _tipNow[num13 + 1], t4), t3);
							_grid[num12 + num17 * 4 + num18] = vec12 + vec13 - vec14;
						}
					}
					num12 += 24;
				}
				Vec3 vec15 = Vec3.Cross(_tipNow[1] - vec3, vec2 - vec3) * num2;
				vec15 = ((Vec3.Dot(vec15, vec15) > 1E-06f) ? vec15.Normalized() : new Vec3(0f, 0f, 1f));
				for (int num19 = 1; num19 < 7; num19++)
				{
					for (int num20 = 1; num20 < 4; num20++)
					{
						float num21 = (float)(Math.Sin(Math.PI * (double)num19 / 7.0) * Math.Sin(Math.PI * (double)num20 / 4.0));
						_grid[num19 * 5 + num20] = _grid[num19 * 5 + num20] + vec15 * (num5 * num21) + new Vec3(0f, 0f, -0.9f * num4 * num21);
					}
				}
				num12 = 40;
				for (int num22 = 0; num22 < 3; num22++)
				{
					for (int num23 = 1; num23 < 6; num23++)
					{
						for (int num24 = 1; num24 < 3; num24++)
						{
							float num25 = (float)(Math.Sin(Math.PI * (double)num24 / 3.0) * Math.Sin(Math.PI / 2.0 * (double)num23 / 5.0));
							_grid[num12 + num23 * 4 + num24] = _grid[num12 + num23 * 4 + num24] + vec15 * (num5 * 0.6f * num25);
						}
					}
					num12 += 24;
				}
				WriteSkin(meshPart, num2);
			}
		}

		private void WriteSkin(MeshPart skin, float side)
		{
			float[] pos = skin.Pos;
			float[] nrm = skin.Nrm;
			for (int i = 0; i < 112; i++)
			{
				Vec3 vec = _grid[i];
				int num = i * 3;
				int num2 = (i + 112) * 3;
				pos[num] = (pos[num2] = vec.X);
				pos[num + 1] = (pos[num2 + 1] = vec.Y);
				pos[num + 2] = (pos[num2 + 2] = vec.Z);
				nrm[num] = (nrm[num + 1] = (nrm[num + 2] = 0f));
			}
			int[] skinTri = _skinTri;
			for (int j = 0; j < skinTri.Length; j += 3)
			{
				Vec3 vec2 = _grid[skinTri[j]];
				Vec3 vec3 = _grid[skinTri[j + 1]];
				Vec3 vec4 = Vec3.Cross(b: _grid[skinTri[j + 2]] - vec2, a: vec3 - vec2) * side;
				for (int k = 0; k < 3; k++)
				{
					int num3 = skinTri[j + k] * 3;
					nrm[num3] += vec4.X;
					nrm[num3 + 1] += vec4.Y;
					nrm[num3 + 2] += vec4.Z;
				}
				int num4 = j / 3;
				float num5 = Math.Abs(vec4.X);
				float num6 = Math.Abs(vec4.Y);
				float num7 = Math.Abs(vec4.Z);
				byte b = (byte)((!(num5 >= num6) || !(num5 >= num7)) ? ((num6 >= num7) ? 1u : 2u) : 0u);
				skin.Axis[num4] = b;
				skin.Axis[num4 + skinTri.Length / 3] = b;
			}
			for (int l = 0; l < 112; l++)
			{
				int num8 = l * 3;
				int num9 = (l + 112) * 3;
				float num10 = nrm[num8];
				float num11 = nrm[num8 + 1];
				float num12 = nrm[num8 + 2];
				float num13 = (float)Math.Sqrt(num10 * num10 + num11 * num11 + num12 * num12);
				if (num13 < 1E-09f)
				{
					num10 = 0f;
					num11 = 0f;
					num12 = 1f;
					num13 = 1f;
				}
				num10 /= num13;
				num11 /= num13;
				num12 /= num13;
				nrm[num8] = num10;
				nrm[num8 + 1] = num11;
				nrm[num8 + 2] = num12;
				nrm[num9] = num10;
				nrm[num9 + 1] = num11;
				nrm[num9 + 2] = num12;
			}
			skin.Version++;
		}

		private static Vec3 Lerp(Vec3 a, Vec3 b, float t)
		{
			return a + (b - a) * t;
		}

		private static float Clamp01(float v)
		{
			if (!(v < 0f))
			{
				if (!(v > 1f))
				{
					return v;
				}
				return 1f;
			}
			return 0f;
		}

		public static void Place(MeshInstance m, Vec3 at, float heading, float pitch, float roll, float scale)
		{
			m.Place(at.X, at.Y, at.Z, heading, scale, scale, scale);
			if (pitch != 0f || roll != 0f)
			{
				Mat3 b = Mat3.RotX(0f - pitch) * Mat3.RotY(roll);
				Vec3 vec = new Vec3(0f, 0f, 4.6f);
				Mat3 a = m.Basis;
				m.Basis = a * b;
				m.Position = at + a.Mul(vec - b.Mul(vec));
			}
		}

		public Vec3 Tip(int s, int k)
		{
			return _tipNow0[s, k];
		}
	}
}
