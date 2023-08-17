
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

[DisableAutoCreation]
[RequireMatchingQueriesForUpdate]
[BurstCompile]
public partial struct AnimationsProcessSystem: ISystem
{
	NativeList<BoneTransform> finalAllAnimatedBones;
	EntityQuery
		animatedObjectQuery,
		skinnedMeshWithAnimatorQuery,
		boneObjectEntitiesQuery,
		rootMotionApplyQuery,
		userCurvesObjectsQuery,
		rigDefinitionQuery;
	NativeParallelHashMap<Entity, int> entityToDataOffsetMap;
	NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> rigToSkinnedMeshRemapTables;
	NativeList<int> bonePosesOffsetsArr;
	NativeList<int2> boneToEntityArr;

	BufferTypeHandle<AnimationToProcessComponent> animationToProcessBufHandle;
	BufferTypeHandle<AnimatorControllerParameterComponent> animatorParameterBufHandleRW;
	BufferTypeHandle<SkinMatrix> skinMatrixBufHandleRW;

	ComponentTypeHandle<RigDefinitionComponent> rigDefinitionTypeHandle;
	ComponentTypeHandle<LocalTransform> localTransformTypeHandleRW;
	ComponentTypeHandle<LocalTransform> localTransformTypeHandle;
	ComponentTypeHandle<AnimatorEntityRefComponent> animatorEntityRefTypeHandle;
	ComponentTypeHandle<AnimatedSkinnedMeshComponent> animatedSkinnedMeshComponentTypeHandle;
	ComponentTypeHandle<AnimatorControllerParameterIndexTableComponent> animatorParameterIndexTableHandle;

	BufferLookup<AnimationToProcessComponent> animationToProcessBufferLookup;
	BufferLookup<RootMotionStateComponent> rootMotionStateBufLookupRW;
	ComponentLookup<RigDefinitionComponent> rigDefinitionComponentLookup;
	ComponentLookup<LocalTransform> localTransformComponentLookup;

	EntityTypeHandle entityTypeHandle;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnCreate(ref SystemState ss)
	{
	#if RUKHANKA_DEBUG_INFO
		Debug.LogWarning("RUKHANKA_DEBUG_INFO is defined. Performance may be reduced. Do not forget remove it in release builds.\nFor debug and logging functionality configuration please see documentation");
	#endif

		finalAllAnimatedBones = new NativeList<BoneTransform>(Allocator.Persistent);
		entityToDataOffsetMap = new NativeParallelHashMap<Entity, int>(128, Allocator.Persistent);
		rigToSkinnedMeshRemapTables = new NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>>(128, Allocator.Persistent);

		using var eqb0 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<RigDefinitionComponent, AnimationToProcessComponent>();
		animatedObjectQuery = ss.GetEntityQuery(eqb0);

		using var eqb1 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<SkinMatrix, AnimatedSkinnedMeshComponent>();
		skinnedMeshWithAnimatorQuery = ss.GetEntityQuery(eqb1);

		using var eqb2 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<AnimatorEntityRefComponent>()
		.WithAllRW<LocalTransform>();
		boneObjectEntitiesQuery = ss.GetEntityQuery(eqb2);

		using var eqb3 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<RootMotionStateComponent>()
		.WithAnyRW<LocalTransform>();
		rootMotionApplyQuery = ss.GetEntityQuery(eqb3);

		using var eqb4 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<AnimationToProcessComponent, AnimatorControllerParameterIndexTableComponent>()
		.WithAllRW<AnimatorControllerParameterComponent>();
		userCurvesObjectsQuery = ss.GetEntityQuery(eqb4);

		using var eqb5 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<RigDefinitionComponent>();
		rigDefinitionQuery = ss.GetEntityQuery(eqb5);

		animationToProcessBufHandle = ss.GetBufferTypeHandle<AnimationToProcessComponent>(true);
		animatorParameterBufHandleRW = ss.GetBufferTypeHandle<AnimatorControllerParameterComponent>();
		rigDefinitionTypeHandle = ss.GetComponentTypeHandle<RigDefinitionComponent>(true);
		rigDefinitionComponentLookup = ss.GetComponentLookup<RigDefinitionComponent>(true);
		entityTypeHandle = ss.GetEntityTypeHandle();
		localTransformTypeHandleRW = ss.GetComponentTypeHandle<LocalTransform>();
		localTransformTypeHandle = ss.GetComponentTypeHandle<LocalTransform>(true);
		rootMotionStateBufLookupRW = ss.GetBufferLookup<RootMotionStateComponent>();
		animatorEntityRefTypeHandle = ss.GetComponentTypeHandle<AnimatorEntityRefComponent>(true);
		animatorParameterIndexTableHandle = ss.GetComponentTypeHandle<AnimatorControllerParameterIndexTableComponent>();
		animationToProcessBufferLookup = ss.GetBufferLookup<AnimationToProcessComponent>(true);
		skinMatrixBufHandleRW = ss.GetBufferTypeHandle<SkinMatrix>();
		animatedSkinnedMeshComponentTypeHandle = ss.GetComponentTypeHandle<AnimatedSkinnedMeshComponent>(true);
		localTransformComponentLookup = ss.GetComponentLookup<LocalTransform>(true);

		bonePosesOffsetsArr = new NativeList<int>(Allocator.Persistent);
		boneToEntityArr = new NativeList<int2>(Allocator.Persistent);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnDestroy(ref SystemState ss)
	{
		if (finalAllAnimatedBones.IsCreated)
			finalAllAnimatedBones.Dispose();

		if (entityToDataOffsetMap.IsCreated)
			entityToDataOffsetMap.Dispose();

		if (rigToSkinnedMeshRemapTables.IsCreated)
			rigToSkinnedMeshRemapTables.Dispose();

		bonePosesOffsetsArr.Dispose();
		boneToEntityArr.Dispose();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle PrepareComputationData(NativeArray<int> chunkBaseEntityIndices, NativeList<Entity> entitesArr, JobHandle dependsOn)
	{
		//	Calculate bone offsets per entity
		var calcBoneOffsetsJob = new CalculateBoneOffsetsJob()
		{
			chunkBaseEntityIndices = chunkBaseEntityIndices,
			bonePosesOffsets = bonePosesOffsetsArr.AsArray(),
			rigDefinitionTypeHandle = rigDefinitionTypeHandle
		};

		var jh = calcBoneOffsetsJob.ScheduleParallel(animatedObjectQuery, dependsOn);

		//	Do prefix sum to calculate absolute offsets
		var prefixSumJob = new DoPrefixSumJob()
		{
			boneOffsets = bonePosesOffsetsArr.AsArray()
		};

		prefixSumJob.Schedule(jh).Complete();

		finalAllAnimatedBones.Resize(bonePosesOffsetsArr[^1], NativeArrayOptions.UninitializedMemory);
		boneToEntityArr.Resize(finalAllAnimatedBones.Length, NativeArrayOptions.UninitializedMemory);
		entityToDataOffsetMap.Capacity = math.max(finalAllAnimatedBones.Length, entityToDataOffsetMap.Capacity);

		//	Fill boneToEntityArr with proper values
		var boneToEntityArrFillJob = new CalculatePerBoneInfoJob()
		{
			bonePosesOffsets = bonePosesOffsetsArr.AsArray(),
			boneToEntityIndices = boneToEntityArr.AsArray(),
			chunkBaseEntityIndices = chunkBaseEntityIndices,
			rigDefinitionTypeHandle = rigDefinitionTypeHandle,
			entites = entitesArr.AsDeferredJobArray(),
			entityToDataOffsetMap = entityToDataOffsetMap.AsParallelWriter()
		};

		var boneToEntityJH = boneToEntityArrFillJob.ScheduleParallel(animatedObjectQuery, default);
	#if RUKHANKA_DEBUG_INFO
		//	Because BoneVisualizationSystem and AnimationProcessSystem has (as Entities says, don't get why) conflicts accessing entityToDataOffsetMap, we need complete job here
		boneToEntityJH.Complete();
	#endif
		return boneToEntityJH;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe JobHandle AnimationCalculation(ref SystemState ss, NativeList<Entity> entitiesArr, JobHandle dependsOn)
	{
		animationToProcessBufferLookup.Update(ref ss);
		rootMotionStateBufLookupRW.Update(ref ss);
		localTransformComponentLookup.Update(ref ss);

		var rigDefsArr = animatedObjectQuery.ToComponentDataListAsync<RigDefinitionComponent>(Allocator.TempJob, out var rigDefsLookupJH);
		var ecbs = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
		var ecb = ecbs.CreateCommandBuffer(ss.WorldUnmanaged);

		var dataGatherJH = JobHandle.CombineDependencies(rigDefsLookupJH, dependsOn);

		var computeAnimationsJob = new ComputeBoneAnimationJob()
		{
			animationsToProcessLookup = animationToProcessBufferLookup,
			boneToEntityIndices = boneToEntityArr,
			entityArr = entitiesArr.AsDeferredJobArray(),
			outBlendedBones = finalAllAnimatedBones,
			rigDefs = rigDefsArr.AsDeferredJobArray(),
			ecb = ecb.AsParallelWriter(),
			rootMotionStateBufLookup = rootMotionStateBufLookupRW,
			localTransformLookup = localTransformComponentLookup,
		};

		var jh = computeAnimationsJob.ScheduleBatch(finalAllAnimatedBones.Length, 16, dataGatherJH);
		rigDefsArr.Dispose(jh);

		return jh;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle ApplyRootMotion(ref SystemState ss, JobHandle dependsOn)
	{
		localTransformTypeHandle.Update(ref ss);
		rigDefinitionTypeHandle.Update(ref ss);
		entityTypeHandle.Update(ref ss);

		var applyRootMotionJob = new ApplyRootMotionJob()
		{
			boneTransforms = finalAllAnimatedBones,
			entityHandle = entityTypeHandle,
			entityToDataOffsetMap = entityToDataOffsetMap,
			ltHandle = localTransformTypeHandle,
			rigDefHandle = rigDefinitionTypeHandle,
		};

		var jh = applyRootMotionJob.ScheduleParallel(rootMotionApplyQuery, dependsOn);
		return jh;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle PropagateAnimatedBonesToEntitesTRS(ref SystemState ss, JobHandle dependsOn)
	{
		animatorEntityRefTypeHandle.Update(ref ss);
		localTransformTypeHandleRW.Update(ref ss);

		var propagateAnimationJob = new PropagateBoneTransformToEntityTRSJob()
		{
			animatorEntityRefHandle = animatorEntityRefTypeHandle,
			ltHandle = localTransformTypeHandleRW,
			entityToDataOffsetMap = entityToDataOffsetMap,
			boneTransforms = new NativeSlice<BoneTransform>(finalAllAnimatedBones.AsArray()),
		};

		var jh = propagateAnimationJob.ScheduleParallel(boneObjectEntitiesQuery, dependsOn);
		return jh;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void FillRigToSkinBonesRemapTableCache(ref SystemState ss)
	{
		rigDefinitionComponentLookup.Update(ref ss);

	#if RUKHANKA_DEBUG_INFO
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
	#endif

		var j = new FillRigToSkinBonesRemapTableCacheJob()
		{
			rigDefinitionArr = rigDefinitionComponentLookup,
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables,
			skinnedMeshes = skinnedMeshWithAnimatorQuery.ToComponentDataArray<AnimatedSkinnedMeshComponent>(Allocator.TempJob),
		#if RUKHANKA_DEBUG_INFO
			doLogging = dc.logAnimationCalculationProcesses
		#endif
		};

		j.Run();
		j.skinnedMeshes.Dispose();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle ApplySkinning(ref SystemState ss, JobHandle dependsOn)
	{
		animatedSkinnedMeshComponentTypeHandle.Update(ref ss);
		rigDefinitionComponentLookup.Update(ref ss);
		skinMatrixBufHandleRW.Update(ref ss);
		localTransformComponentLookup.Update(ref ss);

		var animationApplyJob = new ApplyAnimationToSkinnedMeshJob()
		{
			animatedSkinnedMeshHandle = animatedSkinnedMeshComponentTypeHandle,
			boneTransforms = finalAllAnimatedBones,
			entityToDataOffsetMap = entityToDataOffsetMap,
			rigDefinitionLookup = rigDefinitionComponentLookup,
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables,
			skinMatrixBufHandle = skinMatrixBufHandleRW,
			ltLookup = localTransformComponentLookup
		};

		var jh = animationApplyJob.ScheduleParallel(skinnedMeshWithAnimatorQuery, dependsOn);
		return jh;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle ProcessUserCurves(ref SystemState ss, JobHandle dependsOn)
	{
		animationToProcessBufHandle.Update(ref ss);
		animatorParameterBufHandleRW.Update(ref ss);
		animatorParameterIndexTableHandle.Update(ref ss);

		var userCurveProcessJob = new ProcessUserCurvesJob()
		{
			animationsToProcessBufHandle = animationToProcessBufHandle,
			animatorParametersBufHandle = animatorParameterBufHandleRW,
			parameterIndexTableHandle = animatorParameterIndexTableHandle,
		};

		var jh = userCurveProcessJob.ScheduleParallel(userCurvesObjectsQuery, dependsOn);
		return jh;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe public UnsafeList<BoneTransform> GetEntityBoneTransforms(Entity e)
	{
		if (!entityToDataOffsetMap.TryGetValue(e, out var index))
			return default;

		if (!rigDefinitionComponentLookup.TryGetComponent(e, out var rigDef))
			return default;

		var boneTransformsPtr = (BoneTransform*)finalAllAnimatedBones.GetUnsafeReadOnlyPtr();
		var rv = new UnsafeList<BoneTransform>(boneTransformsPtr + index, rigDef.rigBlob.Value.bones.Length);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnUpdate(ref SystemState ss)
	{
		entityToDataOffsetMap.Clear();

		var entityCount = animatedObjectQuery.CalculateEntityCount();
		if (entityCount == 0) return;

		bonePosesOffsetsArr.Resize(entityCount + 1, NativeArrayOptions.UninitializedMemory);
		var chunkBaseEntityIndices = animatedObjectQuery.CalculateBaseEntityIndexArrayAsync(Allocator.TempJob, ss.Dependency, out var baseIndexCalcJH);
		var entitiesArr = animatedObjectQuery.ToEntityListAsync(Allocator.TempJob, ss.Dependency, out var entityArrJH);

		var combinedJH = JobHandle.CombineDependencies(baseIndexCalcJH, entityArrJH, ss.Dependency);

		animationToProcessBufHandle.Update(ref ss);
		rigDefinitionTypeHandle.Update(ref ss);
		
		FillRigToSkinBonesRemapTableCache(ref ss);

		//	Define array with bone pose offsets for calculated bone poses
		var calcBoneOffsetsJH = PrepareComputationData(chunkBaseEntityIndices, entitiesArr, combinedJH);
		
		//	Spawn jobs for animation calculation
		var computeAnimationJobHandle = AnimationCalculation(ref ss, entitiesArr, calcBoneOffsetsJH);

		//	User curve calculus
		var userCurveProcessJobHandle = ProcessUserCurves(ref ss, computeAnimationJobHandle);

		//	Apply root motion changes
		var applyRootMotionJobHandle = ApplyRootMotion(ref ss, computeAnimationJobHandle);

		combinedJH = JobHandle.CombineDependencies(applyRootMotionJobHandle, userCurveProcessJobHandle);

		//	After all animations were calculated for all bones, write result transforms to the corresponding Entities translation and rotation components
		var propagateTRSJobHandle = PropagateAnimatedBonesToEntitesTRS(ref ss, combinedJH);

		//	Make corresponding skin matrices for all skinned meshes
		var applySkinJobHandle = ApplySkinning(ref ss, propagateTRSJobHandle);

		entitiesArr.Dispose(computeAnimationJobHandle);

		ss.Dependency = applySkinJobHandle;
	}
}
}
