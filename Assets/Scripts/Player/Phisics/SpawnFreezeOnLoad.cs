using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class SpawnFreezeOnLoad : MonoBehaviour
{
    [Tooltip("Tiempo en segundos que el jugador permanece congelado al cargar la escena.")]
    public float freezeTime = 0.15f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Sólo hacemos freeze al cargar la escena.
        if (rb != null && freezeTime > 0f)
        {
            StartCoroutine(FreezeCoroutine());
        }
    }

    private IEnumerator FreezeCoroutine()
    {
        bool prevSim = rb.simulated;
        rb.simulated = false;

        yield return new WaitForSeconds(freezeTime);

        rb.simulated = prevSim;
    }
}
