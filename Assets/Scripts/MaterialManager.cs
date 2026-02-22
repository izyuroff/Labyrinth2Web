using UnityEngine;

/// <summary>
/// Manages material creation and caching for the game.
/// </summary>
public class MaterialManager : MonoBehaviour
{
    private Material _floorMat;
    private Material _bgMat;
    private Material _wallMatTemplate;
    private Material _graffitiMatTemplate;
    private GameConfig _config;
    private bool _isReady;

    /// <summary>
    /// Initialize materials with configuration.
    /// </summary>
    public void Initialize(GameConfig config)
    {
        _config = config;

        if (_config == null)
        {
            Debug.LogError("MaterialManager: GameConfig is null!");
            _isReady = false;
            return;
        }

        _isReady = PrepareMaterials();
    }

    public bool IsReady => _isReady;

    /// <summary>
    /// Prepares materials for floor, walls, background, and graffiti.
    /// </summary>
    private bool PrepareMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("MaterialManager: URP Lit shader not found. Check URP setup (URP pipeline asset assigned).");
            return false;
        }

        // Floor material
        if (_floorMat == null)
            _floorMat = new Material(lit);
        else
            _floorMat.shader = lit;

        if (_floorMat.HasProperty("_Cull"))
            _floorMat.SetFloat("_Cull", 2f); // Back

        if (_floorMat.HasProperty("_BaseColor"))
            _floorMat.SetColor("_BaseColor", _config.floorColor);
        else
            _floorMat.color = _config.floorColor;

        // Wall material template
        if (_wallMatTemplate == null)
            _wallMatTemplate = new Material(lit);
        else
            _wallMatTemplate.shader = lit;

        if (_wallMatTemplate.HasProperty("_Cull"))
            _wallMatTemplate.SetFloat("_Cull", 0f); // Off (double-sided)

        if (_wallMatTemplate.HasProperty("_BaseColor"))
            _wallMatTemplate.SetColor("_BaseColor", Color.white);
        else
            _wallMatTemplate.color = Color.white;

        // Background material
        if (_bgMat == null)
            _bgMat = new Material(lit);
        else
            _bgMat.shader = lit;

        if (_bgMat.HasProperty("_Cull"))
            _bgMat.SetFloat("_Cull", 2f); // Back

        // Graffiti material template
        if (_graffitiMatTemplate == null)
            _graffitiMatTemplate = new Material(lit);
        else
            _graffitiMatTemplate.shader = lit;

        if (_graffitiMatTemplate.HasProperty("_Cull"))
            _graffitiMatTemplate.SetFloat("_Cull", 0f); // Off (double-sided)

        if (_graffitiMatTemplate.HasProperty("_BaseColor"))
            _graffitiMatTemplate.SetColor("_BaseColor", Color.white);
        else
            _graffitiMatTemplate.color = Color.white;

        return true;
    }

    /// <summary>
    /// Gets the floor material.
    /// </summary>
    public Material GetFloorMaterial()
    {
        if (_floorMat == null)
            Debug.LogWarning("MaterialManager: Floor material is null!");
        return _floorMat;
    }

    /// <summary>
    /// Gets the wall material template.
    /// </summary>
    public Material GetWallMaterialTemplate()
    {
        if (_wallMatTemplate == null)
            Debug.LogWarning("MaterialManager: Wall material template is null!");
        return _wallMatTemplate;
    }

    /// <summary>
    /// Gets the background material.
    /// </summary>
    public Material GetBackgroundMaterial()
    {
        if (_bgMat == null)
            Debug.LogWarning("MaterialManager: Background material is null!");
        return _bgMat;
    }

    /// <summary>
    /// Gets the graffiti material template.
    /// </summary>
    public Material GetGraffitiMaterialTemplate()
    {
        if (_graffitiMatTemplate == null)
            Debug.LogWarning("MaterialManager: Graffiti material template is null!");
        return _graffitiMatTemplate;
    }
}
