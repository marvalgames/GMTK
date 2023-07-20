using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;

namespace Sandbox.Player
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerJumpSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial class PlayerDashSystem : SystemBase
    {
        private static readonly int dash = Animator.StringToHash("Dash");


        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var dt = SystemAPI.Time.DeltaTime;

            Entities.WithoutBurst().ForEach(
                (
                    Entity e,
                    ref LocalTransform t,
                    ref PlayerDashComponent playerDash,
                    in InputControllerComponent inputController,
                    in ApplyImpulseComponent apply,
                    in LocalToWorld ltw,
                    in Animator animator,
                    in PlayerDashClass player

                ) =>
                {
                    if (playerDash.active == false) return;
                    var audioSource = player.audioSource;
                  
                    if (playerDash.DelayTimeTicker > 0)
                    {
                        playerDash.DelayTimeTicker -= dt;
                        return;
                    }
                    else
                    {
                        playerDash.DelayTimeTicker = 0;
                    }

                    if (playerDash.DashTimeTicker == 0 && playerDash.DelayTimeTicker <= 0)
                    {
                        var bPressed = inputController.buttonB_DoublePress; // put back for general LD 50 change since no jump
                        //bool rtPressed = inputController.buttonA_Pressed;
                        if (bPressed)
                        {
                            //t.Value += ltw.Forward * dt * playerDash.power;
                            //pv.Linear += ltw.Forward * playerDash.power;
                            playerDash.DashTimeTicker += dt;
                            if (animator.GetInteger(dash) == 0)
                            {
                                animator.SetInteger(dash, 1);
                                //playerDash.Collider = SystemAPI.GetComponent<PhysicsCollider>(e);
                                playerDash.InDash = true;
                                if (playerDash.uses > 0)
                                {
                                    playerDash.uses -= 1;
                                }
                                else
                                {
                                    playerDash.active = false;
                                }

                            }

                            if (audioSource != null)
                            {
                                if (player.audioSource.clip)
                                {
                                    if (audioSource.isPlaying == false)
                                    {
                                        audioSource.clip = player.audioSource.clip;
                                        audioSource.Play();

                                    }

                                }
                            }

                            if (player.ps)
                            {
                                if (player.ps.isPlaying == false)
                                {
                                    player.ps.transform.SetParent(player.transform);
                                    player.ps.Play(true);
                                }
                            }



                        }
                    }
                    else if (playerDash.DashTimeTicker < playerDash.dashTime && animator.speed > 0 && 
                             SystemAPI.HasComponent<PhysicsVelocity>(e) && SystemAPI.HasComponent<PhysicsMass>(e))
                    {
                        //var pv = new PhysicsVelocity();
                        var pv = SystemAPI.GetComponent<PhysicsVelocity>(e);
                        var pm = SystemAPI.GetComponent<PhysicsMass>(e);
                        playerDash.InDash = true;

                        //Debug.Log("fw");
                        //t.Value += ltw.Forward * dt * playerDash.power;
                        var force = ltw.Forward * playerDash.power;
                        //pv.Linear = ltw.Forward * playerDash.power;
                        pv.ApplyLinearImpulse(pm, force);
                        playerDash.DashTimeTicker += dt;
                        SystemAPI.SetComponent(e, pv);
                    }
                    else if (playerDash.DashTimeTicker >= playerDash.dashTime)
                    {
                        //playerDash.colliderAdded = true;
                        //playerDash.colliderRemoved = false;
                        //if(SystemAPI.HasComponent<PhysicsCollider>(e) == false)
                        //{
                            //var collider = SystemAPI.GetComponent<PhysicsCollider>(e);
                            //playerDash.box = collider.Value;
                        //}    
                        playerDash.DashTimeTicker = 0;
                        playerDash.DelayTimeTicker = playerDash.delayTime;
                        animator.SetInteger(dash, 0);
                        playerDash.InDash = false;
                        if (audioSource != null) audioSource.Stop();
                        if (player.ps != null) player.ps.Stop();

                    }


                }
            ).Run();

            ecb.Playback(EntityManager);
            ecb.Dispose();


        }



    }
}


