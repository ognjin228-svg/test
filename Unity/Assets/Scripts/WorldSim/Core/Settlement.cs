using System.Collections.Generic;
using UnityEngine;

/// Поселение: дома задают лимит жителей, без свободного места дети не рождаются.
public class Settlement : MonoBehaviour
{
    public int factionId;
    public float territoryRadius = 6f;
    public int wood;

    public readonly List<House> houses = new();
    public readonly List<NPC> residents = new();

    public int Capacity
    {
        get { int c = 0; foreach (var h in houses) c += h.capacity; return c; }
    }

    public bool HasRoom => residents.Count < Capacity;

    public void Add(NPC n) { if (!residents.Contains(n)) residents.Add(n); }
    public void Remove(NPC n) => residents.Remove(n);

    public void AddHouse(House h)
    {
        houses.Add(h);
        h.settlement = this;
        TerritoryMap.Instance?.Claim(h.transform.position, territoryRadius, factionId);
    }
}
