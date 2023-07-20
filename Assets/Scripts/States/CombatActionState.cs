using UnityEngine;

public class CombatActionState : StateMachineBehaviour
{
    private static readonly int CombatAction = Animator.StringToHash("CombatAction");

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) 
    {
        //change file to be like player combat where using checkComponent
        //animator.GetComponent<EnemyMelee>().attackStarted = true;
        animator.GetComponent<EnemyMelee>().StartAttackUpdateCheckComponent();
        animator.GetComponent<EnemyMelee>().StopAgent();
        animator.GetComponent<EnemyMelee>().StartAimIK();
        animator.GetComponent<EnemyMelee>().StartIK();



    }
    
    override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Implement code that processes and affects root motion
        animator.GetComponent<EnemyMelee>().StartMotionUpdateCheckComponent();

    }


    
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<EnemyMelee>().StartAgent();
        animator.GetComponent<EnemyMelee>().StopAimIK();
        animator.GetComponent<EnemyMelee>().StopIK();
        animator.GetComponent<EnemyMelee>().EndAttack();


    }

  
}
