using Sandbox.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace Collisions
{
    public struct CheckedComponent : IComponentData
    {
        public AttackStages AttackStages;
        public bool anyDefenseStarted;
        public bool anyAttackStarted; //weapon or mellee
        public bool attackFirstFrame;
        public bool attackCompleted;
        public bool attackInProgress;
        public bool hitTriggered; //on during frame only
        public bool hitLanded; //on until end of animation / move
        public bool hitReceived;
        public int totalHits;
        public int totalAttempts;
        public TriggerType primaryTrigger;
        public int animationIndex;
        public int comboIndexPlaying;
        //public int comboCounter;
        public bool comboButtonClicked;
        public float scaleFactor;
    }

    public struct CollisionComponent : IComponentData
    {
        public int Part_entity;
        public int Part_other_entity;
        public Entity Character_entity;
        public Entity Character_other_entity;
        public bool isMelee;
        public bool isDefenseMove;
        public bool isHit;
    }

    public struct PowerTriggerComponent : IComponentData
    {
        public int TriggerType;
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerMoveSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial class CollisionSystem : SystemBase
    {
        EndFixedStepSimulationEntityCommandBufferSystem m_ecbSystem;

        protected override void OnCreate()
        {
            m_ecbSystem = World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var collisionJob = new CollisionJob
            {
                Ecb = m_ecbSystem.CreateCommandBuffer(),
                triggerGroup = GetComponentLookup<TriggerComponent>(true),
                healthGroup = GetComponentLookup<HealthComponent>(true),
                ammoGroup = GetComponentLookup<AmmoComponent>(false),
                checkGroup = GetComponentLookup<CheckedComponent>(true),
                bossGroup = GetComponentLookup<BossComponent>(true)
            };

            Dependency = collisionJob.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), Dependency);
            Dependency.Complete();
        }

        [BurstCompile]
        struct CollisionJob : ICollisionEventsJob
        {
            //[ReadOnly] public PhysicsWorld physicsWorld;
            [ReadOnly] public ComponentLookup<TriggerComponent> triggerGroup;
            [ReadOnly] public ComponentLookup<HealthComponent> healthGroup;
            [ReadOnly] public ComponentLookup<CheckedComponent> checkGroup;
            [ReadOnly] public ComponentLookup<BossComponent> bossGroup;
            public ComponentLookup<AmmoComponent> ammoGroup;
            public EntityCommandBuffer Ecb;

            public void Execute(CollisionEvent ev) // this is never called
            {
                var a = ev.EntityA;
                var b = ev.EntityB;
                if (triggerGroup.HasComponent(a) == false || triggerGroup.HasComponent(b) == false) return;
                var triggerComponentA = triggerGroup[a];
                var triggerComponentB = triggerGroup[b];

                var chA = triggerComponentA.ParentEntity;
                var chB = triggerComponentB.ParentEntity;
                var typeA = triggerComponentA.Type;
                var typeB = triggerComponentB.Type;
                
                if (chA == chB && typeA != (int)TriggerType.Ammo && typeB != (int)TriggerType.Ammo) return; ////?????
                
                if (triggerComponentA.Type == (int)TriggerType.Ground ||
                    triggerComponentB.Type == (int)TriggerType.Ground)
                {
                    return;
                }

 
                var primaryTriggerA = TriggerType.None;
                var primaryTriggerB = TriggerType.None;

             

                var ammoA = typeB is (int)TriggerType.Base or (int)TriggerType.Head or (int)TriggerType.Body &&
                            (typeA == (int)TriggerType.Ammo);

                var ammoB = typeA is (int)TriggerType.Base or (int)TriggerType.Head or (int)TriggerType.Body &&
                            (typeB == (int)TriggerType.Ammo);

                var ammoBlockedA = (typeB == (int)TriggerType.Blocks) &&
                                   (typeA == (int)TriggerType.Ammo);

                var ammoBlockedB = (typeA == (int)TriggerType.Blocks) &&
                                   (typeB == (int)TriggerType.Ammo);

                var effectA = typeB is (int)TriggerType.Base or (int)TriggerType.Head or (int)TriggerType.Body &&
                              (typeA == (int)TriggerType.Particle);

                var effectB = typeA is (int)TriggerType.Base or (int)TriggerType.Head or (int)TriggerType.Body &&
                              (typeB == (int)TriggerType.Particle);


                if (ammoBlockedA)
                {
                    var ammoComponent = ammoGroup[triggerComponentA.Entity];
                    ammoComponent.Charged = true;
                    ammoGroup[triggerComponentA.Entity] = ammoComponent;
                }

                if (ammoBlockedB)
                {
                    var ammoComponent = ammoGroup[triggerComponentB.Entity];
                    ammoComponent.Charged = true;
                    ammoGroup[triggerComponentB.Entity] = ammoComponent;
                }

                if (ammoA || effectA)
                {
                    //coll component part other always ammo ?

                    var collisionComponent =
                        new CollisionComponent()
                        {
                            Part_entity = triggerComponentB.Type,
                            Part_other_entity = triggerComponentA.Type,
                            Character_entity = triggerComponentB.ParentEntity, //actor hit by ammo
                            Character_other_entity = triggerComponentA.Entity,
                            isMelee = false,
                            isHit = false
                        };
                    

                    Ecb.AddComponent(triggerComponentA.ParentEntity, collisionComponent);
                }
                else if (ammoB || effectB)
                {

                    var collisionComponent =
                        new CollisionComponent()
                        {
                            Part_entity = triggerComponentA.Type,
                            Part_other_entity = triggerComponentB.Type,
                            Character_entity = triggerComponentA.ParentEntity,
                            Character_other_entity = triggerComponentB.Entity,
                            isMelee = false,
                            isHit = false
                        };
                    

                    Ecb.AddComponent(triggerComponentB.ParentEntity, collisionComponent);
                }
            
            }
        }
    } // System
}