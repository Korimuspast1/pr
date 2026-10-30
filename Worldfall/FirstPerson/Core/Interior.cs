using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class Interior
	{
		public TerrainCache Floor;

		public readonly List<VoxelObject> Props = new List<VoxelObject>();

		public readonly List<MeshInstance> Meshes = new List<MeshInstance>();

		public readonly List<float[]> Solids = new List<float[]>();

		public readonly List<float[]> Spots = new List<float[]>();

		public readonly List<string> SpotUse = new List<string>();

		public readonly List<RoomLight> Lights = new List<RoomLight>();

		public float MinX;

		public float MinY;

		public float MaxX;

		public float MaxY;

		public float DoorX;

		public float DoorY;

		public float DoorOutX;

		public float DoorOutY = -1f;

		public FitInfo Fit;

		public float SpawnX;

		public float SpawnY;

		public float SpawnYaw;

		public float HearthX;

		public float HearthY;

		public float Top;

		public uint CeilingTop;

		public uint CeilingBottom;

		public string Race = "human";

		public int Tier;

		public readonly List<LightWindow> Windows = new List<LightWindow>();

		public readonly List<string> Skipped = new List<string>();

		public Dictionary<char, float[,]> WallRelief;

		internal MeshInstance Shell;

		internal int GlassSlot = -1;

		public int DoorLeafPart = -1;

		public float DoorSwingSign = 1f;

		public int DoorBarPart = -1;

		public Vec3 DoorBarAlong;

		public Vec3 DoorBarInward;

		public Vec3 DoorBarMove;

		public float DoorBarLean;

		public float DoorBarYaw;

		public float DoorBarTip;

		public float[] DoorBarKeep;

		public bool DoorPortal;

		public void SwingDoor(float radians, float bar = 0f)
		{
			if (Shell == null || DoorLeafPart < 0)
			{
				return;
			}
			bar = ((bar < 0f) ? 0f : ((bar > 1f) ? 1f : bar));
			if (radians <= 0f && bar <= 0f)
			{
				Shell.PartRotation = null;
				Shell.PartOffset = null;
				Shell.PartScale = null;
				return;
			}
			Shell.EnsurePose();
			Shell.PartRotation[DoorLeafPart] = Mat3.RotZ(DoorSwingSign * Math.Max(0f, radians));
			if (DoorBarPart >= 0 && DoorBarPart < Shell.PartRotation.Length)
			{
				float num = bar * bar * (3f - 2f * bar);
				Shell.PartRotation[DoorBarPart] = Mat3.Rotation(DoorBarInward, DoorBarTip * num) * Mat3.RotZ(DoorBarYaw * num) * Mat3.Rotation(DoorBarAlong, (0f - DoorBarLean) * num);
				Shell.PartOffset[DoorBarPart] = DoorBarMove * num;
			}
		}

		public void Daylight(float day)
		{
			if (Shell != null && GlassSlot >= 0 && Shell.Palette != null)
			{
				day = ((day < 0f) ? 0f : ((day > 1f) ? 1f : day));
				Shell.Palette[GlassSlot] = TerrainCache.Lerp(TerrainCache.Rgb(30, 40, 74), TerrainCache.Rgb(200, 224, 246), day);
			}
		}
	}
}
