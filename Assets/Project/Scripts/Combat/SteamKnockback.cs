using System;
using UnityEngine;

// =============================================================================
// SteamKnockback  (Scripts/Combat)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Makes something hittable. ApplyHit(direction, steamDamage):
//     - drains steam,
//     - pushes the target away (the LIGHTER its tank, the FARTHER the push),
//     - stuns it for a moment (lighter = longer),
//     - protects it from more hits while stunned plus a short grace time.
//   The push uses the steam BEFORE the hit, so the hit that empties a tank
//   does not launch it, but the next one does (the 'empty = lethal' rule).
//   The push distance is set in METERS in the Inspector.
//
// PUT IT ON   Tusk_Player and every enemy (needs SteamTank on the same object).
// MOVERS      The object's own movement script must add PushVelocity to its
//             movement and use TakeUpwardImpulse() (see TuskMovement/TestDummy).
// TIP         Right-click the title -> 'Test Hit' (Play mode) to try it.
// =============================================================================
[RequireComponent(typeof(SteamTank))]
public class SteamKnockback : MonoBehaviour
{
    [Header("Push Distance (meters)")]
    [Tooltip("How far a hit pushes when the tank is FULL.")]
    [SerializeField] private float pushDistanceFull = 3f;

    [Tooltip("How far a hit pushes when the tank is EMPTY. " +
             "Make this bigger than the arena so it always knocks you off.")]
    [SerializeField] private float pushDistanceEmpty = 30f;

    [Tooltip("How 'light' the target is for a given steam level. " +
             "Left side = empty tank, right side = full tank. " +
             "Up (1) = lightest, down (0) = heaviest.")]
    [SerializeField] private AnimationCurve lightnessBySteam =
        AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Feel")]
    [Tooltip("How quickly the push slows down. Higher = short, snappy push. " +
             "Lower = long slide.")]
    [SerializeField] private float pushDecay = 6f;

    [Tooltip("Small hop upward when hit.")]
    [SerializeField] private float upwardPop = 4f;

    [Tooltip("Extra seconds of protection AFTER recovering from a hit, " +
             "so enemies cannot chain hits forever.")]
    [SerializeField] private float invulnerableTime = 0.25f;

    [Header("Stun")]
    [Tooltip("Seconds Tusk cannot act after a hit when the tank is FULL.")]
    [SerializeField] private float stunDurationFull = 0.4f;
    [Tooltip("Seconds Tusk cannot act after a hit when the tank is EMPTY. " +
             "Lighter targets stay stunned longer.")]
    [SerializeField] private float stunDurationEmpty = 0.8f;

    private SteamTank tank;
    private float nextHitTime;
    private float pendingUpward;
    private float stunEndTime;
    private bool stunEnabled = true;

    /// <summary>Current sideways push. The mover adds this to its movement.</summary>
    public Vector3 PushVelocity { get; private set; }

    /// <summary>Raised when a hit lands. Direction is the push direction.</summary>
    public event Action<Vector3> Hit;

    /// <summary>True while recovering from a hit (cannot move or attack).</summary>
    public bool IsStunned => stunEnabled && Time.time < stunEndTime;

    private void Awake()
    {
        tank = GetComponent<SteamTank>();
    }

    private void Update()
    {
        if (PushVelocity.sqrMagnitude < 0.0001f)
            return;

        PushVelocity *= Mathf.Exp(-pushDecay * Time.deltaTime);

        if (PushVelocity.sqrMagnitude < 0.0025f)
            PushVelocity = Vector3.zero;
    }

    /// <summary>
    /// Hits this object. Direction is the way it should be pushed.
    /// Returns false if the hit was ignored (still invulnerable).
    /// </summary>
    public bool ApplyHit(Vector3 direction, float steamDamage)
    {
        if (Time.time < nextHitTime)
            return false;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;

        direction.Normalize();

        // The push depends on the steam BEFORE this hit, so a hit that
        // empties the tank doesn't launch you. The NEXT one will.
        float lightness = Mathf.Clamp01(
            lightnessBySteam.Evaluate(tank.Normalized)
        );

        float distance = Mathf.Lerp(
            pushDistanceFull,
            pushDistanceEmpty,
            lightness
        );

        // With exponential slow-down, distance = startSpeed / decay.
        PushVelocity = direction * (distance * pushDecay);
        pendingUpward = upwardPop;

        // Lighter targets stay stunned longer.
        float stun = stunEnabled
            ? Mathf.Lerp(stunDurationFull, stunDurationEmpty, lightness)
            : 0f;

        stunEndTime = Time.time + stun;

        tank.Drain(steamDamage);

        // Protected while stunned, plus a short grace period after.
        nextHitTime = Time.time + stun + invulnerableTime;
        Hit?.Invoke(direction);

        return true;
    }

    /// <summary>
    /// Used by the mover once per hit: the upward speed to apply (0 if none).
    /// </summary>
    public float TakeUpwardImpulse()
    {
        float value = pendingUpward;
        pendingUpward = 0f;
        return value;
    }

    public void ClearPush()
    {
        PushVelocity = Vector3.zero;
        pendingUpward = 0f;
        stunEndTime = 0f;
    }

    /// <summary>For things that should never be stunned (the test dummy).</summary>
    public void DisableStun()
    {
        stunEnabled = false;
        stunEndTime = 0f;
    }

    // Right-click the component title in the Inspector (in Play mode)
    // to test a hit without needing an attacker.
    [ContextMenu("Test Hit (push away from camera)")]
    private void TestHit()
    {
        Vector3 from = Camera.main != null
            ? Camera.main.transform.position
            : transform.position + transform.forward;

        ApplyHit(transform.position - from, 10f);
    }
}
