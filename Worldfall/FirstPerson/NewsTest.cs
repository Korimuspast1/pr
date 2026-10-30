using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class NewsTest : CivilTest
	{
		private Actor _host;

		private Actor _kingA;

		private Actor _kingB;

		private City _a;

		private City _b;

		private Kingdom _ka;

		private Kingdom _kb;

		private City _won;

		private string _kaName;

		private int _savedMode;

		protected override string Prefix => "NEWS";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			_savedMode = Mod.Settings.NewsAlerts;
			Mod.Settings.NewsAlerts = 1;
			_kingA = Village("human", 10, 0);
			if (!Check("a first village", _kingA != null))
			{
				yield break;
			}
			_a = _kingA.city;
			_ka = _a.kingdom;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _kingA && item.isAdult())
				{
					_host = item;
					break;
				}
			}
			_kingB = Village("human", 10, 0, _kingA.current_tile, 30f);
			if (Check("a second village", _kingB != null))
			{
				_b = _kingB.city;
				_kb = _b.kingdom;
				Check("two kingdoms", _ka != null && _kb != null && _ka != _kb);
				_kaName = _ka?.name;
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(_host);
				if (Check("first person on a villager", InControl(_host)))
				{
					Check("the world log is followed", WorldNews.Installed);
					yield return CivilTest.Wait(1f);
					WorldNews.TakeStockForTest(_host);
					Check("their realm is known", WorldNews.Realm == _ka, WorldNews.Realm?.name);
					yield return Part("war declared", War());
					yield return Part("siege", Siege());
					yield return Part("capital taken", Taken());
					yield return Part("a city won", Won());
					yield return Part("the window", Window());
					yield return Part("a busy world", Busy());
					yield return Part("all news and none", Modes());
					Mod.Settings.NewsAlerts = _savedMode;
				}
			}
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.4f);
			H.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!H.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
		}

		private static NewsItem Last(string kind)
		{
			return WorldNews.Items.FindLast((NewsItem n) => n.Kind == kind);
		}

		private IEnumerator War()
		{
			WorldNews.GapOverForTest();
			int before = WorldNews.Items.Count;
			try
			{
				World.world.diplomacy.startWar(_kb, _ka, WarTypeLibrary.normal, pLog: true);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			yield return CivilTest.Wait(0.6f);
			NewsItem newsItem = Last("diplomacy_war_started");
			Check("the war comes live from the world log", newsItem != null && WorldNews.Items.Count > before, newsItem?.Text);
			Check("it is about your realm, and bad news", newsItem != null && newsItem.Mine && newsItem.Tone == NewsTone.Bad && newsItem.War);
			Check("a banner brings it up", WorldNews.Banner == newsItem || WorldNews.PendingAlerts > 0, WorldNews.Banner?.Text);
			Check("it counts as new, of your realm", WorldNews.Unread > 0 && WorldNews.UnreadMine > 0, WorldNews.Unread + "/" + WorldNews.UnreadMine);
			yield return Shot("news_banner.png");
			Rect newsBannerRectForTest = H.NewsBannerRectForTest;
			float num = Screen.width;
			float num2 = Screen.height;
			NewsTest newsTest = this;
			bool ok = newsBannerRectForTest.width > 0f && newsBannerRectForTest.height <= num2 * 0.07f && newsBannerRectForTest.width <= num * 0.52f && newsBannerRectForTest.yMax <= num2 * 0.3f;
			string[] array = new string[5];
			Rect rect = newsBannerRectForTest;
			array[0] = rect.ToString();
			array[1] = " on ";
			array[2] = num.ToString();
			array[3] = "x";
			array[4] = num2.ToString();
			newsTest.Check("the banner is one small strip under the compass, clear of the middle", ok, string.Concat(array));
		}

		private void Besiege(float ticks)
		{
			_a.addCapturePoints(_kb, 5);
			_a.being_captured_by = _kb;
			_a._capture_ticks = ticks;
			WorldNews.WatchNowForTest(_host);
		}

		private IEnumerator Siege()
		{
			yield return CivilTest.Wait(6f);
			Besiege(30f);
			WorldNews.Siege siege = WorldNews.Sieges.Find((WorldNews.Siege q) => q.City == _a);
			Check("your capital is under siege", siege != null && siege.Capital && siege.By == _kb, (siege != null) ? (siege.Name + " " + siege.Progress) : "none");
			Check("how far the taking has gone", siege != null && Mathf.Abs(siege.Progress - 0.3f) < 0.05f, siege?.Progress.ToString());
			NewsItem newsItem = Last("fp_siege");
			Check("a line says so, and a banner waits to", newsItem != null && newsItem.Mine && newsItem.Tone == NewsTone.Warn && newsItem.Text.Contains("under attack") && WorldNews.PendingAlerts > 0, newsItem?.Text);
			yield return CivilTest.Wait(0.3f);
			Check("...not within a minute of the war's banner (one strip a minute at most)", WorldNews.Banner == null && WorldNews.WaitingAlerts == 1, WorldNews.PendingForTest);
			List<KeyValuePair<float, Color>> list = new List<KeyValuePair<float, Color>>();
			WorldNews.AddMarks(list, _host.current_position + new Vector2(40f, 0f));
			Check("a flashing pin on the compass", list.Count == 1);
			int lines = WorldNews.Items.Count;
			Besiege(35f);
			Check("told once, not every look", WorldNews.Items.Count == lines);
			_a.clearCurrentCaptureAmounts();
			WorldNews.WatchNowForTest(_host);
			Check("a moment with no attacker counted is still the same siege", WorldNews.Sieges.Exists((WorldNews.Siege q) => q.City == _a) && WorldNews.Items.Count == lines, WorldNews.Sieges.Count + " sieges, " + (WorldNews.Items.Count - lines) + " new lines");
			WorldNews.GapOverForTest();
			yield return CivilTest.Wait(0.3f);
			Check("...and comes up once the minute is over", WorldNews.Banner != null && WorldNews.Banner.Kind == "fp_siege", WorldNews.PendingForTest);
			Mod.Feedback.Banner = "You killed someone (a test)";
			Mod.Feedback.BannerUntil = Time.unscaledTime + 3f;
			yield return Shot("news_siege.png");
			Besiege(75f);
			newsItem = Last("fp_siege");
			Check("told again when it's about to fall", newsItem != null && newsItem.Text.Contains("about to fall") && WorldNews.Items.Count == lines + 1, newsItem?.Text);
			Check("...in the same strip, not a second one", WorldNews.Banner != null && WorldNews.Banner.Late && WorldNews.WaitingAlerts == 0, WorldNews.PendingForTest);
			Besiege(70f);
			_a.clearCurrentCaptureAmounts();
			WorldNews.WatchNowForTest(_host);
			Besiege(80f);
			Check("...and not again as the fighting comes and goes", WorldNews.Items.Count == lines + 1, WorldNews.Items.Count - lines + " lines");
		}

		private IEnumerator Quiet()
		{
			for (float t = 0f; t < 25f; t += 0.25f)
			{
				if (WorldNews.PendingAlerts <= 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
		}

		private static Building MineNear(Vector2 c)
		{
			WorldTile worldTile = null;
			for (int i = 6; i < 60; i += 2)
			{
				if (worldTile != null)
				{
					break;
				}
				for (int j = 0; j < 32; j++)
				{
					if (worldTile != null)
					{
						break;
					}
					float f = (float)j / 32f * MathF.PI * 2f;
					int num = (int)(c.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(c.y + Mathf.Sin(f) * (float)i);
					bool flag = true;
					for (int k = -4; k <= 5 && flag; k++)
					{
						for (int l = -4; l <= 4 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(num + l, num2 + k);
							if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava || (tile.building != null && tile.building.isKingdomCiv()))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						worldTile = World.world.GetTile(num, num2);
					}
				}
			}
			if (worldTile == null)
			{
				return null;
			}
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int num3 = item.current_tile.x - worldTile.x;
				int num4 = item.current_tile.y - worldTile.y;
				if (num3 < -5 || num3 > 5 || num4 < -7 || num4 > 6)
				{
					continue;
				}
				try
				{
					if (!item.isKingdomCiv())
					{
						item.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
			Building building = null;
			try
			{
				building = World.world.buildings.addBuilding("mine", worldTile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				building?.completeConstruction();
			}
			catch
			{
			}
			return building;
		}

		private IEnumerator Taken()
		{
			yield return Quiet();
			Building mine = MineNear(_host.current_position);
			if (mine != null)
			{
				if (MineInterior.Mouth(mine, out var mouth))
				{
					Vector2 current_position = mouth + new Vector2(0f, -1.4f);
					WorldTile tile = World.world.GetTile((int)current_position.x, (int)current_position.y);
					if (tile != null)
					{
						_host.spawnOn(tile, 0f);
					}
					_host.current_position = current_position;
					_host.dirty_current_tile = true;
				}
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(2f);
				yield return CivilTest.Wait(1.5f);
				Mod.EnterMine(mine);
				yield return CivilTest.Wait(1f);
			}
			bool down = Mod.Mine.Inside;
			if (!down)
			{
				Note("no mine to go down (" + ((mine == null) ? "none built" : ("enterable " + MineInterior.IsEnterable(mine) + ", picked " + (Mod.Picked == mine))) + "): the capital falls with you outside");
			}
			Journal.Open = true;
			yield return CivilTest.Wait(0.3f);
			try
			{
				_a.joinAnotherKingdom(_kb, pCaptured: true);
			}
			catch (Exception ex)
			{
				Note("capture: " + ex.Message);
			}
			yield return CivilTest.Wait(1.2f);
			NewsItem n = Last("fp_taken");
			Check("the game itself has no line for it, the mod does" + (down ? " (told down the mine)" : ""), n != null && n.Mine && n.Tone == NewsTone.Bad, n?.Text);
			Check("while a window is open the banner waits", WorldNews.Banner == null && WorldNews.PendingAlerts > 0, WorldNews.Banner?.Text);
			WorldNews.GapOverForTest();
			yield return CivilTest.Wait(0.2f);
			Check("...the window holds it even then", WorldNews.Banner == null && WorldNews.PendingAlerts > 0, WorldNews.PendingForTest);
			Journal.Open = false;
			yield return CivilTest.Wait(0.3f);
			Check("...and comes up once it's closed", WorldNews.Banner != null && WorldNews.Banner.Kind == "fp_taken", WorldNews.Banner?.Text);
			Check("it says your capital fell, to whom", n != null && n.Text.Contains("capital") && n.Text.Contains(_kb.name), n?.Text);
			Check("and that you're theirs now", n != null && n.Text.Contains("so are you") && _host.kingdom == _kb, _host.kingdom?.name);
			Check("not said twice as a change of realm", Last("fp_joined") == null);
			Check("the siege is over", WorldNews.Sieges.Count == 0);
			Check("your realm now is the conqueror's", WorldNews.Realm == _kb, WorldNews.Realm?.name);
			if (down)
			{
				Mod.LeaveMineForTest();
				yield return CivilTest.Wait(0.5f);
			}
		}

		private IEnumerator Won()
		{
			Actor actor = Village("human", 6, 0, _kingA.current_tile, 30f);
			if (actor == null)
			{
				Note("no room for a third village: skipped");
				yield break;
			}
			City c = actor.city;
			string from = c.kingdom?.name;
			yield return Quiet();
			int pending = WorldNews.PendingAlerts;
			try
			{
				World.world.diplomacy.startWar(_kb, c.kingdom, WarTypeLibrary.normal, pLog: true);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			yield return CivilTest.Wait(0.3f);
			NewsItem newsItem = Last("diplomacy_war_started");
			Check("a war your realm declares is listed, without a banner (the council said so)", newsItem != null && newsItem.Mine && newsItem.Tone == NewsTone.Warn && WorldNews.PendingAlerts == pending, newsItem?.Text + " / " + WorldNews.PendingAlerts);
			WorldNews.WatchNowForTest(_host);
			try
			{
				c.joinAnotherKingdom(_kb, pCaptured: true);
			}
			catch (Exception ex2)
			{
				Note("capture: " + ex2.Message);
			}
			yield return CivilTest.Wait(0.3f);
			WorldNews.WatchNowForTest(_host);
			_won = c;
			NewsItem newsItem2 = Last("fp_won");
			Check("a city your realm takes is good news", newsItem2 != null && newsItem2.Tone == NewsTone.Good && newsItem2.Text.Contains(c.name) && (from == null || newsItem2.Text.Contains(from)), newsItem2?.Text);
			Check("...listed without a banner (a campaign or a soldier's orders say so)", !WorldNews.PendingHas("fp_won"), WorldNews.PendingForTest);
		}

		private IEnumerator Window()
		{
			int count = WorldNews.Items.Count;
			WorldNews.Toggle();
			Check("U opens the window", WorldNews.Open);
			Check("banners stop while it's open", WorldNews.PendingAlerts == 0);
			Check("the save's older lines are read in", WorldNews.Items.Count >= count, count + " -> " + WorldNews.Items.Count);
			NewsItem newsItem = WorldNews.Items.FindLast((NewsItem n) => n.Kind == "city_new" || n.Kind == "kingdom_new");
			Check("the villages' founding (before the test was in) read from the save, in the game's words", newsItem != null && newsItem.Text.Length > 10 && newsItem.Text.IndexOf('$') < 0, newsItem?.Text);
			HashSet<string> hashSet = new HashSet<string>();
			string text = null;
			foreach (NewsItem item in WorldNews.Items)
			{
				if (item.Key != null && !hashSet.Add(item.Key))
				{
					text = item.Key + " seq " + item.Seq;
				}
			}
			Check("no line twice (heard live and read from the save)", text == null, text);
			yield return Shot("news_window.png");
			NewsItem newsItem2 = WorldNews.Items.FindLast((NewsItem n) => n.Kind == "kingdom_destroyed" && n.Seq > 0 && _ka != null && n.Text.Contains(_kaName));
			if (newsItem2 != null)
			{
				Check("your old realm's end is news of yours (it came after you were taken in)", newsItem2.Mine, newsItem2.Text);
			}
			else
			{
				Note("no line of the old realm's end");
			}
			NewsItem newsItem3 = WorldNews.Items.FindLast((NewsItem n) => n.Kind == "diplomacy_war_ended" && n.Seq > 0);
			if (newsItem3 != null)
			{
				Check("the war's end is news of your realm (the line names only the war)", newsItem3.Mine, newsItem3.Text);
			}
			else
			{
				Note("no line of the war's end");
			}
			WorldNews.Filter = 1;
			List<NewsItem> list = WorldNews.Shown(_host.current_position, hasYou: true);
			Check("Your realm: only news of it", list.Count > 0 && list.TrueForAll((NewsItem n) => n.Mine), list.Count + " lines");
			yield return Shot("news_realm.png");
			WorldNews.Filter = 3;
			List<NewsItem> list2 = WorldNews.Shown(_host.current_position, hasYou: true);
			Check("Wars: the war, the siege, the conquests", list2.Exists((NewsItem n) => n.Kind == "diplomacy_war_started") && list2.Exists((NewsItem n) => n.Kind == "fp_siege") && list2.TrueForAll((NewsItem n) => n.War), list2.Count + " lines");
			WorldNews.Filter = 2;
			List<NewsItem> list3 = WorldNews.Shown(_host.current_position, hasYou: true);
			Vector2 at2;
			bool ok = list3.TrueForAll((NewsItem n) => n.Where(out at2) && (at2 - _host.current_position).magnitude <= 70f);
			Check("Near you: only what happened close by", ok, list3.Count + " lines");
			WorldNews.Filter = 0;
			NewsItem taken = Last("fp_taken");
			Ping ping = Mod.Pings.List.Find((Ping p) => p.Kind == PingKind.Go);
			bool flag = taken != null && H.MarkNewsForTest(taken);
			Ping ping2 = Mod.Pings.List.Find((Ping p) => p.Kind == PingKind.Go);
			Check("a click marks the place with a ping", flag && ping2 != null && ping2 != ping && taken.Where(out var at) && (new Vector2(ping2.At.x, ping2.At.y) - at).magnitude < 1f);
			H.MarkNewsForTest(taken);
			Check("again: the mark is taken off", !Mod.Pings.List.Exists((Ping p) => p.Kind == PingKind.Go && taken.Where(out at2) && (new Vector2(p.At.x, p.At.y) - at2).magnitude < 1f));
			Mod.CivilEscape();
			Check("Esc closes it", !WorldNews.Open);
			Check("what was new is read", WorldNews.Unread == 0 && WorldNews.UnreadMine == 0);
			yield return null;
		}

		private IEnumerator Busy()
		{
			Mod.Settings.NewsAlerts = 1;
			yield return Quiet();
			WorldNews.GapOverForTest();
			int lines = WorldNews.Items.Count;
			for (int i = 0; i < 8; i++)
			{
				WorldNews.AlertForTest("diplomacy_war_started", "Test war " + i + " declared on your realm", "test war " + i);
			}
			yield return CivilTest.Wait(0.3f);
			Check("a burst of wars on your realm: every one listed", WorldNews.Items.Count == lines + 8, WorldNews.Items.Count - lines + " lines");
			Check("...one strip up, at most two waiting", WorldNews.Banner != null && WorldNews.WaitingAlerts <= 2, WorldNews.PendingForTest);
			NewsItem up = WorldNews.Banner;
			int waitingAlerts = WorldNews.WaitingAlerts;
			if (up != null)
			{
				WorldNews.AlertForTest(up.Kind, up.Text + " (again)", up.About);
			}
			Check("the same war again while it's up: merged", WorldNews.Banner == up && WorldNews.WaitingAlerts == waitingAlerts, WorldNews.PendingForTest);
			WorldNews.BannerDoneForTest();
			yield return CivilTest.Wait(0.3f);
			Check("its time over, the next waits out the minute", WorldNews.Banner == null && WorldNews.WaitingAlerts > 0, WorldNews.PendingForTest);
			WorldNews.GapOverForTest();
			yield return CivilTest.Wait(0.3f);
			Check("...and comes up once it's over", WorldNews.Banner != null && WorldNews.Banner != up, WorldNews.PendingForTest);
			for (int j = 0; j < 3; j++)
			{
				WorldNews.BannerDoneForTest();
				WorldNews.GapOverForTest();
				yield return CivilTest.Wait(0.3f);
			}
			Check("the rest only listed (U), not shown one after another", WorldNews.PendingAlerts == 0, WorldNews.PendingForTest);
			if (up != null)
			{
				WorldNews.AlertForTest(up.Kind, up.Text + " (once more)", up.About);
			}
			Check("the same war again a moment later: not told twice", WorldNews.PendingAlerts == 0, WorldNews.PendingForTest);
		}

		private IEnumerator Modes()
		{
			Mod.Settings.NewsAlerts = 2;
			yield return Quiet();
			int lines = WorldNews.Items.Count;
			int pending = WorldNews.PendingAlerts;
			try
			{
				WorldLog.logNewCity(_b);
			}
			catch (Exception ex)
			{
				Note("log: " + ex.Message);
			}
			yield return CivilTest.Wait(0.3f);
			Check("Realm news: a new city of yours is listed (U), not put up on screen", WorldNews.Items.Count > lines && WorldNews.PendingAlerts == pending, WorldNews.Items.Count - lines + " lines, " + WorldNews.PendingForTest);
			yield return Shot("news_quiet.png");
			Kingdom realm = WorldNews.Realm;
			bool flag = false;
			try
			{
				flag = realm != null && realm.king != null && realm.king.isAlive();
			}
			catch
			{
			}
			if (flag)
			{
				Mod.Settings.NewsAlerts = 1;
				yield return CivilTest.Wait(0.2f);
				pending = WorldNews.PendingAlerts;
				try
				{
					WorldLog.logNewKing(realm);
				}
				catch (Exception ex2)
				{
					Note("log: " + ex2.Message);
				}
				yield return CivilTest.Wait(0.2f);
				Check("Attacks: your realm's new king only listed", WorldNews.PendingAlerts == pending, WorldNews.PendingForTest);
				Mod.Settings.NewsAlerts = 2;
				yield return CivilTest.Wait(0.2f);
				try
				{
					WorldLog.logNewKing(realm);
				}
				catch (Exception ex3)
				{
					Note("log: " + ex3.Message);
				}
				yield return CivilTest.Wait(0.2f);
				Check("Realm news: ...a banner for it too", WorldNews.PendingAlerts > pending, WorldNews.PendingForTest);
			}
			else
			{
				Note("your realm has no king: the crown's news skipped");
			}
			Mod.Settings.NewsAlerts = 0;
			yield return CivilTest.Wait(6f);
			lines = WorldNews.Items.Count;
			City city = ((_won != null && _won.isAlive() && _won.kingdom == _host.kingdom) ? _won : null);
			Kingdom kingdom = null;
			try
			{
				kingdom = ((_ka != null && _ka.isAlive()) ? _ka : (World.world.kingdoms_wild.get("bandits") ?? World.world.kingdoms_wild.get("possessed")));
			}
			catch
			{
			}
			if (city != null && kingdom != null)
			{
				city.addCapturePoints(kingdom, 5);
				city.being_captured_by = kingdom;
				city._capture_ticks = 30f;
				WorldNews.WatchNowForTest(_host);
				Check("Off: still listed", WorldNews.Items.Count > lines);
				Check("...but no banner", WorldNews.PendingAlerts == 0);
			}
			else
			{
				Note("no city of yours to besiege: skipped");
			}
		}
	}
}
