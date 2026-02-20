using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Renders debug path lines on the floor showing the correct route through the maze.
/// </summary>
public class PathDebugRenderer : MonoBehaviour
{
    [Header("Debug Path Settings")]
    [SerializeField] private Color pathColor = Color.green;
    [SerializeField] private float pathWidth = 0.2f;
    [SerializeField] private float pathHeight = 0.01f; // Slightly above floor
    
    private List<Vector3> _pathPoints = new List<Vector3>();
    private bool _pathVisible = false;
    private InputAction _togglePathAction;
    private LineRenderer _lineRenderer;

    /// <summary>
    /// Initialize the path debug renderer.
    /// </summary>
    public void Initialize()
    {
        // Create LineRenderer for drawing path
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        
        // Use Unlit shader for WebGL compatibility
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
            
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = pathColor;
            _lineRenderer.material = mat;
        }
        
        _lineRenderer.startColor = pathColor;
		_lineRenderer.endColor = pathColor;
        _lineRenderer.startWidth = pathWidth;
        _lineRenderer.endWidth = pathWidth;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 0;
        _lineRenderer.enabled = false;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;

        // Create toggle input action
        _togglePathAction = new InputAction("TogglePath", InputActionType.Button, "<Keyboard>/r");
    }

    /// <summary>
    /// Sets the path points to render.
    /// </summary>
    public void SetPath(List<Vector3> pathPoints, float cellSize)
    {
        _pathPoints.Clear();
        
        if (pathPoints == null || pathPoints.Count < 2)
        {
            _lineRenderer.enabled = false;
            return;
        }

        // Convert grid positions to world positions with slight height offset
        foreach (Vector3 point in pathPoints)
        {
            _pathPoints.Add(new Vector3(point.x, pathHeight, point.z));
        }

        UpdateLineRenderer();
    }

    /// <summary>
    /// Updates the line renderer with current path points.
    /// </summary>
    private void UpdateLineRenderer()
    {
        if (_lineRenderer == null) return;

        _lineRenderer.positionCount = _pathPoints.Count;
        for (int i = 0; i < _pathPoints.Count; i++)
        {
            _lineRenderer.SetPosition(i, _pathPoints[i]);
        }

        _lineRenderer.enabled = _pathVisible && _pathPoints.Count > 0;
    }

    /// <summary>
    /// Toggles path visibility.
    /// </summary>
    public void TogglePath()
    {
        _pathVisible = !_pathVisible;
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = _pathVisible && _pathPoints.Count > 0;
        }
    }

    /// <summary>
    /// Enable input actions.
    /// </summary>
    private void OnEnable()
    {
        _togglePathAction?.Enable();
    }

    /// <summary>
    /// Disable input actions.
    /// </summary>
    private void OnDisable()
    {
        _togglePathAction?.Disable();
    }

    /// <summary>
    /// Update - check for toggle input.
    /// </summary>
    private void Update()
    {
        if (_togglePathAction != null && _togglePathAction.WasPressedThisFrame())
        {
            TogglePath();
        }
    }

    /// <summary>
    /// Updates path color (called when color changes in inspector).
    /// </summary>
    private void OnValidate()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.startColor = pathColor;
			_lineRenderer.endColor = pathColor;
			if (_lineRenderer.material != null)
			_lineRenderer.material.color = pathColor;
        }
    }
}
