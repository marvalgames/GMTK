#if UNITY_EDITOR

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using System.Linq;
using Unity.Assertions;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine.Playables;
using UnityEngine.Animations;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{ 
[BurstCompile]
public partial class AnimationClipBaker
{
	enum BoneType
	{
		Generic,
		MotionCurve,
		RootCurve
	}

	struct ParsedCurveBinding
	{
		public BindingType bindingType;
		public short channelIndex;
		public BoneType boneType;
		public FixedStringName boneName;

		public float2 humanoidMuscleRange;
		public float muscleRotSign;

		public bool IsValid() => boneName.Length > 0;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static ValueTuple<string, string> SplitPath(string path)
	{
		var arr = path.Split('/');
		Assert.IsTrue(arr.Length > 0);
		var rv = (arr.Last(), arr.Length > 1 ? arr[arr.Length - 2] : "");
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static (BindingType, BoneType) PickGenericBindingTypeByString(string bindingString) => bindingString switch
	{
		"m_LocalPosition" => (BindingType.Translation, BoneType.Generic),
		"MotionT" => (BindingType.Translation, BoneType.MotionCurve),
		"RootT" => (BindingType.Translation, BoneType.RootCurve),
		"m_LocalRotation" => (BindingType.Quaternion, BoneType.Generic),
		"MotionQ" => (BindingType.Quaternion, BoneType.MotionCurve),
		"RootQ" => (BindingType.Quaternion, BoneType.RootCurve),
		"localEulerAngles" => (BindingType.EulerAngles, BoneType.Generic),
		"localEulerAnglesRaw" => (BindingType.EulerAngles, BoneType.Generic),
		"m_LocalScale" => (BindingType.Scale, BoneType.Generic),
		_ => (BindingType.Unknown, BoneType.Generic)
	};

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static short ChannelIndexFromString(string c) => c switch
	{
		"x" => 0,
		"y" => 1,
		"z" => 2,
		"w" => 3,
		_ => 999
	};

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static FixedStringName ConstructBoneClipName(ValueTuple<string, string> nameAndPath, BoneType bt)
	{
		FixedStringName rv;
		//	Empty name string is unnamed root bone
		if (nameAndPath.Item1.Length == 0 && nameAndPath.Item2.Length == 0)
		{
			rv = bt switch
			{
				BoneType.Generic => SpecialBones.unnamedRootBoneName,
				BoneType.RootCurve => SpecialBones.rootMotionBoneName + "_Root",
				BoneType.MotionCurve => SpecialBones.rootMotionBoneName + "_Motion",
				_ => SpecialBones.invalidBoneName
			};
		}
		else
		{
			rv = new FixedStringName(nameAndPath.Item1);
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static RTP.AnimationCurve PrepareAnimationCurve(Keyframe[] keysArr, ParsedCurveBinding pb)
	{
		var animCurve = new RTP.AnimationCurve();
		animCurve.channelIndex = pb.channelIndex;
		animCurve.bindingType = pb.bindingType;
		animCurve.keyFrames = new UnsafeList<KeyFrame>(keysArr.Length, Allocator.Persistent);

		foreach (var k in keysArr)
		{
			var kf = new KeyFrame()
			{
				time = k.time,
				inTan = k.inTangent,
				outTan = k.outTangent,
				v = k.value
			};
			animCurve.keyFrames.Add(kf);
		}
		return animCurve;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static int GetOrCreateBoneClipHolder(ref UnsafeList<RTP.BoneClip> clipsArr, in FixedStringName name, BindingType bt)
	{
		var rv = clipsArr.IndexOf(name);
		if (rv < 0)
		{
			rv = clipsArr.Length;
			var bc = new RTP.BoneClip();
			bc.name = name;
			bc.isHumanMuscleClip = bt == BindingType.HumanMuscle;
			bc.animationCurves = new UnsafeList<RTP.AnimationCurve>(32, Allocator.Persistent);
			clipsArr.Add(bc);
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static RTP.BoneClip MakeBoneClipCopy(in RTP.BoneClip bc)
	{
		var rv = bc;
		rv.animationCurves = new UnsafeList<RTP.AnimationCurve>(bc.animationCurves.Length, Allocator.Persistent);
		for (int i = 0; i < bc.animationCurves.Length; ++i)
		{
			var inKf = bc.animationCurves[i].keyFrames;
			var outKf = new UnsafeList<KeyFrame>(inKf.Length, Allocator.Persistent);
			for (int j = 0; j < inKf.Length; ++j)
			{
				outKf.Add(inKf[j]);
			}
			var ac = bc.animationCurves[i];
			ac.keyFrames = outKf;
			rv.animationCurves.Add(ac);
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static void DebugLoggging(RTP.AnimationClip ac, bool hasRootCurves)
	{
#if RUKHANKA_DEBUG_INFO
		var dc = GameObject.FindObjectOfType<RukhankaDebugConfiguration>();
		var logClipBaking = dc != null && dc.logClipBaking;
		if (!logClipBaking) return;

		Debug.Log($"Baking animation clip '{ac.name}'. Tracks: {ac.bones.Length}. User curves: {ac.curves.Length}. Length: {ac.length}s. Looped: {ac.looped}. Has root curves: {hasRootCurves}");
#endif
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static ParsedCurveBinding ParseGenericCurveBinding(EditorCurveBinding b)
	{
		var rv = new ParsedCurveBinding();

		var t = b.propertyName.Split('.');
		var propName = t[0];
		var channel = t.Length > 1 ? t[1] : "";

		rv.channelIndex = ChannelIndexFromString(channel);
		(rv.bindingType, rv.boneType) = PickGenericBindingTypeByString(propName);

		if (rv.bindingType != BindingType.Unknown)
		{
			var nameAndPath = SplitPath(b.path);
			rv.boneName = ConstructBoneClipName(nameAndPath, rv.boneType);
		}
		else
		{
			rv.boneName = new FixedStringName(propName);
		}

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static int GetHumanBoneIndexForHumanName(in HumanDescription hd, FixedStringName humanBoneName)
	{
		var humanBoneIndexInAvatar = Array.FindIndex(hd.human, x => x.humanName == humanBoneName);
		return humanBoneIndexInAvatar;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static ParsedCurveBinding ParseHumanoidCurveBinding(EditorCurveBinding b, Avatar avatar)
	{
		if (!humanoidMappingTable.TryGetValue(b.propertyName, out var rv))
			return rv;

		var hd = avatar.humanDescription;
		var humanBoneIndexInAvatar = GetHumanBoneIndexForHumanName(hd, rv.boneName);
		if (humanBoneIndexInAvatar < 0)
			return rv;

		if (rv.bindingType == BindingType.HumanMuscle)
		{
			var humanBoneDef = hd.human[humanBoneIndexInAvatar];
			rv.boneName = humanBoneDef.boneName;
			var humanBodyBone = (HumanBodyBones)Array.FindIndex(HumanTrait.BoneName, x => x == humanBoneDef.humanName);
			var humanBodyMuscle = HumanTrait.MuscleFromBone((int)humanBodyBone, rv.channelIndex);
			var limit = humanBoneDef.limit;

			var limits = new float2(limit.min[rv.channelIndex], limit.max[rv.channelIndex]);
			if (humanBoneDef.limit.useDefaultValues)
			{
				limits.x = HumanTrait.GetMuscleDefaultMin(humanBodyMuscle);
				limits.y = HumanTrait.GetMuscleDefaultMax(humanBodyMuscle);
			}
			rv.humanoidMuscleRange = limits;
			var limitsSign = avatar.GetLimitSign((int)humanBodyBone);
			rv.muscleRotSign = limitsSign[rv.channelIndex];
		}

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static ParsedCurveBinding ParseCurveBinding(AnimationClip ac, EditorCurveBinding b, Avatar avatar)
	{
		var rv = ac.isHumanMotion ?
			ParseHumanoidCurveBinding(b, avatar) :
			ParseGenericCurveBinding(b);

		return  rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static void AddKeyFrameFromFloatValue(ref UnsafeList<KeyFrame> kfArr, float2 key, float v)
	{
		var kf = new KeyFrame()
		{
			time = key.x,
			inTan = key.y,
			outTan = key.y,
			v = v
		};
		kfArr.Add(kf);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	static void ComputeTangents(ref RTP.AnimationCurve ac)
	{
		for (int i = 0; i < ac.keyFrames.Length; ++i)
		{
			var p0 = i == 0 ? ac.keyFrames[0] : ac.keyFrames[i - 1];
			var p1 = ac.keyFrames[i];
			var p2 = i == ac.keyFrames.Length - 1 ? ac.keyFrames[i] : ac.keyFrames[i + 1];

			var outV = math.normalizesafe(new float2(p2.time, p2.v) - new float2(p1.time, p1.v));
			var outTan = outV.x > 0.0001f ? outV.y / outV.x : 0;

			var inV = math.normalizesafe(new float2(p1.time, p1.v) - new float2(p0.time, p0.v));
			var inTan = inV.x > 0.0001f ? inV.y / inV.x : 0;

			var dt = math.abs(inTan) + math.abs(outTan);
			var f = dt > 0 ? math.abs(inTan) / dt : 0;

			var avgTan = math.lerp(inTan, outTan, f);

			var k = ac.keyFrames[i];
			k.outTan = avgTan;
			k.inTan = avgTan;
			ac.keyFrames[i] = k;
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static NativeList<float> CreateKeyframeTimes(float animationLength, float dt)
	{
		var numFrames = (int)math.ceil(animationLength / dt);

		var rv = new NativeList<float>(numFrames, Allocator.Temp);

		float curTime = 0;
		for (;;)
		{
			rv.Add(curTime);
			curTime += dt;
			if (curTime > animationLength)
			{
				rv.Add(animationLength);
				break;
			}
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static void MakeHumanHipsAnimationBySampling(AnimationClip ac, Animator anm, ref UnsafeList<RTP.BoneClip> boneClips)
	{
		var sampleAnimationFrameTime = 1 / 60.0f;
		var keysList = CreateKeyframeTimes(ac.length, sampleAnimationFrameTime);

		var hipsTransform = anm.GetBoneTransform(HumanBodyBones.Hips);

		var channelDesc = new ValueTuple<BindingType, short>[]
		{
			(BindingType.Translation, 0),
			(BindingType.Translation, 1),
			(BindingType.Translation, 2),
			(BindingType.Quaternion, 0),
			(BindingType.Quaternion, 1),
			(BindingType.Quaternion, 2),
			(BindingType.Quaternion, 3),
		};

		var hipsAnimationCurves = new NativeArray<RTP.AnimationCurve>(channelDesc.Length, Allocator.Temp);
		for (int i = 0; i < hipsAnimationCurves.Length; ++i)
		{
			hipsAnimationCurves[i] = new RTP.AnimationCurve() { bindingType = channelDesc[i].Item1, channelIndex = channelDesc[i].Item2, keyFrames = new UnsafeList<KeyFrame>(keysList.Length, Allocator.Persistent) };
		}

		var rac = anm.runtimeAnimatorController;
		var origPos = anm.transform.position;
		var origRot = anm.transform.rotation;

		anm.runtimeAnimatorController = null;
		AnimationPlayableUtilities.PlayClip(anm, ac, out var pg);
		anm.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		var prevAnmCulling = anm.cullingMode;

		for (int i = 0; i < keysList.Length; ++i)
		{
			var time = keysList[i];
			var dt = i == 0 ? 0 : time - keysList[i - 1];
			pg.Evaluate(dt);

			quaternion q = hipsTransform.localRotation;
			float3 t = hipsTransform.localPosition;

			var vArr = new NativeArray<float>(channelDesc.Length, Allocator.Temp);
			vArr[0] = t.x;
			vArr[1] = t.y;
			vArr[2] = t.z;
			vArr[3] = q.value.x;
			vArr[4] = q.value.y;
			vArr[5] = q.value.z;
			vArr[6] = q.value.w;

			for (int l = 0; l < vArr.Length; ++l)
			{
				var keysArr = hipsAnimationCurves[l];
				AddKeyFrameFromFloatValue(ref keysArr.keyFrames, time, vArr[l]);
				hipsAnimationCurves[l] = keysArr;
			}
		}

		var hd = anm.avatar.humanDescription;
		var humanBoneIndexInDesc = GetHumanBoneIndexForHumanName(hd, "Hips");
		var rigHipsBoneName = hd.human[humanBoneIndexInDesc].boneName;
		var boneId = GetOrCreateBoneClipHolder(ref boneClips, rigHipsBoneName, BindingType.Translation);
		ref var bc = ref boneClips.ElementAt(boneId);

		for (var i = 0; i < hipsAnimationCurves.Length; ++i)
		{
			var hac = hipsAnimationCurves[i];
			ComputeTangents(ref hac);
			bc.animationCurves.Add(hac);
		}

		pg.Destroy();

		anm.cullingMode = prevAnmCulling;
		anm.runtimeAnimatorController = rac;
		anm.transform.position = origPos;
		anm.transform.rotation = origRot;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static bool AdjustRootMotionBones(int rootCurveBoneIndex, int motionCurveBoneIndex, ref UnsafeList<RTP.BoneClip> clips)
	{
		// Motion curves override root curves
		var (rootMotionBoneIndex, unneededMotionBoneIndex) = motionCurveBoneIndex >= 0 ? (motionCurveBoneIndex, rootCurveBoneIndex) : (rootCurveBoneIndex, motionCurveBoneIndex);
		if (rootMotionBoneIndex >= 0)
		{
			if (unneededMotionBoneIndex >= 0)
			{
				clips.RemoveAt(unneededMotionBoneIndex);
				if (rootMotionBoneIndex > unneededMotionBoneIndex)
					rootMotionBoneIndex -= 1;
			}
			clips.ElementAt(rootMotionBoneIndex).name = SpecialBones.rootMotionBoneName;
		}
		return rootMotionBoneIndex >= 0;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static public RTP.AnimationClip PrepareAnimationComputeData(AnimationClip ac, Animator animator)
	{
		var acSettings = AnimationUtility.GetAnimationClipSettings(ac);

		var rv = new RTP.AnimationClip();
		rv.name = ac.name;
		rv.bones = new UnsafeList<RTP.BoneClip>(100, Allocator.Persistent);
		rv.curves = new UnsafeList<RTP.BoneClip>(100, Allocator.Persistent);
		rv.length = ac.length;
		rv.looped = ac.isLooping;
		rv.hash = ac.GetHashCode();
		rv.loopPoseBlend = acSettings.loopBlend;
		rv.cycleOffset = acSettings.cycleOffset;
		rv.additiveReferencePoseTime = acSettings.additiveReferencePoseTime;

		var bindings = AnimationUtility.GetCurveBindings(ac);
		int unnamedRootBoneIndex = -1;
		var motionCurveBoneIndex = -1;
		var rootCurveBoneIndex = -1;

		foreach (var b in bindings)
		{
			var ec = AnimationUtility.GetEditorCurve(ac, b);
			var pb = ParseCurveBinding(ac, b, animator.avatar);
			
			if (!pb.IsValid()) continue;

			var animCurve = PrepareAnimationCurve(ec.keys, pb);
			var isGenericCurve = pb.bindingType == BindingType.Unknown;

			var curveHolder = isGenericCurve ? rv.curves : rv.bones;

			if (pb.channelIndex < 0 && !isGenericCurve) continue;

			var boneId = GetOrCreateBoneClipHolder(ref curveHolder, pb.boneName, pb.bindingType);
			var boneClip = curveHolder[boneId];
			boneClip.animationCurves.Add(animCurve);
			curveHolder[boneId] = boneClip;

			if (isGenericCurve)
				rv.curves = curveHolder;
			else
				rv.bones = curveHolder;

			if (pb.boneName == SpecialBones.unnamedRootBoneName)
				unnamedRootBoneIndex = boneId;

			if (pb.boneType == BoneType.MotionCurve)
				motionCurveBoneIndex = boneId;
			if (pb.boneType == BoneType.RootCurve)
				rootCurveBoneIndex = boneId;
		}

		var hasRootCurves = AdjustRootMotionBones(rootCurveBoneIndex, motionCurveBoneIndex, ref rv.bones);

		//	Copy root bone track to the root motion bone
		if (!hasRootCurves && unnamedRootBoneIndex >= 0)
		{
			var rootMotionBone = MakeBoneClipCopy(rv.bones[unnamedRootBoneIndex]);
			rootMotionBone.name = SpecialBones.rootMotionBoneName;
			rv.bones.Add(rootMotionBone);
		}

		//	Because of mystery around root bone (hips) movement computation for humanoid animation we are forced to sample animations and gather hips positions as movement track
		if (animator.avatar != null && animator.avatar.isHuman)
		{
			MakeHumanHipsAnimationBySampling(ac, animator, ref rv.bones);
			//	Because we have modified hips tracks we need to make animation hash unique
			rv.hash ^= animator.avatar.GetHashCode();
		}

		DebugLoggging(rv, hasRootCurves);

		return rv;
	}
}
}

#endif