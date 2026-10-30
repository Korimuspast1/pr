using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FavPingTest : CivilTest
	{
		private Actor _host;

		private Actor _npc;

		protected override string Prefix => "FAVPING";

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 8, 0);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item != actor && item.isAdult())
				{
					if (_host == null)
					{
						_host = item;
					}
					else if (_npc == null)
					{
						_npc = item;
						break;
					}
				}
			}
			if (Check("two villagers", _host != null && _npc != null))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 300f, pColorEffect: false);
					((BaseSimObject)_npc).addStatusEffect("invincible", 300f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(_host);
				if (Check("first person on a villager", InControl(_host)))
				{
					yield return Part("star from a talk", StarInTalk());
					yield return Part("ping a favorite", PingFavourite());
				}
			}
		}

		private IEnumerator StarInTalk()
		{
			try
			{
				if (_npc.isFavorite())
				{
					_npc.switchFavorite();
				}
			}
			catch
			{
			}
			Mod.EndConversation();
			_host.current_position = _npc.current_position - new Vector2(1.3f, 0f);
			try
			{
				_npc.makeWait(6f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Check("a talk opens", Mod.StartConversationForTest(_npc) && Mod.Converse.Open);
			yield return CivilTest.Wait(0.8f);
			Capture("talk_unstarred");
			yield return CivilTest.Wait(0.4f);
			bool flag = Mod.StarCreature(Mod.Converse.With);
			Mod.Favs.Update(0f, force: true);
			Check("the star on the talk's card stars them", flag && Fav(_npc));
			Check("...and they are on the favorites", Mod.Favs.List.Contains(_npc), Mod.Favs.List.Count + " favorites");
			Check("the talk stays open", Mod.Converse.Open);
			yield return CivilTest.Wait(0.2f);
			Capture("talk_starred");
			yield return CivilTest.Wait(0.4f);
			bool flag2 = Mod.StarCreature(Mod.Converse.With);
			Mod.Favs.Update(0f, force: true);
			Check("again unstars them", !flag2 && !Fav(_npc) && !Mod.Favs.List.Contains(_npc));
			Mod.StarCreature(Mod.Converse.With);
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			Check("starred when the talk ends", Fav(_npc));
		}

		private IEnumerator PingFavourite()
		{
			float viewYaw = Mod.ViewYaw;
			Vector2 away = _host.current_position - new Vector2(Mathf.Cos(viewYaw), Mathf.Sin(viewYaw)) * 14f;
			_npc.current_position = away;
			try
			{
				_npc.makeWait(20f);
			}
			catch
			{
			}
			Mod.Pings.Clear();
			Mod.Craft.Open = true;
			Mod.Favs.Update(0f, force: true);
			yield return CivilTest.Wait(0.3f);
			bool flag = Mod.PingFavorite(_npc);
			Ping p = Mod.Pings.On(_npc);
			Check("the card's ping button pings them", flag && p != null && p.Kind == PingKind.Look, (p != null) ? p.Kind.ToString() : "none");
			Check("...a ping that follows them", p != null && p.Actor == _npc);
			Check("...for ten minutes", p != null && Mathf.Abs(p.Until - p.Born - 600f) < 1f, (p != null) ? ((p.Until - p.Born).ToString("F0") + " s") : "none");
			yield return CivilTest.Wait(0.3f);
			Capture("favs_pinged");
			yield return CivilTest.Wait(0.5f);
			Mod.Craft.Open = false;
			_npc.current_position = away + new Vector2(3f, 2f);
			yield return CivilTest.Wait(0.5f);
			Vector3 vector = ((p != null) ? Pings.Where(p) : Vector3.zero);
			Check("the ping moves with them", p != null && (new Vector2(vector.x, vector.y) - _npc.current_position).magnitude < 0.5f, $"{vector.x:F1},{vector.y:F1} vs {_npc.current_position.x:F1},{_npc.current_position.y:F1}");
			Hud hudForTest = Mod.HudForTest;
			Check("the ping shows (in view or at its edge)", hudForTest.PingsInView + hudForTest.PingsAtEdge >= 1, $"{hudForTest.PingsInView} in view, {hudForTest.PingsAtEdge} at the edge");
			Capture("world_pinged");
			yield return CivilTest.Wait(0.5f);
			bool flag2 = Mod.PingFavorite(_npc);
			Check("the button again takes it away", !flag2 && Mod.Pings.On(_npc) == null);
			Check("you can't ping yourself from the list", !Mod.PingFavorite(_host) && Mod.Pings.On(_host) == null);
		}

		private static bool Fav(Actor a)
		{
			try
			{
				return a.isFavorite();
			}
			catch
			{
				return false;
			}
		}

		private void Capture(string name)
		{
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "favping_" + name + "_hud.png"));
		}
	}
}
