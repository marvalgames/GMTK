
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

//=================================================================================================================//
[assembly: InternalsVisibleTo("Rukhanka.Tests")]

namespace Rukhanka
{
partial struct AnimationsProcessSystem
{

[BurstCompile]
public struct ComputeBoneAnimationJob: IJobParallelForBatch
{
	[WriteOnly, NativeDisableContainerSafetyRestriction]
	public NativeList<BoneTransform> outBlendedBones;
	[ReadOnly]
	public BufferLookup<AnimationToProcessComponent> animationsToProcessLookup;
	[ReadOnly]
	public NativeArray<RigDefinitionComponent> rigDefs;
	[ReadOnly]
	public NativeList<int2> boneToEntityIndices;
	[ReadOnly]
	public NativeArray<Entity> entityArr;
	[ReadOnly]
	public BufferLookup<RootMotionStateComponent> rootMotionStateBufLookup;
	[ReadOnly]
	public ComponentLookup<LocalTransform> localTransformLookup;

	public EntityCommandBuffer.ParallelWriter ecb;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(int startIndex, int count)
	{
		for (int i = startIndex; i < startIndex + count; ++i)
			ExecuteSingle(i);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void ExecuteSingle(int globalBoneIndex)
	{
		var boneToEntityIndex = boneToEntityIndices[globalBoneIndex];
		var (rigBoneIndex, entityIndex) = (boneToEntityIndex.y, boneToEntityIndex.x);
		var e = entityArr[entityIndex];

		var rigDef = rigDefs[entityIndex];
		var rigBlobAsset = rigDef.rigBlob;
		ref var rb = ref rigBlobAsset.Value.bones[rigBoneIndex];
		var animationsToProcess = animationsToProcessLookup[e];

		var boneRefPose = GetBoneRefPose(ref rb, rigBoneIndex, e);
		
		//	Early exit -> just set ref pose and exit
		if (animationsToProcess.IsEmpty)
		{
			outBlendedBones[globalBoneIndex] = boneRefPose;
			return;
		}

		//	Early exit for root motion bone in case of root motion enabled
		if (rb.type == RigBoneInfo.Type.RootBone && rigBlobAsset.Value.applyRootMotion)
		{
			outBlendedBones[globalBoneIndex] = BoneTransform.Identity();
			return;
		}
		
		GetHumanRotationDataForSkeletonBone(out var humanBoneInfo, ref rigBlobAsset.Value.humanData, rigBoneIndex);

		Span<float> layerWeights = stackalloc float[32];
		var refPosWeight = CalculateFinalLayerWeights(layerWeights, animationsToProcess, rb.hash, rb.humanBodyPart);
		float3 totalWeights = refPosWeight;

		var blendedBonePose = BoneTransform.Scale(boneRefPose, refPosWeight);

		PrepareRootMotionStateBuffers(e, globalBoneIndex, ref rb, animationsToProcess, out var curRootMotionState, out var newRootMotionState);

		for (int i = 0; i < animationsToProcess.Length; ++i)
		{
			var atp = animationsToProcess[i];
			var (animTime, nrmAnimTime) = NormalizeAnimationTime(atp.time, ref atp.animation.Value);

			var layerWeight = layerWeights[atp.layerIndex];
			if (layerWeight == 0) continue;

			var foundMatchedBone = atp.boneMap.TryGetValue(rb.hash, out var animationBoneIndex);

			if (Hint.Likely(foundMatchedBone))
			{
				ref var boneAnimation = ref atp.animation.Value.bones[animationBoneIndex];
				var (bonePose, flags) = SampleAnimation(ref boneAnimation, animTime, atp, humanBoneInfo);

				float3 modWeight = flags * atp.weight * layerWeight;
				totalWeights += modWeight;

				// Loop Pose calculus for all bones except root motion
				if (Hint.Unlikely(atp.animation.Value.loopPoseBlend && rb.type != RigBoneInfo.Type.RootMotionBone))
				{
					CalculateLoopPose(ref boneAnimation, atp, ref bonePose, nrmAnimTime);
				}

				// Special handling of root motion
				ProcessRootMotion(ref rb, ref bonePose, ref boneAnimation, atp, i, curRootMotionState, newRootMotionState);

				MixPoses(ref blendedBonePose, bonePose, modWeight, atp.blendMode);
			}
		}

		BoneTransformMakePretty(ref blendedBonePose, boneRefPose, totalWeights);

		outBlendedBones[globalBoneIndex] = blendedBonePose;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void GetHumanRotationDataForSkeletonBone(out HumanRotationData rv, ref BlobPtr<HumanData> hd, int rigBoneIndex)
	{
		rv = default;
		if (hd.IsValid)
		{
			rv = hd.Value.humanRotData[rigBoneIndex];
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	internal static float3 MuscleRangeToRadians(float3 minA, float3 maxA, float3 muscle)
	{
		//	Map [-1; +1] range into [minRot; maxRot]
		var negativeRange = math.min(muscle, 0);
		var positiveRange = math.max(0, muscle);
		var negativeRot = math.lerp(0, minA, -negativeRange);
		var positiveRot = math.lerp(0, maxA, +positiveRange);

		var rv = negativeRot + positiveRot;
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void MuscleValuesToQuaternion(in HumanRotationData humanBoneInfo, ref BoneTransform bt)
	{
		var r = MuscleRangeToRadians(humanBoneInfo.minMuscleAngles, humanBoneInfo.maxMuscleAngles, bt.rot.value.xyz);
		r *= humanBoneInfo.sign;

		var qx = quaternion.AxisAngle(math.right(), r.x);
		var qy = quaternion.AxisAngle(math.up(), r.y);
		var qz = quaternion.AxisAngle(math.forward(), r.z);
		var qzy = math.mul(qz, qy);
		qzy.value.x = 0;
		bt.rot = math.mul(math.normalize(qzy), qx);

		ApplyHumanoidPostTransform(humanBoneInfo, ref bt);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static public (float, float) NormalizeAnimationTime(float at, ref AnimationClipBlob ac)
	{
		at += ac.cycleOffset;
		var normalizedTime = ac.looped ? math.frac(at) : math.saturate(at);
		var rv = normalizedTime * ac.length;
		return (rv, normalizedTime);
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	BoneTransform GetBoneRefPose(ref RigBoneInfo rb, int rigBoneIndex, Entity e)
	{
		var rv = rb.refPose;
		//	For entity root we get current entity pose
		if (Hint.Unlikely(rigBoneIndex == 0))
		{
			var lt = localTransformLookup[e];
			rv = new BoneTransform()
			{
				pos = lt.Position,
				rot = lt.Rotation,
				scale = lt.Scale
			};
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void CalculateLoopPose(ref BoneClipBlob boneAnimation, AnimationToProcessComponent atp, ref BoneTransform bonePose, float normalizedTime)
	{
		var animLen = atp.animation.Value.length;
		var lerpFactor = normalizedTime;
		var (rootPoseStart, _) = SampleAnimation(ref boneAnimation, 0, atp);
		var (rootPoseEnd, _) = SampleAnimation(ref boneAnimation, animLen, atp);

		var dPos = rootPoseEnd.pos - rootPoseStart.pos;
		var dRot = math.mul(math.conjugate(rootPoseEnd.rot), rootPoseStart.rot);
		bonePose.pos -= dPos * lerpFactor;
		bonePose.rot = math.mul(bonePose.rot, math.slerp(quaternion.identity, dRot, lerpFactor));
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void PrepareRootMotionStateBuffers
	(
		Entity e,
		int globalBoneIndex,
		ref RigBoneInfo rb,
		in DynamicBuffer<AnimationToProcessComponent> atps,
		out DynamicBuffer<RootMotionStateComponent> curRootMotionState,
		out DynamicBuffer<RootMotionStateComponent> newRootMotionState
	)
	{
		curRootMotionState = default;
		newRootMotionState = default;

		if (rb.type != RigBoneInfo.Type.RootMotionBone) return;

		if (rootMotionStateBufLookup.HasBuffer(e))
			curRootMotionState = rootMotionStateBufLookup[e];

		newRootMotionState = ecb.AddBuffer<RootMotionStateComponent>(globalBoneIndex, e);
		newRootMotionState.Resize(atps.Length, NativeArrayOptions.UninitializedMemory);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void ProcessRootMotion
	(
		ref RigBoneInfo rb,
		ref BoneTransform bonePose,
		ref BoneClipBlob boneAnimation,
		in AnimationToProcessComponent atp,
		int animationIndex,
		DynamicBuffer<RootMotionStateComponent> curRootMotionState,
		DynamicBuffer<RootMotionStateComponent> newRootMotionState
	)
	{
		if (Hint.Likely(rb.type != RigBoneInfo.Type.RootMotionBone)) return;

		HandleRootMotionLoops(ref bonePose, ref boneAnimation, atp);

		BoneTransform rootMotionPrevPose;

		// Find animation history in history buffer
		var historyBufferIndex = 0;
		for (; curRootMotionState.IsCreated && historyBufferIndex < curRootMotionState.Length && curRootMotionState[historyBufferIndex].animationHash != atp.animation.Value.hash; ++historyBufferIndex){ }

		if (historyBufferIndex < curRootMotionState.Length)
		{
			rootMotionPrevPose = curRootMotionState[historyBufferIndex].rootMotionPose;
		}
		else
		{
			(rootMotionPrevPose, _) = SampleAnimation(ref boneAnimation, 0, atp);
		}
		
		newRootMotionState[animationIndex] = new RootMotionStateComponent() { animationHash = atp.animation.Value.hash, rootMotionPose = bonePose };

		var invPrevPose = BoneTransform.Inverse(rootMotionPrevPose);
		bonePose = BoneTransform.Multiply(invPrevPose, bonePose);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void HandleRootMotionLoops(ref BoneTransform bonePose, ref BoneClipBlob boneAnimation, in AnimationToProcessComponent atp)
	{
		ref var animBlob = ref atp.animation.Value;
		if (!animBlob.looped)
			return;

		int numLoopCycles = (int)math.floor(atp.time + atp.animation.Value.cycleOffset);
		if (numLoopCycles < 1)
			return;

		var animLen = atp.animation.Value.length;
		var (endFramePose, _) = SampleAnimation(ref boneAnimation, animLen, atp);
		var (startFramePose, _) = SampleAnimation(ref boneAnimation, 0, atp);

		var deltaPose = BoneTransform.Multiply(endFramePose, BoneTransform.Inverse(startFramePose));

		BoneTransform accumCyclePose = BoneTransform.Identity();
		for (var c = numLoopCycles; c > 0; c >>= 1)
		{
			if ((c & 1) == 1)
				accumCyclePose = BoneTransform.Multiply(accumCyclePose, deltaPose);
			deltaPose = BoneTransform.Multiply(deltaPose, deltaPose);
		}
		bonePose = BoneTransform.Multiply(accumCyclePose, bonePose);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void MixPoses(ref BoneTransform curPose, BoneTransform inPose, float3 weight, AnimationBlendingMode blendMode)
	{
		if (blendMode == AnimationBlendingMode.Override)
		{
			inPose.rot = MathUtils.ShortestRotation(curPose.rot, inPose.rot);
			var scaledPose = BoneTransform.Scale(inPose, weight);

			curPose.pos += scaledPose.pos;
			curPose.rot.value += scaledPose.rot.value;
			curPose.scale += scaledPose.scale;
		}
		else
		{
			curPose.pos += inPose.pos * weight.x;
			quaternion layerRot = math.normalizesafe(new float4(inPose.rot.value.xyz * weight.y, inPose.rot.value.w));
			layerRot = MathUtils.ShortestRotation(curPose.rot, layerRot);
			curPose.rot = math.mul(layerRot, curPose.rot);
			curPose.scale *= (1 - weight.z) + (inPose.scale * weight.z);
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static float CalculateFinalLayerWeights(in Span<float> layerWeights, in DynamicBuffer<AnimationToProcessComponent> atp, in Hash128 boneHash, AvatarMaskBodyPart humanAvatarMaskBodyPart)
	{
		var layerIndex = -1;
		var w = 1.0f;
		var refPoseWeight = 1.0f;

		for (int i = atp.Length - 1; i >= 0; --i)
		{
			var a = atp[i];
			if (a.layerIndex == layerIndex) continue;

			var inAvatarMask = IsBoneInAvatarMask(boneHash, humanAvatarMaskBodyPart, a.avatarMask);
			var layerWeight = inAvatarMask ? a.layerWeight : 0;

			var lw = w * layerWeight;
			layerWeights[a.layerIndex] = lw;
			refPoseWeight -= lw;
			if (a.blendMode == AnimationBlendingMode.Override)
				w = w * (1 - layerWeight);
			layerIndex = a.layerIndex;
		}
		return atp[0].blendMode == AnimationBlendingMode.Override ? 0 : layerWeights[0];
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void ApplyHumanoidPostTransform(HumanRotationData hrd, ref BoneTransform bt)
	{
		bt.rot = math.mul(math.mul(hrd.preRot, bt.rot), hrd.postRot);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void BoneTransformMakePretty(ref BoneTransform bt, BoneTransform refPose, float3 weights)
	{
		var complWeights = math.saturate(new float3(1) - weights);
		bt.pos += refPose.pos * complWeights.x;
		var shortestRefRot = MathUtils.ShortestRotation(bt.rot.value, refPose.rot.value);
		bt.rot.value += shortestRefRot.value * complWeights.y;
		bt.scale += refPose.scale * complWeights.z;

		bt.rot = math.normalize(bt.rot);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static bool IsBoneInAvatarMask(in Hash128 boneHash, AvatarMaskBodyPart humanAvatarMaskBodyPart, ExternalBlobPtr<AvatarMaskBlob> am)
	{
		// If no avatar mask defined or bone hash is all zeroes assume that bone included
		if (!am.IsCreated || !math.any(boneHash.Value))
			return true;

		return (int)humanAvatarMaskBodyPart >= 0 ?
			IsBoneInHumanAvatarMask(humanAvatarMaskBodyPart, am) :
			IsBoneInGenericAvatarMask(boneHash, am);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static bool IsBoneInHumanAvatarMask(AvatarMaskBodyPart humanBoneAvatarMaskIndex, ExternalBlobPtr<AvatarMaskBlob> am)
	{
		var rv = (am.Value.humanBodyPartsAvatarMask & 1 << (int)humanBoneAvatarMaskIndex) != 0;
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static bool IsBoneInGenericAvatarMask(in Hash128 boneHash, ExternalBlobPtr<AvatarMaskBlob> am)
	{
		for (int i = 0; i < am.Value.includedBoneHashes.Length; ++i)
		{
			var avatarMaskBoneHash = am.Value.includedBoneHashes[i];
			if (avatarMaskBoneHash == boneHash)
				return true;
		}
		return false;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static public float ProcessBezierCurve(KeyFrame f0, KeyFrame f1, float l)
	{
		float dt = f1.time - f0.time;
		float m0 = f0.outTan * dt;
		float m1 = f1.inTan * dt;

		float t2 = l * l;
		float t3 = t2 * l;

		float a = 2 * t3 - 3 * t2 + 1;
		float b = t3 - 2 * t2 + l;
		float c = t3 - t2;
		float d = -2 * t3 + 3 * t2;

		float rv = a * f0.v + b * m0 + c * m1 + d * f1.v;
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static public float SampleAnimationCurve(ref BlobArray<KeyFrame> kf, float time)
	{
		for (int i = 0; i < kf.Length; ++i)
		{
			var frame1 = kf[i];
			if (frame1.time >= time)
			{
				if (i == 0)
					return kf[i].v;
				var frame0 = kf[i - 1];

				float f = (time - frame0.time) / (frame1.time - frame0.time);
				return ProcessBezierCurve(frame0, frame1, f);
			}
		}
		return kf[kf.Length - 1].v;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	(BoneTransform, float3) SampleAnimation(ref BoneClipBlob bcb, float time, in AnimationToProcessComponent atp, in HumanRotationData hrd = default)
	{
		var (bonePose, flags) = ProcessAnimationCurves(ref bcb, hrd, time);
		
		//	Make additive animation if requested
		if (atp.blendMode == AnimationBlendingMode.Additive)
		{
			var (zeroFramePose, _) = ProcessAnimationCurves(ref bcb, hrd, atp.animation.Value.additiveReferencePoseTime);
			MakeAdditiveAnimation(ref bonePose, zeroFramePose);
		}
		return (bonePose, flags);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void MakeAdditiveAnimation(ref BoneTransform rv, in BoneTransform zeroFramePose)
	{
		//	If additive layer make difference between reference pose and current animated pose
		rv.pos = rv.pos - zeroFramePose.pos;
		var conjugateZFRot = math.normalizesafe(math.conjugate(zeroFramePose.rot));
		conjugateZFRot = MathUtils.ShortestRotation(rv.rot, conjugateZFRot);
		rv.rot = math.mul(math.normalize(rv.rot), conjugateZFRot);
		rv.scale = rv.scale / zeroFramePose.scale;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	(BoneTransform, float3) ProcessAnimationCurves(ref BoneClipBlob bcb, HumanRotationData hrd, float time)
	{
		var rv = BoneTransform.Identity();

		bool eulerToQuaternion = false;

		float3 flags = 0;
		for (int i = 0; i < bcb.animationCurves.Length; ++i)
		{
			ref var ac = ref bcb.animationCurves[i];
			var interpolatedCurveValue = SampleAnimationCurve(ref ac.keyFrames, time);

			switch (ac.bindingType)
			{
			case BindingType.Translation:
				rv.pos[ac.channelIndex] = interpolatedCurveValue;
				flags.x = 1;
				break;
			case BindingType.Quaternion:
				rv.rot.value[ac.channelIndex] = interpolatedCurveValue;
				flags.y = 1;
				break;
			case BindingType.EulerAngles:
				eulerToQuaternion = true;
				rv.rot.value[ac.channelIndex] = interpolatedCurveValue;
				flags.y = 1;
				break;
			case BindingType.HumanMuscle:
				rv.rot.value[ac.channelIndex] = interpolatedCurveValue;
				flags.y = 1;
				break;
			case BindingType.Scale:
				if (ac.channelIndex == 0)
				{ 
					rv.scale = interpolatedCurveValue;
					flags.z = 1;
				}
				break;
			default:
				Debug.Assert(false, "Unknown binding type!");
				break;
			}
		}

		//	If we have got Euler angles instead of quaternion, convert them here
		if (eulerToQuaternion)
		{
			rv.rot = quaternion.Euler(math.radians(rv.rot.value.xyz));
		}

		if (bcb.isHumanMuscleClip)
		{
			MuscleValuesToQuaternion(hrd, ref rv);
		}

		return (rv, flags);
	}
}

//=================================================================================================================//

[BurstCompile]
struct ProcessUserCurvesJob: IJobChunk
{
	[ReadOnly]
	public ComponentTypeHandle<AnimatorControllerParameterIndexTableComponent> parameterIndexTableHandle;
	[ReadOnly]
	public BufferTypeHandle<AnimationToProcessComponent> animationsToProcessBufHandle;

	public BufferTypeHandle<AnimatorControllerParameterComponent> animatorParametersBufHandle;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
	{
		var atpAccessor = chunk.GetBufferAccessor(ref animationsToProcessBufHandle);
		var apAccessor = chunk.GetBufferAccessor(ref animatorParametersBufHandle);
		var paramIndexTableArr = chunk.GetNativeArray(ref parameterIndexTableHandle);

		var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
		while (cee.NextEntityIndex(out var i))
		{
			var animationsToProcess = atpAccessor[i];
			var animationParameters = apAccessor[i];
			var paramIndexTable = paramIndexTableArr[i];

			if (animationsToProcess.IsEmpty) return;

			Span<float> layerWeights = stackalloc float[32];
			var isSetByCurve = new BitField64();
			Span<float> finalParamValues = stackalloc float[animationParameters.Length];
			finalParamValues.Clear();

			ComputeBoneAnimationJob.CalculateFinalLayerWeights(layerWeights, animationsToProcess, new Hash128(), (AvatarMaskBodyPart)(-1));

			for (int l = 0; l < animationsToProcess.Length; ++l)
			{
				var atp = animationsToProcess[l];
				var (animTime, nrmAnimTime) = ComputeBoneAnimationJob.NormalizeAnimationTime(atp.time, ref atp.animation.Value);
				var layerWeight = layerWeights[atp.layerIndex];
				ref var curves = ref atp.animation.Value.curves;
				for (int k = 0; k < curves.Length; ++k)
				{
					ref var c = ref curves[k];
					var paramHash = c.hash.Value.x;
					var paramIdx = PerfectHash.QueryPerfectHashTable(ref paramIndexTable.seedTable.Value.seedTable, paramHash);
					var hasParam = paramIdx < animationParameters.Length && paramHash == animationParameters[paramIdx].hash;
					if (!hasParam) continue;

					isSetByCurve.SetBits(paramIdx, true);
					var curveValue = SampleUserCurve(ref c.animationCurves[0].keyFrames, atp, animTime);

					if (atp.animation.Value.loopPoseBlend)
						curveValue -= CalculateLoopPose(ref c.animationCurves[0].keyFrames, atp, nrmAnimTime);

					finalParamValues[paramIdx] += curveValue * atp.weight * layerWeight;
				}
			}

			for (int l = 0; l < animationParameters.Length; ++l)
			{
				if (isSetByCurve.GetBits(l) == 0) continue;

				var ap = animationParameters[l];
				ap.value.floatValue = finalParamValues[l];
				animationParameters[l] = ap;
			}
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	float SampleUserCurve(ref BlobArray<KeyFrame> curve, in AnimationToProcessComponent atp, float animTime)
	{ 
		var curveValue = ComputeBoneAnimationJob.SampleAnimationCurve(ref curve, animTime);
		//	Make additive animation if requested
		if (atp.blendMode == AnimationBlendingMode.Additive)
		{
			var additiveValue = ComputeBoneAnimationJob.SampleAnimationCurve(ref curve, atp.animation.Value.additiveReferencePoseTime);
			curveValue -= additiveValue;
		}
		return curveValue;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	float CalculateLoopPose(ref BlobArray<KeyFrame> curve, in AnimationToProcessComponent atp, float normalizedTime)
	{
		var startV = SampleUserCurve(ref curve, atp, 0);
		var endV = SampleUserCurve(ref curve, atp, atp.animation.Value.length);

		var rv = (endV - startV) * normalizedTime;
		return rv;
	}
}

//=================================================================================================================//

[BurstCompile]
struct ApplyRootMotionJob: IJobChunk
{
	[ReadOnly]
	public NativeParallelHashMap<Entity, int> entityToDataOffsetMap;
	[ReadOnly]
	public ComponentTypeHandle<RigDefinitionComponent> rigDefHandle;
	[ReadOnly]
	public EntityTypeHandle entityHandle;
	[NativeDisableContainerSafetyRestriction]
	public NativeList<BoneTransform> boneTransforms;
	[ReadOnly]
	public ComponentTypeHandle<LocalTransform> ltHandle;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
	{
		var rigDefAccessor = chunk.GetNativeArray(ref rigDefHandle);
		var entityArr = chunk.GetNativeArray(entityHandle);
		var ltLookup = chunk.GetNativeArray(ref ltHandle);

		var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

		while (cee.NextEntityIndex(out var i))
		{
			var rd = rigDefAccessor[i];
			var e = entityArr[i];

			if (!entityToDataOffsetMap.TryGetValue(e, out var entityBoneOffset))
				continue;
			
			//	Root motion bone data is next to the last of generic bones in computed animation data array
			var bonesCount = rd.rigBlob.Value.bones.Length;
			var rootMotionBoneDataIndex = entityBoneOffset + bonesCount - 1;
			var animatedRootMotionDeltaPose = boneTransforms[rootMotionBoneDataIndex];

			var curPose = ltLookup[i];
			var curPoseBT = new BoneTransform() { pos = curPose.Position, rot = curPose.Rotation, scale = curPose.Scale };
			curPoseBT = BoneTransform.Multiply(curPoseBT, animatedRootMotionDeltaPose);
			boneTransforms[entityBoneOffset] = curPoseBT;
		}
	}
}

//=================================================================================================================//

[BurstCompile]
struct CalculateBoneOffsetsJob: IJobChunk
{
	[ReadOnly]
	public ComponentTypeHandle<RigDefinitionComponent> rigDefinitionTypeHandle;
	[ReadOnly]
	public NativeArray<int> chunkBaseEntityIndices;
	
	[WriteOnly, NativeDisableContainerSafetyRestriction]
	public NativeArray<int> bonePosesOffsets;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
	{
		var rigDefAccessor = chunk.GetNativeArray(ref rigDefinitionTypeHandle);
		int baseEntityIndex = chunkBaseEntityIndices[unfilteredChunkIndex];
		int validEntitiesInChunk = 0;

		var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
		bonePosesOffsets[0] = 0;

		while (cee.NextEntityIndex(out var i))
		{
			var rigDef = rigDefAccessor[i];

			int entityInQueryIndex = baseEntityIndex + validEntitiesInChunk;
            ++validEntitiesInChunk;

			bonePosesOffsets[entityInQueryIndex + 1] = rigDef.rigBlob.Value.bones.Length;
		}
	}
}

//=================================================================================================================//

[BurstCompile]
struct CalculatePerBoneInfoJob: IJobChunk
{
	[ReadOnly]
	public ComponentTypeHandle<RigDefinitionComponent> rigDefinitionTypeHandle;
	[ReadOnly, DeallocateOnJobCompletion]
	public NativeArray<int> chunkBaseEntityIndices;
	[ReadOnly]
	public NativeArray<int> bonePosesOffsets;
	[ReadOnly]
	public NativeArray<Entity> entites;
	
	[WriteOnly, NativeDisableContainerSafetyRestriction]
	public NativeArray<int2> boneToEntityIndices;
	[WriteOnly]
	public NativeParallelHashMap<Entity, int>.ParallelWriter entityToDataOffsetMap;

	public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
	{
		var rigDefAccessor = chunk.GetNativeArray(ref rigDefinitionTypeHandle);
		int baseEntityIndex = chunkBaseEntityIndices[unfilteredChunkIndex];
		int validEntitiesInChunk = 0;

		var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

		while (cee.NextEntityIndex(out var i))
		{
			var rigDef = rigDefAccessor[i];
			int entityInQueryIndex = baseEntityIndex + validEntitiesInChunk;
            ++validEntitiesInChunk;
			var offset = bonePosesOffsets[entityInQueryIndex];

			for (int k = 0, l = rigDef.rigBlob.Value.bones.Length; k < l; ++k)
			{
				boneToEntityIndices[k + offset] = new int2(entityInQueryIndex, k);
			}

			entityToDataOffsetMap.TryAdd(entites[entityInQueryIndex], offset);
		}
	}
}

//=================================================================================================================//

[BurstCompile]
struct DoPrefixSumJob: IJob
{
	public NativeArray<int> boneOffsets;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute()
	{
		var sum = 0;
		for (int i = 0; i < boneOffsets.Length; ++i)
		{
			sum += boneOffsets[i];
			boneOffsets[i] = sum;
		}
	}
}

//=================================================================================================================//

[BurstCompile]
struct ApplyAnimationToSkinnedMeshJob: IJobChunk
{
	public BufferTypeHandle<SkinMatrix> skinMatrixBufHandle;
	[ReadOnly]
	public ComponentTypeHandle<AnimatedSkinnedMeshComponent> animatedSkinnedMeshHandle;
	[ReadOnly]
	public ComponentLookup<RigDefinitionComponent> rigDefinitionLookup;
	[ReadOnly]
	public ComponentLookup<LocalTransform> ltLookup;
	[ReadOnly]
	public NativeList<BoneTransform> boneTransforms;
	[ReadOnly]
	public NativeParallelHashMap<Entity, int> entityToDataOffsetMap;
	[ReadOnly]
	public NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> rigToSkinnedMeshRemapTables;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static public Hash128 CalculateBoneRemapTableHash(in BlobAssetReference<SkinnedMeshInfoBlob> skinnedMesh, in BlobAssetReference<RigDefinitionBlob> rigDef)
	{
		var rv = new Hash128(skinnedMesh.Value.hash.Value.x, skinnedMesh.Value.hash.Value.y, rigDef.Value.hash.Value.z, rigDef.Value.hash.Value.w);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	ref BoneRemapTableBlob GetBoneRemapTable(in BlobAssetReference<SkinnedMeshInfoBlob> skinnedMesh, in BlobAssetReference<RigDefinitionBlob> rigDef)
	{
		var h = CalculateBoneRemapTableHash(skinnedMesh, rigDef);
		return ref rigToSkinnedMeshRemapTables[h].Value;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	SkinMatrix MakeSkinMatrixForBone(ref SkinnedMeshBoneInfo boneInfo, in float4x4 boneXForm, in float4x4 entityToRootBoneTransform)
	{
		var boneTransformMatrix = math.mul(entityToRootBoneTransform, boneXForm);
		boneTransformMatrix = math.mul(boneTransformMatrix, boneInfo.bindPose);

		var skinMatrix = new SkinMatrix() { Value = new float3x4(boneTransformMatrix.c0.xyz, boneTransformMatrix.c1.xyz, boneTransformMatrix.c2.xyz, boneTransformMatrix.c3.xyz) };
		return skinMatrix;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	BoneTransform MakeAbsoluteTransform(NativeList<(BoneTransform, byte)> absBoneTransforms, int boneIndex, BlobAssetReference<RigDefinitionBlob> rigBlob, BlobAssetReference<SkinnedMeshInfoBlob> skinnedMeshBlob, ref BoneRemapTableBlob boneRemapTable)
	{
		var resultBoneTransform = BoneTransform.Identity();
		var myBoneIndex = boneIndex;
		ref var rigBones = ref rigBlob.Value.bones;
		ref var skinnedMeshBones = ref skinnedMeshBlob.Value.bones;

		while (boneIndex >= 0)
		{
			var (animatedBoneTransform, isHierarchyTreeCombined) = absBoneTransforms[boneIndex];
			resultBoneTransform = BoneTransform.Multiply(animatedBoneTransform, resultBoneTransform);
			
			//	If transform already absolute, simply break
			if (isHierarchyTreeCombined != 0)
				break;

			ref var boneDef = ref rigBones[boneIndex];
			boneIndex = boneDef.parentBoneIndex;

			if (boneDef.hash == skinnedMeshBlob.Value.rootBoneNameHash)
				break;
		}
		absBoneTransforms[myBoneIndex] = ValueTuple.Create(resultBoneTransform, (byte)1);

		return resultBoneTransform;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
	{
		var skinMatrixBufferAccessor = chunk.GetBufferAccessor(ref skinMatrixBufHandle);
		var animatedSkinnedMeshesVec = chunk.GetNativeArray(ref animatedSkinnedMeshHandle);
		var absoluteBoneTransforms = new NativeList<(BoneTransform, byte)>(128, Allocator.Temp);

		var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

		while (cee.NextEntityIndex(out var i))
		{
			var animatedSkinnedMesh = animatedSkinnedMeshesVec[i];
			var rigEntity = animatedSkinnedMesh.animatedRigEntity;

			if (!rigDefinitionLookup.TryGetComponent(rigEntity, out var rigDef))
				continue;

			if (!entityToDataOffsetMap.TryGetValue(rigEntity, out var boneDataOffset))
				continue;

			ref var boneRemapTable = ref GetBoneRemapTable(animatedSkinnedMesh.boneInfos, rigDef.rigBlob);

			var entityToRootBoneTransform = float4x4.identity;
			if (animatedSkinnedMesh.rootBoneEntity != Entity.Null)
			{
				var trs = ltLookup[animatedSkinnedMesh.rootBoneEntity];
				var boneObjLocalPose = new BoneTransform(trs);
				entityToRootBoneTransform = math.inverse(boneObjLocalPose.ToFloat4x4());
			}

			ref var rigBones = ref rigDef.rigBlob.Value.bones;

			var outSkinMatricesBuf = skinMatrixBufferAccessor[i];
			var skinMeshBonesInfo = animatedSkinnedMesh.boneInfos;
			absoluteBoneTransforms.Resize(rigBones.Length, NativeArrayOptions.UninitializedMemory);

			//	Copy local transforms
			for (int l = 0; l < absoluteBoneTransforms.Length; ++l)
			{
				absoluteBoneTransforms[l] = ValueTuple.Create(boneTransforms[l + boneDataOffset], (byte)0);	
			}
			
			// Iterate over all animated bones and set pose for corresponding skin matrices
			for (int animationBoneIndex = 0; animationBoneIndex < rigBones.Length; ++animationBoneIndex)
			{
				var skinnedMeshBoneIndex = boneRemapTable.rigBoneToSkinnedMeshBoneRemapIndices[animationBoneIndex];

				//	Skip bone if it is not present in skinned mesh
				if (skinnedMeshBoneIndex < 0)
					continue;

				//	Perform animation bones hierarchy walk for calculation final bone matrices for skinned mesh
				var absBonePose = MakeAbsoluteTransform(absoluteBoneTransforms, animationBoneIndex, rigDef.rigBlob, skinMeshBonesInfo, ref boneRemapTable);
				var boneXForm = absBonePose.ToFloat4x4();

				ref var boneInfo = ref skinMeshBonesInfo.Value.bones[skinnedMeshBoneIndex];
				var skinMatrix = MakeSkinMatrixForBone(ref boneInfo, boneXForm, entityToRootBoneTransform);
				outSkinMatricesBuf[skinnedMeshBoneIndex] = skinMatrix;
			}
		}

		absoluteBoneTransforms.Dispose();
	}
}

//=================================================================================================================//

[BurstCompile]
struct PropagateBoneTransformToEntityTRSJob: IJobChunk
{
	[ReadOnly]
	public ComponentTypeHandle<AnimatorEntityRefComponent> animatorEntityRefHandle;
	public ComponentTypeHandle<LocalTransform> ltHandle;
	[ReadOnly]
	public NativeParallelHashMap<Entity, int> entityToDataOffsetMap;
	[ReadOnly]
	public NativeSlice<BoneTransform> boneTransforms;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
	{
		var animatorRefAccessor = chunk.GetNativeArray(ref animatorEntityRefHandle);
		var ltLookup = chunk.GetNativeArray(ref ltHandle);

		var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

		while (cee.NextEntityIndex(out var i))
		{
			var animatorRef = animatorRefAccessor[i];

			if (entityToDataOffsetMap.TryGetValue(animatorRef.animatorEntity, out var dataOffset))
			{
				var bt = boneTransforms[dataOffset + animatorRef.boneIndexInAnimationRig];
				ltLookup[i] = bt.GetLocalTtransform();
			}
		}
	}
}

//=================================================================================================================//

[BurstCompile]
struct FillRigToSkinBonesRemapTableCacheJob: IJob
{
	[ReadOnly]
	public ComponentLookup<RigDefinitionComponent> rigDefinitionArr;
	[ReadOnly]
	public NativeArray<AnimatedSkinnedMeshComponent> skinnedMeshes;
	public NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> rigToSkinnedMeshRemapTables;

#if RUKHANKA_DEBUG_INFO
	public bool doLogging;
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute()
	{
		for (int l = 0; l < skinnedMeshes.Length; ++l)
		{
			var sm = skinnedMeshes[l];
			if (!rigDefinitionArr.TryGetComponent(sm.animatedRigEntity, out var rigDef))
				continue;

			//	Try cache first
			var h = ApplyAnimationToSkinnedMeshJob.CalculateBoneRemapTableHash(sm.boneInfos, rigDef.rigBlob);
			if (rigToSkinnedMeshRemapTables.TryGetValue(h, out var rv))
				continue;

			//	Compute new remap table
			var bb = new BlobBuilder(Allocator.Temp);
			ref var brt = ref bb.ConstructRoot<BoneRemapTableBlob>();

		#if RUKHANKA_DEBUG_INFO
			ref var rnd = ref rigDef.rigBlob.Value.name;
			ref var snd = ref sm.boneInfos.Value.skeletonName;
			if (doLogging)
				Debug.Log($"[FillRigToSkinBonesRemapTableCacheJob] Creating rig '{rnd.ToFixedString()}' to skinned mesh '{snd.ToFixedString()}' remap table");
		#endif
			
			var bba = bb.Allocate(ref brt.rigBoneToSkinnedMeshBoneRemapIndices, rigDef.rigBlob.Value.bones.Length);
			for (int i = 0; i < bba.Length; ++i)
			{
				bba[i] = -1;
				ref var rb = ref rigDef.rigBlob.Value.bones[i];
				var rbHash =  rb.hash;
				
				for (int j = 0; j < sm.boneInfos.Value.bones.Length; ++j)
				{
					ref var bn = ref sm.boneInfos.Value.bones[j];
					var bnHash = bn.hash;

					if (bnHash == rbHash)
					{ 
						bba[i] = j;
					#if RUKHANKA_DEBUG_INFO
						if (doLogging)
							Debug.Log($"[FillRigToSkinBonesRemapTableCacheJob] Remap {rb.name.ToFixedString()}->{bn.name.ToFixedString()} : {i} -> {j}");
					#endif
						continue;
					}
				}
			}
			rv = bb.CreateBlobAssetReference<BoneRemapTableBlob>(Allocator.Persistent);
			rigToSkinnedMeshRemapTables.Add(h, rv);
		}
	}
}
}
}
