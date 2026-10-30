using System;
using System.Collections.Generic;
using System.Diagnostics;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Minimap
	{
		private sealed class Job
		{
			public float X0;

			public float Y0;

			public float Size;

			public int Res;

			public int Row;

			public Color32[] Px;

			public double Ms;
		}

		public Texture2D Land;

		public Texture2D Fog;

		public float TexX;

		public float TexY;

		public float TexSize;

		private float _landAge;

		private int _fogVersion = -1;

		private Explored.Map _fogFor;

		private float _fogX = float.NaN;

		private float _fogY;

		public Texture2D WorldLand;

		public Texture2D WorldFog;

		public int WorldW;

		public int WorldH;

		private float _worldAge;

		private int _worldFogVersion = -1;

		private Explored.Map _worldFogFor;

		public double LandMs;

		public double FogMs;

		public double WorldMs;

		public double WorstFrameMs;

		public int LandBuilds;

		public int FogBuilds;

		public int WorldBuilds;

		public static double BudgetMs = 0.35;

		public static readonly Color FogColor = new Color(0.07f, 0.075f, 0.1f);

		private Job _landJob;

		private Job _worldJob;

		private const int OutR = 13;

		private const int OutG = 15;

		private const int OutB = 23;

		private static readonly Dictionary<int, Color32[]> _fogPx = new Dictionary<int, Color32[]>();

		public void Update(TerrainCache t, Explored.Map map, Vector2 at, float reach, float dt)
		{
			if (t == null || t.Width <= 0 || t.BaseColors.Length < t.Width * t.Height)
			{
				return;
			}
			_landAge += dt;
			float num = Mathf.Ceil(reach * 2.9f / 32f) * 32f;
			float num2 = TexX + TexSize * 0.5f;
			float num3 = TexY + TexSize * 0.5f;
			float num4 = TexSize * 0.5f - reach - 1f;
			float num5 = Mathf.Max(Mathf.Abs(at.x - num2), Mathf.Abs(at.y - num3));
			bool flag = Land == null || Mathf.Abs(num - TexSize) > 0.5f || num5 > num4;
			bool flag2 = num5 > num4 * 0.55f || _landAge > 8f;
			if (_landJob == null && (flag || flag2))
			{
				float x = Mathf.Floor((at.x - num * 0.5f) / 4f) * 4f;
				float y = Mathf.Floor((at.y - num * 0.5f) / 4f) * 4f;
				_landJob = new Job
				{
					X0 = x,
					Y0 = y,
					Size = num,
					Res = Mathf.RoundToInt(num)
				};
				_landAge = 0f;
			}
			if (_landJob != null)
			{
				Job landJob = _landJob;
				if (landJob.Size != num)
				{
					landJob.X0 = Mathf.Floor((at.x - num * 0.5f) / 4f) * 4f;
					landJob.Y0 = Mathf.Floor((at.y - num * 0.5f) / 4f) * 4f;
					landJob.Size = num;
					landJob.Res = Mathf.RoundToInt(num);
					landJob.Row = 0;
					landJob.Px = null;
				}
				if (Step(landJob, t, flag ? double.MaxValue : BudgetMs))
				{
					Land = Upload(Land, landJob, "FirstPersonMinimapLand");
					TexX = landJob.X0;
					TexY = landJob.Y0;
					TexSize = landJob.Size;
					LandMs = landJob.Ms;
					LandBuilds++;
					_landJob = null;
				}
			}
			if (Land != null && map != null && (map != _fogFor || map.Version != _fogVersion || _fogX != TexX || _fogY != TexY))
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				Fog = PaintFog(Fog, map, TexX, TexY, Mathf.RoundToInt(TexSize / 4f), "FirstPersonMinimapFog");
				FogMs = stopwatch.Elapsed.TotalMilliseconds;
				FogBuilds++;
				_fogFor = map;
				_fogVersion = map.Version;
				_fogX = TexX;
				_fogY = TexY;
			}
		}

		public void UpdateWorld(TerrainCache t, Explored.Map map, float dt)
		{
			if (t != null && t.Width > 0 && t.BaseColors.Length >= t.Width * t.Height)
			{
				_worldAge += dt;
				int width = t.Width;
				int height = t.Height;
				bool flag = WorldLand == null || WorldW != width || WorldH != height;
				if (_worldJob == null && (flag || _worldAge > 10f))
				{
					int num = Mathf.Max(width, height);
					_worldJob = new Job
					{
						X0 = 0f,
						Y0 = 0f,
						Size = num,
						Res = Mathf.Min(512, num)
					};
					_worldAge = 0f;
				}
				if (_worldJob != null && Step(_worldJob, t, flag ? double.MaxValue : BudgetMs))
				{
					WorldLand = Upload(WorldLand, _worldJob, "FirstPersonWorldMapLand");
					WorldW = width;
					WorldH = height;
					WorldMs = _worldJob.Ms;
					WorldBuilds++;
					_worldJob = null;
				}
				if (WorldLand != null && map != null && (map != _worldFogFor || map.Version != _worldFogVersion))
				{
					int num2 = Mathf.Max(width, height);
					WorldFog = PaintFog(WorldFog, map, 0f, 0f, Mathf.CeilToInt((float)num2 / 4f), "FirstPersonWorldMapFog");
					_worldFogFor = map;
					_worldFogVersion = map.Version;
				}
			}
		}

		public void Reset()
		{
			_landAge = (_worldAge = 0f);
			_fogVersion = (_worldFogVersion = -1);
			_fogFor = (_worldFogFor = null);
			_fogX = float.NaN;
			_landJob = (_worldJob = null);
			Land = Free(Land);
			Fog = Free(Fog);
			WorldLand = Free(WorldLand);
			WorldFog = Free(WorldFog);
		}

		private static Texture2D Free(Texture2D t)
		{
			if (t != null)
			{
				UnityEngine.Object.Destroy(t);
			}
			return null;
		}

		private static Texture2D Ensure(Texture2D tex, int res, FilterMode filter, string name)
		{
			if (tex != null && tex.width == res)
			{
				return tex;
			}
			if (tex != null)
			{
				UnityEngine.Object.Destroy(tex);
			}
			return new Texture2D(res, res, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = filter,
				wrapMode = TextureWrapMode.Clamp,
				name = name,
				hideFlags = HideFlags.HideAndDontSave
			};
		}

		public Texture2D Area(TerrainCache t, float x0, float y0, float size, int res, Texture2D tex, string name)
		{
			if (t == null || t.Width <= 0 || t.BaseColors.Length < t.Width * t.Height)
			{
				return tex;
			}
			Job j = new Job
			{
				X0 = x0,
				Y0 = y0,
				Size = size,
				Res = res
			};
			Step(j, t, double.MaxValue);
			return Upload(tex, j, name);
		}

		private static Texture2D Upload(Texture2D tex, Job j, string name)
		{
			tex = Ensure(tex, j.Res, FilterMode.Point, name);
			tex.SetPixels32(j.Px);
			tex.Apply(updateMipmaps: false);
			return tex;
		}

		private bool Step(Job j, TerrainCache t, double budgetMs)
		{
			long timestamp = Stopwatch.GetTimestamp();
			long num = ((budgetMs >= double.MaxValue) ? long.MaxValue : ((long)(budgetMs * (double)Stopwatch.Frequency / 1000.0)));
			j.Res = Mathf.Clamp(j.Res, 16, 1024);
			if (j.Px == null || j.Px.Length != j.Res * j.Res)
			{
				j.Px = new Color32[j.Res * j.Res];
				j.Row = 0;
			}
			bool result = false;
			while (j.Row < j.Res)
			{
				PaintRow(j, t, j.Row++);
				if (Stopwatch.GetTimestamp() - timestamp > num)
				{
					break;
				}
			}
			if (j.Row >= j.Res)
			{
				PaintBuildings(j.Px, j.Res, j.X0, j.Y0, j.Size / (float)j.Res);
				result = true;
			}
			double num2 = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			j.Ms += num2;
			WorstFrameMs = Math.Max(WorstFrameMs, num2);
			return result;
		}

		private static void PaintRow(Job j, TerrainCache t, int y)
		{
			int res = j.Res;
			int width = t.Width;
			int height = t.Height;
			float num = j.Size / (float)res;
			int num2 = Math.Max(1, (int)(num + 0.5f));
			float num3 = 0.16f / (float)num2;
			float[] heights = t.Heights;
			uint[] baseColors = t.BaseColors;
			byte[] flags = t.Flags;
			byte[] materials = t.Materials;
			int[] pathSlot = t.PathSlot;
			Color32[] px = j.Px;
			int num4 = (int)Math.Floor(j.Y0 + ((float)y + 0.5f) * num);
			int num5 = y * res;
			if (num4 < 0 || num4 >= height)
			{
				for (int i = 0; i < res; i++)
				{
					px[num5 + i] = new Color32(13, 15, 23, byte.MaxValue);
				}
				return;
			}
			int num6 = Math.Min(height - 1, num4 + num2) * width;
			int num7 = Math.Max(0, num4 - num2) * width;
			int num8 = num4 * width;
			for (int k = 0; k < res; k++)
			{
				int num9 = (int)Math.Floor(j.X0 + ((float)k + 0.5f) * num);
				if (num9 < 0 || num9 >= width)
				{
					px[num5 + k] = new Color32(13, 15, 23, byte.MaxValue);
					continue;
				}
				int num10 = num8 + num9;
				uint num11 = baseColors[num10];
				float num12 = num11 & 0xFF;
				float num13 = (num11 >> 8) & 0xFF;
				float num14 = (num11 >> 16) & 0xFF;
				byte b = flags[num10];
				if ((b & 2) != 0)
				{
					num12 = 242f;
					num13 = 102f;
					num14 = 26f;
				}
				else if ((b & 1) != 0)
				{
					num12 = (num12 * 0.45f + 22.550001f) * 0.92f;
					num13 = (num13 * 0.45f + 48.95f) * 0.92f;
					num14 = (num14 * 0.45f + 86.9f) * 0.92f;
				}
				else
				{
					int num15 = ((num9 - num2 >= 0) ? (num9 - num2) : 0);
					int num16 = ((num9 + num2 >= width) ? (width - 1) : (num9 + num2));
					float num17 = heights[num8 + num16] - heights[num8 + num15];
					float num18 = heights[num6 + num9] - heights[num7 + num9];
					float num19 = 1f + (num18 - num17) * num3;
					if (num19 < 0.62f)
					{
						num19 = 0.62f;
					}
					else if (num19 > 1.3f)
					{
						num19 = 1.3f;
					}
					num12 *= num19;
					num13 *= num19;
					num14 *= num19;
					if (pathSlot.Length > num10 && pathSlot[num10] != 0)
					{
						num12 = num12 * 0.65f + 55.3f;
						num13 = num13 * 0.65f + 44.8f;
						num14 = num14 * 0.65f + 32.2f;
					}
					if (materials[num10] == 6)
					{
						num12 = num12 * 0.5f + 57.5f;
						num13 = num13 * 0.5f + 48.5f;
						num14 = num14 * 0.5f + 38.5f;
					}
				}
				px[num5 + k] = new Color32((byte)((num12 > 255f) ? 255f : num12), (byte)((num13 > 255f) ? 255f : num13), (byte)((num14 > 255f) ? 255f : num14), byte.MaxValue);
			}
		}

		private static void PaintBuildings(Color32[] px, int res, float x0, float y0, float step)
		{
			List<Building> simpleList;
			try
			{
				simpleList = World.world.buildings.getSimpleList();
			}
			catch
			{
				return;
			}
			float num = x0 + (float)res * step;
			float num2 = y0 + (float)res * step;
			Color32 color = new Color32(34, 78, 30, byte.MaxValue);
			Color32 color2 = new Color32(118, 120, 118, byte.MaxValue);
			Color32 color3 = new Color32(110, 76, 48, byte.MaxValue);
			for (int i = 0; i < simpleList.Count; i++)
			{
				Building building = simpleList[i];
				try
				{
					if (building == null || building.asset == null || building.current_tile == null || !building.isAlive())
					{
						continue;
					}
					WorldTile current_tile = building.current_tile;
					if ((float)current_tile.x < x0 - 4f || (float)current_tile.y < y0 - 4f || (float)current_tile.x > num + 4f || (float)current_tile.y > num2 + 4f)
					{
						continue;
					}
					Color32 b;
					float t;
					switch (building.asset.building_type)
					{
					case BuildingType.Building_Poops:
					case BuildingType.Building_Wheat:
					case BuildingType.Building_Plant:
					case BuildingType.Building_Creep:
						goto end_IL_0070;
					case BuildingType.Building_Tree:
					case BuildingType.Building_Fruits:
						b = color;
						t = 0.55f;
						break;
					case BuildingType.Building_Mineral:
						b = color2;
						t = 0.6f;
						break;
					default:
					{
						Kingdom kingdom = building.kingdom;
						b = ((kingdom != null && kingdom.isCiv()) ? ((Color32)(Royal.ColorOf(kingdom) * 0.85f)) : color3);
						t = 0.9f;
						break;
					}
					}
					BuildingFundament fundament = building.asset.fundament;
					int num3 = fundament?.left ?? 0;
					int num4 = fundament?.right ?? 0;
					int num5 = fundament?.top ?? 0;
					int num6 = fundament?.bottom ?? 0;
					for (int j = current_tile.y - num6; j <= current_tile.y + num5; j++)
					{
						for (int k = current_tile.x - num3; k <= current_tile.x + num4; k++)
						{
							int num7 = (int)Math.Floor(((float)k + 0.5f - x0) / step);
							int num8 = (int)Math.Floor(((float)j + 0.5f - y0) / step);
							if (num7 >= 0 && num8 >= 0 && num7 < res && num8 < res)
							{
								int num9 = num8 * res + num7;
								px[num9] = Color32.Lerp(px[num9], b, t);
							}
						}
					}
					end_IL_0070:;
				}
				catch
				{
				}
			}
		}

		private static Texture2D PaintFog(Texture2D tex, Explored.Map map, float x0, float y0, int cells, string name)
		{
			cells = Mathf.Clamp(cells, 4, 1024);
			tex = Ensure(tex, cells, FilterMode.Bilinear, name);
			if (!_fogPx.TryGetValue(cells, out var value))
			{
				if (_fogPx.Count > 4)
				{
					_fogPx.Clear();
				}
				value = (_fogPx[cells] = new Color32[cells * cells]);
			}
			int num = Mathf.FloorToInt(x0 / 4f);
			int num2 = Mathf.FloorToInt(y0 / 4f);
			Color32 color = FogColor;
			Color32 color2 = new Color32(color.r, color.g, color.b, 0);
			for (int i = 0; i < cells; i++)
			{
				for (int j = 0; j < cells; j++)
				{
					value[i * cells + j] = (Explored.Seen(map, num + j, num2 + i) ? color2 : color);
				}
			}
			tex.SetPixels32(value);
			tex.Apply(updateMipmaps: false);
			return tex;
		}
	}
}
