using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// Реализуйте на любом компоненте пулируемого объекта, чтобы сбрасывать состояние.
public interface IPoolable
{
    void OnSpawned();
    void OnDespawned();
}

/// Добавляется автоматически к каждому объекту из пула.
public sealed class PooledObject : MonoBehaviour
{
    internal ObjectPool<GameObject> Pool;
    internal IPoolable[] Callbacks;
    bool inPool;

    internal void MarkSpawned() { inPool = false; CancelInvoke(); }

    public void Despawn()
    {
        if (inPool) return;
        inPool = true;
        CancelInvoke();
        Pool.Release(gameObject);
    }

    public void DespawnAfter(float seconds) => Invoke(nameof(Despawn), seconds);
}

/// Единая точка создания/удаления: юниты, дома, эффекты, брёвна. Instantiate/Destroy в игре не вызываем.
public static class Pools
{
    static readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    static Transform root;

    static ObjectPool<GameObject> GetPool(GameObject prefab)
    {
        // Корень уничтожается при смене сцены — тогда старые пулы недействительны.
        if (root == null)
        {
            pools.Clear();
            root = new GameObject("[Pools]").transform;
        }
        if (pools.TryGetValue(prefab, out var pool)) return pool;

        ObjectPool<GameObject> p = null;
        p = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                var go = Object.Instantiate(prefab, root);
                go.SetActive(false);
                var po = go.AddComponent<PooledObject>();
                po.Pool = p;
                po.Callbacks = go.GetComponentsInChildren<IPoolable>(true);
                return go;
            },
            actionOnGet: null,
            actionOnRelease: go =>
            {
                foreach (var c in go.GetComponent<PooledObject>().Callbacks) c.OnDespawned();
                go.SetActive(false);
                go.transform.SetParent(root, false);
            },
            actionOnDestroy: Object.Destroy,
            collectionCheck: false, defaultCapacity: 32, maxSize: 10000);
        pools[prefab] = p;
        return p;
    }

    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        var go = GetPool(prefab).Get();
        var t = go.transform;
        if (parent) t.SetParent(parent, false);
        t.SetPositionAndRotation(pos, rot);
        var po = go.GetComponent<PooledObject>();
        po.MarkSpawned();
        go.SetActive(true);
        foreach (var c in po.Callbacks) c.OnSpawned();
        return go;
    }

    public static T Spawn<T>(T prefab, Vector3 pos, Quaternion rot, Transform parent = null) where T : Component
        => Spawn(prefab.gameObject, pos, rot, parent).GetComponent<T>();

    public static void Despawn(GameObject go, float delay = 0f)
    {
        var po = go.GetComponent<PooledObject>();
        if (po == null) { Object.Destroy(go, delay); return; }
        if (delay > 0f) po.DespawnAfter(delay); else po.Despawn();
    }

    /// Создать объекты заранее (на экране загрузки), чтобы не было фризов в бою.
    public static void Prewarm(GameObject prefab, int count)
    {
        var pool = GetPool(prefab);
        var tmp = new List<GameObject>(count);
        for (int i = 0; i < count; i++) tmp.Add(pool.Get());
        foreach (var g in tmp) g.GetComponent<PooledObject>().Despawn();
    }
}
