using UnityEngine;
using UnityEngine.InputSystem;

public class TuskMovement : MonoBehaviour
{
    [Header("Ground Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 5f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpHoldTime = 0.25f;

    [Header("Bunny Hop")]
    [SerializeField] private float firstJumpSpeedMultiplier = 1.2f;
    [SerializeField] private float secondJumpSpeedMultiplier = 1.3f;
    [SerializeField] private float maxJumpSpeedMultiplier = 1.4f;
    [SerializeField] private float bunnyHopResetTime = 0.35f;

    [Header("References")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualTransform;
    [SerializeField] private ParticleSystem runningDust;
    [SerializeField] private ParticleSystem jumpDust;
    [SerializeField] private ParticleSystem landingDust;
    [SerializeField] private TuskDash dash;
    [SerializeField] private TuskFlight flight;
    [SerializeField] private TuskAFK afk;
    [SerializeField] private TuskAudio audio;
    [SerializeField] private float modelRotationOffset = 0f;

    public Vector3 MovementDirection { get; private set; }

    private CharacterController controller;
    private ParticleSystem.EmissionModule dustEmission;

    private float verticalVelocity;
    private float coyoteTimer;
    private float jumpHoldTimer;

    private float currentHorizontalSpeed;

    private float bunnyHopTimer;
    private int bunnyHopCount;

    private bool isJumping;
    private bool wasGrounded;
    private bool wasFalling;
    private bool wasRunning;
    private bool wasIdle;
    private bool wasAFK;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (runningDust != null)
        {
            dustEmission = runningDust.emission;
        }

        wasGrounded = controller.isGrounded;
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
    }

    private void Update()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();

        Vector3 movement = new Vector3(input.x, 0f, input.y);
        movement = Vector3.ClampMagnitude(movement, 1f);

        MovementDirection = movement;

        // AFK RESET - MOVEMENT
        if (input.sqrMagnitude > 0.01f)
        {
            afk.ResetAFK();
        }

        // FACE MOVEMENT DIRECTION
        if (movement.sqrMagnitude > 0.01f)
        {
            float angle =
                Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg;

            float snappedAngle =
                Mathf.Round(angle / 45f) * 45f;

            visualTransform.rotation =
                Quaternion.Euler(
                    0f,
                    snappedAngle + modelRotationOffset,
                    0f
                );
        }

        // COYOTE TIME
        if (controller.isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        // BUNNY HOP TIMER
        if (bunnyHopTimer > 0f)
        {
            bunnyHopTimer -= Time.deltaTime;
        }
        else if (controller.isGrounded)
        {
            bunnyHopCount = 0;
        }

        // STOPPING MOVEMENT RESETS BUNNY HOP
        if (input.sqrMagnitude < 0.01f)
        {
            bunnyHopCount = 0;
            bunnyHopTimer = 0f;
        }

        // JUMP
        if (jumpAction.action.WasPressedThisFrame() &&
            coyoteTimer > 0f)
        {
            afk.ResetAFK();

            bunnyHopCount++;

            float jumpSpeedMultiplier;

            if (bunnyHopCount <= 1)
            {
                jumpSpeedMultiplier = firstJumpSpeedMultiplier;
            }
            else if (bunnyHopCount == 2)
            {
                jumpSpeedMultiplier = secondJumpSpeedMultiplier;
            }
            else
            {
                jumpSpeedMultiplier = maxJumpSpeedMultiplier;
            }

            if (input.sqrMagnitude > 0.01f)
            {
                float desiredSpeed =
                    moveSpeed * jumpSpeedMultiplier;

                currentHorizontalSpeed =
                    Mathf.Max(
                        currentHorizontalSpeed,
                        desiredSpeed
                    );
            }

            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);

            jumpHoldTimer = 0f;

            isJumping = true;

            // STOP RUNNING
            if (wasRunning)
            {
                if (audio != null)
                {
                    audio.StopRunning();
                }

                wasRunning = false;
            }

            // STOP IDLE
            if (wasIdle)
            {
                if (audio != null)
                {
                    audio.StopIdle();
                }

                wasIdle = false;
            }

            // STOP AFK
            if (wasAFK)
            {
                if (audio != null)
                {
                    audio.StopAFK();
                }

                wasAFK = false;
            }

            // JUMP SOUND
            if (audio != null)
            {
                audio.PlayJump();
            }

            coyoteTimer = 0f;
            bunnyHopTimer = bunnyHopResetTime;

            // JUMP DUST
            if (jumpDust != null)
            {
                jumpDust.Play();
            }
        }

        // VARIABLE JUMP HEIGHT
        if (isJumping &&
            jumpAction.action.IsPressed() &&
            jumpHoldTimer < jumpHoldTime &&
            verticalVelocity > 0f)
        {
            jumpHoldTimer += Time.deltaTime;

            verticalVelocity +=
                -gravity * Time.deltaTime * 0.5f;
        }

        // RELEASE JUMP EARLY
        if (isJumping &&
            jumpAction.action.WasReleasedThisFrame() &&
            verticalVelocity > 0f)
        {
            verticalVelocity *= 0.45f;
            isJumping = false;
        }

        if (jumpHoldTimer >= jumpHoldTime)
        {
            isJumping = false;
        }

        // HORIZONTAL MOVEMENT
        if (movement.sqrMagnitude > 0.01f)
        {
            float speedLimit =
                moveSpeed + dash.SpeedBonus;

            float targetSpeed =
                Mathf.Max(
                    speedLimit,
                    currentHorizontalSpeed
                );

            currentHorizontalSpeed =
                Mathf.MoveTowards(
                    currentHorizontalSpeed,
                    targetSpeed,
                    acceleration * Time.deltaTime
                );
        }
        else
        {
            currentHorizontalSpeed =
                Mathf.MoveTowards(
                    currentHorizontalSpeed,
                    0f,
                    acceleration * 2f * Time.deltaTime
                );
        }

        // GRAVITY / FLIGHT
        if (flight != null && flight.IsFlying)
        {
            verticalVelocity = flight.GetFlightLift();
            isJumping = false;
        }
        else
        {
            if (controller.isGrounded &&
                verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                isJumping = false;
            }

            verticalVelocity += gravity * Time.deltaTime;
        }

        // FINAL MOVEMENT
        Vector3 finalMovement =
            movement * currentHorizontalSpeed;

        finalMovement.y = verticalVelocity;

        controller.Move(
            finalMovement * Time.deltaTime
        );

        // GROUND CHECK
        bool grounded = controller.isGrounded;

        // FALLING
        bool falling =
            !grounded &&
            !isJumping &&
            verticalVelocity < 0f &&
            (flight == null || !flight.IsFlying);

        if (falling && !wasFalling)
        {
            if (audio != null)
            {
                audio.PlayFalling();
            }
        }

        if (!falling && wasFalling)
        {
            if (audio != null)
            {
                audio.StopFalling();
            }
        }

        wasFalling = falling;

        // RUNNING
        bool running =
            grounded &&
            input.sqrMagnitude > 0.01f &&
            !isJumping &&
            (flight == null || !flight.IsFlying);

        if (running && !wasRunning)
        {
            if (audio != null)
            {
                audio.PlayRunning();
            }
        }

        if (!running && wasRunning)
        {
            if (audio != null)
            {
                audio.StopRunning();
            }
        }

        wasRunning = running;

        // IDLE
        bool idle =
            grounded &&
            input.sqrMagnitude < 0.01f &&
            !isJumping &&
            !falling &&
            (flight == null || !flight.IsFlying) &&
            (afk == null || !afk.IsAFK);

        if (idle && !wasIdle)
        {
            if (audio != null)
            {
                audio.PlayIdle();
            }
        }

        if (!idle && wasIdle)
        {
            if (audio != null)
            {
                audio.StopIdle();
            }
        }

        wasIdle = idle;

        // AFK
        bool isAFK =
            afk != null &&
            afk.IsAFK;

        if (isAFK && !wasAFK)
        {
            if (audio != null)
            {
                audio.PlayAFK();
            }
        }

        if (!isAFK && wasAFK)
        {
            if (audio != null)
            {
                audio.StopAFK();
            }
        }

        wasAFK = isAFK;

        // LANDING DUST + SOUND
        if (!wasGrounded && grounded)
        {
            if (landingDust != null)
            {
                landingDust.Play();
            }

            if (audio != null)
            {
                audio.PlayLanding();
            }
        }

        wasGrounded = grounded;

        // ANIMATOR
        animator.SetFloat(
            "Speed",
            input.magnitude
        );

        animator.SetBool(
            "IsGrounded",
            grounded
        );

        animator.SetBool(
            "IsJumping",
            isJumping
        );

        animator.SetBool(
            "IsFlying",
            flight != null && flight.IsFlying
        );

        animator.SetBool(
            "IsAFK",
            afk != null && afk.IsAFK
        );

        // RUNNING DUST
        if (runningDust != null)
        {
            dustEmission.enabled =
                grounded &&
                input.sqrMagnitude > 0.01f;
        }
    }
}