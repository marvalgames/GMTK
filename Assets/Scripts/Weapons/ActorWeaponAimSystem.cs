using Rewired;
using Sandbox.Player;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


[UpdateInGroup(typeof(TransformSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial class EnemyWeaponAimSystemLateUpdate : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.WithoutBurst().WithAny<DeadComponent>()
            .ForEach((in EnemyWeaponAim mb, in ActorWeaponAimComponent actorWeaponAimComponent) =>
            {
                mb.weaponRaised = actorWeaponAimComponent.weaponRaised == WeaponMotion.Started;
                mb.LateUpdateSystem();
            }).Run();
    }
}

public partial class PlayerWeaponAimSystemLateUpdate : SystemBase
{
    private static readonly int Turning = Animator.StringToHash("Turning");

    protected override void OnUpdate()
    {
        Entities.WithoutBurst().WithAny<DeadComponent>().WithNone<Pause>().ForEach((
            PlayerWeaponAim mb, ref ActorWeaponAimComponent playerWeaponAimComponent,
            ref LocalTransform localTransform) =>
        {
            if (mb.Player.controllers.GetLastActiveController() == null || playerWeaponAimComponent.combatMode) return;
            mb.LateUpdateSystem(playerWeaponAimComponent.weaponRaised);
            playerWeaponAimComponent.aimDirection = mb.aimDir;
            var direction = math.normalize(mb.aimDir);
            direction.y = 0;
            
            var targetRotation = quaternion.LookRotationSafe(direction, math.up()); //always face xHair
            localTransform.Rotation = targetRotation;
        }).Run();
    }
}
