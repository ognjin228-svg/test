using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// FSM жителя: Мирная жизнь / Работа -> Обнаружение врага (Chase) -> Атака -> Смерть.
/// Параметры Animator: float Speed, trigger Attack, Hit, Die, Chop.
[RequireComponent(typeof(NavMeshAgent))]
public class NPCBrain : MonoBehaviour, IPoolable
{
    public enum State { Peaceful, Work, Chase, Attack, Dead }

    [Header("Мирная жизнь")]
    public float baseSpeed = 3.5f;
    public float wanderRadius = 8f;
    public bool isWoodcutter = true;
    [Range(0, 1)] public float workChance = 0.35f;

    [Header("Бой")]
    public LayerMask unitMask;
    public float sightRadius = 10f;
    public float attackRange = 1.6f;
    public float damage = 12f;
    public float attackCooldown = 1.2f;
    public float hitDelay = 0.35f;            // момент удара внутри анимации замаха
    [Range(0, 1)] public float captureChance = 0.05f;

    [Header("Смерть")]
    public float corpseTime = 4f;

    public State Current { get; private set; }

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int AttackId = Animator.StringToHash("Attack");
    static readonly int HitId = Animator.StringToHash("Hit");
    static readonly int DieId = Animator.StringToHash("Die");
    static readonly Collider[] buffer = new Collider[32];

    NPC npc;
    NavMeshAgent agent;
    Animator anim;
    Collider[] colliders;
    WoodcutterJob job;
    Health target;
    float scanTimer, repathTimer, idleTimer, attackTimer, hitAt = -1f;

    public Animator Anim => anim;

    void Awake()
    {
        npc = GetComponent<NPC>();
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        colliders = GetComponentsInChildren<Collider>();
        job = new WoodcutterJob(npc, agent, anim);
        var h = GetComponent<Health>();
        h.Died += _ => Enter(State.Dead);
        h.Damaged += OnDamaged;
    }

    public void OnSpawned() => ResetState();
    public void OnDespawned() { job.Cancel(); target = null; StopAllCoroutines(); }

    public void ResetState()
    {
        Current = State.Peaceful;
        target = null;
        hitAt = -1f;
        scanTimer = Random.value * 0.5f;   // разносим поиск врагов по кадрам
        idleTimer = Random.Range(0.5f, 2f);
        foreach (var c in colliders) c.enabled = true;
        agent.enabled = true;
        if (anim) anim.Rebind();           // сбросить позу смерти у объекта из пула
    }

    void Enter(State s)
    {
        if (Current == State.Work && s != State.Work) job.Cancel();
        Current = s;
        hitAt = -1f;
        switch (s)
        {
            case State.Peaceful:
                agent.isStopped = false;
                idleTimer = Random.Range(0.5f, 2f);
                break;
            case State.Chase:
                agent.isStopped = false;
                repathTimer = 0f;
                break;
            case State.Attack:
                agent.isStopped = true;
                attackTimer = Random.Range(0.1f, 0.4f);
                break;
            case State.Dead:
                agent.isStopped = true;
                agent.enabled = false;
                foreach (var c in colliders) c.enabled = false;
                anim.SetTrigger(DieId);
                StartCoroutine(Corpse());
                break;
        }
    }

    void Update()
    {
        if (Current == State.Dead) return;
        agent.speed = baseSpeed * npc.aging.SpeedMultiplier;
        anim.SetFloat(SpeedId, agent.velocity.magnitude);

        if ((scanTimer -= Time.deltaTime) <= 0f)
        {
            scanTimer = 0.4f;
            Scan();
        }

        switch (Current)
        {
            case State.Peaceful: TickPeaceful(); break;
            case State.Work: if (!job.Tick()) Enter(State.Peaceful); break;
            case State.Chase: TickChase(); break;
            case State.Attack: TickAttack(); break;
        }
    }

    void Scan()
    {
        if (Current == State.Chase || Current == State.Attack)
        {
            if (TerritoryMap.Instance) TerritoryMap.Instance.TryCapture(transform.position, npc.factionId, captureChance);
            return;
        }
        var enemy = FindEnemy();
        if (!enemy) return;
        if (npc.aging.CanFight) { target = enemy; Enter(State.Chase); }
        else Flee(enemy.transform.position);
    }

    Health FindEnemy()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, sightRadius, buffer, unitMask);
        Health best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (!buffer[i].TryGetComponent<NPC>(out var other) || other == npc) continue;
            if (other.health.IsDead || !Factions.AtWar(npc.factionId, other.factionId)) continue;
            float d = (other.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = other.health; }
        }
        return best;
    }

    void Flee(Vector3 from)
    {
        var away = transform.position - from;
        away.y = 0f;
        if (Current == State.Work) Enter(State.Peaceful);
        if (NavMesh.SamplePosition(transform.position + away.normalized * 6f, out var hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        idleTimer = 2f;
    }

    void TickPeaceful()
    {
        if (agent.pathPending || agent.remainingDistance > 0.5f) return;
        if ((idleTimer -= Time.deltaTime) > 0f) return;
        idleTimer = Random.Range(2f, 6f);

        if (isWoodcutter && npc.aging.CanWork && Random.value < workChance && job.Begin())
        {
            Enter(State.Work);
            return;
        }
        var center = npc.home ? npc.home.transform.position : transform.position;
        var p = center + Random.insideUnitSphere * wanderRadius;
        if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    bool TargetValid() => target && !target.IsDead && target.isActiveAndEnabled;

    void TickChase()
    {
        if (!TargetValid()) { Enter(State.Peaceful); return; }
        float d = Vector3.Distance(transform.position, target.transform.position);
        if (d > sightRadius * 1.5f) { Enter(State.Peaceful); return; }
        if (d <= attackRange) { Enter(State.Attack); return; }
        if ((repathTimer -= Time.deltaTime) <= 0f)
        {
            repathTimer = 0.25f;
            agent.SetDestination(target.transform.position);
        }
    }

    void TickAttack()
    {
        if (!TargetValid()) { Enter(State.Peaceful); return; }
        var to = target.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude > attackRange * 1.25f) { Enter(State.Chase); return; }
        if (to.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 10f * Time.deltaTime);

        if (hitAt < 0f && (attackTimer -= Time.deltaTime) <= 0f)
        {
            anim.SetTrigger(AttackId);
            hitAt = Time.time + hitDelay;
            attackTimer = attackCooldown * Random.Range(0.85f, 1.15f);
        }
        if (hitAt > 0f && Time.time >= hitAt)
        {
            hitAt = -1f;
            if (to.magnitude <= attackRange * 1.4f)
            {
                float dmg = damage * Random.Range(0.7f, 1.3f) * (npc.aging.Stage == LifeStage.Elder ? 0.6f : 1f);
                target.TakeDamage(dmg, target.transform.position + Vector3.up * target.transform.localScale.y, npc.health);
            }
        }
    }

    void OnDamaged(Health self, float amount, Vector3 point, Health attacker)
    {
        if (self.IsDead) return;
        anim.SetTrigger(HitId);   // короткое вздрагивание на отдельном слое Animator
        if (!attacker || Current == State.Chase || Current == State.Attack) return;
        if (npc.aging.CanFight) { target = attacker; Enter(State.Chase); }
        else Flee(attacker.transform.position);
    }

    IEnumerator Corpse()
    {
        yield return new WaitForSeconds(corpseTime);
        // Тело уходит под землю, затем объект возвращается в пул.
        var start = transform.position;
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            transform.position = start + Vector3.down * (t / 1.5f);
            yield return null;
        }
        Pools.Despawn(gameObject);
    }
}
