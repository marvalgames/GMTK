using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Sandbox.Player
{
    public partial struct AnimationSystem : ISystem
    {
        private bool isInitialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CharacterData>();
            state.RequireForUpdate<Bot>();
        }

        // Because this update accesses managed objects, it cannot be Burst compiled,
        // so we do not add the [BurstCompile] attribute.
        public void OnUpdate(ref SystemState state)
        {
            if (!isInitialized)
            {
                isInitialized = true;

                var configEntity = SystemAPI.GetSingletonEntity<CharacterData>();
                if (state.EntityManager.HasComponent<CharacterDataManaged>(configEntity))
                {
                    var configManaged = state.EntityManager.GetComponentObject<CharacterDataManaged>(configEntity);

                    var ecb = new EntityCommandBuffer(Allocator.Temp);

                    foreach (var (transform, entity) in
                             SystemAPI.Query<RefRO<LocalTransform>>()
                                 .WithAll<Bot>()
                                 .WithEntityAccess())
                    {
                        var botAnimation = new BotAnimation();
                        var go = GameObject.Instantiate(configManaged.BotAnimatedPrefabGO);
                        botAnimation.AnimatedGO = go;
                        go.transform.localPosition = (Vector3)transform.ValueRO.Position;
                        ecb.AddComponent(entity, botAnimation);

                        // disable rendering
                        ecb.RemoveComponent<MaterialMeshInfo>(entity);
                    }

                    ecb.Playback(state.EntityManager);
                }
            }

            var isMovingId = Animator.StringToHash("IsMoving");
            var vertical = Animator.StringToHash("Vertical");


            foreach (var (bot, transform, botAnimation) in
                     SystemAPI.Query<RefRO<Bot>, RefRO<LocalTransform>, BotAnimation>())
            {
                var pos = (Vector3)transform.ValueRO.Position;
                pos.y = 0;
                botAnimation.AnimatedGO.transform.localPosition = pos;
                botAnimation.AnimatedGO.transform.localRotation = (Quaternion)transform.ValueRO.Rotation;

                var animator = botAnimation.AnimatedGO.GetComponent<Animator>();
                //animator.SetBool(isMovingId, bot.ValueRO.IsMoving());
                animator.SetFloat(vertical, 1);
            }
        }
    }
}