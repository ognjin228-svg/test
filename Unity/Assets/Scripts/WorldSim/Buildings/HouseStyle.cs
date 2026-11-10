using UnityEngine;

[CreateAssetMenu(menuName = "WorldSim/House Style")]
public class HouseStyle : ScriptableObject
{
    public HouseModule[] foundations;
    public HouseModule[] walls;
    public HouseModule[] roofs;
    public GameObject[] decor;
    public Color[] wallColors;
    public Color[] roofColors;
    [Range(0, 1)] public float roofFactionTint = 0.5f;
    [Range(0, 1)] public float decorChance = 0.45f;
    [Range(0, 1)] public float mixedFloorChance = 0.3f;
    public int minFloors = 1, maxFloors = 2;
    public int capacityPerFloor = 2;
}
