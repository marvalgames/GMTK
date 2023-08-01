using System.Collections;
using System.Collections.Generic;
using Sandbox.Player;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;


public partial struct EnemiesAttackEnableableComponentSystem : ISystem
{
    private EntityQuery playerQuery;


    public void OnCreate(ref SystemState state)
    {
        //state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var playerBuilder = new EntityQueryBuilder(Allocator.Temp);
        playerBuilder.WithAll<PlayerComponent, WeaponComponent>();
        playerQuery = state.GetEntityQuery(playerBuilder);
    }

    public void OnUpdate(ref SystemState system)
    {
        if (LevelManager.instance.endGame ||
            LevelManager.instance.currentLevelCompleted >= LevelManager.instance.totalLevels) return;

        var playerEntityList = playerQuery.ToEntityArray(Allocator.TempJob);
        if(playerEntityList.Length == 0) return;
        var enemiesAttackComponentGroup = SystemAPI.GetComponentLookup<EnemiesAttackComponent>();
        //var enemyComponentGroup = SystemAPI.GetComponentLookup<EnemyComponent>();
        var roleReversalMode = LevelManager.instance.levelSettings[LevelManager.instance.currentLevelCompleted]
            .roleReversalMode == RoleReversalMode.Toggle;
        var roleReversal = SystemAPI.GetComponent<WeaponComponent>(playerEntityList[0]).roleReversal ==
                           RoleReversalMode.Off; //p1 shoots normal and enemies do not attack each other

        var job = new EnemiesAttackEnableableJob()
        {
            //playerEntityList = playerEntityList,
            //enemyComponentGroup = enemyComponentGroup,
            enemiesAttackComponentGroup = enemiesAttackComponentGroup,
            reverseMode = roleReversalMode && roleReversal
        };
        job.Schedule();
    }
}

partial struct EnemiesAttackEnableableJob : IJobEntity
{
    //[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> playerEntityList;
    public ComponentLookup<EnemiesAttackComponent> enemiesAttackComponentGroup;
    public bool reverseMode;

    void Execute(Entity e, EnemyComponent enemyComponent)
    {
        if (enemiesAttackComponentGroup.HasComponent(e))
        {
            enemiesAttackComponentGroup.SetComponentEnabled(e, !reverseMode);
        }
    }
}