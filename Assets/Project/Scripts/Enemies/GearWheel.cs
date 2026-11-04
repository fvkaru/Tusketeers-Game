using UnityEngine;

// The behavior states of a GearWheel (read it through GearWheel.State).
public enum GearWheelState
{
    Approach,   // rolling toward Tusk in a curved path
    Orbit,      // "locked in": circling around Tusk
    WindUp,     // stops and aims at Tusk (the warning before the charge)
    Charge,     // dashes in a straight line; hurts Tusk if it touches her
    Recover,    // tired after the charge: a safe moment to hit it
    Hurt        // just got hit: pushed back and stunned
}

// =============================================================================
// GearWheel  (Scripts/Enemies)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Enemy 1: a rolling gear. Simple on purpose.
//     1. APPROACH  Rolls toward Tusk along a curved path.
//     2. ORBIT     Close enough = "locked in": circles around Tusk.
//     3. WIND UP   After a few seconds it stops and aims at her (the warning).
//     4. CHARGE    Rushes in a STRAIGHT line at full speed. Dodge it by dashing
//                  sideways, or by flying (or jumping) above it. If it touches
//                  Tusk it hits her with ApplyHit: steam loss, push, stun.
//                  The charge runs to where Tusk WAS plus a short overshoot,
//                  then stops. It only ends up off the arena if Tusk stood
//                  close to an edge and dodged at the right moment.
//     5. RECOVER   Tired for a moment (your chance to hit it), then back to 1.
//   Getting hit by Tusk interrupts everything (HURT), then it starts again.
//   It has a SteamTank that slowly refills, and it can be knocked off the
//   arena. When it falls it respawns (for testing) or is removed (for real).
//   While ROLLING (approach and orbit) it keeps away from edges, so it never
//   drives off by itself. Only a CHARGE (or a hit) can carry it off the arena.
//   The LESS steam it has, the faster it rolls (up to +25% by default). The
//   charge speed does not change, so the warning stays fair.
//   Add a SteamDropper to it and it drops steam when it falls off.
//
// PUT IT ON   An empty parent object with a model child (the gear), plus:
//             Character Controller, SteamTank, SteamKnockback, GearWheel.
//             Remove any other collider (the Character Controller is the body).
// TARGET      Left empty = it finds Tusk by itself.
//
// ANIMATION (optional, none needed yet)
//   Plan: ONE rolling clip, played faster or slower depending on speed.
//   Animator parameters it fills in, only if they exist:
//     RollSpeed (float)  speed * Roll Animation Scale. In the Animator, click
//                        the rolling state, and under Speed tick "Multiplier
//                        Parameter" and pick RollSpeed.
//     IsAttacking (bool)  true during the wind-up AND the charge together
//                         (use this if one 'charging' clip covers both)
//     IsWindingUp, IsCharging (bool)  the same two phases, separately
//     IsHurt (bool)
//   Sounds and effects can listen to the StateChanged event.
//
// SOUNDS (optional)
//   Rolling loop (volume and pitch follow the speed), a charging sound at the
//   start of the wind-up, an attacking sound at the start of the charge. The
//   wheel creates its own 3D audio sources, so just drop the clips in.
// PARTICLES (optional)
//   Rolling Dust: dust behind the wheel while it rolls on the ground, more
//   with speed (the same idea as Tusk's running dust).
// =============================================================================
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(SteamTank))]
[RequireComponent(typeof(SteamKnockback))]
public class GearWheel : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Leave empty: it finds Tusk automatically.")]
    [SerializeField] private Transform target;

    [Header("1. Approach")]
    [SerializeField] private float approachSpeed = 10f;
    [Tooltip("0 = straight line to Tusk. Higher = wider, curvier path.")]
    [SerializeField] private float approachCurve = 0.8f;

    [Header("2. Orbit (locked in)")]
    [Tooltip("Closer than this to Tusk = locks in and starts circling.")]
    [SerializeField] private float lockInDistance = 13f;
    [Tooltip("How far from Tusk it circles.")]
    [SerializeField] private float orbitRadius = 9f;
    [SerializeField] private float orbitSpeed = 13f;
    [Tooltip("It circles for a random time between these two values (seconds).")]
    [SerializeField] private float minOrbitTime = 2.5f;
    [SerializeField] private float maxOrbitTime = 4f;

    [Header("3 to 5. Attack")]
    [Tooltip("Seconds it stops and aims before charging. This is the player's warning.")]
    [SerializeField] private float windUpTime = 0.7f;
    [SerializeField] private float chargeSpeed = 20f;
    [Tooltip("Safety limit: the longest a charge can last (seconds).")]
    [SerializeField] private float chargeDuration = 1.1f;
    [Tooltip("The charge runs to where Tusk was when it started, plus this " +
             "extra distance (meters). Lower = it almost never falls off by itself.")]
    [SerializeField] private float chargeOvershoot = 1.5f;
    [Tooltip("Shortest and longest a charge can be (meters).")]
    [SerializeField] private float minChargeDistance = 5f;
    [SerializeField] private float maxChargeDistance = 16f;
    [Tooltip("Seconds it is tired after a charge.")]
    [SerializeField] private float recoverTime = 1f;
    [SerializeField] private float steamDamage = 15f;
    [Tooltip("Extra distance (meters) around the wheel's body in which it still " +
             "counts as touching Tusk. The body is the Character Controller " +
             "(Center, Radius, Height): make it smaller to make the wheel easier " +
             "to dodge. 0 = she must really touch it.")]
    [SerializeField] private float hitExtraReach = 0.15f;

    [Header("Movement")]
    [Tooltip("How fast it can turn, in degrees per second.")]
    [SerializeField] private float turnSpeed = 240f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float gravity = -20f;

    [Header("Edge Safety (rolling only, not the charge)")]
    [Tooltip("ON: while approaching or circling, it turns away from edges.")]
    [SerializeField] private bool avoidEdges = true;
    [Tooltip("How far ahead (in seconds of travel) it looks for the floor.")]
    [SerializeField] private float edgeLookAheadTime = 0.35f;
    [Tooltip("It always looks at least this far ahead (meters).")]
    [SerializeField] private float minEdgeLookAhead = 2.5f;
    [Tooltip("Extra safety distance (meters) on top of the look-ahead, so it turns earlier.")]
    [SerializeField] private float edgeMargin = 1.5f;
    [Tooltip("How much it slows down while turning away from an edge " +
             "(1 = no slowdown, 0.5 = half speed).")]
    [Range(0.2f, 1f)]
    [SerializeField] private float edgeSlowdown = 0.6f;
    [Tooltip("Layers that count as floor. Everything is fine: Tusk and " +
             "other enemies are ignored automatically.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Steam Recovery")]
    [SerializeField] private float regenPerSecond = 0.5f;
    [Tooltip("Seconds after being hit before steam starts coming back.")]
    [SerializeField] private float regenDelay = 4f;

    [Header("Low Steam = Faster")]
    [Tooltip("How much faster it rolls with an EMPTY tank (0.25 = 25% faster). " +
             "It grows smoothly as the tank empties. Only approach and orbit " +
             "speed change, not the charge.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowSteamSpeedBonus = 0.25f;

    [Header("Falling Off The Arena")]
    [SerializeField] private float respawnBelowY = -30f;
    [Tooltip("ON for testing: comes back at its start spot. OFF: it disappears.")]
    [SerializeField] private bool respawnWhenFallen = true;

    [Header("Visual (all optional)")]
    [Tooltip("The model. Shakes slightly during the wind-up as a warning.")]
    [SerializeField] private Transform visualTransform;
    [SerializeField] private float windUpShake = 0.05f;
    [SerializeField] private Animator animator;
    [Tooltip("RollSpeed = speed * this. Tune it until the rolling matches the ground.")]
    [SerializeField] private float rollAnimationScale = 0.15f;

    [Header("Sounds (all optional)")]
    [Tooltip("Loops while the wheel rolls. Louder and higher-pitched the faster it goes.")]
    [SerializeField] private AudioClip rollingSound;
    [Tooltip("Plays once when the wind-up starts: the warning before the charge.")]
    [SerializeField] private AudioClip chargingSound;
    [Tooltip("Plays once when the charge starts.")]
    [SerializeField] private AudioClip attackingSound;
    [Range(0f, 1f)]
    [SerializeField] private float rollingVolume = 0.6f;
    [Range(0f, 1f)]
    [SerializeField] private float chargingVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float attackingVolume = 1f;
    [Tooltip("Pitch of the rolling sound when slow, and at full speed.")]
    [SerializeField] private float rollingMinPitch = 0.8f;
    [SerializeField] private float rollingMaxPitch = 1.3f;
    [Tooltip("Leave both empty: the wheel creates its own 3D sound sources.")]
    [SerializeField] private AudioSource loopSource;
    [SerializeField] private AudioSource effectsSource;

    [Header("Particles (optional)")]
    [Tooltip("Dust while rolling on the ground, like Tusk's running dust. A particle " +
             "system at the base of the wheel with Play On Awake ON and Simulation " +
             "Space on World. The amount grows with speed.")]
    [SerializeField] private ParticleSystem rollingDust;

    // Animator parameters (only used if they exist).
    private static readonly int RollSpeedHash = Animator.StringToHash("RollSpeed");
    private static readonly int IsWindingUpHash = Animator.StringToHash("IsWindingUp");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int IsHurtHash = Animator.StringToHash("IsHurt");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private bool hasRollSpeed;
    private bool hasWindingUp;
    private bool hasCharging;
    private bool hasHurt;
    private bool hasAttacking;

    private ParticleSystem.EmissionModule dustEmission;
    private float baseDustRate;

    private CharacterController controller;
    private SteamTank tank;
    private SteamKnockback knockback;
    private SteamKnockback targetKnockback;
    private float targetRadius = 0.5f;

    private Vector3 startPosition;
    private Vector3 visualStartPosition;

    private Vector3 currentHeading;
    private float currentSpeed;
    private float verticalVelocity;

    private float stateTimer;
    private float orbitSign = 1f;
    private Vector3 chargeDirection;
    private Vector3 chargeStartPosition;
    private float chargeTravelDistance;
    private CollisionFlags lastMoveFlags;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private float lastHitTime = -100f;

    /// <summary>What the wheel is doing right now.</summary>
    public GearWheelState State { get; private set; } = GearWheelState.Approach;

    /// <summary>Raised on every state change (use it for sounds and effects).</summary>
    public event System.Action<GearWheelState> StateChanged;

    /// <summary>Raised when it falls off the arena (steam balls will use this).</summary>
    public event System.Action<GearWheel> RingedOut;

    // Unity calls this when the component is added (or Reset is chosen in its
    // menu). It gives a new wheel the default body (small and generous, so the
    // charge is easy to dodge) and a tiny steam tank (tutorial enemy: it goes
    // down in one or two hits).
    private void Reset()
    {
        CharacterController body = GetComponent<CharacterController>();

        if (body != null)
        {
            body.center = new Vector3(0f, 0.55f, 0f);
            body.radius = 0.3f;
            body.height = 0.8f;
        }

        SteamTank steam = GetComponent<SteamTank>();

        if (steam != null)
            steam.SetMaxSteam(12f);

#if UNITY_EDITOR
        if (body != null)
            UnityEditor.EditorUtility.SetDirty(body);

        if (steam != null)
            UnityEditor.EditorUtility.SetDirty(steam);
#endif
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        tank = GetComponent<SteamTank>();
        knockback = GetComponent<SteamKnockback>();

        startPosition = transform.position;

        currentHeading = transform.forward;
        currentHeading.y = 0f;

        if (currentHeading.sqrMagnitude < 0.001f)
            currentHeading = Vector3.forward;

        currentHeading.Normalize();

        if (visualTransform != null)
            visualStartPosition = visualTransform.localPosition;

        SetupAudio();

        if (rollingDust != null)
        {
            dustEmission = rollingDust.emission;
            baseDustRate = dustEmission.rateOverTimeMultiplier;
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            hasRollSpeed = HasAnimatorParameter(RollSpeedHash);
            hasWindingUp = HasAnimatorParameter(IsWindingUpHash);
            hasCharging = HasAnimatorParameter(IsChargingHash);
            hasHurt = HasAnimatorParameter(IsHurtHash);
            hasAttacking = HasAnimatorParameter(IsAttackingHash);
        }
    }

    private void Start()
    {
        // Find Tusk automatically if the slot was left empty.
        if (target == null)
        {
            TuskMovement tusk = FindFirstObjectByType<TuskMovement>();

            if (tusk != null)
                target = tusk.transform;
        }

        if (target != null)
            targetKnockback = target.GetComponentInParent<SteamKnockback>();

            // Tusk's own size, so the hit matches her body.
            CharacterController targetBody =
                target.GetComponentInParent<CharacterController>();

            if (targetBody != null)
                targetRadius = targetBody.radius;
        else
            Debug.LogWarning("GearWheel: no Tusk found in the scene.", this);

        // Half of the wheels circle one way, half the other.
        orbitSign = Random.value < 0.5f ? 1f : -1f;
    }

    private void OnEnable()
    {
        knockback.Hit += OnHit;
    }

    private void OnDisable()
    {
        knockback.Hit -= OnHit;
        ResetVisual();

        if (loopSource != null)
            loopSource.Stop();

        if (rollingDust != null)
            dustEmission.enabled = false;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        RecoverSteam(deltaTime);

        if (State == GearWheelState.Hurt)
        {
            // Wait until the stun is over, then start again.
            if (!knockback.IsStunned)
                EnterState(GearWheelState.Approach);
        }
        else if (target != null)
        {
            UpdateBehavior(deltaTime);
        }

        ApplyMovement(deltaTime);
        UpdateRollingSound(deltaTime);
        UpdateRollingDust();
        UpdateAnimator();
        CheckRingOut();
    }

    // ------------------------------------------------------------------
    // Behavior
    // ------------------------------------------------------------------

    private void UpdateBehavior(float deltaTime)
    {
        Vector3 offset = target.position - transform.position;

        Vector3 flatOffset = offset;
        flatOffset.y = 0f;

        float distance = flatOffset.magnitude;

        Vector3 toTarget = distance > 0.01f
            ? flatOffset / distance
            : currentHeading;

        // A direction at a right angle to "toward Tusk" (used for circling).
        Vector3 tangent = Vector3.Cross(Vector3.up, toTarget) * orbitSign;

        switch (State)
        {
            case GearWheelState.Approach:
                UpdateApproach(toTarget, tangent, distance, deltaTime);
                break;

            case GearWheelState.Orbit:
                UpdateOrbit(toTarget, tangent, distance, deltaTime);
                break;

            case GearWheelState.WindUp:
                UpdateWindUp(toTarget, distance, deltaTime);
                break;

            case GearWheelState.Charge:
                UpdateCharge(offset, deltaTime);
                break;

            case GearWheelState.Recover:
                UpdateRecover(deltaTime);
                break;
        }
    }

    // Roll toward Tusk, bending sideways so the path is a curve.
    private void UpdateApproach(
        Vector3 toTarget, Vector3 tangent, float distance, float deltaTime)
    {
        if (distance <= lockInDistance)
        {
            EnterState(GearWheelState.Orbit);
            return;
        }

        Vector3 desired = (toTarget + tangent * approachCurve).normalized;
        desired = AvoidEdges(desired, out float edgeSpeed);

        Roll(desired, approachSpeed * RollSpeedMultiplier * edgeSpeed, deltaTime);
    }

    // Circle around Tusk. The "radial" part keeps the circle at Orbit Radius:
    // too far = drift inward, too close = drift outward.
    private void UpdateOrbit(
        Vector3 toTarget, Vector3 tangent, float distance, float deltaTime)
    {
        // Tusk ran far away: give up circling and chase again.
        if (distance > lockInDistance * 1.6f)
        {
            EnterState(GearWheelState.Approach);
            return;
        }

        float radial = Mathf.Clamp((distance - orbitRadius) * 0.5f, -1f, 1f);
        Vector3 desired = (tangent + toTarget * radial).normalized;
        desired = AvoidEdges(desired, out float edgeSpeed);

        Roll(desired, orbitSpeed * RollSpeedMultiplier * edgeSpeed, deltaTime);

        stateTimer -= deltaTime;

        if (stateTimer <= 0f)
            EnterState(GearWheelState.WindUp);
    }

    // Stop, turn to face Tusk, shake a little. Then lock the direction.
    private void UpdateWindUp(Vector3 toTarget, float distance, float deltaTime)
    {
        currentSpeed = Mathf.MoveTowards(
            currentSpeed, 0f, acceleration * 2f * deltaTime);

        // Turn faster than normal so it really looks at her.
        currentHeading = Vector3.RotateTowards(
            currentHeading,
            toTarget,
            turnSpeed * 2f * Mathf.Deg2Rad * deltaTime,
            0f
        );

        if (visualTransform != null && windUpShake > 0f)
        {
            visualTransform.localPosition =
                visualStartPosition + Random.insideUnitSphere * windUpShake;
        }

        stateTimer -= deltaTime;

        if (stateTimer <= 0f)
        {
            // From here on the direction never changes: dodge it!
            chargeDirection = toTarget;

            // The charge goes to where Tusk is NOW, plus a little extra.
            chargeStartPosition = transform.position;
            chargeTravelDistance = Mathf.Clamp(
                distance + chargeOvershoot,
                minChargeDistance,
                maxChargeDistance
            );

            EnterState(GearWheelState.Charge);
        }
    }

    // Straight line at full speed. Ends when it hits Tusk, hits a wall,
    // or runs out of time.
    private void UpdateCharge(Vector3 offsetToTarget, float deltaTime)
    {
        currentHeading = chargeDirection;
        currentSpeed = chargeSpeed;

        stateTimer -= deltaTime;

        bool touchedTusk = TryHitTusk(offsetToTarget);
        bool hitWall = (lastMoveFlags & CollisionFlags.Sides) != 0;

        // Has it run as far as planned?
        Vector3 travelled = transform.position - chargeStartPosition;
        travelled.y = 0f;

        bool reachedEnd = travelled.magnitude >= chargeTravelDistance;

        if (touchedTusk || hitWall || reachedEnd || stateTimer <= 0f)
            EnterState(GearWheelState.Recover);
    }

    private void UpdateRecover(float deltaTime)
    {
        currentSpeed = Mathf.MoveTowards(
            currentSpeed, 0f, acceleration * 2f * deltaTime);

        stateTimer -= deltaTime;

        if (stateTimer <= 0f)
            EnterState(GearWheelState.Approach);
    }

    // Returns true if the wheel touched Tusk (even if she was protected).
    // Passing above her (flying) means no contact.
    private bool TryHitTusk(Vector3 offsetToTarget)
    {
        float height = offsetToTarget.y;

        Vector3 flat = offsetToTarget;
        flat.y = 0f;

        // The wheel's body is its Character Controller. It touches Tusk when
        // she is within both bodies' radii (plus a little reach) and her feet
        // are not above the top of the wheel: jumping or flying over dodges it.
        float reach = controller.radius + targetRadius + hitExtraReach;

        float bodyTop =
            controller.center.y + controller.height * 0.5f + hitExtraReach;

        if (flat.magnitude > reach || height > bodyTop)
            return false;

        if (targetKnockback != null)
            targetKnockback.ApplyHit(chargeDirection, steamDamage);

        return true;
    }

    // If the floor ends ahead, turn away from the edge (trying 30, 60, 90...
    // degrees to either side) until the floor is there, and slow down while
    // doing it. Returns zero if every direction is unsafe.
    private Vector3 AvoidEdges(Vector3 desired, out float speedMultiplier)
    {
        speedMultiplier = 1f;

        if (!avoidEdges)
            return desired;

        // Look far enough ahead to have time to turn, plus a safety margin.
        float lookAhead = Mathf.Max(
            minEdgeLookAhead, currentSpeed * edgeLookAheadTime) + edgeMargin;

        if (HasGroundAhead(desired, lookAhead))
            return desired;

        // An edge is ahead: slow down while turning away from it.
        speedMultiplier = edgeSlowdown;

        for (int step = 1; step <= 6; step++)
        {
            float angle = 30f * step;

            Vector3 left = Quaternion.AngleAxis(-angle, Vector3.up) * desired;
            Vector3 right = Quaternion.AngleAxis(angle, Vector3.up) * desired;

            if (HasGroundAhead(left, lookAhead))
                return left;

            if (HasGroundAhead(right, lookAhead))
                return right;
        }

        return Vector3.zero;
    }

    // Is there floor ahead in this direction? The wheel is wide, so the floor
    // must also be there on both sides of its path, not just in the middle.
    private bool HasGroundAhead(Vector3 direction, float distance)
    {
        Vector3 center = transform.position + direction * distance;

        Vector3 side =
            Vector3.Cross(Vector3.up, direction).normalized *
            (controller.radius + 0.2f);

        return HasFloorAt(center) &&
               HasFloorAt(center + side) &&
               HasFloorAt(center - side);
    }

    // Looks straight down at one point for walkable floor.
    private bool HasFloorAt(Vector3 point)
    {
        const float probeHeight = 1f;   // start a bit above, so slopes are fine
        const float maxDrop = 2f;       // a drop smaller than this still counts as floor

        Vector3 origin = point + Vector3.up * probeHeight;

        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            groundHits,
            probeHeight + maxDrop,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];

            // Tusk and the enemies are not floor.
            if (hit.collider is CharacterController)
                continue;

            // Only upward-facing surfaces count as floor.
            if (hit.normal.y < 0.5f)
                continue;

            return true;
        }

        return false;
    }

    // Turn gradually toward a direction and speed up or slow down.
    private void Roll(Vector3 desiredHeading, float targetSpeed, float deltaTime)
    {
        // No safe direction at all: just slow down and wait.
        if (desiredHeading.sqrMagnitude < 0.001f)
        {
            currentSpeed = Mathf.MoveTowards(
                currentSpeed, 0f, acceleration * 2f * deltaTime);
            return;
        }

        currentHeading = Vector3.RotateTowards(
            currentHeading,
            desiredHeading,
            turnSpeed * Mathf.Deg2Rad * deltaTime,
            0f
        );

        currentSpeed = Mathf.MoveTowards(
            currentSpeed, targetSpeed, acceleration * deltaTime);
    }

    private void EnterState(GearWheelState next)
    {
        // Leaving the wind-up: stop the shaking.
        if (State == GearWheelState.WindUp)
            ResetVisual();

        State = next;

        switch (next)
        {
            case GearWheelState.Orbit:
                stateTimer = Random.Range(minOrbitTime, maxOrbitTime);
                break;

            case GearWheelState.WindUp:
                stateTimer = windUpTime;
                PlayEffect(chargingSound, chargingVolume);
                break;

            case GearWheelState.Charge:
                stateTimer = chargeDuration;
                lastMoveFlags = CollisionFlags.None;
                PlayEffect(attackingSound, attackingVolume);
                break;

            case GearWheelState.Recover:
                stateTimer = recoverTime;
                break;

            case GearWheelState.Hurt:
                currentSpeed = 0f;
                break;
        }

        StateChanged?.Invoke(next);
    }

    // A hit from Tusk (or anything) interrupts whatever the wheel was doing.
    private void OnHit(Vector3 pushDirection)
    {
        lastHitTime = Time.time;

        if (State != GearWheelState.Hurt)
            EnterState(GearWheelState.Hurt);
    }

    // ------------------------------------------------------------------
    // Movement, steam, falling
    // ------------------------------------------------------------------

    private void ApplyMovement(float deltaTime)
    {
        // Face where it is rolling (not while it is being knocked around).
        if (State != GearWheelState.Hurt && currentHeading.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(currentHeading, Vector3.up),
                720f * deltaTime
            );
        }

        Vector3 move = currentHeading * currentSpeed;

        // Hits push the wheel on top of its own rolling.
        move += knockback.PushVelocity;

        float upward = knockback.TakeUpwardImpulse();

        if (upward > 0f)
            verticalVelocity = Mathf.Max(verticalVelocity, upward);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * deltaTime;
        move.y = verticalVelocity;

        lastMoveFlags = controller.Move(move * deltaTime);
    }

    // 1 with a full tank, up to 1 + Low Steam Speed Bonus with an empty one.
    private float RollSpeedMultiplier =>
        1f + lowSteamSpeedBonus * (1f - tank.Normalized);

    // Enemies slowly refill their steam when nobody is hitting them.
    private void RecoverSteam(float deltaTime)
    {
        if (Time.time >= lastHitTime + regenDelay)
            tank.Gain(regenPerSecond * deltaTime);
    }

    private void CheckRingOut()
    {
        if (transform.position.y >= respawnBelowY)
            return;

        RingedOut?.Invoke(this);

        if (respawnWhenFallen)
            Respawn();
        else
            Destroy(gameObject);
    }

    /// <summary>Puts the wheel back at its start spot with a full tank.</summary>
    public void ResetToStart()
    {
        Respawn();
    }

    private void Respawn()
    {
        // The controller must be off while teleporting, or it fights the move.
        controller.enabled = false;
        transform.position = startPosition;
        controller.enabled = true;

        verticalVelocity = 0f;
        currentSpeed = 0f;

        knockback.ClearPush();
        tank.SetFull();

        EnterState(GearWheelState.Approach);
    }

    // ------------------------------------------------------------------
    // Visual / animation
    // ------------------------------------------------------------------

    private void ResetVisual()
    {
        if (visualTransform != null)
            visualTransform.localPosition = visualStartPosition;
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

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        if (hasRollSpeed)
            animator.SetFloat(RollSpeedHash, currentSpeed * rollAnimationScale);

        if (hasWindingUp)
            animator.SetBool(IsWindingUpHash, State == GearWheelState.WindUp);

        if (hasCharging)
            animator.SetBool(IsChargingHash, State == GearWheelState.Charge);

        if (hasHurt)
            animator.SetBool(IsHurtHash, State == GearWheelState.Hurt);

        if (hasAttacking)
        {
            animator.SetBool(
                IsAttackingHash,
                State == GearWheelState.WindUp || State == GearWheelState.Charge);
        }
    }

    // ------------------------------------------------------------------
    // Sounds and dust
    // ------------------------------------------------------------------

    // The speed that counts as "full speed" for the sound pitch and dust amount.
    private float SpeedReference =>
        Mathf.Max(orbitSpeed * (1f + lowSteamSpeedBonus), 1f);

    private void SetupAudio()
    {
        // Nothing assigned: create two 3D sound sources on the wheel itself.
        if (loopSource == null)
            loopSource = CreateSource();

        if (effectsSource == null)
            effectsSource = CreateSource();

        loopSource.loop = true;
        loopSource.volume = 0f;   // fades in when the wheel starts rolling
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.spatialBlend = 1f;   // 3D: the sound comes from the wheel
        source.minDistance = 4f;
        source.maxDistance = 40f;

        return source;
    }

    private void PlayEffect(AudioClip clip, float volume)
    {
        if (effectsSource != null && clip != null)
            effectsSource.PlayOneShot(clip, volume);
    }

    // The rolling loop: louder and higher with speed, fades out as the wheel stops.
    private void UpdateRollingSound(float deltaTime)
    {
        if (loopSource == null || rollingSound == null)
            return;

        float speedAmount = Mathf.Clamp01(currentSpeed / SpeedReference);
        bool rolling = State != GearWheelState.Hurt && currentSpeed > 0.5f;

        float targetVolume = rolling ? rollingVolume * speedAmount : 0f;

        loopSource.volume = Mathf.MoveTowards(
            loopSource.volume, targetVolume, 4f * deltaTime);

        if (rolling)
        {
            loopSource.pitch =
                Mathf.Lerp(rollingMinPitch, rollingMaxPitch, speedAmount);

            if (!loopSource.isPlaying)
            {
                loopSource.clip = rollingSound;
                loopSource.Play();
            }
        }
        else if (loopSource.isPlaying && loopSource.volume <= 0.01f)
        {
            loopSource.Stop();
        }
    }

    // Dust behind the wheel while it rolls on the ground (like Tusk's running
    // dust: the particles only switch on and off, and grow with speed).
    private void UpdateRollingDust()
    {
        if (rollingDust == null)
            return;

        bool onGround =
            controller.isGrounded &&
            currentSpeed > 1f &&
            State != GearWheelState.Hurt;

        dustEmission.enabled = onGround;

        if (onGround)
        {
            dustEmission.rateOverTimeMultiplier =
                baseDustRate * Mathf.Clamp01(currentSpeed / SpeedReference);
        }
    }

    // Select the wheel in the Scene view to see its ranges.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lockInDistance);

        CharacterController body = GetComponent<CharacterController>();

        Vector3 bodyCenter = transform.position +
            (body != null ? body.center : Vector3.zero);

        float bodyRadius = body != null ? body.radius : 0.5f;

        // Red = how close Tusk must be for the charge to hit her.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            bodyCenter, bodyRadius + targetRadius + hitExtraReach);
    }
}
