using Collisions;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;

namespace Sandbox.Player
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial class PlayerCombatSystem : SystemBase
    {
        private static readonly int Vertical = Animator.StringToHash("Vertical");
        private static readonly int CombatAction = Animator.StringToHash("CombatAction");
        private static readonly int ComboAnimationPlayed = Animator.StringToHash("ComboAnimationPlayed");


        protected override void OnUpdate()
        {
            Entities.WithoutBurst().ForEach(
                (
                    PlayerCombat playerCombat,
                    Animator animator,
                    ref CheckedComponent checkedComponent,
                    ref LocalTransform localTransform,
                    ref InputControllerComponent inputController,
                    in LocalToWorld ltw,
                    in ApplyImpulseComponent applyImpulse
                ) =>
                {
                    var buttonXpressed = inputController.buttonX_Press; //kick types
                    var buttonXtap = inputController.buttonX_Tap; //punch types
                    //var bPressed = inputController.buttonB_SinglePress; // put back for general LD 50 change since no jump
                    //var bPressed = inputController.buttonB_held;
                    var leftBumperPressed = inputController.leftBumperPressed;
                    var leftBumperUp = inputController.leftBumperReleased;
                    var allowKick = buttonXpressed == true &&
                                    (math.abs(animator.GetFloat(Vertical)) < 2 || applyImpulse.Grounded == false);
                    var buttonXunPressed = inputController.buttonTimeX_UnPressed;
                    var comboBufferTimeMax = inputController.comboBufferTimeMax;
                    Debug.Log("Attack Stage " + checkedComponent.AttackStages);
                    //Debug.Log("Combo Counter " + checkedComponent.comboCounter);
                    
                    
                    if (buttonXtap && checkedComponent.comboCounter == 0)
                    {
                        checkedComponent.comboCounter = 1;
                        inputController.comboBufferTimeStart = 0;
                        inputController.comboBufferTimeEnd = 0; 
                        playerCombat.SelectMove(1);
                        checkedComponent.comboIndexPlaying = 1;
                        //checkedComponent.AttackStages = AttackStages.Action;
                    }
                    else if (buttonXtap && checkedComponent.AttackStages == AttackStages.Action &&
                             checkedComponent.comboCounter == 1)
                    {
                        //checkedComponent.AttackStages = AttackStages.Action;
                        checkedComponent.comboCounter = 2;
                    }
                    else if (buttonXtap && checkedComponent.AttackStages == AttackStages.Action &&
                             checkedComponent.comboCounter == 2)
                    {
                        //checkedComponent.AttackStages = AttackStages.Action;
                        checkedComponent.comboCounter = 3;
                    }

                    if (checkedComponent.AttackStages == AttackStages.End)
                    {
                        if (inputController.comboBufferTimeStart == 0)
                        {
                            inputController.comboBufferTimeStart = buttonXunPressed;
                        }
                        inputController.comboBufferTimeEnd = buttonXunPressed;
                        var timeSincePressed =
                            inputController.comboBufferTimeEnd - inputController.comboBufferTimeStart;
                        if (timeSincePressed > comboBufferTimeMax || timeSincePressed < 0)
                        {
                            checkedComponent.comboCounter = 0;
                        }
                    }
                    
                    if (checkedComponent.comboCounter > 1 && checkedComponent.AttackStages == AttackStages.End)
                    {
                        checkedComponent.comboIndexPlaying += 1;
                        playerCombat.SelectMove(1);
                    }
                    
                    
                    
                    // else if (buttonXtap && checkedComponent.AttackStages == AttackStages.Start &&
                    //          checkedComponent.comboAnimationPlayed == 2)
                    // {
                    //     checkedComponent.AttackStages = AttackStages.Action;
                    //     checkedComponent.comboAnimationPlayed = 3;
                    //     playerCombat.SelectMove(1);
                    // }
                    // else if (buttonXunPressed >= comboBufferTimeMax && checkedComponent.AttackStages == AttackStages.End)
                    // {
                    //     checkedComponent.comboAnimationPlayed = 0;
                    // }


                    
                    /*if (buttonXunPressed >= comboBufferTimeMax && checkedComponent.comboAnimationPlayed == 0)
                    {
                        checkedComponent.comboCounter = 0;
                    }
                    if (buttonXtap || checkedComponent is { comboCounter: > 1 })//punch
                    {
                        if (buttonXtap && buttonXunPressed < comboBufferTimeMax)
                        {
                            checkedComponent.comboCounter += 1;
                            if (checkedComponent.comboCounter == 1)
                            {
                                checkedComponent.comboAnimationPlayed = 1;
                                playerCombat.SelectMove(1);
                            }

                        }
                        else if (!buttonXtap && checkedComponent is { comboCounter: > 1, AttackStages: AttackStages.End })
                        {
                            checkedComponent.AttackStages = AttackStages.No;
                            checkedComponent.comboAnimationPlayed += 1;
                            if (checkedComponent.comboAnimationPlayed <= 3)
                            {
                                playerCombat.SelectMove(1);
                            }
                            else
                            {
                                checkedComponent.comboAnimationPlayed = 0;
                                checkedComponent.comboCounter = 0;
                            }
                        }
                    }*/
                    else if (allowKick) //kick
                    {
                        playerCombat.SelectMove(2);
                    }
                    else if (leftBumperPressed)
                    {
                        playerCombat.SelectMove(10);
                    }
                    else if (leftBumperUp)
                    {
                        animator.SetInteger(CombatAction, 0);
                    }

                    animator.SetInteger(ComboAnimationPlayed, checkedComponent.comboIndexPlaying);
                }
            ).Run();
        }
    }
}