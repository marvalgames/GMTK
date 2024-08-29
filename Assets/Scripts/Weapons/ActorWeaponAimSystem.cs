using Rewired;
using Sandbox.Player;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;



public partial class PlayerWeaponAimSystemLateUpdate : SystemBase
{

    protected override void OnUpdate()
    {
        Entities.WithoutBurst().WithAny<DeadComponent>().WithNone<Pause>().ForEach((
            PlayerWeaponAim mb, ref ActorWeaponAimComponent playerWeaponAimComponent,
            ref LocalTransform localTransform) =>
        {
            if (mb.Player.controllers.GetLastActiveController() == null) return;
            playerWeaponAimComponent.aimDirection = mb.aimDir;
            var direction = math.normalize(mb.aimDir);
            direction.y = 0;
            var targetRotation = quaternion.LookRotationSafe(direction, math.up()); //always face xHair
            localTransform.Rotation = targetRotation;
        }).Run();
    }
}
