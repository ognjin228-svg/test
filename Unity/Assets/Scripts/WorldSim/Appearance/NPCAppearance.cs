using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

/// Всё, что определяет внешность. Компактно: можно сохранять в сейв и передавать детям.
[Serializable]
public struct AppearanceData
{
    public Color skin, hair, clothA, clothB;
    public int torso, legs, hairStyle, beard, hat, weapon;   // -1 = нет
    public float height, shoulders, girth;
}

public enum TintChannel { Skin, Hair, ClothA, ClothB }

[Serializable]
public struct TintTarget
{
    public Renderer renderer;
    public int materialIndex;
    public TintChannel channel;
}

/// Процедурная внешность: сменные меши на одном скелете + цвета через MaterialPropertyBlock
/// (один общий материал на всех) + аксессуары в сокетах + пропорции через блендшейпы.
public class NPCAppearance : MonoBehaviour, IPoolable
{
    public AppearanceLibrary library;

    [Header("Сменные части тела")]
    public SkinnedMeshRenderer torso;
    public SkinnedMeshRenderer legs;

    [Header("Покраска (добавьте сюда и меши LOD1)")]
    public TintTarget[] tintTargets;

    [Header("Сокеты (пустые Transform на костях)")]
    public Transform headSocket;
    public Transform faceSocket;
    public Transform rightHandSocket;

    [Header("Блендшейпы")]
    public string shouldersBlendShape = "Wide";
    public string girthBlendShape = "Fat";

    public AppearanceData Data { get; private set; }

    // URP Lit/Unlit: _BaseColor; Built-in Standard: _Color.
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock mpb;
    SkinnedMeshRenderer[] skinned;
    readonly List<GameObject> attached = new();
    readonly List<Renderer> attachedRenderers = new();
    NPCLod lod;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        lod = GetComponent<NPCLod>();
    }

    public void OnSpawned() { }
    public void OnDespawned() => ClearAccessories();

    public void Randomize(int seed, Sex sex, Color factionColor)
    {
        var rng = new Random(seed);
        var L = library;
        var d = new AppearanceData
        {
            skin = L.skinTones.Evaluate((float)rng.NextDouble()),
            hair = Jitter(Pick(rng, L.hairColors, Color.black), rng, 0.08f),
            clothA = Color.Lerp(Jitter(Pick(rng, L.clothColors, Color.gray), rng, 0.1f), factionColor, L.factionTint),
            clothB = Jitter(Pick(rng, L.clothColors, Color.gray), rng, 0.1f),
            torso = Index(rng, L.torsoMeshes),
            legs = Index(rng, L.legsMeshes),
            hairStyle = Index(rng, sex == Sex.Male ? L.maleHair : L.femaleHair),
            beard = sex == Sex.Male && rng.NextDouble() < L.beardChance ? Index(rng, L.beards) : -1,
            hat = rng.NextDouble() < L.hatChance ? Index(rng, L.hats) : -1,
            weapon = rng.NextDouble() < L.weaponChance ? Index(rng, L.weapons) : -1,
            height = Range(rng, L.height),
            shoulders = Range(rng, L.shoulders) * (sex == Sex.Male ? 1f : 0.6f),
            girth = Range(rng, L.girth),
        };
        Apply(d, sex);
    }

    /// Ребёнок похож на родителей: кожа — смесь, волосы — от одного из них, рост — среднее ± шум.
    public void InheritFrom(AppearanceData a, AppearanceData b, Sex sex, Color factionColor)
    {
        var seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        Randomize(seed, sex, factionColor);   // одежда/аксессуары — новые
        var rng = new Random(seed ^ 0x5bd1e995);
        var d = Data;
        d.skin = Jitter(Color.Lerp(a.skin, b.skin, (float)rng.NextDouble()), rng, 0.03f);
        d.hair = rng.NextDouble() < 0.5 ? a.hair : b.hair;
        d.height = Mathf.Clamp((a.height + b.height) * 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.08f,
                               library.height.x, library.height.y);
        Apply(d, sex);
    }

    void Apply(AppearanceData d, Sex sex)
    {
        Data = d;
        if (torso && d.torso >= 0) torso.sharedMesh = library.torsoMeshes[d.torso];
        if (legs && d.legs >= 0) legs.sharedMesh = library.legsMeshes[d.legs];

        foreach (var t in tintTargets)
        {
            if (!t.renderer) continue;
            var c = t.channel switch
            {
                TintChannel.Skin => d.skin,
                TintChannel.Hair => d.hair,
                TintChannel.ClothA => d.clothA,
                _ => d.clothB,
            };
            Tint(t.renderer, c, t.materialIndex);
        }

        foreach (var smr in skinned)
        {
            var m = smr.sharedMesh;
            if (!m) continue;
            int wi = m.GetBlendShapeIndex(shouldersBlendShape);
            if (wi >= 0) smr.SetBlendShapeWeight(wi, d.shoulders);
            int gi = m.GetBlendShapeIndex(girthBlendShape);
            if (gi >= 0) smr.SetBlendShapeWeight(gi, d.girth);
        }
        // Рост применяет Aging: scale = кривая возраста × Data.height.
    }

    /// Вызывается при смене стадии жизни: детям — только причёска, подросткам — + шляпа, взрослым — всё.
    public void ApplyStage(LifeStage stage)
    {
        ClearAccessories();
        var d = Data;
        var sexHair = GetComponent<NPC>().sex == Sex.Male ? library.maleHair : library.femaleHair;
        bool hatOn = stage >= LifeStage.Teen && d.hat >= 0;

        if (!hatOn) Attach(sexHair, d.hairStyle, headSocket, d.hair);
        else Attach(library.hats, d.hat, headSocket, d.clothB);
        if (stage >= LifeStage.Adult) Attach(library.beards, d.beard, faceSocket, d.hair);
        if (stage == LifeStage.Adult) Attach(library.weapons, d.weapon, rightHandSocket, null);

        if (lod) lod.Apply(attachedRenderers);
    }

    void Attach(GameObject[] set, int index, Transform socket, Color? tint)
    {
        if (set == null || index < 0 || index >= set.Length || !socket) return;
        var go = Pools.Spawn(set[index], socket.position, socket.rotation, socket);
        attached.Add(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            attachedRenderers.Add(r);
            if (tint.HasValue) Tint(r, tint.Value, 0);
        }
    }

    void ClearAccessories()
    {
        foreach (var go in attached) if (go) Pools.Despawn(go);
        attached.Clear();
        attachedRenderers.Clear();
    }

    void Tint(Renderer r, Color c, int materialIndex)
    {
        r.GetPropertyBlock(mpb, materialIndex);
        mpb.SetColor(BaseColorId, c);
        r.SetPropertyBlock(mpb, materialIndex);
    }

    static Color Pick(Random rng, Color[] arr, Color fallback) =>
        arr == null || arr.Length == 0 ? fallback : arr[rng.Next(arr.Length)];
    static int Index<T>(Random rng, T[] arr) => arr == null || arr.Length == 0 ? -1 : rng.Next(arr.Length);
    static float Range(Random rng, Vector2 r) => Mathf.Lerp(r.x, r.y, (float)rng.NextDouble());

    static Color Jitter(Color c, Random rng, float amount)
    {
        Color.RGBToHSV(c, out var h, out var s, out var v);
        h = Mathf.Repeat(h + ((float)rng.NextDouble() - 0.5f) * amount * 0.3f, 1f);
        s = Mathf.Clamp01(s + ((float)rng.NextDouble() - 0.5f) * amount);
        v = Mathf.Clamp01(v + ((float)rng.NextDouble() - 0.5f) * amount);
        return Color.HSVToRGB(h, s, v);
    }
}
