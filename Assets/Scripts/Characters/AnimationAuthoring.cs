using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Sandbox.Player
{
    public class AnimationAuthoring : MonoBehaviour
    {
        [Header("Bots")] public int NumBots = 10;
        public float BotMoveSpeed = 3; // units per second

        [Header("Prefabs")] public GameObject BotPrefab;
        public GameObject BotAnimatedPrefabGO;

        class Baker : Baker<AnimationAuthoring>
        {
            public override void Bake(AnimationAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                var animatedPrefab = authoring.BotAnimatedPrefabGO is not null;
                AddComponent(entity, new CharacterData()
                {
                    NumBots = authoring.NumBots,
                    BotMoveSpeed = authoring.BotMoveSpeed,
                    BotPrefab = GetEntity(authoring.BotPrefab, TransformUsageFlags.Dynamic),
                    HasAnimatedPrefab = animatedPrefab
                });

                if (animatedPrefab)
                {
                    var configManaged = new CharacterDataManaged
                    {
                        BotAnimatedPrefabGO = authoring.BotAnimatedPrefabGO
                    };
                    AddComponentObject(entity, configManaged);
                }
            }
        }
    }

    public struct CharacterData : IComponentData
    {
        public int NumBots;
        public float BotMoveSpeed;
        public Entity BotPrefab;
        public bool HasAnimatedPrefab;
    }

    public class CharacterDataManaged : IComponentData
    {
        public GameObject BotAnimatedPrefabGO;
    }

    public class BotAnimation : IComponentData
    {
        public GameObject AnimatedGO; // the GO that is rendered and animated
    }
}