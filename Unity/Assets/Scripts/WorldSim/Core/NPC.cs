using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum Sex { Male, Female }

public static class Factions
{
    public const int Max = 16;
    static readonly bool[,] war = new bool[Max, Max];

    public static Color[] Colors =
    {
        new(0.30f, 0.56f, 0.91f), new(0.38f, 0.84f, 0.50f), new(0.89f, 0.31f, 0.27f), new(0.93f, 0.69f, 0.24f),
        new(0.68f, 0.40f, 0.90f), new(0.95f, 0.55f, 0.75f), new(0.30f, 0.85f, 0.85f), new(0.60f, 0.60f, 0.60f),
    };

    public static bool AtWar(int a, int b) => a != b && a >= 0 && b >= 0 && war[a, b];
    public static void SetWar(int a, int b, bool v) { if (a != b) war[a, b] = war[b, a] = v; }
    public static Color ColorOf(int f) => f >= 0 ? Colors[f % Colors.Length] : Color.clear;
}

/// Корневой компонент жителя: связывает внешность, возраст, бой и размножение.
[RequireComponent(typeof(Health), typeof(Aging), typeof(NPCAppearance))]
[RequireComponent(typeof(NPCBrain), typeof(NavMeshAgent))]
public class NPC : MonoBehaviour, IPoolable
{
    public static readonly List<NPC> All = new();

    public int factionId;
    public Sex sex;
    public Settlement home;

    [HideInInspector] public Health health;
    [HideInInspector] public Aging aging;
    [HideInInspector] public NPCAppearance appearance;
    [HideInInspector] public NPCBrain brain;
    [HideInInspector] public Reproduction reproduction;
    [HideInInspector] public NavMeshAgent agent;

    void Awake()
    {
        health = GetComponent<Health>();
        aging = GetComponent<Aging>();
        appearance = GetComponent<NPCAppearance>();
        brain = GetComponent<NPCBrain>();
        reproduction = GetComponent<Reproduction>();
        agent = GetComponent<NavMeshAgent>();
        aging.StageChanged += appearance.ApplyStage;
    }

    public void OnSpawned() => All.Add(this);

    public void OnDespawned()
    {
        All.Remove(this);
        if (home) home.Remove(this);
        home = null;
    }

    /// age = 0 — новорождённый; для стартовых поселенцев передавайте 18–30.
    public static NPC Create(NPC prefab, Vector3 pos, int faction, Settlement home, float age,
                             AppearanceData? mother = null, AppearanceData? father = null)
    {
        var npc = Pools.Spawn(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        npc.agent.Warp(pos);
        npc.factionId = faction;
        npc.sex = Random.value < 0.5f ? Sex.Male : Sex.Female;
        npc.home = home;
        if (home) home.Add(npc);

        var fc = Factions.ColorOf(faction);
        if (mother.HasValue && father.HasValue)
            npc.appearance.InheritFrom(mother.Value, father.Value, npc.sex, fc);
        else
            npc.appearance.Randomize(Random.Range(int.MinValue, int.MaxValue), npc.sex, fc);

        npc.aging.SetAge(age);
        npc.brain.ResetState();
        return npc;
    }
}
