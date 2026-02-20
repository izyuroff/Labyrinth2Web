using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Manages UI elements (menus, buttons, instructions).
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public Canvas mainMenuCanvas;
    public Canvas pauseCanvas;
    public Button level1Button;
    public Button level2Button;
    public Button level3Button;
    public Button instructionsButton;
    public Button continueButton;
    public Button quitButton;
    public Button soundOnButton;
    public Button soundOffButton;
    public Text instructionsText;

    private InputAction _pauseAction;
    private System.Action<int> _onLevelSelected;
    private System.Action _onContinue;
    private System.Action _onQuitToMenu;
    private System.Action<bool> _onSoundToggle;

    private bool _inMainMenu = true;
    private bool _isPaused = false;

    /// <summary>
    /// Initialize the UI manager.
    /// </summary>
    public void Initialize(
        System.Action<int> onLevelSelected,
        System.Action onContinue,
        System.Action onQuitToMenu,
        System.Action<bool> onSoundToggle)
    {
        _onLevelSelected = onLevelSelected;
        _onContinue = onContinue;
        _onQuitToMenu = onQuitToMenu;
        _onSoundToggle = onSoundToggle;

        CreatePauseAction();
        EnablePauseAction();
        
        // SetupButtonListeners will be called after UI references are assigned
        // Call it here in case they're already assigned, or call RefreshButtonListeners() later
        RefreshButtonListeners();

        SetMainMenuVisible(true);
        SetPauseVisible(false);
    }

    /// <summary>
    /// Enable pause action.
    /// </summary>
    private void EnablePauseAction()
    {
        if (_pauseAction != null)
        {
            _pauseAction.Enable();
        }
    }

    /// <summary>
    /// Refreshes button listeners. Call this after UI references are assigned.
    /// </summary>
    public void RefreshButtonListeners()
    {
        SetupButtonListeners();
    }

    /// <summary>
    /// Creates the pause input action.
    /// </summary>
    private void CreatePauseAction()
    {
        _pauseAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
    }

    /// <summary>
    /// Sets up button click listeners.
    /// </summary>
    private void SetupButtonListeners()
    {
        // Setup level buttons
        if (level1Button != null)
        {
            level1Button.onClick.RemoveAllListeners();
            level1Button.onClick.AddListener(() => OnLevelSelected(1));
        }
        else
        {
            Debug.LogWarning("UIManager: level1Button is null!");
        }

        if (level2Button != null)
        {
            level2Button.onClick.RemoveAllListeners();
            level2Button.onClick.AddListener(() => OnLevelSelected(2));
        }
        else
        {
            Debug.LogWarning("UIManager: level2Button is null!");
        }

        if (level3Button != null)
        {
            level3Button.onClick.RemoveAllListeners();
            level3Button.onClick.AddListener(() => OnLevelSelected(3));
        }
        else
        {
            Debug.LogWarning("UIManager: level3Button is null!");
        }

        if (instructionsButton != null)
        {
            instructionsButton.onClick.RemoveAllListeners();
            instructionsButton.onClick.AddListener(OnToggleInstructions);
        }
        else
        {
            Debug.LogWarning("UIManager: instructionsButton is null!");
        }

        // Setup pause menu buttons
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinue);
        }
        else
        {
            Debug.LogWarning("UIManager: continueButton is null!");
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitToMenu);
        }
        else
        {
            Debug.LogWarning("UIManager: quitButton is null!");
        }

        if (soundOnButton != null)
        {
            soundOnButton.onClick.RemoveAllListeners();
            soundOnButton.onClick.AddListener(() => OnSoundToggle(true));
        }
        else
        {
            Debug.LogWarning("UIManager: soundOnButton is null!");
        }

        if (soundOffButton != null)
        {
            soundOffButton.onClick.RemoveAllListeners();
            soundOffButton.onClick.AddListener(() => OnSoundToggle(false));
        }
        else
        {
            Debug.LogWarning("UIManager: soundOffButton is null!");
        }
    }

    /// <summary>
    /// Enable input actions.
    /// </summary>
    private void OnEnable()
    {
        // Only enable if action has been created (after Initialize)
        if (_pauseAction != null)
        {
            _pauseAction.Enable();
        }
    }

    /// <summary>
    /// Disable input actions.
    /// </summary>
    private void OnDisable()
    {
        _pauseAction?.Disable();
    }

    /// <summary>
    /// Update UI state (check for pause input).
    /// </summary>
    public void UpdateUI()
    {
        if (_pauseAction != null && _pauseAction.WasPressedThisFrame())
        {
            if (!_inMainMenu)
            {
                TogglePause();
            }
        }
    }

    /// <summary>
    /// Shows or hides the main menu.
    /// </summary>
    public void SetMainMenuVisible(bool visible)
    {
        _inMainMenu = visible;
        if (mainMenuCanvas != null)
            mainMenuCanvas.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Shows or hides the pause menu.
    /// </summary>
    public void SetPauseVisible(bool visible)
    {
        if (pauseCanvas != null)
            pauseCanvas.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Toggles pause state.
    /// </summary>
    public void TogglePause()
    {
        SetPaused(!_isPaused);
    }

    /// <summary>
    /// Sets pause state.
    /// </summary>
    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        SetPauseVisible(paused);
        
        // Lock/unlock cursor based on pause state
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    /// <summary>
    /// Gets the current pause state.
    /// </summary>
    public bool IsPaused()
    {
        return _isPaused;
    }

    /// <summary>
    /// Gets the current main menu state.
    /// </summary>
    public bool IsInMainMenu()
    {
        return _inMainMenu;
    }

    /// <summary>
    /// Button callback for level selection.
    /// </summary>
    private void OnLevelSelected(int levelIndex)
    {
        _onLevelSelected?.Invoke(levelIndex);
    }

    /// <summary>
    /// Button callback for continue.
    /// </summary>
    private void OnContinue()
    {
        _onContinue?.Invoke();
    }

    /// <summary>
    /// Button callback for quit to menu.
    /// </summary>
    private void OnQuitToMenu()
    {
        _onQuitToMenu?.Invoke();
    }

    /// <summary>
    /// Button callback for sound toggle.
    /// </summary>
    private void OnSoundToggle(bool enabled)
    {
        _onSoundToggle?.Invoke(enabled);
    }

    /// <summary>
    /// Button callback for instructions toggle.
    /// </summary>
    private void OnToggleInstructions()
    {
        if (instructionsText != null)
            instructionsText.gameObject.SetActive(!instructionsText.gameObject.activeSelf);
    }
}
