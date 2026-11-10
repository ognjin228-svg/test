using UnityEngine;

/// Повесьте на префаб эффекта: проигрывается при выдаче из пула и сам возвращается обратно.
[RequireComponent(typeof(ParticleSystem))]
public class PooledParticle : MonoBehaviour, IPoolable
{
    ParticleSystem ps;
    float life;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;
        life = main.duration + main.startLifetime.constantMax;
    }

    public void OnSpawned()
    {
        ps.Clear(true);
        ps.Play(true);
        Pools.Despawn(gameObject, life);
    }

    public void OnDespawned() => ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
}
