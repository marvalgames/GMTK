using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using static Rukhanka.AnimatorControllerSystemJobs;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{ 
public partial struct FillAnimationsFromControllerSystem
{

[BurstCompile]
partial struct FillAnimationsBufferJob: IJobChunk
{
	public BufferTypeHandle<AnimatorControllerLayerComponent> controllerLayersBufferHandle;
	public BufferTypeHandle<AnimatorControllerParameterComponent> controllerParametersBufferHandle;
	public EntityCommandBuffer.ParallelWriter ecbp;
	[ReadOnly]
	public NativeParallelHashMap<Hash128, UnsafeHashMap<Hash128, int>>.ReadOnly boneMapsCache;
	[ReadOnly]
	public EntityTypeHandle entityTypeHandle;

/////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
	{
		var layerBuffers = chunk.GetBufferAccessor(ref controllerLayersBufferHandle);
		var parameterBuffers = chunk.GetBufferAccessor(ref controllerParametersBufferHandle);
		var entities = chunk.GetNativeArray(entityTypeHandle);

		var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

		while (cee.NextEntityIndex(out var i))
		{
			var layers = layerBuffers[i];
			var parameters = parameterBuffers.Length > 0 ? parameterBuffers[i] : default;
			var e = entities[i];

			AddAnimationsForEntity(ref ecbp, unfilteredChunkIndex, layers, e, parameters);
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////
	
	void AnimationsPostSetup(Span<AnimationToProcessComponent> animations, ref LayerBlob lb, int layerIndex, float weightMultiplier)
	{
		//	Set blending mode and adjust animations weight according to layer weight
		for (int k = 0; k < animations.Length; ++k)
		{
			var a = animations[k];
			a.blendMode = lb.blendingMode;
			a.layerWeight = lb.weight;
			a.layerIndex = layerIndex;
			a.weight *= weightMultiplier;
			if (lb.avatarMask.hash.IsValid)
				a.avatarMask = ExternalBlobPtr<AvatarMaskBlob>.Create(ref lb.avatarMask);
			animations[k] = a;
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe void AddAnimationsForEntity
	(
		ref EntityCommandBuffer.ParallelWriter ecb,
		int sortKey,
		in DynamicBuffer<AnimatorControllerLayerComponent> aclc,
		Entity deformedEntity,
		in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams
	)
	{
		if (deformedEntity == Entity.Null)
			return;

		var animations = ecb.AddBuffer<AnimationToProcessComponent>(sortKey, deformedEntity);

		for (int i = 0; i < aclc.Length; ++i)
		{
			var animationCurIndex = animations.Length;

			ref var l = ref aclc.ElementAt(i);
			ref var cb = ref l.controller;
			ref var lb = ref cb.Value.layers[i];
			if (lb.weight == 0 || l.rtd.srcState.id < 0)
				continue;

			ref var srcStateBlob = ref lb.states[l.rtd.srcState.id];

			var srcStateWeight = 1.0f;
			var dstStateWeight = 0.0f;

			if (l.rtd.activeTransition.id >= 0)
			{
				ref var transitionBlob = ref srcStateBlob.transitions[l.rtd.activeTransition.id];
				dstStateWeight = l.rtd.activeTransition.normalizedDuration;
				srcStateWeight = (1 - dstStateWeight);
			}

			var srcStateTime = GetDurationTime(ref srcStateBlob, runtimeParams, l.rtd.srcState.normalizedDuration);

			var dstStateAnimCount = 0;
			if (l.rtd.dstState.id >= 0)
			{
				ref var dstStateBlob = ref lb.states[l.rtd.dstState.id];
				var dstStateTime = GetDurationTime(ref dstStateBlob, runtimeParams, l.rtd.dstState.normalizedDuration);
				dstStateAnimCount = AddMotionForEntity(ref animations, ref dstStateBlob.motion, runtimeParams, 1, dstStateTime);
			}
			var srcStateAnimCount = AddMotionForEntity(ref animations, ref srcStateBlob.motion, runtimeParams, 1, srcStateTime);

			var animStartPtr = (AnimationToProcessComponent*)animations.GetUnsafePtr() + animationCurIndex;
			var dstAnimsSpan = new Span<AnimationToProcessComponent>(animStartPtr, dstStateAnimCount);
			var srcAnimsSpan = new Span<AnimationToProcessComponent>(animStartPtr + dstStateAnimCount, srcStateAnimCount);

			AnimationsPostSetup(dstAnimsSpan, ref lb, i, math.select(1, dstStateWeight, srcStateAnimCount > 0));
			AnimationsPostSetup(srcAnimsSpan, ref lb, i, math.select(1, srcStateWeight, dstStateAnimCount > 0));
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	void AddAnimationForEntity(ref DynamicBuffer<AnimationToProcessComponent> outAnims, ref MotionBlob mb, float weight, float normalizedStateTime)
	{
		var atp = new AnimationToProcessComponent();
		if (mb.animationBlob.IsValid)
			atp.animation = ExternalBlobPtr<AnimationClipBlob>.Create(ref mb.animationBlob);

		atp.weight = weight;
		atp.time = normalizedStateTime;
		atp.boneMap = GetBoneMapForAnimationSkeleton(ref atp.animation.Value);
		outAnims.Add(atp);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	void AddMotionsFromBlendtree
	(
		in NativeList<MotionIndexAndWeight> miws,
		ref DynamicBuffer<AnimationToProcessComponent> outAnims,
		in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams,
		ref BlobArray<ChildMotionBlob> motions,
		float weight,
		float normalizedStateTime
	)
	{
		for (int i = 0; i < miws.Length; ++i)
		{
			var miw = miws[i];
			ref var m = ref motions[miw.motionIndex];
			AddMotionForEntity(ref outAnims, ref m.motion, runtimeParams, weight * miw.weight, normalizedStateTime);
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	int AddMotionForEntity
	(
		ref DynamicBuffer<AnimationToProcessComponent> outAnims,
		ref MotionBlob mb,
		in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams,
		float weight,
		float normalizedStateTime
	)
	{
		var startLen = outAnims.Length;
		var blendTreeMotionsAndWeights = new NativeList<MotionIndexAndWeight>(Allocator.Temp);

		switch (mb.type)
		{
		case MotionBlob.Type.None:
			break;
		case MotionBlob.Type.AnimationClip:
			AddAnimationForEntity(ref outAnims, ref mb, weight, normalizedStateTime);
			break;
		case MotionBlob.Type.BlendTreeDirect:
			blendTreeMotionsAndWeights = StateMachineProcessJob.GetBlendTreeDirectCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree1D:
			blendTreeMotionsAndWeights = StateMachineProcessJob.GetBlendTree1DCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DSimpleDirectional:
			blendTreeMotionsAndWeights = StateMachineProcessJob.GetBlendTree2DSimpleDirectionalCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DFreeformCartesian:
			blendTreeMotionsAndWeights = StateMachineProcessJob.GetBlendTree2DFreeformCartesianCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DFreeformDirectional:
			blendTreeMotionsAndWeights = StateMachineProcessJob.GetBlendTree2DFreeformDirectionalCurrentMotions(ref mb, runtimeParams);
			break;
		}

		if (blendTreeMotionsAndWeights.IsCreated)
		{
			AddMotionsFromBlendtree(blendTreeMotionsAndWeights, ref outAnims, runtimeParams, ref mb.blendTree.motions, weight, normalizedStateTime);
		}

		return outAnims.Length - startLen;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	float GetDurationTime(ref StateBlob sb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float normalizedDuration)
	{
		var timeDuration = normalizedDuration;
		if (sb.timeParameterIndex >= 0)
		{
			timeDuration = runtimeParams[sb.timeParameterIndex].FloatValue;
		}
		var stateCycleOffset = sb.cycleOffset;
		if (sb.cycleOffsetParameterIndex >= 0)
		{
			stateCycleOffset = runtimeParams[sb.cycleOffsetParameterIndex].FloatValue;
		}
		timeDuration += stateCycleOffset;
		return timeDuration;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	UnsafeHashMap<Hash128, int> GetBoneMapForAnimationSkeleton(ref AnimationClipBlob acb)
	{
		var alreadyHave = boneMapsCache.TryGetValue(acb.hash, out var rv);
		if (alreadyHave)
			return rv;
 
		//	Build new one
		ref var bones = ref acb.bones;
		var newMap = new UnsafeHashMap<Hash128, int>(bones.Length, Allocator.Persistent);
 
		for (int i = 0; i < bones.Length; ++i)
		{
			var boneHash = bones[i].hash;
			newMap.Add(boneHash, i);
		}
		return newMap;
	}
}

//=================================================================================================================//

[BurstCompile]
partial struct CreateBoneRemapTablesJob: IJobEntity
{
	public NativeParallelHashMap<Hash128, UnsafeHashMap<Hash128, int>>.ParallelWriter internalBoneMapsCache;

/////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(ref DynamicBuffer<AnimationToProcessComponent> atps)
	{ 
		for (int i = 0; i < atps.Length; ++i)	
		{
			var atp = atps[i];
			if (atp.animation.IsCreated && atp.boneMap.IsCreated)
			{
				internalBoneMapsCache.TryAdd(atp.animation.Value.hash, atp.boneMap);
			}
		}
	}
}
}
}
