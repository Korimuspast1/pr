namespace FirstPerson.Core
{
	public sealed class DragonPose
	{
		public readonly float[] Elev = new float[2];

		public readonly float[] Sweep = new float[2];

		public readonly float[] Twist = new float[2];

		public readonly float[] Elbow = new float[2];

		public readonly float[] Wrist = new float[2];

		public readonly float[] Spread = new float[2] { 1f, 1f };

		public readonly float[] Billow = new float[2];

		public float NeckPitch;

		public float NeckYaw;

		public float NeckCurl;

		public float HeadPitch;

		public float HeadYaw;

		public float HeadRoll;

		public float Jaw;

		public float TailPitch;

		public float TailYaw;

		public float TailWave;

		public float TailSway;

		public float TailLift;

		public float Tuck;

		public float Reach;

		public float Crouch;

		public float WalkPhase;

		public float Stride;

		public float Chest;

		public float Glow;

		public float BodyPitch;

		public float BodyRoll;

		public float Bob;

		public void CopyFrom(DragonPose o)
		{
			for (int i = 0; i < 2; i++)
			{
				Elev[i] = o.Elev[i];
				Sweep[i] = o.Sweep[i];
				Twist[i] = o.Twist[i];
				Elbow[i] = o.Elbow[i];
				Wrist[i] = o.Wrist[i];
				Spread[i] = o.Spread[i];
				Billow[i] = o.Billow[i];
			}
			NeckPitch = o.NeckPitch;
			NeckYaw = o.NeckYaw;
			NeckCurl = o.NeckCurl;
			HeadPitch = o.HeadPitch;
			HeadYaw = o.HeadYaw;
			HeadRoll = o.HeadRoll;
			Jaw = o.Jaw;
			TailPitch = o.TailPitch;
			TailYaw = o.TailYaw;
			TailWave = o.TailWave;
			TailSway = o.TailSway;
			TailLift = o.TailLift;
			Tuck = o.Tuck;
			Reach = o.Reach;
			Crouch = o.Crouch;
			WalkPhase = o.WalkPhase;
			Stride = o.Stride;
			Chest = o.Chest;
			Glow = o.Glow;
			BodyPitch = o.BodyPitch;
			BodyRoll = o.BodyRoll;
			Bob = o.Bob;
		}

		public void Blend(DragonPose o, float k)
		{
			for (int i = 0; i < 2; i++)
			{
				Elev[i] += (o.Elev[i] - Elev[i]) * k;
				Sweep[i] += (o.Sweep[i] - Sweep[i]) * k;
				Twist[i] += (o.Twist[i] - Twist[i]) * k;
				Elbow[i] += (o.Elbow[i] - Elbow[i]) * k;
				Wrist[i] += (o.Wrist[i] - Wrist[i]) * k;
				Spread[i] += (o.Spread[i] - Spread[i]) * k;
				Billow[i] += (o.Billow[i] - Billow[i]) * k;
			}
			NeckPitch += (o.NeckPitch - NeckPitch) * k;
			NeckYaw += (o.NeckYaw - NeckYaw) * k;
			NeckCurl += (o.NeckCurl - NeckCurl) * k;
			HeadPitch += (o.HeadPitch - HeadPitch) * k;
			HeadYaw += (o.HeadYaw - HeadYaw) * k;
			HeadRoll += (o.HeadRoll - HeadRoll) * k;
			Jaw += (o.Jaw - Jaw) * k;
			TailPitch += (o.TailPitch - TailPitch) * k;
			TailYaw += (o.TailYaw - TailYaw) * k;
			TailSway += (o.TailSway - TailSway) * k;
			TailLift += (o.TailLift - TailLift) * k;
			Tuck += (o.Tuck - Tuck) * k;
			Reach += (o.Reach - Reach) * k;
			Crouch += (o.Crouch - Crouch) * k;
			Stride += (o.Stride - Stride) * k;
			Chest += (o.Chest - Chest) * k;
			Glow += (o.Glow - Glow) * k;
			BodyPitch += (o.BodyPitch - BodyPitch) * k;
			BodyRoll += (o.BodyRoll - BodyRoll) * k;
			Bob += (o.Bob - Bob) * k;
		}
	}
}
