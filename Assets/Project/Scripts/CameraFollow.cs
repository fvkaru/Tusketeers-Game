
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float targetHeight = 1.2f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference zoomAction;

    [Header("Orbit")]
    [SerializeField] private float mouseSensitivity = 0.22f;
    [SerializeField] private float horizontalSpeedLimit = 340f;
    [SerializeField] private float verticalSpeedLimit = 240f;
    [SerializeField] private float minimumPitch = -10f;
    [SerializeField] private float maximumPitch = 60f;

    [Header("Follow")]
    [SerializeField] private float followSmoothness = 15f;

    [Header("Zoom")]
    [SerializeField] private float distance = 12f;
    [SerializeField] private float minimumDistance = 8f;
    [SerializeField] private float maximumDistance = 16f;
    [SerializeField] private float zoomSensitivity = 60f;
    [SerializeField] private float zoomSmoothness = 8f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionPadding = 0.15f;
    [SerializeField] private float collisionSmoothness = 20f;

    private float yaw;
    private float pitch;
    private float targetDistance;
    private float safeDistance;

    private Vector3 smoothedPivot;
    private Vector3 pivotVelocity;

    private bool cursorLocked = true;
    private bool initialized;

    private void Awake()
    {
        distance = Mathf.Clamp(
            distance,
            minimumDistance,
            maximumDistance
        );

        targetDistance = distance;
        safeDistance = distance;
    }

    private void OnEnable()
    {
        if (lookAction != null)
            lookAction.action.Enable();

        if (zoomAction != null)
            zoomAction.action.Enable();
    }

    private void OnDisable()
    {
        if (lookAction != null)
            lookAction.action.Disable();

        if (zoomAction != null)
            zoomAction.action.Disable();
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError(
                "CameraFollow: Target is not assigned.",
                this
            );

            enabled = false;
            return;
        }

        if (lookAction == null || zoomAction == null)
        {
            Debug.LogError(
                "CameraFollow: Assign the Look and Zoom actions " +
                "in the Inspector.",
                this
            );

            enabled = false;
            return;
        }

        Vector3 offset = transform.position - GetPivotPosition();

        if (offset.sqrMagnitude < 0.01f)
        {
            offset = new Vector3(0f, 4f, -12f);
        }

        distance = Mathf.Clamp(
            offset.magnitude,
            minimumDistance,
            maximumDistance
        );

        targetDistance = distance;
        safeDistance = distance;

        yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;

        pitch = Mathf.Asin(
            Mathf.Clamp(offset.y / offset.magnitude, -1f, 1f)
        ) * Mathf.Rad2Deg;

        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);

        smoothedPivot = GetPivotPosition();

        SetCursorLock(true);
        initialized = true;
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }

        if (!cursorLocked &&
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            SetCursorLock(true);
        }

        if (!cursorLocked)
            return;

        Vector2 mouseDelta =
            lookAction.action.ReadValue<Vector2>();

        float maxYawStep =
            horizontalSpeedLimit * Time.unscaledDeltaTime;

        float maxPitchStep =
            verticalSpeedLimit * Time.unscaledDeltaTime;

        yaw += Mathf.Clamp(
            mouseDelta.x * mouseSensitivity,
            -maxYawStep,
            maxYawStep
        );

        pitch -= Mathf.Clamp(
            mouseDelta.y * mouseSensitivity,
            -maxPitchStep,
            maxPitchStep
        );

        pitch = Mathf.Clamp(
            pitch,
            minimumPitch,
            maximumPitch
        );

        float scroll =
            zoomAction.action.ReadValue<float>();

        targetDistance -= scroll * zoomSensitivity * 0.01f;

        targetDistance = Mathf.Clamp(
            targetDistance,
            minimumDistance,
            maximumDistance
        );
    }

    private void LateUpdate()
    {
        if (!initialized || target == null)
            return;

        float deltaTime = Time.deltaTime;

        smoothedPivot = Vector3.SmoothDamp(
            smoothedPivot,
            GetPivotPosition(),
            ref pivotVelocity,
            1f / Mathf.Max(followSmoothness, 0.01f),
            Mathf.Infinity,
            deltaTime
        );

        distance = Mathf.Lerp(
            distance,
            targetDistance,
            1f - Mathf.Exp(-zoomSmoothness * deltaTime)
        );

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = orbit * Vector3.back;

        float desiredSafeDistance = distance;

        if (Physics.SphereCast(
            smoothedPivot,
            collisionRadius,
            direction,
            out RaycastHit hit,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            desiredSafeDistance = Mathf.Max(
                0.1f,
                hit.distance - collisionPadding
            );
        }

        if (desiredSafeDistance < safeDistance)
        {
            safeDistance = desiredSafeDistance;
        }
        else
        {
            safeDistance = Mathf.Lerp(
                safeDistance,
                desiredSafeDistance,
                1f - Mathf.Exp(
                    -collisionSmoothness * deltaTime
                )
            );
        }

        transform.position =
            smoothedPivot + direction * safeDistance;

        transform.rotation = Quaternion.LookRotation(
            smoothedPivot - transform.position,
            Vector3.up
        );
    }

    private Vector3 GetPivotPosition()
    {
        return target.position + Vector3.up * targetHeight;
    }

    private void SetCursorLock(bool locked)
    {
        cursorLocked = locked;

        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }
}
