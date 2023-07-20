using Unity.Entities;
using UnityEngine;
[RequireMatchingQueriesForUpdate]
public partial class BossAmmoManagerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.WithoutBurst().ForEach(
            (
                Entity e,
                //BossAmmoManager bulletManager,
                //AudioClip weaponAudioClip,
                Animator animator,
                ref BossAmmoManagerComponent bulletManagerComponent,
                in BossAmmoManagerClass bossAmmoManagerClass
            ) =>
            {
                //Debug.Log("BOSS AMMO MANAGER");

                var weaponAudioSource = bossAmmoManagerClass.audioSource;
                if (weaponAudioSource && bulletManagerComponent.playSound)
                {
                    var clip = bossAmmoManagerClass.clip;
                    weaponAudioSource.PlayOneShot(clip, .25f);
                    bulletManagerComponent.playSound = false;
                }

                if (bulletManagerComponent.setAnimationLayer)
                {
                    animator.SetLayerWeight(0, 0);
                    bulletManagerComponent.setAnimationLayer = false;
                }
            }
        ).Run();
    }
}