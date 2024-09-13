using Unity.Entities;
using Unity.Transforms;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Collisions
{
    public struct CrosshairComponent : IComponentData
    {
        public float raycastDistance;
        public float targetDelayCounter;
        public bool spawnCrosshair;
    }

    public class CrosshairClass : IComponentData
    {
        public GameObject crosshairPrefab;
    }

    public class CrosshairInstance : IComponentData
    {
        public GameObject crosshairInstance;
    }
    
    

    public class CrosshairComponentAuthoring : MonoBehaviour
    {
        public float raycastDistance = 140;
        public GameObject crosshairPrefab;


        void Start()
        {
            //var localTransform = LocalTransform.Identity;
            //manager.AddComponentData(entity, localTransform);
        }

        private class CrosshairComponentAuthoringBaker : Baker<CrosshairComponentAuthoring>
        {
            public override void Bake(CrosshairComponentAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(entity, new CrosshairComponent {raycastDistance = authoring.raycastDistance});
                AddComponentObject(entity, new CrosshairClass {crosshairPrefab = authoring.crosshairPrefab});                
            }
        }


  



    }
}