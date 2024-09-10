using Sandbox.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


public partial struct InstantiateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CharacterData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        state.Enabled = false;
        var instantiator = SystemAPI.GetSingleton<CharacterData>();

        var minPosX = -15.0f;
        var maxPosX = 15.0f;
        var minPosY = -895f;
        var maxPosY = -875f;

        // Prepare random generator
        Random random = new(123456);

        for (int i = 0; i < instantiator.NumBots; i++)
        {
            Entity instance = state.EntityManager.Instantiate(instantiator.BotPrefab);

            // Random position at x and z
            float3 position = new()
            {
                x = random.NextFloat(minPosX, maxPosX),
                y = 3,
                z = random.NextFloat(minPosY, maxPosY)
            };

            // Random euler rotation but only at y
            float3 euler = new()
            {
                y = random.NextFloat(0.0f, 360.0f)
            };
            quaternion rotation = quaternion.Euler(euler);

            // Set LocalTransform
            state.EntityManager.SetComponentData(instance,
                LocalTransform.FromPositionRotation(position, rotation));
        }
    }
}