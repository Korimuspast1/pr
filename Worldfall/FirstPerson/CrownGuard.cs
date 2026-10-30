using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ai;
using ai.behaviours;

namespace FirstPerson
{
	internal static class CrownGuard
	{
		private const int KingIl = 302;

		private const int LeaderIl = 16;

		private static bool _installed;

		internal static int Asked;

		internal static int KeptHome;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(KingdomBehCheckKing).GetMethod("execute", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Kingdom) }, null);
				MethodInfo method2 = typeof(CityBehCheckLeader).GetMethod("execute", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(City) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				int num2 = method2?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 302 || num2 != 16)
				{
					throw new InvalidOperationException($"the game's choice of kings and leaders has changed ({num} and {num2} bytes of IL, not {302} and {16})");
				}
				Detour.Apply(method, typeof(CrownGuard).GetMethod("KingExecute", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(method2, typeof(CrownGuard).GetMethod("LeaderExecute", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static Actor You(Kingdom k)
		{
			try
			{
				Actor actor = ControllableUnit._unit_main;
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (instance != null && instance.Body != null && instance.Body.isAlive())
				{
					actor = instance.Body;
				}
				return (actor != null && actor.isAlive() && k != null && actor.kingdom == k) ? actor : null;
			}
			catch
			{
				return null;
			}
		}

		private static BehResult KingExecute(KingdomBehCheckKing self, Kingdom k)
		{
			if (k.data.timer_new_king > 0f)
			{
				return BehResult.Continue;
			}
			if (k.hasKing())
			{
				Actor king = k.king;
				if (king.isAlive())
				{
					self.tryToGiveGoldenTooth(king);
					self.checkClanCreation(king);
					CrownOffer.Settled(k, null);
					return BehResult.Continue;
				}
			}
			Actor actor = You(k);
			if (actor != null && CrownOffer.Waiting(k, null))
			{
				return BehResult.Continue;
			}
			k.clearKingData();
			if (k.data.royal_clan_id != -1)
			{
				Clan clan = World.world.clans.get(k.data.royal_clan_id);
				bool flag = !clan.isRekt();
				bool asked;
				Actor actor2 = FindKing(self, k, actor, out asked);
				if (asked)
				{
					return BehResult.Continue;
				}
				if (actor2 == null)
				{
					if (k.countCities() == 1)
					{
						if (SingleTown(k, actor))
						{
							return BehResult.Continue;
						}
					}
					else if (Chaos(self, k, actor))
					{
						return BehResult.Continue;
					}
				}
				else if (k.hasCulture() && k.culture.hasTrait("shattered_crown") && flag && (actor == null || actor.clan != clan))
				{
					self.checkShatteredCrownEvent(k, actor2, clan);
				}
				if (!flag)
				{
					k.data.royal_clan_id = -1L;
				}
			}
			else
			{
				Actor actor3 = SuccessionTool.getKingFromLeaders(k);
				if (actor3 != null && actor3 == actor)
				{
					if (!CrownOffer.Declined(k, null, actor))
					{
						Ask(k, actor, NextBut(k, actor));
						return BehResult.Continue;
					}
					actor3 = NextBut(k, actor);
				}
				if (actor3 != null)
				{
					self.makeKingAndMoveToCapital(k, actor3);
				}
				else if (actor != null && k.countCities() == 1 && k.capital != null && k.capital.leader == actor)
				{
					SingleTown(k, actor);
				}
				else
				{
					Chaos(self, k, actor);
				}
			}
			return BehResult.Continue;
		}

		private static Actor FindKing(KingdomBehCheckKing self, Kingdom k, Actor you, out bool asked)
		{
			asked = false;
			bool flag = k.hasCulture() && (k.culture.hasTrait("unbroken_chain") || !self.isRebellionsEnabled());
			Actor actor = SuccessionTool.getKingFromRoyalClan(k);
			if (actor == null && flag)
			{
				actor = SuccessionTool.getKingFromLeaders(k);
			}
			if (actor != null && actor == you)
			{
				Actor actor2 = SuccessionTool.getKingFromRoyalClan(k, you);
				if (actor2 == null && flag)
				{
					actor2 = LeadersBut(k, you);
				}
				if (!CrownOffer.Declined(k, null, you))
				{
					Ask(k, you, actor2);
					asked = true;
					return null;
				}
				actor = actor2;
			}
			if (actor == null)
			{
				return null;
			}
			self.makeKingAndMoveToCapital(k, actor);
			return actor;
		}

		private static bool SingleTown(Kingdom k, Actor you)
		{
			City capital = k.capital;
			if (capital == null || !capital.hasLeader())
			{
				return false;
			}
			Actor leader = capital.leader;
			if (leader != null && leader == you)
			{
				Actor actor = TownBut(capital, you);
				if (!CrownOffer.Declined(k, null, you))
				{
					Ask(k, you, actor);
				}
				else if (actor != null)
				{
					CrownOther(k, actor);
				}
				return true;
			}
			capital.removeLeader();
			leader.stopBeingWarrior();
			k.setKing(leader);
			return false;
		}

		private static bool Chaos(KingdomBehCheckKing self, Kingdom k, Actor you)
		{
			City city = ((you != null && you.isCityLeader()) ? you.city : null);
			if (city == null || city.kingdom != k)
			{
				self.checkKingdomChaos(k);
				return false;
			}
			if (!CrownOffer.Declined(k, null, you))
			{
				Ask(k, you, null);
				return true;
			}
			if (k.countCities() == 0)
			{
				return false;
			}
			List<City> list = new List<City>();
			foreach (City city2 in k.getCities())
			{
				if (city2 != city && city2 != k.capital && city2.hasLeader())
				{
					list.Add(city2);
				}
			}
			if (list.Count == 0)
			{
				return false;
			}
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = UnityEngine.Random.Range(0, num + 1);
				City value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			bool flag = false;
			foreach (City item in list)
			{
				if (item.leader != null && item.leader.isAlive())
				{
					item.makeOwnKingdom(item.leader, pRebellion: false, pFellApart: true);
					flag = true;
				}
			}
			if (!flag)
			{
				return false;
			}
			try
			{
				if (k.hasAlliance())
				{
					k.getAlliance().leave(k);
				}
			}
			catch
			{
			}
			try
			{
				WorldLog.logFracturedKingdom(k);
			}
			catch
			{
			}
			return false;
		}

		private static Actor NextBut(Kingdom k, Actor you)
		{
			Actor actor = LeadersBut(k, you);
			if (actor == null && k.countCities() == 1 && k.capital != null)
			{
				actor = TownBut(k.capital, you);
			}
			return actor;
		}

		private static Actor TownBut(City c, Actor you)
		{
			try
			{
				return ClanLeader(c, you) ?? Dice(c, you);
			}
			catch
			{
				return null;
			}
		}

		private static void CrownOther(Kingdom k, Actor a)
		{
			try
			{
				a.stopBeingWarrior();
				new KingdomBehCheckKing().makeKingAndMoveToCapital(k, a);
				Log.Info("crown: declined; crowned " + WorldBoxMod.Describe(a));
			}
			catch (Exception ex)
			{
				Log.Error("crown: crowning the next: " + ex.Message);
			}
		}

		private static Actor LeadersBut(Kingdom k, Actor you)
		{
			Actor actor = null;
			int num = int.MinValue;
			foreach (City city in k.getCities())
			{
				if (city.hasLeader() && city.leader != null && city.leader != you && city.leader.isAlive())
				{
					int num2 = ActorTool.attributeDice(city.leader);
					if (actor == null || num2 > num)
					{
						actor = city.leader;
						num = num2;
					}
				}
			}
			return actor;
		}

		private static void Ask(Kingdom k, Actor you, Actor alt)
		{
			Asked++;
			k.data.timer_new_king = 0f;
			CrownOffer.Ask(k, null, you, alt);
		}

		internal static void Crown(Kingdom k, Actor you)
		{
			new KingdomBehCheckKing().makeKingAndMoveToCapital(k, you);
		}

		private static BehResult LeaderExecute(CityBehCheckLeader self, City c)
		{
			self.checkLeaderClan(c);
			if (c.hasLeader() || c.isGettingCaptured())
			{
				CrownOffer.Settled(c.kingdom, c);
				return BehResult.Continue;
			}
			Kingdom kingdom = c.kingdom;
			Actor actor = You(kingdom);
			if (actor == null)
			{
				FindLeader(self, c, null);
				return BehResult.Continue;
			}
			if (CrownOffer.Waiting(kingdom, c))
			{
				return BehResult.Continue;
			}
			FindLeader(self, c, actor);
			return BehResult.Continue;
		}

		private static void FindLeader(CityBehCheckLeader self, City c, Actor you)
		{
			Actor actor = ((you == null) ? self.tryGetClanLeader(c) : ClanLeader(c, null));
			if (actor != null && actor == you)
			{
				if (you.city != c)
				{
					KeptHome++;
					actor = ClanLeader(c, you);
				}
				else
				{
					if (!CrownOffer.Declined(c.kingdom, c, you))
					{
						Asked++;
						CrownOffer.Ask(c.kingdom, c, you, ClanLeader(c, you) ?? Dice(c, you));
						return;
					}
					actor = ClanLeader(c, you);
				}
			}
			if (actor != null)
			{
				if (actor.city != c)
				{
					actor.stopBeingWarrior();
				}
				actor.joinCity(c);
				c.setLeader(actor, pNew: true);
				return;
			}
			actor = Dice(c, null);
			if (actor != null && actor == you)
			{
				if (!CrownOffer.Declined(c.kingdom, c, you))
				{
					Asked++;
					CrownOffer.Ask(c.kingdom, c, you, Dice(c, you));
					return;
				}
				actor = Dice(c, you);
			}
			if (actor != null)
			{
				c.setLeader(actor, pNew: true);
			}
		}

		private static Actor ClanLeader(City c, Actor skip)
		{
			Kingdom kingdom = c.kingdom;
			Clan clan = null;
			if (kingdom.data.royal_clan_id != -1)
			{
				clan = World.world.clans.get(kingdom.data.royal_clan_id);
			}
			using (ListPool<Actor> listPool = new ListPool<Actor>())
			{
				using (ListPool<Actor> listPool2 = new ListPool<Actor>())
				{
					foreach (City city in kingdom.getCities())
					{
						foreach (Actor unit in city.getUnits())
						{
							if (unit != skip && unit.isUnitFitToRule() && !unit.isKing() && !unit.isCityLeader() && unit.hasClan())
							{
								if (clan != null && unit.clan == clan)
								{
									listPool.Add(unit);
								}
								else
								{
									listPool2.Add(unit);
								}
							}
						}
					}
					ListPool<Actor> listPool3 = ((listPool.Count > 0) ? listPool : ((listPool2.Count > 0) ? listPool2 : null));
					if (listPool3 == null)
					{
						return null;
					}
					if (c.hasCulture())
					{
						return ListSorters.getUnitSortedByAgeAndTraits(listPool3, c.culture);
					}
					listPool3.Sort(ListSorters.sortUnitByAgeOldFirst);
					return listPool3[0];
				}
			}
		}

		private static Actor Dice(City c, Actor skip)
		{
			Actor actor = null;
			int num = 0;
			foreach (Actor unit in c.units)
			{
				if (unit != skip && !unit.isKing() && !unit.isCityLeader() && unit.is_profession_citizen)
				{
					int num2 = ActorTool.attributeDice(unit, (!unit.isFavorite()) ? 1 : 3);
					if (actor == null || num2 > num)
					{
						actor = unit;
						num = num2;
					}
				}
			}
			return actor;
		}
	}
}
