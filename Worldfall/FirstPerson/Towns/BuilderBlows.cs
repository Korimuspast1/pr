using System;
using System.Reflection;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class BuilderBlows
	{
		private const int GameIlBytes = 52;

		private static bool _installed;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(BehBuildTarget).GetMethod("execute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Actor) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 52)
				{
					throw new InvalidOperationException("the game's building blow has changed (" + num + " bytes of IL, not " + 52 + ")");
				}
				Detour.Apply(method, typeof(BuilderBlows).GetMethod("Execute", BindingFlags.Static | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static BehResult Execute(BehBuildTarget self, Actor pActor)
		{
			Building beh_building_target = pActor.beh_building_target;
			if (!beh_building_target.isUnderConstruction())
			{
				return BehResult.Stop;
			}
			int constructionSpeed = pActor.getConstructionSpeed();
			bool flag;
			try
			{
				flag = (Village.PlansAlone(beh_building_target.city) ? Blueprints.BuilderBlow(beh_building_target, constructionSpeed) : beh_building_target.updateBuild(constructionSpeed));
			}
			catch (Exception e)
			{
				Hub.Fail("builder blow", e);
				flag = beh_building_target.updateBuild(constructionSpeed);
			}
			if (flag)
			{
				pActor.addLoot(SimGlobals.m.coins_for_building);
			}
			return BehResult.Continue;
		}
	}
}
