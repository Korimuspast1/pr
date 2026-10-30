using System;
using System.Reflection;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Taxes
	{
		public const string LocalKey = "fp_tax_local";

		public const string TributeKey = "fp_tax_tribute";

		public static readonly string[] StepNames = new string[5] { "None", "Low", "Normal", "High", "Harsh" };

		private static readonly float[] Rates = new float[5] { 0f, 0.2f, 0.5f, 0.7f, 0.9f };

		private static readonly int[] Mood = new int[5] { 0, -2, -5, -8, -14 };

		public const int NormalStep = 2;

		private const int GameIlBytes = 63;

		private static float _apply;

		private static bool _hooked;

		private static bool _installed;

		private static int _payers;

		private static int _towns;

		private static long _countedFor;

		public static int StepOf(Kingdom k, bool tribute)
		{
			if (k == null)
			{
				return 2;
			}
			try
			{
				k.data.get(tribute ? "fp_tax_tribute" : "fp_tax_local", out var pResult, -1);
				return (pResult < 0) ? 2 : Mathf.Clamp(pResult, 0, 4);
			}
			catch
			{
				return 2;
			}
		}

		public static bool IsSet(Kingdom k, bool tribute)
		{
			try
			{
				k.data.get(tribute ? "fp_tax_tribute" : "fp_tax_local", out var pResult, -1);
				return pResult >= 0;
			}
			catch
			{
				return false;
			}
		}

		public static void SetStep(Kingdom k, bool tribute, int step, Action<string> toast)
		{
			if (k != null)
			{
				step = Mathf.Clamp(step, 0, 4);
				try
				{
					k.data.set(tribute ? "fp_tax_tribute" : "fp_tax_local", step);
				}
				catch (Exception e)
				{
					Hub.Fail("tax step", e);
					return;
				}
				Apply(k);
				Log.Info(string.Format("taxes: {0} {1} rate {2} ({3:0.0})", Royal.NameOf(k), tribute ? "tribute" : "local", StepNames[step], Rates[step]));
				toast?.Invoke((tribute ? "Towns' tribute: " : "Taxes: ") + StepNames[step] + " (" + Mathf.RoundToInt(Rates[step] * 100f) + "%)");
			}
		}

		private static void Apply(Kingdom k)
		{
			try
			{
				if (IsSet(k, tribute: false))
				{
					k._cached_tax_local = Rates[StepOf(k, tribute: false)];
				}
				if (IsSet(k, tribute: true))
				{
					k._cached_tax_tribute = Rates[StepOf(k, tribute: true)];
				}
			}
			catch (Exception e)
			{
				Hub.Fail("tax apply", e);
			}
		}

		public static float RateOf(Kingdom k, bool tribute)
		{
			try
			{
				return tribute ? k.getTaxRateTribute() : k.getTaxRateLocal();
			}
			catch
			{
				return 0.5f;
			}
		}

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(Actor).GetMethod("paidTax", BindingFlags.Instance | BindingFlags.Public, null, new Type[2]
				{
					typeof(float),
					typeof(string)
				}, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 63)
				{
					throw new InvalidOperationException("the game's tax payment has changed (" + num + " bytes of IL, not " + 63 + ")");
				}
				Detour.Apply(method, typeof(Taxes).GetMethod("PaidTax", BindingFlags.Static | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static void PaidTax(Actor self, float pTaxRate, string pEffect)
		{
			self.lootEmpty();
			try
			{
				EffectsLibrary.showMoneyEffect(pEffect, self.current_position, self.current_zone, self.actor_scale);
			}
			catch
			{
			}
			int num = ((pTaxRate > 0.7f) ? (-10) : (-5));
			try
			{
				Kingdom kingdom = self.kingdom;
				bool tribute = pEffect == "fx_money_paid_tribute";
				if (kingdom != null && IsSet(kingdom, tribute))
				{
					num = Mood[StepOf(kingdom, tribute)];
				}
				Count(self, tribute);
			}
			catch
			{
			}
			if (num != 0)
			{
				self.changeHappiness("paid_tax", num);
			}
		}

		private static void Count(Actor payer, bool tribute)
		{
			Actor actor = WorldBoxMod.Instance?.Host;
			if (actor != null && payer != actor && (tribute ? (payer.kingdom?.king == actor) : (payer.city?.leader == actor)))
			{
				if (_countedFor != actor.data.id)
				{
					_countedFor = actor.data.id;
					_payers = (_towns = 0);
				}
				if (tribute)
				{
					_towns++;
				}
				else
				{
					_payers++;
				}
			}
		}

		public static void Tick(float dt)
		{
			if (!_hooked)
			{
				_hooked = true;
				Calendar.Dawn += OnDawn;
			}
			if ((_apply -= dt) > 0f)
			{
				return;
			}
			_apply = 0.5f;
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item != null && item.isAlive() && (IsSet(item, tribute: false) || IsSet(item, tribute: true)))
					{
						Apply(item);
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("tax rates", e);
			}
		}

		private static void OnDawn(int day)
		{
			if (WorldSync.Mirroring)
			{
				return;
			}
			try
			{
				Calendar.SeasonOf(day, out var dayIn, out var _);
				if (dayIn == 0)
				{
					Payday(WorldBoxMod.Instance?.Host, Calendar.Season, Hub.Toast);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("payday", e);
			}
		}

		public static int Due(Actor you)
		{
			try
			{
				return (you != null && you.isAlive()) ? you.loot : 0;
			}
			catch
			{
				return 0;
			}
		}

		public static int Payday(Actor you, Season season, Action<string> toast)
		{
			if (you == null || !you.isAlive())
			{
				return 0;
			}
			int num = Due(you);
			int num2 = ((_countedFor == you.data.id) ? _payers : 0);
			int num3 = ((_countedFor == you.data.id) ? _towns : 0);
			_payers = (_towns = 0);
			if (num <= 0)
			{
				return 0;
			}
			City city = you.city;
			Kingdom kingdom = you.kingdom;
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = you.isKing();
				flag2 = !flag && you.isCityLeader();
			}
			catch
			{
			}
			int num4 = 0;
			int num5 = 0;
			int num7;
			try
			{
				you.giveAllLoot();
				Actor actor = ((flag2 && kingdom != null && kingdom.hasKing()) ? kingdom.king : null);
				if (actor != null && actor != you)
				{
					num4 = (int)((float)num * kingdom.getTaxRateTribute());
					int num6 = num - num4;
					num5 = (Economy.HasStores(city) ? ((int)((float)num6 * 0.5f)) : 0);
					num7 = num6 - num5;
					actor.addLoot(num4);
					if (num5 > 0)
					{
						Growth.Ignore(city, "gold", num5);
						city.addResourcesToRandomStockpile("gold", num5);
					}
				}
				else
				{
					num7 = num;
				}
				you.addMoney(num7);
			}
			catch (Exception e)
			{
				Hub.Fail("payday", e);
				return 0;
			}
			string text = ((flag || flag2) ? ($"{season} taxes: {num}" + ((num2 > 0) ? $" from {num2} payments" : "") + ((num3 > 0) ? $", {num3} from towns' tribute" : "")) : $"{season}: your earnings {num}");
			string text2 = ((num4 > 0) ? $". The king's share {num4}, the town {num5}." : ".");
			toast?.Invoke(text + text2 + $" Your purse +{num7}.");
			Log.Info($"taxes: payday ({season}) for {WorldBoxMod.Describe(you)}: loot {num}, king {num4}, town {num5}, purse {num7} (payments {num2}, tribute {num3})");
			return num7;
		}
	}
}
