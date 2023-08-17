using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System;
using Unity.Jobs;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
partial class BoneVisualizationSystem
{
[BurstCompile]
struct PrepareRenderDataJob: IJobParallelForBatch
{
    [ReadOnly]
    public NativeList<Entity> entityArr;
    [ReadOnly]
    public NativeList<RigDefinitionComponent> rigDefArr;
    [ReadOnly]
    public NativeList<UnsafeList<BoneTransform>> boneTransforms;
    [ReadOnly]
    public ComponentLookup<BoneVisualizationComponent> boneVisComponentLookup;
    public DebugConfigurationComponent debugConfig;

    [WriteOnly]
    public NativeList<BoneGPUData>.ParallelWriter boneGPUData;

/////////////////////////////////////////////////////////////////////////////////

    BoneTransform CalculateBoneWorldPose(int boneIndex, BlobAssetReference<RigDefinitionBlob> rdb, in UnsafeList<BoneTransform> boneTransforms)
    {
        var rv = BoneTransform.Identity();

        while (boneIndex >= 0)
        {
            var bt = boneTransforms[boneIndex];
            rv = BoneTransform.Multiply(bt, rv);
            boneIndex = rdb.Value.bones[boneIndex].parentBoneIndex;
        }
        return rv;
    }

/////////////////////////////////////////////////////////////////////////////////

    public void Execute(int startIndex, int count)
    {
        for (int i = startIndex; i < startIndex + count; ++i)
        {
            var rd = rigDefArr[i];
            var bt = boneTransforms[i];
            var e = entityArr[i];

            if (!boneVisComponentLookup.TryGetComponent(e, out var bvc))
            {
                if (!debugConfig.visualizeAllRigs) continue;

                bvc = new BoneVisualizationComponent()
                {
                    colorLines = debugConfig.colorLines,
                    colorTri = debugConfig.colorTri
                };
            }

            Span<float3> worldPoses = stackalloc float3[bt.Length];

            //	Make absolute transforms
            for (int l = 0; l < bt.Length; ++l)
            {
                var p = CalculateBoneWorldPose(l, rd.rigBlob, bt);
                worldPoses[l] = p.pos;
            }
            
			var rootBoneId = -1;
            for (int l = 0; l < bt.Length; ++l)
            {
                var bgd = new BoneGPUData();
                ref var rb = ref rd.rigBlob.Value.bones[l];

				if (rb.parentBoneIndex < 0)
                    continue;

                bgd.pos0 = worldPoses[l];
                bgd.pos1 = worldPoses[rb.parentBoneIndex];
                bgd.colorTri = bvc.colorTri;
                bgd.colorLines = bvc.colorLines;

				if (math.any(math.abs(bgd.pos0 - bgd.pos1)))
					boneGPUData.AddNoResize(bgd);
            }
        }
    }
}
}
}
