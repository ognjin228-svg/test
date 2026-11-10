using System;
using UnityEngine;

public enum LifeStage { Child, Teen, Adult, Elder }

/// Возраст: стадии жизни, плавный рост модели, скорость, смерть от старости.
public class Aging : MonoBehaviour, IPoolable
{
    /// Скорость времени: 0.2 = один год за 5 секунд (при Time.timeScale = 1).
    public static float YearsPerSecond = 0.2f;

    public float teenAge = 6f, adultAge = 14f, elderAge = 50f;
    public Vector2 lifespan = new(60f, 80f);

    /// Масштаб модели от возраста; итог умножается на индивидуальный рост из внешности.
    public AnimationCurve scaleByAge = new(
        new Keyframe(0f, 0.4f), new Keyframe(6f, 0.65f), new Keyframe(14f, 1f),
        new Keyframe(50f, 1f), new Keyframe(80f, 0.94f));

    public float scaleSmoothing = 4f;

    public float Age { get; private set; }
    public LifeStage Stage { get; private set; }
    public event Action<LifeStage> StageChanged;

    public float SpeedMultiplier => Stage switch
    {
        LifeStage.Child => 0.55f,
        LifeStage.Teen => 0.85f,
        LifeStage.Elder => 0.7f,
        _ => 1f,
    };

    public bool CanWork => Stage >= LifeStage.Teen;
    public bool CanFight => Stage >= LifeStage.Adult;
    public bool CanReproduce => Stage == LifeStage.Adult;

    NPC npc;
    float lifespanYears;

    void Awake() => npc = GetComponent<NPC>();
    public void OnSpawned() { }
    public void OnDespawned() { }

    public void SetAge(float years)
    {
        Age = years;
        lifespanYears = UnityEngine.Random.Range(lifespan.x, lifespan.y);
        Stage = StageFor(Age);
        transform.localScale = Vector3.one * TargetScale();
        StageChanged?.Invoke(Stage);
    }

    LifeStage StageFor(float a) =>
        a < teenAge ? LifeStage.Child : a < adultAge ? LifeStage.Teen : a < elderAge ? LifeStage.Adult : LifeStage.Elder;

    float TargetScale() => scaleByAge.Evaluate(Age) * npc.appearance.Data.height;

    void Update()
    {
        if (npc.health.IsDead) return;
        Age += Time.deltaTime * YearsPerSecond;

        var s = StageFor(Age);
        if (s != Stage) { Stage = s; StageChanged?.Invoke(s); }

        float cur = transform.localScale.x;
        float k = 1f - Mathf.Exp(-scaleSmoothing * Time.deltaTime);
        transform.localScale = Vector3.one * Mathf.Lerp(cur, TargetScale(), k);

        if (Age >= lifespanYears) npc.health.Kill();
    }
}
