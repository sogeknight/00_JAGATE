using UnityEngine;
using System.Collections.Generic;

public class DebrisSpawner : MonoBehaviour
{
    public static DebrisSpawner Instance { get; private set; }

    [Header("Particle System")]
    [SerializeField] private ParticleSystem ps;

    [Header("Default Spawn Settings")]
    public int emitCount = 24;
    public Color startColor = Color.white;
    public float startSize = 0.25f;
    public float startLifetime = 2.5f;

    [Header("Motion (Default)")]
    public float speedMin = 0.5f;
    public float speedMax = 2.5f;
    public float spreadX = 1.0f;
    public float spreadY = 1.0f;

    [Header("Anti-spam (crítico)")]
    [Tooltip("Máximo de partículas que este spawner puede emitir por frame. Mantiene la cadencia sin inundar.")]
    public int maxEmitPerFrame = 10;

    [Tooltip("Si muchos subtiles rompen en el mismo frame, se agrupan en celdas. Más grande = más agrupación, menos spam.")]
    public float mergeCellSize = 0.5f;

    private struct Req
    {
        public Vector3 pos;
        public int count;
        public Color color;
        public float size;
        public float lifetime;
        public float vMin, vMax;
        public float sX, sY;
    }

    private readonly List<Req> queue = new List<Req>(256);
    private readonly Dictionary<Vector2Int, int> cellToReqIndex = new Dictionary<Vector2Int, int>(256);

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

    // API vieja
    public void Spawn(Vector3 worldPos, int count)
    {
        SpawnCustom(worldPos, count, startColor, startSize, startLifetime, speedMin, speedMax, spreadX, spreadY);
    }

    // API nueva
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
        if (ps == null) return;
        if (count <= 0) return;

        worldPos.z = 0f;

        // Encola y agrupa por celda para que 30 subtiles no sean 30 bursts separados.
        Vector2Int cell = new Vector2Int(
            Mathf.FloorToInt(worldPos.x / Mathf.Max(0.0001f, mergeCellSize)),
            Mathf.FloorToInt(worldPos.y / Mathf.Max(0.0001f, mergeCellSize))
        );

        if (cellToReqIndex.TryGetValue(cell, out int idx))
        {
            // Sumamos cantidad, cap suave (evita explosiones)
            var r = queue[idx];
            r.count += count;
            queue[idx] = r;
            return;
        }

        var req = new Req
        {
            pos = worldPos,
            count = count,
            color = startColor,
            size = startSize,
            lifetime = startLifetime,
            vMin = speedMin,
            vMax = speedMax,
            sX = spreadX,
            sY = spreadY
        };

        cellToReqIndex[cell] = queue.Count;
        queue.Add(req);
    }

    private void LateUpdate()
    {
        Flush();
    }

    private void Flush()
    {
        if (ps == null) return;
        if (queue.Count == 0) return;

        if (!ps.gameObject.activeInHierarchy) { queue.Clear(); cellToReqIndex.Clear(); return; }
        if (!ps.isPlaying) ps.Play(true);

        int budget = Mathf.Max(1, maxEmitPerFrame);
        var main = ps.main;
        bool local = (main.simulationSpace == ParticleSystemSimulationSpace.Local);

        var emitParams = new ParticleSystem.EmitParams();

        // Emitimos hasta agotar budget, distribuyendo entre requests
        for (int r = 0; r < queue.Count && budget > 0; r++)
        {
            var q = queue[r];
            int n = Mathf.Min(q.count, budget);

            for (int i = 0; i < n; i++)
            {
                emitParams.position = local ? ps.transform.InverseTransformPoint(q.pos) : q.pos;

                float vx = Random.Range(-q.sX, q.sX);
                float vy = Random.Range(-q.sY, q.sY);

                Vector3 v = (q.vMax <= 0f)
                    ? Vector3.zero
                    : new Vector3(vx, vy, 0f).normalized * Random.Range(q.vMin, q.vMax);

                emitParams.velocity = v;
                emitParams.startLifetime = q.lifetime;
                emitParams.startSize = q.size;
                emitParams.startColor = q.color;

                ps.Emit(emitParams, 1);
            }

            budget -= n;
        }

        queue.Clear();
        cellToReqIndex.Clear();
    }
}
