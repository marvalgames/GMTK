using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Rukhanka.Editor;
using System.Reflection;
using System.Collections.Generic;
using System;
using Unity.Mathematics;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

[TemporaryBakingType]
public struct RigDefinitionBakerComponent: IComponentData
{
	public RTP.RigDefinition rigDefData;
	public Entity targetEntity;
	public UnsafeList<Entity> boneHierarchyEntities;
	public int hash;
#if RUKHANKA_DEBUG_INFO
	public FixedStringName name;
#endif
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public class RigDefinitionBaker: Baker<RigDefinitionAuthoring>
{
	static FieldInfo parentBoneNameField;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static RigDefinitionBaker()
	{
		parentBoneNameField = typeof(SkeletonBone).GetField("parentName", BindingFlags.NonPublic | BindingFlags.Instance);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public override void Bake(RigDefinitionAuthoring a)
	{
		var animator = GetComponent<Animator>();
		var processedRig = CreateRigDefinitionFromRigAuthoring(a, animator);

		//	Create additional "bake-only" entity that will be removed from live world
		var be = CreateAdditionalEntity(TransformUsageFlags.None, true);
		var acbd = new RigDefinitionBakerComponent
		{
			rigDefData = processedRig,
			targetEntity = GetEntity(TransformUsageFlags.Dynamic),
			hash = processedRig.GetHashCode(),
		#if RUKHANKA_DEBUG_INFO
			name = a.name
		#endif
		};

		DependsOn(a.transform);
		AddComponent(be, acbd);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	SkeletonBone CreateSkeletonBoneFromTransform(Transform t, string parentName)
	{
		var bone = new SkeletonBone();
		bone.name = t.name;
		bone.position = t.localPosition;
		bone.rotation = t.localRotation;
		bone.scale = t.localScale;
		parentBoneNameField.SetValue(bone, parentName);
		return bone;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void TransformHierarchyWalk(Transform parent, List<SkeletonBone> sb)
	{
		for (int i = 0; i < parent.childCount; ++i)
		{
			var c = parent.GetChild(i);
			var ct = c.transform;
			var bone = CreateSkeletonBoneFromTransform(ct, parent.name);
			sb.Add(bone);

			TransformHierarchyWalk(ct, sb);
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	SkeletonBone[] CreateAvatarFromObjectHierarchy(GameObject root)
	{
		//	Manually fill all bone transforms
		var sb = new List<SkeletonBone>();
		var rootBone = CreateSkeletonBoneFromTransform(root.transform, "");
		sb.Add(rootBone);

		TransformHierarchyWalk(root.transform, sb);
		return sb.ToArray();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.RigDefinition CreateRigDefinitionFromRigAuthoring(RigDefinitionAuthoring rigDef, Animator animator)
	{
		var avatar = animator.avatar;

		var rv = new RTP.RigDefinition();
		rv.rigBones = new UnsafeList<RTP.RigBoneInfo>(60, Allocator.Persistent);

		rv.name = rigDef.gameObject.name;
		rv.applyRootMotion = animator.applyRootMotion;
		rv.isHuman = avatar != null && avatar.isHuman;

		var skeletonBones = avatar == null ? CreateAvatarFromObjectHierarchy(rigDef.gameObject) : avatar.humanDescription.skeleton;
		if (skeletonBones.Length == 0)
		{
			Debug.LogError($"Unity avatar '{avatar.name}' setup is incorrect. Follow <a href=\"https://docs.rukhanka.com/getting_started#rig-definition\">documentation</a> about avatar setup process please.");
			return rv;
		}

		for (int i = 0; i < skeletonBones.Length; ++i)
		{
			var ab = CreateRigBoneInfo(rigDef, skeletonBones, avatar, i);
			rv.rigBones.Add(ab);
		}

		if (animator.applyRootMotion)
		{
			var rootMotionBone = CreateRootMotionBone();
			rv.rigBones.Add(rootMotionBone);
		}

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.RigBoneInfo CreateRootMotionBone()
	{
		var rv = new RTP.RigBoneInfo()
		{
			name = SpecialBones.rootMotionBoneName,
			hash = SpecialBones.rootMotionBoneName.CalculateHash128(),
			parentBoneIndex = -1,
			humanRotationData = new RTP.RigBoneInfo.HumanRotationData() {humanRigIndex = -1},
			type = RigBoneInfo.Type.RootMotionBone,
			refPose = BoneTransform.Identity()
		};
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.RigBoneInfo.HumanRotationData GetHumanoidBoneRotationData(Avatar a, string boneName)
	{
		if (a == null || !a.isHuman)
			return RTP.RigBoneInfo.HumanRotationData.Identity();

		var hd = a.humanDescription;
		var humanBoneInSkeletonIndex = Array.FindIndex(hd.human, x => x.boneName == boneName);
		if (humanBoneInSkeletonIndex < 0)
			return RTP.RigBoneInfo.HumanRotationData.Identity();
			
		var humanBones = HumanTrait.BoneName;
		var humanBoneDef = hd.human[humanBoneInSkeletonIndex];
		var humanBoneId = Array.FindIndex(humanBones, x => x == humanBoneDef.humanName);
		Debug.Assert(humanBoneId >= 0);

		var rv = RTP.RigBoneInfo.HumanRotationData.Identity();
		rv.preRot = a.GetPreRotation(humanBoneId);
		rv.postRot = a.GetPostRotation(humanBoneId);
		rv.sign = a.GetLimitSign(humanBoneId);
		rv.humanRigIndex = humanBoneId;

		var minA = humanBoneDef.limit.min;
		var maxA = humanBoneDef.limit.max;
		if (humanBoneDef.limit.useDefaultValues)
		{
			minA.x = HumanTrait.GetMuscleDefaultMin(HumanTrait.MuscleFromBone(humanBoneId, 0));
			minA.y = HumanTrait.GetMuscleDefaultMin(HumanTrait.MuscleFromBone(humanBoneId, 1));
			minA.z = HumanTrait.GetMuscleDefaultMin(HumanTrait.MuscleFromBone(humanBoneId, 2));

			maxA.x = HumanTrait.GetMuscleDefaultMax(HumanTrait.MuscleFromBone(humanBoneId, 0));
			maxA.y = HumanTrait.GetMuscleDefaultMax(HumanTrait.MuscleFromBone(humanBoneId, 1));
			maxA.z = HumanTrait.GetMuscleDefaultMax(HumanTrait.MuscleFromBone(humanBoneId, 2));
		}
		rv.minAngle = math.radians(minA);
		rv.maxAngle = math.radians(maxA);

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.RigBoneInfo CreateRigBoneInfo(RigDefinitionAuthoring rda, SkeletonBone[] skeletonBones, Avatar avatar, int boneIndex)
	{
		var boneIsObjectRoot = boneIndex == 0;
		var skeletonBone = skeletonBones[boneIndex];
		var t = boneIsObjectRoot ? rda.transform : FindChildRecursively(rda.transform, skeletonBone.name);

		var name = skeletonBone.name;
		// Special handling of hierarchy root
		if (boneIsObjectRoot)
		{
			name = SpecialBones.unnamedRootBoneName.ToString();
		}

		var parentBoneName = (string)parentBoneNameField.GetValue(skeletonBone);
		var parentBoneIndex = Array.FindIndex(skeletonBones, x => x.name == parentBoneName);

		var pose = new BoneTransform()
		{
			pos = skeletonBone.position,
			rot = skeletonBone.rotation,
			scale = skeletonBone.scale
		};

		//	Add humanoid avatar info
		var humanRotData = GetHumanoidBoneRotationData(avatar, name);

		var rootMotionBoneFromAvatar = avatar.GetRootMotionNodeName();

		var isRootMotionBone = rootMotionBoneFromAvatar.Length > 0 && t != null && rootMotionBoneFromAvatar == t.name;
		var boneName = new FixedStringName(name);
		var boneHash = boneName.CalculateHash128();
		var ab = new RTP.RigBoneInfo()
		{
			name = boneName,
			hash = boneHash,
			parentBoneIndex = parentBoneIndex,
			type = isRootMotionBone ? RigBoneInfo.Type.RootBone : RigBoneInfo.Type.GenericBone,
			refPose = pose,
			boneObjectEntity = GetEntity(t, TransformUsageFlags.Dynamic),
			humanRotationData = humanRotData
		};
		return ab;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	Transform FindTransformInHierarchy(Transform root, string name)
	{
		if (name == root.name)
			return root;

		return FindChildRecursively(root, name);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	Transform FindChildRecursively(Transform root, string name)
	{
		var rv = root.Find(name);
		if (rv != null)
			return rv;

		var childCount = root.childCount;
		for (int i = 0; i < childCount; ++i)
		{
			 var c = root.GetChild(i);
			 var crv = FindChildRecursively(c, name);
			 if (crv != null)
				return crv;
		}
		return null;
	}
}
}
