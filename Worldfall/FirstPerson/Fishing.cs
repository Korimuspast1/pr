using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class Fishing
	{
		static float _castAt = -100f;
		static Vector2 _castPos;
		static bool _casting;

		static readonly string[] _fish = new string[] { "fish", "fish", "fish", "fish", "meat", "wheat", "berries" };

		public static void Update(Actor host, float dt, Action<string> toast)
		{
			if (host == null || World.world == null) return;
			// hold F near water to fish
			bool nearWater = false;
			Vector2 pos = host.current_position;
			try
			{
				for (int dx=-1; dx<=1; dx++) for (int dy=-1; dy<=1; dy++)
				{
					var tile = World.world.GetTile((int)(pos.x+dx), (int)(pos.y+dy));
					if (tile != null && tile.is_liquid) { nearWater = true; break; }
				}
			} catch {}
			if (!nearWater) { _casting = false; return; }

			if (PlayInput.GetKeyDown(KeyCode.F) && !WorldBoxMod.Instance.AnyMenuOpen)
			{
				if (!_casting)
				{
					_casting = true;
					_castAt = Time.unscaledTime;
					_castPos = pos;
					toast?.Invoke("Cast line... wait for bite (F to reel)");
					Log.Info("fish: cast at " + pos);
				}
				else
				{
					// reel
					float wait = Time.unscaledTime - _castAt;
					_casting = false;
					if (wait < 2f)
					{
						toast?.Invoke("Too early — fish not biting yet");
						return;
					}
					// success chance 70% after 2s, 100% after 5s
					float chance = Mathf.Clamp01((wait-2f)/3f * 0.8f + 0.4f);
					if (UnityEngine.Random.value < chance)
					{
						string reward = _fish[UnityEngine.Random.Range(0, _fish.Length)];
						int amt = 1 + UnityEngine.Random.Range(0, 3);
						try { Bag.Put(host, reward, amt, spill:false); } catch {}
						toast?.Invoke($"Caught {amt} {reward}! (+{amt})");
						Log.Info($"fish: caught {amt} {reward} after {wait:F1}s");
					}
					else
					{
						toast?.Invoke("No bite, try again");
					}
				}
			}
			// auto cancel if moved far
			if (_casting && (pos - _castPos).sqrMagnitude > 9f)
			{
				_casting = false;
				toast?.Invoke("Line pulled — moved away");
			}
			// auto bite hint after 3s
			if (_casting && Time.unscaledTime - _castAt > 3f && Time.unscaledTime - _castAt < 3.1f)
			{
				toast?.Invoke("Bite! Press F to reel!");
			}
		}
	}
}
