using UnityEngine;

public class DebrisSpawner : MonoBehaviour
{
    public static DebrisSpawner Instance { get; private set; }

    [Header("Particle System")]
    [SerializeField] private ParticleSystem ps;

    [Header("Default Spawn Settings")]
    public int emitCount = 24;
    public Color startColor = Color.white;
    public float startSize = 0.25f;
    public float startLifetime = 9.2f;

    [Header("Motion (Default)")]
    public float speedMin = 0.5f;
    public float speedMax = 2.5f;
    public float spreadX = 1.0f;
    public float spreadY = 1.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (ps == null) ps = GetComponentInChildren<ParticleSystem>(true);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var em = ps.emission;
        em.enabled = false;

        ps.Clear(true);

    }


    // Tu API vieja (la conservamos)
    public void Spawn(Vector3 worldPos, int count)
    {
        SpawnCustom(
            worldPos: worldPos,
            count: count,
            startColor: startColor,
            startSize: startSize,
            startLifetime: startLifetime,
            speedMin: speedMin,
            speedMax: speedMax,
            spreadX: spreadX,
            spreadY: spreadY
        );
    }

    // NUEVA API (para tu TileBreakVFXManager)
    public void SpawnCustom(
        Vector3 worldPos,
        int count,
        Color startColor,
        float startSize,
        float startLifetime,
        float speedMin,
        float speedMax,
        float spreadX,
        float spreadY
    )
    {
        Debug.Log("[SPAWNER] SpawnCustom");
        if (ps == null) { Debug.LogError("[DebrisSpawner] ps NULL."); return; }
        if (count <= 0) return;

        worldPos.z = 0f;

        if (!ps.gameObject.activeInHierarchy)
        {
            Debug.LogError("[DebrisSpawner] ParticleSystem GameObject INACTIVO.");
            return;
        }

        if (!ps.isPlaying) ps.Play(true);

        var emitParams = new ParticleSystem.EmitParams();

        for (int i = 0; i < count; i++)
        {
            var main = ps.main;

            if (main.simulationSpace == ParticleSystemSimulationSpace.Local)
            {
                emitParams.position = ps.transform.InverseTransformPoint(worldPos);
            }
            else
            {
                emitParams.position = worldPos;
            }


            float vx = Random.Range(-spreadX, spreadX);
            float vy = Random.Range(-spreadY, spreadY);

            Vector3 v = (speedMax <= 0f)
                ? Vector3.zero
                : new Vector3(vx, vy, 0f).normalized * Random.Range(speedMin, speedMax);

            emitParams.velocity = v;
            emitParams.startLifetime = startLifetime;
            emitParams.startSize = startSize;
            emitParams.startColor = startColor;

            ps.Emit(emitParams, 1);
        }
    }

}
