using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint")]
    public string checkpointId = "CP_01";
    public bool refillFlame = true;

    [Header("Opcional: feedback")]
    public AudioSource sfx;
    public GameObject activateVfx;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 1) CANÓNICO: PlayerRespawn guarda y gestiona CONTINUE (tecla C)
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null) return;

        // Guardar el checkpoint real (por escena, según tu PlayerRespawn)
        respawn.SetCheckpoint(transform);

        // 2) Recargar flame (si existe el script)
        if (refillFlame)
        {
            var bounce = other.GetComponentInParent<PlayerBounceAttack>();
            if (bounce != null)
                bounce.flame = bounce.maxFlame;
        }

        if (sfx) sfx.Play();
        if (activateVfx) activateVfx.SetActive(true);
    }
}
