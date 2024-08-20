using Collisions;
using Sandbox.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Enemy
{
    [RequireMatchingQueriesForUpdate]
    public partial class EnemyMovementSystem : SystemBase
    {
        private static readonly int Zone = Animator.StringToHash("Zone");

        [DeallocateOnJobCompletion] private NativeArray<Entity> PlayerEntities;

        private EntityQuery playerQuery;

        protected override void OnUpdate()
        {
            if (LevelManager.instance.endGame ||
                LevelManager.instance.currentLevelCompleted >= LevelManager.instance.totalLevels) return;


            var roleReversalDisabled =
                LevelManager.instance.levelSettings[LevelManager.instance.currentLevelCompleted].roleReversalMode ==
                RoleReversalMode.Off;

            var toggleEnabled =
                LevelManager.instance.levelSettings[LevelManager.instance.currentLevelCompleted].roleReversalMode ==
                RoleReversalMode.Toggle;


            var transformGroup = SystemAPI.GetComponentLookup<LocalTransform>(false);
            playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerComponent>());
            PlayerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            var playerIsFiring = false;
            var playerInShootingRange = true;
            var enemyInShootingRange = true;
            for (var i = 0; i < PlayerEntities.Length; i++)
            {
                var e = PlayerEntities[i];
                var hasWeapon = SystemAPI.HasComponent<WeaponComponent>(e);
                if (hasWeapon && roleReversalDisabled == false)
                {
                    var player = SystemAPI.GetComponent<WeaponComponent>(e);
                    if (player is { IsFiring: 1, roleReversal: RoleReversalMode.On }) playerIsFiring = true;
                    if (player.roleReversal == RoleReversalMode.On) player.IsFiring = 0;
                    SystemAPI.SetComponent(e, player);
                }
            }


            Entities.WithoutBurst().WithNone<Pause>().WithAll<EnemyComponent>().ForEach
            (
                (
                    Entity e,
                    ref WeaponComponent weaponComponent,
                    in MatchupComponent matchupComponent,
                    in LevelCompleteComponent levelCompleteComponent,
                    in LocalTransform localTransform
                ) =>
                {
                    if (SystemAPI.HasComponent<DeadComponent>(e) == false) return;
                    if (SystemAPI.GetComponent<DeadComponent>(e).isDead) return;
                    if (matchupComponent.targetEntity == Entity.Null ||
                        matchupComponent.closestPlayerEntity == Entity.Null) return;
                    if (levelCompleteComponent.areaIndex > LevelManager.instance.currentLevelCompleted) return;
                    weaponComponent.tooFarTooAttack = false;


                    var enemyPosition = localTransform.Position;
                    //var pl = matchupComponent.opponentTargetPosition;
                    var pl = SystemAPI.GetComponent<LocalTransform>(matchupComponent.closestPlayerEntity).Position;
                    pl.y = 0;
                    var en = enemyPosition;
                    en.y = 0;
                    var distFromOpponent = math.distance(pl, en);

                    if (distFromOpponent <= weaponComponent.roleReversalRangeMechanic &&
                        toggleEnabled)

                    {
                        enemyInShootingRange = false;
                    }

                    var multiplier = 2.0f;

                    if (distFromOpponent > weaponComponent.roleReversalRangeMechanic * multiplier &&
                        !roleReversalDisabled)

                    {
                        weaponComponent.tooFarTooAttack = true;
                        //Debug.Log("TOO FAR LOOP " + " " + e);
                    }

//                    weaponComponent.tooFarTooAttack = false;
                }
            ).Run();


            Entities.WithoutBurst().WithNone<Pause>().WithAll<EnemyComponent>().WithAll<EnemyMeleeMovementComponent>()
                .WithAll<EnemyWeaponMovementComponent>().ForEach
                (
                    (
                        EnemyMove enemyMove,
                        Entity e,
                        LevelCompleteComponent levelCompleteComponent,
                        ref EnemyStateComponent enemyState,
                        ref CheckedComponent checkedComponent,
                        ref MatchupComponent matchupComponent,
                        in AnimatorWeightsComponent animatorWeightsComponent
                    ) =>
                    {
                        if (SystemAPI.HasComponent<DeadComponent>(e) == false) return;
                        if (SystemAPI.GetComponent<DeadComponent>(e).isDead) return;
                        if (matchupComponent.closestOpponent == Entity.Null ||
                            matchupComponent.closestPlayerEntity == Entity.Null) return;
                        

                        if (levelCompleteComponent.areaIndex > LevelManager.instance.currentLevelCompleted) return;
                        var animator = enemyMove.anim;
                        var enemyWeaponMovementComponent = SystemAPI.GetComponent<EnemyWeaponMovementComponent>(e);
                        var enemyBehaviourComponent = SystemAPI.GetComponent<EnemyBehaviourComponent>(e);
                        var weaponMovement = enemyWeaponMovementComponent.enabled;

                        enemyMove.speedMultiple = 1;
                        enemyState.selectMove = false;
                        var role = enemyMove.enemyRole;

                        if (role != EnemyRoles.None)
                        {
                            var enemyPosition = SystemAPI.GetComponent<LocalTransform>(e).Position;
                            var closestPlayerEntity = matchupComponent.closestPlayerEntity;
                            var closestPlayerPosition =
                                SystemAPI.GetComponent<LocalTransform>(closestPlayerEntity).Position;
                            closestPlayerPosition.y = 0;

                            var closestOpponentEntity = matchupComponent.closestOpponent;
                            var closestOpponentPosition =
                                SystemAPI.GetComponent<LocalTransform>(closestOpponentEntity).Position;
                            closestOpponentPosition.y = 0;


                            var en = enemyPosition;
                            en.y = 0;
                            var distFromOpponent = math.distance(closestOpponentPosition, en);
                            var chaseRange = enemyBehaviourComponent.chaseRange;
                            var stopRange = enemyBehaviourComponent.stopRange;
                            var weaponRaised = WeaponMotion.None;
                            //if closer than weapon shooting stop range always melee if melee switch active 
                            var hasWeaponComponent = SystemAPI.HasComponent<WeaponComponent>(e);

                            if (hasWeaponComponent)
                            {
                                var weaponComponent = SystemAPI.GetComponent<WeaponComponent>(e);
                                if (weaponComponent.tooFarTooAttack)
                                {
                                    //Debug.Log("TOO FAR");
                                    weaponMovement = false;
                                }
                            }


                            if (hasWeaponComponent)
                            {
                                var weaponComponent = SystemAPI.GetComponent<WeaponComponent>(e);


                                if (SystemAPI.HasComponent<ActorWeaponAimComponent>(e))
                                {
                                    var actorWeaponAim = SystemAPI.GetComponent<ActorWeaponAimComponent>(e);
                                    if (playerIsFiring &&
                                        !weaponComponent.tooFarTooAttack || distFromOpponent <
                                        enemyWeaponMovementComponent.shootRangeDistance && weaponMovement &&
                                        enemyInShootingRange)
                                    {
                                        if (weaponComponent.firingStage == FiringStage.None)
                                        {
                                            weaponRaised = WeaponMotion.Started;
                                        }
                                        else if (weaponComponent is { IsFiring: 1, firingStage: FiringStage.Start })
                                        {
                                            weaponComponent.firingStage = FiringStage.Update;
                                            weaponRaised = WeaponMotion.Started;
                                        }


                                        weaponComponent.IsFiring = 1; //hmm

                                    }

                                    actorWeaponAim.weaponRaised = weaponRaised;
                                    SystemAPI.SetComponent(e, actorWeaponAim);
                                    SystemAPI.SetComponent(e, weaponComponent);
                                }
                            }
                            MoveStates moveState;
                            if (distFromOpponent < chaseRange &&
                                distFromOpponent > stopRange) //weapon 1st option
                            {
                                moveState = MoveStates.Chase;
                                animator.SetInteger(Zone, 1);
                            }
                            else if (distFromOpponent < chaseRange) //weapon 2nd
                            {
                                animator.SetInteger(Zone, 1);
                                moveState = MoveStates.Idle;
                            }
                            else
                            {
                                animator.SetInteger(Zone, 1);
                                moveState = MoveStates.Stopped;
                            }

                            // var lastState = enemyState.MoveState; //reads previous
                            // enemyState.currentStateTimer += SystemAPI.Time.DeltaTime;
                            // if (moveState == lastState || enemyState.MoveState == MoveStates.Default) //no change
                            // {
                            //     enemyState.MoveState = moveState;
                            // }
                            // else if (moveState != lastState &&
                            //          enemyState.currentStateTimer > 1) //switched but after time required in role
                            // {
                            //     enemyState.MoveState = moveState;
                            //     enemyState.currentStateTimer = 0;
                            // }
                            //
                            // enemyState.MoveState = MoveStates.Chase;


                            float3 opponentTargetPosition = new float3();
                            float3 targetPosition = new float3();

                            var targetEntity = matchupComponent.targetEntity;
                            matchupComponent.isWaypointTarget = false;
                            opponentTargetPosition = transformGroup[targetEntity].Position;


                            matchupComponent.isWaypointTarget = false;
                            targetPosition = opponentTargetPosition;
                            matchupComponent.aimTarget = transformGroup[targetEntity];


                            matchupComponent.opponentTargetPosition = opponentTargetPosition;

                            enemyMove.UpdateEnemyMovement();
                            enemyMove.AnimationMovement(targetPosition);
                            enemyMove.FaceWaypoint();
                        }

                        var time = SystemAPI.Time.DeltaTime;
                        checkedComponent.scaleFactor = checkedComponent.scaleFactor - time * .005f;
                        if (checkedComponent.scaleFactor < 1) checkedComponent.scaleFactor = 1;


                        var enemyTransform =
                            SystemAPI.GetComponent<LocalTransform>(e);
                        enemyTransform.Scale = checkedComponent.scaleFactor;
                        SystemAPI.SetComponent(e, enemyTransform);

                    }
                ).Run();


            
            
        }
    }
}