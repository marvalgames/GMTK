using Unity.Entities;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

public struct FastAnimatorParameter
{
	public FixedStringName paramName;
	public uint hash;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public FastAnimatorParameter(FixedStringName name)
	{
		hash = name.CalculateHash32();
		paramName = name;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public bool GetRuntimeParameterData(BlobAssetReference<ParameterPerfectHashTableBlob> cb, DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, out ParameterValue outData)
	{
		var paramIdx = GetRuntimeParameterIndex(cb, runtimeParameters);
		bool isValid = paramIdx >= 0;

		if (isValid)
		{
			outData = runtimeParameters[paramIdx].value;
		}
		else
		{
			outData = default;
		#if RUKHANKA_DEBUG_INFO
			Debug.LogError($"Could find animator parameter with name {paramName} in hash table! Returning default value!");
		#endif
		}
		return isValid;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public bool SetRuntimeParameterData(BlobAssetReference<ParameterPerfectHashTableBlob> cb, DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, in ParameterValue paramData)
	{
		var paramIdx = GetRuntimeParameterIndex(cb, runtimeParameters);
		bool isValid = paramIdx >= 0;

		if (isValid)
		{
			var p = runtimeParameters[paramIdx];
			p.value = paramData;
			runtimeParameters[paramIdx] = p;
		}
	#if RUKHANKA_DEBUG_INFO
		else
		{
			Debug.LogError($"Could find animator parameter with name {paramName} in hash table! Setting value is failed!");
		}
	#endif
		return isValid;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static unsafe int GetRuntimeParameterIndexFromHashMap(uint hash, in BlobAssetReference<ParameterPerfectHashTableBlob> cb, void* ptr, int length)
	{
		ref var seedTable = ref cb.Value.seedTable;
		var paramIdxShuffled = PerfectHash.QueryPerfectHashTable(ref seedTable, hash);

		if (paramIdxShuffled >= length)
			return -1;

		var paramIdx = cb.Value.indirectionTable[paramIdxShuffled];

		var p = UnsafeUtility.ReadArrayElement<AnimatorControllerParameterComponent>(ptr, paramIdx);
		if (p.hash != hash)
			return -1;

		return paramIdx;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static unsafe int GetRuntimeParameterIndex(uint hash, in BlobAssetReference<ParameterPerfectHashTableBlob> cb, void* ptr, int length)
	{
		//	Parameter buffer access with length to ~10 is faster with linear search, so we do linear search for small arrays and hash map lookup for bigger ones
		if (length > 10)
			return GetRuntimeParameterIndexFromHashMap(hash, cb, ptr, length);

		for (int i = 0; i < length; ++i)
		{
			var p = (AnimatorControllerParameterComponent*)ptr + i;
			if (p->hash == hash)
				return i;
		}
		return -1;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public unsafe int GetRuntimeParameterIndex(in BlobAssetReference<ParameterPerfectHashTableBlob> cb, in DynamicBuffer<AnimatorControllerParameterComponent> acpc)
	{
		return GetRuntimeParameterIndex(hash, cb, acpc.GetUnsafePtr(), acpc.Length);
	}
}
}
