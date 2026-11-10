using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Полоски здоровья над раненными юнитами. Один Canvas (Screen Space - Overlay) на всех,
/// полоски берутся из пула и видны 3 секунды после удара — сотни юнитов не тормозят UI.
public class HealthBarManager : MonoBehaviour
{
    public static HealthBarManager Instance { get; private set; }

    public Canvas canvas;
    public RectTransform barPrefab;      // фон + дочерний Image (Type = Filled, Horizontal)
    public float showTime = 3f;
    public float heightOffset = 2.2f;
    public float maxDistance = 70f;

    class Entry { public Health h; public RectTransform rt; public Image fill; public float until; }

    readonly Dictionary<Health, Entry> active = new();
    readonly Stack<Entry> free = new();
    readonly List<Health> toRemove = new();
    Camera cam;

    void Awake() => Instance = this;

    public void Show(Health h)
    {
        if (!active.TryGetValue(h, out var e))
        {
            if (free.Count > 0) e = free.Pop();
            else
            {
                var rt = Instantiate(barPrefab, canvas.transform);
                e = new Entry { rt = rt, fill = rt.GetChild(0).GetComponent<Image>() };
            }
            e.h = h;
            active[h] = e;
        }
        e.until = Time.time + showTime;
    }

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        foreach (var kv in active)
        {
            var e = kv.Value;
            if (Time.time > e.until || e.h.IsDead || !e.h.isActiveAndEnabled) { toRemove.Add(kv.Key); continue; }
            var t = e.h.transform;
            var sp = cam.WorldToScreenPoint(t.position + Vector3.up * heightOffset * t.localScale.y);
            bool visible = sp.z > 0f && sp.z < maxDistance;
            if (e.rt.gameObject.activeSelf != visible) e.rt.gameObject.SetActive(visible);
            if (!visible) continue;
            e.rt.position = sp;
            float f = e.h.Current / e.h.max;
            e.fill.fillAmount = f;
            e.fill.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.9f, 0.3f), f);
        }
        foreach (var h in toRemove)
        {
            var e = active[h];
            e.rt.gameObject.SetActive(false);
            e.h = null;
            free.Push(e);
            active.Remove(h);
        }
        toRemove.Clear();
    }
}
