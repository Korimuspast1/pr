using System;
using System.Reflection;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace FirstPerson
{
	internal static class SpatialHooks
	{
		private const int PlayOneShotIl = 120;

		private const int AttachIl = 50;

		private const int SetPanIl = 67;

		private static bool _installed;

		private const int IdleIl = 37;

		private const int AttackIl = 38;

		private const int DeathIl = 39;

		private static Actor _speaker;

		private static Action<EventInstance, Transform, bool> _attach;

		public static bool Installed => _installed;

		public static bool VoicesInstalled { get; private set; }

		public static void Install()
		{
			if (_installed)
			{
				return;
			}
			MethodInfo original = Game("PlayOneShot", 120, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, typeof(GUID), typeof(Vector3), typeof(bool));
			MethodInfo original2 = Game("attachToObject", 50, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, typeof(string), typeof(GameObject), typeof(bool));
			MethodInfo original3 = Game("setPan", 67, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, typeof(EventInstance), typeof(float), typeof(float));
			Detour.Apply(original, typeof(SpatialHooks).GetMethod("PlayOneShot", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			Detour.Apply(original2, typeof(SpatialHooks).GetMethod("AttachToObject", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			Detour.Apply(original3, typeof(SpatialHooks).GetMethod("SetPan", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			_installed = true;
			try
			{
				MethodInfo original4 = Game(typeof(Actor), "playIdleSound", 37, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo original5 = Game(typeof(Actor), "makeSoundAttack", 38, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo original6 = Game(typeof(Actor), "playDeathSound", 39, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(Actor));
				Detour.Apply(original4, typeof(SpatialHooks).GetMethod("PlayIdleSound", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(original5, typeof(SpatialHooks).GetMethod("MakeSoundAttack", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(original6, typeof(SpatialHooks).GetMethod("PlayDeathSound", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				VoicesInstalled = true;
			}
			catch (Exception ex)
			{
				Log.Error("3D sound: the creatures' voices stay at their tiles: " + ex.Message);
			}
		}

		private static MethodInfo Game(string name, int ilBytes, BindingFlags flags, params Type[] args)
		{
			return Game(typeof(MusicBox), name, ilBytes, flags, args);
		}

		private static MethodInfo Game(Type type, string name, int ilBytes, BindingFlags flags, params Type[] args)
		{
			MethodInfo method = type.GetMethod(name, flags, null, args, null);
			int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
			if (num != ilBytes)
			{
				throw new InvalidOperationException("the game's " + type.Name + "." + name + " has changed (" + num + " bytes of IL, not " + ilBytes + ")");
			}
			return method;
		}

		private static void PlayIdleSound(Actor self)
		{
			if (!self.asset.has_sound_idle || (FirstPersonAudio.Speech != null && FirstPersonAudio.Speech.TakeChatter(self)))
			{
				return;
			}
			Actor speaker = _speaker;
			_speaker = self;
			try
			{
				MusicBox.playIdleSoundVisibleOnly(self.asset.sound_idle, self.current_tile);
			}
			finally
			{
				_speaker = speaker;
			}
		}

		private static void MakeSoundAttack(Actor self)
		{
			if (!self.asset.has_sound_attack)
			{
				return;
			}
			Actor speaker = _speaker;
			_speaker = self;
			try
			{
				MusicBox.playSound(self.asset.sound_attack, self.current_tile, pGameViewOnly: true, pVisibleOnly: true);
			}
			finally
			{
				_speaker = speaker;
			}
		}

		private static void PlayDeathSound(Actor self, Actor actor)
		{
			if (!self.asset.has_sound_death)
			{
				return;
			}
			Actor speaker = _speaker;
			_speaker = self;
			try
			{
				MusicBox.playSound(self.asset.sound_death, self.current_tile, pGameViewOnly: true, pVisibleOnly: true);
			}
			finally
			{
				_speaker = speaker;
			}
		}

		private static EventInstance PlayOneShot(GUID guid, Vector3 position, bool set3D)
		{
			EventInstance eventInstance = RuntimeManager.CreateInstance(guid);
			if (set3D)
			{
				eventInstance.set3DAttributes(Attributes(position));
				FirstPersonAudio live = FirstPersonAudio.Live;
				Actor speaker = _speaker;
				_speaker = null;
				try
				{
					if (live == null || !live.TakeOneShot(eventInstance, position, speaker))
					{
						FirstPersonAudio.Walls?.TakeFlat(eventInstance, position, null, bed: false, speaker);
					}
				}
				catch (Exception ex)
				{
					Log.Error("audio: one-shot: " + ex.Message);
				}
			}
			else
			{
				Vector3 position2 = World.world.move_camera.transform.position;
				float orthographicSize = World.world.move_camera.main_camera.orthographicSize;
				eventInstance.set3DAttributes(Attributes(new Vector3(position2.x, position2.y, orthographicSize)));
			}
			eventInstance.start();
			eventInstance.release();
			return eventInstance;
		}

		private static EventInstance AttachToObject(string id, GameObject follow, bool play)
		{
			if (!MusicBox.sounds_on)
			{
				return default(EventInstance);
			}
			EventInstance newInstance = MusicBox.getNewInstance(id);
			AttachToTransform(newInstance, follow.transform);
			FirstPersonAudio live = FirstPersonAudio.Live;
			if (live == null || !live.TakeAttached(newInstance, follow.transform, play))
			{
				FirstPersonAudio.Walls?.TakeFlat(newInstance, follow.transform.position, follow.transform, bed: false);
			}
			if (play)
			{
				newInstance.start();
			}
			return newInstance;
		}

		private static void SetPan(EventInstance instance, float x, float y)
		{
			if (x != -1f || y != -1f)
			{
				FirstPersonAudio live = FirstPersonAudio.Live;
				if (live == null || !live.TakeBed(instance, x, y))
				{
					FirstPersonAudio.Walls?.TakeFlat(instance, new Vector3(x, y, 0f), null, bed: true);
					Transform transform = MusicBox._sound_object.transform;
					transform.position = new Vector3(x, y, 0f);
					instance.set3DAttributes(Attributes(transform.position, transform.forward, transform.up));
				}
			}
		}

		internal static ATTRIBUTES_3D Attributes(Vector3 at)
		{
			return Attributes(at, Vector3.forward, Vector3.up);
		}

		private static ATTRIBUTES_3D Attributes(Vector3 at, Vector3 forward, Vector3 up)
		{
			return new ATTRIBUTES_3D
			{
				position = new VECTOR
				{
					x = at.x,
					y = at.y,
					z = at.z
				},
				forward = new VECTOR
				{
					x = forward.x,
					y = forward.y,
					z = forward.z
				},
				up = new VECTOR
				{
					x = up.x,
					y = up.y,
					z = up.z
				}
			};
		}

		internal static void AttachToTransform(EventInstance instance, Transform follow)
		{
			if (_attach == null)
			{
				MethodInfo method = typeof(RuntimeManager).GetMethod("AttachInstanceToGameObject", BindingFlags.Static | BindingFlags.Public, null, new Type[3]
				{
					typeof(EventInstance),
					typeof(Transform),
					typeof(bool)
				}, null);
				_attach = (Action<EventInstance, Transform, bool>)Delegate.CreateDelegate(typeof(Action<EventInstance, Transform, bool>), method);
			}
			_attach(instance, follow, arg3: false);
		}
	}
}
