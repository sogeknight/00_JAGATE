using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Nombre EXACTO de la escena de gameplay (solo para las claves de PlayerPrefs).")]
    public string gameplaySceneName = "0000_MaterialTest";

    [Tooltip("Build index de la escena de gameplay (míralo en File > Build Settings).")]
    public int gameplaySceneIndex = 1;  // Índice de 0000_MaterialTest

    [Header("UI Buttons")]
    [Tooltip("Botón NEW GAME del menú principal.")]
    public Button newGameButton;

    [Tooltip("Botón CONTINUE del menú principal.")]
    public Button continueButton;

    [Tooltip("Botón QUIT del menú principal.")]
    public Button quitButton;

    private const string PREF_RUNMODE = "RUN_MODE"; // 0 NEW, 1 CONTINUE

    // mismas base keys que PlayerRespawn
    private const string PREF_HAS = "CP_HAS";
    private const string PREF_X   = "CP_X";
    private const string PREF_Y   = "CP_Y";
    private const string PREF_ID  = "CP_ID";

    // Sufijo por escena, usando el nombre de gameplaySceneName
    private string K(string baseKey)
    {
        return $"{baseKey}__{gameplaySceneName}";
    }

    public bool HasSave()
    {
        return PlayerPrefs.GetInt(K(PREF_HAS), 0) == 1;
    }

    // ---------------------------------------------------------
    //  CICLO DE VIDA
    // ---------------------------------------------------------

    private void Awake()
    {
        // Selected = Highlighted para cruceta/joystick
        NormalizeButtonColors();

        RefreshContinueButton();
        EnsureInitialSelection();
    }

    private void OnEnable()
    {
        RefreshContinueButton();
        EnsureInitialSelection();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeButtonColors();
    }
#endif

    /// <summary>
    /// Copia HighlightedColor -> SelectedColor en cada botón.
    /// No toca Normal / Pressed / Disabled.
    /// </summary>
    private void NormalizeButtonColors()
    {
        ApplyColorNormalization(newGameButton);
        ApplyColorNormalization(continueButton);
        ApplyColorNormalization(quitButton);
    }

    private void ApplyColorNormalization(Button btn)
    {
        if (btn == null) return;

        var cb = btn.colors;
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;
    }

    private void RefreshContinueButton()
    {
        bool has = HasSave();

        if (continueButton != null)
            continueButton.interactable = has;

        Debug.Log($"[MainMenu] RefreshContinueButton -> hasSave={has}");
    }

    /// <summary>
    /// Asegura que siempre haya un botón seleccionado en el EventSystem.
    /// </summary>
    private void EnsureInitialSelection()
    {
        if (EventSystem.current == null)
            return;

        if (EventSystem.current.currentSelectedGameObject != null)
            return;

        Button first = null;

        // Prioridad: Continue si es interactuable, luego New, luego Quit
        if (continueButton != null && continueButton.interactable)
            first = continueButton;
        else if (newGameButton != null)
            first = newGameButton;
        else if (quitButton != null)
            first = quitButton;

        if (first != null)
        {
            EventSystem.current.SetSelectedGameObject(first.gameObject);
            first.Select(); // entra en estado Selected (color = Highlighted)
        }
    }

    // ---------------------------------------------------------
    //  UPDATE: solo Submit (Z / mando). La navegación la hace UNITY.
    // ---------------------------------------------------------

    private void Update()
    {
        // Si por lo que sea se pierde la selección, la recuperamos
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == null)
        {
            EnsureInitialSelection();
        }

        HandleSubmitInput();
    }

    private void HandleSubmitInput()
    {
        if (EventSystem.current == null)
            return;

        GameObject current = EventSystem.current.currentSelectedGameObject;
        if (current == null)
            return;

        Button btn = current.GetComponent<Button>();
        if (btn == null || !btn.interactable)
            return;

        // Teclado: Z, Enter o Espacio
        bool keyboardSubmit =
            Input.GetKeyDown(KeyCode.Z) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Space);

        // Mando: probamos varios botones típicos (0–3)
        bool gamepadSubmit =
            Input.GetKeyDown(KeyCode.JoystickButton0) ||   // suele ser A / X
            Input.GetKeyDown(KeyCode.JoystickButton1) ||
            Input.GetKeyDown(KeyCode.JoystickButton2) ||
            Input.GetKeyDown(KeyCode.JoystickButton3);

        if (keyboardSubmit || gamepadSubmit)
        {
            StartCoroutine(PressFlashAndClick(btn));
        }
    }

    /// <summary>
    /// Fuerza el color Pressed del botón durante un instante, luego hace onClick.
    /// Desactiva temporalmente el ColorTint del Button para que Unity no pise el color.
    /// </summary>
    private IEnumerator PressFlashAndClick(Button btn)
    {
        if (btn == null)
            yield break;

        var graphic = btn.targetGraphic; // normalmente la Image del botón
        if (graphic == null)
        {
            btn.onClick.Invoke();
            yield break;
        }

        var colors = btn.colors;
        var originalColor = graphic.color;
        var originalTransition = btn.transition;

        // Desactivamos la transición automática para que NO nos pise el color
        btn.transition = Selectable.Transition.None;

        // Aplicamos manualmente el PressedColor
        graphic.color = colors.pressedColor;

        // Tiempo mínimo para que se note. Usamos fadeDuration como referencia.
        float wait = Mathf.Max(0.05f, colors.fadeDuration);
        yield return new WaitForSecondsRealtime(wait);

        // Volvemos al color que tenía (Selected/Highlighted)
        graphic.color = originalColor;

        // Restauramos el tipo de transición original
        btn.transition = originalTransition;

        // Ejecutamos el click de verdad (NewGame / Continue / Quit)
        btn.onClick.Invoke();
    }

    // ---------------------------------------------------------
    //  BOTONES
    // ---------------------------------------------------------

    public void NewGame()
    {
        // NEW GAME: modo 0 SIEMPRE
        PlayerPrefs.SetInt(PREF_RUNMODE, 0);

        // LIMPIAR checkpoint SOLO de ESTA escena
        PlayerPrefs.DeleteKey(K(PREF_HAS));
        PlayerPrefs.DeleteKey(K(PREF_X));
        PlayerPrefs.DeleteKey(K(PREF_Y));
        PlayerPrefs.DeleteKey(K(PREF_ID));

        PlayerPrefs.Save();

        Debug.Log("[MainMenu] NewGame() -> RUN_MODE=0, checkpoint borrado, cargando escena");
        LoadGameplayScene();
    }

    public void Continue()
    {
        // Defensa dura: si no hay checkpoint, no hacemos nada
        if (!HasSave())
        {
            Debug.LogWarning("[MainMenu] Continue() sin checkpoint -> ignorado. El botón debería estar DESACTIVADO.");
            RefreshContinueButton();
            return;
        }

        // CONTINUE: modo 1, NO borramos el checkpoint
        PlayerPrefs.SetInt(PREF_RUNMODE, 1);
        PlayerPrefs.Save();

        Debug.Log("[MainMenu] Continue() -> RUN_MODE=1, usando checkpoint guardado, cargando escena");
        LoadGameplayScene();
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------
    //  CARGA DE ESCENA (POR ÍNDICE)
    // ---------------------------------------------------------

    private void LoadGameplayScene()
    {
        Debug.Log($"[MainMenu] LoadGameplayScene -> index={gameplaySceneIndex}, name={gameplaySceneName}");
        SceneManager.LoadScene(gameplaySceneIndex);
    }
}
