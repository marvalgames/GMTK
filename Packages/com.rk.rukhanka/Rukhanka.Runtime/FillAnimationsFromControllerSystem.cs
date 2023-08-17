using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{ 

[DisableAutoCreation]
[BurstCompile]
[RequireMatchingQueriesForUpdate]
public partial struct FillAnimationsFromControllerSystem: ISystem
{
	NativeParallelHashMap<Hash128, UnsafeHashMap<Hash128, int>> internalBoneMapsCache;
	EntityQuery fillAnimationsBufferQuery;

	BufferTypeHandle<AnimatorControllerLayerComponent> controllerLayersBufferHandle;
	BufferTypeHandle<AnimatorControllerParameterComponent> controllerParametersBufferHandle;
	EntityTypeHandle entityTypeHandle;

/////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnCreate(ref SystemState ss)
	{
		internalBoneMapsCache = new (100, Allocator.Persistent);

		var eqBuilder1 = new EntityQueryBuilder(Allocator.Temp)
		.WithAllRW<AnimatorControllerLayerComponent>();
		fillAnimationsBufferQuery = ss.GetEntityQuery(eqBuilder1);

		controllerLayersBufferHandle = ss.GetBufferTypeHandle<AnimatorControllerLayerComponent>();
		controllerParametersBufferHandle = ss.GetBufferTypeHandle<AnimatorControllerParameterComponent>();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnDestroy(ref SystemState ss)
	{
		if (internalBoneMapsCache.IsCreated)
		{
			foreach (var kv in internalBoneMapsCache)
			{
				kv.Value.Dispose();
			}
			internalBoneMapsCache.Dispose();
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnUpdate(ref SystemState ss)
	{
		var s = SystemAPI.GetSingleton<BeforeAnimationProcessCommandBufferSystem.Singleton>();
		var ecb = s.CreateCommandBuffer(ss.WorldUnmanaged);
		var ecbp = ecb.AsParallelWriter();

		controllerLayersBufferHandle.Update(ref ss);
		controllerParametersBufferHandle.Update(ref ss);
		entityTypeHandle.Update(ref ss);

		//	Update tables from previous frame
		var fillCacheJob = new CreateBoneRemapTablesJob()
		{
			internalBoneMapsCache = internalBoneMapsCache.AsParallelWriter()
		};
		//	IJobEntity codegen will take care of dependency management
		fillCacheJob.ScheduleParallel();

		var fillAnimationsBufferJob = new FillAnimationsBufferJob()
		{
			controllerLayersBufferHandle = controllerLayersBufferHandle,
			controllerParametersBufferHandle = controllerParametersBufferHandle,
			ecbp = ecbp,
			entityTypeHandle = entityTypeHandle,
			boneMapsCache = internalBoneMapsCache.AsReadOnly()
		};

		ss.Dependency = fillAnimationsBufferJob.ScheduleParallel(fillAnimationsBufferQuery, ss.Dependency);
	}
}
}
