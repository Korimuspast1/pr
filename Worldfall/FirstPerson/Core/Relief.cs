using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FirstPerson.Core
{
	public sealed class Relief
	{
		public sealed class Snapshot
		{
			public short[] Edit;

			public short[] LakeLevel;

			public byte[] Raw;

			public ushort[] LakeMain;

			public ushort[] LakeTop;

			public int Width;

			public int Height;
		}

		private delegate void TileReader(int i, byte[] data);

		public const float Unit = 1f / 64f;

		public const int MostSteps = 32767;

		public int Width;

		public int Height;

		public bool Terrainy;

		public int Seed;

		public short[] Edit;

		public short[] Gen;

		public byte[] Raw;

		public readonly List<int> RawTiles = new List<int>();

		public short[] LakeLevel;

		public ushort[] LakeMain;

		public ushort[] LakeTop;

		public int Version;

		public int EditVersion;

		private readonly List<int> _changed = new List<int>();

		private readonly object _gate = new object();

		private bool[] _pending;

		public const string Tag = "R2";

		public const string OldTag = "R1";

		public float Terraces
		{
			get
			{
				if (!Terrainy)
				{
					return 0f;
				}
				return 1f;
			}
		}

		public float RollLess
		{
			get
			{
				if (!Terrainy)
				{
					return 0f;
				}
				return 0.5f;
			}
		}

		public bool Any
		{
			get
			{
				if (Edit == null && Gen == null && Raw == null)
				{
					return LakeMain != null;
				}
				return true;
			}
		}

		public void Allocate(int w, int h)
		{
			lock (_gate)
			{
				Width = w;
				Height = h;
				Terrainy = false;
				Edit = null;
				Gen = null;
				Raw = null;
				LakeLevel = null;
				LakeMain = (LakeTop = null);
				RawTiles.Clear();
				_changed.Clear();
				_pending = null;
				Version++;
				EditVersion++;
			}
		}

		public float Offset(int i)
		{
			int num = 0;
			short[] edit = Edit;
			short[] gen = Gen;
			if (edit != null && i < edit.Length)
			{
				num += edit[i];
			}
			if (gen != null && i < gen.Length)
			{
				num += gen[i];
			}
			return (float)num * (1f / 64f);
		}

		public float EditOf(int i)
		{
			short[] edit = Edit;
			if (edit == null || i < 0 || i >= edit.Length)
			{
				return 0f;
			}
			return (float)edit[i] * (1f / 64f);
		}

		public float GenOf(int i)
		{
			short[] gen = Gen;
			if (gen == null || i < 0 || i >= gen.Length)
			{
				return 0f;
			}
			return (float)gen[i] * (1f / 64f);
		}

		public bool Shaped(int i)
		{
			short[] edit = Edit;
			if (edit != null && i >= 0 && i < edit.Length)
			{
				return edit[i] != 0;
			}
			return false;
		}

		public float RawOf(int i)
		{
			byte[] raw = Raw;
			if (raw == null || i < 0 || i >= raw.Length)
			{
				return 0f;
			}
			return (float)(int)raw[i] / 255f;
		}

		public bool Lake(int i, out float level, out int main, out int top)
		{
			ushort[] lakeMain = LakeMain;
			short[] lakeLevel = LakeLevel;
			ushort[] lakeTop = LakeTop;
			if (lakeMain == null || lakeLevel == null || i < 0 || i >= lakeMain.Length || lakeMain[i] == 0)
			{
				level = 0f;
				main = (top = 0);
				return false;
			}
			level = (float)lakeLevel[i] * (1f / 64f);
			main = lakeMain[i];
			top = ((lakeTop != null) ? lakeTop[i] : 0);
			return true;
		}

		public bool IsLake(int i)
		{
			ushort[] lakeMain = LakeMain;
			if (lakeMain != null && i >= 0 && i < lakeMain.Length)
			{
				return lakeMain[i] != 0;
			}
			return false;
		}

		public int Stamp(int i)
		{
			int num = 0;
			short[] edit = Edit;
			short[] gen = Gen;
			short[] lakeLevel = LakeLevel;
			byte[] raw = Raw;
			ushort[] lakeMain = LakeMain;
			if (edit != null && i < edit.Length)
			{
				num = edit[i];
			}
			if (gen != null && i < gen.Length)
			{
				num = num * 31 + gen[i];
			}
			if (raw != null && i < raw.Length)
			{
				num = num * 31 + (raw[i] >> 3);
			}
			if (lakeMain != null && i < lakeMain.Length && lakeMain[i] != 0)
			{
				num = (num * 31 + lakeMain[i]) * 31 + ((lakeLevel != null) ? lakeLevel[i] : 0);
			}
			return num;
		}

		public static short ToSteps(float tiles)
		{
			float num = tiles / (1f / 64f);
			num = ((num < -32767f) ? (-32767f) : ((num > 32767f) ? 32767f : num));
			return (short)Math.Round(num);
		}

		public bool SetEdit(int i, float tiles, bool basins = true)
		{
			if (i < 0 || i >= Width * Height)
			{
				return false;
			}
			short num = ToSteps(tiles);
			lock (_gate)
			{
				if (Edit == null)
				{
					if (num == 0)
					{
						return false;
					}
					Edit = new short[Width * Height];
				}
				if (Edit[i] == num)
				{
					return false;
				}
				Edit[i] = num;
				Version++;
				if (basins)
				{
					EditVersion++;
				}
				Mark(i);
			}
			return true;
		}

		public void ClearEdits()
		{
			lock (_gate)
			{
				int num = Width * Height;
				for (int i = 0; i < num; i++)
				{
					if ((Edit != null && Edit[i] != 0) || (Raw != null && Raw[i] != 0) || (LakeMain != null && LakeMain[i] != 0))
					{
						Mark(i);
					}
				}
				Edit = null;
				Raw = null;
				LakeLevel = null;
				LakeMain = (LakeTop = null);
				RawTiles.Clear();
				Version++;
				EditVersion++;
			}
		}

		public void SetRaw(int i, float raw = 1f)
		{
			if (i < 0 || i >= Width * Height)
			{
				return;
			}
			byte b = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(raw * 255f)));
			lock (_gate)
			{
				if (Raw == null)
				{
					if (b == 0)
					{
						return;
					}
					Raw = new byte[Width * Height];
				}
				if (Raw[i] != b)
				{
					if (Raw[i] == 0 && b > 0)
					{
						RawTiles.Add(i);
					}
					Raw[i] = b;
					Version++;
					Mark(i);
				}
			}
		}

		public void SetLake(int i, float level, int main, int top)
		{
			if (i < 0 || i >= Width * Height)
			{
				return;
			}
			short num = (short)((main != 0) ? ToSteps(level) : 0);
			lock (_gate)
			{
				if (LakeMain == null)
				{
					if (main == 0)
					{
						return;
					}
					int num2 = Width * Height;
					LakeLevel = new short[num2];
					LakeMain = new ushort[num2];
					LakeTop = new ushort[num2];
				}
				if (LakeMain[i] != main || LakeTop[i] != top || LakeLevel[i] != num)
				{
					LakeMain[i] = (ushort)main;
					LakeTop[i] = (ushort)top;
					LakeLevel[i] = num;
					Version++;
					Mark(i);
				}
			}
		}

		public void SetGen(short[] gen)
		{
			lock (_gate)
			{
				short[] gen2 = Gen;
				int num = Width * Height;
				if ((gen != null && gen.Length != num) || (gen2 == null && gen == null))
				{
					return;
				}
				for (int i = 0; i < num; i++)
				{
					int num2 = ((gen2 != null) ? gen2[i] : 0);
					short num3 = (short)((gen != null) ? gen[i] : 0);
					if (num2 != num3)
					{
						Mark(i);
					}
				}
				Gen = gen;
				Version++;
			}
		}

		public int EaseToward(short[] target, int steps, bool[] pinned, out int left)
		{
			left = 0;
			int num = 0;
			lock (_gate)
			{
				short[] gen = Gen;
				if (gen == null || target == null || gen.Length != target.Length)
				{
					return 0;
				}
				for (int i = 0; i < gen.Length; i++)
				{
					int num2 = target[i] - gen[i];
					if (num2 == 0)
					{
						continue;
					}
					left++;
					if (pinned == null || i >= pinned.Length || !pinned[i])
					{
						int num3 = ((num2 > steps) ? steps : ((num2 < -steps) ? (-steps) : num2));
						gen[i] = (short)(gen[i] + num3);
						if (num3 == num2)
						{
							left--;
						}
						num++;
						Mark(i);
					}
				}
				if (num > 0)
				{
					Version++;
				}
			}
			return num;
		}

		public void GenChanged(List<int> tiles)
		{
			if (tiles == null || tiles.Count == 0)
			{
				return;
			}
			lock (_gate)
			{
				foreach (int tile in tiles)
				{
					Mark(tile);
				}
				Version++;
			}
		}

		public int TakeChanged(List<int> into)
		{
			lock (_gate)
			{
				int count = _changed.Count;
				if (count > 0)
				{
					into.AddRange(_changed);
					if (_pending != null)
					{
						foreach (int item in _changed)
						{
							if (item >= 0 && item < _pending.Length)
							{
								_pending[item] = false;
							}
						}
					}
				}
				_changed.Clear();
				return count;
			}
		}

		private void Mark(int i)
		{
			int num = Width * Height;
			if (i >= 0 && i < num)
			{
				if (_pending == null || _pending.Length != num)
				{
					_pending = new bool[num];
				}
				if (!_pending[i])
				{
					_pending[i] = true;
					_changed.Add(i);
				}
			}
		}

		public void Green(float amount)
		{
			if (Raw == null || RawTiles.Count == 0 || amount <= 0f)
			{
				return;
			}
			int num = Math.Max(1, (int)Math.Round(amount * 255f));
			lock (_gate)
			{
				for (int num2 = RawTiles.Count - 1; num2 >= 0; num2--)
				{
					int num3 = RawTiles[num2];
					int num4 = Raw[num3] - num;
					if (num4 <= 0)
					{
						num4 = 0;
						RawTiles[num2] = RawTiles[RawTiles.Count - 1];
						RawTiles.RemoveAt(RawTiles.Count - 1);
					}
					if (num4 >> 3 != Raw[num3] >> 3)
					{
						Mark(num3);
					}
					Raw[num3] = (byte)num4;
				}
				Version++;
			}
		}

		public int EditedCount()
		{
			short[] edit = Edit;
			if (edit == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < edit.Length; i++)
			{
				if (edit[i] != 0)
				{
					num++;
				}
			}
			return num;
		}

		public int LakeTileCount()
		{
			ushort[] lakeMain = LakeMain;
			if (lakeMain == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < lakeMain.Length; i++)
			{
				if (lakeMain[i] != 0)
				{
					num++;
				}
			}
			return num;
		}

		public string Save(Func<int, string> typeName = null)
		{
			return Encode(TakeSnapshot(), typeName);
		}

		public Snapshot TakeSnapshot(Snapshot into = null)
		{
			Snapshot snapshot = into ?? new Snapshot();
			lock (_gate)
			{
				snapshot.Edit = Copy(Edit, snapshot.Edit);
				snapshot.Raw = Copy(Raw, snapshot.Raw);
				snapshot.LakeLevel = Copy(LakeLevel, snapshot.LakeLevel);
				snapshot.LakeMain = Copy(LakeMain, snapshot.LakeMain);
				snapshot.LakeTop = Copy(LakeTop, snapshot.LakeTop);
				snapshot.Width = Width;
				snapshot.Height = Height;
				return snapshot;
			}
		}

		private static T[] Copy<T>(T[] from, T[] to)
		{
			if (from == null)
			{
				return null;
			}
			if (to == null || to.Length != from.Length)
			{
				to = new T[from.Length];
			}
			Array.Copy(from, to, from.Length);
			return to;
		}

		public static string Encode(Snapshot snap, Func<int, string> typeName = null)
		{
			short[] e = snap.Edit;
			short[] lakeLevel = snap.LakeLevel;
			byte[] r = snap.Raw;
			ushort[] lm = snap.LakeMain;
			ushort[] lt = snap.LakeTop;
			int width = snap.Width;
			int height = snap.Height;
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			if (e != null)
			{
				short[] array = e;
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i] != 0)
					{
						flag = true;
						break;
					}
				}
			}
			if (r != null)
			{
				byte[] array2 = r;
				for (int i = 0; i < array2.Length; i++)
				{
					if (array2[i] != 0)
					{
						flag2 = true;
						break;
					}
				}
			}
			if (lm != null && typeName != null)
			{
				ushort[] array3 = lm;
				for (int i = 0; i < array3.Length; i++)
				{
					if (array3[i] != 0)
					{
						flag3 = true;
						break;
					}
				}
			}
			if (!flag && !flag2 && !flag3)
			{
				return null;
			}
			MemoryStream memoryStream = new MemoryStream();
			int n = width * height;
			memoryStream.WriteByte((byte)((flag ? 1u : 0u) | (uint)(flag2 ? 2 : 0) | (uint)(flag3 ? 4 : 0)));
			if (flag)
			{
				Runs(memoryStream, n, (int num) => e[num] != 0, ZigDeltas(e));
			}
			if (flag2)
			{
				Runs(memoryStream, n, (int num) => r[num] != 0, delegate(Stream s, int num)
				{
					s.WriteByte(r[num]);
				});
			}
			List<string> names;
			Dictionary<int, int> index;
			if (flag3)
			{
				names = new List<string>();
				index = new Dictionary<int, int>();
				Action<Stream, int> levels = ZigDeltas(lakeLevel);
				Runs(memoryStream, n, (int num) => lm[num] != 0, delegate(Stream s, int num)
				{
					levels(s, num);
					WriteVar(s, (uint)Id(lm[num]));
					WriteVar(s, (uint)Id((lt != null) ? lt[num] : 0));
				});
				WriteVar(memoryStream, (uint)names.Count);
				foreach (string item in names)
				{
					byte[] bytes = Encoding.UTF8.GetBytes(item);
					WriteVar(memoryStream, (uint)bytes.Length);
					memoryStream.Write(bytes, 0, bytes.Length);
				}
			}
			return "R2:" + width + "x" + height + ":" + Convert.ToBase64String(memoryStream.ToArray());
			int Id(int t)
			{
				if (t == 0)
				{
					return 0;
				}
				if (!index.TryGetValue(t, out var value))
				{
					value = names.Count + 1;
					index[t] = value;
					names.Add(typeName(t) ?? "");
				}
				return value;
			}
		}

		public bool Load(string text, Func<string, int> typeId = null)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			bool flag = text.StartsWith("R1:", StringComparison.Ordinal);
			if (!flag && !text.StartsWith("R2:", StringComparison.Ordinal))
			{
				return false;
			}
			try
			{
				string[] array = text.Split(':');
				if (array.Length != 3)
				{
					return false;
				}
				string[] array2 = array[1].Split('x');
				if (array2.Length != 2 || int.Parse(array2[0]) != Width || int.Parse(array2[1]) != Height)
				{
					return false;
				}
				byte[] array3 = Convert.FromBase64String(array[2]);
				int num = Width * Height;
				int p = 0;
				if (array3.Length < 1)
				{
					return false;
				}
				int num2 = array3[p++];
				short[] e = null;
				short[] l = null;
				byte[] r = null;
				ushort[] array4 = null;
				ushort[] array5 = null;
				if ((num2 & 1) != 0)
				{
					e = new short[num];
					int prev = 0;
					if (!ReadRuns(array3, ref p, num, delegate(int i, byte[] d)
					{
						prev += Unzig(ReadVar(d, ref p));
						e[i] = Clamp(prev);
					}))
					{
						return false;
					}
				}
				if ((num2 & 2) != 0)
				{
					r = new byte[num];
					if (!ReadRuns(array3, ref p, num, delegate(int i, byte[] d)
					{
						if (p >= d.Length)
						{
							throw new InvalidDataException();
						}
						r[i] = d[p++];
					}))
					{
						return false;
					}
				}
				if (!flag && (num2 & 4) != 0)
				{
					l = new short[num];
					int[] mi = new int[num];
					int[] ti = new int[num];
					int prev2 = 0;
					if (!ReadRuns(array3, ref p, num, delegate(int i, byte[] d)
					{
						prev2 += Unzig(ReadVar(d, ref p));
						l[i] = Clamp(prev2);
						mi[i] = (int)ReadVar(d, ref p);
						ti[i] = (int)ReadVar(d, ref p);
					}))
					{
						return false;
					}
					int num3 = (int)ReadVar(array3, ref p);
					int[] array6 = new int[num3 + 1];
					for (int num4 = 1; num4 <= num3; num4++)
					{
						int num5 = (int)ReadVar(array3, ref p);
						if (num5 < 0 || p + num5 > array3.Length)
						{
							return false;
						}
						string arg = Encoding.UTF8.GetString(array3, p, num5);
						p += num5;
						array6[num4] = typeId?.Invoke(arg) ?? 0;
					}
					array4 = new ushort[num];
					array5 = new ushort[num];
					for (int num6 = 0; num6 < num; num6++)
					{
						if (mi[num6] <= 0 || mi[num6] > num3)
						{
							l[num6] = 0;
							continue;
						}
						array4[num6] = (ushort)array6[mi[num6]];
						array5[num6] = (ushort)((ti[num6] > 0 && ti[num6] <= num3) ? ((ushort)array6[ti[num6]]) : 0);
						if (array4[num6] == 0)
						{
							l[num6] = 0;
						}
					}
				}
				lock (_gate)
				{
					Edit = e;
					Raw = r;
					LakeLevel = l;
					LakeMain = array4;
					LakeTop = array5;
					RawTiles.Clear();
					if (r != null)
					{
						for (int num7 = 0; num7 < num; num7++)
						{
							if (r[num7] != 0)
							{
								RawTiles.Add(num7);
							}
						}
					}
					for (int num8 = 0; num8 < num; num8++)
					{
						if ((e != null && e[num8] != 0) || (r != null && r[num8] != 0) || (array4 != null && array4[num8] != 0))
						{
							Mark(num8);
						}
					}
					Version++;
					EditVersion++;
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static short Clamp(int v)
		{
			return (short)Math.Max(-32767, Math.Min(32767, v));
		}

		private static Action<Stream, int> ZigDeltas(short[] values)
		{
			int prev = 0;
			return delegate(Stream s, int i)
			{
				int num = values[i];
				WriteVar(s, Zig(num - prev));
				prev = num;
			};
		}

		private static void Runs(Stream s, int n, Func<int, bool> touched, Action<Stream, int> write)
		{
			int j;
			int k;
			for (int i = 0; i < n; i += j + k)
			{
				for (j = 0; i + j < n && !touched(i + j); j++)
				{
				}
				for (k = 0; i + j + k < n && touched(i + j + k); k++)
				{
				}
				WriteVar(s, (uint)j);
				WriteVar(s, (uint)k);
				for (int l = 0; l < k; l++)
				{
					write(s, i + j + l);
				}
			}
		}

		private static bool ReadRuns(byte[] data, ref int p, int n, TileReader read)
		{
			int num = 0;
			while (num < n)
			{
				int num2 = (int)ReadVar(data, ref p);
				int num3 = (int)ReadVar(data, ref p);
				if (num2 < 0 || num3 < 0 || num + num2 + num3 > n)
				{
					return false;
				}
				if (num2 == 0 && num3 == 0)
				{
					return false;
				}
				num += num2;
				for (int i = 0; i < num3; i++)
				{
					read(num++, data);
				}
			}
			return true;
		}

		private static uint Zig(int v)
		{
			return (uint)((v << 1) ^ (v >> 31));
		}

		private static int Unzig(uint v)
		{
			return (int)((v >> 1) ^ (0 - (v & 1)));
		}

		private static void WriteVar(Stream s, uint v)
		{
			while (v >= 128)
			{
				s.WriteByte((byte)(v | 0x80));
				v >>= 7;
			}
			s.WriteByte((byte)v);
		}

		private static uint ReadVar(byte[] d, ref int p)
		{
			uint num = 0u;
			int num2 = 0;
			while (true)
			{
				if (p >= d.Length || num2 > 28)
				{
					throw new InvalidDataException("relief record cut short");
				}
				byte b = d[p++];
				num |= (uint)((b & 0x7F) << num2);
				if ((b & 0x80) == 0)
				{
					break;
				}
				num2 += 7;
			}
			return num;
		}
	}
}
