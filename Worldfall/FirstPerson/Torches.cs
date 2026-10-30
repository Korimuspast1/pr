using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Torches
	{
		private const string LitKey = "fp_torch_lit";

		public static bool Out;

		public static bool MainLit;

		internal static readonly List<TorchFire> Others = new List<TorchFire>();

		private static readonly List<Actor> _units = new List<Actor>();

		private static readonly Dictionary<long, float> _lastLit = new Dictionary<long, float>();

		public static int Count(Actor a)
		{
			if (a != null)
			{
				return Supplies.Count(a, "tw_item_torch");
			}
			return 0;
		}

		public static bool InMain(Actor a)
		{
			if (a == null)
			{
				return false;
			}
			try
			{
				return Hotbar.Supply(a)?.Id == "tw_item_torch";
			}
			catch
			{
				return false;
			}
		}

		public static bool Lit(Actor a)
		{
			if (a == null)
			{
				return false;
			}
			int pResult = 0;
			try
			{
				a.data.get("fp_torch_lit", out pResult, 0);
			}
			catch
			{
			}
			return pResult != 0;
		}

		public static void SetLit(Actor a, bool lit)
		{
			if (a == null)
			{
				return;
			}
			try
			{
				a.data.set("fp_torch_lit", lit ? 1 : 0);
			}
			catch
			{
			}
		}

		public static bool Toggle(Actor a, Action<string> toast)
		{
			if (a == null)
			{
				return false;
			}
			if (!Out && (InMain(a) || Count(a) <= 0))
			{
				toast?.Invoke(InMain(a) ? "Your torch is in your hand" : "No torch. Make one from 1 wood (Tab)");
				return false;
			}
			Out = !Out;
			if (Out)
			{
				toast?.Invoke(Lit(a) ? "Torch taken up" : "Torch: unlit. Hold it into a fire to light it");
			}
			return true;
		}

		public static string Tip(Actor a)
		{
			return (Lit(a) ? "Lit. " : "Unlit. ") + "Light it at a fire. Water and rain put it out\nClick or hold Q: take it up";
		}

		public static bool FireAt(Vec3 flame, float reach, Func<float, float, float> ground)
		{
			try
			{
				WorldTile tile = World.world.GetTile((int)flame.X, (int)flame.Y);
				if (tile == null)
				{
					return false;
				}
				int num = Mathf.Clamp(Mathf.CeilToInt(reach), 1, 4);
				for (int i = -num; i <= num; i++)
				{
					for (int j = -num; j <= num; j++)
					{
						WorldTile tile2 = World.world.GetTile(tile.x + j, tile.y + i);
						if (tile2 != null)
						{
							float num2 = (float)tile2.x + 0.5f;
							float num3 = (float)tile2.y + 0.5f;
							float num4 = (flame.X - num2) * (flame.X - num2) + (flame.Y - num3) * (flame.Y - num3);
							float num5 = ((ground != null) ? (flame.Z - ground(num2, num3)) : 0f);
							if (num4 < (reach + 0.5f) * (reach + 0.5f) && num5 < 1.1f + reach && (tile2.isOnFire() || (tile2.Type != null && tile2.Type.lava)))
							{
								return true;
							}
							Building building = tile2.building;
							if (building != null && !(num4 > (reach + 0.7f) * (reach + 0.7f)) && (Burning(building) || (building.asset != null && building.asset.id != null && building.asset.id.Contains("bonfire") && num5 < 1.4f + reach)))
							{
								return true;
							}
						}
					}
				}
				_units.Clear();
				foreach (Actor item in Finder.getUnitsFromChunk(tile, 1))
				{
					if (item != null && item.isAlive() && Burning(item))
					{
						Vector2 current_position = item.current_position;
						if ((current_position.x - flame.X) * (current_position.x - flame.X) + (current_position.y - flame.Y) * (current_position.y - flame.Y) < (reach + 0.4f) * (reach + 0.4f))
						{
							return true;
						}
					}
				}
			}
			catch
			{
			}
			return NearFlames(flame, reach);
		}

		private static bool NearFlames(Vec3 flame, float reach)
		{
			if (PlacedLights.FireNear(flame, reach))
			{
				return true;
			}
			float unscaledTime = Time.unscaledTime;
			foreach (TorchFire other in Others)
			{
				if (other.Shown && other.Lit && !(unscaledTime - other.PosedAt > 0.15f))
				{
					float num = reach + 0.06f * other.Scale;
					if (Len2(other.Flame + new Vec3(0f, 0f, 0.08f * other.Scale) - flame) < num * num)
					{
						return true;
					}
				}
			}
			foreach (FlameSword item in People.FlamesDrawn)
			{
				if (item.Shown && !(unscaledTime - item.PosedAt > 0.15f) && Len2(Closest(item.Base, item.Point, flame) - flame) < (reach + 0.04f) * (reach + 0.04f))
				{
					return true;
				}
			}
			return false;
		}

		public static bool FireInRoom(IList<RoomLight> lights, Vec3 flame, float reach)
		{
			if (lights == null)
			{
				return false;
			}
			for (int i = 0; i < lights.Count; i++)
			{
				RoomLight roomLight = lights[i];
				Vec3 vec = new Vec3(roomLight.Light.X, roomLight.Light.Y, roomLight.Light.Z);
				float num = reach + (roomLight.Fire ? 0.35f : 0.2f);
				if (Len2(vec - flame) < num * num)
				{
					return true;
				}
			}
			return false;
		}

		public static bool FireInRoom(IList<PointLight> lights, Vec3 flame, float reach)
		{
			if (lights == null)
			{
				return false;
			}
			for (int i = 0; i < lights.Count; i++)
			{
				PointLight pointLight = lights[i];
				float num = reach + 0.2f;
				if (Len2(new Vec3(pointLight.X, pointLight.Y, pointLight.Z) - flame) < num * num)
				{
					return true;
				}
			}
			return false;
		}

		private static float Len2(Vec3 v)
		{
			return v.X * v.X + v.Y * v.Y + v.Z * v.Z;
		}

		private static Vec3 Closest(Vec3 a, Vec3 b, Vec3 p)
		{
			Vec3 vec = b - a;
			float num = Len2(vec);
			float num2 = ((num > 1E-06f) ? Mathf.Clamp01(Vec3.Dot(p - a, vec) / num) : 0f);
			return a + vec * num2;
		}

		public static bool Burning(BaseSimObject o)
		{
			try
			{
				return o != null && o.hasAnyStatusEffect() && o.hasStatus("burning");
			}
			catch
			{
				return false;
			}
		}

		public static void Struck(BaseSimObject target)
		{
			if (!MainLit || target == null)
			{
				return;
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = ((instance != null) ? instance.Body : null);
			if (actor == null)
			{
				return;
			}
			try
			{
				if (target.isActor())
				{
					ActionLibrary.addBurningEffectOnTarget(actor, target, target.current_tile);
				}
				else if (target.isBuilding())
				{
					Ignite(actor, target.b, instance.Toast);
				}
			}
			catch (Exception ex)
			{
				Log.Error("torch: struck: " + ex.Message);
			}
		}

		public static bool Ignite(Actor you, Building b, Action<string> toast)
		{
			if (b == null || you == null)
			{
				return false;
			}
			try
			{
				if (!b.isAlive() || Burning(b))
				{
					return false;
				}
				long key = ((b.data != null) ? b.data.id : b.GetHashCode());
				float unscaledTime = Time.unscaledTime;
				if (_lastLit.TryGetValue(key, out var value) && unscaledTime - value < 3f)
				{
					return false;
				}
				if (_lastLit.Count > 256)
				{
					_lastLit.Clear();
				}
				_lastLit[key] = unscaledTime;
				bool flag = WorldSync.ForwardIgnite(b, null);
				if (!flag)
				{
					flag = ActionLibrary.addBurningEffectOnTarget(you, b, b.current_tile);
				}
				if (!flag && b.current_tile != null)
				{
					flag = b.current_tile.startFire(pForce: false);
				}
				if (!flag)
				{
					return false;
				}
				Log.Info("torch: set " + b.asset?.id + " alight");
				if (b.asset != null && !b.asset.flora && b.kingdom != null && b.isKingdomCiv())
				{
					Law.ArsonSeen(b.kingdom, (b.current_tile != null) ? new Vector2(b.current_tile.x, b.current_tile.y) : you.current_position, toast);
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("torch: ignite: " + ex.Message);
				return false;
			}
		}

		public static bool IgniteGround(Vec3 flame)
		{
			try
			{
				WorldTile tile = World.world.GetTile((int)flame.X, (int)flame.Y);
				if (tile == null || tile.is_liquid || tile.isOnFire() || tile.building != null)
				{
					return false;
				}
				if (WorldSync.ForwardIgnite(null, tile))
				{
					return true;
				}
				return tile.startFire(pForce: false);
			}
			catch
			{
				return false;
			}
		}

		public static Building Against(Vec3 flame, BaseSimObject picked)
		{
			try
			{
				if (picked is Building building && building.isAlive() && building.current_tile != null)
				{
					float num = (float)building.current_tile.x + 0.5f - flame.X;
					float num2 = (float)building.current_tile.y + 0.5f - flame.Y;
					if (num * num + num2 * num2 < 2.5600002f)
					{
						return building;
					}
				}
				WorldTile tile = World.world.GetTile((int)flame.X, (int)flame.Y);
				if (tile == null)
				{
					return null;
				}
				Building result = null;
				float num3 = 0.5625f;
				for (int i = -1; i <= 1; i++)
				{
					for (int j = -1; j <= 1; j++)
					{
						WorldTile tile2 = World.world.GetTile(tile.x + j, tile.y + i);
						Building building2 = tile2?.building;
						if (building2 != null && building2.isAlive())
						{
							float num4 = (float)tile2.x + 0.5f - flame.X;
							float num5 = (float)tile2.y + 0.5f - flame.Y;
							float num6 = num4 * num4 + num5 * num5;
							if (num6 < num3)
							{
								num3 = num6;
								result = building2;
							}
						}
					}
				}
				return result;
			}
			catch
			{
				return null;
			}
		}
	}
}
