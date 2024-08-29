using Unity.Entities;

namespace Enemy
{
    [RequireMatchingQueriesForUpdate]
    public partial class BossAttackManagerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var positionBuffer = GetBufferLookup<BossWaypointBufferElement>(true);
            var ammoList = GetBufferLookup<BossAmmoListBuffer>(true);
            Entities.WithoutBurst().ForEach((Entity enemyE,
                in BossMovementComponent bossMovementComponent) =>
            {
                var targetPointBuffer = positionBuffer[enemyE];
                if (targetPointBuffer.Length <= 0 || bossMovementComponent.WayPointReached == false)
                    return;
                var weaponIndex = targetPointBuffer[bossMovementComponent.CurrentIndex].weaponListIndex;
                if (bossMovementComponent.CurrentIndex <= 0) return;
            }).Run();
        }
    }
}


