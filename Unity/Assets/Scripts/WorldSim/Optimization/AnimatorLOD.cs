using UnityEngine;

/// Анимационный LOD: дальние юниты обновляют Animator реже (каждый 3-й/6-й кадр).
public class AnimatorLOD : MonoBehaviour
{
    public Animator animator;
    public float nearDistance = 25f, farDistance = 60f;

    static Camera cam;
    float accumulated;
    int frameOffset;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        frameOffset = Random.Range(0, 6);
    }

    void OnDisable() { if (animator) animator.enabled = true; }

    void Update()
    {
        if (!cam) cam = Camera.main;
        float d = (cam.transform.position - transform.position).sqrMagnitude;
        int every = d < nearDistance * nearDistance ? 1 : d < farDistance * farDistance ? 3 : 6;

        if (every == 1)
        {
            if (!animator.enabled) animator.enabled = true;
            accumulated = 0f;
            return;
        }
        if (animator.enabled) animator.enabled = false;
        accumulated += Time.deltaTime;
        if ((Time.frameCount + frameOffset) % every == 0)
        {
            animator.Update(accumulated);
            accumulated = 0f;
        }
    }
}
