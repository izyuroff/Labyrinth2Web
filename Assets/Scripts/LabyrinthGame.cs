using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Main game controller that orchestrates all game systems.
/// </summary>
public class LabyrinthGame : MonoBehaviour
{
    [Header("Game Configuration")]
    public GameConfig gameConfig;

    // Component references
    private MazeGenerator _mazeGenerator;
    private PlayerController _playerController;
    private GraffitiSystem _graffitiSystem;
    private UIManager _uiManager;
    private AudioManager _audioManager;
    private MaterialManager _materialManager;
    private PathDebugRenderer _pathDebugRenderer;

    // Level management
    private GameObject _levelsRoot;
    private GameObject _activeLevelRoot;
    private GameObject _activeGraffitiRoot;

    // Input action for graffiti
    private InputAction _placeGraffitiAction;

    // State
    private bool _isInitialized = false;
    private bool _mazeGeneratorReady = false;

    /// <summary>
    /// Initialize all game systems.
    /// </summary>
    private void Awake()
    {
        // Resolve UI early so menu callbacks can still be wired in Start.
        _uiManager = GetComponent<UIManager>();
        if (_uiManager == null)
        {
            Debug.LogWarning("LabyrinthGame: UIManager component not found. UI functionality will be limited.");
        }

        // Try to find GameConfig if not assigned
        if (gameConfig == null)
        {
            gameConfig = FindOrCreateGameConfig();
        }

        // Validate configuration
        if (gameConfig == null)
        {
            Debug.LogError("LabyrinthGame: GameConfig is not assigned and could not be found or created! Please assign a GameConfig asset.");
            return;
        }

        string validationError = gameConfig.Validate();
        if (validationError != null)
        {
            Debug.LogError($"LabyrinthGame: Configuration validation failed: {validationError}");
            return;
        }

        // Initialize components
        if (!InitializeComponents())
            return;

        // Create graffiti input action
        CreateGraffitiInputAction();

        _isInitialized = true;
    }

    /// <summary>
    /// Start is called after all objects are initialized.
    /// Use this to set up UI after scene generation completes.
    /// </summary>
    private void Start()
    {
        // Setup UI callbacks after scene is fully set up
        // This ensures UI references are assigned if scene was generated
        SetupUICallbacks();
    }

    /// <summary>
    /// Initialize all game components.
    /// </summary>
    private bool InitializeComponents()
    {
        // Material Manager
        _materialManager = gameObject.AddComponent<MaterialManager>();
        _materialManager.Initialize(gameConfig);
        if (!_materialManager.IsReady)
        {
            Debug.LogError("LabyrinthGame: MaterialManager failed to initialize.");
            return false;
        }

        Material floorMat = _materialManager.GetFloorMaterial();
        Material wallTemplate = _materialManager.GetWallMaterialTemplate();
        Material bgMat = _materialManager.GetBackgroundMaterial();
        Material graffitiMat = _materialManager.GetGraffitiMaterialTemplate();

        if (floorMat == null || wallTemplate == null || bgMat == null || graffitiMat == null)
        {
            Debug.LogError("LabyrinthGame: MaterialManager did not produce required materials.");
            return false;
        }

        // Maze Generator
        _mazeGenerator = gameObject.AddComponent<MazeGenerator>();
        _mazeGeneratorReady = _mazeGenerator.Initialize(gameConfig, floorMat, wallTemplate, bgMat);
        if (!_mazeGeneratorReady)
        {
            Debug.LogError("LabyrinthGame: MazeGenerator failed to initialize with provided materials.");
            return false;
        }

        // Player Controller
        _playerController = gameObject.AddComponent<PlayerController>();
        _playerController.Initialize(gameConfig);

        // Graffiti System
        _graffitiSystem = gameObject.AddComponent<GraffitiSystem>();
        _graffitiSystem.Initialize(gameConfig, graffitiMat);

        // Audio Manager
        _audioManager = gameObject.AddComponent<AudioManager>();
        _audioManager.Initialize(gameConfig);

        // Path Debug Renderer
        _pathDebugRenderer = gameObject.AddComponent<PathDebugRenderer>();
        _pathDebugRenderer.Initialize();

        return true;
    }

    /// <summary>
    /// Setup UI callbacks.
    /// </summary>
    private void SetupUICallbacks()
    {
        if (_uiManager == null)
        {
            _uiManager = GetComponent<UIManager>();
            if (_uiManager == null)
                return;
        }

        _uiManager.Initialize(
            onLevelSelected: LoadLevel,
            onContinue: OnContinue,
            onQuitToMenu: OnQuitToMenu,
            onSoundToggle: OnSoundToggle
        );

        // Set initial UI state
        _uiManager.SetMainMenuVisible(true);
        _playerController?.SetCursorLocked(false);
    }

    /// <summary>
    /// Create input action for graffiti placement.
    /// </summary>
    private void CreateGraffitiInputAction()
    {
        _placeGraffitiAction = new InputAction("PlaceGraffiti", InputActionType.Button, "<Mouse>/leftButton");
    }

    /// <summary>
    /// Enable input actions.
    /// </summary>
    private void OnEnable()
    {
        _placeGraffitiAction?.Enable();
    }

    /// <summary>
    /// Disable input actions.
    /// </summary>
    private void OnDisable()
    {
        _placeGraffitiAction?.Disable();
    }

    /// <summary>
    /// Main update loop.
    /// </summary>
    private void Update()
    {
        if (!_isInitialized) return;

        // Update UI (handles pause input)
        _uiManager?.UpdateUI();

        // If in main menu or paused, don't update gameplay
        if (_uiManager != null && (_uiManager.IsInMainMenu() || _uiManager.IsPaused()))
        {
            _audioManager?.UpdateFootsteps(false);
            return;
        }

        // Update gameplay systems
        _playerController?.UpdatePlayer();

        // Try to place graffiti
        if (_playerController != null && _graffitiSystem != null && _placeGraffitiAction != null)
        {
            Transform cameraTransform = _playerController.GetCameraTransform();
            if (cameraTransform != null)
            {
                _graffitiSystem.TryPlaceGraffiti(cameraTransform, _placeGraffitiAction);
            }
        }

    }

    /// <summary>
    /// Loads a level by index.
    /// </summary>
    private void LoadLevel(int levelIndex)
    {
        if (gameConfig == null)
        {
            Debug.LogError("LabyrinthGame: Cannot load level - GameConfig is null!");
            return;
        }

        if (gameConfig.levels == null || levelIndex < 1 || levelIndex > gameConfig.levels.Length)
        {
            Debug.LogError($"LabyrinthGame: Invalid level index {levelIndex}!");
            return;
        }

        LevelConfig levelConfig = gameConfig.levels[levelIndex - 1];
        if (levelConfig == null)
        {
            Debug.LogError($"LabyrinthGame: LevelConfig for level {levelIndex} is null!");
            return;
        }

        Debug.Log($"LabyrinthGame: Loading level {levelIndex}");

        // Ensure levels root exists
        EnsureLevelsRoot();

        // Clear previous level
        ClearActiveLevel();

        // Create new level root
        _activeLevelRoot = new GameObject($"Level_{levelIndex}");
        _activeLevelRoot.transform.SetParent(_levelsRoot.transform, false);

        _activeGraffitiRoot = new GameObject("GraffitiRoot");
        _activeGraffitiRoot.transform.SetParent(_activeLevelRoot.transform, false);

        // Set active level for graffiti system
        _graffitiSystem?.SetActiveLevel(_activeLevelRoot.transform, _activeGraffitiRoot.transform);

        // Generate maze
        Vector3 entrance1Position = Vector3.zero;
        Vector3 entrance2Position = Vector3.zero;
        
        if (_mazeGenerator == null || !_mazeGeneratorReady)
        {
            Debug.LogError("LabyrinthGame: MazeGenerator is not initialized, cannot generate level!");
            return;
        }

        _mazeGenerator.GenerateLevel(levelConfig, _activeLevelRoot.transform, levelIndex, out entrance1Position, out entrance2Position);
        
        // Set debug path after maze generation
        if (_pathDebugRenderer != null)
        {
            var pathPoints = _mazeGenerator.GetPathPoints();
            _pathDebugRenderer.SetPath(pathPoints, gameConfig.cellSize);
        }

        // Position player at entrance 1, facing the entrance (looking into the maze)
        // Entrance positions are already calculated outside the maze walls
        if (_playerController != null)
        {
            Debug.Log($"LabyrinthGame: Positioning player at entrance1: {entrance1Position}, facing entrance2: {entrance2Position}");
            _playerController.SetPositionAndRotation(entrance1Position, entrance2Position);
        }
        else
        {
            Debug.LogWarning("LabyrinthGame: PlayerController is null, cannot position player!");
        }

        // Update UI state
        _uiManager?.SetMainMenuVisible(false);
        _uiManager?.SetPaused(false);
        _playerController?.SetCursorLocked(true);
        
        // Ensure cursor is locked when starting level
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Ensures the levels root GameObject exists.
    /// </summary>
    private void EnsureLevelsRoot()
    {
        if (_levelsRoot != null) return;

        _levelsRoot = GameObject.Find("LevelsRoot");
        if (_levelsRoot == null)
        {
            _levelsRoot = new GameObject("LevelsRoot");
        }
    }

    /// <summary>
    /// Clears the active level.
    /// </summary>
    private void ClearActiveLevel()
    {
        if (_activeLevelRoot != null)
        {
            Destroy(_activeLevelRoot);
        }
        _activeLevelRoot = null;
        _activeGraffitiRoot = null;
    }

    /// <summary>
    /// UI callback: Continue from pause.
    /// </summary>
    private void OnContinue()
    {
        _uiManager?.SetPaused(false);
        _playerController?.SetCursorLocked(true);
    }

    /// <summary>
    /// UI callback: Quit to main menu.
    /// </summary>
    private void OnQuitToMenu()
    {
        ClearActiveLevel();
        _uiManager?.SetPaused(false);
        _uiManager?.SetMainMenuVisible(true);
        _playerController?.SetCursorLocked(false);
        
        // Unlock cursor for menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// UI callback: Sound toggle.
    /// </summary>
    private void OnSoundToggle(bool enabled)
    {
        _audioManager?.SetSoundEnabled(enabled);
    }

    /// <summary>
    /// Finds an existing GameConfig or creates one if none exists.
    /// </summary>
    private GameConfig FindOrCreateGameConfig()
    {
        // Try to find in Resources folder first (works at runtime)
        GameConfig[] configsInResources = Resources.LoadAll<GameConfig>("");
        if (configsInResources != null && configsInResources.Length > 0)
        {
            Debug.Log($"LabyrinthGame: Found GameConfig in Resources: {configsInResources[0].name}");
            return configsInResources[0];
        }

#if UNITY_EDITOR
        // Try to find via AssetDatabase (editor only)
        string[] guids = AssetDatabase.FindAssets("t:GameConfig");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (config != null)
            {
                Debug.Log($"LabyrinthGame: Found GameConfig via AssetDatabase: {path}");
                return config;
            }
        }

        // Create a new one if none found (editor only)
        Debug.LogWarning("LabyrinthGame: No GameConfig found. Creating a temporary one. Consider running 'Tools → Generate Game' to create a proper asset.");
        return ScriptableObject.CreateInstance<GameConfig>();
#else
        // At runtime, if not in Resources, we can't load it
        Debug.LogError("LabyrinthGame: GameConfig not found in Resources folder. Please ensure GameConfig asset is in a Resources folder or assign it in the inspector.");
        return null;
#endif
    }
}
