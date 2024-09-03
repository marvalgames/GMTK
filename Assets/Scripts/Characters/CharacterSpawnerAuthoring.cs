using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct CharacterSpawnComponent : IComponentData
{
    public Entity entityPrefab;
    public  float3 entityPosition;
    public bool instantiated;
    public int instanceCount;
    public bool lockY;
}

public class CharacterSpawnerAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int instanceCount;
    [SerializeField] private bool lockY = true;

    private class CharacterSpawnerAuthoringBaker : Baker<CharacterSpawnerAuthoring>
    {
        public override void Bake(CharacterSpawnerAuthoring authoring)
        {
            var e = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic);
            CharacterSpawnComponent characterSpawnComponent = new CharacterSpawnComponent {entityPrefab = e, entityPosition = authoring.transform.position, instanceCount = authoring.instanceCount, lockY = authoring.lockY} ;
            AddComponent(GetEntity(TransformUsageFlags.None), characterSpawnComponent);
            
            
        }
    }
}