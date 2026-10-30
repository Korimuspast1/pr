using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PaceTest : CivilTest
	{
		private Actor _host;

		private Actor _subject;

		private readonly List<Actor> _village = new List<Actor>();

		private float _worldAtX1 = -1f;

		private float _worldAtFifth = -1f;

		private float _actionAtFifth = -1f;

		protected override string Prefix => "PACE";

		protected override IEnumerator Body()
		{
			int saved = ((Mod.Settings != null) ? Mod.Settings.WorldPace : 0);
			yield return Part("the slower speeds", Speeds());
			Actor actor = Village("human", 14, 3);
			if (!Check("a village founded", actor != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive())
				{
					_village.Add(item);
				}
			}
			foreach (Actor item2 in _village)
			{
				if (item2 != actor && item2.isAdult() && _host == null)
				{
					_host = item2;
				}
			}
			foreach (Actor item3 in _village)
			{
				if (item3 != actor && item3 != _host && item3.isAdult() && _subject == null)
				{
					_subject = item3;
				}
			}
			if (!Check("a host and a subject", _host != null && _subject != null))
			{
				yield break;
			}
			yield return Possess(_host);
			if (Check("first person on a villager", InControl(_host)))
			{
				yield return Part("the calendar at x1", Clock(0));
				yield return Part("the calendar at 5x slower", Clock(2));
				yield return Part("the clock without slowing kept with the world", Kept());
				yield return Part("a stun lasts as long", Stun());
				yield return Part("regeneration heals as often", Regeneration());
				yield return Part("walking keeps its pace", Walking());
				yield return Part("possession keeps the pace", Possession());
				yield return Part("fire still spreads", Fire());
				if (Mod.Settings != null)
				{
					Mod.Settings.WorldPace = saved;
				}
				WorldPace.Apply(saved);
			}
		}

		private IEnumerator Speeds()
		{
			yield return CivilTest.Wait(2f);
			WorldTimeScaleLibrary time_scales = AssetManager.time_scales;
			bool flag = true;
			WorldPace.Pace[] paces = WorldPace.Paces;
			foreach (WorldPace.Pace pace in paces)
			{
				flag &= time_scales.dict.ContainsKey(pace.Id);
			}
			Check("the game's speed list has the three slower speeds", flag);
			string text = "";
			for (int j = 0; j < Math.Min(6, time_scales.list.Count); j++)
			{
				text = text + ((j > 0) ? " " : "") + time_scales.list[j].id;
			}
			Check("slowest first, then the game's x½ and x1", text.StartsWith("fp_years_10 fp_years_5 fp_years_2 slow_mo x1", StringComparison.Ordinal), text);
			Transform fifth = WorldPace.Ui.ButtonForTest("fp_years_5");
			bool flag2 = fifth != null && WorldPace.Ui.ButtonForTest("fp_years_2") != null && WorldPace.Ui.ButtonForTest("fp_years_10") != null;
			Check("the hourglass's row has a button for each", flag2);
			if (!flag2)
			{
				Note("hourglass: " + WorldPace.Ui.DescribeForTest());
			}
			if (fifth != null)
			{
				Transform parent = fifth.parent;
				string text2 = "";
				for (int k = 0; k < parent.childCount; k++)
				{
					text2 = text2 + ((k > 0) ? " " : "") + parent.GetChild(k).name;
				}
				Note("hourglass row: " + text2);
				try
				{
					fifth.GetComponent<PowerButton>()?.clickButton();
				}
				catch (Exception ex)
				{
					Note("click: " + ex.Message);
				}
				yield return CivilTest.Wait(0.3f);
				Check("a click on 5x slower sets it", Config.time_scale_asset?.id == "fp_years_5", Config.time_scale_asset?.id);
				Sprite sprite = fifth.GetComponent<PowerButton>()?.icon?.sprite;
				Check("its icon is the slower years' own", sprite != null && sprite.texture != null && sprite.texture.width == 28, (sprite != null) ? (sprite.name + " " + sprite.texture?.width) : "none");
			}
			WorldPace.Apply(0);
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator Clock(int pace)
		{
			WorldPace.Apply(pace);
			yield return CivilTest.Wait(1f);
			double w0 = World.world.getCurWorldTime();
			double a0 = WorldPace.ActionNow;
			float t0 = Time.unscaledTime;
			yield return CivilTest.Wait(6f);
			float num = Time.unscaledTime - t0;
			float num2 = (float)(World.world.getCurWorldTime() - w0) / num;
			float num3 = (float)(WorldPace.ActionNow - a0) / num;
			Note($"pace {pace}: {num2:0.000} world s and {num3:0.000} unslowed s per real s (factor {WorldPace.Factor})");
			if (pace == 0)
			{
				_worldAtX1 = num2;
				Check("at x1 the clock without slowing is the world clock", Mathf.Abs(num3 - num2) < 0.02f * Mathf.Max(num2, 0.1f) + 0.01f, $"{num3:0.000} vs {num2:0.000}");
				yield break;
			}
			_worldAtFifth = num2;
			_actionAtFifth = num3;
			float num4 = ((_worldAtX1 > 0f) ? (num2 / _worldAtX1) : (-1f));
			Check("the calendar runs at a fifth of x1's", num4 > 0.15f && num4 < 0.26f, $"{num4:0.000}");
			float num5 = ((_worldAtX1 > 0f) ? (num3 / _worldAtX1) : (-1f));
			Check("the clock without slowing runs as x1's", num5 > 0.8f && num5 < 1.25f, $"{num5:0.000}");
			Check("the detours are in", WorldPace.Installed);
		}

		private IEnumerator Kept()
		{
			WorldPace.Apply(2);
			yield return CivilTest.Wait(1.5f);
			string pResult = null;
			try
			{
				World.world.map_stats.custom_data?.get("fp_pace_clock", out pResult, null);
			}
			catch
			{
			}
			double saved = 0.0;
			bool flag = pResult != null && double.TryParse(pResult, NumberStyles.Float, CultureInfo.InvariantCulture, out saved);
			Check("the world's save has how far it is ahead", flag && saved > 2.0, pResult ?? "none");
			double before = WorldPace.ActionNow;
			WorldPace.ForgetWorldForTest();
			yield return null;
			yield return null;
			double num = WorldPace.ActionNow - before;
			Note($"ahead by {saved:0.00} s; across the world read again it moved {num:0.000} s");
			Check("a loaded world's clock carries on from its save", Math.Abs(num) < 1.2, $"{num:0.000} s");
			WorldPace.Apply(0);
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator StunAt(int pace, float[] into, int slot)
		{
			WorldPace.Apply(pace);
			yield return CivilTest.Wait(0.5f);
			try
			{
				((BaseSimObject)_subject).addStatusEffect("stunned", 2f, pColorEffect: false);
			}
			catch (Exception ex)
			{
				Note("stun: " + ex.Message);
			}
			float t0 = Time.unscaledTime;
			yield return null;
			while (Time.unscaledTime - t0 < 20f)
			{
				bool flag = false;
				try
				{
					flag = _subject.isAlive() && ((BaseSimObject)_subject).hasStatus("stunned");
				}
				catch
				{
				}
				if (!flag)
				{
					break;
				}
				yield return null;
			}
			into[slot] = Time.unscaledTime - t0;
		}

		private IEnumerator Stun()
		{
			float[] s = new float[2];
			yield return StunAt(0, s, 0);
			yield return StunAt(2, s, 1);
			Note($"a 2 s stun: {s[0]:0.00} s at x1, {s[1]:0.00} s at 5x slower years");
			Check("a stun lasts about as long at 5x slower years", s[0] > 0.5f && s[1] > 0.6f * s[0] && s[1] < 1.5f * s[0], $"{s[0]:0.00} / {s[1]:0.00}");
			WorldPace.Apply(0);
		}

		private IEnumerator HealAt(int pace, int[] into, int slot)
		{
			WorldPace.Apply(pace);
			yield return CivilTest.Wait(0.5f);
			int before = 0;
			try
			{
				int maxHealth = _subject.getMaxHealth();
				_subject.setHealth(Mathf.Max(1, maxHealth / 4));
				before = _subject.getHealth();
			}
			catch (Exception ex)
			{
				Note("heal: " + ex.Message);
			}
			WorldPace.WatchedForTest = _subject;
			WorldPace.WatchedFired = 0;
			yield return CivilTest.Wait(9f);
			int num = before;
			try
			{
				num = _subject.getHealth();
			}
			catch
			{
			}
			into[slot] = WorldPace.WatchedFired;
			WorldPace.WatchedForTest = null;
			Note($"pace {pace}: the regeneration fired {into[slot]} times, health +{num - before}");
		}

		private IEnumerator Regeneration()
		{
			try
			{
				_subject.addTrait("regeneration");
			}
			catch (Exception ex)
			{
				Note("trait: " + ex.Message);
			}
			int[] h = new int[2];
			yield return HealAt(0, h, 0);
			yield return HealAt(2, h, 1);
			Check("regeneration fires about as often at 5x slower years", h[0] > 0 && (float)h[1] >= 0.6f * (float)h[0] && (float)h[1] <= 1.6f * (float)h[0], $"{h[0]} / {h[1]} times in 9 s");
			WorldPace.Apply(0);
		}

		private IEnumerator WalkAt(int pace, float[] into, int slot)
		{
			WorldPace.Apply(pace);
			yield return CivilTest.Wait(1f);
			Dictionary<Actor, Vector2> from = new Dictionary<Actor, Vector2>();
			foreach (Actor item in _village)
			{
				if (item != null && item.isAlive() && item != _host)
				{
					from[item] = item.current_position;
				}
			}
			float t0 = Time.unscaledTime;
			yield return CivilTest.Wait(4f);
			float num = Time.unscaledTime - t0;
			List<float> list = new List<float>();
			foreach (KeyValuePair<Actor, Vector2> item2 in from)
			{
				if (item2.Key != null && item2.Key.isAlive())
				{
					float magnitude = (item2.Key.current_position - item2.Value).magnitude;
					if (magnitude > 0.5f)
					{
						list.Add(magnitude / num);
					}
				}
			}
			list.Sort();
			into[slot] = ((list.Count > 0) ? list[list.Count / 2] : (-1f));
			Note($"pace {pace}: {list.Count} walked, median {into[slot]:0.00} tiles/s");
		}

		private IEnumerator Walking()
		{
			float[] w = new float[2];
			yield return WalkAt(0, w, 0);
			yield return WalkAt(2, w, 1);
			Check("villagers walk about as fast at 5x slower years", w[0] > 0f && w[1] > 0.6f * w[0] && w[1] < 1.6f * w[0], $"{w[0]:0.00} / {w[1]:0.00} tiles/s");
			WorldPace.Apply(0);
		}

		private IEnumerator Possession()
		{
			if (Mod.Settings != null)
			{
				Mod.Settings.WorldPace = 2;
			}
			WorldPace.Apply(2);
			yield return CivilTest.Wait(0.5f);
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch (Exception ex)
			{
				Note("release: " + ex.Message);
			}
			yield return CivilTest.Wait(1.5f);
			try
			{
				ControllableUnit.setControllableCreature(_host);
			}
			catch (Exception ex2)
			{
				Note("possess: " + ex2.Message);
			}
			yield return CivilTest.Wait(2f);
			Check("possessing someone keeps 5x slower years", Config.time_scale_asset?.id == "fp_years_5", Config.time_scale_asset?.id);
			if (Mod.Settings != null)
			{
				Mod.Settings.WorldPace = 0;
			}
			WorldPace.Apply(0);
		}

		private IEnumerator Fire()
		{
			WorldPace.Apply(2);
			yield return CivilTest.Wait(0.5f);
			WorldTile start = null;
			Vector2 current_position = _host.current_position;
			for (int i = 14; i < 40; i += 3)
			{
				if (start != null)
				{
					break;
				}
				for (int j = 0; j < 16; j++)
				{
					if (start != null)
					{
						break;
					}
					float f = (float)j * MathF.PI / 8f;
					WorldTile tile = World.world.GetTile((int)(current_position.x + Mathf.Cos(f) * (float)i), (int)(current_position.y + Mathf.Sin(f) * (float)i));
					if (tile != null && tile.Type != null && tile.Type.burnable && !tile.isOnFire())
					{
						start = tile;
					}
				}
			}
			if (start == null)
			{
				Note("(nothing burnable found)");
				yield break;
			}
			try
			{
				start.startFire(pForce: true);
			}
			catch (Exception ex)
			{
				Note("fire: " + ex.Message);
			}
			yield return CivilTest.Wait(8f);
			int num = 0;
			int num2 = 0;
			for (int k = -10; k <= 10; k++)
			{
				for (int l = -10; l <= 10; l++)
				{
					WorldTile tile2 = World.world.GetTile(start.x + l, start.y + k);
					if (tile2 != null)
					{
						if (tile2.isOnFire())
						{
							num++;
						}
						if (tile2.burned_stages > 0)
						{
							num2++;
						}
					}
				}
			}
			Note($"8 s after a fire at 5x slower years: {num} tiles burning, {num2} burnt");
			Check("fire still spreads at 5x slower years", num + num2 > 1, $"{num} burning, {num2} burnt");
			WorldPace.Apply(0);
		}
	}
}
