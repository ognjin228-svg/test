using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Дерево: качается от ударов, падает в сторону от дровосека, оставляет брёвна и пень, потом отрастает.
/// Pivot у visual должен быть у основания ствола.
public class TreeResource : MonoBehaviour
{
    public static readonly List<TreeResource> All = new();

    public Transform visual;
    public GameObject stump;
    public ResourcePickup logPrefab;
    public int chopsToFell = 5;
    public int logs = 3;
    public float regrowTime = 120f;

    public bool Felled { get; private set; }

    int hits;
    NPC reservedBy;
    Vector3 baseScale;
    Quaternion baseRot;

    void Awake()
    {
        baseScale = visual.localScale;
        baseRot = visual.localRotation;
        if (stump) stump.SetActive(false);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool IsFree => !Felled && (reservedBy == null || !reservedBy.isActiveAndEnabled);

    /// Для тысяч деревьев замените перебор на пространственную сетку.
    public static TreeResource FindNearestFree(Vector3 p, float maxDist)
    {
        TreeResource best = null;
        float bestD = maxDist * maxDist;
        foreach (var t in All)
        {
            if (!t.IsFree) continue;
            float d = (t.transform.position - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    public void Reserve(NPC n) => reservedBy = n;
    public void Release(NPC n) { if (reservedBy == n) reservedBy = null; }

    public Vector3 ChopPoint(Vector3 from)
    {
        var dir = from - transform.position;
        dir.y = 0f;
        return transform.position + (dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward) * 0.9f;
    }

    /// Один удар топора. true — дерево срублено, брёвна добавлены в spawned.
    public bool Chop(NPC by, List<ResourcePickup> spawned)
    {
        if (Felled) return false;
        hits++;
        var fromDir = transform.position - by.transform.position;
        fromDir.y = 0f;
        CombatFX.WoodChips(transform.position + Vector3.up * 0.8f, -fromDir);
        StopAllCoroutines();
        if (hits < chopsToFell)
        {
            StartCoroutine(Shake(fromDir));
            return false;
        }
        Felled = true;
        reservedBy = null;
        StartCoroutine(Fall(fromDir.normalized, spawned));
        return true;
    }

    IEnumerator Shake(Vector3 fromDir)
    {
        var axis = Vector3.Cross(Vector3.up, fromDir.normalized);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float a = Mathf.Sin(t * 45f) * 5f * (1f - t / 0.3f);
            visual.rotation = Quaternion.AngleAxis(a, axis) * transform.rotation * baseRot;
            yield return null;
        }
        visual.localRotation = baseRot;
    }

    IEnumerator Fall(Vector3 dir, List<ResourcePickup> spawned)
    {
        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
        var axis = Vector3.Cross(Vector3.up, dir);
        var startRot = transform.rotation * baseRot;
        if (stump) stump.SetActive(true);

        // Брёвна появляются сразу, но «выпрыгивают» после удара ствола о землю.
        const float fallTime = 1.3f;
        for (int i = 0; i < logs; i++)
        {
            var land = transform.position + dir * (1.2f + i * 1.1f) + Random.insideUnitSphere.Flat() * 0.4f;
            var log = Pools.Spawn(logPrefab, land, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            log.Launch(transform.position + Vector3.up * 0.5f, land, fallTime + i * 0.12f);
            spawned.Add(log);
        }

        for (float t = 0f; t < fallTime; t += Time.deltaTime)
        {
            float k = t / fallTime;
            visual.rotation = Quaternion.AngleAxis(88f * k * k, axis) * startRot;   // ускоряется, как настоящее
            yield return null;
        }
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)   // отскок от земли
        {
            visual.rotation = Quaternion.AngleAxis(88f - Mathf.Sin(t / 0.25f * Mathf.PI) * 6f, axis) * startRot;
            yield return null;
        }
        CombatFX.Dust(transform.position + dir * 2f);

        yield return new WaitForSeconds(1.5f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            visual.localScale = baseScale * (1f - t / 0.6f);
            yield return null;
        }
        visual.gameObject.SetActive(false);

        yield return new WaitForSeconds(regrowTime);
        visual.localRotation = baseRot;
        visual.gameObject.SetActive(true);
        if (stump) stump.SetActive(false);
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            visual.localScale = baseScale * Mathf.SmoothStep(0.1f, 1f, t / 3f);
            yield return null;
        }
        visual.localScale = baseScale;
        hits = 0;
        Felled = false;
    }
}

public static class VectorExt
{
    public static Vector3 Flat(this Vector3 v) => new(v.x, 0f, v.z);
}
