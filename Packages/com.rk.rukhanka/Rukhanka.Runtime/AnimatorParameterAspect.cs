using Unity.Entities;
using FixedStringName = Unity.Collections.FixedString512Bytes;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public readonly partial struct AnimatorParametersAspect: IAspect
{
	readonly RefRO<AnimatorControllerParameterIndexTableComponent> indexTable;
	readonly DynamicBuffer<AnimatorControllerParameterComponent> parametersArr;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public float GetFloatParameter(FastAnimatorParameter fp) => GetParameterValue(fp).floatValue;
	public int GetIntParameter(FastAnimatorParameter fp) => GetParameterValue(fp).intValue;
	public bool GetBoolParameter(FastAnimatorParameter fp) => GetParameterValue(fp).boolValue;
	public float GetFloatParameter(uint h) => GetParameterValue(h).floatValue;
	public int GetIntParameter(uint h) => GetParameterValue(h).intValue;
	public bool GetBoolParameter(uint h) => GetParameterValue(h).boolValue;
	public float GetFloatParameter(FixedStringName n) => GetParameterValue(n).floatValue;
	public int GetIntParameter(FixedStringName n) => GetParameterValue(n).intValue;
	public bool GetBoolParameter(FixedStringName n) => GetParameterValue(n).boolValue;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public ParameterValue GetParameterValue(FastAnimatorParameter fp)
	{
		fp.GetRuntimeParameterData(indexTable.ValueRO.seedTable, parametersArr, out var rv);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public ParameterValue GetParameterValue(uint parameterHash)
	{
		var fp = new FastAnimatorParameter()
		{
			hash = parameterHash,
			paramName = default,
		};
		return GetParameterValue(fp);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public ParameterValue GetParameterValue(FixedStringName parameterName)
	{
		var fp = new FastAnimatorParameter(parameterName);
		return GetParameterValue(fp);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void SetParameterValue(FastAnimatorParameter fp, ParameterValue value)
	{
		fp.SetRuntimeParameterData(indexTable.ValueRO.seedTable, parametersArr, value);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void SetTrigger(FastAnimatorParameter fp)
	{
		fp.SetRuntimeParameterData(indexTable.ValueRO.seedTable, parametersArr, new ParameterValue() { boolValue = true });
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void SetParameterValue(uint parameterHash, ParameterValue value)
	{
		var fp = new FastAnimatorParameter()
		{
			hash = parameterHash,
			paramName = default,
		};
		SetParameterValue(fp, value);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void SetParameterValue(FixedStringName parameterName, ParameterValue value)
	{
		var fp = new FastAnimatorParameter(parameterName);
		SetParameterValue(fp, value);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public bool HasParameter(FastAnimatorParameter fp)
	{
		return fp.GetRuntimeParameterIndex(indexTable.ValueRO.seedTable, parametersArr) != -1;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public bool HasParameter(uint parameterHash)
	{
		var fp = new FastAnimatorParameter()
		{
			hash = parameterHash,
			paramName = default,
		};
		return HasParameter(fp);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public bool HasParameter(FixedStringName parameterName)
	{
		var fp = new FastAnimatorParameter(parameterName);
		return HasParameter(fp);
	}
}
}
