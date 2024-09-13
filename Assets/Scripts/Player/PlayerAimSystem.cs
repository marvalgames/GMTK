using Sandbox.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Player
{
    public partial struct PlayerAimSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (actorAim, playerAim, entity) in SystemAPI.Query<RefRO<ActorWeaponAimComponent>, RefRW<PlayerAimComponent>>().WithEntityAccess())
            {
                var aimTarget = actorAim.ValueRO.crosshairRaycastTarget;
                playerAim.ValueRW.aimDirection = math.normalize(aimTarget - playerAim.ValueRW.aimLocation);
            }

        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}