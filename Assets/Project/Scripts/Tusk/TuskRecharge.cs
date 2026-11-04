using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// TuskRecharge  (Scripts/Tusk)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   The last-resort heal. HOLD the Recharge button while standing on the
//   ground and Tusk shakes her backpack, gaining a little steam every few
//   seconds. She cannot move while doing it. A hit, letting go, leaving the
//   ground, or a full tank stops it.
//
// PUT IT ON   Tusk_Player.
// OWNS INPUT  Recharge action (hold).
// READ BY     TuskMovement (locks movement and sets the IsRecharging bool),
//             TuskAttack.
// TUNING      Steam Per Tick can be raised by the level system (it is a
//             public property). Max Steam Fraction limits it to low steam.
// =============================================================================
[RequireComponent(typeof(SteamTank))]
public class TuskRecharge : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference rechargeAction;

    [Header("Recharge")]
    [Tooltip("Steam gained each tick. Raise it with level later.")]
    [SerializeField] private float steamPerTick = 1f;
    [Tooltip("Seconds between ticks while holding the button.")]
    [SerializeField] private float tickInterval = 2f;
    [Tooltip("Recharging only works while steam is below this fraction of the tank. " +
             "1 = any time the tank is not full. 0.25 = only when under a quarter.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxSteamFraction = 1f;

    [Header("Feel")]
    [Tooltip("Optional puff played on every tick. Turn Play On Awake OFF on it.")]
    [SerializeField] private ParticleSystem tickEffect;

    [Header("References")]
    [SerializeField] private TuskMovement movement;

    private SteamTank tank;
    private float tickTimer;

    /// <summary>True while Tusk is shaking her backpack.</summary>
    public bool IsRecharging { get; private set; }

    /// <summary>Other scripts (levels) can raise this.</summary>
    public float SteamPerTick
    {
        get => steamPerTick;
        set => steamPerTick = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        tank = GetComponent<SteamTank>();

        if (movement == null)
            movement = GetComponent<TuskMovement>();
    }

    private void OnEnable()
    {
        // TuskRecharge "owns" the recharge action.
        if (rechargeAction != null && rechargeAction.action != null)
            rechargeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (rechargeAction != null && rechargeAction.action != null)
            rechargeAction.action.Disable();

        IsRecharging = false;
        tickTimer = 0f;
    }

    private void Update()
    {
        bool wantsRecharge =
            rechargeAction != null &&
            rechargeAction.action != null &&
            rechargeAction.action.IsPressed();

        bool canRecharge =
            movement != null &&
            movement.IsGrounded &&
            !movement.IsStunned &&
            !movement.IsAttacking &&
            tank.Current < tank.Max * maxSteamFraction;

        IsRecharging = wantsRecharge && canRecharge;

        if (!IsRecharging)
        {
            tickTimer = 0f;
            return;
        }

        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            tank.Gain(steamPerTick);

            if (tickEffect != null)
                tickEffect.Play();
        }
    }
}
