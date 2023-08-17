using System;
using Unity.Collections.LowLevel.Unsafe;
using FixedStringName = Unity.Collections.FixedString512Bytes;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
//	RTP - Ready to process
namespace RTP
{
public struct AnimationClip: IDisposable
{
	public FixedStringName name;
	public UnsafeList<BoneClip> bones;
	public UnsafeList<BoneClip> curves;
	public bool looped;
	public bool loopPoseBlend;
	public float cycleOffset;
	public float length;
	public float additiveReferencePoseTime;
	public int hash;
	public FixedStringName rootMotionBoneName;

	public void Dispose()
	{
		foreach (var a in bones) a.Dispose();

		bones.Dispose();
	}
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct BoneClip: IEquatable<FixedStringName>, IDisposable
{
	public FixedStringName name;
	public bool isHumanMuscleClip;
	public UnsafeList<AnimationCurve> animationCurves;

	public bool Equals(FixedStringName o) => o == name;

	public void Dispose()
	{
		foreach (var a in animationCurves) a.Dispose();
		animationCurves.Dispose();
	}
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct AnimationCurve: IDisposable
{
	public BindingType bindingType;
	public short channelIndex; // 0, 1, 2, 3 -> x, y, z, w
	public UnsafeList<KeyFrame> keyFrames;

	public void Dispose() => keyFrames.Dispose();
}

} // RTP
}


