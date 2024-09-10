using Sandbox.Player;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


struct SimpleFollowComponent : IComponentData
{
    
}



public class SimpleFollowAuthoring : MonoBehaviour
{
    
    private Entity playerEntity;
    private EntityManager entityManager;
    public GameObject player;
    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        // Find the player entity (can be done via tags or queries)
        var entityQuery = entityManager.CreateEntityQuery(typeof(PlayerComponent));
        playerEntity = entityQuery.GetSingletonEntity();
    }

    void LateUpdate()
    {
        if (entityManager.Exists(playerEntity))
        {
            // Get the player's position from ECS
            LocalTransform playerPosition = entityManager.GetComponentData<LocalTransform>(playerEntity);

            // Update the proxy GameObject position to follow the player entity
            player.transform.position = playerPosition.Position;
            player.transform.rotation = playerPosition.Rotation;
        }
    }

}
