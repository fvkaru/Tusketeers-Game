using UnityEngine;

// =============================================================================
// CameraFollow  (Scripts/Camera)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Fixed-angle follow camera. It uses NO player input, so it works the same
//   with keyboard and on a phone. It follows Tusk loosely and leans a little
//   toward the arena center. When Tusk falls below the platform, it aims up
//   toward the platform and zooms out so the player can see where to fly.
//   It also pulls closer if something blocks the view, and has a Shake().
//   For the game intro, GameFlow calls BeginIntro() / EndIntro(): the camera
//   starts higher and farther away, follows Tusk down, then glides to the normal view.
//
// PUT IT ON   The Main Camera.
// SLOTS       Target = Tusk_Player.
//             Arena Center = an empty object at the middle of the arena's TOP
//             surface. It must stay at the scene root, NOT inside Tusk.
// OTHERS CALL Shake(strength, duration), for example on a big hit.
// TIP         In Collision Layers, untick Tusk's layer.
// =============================================================================
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float targetHeight = 1.2f;

    [Header("Fixed View")]
    [Tooltip("Which side the camera looks from, in degrees. " +
             "0 = camera behind the arena looking toward +Z.")]
    [SerializeField] private float yaw = 0f;
    [Tooltip("Camera tilt. 0 = level with Tusk, 90 = straight down.")]
    [Range(10f, 80f)]
    [SerializeField] private float pitch = 50f;
    [SerializeField] private float distance = 18f;

    [Header("Follow")]
    [Tooltip("Seconds the camera takes to catch up. Higher = floatier.")]
    [SerializeField] private float followSmoothTime = 0.2f;

    [Header("Arena (optional)")]
    [Tooltip("Empty object at the CENTER of the arena, at the height of the " +
             "platform's top surface. Leave empty to turn off the " +
             "arena features below.")]
    [SerializeField] private Transform arenaCenter;
    [Tooltip("How much the camera leans toward the arena center instead of " +
             "staying exactly on Tusk. 0 = off.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float centerBias = 0.2f;
    [SerializeField] private float maxCenterShift = 4f;

    [Header("Falling")]
    [Tooltip("When Tusk is below the platform, the camera aims this fraction " +
             "of the way up toward the platform. 0 = off.")]
    [Range(0f, 1f)]
    [SerializeField] private float fallFocusRaise = 0.5f;
    [SerializeField] private float maxFallFocusRaise = 8f;
    [Tooltip("Extra distance (zoom out) while Tusk is far below the platform.")]
    [SerializeField] private float fallZoomOut = 6f;
    [Tooltip("How quickly the falling camera eases in and out.")]
    [SerializeField] private float fallSmoothness = 3f;

    [Header("Collision")]
    [Tooltip("Untick Tusk's layer here so the camera does not hit her.")]
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionPadding = 0.15f;
    [SerializeField] private float collisionSmoothness = 20f;

    [Header("Intro View (used by GameFlow)")]
    [Tooltip("Camera tilt while Tusk flies down. Low = from the side, high = from above (60 was the old, very high view).")]
    [Range(0f, 80f)]
    [SerializeField] private float introPitch = 35f;
    [Tooltip("How far the camera is from Tusk during the intro, in meters (36 was the old distance).")]
    [SerializeField] private float introDistance = 36f;

    // How far below the platform counts as "falling".
    private const float FallStartDepth = 1f;
    private const float FallFullDepth = 6f;

    private Vector3 smoothedPivot;
    private Vector3 pivotVelocity;

    private float fallAmount;
    private float currentDistance;
    private float safeDistance;

    private float shakeStrength;
    private float shakeDuration;
    private float shakeTimer;

    private bool initialized;

    // 1 = full intro view, 0 = normal view.
    private float introAmount;
    private float introTarget;
    private float introBlendTime = 1.5f;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("CameraFollow: Target is not assigned.", this);
            enabled = false;
            return;
        }

        // Start exactly in place, with no swooping in from somewhere else.
        smoothedPivot = GetDesiredPivot();
        currentDistance = distance;
        safeDistance = distance;

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized || target == null)
            return;

        float deltaTime = Time.deltaTime;

        UpdateFallAmount(deltaTime);

        introAmount = Mathf.MoveTowards(
            introAmount, introTarget, deltaTime / introBlendTime);

        float introBlend = Mathf.SmoothStep(0f, 1f, introAmount);

        smoothedPivot = Vector3.SmoothDamp(
            smoothedPivot,
            GetDesiredPivot(),
            ref pivotVelocity,
            Mathf.Max(followSmoothTime, 0.01f),
            Mathf.Infinity,
            deltaTime
        );

        // Zoom out a bit while falling.
        float wantedDistance = Mathf.Lerp(
            distance + fallZoomOut * fallAmount,
            introDistance,
            introBlend
        );

        currentDistance = Mathf.Lerp(
            currentDistance,
            wantedDistance,
            1f - Mathf.Exp(-fallSmoothness * deltaTime)
        );

        Quaternion orbit = Quaternion.Euler(
            Mathf.Lerp(pitch, introPitch, introBlend), yaw, 0f);
        Vector3 direction = orbit * Vector3.back;

        UpdateSafeDistance(direction, deltaTime);

        Vector3 position = smoothedPivot + direction * safeDistance;

        transform.position = position + GetShakeOffset(deltaTime);

        transform.rotation = Quaternion.LookRotation(
            smoothedPivot - position,
            Vector3.up
        );
    }

    // ------------------------------------------------------------------
    // Public
    // ------------------------------------------------------------------

    /// <summary>
    /// Shakes the camera. Call it from other scripts, for example when a
    /// big hit lands: cameraFollow.Shake(0.4f, 0.25f);
    /// </summary>
    public void Shake(float strength, float duration)
    {
        shakeStrength = Mathf.Max(shakeStrength * ShakeFade(), strength);
        shakeDuration = Mathf.Max(duration, 0.01f);
        shakeTimer = shakeDuration;
    }

    /// <summary>
    /// Jumps to the high intro view at once (no swooping). Used by GameFlow.
    /// </summary>
    public void BeginIntro()
    {
        introTarget = 1f;
        introAmount = 1f;

        Snap();
    }

    /// <summary>Glides from the intro view to the normal view.</summary>
    public void EndIntro(float blendTime)
    {
        introBlendTime = Mathf.Max(blendTime, 0.01f);
        introTarget = 0f;
    }

    /// <summary>Puts the camera exactly where it should be, with no smoothing.</summary>
    public void Snap()
    {
        if (!initialized || target == null)
            return;

        smoothedPivot = GetDesiredPivot();
        pivotVelocity = Vector3.zero;
        fallAmount = 0f;

        currentDistance = Mathf.Lerp(
            distance,
            introDistance,
            Mathf.SmoothStep(0f, 1f, introAmount)
        );

        safeDistance = currentDistance;
    }

    // ------------------------------------------------------------------
    // Pivot / falling
    // ------------------------------------------------------------------

    // The point the camera orbits around and looks at: Tusk, leaning a little
    // toward the arena center.
    private Vector3 GetDesiredPivot()
    {
        Vector3 pivot = target.position + Vector3.up * targetHeight;

        if (arenaCenter == null)
            return pivot;

        // Lean slightly toward the arena center.
        if (centerBias > 0f)
        {
            Vector3 toCenter = arenaCenter.position - pivot;
            toCenter.y = 0f;

            pivot += Vector3.ClampMagnitude(
                toCenter * centerBias,
                maxCenterShift
            );
        }

        // While falling, aim part of the way up toward the platform
        // so the player can see where to fly.
        if (fallFocusRaise > 0f && fallAmount > 0f)
        {
            float depth = arenaCenter.position.y - target.position.y;

            float raise = Mathf.Min(
                Mathf.Max(depth, 0f) * fallFocusRaise,
                maxFallFocusRaise
            );

            pivot += Vector3.up * raise * fallAmount;
        }

        return pivot;
    }

    // 0 = Tusk is on or above the platform, 1 = far below it.
    private void UpdateFallAmount(float deltaTime)
    {
        float wanted = 0f;

        if (arenaCenter != null)
        {
            float depth = arenaCenter.position.y - target.position.y;

            wanted = Mathf.InverseLerp(
                FallStartDepth,
                FallFullDepth,
                depth
            );
        }

        fallAmount = Mathf.Lerp(
            fallAmount,
            wanted,
            1f - Mathf.Exp(-fallSmoothness * deltaTime)
        );
    }

    // ------------------------------------------------------------------
    // Collision
    // ------------------------------------------------------------------

    // Pulls the camera closer if something is between it and the pivot.
    // Moves in quickly, moves back out smoothly.
    private void UpdateSafeDistance(Vector3 direction, float deltaTime)
    {
        float desiredSafeDistance = currentDistance;

        if (Physics.SphereCast(
            smoothedPivot,
            collisionRadius,
            direction,
            out RaycastHit hit,
            currentDistance,
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
                1f - Mathf.Exp(-collisionSmoothness * deltaTime)
            );
        }
    }

    // ------------------------------------------------------------------
    // Shake
    // ------------------------------------------------------------------

    private float ShakeFade()
    {
        return shakeTimer > 0f ? shakeTimer / shakeDuration : 0f;
    }

    private Vector3 GetShakeOffset(float deltaTime)
    {
        if (shakeTimer <= 0f)
            return Vector3.zero;

        float strength = shakeStrength * ShakeFade();

        shakeTimer -= deltaTime;

        return Random.insideUnitSphere * strength;
    }
}
