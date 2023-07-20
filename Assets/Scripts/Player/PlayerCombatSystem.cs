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
                    var bPressed = inputController.buttonB_SinglePress; // put back for general LD 50 change since no jump
                    var allowKick = buttonXpressed == true && (math.abs(animator.GetFloat(Vertical)) < 2 || applyImpulse.Grounded == false);
                    if (buttonXtap)//punch
                    {
                        playerCombat.SelectMove(1);
                    }
                    else if (allowKick)//kick
                    {
                        playerCombat.SelectMove(2);
                    }
                    else if (bPressed)
                    {
                        playerCombat.SelectMove(10);
                    }
                }
            ).Run();
        }
    }
}



