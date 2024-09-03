using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct LockAxisSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // The fixed Y value you want to lock entities to
        float fixedY = 3.0f;

        
        //foreach (var (prefab, entity) in
           //      SystemAPI.Query<PlayerMoveGameObjectClass>().WithEntityAccess())
            
        // Process all entities with a Position component
        foreach (var (transform, entity) in SystemAPI.Query<RefRW<LocalTransform>>().WithAny<EnemyComponent>().WithEntityAccess())
        {
            float3 position = transform.ValueRO.Position;

            // Lock the Y-axis
            position.y = fixedY;
            //Debug.Log("FIXED  " + fixedY);

            // Update the entity's position
            transform.ValueRW.Position = position;
        }
    }
}


