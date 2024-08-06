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
                mb.weaponRaised = actorWeaponAimComponent.weaponRaised == WeaponMotion.Started ||
                                  actorWeaponAimComponent.weaponRaised == WeaponMotion.Raised;


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
            //Debug.Log("MB AIM DIR " + mb.aimDir);
            var direction = math.normalize(mb.aimDir);
            direction.y = 0;


            var forwardVector = math.forward(localTransform.Rotation);
            forwardVector.y = 0;
            var degrees = Vector3.SignedAngle(forwardVector, direction, Vector3.up);
            var turnSpeed = mb.turnSpeed;

            if (math.abs(degrees - playerWeaponAimComponent.angleToTarget) < .03)
            {
                degrees = 0;
            }

            playerWeaponAimComponent.angleToTarget = degrees;
            var turningValue = math.sign(degrees);
            var slerpDampTime = mb.rotateSpeed;


            if (playerWeaponAimComponent.aimMode == false)
            {
                turningValue = 0;
                slerpDampTime = 0;
            }

            var targetRotation = quaternion.LookRotationSafe(direction, math.up()); //always face xHair
            localTransform.Rotation = math.slerp(localTransform.Rotation, targetRotation.value,
                slerpDampTime * SystemAPI.Time.DeltaTime);
            mb.animator.SetFloat(Turning, turningValue, turnSpeed, SystemAPI.Time.DeltaTime);
        }).Run();
    }
}


/*
public partial class PlayerCombatAimSystemLateUpdate : SystemBase
{
    private static readonly int Turning = Animator.StringToHash("Turning");

    protected override void OnUpdate()
    {
        Entities.WithoutBurst().WithAny<DeadComponent>().WithNone<Pause>().ForEach((
            PlayerCombat mb,
            ref ActorWeaponAimComponent playerCombatAimComponent,
            in MatchupComponent matchupComponent,
            in Entity playerEntity
        ) =>
        {
            /*var localTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
            var targetEntity = matchupComponent.closestEnemyEntity;
            if (targetEntity == Entity.Null || !playerCombatAimComponent.combatMode ||
                playerCombatAimComponent.aimMode) return;
            var playerPosition = localTransform.Position;
            var targetPosition = SystemAPI.GetComponent<LocalTransform>(targetEntity).Position;
            var direction = math.normalize(targetPosition - playerPosition);
            var slerpDampTime = mb.rotateSpeed;
            var targetRotation = quaternion.LookRotationSafe(direction, math.up());//always face player
            var playerRotation = SystemAPI.GetComponent<LocalTransform>(playerEntity).Rotation;
            playerRotation = math.slerp(playerRotation, targetRotation.value,
                slerpDampTime * SystemAPI.Time.DeltaTime);
            localTransform.Rotation = playerRotation;#1#
            Debug.Log("direction");
            //mb.animator.SetFloat(Turning, turningValue, turnSpeed, SystemAPI.Time.DeltaTime);
            
        }).Run();
    }
}
*/

/*
var bossXZ = new float3(bossLocalTransform.Position.x, 0, bossLocalTransform.Position.z);
var playerXZ = new float3(playerMove.Position.x, 0, playerMove.Position.z);
var direction = math.normalize(playerXZ - bossXZ);
var dist = math.distance(bossXZ, playerXZ);
if (dist < 1) direction = -math.forward();//????????????????? 1
var targetRotation = quaternion.LookRotationSafe(direction, math.up());//always face player
var slerpDampTime = bossMovementComponent.RotateSpeed;
var rotation = SystemAPI.GetComponent<LocalTransform>(enemyE).Rotation;
rotation = math.slerp(rotation, targetRotation.value, slerpDampTime * SystemAPI.Time.DeltaTime);
var localTransform = SystemAPI.GetComponent<LocalTransform>(enemyE);
localTransform.Rotation = rotation;
SystemAPI.SetComponent(enemyE, localTransform);*/
