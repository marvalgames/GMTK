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
        private static readonly int JumpState = Animator.StringToHash("JumpState");

        [DeallocateOnJobCompletion] private NativeArray<Entity> PlayerEntities;

        private EntityQuery playerQuery;

        protected override void OnUpdate()
        {
            if (LevelManager.instance.endGame == true) return;
            var roleReversalDisabled =
                LevelManager.instance.levelSettings[LevelManager.instance.currentLevelCompleted].roleReversalMode ==
                RoleReversalMode.Off;
            
            Debug.Log("REVERSE " + roleReversalDisabled + LevelManager.instance.currentLevelCompleted);
            
            
            var transformGroup = SystemAPI.GetComponentLookup<LocalTransform>(false);

            //var weaponComponentGroup = SystemAPI.GetComponentLookup<WeaponComponent>(false);
            playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerComponent>());
            PlayerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            var playerIsFiring = false;
            var playerInShootingRange = false;
            for (var i = 0; i < PlayerEntities.Length; i++)
            {
                var e = PlayerEntities[i];
                var hasWeapon = SystemAPI.HasComponent<WeaponComponent>(e);
                if (hasWeapon &&  roleReversalDisabled == false)
                {
                    var player = SystemAPI.GetComponent<WeaponComponent>(e);
                    if (player is {IsFiring: 1, roleReversal: RoleReversalMode.On}) playerIsFiring = true;
                    if (player.roleReversal == RoleReversalMode.On) player.IsFiring = 0;
                    SystemAPI.SetComponent(e, player);
                }
            }


            Entities.WithoutBurst().WithNone<Pause>().WithAll<EnemyComponent>().WithAll<EnemyMeleeMovementComponent>()
                .WithAll<EnemyWeaponMovementComponent>().ForEach
                (
                    (
                        EnemyMove enemyMove,
                        Entity e,
                        ref EnemyStateComponent enemyState,
                        ref CheckedComponent checkedComponent,
                        ref LocalTransform localTransform,
                        ref MatchupComponent matchupComponent,
                        in AnimatorWeightsComponent animatorWeightsComponent
                    ) =>
                    {
                        if (SystemAPI.HasComponent<DeadComponent>(e) == false) return;
                        if (SystemAPI.GetComponent<DeadComponent>(e).isDead) return;
                        if (matchupComponent.targetEntity == Entity.Null) return;
                        var animator = enemyMove.anim;
                        var defensiveRole = SystemAPI.GetComponent<DefensiveStrategyComponent>(e).currentRole;
                        var enemyMeleeMovementComponent = SystemAPI.GetComponent<EnemyMeleeMovementComponent>(e);
                        var enemyWeaponMovementComponent = SystemAPI.GetComponent<EnemyWeaponMovementComponent>(e);
                        var enemyBehaviourComponent = SystemAPI.GetComponent<EnemyBehaviourComponent>(e);
                        var meleeMovement = enemyMeleeMovementComponent.enabled;
                        var weaponMovement = enemyWeaponMovementComponent.enabled;
                        enemyMove.speedMultiple = 1;
                        enemyState.selectMove = false;
                        var role = enemyMove.enemyRole;
                        if (role != EnemyRoles.None)
                        {
                            var enemyPosition = localTransform.Position;
                            var homePosition = enemyMove.originalPosition;
                            var stayHome = enemyBehaviourComponent.useDistanceFromStation;
                            var pl = matchupComponent.opponentTargetPosition;
                            var way = matchupComponent.wayPointTargetPosition;
                            var aimWeight = animatorWeightsComponent.aimWeight;
                            pl.y = 0;
                            var en = enemyPosition;
                            en.y = 0;
                            var distFromOpponent = math.distance(pl, en);
                            var distFromWaypoint = math.distance(way, en);
                            var distFromStation = math.distance(homePosition, enemyPosition);
                            var chaseRange = enemyBehaviourComponent.chaseRange;
                            var aggression = enemyBehaviourComponent.aggression;
                            var stopRange = enemyBehaviourComponent.stopRange;
                            var weaponRaised = WeaponMotion.None;
                            //if closer than weapon shooting stop range always melee if melee switch active 

                            if (distFromOpponent < stopRange && weaponMovement && enemyMeleeMovementComponent.switchUp)
                            {
                                weaponMovement = false;
                                meleeMovement = true;
                            }

                            var hasWeaponComponent = SystemAPI.HasComponent<WeaponComponent>(e);

                            if (hasWeaponComponent)
                            {
                                var weaponComponent = SystemAPI.GetComponent<WeaponComponent>(e);
                                var roleReversal = weaponComponent.roleReversal;
                                if (distFromOpponent > weaponComponent.roleReversalRangeMechanic || roleReversalDisabled)
                                {
                                    roleReversal = RoleReversalMode.Off;
                                    playerInShootingRange = true;
                                }
                                else if(!roleReversalDisabled)
                                {
                                    roleReversal = RoleReversalMode.On; //fix need original
                                }

                           
                                if (roleReversal == RoleReversalMode.On) weaponMovement = true;

                                if (SystemAPI.HasComponent<ActorWeaponAimComponent>(e))
                                {
                                    var actorWeaponAim = SystemAPI.GetComponent<ActorWeaponAimComponent>(e);


                                    if (playerIsFiring && roleReversal == RoleReversalMode.On || distFromOpponent <
                                        enemyWeaponMovementComponent.shootRangeDistance && weaponMovement &&
                                        roleReversal == RoleReversalMode.Off)
                                    {
                                        if (weaponComponent.IsFiring == 0)
                                        {
                                            weaponRaised = WeaponMotion.Started;
                                        }
                                        else
                                        {
                                            weaponRaised = WeaponMotion.Raised;
                                        }

                                        weaponComponent.IsFiring = 1;
                                        actorWeaponAim.weaponRaised = weaponRaised;
                                        SystemAPI.SetComponent(e, actorWeaponAim);
                                        SystemAPI.SetComponent(e, weaponComponent);
                                    }
                                }
                            }
                            //}

                            var backupZoneClose = enemyMeleeMovementComponent.combatStrikeDistanceZoneBegin;
                            var backupZoneFar = enemyMeleeMovementComponent.combatStrikeDistanceZoneEnd;

                            var strike = false;
                            //meleeMovement = false;
                            if (distFromOpponent < backupZoneClose && meleeMovement)
                            {
                                enemyMove.backup = true; //only time to turn on 
                                enemyMove.speedMultiple = distFromOpponent / backupZoneClose; //try zero
                                float n = Random.Range(0, 100);
                                if (n <= aggression && enemyMove.backupTimer <= 0 &&
                                    distFromOpponent > backupZoneClose / 2)
                                {
                                    enemyMove.backup = false; //only time to turn on 
                                    strike = true;
                                }
                            }

                            if (enemyMove.backup && distFromOpponent > backupZoneFar && meleeMovement)
                            {
                                enemyMove.backup = false; //only time to turn off
                                enemyMove.backupTimer = 0;
                            }
                            else if (distFromOpponent >= backupZoneClose && distFromOpponent <= backupZoneFar &&
                                     meleeMovement)
                            {
                                enemyMove.speedMultiple =
                                    math.sqrt((distFromOpponent - backupZoneClose) / (backupZoneFar - backupZoneClose));
                                //enemyMove.speedMultiple *= 2;
                                float n = Random.Range(0, 100);

                                if (n <= aggression * 1.0f && enemyMove.backupTimer <= 0)
                                {
                                    strike = true;
                                    enemyMove.backup = false; //try
                                }
                            }

                            var backup = enemyMove.backup;
                            if (stayHome && distFromStation > chaseRange) chaseRange = distFromStation;
                            MoveStates moveState;
                            if (checkedComponent.anyAttackStarted == false && backup == false && strike &&
                                distFromOpponent < chaseRange)
                            {
                                moveState = MoveStates.Default;
                                enemyState.selectMove = true;
                                animator.SetInteger(Zone, 3);
                            }
                            else if (checkedComponent.anyAttackStarted == false)
                            {
                                if (backup && distFromOpponent < chaseRange && meleeMovement)
                                {
                                    moveState = MoveStates.Default;
                                    animator.SetInteger(Zone, 2);
                                    enemyMove.SetBackup();
                                }
                                else if (distFromOpponent < enemyMeleeMovementComponent.combatRangeDistance &&
                                         distFromOpponent < chaseRange &&
                                         meleeMovement)
                                {
                                    moveState = MoveStates.Default;
                                    animator.SetInteger(Zone, 2);
                                }
                                else if (distFromOpponent < chaseRange &&
                                         distFromOpponent > stopRange) //weapon 1st option
                                {
                                    if (animator.GetComponent<EnemyMelee>())
                                        matchupComponent.currentStrikeDistanceAdjustment =
                                            1; //reset when out of strike range

                                    moveState = MoveStates.Chase;
                                    animator.SetInteger(Zone, 1);
                                }
                                else if (distFromOpponent < chaseRange) //weapon 2nd
                                {
                                    animator.SetInteger(Zone, 1);
                                    moveState = MoveStates.Idle;
                                }
                                else if (distFromOpponent >= chaseRange &&
                                         (role == EnemyRoles.Chase || defensiveRole == DefensiveRoles.Chase))
                                {
                                    animator.SetInteger(Zone, 1);
                                    moveState = MoveStates.Idle;
                                }
                                else if (distFromOpponent >= chaseRange && role == EnemyRoles.Patrol)
                                {
                                    animator.SetInteger(Zone, 1);
                                    moveState = MoveStates.Patrol;
                                    enemyMove.Patrol();
                                }
                                else
                                {
                                    animator.SetInteger(Zone, 1);
                                    moveState = MoveStates.Stopped;
                                }

                                //enemyMove.FaceWaypoint();
                                var lastState = enemyState.MoveState; //reads previous
                                enemyState.currentStateTimer += SystemAPI.Time.DeltaTime;
                                if (moveState == lastState || enemyState.MoveState == MoveStates.Default) //no change
                                {
                                    enemyState.MoveState = moveState;
                                }
                                else if (moveState != lastState &&
                                         enemyState.currentStateTimer > 1) //switched but after time required in role
                                {
                                    enemyState.MoveState = moveState;
                                    enemyState.currentStateTimer = 0;
                                }

                                var state = enemyState.MoveState;
                                float3 wayPointTargetPosition = new float3();
                                float3 opponentTargetPosition = new float3();
                                float3 targetPosition = new float3();
                                var targetEntity = matchupComponent.targetEntity;


                                matchupComponent.isWaypointTarget = false;
                                //if (targetEntity != Entity.Null)
                                //{

                                opponentTargetPosition = transformGroup[targetEntity].Position;


                                wayPointTargetPosition =
                                    enemyMove.wayPoints[enemyMove.currentWayPointIndex].targetPosition;

                                if (state == MoveStates.Patrol)
                                {
                                    matchupComponent.isWaypointTarget = true;
                                    targetPosition = wayPointTargetPosition;
                                }
                                else
                                {
                                    matchupComponent.isWaypointTarget = false;
                                    targetPosition = opponentTargetPosition;
                                    // }

                                    matchupComponent.aimTarget = transformGroup[targetEntity];
                                }

                                matchupComponent.wayPointTargetPosition = wayPointTargetPosition;
                                matchupComponent.opponentTargetPosition = opponentTargetPosition;
                                
                                enemyMove.UpdateEnemyMovement();
                                enemyMove.AnimationMovement(targetPosition);
                                enemyMove.FaceWaypoint();


                                //Debug.Log("DISTANCE " + Mathf.Round(distFromOpponent));
                            }
                        }
                    }
                ).Run();

            Debug.Log("IN RANGE " + playerInShootingRange);
            for (var i = 0; i < PlayerEntities.Length; i++)
            {
                var e = PlayerEntities[i];
                var hasWeapon = SystemAPI.HasComponent<WeaponComponent>(e);
                if (hasWeapon)
                {
                    var player = SystemAPI.GetComponent<WeaponComponent>(e);
                    player.roleReversal = playerInShootingRange ? RoleReversalMode.Off : RoleReversalMode.On;
                    SystemAPI.SetComponent(e, player);
                }
            }
        }
    }
}

