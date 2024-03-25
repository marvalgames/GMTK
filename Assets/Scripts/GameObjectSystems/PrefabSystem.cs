using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VFX;


public class VisualEffectGO : IComponentData
{
    public VisualEffect VisualEffect;
}

public class AudioPlayerGO : IComponentData
{
    public AudioSource AudioSource;
    public AudioClip AudioClip;
}
public class VisualEffectJumpGO : IComponentData
{
    public VisualEffect VisualEffect;
}

public class AudioPlayerJumpGO : IComponentData
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
            GameObject vfxGo = GameObject.Instantiate(prefab.vfxSystemGo);
            ecb.AddComponent(entity,
                new VisualEffectGO { VisualEffect = vfxGo.GetComponent<VisualEffect>() });

            GameObject audioGo = GameObject.Instantiate(prefab.audioSourceGo);
            ecb.AddComponent(entity,
                new AudioPlayerGO { AudioSource = audioGo.GetComponent<AudioSource>(), AudioClip = prefab.clip });
            ecb.RemoveComponent<PlayerMoveGameObjectClass>(entity);
        }
        
        foreach (var (prefab, entity) in
                 SystemAPI.Query<PlayerJumpGameObjectClass>().WithEntityAccess())
        {
            GameObject vfxGo = GameObject.Instantiate(prefab.vfxSystem);
            ecb.AddComponent(entity,
                new VisualEffectJumpGO() { VisualEffect = vfxGo.GetComponent<VisualEffect>() });

            GameObject audioGo = GameObject.Instantiate(prefab.audioSourceGo);
            ecb.AddComponent(entity,
                new AudioPlayerJumpGO() { AudioSource = audioGo.GetComponent<AudioSource>(), AudioClip = prefab.clip });
            ecb.RemoveComponent<PlayerJumpGameObjectClass>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}