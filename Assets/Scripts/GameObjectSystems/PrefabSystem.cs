using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VFX;


public class VisualEffectGO: IComponentData
{
    public VisualEffect VisualEffect;
}

public class AudioPlayerGO : IComponentData
{
    public AudioSource AudioSource;
    public AudioClip AudioClip;
}





public partial struct InstantiatePrefabSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Get all Entities that have the component with the Entity reference
        foreach (var (prefab, entity) in
                 SystemAPI.Query<PlayerMoveGameObjectClass>().WithEntityAccess())
        {
            // Instantiate the prefab Entity
            GameObject  go = GameObject.Instantiate(prefab.vfxSystem);
            ecb.RemoveComponent<PlayerMoveGameObjectClass>(entity);
            ecb.AddComponent(entity,
                new VisualEffectGO { VisualEffect = go.GetComponent<VisualEffect>() });
            ecb.AddComponent(entity, new AudioPlayerGO { AudioSource = prefab.audioSource, AudioClip = prefab.clip});
            // Note: the returned instance is only relevant when used in the ECB
            // as the entity is not created in the EntityManager until ECB.Playback
            //ecb.AddComponent<VfxComponentTag>(instance);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}











