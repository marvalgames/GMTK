using Collisions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Sandbox.Player
{
    public partial struct CrosshairVisualSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CrosshairComponent>();
        }

        // Because this update accesses managed objects, it cannot be Burst compiled,
        // so we do not add the [BurstCompile] attribute.
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (transform, crosshair) in
                     SystemAPI.Query<RefRO<LocalTransform>, CrosshairInstance>())
            {
                var pos = (Vector3)transform.ValueRO.Position;
                //pos.y = 0;
                crosshair.crosshairInstance.GetComponent<Image>().transform.position = pos;
                Debug.Log("Pos " + pos);
            }
        }
    }
}