using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class IngamePauseMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;

    [Header("Buttons")]
    public Button continueButton;
    public Button restartButton;
    public Button loadCheckpointButton;
    public Button returnToMainButton;
    public Button exitGameButton;

    [Header("Scenes")]
    public string mainMenuSceneName = "00_Menu";

    [Header("Input lock")]
    [Tooltip("Tiempo en segundos que se bloquea el movimiento del player al salir de la pausa para evitar que la pulsación afecte al gameplay.")]
    public float resumeInputDelay = 0.12f;

    [Header("UI Timing")]
    [Tooltip("Tiempo mínimo que se mantiene el color de 'pressed' antes de ejecutar el onClick (tiempo de cortesía).")]
    public float pressFlashDuration = 0.14f;

    private PlayerRespawn respawn;
    private PlayerMovementController move;

    private bool isPaused;
    private Coroutine resumeInputCoroutine;

    private const string PREF_RUNMODE = "RUN_MODE";

    // MISMAS KEYS QUE PlayerRespawn
    private const string PREF_HAS = "CP_HAS";
    private const string PREF_X   = "CP_X";
    private const string PREF_Y   = "CP_Y";
    private const string PREF_ID  = "CP_ID";

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        respawn = FindObjectOfType<PlayerRespawn>();
        move    = FindObjectOfType<PlayerMovementController>();

        NormalizeButtonColors();
        RefreshLoadCheckpointButton();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeButtonColors();
    }
#endif

    // =========================
    //  COLORES (Selected = Highlighted)
    // =========================
    private void NormalizeButtonColors()
    {
        ApplyNorm(continueButton);
        ApplyNorm(restartButton);
        ApplyNorm(loadCheckpointButton);
        ApplyNorm(returnToMainButton);
        ApplyNorm(exitGameButton);
    }

    private void ApplyNorm(Button btn)
    {
        if (!btn) return;
        var c = btn.colors;
        c.selectedColor = c.highlightedColor;
        btn.colors = c;
    }

    // =========================
    //  UPDATE
    // =========================
    private void Update()
    {
        // Abrir / cerrar pausa: ESC o START (JoystickButton7)
        bool pauseInput =
            Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.JoystickButton7);

        if (pauseInput)
        {
            TogglePause();
        }

        if (!isPaused)
            return;

        HandleSubmitInput();

        // Si se pierde selección, la restauramos SIEMPRE al primer botón
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject == null)
        {
            EnsureInitialSelection();
        }
    }

    // =========================
    //  PAUSA + BLOQUEO MOVIMIENTO
    // =========================
    public void TogglePause()
    {
        isPaused = !isPaused;

        if (panelRoot)
            panelRoot.SetActive(isPaused);

        Time.timeScale = isPaused ? 0f : 1f;

        if (isPaused)
        {
            // Al entrar en pausa: bloquear movimiento y refrescar botón de checkpoint
            LockPlayerMovement(true);
            RefreshLoadCheckpointButton();
            EnsureInitialSelection();
        }
        else
        {
            // Al salir de pausa: mantener bloqueado un rato para que la pulsación
            // del botón (Z / X / A / etc.) NO afecte al movimiento.
            if (resumeInputCoroutine != null)
                StopCoroutine(resumeInputCoroutine);

            resumeInputCoroutine = StartCoroutine(ResumeMovementAfterDelay());
        }
    }

    private void LockPlayerMovement(bool locked)
    {
        if (move != null)
            move.movementLocked = locked;
    }

    private IEnumerator ResumeMovementAfterDelay()
    {
        // Espera en tiempo REAL, independiente de timeScale
        float t = Mathf.Max(0f, resumeInputDelay);
        if (t > 0f)
            yield return new WaitForSecondsRealtime(t);

        LockPlayerMovement(false);
    }

    // =========================
    //  SELECCIÓN INICIAL
    // =========================
    private void EnsureInitialSelection()
    {
        if (!EventSystem.current) return;

        // Siempre forzamos selección al primer botón lógico
        Button first =
            continueButton ? continueButton :
            restartButton ? restartButton :
            (loadCheckpointButton && loadCheckpointButton.interactable) ? loadCheckpointButton :
            returnToMainButton ? returnToMainButton :
            exitGameButton ? exitGameButton :
            null;

        if (first)
        {
            EventSystem.current.SetSelectedGameObject(first.gameObject);
            first.Select(); // esto activa el SelectedColor (igual que Highlighted)
        }
    }

    // =========================
    //  SUBMIT (Z / mando)
    // =========================
    private void HandleSubmitInput()
    {
        if (!EventSystem.current) return;

        var go = EventSystem.current.currentSelectedGameObject;
        if (!go) return;

        var btn = go.GetComponent<Button>();
        if (!btn || !btn.interactable) return;

        // Teclado: Z / Enter / Espacio
        bool keyboardSubmit =
            Input.GetKeyDown(KeyCode.Z) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Space);

        // Mando: mismos botones que en el MainMenu (0–3)
        bool gamepadSubmit =
            Input.GetKeyDown(KeyCode.JoystickButton0) ||
            Input.GetKeyDown(KeyCode.JoystickButton1) ||
            Input.GetKeyDown(KeyCode.JoystickButton2) ||
            Input.GetKeyDown(KeyCode.JoystickButton3);

        if (keyboardSubmit || gamepadSubmit)
        {
            StartCoroutine(PressFlashAndClick(btn));
        }
    }

    private IEnumerator PressFlashAndClick(Button btn)
    {
        if (!btn) yield break;

        // Nos aseguramos de que ESTE botón es el seleccionado en el EventSystem,
        // igual que hace Unity al navegar con cruceta/joystick.
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(btn.gameObject);
            btn.Select();
        }

        Graphic g = btn.targetGraphic;
        if (!g)
        {
            btn.onClick.Invoke();
            yield break;
        }

        var colors = btn.colors;
        var originalColor = g.color;
        var originalTransition = btn.transition;

        // Apagamos la transición para que Unity no nos pise el PressedColor
        btn.transition = Selectable.Transition.None;

        // Color de pulsación EXACTO del inspector
        g.color = colors.pressedColor;

        // Tiempo mínimo de cortesía para que se vea el estado "pressed"
        float wait = Mathf.Max(0.05f, colors.fadeDuration, pressFlashDuration);
        yield return new WaitForSecondsRealtime(wait);

        // Volvemos al color anterior (Selected/Highlighted)
        g.color = originalColor;
        btn.transition = originalTransition;

        // Lógica real del botón
        btn.onClick.Invoke();
    }

    // =========================
    //  CHECKPOINT: HABILITAR / DESHABILITAR BOTÓN
    // =========================

    private string K(string baseKey)
    {
        // MISMA CONVENCIÓN QUE PlayerRespawn
        return $"{baseKey}__{SceneManager.GetActiveScene().name}";
    }

    private bool HasCheckpointSave()
    {
        return PlayerPrefs.GetInt(K(PREF_HAS), 0) == 1;
    }

    private void ClearCheckpointSave()
    {
        PlayerPrefs.DeleteKey(K(PREF_HAS));
        PlayerPrefs.DeleteKey(K(PREF_X));
        PlayerPrefs.DeleteKey(K(PREF_Y));
        PlayerPrefs.DeleteKey(K(PREF_ID));
        PlayerPrefs.Save();
    }

    private void RefreshLoadCheckpointButton()
    {
        bool has = HasCheckpointSave();
        if (loadCheckpointButton != null)
            loadCheckpointButton.interactable = has;

        Debug.Log($"[IngamePauseMenu] RefreshLoadCheckpointButton -> hasCheckpoint={has}");
    }

    // =========================
    //  CALLBACKS DE BOTONES
    // =========================

    public void OnContinueButton()
    {
        if (isPaused)
            TogglePause();
    }

    public void OnRestartButton()
    {
        // RESTART = NEW GAME sin checkpoint previo de esta escena
        Time.timeScale = 1f;

        // Limpia checkpoint de ESTA escena
        ClearCheckpointSave();

        // Modo NEW
        PlayerPrefs.SetInt(PREF_RUNMODE, 0);
        PlayerPrefs.Save();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnLoadCheckpointButton()
    {
        // Defensa dura extra: si no hay checkpoint, NO haces nada (ni aunque el botón esté mal configurado)
        if (!HasCheckpointSave())
        {
            Debug.LogWarning("[IngamePauseMenu] OnLoadCheckpointButton() sin checkpoint. Ignorado.");
            RefreshLoadCheckpointButton();
            return;
        }

        if (isPaused)
            TogglePause();

        // 1) Matar cualquier SparkBoost activo para que no secuestre al player/cámara
        var spark = FindObjectOfType<PlayerSparkBoost>();
        if (spark != null)
        {
            spark.ForceEndAll();
        }

        // 2) Respawn real del player
        if (respawn)
        {
            respawn.RespawnAfterDeath();
        }
        else
        {
            Debug.LogWarning("[IngamePauseMenu] No PlayerRespawn found in scene.");
        }

        // 3) Pedir a la cámara que se ENGANCHÉ YA al checkpoint
        var cam = FindObjectOfType<FollowCamera2D>();
        if (cam != null)
        {
            cam.RequestImmediateSnap();
        }
    }

    public void OnReturnToMainMenuButton()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OnExitGameButton()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
