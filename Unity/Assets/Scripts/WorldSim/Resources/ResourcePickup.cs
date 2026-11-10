using System.Collections;
using UnityEngine;

/// Бревно/ресурс: выпрыгивает с задержкой, лежит, при сборе подлетает к жителю и уходит в пул.
public class ResourcePickup : MonoBehaviour, IPoolable
{
    public int amount = 5;
    public float hopHeight = 0.8f;
    public float hopTime = 0.45f;
    public float lifetime = 60f;          // несобранное исчезает

    public bool Available { get; private set; }
    public bool Collected { get; private set; }

    Vector3 baseScale;

    void Awake() => baseScale = transform.localScale;

    public void OnSpawned()
    {
        Available = false;
        Collected = false;
        transform.localScale = Vector3.zero;
    }

    public void OnDespawned() => StopAllCoroutines();

    public void Launch(Vector3 from, Vector3 land, float delay) => StartCoroutine(Hop(from, land, delay));

    IEnumerator Hop(Vector3 start, Vector3 land, float delay)
    {
        yield return new WaitForSeconds(delay);
        for (float t = 0f; t < hopTime; t += Time.deltaTime)
        {
            float k = t / hopTime;
            transform.position = Vector3.Lerp(start, land, k) + Vector3.up * (4f * hopHeight * k * (1f - k));
            transform.localScale = baseScale * Mathf.Min(1f, k * 2f);
            yield return null;
        }
        transform.position = land;
        transform.localScale = baseScale;
        Available = true;
        yield return new WaitForSeconds(lifetime);
        if (!Collected) Pools.Despawn(gameObject);
    }

    public int Collect(Transform collector)
    {
        if (Collected) return 0;
        Collected = true;
        Available = false;
        StopAllCoroutines();
        StartCoroutine(FlyTo(collector));
        return amount;
    }

    IEnumerator FlyTo(Transform target)
    {
        var start = transform.position;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            transform.position = Vector3.Lerp(start, target.position + Vector3.up, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
            transform.localScale = baseScale * (1f - k);
            yield return null;
        }
        Pools.Despawn(gameObject);
    }
}
