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
                        Entity e,
                        LevelCompleteComponent levelCompleteComponent,
                        ref EnemyStateComponent enemyState,
                        ref CheckedComponent checkedComponent,
                        ref MatchupComponent matchupComponent
                    ) =>
                    {
                        if (SystemAPI.HasComponent<DeadComponent>(e) == false) return;
                        if (SystemAPI.GetComponent<DeadComponent>(e).isDead) return;
                        if (matchupComponent.closestOpponent == Entity.Null ||
                            matchupComponent.closestPlayerEntity == Entity.Null) return;


                        if (levelCompleteComponent.areaIndex > LevelManager.instance.currentLevelCompleted) return;
                        var enemyWeaponMovementComponent = SystemAPI.GetComponent<EnemyWeaponMovementComponent>(e);
                        var enemyBehaviourComponent = SystemAPI.GetComponent<EnemyBehaviourComponent>(e);
                        var weaponMovement = enemyWeaponMovementComponent.enabled;

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
                        var hasWeaponComponent = SystemAPI.HasComponent<WeaponComponent>(e);
                        if (hasWeaponComponent)
                        {
                            var weaponComponent = SystemAPI.GetComponent<WeaponComponent>(e);
                            if (weaponComponent.tooFarTooAttack)
                            {
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
                                    weaponComponent.IsFiring = 1; //hmm
                                }

                                SystemAPI.SetComponent(e, actorWeaponAim);
                                SystemAPI.SetComponent(e, weaponComponent);
                            }
                        }

                        MoveStates moveState;
                        if (distFromOpponent < chaseRange &&
                            distFromOpponent > stopRange) //weapon 1st option
                        {
                            moveState = MoveStates.Chase;
                        }
                        else if (distFromOpponent < chaseRange) //weapon 2nd
                        {
                            moveState = MoveStates.Idle;
                        }
                        else
                        {
                            moveState = MoveStates.Stopped;
                        }


                        float3 opponentTargetPosition = new float3();
                        float3 targetPosition = new float3();

                        var targetEntity = matchupComponent.targetEntity;
                        matchupComponent.isWaypointTarget = false;
                        opponentTargetPosition = transformGroup[targetEntity].Position;
                        matchupComponent.isWaypointTarget = false;
                        matchupComponent.aimTarget = transformGroup[targetEntity];
                        matchupComponent.opponentTargetPosition = opponentTargetPosition;
                    }
                ).Run();
        }
    }
}