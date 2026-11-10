using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

/// Конструктор дома: фундамент -> 1..N этажей стен -> крыша -> случайный декор.
/// Модули соединяются через сокеты, красятся MaterialPropertyBlock, дом «вырастает» по частям.
public class House : MonoBehaviour, IPoolable
{
    public int capacity;
    [HideInInspector] public Settlement settlement;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    readonly List<GameObject> parts = new();
    MaterialPropertyBlock mpb;

    void Awake() => mpb = new MaterialPropertyBlock();

    public void OnSpawned() { }
    public void OnDespawned() { StopAllCoroutines(); Clear(); settlement = null; }

    public void Build(HouseStyle style, int seed, Color factionColor, bool animate = true)
    {
        Clear();
        var rng = new Random(seed);

        // Поворот на 90° и лёгкий разброс размера — соседние дома не совпадают.
        transform.rotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);
        float s = 0.92f + (float)rng.NextDouble() * 0.16f;
        transform.localScale = new Vector3(s, 0.95f + (float)rng.NextDouble() * 0.1f, s);

        var wallColor = Pick(rng, style.wallColors, Color.white);
        var roofColor = Color.Lerp(Pick(rng, style.roofColors, Color.gray), factionColor, style.roofFactionTint);

        var socket = Add(Pick(rng, style.foundations), transform, null).top;

        int floors = rng.Next(style.minFloors, style.maxFloors + 1);
        var wallPrefab = Pick(rng, style.walls);
        for (int i = 0; i < floors; i++)
        {
            var prefab = i > 0 && rng.NextDouble() < style.mixedFloorChance ? Pick(rng, style.walls) : wallPrefab;
            var wall = Add(prefab, socket, wallColor);
            if (rng.NextDouble() < 0.5)   // зеркалим — двери/окна с другой стороны
            {
                var ls = wall.transform.localScale;
                wall.transform.localScale = new Vector3(-ls.x, ls.y, ls.z);
            }
            foreach (var d in wall.decorSockets)
                if (style.decor.Length > 0 && rng.NextDouble() < style.decorChance)
                    AddRaw(style.decor[rng.Next(style.decor.Length)], d);
            socket = wall.top;
        }
        Add(Pick(rng, style.roofs), socket, roofColor);

        capacity = floors * style.capacityPerFloor;
        if (animate) StartCoroutine(Construct());
    }

    HouseModule Add(HouseModule prefab, Transform socket, Color? tint)
    {
        var m = AddRaw(prefab.gameObject, socket).GetComponent<HouseModule>();
        if (tint.HasValue)
            foreach (var r in m.tintRenderers)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, tint.Value);
                r.SetPropertyBlock(mpb);
            }
        return m;
    }

    GameObject AddRaw(GameObject prefab, Transform socket)
    {
        // Родитель — корень дома, а не сокет: так зеркалирование стены не переворачивает крышу.
        var go = Pools.Spawn(prefab, socket.position, socket.rotation, transform);
        go.transform.localScale = prefab.transform.localScale;
        parts.Add(go);
        return go;
    }

    IEnumerator Construct()
    {
        var targets = new Vector3[parts.Count];
        for (int i = 0; i < parts.Count; i++) { targets[i] = parts[i].transform.localScale; parts[i].transform.localScale = Vector3.zero; }
        CombatFX.Dust(transform.position);
        for (int i = 0; i < parts.Count; i++)
        {
            var t = parts[i].transform;
            var target = targets[i];
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.35f)
            {
                // Небольшой «перелёт» масштаба — модуль будто ставят на место.
                float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                t.localScale = target * e;
                yield return null;
            }
            t.localScale = target;
        }
    }

    void Clear()
    {
        foreach (var p in parts) if (p) Pools.Despawn(p);
        parts.Clear();
    }

    static T Pick<T>(Random rng, T[] arr, T fallback = default) =>
        arr == null || arr.Length == 0 ? fallback : arr[rng.Next(arr.Length)];
}
