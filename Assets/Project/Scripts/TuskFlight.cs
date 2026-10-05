using UnityEngine;
using UnityEngine.InputSystem;

public class TuskFlight : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference jumpAction;

    [Header("Flight")]
    [SerializeField] private float startingLift = 2f;
    [SerializeField] private float maximumLift = 4f;
    [SerializeField] private float liftAcceleration = 0.8f;

    [Header("References")]
    [SerializeField] private TuskMovement movement;
    [SerializeField] private ParticleSystem flyingSteam;

    public bool IsFlying { get; private set; }

    private CharacterController controller;

    private float currentLift;

    private bool canStartFlying;
    private bool spaceWasReleased;

    private ParticleSystem.EmissionModule steamEmission;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (flyingSteam != null)
        {
            steamEmission = flyingSteam.emission;
            steamEmission.enabled = false;
        }
    }

    private void OnEnable()
    {
        jumpAction.action.Enable();
    }

    private void OnDisable()
    {
        jumpAction.action.Disable();
    }

    private void Update()
    {
        if (controller.isGrounded)
        {
            IsFlying = false;
            currentLift = startingLift;

            canStartFlying = false;
            spaceWasReleased = false;

            UpdateSteam();

            return;
        }

        if (!jumpAction.action.IsPressed())
        {
            spaceWasReleased = true;
            canStartFlying = true;
        }

        if (canStartFlying &&
            spaceWasReleased &&
            jumpAction.action.WasPressedThisFrame())
        {
            IsFlying = true;
            spaceWasReleased = false;
        }

        if (IsFlying)
        {
            currentLift = Mathf.MoveTowards(
                currentLift,
                maximumLift,
                liftAcceleration * Time.deltaTime
            );
        }
        else
        {
            currentLift = startingLift;
        }

        if (IsFlying &&
            jumpAction.action.WasReleasedThisFrame())
        {
            IsFlying = false;
            currentLift = startingLift;
        }

        UpdateSteam();
    }

    private void UpdateSteam()
    {
        if (flyingSteam != null)
        {
            steamEmission.enabled = IsFlying;
        }
    }

    public float GetFlightLift()
    {
        if (!IsFlying)
        {
            return 0f;
        }

        return currentLift;
    }
}