using ai.behaviours;

namespace FirstPerson.Towns
{
	internal sealed class BehRaiseWall : BehaviourActionActor
	{
		public override BehResult execute(Actor a)
		{
			WallPlans.Done(a);
			return BehResult.Continue;
		}
	}
}
