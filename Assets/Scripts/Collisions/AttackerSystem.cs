using Sandbox.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Collisions
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    //[UpdateAfter(typeof(CollisionSystem))]
    [UpdateAfter(typeof(SphereRaycastSystem))]
    public partial class AttackerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            //var playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerComponent>()); //player 0
            //var playerList = playerQuery.ToEntityArray(Allocator.Temp);
            //if (playerList.Length == 0) return;


            Entities.ForEach(
                (
                    in CollisionComponent collisionComponent
                ) =>
                {

                    var entityA = collisionComponent.Character_entity; //ammo
                    var entityB = collisionComponent.Character_other_entity; //target
                    if (entityA == entityB) return;

                    var shooter = Entity.Null;
                    if (SystemAPI.HasComponent<TriggerComponent>(entityA))
                    {
                        shooter = SystemAPI.GetComponent<TriggerComponent>(entityA).ParentEntity;
                    }
                    
                    if (shooter != Entity.Null && SystemAPI.HasComponent<AmmoComponent>(entityA))
                    {

                        //Debug.Log("SHOOTER " + shooter);
                        var ammo =
                            SystemAPI.GetComponent<AmmoComponent>(entityA);
                        var ammoData =
                            SystemAPI.GetComponent<AmmoDataComponent>(entityA);
                        float damage = 0; //why using enemy data and not ammo data ?? change this
                        damage = ammoData.GameDamage; //overrides previous

                        if (ammoData.ChargeRequired && ammo.Charged == false)
                        {
                            damage = 0;
                        }

                        if (SystemAPI.HasComponent<DeadComponent>(entityB) == false ||
                            SystemAPI.GetComponent<DeadComponent>(entityB).isDead)
                        {
                            damage = 0;
                        }


                        ecb.AddComponent(shooter,
                            new DamageComponent
                            {
                                DamageLanded = damage, DamageReceived = 0, EntityCausingDamage = entityA,
                                LosingDamage = false
                            });


                        ecb.AddComponent(entityB,
                            new DamageComponent
                            {
                                DamageLanded = 0,
                                DamageReceived = damage,
                                StunLanded = damage,
                                EffectsIndex = ammo.effectIndex,
                                LosingDamage = false,
                                EntityCausingDamage = entityA
                            });
                        
                        //Debug.Log("DAMAGE " + damage);
                        
                        if (SystemAPI.HasComponent<SkillTreeComponent>(shooter))
                        {
                            var skill = SystemAPI.GetComponent<SkillTreeComponent>(shooter);
                            skill.CurrentLevelXp += damage;
                            SystemAPI.SetComponent(shooter, skill);
                        }
                        if (SystemAPI.HasComponent<ScoreComponent>(shooter) && damage != 0)
                        {
                            var scoreComponent = SystemAPI.GetComponent<ScoreComponent>(shooter);
                            scoreComponent.addBonus = 0;
                            if (!scoreComponent.zeroPoints)
                            {
                                scoreComponent.scoringAmmoEntity = ammo.ammoEntity;
                                scoreComponent.pointsScored = true;
                                //Debug.Log("Score ");
                                scoreComponent.combo = 1;
                                scoreComponent.scoredAgainstEntity = entityA;
                            }


                            SystemAPI.SetComponent(shooter, scoreComponent);
                        }

                        if (SystemAPI.HasComponent<ScoreComponent>(entityB) && damage >= 0)
                        {
                            var scoreComponent = SystemAPI.GetComponent<ScoreComponent>(entityB);
                            scoreComponent.combo = 0;
                            scoreComponent.streak = 0;
                            SystemAPI.SetComponent(entityB, scoreComponent);
                        }

                        ecb.SetComponent(entityA, ammo);
                    }
                }
            ).Run();

            //playerList.Dispose();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}