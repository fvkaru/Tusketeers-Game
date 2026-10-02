using UnityEngine;
using UnityEngine.InputSystem;

public class TuskMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualTransform;
    [SerializeField] private float modelRotationOffset = 0f;

    public Vector3 MovementDirection { get; private set; }

    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
    }

    private void Update()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();

        // WASD → world movement
        // W = +Z
        // S = -Z
        // D = +X
        // A = -X
        Vector3 movement = new Vector3(
            input.x,
            0f,
            input.y
        );

        // Keep diagonal movement from being faster.
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

        // Move Tusk
        controller.Move(
            movement * moveSpeed * Time.deltaTime
        );

        // Update animation
        animator.SetFloat("Speed", input.magnitude);
    }
}