using Sandbox.Player;
using System.Diagnostics;
using Collisions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;


namespace Enemy
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial class EnemyAmmoHandlerSystem : SystemBase
    {
        //BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

        protected override void OnUpdate()
        {
            if (LevelManager.instance == null) return;

            if (LevelManager.instance.endGame == true) return;

            var dt = SystemAPI.Time.DeltaTime;

            var commandBuffer = new EntityCommandBuffer(Allocator.Persistent);

            var ammoGroup = GetComponentLookup<AmmoComponent>(false);
            Entities.WithNone<Pause>().WithAll<EnemyComponent>().ForEach(
                (
                    Entity entity,
                    ref AmmoManagerComponent ammoManagerComponent,
                    in DefensiveStrategyComponent defensiveStrategyComponent,
                    in AnimatorWeightsComponent animatorWeightsComponent,
                    in LocalTransform enemyLocalTransform
                ) =>
                {
                    var playerE = defensiveStrategyComponent.closestPlayerEntity;

                    if (!SystemAPI.HasComponent<WeaponComponent>(entity)) return;
                    var enemyWeapon = SystemAPI.GetComponent<WeaponComponent>(entity);

                    var primaryAmmoEntity = enemyWeapon.PrimaryAmmo;
                    var ammoDataComponent = SystemAPI.GetComponent<AmmoDataComponent>(primaryAmmoEntity);
                    var rate = ammoDataComponent.GameRate;
                    var strength = ammoDataComponent.GameStrength;
                    if (enemyWeapon.ChangeAmmoStats > 0)
                    {
                        strength = strength * (100 - enemyWeapon.ChangeAmmoStats * 2) / 100;
                        if (strength <= 0) strength = 0;
                    }

                    //if (enemyWeapon is { IsFiring: 1, Duration: 0 } )
                    if (enemyWeapon is { IsFiring: 1, Duration: 0 } &&
                        animatorWeightsComponent.aimWeight > enemyWeapon.animTriggerWeight)
                    {
                        enemyWeapon.Duration += dt;
                        //enemyWeapon.IsFiring = 0;
                        //var bossLocalTransform = SystemAPI.GetComponent<LocalTransform>(entity);
                        var e = commandBuffer.Instantiate(enemyWeapon.PrimaryAmmo);
                        //var ammoPosition = enemyWeapon.AmmoStartTransform.Position; //use bone mb transform
                        var ammoStartTransform =
                            LocalTransform.FromPosition(enemyWeapon.AmmoStartTransform
                                .Position); //use bone mb transform
                        var playerLocalTransform = SystemAPI.GetComponent<LocalTransform>(playerE).Position;
                        var ammoRotation = enemyWeapon.AmmoStartTransform.Rotation;
                        var velocity = new PhysicsVelocity();

                        //var ammoTransform = new LocalTransform{Position = ammoPosition, Rotation = ammoRotation};

                        var ammoStartXZ = new float3(ammoStartTransform.Position.x, ammoStartTransform.Position.y,
                            ammoStartTransform.Position.z);
                        //
                        var yOffset = 1;//make member later
                        var playerStartXZ = new float3(playerLocalTransform.x, playerLocalTransform.y + yOffset,
                            playerLocalTransform.z);
                        var forward = math.forward(ammoRotation);
                        if (math.distancesq(ammoStartXZ, playerStartXZ) > 0)
                        {
                            var direction = math.normalize(playerStartXZ - ammoStartXZ);
                            var targetRotation = quaternion.LookRotationSafe(direction, math.up()); //always face player
                            forward = direction;
                        }

                        velocity.Linear = math.normalize(forward) * strength;
                        ammoStartTransform.Rotation = ammoRotation;


                        ammoManagerComponent.playSound = true;
                        ammoDataComponent.Shooter = entity;
                        commandBuffer.SetComponent(e, ammoDataComponent);
                        commandBuffer.SetComponent(e, new TriggerComponent
                            { Type = (int)TriggerType.Ammo, ParentEntity = entity, Entity = e, Active = true });
                        commandBuffer.SetComponent(e, ammoStartTransform);
                        commandBuffer.SetComponent(e, velocity);
                        //actorWeaponAimComponent.weaponRaised = WeaponMotion.Started;
                    }
                    else if (enemyWeapon is { IsFiring: 1, Duration: > 0 })
                    {
                        //actorWeaponAimComponent.weaponRaised = WeaponMotion.Raised;
                        enemyWeapon.Duration += dt;
                        if ((enemyWeapon.Duration > rate) && (enemyWeapon.IsFiring == 1))
                        {
                            //actorWeaponAimComponent.weaponRaised = WeaponMotion.Lowering;
                            enemyWeapon.Duration = 0;
                            enemyWeapon.IsFiring = 0;
                        }
                    }


                    commandBuffer.SetComponent(entity, enemyWeapon);
                }
            ).Run();
            commandBuffer.Playback(EntityManager);
            commandBuffer.Dispose();
        }
    }
}