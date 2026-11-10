using UnityEngine;

/// Все эффекты боя и работы. Префабы — ParticleSystem с компонентом PooledParticle.
public class CombatFX : MonoBehaviour
{
    static CombatFX I;

    public GameObject blood, sparks, deathPuff, woodChips, dust;
    public int prewarmEach = 30;

    void Awake()
    {
        I = this;
        foreach (var p in new[] { blood, sparks, deathPuff, woodChips, dust })
            if (p) Pools.Prewarm(p, prewarmEach);
    }

    static void Play(GameObject prefab, Vector3 pos, Vector3 dir)
    {
        if (!prefab) return;
        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.up;
        Pools.Spawn(prefab, pos, Quaternion.LookRotation(dir));
    }

    public static void Hit(Vector3 p, Vector3 dir, bool blocked) { if (I) Play(blocked ? I.sparks : I.blood, p, dir); }
    public static void Death(Vector3 p) { if (I) Play(I.deathPuff, p + Vector3.up * 0.5f, Vector3.up); }
    public static void WoodChips(Vector3 p, Vector3 dir) { if (I) Play(I.woodChips, p, dir); }
    public static void Dust(Vector3 p) { if (I) Play(I.dust, p, Vector3.up); }
}
