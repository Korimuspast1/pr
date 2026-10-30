using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Portraits
	{
		private sealed class Shot
		{
			public Texture2D Texture;

			public ulong Key;

			public bool Tried;

			public int Fails;

			public int FailedFrame;
		}

		public const int Size = 40;

		private static readonly Dictionary<long, Shot> Shots = new Dictionary<long, Shot>();

		private static readonly CharacterView View = new CharacterView();

		private static int _drawnFrame = -1;

		internal static int Drawn;

		public static Texture2D For(Actor a, SpriteCache sprites, bool palette)
		{
			if (a == null)
			{
				return null;
			}
			long id;
			try
			{
				id = a.data.id;
			}
			catch
			{
				return null;
			}
			Shots.TryGetValue(id, out var value);
			bool flag;
			try
			{
				flag = a.isAlive();
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				return value?.Texture;
			}
			ulong num = KeyOf(a);
			if (value != null && value.Tried && value.Key == num)
			{
				return value.Texture;
			}
			if (value != null && value.Key == num && value.Fails > 0 && Time.frameCount - value.FailedFrame < 20)
			{
				return value.Texture;
			}
			if (Time.frameCount == _drawnFrame || sprites == null || Event.current == null || Event.current.type != EventType.Repaint)
			{
				return value?.Texture;
			}
			_drawnFrame = Time.frameCount;
			if (value == null)
			{
				if (Shots.Count > 300)
				{
					Forget();
				}
				value = new Shot();
				Shots[id] = value;
			}
			if (value.Key != num)
			{
				value.Fails = 0;
			}
			value.Key = num;
			if (!View.DrawHead(a, sprites, 40, 40, palette))
			{
				value.FailedFrame = Time.frameCount;
				value.Tried = ++value.Fails >= 6;
				return value.Texture;
			}
			value.Tried = true;
			value.Fails = 0;
			if (value.Texture == null)
			{
				value.Texture = new Texture2D(40, 40, TextureFormat.RGBA32, mipChain: false)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonPortrait"
				};
			}
			value.Texture.SetPixelData(View.Rgba, 0);
			value.Texture.Apply(updateMipmaps: false);
			Drawn++;
			return value.Texture;
		}

		private static ulong KeyOf(Actor a)
		{
			ulong num = 1469598103934665603uL;
			if (People.LookForTest(a, out var look, out var _))
			{
				num ^= look.Key();
			}
			string text = null;
			try
			{
				text = ((a.equipment != null) ? People.ItemIdOf(a.equipment.helmet) : null);
			}
			catch
			{
			}
			if (text != null)
			{
				string text2 = text;
				foreach (char c in text2)
				{
					num ^= c;
					num *= 1099511628211L;
				}
			}
			num = num * 31 + (ulong)People.AgeOf(a);
			return num * 31 + (ulong)(Models.IsFemale(a) ? 1 : 0);
		}

		internal static Texture2D ShotForTest(Actor a)
		{
			try
			{
				Shot value;
				return (a != null && Shots.TryGetValue(a.data.id, out value)) ? value.Texture : null;
			}
			catch
			{
				return null;
			}
		}

		private static void Forget()
		{
			foreach (Shot value in Shots.Values)
			{
				if (value.Texture != null)
				{
					Object.Destroy(value.Texture);
				}
			}
			Shots.Clear();
		}
	}
}
