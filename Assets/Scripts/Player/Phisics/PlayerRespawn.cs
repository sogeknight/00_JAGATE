using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Checkpoint")]
    public bool useCheckpoint = true;

    [Tooltip("Opcional: si lo asignas, NEW usará este spawn en vez de la pos inicial del player.")]
    public Transform startSpawnOverride;

    [Tooltip("Checkpoint actual en runtime (el último tocado en esta sesión).")]
    public Transform currentCheckpoint;

    [Header("Run Mode (NEW vs CONTINUE) - solo afecta al SPAWN INICIAL")]
    [Tooltip("Si está activo, al iniciar en NEW ignorará el save (pero NO lo borra).")]
    public bool newGameIgnoresSavedCheckpointOnStart = true;

    public KeyCode newGameKey = KeyCode.F1;     // fuerza NEW (spawn en inicio)
    public KeyCode continueKey = KeyCode.C;     // fuerza CONTINUE (spawn en save si existe)

    [Tooltip("Botón de mando para forzar CONTINUE (ej. SELECT / BACK).")]
    public KeyCode continuePadKey = KeyCode.JoystickButton6;

    [Header("Death Respawn")]
    [Tooltip("Si está activo, no recarga escena al morir; teleporta y listo.")]
    public bool respawnWithoutReload = true;

    [Tooltip("Si false, y NO hay checkpoint, recarga escena. Si true, teleporta a inicio igualmente.")]
    public bool teleportToInitialIfNoCheckpoint = true;

    [Header("Physics reset (Unity 6)")]
    public bool resetVelocityOnTeleport = true;

    [Header("Editor behavior")]
    [Tooltip("En el Editor, al dar Play, borra el checkpoint guardado para que CONTINUE solo funcione si lo has tocado en ESTA sesión.")]
    public bool editorClearCheckpointOnPlay = true;

    [Header("Spawn Freeze")]
    [Tooltip("Si está activo, al entrar en la escena congelamos al player un momento para que veas el spawn.")]
    public bool freezeOnSceneSpawn = true;

    [Tooltip("Duración del congelado de spawn (en segundos). Valores típicos 0.05–0.2.")]
    public float sceneSpawnFreezeTime = 0.12f;

    // ===== PlayerPrefs keys =====
    private const string PREF_RUNMODE = "RUN_MODE"; // 0 NEW, 1 CONTINUE
    private const string PREF_HAS = "CP_HAS";
    private const string PREF_X = "CP_X";
    private const string PREF_Y = "CP_Y";
    private const string PREF_ID = "CP_ID";

    // Keys por escena (evita que se mezcle entre escenas)
    private string K(string baseKey)
    {
        return $"{baseKey}__{SceneManager.GetActiveScene().name}";
    }

    private Rigidbody2D rb;
    private Vector3 initialSpawnPos;

    // control interno del freeze
    private Coroutine spawnFreezeCoroutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Posición inicial = donde esté el player en la escena (o override)
        initialSpawnPos = (startSpawnOverride != null) ? startSpawnOverride.position : transform.position;
    }

    private void Start()
    {
#if UNITY_EDITOR
        int mode = GetRunMode(); // 0 NEW, 1 CONTINUE

        // Solo limpiar checkpoint al arrancar si estás en NEW
        if (mode == 0 && editorClearCheckpointOnPlay)
        {
            ClearSavedCheckpoint();
            Debug.Log("[PlayerRespawn] Editor clear checkpoint on play (NEW).");
        }
#endif

        ApplySpawnForSceneEntry();
    }

    private void Update()
    {
        // Teclas / botones para decidir desde la propia TrainingArea (sin menú)
        bool newGameInput    = Input.GetKeyDown(newGameKey);
        bool continueInput   = Input.GetKeyDown(continueKey) || Input.GetKeyDown(continuePadKey);

        if (newGameInput)
        {
            SetRunMode(0);
            ApplySpawnForSceneEntry();
        }
        else if (continueInput)
        {
            SetRunMode(1);
            ApplySpawnForSceneEntry();
        }
    }

    // =========================
    // API para tus checkpoints
    // =========================
    public void SetCheckpoint(Transform checkpoint)
    {
        if (!useCheckpoint) return;

        currentCheckpoint = checkpoint;
        Vector3 p = checkpoint != null ? checkpoint.position : initialSpawnPos;

        SaveCheckpointPrefs(checkpoint != null ? checkpoint.name : "NULL", p);

        Debug.Log($"[PlayerRespawn] SetCheckpoint -> {(checkpoint ? checkpoint.name : "NULL")} @ {p}");
    }

    public void SetCheckpoint(Vector3 worldPos)
    {
        if (!useCheckpoint) return;

        currentCheckpoint = null; // runtime no transform, pero guardamos posición
        SaveCheckpointPrefs("POS", worldPos);

        Debug.Log($"[PlayerRespawn] SetCheckpoint(Vector3) -> {worldPos}");
    }

    // =========================
    // Lo que tú quieres: MORIR => checkpoint si existe
    // =========================
    public void RespawnAfterDeath()
    {
        Vector3 target = GetBestRespawnPoint();

        // CLAVE: aunque hayas empezado NEW, si ya hay checkpoint, respawnea ahí.
        bool hasAnyCheckpoint = HasRuntimeCheckpoint() || HasSavedCheckpoint();

        if (respawnWithoutReload || hasAnyCheckpoint)
        {
            TeleportTo(target);
            Debug.Log($"[PlayerRespawn] RespawnAfterDeath -> {target} (hasCP={hasAnyCheckpoint})");
            return;
        }

        // Si no hay checkpoint y no quieres teleport directo:
        if (teleportToInitialIfNoCheckpoint)
        {
            TeleportTo(initialSpawnPos);
            Debug.Log($"[PlayerRespawn] RespawnAfterDeath -> INITIAL {initialSpawnPos} (no checkpoint)");
            return;
        }

        // Último recurso: recargar escena
        Debug.Log("[PlayerRespawn] RespawnAfterDeath -> Reload scene (no checkpoint)");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // =========================
    // Spawn al ENTRAR a la escena (NEW vs CONTINUE)
    // =========================
    private void ApplySpawnForSceneEntry()
    {
        int mode = GetRunMode(); // 0 NEW, 1 CONTINUE

        if (mode == 1)
        {
            // CONTINUE: si hay save, úsalo; si no, inicio
            Vector3 p = HasSavedCheckpoint() ? GetSavedCheckpointPosition() : initialSpawnPos;
            TeleportTo(p);
            Debug.Log($"[PlayerRespawn] Start spawn (CONTINUE) -> {p} (hasSave={HasSavedCheckpoint()})");
        }
        else
        {
            // NEW: inicio SIEMPRE (aunque exista save), pero NO se borra el save.
            if (newGameIgnoresSavedCheckpointOnStart)
            {
                TeleportTo(initialSpawnPos);
                Debug.Log($"[PlayerRespawn] Start spawn (NEW) -> {initialSpawnPos} (save ignored on start)");
            }
            else
            {
                Vector3 p = HasSavedCheckpoint() ? GetSavedCheckpointPosition() : initialSpawnPos;
                TeleportTo(p);
                Debug.Log($"[PlayerRespawn] Start spawn (NEW but allowed save) -> {p}");
            }
        }

        StartSpawnFreezeIfNeeded();
    }

    // =========================
    // Helpers
    // =========================
    private Vector3 GetBestRespawnPoint()
    {
        // Prioridad: checkpoint runtime > checkpoint guardado > inicio
        if (HasRuntimeCheckpoint()) return currentCheckpoint.position;
        if (HasSavedCheckpoint()) return GetSavedCheckpointPosition();
        return initialSpawnPos;
    }

    private bool HasRuntimeCheckpoint()
    {
        return useCheckpoint && currentCheckpoint != null;
    }

    private bool HasSavedCheckpoint()
    {
        return useCheckpoint && PlayerPrefs.GetInt(K(PREF_HAS), 0) == 1;
    }

    private Vector3 GetSavedCheckpointPosition()
    {
        float x = PlayerPrefs.GetFloat(K(PREF_X), initialSpawnPos.x);
        float y = PlayerPrefs.GetFloat(K(PREF_Y), initialSpawnPos.y);
        return new Vector3(x, y, transform.position.z);
    }

    private void SaveCheckpointPrefs(string id, Vector3 p)
    {
        PlayerPrefs.SetInt(K(PREF_HAS), 1);
        PlayerPrefs.SetFloat(K(PREF_X), p.x);
        PlayerPrefs.SetFloat(K(PREF_Y), p.y);
        PlayerPrefs.SetString(K(PREF_ID), id);
        PlayerPrefs.Save();
    }

    public void ClearSavedCheckpoint()
    {
        PlayerPrefs.DeleteKey(K(PREF_HAS));
        PlayerPrefs.DeleteKey(K(PREF_X));
        PlayerPrefs.DeleteKey(K(PREF_Y));
        PlayerPrefs.DeleteKey(K(PREF_ID));
        PlayerPrefs.Save();

        currentCheckpoint = null;

        Debug.Log($"[PlayerRespawn] ClearSavedCheckpoint() (scene={SceneManager.GetActiveScene().name})");
    }

    private int GetRunMode()
    {
        return PlayerPrefs.GetInt(PREF_RUNMODE, 0);
    }

    private void SetRunMode(int mode)
    {
        PlayerPrefs.SetInt(PREF_RUNMODE, mode);
        PlayerPrefs.Save();
        Debug.Log("[PlayerRespawn] SetRunMode -> " + mode);
    }

    private void TeleportTo(Vector3 worldPos)
    {
        if (rb != null)
        {
            rb.position = worldPos;

            if (resetVelocityOnTeleport)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
        else
        {
            transform.position = worldPos;
        }
    }

    // Compatibilidad con código antiguo que llama TriggerGameOver()
    public void TriggerGameOver()
    {
        RespawnAfterDeath();
    }

    // =========================
    // Spawn freeze SIN tocar rb.simulated
    // =========================
    private void StartSpawnFreezeIfNeeded()
    {
        if (!freezeOnSceneSpawn || rb == null)
            return;

        if (spawnFreezeCoroutine != null)
            StopCoroutine(spawnFreezeCoroutine);

        spawnFreezeCoroutine = StartCoroutine(SceneSpawnFreezeCoroutine());
    }

    private IEnumerator SceneSpawnFreezeCoroutine()
    {
        RigidbodyType2D prevBodyType = rb.bodyType;
        float prevGravity = rb.gravityScale;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        yield return new WaitForSeconds(sceneSpawnFreezeTime);

        rb.bodyType = prevBodyType;
        rb.gravityScale = prevGravity;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        spawnFreezeCoroutine = null;
    }
}
