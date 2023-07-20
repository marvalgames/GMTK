using Unity.Entities;
using System.Collections.Generic;
using Collisions;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

namespace Sandbox.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        public MovesManager movesInspector;
        private Animator animator;
        private List<Moves> moveList = new List<Moves>();
        public Moves moveUsing = new Moves();
        private Entity meleeEntity;
        private EntityManager entityManager;
        private static readonly int CombatAction = Animator.StringToHash("CombatAction");

        void Start()
        {
            animator = GetComponent<Animator>();

            for (var i = 0; i < movesInspector.Moves.Count; i++)
            {
                var move = movesInspector.Moves[i];
                move.target = moveUsing.target;//default target assigned in system
                move.targetEntity = meleeEntity;
                moveList.Add(move);

            }
            
            
            if (meleeEntity == Entity.Null)
            {
                meleeEntity = GetComponent<CharacterEntityTracker>().linkedEntity;
                if (entityManager == default)
                {
                    entityManager = GetComponent<CharacterEntityTracker>().entityManager;
                }
                if(meleeEntity != Entity.Null) entityManager.AddComponentObject(meleeEntity, this);
            }
        }

        public void SelectMove(int combatAction)
        {
            if (moveList.Count <= 0) return;
            var animationIndex = -1;
            var primaryTrigger = TriggerType.None;

            for (var i = 0; i < moveList.Count; i++)//pick from list defined in inspector
            {
                if ((int)moveList[i].animationType == combatAction)
                {
                    moveUsing = moveList[i];
                    animationIndex = (int)moveUsing.animationType;
                    primaryTrigger = moveUsing.triggerType;
                }
            }

            if (animationIndex <= 0 || moveUsing.active == false) return;//0 is none on enum
            var defense = animationIndex == (int)AnimationType.Deflect;
            StartMove(animationIndex, primaryTrigger, defense);
        }

        public void StartMove(int animationIndex, TriggerType primaryTrigger, bool defense)
        {
            if (moveList.Count <= 0) return;
            if (entityManager.HasComponent<CheckedComponent>(meleeEntity))
            {
                var checkedComponent = entityManager.GetComponentData<CheckedComponent>(meleeEntity);
                checkedComponent.anyAttackStarted = true;
                checkedComponent.anyDefenseStarted = defense;
                Debug.Log("DEFENSE ANY STARTED " + defense);
                checkedComponent.primaryTrigger = primaryTrigger;
                entityManager.SetComponentData(meleeEntity, checkedComponent);
            }

            animator.SetInteger(CombatAction, animationIndex);
            animator.SetLayerWeight(0, 0);
            animator.SetLayerWeight(1, 1);
        }

        
        public void Aim()
        {

        }

        public void LateUpdateSystem()
        {
            if (moveList.Count == 0) return;
            Aim();
        


        }
        
        public void StartMotionUpdateCheckComponent()//event
        {
        }
        

        public void StartAttackUpdateCheckComponent()//event
        {
            
            if(entityManager.HasComponent<MeleeComponent>(meleeEntity))
            {
                var melee = entityManager.GetComponentData<MeleeComponent>(meleeEntity);
                moveUsing.target = melee.target;
            }
            
            if (moveUsing.moveAudioSource && moveUsing.moveAudioClip)
            {
                moveUsing.moveAudioSource.clip = moveUsing.moveAudioClip;
                moveUsing.moveAudioSource.PlayOneShot(moveUsing.moveAudioClip);

            }
            if (moveUsing.moveParticleSystem)
            {
                moveUsing.moveParticleSystem.Play(true);
            }
            
            if (entityManager.HasComponent<CheckedComponent>(meleeEntity))
            {
                var checkedComponent = entityManager.GetComponentData<CheckedComponent>(meleeEntity);
                checkedComponent.anyAttackStarted = true;
                checkedComponent.attackFirstFrame = true;
                //checkedComponent.anyDefenseStarted = false;
                checkedComponent.hitTriggered = false;  
                entityManager.SetComponentData(meleeEntity, checkedComponent);
            }
        }

        public void EndAttack()
        {
            if (entityManager.HasComponent<CheckedComponent>(meleeEntity))
            {
                var checkedComponent = entityManager.GetComponentData<CheckedComponent>(meleeEntity);
                if (checkedComponent.hitTriggered == false && entityManager.HasComponent<ScoreComponent>(meleeEntity))
                {
                    var score = entityManager.GetComponentData<ScoreComponent>(meleeEntity);
                    score.combo = 0;
                    score.streak = 0;
                    entityManager.SetComponentData(meleeEntity, score);
                }
                checkedComponent.hitLanded = false;//set at end of attack only
                checkedComponent.anyDefenseStarted = false;
                checkedComponent.anyAttackStarted = false;
                checkedComponent.AttackStages = AttackStages.End;//only for one frame
                entityManager.SetComponentData(meleeEntity, checkedComponent);

            }

        }


     
    }
}