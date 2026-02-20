using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles graffiti placement and rendering.
/// </summary>
public class GraffitiSystem : MonoBehaviour
{
    private GameConfig _config;
    private Material _graffitiMatTemplate;
    private Transform _activeGraffitiRoot;
    private Transform _activeLevelRoot;

    // Mesh cache for sprites
    private readonly Dictionary<Sprite, Mesh> _spriteMeshCache = new Dictionary<Sprite, Mesh>();

    /// <summary>
    /// Initialize the graffiti system.
    /// </summary>
    public void Initialize(GameConfig config, Material graffitiMatTemplate)
    {
        _config = config;
        _graffitiMatTemplate = graffitiMatTemplate;

        if (_config == null)
        {
            Debug.LogError("GraffitiSystem: GameConfig is null!");
        }

        if (_graffitiMatTemplate == null)
        {
            Debug.LogError("GraffitiSystem: Graffiti material template is null!");
        }
    }

    /// <summary>
    /// Set the active level root for graffiti placement validation.
    /// </summary>
    public void SetActiveLevel(Transform levelRoot, Transform graffitiRoot)
    {
        _activeLevelRoot = levelRoot;
        _activeGraffitiRoot = graffitiRoot;
    }

    /// <summary>
    /// Attempts to place graffiti at the camera's forward direction.
    /// </summary>
    public bool TryPlaceGraffiti(Transform cameraTransform, InputAction placeAction)
    {
        if (_config == null || _graffitiMatTemplate == null)
        {
            Debug.LogWarning("GraffitiSystem: Not initialized properly!");
            return false;
        }

        if (cameraTransform == null)
        {
            Debug.LogWarning("GraffitiSystem: Camera transform is null!");
            return false;
        }

        if (placeAction == null || !placeAction.WasPressedThisFrame())
            return false;

        if (_config.graffitiSprites == null || _config.graffitiSprites.Length == 0)
        {
            Debug.LogWarning("GraffitiSystem: No graffiti sprites configured!");
            return false;
        }

        if (_activeGraffitiRoot == null || _activeLevelRoot == null)
        {
            Debug.LogWarning("GraffitiSystem: Active level or graffiti root is null!");
            return false;
        }

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
            return false;

        // Only allow graffiti on level objects
        if (!hit.collider.transform.IsChildOf(_activeLevelRoot))
            return false;

        // Select random sprite
        Sprite sprite = _config.graffitiSprites[Random.Range(0, _config.graffitiSprites.Length)];

        // Create graffiti object
        GameObject graffiti = new GameObject("Graffiti");
        graffiti.transform.SetParent(_activeGraffitiRoot, true);

        Vector3 pos = hit.point + hit.normal * _config.graffitiOffset;
        graffiti.transform.position = pos;
        graffiti.transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);

        MeshFilter mf = graffiti.AddComponent<MeshFilter>();
        MeshRenderer mr = graffiti.AddComponent<MeshRenderer>();

        mf.sharedMesh = GetOrCreateMeshForSprite(sprite);

        Material mat = new Material(_graffitiMatTemplate);
        ApplySpriteToMaterial(sprite, mat);
        mr.sharedMaterial = mat;

        graffiti.transform.localScale = Vector3.one * _config.graffitiSize;

        return true;
    }

    /// <summary>
    /// Gets or creates a mesh for a sprite with proper UV mapping.
    /// </summary>
    private Mesh GetOrCreateMeshForSprite(Sprite sprite)
    {
        if (sprite == null)
            return CreateUnitQuadMesh();

        if (_spriteMeshCache.TryGetValue(sprite, out Mesh cached) && cached != null)
            return cached;

        Mesh mesh = CreateUnitQuadMesh();

        Rect textureRect = sprite.textureRect;
        Texture texture = sprite.texture;

        float u0 = textureRect.xMin / texture.width;
        float v0 = textureRect.yMin / texture.height;
        float u1 = textureRect.xMax / texture.width;
        float v1 = textureRect.yMax / texture.height;

        Vector2[] uv = new Vector2[4];
        uv[0] = new Vector2(u0, v0);
        uv[1] = new Vector2(u1, v0);
        uv[2] = new Vector2(u0, v1);
        uv[3] = new Vector2(u1, v1);
        mesh.uv = uv;

        _spriteMeshCache[sprite] = mesh;
        return mesh;
    }

    /// <summary>
    /// Creates a unit quad mesh.
    /// </summary>
    private Mesh CreateUnitQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "UnitQuadMesh";

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(-0.5f, -0.5f, 0f);
        vertices[1] = new Vector3(0.5f, -0.5f, 0f);
        vertices[2] = new Vector3(-0.5f, 0.5f, 0f);
        vertices[3] = new Vector3(0.5f, 0.5f, 0f);

        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Applies sprite texture to material.
    /// </summary>
    private void ApplySpriteToMaterial(Sprite sprite, Material material)
    {
        if (sprite == null || material == null) return;

        material.mainTexture = sprite.texture;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", sprite.texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
    }
}
