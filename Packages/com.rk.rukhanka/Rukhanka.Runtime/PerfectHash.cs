using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public class PerfectHash
{
	public static void CreateMinimalPerfectHash(in NativeArray<uint> inArr, out NativeList<int> seedValues, out NativeList<int> shuffleIndices)
	{
		var sz = inArr.Length;

		Span<int> buckets = stackalloc int[sz * sz];
		Span<int2> bucketsCount = stackalloc int2[sz];
		for (var l = 0; l < bucketsCount.Length; ++l)
		{
			bucketsCount[l] = new int2(l, 0);
		}

		for (int i = 0; i < sz; ++i)
		{
			var v = inArr[i];
			var h = InternalHashFunc(v, 0);
			var k = (int)(h % sz);
			buckets[k * sz + bucketsCount[k].y++] = i;
		}

		//	Simple bubble sort
		for (int i = 0; i < bucketsCount.Length - 1; ++i)
		{
			for (int l = 0; l < bucketsCount.Length - i - 1; ++l)
			{
				if (bucketsCount[l].y < bucketsCount[l + 1].y)
				{
					var t = bucketsCount[l];
					bucketsCount[l] = bucketsCount[l + 1];
					bucketsCount[l + 1] = t;
				}
			}
		}

		Span<int> freeList = stackalloc int[sz];
		seedValues = new NativeList<int>(sz, Allocator.Temp);
		shuffleIndices = new NativeList<int>(sz, Allocator.Temp);

		for (int i = 0; i < sz; ++i)
		{
			seedValues.Add(-seedValues.Length);
			shuffleIndices.Add(-1);
		}

		int bucketIndex = 0;
		for (; bucketIndex < bucketsCount.Length && bucketsCount[bucketIndex].y > 1; ++bucketIndex)
		{
			var seed = 1;
			var l = 0;
			var bucketInfo = bucketsCount[bucketIndex];

			//	Skip buckets with less than two items
			ResetFreeList(ref freeList);

			while (l < bucketInfo.y && seed < 256)
			{
				var itemIndex = buckets[bucketInfo.x * sz + l];
				var item = inArr[itemIndex];
				var slotIndex = (int)(InternalHashFunc(item, seed) % sz);
				if (freeList[slotIndex] >= 0 || shuffleIndices[slotIndex] >= 0)
				{
					ResetFreeList(ref freeList);
					l = 0;
					seed++;
				}
				else
				{
					freeList[slotIndex] = itemIndex;
					l++;
				}
			}

			seedValues[bucketInfo.x] = seed;
			for (int k = 0; k < freeList.Length; ++k)
			{
				var f = freeList[k];
				if (f < 0) continue;

				shuffleIndices[k] = f;
			}
		}

		//	Add buckets with one element
		for (int i = bucketIndex; i < bucketsCount.Length && bucketsCount[i].y > 0; ++i)
		{
			var bucketInfo = bucketsCount[i];
			var l = buckets[bucketInfo.x * sz];

			var freeSlotIndex = shuffleIndices.IndexOf(-1);
			seedValues[bucketInfo.x] = -freeSlotIndex - 1;
			shuffleIndices[freeSlotIndex] = l;
		}
	}

/////////////////////////////////////////////////////////////////////////////////

	unsafe public static int QueryPerfectHashTable(in NativeList<int> t, uint h)
	{
		var tablePtr = t.GetUnsafePtr();
		var seedTableAsArr = new Span<int>(tablePtr, t.Length);
		var paramIdx = QueryPerfectHashTable(seedTableAsArr, h);
		return paramIdx;
	}

/////////////////////////////////////////////////////////////////////////////////

	unsafe public static int QueryPerfectHashTable(ref BlobArray<int> t, uint h)
	{
		var tablePtr = t.GetUnsafePtr();
		var seedTableAsArr = new Span<int>(tablePtr, t.Length);
		var paramIdx = QueryPerfectHashTable(seedTableAsArr, h);
		return paramIdx;
	}

/////////////////////////////////////////////////////////////////////////////////

	public static int QueryPerfectHashTable(ReadOnlySpan<int> t, uint h)
	{
		var h0 = InternalHashFunc(h, 0);
		var i0 = (int)(h0 % t.Length);
		var d = t[i0];
		if (d < 0)
			return -d - 1;

		var h1 = InternalHashFunc(h, d);
		var i1 = (int)(h1 % t.Length);
		return i1;
	}

/////////////////////////////////////////////////////////////////////////////////

	static void ResetFreeList(ref Span<int> freeList)
	{
		for (int k = 0; k < freeList.Length; ++k)
			freeList[k] = -1;
	}

/////////////////////////////////////////////////////////////////////////////////

	static uint InternalHashFunc(uint v, int offset)
	{
		var p = v >> offset;
		var rv = p * 0x83B58237u + 0xA9D919BFu;
		return rv;
	}
}
}
