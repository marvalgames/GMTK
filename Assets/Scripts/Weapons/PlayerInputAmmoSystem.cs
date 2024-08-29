using Sandbox.Player;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial class PlayerInputAmmoSystem : SystemBase
{
    private static readonly int WeaponRaised = Animator.StringToHash("WeaponRaised");

    protected override void OnUpdate()
    {
        //var check = new NativeArray<int>(1, Allocator.TempJob);
        Entities.WithoutBurst().ForEach((
            Entity e,
            ref WeaponComponent gunComponent, ref ActorWeaponAimComponent playerWeaponAimComponent,
            in InputControllerComponent inputController) =>
        {
            //lt mapped to 1 on keyboard when LT is not used for shooting - if not map to left mouse
            if (inputController.leftTriggerPressed)
            {
                playerWeaponAimComponent.aimMode = !playerWeaponAimComponent.aimMode;
            }


            var rtPressed = inputController.rightTriggerPressed;
            var aimMode = playerWeaponAimComponent.aimMode;
            if (gunComponent.roleReversal == RoleReversalMode.On)
            {
                aimMode = true;
                playerWeaponAimComponent.aimDisabled = true;
            }

            if (aimMode && rtPressed)
            {
                gunComponent.IsFiring = 1;
                if (SystemAPI.HasComponent<ScoreComponent>(e))
                {
                    var score = SystemAPI.GetComponent<ScoreComponent>(e);
                    score.startShotValue = score.score;
                    score.zeroPoints = false; //also in ammosystem but thats for normal not GMTK 23
                    SystemAPI.SetComponent(e, score);
                }

            }

        }).Run();
    }

}