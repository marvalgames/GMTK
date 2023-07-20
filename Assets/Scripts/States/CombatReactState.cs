using UnityEngine;

public class CombatReactState : StateMachineBehaviour
{
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetInteger("HitReact", 0);
        animator.SetInteger("Dash", 0);
        //Debug.Log("hit");
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetComponent<Rigidbody>() == false) return;
        animator.GetComponent<Rigidbody>().isKinematic = false;
    }

}
