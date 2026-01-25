using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Главный контроллер игры: меню, пауза, генерация уровней, управление от первого лица, граффити и звук.
/// Все уровни создаются в одной сцене во время выполнения.
/// </summary>
public class LabyrinthGame : MonoBehaviour
{
    [Header("Спрайты граффити")]
    public Sprite[] graffitiSprites;

    [Header("Размер и высота лабиринта")]
    public float cellSize = 6f;
    public float wallHeight = 120f;
    public float wallThickness = 0.25f;

    [Header("Параметры игрока")]
    public float moveSpeed = 8f;
    public float lookSensitivity = 0.12f;
    public float cameraHeight = 2.0f;
    public float graffitiSize = 2.2f;
    public float graffitiOffset = 0.02f;

    [Header("Цвета стен (фиксированные, не рандом)")]
    public Color[] wallColors =
    {
        new Color(0.95f, 0.35f, 0.35f, 1f),
        new Color(0.35f, 0.75f, 0.95f, 1f),
        new Color(0.55f, 0.95f, 0.45f, 1f),
        new Color(0.95f, 0.85f, 0.35f, 1f),
        new Color(0.85f, 0.45f, 0.95f, 1f),
        new Color(0.95f, 0.55f, 0.15f, 1f),
    };

    [Header("Материалы и фон")]
    public Color floorColor = new Color(0.78f, 0.78f, 0.78f, 1f);
    public Texture2D[] backgroundTextures; // опционально
    public float backgroundSize = 1200f;
    public float backgroundHeight = 200f;

    [Header("Аудио (WebGL: wav/ogg)")]
    public AudioClip footstepLoop;
    public AudioClip backgroundMusic;
    public float footstepVolume = 0.65f;
    public float musicVolume = 0.45f;

    // UI ссылки (создаются и привязываются Editor-скриптом)
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

    // Player
    private CharacterController _cc;
    private Transform _playerRoot;
    private Camera _cam;

    // Audio
    private AudioSource _footstepsSource;
    private AudioSource _musicSource;

    // State
    private bool _inMainMenu = true;
    private bool _isPaused = false;
    private bool _soundEnabled = true;

    // Input System actions (создаем программно, без ассетов)
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _flyAction;
    private InputAction _placeGraffitiAction;
    private InputAction _pauseAction;

    // Level roots
    private GameObject _levelsRoot;
    private GameObject _activeLevelRoot;
    private GameObject _activeGraffitiRoot;

    // Materials cache
    private Material _floorMat;
    private Material _bgMat;
    private Material _wallMatTemplate;
    private Material _graffitiMatTemplate;

    // Graffiti mesh cache per sprite
    private readonly Dictionary<Sprite, Mesh> _spriteMeshCache = new Dictionary<Sprite, Mesh>();

    // Look state
    private float _yaw;
    private float _pitch;

    /// <summary>
    /// Инициализация: поиск/подготовка компонентов, создание InputActions.
    /// </summary>
    private void Awake()
    {
        // Игрок
        _playerRoot = GameObject.Find("Player")?.transform;
        if (_playerRoot != null)
        {
            _cc = _playerRoot.GetComponent<CharacterController>();
            _cam = _playerRoot.GetComponentInChildren<Camera>(true);
        }

        // Audio
        var a = GameObject.Find("Audio")?.transform;
        if (a != null)
        {
            _footstepsSource = a.Find("Footsteps")?.GetComponent<AudioSource>();
            _musicSource = a.Find("Music")?.GetComponent<AudioSource>();
        }

        PrepareMaterials();
        CreateInputActions();

        ApplySoundState(true);

        // старт: меню
        SetMainMenuVisible(true);
        SetPauseVisible(false);
        LockCursor(false);
		
		
		if (level1Button != null) { level1Button.onClick.RemoveAllListeners(); level1Button.onClick.AddListener(UI_LoadLevel1); }
		if (level2Button != null) { level2Button.onClick.RemoveAllListeners(); level2Button.onClick.AddListener(UI_LoadLevel2); }
		if (level3Button != null) { level3Button.onClick.RemoveAllListeners(); level3Button.onClick.AddListener(UI_LoadLevel3); }
		if (instructionsButton != null) { instructionsButton.onClick.RemoveAllListeners(); instructionsButton.onClick.AddListener(UI_ToggleInstructions); }

		if (continueButton != null) { continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(UI_Continue); }
		if (quitButton != null) { quitButton.onClick.RemoveAllListeners(); quitButton.onClick.AddListener(UI_QuitToMenu); }
		if (soundOnButton != null) { soundOnButton.onClick.RemoveAllListeners(); soundOnButton.onClick.AddListener(UI_SoundOn); }
		if (soundOffButton != null) { soundOffButton.onClick.RemoveAllListeners(); soundOffButton.onClick.AddListener(UI_SoundOff); }

		
    }

    /// <summary>
    /// Включение input actions.
    /// </summary>
    private void OnEnable()
    {
        _moveAction?.Enable();
        _lookAction?.Enable();
        _flyAction?.Enable();
        _placeGraffitiAction?.Enable();
        _pauseAction?.Enable();
    }

    /// <summary>
    /// Выключение input actions.
    /// </summary>
    private void OnDisable()
    {
        _moveAction?.Disable();
        _lookAction?.Disable();
        _flyAction?.Disable();
        _placeGraffitiAction?.Disable();
        _pauseAction?.Disable();
    }

    /// <summary>
    /// Главный цикл: управление, граффити, пауза.
    /// </summary>
    private void Update()
    {
        // ESC
        if (_pauseAction != null && _pauseAction.WasPressedThisFrame())
        {
            if (!_inMainMenu)
            {
                TogglePause();
            }
        }

        if (_inMainMenu) return;

        if (_isPaused)
        {
            UpdateFootsteps(false);
            return;
        }

        UpdateLook();
        UpdateMovement();
        UpdateGraffiti();

        // курсор всегда скрыт/залочен в игре
        LockCursor(true);
    }

    /// <summary>
    /// Обработчик кнопки "Level 1".
    /// </summary>
    public void UI_LoadLevel1() => LoadLevel(1);

    /// <summary>
    /// Обработчик кнопки "Level 2".
    /// </summary>
    public void UI_LoadLevel2() => LoadLevel(2);

    /// <summary>
    /// Обработчик кнопки "Level 3".
    /// </summary>
    public void UI_LoadLevel3() => LoadLevel(3);

    /// <summary>
    /// Обработчик кнопки "Instructions".
    /// </summary>
    public void UI_ToggleInstructions()
    {
        if (instructionsText == null) return;
        instructionsText.gameObject.SetActive(!instructionsText.gameObject.activeSelf);
    }

    /// <summary>
    /// Обработчик кнопки "Continue" в паузе.
    /// </summary>
    public void UI_Continue()
    {
        SetPaused(false);
    }

    /// <summary>
    /// Обработчик кнопки "Quit" (возврат в главное меню).
    /// </summary>
    public void UI_QuitToMenu()
    {
        // очищаем текущий уровень
        ClearActiveLevel();
        _inMainMenu = true;
        SetMainMenuVisible(true);
        SetPauseVisible(false);
        LockCursor(false);
    }

    /// <summary>
    /// Включить звук.
    /// </summary>
    public void UI_SoundOn()
    {
        ApplySoundState(true);
    }

    /// <summary>
    /// Выключить звук.
    /// </summary>
    public void UI_SoundOff()
    {
        ApplySoundState(false);
    }

    /// <summary>
    /// Загрузка/генерация уровня по индексу.
    /// </summary>
    /// <param name="levelIndex">1..3</param>
/// <summary>
/// Загрузка/генерация уровня по индексу.
/// </summary>
/// <param name="levelIndex">1..3</param>
private void LoadLevel(int levelIndex)
{
    Debug.Log("LoadLevel " + levelIndex);
    _inMainMenu = false;
    SetMainMenuVisible(false);
    SetPaused(false);

    EnsureLevelsRoot();
    ClearActiveLevel();

    // Генерация
    _activeLevelRoot = new GameObject($"Level_{levelIndex}");
    _activeLevelRoot.transform.SetParent(_levelsRoot.transform, false);

    _activeGraffitiRoot = new GameObject("GraffitiRoot");
    _activeGraffitiRoot.transform.SetParent(_activeLevelRoot.transform, false);

    int cols = 0;
    int rows = 0;

    // Выбор данных уровня
    if (levelIndex == 1)
    {
        var ascii = GetLevel1AsciiModel();
        rows = ascii.Length;
        cols = (ascii.Length > 0) ? ascii[0].Length : 0;

        BuildMazeFromAscii(ascii, _activeLevelRoot.transform, levelIndex);
    }
    else if (levelIndex == 2)
    {
        int genW = 24;
        int genH = 18;

        // В твоем генераторе пол создается как (w*2+1, h*2+1)
        cols = genW * 2 + 1;
        rows = genH * 2 + 1;

        BuildGeneratedMaze(genW, genH, 202602, _activeLevelRoot.transform, levelIndex);
    }
    else
    {
        int genW = 34;
        int genH = 24;

        cols = genW * 2 + 1;
        rows = genH * 2 + 1;

        BuildGeneratedMaze(genW, genH, 202603, _activeLevelRoot.transform, levelIndex);
    }

    // Фон по периметру
    if (cols > 0 && rows > 0)
        BuildBackground(_activeLevelRoot.transform, cols, rows);
    else
        BuildBackground(_activeLevelRoot.transform, 10, 10);

    // Позиция игрока
    PositionPlayerAtStart();
    LockCursor(true);
}


    /// <summary>
    /// Ставит игрока в стартовую точку.
    /// </summary>
    private void PositionPlayerAtStart()
    {
        if (_playerRoot == null) return;

        // старт около нижнего левого угла уровня
        var start = _activeLevelRoot != null ? _activeLevelRoot.transform.position : Vector3.zero;
        _playerRoot.position = start + new Vector3(cellSize * 1.5f, 0f, cellSize * 1.5f);

        // высота камеры
        if (_cc != null)
        {
            var p = _playerRoot.position;
            p.y = 1f;
            _playerRoot.position = p;
        }

        _yaw = _playerRoot.eulerAngles.y;
        _pitch = 0f;
        if (_cam != null)
        {
            _cam.transform.localPosition = new Vector3(0f, cameraHeight, 0f);
            _cam.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Создает или находит корень уровней.
    /// </summary>
    private void EnsureLevelsRoot()
    {
        if (_levelsRoot != null) return;
        _levelsRoot = GameObject.Find("LevelsRoot");
        if (_levelsRoot == null) _levelsRoot = new GameObject("LevelsRoot");
    }

    /// <summary>
    /// Удаляет активный уровень и все граффити.
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
    /// Переключение паузы.
    /// </summary>
    private void TogglePause()
    {
        SetPaused(!_isPaused);
    }

    /// <summary>
    /// Установка паузы.
    /// </summary>
    /// <param name="paused">true/false</param>
    private void SetPaused(bool paused)
    {
        _isPaused = paused;
        SetPauseVisible(paused);
        LockCursor(!paused);

        UpdateFootsteps(false);
    }

    /// <summary>
    /// Показывает/прячет главное меню.
    /// </summary>
    private void SetMainMenuVisible(bool visible)
    {
        if (mainMenuCanvas != null) mainMenuCanvas.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Показывает/прячет паузу.
    /// </summary>
    private void SetPauseVisible(bool visible)
    {
        if (pauseCanvas != null) pauseCanvas.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Лочит курсор для FPS или показывает для UI.
    /// </summary>
    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

/// <summary>
/// Подготавливает материалы для пола, стен, фона и граффити.
/// В URP магента означает "сломанный шейдер/материал", поэтому здесь гарантируем валидные материалы.
/// Стены делаем двусторонними (Quad иначе "пропадает" с обратной стороны).
/// </summary>
private void PrepareMaterials()
{
    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
    if (lit == null)
    {
        Debug.LogError("URP Lit shader not found. Check URP setup (URP pipeline asset assigned).");
        return;
    }

    // --- Пол ---
    if (_floorMat == null)
        _floorMat = new Material(lit);
    else
        _floorMat.shader = lit;

    // Пол можно оставлять односторонним
    if (_floorMat.HasProperty("_Cull"))
        _floorMat.SetFloat("_Cull", 2f); // Back

    if (_floorMat.HasProperty("_BaseColor"))
        _floorMat.SetColor("_BaseColor", floorColor);
    else
        _floorMat.color = floorColor;

    // --- Стены (шаблон) ---
    if (_wallMatTemplate == null)
        _wallMatTemplate = new Material(lit);
    else
        _wallMatTemplate.shader = lit;

    // Двусторонние стены для Quad
    if (_wallMatTemplate.HasProperty("_Cull"))
        _wallMatTemplate.SetFloat("_Cull", 0f); // Off

    if (_wallMatTemplate.HasProperty("_BaseColor"))
        _wallMatTemplate.SetColor("_BaseColor", Color.white);
    else
        _wallMatTemplate.color = Color.white;

    // --- Фон (опционально) ---
    if (_bgMat == null)
        _bgMat = new Material(lit);
    else
        _bgMat.shader = lit;

    if (_bgMat.HasProperty("_Cull"))
        _bgMat.SetFloat("_Cull", 2f); // Back

    // --- Граффити (шаблон) ---
    if (_graffitiMatTemplate == null)
        _graffitiMatTemplate = new Material(lit);
    else
        _graffitiMatTemplate.shader = lit;

    // Граффити обычно двустороннее, чтобы не пропадало при углах
    if (_graffitiMatTemplate.HasProperty("_Cull"))
        _graffitiMatTemplate.SetFloat("_Cull", 0f);

    if (_graffitiMatTemplate.HasProperty("_BaseColor"))
        _graffitiMatTemplate.SetColor("_BaseColor", Color.white);
    else
        _graffitiMatTemplate.color = Color.white;
}



    /// <summary>
    /// Создает InputActions программно под новую Input System.
    /// </summary>
    private void CreateInputActions()
    {
        _moveAction = new InputAction("Move", InputActionType.Value, null, null, null, "Vector2");
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        _lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", null, null, "Vector2");

        _flyAction = new InputAction("Fly", InputActionType.Value, null, null, null, "Axis");
        _flyAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/q")
            .With("Positive", "<Keyboard>/e");

        _placeGraffitiAction = new InputAction("PlaceGraffiti", InputActionType.Button, "<Mouse>/leftButton");

        _pauseAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
    }

    /// <summary>
    /// Обновляет вращение камеры мышью.
    /// </summary>
    private void UpdateLook()
    {
        if (_cam == null || _playerRoot == null) return;

        Vector2 delta = _lookAction.ReadValue<Vector2>();
        _yaw += delta.x * lookSensitivity;
        _pitch -= delta.y * lookSensitivity;
        _pitch = Mathf.Clamp(_pitch, -85f, 85f);

        _playerRoot.rotation = Quaternion.Euler(0f, _yaw, 0f);
        _cam.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    /// <summary>
    /// Обновляет перемещение (WASD + Q/E).
    /// </summary>
    private void UpdateMovement()
    {
        if (_cc == null || _cam == null) return;

        Vector2 mv = _moveAction.ReadValue<Vector2>();
        float fly = _flyAction.ReadValue<float>();

        Vector3 forward = _playerRoot.forward;
        Vector3 right = _playerRoot.right;

        Vector3 dir = (forward * mv.y + right * mv.x);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 up = Vector3.up * fly;

        Vector3 motion = (dir + up).normalized * moveSpeed * Time.deltaTime;
        bool isMoving = motion.sqrMagnitude > 0.000001f;

        _cc.Move(motion);

        UpdateFootsteps(isMoving);
    }

    /// <summary>
    /// Проигрывает/останавливает шаги в зависимости от движения.
    /// </summary>
    private void UpdateFootsteps(bool moving)
    {
        if (_footstepsSource == null) return;
        if (!_soundEnabled) { _footstepsSource.Stop(); return; }

        if (footstepLoop != null) _footstepsSource.clip = footstepLoop;
        _footstepsSource.loop = true;
        _footstepsSource.volume = footstepVolume;

        if (moving)
        {
            if (!_footstepsSource.isPlaying && footstepLoop != null)
                _footstepsSource.Play();
        }
        else
        {
            if (_footstepsSource.isPlaying)
                _footstepsSource.Stop();
        }
    }

    /// <summary>
    /// Размещение граффити по клику ЛКМ: луч из камеры, попадание по стене/полу.
    /// </summary>
    private void UpdateGraffiti()
    {
        if (_cam == null) return;
        if (_placeGraffitiAction == null) return;
        if (!_placeGraffitiAction.WasPressedThisFrame()) return;
        if (graffitiSprites == null || graffitiSprites.Length == 0) return;
        if (_activeGraffitiRoot == null) return;

        Ray r = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(r, out RaycastHit hit, 500f)) return;

        // Разрешаем граффити только по объектам текущего уровня (чтобы не лепить на UI/фон)
        if (_activeLevelRoot == null) return;
        if (!hit.collider.transform.IsChildOf(_activeLevelRoot.transform)) return;

        Sprite s = graffitiSprites[UnityEngine.Random.Range(0, graffitiSprites.Length)];

        var go = new GameObject("Graffiti");
        go.transform.SetParent(_activeGraffitiRoot.transform, true);

        Vector3 pos = hit.point + hit.normal * graffitiOffset;
        go.transform.position = pos;

        // Ориентация по нормали
        go.transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        mf.sharedMesh = GetOrCreateMeshForSprite(s);

        Material m = new Material(_graffitiMatTemplate);
        ApplySpriteToMaterial(s, m);
        mr.sharedMaterial = m;

        // Масштаб
        go.transform.localScale = Vector3.one * graffitiSize;
    }

    /// <summary>
    /// Применяет спрайт к материалу (текстура + корректные UV через mesh).
    /// </summary>
    private void ApplySpriteToMaterial(Sprite s, Material m)
    {
        if (s == null || m == null) return;
        m.mainTexture = s.texture;

        // URP/Unlit использует _BaseMap, а Standard/Unlit — mainTexture
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", s.texture);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
    }

    /// <summary>
    /// Возвращает mesh-квад для конкретного спрайта с UV под его rect.
    /// </summary>
    private Mesh GetOrCreateMeshForSprite(Sprite s)
    {
        if (s == null) return CreateUnitQuadMesh();

        if (_spriteMeshCache.TryGetValue(s, out Mesh cached) && cached != null)
            return cached;

        Mesh mesh = CreateUnitQuadMesh();

        Rect tr = s.textureRect;
        Texture tex = s.texture;

        float u0 = tr.xMin / tex.width;
        float v0 = tr.yMin / tex.height;
        float u1 = tr.xMax / tex.width;
        float v1 = tr.yMax / tex.height;

        // Важно: Unity UV снизу вверх, а textureRect уже в UV-координатах текстуры
        Vector2[] uv = new Vector2[4];
        uv[0] = new Vector2(u0, v0);
        uv[1] = new Vector2(u1, v0);
        uv[2] = new Vector2(u0, v1);
        uv[3] = new Vector2(u1, v1);
        mesh.uv = uv;

        _spriteMeshCache[s] = mesh;
        return mesh;
    }

    /// <summary>
    /// Создает единичный quad mesh (в плоскости XY, лицом в +Z).
    /// </summary>
    private Mesh CreateUnitQuadMesh()
    {
        Mesh m = new Mesh();
        m.name = "UnitQuadMesh";

        Vector3[] v = new Vector3[4];
        v[0] = new Vector3(-0.5f, -0.5f, 0f);
        v[1] = new Vector3(0.5f, -0.5f, 0f);
        v[2] = new Vector3(-0.5f, 0.5f, 0f);
        v[3] = new Vector3(0.5f, 0.5f, 0f);

        int[] t = new int[6] { 0, 2, 1, 2, 3, 1 };

        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0f,0f),
            new Vector2(1f,0f),
            new Vector2(0f,1f),
            new Vector2(1f,1f)
        };

        m.vertices = v;
        m.triangles = t;
        m.uv = uv;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// Включает/выключает звук (глобально).
    /// </summary>
    /// <param name="enabled">true/false</param>
    private void ApplySoundState(bool enabled)
    {
        _soundEnabled = enabled;

        AudioListener.volume = enabled ? 1f : 0f;

        if (_musicSource != null)
        {
            _musicSource.volume = musicVolume;
            if (enabled)
            {
                if (backgroundMusic != null) _musicSource.clip = backgroundMusic;
                _musicSource.loop = true;
                if (backgroundMusic != null && !_musicSource.isPlaying)
                    _musicSource.Play();
            }
            else
            {
                _musicSource.Stop();
            }
        }

        if (!enabled)
        {
            UpdateFootsteps(false);
        }
    }

    /// <summary>
    /// Строит фоновые "пейзажные" квадраты вокруг лабиринта.
    /// </summary>
  //  private void BuildBackground(Transform parent)
/// <summary>
/// Создает фоновые "стены" по периметру лабиринта (4 стороны).
/// Располагает их близко к полю, строго по границам, чтобы в игре не было артефактов и "далекого" фона.
/// </summary>

/// <summary>
/// Создает фоновые "стены" по периметру лабиринта (4 стороны).
/// Низ фона фиксируем на Y=0, чтобы "картинка стояла на земле".
/// </summary>
/// <summary>
/// Создает фоновые "стены" по периметру лабиринта (4 стороны).
/// Низ фона фиксируем на Y=0, фон ставим ВПРИТЫК к границе (без выноса наружу).
/// </summary>
private void BuildBackground(Transform parent, int cols, int rows)
{
    float w = cols * cellSize;
    float h = rows * cellSize;

    // ВПРИТЫК к границе
    float gap = 0f;

    float wallW = w + gap * 2f;
    float wallH = Mathf.Max(10f, backgroundHeight);

    float yCenter = wallH * 0.5f;

    Material MakeSideMat(int texIndex)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material m = (lit != null) ? new Material(lit) : new Material(Shader.Find("Standard"));

        if (backgroundTextures != null && texIndex >= 0 && texIndex < backgroundTextures.Length && backgroundTextures[texIndex] != null)
            m.mainTexture = backgroundTextures[texIndex];

        m.mainTextureScale = Vector2.one;
        m.mainTextureOffset = Vector2.zero;

        return m;
    }

    // Север
    CreateBackgroundQuad(
        parent,
        new Vector3(w * 0.5f, yCenter, h + gap),
        new Vector3(wallW, wallH, 1f),
        Quaternion.identity,
        MakeSideMat(0),
        "BG_N"
    );

    // Юг
    CreateBackgroundQuad(
        parent,
        new Vector3(w * 0.5f, yCenter, -gap),
        new Vector3(wallW, wallH, 1f),
        Quaternion.Euler(0f, 180f, 0f),
        MakeSideMat(1),
        "BG_S"
    );

    // Восток
    CreateBackgroundQuad(
        parent,
        new Vector3(w + gap, yCenter, h * 0.5f),
        new Vector3(h + gap * 2f, wallH, 1f),
        Quaternion.Euler(0f, -90f, 0f),
        MakeSideMat(2),
        "BG_E"
    );

    // Запад
    CreateBackgroundQuad(
        parent,
        new Vector3(-gap, yCenter, h * 0.5f),
        new Vector3(h + gap * 2f, wallH, 1f),
        Quaternion.Euler(0f, 90f, 0f),
        MakeSideMat(3),
        "BG_W"
    );
}




    /// <summary>
    /// Создает один фоновый квадрат.
    /// </summary>
	
/// <summary>
/// Создает фоновый объект (Cube) без коллайдера.
/// Нужен для дальнего окружения/пейзажа. Cube надежнее Quad (виден с любых углов и не "пропадает").
/// </summary>

/// <summary>
/// Создает фоновый объект (плоский Cube) без коллайдера.
/// Плоский Cube надежнее Quad и не дает пропаданий/артефактов из-за "изнанки".
/// Толщина фиксирована, чтобы фон выглядел как плоскость.
/// </summary>
private void CreateBackgroundQuad(Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, string name)
{
    const float thickness = 0.2f;

    GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
    bg.name = name;
    bg.transform.SetParent(parent, false);

    bg.transform.localPosition = pos;
    bg.transform.localRotation = rot;

    // scale.x = ширина, scale.y = высота, а толщину задаем фиксированно (по Z в локале)
    bg.transform.localScale = new Vector3(scale.x, scale.y, thickness);

    var mr = bg.GetComponent<MeshRenderer>();
    mr.sharedMaterial = mat;

    foreach (var c in bg.GetComponents<Collider>())
        Destroy(c);
}



    /// <summary>
    /// Уровень 1: ASCII модель (пример). Можно заменить на точные данные из вашего labyrinth_model.
    /// '#' = стена, '.' = проход. Логика создает стены по границе "стена/пусто".
    /// </summary>
    private string[] GetLevel1AsciiModel()
    {
        // Пример-рыба: заменишь на точную геометрию из models.txt (если нужно — скажешь, подстрою парсер под ваш формат).
        return new[]
        {
            "########################",
            "#........#.............#",
            "#.######.#.##########..#",
            "#.#....#.#......#......#",
            "#.#.##.#.######.#.######",
            "#...##...#....#.#......#",
            "###.######.##.#.######.#",
            "#...#......##.#......#.#",
            "#.###.########.#######.#",
            "#.....#........#.......#",
            "########################",
        };
    }

    /// <summary>
    /// Построение лабиринта из ASCII сетки.
    /// </summary>
    private void BuildMazeFromAscii(string[] grid, Transform parent, int levelIndex)
    {
        if (grid == null || grid.Length == 0) return;

        int rows = grid.Length;
        int cols = grid[0].Length;

        // Пол (один quad)
        CreateFloor(parent, cols, rows);

        // Стены по границе между стеной и пустотой
        bool IsWall(int r, int c)
        {
            if (r < 0 || c < 0 || r >= rows || c >= cols) return true;
            return grid[r][c] == '#';
        }

        int wallId = 0;

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            if (!IsWall(r, c)) continue;

            // Если рядом проход — ставим стенку на границе
            // North
            if (!IsWall(r - 1, c)) CreateWallSegment(parent, c, r, "N", ref wallId, levelIndex);
            // South
            if (!IsWall(r + 1, c)) CreateWallSegment(parent, c, r, "S", ref wallId, levelIndex);
            // West
            if (!IsWall(r, c - 1)) CreateWallSegment(parent, c, r, "W", ref wallId, levelIndex);
            // East
            if (!IsWall(r, c + 1)) CreateWallSegment(parent, c, r, "E", ref wallId, levelIndex);
        }
    }

    /// <summary>
    /// Генерация детерминированного лабиринта (Levels 2/3) через backtracker.
    /// </summary>
    private void BuildGeneratedMaze(int w, int h, int seed, Transform parent, int levelIndex)
    {
        // Пол
        CreateFloor(parent, w * 2 + 1, h * 2 + 1);

        // Генерим ячейки, потом рисуем стены как "решетку"
        System.Random rnd = new System.Random(seed);

        bool[,] visited = new bool[w, h];
        // стены между клетками: вертикальные и горизонтальные
        bool[,] vWall = new bool[w + 1, h]; // вертикальные границы
        bool[,] hWall = new bool[w, h + 1]; // горизонтальные границы

        // по умолчанию все стены есть
        for (int x = 0; x < w + 1; x++)
        for (int y = 0; y < h; y++)
            vWall[x, y] = true;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h + 1; y++)
            hWall[x, y] = true;

        // DFS stack
        Stack<Vector2Int> st = new Stack<Vector2Int>();
        Vector2Int cur = new Vector2Int(0, 0);
        visited[cur.x, cur.y] = true;
        st.Push(cur);

        Vector2Int[] dirs = { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1) };

        while (st.Count > 0)
        {
            cur = st.Peek();
            List<Vector2Int> options = new List<Vector2Int>();

            for (int i = 0; i < 4; i++)
            {
                var n = cur + dirs[i];
                if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;
                if (visited[n.x, n.y]) continue;
                options.Add(n);
            }

            if (options.Count == 0)
            {
                st.Pop();
                continue;
            }

            var next = options[rnd.Next(options.Count)];
            // ломаем стену между cur и next
            if (next.x == cur.x + 1) vWall[cur.x + 1, cur.y] = false;       // вправо
            else if (next.x == cur.x - 1) vWall[cur.x, cur.y] = false;     // влево
            else if (next.y == cur.y + 1) hWall[cur.x, cur.y + 1] = false; // вверх
            else if (next.y == cur.y - 1) hWall[cur.x, cur.y] = false;     // вниз

            visited[next.x, next.y] = true;
            st.Push(next);
        }

        // Рисуем стены как набор сегментов по границам
        int wallId = 0;

        // Вертикальные границы
        for (int x = 0; x < w + 1; x++)
        for (int y = 0; y < h; y++)
        {
            if (!vWall[x, y]) continue;
            // сегмент между клетками в мировых координатах
            // ставим стену вдоль оси Z (по рядам), на границе x
            CreateWallAtWorld(parent,
                new Vector3(x * cellSize, 0f, y * cellSize + cellSize * 0.5f),
                Quaternion.Euler(0f, 90f, 0f),
                cellSize,
                ref wallId,
                levelIndex);
        }

        // Горизонтальные границы
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h + 1; y++)
        {
            if (!hWall[x, y]) continue;
            CreateWallAtWorld(parent,
                new Vector3(x * cellSize + cellSize * 0.5f, 0f, y * cellSize),
                Quaternion.identity,
                cellSize,
                ref wallId,
                levelIndex);
        }
    }

    /// <summary>
    /// Создает пол одним quad на весь размер.
    /// </summary>
    private void CreateFloor(Transform parent, int cols, int rows)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "Floor";
        floor.transform.SetParent(parent, false);

        // Quad по умолчанию в XY, развернем в XZ
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        float w = cols * cellSize;
        float h = rows * cellSize;

        floor.transform.localScale = new Vector3(w, h, 1f);
        floor.transform.localPosition = new Vector3(w * 0.5f, 0f, h * 0.5f);

        var mr = floor.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _floorMat;

        // Collider оставляем (для граффити)
        var bc = floor.GetComponent<Collider>();
        if (bc == null) floor.AddComponent<MeshCollider>();
    }

    /// <summary>
    /// Создает один "сегмент стены" на границе клетки ASCII-уровня.
    /// </summary>
    private void CreateWallSegment(Transform parent, int c, int r, string dir, ref int wallId, int levelIndex)
    {
        // стенка на границе клетки r,c
        float x = c * cellSize;
        float z = (gridRowToWorldZ(r)) * cellSize;

        Vector3 pos;
        Quaternion rot;

        if (dir == "N")
        {
            pos = new Vector3(x + cellSize * 0.5f, 0f, z);
            rot = Quaternion.identity;
        }
        else if (dir == "S")
        {
            pos = new Vector3(x + cellSize * 0.5f, 0f, z + cellSize);
            rot = Quaternion.identity;
        }
        else if (dir == "W")
        {
            pos = new Vector3(x, 0f, z + cellSize * 0.5f);
            rot = Quaternion.Euler(0f, 90f, 0f);
        }
        else // E
        {
            pos = new Vector3(x + cellSize, 0f, z + cellSize * 0.5f);
            rot = Quaternion.Euler(0f, 90f, 0f);
        }

        CreateWallAtWorld(parent, pos, rot, cellSize, ref wallId, levelIndex);

        float gridRowToWorldZ(int rr) => rr; // тут намеренно просто: ряды идут по Z
    }

    /// <summary>
    /// Создает стену в мире: quad + box collider. Высота 120 (по Y) как в требовании.
    /// </summary>
/// <summary>
/// Создает стену в мире: quad + box collider.
/// </summary>
private void CreateWallAtWorld(Transform parent, Vector3 baseCenter, Quaternion rot, float length, ref int wallId, int levelIndex)
{
    GameObject w = GameObject.CreatePrimitive(PrimitiveType.Quad);
    w.name = $"Wall_{wallId}";
    w.transform.SetParent(parent, false);

    // Quad в XY, используем вертикальную плоскость: высота по Y
    w.transform.localRotation = rot;
    w.transform.localPosition = baseCenter + new Vector3(0f, wallHeight * 0.5f, 0f);
    w.transform.localScale = new Vector3(length, wallHeight, 1f);

    // Материал: фиксированный цвет по wallId (не рандом)
    Material mat = new Material(_wallMatTemplate);
    Color col = wallColors != null && wallColors.Length > 0
        ? wallColors[wallId % wallColors.Length]
        : Color.white;

    // Легкая детерминированная вариация оттенка между уровнями (но не рандом)
    col = Color.Lerp(col, Color.white, Mathf.Clamp01((levelIndex - 1) * 0.08f));
    mat.color = col;

    var mr = w.GetComponent<MeshRenderer>();
    mr.sharedMaterial = mat;

    // Collider: убираем любой коллайдер, который создался вместе с Primitive
    foreach (var c in w.GetComponents<Collider>())
        Destroy(c);

    // ВАЖНО: size задаем в локальных единицах, иначе он умножится на localScale второй раз
    var bc = w.AddComponent<BoxCollider>();
    bc.size = new Vector3(1f, 1f, wallThickness);
    bc.center = Vector3.zero;

    wallId++;
}

}
