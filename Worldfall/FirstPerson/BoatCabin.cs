using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BoatCabin : ModelRoom
	{
		public float Heading;

		public float Pitch;

		public float Roll;

		private List<int> _lamps = new List<int>();

		private Vector2 _lastBoat;

		private float _seed;

		private float _span = 1.5f;

		private Vector3 _anchor;

		private float _rcx;

		private float _rcy;

		public Actor Boat { get; private set; }

		public override Actor Hidden => Boat;

		public override Vector2 Where
		{
			get
			{
				if (Boat == null)
				{
					return Eye;
				}
				return Boat.current_position;
			}
		}

		internal override object CombatKey => Boat;

		public bool Enter(Actor boat, Actor host)
		{
			string text = Models.BoatFamily(boat);
			string text2 = Models.CabinFor(text);
			MeshModel meshModel = Models.Get(text2);
			if (meshModel == null)
			{
				return false;
			}
			Interior interior = CabinBuilder.Build(meshModel, ModelRoom.KingdomCloth(boat.kingdom));
			_lamps = CabinBuilder.SwingingParts(meshModel);
			interior.Shell.EnsurePose();
			Boat = boat;
			float[] array = CabinBuilder.Floats(Models.Get(text)?.Info("cabin"));
			_anchor = ((array != null && array.Length >= 3) ? new Vector3(array[0], array[1], array[2]) : new Vector3(0f, 0f, 1f));
			_span = ((array != null && array.Length >= 4) ? array[3] : 1.2f);
			_rcx = (float)interior.Floor.Width * 0.5f;
			_rcy = (float)interior.Floor.Height * 0.5f;
			_seed = (float)(boat.GetHashCode() & 0x3FF) / 97f;
			Heading = (Creatures.BoatHeading(boat, out var heading) ? heading : (boat.flip ? 0f : MathF.PI));
			_lastBoat = boat.current_position;
			object name;
			switch (text2)
			{
			default:
				name = "cuddy";
				break;
			case "cabin_raft":
				name = "shelter";
				break;
			case "cabin_barge":
				name = "hold";
				break;
			case "cabin_cog":
				name = "cabin";
				break;
			case "cabin_galleon":
				name = "great cabin";
				break;
			}
			Name = (string)name;
			List<Actor> list = new List<Actor>();
			try
			{
				Boat simpleComponent = boat.getSimpleComponent<Boat>();
				if (simpleComponent != null)
				{
					foreach (Actor passenger in simpleComponent.getPassengers())
					{
						if (passenger != null && passenger != host && passenger.isAlive())
						{
							list.Add(passenger);
						}
					}
				}
			}
			catch
			{
			}
			Begin(interior, list, (int)(boat.getID() & 0x7FFFFFFF));
			Log.Info($"aboard {WorldBoxMod.Describe(boat)}: {meshModel.Name}, {interior.Lights.Count} lights, {interior.Windows.Count} windows, {Occupants.Count} aboard with you");
			return true;
		}

		public override void Exit()
		{
			base.Exit();
			Boat = null;
		}

		protected override bool Present(Actor a)
		{
			if (a.is_inside_boat && a.inside_boat != null)
			{
				return ((ActorSimpleComponent)a.inside_boat).actor == Boat;
			}
			return false;
		}

		public override void Tick(float dt)
		{
			if (Boat != null)
			{
				Vector2 current_position = Boat.current_position;
				Vector2 vector = current_position - _lastBoat;
				_lastBoat = current_position;
				if (Creatures.BoatHeading(Boat, out var heading))
				{
					Heading = heading;
				}
				else if (vector.sqrMagnitude > 1E-06f && dt > 0f)
				{
					Heading = Mathf.LerpAngle(Heading * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f, 1f - Mathf.Exp(-2.5f * dt)) * (MathF.PI / 180f);
				}
				float num = Sea.Now + _seed * 7f;
				if (Creatures.BoatTilt(Boat, out var bowUp, out var portUp))
				{
					Roll = 0f - portUp;
					Pitch = 0f - bowUp;
				}
				else
				{
					Roll = 0.03f * Mathf.Sin(num * 0.9f);
					Pitch = 0.012f * Mathf.Sin(num * 1.15f + 1f);
				}
				MeshInstance shell = base.Room.Shell;
				shell.EnsurePose();
				for (int i = 0; i < _lamps.Count; i++)
				{
					shell.PartRotation[_lamps[i]] = Mat3.RotY((0f - Roll) * 3f) * Mat3.RotX((0f - Pitch) * 3f + 0.04f * Mathf.Sin(num * 2.3f + (float)i));
				}
			}
		}

		public override ViewState OutsideView(ViewState inside)
		{
			Creatures.BoatPose(Boat, Boat.current_position.x, Boat.current_position.y, Heading, out var pos, out var basis, out var _);
			float num = _span / Mathf.Max(1f, base.Room.MaxY - base.Room.MinY);
			Vec3 v = new Vec3(_anchor.x + (inside.X - _rcx) * num, _anchor.y + (inside.Y - _rcy) * num, _anchor.z);
			Vec3 vec = basis.Mul(v) + pos;
			float num2 = (0f - Pitch) * Mathf.Sin(inside.Yaw) + Roll * Mathf.Cos(inside.Yaw);
			return new ViewState
			{
				X = vec.X,
				Y = vec.Y,
				Z = Mathf.Max(vec.Z, 0.12f),
				Yaw = inside.Yaw + Heading + MathF.PI / 2f,
				Pitch = Mathf.Tan(Mathf.Atan(inside.Pitch) - num2),
				Fov = inside.Fov,
				Shear = inside.Shear,
				Free = inside.Free
			};
		}

		public override Vec3 LightInside(Vec3 world)
		{
			float f = 0f - (Heading + MathF.PI / 2f);
			float num = Mathf.Cos(f);
			float num2 = Mathf.Sin(f);
			return new Vec3(world.X * num - world.Y * num2, world.X * num2 + world.Y * num, Mathf.Max(0.3f, world.Z)).Normalized();
		}
	}
}
