using Collisions;
using Sandbox.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;


public partial struct InstantiateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<CharacterData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        var entity = SystemAPI.GetSingletonEntity<CharacterData>();
        var characterData = SystemAPI.GetSingleton<CharacterData>();
        // Prepare random generator
        Random random = new(123456);

        var characterDataBuffer = SystemAPI.GetBufferLookup<CharacterDataElement>(true);
        var prefabCount = characterDataBuffer[entity].Length;
        for (var i = 0; i < prefabCount; i++)
        {
            var bots = characterDataBuffer[entity][i].NumBots;
            var botPrefab = characterDataBuffer[entity][i].BotPrefab;
            var minPosX = characterDataBuffer[entity][i].minPosX;
            var maxPosX = characterDataBuffer[entity][i].maxPosX;
            var minPosZ = characterDataBuffer[entity][i].minPosZ;
            var maxPosZ = characterDataBuffer[entity][i].maxPosZ;
            for (var j = 0; j < bots; j++)
            {
                var instance = state.EntityManager.Instantiate(botPrefab);
                SystemAPI.SetComponent(instance, new CharacterIndexComponent { GroupIndex = i, BotIndex = j });
                // Random position at x and z

                var yLocation = characterData.yLocation;

                float3 position = new()
                {
                    x = random.NextFloat(minPosX, maxPosX),
                    y = yLocation, // add member along with scale and if random rotation
                    z = random.NextFloat(minPosZ, maxPosZ)
                };

                if (characterData.UseTerrainHeight && SystemAPI.HasComponent<RaycastComponent>(instance))
                {
                    var raycastComponent = SystemAPI.GetComponent<RaycastComponent>(instance);
                    yLocation = RaycastUtilities.ExecuteRaycast(SystemAPI.GetSingleton<PhysicsWorldSingleton>(),
                        position, raycastComponent);
                    position.y = yLocation + .05f;

                }


                // Random euler rotation but only at y //remove later 
                float3 euler = new()
                {
                    y = random.NextFloat(0.0f, 360.0f)
                };
                quaternion rotation = quaternion.Euler(euler);

                // Set LocalTransform
                //SCALE has no effect since it's an entity
                SystemAPI.SetComponent(instance,
                    LocalTransform.FromPositionRotation(position, rotation));
            }
        }
    }
}