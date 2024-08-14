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
        private static readonly int ComboCounter = Animator.StringToHash("ComboCounter");


        protected override void OnUpdate()
        {
            Entities.WithoutBurst().ForEach(
                (
                    PlayerCombat playerCombat,
                    Animator animator,
                    ref CheckedComponent checkedComponent,
                    ref LocalTransform localTransform,
                    in LocalToWorld ltw,
                    in InputControllerComponent inputController,
                    in ApplyImpulseComponent applyImpulse
                ) =>
                {
                    var buttonXpressed = inputController.buttonX_Press;//kick types
                    var buttonXtap = inputController.buttonX_Tap;//punch types
                    //var bPressed = inputController.buttonB_SinglePress; // put back for general LD 50 change since no jump
                    //var bPressed = inputController.buttonB_held;
                    var leftBumperPressed = inputController.leftBumperPressed;
                    var leftBumperUp = inputController.leftBumperReleased;
                    var allowKick = buttonXpressed == true && (math.abs(animator.GetFloat(Vertical)) < 2 || applyImpulse.Grounded == false);
                    var buttonXunPressed = inputController.buttonTimeX_UnPressed;
                    var comboBufferTimeMax = inputController.comboBufferTimeMax;
                    if (buttonXunPressed >= comboBufferTimeMax)
                    {
                        checkedComponent.comboCounter = 0;
                    }
                    if (buttonXtap)//punch
                    {
                        if (buttonXunPressed < comboBufferTimeMax)
                        {
                            checkedComponent.comboCounter = checkedComponent.comboCounter == 4
                                ? checkedComponent.comboCounter = 0 : checkedComponent.comboCounter += 1;

                        }
                        playerCombat.SelectMove(1);
                    }
                    else if (allowKick)//kick
                    {
                        playerCombat.SelectMove(2);
                    }
                    else if (leftBumperPressed)
                    {
                        playerCombat.SelectMove(10);
                    }
                    else if(leftBumperUp)
                    {
                        animator.SetInteger(CombatAction, 0);
                    }
                    
                    //Debug.Log("COMBO COUNTER " + checkedComponent.comboCounter);
                    animator.SetInteger(ComboCounter, checkedComponent.comboCounter);

                    
                }
            ).Run();
        }
    }
}



