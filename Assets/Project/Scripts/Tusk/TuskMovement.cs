using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// =============================================================================
// TuskMovement  (Scripts/Tusk)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Moves Tusk: running relative to the camera (facing snaps to 8 directions),
//   jumping (coyote time, hold for height, bunny hop), gravity, and the push
//   from hits. It is the 'brain' of the player: it also drives the Animator,
//   the sounds and the dust effects.
//
// PUT IT ON   Tusk_Player (needs a CharacterController; the other Tusk scripts
//             and SteamTank / SteamKnockback must be on the same object).
// OWNS INPUT  Move and Jump actions (nobody else enables or disables them).
// READS       TuskDash, TuskFlight (speed bonus, lift), SteamKnockback (push),
//             TuskRecharge and TuskAttack (they lock control while active).
// OTHERS READ IsGrounded, IsStunned, IsControlLocked.
// ANIMATOR    Sets: Speed (float), IsGrounded, IsJumping, IsFlying, IsDashing,
//             and, if they exist in your Animator, IsHurt, IsRecharging and
//             IsAttacking.
// =============================================================================
[RequireComponent(typeof(CharacterController))]
public class TuskMovement : MonoBehaviour
{
    [Header("Ground Movement")]
    [SerializeField] private float moveSpeed = 7.5f;
    [SerializeField] private float acceleration = 35f;
    [SerializeField] private float groundDeceleration = 22f;
    [SerializeField] private float airDeceleration = 20f;
    [SerializeField] private float groundTurnSpeed = 16f;
    [SerializeField] private float airTurnSpeed = 16f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpHoldTime = 0.25f;

    [Header("Bunny Hop")]
    [SerializeField] private float firstJumpSpeedMultiplier = 1.25f;
    [SerializeField] private float secondJumpSpeedMultiplier = 1.45f;
    [SerializeField] private float maxJumpSpeedMultiplier = 1.65f;
    [SerializeField] private float bunnyHopResetTime = 0.5f;

    [Header("References")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualTransform;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private ParticleSystem runningDust;
    [SerializeField] private ParticleSystem jumpDust;
    [SerializeField] private ParticleSystem landingDust;
    [SerializeField] private TuskDash dash;
    [SerializeField] private TuskFlight flight;

    // Renamed from "audio". FormerlySerializedAs keeps your Inspector link.
    [FormerlySerializedAs("audio")]
    [SerializeField] private TuskAudio tuskAudio;

    [SerializeField] private float modelRotationOffset = 0f;

    public Vector3 MovementDirection { get; private set; }

    /// <summary>
    /// Ground state from the main movement step. Other scripts should read
    /// this instead of controller.isGrounded.
    /// </summary>
    public bool IsGrounded { get; private set; }

    /// <summary>True while recovering from a hit.</summary>
    public bool IsStunned => knockback != null && knockback.IsStunned;

    /// <summary>True during the attack swing.</summary>
    public bool IsAttacking => attack != null && attack.IsAttacking;

    /// <summary>
    /// True while hit, shaking the backpack or swinging:
    /// no moving, jumping or dashing, and no new attack.
    /// </summary>
    public bool IsControlLocked =>
        IsStunned ||
        IsAttacking ||
        (recharge != null && recharge.IsRecharging);

    // Animator parameter IDs (faster than strings, and typo-proof).
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsFlyingHash = Animator.StringToHash("IsFlying");
    private static readonly int IsDashingHash = Animator.StringToHash("IsDashing");

    // Optional Animator parameters: only used if your Animator has them.
    private static readonly int IsHurtHash = Animator.StringToHash("IsHurt");
    private static readonly int IsRechargingHash = Animator.StringToHash("IsRecharging");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private bool animatorHasHurt;
    private bool animatorHasRecharging;
    private bool animatorHasAttacking;

    private CharacterController controller;
    private ParticleSystem.EmissionModule dustEmission;
    private SteamKnockback knockback;
    private TuskRecharge recharge;
    private TuskAttack attack;

    private Vector3 horizontalVelocity;

    private float verticalVelocity;
    private float coyoteTimer;
    private float jumpHoldTimer;
    private float bunnyHopTimer;

    private int bunnyHopCount;

    private bool isJumping;
    private bool wasGrounded;
    private bool wasFalling;
    private bool wasRunning;
    private bool wasIdle;
    private bool wasFlying;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        knockback = GetComponent<SteamKnockback>();
        recharge = GetComponent<TuskRecharge>();
        attack = GetComponent<TuskAttack>();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animatorHasHurt = HasAnimatorParameter(IsHurtHash);
            animatorHasRecharging = HasAnimatorParameter(IsRechargingHash);
            animatorHasAttacking = HasAnimatorParameter(IsAttackingHash);
        }

        if (runningDust != null)
            dustEmission = runningDust.emission;

        IsGrounded = controller.isGrounded;
        wasGrounded = IsGrounded;
    }

    private bool HasAnimatorParameter(int hash)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash)
                return true;
        }

        return false;
    }

    private void OnEnable()
    {
        // TuskMovement "owns" the move and jump actions.
        // Other scripts should not enable/disable them.
        if (moveAction != null && moveAction.action != null)
            moveAction.action.Enable();

        if (jumpAction != null && jumpAction.action != null)
            jumpAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null && moveAction.action != null)
            moveAction.action.Disable();

        if (jumpAction != null && jumpAction.action != null)
            jumpAction.action.Disable();
    }

    // What happens every frame, in this order:
    //   1. Read input (it is zeroed while Tusk is hit or recharging).
    //   2. Face the direction, update jump timers, start a jump if pressed.
    //   3. Work out horizontal speed (her own movement) and vertical speed
    //      (gravity, flight lift, or the little hop from a hit).
    //   4. Add the hit's push on top, then move the CharacterController.
    //   5. Remember the ground state, then update sounds, Animator and dust.
    private void Update()
    {
        if (moveAction == null || moveAction.action == null ||
            jumpAction == null || jumpAction.action == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        bool controlLocked = IsControlLocked;

        // While hit or recharging, the player cannot steer.
        Vector2 input = controlLocked
            ? Vector2.zero
            : Vector2.ClampMagnitude(
                moveAction.action.ReadValue<Vector2>(),
                1f
            );

        bool hasInput = input.sqrMagnitude > 0.01f;

        Vector3 moveDirection = GetCameraRelativeDirection(input);
        MovementDirection = moveDirection;

        bool hasMoveDirection = moveDirection.sqrMagnitude > 0.01f;

        FaceMovementDirection(moveDirection, hasMoveDirection);
        UpdateJumpTimers(deltaTime);

        if (jumpAction.action.WasPressedThisFrame() &&
            !controlLocked &&
            coyoteTimer > 0f)
        {
            StartJump(moveDirection, hasMoveDirection);
        }

        UpdateVariableJump(deltaTime);

        // While hit or recharging, Tusk does not move by herself.
        // Only the hit's push moves her.
        if (controlLocked)
            horizontalVelocity = Vector3.zero;
        else
            UpdateHorizontalVelocity(moveDirection, hasMoveDirection, deltaTime);

        UpdateVerticalVelocity(deltaTime);

        Vector3 finalMovement = horizontalVelocity;

        // Hits push Tusk on top of her own movement.
        if (knockback != null)
            finalMovement += knockback.PushVelocity;

        finalMovement.y = verticalVelocity;

        controller.Move(finalMovement * deltaTime);

        IsGrounded = controller.isGrounded;

        if (dash != null)
        {
            dash.NotifyGrounded(IsGrounded);
            dash.NotifyControlLocked(controlLocked);
        }

        UpdateStateEffects(hasInput);
        UpdateAnimator(input.magnitude);
        UpdateRunningDust(hasInput);
    }

    // ------------------------------------------------------------------
    // Used by GameFlow (intro and respawn)
    // ------------------------------------------------------------------

    /// <summary>
    /// Clears all movement memory: speed, jumps, bunny hops, effects.
    /// Used when Tusk is placed back on the ground.
    /// </summary>
    public void ResetState()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;

        coyoteTimer = 0f;
        bunnyHopTimer = 0f;
        bunnyHopCount = 0;
        isJumping = false;

        MovementDirection = Vector3.zero;

        // She starts on the ground, so no landing effect plays again.
        IsGrounded = true;
        wasGrounded = true;
        wasFalling = false;
        wasRunning = false;
        wasIdle = false;
        wasFlying = false;

        if (knockback != null)
            knockback.ClearPush();
    }

    /// <summary>
    /// Turns the model to face a world direction (same angles as running:
    /// 0 = toward +Z, 90 = toward +X, 180 = toward -Z).
    /// </summary>
    public void FaceWorldAngle(float degrees)
    {
        if (visualTransform != null)
        {
            visualTransform.rotation =
                Quaternion.Euler(0f, degrees + modelRotationOffset, 0f);
        }
    }

    /// <summary>Plays the landing sound and dust (used by the intro).</summary>
    public void PlayLandingEffects()
    {
        if (tuskAudio != null)
            tuskAudio.PlayLanding();

        if (landingDust != null)
            landingDust.Play();
    }

    // ------------------------------------------------------------------
    // Input / facing
    // ------------------------------------------------------------------

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Transform cam = cameraTransform != null
            ? cameraTransform
            : (Camera.main != null ? Camera.main.transform : null);

        Vector3 forward = cam != null ? cam.forward : Vector3.forward;
        Vector3 right = cam != null ? cam.right : Vector3.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        return Vector3.ClampMagnitude(
            right * input.x + forward * input.y,
            1f
        );
    }

    // Face the movement direction, snapped to 8 directions.
    private void FaceMovementDirection(Vector3 moveDirection, bool hasMoveDirection)
    {
        if (!hasMoveDirection || visualTransform == null)
            return;

        float angle =
            Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;

        float snappedAngle = Mathf.Round(angle / 45f) * 45f;

        visualTransform.rotation = Quaternion.Euler(
            0f,
            snappedAngle + modelRotationOffset,
            0f
        );
    }

    // ------------------------------------------------------------------
    // Jumping
    // ------------------------------------------------------------------

    private void UpdateJumpTimers(float deltaTime)
    {
        // Coyote time: a short grace period after leaving the ground.
        if (IsGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= deltaTime;

        // Bunny-hop window.
        if (bunnyHopTimer > 0f)
            bunnyHopTimer -= deltaTime;
        else if (IsGrounded)
            bunnyHopCount = 0;
    }

    private void StartJump(Vector3 moveDirection, bool hasMoveDirection)
    {
        bunnyHopCount++;

        float jumpSpeedMultiplier;

        if (bunnyHopCount == 1)
            jumpSpeedMultiplier = firstJumpSpeedMultiplier;
        else if (bunnyHopCount == 2)
            jumpSpeedMultiplier = secondJumpSpeedMultiplier;
        else
            jumpSpeedMultiplier = maxJumpSpeedMultiplier;

        // Boost existing momentum instead of replacing it.
        if (hasMoveDirection)
        {
            if (horizontalVelocity.sqrMagnitude < 0.01f)
                horizontalVelocity = moveDirection * moveSpeed;

            float desiredHopSpeed = moveSpeed * jumpSpeedMultiplier;

            float newSpeed = Mathf.Max(
                horizontalVelocity.magnitude,
                desiredHopSpeed
            );

            horizontalVelocity = horizontalVelocity.normalized * newSpeed;
        }

        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        jumpHoldTimer = 0f;
        isJumping = true;
        coyoteTimer = 0f;
        bunnyHopTimer = bunnyHopResetTime;

        if (tuskAudio != null)
            tuskAudio.PlayJump();

        if (jumpDust != null)
            jumpDust.Play();
    }

    private void UpdateVariableJump(float deltaTime)
    {
        // Holding jump: rise a little higher.
        if (isJumping &&
            jumpAction.action.IsPressed() &&
            jumpHoldTimer < jumpHoldTime &&
            verticalVelocity > 0f)
        {
            jumpHoldTimer += deltaTime;
            verticalVelocity += -gravity * deltaTime * 0.5f;
        }

        // Releasing jump early: shorter jump.
        if (isJumping &&
            jumpAction.action.WasReleasedThisFrame() &&
            verticalVelocity > 0f)
        {
            verticalVelocity *= 0.45f;
            isJumping = false;
        }

        if (jumpHoldTimer >= jumpHoldTime)
            isJumping = false;
    }

    // ------------------------------------------------------------------
    // Velocity
    // ------------------------------------------------------------------

    private void UpdateHorizontalVelocity(
        Vector3 moveDirection,
        bool hasMoveDirection,
        float deltaTime)
    {
        float speedBonus = 0f;

        if (dash != null)
            speedBonus += dash.SpeedBonus;

        if (flight != null)
            speedBonus += flight.HorizontalSpeedBonus;

        float targetSpeed = moveSpeed + speedBonus;

        // During a dash, Tusk uses the dash's own speed and direction.
        if (dash != null && dash.IsDashing)
        {
            horizontalVelocity = dash.DashDirection * dash.DashSpeed;
            return;
        }

        if (hasMoveDirection)
        {
            float currentSpeed = horizontalVelocity.magnitude;

            // Accelerate toward target speed, or brake excess speed.
            float speedChange = currentSpeed < targetSpeed
                ? acceleration
                : (IsGrounded ? groundDeceleration : airDeceleration);

            float newSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                speedChange * deltaTime
            );

            Vector3 currentDirection =
                horizontalVelocity.sqrMagnitude > 0.01f
                    ? horizontalVelocity.normalized
                    : moveDirection;

            float turnSpeed = IsGrounded ? groundTurnSpeed : airTurnSpeed;

            Vector3 newDirection = Vector3.RotateTowards(
                currentDirection,
                moveDirection,
                turnSpeed * deltaTime,
                0f
            );

            horizontalVelocity = newDirection.normalized * newSpeed;
        }
        else
        {
            // No input: brake instead of coasting forever.
            float deceleration = IsGrounded
                ? groundDeceleration
                : airDeceleration;

            float newSpeed = Mathf.MoveTowards(
                horizontalVelocity.magnitude,
                0f,
                deceleration * deltaTime
            );

            if (newSpeed > 0.01f && horizontalVelocity.sqrMagnitude > 0.0001f)
                horizontalVelocity = horizontalVelocity.normalized * newSpeed;
            else
                horizontalVelocity = Vector3.zero;
        }
    }

    private void UpdateVerticalVelocity(float deltaTime)
    {
        bool isFlying = flight != null && flight.IsFlying && !IsStunned;

        if (isFlying)
        {
            verticalVelocity = flight.GetFlightLift();
            isJumping = false;
        }
        else
        {
            // Flight just ended: drop the leftover lift so there is no pop upward.
            if (wasFlying)
                verticalVelocity = 0f;

            // Hits pop Tusk slightly into the air.
            if (knockback != null)
            {
                float upward = knockback.TakeUpwardImpulse();

                if (upward > 0f)
                {
                    verticalVelocity = Mathf.Max(verticalVelocity, upward);
                    isJumping = false;
                }
            }

            if (IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                isJumping = false;
            }

            verticalVelocity += gravity * deltaTime;
        }

        wasFlying = isFlying;
    }

    // ------------------------------------------------------------------
    // Sounds, dust, animation
    // ------------------------------------------------------------------

    private void UpdateStateEffects(bool hasInput)
    {
        bool grounded = IsGrounded;
        bool isFlying = flight != null && flight.IsFlying;

        bool falling =
            !grounded &&
            !isJumping &&
            verticalVelocity < 0f &&
            !isFlying;

        bool running =
            grounded &&
            hasInput &&
            !isJumping &&
            !isFlying;

        bool idle =
            grounded &&
            !IsControlLocked &&
            !hasInput &&
            !isJumping &&
            !falling &&
            !isFlying;

        bool justLanded = !wasGrounded && grounded;

        if (tuskAudio != null)
        {
            if (falling && !wasFalling) tuskAudio.PlayFalling();
            if (!falling && wasFalling) tuskAudio.StopFalling();

            if (running && !wasRunning) tuskAudio.PlayRunning();
            if (!running && wasRunning) tuskAudio.StopRunning();

            if (idle && !wasIdle) tuskAudio.PlayIdle();
            if (!idle && wasIdle) tuskAudio.StopIdle();

            if (justLanded) tuskAudio.PlayLanding();
        }

        if (justLanded && landingDust != null)
            landingDust.Play();

        wasFalling = falling;
        wasRunning = running;
        wasIdle = idle;
        wasGrounded = grounded;
    }

    private void UpdateAnimator(float inputMagnitude)
    {
        if (animator == null)
            return;

        animator.SetFloat(SpeedHash, inputMagnitude);
        animator.SetBool(IsGroundedHash, IsGrounded);
        animator.SetBool(IsJumpingHash, isJumping);
        animator.SetBool(IsFlyingHash, flight != null && flight.IsFlying);
        animator.SetBool(IsDashingHash, dash != null && dash.IsDashing);

        if (animatorHasHurt)
            animator.SetBool(IsHurtHash, IsStunned);

        if (animatorHasRecharging)
            animator.SetBool(IsRechargingHash, recharge != null && recharge.IsRecharging);

        if (animatorHasAttacking)
            animator.SetBool(IsAttackingHash, IsAttacking);
    }

    private void UpdateRunningDust(bool hasInput)
    {
        if (runningDust == null)
            return;

        dustEmission.enabled = IsGrounded && hasInput;
    }
}
