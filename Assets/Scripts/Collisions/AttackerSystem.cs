using Sandbox.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Collisions
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionSystem))]
    public partial class AttackerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerComponent>()); //player 0
            var playerList = playerQuery.ToEntityArray(Allocator.Temp);
            if (playerList.Length == 0) return;


            Entities.ForEach(
                (
                    in DeadComponent dead,
                    in CollisionComponent collisionComponent,
                    in Entity entity
                ) =>
                {
                    if (dead.isDead == true) return;

                    var typeA = collisionComponent.Part_entity;
                    var typeB = collisionComponent.Part_other_entity;
                    var entityA = collisionComponent.Character_entity;
                    var entityB = collisionComponent.Character_other_entity;
                    if (entityA == entityB && typeA != (int)TriggerType.Ammo && typeB != (int)TriggerType.Ammo) return;


                    if (typeB == (int)TriggerType.Ammo && SystemAPI.HasComponent<TriggerComponent>(entityA)
                                                       && SystemAPI
                                                           .HasComponent<
                                                               TriggerComponent>(
                                                               entityB)) //b is ammo so causes damage to entity
                    {
                        var shooter = Entity.Null;
                        shooter = SystemAPI.GetComponent<TriggerComponent>(entityB)
                            .ParentEntity;

                        if (shooter != Entity.Null && SystemAPI.HasComponent<AmmoComponent>(entityB))
                        {
                            var isEnemyShooter = SystemAPI.HasComponent<EnemyComponent>(shooter);
                            var target = SystemAPI.GetComponent<TriggerComponent>(entityA)
                                .ParentEntity;
                            var isEnemyTarget = SystemAPI.HasComponent<EnemyComponent>(target);
                            var ammo =
                                SystemAPI.GetComponent<AmmoComponent>(entityB);
                            var ammoData =
                                SystemAPI.GetComponent<AmmoDataComponent>(entityB);

                            float damage = 0; //why using enemy data and not ammo data ?? change this
                            damage = ammoData.GameDamage; //overrides previous
                            ammo.AmmoDead = true;

                            if (ammo.DamageCausedPreviously &&
                                ammo.frameSkipCounter > ammo.framesToSkip) //count in ammosystem
                            {
                                ammo.DamageCausedPreviously = false;
                                ammo.frameSkipCounter = 0;
                            }

                            if (ammo.DamageCausedPreviously || ammoData.ChargeRequired == true && ammo.Charged == false)
                            {
                                damage = 0;
                            }

                            if (SystemAPI.HasComponent<DeadComponent>(entityA) == false ||
                                SystemAPI.GetComponent<DeadComponent>(entityA).isDead)
                            {
                                damage = 0;
                            }

                            ammo.DamageCausedPreviously = true;


                            ecb.AddComponent(shooter,
                                new DamageComponent
                                {
                                    DamageLanded = damage, DamageReceived = 0, EntityCausingDamage = entityB,
                                    LosingDamage = false
                                });


                            ecb.AddComponent(entityA,
                                new DamageComponent
                                {
                                    DamageLanded = 0,
                                    DamageReceived = damage,
                                    StunLanded = damage,
                                    EffectsIndex = ammo.effectIndex,
                                    LosingDamage = false,
                                    EntityCausingDamage = entityB
                                });

                            if (SystemAPI.HasComponent<CheckedComponent>(entityA) && damage > 0)
                            {
                                var checkedComponent = SystemAPI.GetComponent<CheckedComponent>(entityA);
                                checkedComponent.scaleFactor *= checkedComponent.scale_multiplier;
                                SystemAPI.SetComponent(entityA, checkedComponent);
                                Debug.Log("SCALE " + checkedComponent.scaleFactor);
                            }


                            if (SystemAPI.HasComponent<SkillTreeComponent>(shooter))
                            {
                                var skill = SystemAPI.GetComponent<SkillTreeComponent>(shooter);
                                skill.CurrentLevelXp += damage;
                                SystemAPI.SetComponent(shooter, skill);
                            }


                            //var isPlayerShooter = SystemAPI.HasComponent<PlayerComponent>(shooter);
                            if (SystemAPI.HasComponent<ScoreComponent>(shooter) && damage != 0)
                            {
                                var scoreComponent = SystemAPI.GetComponent<ScoreComponent>(shooter);
                                scoreComponent.addBonus = 0;
                                if (!scoreComponent.zeroPoints)
                                {
                                    scoreComponent.scoringAmmoEntity = ammo.ammoEntity;
                                    scoreComponent.pointsScored = true;
                                    scoreComponent.combo = 1;
                                    scoreComponent.scoredAgainstEntity = entityA;
                                }


                                SystemAPI.SetComponent(shooter, scoreComponent);
                            }

                            if (SystemAPI.HasComponent<ScoreComponent>(entityA) && damage >= 0)
                            {
                                var scoreComponent = SystemAPI.GetComponent<ScoreComponent>(entityA);
                                scoreComponent.combo = 0;
                                scoreComponent.streak = 0;
                                SystemAPI.SetComponent(entityA, scoreComponent);
                            }

                            ecb.SetComponent(entityB, ammo);
                        }
                    }
                }
            ).Run();

            playerList.Dispose();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}