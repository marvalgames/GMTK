using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

namespace Sandbox.Player
{
    
    public class PlayerMove : MonoBehaviour
    {
        public Entity linkedEntity;
        public EntityManager entityManager;
        [HideInInspector]
        public int targetFrameRate = -1;


        // void Start()
        // {
        //     if (linkedEntity == Entity.Null)
        //     {
        //         linkedEntity = GetComponent<CharacterEntityTracker>().linkedEntity;
        //         if (entityManager == default)
        //         {
        //             entityManager = GetComponent<CharacterEntityTracker>().entityManager;
        //         }
        //     }
        //     
        //     
        //
        //     if (targetFrameRate >= 10)
        //     {
        //         Application.targetFrameRate = targetFrameRate;
        //     }
        //
        //     
        //  
        //     if (linkedEntity != Entity.Null)
        //     {
        //
        //     }
        // }

      
    }
}