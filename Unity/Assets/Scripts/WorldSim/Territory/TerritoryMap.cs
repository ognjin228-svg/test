using Unity.Collections;
using UnityEngine;

/// Пиксельная карта территорий как в WorldBox: 1 клетка мира = 1 пиксель текстуры,
/// фильтрация Point, текстура лежит прозрачным слоем поверх земли.
/// При захвате пиксель плавно перетекает в цвет захватчика со вспышкой; границы ярче заливки.
public class TerritoryMap : MonoBehaviour
{
    public static TerritoryMap Instance { get; private set; }

    [Header("Сетка")]
    public int width = 256, height = 256;
    public float cellSize = 1f;
    public Vector3 origin;            // мировая позиция угла клетки (0,0)
    public float overlayHeight = 0.05f;

    [Header("Вид")]
    public Material overlayMaterial;  // URP Unlit, Surface = Transparent
    [Range(0, 1)] public float fillAlpha = 0.35f, borderAlpha = 0.9f;
    public float transitionTime = 0.6f;
    [Range(0, 1)] public float captureFlash = 0.45f;

    int[] owner;
    float[] progress;
    Color32[] from;
    NativeArray<Color32> pixels;      // смотрит прямо в память текстуры, без копий
    Texture2D tex;
    int[] animating; bool[] isAnimating; int animCount;

    void Awake()
    {
        Instance = this;
        int n = width * height;
        owner = new int[n]; progress = new float[n]; from = new Color32[n];
        animating = new int[n]; isAnimating = new bool[n];
        for (int i = 0; i < n; i++) owner[i] = -1;

        tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        pixels = tex.GetRawTextureData<Color32>();
        for (int i = 0; i < n; i++) pixels[i] = default;
        tex.Apply(false);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "TerritoryOverlay";
        quad.transform.SetParent(transform, false);
        quad.transform.SetPositionAndRotation(
            origin + new Vector3(width * cellSize * 0.5f, overlayHeight, height * cellSize * 0.5f),
            Quaternion.Euler(90f, 0f, 0f));
        quad.transform.localScale = new Vector3(width * cellSize, height * cellSize, 1f);
        var mr = quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(overlayMaterial) { mainTexture = tex };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public bool WorldToCell(Vector3 p, out int x, out int y)
    {
        x = Mathf.FloorToInt((p.x - origin.x) / cellSize);
        y = Mathf.FloorToInt((p.z - origin.z) / cellSize);
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    public int GetOwner(Vector3 p) => WorldToCell(p, out var x, out var y) ? owner[y * width + x] : -1;

    public void SetOwner(int x, int y, int faction)
    {
        int i = y * width + x;
        if (owner[i] == faction) return;
        owner[i] = faction;
        StartTransition(i);
        // Соседям нужно пересчитать «граница или нет».
        if (x > 0) StartTransition(i - 1);
        if (x < width - 1) StartTransition(i + 1);
        if (y > 0) StartTransition(i - width);
        if (y < height - 1) StartTransition(i + width);
    }

    /// Заявить круг клеток (новый дом/поселение). Чужие клетки не трогает.
    public void Claim(Vector3 center, float radius, int faction)
    {
        if (!WorldToCell(center, out var cx, out var cy)) return;
        int r = Mathf.CeilToInt(radius / cellSize);
        for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(height - 1, cy + r); y++)
        for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(width - 1, cx + r); x++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r && owner[y * width + x] < 0)
                SetOwner(x, y, faction);
    }

    /// Воин, стоящий на земле врага, с шансом захватывает клетку.
    public bool TryCapture(Vector3 p, int faction, float chance)
    {
        if (!WorldToCell(p, out var x, out var y)) return false;
        int o = owner[y * width + x];
        if (o < 0 || !Factions.AtWar(o, faction) || Random.value > chance) return false;
        SetOwner(x, y, faction);
        return true;
    }

    void StartTransition(int i)
    {
        from[i] = pixels[i];
        progress[i] = 0f;
        if (!isAnimating[i]) { isAnimating[i] = true; animating[animCount++] = i; }
    }

    Color32 Target(int i)
    {
        int o = owner[i];
        if (o < 0) return default;
        int x = i % width, y = i / width;
        bool border = (x == 0 || owner[i - 1] != o) || (x == width - 1 || owner[i + 1] != o)
                   || (y == 0 || owner[i - width] != o) || (y == height - 1 || owner[i + width] != o);
        var c = Factions.ColorOf(o);
        if (border) c = Color.Lerp(c, Color.white, 0.25f);
        c.a = border ? borderAlpha : fillAlpha;
        return c;
    }

    void LateUpdate()
    {
        if (animCount == 0) return;
        float step = Time.deltaTime / Mathf.Max(0.01f, transitionTime);
        for (int k = animCount - 1; k >= 0; k--)
        {
            int i = animating[k];
            float t = progress[i] = Mathf.Min(1f, progress[i] + step);
            float e = t * t * (3f - 2f * t);
            Color c = Color.Lerp(from[i], Target(i), e);
            float flash = Mathf.Sin(t * Mathf.PI) * captureFlash;
            c = Color.Lerp(c, new Color(1f, 1f, 1f, Mathf.Max(c.a, 0.6f)), flash);
            pixels[i] = c;
            if (t >= 1f)
            {
                pixels[i] = Target(i);
                isAnimating[i] = false;
                animating[k] = animating[--animCount];
            }
        }
        tex.Apply(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
