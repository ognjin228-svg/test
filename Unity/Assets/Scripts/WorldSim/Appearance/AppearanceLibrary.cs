using UnityEngine;

[CreateAssetMenu(menuName = "WorldSim/Appearance Library")]
public class AppearanceLibrary : ScriptableObject
{
    [Header("Цвета")]
    public Gradient skinTones;
    public Color[] hairColors;
    public Color[] clothColors;
    [Range(0, 1)] public float factionTint = 0.45f;   // насколько одежда тянется к цвету фракции

    [Header("Сменные меши одежды (все скинены на один скелет!)")]
    public Mesh[] torsoMeshes;
    public Mesh[] legsMeshes;

    [Header("Аксессуары (префабы)")]
    public GameObject[] maleHair;
    public GameObject[] femaleHair;
    public GameObject[] beards;
    public GameObject[] hats;
    public GameObject[] weapons;
    [Range(0, 1)] public float hatChance = 0.35f, beardChance = 0.45f, weaponChance = 0.7f;

    [Header("Пропорции")]
    public Vector2 height = new(0.9f, 1.1f);
    public Vector2 shoulders = new(0f, 100f);   // вес блендшейпа "Wide"
    public Vector2 girth = new(0f, 80f);        // вес блендшейпа "Fat"
}

