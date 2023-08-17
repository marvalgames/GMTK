
using Unity.Entities;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
	public enum BindingType: short
{
	Translation,
	Quaternion,
	EulerAngles,
	HumanMuscle,
	Scale,
	Unknown
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct KeyFrame
{
	public float v;
	public float inTan, outTan;
	public float time;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct AnimationCurve
{
	public BindingType bindingType;
	public short channelIndex; // 0, 1, 2, 3 -> x, y, z, w
	public BlobArray<KeyFrame> keyFrames;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct BoneClipBlob
{
#if RUKHANKA_DEBUG_INFO
	public BlobString name;
#endif
	public Hash128 hash;
	public bool isHumanMuscleClip;
	public BlobArray<AnimationCurve> animationCurves;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct AvatarMaskBlob
{
#if RUKHANKA_DEBUG_INFO
	public BlobString name;
	public BlobArray<BlobString> includedBoneNames;
#endif
	public Hash128 hash;
	public BlobArray<Hash128> includedBoneHashes;
	public uint humanBodyPartsAvatarMask;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct AnimationClipBlob
{
#if RUKHANKA_DEBUG_INFO
	public BlobString name;
#endif
	public Hash128 hash;
	public BlobArray<BoneClipBlob> bones;
	public BlobArray<BoneClipBlob> curves;
	public bool looped;
	public bool loopPoseBlend;
	public float cycleOffset;
	public float length;
	public float additiveReferencePoseTime;

}

}
