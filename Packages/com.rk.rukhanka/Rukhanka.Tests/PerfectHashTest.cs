using NUnit.Framework;
using System;
using Unity.Collections;
using Random = Unity.Mathematics.Random;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{ 
public class PerfectHashTest
{
    [Test]
    public void InternalHashFuncTest()
    {
		var rng = new Random((uint)DateTime.Now.Millisecond);

		var hashArr = new NativeList<uint>(Allocator.Temp);
		for (int i = 0; i < 100; ++i)
		{
			hashArr.Add(rng.NextUInt());
		};
		PerfectHash.CreateMinimalPerfectHash(hashArr.AsArray(), out var seedArr, out var shuffleArr);

		var shuffleVerifyArr = new NativeArray<int>(shuffleArr.Length, Allocator.Temp);
		for (int i = 0; i < shuffleArr.Length; ++i)
		{
			var sv = shuffleArr[i];
			Assert.IsTrue(shuffleVerifyArr[sv] == 0);
			shuffleVerifyArr[sv] = 1;
		}

		for (int i = 0; i < hashArr.Length; ++i)
		{
			var iHash = hashArr[i];
			var l = PerfectHash.QueryPerfectHashTable(seedArr, iHash);
			Assert.IsTrue(hashArr[shuffleArr[l]] == iHash);
		}
    }
}
}
