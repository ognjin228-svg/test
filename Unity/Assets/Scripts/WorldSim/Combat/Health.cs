using System;
using UnityEngine;

public class Health : MonoBehaviour, IPoolable
{
    public float max = 100f;
    [Range(0, 1)] public float blockChance = 0.2f;   // блок бронёй: искры вместо крови, урон ×0.3

    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    /// (кто, урон, точка удара, атакующий)
    public event Action<Health, float, Vector3, Health> Damaged;
    public event Action<Health> Died;

    void Awake() => Current = max;
    public void OnSpawned() => Current = max;
    public void OnDespawned() { }

    public void TakeDamage(float amount, Vector3 point, Health attacker)
    {
        if (IsDead) return;
        bool blocked = UnityEngine.Random.value < blockChance;
        if (blocked) amount *= 0.3f;
        Current = Mathf.Max(0f, Current - amount);

        var dir = attacker ? (transform.position - attacker.transform.position).normalized : Vector3.up;
        CombatFX.Hit(point, dir, blocked);
        if (HealthBarManager.Instance) HealthBarManager.Instance.Show(this);
        Damaged?.Invoke(this, amount, point, attacker);

        if (IsDead)
        {
            CombatFX.Death(transform.position);
            Died?.Invoke(this);
        }
    }

    public void Kill()
    {
        if (IsDead) return;
        Current = 0f;
        Died?.Invoke(this);
    }
}
