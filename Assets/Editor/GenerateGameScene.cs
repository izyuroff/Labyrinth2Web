using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using System.Reflection;

/// <summary>
/// Генератор сцены: очищает текущую сцену и создает полностью рабочую игру (меню, пауза, игрок, свет, аудио).
/// После запуска Tools → Generate Game можно сразу нажимать Play.
/// </summary>
public static class GenerateGameScene
{
    private const string PendingKey = "LG_PendingGenerate";

    [MenuItem("Tools/Generate Game")]
    public static void Generate()
    {
        // Если таргет не WebGL — ставим флаг и выходим, иначе Unity перезагрузит домен и оборвет выполнение.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorPrefs.SetBool(PendingKey, true);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            EditorApplication.delayCall += ContinueIfPending;
            return;
        }

        GenerateInternal();
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        ContinueIfPending();
    }

    private static void ContinueIfPending()
    {
        if (!EditorPrefs.GetBool(PendingKey, false)) return;
        EditorPrefs.SetBool(PendingKey, false);

        // Выполнить после перезагрузки/реимпорта
        EditorApplication.delayCall += GenerateInternal;
    }

    private static void GenerateInternal()
    {
        // Input System only
        try
        {
            var psType = typeof(PlayerSettings);

            // пытаемся найти свойство activeInputHandling
            var prop = psType.GetProperty("activeInputHandling", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null) goto CONTINUE;

            // пытаемся найти enum ActiveInputHandling и значение InputSystemPackage
            var enumType = psType.GetNestedType("ActiveInputHandling", BindingFlags.Public | BindingFlags.NonPublic);
            if (enumType == null || !enumType.IsEnum) goto CONTINUE;

            var value = Enum.Parse(enumType, "InputSystemPackage");
            prop.SetValue(null, value);
        }
        catch
        {
            // если API отличается — проект всё равно соберется, но пользователь должен включить Input System вручную
        }

CONTINUE:
        // Очистка сцены
        var scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded) return;

        foreach (var root in scene.GetRootGameObjects())
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        // Light
        CreateDirectionalLight();

        // Skybox (процедурный, без ассетов)
        TrySetProceduralSkybox();

        // EventSystem + UI Input Module
        CreateEventSystem();

        // Game root + controller
        GameObject game = new GameObject("Game");
        var controller = game.AddComponent<LabyrinthGame>();

        // Player
        CreatePlayer(out GameObject player, out Camera cam);
        player.transform.SetParent(game.transform, false);

        // Audio
        CreateAudio(out GameObject audioRoot);
        audioRoot.transform.SetParent(game.transform, false);

        // Levels root
        new GameObject("LevelsRoot").transform.SetParent(game.transform, false);

        // Main Menu UI
        CreateMainMenu(controller, out Canvas mainCanvas);

        // Pause UI
        CreatePauseMenu(controller, out Canvas pauseCanvas);

        // Связи
        controller.mainMenuCanvas = mainCanvas;
        controller.pauseCanvas = pauseCanvas;

        // По умолчанию инструкции скрыты
        if (controller.instructionsText != null)
            controller.instructionsText.gameObject.SetActive(false);

        // Важно: чтобы ссылка на Player/Audio нашлась по имени в Awake()
        player.name = "Player";
        audioRoot.name = "Audio";

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Game scene generated. Press Play.");
    }

    /// <summary>
    /// Создает Directional Light для ориентации и стабильного освещения.
    /// </summary>
    private static void CreateDirectionalLight()
    {
        var go = new GameObject("Directional Light");
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.1f;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    /// <summary>
    /// Пытается включить процедурный skybox (без внешних ассетов).
    /// </summary>
    private static void TrySetProceduralSkybox()
    {
        var shader = Shader.Find("Skybox/Procedural");
        if (shader == null) return;

        var mat = new Material(shader);
        RenderSettings.skybox = mat;
    }

    /// <summary>
    /// Создает EventSystem + InputSystemUIInputModule для работы UI в новом Input System.
    /// </summary>
    private static void CreateEventSystem()
    {
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    /// <summary>
    /// Создает игрока с CharacterController и камерой.
    /// </summary>
    private static void CreatePlayer(out GameObject player, out Camera cam)
    {
        player = new GameObject("Player");
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2.2f;
        cc.radius = 0.45f;
        cc.center = new Vector3(0f, 1.1f, 0f);

        GameObject camGo = new GameObject("Camera");
        camGo.transform.SetParent(player.transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 2000f;

        camGo.AddComponent<AudioListener>();
    }

    /// <summary>
    /// Создает корень Audio с двумя AudioSource (шаги + музыка).
    /// </summary>
    private static void CreateAudio(out GameObject audioRoot)
    {
        audioRoot = new GameObject("Audio");

        var footsteps = new GameObject("Footsteps");
        footsteps.transform.SetParent(audioRoot.transform, false);
        footsteps.AddComponent<AudioSource>();

        var music = new GameObject("Music");
        music.transform.SetParent(audioRoot.transform, false);
        music.AddComponent<AudioSource>();
    }

    /// <summary>
    /// Создает главное меню (Canvas + кнопки).
    /// </summary>
    private static void CreateMainMenu(LabyrinthGame controller, out Canvas canvas)
    {
        var root = new GameObject("MainMenu");
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        root.AddComponent<GraphicRaycaster>();

        var panel = CreatePanel(root.transform, "Panel");

        var title = CreateText(panel.transform, "Title", "MAZE EXPLORER", 34, TextAnchor.UpperCenter);
        (title.transform as RectTransform).anchoredPosition = new Vector2(0, -40);

        // Buttons container
        var buttons = new GameObject("Buttons");
        buttons.transform.SetParent(panel.transform, false);
        var rt = buttons.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420, 420);
        rt.anchoredPosition = new Vector2(0, -20);

        // Level buttons
        controller.level1Button = CreateButton(buttons.transform, "Level 1", new Vector2(0, 120));
        controller.level2Button = CreateButton(buttons.transform, "Level 2", new Vector2(0, 40));
        controller.level3Button = CreateButton(buttons.transform, "Level 3", new Vector2(0, -40));
        controller.instructionsButton = CreateButton(buttons.transform, "Instructions", new Vector2(0, -120));

        // Placeholder
        var placeholder = CreateText(panel.transform, "Placeholder", "More buttons coming soon...", 16, TextAnchor.LowerCenter);
        (placeholder.transform as RectTransform).anchoredPosition = new Vector2(0, 18);

        // Instructions text (toggle)
        controller.instructionsText = CreateText(panel.transform, "InstructionsText",
            "WASD = Move\nMouse = Look\nQ/E = Down/Up\nLMB = Place graffiti\nESC = Pause",
            18, TextAnchor.MiddleCenter);
        var insRT = controller.instructionsText.transform as RectTransform;
        insRT.sizeDelta = new Vector2(520, 220);
        insRT.anchoredPosition = new Vector2(0, 170);

        // Bind events
        controller.level1Button.onClick.AddListener(controller.UI_LoadLevel1);
        controller.level2Button.onClick.AddListener(controller.UI_LoadLevel2);
        controller.level3Button.onClick.AddListener(controller.UI_LoadLevel3);
        controller.instructionsButton.onClick.AddListener(controller.UI_ToggleInstructions);
    }

    /// <summary>
    /// Создает меню паузы (Canvas + кнопки).
    /// </summary>
    private static void CreatePauseMenu(LabyrinthGame controller, out Canvas canvas)
    {
        var root = new GameObject("PauseMenu");
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        root.AddComponent<GraphicRaycaster>();

        var panel = CreatePanel(root.transform, "Panel");
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var title = CreateText(panel.transform, "Title", "PAUSED", 34, TextAnchor.UpperCenter);
        (title.transform as RectTransform).anchoredPosition = new Vector2(0, -50);

        controller.continueButton = CreateButton(panel.transform, "Continue", new Vector2(0, 40));
        controller.soundOnButton = CreateButton(panel.transform, "Sound ON", new Vector2(0, -40));
        controller.soundOffButton = CreateButton(panel.transform, "Sound OFF", new Vector2(0, -120));
        controller.quitButton = CreateButton(panel.transform, "Quit", new Vector2(0, -200));

        controller.continueButton.onClick.AddListener(controller.UI_Continue);
        controller.soundOnButton.onClick.AddListener(controller.UI_SoundOn);
        controller.soundOffButton.onClick.AddListener(controller.UI_SoundOff);
        controller.quitButton.onClick.AddListener(controller.UI_QuitToMenu);

        // стартово скрыто (runtime включит при паузе)
        root.SetActive(false);
    }

    /// <summary>
    /// Создает полупрозрачную панель на весь экран.
    /// </summary>
    private static GameObject CreatePanel(Transform parent, string name)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = panel.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);

        return panel;
    }

    /// <summary>
    /// Создает кнопку (uGUI) с текстом.
    /// </summary>
    private static Button CreateButton(Transform parent, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(label.Replace(" ", "") + "Button");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(320, 56);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.92f);

        var btn = go.AddComponent<Button>();

        var text = CreateText(go.transform, "Text", label, 20, TextAnchor.MiddleCenter);
        var trt = text.transform as RectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        text.color = new Color(0.12f, 0.12f, 0.12f, 1f);

        return btn;
    }

    /// <summary>
    /// Создает Text элемент (uGUI).
    /// </summary>
    private static Text CreateText(Transform parent, string name, string value, int size, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.text = value;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;

        // стандартный font
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720, 120);

        return t;
    }
}
