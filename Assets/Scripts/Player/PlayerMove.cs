using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

namespace Sandbox.Player
{
    
    public class PlayerMove : MonoBehaviour
    {
        public Entity linkedEntity;
        public EntityManager entityManager;
        //public VisualEffect vfxSystem;
        private Animator animator;
        public int targetFrameRate = -1;
        public AudioSource AudioSource;
        public AudioClip AudioClip;


        void Start()
        {
            if (linkedEntity == Entity.Null)
            {
                linkedEntity = GetComponent<CharacterEntityTracker>().linkedEntity;
                if (entityManager == default)
                {
                    entityManager = GetComponent<CharacterEntityTracker>().entityManager;
                }
            }
            
            

            if (targetFrameRate >= 10)
            {
                Application.targetFrameRate = targetFrameRate;
            }

            
         
            if (linkedEntity != Entity.Null)
            {
                animator = GetComponent<Animator>();
                //var go = entityManager.GetComponentObject<PlayerMoveGameObjectClass>(linkedEntity);
                //go.vfxSystem = vfxSystem;
                //entityManager.SetComponentData(linkedEntity, go);
                
                //pass  this to playermove mb and set VFX effect there - for some reason if set in Sub-Scene it ignores parameters

            }
        }

      
    }
}