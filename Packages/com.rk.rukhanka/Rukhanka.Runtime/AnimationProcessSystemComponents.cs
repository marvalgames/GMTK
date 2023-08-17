using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
#if RUKHANKA_WITH_NETCODE
using Unity.NetCode;
#endif
using FixedStringName = Unity.Collections.FixedString512Bytes;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public struct AnimationToProcessComponent: IBufferElementData
{
	public float weight;
	public float time;
	public ExternalBlobPtr<AnimationClipBlob> animation;
	public ExternalBlobPtr<AvatarMaskBlob> avatarMask;
	public UnsafeHashMap<Hash128, int> boneMap;
	public AnimationBlendingMode blendMode;
	public float layerWeight;
	public int layerIndex;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct AnimatorEntityRefComponent: IComponentData
{
	public int boneIndexInAnimationRig;
	public Entity animatorEntity;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if RUKHANKA_WITH_NETCODE
[GhostComponent(PrefabType = GhostPrefabType.Client)]
#endif
public struct AnimatedSkinnedMeshComponent: IComponentData
{
	public Entity animatedRigEntity;
	public Entity rootBoneEntity;
	public BlobAssetReference<SkinnedMeshInfoBlob> boneInfos;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct RootMotionStateComponent: IBufferElementData
{
	public BoneTransform rootMotionPose;
	public Hash128 animationHash;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

//	Define some special bone names
public static class SpecialBones
{
	public readonly static FixedStringName unnamedRootBoneName = "RUKHANKA_UnnamedRootBone";
	public readonly static FixedStringName rootMotionBoneName = "RUKHANKA_RootMotionBone";
	public readonly static FixedStringName invalidBoneName = "RUKHANKA_INVALID_BONE";
}
}

