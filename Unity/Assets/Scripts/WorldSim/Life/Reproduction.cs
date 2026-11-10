using System.Collections;
using UnityEngine;

/// Рождение детей вместо мгновенного спавна взрослых:
/// взрослая женщина + свободный взрослый мужчина своей фракции рядом + место в домах + кулдаун + беременность.
public class Reproduction : MonoBehaviour, IPoolable
{
    public static int GlobalCap = 2000;

    public NPC childPrefab;                          // обычно тот же префаб расы
    public Vector2 cooldownYears = new(3f, 6f);      // пауза между детьми
    public float gestationYears = 0.75f;
    public float partnerRadius = 6f;
    public float checkInterval = 2f;                 // реальные секунды между попытками
    [Range(0, 1)] public float chancePerCheck = 0.25f;

    float cooldownLeft, checkTimer;
    bool pregnant;
    NPC npc;

    void Awake() => npc = GetComponent<NPC>();

    public void OnSpawned()
    {
        cooldownLeft = Random.Range(cooldownYears.x, cooldownYears.y);
        checkTimer = Random.value * checkInterval;
        pregnant = false;
    }

    public void OnDespawned() => StopAllCoroutines();

    public bool Ready => npc.aging.CanReproduce && cooldownLeft <= 0f && !pregnant && !npc.health.IsDead;

    void Update()
    {
        cooldownLeft -= Time.deltaTime * Aging.YearsPerSecond;
        if ((checkTimer -= Time.deltaTime) > 0f) return;
        checkTimer = checkInterval;

        if (npc.sex != Sex.Female || !Ready) return;
        if (!npc.home || !npc.home.HasRoom || NPC.All.Count >= GlobalCap) return;
        if (npc.brain.Current is NPCBrain.State.Chase or NPCBrain.State.Attack) return;
        if (Random.value > chancePerCheck) return;

        var father = FindPartner();
        if (!father) return;

        cooldownLeft = Random.Range(cooldownYears.x, cooldownYears.y);
        father.reproduction.cooldownLeft = cooldownLeft * 0.5f;
        // Копируем внешность сейчас: к родам отец может погибнуть и уйти в пул.
        StartCoroutine(Birth(npc.appearance.Data, father.appearance.Data));
    }

    NPC FindPartner()
    {
        NPC best = null;
        float bestD = partnerRadius * partnerRadius;
        foreach (var o in NPC.All)
        {
            if (o == npc || o.sex != Sex.Male || o.factionId != npc.factionId || !o.reproduction || !o.reproduction.Ready) continue;
            float d = (o.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = o; }
        }
        return best;
    }

    IEnumerator Birth(AppearanceData mother, AppearanceData father)
    {
        pregnant = true;
        yield return new WaitForSeconds(gestationYears / Aging.YearsPerSecond);
        pregnant = false;
        if (npc.health.IsDead || !npc.home || !npc.home.HasRoom) yield break;
        var pos = transform.position + Random.insideUnitSphere.Flat() * 0.8f;
        NPC.Create(childPrefab, pos, npc.factionId, npc.home, 0f, mother, father);
    }
}
