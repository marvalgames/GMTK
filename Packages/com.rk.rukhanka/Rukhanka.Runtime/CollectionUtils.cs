
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public static class CollectionUtils
{
	public static unsafe NativeArray<T> AsArray<T>(this NativeSlice<T> v) where T: unmanaged
	{
		var ptr = v.GetUnsafePtr();
		var rv = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, v.Length, Allocator.None);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public static unsafe UnsafeList<T> AsUnsafeList<T>(this NativeSlice<T> v) where T: unmanaged
	{
		var ptr = (T*)v.GetUnsafePtr();
		var rv = new UnsafeList<T>(ptr, v.Length);
		return rv;
	}
}
}
