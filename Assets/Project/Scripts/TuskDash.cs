using UnityEngine;
using UnityEngine.InputSystem;

public class TuskDash : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference dashAction;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.15f;

    [Header("Excitement Time")]
    [SerializeField] private float excitementSpeedBonus = 0.4f;
    [SerializeField] private float excitementDuration = 2f;

    [Header("References")]
    [SerializeField] private Transform visualTransform;
    [SerializeField] private Animator animator;
    [SerializeField] private TuskAFK afk;

    public float SpeedBonus { get; private set; }

    private CharacterController controller;

    private bool isDashing;
    private bool hasAirDashed;

    private float dashTimer;
    private float cooldownTimer;
    private float excitementTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        dashAction.action.Enable();
    }

    private void OnDisable()
    {
        dashAction.action.Disable();
    }

    private void Update()
    {
        if (controller.isGrounded)
        {
            hasAirDashed = false;
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (excitementTimer > 0f)
        {
            excitementTimer -= Time.deltaTime;
            SpeedBonus = excitementSpeedBonus;
        }
        else
        {
            SpeedBonus = 0f;
        }

        if (!isDashing &&
            cooldownTimer <= 0f &&
            dashAction.action.WasPressedThisFrame())
        {
            if (controller.isGrounded || !hasAirDashed)
            {
                StartDash();
            }
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            Vector3 dashDirection = visualTransform.forward;

            controller.Move(
                dashDirection * dashSpeed * Time.deltaTime
            );

            if (dashTimer <= 0f)
            {
                isDashing = false;

                animator.SetBool("IsDashing", false);
            }
        }
    }

    private void StartDash()
    {
        // PLAYER ACTIVITY - RESET AFK
        afk.ResetAFK();

        isDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;

        excitementTimer = excitementDuration;

        if (!controller.isGrounded)
        {
            hasAirDashed = true;
        }

        animator.SetBool("IsDashing", true);
    }
}