using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class Favorites
	{
		public readonly List<Actor> List = new List<Actor>();

		public Vector2 Scroll;

		public static int Tab = 1;

		private float _refresh;

		public void Update(float dt, bool force)
		{
			_refresh -= dt;
			if (_refresh > 0f && !force)
			{
				return;
			}
			_refresh = 0.6f;
			List.Clear();
			try
			{
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					Actor actor = simpleList[i];
					if (actor != null && actor.isAlive())
					{
						bool flag;
						try
						{
							flag = actor.isFavorite();
						}
						catch
						{
							flag = false;
						}
						if (flag)
						{
							List.Add(actor);
						}
						if (List.Count >= 60)
						{
							break;
						}
					}
				}
				List.Sort(delegate(Actor a, Actor b)
				{
					int num = string.CompareOrdinal(KingdomName(a), KingdomName(b));
					return (num == 0) ? string.CompareOrdinal(NameOf(a), NameOf(b)) : num;
				});
			}
			catch (Exception ex)
			{
				Log.Error("favorites: " + ex.Message);
				_refresh = 10f;
			}
		}

		public bool Toggle(Actor a)
		{
			if (a == null)
			{
				return false;
			}
			try
			{
				if (a.asset != null && !a.asset.can_be_favorited)
				{
					return a.isFavorite();
				}
				a.switchFavorite();
				_refresh = 0f;
				return a.isFavorite();
			}
			catch
			{
				return false;
			}
		}

		public Actor Step(Actor current, int dir)
		{
			if (List.Count == 0)
			{
				return null;
			}
			int num = List.IndexOf(current);
			for (int i = 0; i < List.Count; i++)
			{
				num = ((num >= 0) ? ((num + dir + List.Count) % List.Count) : ((dir <= 0) ? (List.Count - 1) : 0));
				Actor actor = List[num];
				if (actor != current && Possess.CanJumpTo(actor))
				{
					return actor;
				}
			}
			return null;
		}

		internal static string NameOf(Actor a)
		{
			try
			{
				return a.getName() ?? "";
			}
			catch
			{
				return "";
			}
		}

		private static string KingdomName(Actor a)
		{
			try
			{
				return (a.kingdom != null) ? (a.kingdom.name ?? "") : "~";
			}
			catch
			{
				return "~";
			}
		}

		internal static string Doing(Actor a)
		{
			try
			{
				if (a.isFighting())
				{
					if (a.attack_target is Actor actor && actor.isAlive())
					{
						return "fighting " + WorldBoxMod.Describe(actor);
					}
					return "fighting";
				}
				if (a.is_inside_building)
				{
					return "indoors";
				}
				if (a.is_inside_boat)
				{
					return "at sea";
				}
				if (a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null)
				{
					string text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id ?? "";
					switch (text)
					{
					case "fp_royal_march":
						return "marching";
					case "fp_royal_hold":
						return "holding a flag";
					case "fp_royal_board":
						return "going aboard a ship";
					case "fp_royal_sail":
						return WarMap.ShipDoing(a) ?? "sailing under the king's orders";
					default:
						return text.Replace("_", " ");
					}
				}
				if (a._is_moving)
				{
					return "walking";
				}
			}
			catch
			{
			}
			return "idle";
		}
	}
}
