namespace FirstPerson.Core
{
	public sealed class SpriteImage
	{
		public int Width;

		public int Height;

		public uint[] Pixels;

		public float PivotX = 0.5f;

		public float PivotY;

		public float UnitWidth = 1f;

		public float UnitHeight = 1f;

		public int OpaqueBottom;

		public int OpaqueTop;

		private VoxelShape _inflated;

		private VoxelShape _slab;

		private VoxelShape _tree;

		private int _slabDepth;

		private VoxelShape _creature;

		private VoxelShape _house;

		private int _houseDepth;

		public VoxelShape Inflated => _inflated ?? (_inflated = VoxelShape.Inflate(this, 0.85f, 10));

		public VoxelShape Tree => _tree ?? (_tree = VoxelShape.Leafy(this));

		public VoxelShape CreatureShape => _creature ?? (_creature = VoxelShape.Creature(this, 0.8f, 8));

		public float BottomPadding
		{
			get
			{
				if (Height <= 0)
				{
					return 0f;
				}
				return (float)OpaqueBottom / (float)Height;
			}
		}

		public float VisibleFraction
		{
			get
			{
				if (Height <= 0)
				{
					return 1f;
				}
				return (float)(OpaqueTop - OpaqueBottom) / (float)Height;
			}
		}

		public VoxelShape SlabShape(int halfThickness)
		{
			if (_slab == null || _slabDepth != halfThickness)
			{
				_slab = VoxelShape.Slab(this, halfThickness);
				_slabDepth = halfThickness;
			}
			return _slab;
		}

		public VoxelShape HouseShape(int halfDepth)
		{
			if (_house == null || _houseDepth != halfDepth)
			{
				_house = VoxelShape.House(this, halfDepth, 0.45f);
				_houseDepth = halfDepth;
			}
			return _house;
		}

		public void MeasureOpaqueRows()
		{
			OpaqueBottom = Height;
			OpaqueTop = 0;
			for (int i = 0; i < Height; i++)
			{
				for (int j = 0; j < Width; j++)
				{
					if (Pixels[i * Width + j] >> 24 >= 128)
					{
						if (i < OpaqueBottom)
						{
							OpaqueBottom = i;
						}
						OpaqueTop = i + 1;
						break;
					}
				}
			}
			if (OpaqueTop <= OpaqueBottom)
			{
				OpaqueBottom = 0;
				OpaqueTop = Height;
			}
		}
	}
}
