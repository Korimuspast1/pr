using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class MapSprites
	{
		internal sealed class Info
		{
			public string Id;

			public int W;

			public int H;

			public RectInt Door;

			public Vector2Int Chimney = new Vector2Int(-1, -1);

			public bool NoChimney;
		}

		private static Dictionary<string, Info> _byId;

		public static Info Of(string id, int w, int h)
		{
			if (_byId == null)
			{
				Load();
			}
			if (id == null || !_byId.TryGetValue(id, out var value) || value.W != w || value.H != h)
			{
				return null;
			}
			return value;
		}

		private static void Load()
		{
			_byId = new Dictionary<string, Info>();
			string[] array = new string[2] { "game_sprites", "map_sprites" };
			for (int i = 0; i < array.Length; i++)
			{
				string text = Read(array[i]);
				if (text == null)
				{
					continue;
				}
				string[] array2 = text.Split('\n');
				for (int j = 0; j < array2.Length; j++)
				{
					string text2 = array2[j].Trim();
					if (text2.Length == 0 || text2[0] == '#')
					{
						continue;
					}
					try
					{
						string[] array3 = text2.Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
						Info info = new Info
						{
							Id = array3[0],
							W = int.Parse(array3[1]),
							H = int.Parse(array3[2])
						};
						string[] array4 = array3;
						foreach (string text3 in array4)
						{
							int num = text3.IndexOf('=');
							if (num <= 0)
							{
								continue;
							}
							string text4 = text3.Substring(0, num);
							string[] array5 = text3.Substring(num + 1).Split(',');
							if (text4 == "door" && array5.Length == 4)
							{
								int num2 = int.Parse(array5[0]);
								int num3 = int.Parse(array5[1]);
								int num4 = int.Parse(array5[2]);
								int num5 = int.Parse(array5[3]);
								if (num4 >= num2 && num5 >= num3)
								{
									info.Door = new RectInt(num2, info.H - 1 - num5, num4 - num2 + 1, num5 - num3 + 1);
								}
							}
							else
							{
								if (!(text4 == "chimney"))
								{
									continue;
								}
								if (array5.Length == 1 && array5[0] == "-")
								{
									info.NoChimney = true;
								}
								else if (array5.Length == 2)
								{
									int num6 = int.Parse(array5[0]);
									int num7 = int.Parse(array5[1]);
									if (num6 >= 0 && num7 >= -1)
									{
										info.Chimney = new Vector2Int(num6, info.H - 1 - num7);
									}
								}
							}
						}
						_byId[info.Id] = info;
					}
					catch
					{
					}
				}
			}
			Log.Info($"map houses: {_byId.Count} house sprites known (doors, chimneys)");
		}

		private static string Read(string name)
		{
			try
			{
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FirstPerson.map_sprites." + name + ".txt"))
				{
					if (stream != null)
					{
						using (StreamReader streamReader = new StreamReader(stream))
						{
							return streamReader.ReadToEnd();
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}
	}
}
