using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


public partial struct InstantiateSystem : ISystem {
    [BurstCompile]
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<CharacterSpawnComponent>();
    }
 
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        EndSimulationEntityCommandBufferSystem.Singleton commandBufferSystem = 
            SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
         
        // Schedule job
        InstantiateJob instantiateJob = new() {
            commandBuffer = commandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged),
            minPosX = -15.0f,
            maxPosX = 15.0f,
            minPosY = -895f,
            maxPosY = -875f
        };
        state.Dependency = instantiateJob.Schedule(state.Dependency);
    }
 
    [BurstCompile]
    public void OnDestroy(ref SystemState state) {
    }
 
    [BurstCompile]
    private partial struct InstantiateJob : IJobEntity {
        public EntityCommandBuffer commandBuffer;
        public float minPosX;
        public float maxPosX;
        public float minPosY;
        public float maxPosY;
         
        public void Execute(ref CharacterSpawnComponent instantiator) {
            if (instantiator.instantiated) {
                // Already instantiated
                return;
            }
 
            // Prepare random generator
            Random random = new(123456);
 
            for (int i = 0; i < instantiator.instanceCount; i++) {
                Entity instance = this.commandBuffer.Instantiate(instantiator.entityPrefab);
                 
                // Random position at x and z
                float3 position = new() {
                    x = random.NextFloat(this.minPosX, this.maxPosX),
                    y = 3,
                    z = random.NextFloat(this.minPosY, this.maxPosY)
                };
                 
                // Random euler rotation but only at y
                float3 euler = new() {
                    y = random.NextFloat(0.0f, 360.0f)
                };
                quaternion rotation = quaternion.Euler(euler);
                 
                // Set LocalTransform
                this.commandBuffer.SetComponent(instance, 
                    LocalTransform.FromPositionRotation(position, rotation));
            }
 
            // We set this to true so it will no longer be processed on the next frame
            instantiator.instantiated = true;
        }
    }
}


