using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates mazes with exactly two entrances on opposite sides.
/// Ensures a valid path exists between the two entrances.
/// Maze is centered within background walls with padding.
/// </summary>
public class MazeGenerator : MonoBehaviour
{
    private GameConfig _config;
    private Material _floorMat;
    private Material _wallMatTemplate;
    private Material _bgMat;

    // Cached list to avoid allocations
    private readonly List<Vector2Int> _cachedOptionsList = new List<Vector2Int>(4);
    private readonly Dictionary<Sprite, Mesh> _spriteMeshCache = new Dictionary<Sprite, Mesh>();

    // Maze generation data
    private bool[,] _visited;
    private bool[,] _vWall; // vertical walls
    private bool[,] _hWall; // horizontal walls
    private int _mazeWidth;
    private int _mazeHeight;
    private Vector2Int _entrance1;
    private Vector2Int _entrance2;
    private Vector3 _mazeOffset; // Offset to center the maze within background walls
    private List<Vector3> _pathPoints = new List<Vector3>(); // Path from entrance1 to entrance2

    /// <summary>
    /// Initialize the generator with configuration.
    /// </summary>
    public void Initialize(GameConfig config, Material floorMat, Material wallMatTemplate, Material bgMat)
    {
        _config = config;
        _floorMat = floorMat;
        _wallMatTemplate = wallMatTemplate;
        _bgMat = bgMat;

        if (_config == null)
        {
            Debug.LogError("MazeGenerator: GameConfig is null!");
        }
    }

    /// <summary>
    /// Generates a maze level with two entrances on opposite sides.
    /// </summary>
    public void GenerateLevel(LevelConfig levelConfig, Transform parent, int levelIndex, out Vector3 entrance1Position, out Vector3 entrance2Position)
    {
        if (_config == null)
        {
            Debug.LogError("MazeGenerator: Not initialized! Call Initialize() first.");
            entrance1Position = Vector3.zero;
            entrance2Position = Vector3.zero;
            return;
        }

        if (levelConfig == null)
        {
            Debug.LogError($"MazeGenerator: LevelConfig for level {levelIndex} is null!");
            entrance1Position = Vector3.zero;
            entrance2Position = Vector3.zero;
            return;
        }

        _mazeWidth = levelConfig.width;
        _mazeHeight = levelConfig.height;

        // Calculate padding to center maze within background (minimum 2 cells padding)
        int padding = Mathf.Max(2, Mathf.Min(_mazeWidth, _mazeHeight) / 4);
        
        // Calculate total floor size including padding
        int floorCols = _mazeWidth * 2 + 1 + padding * 2;
        int floorRows = _mazeHeight * 2 + 1 + padding * 2;
        
        // Calculate maze offset to center it
        _mazeOffset = new Vector3(padding * _config.cellSize, 0f, padding * _config.cellSize);

        // Create floor (covers entire area including padding)
        CreateFloor(parent, floorCols, floorRows);

        if (levelConfig.useAscii && levelConfig.asciiData != null && levelConfig.asciiData.Length > 0)
        {
            BuildMazeFromAscii(levelConfig.asciiData, parent, levelIndex);
            // For ASCII mazes, two entrances at fixed positions (outside the maze walls)
            int asciiCols = levelConfig.asciiData[0].Length;
            int asciiRows = levelConfig.asciiData.Length;
            entrance1Position = _mazeOffset + new Vector3(
                (asciiCols / 2) * _config.cellSize + _config.cellSize * 0.5f,
                0f,
                -_config.cellSize * 0.5f // Outside South wall
            );
            entrance2Position = _mazeOffset + new Vector3(
                (asciiCols / 2) * _config.cellSize + _config.cellSize * 0.5f,
                0f,
                asciiRows * _config.cellSize + _config.cellSize * 0.5f // Outside North wall
            );
            
            // Calculate path for ASCII mazes (simple path through center)
            CalculatePathPointsForAscii(asciiCols, asciiRows);
        }
        else
        {
            // Generate procedural maze with exactly two entrances
            GenerateProceduralMaze(levelConfig.seed, parent, levelIndex, out entrance1Position, out entrance2Position);
        }

        // Build background (covers entire floor area)
        BuildBackground(parent, floorCols, floorRows);
    }

    /// <summary>
    /// Generates a procedural maze using DFS backtracker with exactly two entrances.
    /// </summary>
    private void GenerateProceduralMaze(int seed, Transform parent, int levelIndex, out Vector3 entrance1Position, out Vector3 entrance2Position)
    {
        System.Random rnd = new System.Random(seed);

        // Initialize wall arrays
        _visited = new bool[_mazeWidth, _mazeHeight];
        _vWall = new bool[_mazeWidth + 1, _mazeHeight];
        _hWall = new bool[_mazeWidth, _mazeHeight + 1];

        // All walls exist initially
        for (int x = 0; x < _mazeWidth + 1; x++)
            for (int y = 0; y < _mazeHeight; y++)
                _vWall[x, y] = true;

        for (int x = 0; x < _mazeWidth; x++)
            for (int y = 0; y < _mazeHeight + 1; y++)
                _hWall[x, y] = true;

        // Set two entrances on opposite sides (South and North)
        // Entrance 1: South side, center
        _entrance1 = new Vector2Int(_mazeWidth / 2, 0);
        _hWall[_entrance1.x, 0] = false; // Remove wall at entrance 1

        // Entrance 2: North side, center or slightly offset for difficulty
        int entrance2Offset = Mathf.Min(levelIndex - 1, _mazeWidth / 4); // More offset = harder
        _entrance2 = new Vector2Int(_mazeWidth / 2 + entrance2Offset, _mazeHeight - 1);
        _hWall[_entrance2.x, _mazeHeight] = false; // Remove wall at entrance 2

        // DFS generation starting from entrance 1
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int current = _entrance1;
        _visited[current.x, current.y] = true;
        stack.Push(current);

        Vector2Int[] directions = {
            new Vector2Int(1, 0),   // Right
            new Vector2Int(-1, 0),  // Left
            new Vector2Int(0, 1),   // Up
            new Vector2Int(0, -1)   // Down
        };

        while (stack.Count > 0)
        {
            current = stack.Peek();

            // Clear and reuse cached list
            _cachedOptionsList.Clear();

            // Find unvisited neighbors
            for (int i = 0; i < 4; i++)
            {
                Vector2Int neighbor = current + directions[i];
                if (neighbor.x >= 0 && neighbor.x < _mazeWidth &&
                    neighbor.y >= 0 && neighbor.y < _mazeHeight &&
                    !_visited[neighbor.x, neighbor.y])
                {
                    _cachedOptionsList.Add(neighbor);
                }
            }

            if (_cachedOptionsList.Count == 0)
            {
                stack.Pop();
                continue;
            }

            // Choose random unvisited neighbor
            Vector2Int next = _cachedOptionsList[rnd.Next(_cachedOptionsList.Count)];

            // Remove wall between current and next
            if (next.x == current.x + 1)
            {
                _vWall[current.x + 1, current.y] = false; // Right
            }
            else if (next.x == current.x - 1)
            {
                _vWall[current.x, current.y] = false; // Left
            }
            else if (next.y == current.y + 1)
            {
                _hWall[current.x, current.y + 1] = false; // Up
            }
            else if (next.y == current.y - 1)
            {
                _hWall[current.x, current.y] = false; // Down
            }

            _visited[next.x, next.y] = true;
            stack.Push(next);
        }

        // Verify path exists between the two entrances (BFS from entrance1 to entrance2)
        if (!VerifyPathExists())
        {
            Debug.LogWarning($"MazeGenerator: No path found between entrances for level {levelIndex}. Creating direct path...");
            // Try to create a direct path
            CreateDirectPath();
        }

        // Calculate path points for debug visualization
        CalculatePathPoints();

        // Build walls (with offset to center the maze)
        int wallId = 0;

        // Vertical walls
        for (int x = 0; x < _mazeWidth + 1; x++)
        {
            for (int y = 0; y < _mazeHeight; y++)
            {
                if (_vWall[x, y])
                {
                    CreateWallAtWorld(parent,
                        _mazeOffset + new Vector3(x * _config.cellSize, 0f, y * _config.cellSize + _config.cellSize * 0.5f),
                        Quaternion.Euler(0f, 90f, 0f),
                        _config.cellSize,
                        ref wallId,
                        levelIndex);
                }
            }
        }

        // Horizontal walls
        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int y = 0; y < _mazeHeight + 1; y++)
            {
                if (_hWall[x, y])
                {
                    CreateWallAtWorld(parent,
                        _mazeOffset + new Vector3(x * _config.cellSize + _config.cellSize * 0.5f, 0f, y * _config.cellSize),
                        Quaternion.identity,
                        _config.cellSize,
                        ref wallId,
                        levelIndex);
                }
            }
        }

        // Calculate world positions for the two entrances (with offset)
        // Position entrances slightly outside the maze boundary (before the outer wall)
        Vector3 entrance1CellCenter = _mazeOffset + new Vector3(
            _entrance1.x * _config.cellSize + _config.cellSize * 0.5f,
            0f,
            _entrance1.y * _config.cellSize + _config.cellSize * 0.5f
        );
        
        entrance1Position = entrance1CellCenter + new Vector3(0f, 0f, -_config.cellSize * 0.5f); // Move outside South wall

        Vector3 entrance2CellCenter = _mazeOffset + new Vector3(
            _entrance2.x * _config.cellSize + _config.cellSize * 0.5f,
            0f,
            _entrance2.y * _config.cellSize + _config.cellSize * 0.5f
        );
        
        entrance2Position = entrance2CellCenter + new Vector3(0f, 0f, _config.cellSize * 0.5f); // Move outside North wall
        
        // Add entrance positions to path for visualization
        if (_pathPoints.Count > 0)
        {
            _pathPoints.Insert(0, entrance1CellCenter); // Add entrance1 cell center
            _pathPoints.Add(entrance2CellCenter); // Add entrance2 cell center
        }
    }

    /// <summary>
    /// Calculates the path points from entrance1 to entrance2 for debug visualization.
    /// </summary>
    private void CalculatePathPoints()
    {
        _pathPoints.Clear();

        // Use BFS to find path and store points
        bool[,] visited = new bool[_mazeWidth, _mazeHeight];
        Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        
        queue.Enqueue(_entrance1);
        visited[_entrance1.x, _entrance1.y] = true;

        Vector2Int[] directions = {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        bool found = false;
        while (queue.Count > 0 && !found)
        {
            Vector2Int current = queue.Dequeue();

            if (current.x == _entrance2.x && current.y == _entrance2.y)
            {
                found = true;
                // Reconstruct path (including both entrances)
                Vector2Int node = _entrance2;
                List<Vector3> tempPath = new List<Vector3>();
                
                // Add entrance2 position
                tempPath.Add(_mazeOffset + new Vector3(
                    node.x * _config.cellSize + _config.cellSize * 0.5f,
                    0f,
                    node.y * _config.cellSize + _config.cellSize * 0.5f
                ));
                
                // Reconstruct path backwards
                while (true)
                {
                    if (node.x == _entrance1.x && node.y == _entrance1.y)
                        break;

                    if (parent.TryGetValue(node, out Vector2Int prev))
                    {
                        node = prev;
                        Vector3 worldPos = _mazeOffset + new Vector3(
                            node.x * _config.cellSize + _config.cellSize * 0.5f,
                            0f,
                            node.y * _config.cellSize + _config.cellSize * 0.5f
                        );
                        tempPath.Insert(0, worldPos);
                    }
                    else
                        break;
                }
                
                // Reverse to get path from entrance1 to entrance2
                _pathPoints = tempPath;
                break;
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2Int next = current + directions[i];
                if (next.x >= 0 && next.x < _mazeWidth &&
                    next.y >= 0 && next.y < _mazeHeight &&
                    !visited[next.x, next.y] &&
                    CanMove(current, next))
                {
                    visited[next.x, next.y] = true;
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }
        }
    }

    /// <summary>
    /// Gets the calculated path points for debug visualization.
    /// </summary>
    public List<Vector3> GetPathPoints()
    {
        return _pathPoints;
    }

    /// <summary>
    /// Verifies that a path exists between the two entrances using BFS.
    /// </summary>
    private bool VerifyPathExists()
    {
        bool[,] visited = new bool[_mazeWidth, _mazeHeight];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(_entrance1);
        visited[_entrance1.x, _entrance1.y] = true;

        Vector2Int[] directions = {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current.x == _entrance2.x && current.y == _entrance2.y)
                return true;

            for (int i = 0; i < 4; i++)
            {
                Vector2Int next = current + directions[i];
                if (next.x >= 0 && next.x < _mazeWidth &&
                    next.y >= 0 && next.y < _mazeHeight &&
                    !visited[next.x, next.y] &&
                    CanMove(current, next))
                {
                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if movement is possible between two cells.
    /// </summary>
    private bool CanMove(Vector2Int from, Vector2Int to)
    {
        if (to.x == from.x + 1) return !_vWall[from.x + 1, from.y]; // Right
        if (to.x == from.x - 1) return !_vWall[from.x, from.y]; // Left
        if (to.y == from.y + 1) return !_hWall[from.x, from.y + 1]; // Up
        if (to.y == from.y - 1) return !_hWall[from.x, from.y]; // Down
        return false;
    }

    /// <summary>
    /// Creates a direct path between the two entrances if verification failed.
    /// </summary>
    private void CreateDirectPath()
    {
        Vector2Int current = _entrance1;
        Vector2Int target = _entrance2;

        // Simple path: move horizontally first, then vertically
        int xDir = target.x > current.x ? 1 : -1;
        int yDir = target.y > current.y ? 1 : -1;

        // Move horizontally
        while (current.x != target.x)
        {
            Vector2Int next = new Vector2Int(current.x + xDir, current.y);
            if (next.x >= 0 && next.x < _mazeWidth)
            {
                if (xDir > 0) _vWall[current.x + 1, current.y] = false;
                else _vWall[current.x, current.y] = false;
                current = next;
                _visited[current.x, current.y] = true;
            }
            else break;
        }

        // Move vertically
        while (current.y != target.y)
        {
            Vector2Int next = new Vector2Int(current.x, current.y + yDir);
            if (next.y >= 0 && next.y < _mazeHeight)
            {
                if (yDir > 0) _hWall[current.x, current.y + 1] = false;
                else _hWall[current.x, current.y] = false;
                current = next;
                _visited[current.x, current.y] = true;
            }
            else break;
        }
    }

    /// <summary>
    /// Builds maze from ASCII data.
    /// </summary>
    private void BuildMazeFromAscii(string[] grid, Transform parent, int levelIndex)
    {
        if (grid == null || grid.Length == 0) return;

        int rows = grid.Length;
        int cols = grid[0].Length;

        bool IsWall(int r, int c)
        {
            if (r < 0 || c < 0 || r >= rows || c >= cols) return true;
            return grid[r][c] == '#';
        }

        int wallId = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (!IsWall(r, c)) continue;

                if (!IsWall(r - 1, c))
                    CreateWallSegment(parent, c, r, Direction.North, ref wallId, levelIndex);
                if (!IsWall(r + 1, c))
                    CreateWallSegment(parent, c, r, Direction.South, ref wallId, levelIndex);
                if (!IsWall(r, c - 1))
                    CreateWallSegment(parent, c, r, Direction.West, ref wallId, levelIndex);
                if (!IsWall(r, c + 1))
                    CreateWallSegment(parent, c, r, Direction.East, ref wallId, levelIndex);
            }
        }
        
        // Set two entrances for ASCII mazes (South and North sides)
        _entrance1 = new Vector2Int(cols / 2, 0);
        _entrance2 = new Vector2Int(cols / 2, rows - 1);
    }

    /// <summary>
    /// Calculates path points for ASCII mazes (simplified path).
    /// </summary>
    private void CalculatePathPointsForAscii(int cols, int rows)
    {
        _pathPoints.Clear();
        
        // Simple path: from entrance1 (South center) to entrance2 (North center)
        // Go straight up the middle
        int centerX = cols / 2;
        for (int y = 0; y < rows; y++)
        {
            Vector3 worldPos = _mazeOffset + new Vector3(
                centerX * _config.cellSize + _config.cellSize * 0.5f,
                0f,
                y * _config.cellSize + _config.cellSize * 0.5f
            );
            _pathPoints.Add(worldPos);
        }
    }

    /// <summary>
    /// Creates a wall segment for ASCII-based mazes.
    /// </summary>
    private void CreateWallSegment(Transform parent, int c, int r, Direction dir, ref int wallId, int levelIndex)
    {
        float x = _mazeOffset.x + c * _config.cellSize;
        float z = _mazeOffset.z + r * _config.cellSize;

        Vector3 pos;
        Quaternion rot;

        switch (dir)
        {
            case Direction.North:
                pos = new Vector3(x + _config.cellSize * 0.5f, 0f, z);
                rot = Quaternion.identity;
                break;
            case Direction.South:
                pos = new Vector3(x + _config.cellSize * 0.5f, 0f, z + _config.cellSize);
                rot = Quaternion.identity;
                break;
            case Direction.West:
                pos = new Vector3(x, 0f, z + _config.cellSize * 0.5f);
                rot = Quaternion.Euler(0f, 90f, 0f);
                break;
            case Direction.East:
                pos = new Vector3(x + _config.cellSize, 0f, z + _config.cellSize * 0.5f);
                rot = Quaternion.Euler(0f, 90f, 0f);
                break;
            default:
                pos = Vector3.zero;
                rot = Quaternion.identity;
                break;
        }

        CreateWallAtWorld(parent, pos, rot, _config.cellSize, ref wallId, levelIndex);
    }

    /// <summary>
    /// Creates a floor quad.
    /// </summary>
    private void CreateFloor(Transform parent, int cols, int rows)
    {
        if (_floorMat == null)
        {
            Debug.LogError("MazeGenerator: Floor material is null!");
            return;
        }

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "Floor";
        floor.transform.SetParent(parent, false);
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        float w = cols * _config.cellSize;
        float h = rows * _config.cellSize;

        floor.transform.localScale = new Vector3(w, h, 1f);
        floor.transform.localPosition = new Vector3(w * 0.5f, 0f, h * 0.5f);

        var mr = floor.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.sharedMaterial = _floorMat;
        else
            Debug.LogError("MazeGenerator: Floor MeshRenderer is null!");

        var bc = floor.GetComponent<Collider>();
        if (bc == null)
            floor.AddComponent<MeshCollider>();
    }

    /// <summary>
    /// Creates a wall in world space.
    /// </summary>
    private void CreateWallAtWorld(Transform parent, Vector3 baseCenter, Quaternion rot, float length, ref int wallId, int levelIndex)
    {
        if (_wallMatTemplate == null)
        {
            Debug.LogError("MazeGenerator: Wall material template is null!");
            return;
        }

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wall.name = $"Wall_{wallId}";
        wall.transform.SetParent(parent, false);
        wall.transform.localRotation = rot;
        wall.transform.localPosition = baseCenter + new Vector3(0f, _config.wallHeight * 0.5f, 0f);
        wall.transform.localScale = new Vector3(length, _config.wallHeight, 1f);

        Material mat = new Material(_wallMatTemplate);
        Color col = _config.wallColors != null && _config.wallColors.Length > 0
            ? _config.wallColors[wallId % _config.wallColors.Length]
            : Color.white;

        col = Color.Lerp(col, Color.white, Mathf.Clamp01((levelIndex - 1) * 0.08f));
        mat.color = col;

        var mr = wall.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.sharedMaterial = mat;
        else
            Debug.LogError("MazeGenerator: Wall MeshRenderer is null!");

        foreach (var c in wall.GetComponents<Collider>())
            Destroy(c);

        var bc = wall.AddComponent<BoxCollider>();
        bc.size = new Vector3(1f, 1f, _config.wallThickness);
        bc.center = Vector3.zero;

        wallId++;
    }

    /// <summary>
    /// Creates background walls around the maze perimeter.
    /// </summary>
    private void BuildBackground(Transform parent, int cols, int rows)
    {
        if (_bgMat == null)
        {
            Debug.LogWarning("MazeGenerator: Background material is null, skipping background creation.");
            return;
        }

        float w = cols * _config.cellSize;
        float h = rows * _config.cellSize;
        float gap = 0f;
        float wallW = w + gap * 2f;
        float wallH = Mathf.Max(10f, _config.backgroundHeight);
        float yCenter = wallH * 0.5f;

        Material MakeSideMat(int texIndex)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material m = (lit != null) ? new Material(lit) : new Material(Shader.Find("Standard"));

            if (_config.backgroundTextures != null &&
                texIndex >= 0 && texIndex < _config.backgroundTextures.Length &&
                _config.backgroundTextures[texIndex] != null)
            {
                m.mainTexture = _config.backgroundTextures[texIndex];
            }

            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
            return m;
        }

        CreateBackgroundQuad(parent, new Vector3(w * 0.5f, yCenter, h + gap), new Vector3(wallW, wallH, 1f), Quaternion.identity, MakeSideMat(0), "BG_N");
        CreateBackgroundQuad(parent, new Vector3(w * 0.5f, yCenter, -gap), new Vector3(wallW, wallH, 1f), Quaternion.Euler(0f, 180f, 0f), MakeSideMat(1), "BG_S");
        CreateBackgroundQuad(parent, new Vector3(w + gap, yCenter, h * 0.5f), new Vector3(h + gap * 2f, wallH, 1f), Quaternion.Euler(0f, -90f, 0f), MakeSideMat(2), "BG_E");
        CreateBackgroundQuad(parent, new Vector3(-gap, yCenter, h * 0.5f), new Vector3(h + gap * 2f, wallH, 1f), Quaternion.Euler(0f, 90f, 0f), MakeSideMat(3), "BG_W");
    }

    /// <summary>
    /// Creates a background quad.
    /// </summary>
    private void CreateBackgroundQuad(Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, string name)
    {
        const float thickness = 0.2f;

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bg.name = name;
        bg.transform.SetParent(parent, false);
        bg.transform.localPosition = pos;
        bg.transform.localRotation = rot;
        bg.transform.localScale = new Vector3(scale.x, scale.y, thickness);

        var mr = bg.GetComponent<MeshRenderer>();
        if (mr != null && mat != null)
            mr.sharedMaterial = mat;
        else
            Debug.LogWarning($"MazeGenerator: Could not set material for {name}");

        foreach (var c in bg.GetComponents<Collider>())
            Destroy(c);
    }
}
