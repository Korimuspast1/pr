using System;
using System.Text;

namespace FirstPerson.Net
{
	internal static class JoinCode
	{
		internal struct Parsed
		{
			public bool Valid;

			public bool SteamOnly;

			public string Host;

			public int Port;

			public string Code;
		}

		private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

		private const int KindIp = 0;

		private const int KindIpPort = 1;

		private const int KindSteam = 2;

		private const uint Mix = 2654435761u;

		private const uint Salt = 1511506921u;

		public static string ForAddress(string ipv4, int port)
		{
			if (!TryIp(ipv4, out var ip))
			{
				return "";
			}
			uint num = Shuffle(ip);
			if (port == 27777)
			{
				return Write(0, num, 32, 8);
			}
			return Write(1, ((ulong)num << 16) | (ushort)port, 48, 12);
		}

		public static string SteamOnly(Random rng)
		{
			return Write(2, (uint)(rng.Next() ^ (rng.Next() << 16)), 32, 8);
		}

		public static Parsed Read(string typed)
		{
			Parsed result = default(Parsed);
			if (string.IsNullOrEmpty(typed))
			{
				return result;
			}
			StringBuilder stringBuilder = new StringBuilder();
			string text = typed.Trim().ToUpperInvariant();
			foreach (char c in text)
			{
				int num;
				switch (c)
				{
				default:
					num = c;
					break;
				case 'O':
					num = 48;
					break;
				case 'I':
				case 'L':
					num = 49;
					break;
				}
				char c2 = (char)num;
				if (c2 != '-' && c2 != ' ')
				{
					if ("0123456789ABCDEFGHJKMNPQRSTVWXYZ".IndexOf(c2) < 0)
					{
						return result;
					}
					stringBuilder.Append(c2);
				}
			}
			string text2 = stringBuilder.ToString();
			if (text2.Length != 8 && text2.Length != 12)
			{
				return result;
			}
			ulong num2 = 0uL;
			text = text2;
			foreach (char value in text)
			{
				num2 = (num2 << 5) | (uint)"0123456789ABCDEFGHJKMNPQRSTVWXYZ".IndexOf(value);
			}
			int num3 = text2.Length * 5;
			int num4 = (int)(num2 >> num3 - 3);
			int num5 = ((num4 == 1) ? 48 : 32);
			int num6 = num3 - 3 - num5 - 5;
			if (num4 == 1 != (text2.Length == 12) || num6 < 0)
			{
				return result;
			}
			if ((num2 & (ulong)((1L << num6) - 1)) != 0L)
			{
				return result;
			}
			ulong num7 = (num2 >> 5 + num6) & (ulong)((1L << num5) - 1);
			if ((int)((num2 >> num6) & 0x1F) != Check(num4, num7))
			{
				return result;
			}
			Parsed result2 = new Parsed
			{
				Valid = true,
				Code = Pretty(text2)
			};
			switch (num4)
			{
			case 2:
				result2.SteamOnly = true;
				return result2;
			default:
				return result;
			case 0:
			case 1:
			{
				uint num8 = Unshuffle((uint)((num4 == 0) ? num7 : (num7 >> 16)));
				result2.Host = (num8 >> 24) + "." + ((num8 >> 16) & 0xFF) + "." + ((num8 >> 8) & 0xFF) + "." + (num8 & 0xFF);
				result2.Port = (int)((num4 == 0) ? 27777 : (num7 & 0xFFFF));
				if (result2.Port < 1)
				{
					return result;
				}
				return result2;
			}
			}
		}

		public static bool Looks(string typed)
		{
			if (!string.IsNullOrEmpty(typed) && typed.IndexOf('.') < 0 && typed.IndexOf(':') < 0)
			{
				return Read(typed).Valid;
			}
			return false;
		}

		private static string Write(int kind, ulong payload, int payloadBits, int chars)
		{
			int num = chars * 5;
			int num2 = num - 3 - payloadBits - 5;
			ulong num3 = (ulong)((long)kind << num - 3) | (payload << 5 + num2) | (ulong)((long)Check(kind, payload) << num2);
			StringBuilder stringBuilder = new StringBuilder();
			for (int num4 = chars - 1; num4 >= 0; num4--)
			{
				stringBuilder.Append("0123456789ABCDEFGHJKMNPQRSTVWXYZ"[(int)((num3 >> num4 * 5) & 0x1F)]);
			}
			return Pretty(stringBuilder.ToString());
		}

		private static string Pretty(string s)
		{
			if (s.Length != 8)
			{
				return s.Substring(0, 4) + "-" + s.Substring(4, 4) + "-" + s.Substring(8);
			}
			return s.Substring(0, 4) + "-" + s.Substring(4);
		}

		private static int Check(int kind, ulong payload)
		{
			ulong num = (ulong)(0x14650FB0739D0383L ^ kind);
			for (int i = 0; i < 8; i++)
			{
				num ^= (payload >> i * 8) & 0xFF;
				num *= 1099511628211L;
			}
			return (int)((num ^ (num >> 29)) & 0x1F);
		}

		private static uint Shuffle(uint x)
		{
			x ^= 0x5A17C3E9;
			x *= 2654435761u;
			x ^= x >> 16;
			return x;
		}

		private static uint Unshuffle(uint x)
		{
			x ^= x >> 16;
			x *= Inverse(2654435761u);
			return x ^ 0x5A17C3E9;
		}

		private static uint Inverse(uint a)
		{
			uint num = a;
			for (int i = 0; i < 5; i++)
			{
				num *= 2 - a * num;
			}
			return num;
		}

		private static bool TryIp(string s, out uint ip)
		{
			ip = 0u;
			if (string.IsNullOrEmpty(s))
			{
				return false;
			}
			string[] array = s.Split('.');
			if (array.Length != 4)
			{
				return false;
			}
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (!byte.TryParse(array2[i], out var result))
				{
					return false;
				}
				ip = (ip << 8) | result;
			}
			return true;
		}

		public static bool Private(string ipv4)
		{
			if (!TryIp(ipv4, out var ip))
			{
				return true;
			}
			uint num = ip >> 24;
			uint num2 = (ip >> 16) & 0xFF;
			if (num != 10 && num != 127 && num != 0 && (num != 172 || num2 < 16 || num2 > 31) && (num != 192 || num2 != 168) && (num != 169 || num2 != 254) && (num != 100 || num2 < 64 || num2 > 127))
			{
				return num >= 224;
			}
			return true;
		}
	}
}
