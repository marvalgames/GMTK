using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Sandbox.Player
{
    public class AnimationAuthoring : MonoBehaviour
    {
        [Header("Bots")] public int NumBots = 10;

        [Header("Prefabs")] public GameObject BotPrefab;
        public GameObject BotAnimatedPrefabGO;
        
        public List<CharacterDataClass> CharacterDataObject = new List<CharacterDataClass>();
        

        class Baker : Baker<AnimationAuthoring>
        {
            public override void Bake(AnimationAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                var animatedPrefab = authoring.BotAnimatedPrefabGO is not null;
                AddComponent(entity, new CharacterData()
                {
                    NumBots = authoring.NumBots,
                    BotPrefab = GetEntity(authoring.BotPrefab, TransformUsageFlags.Dynamic),
                    HasAnimatedPrefab = animatedPrefab
                });

                var buffer = AddBuffer<CharacterDataElement>(entity);
                
                for (var i = 0; i < authoring.CharacterDataObject.Count; i++)
                {
                    var characterData = authoring.CharacterDataObject[i];
                    var characterDataElement = new CharacterDataElement
                    {
                        BotPrefab = GetEntity(characterData.BotPrefab, TransformUsageFlags.Dynamic),
                        NumBots = characterData.NumBots
                    };
                    
                    buffer.Add(characterDataElement);
                }
                
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

    [System.Serializable]
    public class CharacterDataClass
    {
        public GameObject BotPrefab;
        public int NumBots;
    }
    public struct CharacterDataElement : IBufferElementData
    {
        public int NumBots;
        public Entity BotPrefab;
    }

    public struct CharacterData : IComponentData
    {
        public int NumBots;
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