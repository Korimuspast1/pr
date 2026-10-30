using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class CreatureRig
	{
		private int _legFL2 = -1;

		private int _legFR2 = -1;

		private int _legBL2 = -1;

		private int _legBR2 = -1;

		private int _legL2 = -1;

		private int _legR2 = -1;

		private int _jaw = -1;

		private bool _beast;

		private bool _peck;

		private bool _prowl;

		private bool _pant;

		private bool _young;

		private float _peckAngle;

		private float _prowlHead;

		private float _prowlTail;

		private const float FoldFront = 1.05f;

		private const float FoldHind = 0.7f;

		private const float FoldBird = 1.1f;

		public readonly MeshModel Model;

		public readonly string Kind;

		private readonly int _body;

		private readonly int _head;

		private readonly int _tail;

		private readonly int _flames;

		private readonly int _clawL;

		private readonly int _clawR;

		private readonly int _wingL;

		private readonly int _wingR;

		private readonly int _wingL2;

		private readonly int _wingR2;

		private readonly int _halo;

		private readonly int _legFL;

		private readonly int _legFR;

		private readonly int _legBL;

		private readonly int _legBR;

		private readonly int _legL;

		private readonly int _legR;

		private readonly int[] _insectLegs;

		private readonly int[] _segs;

		private Vec3? _mouth;

		private float _height;

		private float _length;

		private float _legLength;

		private bool _insect;

		private bool _dragon;

		private bool _biped;

		private bool _snowman;

		private bool _seraph;

		private bool _fish;

		private bool _floating;

		private bool _ghost;

		private bool _blob;

		private bool[] _posed;

		public bool Young => _young;

		public bool Prowls => _prowl;

		public float Height
		{
			get
			{
				if (!RigTune.Creature)
				{
					return HeightOld;
				}
				return _height;
			}
		}

		public float Length
		{
			get
			{
				if (!RigTune.Creature)
				{
					return LengthOld;
				}
				return _length;
			}
		}

		public float LegLength
		{
			get
			{
				if (!RigTune.Creature)
				{
					return LegLengthOld;
				}
				return _legLength;
			}
		}

		private float HeightOld => Model.InfoFloat("height", Model.Max.Z - Math.Max(0f, Model.Min.Z));

		private float LengthOld => Model.InfoFloat("length", Model.Max.Y - Model.Min.Y);

		private float LegLengthOld
		{
			get
			{
				int num = ((_legFL >= 0) ? _legFL : _legL);
				if (num < 0)
				{
					return 0f;
				}
				return Math.Max(0f, Model.Parts[num].Pivot.Z - Math.Max(0f, Model.Min.Z));
			}
		}

		public float SwingPerStride
		{
			get
			{
				if (_legFL < 0)
				{
					return 0.6f;
				}
				return 0.55f;
			}
		}

		public int HeadPart => _head;

		public Vec3 Mouth
		{
			get
			{
				if (_mouth.HasValue)
				{
					return _mouth.Value;
				}
				Vec3 vec = new Vec3(0f, 0f, 0f);
				if (_head >= 0)
				{
					MeshPart meshPart = Model.Parts[_head];
					float num = float.MaxValue;
					for (int i = 0; i < meshPart.VertexCount; i++)
					{
						if (meshPart.Pos[i * 3 + 1] < num)
						{
							num = meshPart.Pos[i * 3 + 1];
						}
					}
					float num2 = 0f;
					float num3 = 0f;
					int num4 = 0;
					for (int j = 0; j < meshPart.VertexCount; j++)
					{
						if (meshPart.Pos[j * 3 + 1] < num + 0.06f)
						{
							num2 += meshPart.Pos[j * 3];
							num3 += meshPart.Pos[j * 3 + 2];
							num4++;
						}
					}
					vec = new Vec3((num4 > 0) ? (num2 / (float)num4) : 0f, num, (num4 > 0) ? (num3 / (float)num4) : 0f);
				}
				_mouth = vec;
				return vec;
			}
		}

		private void ReadBeast()
		{
			MeshModel model = Model;
			_young = model.Info("young") == "1";
			if (!(Kind != "quadruped") || !(Kind != "bird"))
			{
				_legFL2 = model.PartIndex("leg_fl2");
				_legFR2 = model.PartIndex("leg_fr2");
				_legBL2 = model.PartIndex("leg_bl2");
				_legBR2 = model.PartIndex("leg_br2");
				_legL2 = model.PartIndex("leg_l2");
				_legR2 = model.PartIndex("leg_r2");
				_jaw = model.PartIndex("jaw");
				_peck = model.Info("peck") == "1" && _head >= 0;
				_prowl = model.Info("prowl") == "1";
				_pant = model.Info("pant") == "1";
				_prowlHead = model.InfoFloat("prowl_head", 0.3f);
				_prowlTail = model.InfoFloat("prowl_tail", 0.75f);
				_beast = _legFL2 >= 0 || _legFR2 >= 0 || _legBL2 >= 0 || _legBR2 >= 0 || _legL2 >= 0 || _legR2 >= 0 || _jaw >= 0 || _peck;
				if (_peck)
				{
					_peckAngle = PeckAngle();
				}
			}
		}

		private float PeckAngle()
		{
			Vec3 pivot = Model.Parts[_head].Pivot;
			Vec3 vec = Mouth - pivot;
			for (float num = 0.2f; num < 1.7f; num += 0.02f)
			{
				if (pivot.Z + vec.Y * (float)Math.Sin(num) + vec.Z * (float)Math.Cos(num) <= 0.012f)
				{
					return num;
				}
			}
			return 1.2f;
		}

		private void PoseBeast(MeshInstance m, float stride, float phase, float time, float attack, float seed)
		{
			if (!_beast)
			{
				return;
			}
			float num = (float)Math.Cos(phase);
			float num2 = stride * Math.Max(0f, num);
			float num3 = stride * Math.Max(0f, 0f - num);
			if (_legFL2 >= 0)
			{
				m.PartRotation[_legFL2] = Mat3.RotX(1.05f * num2);
			}
			if (_legBR2 >= 0)
			{
				m.PartRotation[_legBR2] = Mat3.RotX(0.7f * num2);
			}
			if (_legFR2 >= 0)
			{
				m.PartRotation[_legFR2] = Mat3.RotX(1.05f * num3);
			}
			if (_legBL2 >= 0)
			{
				m.PartRotation[_legBL2] = Mat3.RotX(0.7f * num3);
			}
			if (_legL2 >= 0)
			{
				m.PartRotation[_legL2] = Mat3.RotX(1.1f * num2);
			}
			if (_legR2 >= 0)
			{
				m.PartRotation[_legR2] = Mat3.RotX(1.1f * num3);
			}
			if (_jaw >= 0)
			{
				float num4 = ((attack > 0f) ? ((float)Math.Sin((double)attack * Math.PI)) : 0f);
				float a = 0.55f * num4 + (_pant ? (0.13f * Math.Max(0f, stride - 0.45f)) : 0f);
				m.PartRotation[_jaw] = Mat3.RotX(a);
				if (num4 > 0f && _head >= 0)
				{
					m.PartRotation[_head] = m.PartRotation[_head] * Mat3.RotX(0.5f * num4);
				}
			}
			if (_peck)
			{
				PoseChickenHead(m, stride, phase, time, seed);
			}
		}

		private void PoseChickenHead(MeshInstance m, float stride, float phase, float time, float seed)
		{
			if (stride > 0.02f)
			{
				float num = phase / MathF.PI;
				num -= (float)Math.Floor(num);
				float num2 = (((num < 0.7f) ? (num / 0.7f) : (1f - Smooth((num - 0.7f) / 0.3f))) - 0.5f) * 0.7f * _legLength * Math.Min(1f, stride);
				m.PartOffset[_head] = m.PartOffset[_head] + new Vec3(0f, num2, -0.15f * Math.Abs(num2));
			}
			float num3 = 1f - Math.Min(1f, stride * 4f);
			if (!(num3 <= 0f))
			{
				float num4 = seed * 7.13f;
				float num5 = 4.5f + 3f * (num4 - (float)Math.Floor(num4));
				float num6 = (time + seed * 13.7f) % num5;
				if (!(num6 >= 1.05f))
				{
					float num7 = num6 / 1.05f * 3f;
					num7 -= (float)Math.Floor(num7);
					float num8 = ((num7 < 0.4f) ? Smooth(num7 / 0.4f) : (1f - Smooth((num7 - 0.4f) / 0.6f)));
					m.PartRotation[_head] = m.PartRotation[_head] * Mat3.RotX(_peckAngle * num8 * num3);
				}
			}
		}

		public void Prowl(MeshInstance m, float k)
		{
			if (_prowl && !(k <= 0f))
			{
				m.EnsurePose();
				if (_head >= 0)
				{
					m.PartRotation[_head] = m.PartRotation[_head] * Mat3.RotX(_prowlHead * k);
				}
				if (_tail >= 0)
				{
					m.PartRotation[_tail] = m.PartRotation[_tail] * Mat3.RotX(_prowlTail * k);
				}
				if (_jaw >= 0)
				{
					m.PartRotation[_jaw] = m.PartRotation[_jaw] * Mat3.RotX(0.07f * k);
				}
			}
		}

		private static float Smooth(float t)
		{
			t = ((t < 0f) ? 0f : ((t > 1f) ? 1f : t));
			return t * t * (3f - 2f * t);
		}

		public CreatureRig(MeshModel m)
		{
			Model = m;
			Kind = m.Info("kind") ?? "";
			_body = m.PartIndex("body");
			_head = m.PartIndex("head");
			_tail = m.PartIndex("tail");
			_flames = m.PartIndex("flames");
			_clawL = m.PartIndex("claw_l");
			_clawR = m.PartIndex("claw_r");
			_wingL = m.PartIndex("wing_l");
			_wingR = m.PartIndex("wing_r");
			_wingL2 = m.PartIndex("wing_l2");
			_wingR2 = m.PartIndex("wing_r2");
			_halo = m.PartIndex("halo");
			_legFL = m.PartIndex("leg_fl");
			_legFR = m.PartIndex("leg_fr");
			_legBL = m.PartIndex("leg_bl");
			_legBR = m.PartIndex("leg_br");
			_legL = m.PartIndex("leg_l");
			_legR = m.PartIndex("leg_r");
			List<int> list = new List<int>();
			for (int i = 1; i <= 8; i++)
			{
				int num = m.PartIndex("leg_" + i);
				if (num >= 0)
				{
					list.Add(num);
				}
			}
			_insectLegs = list.ToArray();
			List<int> list2 = new List<int>();
			for (int j = 1; j <= 12; j++)
			{
				int num2 = m.PartIndex("seg_" + j);
				if (num2 >= 0)
				{
					list2.Add(num2);
				}
			}
			_segs = list2.ToArray();
			ReadOnce();
			ReadBeast();
		}

		public void Pose(MeshInstance m, float stride, float phase, float time, bool flying, float attack, float seed)
		{
			if (RigTune.Creature)
			{
				PoseFast(m, stride, phase, time, flying, attack, seed);
			}
			else
			{
				PoseOld(m, stride, phase, time, flying, attack, seed);
			}
			PoseBeast(m, stride, phase, time, attack, seed);
		}

		internal void PoseOld(MeshInstance m, float stride, float phase, float time, bool flying, float attack, float seed)
		{
			m.EnsurePose();
			m.ResetPose();
			float num = (float)Math.Sin(phase);
			float num2 = (float)Math.Cos(phase);
			float num3 = 0.55f * stride;
			Rot(m, _legFL, Mat3.RotX((0f - num3) * num));
			Rot(m, _legBR, Mat3.RotX((0f - num3) * num));
			Rot(m, _legFR, Mat3.RotX(num3 * num));
			Rot(m, _legBL, Mat3.RotX(num3 * num));
			float num4 = 0.6f * stride;
			Rot(m, _legL, Mat3.RotX((0f - num4) * num));
			Rot(m, _legR, Mat3.RotX(num4 * num));
			for (int i = 0; i < _insectLegs.Length; i++)
			{
				float num5 = (((i / 2 + i % 2) % 2 == 0) ? 1f : (-1f));
				Rot(m, _insectLegs[i], Mat3.RotZ(0.35f * stride * num * num5) * Mat3.RotX(0.12f * stride * Math.Max(0f, num2 * num5)));
			}
			if (_wingL >= 0 || _wingR >= 0)
			{
				float num6 = ((Kind == "insect") ? 32f : ((Kind == "dragon") ? 3.2f : ((Kind == "biped") ? 14f : 11f)));
				float num7 = (flying ? (0.15f + 0.75f * (float)Math.Sin(time * num6 + seed)) : ((Kind == "insect") ? 0.2f : (0.05f * (float)Math.Sin(time * 1.3f + seed))));
				if (Kind == "snowman" || (Kind == "biped" && !flying))
				{
					num7 = 0.1f * (float)Math.Sin(phase) * stride - 0.05f;
				}
				if (Kind == "seraph")
				{
					num7 = (flying ? 0.2f : 0.1f) + 0.38f * (float)Math.Sin(time * 3.4f + seed);
				}
				Rot(m, _wingL, Mat3.RotY(num7));
				Rot(m, _wingR, Mat3.RotY(0f - num7));
				float num8 = ((Kind == "seraph") ? (0.05f + 0.26f * (float)Math.Sin(time * 3.4f + seed - 1.1f)) : (num7 * 0.8f));
				Rot(m, _wingL2, Mat3.RotY(0f - num8));
				Rot(m, _wingR2, Mat3.RotY(num8));
			}
			if (_halo >= 0)
			{
				m.PartRotation[_halo] = Mat3.RotZ(time * 1.2f + seed);
			}
			if (flying && Kind == "dragon")
			{
				Rot(m, _legFL, Mat3.RotX(0.75f));
				Rot(m, _legFR, Mat3.RotX(0.75f));
				Rot(m, _legBL, Mat3.RotX(1f));
				Rot(m, _legBR, Mat3.RotX(1f));
			}
			Rot(m, _tail, Mat3.RotZ((0.18f + 0.25f * stride) * (float)Math.Sin(time * (2.2f + 4f * stride) + seed)));
			for (int j = 0; j < _segs.Length; j++)
			{
				float num9 = (0.18f + 0.3f * Math.Min(1f, stride + 0.25f)) * ((Kind == "fish") ? (0.5f + 0.5f * (float)j) : 1f) / ((Kind == "fish") ? 1f : 1.6f);
				Rot(m, _segs[j], Mat3.RotZ(num9 * (float)Math.Sin(phase * 1f - (float)j * 0.9f + seed)));
			}
			float num10 = ((attack > 0f) ? ((float)Math.Sin((double)attack * Math.PI)) : (0.06f * (float)Math.Sin(time * 2f + seed)));
			Rot(m, _clawL, Mat3.RotZ(-0.35f * num10) * Mat3.RotX(-0.3f * num10));
			Rot(m, _clawR, Mat3.RotZ(0.35f * num10) * Mat3.RotX(-0.3f * num10));
			float a = (1f - stride) * 0.35f * (float)Math.Sin(time * 0.45f + seed * 3f);
			float num11 = ((attack > 0f) ? (-0.35f * (float)Math.Sin((double)attack * Math.PI)) : 0f);
			Rot(m, _head, Mat3.RotZ(a) * Mat3.RotX(0.07f * stride * (float)Math.Sin(phase * 2f) + num11));
			if (_flames >= 0)
			{
				m.PartRotation[_flames] = Mat3.RotZ(time * 1.3f);
				m.PartScale[_flames] = 0.9f + 0.12f * (float)Math.Sin(time * 11f + seed);
			}
			if (_body >= 0)
			{
				float num12 = ((Kind == "floating" || Kind == "ghost") ? (0.06f * (float)Math.Sin(time * 1.6f + seed)) : ((Kind == "seraph") ? (0.07f * (float)Math.Sin(time * 1.7f + seed)) : ((Kind == "blob") ? (0.03f * Math.Abs((float)Math.Sin(phase)) + 0.01f * (float)Math.Sin(time * 3f)) : (0.02f * stride * Math.Abs(num2)))));
				m.PartOffset[_body] = new Vec3(0f, 0f, num12 * Height);
				if (Kind == "floating")
				{
					m.PartRotation[_body] = Mat3.RotZ(time * 0.8f + seed);
				}
				else if (Kind == "seraph")
				{
					m.PartRotation[_body] = Mat3.RotX(0.05f * (float)Math.Sin(time * 3.4f + seed + 0.6f)) * Mat3.RotY(0.06f * (float)Math.Sin(time * 0.9f + seed));
				}
				else if (attack > 0f && _head < 0)
				{
					m.PartRotation[_body] = Mat3.RotX(-0.25f * (float)Math.Sin((double)attack * Math.PI));
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

		public void Breathe(MeshInstance m, float k, float time)
		{
			if (!(k <= 0f))
			{
				m.EnsurePose();
				if (_head >= 0)
				{
					m.PartRotation[_head] = Mat3.RotX(0.32f * k) * Mat3.RotZ(0.035f * k * (float)Math.Sin(time * 31f));
				}
				if (_wingL >= 0)
				{
					m.PartRotation[_wingL] = m.PartRotation[_wingL] * Mat3.RotY(0.45f * k);
				}
				if (_wingR >= 0)
				{
					m.PartRotation[_wingR] = m.PartRotation[_wingR] * Mat3.RotY(-0.45f * k);
				}
			}
		}

		private void ReadOnce()
		{
			_height = HeightOld;
			_length = LengthOld;
			_legLength = LegLengthOld;
			_insect = Kind == "insect";
			_dragon = Kind == "dragon";
			_biped = Kind == "biped";
			_snowman = Kind == "snowman";
			_seraph = Kind == "seraph";
			_fish = Kind == "fish";
			_floating = Kind == "floating";
			_ghost = Kind == "ghost";
			_blob = Kind == "blob";
			FindPosed();
		}

		private void FindPosed()
		{
			bool[] posed = new bool[Model.Parts.Length];
			Set(_legFL);
			Set(_legBR);
			Set(_legFR);
			Set(_legBL);
			Set(_legL);
			Set(_legR);
			int[] insectLegs = _insectLegs;
			for (int i = 0; i < insectLegs.Length; i++)
			{
				Set(insectLegs[i]);
			}
			if (_wingL >= 0 || _wingR >= 0)
			{
				Set(_wingL);
				Set(_wingR);
				Set(_wingL2);
				Set(_wingR2);
			}
			Set(_halo);
			Set(_tail);
			insectLegs = _segs;
			for (int i = 0; i < insectLegs.Length; i++)
			{
				Set(insectLegs[i]);
			}
			Set(_clawL);
			Set(_clawR);
			Set(_head);
			Set(_flames);
			_posed = posed;
			void Set(int num)
			{
				if (num >= 0 && num < posed.Length)
				{
					posed[num] = true;
				}
			}
		}

		private void ResetUnposed(MeshInstance m)
		{
			bool[] posed = _posed;
			Mat3[] partRotation = m.PartRotation;
			if (posed == null || partRotation == null || partRotation.Length != posed.Length)
			{
				m.ResetPose();
				return;
			}
			for (int i = 0; i < partRotation.Length; i++)
			{
				if (!posed[i])
				{
					partRotation[i] = Turns.Ident;
				}
			}
			if (m.PartOffset != null)
			{
				Array.Clear(m.PartOffset, 0, m.PartOffset.Length);
			}
			if (m.PartScale != null)
			{
				for (int j = 0; j < m.PartScale.Length; j++)
				{
					m.PartScale[j] = 1f;
				}
			}
		}

		internal void PoseFast(MeshInstance m, float stride, float phase, float time, bool flying, float attack, float seed)
		{
			m.EnsurePose();
			ResetUnposed(m);
			float num = (float)Math.Sin(phase);
			float num2 = (float)Math.Cos(phase);
			float num3 = 0.55f * stride;
			if (_legFL >= 0)
			{
				Rot(m, _legFL, Turns.X((0f - num3) * num));
			}
			if (_legBR >= 0)
			{
				Rot(m, _legBR, Turns.X((0f - num3) * num));
			}
			if (_legFR >= 0)
			{
				Rot(m, _legFR, Turns.X(num3 * num));
			}
			if (_legBL >= 0)
			{
				Rot(m, _legBL, Turns.X(num3 * num));
			}
			float num4 = 0.6f * stride;
			if (_legL >= 0)
			{
				Rot(m, _legL, Turns.X((0f - num4) * num));
			}
			if (_legR >= 0)
			{
				Rot(m, _legR, Turns.X(num4 * num));
			}
			for (int i = 0; i < _insectLegs.Length; i++)
			{
				float num5 = (((i / 2 + i % 2) % 2 == 0) ? 1f : (-1f));
				Rot(m, _insectLegs[i], Turns.ZX(0.35f * stride * num * num5, 0.12f * stride * Math.Max(0f, num2 * num5)));
			}
			if (_wingL >= 0 || _wingR >= 0)
			{
				float num6 = (_insect ? 32f : (_dragon ? 3.2f : (_biped ? 14f : 11f)));
				float num7 = (flying ? (0.15f + 0.75f * (float)Math.Sin(time * num6 + seed)) : (_insect ? 0.2f : (0.05f * (float)Math.Sin(time * 1.3f + seed))));
				if (_snowman || (_biped && !flying))
				{
					num7 = 0.1f * (float)Math.Sin(phase) * stride - 0.05f;
				}
				if (_seraph)
				{
					num7 = (flying ? 0.2f : 0.1f) + 0.38f * (float)Math.Sin(time * 3.4f + seed);
				}
				if (_wingL >= 0)
				{
					Rot(m, _wingL, Mat3.RotY(num7));
				}
				if (_wingR >= 0)
				{
					Rot(m, _wingR, Mat3.RotY(0f - num7));
				}
				if (_wingL2 >= 0 || _wingR2 >= 0)
				{
					float num8 = (_seraph ? (0.05f + 0.26f * (float)Math.Sin(time * 3.4f + seed - 1.1f)) : (num7 * 0.8f));
					if (_wingL2 >= 0)
					{
						Rot(m, _wingL2, Mat3.RotY(0f - num8));
					}
					if (_wingR2 >= 0)
					{
						Rot(m, _wingR2, Mat3.RotY(num8));
					}
				}
			}
			if (_halo >= 0)
			{
				m.PartRotation[_halo] = Mat3.RotZ(time * 1.2f + seed);
			}
			if (flying && _dragon)
			{
				if (_legFL >= 0)
				{
					Rot(m, _legFL, Mat3.RotX(0.75f));
				}
				if (_legFR >= 0)
				{
					Rot(m, _legFR, Mat3.RotX(0.75f));
				}
				if (_legBL >= 0)
				{
					Rot(m, _legBL, Mat3.RotX(1f));
				}
				if (_legBR >= 0)
				{
					Rot(m, _legBR, Mat3.RotX(1f));
				}
			}
			if (_tail >= 0)
			{
				Rot(m, _tail, Mat3.RotZ((0.18f + 0.25f * stride) * (float)Math.Sin(time * (2.2f + 4f * stride) + seed)));
			}
			for (int j = 0; j < _segs.Length; j++)
			{
				float num9 = (0.18f + 0.3f * Math.Min(1f, stride + 0.25f)) * (_fish ? (0.5f + 0.5f * (float)j) : 1f) / (_fish ? 1f : 1.6f);
				Rot(m, _segs[j], Mat3.RotZ(num9 * (float)Math.Sin(phase * 1f - (float)j * 0.9f + seed)));
			}
			if (_clawL >= 0 || _clawR >= 0)
			{
				float num10 = ((attack > 0f) ? ((float)Math.Sin((double)attack * Math.PI)) : (0.06f * (float)Math.Sin(time * 2f + seed)));
				if (_clawL >= 0)
				{
					Rot(m, _clawL, Turns.ZX(-0.35f * num10, -0.3f * num10));
				}
				if (_clawR >= 0)
				{
					Rot(m, _clawR, Turns.ZX(0.35f * num10, -0.3f * num10));
				}
			}
			if (_head >= 0)
			{
				float z = (1f - stride) * 0.35f * (float)Math.Sin(time * 0.45f + seed * 3f);
				float num11 = ((attack > 0f) ? (-0.35f * (float)Math.Sin((double)attack * Math.PI)) : 0f);
				Rot(m, _head, Turns.ZX(z, 0.07f * stride * (float)Math.Sin(phase * 2f) + num11));
			}
			if (_flames >= 0)
			{
				m.PartRotation[_flames] = Mat3.RotZ(time * 1.3f);
				m.PartScale[_flames] = 0.9f + 0.12f * (float)Math.Sin(time * 11f + seed);
			}
			if (_body >= 0)
			{
				float num12 = ((_floating || _ghost) ? (0.06f * (float)Math.Sin(time * 1.6f + seed)) : (_seraph ? (0.07f * (float)Math.Sin(time * 1.7f + seed)) : (_blob ? (0.03f * Math.Abs((float)Math.Sin(phase)) + 0.01f * (float)Math.Sin(time * 3f)) : (0.02f * stride * Math.Abs(num2)))));
				m.PartOffset[_body] = new Vec3(0f, 0f, num12 * _height);
				if (_floating)
				{
					m.PartRotation[_body] = Mat3.RotZ(time * 0.8f + seed);
				}
				else if (_seraph)
				{
					m.PartRotation[_body] = Turns.XY(0.05f * (float)Math.Sin(time * 3.4f + seed + 0.6f), 0.06f * (float)Math.Sin(time * 0.9f + seed));
				}
				else if (attack > 0f && _head < 0)
				{
					m.PartRotation[_body] = Mat3.RotX(-0.25f * (float)Math.Sin((double)attack * Math.PI));
				}
			}
		}
	}
}
