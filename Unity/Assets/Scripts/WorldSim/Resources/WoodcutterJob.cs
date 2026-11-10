using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Работа дровосека: подойти к дереву -> замахи топором -> дерево падает -> собрать брёвна.
public class WoodcutterJob
{
    enum Step { GoToTree, Chop, Collect, Done }

    public float searchRadius = 40f, reach = 1.5f, chopInterval = 1.1f, hitDelay = 0.45f;

    static readonly int ChopId = Animator.StringToHash("Chop");

    readonly NPC npc;
    readonly NavMeshAgent agent;
    readonly Animator anim;
    readonly List<ResourcePickup> pickups = new();
    TreeResource tree;
    ResourcePickup heading;
    Step step = Step.Done;
    float swingTimer, hitAt = -1f;

    public WoodcutterJob(NPC npc, NavMeshAgent agent, Animator anim)
    {
        this.npc = npc; this.agent = agent; this.anim = anim;
    }

    public bool Begin()
    {
        tree = TreeResource.FindNearestFree(npc.transform.position, searchRadius);
        if (!tree) return false;
        tree.Reserve(npc);
        step = Step.GoToTree;
        agent.isStopped = false;
        agent.SetDestination(tree.ChopPoint(npc.transform.position));
        return true;
    }

    /// false — работа закончена.
    public bool Tick()
    {
        switch (step)
        {
            case Step.GoToTree:
                if (!tree || tree.Felled) { Cancel(); return false; }
                if (agent.pathPending || agent.remainingDistance > reach) return true;
                agent.isStopped = true;
                step = Step.Chop;
                swingTimer = 0.2f;
                return true;

            case Step.Chop:
                if (!tree) { Cancel(); return false; }
                Face(tree.transform.position);
                if (hitAt < 0f && (swingTimer -= Time.deltaTime) <= 0f)
                {
                    anim.SetTrigger(ChopId);
                    hitAt = Time.time + hitDelay;
                    swingTimer = chopInterval;
                }
                if (hitAt > 0f && Time.time >= hitAt)
                {
                    hitAt = -1f;
                    if (tree.Chop(npc, pickups))
                    {
                        tree = null;
                        step = Step.Collect;
                        agent.isStopped = false;
                    }
                }
                return true;

            case Step.Collect:
                for (int i = pickups.Count - 1; i >= 0; i--)
                    if (!pickups[i] || !pickups[i].isActiveAndEnabled || pickups[i].Collected) pickups.RemoveAt(i);
                if (pickups.Count == 0) { step = Step.Done; return false; }
                var p = pickups[pickups.Count - 1];
                if (!p.Available) return true;            // брёвна ещё падают
                if (heading != p) { heading = p; agent.SetDestination(p.transform.position); }
                if (!agent.pathPending && agent.remainingDistance < 0.9f)
                {
                    int amount = p.Collect(npc.transform);
                    if (npc.home) npc.home.wood += amount;
                    pickups.RemoveAt(pickups.Count - 1);
                    heading = null;
                }
                return true;
        }
        return false;
    }

    public void Cancel()
    {
        if (tree) tree.Release(npc);
        tree = null;
        heading = null;
        pickups.Clear();
        hitAt = -1f;
        step = Step.Done;
    }

    void Face(Vector3 p)
    {
        var to = p - npc.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 1e-4f)
            npc.transform.rotation = Quaternion.Slerp(npc.transform.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);
    }
}
