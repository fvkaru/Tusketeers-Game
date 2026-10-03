using UnityEngine;
using UnityEngine.InputSystem;

public class TuskMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -20f;

    [Header("References")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualTransform;
    [SerializeField] private ParticleSystem runningDust;
    [SerializeField] private float modelRotationOffset = 0f;

    public Vector3 MovementDirection { get; private set; }

    private CharacterController controller;
    private ParticleSystem.EmissionModule dustEmission;

    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (runningDust != null)
        {
            dustEmission = runningDust.emission;
        }
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

        // WASD → world movement
        Vector3 movement = new Vector3(
            input.x,
            0f,
            input.y
        );

        // Prevent diagonal movement from being faster.
        movement = Vector3.ClampMagnitude(movement, 1f);

        MovementDirection = movement;

        // 8-direction facing
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

        // Jump
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpAction.action.WasPressedThisFrame())
            {
                verticalVelocity =
                    Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        // Gravity
        verticalVelocity += gravity * Time.deltaTime;

        // Final movement
        Vector3 finalMovement =
            movement * moveSpeed;

        finalMovement.y = verticalVelocity;

        controller.Move(
            finalMovement * Time.deltaTime
        );

        // Animation
        animator.SetFloat("Speed", input.magnitude);

        // Running dust
        if (runningDust != null)
        {
            dustEmission.enabled =
                controller.isGrounded &&
                input.sqrMagnitude > 0.01f;
        }
    }
}