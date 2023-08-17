#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FixedStringName = Unity.Collections.FixedString512Bytes;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

[TemporaryBakingType]
public struct AnimatorControllerBakerData: IComponentData
{
	public RTP.Controller controllerData;
	public Entity targetEntity;
	public int hash;
#if RUKHANKA_DEBUG_INFO
	public FixedStringName name;
#endif
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public class AnimatorControllerBaker: Baker<Animator>
{
	public override void Bake(Animator a)
	{
		if (a.runtimeAnimatorController == null)
		{
			Debug.LogWarning($"There is no controller attached to '{a.name}' animator. Skipping this object");
			return;
		}

		var rac = GetRuntimeAnimatorController(a);
		var ac = GetAnimatorControllerFromRuntime(rac);
		var animationHashCodes = GatherUnityAnimationsHashCodes(rac.animationClips);
		var cd = GenerateControllerComputationData(ac, animationHashCodes);

		//	If AnimatorOverrideController is used, substitute animations
		var aoc = a.runtimeAnimatorController as AnimatorOverrideController;
		var animClipsWithOverride = aoc != null ? aoc.animationClips : rac.animationClips;
		var	animationClips = ConvertAllControllerAnimations(animClipsWithOverride, a);
		cd.animationClips = animationClips;

		//	Create additional "bake-only" entity that will be removed from live world
		var be = CreateAdditionalEntity(TransformUsageFlags.None, true);
		var acbd = new AnimatorControllerBakerData
		{
			controllerData = cd,
			targetEntity = GetEntity(TransformUsageFlags.Dynamic),
			hash = GetControllerHashCode(ac, a.avatar),
		#if RUKHANKA_DEBUG_INFO
			name = a.name
		#endif
		};

		MakeDependencies(ac, rac);
		AddComponent(be, acbd);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	int GetControllerHashCode(AnimatorController ac, Avatar a)
	{
		//	Need to make unique animator controller for humanoid avatars because of uniqueness of hips animation tracks
		var rv = ac.GetHashCode();
		if (a != null && a.isHuman)
			rv ^= a.GetHashCode();
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void MakeDependencies(AnimatorController ac, RuntimeAnimatorController rac)
	{
		DependsOn(rac);
		DependsOn(ac);

		var acs = rac.animationClips;
		foreach (var clip in acs)
		{
			DependsOn(clip);
		}
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RuntimeAnimatorController GetRuntimeAnimatorController(Animator a)
	{
		var rv = a.runtimeAnimatorController;
		//	Check for animator override controller
		var aoc = rv as AnimatorOverrideController;
		if (aoc != null)
		{
			rv = aoc.runtimeAnimatorController;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	AnimatorController GetAnimatorControllerFromRuntime(RuntimeAnimatorController rac)
	{
		if (rac == null) return null;
		var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimatorController>(UnityEditor.AssetDatabase.GetAssetPath(rac));
		return controller;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	List<int> GatherUnityAnimationsHashCodes(AnimationClip[] allClips)
	{
		allClips = Deduplicate(allClips);

		var rv = new List<int>();
		for (int i = 0; i < allClips.Length; ++i)
			rv.Add(allClips[i].GetHashCode());
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Controller GenerateControllerComputationData(AnimatorController ac, List<int> allClipsHashCodes)
	{
		var rv = new RTP.Controller();
		rv.name = ac.name;
		rv.parameters = GenerateControllerParametersComputationData(ac.parameters);

		rv.layers = new UnsafeList<RTP.Layer>(ac.layers.Length, Allocator.Persistent);

		for (int i = 0; i < ac.layers.Length; ++i)
		{
			var l = ac.layers[i];
			var lOverriden = l.syncedLayerIndex >= 0 ? ac.layers[l.syncedLayerIndex] : l;
			var layerData = GenerateControllerLayerComputationData(lOverriden, l, allClipsHashCodes, i, rv.parameters);
			if (!layerData.states.IsEmpty)
				rv.layers.Add(layerData);
		}

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	UnsafeList<RTP.Parameter> GenerateControllerParametersComputationData(AnimatorControllerParameter[] aps)
	{
		var parameters = new UnsafeList<RTP.Parameter>(aps.Length, Allocator.Persistent);
		for (int i = 0; i < aps.Length; ++i)
		{
			var sourceParam = aps[i];
			var outParam = new RTP.Parameter();

			switch (sourceParam.type)
			{
			case AnimatorControllerParameterType.Float:
				outParam.type = ControllerParameterType.Float;
				outParam.defaultValue.floatValue = sourceParam.defaultFloat;
				break;
			case AnimatorControllerParameterType.Int:
				outParam.type = ControllerParameterType.Int;
				outParam.defaultValue.intValue = sourceParam.defaultInt;
				break;
			case AnimatorControllerParameterType.Bool:
				outParam.type = ControllerParameterType.Bool;
				outParam.defaultValue.boolValue = sourceParam.defaultBool;
				break;
			case AnimatorControllerParameterType.Trigger:
				outParam.type = ControllerParameterType.Trigger;
				outParam.defaultValue.boolValue = sourceParam.defaultBool;
				break;
			};

			outParam.name = sourceParam.name;
			parameters.Add(outParam);
		}
		return parameters;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Layer GenerateControllerLayerComputationData(AnimatorControllerLayer acl, AnimatorControllerLayer aclOverriden, List<int> allClipsHashCodes, int layerIndex, in UnsafeList<RTP.Parameter> allParams)
	{
		var l = new RTP.Layer();
		l.name = acl.name;

		var stateList = new UnsafeList<RTP.State>(128, Allocator.Persistent);
		var anyStateTransitions = new UnsafeList<RTP.Transition>(128, Allocator.Persistent);

		GenerateControllerStateMachineComputationData(acl.stateMachine, null, acl, aclOverriden, allClipsHashCodes, ref stateList, ref anyStateTransitions, allParams);
		l.avatarMask = AvatarMaskConversionSystem.PrepareAvatarMaskComputeData(acl.avatarMask);
		l.states = stateList;
		
		l.defaultStateIndex = stateList.IndexOf(acl.stateMachine.defaultState.GetHashCode());
		l.anyStateTransitions = anyStateTransitions;
		l.weight = layerIndex == 0 ? 1 : aclOverriden.defaultWeight;
		l.blendMode = (AnimationBlendingMode)aclOverriden.blendingMode;

		return l;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Condition GenerateControllerConditionComputationData(AnimatorCondition c, in UnsafeList<RTP.Parameter> allParams)
	{
		var rv = new RTP.Condition();
		rv.paramName = c.parameter;

		var paramIdx = allParams.IndexOf(rv.paramName);
		var p = allParams[paramIdx];

		switch (p.type)
		{
		case ControllerParameterType.Int:
			rv.threshold.intValue = (int)c.threshold;
			break;
		case ControllerParameterType.Float:
			rv.threshold.floatValue = c.threshold;
			break;
		case ControllerParameterType.Bool:
		case ControllerParameterType.Trigger:
			rv.threshold.boolValue = c.threshold > 0;
			break;
		}
		rv.conditionMode = (AnimatorConditionMode)c.mode;
		rv.name = $"{rv.paramName} {rv.conditionMode} {rv.threshold}";
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Transition GenerateTransitionDataBetweenStates(AnimatorStateTransition t, string ownStateName, AnimatorState dstState, AnimatorCondition[] conditions, in UnsafeList<RTP.Parameter> allParams)
	{
		var rv = new RTP.Transition();

		rv.duration = t.duration;
		rv.exitTime = t.exitTime;
		rv.hasExitTime = t.hasExitTime;
		rv.hasFixedDuration = t.hasFixedDuration;
		rv.offset = t.offset;
		rv.targetStateHash = dstState.GetHashCode();
		rv.conditions = new UnsafeList<RTP.Condition>(conditions.Length, Allocator.Persistent);
		rv.soloFlag = t.solo;
		rv.muteFlag = t.mute;
		rv.canTransitionToSelf = t.canTransitionToSelf;

		if (t.name != "")
		{ 
			rv.name = t.name;
		}
		else
		{
			rv.name = $"{ownStateName} -> {dstState.name}";
		}

		for (int i = 0; i < conditions.Length; ++i)
		{
			var c = conditions[i];
			rv.conditions.Add(GenerateControllerConditionComputationData(c, allParams));
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	NativeList<RTP.Transition> GenerateControllerTransitionComputationData(AnimatorStateTransition t, AnimatorStateMachine ourStateMachine, AnimatorStateMachine parentStateMachine, string ownStateName, in UnsafeList<RTP.Parameter> allParams)
	{
		//	Because exit and enter states of substatemachines can have several transitions with different conditions this function can generate several transitions
		var rv = new NativeList<RTP.Transition>(Allocator.Temp);
		if (t.destinationState != null)
		{
			var outT = GenerateTransitionDataBetweenStates(t, ownStateName, t.destinationState, t.conditions, allParams);
			rv.Add(outT);
		}
		else
		{
			if (t.destinationStateMachine == null)
			{
				//	This is exit state transition. Transition to parent state machine default state
				//	If there is no parent statemachine then go to own statemachine default state
				var targetState = parentStateMachine == null ? ourStateMachine.defaultState : parentStateMachine.defaultState;
				var outToParentSM = GenerateTransitionDataBetweenStates(t, ownStateName, targetState, t.conditions, allParams);
				rv.Add(outToParentSM);
			}
			else
			{
				//	Generate transitions to every state connected with entry state
				var conditionsArr = new List<AnimatorCondition>(t.conditions);
				var initialConditionsLen = conditionsArr.Count;
				for (int i = 0; i < t.destinationStateMachine.entryTransitions.Length; ++i)
				{
					conditionsArr.RemoveRange(initialConditionsLen, conditionsArr.Count - initialConditionsLen);
					var e = t.destinationStateMachine.entryTransitions[i];
					conditionsArr.AddRange(e.conditions);
					var outEntryT = GenerateTransitionDataBetweenStates(t, ownStateName, e.destinationState, conditionsArr.ToArray(), allParams);
					rv.Add(outEntryT);
				}

				//	Add transition to the default state of target state machine with lowest priority
				var outT = GenerateTransitionDataBetweenStates(t, ownStateName, t.destinationStateMachine.defaultState, t.conditions, allParams);
				rv.Add(outT);
			}
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.ChildMotion GenerateChildMotionComputationData(ChildMotion cm, List<int> allClipsHashCodes)
	{
		var rv = new RTP.ChildMotion();
		rv.threshold = cm.threshold;
		rv.timeScale = cm.timeScale;
		rv.directBlendParameterName = cm.directBlendParameter;
		//	Data for 2D blend trees
		rv.position2D = cm.position;
		rv.motion = GenerateMotionComputationData(cm.motion, allClipsHashCodes);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Motion GenerateMotionComputationData(Motion m, List<int> allClipsHashCodes)
	{
		var rv = new RTP.Motion();
		rv.animationIndex = -1;

		if (m == null)
		{
			rv.name = "NULL_MOTION";
			return rv;
		}

		rv.name = m.name;

		var anm = m as AnimationClip;
		if (anm)
		{
			rv.animationIndex = allClipsHashCodes.IndexOf(anm.GetHashCode());
			rv.type = MotionBlob.Type.AnimationClip;
		}

		var bt = m as BlendTree;
		if (bt)
		{
			rv.type = bt.blendType switch
			{
				BlendTreeType.Simple1D => MotionBlob.Type.BlendTree1D,
				BlendTreeType.Direct => MotionBlob.Type.BlendTreeDirect,
				BlendTreeType.SimpleDirectional2D => MotionBlob.Type.BlendTree2DSimpleDirectional,
				BlendTreeType.FreeformDirectional2D => MotionBlob.Type.BlendTree2DFreeformDirectional,
				BlendTreeType.FreeformCartesian2D => MotionBlob.Type.BlendTree2DFreeformCartesian,
				_ => MotionBlob.Type.None
			};
			rv.blendTree = new RTP.BlendTree();
			rv.blendTree.name = bt.name;
			rv.blendTree.motions = new UnsafeList<RTP.ChildMotion>(bt.children.Length, Allocator.Persistent);
			rv.blendTree.blendParameterName = bt.blendParameter;
			rv.blendTree.blendParameterYName = bt.blendParameterY;
			rv.blendTree.normalizeBlendValues = GetNormalizedBlendValuesProp(bt);
			for (int i = 0; i < bt.children.Length; ++i)
			{
				var c = bt.children[i];
				if (c.motion != null)
				{
					var childMotion = GenerateChildMotionComputationData(bt.children[i], allClipsHashCodes);
					rv.blendTree.motions.Add(childMotion);
				}
			}
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	bool GetNormalizedBlendValuesProp(BlendTree bt)
	{
		//	Hacky way to extract "Normalized Blend Values" prop
		var rv = false;
		using (var so = new SerializedObject(bt))
		{
			var p = so.FindProperty("m_NormalizedBlendValues");
			if (p != null)
				rv = p.boolValue;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	
	RTP.State GenerateControllerStateComputationData
	(
		AnimatorState state,
		AnimatorStateMachine ourStateMachine,
		AnimatorStateMachine parentStateMachine,
		AnimatorControllerLayer aclOverriden,
		List<int> allClipsHashCodes,
		in UnsafeList<RTP.Parameter> allParams
	)
	{
		var rv = new RTP.State();
		rv.name = state.name;
		rv.hashCode = state.GetHashCode();
		
		rv.speed = state.speed;
		rv.speedMultiplierParameter = state.speedParameterActive ? state.speedParameter : "";
		rv.transitions = new UnsafeList<RTP.Transition>(state.transitions.Length, Allocator.Persistent);

		for (int i = 0; i < state.transitions.Length; ++i)
		{
			var t = state.transitions[i];
			var generatedTransitions = GenerateControllerTransitionComputationData(t, ourStateMachine, parentStateMachine, state.name, allParams);
			foreach (var gt in generatedTransitions)
				rv.transitions.Add(gt);
		}

		FilterSoloAndMuteTransitions(ref rv.transitions);

		var motion = aclOverriden.GetOverrideMotion(state);
		if (motion == null)
			motion = state.motion;

		rv.motion = GenerateMotionComputationData(motion, allClipsHashCodes);
		if (state.timeParameterActive)
			rv.timeParameter = state.timeParameter;

		rv.cycleOffset = state.cycleOffset;
		if (state.cycleOffsetParameterActive)
			rv.cycleOffsetParameter = state.cycleOffsetParameter;

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void FilterSoloAndMuteTransitions(ref UnsafeList<RTP.Transition> transitions)
	{
		var hasSoloTransitions = false;
		var l = transitions.Length;
		for (int i = 0; i < l && !hasSoloTransitions; ++i)
		{
			hasSoloTransitions = transitions[i].soloFlag;
		}

		for (int i = 0; i < l;)
		{
			var t = transitions[i];
			//	According to documentation mute flag has precedence
			if (t.muteFlag)
			{
				transitions.RemoveAtSwapBack(i);
				--l;
			}
			else if (!t.soloFlag && hasSoloTransitions)
			{
				transitions.RemoveAtSwapBack(i);
				--l;
			}
			else
			{
				++i;
			}
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	bool GenerateControllerStateMachineComputationData
	(
		AnimatorStateMachine asm,
		AnimatorStateMachine parentSM,
		AnimatorControllerLayer acl,
		AnimatorControllerLayer aclOverriden,
		List<int> allClipsHashCodes,
		ref UnsafeList<RTP.State> sl,
		ref UnsafeList<RTP.Transition> anyStateTransitions,
		in UnsafeList<RTP.Parameter> allParams
	)
	{
		for (int k = 0; k < asm.anyStateTransitions.Length; ++k)
		{
			var ast = asm.anyStateTransitions[k];
			var stateName = "Any State";
			var generatedTransitions = GenerateControllerTransitionComputationData(ast, asm, parentSM, stateName, allParams);
			foreach (var gt in generatedTransitions)
				anyStateTransitions.Add(gt);
		}

		FilterSoloAndMuteTransitions(ref anyStateTransitions);

		for (int i = 0; i < asm.states.Length; ++i)
		{
			var s = asm.states[i];
			var generatedState = GenerateControllerStateComputationData(s.state, asm, parentSM, aclOverriden, allClipsHashCodes, allParams);
			sl.Add(generatedState);
		}

		for (int j = 0; j < asm.stateMachines.Length; ++j)
		{
			var sm = asm.stateMachines[j];
			GenerateControllerStateMachineComputationData(sm.stateMachine, asm, acl, aclOverriden, allClipsHashCodes, ref sl, ref anyStateTransitions, allParams);
		}
		return true;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	AnimationClip[] Deduplicate(AnimationClip[] animationClips)
	{
		var dedupList = new List<AnimationClip>();
		using var dupSet = new NativeHashSet<int>(animationClips.Length, Allocator.Temp);

		foreach (var a in animationClips)
		{
			if (!dupSet.Add(a.GetHashCode()))
			{
				continue;
			}

			dedupList.Add(a);
		}
		return dedupList.ToArray();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	UnsafeList<RTP.AnimationClip> ConvertAllControllerAnimations(AnimationClip[] animationClips, Animator animator)
	{
		animationClips = Deduplicate(animationClips);

		var rv = new UnsafeList<RTP.AnimationClip>(animationClips.Length, Allocator.Persistent);

		foreach (var a in animationClips)
		{
			var acd = AnimationClipBaker.PrepareAnimationComputeData(a, animator);
			rv.Add(acd);
		}

		return rv;
	}
}
}

#endif