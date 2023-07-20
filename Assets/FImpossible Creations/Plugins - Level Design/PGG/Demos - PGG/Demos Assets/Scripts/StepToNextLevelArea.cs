using UnityEngine;

namespace FIMSpace.Generating
{
    public class StepToNextLevelArea : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if ( other.tag == "Player")
            {
                SimpleGameController.Instance.StepToNextLevel();
                GameObject.Destroy(gameObject);
            }
        }
    }
}