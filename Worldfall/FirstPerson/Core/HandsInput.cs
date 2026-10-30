namespace FirstPerson.Core
{
	public struct HandsInput
	{
		public SpriteImage Body;

		public object Owner;

		public int Look;

		public bool Humanoid;

		public string Species;

		public string ItemId;

		public SpriteImage Icon;

		public float SinceAttack;

		public float Charge;

		public float Guard;

		public bool Melee;

		public int Attacks;

		public Swing Swing;

		public float Hurt;

		public bool Swimming;

		public float SwimPhase;

		public float Stride;

		public float GaitPhase;

		public float Run;

		public float BodyHeight;

		public float Dt;

		public BowState Bow;

		public MeshModel ArrowModel;

		public float Time;

		public Gesture Gesture;

		public float SinceGesture;

		public bool GestureHeld;

		public float GestureRelease;

		public MeshModel GestureProp;

		public uint GestureGlow;

		public MeshModel GunModel;

		public BlasterState Blaster;

		public ArmLook ArmLook;

		public string Armor;

		public float Binoculars;

		public CompassState Compass;

		public uint Skin;

		public uint Sleeve;

		public float StoppedAt;

		public float StoppedWeight;

		public bool StoppedRings;

		public float ClashAge;

		public float ClashWeight;

		public bool ClashRings;

		public ShotgunState Shotgun;

		public MeshModel ShotgunModel;

		public MeshModel ShellModel;

		public MeshModel HeldModel;

		public float WindupScale;

		public WeaponKind HeldKind;

		public bool Dig;

		public float DigRate;

		public bool DigFill;

		public bool HeldItem;

		public bool HeldPalm;

		public bool HeldDrink;

		public Vec3 HeldMouth;

		public float EatAge;

		public float Bandage;

		public bool BandageOther;

		public bool BandageOver;

		public bool Wrapped;

		public MeshModel BandageRoll;

		public bool Tracked;

		public HandTrack TrackR;

		public HandTrack TrackL;

		public bool BowOnString;

		public MeshModel TorchModel;

		public bool TorchLeft;

		public bool TorchMain;

		public bool TorchReach;

		public bool TorchLit;

		public float TorchSputter;
	}
}
