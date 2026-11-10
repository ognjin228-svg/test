using System.Collections.Generic;
using UnityEngine;

/// LOD юнита: LOD0 — полная модель + аксессуары, LOD1 — упрощённый меш без аксессуаров,
/// LOD2 — импостор/куб из 12 треугольников, дальше — отсечение.
[RequireComponent(typeof(LODGroup))]
public class NPCLod : MonoBehaviour
{
    public Renderer[] lod0;
    public Renderer[] lod1;
    public Renderer[] lod2;
    [Tooltip("Доля высоты экрана, ниже которой включается следующий LOD")]
    public float[] screenHeights = { 0.15f, 0.05f, 0.012f };

    LODGroup group;
    readonly List<Renderer> tmp = new();

    void Awake()
    {
        group = GetComponent<LODGroup>();
        group.fadeMode = LODFadeMode.None;
        Apply(null);
    }

    /// Аксессуары создаются в рантайме, поэтому LOD0 пересобирается после каждой смены внешности.
    public void Apply(List<Renderer> extraLod0)
    {
        if (!group) group = GetComponent<LODGroup>();
        tmp.Clear();
        tmp.AddRange(lod0);
        if (extraLod0 != null) tmp.AddRange(extraLod0);
        group.SetLODs(new[]
        {
            new LOD(screenHeights[0], tmp.ToArray()),
            new LOD(screenHeights[1], lod1),
            new LOD(screenHeights[2], lod2),
        });
        group.RecalculateBounds();
    }
}

