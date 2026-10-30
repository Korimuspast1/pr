using System;
using System.Collections.Generic;
using System.IO;

namespace FirstPerson.Core
{
	public sealed class Fsb5
	{
		public long Offset = -1L;

		public readonly Dictionary<string, int> Index = new Dictionary<string, int>(StringComparer.Ordinal);

		public int Count;

		private const int HeaderBytes = 60;

		public static Fsb5 Read(string bankPath, int searchBytes = 16777216)
		{
			Fsb5 fsb = new Fsb5();
			using (FileStream fileStream = new FileStream(bankPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				byte[] array = new byte[Math.Min(searchBytes, (int)Math.Min(2147483647L, fileStream.Length))];
				int num = ReadAll(fileStream, array, 0, array.Length);
				for (int i = 0; i + 4 <= num; i++)
				{
					if (array[i] == 70 && array[i + 1] == 83 && array[i + 2] == 66 && array[i + 3] == 53)
					{
						fsb.Offset = i;
						break;
					}
				}
				if (fsb.Offset < 0)
				{
					return fsb;
				}
				byte[] array2 = new byte[60];
				fileStream.Position = fsb.Offset;
				if (ReadAll(fileStream, array2, 0, 60) < 60)
				{
					fsb.Offset = -1L;
					return fsb;
				}
				int num2 = BitConverter.ToInt32(array2, 8);
				int num3 = BitConverter.ToInt32(array2, 12);
				int num4 = BitConverter.ToInt32(array2, 16);
				if (num2 <= 0 || num4 <= num2 * 4 || num4 > 67108864)
				{
					fsb.Offset = -1L;
					return fsb;
				}
				byte[] array3 = new byte[num4];
				fileStream.Position = fsb.Offset + 60 + num3;
				if (ReadAll(fileStream, array3, 0, num4) < num4)
				{
					fsb.Offset = -1L;
					return fsb;
				}
				fsb.Count = num2;
				char[] array4 = new char[256];
				for (int j = 0; j < num2; j++)
				{
					int num5 = BitConverter.ToInt32(array3, j * 4);
					if (num5 >= 0 && num5 < num4)
					{
						int k;
						for (k = 0; num5 + k < num4 && array3[num5 + k] != 0 && k < array4.Length; k++)
						{
							array4[k] = (char)array3[num5 + k];
						}
						string key = new string(array4, 0, k);
						if (!fsb.Index.ContainsKey(key))
						{
							fsb.Index[key] = j;
						}
					}
				}
				return fsb;
			}
		}

		private static int ReadAll(Stream s, byte[] buf, int at, int count)
		{
			int i;
			int num;
			for (i = 0; i < count; i += num)
			{
				num = s.Read(buf, at + i, count - i);
				if (num <= 0)
				{
					break;
				}
			}
			return i;
		}
	}
}
