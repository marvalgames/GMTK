using Unity.Entities;
#if RUKHANKA_WITH_NETCODE
using Unity.NetCode;
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
[CreateAfter(typeof(RukhankaAnimationSystemGroup))]
#if RUKHANKA_WITH_NETCODE
[CreateAfter(typeof(PredictedSimulationSystemGroup))]
#endif
public partial class RukhankaSystemsBootstrap: SystemBase
{
	protected override void OnCreate()
	{
	#if RUKHANKA_WITH_NETCODE
		bool isClient = World.IsClient();
		bool isServer = World.IsServer();
	#else
		bool isClient = IsClientOrLocalSimulationWorld(World);
	#endif

		//	Add client animator controller systems
		if (isClient)
		{
			var sysGroup = World.GetOrCreateSystemManaged<RukhankaAnimationSystemGroup>();
			var acs = World.CreateSystem<AnimatorControllerSystem<AnimatorControllerQuery>>();
			var facs = World.CreateSystem<FillAnimationsFromControllerSystem>();
			var baecb = World.CreateSystem<BeforeAnimationProcessCommandBufferSystem>();
			var aps = World.CreateSystem<AnimationsProcessSystem>();
			var bvs = World.CreateSystem<BoneVisualizationSystem>();
			sysGroup.AddSystemToUpdateList(acs);
			sysGroup.AddSystemToUpdateList(facs);
			sysGroup.AddSystemToUpdateList(baecb);
			sysGroup.AddSystemToUpdateList(aps);
			sysGroup.AddSystemToUpdateList(bvs);

		#if RUKHANKA_WITH_NETCODE
			var acsForPrediction = World.CreateSystem<AnimatorControllerSystem<PredictedAnimatorControllerQuery>>();
			var sysGroupPrediction = World.GetOrCreateSystemManaged<RukhankaPredictedAnimationSystemGroup>();
			sysGroupPrediction.AddSystemToUpdateList(acsForPrediction);
		#endif
		}

		//	Server systems only for Netcode enabled version
	#if RUKHANKA_WITH_NETCODE
		if (isServer)
		{
			var sysGroup = World.GetOrCreateSystemManaged<RukhankaAnimationSystemGroup>();
			var acs = World.CreateSystem<AnimatorControllerSystem<AnimatorControllerQuery>>();
			var facs = World.CreateSystem<FillAnimationsFromControllerSystem>();
			var baecb = World.CreateSystem<BeforeAnimationProcessCommandBufferSystem>();
			var aps = World.CreateSystem<AnimationsProcessSystem>();
			sysGroup.AddSystemToUpdateList(acs);
			sysGroup.AddSystemToUpdateList(facs);
			sysGroup.AddSystemToUpdateList(baecb);
			sysGroup.AddSystemToUpdateList(aps);

			var acsForPrediction = World.CreateSystem<AnimatorControllerSystem<PredictedAnimatorControllerQuery>>();
			var sysGroupPrediction = World.GetOrCreateSystemManaged<RukhankaPredictedAnimationSystemGroup>();
			sysGroupPrediction.AddSystemToUpdateList(acsForPrediction);
		}
	#endif

		//	Remove bootstrap system from world
		this.Enabled = false;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	public static bool IsClientOrLocalSimulationWorld(World w)
	{
		var rv =
			(w.Flags & WorldFlags.GameClient) == WorldFlags.GameClient ||
			(w.Flags & WorldFlags.Game) == WorldFlags.Game;
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	protected override void OnUpdate() {}
}
}
