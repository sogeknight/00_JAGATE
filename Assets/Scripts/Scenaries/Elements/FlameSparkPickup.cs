using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class FlameSparkPickup : MonoBehaviour
{
    [Header("Spark Pickup")]
    public float windowDuration = 1.0f;
    public Transform anchorPoint;

    [Header("Consume / Respawn")]
    public bool destroyOnPickup = true;
    public float respawnSeconds = 0f;

    private SpriteRenderer sr;
    private Collider2D col;

    private bool available = true;
    private Coroutine respawnCo;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        if (col != null) col.isTrigger = true;
        if (anchorPoint == null) anchorPoint = transform;

        available = true;
    }

    public Vector2 GetAnchorWorld()
    {
        return (anchorPoint != null) ? (Vector2)anchorPoint.position : (Vector2)transform.position;
    }

    public bool IsAvailable() => available;

    // ====== API que llama PlayerSparkBoost (dash) ======
    public void Consume()
    {
        if (!available) return;
        ConsumeInternal();
    }

    // Alias por compatibilidad si en algún momento lo llamas con otro nombre
    public void ConsumeImmediate()
    {
        if (!available) return;
        ConsumeInternal();
    }

    private void ConsumeInternal()
    {
        available = false;

        if (sr != null) sr.enabled = false;
        if (col != null) col.enabled = false;

        if (destroyOnPickup)
        {
            Destroy(gameObject);
            return;
        }

        if (respawnSeconds > 0f)
        {
            if (respawnCo != null) StopCoroutine(respawnCo);
            respawnCo = StartCoroutine(RespawnAfter(respawnSeconds));
        }
    }

    private IEnumerator RespawnAfter(float secs)
    {
        yield return new WaitForSeconds(secs);

        available = true;

        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;

        respawnCo = null;
    }

    // ====== Pick-up por caminar (trigger normal) ======
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!available) return;

        var spark = other.GetComponent<PlayerSparkBoost>() ?? other.GetComponentInParent<PlayerSparkBoost>();
        if (spark == null) return;

        // Si está en dash o ya tiene spark activo, NO gestionamos nada aquí
        if (spark.IsDashing() || spark.IsSparkActive()) return;

        // Caminar encima => abre ventana de spark anclada, SIN bounce físico
        spark.ActivateSpark(windowDuration, GetAnchorWorld());

        // Consumimos (respawn o destroy según config)
        ConsumeInternal();
    }
}
