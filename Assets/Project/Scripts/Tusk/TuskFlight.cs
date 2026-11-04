using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// TuskFlight  (Scripts/Tusk)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Flying. In the air, release jump and press it again to start flying,
//   then HOLD it to keep rising. Flying burns steam (a small cost to start,
//   then a cost every second) and stops when steam runs out. Being hit also
//   interrupts flight.
//   TuskMovement reads GetFlightLift() and HorizontalSpeedBonus to move Tusk.
//
// PUT IT ON   Tusk_Player (or any object, as long as the Movement slot is set).
// READS       The Jump action (TuskMovement owns it) and movement.IsGrounded /
//             movement.IsStunned.
// NEEDS       Movement slot (required), SteamTank slot (empty = free flight),
//             optional steam particles and TuskAudio.
// TIP         Turn 'Play On Awake' OFF on the flying steam particle system.
// =============================================================================
public class TuskFlight : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference jumpAction;

    [Header("Flight")]
    [SerializeField] private float startingLift = 2.5f;
    [SerializeField] private float maximumLift = 5f;
    [SerializeField] private float liftAcceleration = 3f;
    [SerializeField] private float horizontalSpeedBonus = 3f;

    [Header("Steam Cost")]
    [Tooltip("Leave empty for free flight.")]
    [SerializeField] private SteamTank steamTank;
    [Tooltip("Steam spent once when flight starts.")]
    [SerializeField] private float startCost = 5f;
    [Tooltip("Steam spent every second while flying.")]
    [SerializeField] private float costPerSecond = 12f;
    [Tooltip("Flight stops when steam falls to this amount. 0 = fly until empty.")]
    [SerializeField] private float steamReserve = 0f;

    [Header("References")]
    [SerializeField] private TuskMovement movement;
    [SerializeField] private ParticleSystem flyingSteam;
    [SerializeField] private TuskAudio tuskAudio;

    private bool jumpReleasedAfterTakeoff;
    private float currentLift;

    public bool IsFlying { get; private set; }

    public float HorizontalSpeedBonus =>
        IsFlying ? horizontalSpeedBonus : 0f;

    private void Start()
    {
        if (movement == null)
        {
            Debug.LogError(
                "TuskFlight: Movement is not assigned.",
                this
            );

            enabled = false;
        }
    }

    private void OnEnable()
    {
        // The jump action is enabled and disabled by TuskMovement.
        // This script only reads it.
        jumpReleasedAfterTakeoff = false;
    }

    private void OnDisable()
    {
        StopFlight();
    }

    private void Update()
    {
        if (jumpAction == null || jumpAction.action == null || movement == null)
            return;

        bool grounded = movement.IsGrounded;
        bool jumpHeld = jumpAction.action.IsPressed();
        bool jumpPressed = jumpAction.action.WasPressedThisFrame();

        // Getting hit interrupts flight. After recovering, Tusk needs
        // a fresh jump release and press to fly again.
        if (movement.IsStunned)
        {
            jumpReleasedAfterTakeoff = false;

            if (IsFlying)
                StopFlight();

            return;
        }

        // On the ground, flight is unavailable.
        // The next jump must begin with a fresh press and release.
        if (grounded)
        {
            jumpReleasedAfterTakeoff = false;

            if (IsFlying)
                StopFlight();

            currentLift = 0f;
            return;
        }

        // Jump must be released after leaving the ground
        // before another press can activate flight.
        if (!jumpHeld)
            jumpReleasedAfterTakeoff = true;

        // A second press while airborne starts flight.
        if (!IsFlying &&
            jumpReleasedAfterTakeoff &&
            jumpPressed &&
            HasEnoughSteamToStart())
        {
            StartFlight();
        }

        // Holding jump sustains flight.
        if (IsFlying)
        {
            if (!jumpHeld)
            {
                StopFlight();
                return;
            }

            // Flying burns steam. Out of steam (or down to the reserve): stop.
            if (steamTank != null)
            {
                steamTank.Drain(costPerSecond * Time.deltaTime);

                if (steamTank.Current <= steamReserve)
                {
                    StopFlight();
                    return;
                }
            }

            currentLift = Mathf.MoveTowards(
                currentLift,
                maximumLift,
                liftAcceleration * Time.deltaTime
            );
        }
    }

    private bool HasEnoughSteamToStart()
    {
        if (steamTank == null)
            return true;

        return steamTank.Current >= startCost + steamReserve &&
               steamTank.Current > 0f;
    }

    private void StartFlight()
    {
        if (IsFlying)
            return;

        if (steamTank != null)
            steamTank.TrySpend(startCost);

        IsFlying = true;
        currentLift = startingLift;

        if (flyingSteam != null && !flyingSteam.isPlaying)
            flyingSteam.Play();

        if (tuskAudio != null)
            tuskAudio.PlayFlying();
    }

    public void StopFlight()
    {
        if (!IsFlying)
            return;

        IsFlying = false;
        currentLift = 0f;

        if (flyingSteam != null && flyingSteam.isPlaying)
            flyingSteam.Stop();

        if (tuskAudio != null)
            tuskAudio.StopFlying();
    }

    /// <summary>
    /// Plays or stops the flying steam and sound WITHOUT real flight.
    /// Used by the intro, where Tusk floats down by herself.
    /// </summary>
    public void ShowFlightEffects(bool show)
    {
        if (flyingSteam != null)
        {
            if (show && !flyingSteam.isPlaying)
                flyingSteam.Play();
            else if (!show && flyingSteam.isPlaying)
                flyingSteam.Stop();
        }

        if (tuskAudio != null)
        {
            if (show)
                tuskAudio.PlayFlying();
            else
                tuskAudio.StopFlying();
        }
    }

    public float GetFlightLift()
    {
        return IsFlying ? currentLift : 0f;
    }
}
