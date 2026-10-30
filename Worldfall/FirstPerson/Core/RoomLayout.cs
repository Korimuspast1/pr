using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class RoomLayout
	{
		public enum Use
		{
			Bed,
			HearthSeat,
			TableSeat,
			Hearth,
			Table,
			Bench,
			Chest,
			Stair,
			Door,
			Free
		}

		public enum PieceKind
		{
			Bed,
			Hearth,
			Table,
			Seat,
			Storage,
			Bench,
			Rug,
			Stair,
			Other
		}

		public sealed class Place
		{
			public Use Use;

			public float X;

			public float Y;

			public float Z;

			public float Yaw;

			public float MidX;

			public float MidY;

			public int Storey;

			public bool Seat;

			public int Sleeps = 1;

			public float Width;

			public float SideX;

			public float SideY;

			public int Spot = -1;
		}

		public sealed class Piece
		{
			public string Name;

			public PieceKind Kind;

			public float[] Corners;

			public float X;

			public float Y;

			public float Yaw;

			public int Storey;
		}

		public readonly List<Place> Places = new List<Place>();

		public readonly List<Piece> Pieces = new List<Piece>();

		public readonly List<List<float[]>> Walls = new List<List<float[]>>();

		public int Storeys = 1;

		public float MinX;

		public float MinY;

		public float MaxX;

		public float MaxY;

		public float DoorX;

		public float DoorY;

		public float DoorOutX;

		public float DoorOutY;

		public bool Fitted;

		public float OffsetX;

		public float OffsetY;

		public IEnumerable<Place> Of(Use use)
		{
			foreach (Place place in Places)
			{
				if (place.Use == use)
				{
					yield return place;
				}
			}
		}

		public void ToHouse(float x, float y, float footW, float footD, out float hx, out float hy)
		{
			if (Fitted)
			{
				hx = x - OffsetX;
				hy = y - OffsetY;
				return;
			}
			float num = ((MaxX > MinX) ? ((x - MinX) / (MaxX - MinX) - 0.5f) : 0f);
			float num2 = ((MaxY > MinY) ? ((y - MinY) / (MaxY - MinY) - 0.5f) : 0f);
			hx = num * footW * 0.8f;
			hy = num2 * footD * 0.8f;
		}

		public static RoomLayout Of(Interior room)
		{
			RoomLayout roomLayout = new RoomLayout
			{
				Fitted = (room.Fit != null),
				MinX = room.MinX,
				MinY = room.MinY,
				MaxX = room.MaxX,
				MaxY = room.MaxY,
				DoorX = room.DoorX,
				DoorY = room.DoorY,
				DoorOutX = room.DoorOutX,
				DoorOutY = room.DoorOutY
			};
			FitInfo fit = room.Fit;
			if (fit != null)
			{
				roomLayout.Storeys = fit.StoreyZ.Length;
				roomLayout.OffsetX = fit.OffsetX;
				roomLayout.OffsetY = fit.OffsetY;
				for (int i = 0; i < roomLayout.Storeys; i++)
				{
					List<float[]> segs = new List<float[]>();
					fit.ForEachWall(i, delegate(float num15, float num16, float num17, float num18)
					{
						segs.Add(new float[4] { num15, num16, num17, num18 });
					});
					roomLayout.Walls.Add(segs);
				}
			}
			else
			{
				roomLayout.Walls.Add(new List<float[]>
				{
					new float[4] { room.MinX, room.MinY, room.MaxX, room.MinY },
					new float[4] { room.MaxX, room.MinY, room.MaxX, room.MaxY },
					new float[4] { room.MaxX, room.MaxY, room.MinX, room.MaxY },
					new float[4] { room.MinX, room.MaxY, room.MinX, room.MinY }
				});
			}
			List<Piece> list = new List<Piece>();
			List<Piece> list2 = new List<Piece>();
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (mesh == room.Shell || mesh.Model == null)
				{
					continue;
				}
				MeshModel model = mesh.Model;
				float num = (float)Math.Sqrt(Vec3.Dot(mesh.Basis.Mul(new Vec3(1f, 0f, 0f)), mesh.Basis.Mul(new Vec3(1f, 0f, 0f))));
				float num2 = mesh.Position.Z + model.Min.Z * num;
				int num3 = StoreyOfZ(mesh.Position.Z + 0.05f);
				float num4 = ((fit != null && num3 < fit.StoreyZ.Length) ? fit.StoreyZ[num3] : 0f);
				if (!(num2 > num4 + 0.6f))
				{
					float[] array = new float[8];
					for (int num5 = 0; num5 < 4; num5++)
					{
						float x = ((num5 == 0 || num5 == 3) ? model.Min.X : model.Max.X);
						float y = ((num5 < 2) ? model.Min.Y : model.Max.Y);
						Vec3 vec = mesh.Position + mesh.Basis.Mul(new Vec3(x, y, 0f));
						array[num5 * 2] = vec.X;
						array[num5 * 2 + 1] = vec.Y;
					}
					Vec3 vec2 = mesh.Basis.Mul(new Vec3(0f, -1f, 0f));
					Vec3 vec3 = mesh.Position + mesh.Basis.Mul(new Vec3((model.Min.X + model.Max.X) * 0.5f, (model.Min.Y + model.Max.Y) * 0.5f, 0f));
					Piece piece = new Piece
					{
						Name = model.Name,
						Kind = KindOf(model),
						Corners = array,
						X = vec3.X,
						Y = vec3.Y,
						Yaw = (float)Math.Atan2(vec2.Y, vec2.X),
						Storey = num3
					};
					roomLayout.Pieces.Add(piece);
					if (piece.Kind == PieceKind.Hearth)
					{
						list.Add(piece);
					}
					else if (piece.Kind == PieceKind.Table)
					{
						list2.Add(piece);
					}
					if (piece.Kind == PieceKind.Bed)
					{
						roomLayout.Places.Add(BedOf(mesh, model, num, num3, num4, room));
					}
					else if (piece.Kind == PieceKind.Storage && IsChest(model.Name))
					{
						float num6 = (float)Math.Sqrt(vec2.X * vec2.X + vec2.Y * vec2.Y);
						float num7 = ((num6 > 0.0001f) ? (vec2.X / num6) : 0f);
						float num8 = ((num6 > 0.0001f) ? (vec2.Y / num6) : (-1f));
						float num9 = (model.Max.Y - model.Min.Y) * 0.5f * num;
						roomLayout.Places.Add(new Place
						{
							Use = Use.Chest,
							X = vec3.X + num7 * (num9 + 0.3f),
							Y = vec3.Y + num8 * (num9 + 0.3f),
							Z = num4,
							Yaw = (float)Math.Atan2(0f - num8, 0f - num7),
							Storey = num3
						});
					}
				}
			}
			for (int num10 = 0; num10 < room.Spots.Count; num10++)
			{
				float[] array2 = room.Spots[num10];
				string text = ((num10 < room.SpotUse.Count) ? room.SpotUse[num10] : "");
				float num11 = ((array2.Length > 4) ? array2[4] : 0f);
				bool flag = array2.Length > 3 && array2[3] > 0f;
				Place place = new Place
				{
					X = array2[0],
					Y = array2[1],
					Z = num11,
					Yaw = array2[2] * (MathF.PI / 180f),
					Storey = StoreyOfZ(num11 + 0.05f),
					Seat = flag,
					Spot = num10
				};
				if (flag)
				{
					place.Use = ((Nearer(list, array2[0], array2[1], place.Storey) <= Nearer(list2, array2[0], array2[1], place.Storey)) ? Use.HearthSeat : Use.TableSeat);
				}
				else
				{
					switch (text)
					{
					case "hearth":
						place.Use = Use.Hearth;
						break;
					case "table":
						place.Use = Use.Table;
						break;
					case "anvil":
					case "workbench":
					case "counter":
						place.Use = Use.Bench;
						break;
					case "door":
						place.Use = Use.Door;
						break;
					default:
						place.Use = Use.Free;
						break;
					case "bed":
						continue;
					}
				}
				roomLayout.Places.Add(place);
			}
			if (fit != null && !float.IsNaN(fit.StairFootX))
			{
				float num12 = fit.StairHeadX - fit.StairFootX;
				float num13 = fit.StairHeadY - fit.StairFootY;
				float num14 = (float)Math.Sqrt(num12 * num12 + num13 * num13);
				if (num14 > 0.0001f)
				{
					roomLayout.Places.Add(new Place
					{
						Use = Use.Stair,
						X = fit.StairFootX - num12 / num14 * 0.35f,
						Y = fit.StairFootY - num13 / num14 * 0.35f,
						Yaw = (float)Math.Atan2(num13, num12),
						Storey = 0
					});
				}
			}
			roomLayout.Places.Add(new Place
			{
				Use = Use.Door,
				X = room.DoorX,
				Y = room.DoorY,
				Yaw = (float)Math.Atan2(0f - room.DoorOutY, 0f - room.DoorOutX),
				Storey = 0
			});
			foreach (Place place2 in roomLayout.Places)
			{
				if (place2.Use != Use.Bed)
				{
					place2.MidX = place2.X;
					place2.MidY = place2.Y;
				}
			}
			return roomLayout;
			int StoreyOfZ(float z)
			{
				if (fit == null)
				{
					return 0;
				}
				return fit.StoreyAt(z);
			}
		}

		private static float Nearer(List<Piece> pieces, float x, float y, int storey)
		{
			float num = float.MaxValue;
			foreach (Piece piece in pieces)
			{
				if (piece.Storey == storey)
				{
					num = Math.Min(num, (piece.X - x) * (piece.X - x) + (piece.Y - y) * (piece.Y - y));
				}
			}
			return num;
		}

		private static bool IsChest(string name)
		{
			if (name != null)
			{
				if (!name.Contains("_chest"))
				{
					return name == "int_crate";
				}
				return true;
			}
			return false;
		}

		public static PieceKind KindOf(MeshModel md)
		{
			string text = md.Info("use") ?? "";
			string text2 = md.Name ?? "";
			if (text == "bed" || text == "loft" || (text2.Contains("_bed") && !text2.Contains("side")) || text2 == "int_v_bedroll")
			{
				return PieceKind.Bed;
			}
			if (text == "hearth" || text2.Contains("hearth") || text2.Contains("fireplace") || text2.Contains("firepit") || text2.Contains("forge") || text2.Contains("stove"))
			{
				return PieceKind.Hearth;
			}
			switch (text)
			{
			case "table":
				return PieceKind.Table;
			case "seat":
				return PieceKind.Seat;
			default:
				if (!text2.Contains("_rug"))
				{
					switch (text)
					{
					case "workbench":
					case "anvil":
					case "counter":
						return PieceKind.Bench;
					default:
						if (!text2.Contains("_chest"))
						{
							switch (text2)
							{
							default:
								if (!text2.Contains("kegs"))
								{
									if (text == "walk" || text2.Contains("stair") || text2.Contains("spiral"))
									{
										return PieceKind.Stair;
									}
									return PieceKind.Other;
								}
								break;
							case "int_crate":
							case "int_barrel":
							case "int_sack":
								break;
							}
						}
						return PieceKind.Storage;
					}
				}
				goto case "rug";
			case "rug":
				return PieceKind.Rug;
			}
		}

		private static Place BedOf(MeshInstance m, MeshModel model, float scale, int storey, float floor, Interior room)
		{
			Vec3 vec = m.Basis.Mul(new Vec3(0f, -1f, 0f));
			float num = (float)Math.Atan2(vec.Y, vec.X);
			Vec3 min = model.Min;
			Vec3 max = model.Max;
			float num2 = (max.Y - min.Y) * 0.5f * scale;
			Vec3 vec2 = m.Basis.Mul(new Vec3((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f, 0f)) + m.Position;
			float num3 = (float)Math.Cos(num);
			float num4 = (float)Math.Sin(num);
			float num5 = Math.Abs(max.X - min.X) * scale;
			float sideX = vec2.X - num4 * 0.5f;
			float sideY = vec2.Y + num3 * 0.5f;
			float num6 = 1.44f;
			for (int i = 0; i < room.Spots.Count && i < room.SpotUse.Count; i++)
			{
				if (room.SpotUse[i] != "bed")
				{
					continue;
				}
				float[] array = room.Spots[i];
				float num7 = ((array.Length > 4) ? array[4] : 0f);
				if (room.Fit == null || room.Fit.StoreyAt(num7 + 0.05f) == storey)
				{
					float num8 = (array[0] - vec2.X) * (array[0] - vec2.X) + (array[1] - vec2.Y) * (array[1] - vec2.Y);
					if (num8 < num6)
					{
						num6 = num8;
						sideX = array[0];
						sideY = array[1];
					}
				}
			}
			return new Place
			{
				Use = Use.Bed,
				X = vec2.X + num3 * (num2 - 0.12f),
				Y = vec2.Y + num4 * (num2 - 0.12f),
				MidX = vec2.X,
				MidY = vec2.Y,
				Z = floor,
				Yaw = num,
				Storey = storey,
				Width = num5,
				Sleeps = ((!(num5 >= 0.6f)) ? 1 : 2),
				SideX = sideX,
				SideY = sideY
			};
		}
	}
}
