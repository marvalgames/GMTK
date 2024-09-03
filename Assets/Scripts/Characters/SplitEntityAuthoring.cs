using Unity.Entities;
using UnityEngine;


public struct SplitComponent : IComponentData, IEnableableComponent
{
    public bool split;
    public Entity splitPrefab;
}

public class SplitEntityAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject splitPrefab;
    private class SplitEntityAuthoringBaker : Baker<SplitEntityAuthoring>
    {
        public override void Bake(SplitEntityAuthoring authoring)
        {
            var e = GetEntity(authoring.splitPrefab, TransformUsageFlags.Dynamic);
            AddComponent(e, new SplitComponent { splitPrefab = e,  split = false });

        }
        
    }
}