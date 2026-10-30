using ai.behaviours;

namespace FirstPerson.Towns
{
	internal sealed class BehWallStand : BehaviourActionActor
	{
		public override BehResult execute(Actor a)
		{
			if (!WallPlans.JobOf(a, out var c, out var t, out var _))
			{
				return BehResult.Stop;
			}
			WorldTile worldTile = WallPlans.StandBy(c, t);
			if (worldTile == null)
			{
				return BehResult.Stop;
			}
			a.beh_tile_target = worldTile;
			return BehResult.Continue;
		}
	}
}
