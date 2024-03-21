using Unity.Entities;
using UnityEngine;

namespace Sandbox.Player
{
    

    public struct PlayerDashComponent : IComponentData
    {
        public bool active;
        public int uses;
        public float power;
        public float dashTime;
        public float DashTimeTicker;
        public float delayTime;
        public float DelayTimeTicker;
        public float invincibleStart;
        public float invincibleEnd;
        //public PhysicsCollider Collider;
        public BlobAssetReference<Unity.Physics.Collider> box;
        public bool Invincible;
        public bool InDash;

    }

    public struct Invincible : IComponentData
    {
        public int Value;
    }

    public class PlayerDashClass : IComponentData
    {
        //public BlobAssetReference<Unity.Physics.Collider> box;
        public AudioSource audioSource;
        public AudioClip audioClip;
        public ParticleSystem ps;
        public Transform transform;
    }

    public class PlayerDashAuthoring : MonoBehaviour

    {
        //public BlobAssetReference<Unity.Physics.Collider> box;
        public float power = 10;
        public float dashTime = 1;
        public float delayTime = .5f;
        public float invincibleStart = .1f;
        public float invincibleEnd = 1f;
        public int uses = 9999;
        public bool active = true;

        public AudioSource audioSource;
        public AudioClip clip;
        public ParticleSystem ps;



        class PlayerDashBaker : Baker<PlayerDashAuthoring>
        {
            public override void Bake(PlayerDashAuthoring authoring)
            {
                var e = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);

                AddComponent(e, new PlayerDashComponent
                    {
                        active = authoring.active,
                        power = authoring.power,
                        uses = authoring.uses,
                        dashTime = authoring.dashTime,
                        delayTime = authoring.delayTime,
                        invincibleStart = authoring.invincibleStart,
                        invincibleEnd = authoring.invincibleEnd
                    }
                );

                // AudioSource authoringAudioSource = null;
                // if (authoring.audioSource != null)
                // {
                //     authoringAudioSource = authoring.audioSource;
                //     authoringAudioSource.clip = authoring.clip;
                // }
                //
                //
                // AddComponentObject(e, 
                //     new PlayerDashClass
                //     {
                //         audioSource = authoring.audioSource,
                //         audioClip = authoring.clip,
                //         ps = authoring.ps,
                //         transform = authoring.transform
                //     } );

                
            }
        }



      
    }
}