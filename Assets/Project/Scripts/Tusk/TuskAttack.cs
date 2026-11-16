using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// TuskAttack  (Scripts/Tusk)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   The basic attack: ONE standing swing. Press Attack and Tusk stops, plays
//   the swing, and at the right moment (Hit Time) everything with a
//   SteamKnockback inside a cone in front of her gets hit (loses steam and is
//   pushed away). When the swing ends (Swing Duration) she can move again.
//
//   - Only starts on the ground, and not while hit or recharging.
//   - Movement, jump and dash are locked during the swing (TuskMovement reads
//     IsAttacking through IsControlLocked).
//   - Getting hit interrupts the swing, and the hit never lands.
//   - A click made just before the swing ends is remembered (Input Buffer),
//     so clicking fast feels smooth.
//   - Timing uses simple timers, no Animation Events needed: set Swing Duration
//     to the length of your attack clip and Hit Time to the moment it connects.
//
// PUT IT ON   Tusk_Player.
// OWNS INPUT  Attack action.
// ANIMATOR    TuskMovement sets the bool IsAttacking while the swing lasts.
// SOUND       Plays TuskAudio's Attack Sound at the moment of the hit (Hit Time),
//             whether or not the poke touches anything.
// SLOTS       Visual Transform (the model that rotates), optional CameraFollow
//             for a small shake on hits. Movement is found automatically.
// TIP         Select Tusk in the Scene view to see the hit range (yellow).
// =============================================================================
public class TuskAttack : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference attackAction;

    [Header("Swing Timing (seconds)")]
    [Tooltip("How long the whole swing lasts. Match it to your attack " +
             "animation's length. Tusk cannot move during this time.")]
    [SerializeField] private float swingDuration = 0.5f;

    [Tooltip("When the hit lands, counted from the start of the swing. " +
             "Use the moment the swing connects in your animation.")]
    [SerializeField] private float hitTime = 0.2f;

    [Tooltip("Short rest after a swing before the next one can start.")]
    [SerializeField] private float cooldownAfterSwing = 0.1f;

    [Tooltip("A click made this long before the swing ends is remembered " +
             "and starts the next swing right away. 0 = off.")]
    [SerializeField] private float inputBuffer = 0.2f;

    [Header("Hit")]
    [SerializeField] private float range = 3f;
    [Tooltip("Total width of the hit area in front of Tusk, in degrees.")]
    [Range(10f, 360f)]
    [SerializeField] private float coneAngle = 120f;
    [SerializeField] private float steamDamage = 10f;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Feel")]
    [Tooltip("Optional: small camera shake when a hit lands.")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private float shakeStrength = 0.15f;
    [SerializeField] private float shakeDuration = 0.12f;

    [Header("References")]
    [Tooltip("The model that turns to face where Tusk is going.")]
    [SerializeField] private Transform visualTransform;
    [Tooltip("Left empty: found on this object automatically.")]
    [SerializeField] private TuskMovement movement;
    [Tooltip("Left empty: Tusk's TuskAudio is found automatically.")]
    [SerializeField] private TuskAudio tuskAudio;

    private readonly Collider[] colliderBuffer = new Collider[16];
    private readonly List<SteamKnockback> alreadyHit = new List<SteamKnockback>();

    private float swingTimer;
    private bool hitDelivered;
    private float nextAttackTime;
    private float bufferedUntil = -1f;

    /// <summary>True from the start to the end of a swing.</summary>
    public bool IsAttacking { get; private set; }

    /// <summary>Raised for every enemy this attack hits (the enemy health bar uses it).</summary>
    public event System.Action<SteamKnockback> HitLanded;

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<TuskMovement>();

        if (tuskAudio == null)
            tuskAudio = GetComponent<TuskAudio>();

        if (tuskAudio == null)
            tuskAudio = FindFirstObjectByType<TuskAudio>();
    }

    private void OnEnable()
    {
        // TuskAttack "owns" the attack action.
        if (attackAction != null && attackAction.action != null)
            attackAction.action.Enable();
    }

    private void OnDisable()
    {
        if (attackAction != null && attackAction.action != null)
            attackAction.action.Disable();

        IsAttacking = false;
    }

    private void Update()
    {
        // Getting hit interrupts everything: the swing stops, and so does
        // any remembered click.
        if (movement != null && movement.IsStunned)
        {
            IsAttacking = false;
            bufferedUntil = -1f;
            return;
        }

        RememberClick();

        if (IsAttacking)
            UpdateSwing();
        else
            TryStartSwing();
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    // Clicks are remembered for a moment, so pressing a little early
    // still counts. Clicks in the air are ignored.
    private void RememberClick()
    {
        if (attackAction == null || attackAction.action == null)
            return;

        if (!attackAction.action.WasPressedThisFrame())
            return;

        if (movement != null && !movement.IsGrounded)
            return;

        bufferedUntil = Time.time + inputBuffer;
    }

    private void TryStartSwing()
    {
        if (Time.time > bufferedUntil)
            return;

        if (Time.time < nextAttackTime)
            return;

        // No attacking while hit, recharging, or in the air.
        if (movement != null &&
            (movement.IsControlLocked || !movement.IsGrounded))
        {
            return;
        }

        bufferedUntil = -1f;

        IsAttacking = true;
        swingTimer = 0f;
        hitDelivered = false;
    }

    // ------------------------------------------------------------------
    // The swing
    // ------------------------------------------------------------------

    private void UpdateSwing()
    {
        swingTimer += Time.deltaTime;

        // The hit can never come after the end of the swing.
        float landTime = Mathf.Min(hitTime, swingDuration);

        if (!hitDelivered && swingTimer >= landTime)
        {
            hitDelivered = true;
            PerformHit();
        }

        if (swingTimer >= swingDuration)
        {
            IsAttacking = false;
            nextAttackTime = Time.time + cooldownAfterSwing;
        }
    }

    private void PerformHit()
    {
        if (tuskAudio != null)
            tuskAudio.PlayAttack();

        Vector3 facing = visualTransform != null
            ? visualTransform.forward
            : transform.forward;

        facing.y = 0f;

        if (facing.sqrMagnitude < 0.001f)
            facing = Vector3.forward;

        facing.Normalize();

        Vector3 origin = transform.position + Vector3.up;

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            range,
            colliderBuffer,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        alreadyHit.Clear();
        bool hitAnything = false;

        for (int i = 0; i < count; i++)
        {
            SteamKnockback target =
                colliderBuffer[i].GetComponentInParent<SteamKnockback>();

            // Skip: things that cannot be hit, ourselves, and targets already
            // hit in this swing (one target can have several colliders).
            if (target == null ||
                target.gameObject == gameObject ||
                alreadyHit.Contains(target))
            {
                continue;
            }

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = facing;

            if (Vector3.Angle(facing, toTarget) > coneAngle * 0.5f)
                continue;

            alreadyHit.Add(target);

            if (target.ApplyHit(toTarget, steamDamage))
            {
                hitAnything = true;
                HitLanded?.Invoke(target);
            }
        }

        if (hitAnything && cameraFollow != null)
            cameraFollow.Shake(shakeStrength, shakeDuration);
    }

    // Shows the hit range when Tusk is selected in the Scene view.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, range);
    }
}
