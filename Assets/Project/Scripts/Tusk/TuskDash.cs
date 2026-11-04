using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// TuskDash  (Scripts/Tusk)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   A short burst of speed in the direction Tusk faces. Costs steam, has a
//   cooldown, and allows one dash per time in the air. After a dash Tusk is
//   'excited' for a moment and runs a little faster.
//   This script only decides WHEN a dash happens and its speed/direction;
//   TuskMovement actually moves Tusk using DashDirection and DashSpeed.
//
// PUT IT ON   Tusk_Player.
// OWNS INPUT  Dash action.
// NEEDS       Optional SteamTank slot (empty = free dashes).
// TOLD BY     TuskMovement, every frame: whether Tusk is grounded, and whether
//             she is locked (hit or recharging). A lock cancels the dash.
// =============================================================================
[DefaultExecutionOrder(-100)]
public class TuskDash : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference dashAction;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.30f;
    [SerializeField] private float dashCooldown = 0.7f;

    [Header("Steam Cost")]
    [Tooltip("Leave empty for free dashes.")]
    [SerializeField] private SteamTank steamTank;
    [Tooltip("Steam spent on every dash. 0 = free.")]
    [SerializeField] private float dashCost = 15f;

    [Header("Momentum")]
    [SerializeField] private float excitementSpeedBonus = 0.5f;
    [SerializeField] private float excitementDuration = 2f;

    [Header("References")]
    [SerializeField] private Transform visualTransform;
    [SerializeField] private TuskAudio tuskAudio;

    private float dashTimer;
    private float cooldownTimer;
    private float excitementTimer;

    private bool hasAirDashed;
    private bool isGrounded;
    private bool isControlLocked;

    public float SpeedBonus { get; private set; }
    public bool IsDashing { get; private set; }
    public Vector3 DashDirection { get; private set; }
    public float DashSpeed => dashSpeed;

    private void OnEnable()
    {
        if (dashAction != null && dashAction.action != null)
            dashAction.action.Enable();
    }

    private void OnDisable()
    {
        if (dashAction != null && dashAction.action != null)
            dashAction.action.Disable();

        IsDashing = false;
        SpeedBonus = 0f;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (cooldownTimer > 0f)
            cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);

        if (excitementTimer > 0f)
            excitementTimer = Mathf.Max(0f, excitementTimer - deltaTime);

        SpeedBonus = excitementTimer > 0f
            ? excitementSpeedBonus
            : 0f;

        if (IsDashing)
        {
            dashTimer -= deltaTime;

            if (dashTimer <= 0f)
                EndDash();
        }

        // Hit, or shaking the backpack: no dashing.
        if (isControlLocked)
        {
            if (IsDashing)
                EndDash();

            return;
        }

        if (dashAction == null || dashAction.action == null)
            return;

        if (!dashAction.action.WasPressedThisFrame())
            return;

        if (cooldownTimer > 0f || IsDashing)
            return;

        // Only one air dash per time in the air.
        if (!isGrounded && hasAirDashed)
            return;

        // Not enough steam: no dash (and nothing is spent).
        if (steamTank != null && !steamTank.TrySpend(dashCost))
            return;

        StartDash();
    }

    private void StartDash()
    {
        Vector3 forward = visualTransform != null
            ? visualTransform.forward
            : transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        DashDirection = forward.normalized;

        // Only an airborne dash uses up the air-dash allowance.
        if (!isGrounded)
            hasAirDashed = true;

        IsDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;
        excitementTimer = excitementDuration;

        if (tuskAudio != null)
            tuskAudio.PlayDash();
    }

    private void EndDash()
    {
        IsDashing = false;
    }

    // Called by TuskMovement after its movement step every frame.
    // (The Animator's IsDashing flag is also set by TuskMovement.)
    public void NotifyGrounded(bool grounded)
    {
        isGrounded = grounded;

        if (grounded)
            hasAirDashed = false;
    }

    // Called by TuskMovement every frame: true while hit or recharging.
    public void NotifyControlLocked(bool locked)
    {
        isControlLocked = locked;
    }
}
