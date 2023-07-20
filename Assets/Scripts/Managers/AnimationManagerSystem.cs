using Enemy;
using Unity.Entities;
using UnityEngine;

namespace Managers
{
    [UpdateAfter(typeof(EnemyDefenseMoves))]
    [RequireMatchingQueriesForUpdate]
    public partial class AnimationManagerSystem : SystemBase
    {
        private static readonly int EvadeStrike = Animator.StringToHash("EvadeStrike");

        protected override void OnUpdate()
        {
            Entities.WithoutBurst().ForEach((Animator animator, ref AnimationManagerComponentData animationManagerData) =>
                {
                    animator.SetBool(EvadeStrike, animationManagerData.evadeStrike);

                    if (animationManagerData.evadeStrike)
                    {
                        //animationManagerData.evadeStrike = false;
                    }
                }
            ).Run();
    
    
        }
    }
}