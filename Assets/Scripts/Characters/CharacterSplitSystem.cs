using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateBefore(typeof(CleanupSystem))]
public partial struct SplitSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<SplitComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EndSimulationEntityCommandBufferSystem.Singleton commandBufferSystem =
            SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();


        // Schedule job
        SplitJob splitJob = new()
        {
            commandBuffer = commandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged),
        };
        state.Dependency = splitJob.Schedule(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    private partial struct SplitJob : IJobEntity
    {
        public EntityCommandBuffer commandBuffer;

        public void Execute(ref SplitComponent splitComponent, ref LocalTransform localTransform, in DamageComponent damage)
        {
            Debug.Log("SPLIT " + damage.DamageReceived);
            if (splitComponent.split || damage.DamageReceived == 0) return;

            Entity instance = this.commandBuffer.Instantiate(splitComponent.splitPrefab);

            // Random position at x and z
            var position = localTransform.Position;

            var rotation = localTransform.Rotation;

            // Set LocalTransform
            this.commandBuffer.SetComponent(instance,
                LocalTransform.FromPositionRotation(position, rotation));
            splitComponent.split = true;
        }

        // We set this to true so it will no longer be processed on the next frame
    }
}

