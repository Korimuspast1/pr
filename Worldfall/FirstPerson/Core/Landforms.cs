using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class Landforms
	{
		public const byte Water = 0;

		public const byte Lowland = 1;

		public const byte Hills = 2;

		public const byte Mountains = 3;

		public const byte Summit = 4;

		public int Width;

		public int Height;

		public byte[] Form = new byte[0];

		public float[] LandDepth = new float[0];

		public float[] HillDepth = new float[0];

		public float[] MountainDepth = new float[0];

		public float[] SummitDepth = new float[0];

		public float[] RangeSize = new float[0];

		private byte[] _a = new byte[0];

		private byte[] _b = new byte[0];

		private float[] _scratch = new float[0];

		private int[] _label = new int[0];

		public static float PassDepth = 2f;

		public static bool OutOfPlace(int heightMin, bool liquid, byte form)
		{
			if (!liquid && heightMin >= 98 && form >= 1)
			{
				return TerrainModel.ClassOf(heightMin) != form;
			}
			return false;
		}

		public void Compute(byte[] codes, int w, int h)
		{
			int num = w * h;
			if (Form.Length != num)
			{
				Form = new byte[num];
				LandDepth = new float[num];
				HillDepth = new float[num];
				MountainDepth = new float[num];
				SummitDepth = new float[num];
				RangeSize = new float[num];
				_a = new byte[num];
				_b = new byte[num];
				_scratch = new float[num];
				_label = new int[num];
			}
			Width = w;
			Height = h;
			byte[] array = new byte[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = ((codes[i] >= 2) ? ((byte)1) : ((byte)0));
			}
			Clean(array, codes, w, h);
			byte[] array2 = new byte[num];
			for (int j = 0; j < num; j++)
			{
				array2[j] = ((codes[j] >= 3) ? ((byte)1) : ((byte)0));
			}
			Clean(array2, codes, w, h);
			for (int k = 0; k < num; k++)
			{
				Form[k] = (byte)((codes[k] != 0) ? ((array2[k] != 0) ? 3 : ((array[k] == 0) ? 1 : 2)) : 0);
			}
			Chamfer(codes, 1, w, h, LandDepth);
			Chamfer(Form, 2, w, h, HillDepth);
			Chamfer(Form, 3, w, h, MountainDepth);
			for (int l = 0; l < num; l++)
			{
				_a[l] = (byte)((codes[l] == 4 && Form[l] == 3) ? 1u : 0u);
			}
			Chamfer(_a, 1, w, h, SummitDepth);
			for (int m = 0; m < 2; m++)
			{
				Smooth(MountainDepth, Form, 3, w, h);
				Smooth(HillDepth, Form, 2, w, h);
				Smooth(LandDepth, codes, 1, w, h);
				Smooth(SummitDepth, _a, 1, w, h);
			}
			Ranges(w, h);
			Passes(codes, w, h);
		}

		private void Passes(byte[] codes, int w, int h)
		{
			int num = w * h;
			int[] label = _label;
			Array.Clear(label, 0, num);
			Stack<int> stack = new Stack<int>();
			List<int> list = new List<int>();
			for (int i = 0; i < num; i++)
			{
				if (label[i] != 0 || !Sealed(codes, i))
				{
					continue;
				}
				list.Clear();
				bool flag = false;
				float num2 = 0f;
				label[i] = 1;
				stack.Push(i);
				while (stack.Count > 0)
				{
					int num3 = stack.Pop();
					int num4 = num3 % w;
					int num5 = num3 / w;
					list.Add(num3);
					num2 = Math.Max(num2, (Form[num3] == 3) ? MountainDepth[num3] : HillDepth[num3]);
					for (int j = -1; j <= 1; j++)
					{
						int num6 = num5 + j;
						if (num6 < 0 || num6 >= h)
						{
							continue;
						}
						for (int k = -1; k <= 1; k++)
						{
							int num7 = num4 + k;
							if (num7 >= 0 && num7 < w)
							{
								int num8 = num6 * w + num7;
								if (Form[num8] <= 1)
								{
									flag = true;
								}
								else if (label[num8] == 0 && Sealed(codes, num8))
								{
									label[num8] = 1;
									stack.Push(num8);
								}
							}
						}
					}
				}
				if (!flag || num2 < PassDepth)
				{
					continue;
				}
				foreach (int item in list)
				{
					Form[item] = 1;
				}
				foreach (int item2 in list)
				{
					int num9 = item2 % w;
					int num10 = item2 / w;
					for (int l = -1; l <= 1; l++)
					{
						for (int m = -1; m <= 1; m++)
						{
							int num11 = num9 + m;
							int num12 = num10 + l;
							if (num11 >= 0 && num12 >= 0 && num11 < w && num12 < h)
							{
								int num13 = num12 * w + num11;
								if (Form[num13] >= 2)
								{
									MountainDepth[num13] = Math.Min(MountainDepth[num13], 0.3f);
									HillDepth[num13] = Math.Min(HillDepth[num13], 0.5f);
								}
							}
						}
					}
				}
			}
		}

		private bool Sealed(byte[] codes, int i)
		{
			if (codes[i] == 1)
			{
				return Form[i] >= 2;
			}
			return false;
		}

		private void Clean(byte[] mask, byte[] codes, int w, int h)
		{
			Dilate(mask, _a, w, h);
			Erode(_a, _b, w, h);
			for (int i = 0; i < mask.Length; i++)
			{
				_b[i] = (byte)((codes[i] != 0) ? _b[i] : 0);
			}
			Erode(_b, _a, w, h);
			Dilate(_a, mask, w, h);
			for (int j = 0; j < mask.Length; j++)
			{
				mask[j] &= _b[j];
			}
		}

		private static void Dilate(byte[] src, byte[] dst, int w, int h)
		{
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					byte b = 0;
					for (int k = -1; k <= 1; k++)
					{
						if (b != 0)
						{
							break;
						}
						int num = i + k;
						if (num < 0 || num >= h)
						{
							continue;
						}
						int num2 = num * w;
						for (int l = -1; l <= 1; l++)
						{
							int num3 = j + l;
							if (num3 >= 0 && num3 < w && src[num2 + num3] != 0)
							{
								b = 1;
								break;
							}
						}
					}
					dst[i * w + j] = b;
				}
			}
		}

		private static void Erode(byte[] src, byte[] dst, int w, int h)
		{
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					byte b = 1;
					for (int k = -1; k <= 1; k++)
					{
						if (b == 0)
						{
							break;
						}
						int num = i + k;
						if (num < 0 || num >= h)
						{
							continue;
						}
						int num2 = num * w;
						for (int l = -1; l <= 1; l++)
						{
							int num3 = j + l;
							if (num3 >= 0 && num3 < w && src[num2 + num3] == 0)
							{
								b = 0;
								break;
							}
						}
					}
					dst[i * w + j] = b;
				}
			}
		}

		private void Smooth(float[] d, byte[] codes, byte minCode, int w, int h)
		{
			float[] scratch = _scratch;
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					int num = i * w + j;
					if (codes[num] < minCode)
					{
						scratch[num] = 0f;
						continue;
					}
					float num2 = 0f;
					int num3 = 0;
					for (int k = -1; k <= 1; k++)
					{
						int num4 = i + k;
						if (num4 < 0 || num4 >= h)
						{
							continue;
						}
						for (int l = -1; l <= 1; l++)
						{
							int num5 = j + l;
							if (num5 >= 0 && num5 < w)
							{
								num2 += d[num4 * w + num5];
								num3++;
							}
						}
					}
					scratch[num] = Math.Max(num2 / (float)num3, d[num] * 0.6f);
				}
			}
			Array.Copy(scratch, d, d.Length);
		}

		private static void Chamfer(byte[] codes, byte minCode, int w, int h, float[] d)
		{
			int num = w * h;
			for (int i = 0; i < num; i++)
			{
				d[i] = ((codes[i] >= minCode) ? 64f : 0f);
			}
			for (int j = 0; j < h; j++)
			{
				int num2 = j * w;
				for (int k = 0; k < w; k++)
				{
					int num3 = num2 + k;
					float num4 = d[num3];
					if (num4 == 0f)
					{
						continue;
					}
					if (k > 0)
					{
						num4 = Math.Min(num4, d[num3 - 1] + 1f);
					}
					if (j > 0)
					{
						num4 = Math.Min(num4, d[num3 - w] + 1f);
						if (k > 0)
						{
							num4 = Math.Min(num4, d[num3 - w - 1] + 1.4142f);
						}
						if (k < w - 1)
						{
							num4 = Math.Min(num4, d[num3 - w + 1] + 1.4142f);
						}
					}
					d[num3] = num4;
				}
			}
			for (int num5 = h - 1; num5 >= 0; num5--)
			{
				int num6 = num5 * w;
				for (int num7 = w - 1; num7 >= 0; num7--)
				{
					int num8 = num6 + num7;
					float num9 = d[num8];
					if (num9 != 0f)
					{
						if (num7 < w - 1)
						{
							num9 = Math.Min(num9, d[num8 + 1] + 1f);
						}
						if (num5 < h - 1)
						{
							num9 = Math.Min(num9, d[num8 + w] + 1f);
							if (num7 < w - 1)
							{
								num9 = Math.Min(num9, d[num8 + w + 1] + 1.4142f);
							}
							if (num7 > 0)
							{
								num9 = Math.Min(num9, d[num8 + w - 1] + 1.4142f);
							}
						}
						d[num8] = num9;
					}
				}
			}
		}

		private void Ranges(int w, int h)
		{
			int num = w * h;
			int[] label = _label;
			Array.Clear(label, 0, num);
			Array.Clear(RangeSize, 0, num);
			Stack<int> stack = new Stack<int>();
			List<int> list = new List<int>();
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				if (label[i] != 0 || Form[i] != 3)
				{
					continue;
				}
				num2++;
				list.Clear();
				float num3 = 0f;
				label[i] = num2;
				stack.Push(i);
				while (stack.Count > 0)
				{
					int num4 = stack.Pop();
					int num5 = num4 % w;
					int num6 = num4 / w;
					list.Add(num4);
					if (MountainDepth[num4] > num3)
					{
						num3 = MountainDepth[num4];
					}
					for (int j = -1; j <= 1; j++)
					{
						int num7 = num6 + j;
						if (num7 < 0 || num7 >= h)
						{
							continue;
						}
						for (int k = -1; k <= 1; k++)
						{
							int num8 = num5 + k;
							if (num8 >= 0 && num8 < w)
							{
								int num9 = num7 * w + num8;
								if (label[num9] == 0 && Form[num9] == 3)
								{
									label[num9] = num2;
									stack.Push(num9);
								}
							}
						}
					}
				}
				foreach (int item in list)
				{
					RangeSize[item] = num3;
				}
			}
		}
	}
}
