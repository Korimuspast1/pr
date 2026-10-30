using System;
using System.Collections.Generic;

namespace FirstPerson
{
	internal static class TavernBoard
	{
		public sealed class Daily
		{
			public string Id;
			public string Title;
			public string Desc;
			public int RewardGold;
			public int RewardRep;
			public QuestKind Kind;
			public float ExpireAt;
		}

		private static readonly List<Daily> _daily = new List<Daily>();
		private static float _refreshAt;
		private static readonly System.Random _rng = new System.Random();

		static readonly string[] _templates = new string[]
		{
			"Clear wolves near {town}",
			"Deliver {n} wheat to {town}",
			"Escort caravan to {town}",
			"Hunt boar for the feast",
			"Collect herbs in the forest",
			"Patrol road to {town}"
		};

		public static IReadOnlyList<Daily> Dailies => _daily;

		public static void Tick(Actor host, float dt, Action<string> toast)
		{
			if (host == null || World.world == null) return;
			if (Time.unscaledTime >= _refreshAt || _daily.Count == 0)
			{
				Refresh(host);
				_refreshAt = Time.unscaledTime + 600f; // 10 min
			}
		}

		static void Refresh(Actor host)
		{
			_daily.Clear();
			int n = 3 + _rng.Next(3); // 3-5
			for (int i = 0; i < n; i++)
			{
				var t = _templates[_rng.Next(_templates.Length)];
				string town = host.city != null ? host.city.name : "Town";
				t = t.Replace("{town}", town).Replace("{n}", (5 + _rng.Next(15)).ToString());
				_daily.Add(new Daily
				{
					Id = Guid.NewGuid().ToString("N").Substring(0, 6),
					Title = t,
					Desc = "Posted by the tavern keeper. Speak to her to accept.",
					RewardGold = 10 + _rng.Next(40),
					RewardRep = 1 + _rng.Next(3),
					Kind = (QuestKind)(_rng.Next(5)),
					ExpireAt = Time.unscaledTime + 1200f
				});
			}
			Log.Info("tavern: refreshed " + _daily.Count + " dailies");
		}

		public static bool Accept(string id, Actor host, Action<string> toast)
		{
			var d = _daily.Find(x => x.Id == id);
			if (d == null || host == null) return false;
			try
			{
				// Simple reward without broken quest - give gold and rep directly
				try { host.addMoney(d.RewardGold); } catch {}
				try { host.changeHappiness(d.RewardRep); } catch {}
				Quests.Award(host, d.RewardGold, d.RewardRep, 0);
				toast?.Invoke($"Completed: {d.Title} (+{d.RewardGold} gold)");
				Log.Info($"tavern: {d.Title} completed for {WorldBoxMod.Describe(host)} +{d.RewardGold}g");
				_daily.Remove(d);
				return true;
			}
			catch (Exception ex) { Log.Error("tavern accept: " + ex.Message); return false; }
		}
	}
}
