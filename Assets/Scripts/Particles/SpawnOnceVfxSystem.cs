using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VFX;


public struct EntityPrefabComponent : IComponentData
{
    public Entity moveVfxSystem;
}



[RequireMatchingQueriesForUpdate]
public partial struct SpawnOnceVfxSystem :  ISystem // Deprecated
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var transformGroup = SystemAPI.GetComponentLookup<LocalTransform>();


        var job = new SpawnOnceVfxJob()
        {
            ecb = ecb,
            transformGroup = transformGroup
        };
        job.ScheduleParallel();
    }
    
    [BurstCompile]
    partial struct SpawnOnceVfxJob : IJobEntity
    {
        public EntityCommandBuffer ecb;
        public ComponentLookup<LocalTransform> transformGroup;

        void Execute(ref SpawnOnceVfxComponent spawnOnceVfxComponent)
        {
            var e = spawnOnceVfxComponent.spawnOnceVfxEntity;
            //Debug.Log("system " + e);

            if (spawnOnceVfxComponent.spawned < 1 && e != Entity.Null)
            {
                spawnOnceVfxComponent.spawned += 1;
                //ecb.Instantiate(e);
                var entity = ecb.Instantiate(e);

                var tr = transformGroup[e];
                tr.Position = spawnOnceVfxComponent.spawnPosition;
                var ro = tr.Rotation;
                tr.Rotation = ro;
                ecb.SetComponent(entity, tr);
                ecb.AddComponent<VfxComponentTag>(e);//can also have this on the prefab to start with as needed
            }
        }

     
            
        
        
    }
    
    
    
}



public partial struct InstantiatePrefabSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Get all Entities that have the component with the Entity reference
        foreach (var prefab in
                 SystemAPI.Query<RefRO<EntityPrefabComponent>>())
        {
            // Instantiate the prefab Entity
            //var instance = ecb.Instantiate(prefab.ValueRO.moveVfxSystem);
            // Note: the returned instance is only relevant when used in the ECB
            // as the entity is not created in the EntityManager until ECB.Playback
            //ecb.AddComponent<VfxComponentTag>(instance);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
















