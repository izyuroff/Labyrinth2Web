using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles first-person player movement and camera control.
/// </summary>
public class PlayerController : MonoBehaviour
{
    private GameConfig _config;
    private CharacterController _characterController;
    private Transform _playerRoot;
    private Camera _camera;

    // Input actions
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _flyAction;

    // Look state
    private float _yaw;
    private float _pitch;

    /// <summary>
    /// Initialize the player controller.
    /// </summary>
    public void Initialize(GameConfig config)
    {
        _config = config;

        if (_config == null)
        {
            Debug.LogError("PlayerController: GameConfig is null!");
            return;
        }

        // Find player objects
        _playerRoot = GameObject.Find("Player")?.transform;
        if (_playerRoot == null)
        {
            Debug.LogError("PlayerController: Player GameObject not found!");
            return;
        }

        _characterController = _playerRoot.GetComponent<CharacterController>();
        if (_characterController == null)
        {
            Debug.LogError("PlayerController: CharacterController component not found!");
        }

        _camera = _playerRoot.GetComponentInChildren<Camera>(true);
        if (_camera == null)
        {
            Debug.LogError("PlayerController: Camera not found!");
        }

        CreateInputActions();
    }

    /// <summary>
    /// Creates input actions for player control.
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

        // Enable actions immediately after creation
        EnableInputActions();
    }

    /// <summary>
    /// Enable input actions.
    /// </summary>
    private void EnableInputActions()
    {
        if (_moveAction != null) _moveAction.Enable();
        if (_lookAction != null) _lookAction.Enable();
        if (_flyAction != null) _flyAction.Enable();
    }

    /// <summary>
    /// Enable input actions when component is enabled.
    /// </summary>
    private void OnEnable()
    {
        // Only enable if actions have been created (after Initialize)
        if (_moveAction != null)
        {
            EnableInputActions();
        }
    }

    /// <summary>
    /// Disable input actions.
    /// </summary>
    private void OnDisable()
    {
        _moveAction?.Disable();
        _lookAction?.Disable();
        _flyAction?.Disable();
    }

    /// <summary>
    /// Update player movement and camera look.
    /// </summary>
    public void UpdatePlayer()
    {
        if (_config == null || _playerRoot == null) return;

        UpdateLook();
        UpdateMovement();
    }

    /// <summary>
    /// Updates camera rotation based on mouse input.
    /// </summary>
    private void UpdateLook()
    {
        if (_camera == null || _playerRoot == null || _lookAction == null) return;

        Vector2 delta = _lookAction.ReadValue<Vector2>();
        _yaw += delta.x * _config.lookSensitivity;
        _pitch -= delta.y * _config.lookSensitivity;
        _pitch = Mathf.Clamp(_pitch, -85f, 85f);

        _playerRoot.rotation = Quaternion.Euler(0f, _yaw, 0f);
        _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    /// <summary>
    /// Updates player movement based on input.
    /// </summary>
    private void UpdateMovement()
    {
        if (_characterController == null || _camera == null || _moveAction == null || _flyAction == null) return;

        Vector2 mv = _moveAction.ReadValue<Vector2>();
        float fly = _flyAction.ReadValue<float>();

        Vector3 forward = _playerRoot.forward;
        Vector3 right = _playerRoot.right;

        Vector3 dir = (forward * mv.y + right * mv.x);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 up = Vector3.up * fly;

        Vector3 motion = (dir + up).normalized * _config.moveSpeed * Time.deltaTime;
        _characterController.Move(motion);
    }

    /// <summary>
    /// Positions the player at the specified world position.
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        if (_playerRoot == null) return;

        position.y = 1f; // Ensure player is on ground
        _playerRoot.position = position;

        // Reset camera
        _yaw = _playerRoot.eulerAngles.y;
        _pitch = 0f;

        if (_camera != null)
        {
            _camera.transform.localPosition = new Vector3(0f, _config.cameraHeight, 0f);
            _camera.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Positions the player at entrance and faces them toward the target position (other entrance).
    /// </summary>
    public void SetPositionAndRotation(Vector3 entrancePosition, Vector3 targetPosition)
    {
        if (_playerRoot == null)
        {
            Debug.LogError("PlayerController: Player root is null!");
            return;
        }

        // Ensure player is on ground and positioned correctly
        entrancePosition.y = 1f; // Ensure player is on ground
        
        Debug.Log($"PlayerController: Setting position to {entrancePosition}, target is {targetPosition}");
        _playerRoot.position = entrancePosition;

        // Calculate direction from entrance to target (looking into the maze)
        Vector3 direction = (targetPosition - entrancePosition).normalized;
        direction.y = 0f; // Keep horizontal only
        
        if (direction.sqrMagnitude > 0.001f)
        {
            // Set rotation to face the target
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            _playerRoot.rotation = targetRotation;
            _yaw = targetRotation.eulerAngles.y;
        }
        else
        {
            // Fallback: face forward if direction is invalid
            _yaw = 0f;
            _playerRoot.rotation = Quaternion.identity;
        }

        _pitch = 0f;

        if (_camera != null)
        {
            _camera.transform.localPosition = new Vector3(0f, _config.cameraHeight, 0f);
            _camera.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Gets the camera transform for raycasting (e.g., graffiti placement).
    /// </summary>
    public Transform GetCameraTransform()
    {
        return _camera != null ? _camera.transform : null;
    }

    /// <summary>
    /// Locks or unlocks the cursor.
    /// </summary>
    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
