using UnityEngine;

[DisallowMultipleComponent]
public class DebrisProbe : MonoBehaviour
{
    [SerializeField] private ParticleSystem ps;
    [SerializeField] private int count = 80;
    [SerializeField] private float speed = 6f;
    [SerializeField] private float spread = 2f;

    void Reset()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void OnEnable()
    {
        if (!ps) ps = GetComponent<ParticleSystem>();
        Debug.Log($"[DebrisProbe] OnEnable on '{name}'. ps={(ps ? "OK" : "NULL")}", this);
    }

    [ContextMenu("Emit NOW (Inspector Button)")]
    public void EmitNow()
    {
        if (!ps) ps = GetComponent<ParticleSystem>();
        if (!ps)
        {
            Debug.LogError("[DebrisProbe] No ParticleSystem on this GameObject.", this);
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var emitParams = new ParticleSystem.EmitParams();
        emitParams.position = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            Vector2 v = Random.insideUnitCircle * spread;
            emitParams.velocity = new Vector3(v.x, 1.0f + Mathf.Abs(v.y), 0f) * speed;
            ps.Emit(emitParams, 1);
        }

        ps.Play(true);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        Debug.Log($"[DebrisProbe] EMIT {count}. Renderer={(r ? "OK" : "NULL")}, " +
                  $"SortingLayer={(r ? r.sortingLayerName : "n/a")}, Order={(r ? r.sortingOrder : -1)}", this);
    }
}
