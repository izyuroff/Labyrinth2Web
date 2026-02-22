using UnityEngine;

/// <summary>
/// Configuration ScriptableObject for game settings.
/// Create instances via: Right-click → Create → Labyrinth → Game Config
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Labyrinth/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Graffiti Sprites")]
    public Sprite[] graffitiSprites;

    [Header("Maze Dimensions")]
    public float cellSize = 6f;
    public float wallHeight = 12f; // Reduced from 120f - was too high
    public float wallThickness = 0.25f;

    [Header("Player Parameters")]
    public float moveSpeed = 8f;
    public float lookSensitivity = 0.12f;
    public float cameraHeight = 2.0f;
    public float graffitiSize = 2.2f;
    public float graffitiOffset = 0.02f;

    [Header("Wall Colors (Fixed, not random)")]
    public Color[] wallColors =
    {
        new Color(0.95f, 0.35f, 0.35f, 1f),
        new Color(0.35f, 0.75f, 0.95f, 1f),
        new Color(0.55f, 0.95f, 0.45f, 1f),
        new Color(0.95f, 0.85f, 0.35f, 1f),
        new Color(0.85f, 0.45f, 0.95f, 1f),
        new Color(0.95f, 0.55f, 0.15f, 1f),
    };

    [Header("Materials and Background")]
    public Color floorColor = new Color(0.78f, 0.78f, 0.78f, 1f);
    public Texture2D[] backgroundTextures;
    public float backgroundSize = 1200f;
    public float backgroundHeight = 200f;

    [Header("Audio (WebGL: wav/ogg)")]
    public AudioClip footstepLoop;
    public AudioClip backgroundMusic;
    public float footstepVolume = 0.65f;
    public float musicVolume = 0.45f;

    [Header("Level Settings")]
    public LevelConfig[] levels = new LevelConfig[]
    {
        new LevelConfig
        {
            width = 20,
            height = 15,
            seed = 202601,
            useAscii = true,
            asciiData = new string[]
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
                "########################"
            }
        },
        new LevelConfig { width = 24, height = 18, seed = 202602, useAscii = false },
        new LevelConfig { width = 34, height = 24, seed = 202603, useAscii = false }
    };

    /// <summary>
    /// Validates configuration and returns error message if invalid.
    /// </summary>
    public string Validate()
    {
        if (levels == null || levels.Length == 0)
            return "No levels configured";

        if (wallColors == null || wallColors.Length == 0)
            return "No wall colors configured";

        if (cellSize <= 0)
            return "Cell size must be greater than 0";

        if (wallHeight <= 0)
            return "Wall height must be greater than 0";

        for (int i = 0; i < levels.Length; i++)
        {
            LevelConfig level = levels[i];
            if (level == null)
                return $"Level config at index {i + 1} is null";

            if (level.width <= 0)
                return $"Level {i + 1}: width must be greater than 0";

            if (level.height <= 0)
                return $"Level {i + 1}: height must be greater than 0";

            if (level.useAscii)
            {
                if (level.asciiData == null || level.asciiData.Length == 0)
                    return $"Level {i + 1}: ASCII data is required when useAscii is true";

                int lineLength = level.asciiData[0]?.Length ?? 0;
                if (lineLength == 0)
                    return $"Level {i + 1}: ASCII lines must not be empty";

                for (int row = 0; row < level.asciiData.Length; row++)
                {
                    string line = level.asciiData[row];
                    if (line == null || line.Length != lineLength)
                        return $"Level {i + 1}: ASCII lines must share the same length";
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Configuration for a single level.
/// </summary>
[System.Serializable]
public class LevelConfig
{
    public int width = 20;
    public int height = 15;
    public int seed = 0;
    public bool useAscii = false;
    public string[] asciiData;
}
