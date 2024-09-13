using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Player
{
    struct PlayerAimComponent : IComponentData
    {
        public float3 aimDirection;
        public float3 aimLocation;
    }

    public class PlayerAimAuthoring : MonoBehaviour
    {
        public float3 aimLocation;
        private class PlayerAimAuthoringBaker : Baker<PlayerAimAuthoring>
        {
            public override void Bake(PlayerAimAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, new PlayerAimComponent { aimLocation = authoring.aimLocation });

            }
        }
    }
}