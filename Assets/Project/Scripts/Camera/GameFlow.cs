using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// GameFlow  (Scripts/Game)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Runs the start of the game and the respawn.
//
//   INTRO (every time the game starts, and again after every respawn)
//     1. The screen is white (a cloud). Tusk is high above the arena center,
//        in her flying pose.
//     2. The white clears while Tusk slowly floats down to the arena center,
//        followed by the camera.
//     3. She lands (landing sound and dust), pauses a moment, the camera glides
//        to the normal view, and the player gets control.
//     During the intro, Tusk's controls and the enemies are frozen.
//
//   RESPAWN
//     If Tusk falls deeper than Respawn Depth below the platform, the screen
//     fades to white, the world is reset (steam refilled, drops removed,
//     enemies back at their start), and the intro plays again.
//
// PUT IT ON   One empty object in the scene, called GameFlow.
// SLOTS       None needed. It finds Tusk, the camera and the object called
//             ArenaCenter by itself. Tusk lands on ArenaCenter (or on 0,0,0
//             if there is none).
// ANIMATOR    Uses Tusk's bools IsFlying and IsGrounded. Tusk's Animator needs a
//             transition Any State -> Flying (IsFlying = true), so the flying
//             pose shows from any state, including on the very first start.
// =============================================================================
public class GameFlow : MonoBehaviour
{
    private enum Phase { Intro, Playing, Respawning }

    [Header("References (all found automatically if left empty)")]
    [Tooltip("The empty object in the middle of the arena's top surface. " +
             "Tusk lands here. Without it she lands at 0,0,0.")]
    [SerializeField] private Transform arenaCenter;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Intro")]
    [Tooltip("How high above the landing spot Tusk starts (meters).")]
    [SerializeField] private float startHeight = 20f;
    [Tooltip("Seconds she takes to float down. It slows down as she lands.")]
    [SerializeField] private float descentTime = 4f;
    [Tooltip("How long the white takes to clear.")]
    [SerializeField] private float revealTime = 1.2f;
    [Tooltip("Seconds she stands still after landing, before the player has control.")]
    [SerializeField] private float landingPause = 0.5f;
    [Tooltip("Seconds the camera takes to glide from the intro view to the normal one.")]
    [SerializeField] private float cameraBlendTime = 1.5f;
    [Tooltip("The way Tusk faces when she lands. 180 = toward a camera at Yaw 0.")]
    [SerializeField] private float landingFaceAngle = 180f;

    [Header("Animation (optional)")]
    [Tooltip("Normally leave this EMPTY: the Animator should reach the flying " +
             "animation by itself (a transition Any State -> Flying when " +
             "IsFlying is true). Only if you type the exact name of Tusk's " +
             "flying state here does the intro force that state.")]
    [SerializeField] private string flyingStateName = "";

    [Header("Respawn")]
    [Tooltip("Meters below the platform before the respawn starts.")]
    [SerializeField] private float respawnDepth = 40f;
    [Tooltip("Seconds the screen takes to turn white.")]
    [SerializeField] private float whiteOutTime = 0.8f;
    [Tooltip("Seconds the screen stays fully white.")]
    [SerializeField] private float whiteHoldTime = 0.3f;

    private Phase phase;

    private Transform tusk;
    private CharacterController controller;
    private TuskMovement movement;
    private TuskDash dash;
    private TuskFlight flight;
    private TuskAttack attack;
    private TuskRecharge recharge;
    private TuskAudio tuskAudio;
    private SteamTank tank;
    private SteamKnockback knockback;
    private Animator animator;
    private ScreenFade fade;

    private readonly HashSet<string> animatorParameters = new HashSet<string>();
    private int flyingStateHash;
    private bool hasFlyingState;

    private Vector3 LandingPoint =>
        arenaCenter != null ? arenaCenter.position : Vector3.zero;

    private void Start()
    {
        if (!FindReferences())
        {
            enabled = false;
            return;
        }

        // Before the first frame is drawn: white screen, everything frozen.
        fade.SetAlpha(1f);
        SetPlayerControl(false);
        SetEnemiesActive(false);

        StartCoroutine(IntroSequence());
    }

    private void Update()
    {
        if (phase != Phase.Playing)
            return;

        if (tusk.position.y < LandingPoint.y - respawnDepth)
            StartCoroutine(RespawnSequence());
    }

    // ------------------------------------------------------------------
    // The intro
    // ------------------------------------------------------------------

    private IEnumerator IntroSequence()
    {
        phase = Phase.Intro;

        // One frame, so every other script has finished its own Start().
        yield return null;

        fade.SetAlpha(1f);
        SetPlayerControl(false);
        SetEnemiesActive(false);

        Vector3 landing = LandingPoint;
        Vector3 sky = landing + Vector3.up * startHeight;

        // The CharacterController is off, so moving Tusk directly is safe.
        tusk.position = sky;
        movement.FaceWorldAngle(landingFaceAngle);

        SetFlyingPose(true);

        if (flight != null)
            flight.ShowFlightEffects(true);

        cameraFollow.BeginIntro();

        // One more frame so the camera has jumped to its intro view.
        yield return null;

        // The white clears while Tusk comes down.
        StartCoroutine(fade.FadeTo(0f, revealTime));

        float elapsed = 0f;

        while (elapsed < descentTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / descentTime);

            // Ease-out: fast at first, very gentle at the end.
            float eased = 1f - (1f - t) * (1f - t);

            tusk.position = Vector3.Lerp(sky, landing, eased);

            yield return null;
        }

        tusk.position = landing;

        // Touch down.
        SetFlyingPose(false);

        if (flight != null)
            flight.ShowFlightEffects(false);

        movement.PlayLandingEffects();

        yield return new WaitForSeconds(landingPause);

        // Hand the game over to the player.
        movement.ResetState();
        SetPlayerControl(true);
        SetEnemiesActive(true);

        cameraFollow.EndIntro(cameraBlendTime);

        phase = Phase.Playing;
    }

    // ------------------------------------------------------------------
    // The respawn
    // ------------------------------------------------------------------

    private IEnumerator RespawnSequence()
    {
        phase = Phase.Respawning;

        // The screen turns white (Tusk keeps falling behind it).
        yield return StartCoroutine(fade.FadeTo(1f, whiteOutTime));
        yield return new WaitForSeconds(whiteHoldTime);

        ResetWorld();

        // Same start as the beginning of the game.
        yield return StartCoroutine(IntroSequence());
    }

    private void ResetWorld()
    {
        // Steam drops that are still lying around.
        foreach (SteamPickup pickup in
                 FindObjectsByType<SteamPickup>(FindObjectsSortMode.None))
        {
            Destroy(pickup.gameObject);
        }

        // Enemies go back to where they started, with full tanks.
        foreach (GearWheel wheel in
                 FindObjectsByType<GearWheel>(FindObjectsSortMode.None))
        {
            wheel.ResetToStart();
        }

        if (tank != null)
            tank.SetFull();

        if (knockback != null)
            knockback.ClearPush();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private bool FindReferences()
    {
        movement = FindFirstObjectByType<TuskMovement>();

        if (movement == null)
        {
            Debug.LogError(
                "GameFlow: no Tusk (TuskMovement) found in the scene.", this);
            return false;
        }

        tusk = movement.transform;
        controller = tusk.GetComponent<CharacterController>();
        tank = tusk.GetComponent<SteamTank>();
        knockback = tusk.GetComponent<SteamKnockback>();
        animator = tusk.GetComponentInChildren<Animator>();

        dash = FindFirstObjectByType<TuskDash>();
        flight = FindFirstObjectByType<TuskFlight>();
        attack = FindFirstObjectByType<TuskAttack>();
        recharge = FindFirstObjectByType<TuskRecharge>();
        tuskAudio = FindFirstObjectByType<TuskAudio>();

        if (cameraFollow == null)
            cameraFollow = FindFirstObjectByType<CameraFollow>();

        if (cameraFollow == null)
        {
            Debug.LogError("GameFlow: no CameraFollow found in the scene.", this);
            return false;
        }

        if (arenaCenter == null)
        {
            GameObject found = GameObject.Find("ArenaCenter");

            if (found != null)
                arenaCenter = found.transform;
        }

        // The white fade is added automatically.
        if (!TryGetComponent(out fade))
            fade = gameObject.AddComponent<ScreenFade>();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                animatorParameters.Add(parameter.name);

            FindFlyingState();
        }

        return true;
    }

    // Only used if a state name was typed in the Inspector: finds that state
    // so the intro can force it. Normally the Animator reaches the flying
    // animation by itself (Any State -> Flying when IsFlying is true).
    private void FindFlyingState()
    {
        hasFlyingState = false;

        if (string.IsNullOrWhiteSpace(flyingStateName))
            return;

        int hash = Animator.StringToHash(flyingStateName.Trim());

        if (animator.HasState(0, hash))
        {
            flyingStateHash = hash;
            hasFlyingState = true;
            return;
        }

        Debug.LogWarning(
            "GameFlow: the Animator has no state called '" + flyingStateName +
            "'. Check the spelling, or leave Flying State Name empty.", this);
    }

    // Turns Tusk's controls on or off. The shadow, audio and steam tank stay on.
    private void SetPlayerControl(bool on)
    {
        if (!on && tuskAudio != null)
            tuskAudio.StopAllLoops();

        if (controller != null)
            controller.enabled = on;

        SetEnabled(movement, on);
        SetEnabled(dash, on);
        SetEnabled(flight, on);
        SetEnabled(attack, on);
        SetEnabled(recharge, on);
    }

    private void SetEnemiesActive(bool active)
    {
        foreach (GearWheel wheel in
                 FindObjectsByType<GearWheel>(FindObjectsSortMode.None))
        {
            wheel.enabled = active;
        }
    }

    private static void SetEnabled(Behaviour behaviour, bool on)
    {
        if (behaviour != null)
            behaviour.enabled = on;
    }

    // Tells Tusk's Animator whether she is flying or standing.
    private void SetFlyingPose(bool flying)
    {
        SetAnimatorBool("IsFlying", flying);
        SetAnimatorBool("IsGrounded", !flying);

        SetAnimatorBool("IsJumping", false);
        SetAnimatorBool("IsDashing", false);
        SetAnimatorBool("IsHurt", false);
        SetAnimatorBool("IsRecharging", false);
        SetAnimatorBool("IsAttacking", false);

        if (animator != null && animatorParameters.Contains("Speed"))
            animator.SetFloat("Speed", 0f);

        // Go straight to the flying animation, whatever state she was in.
        if (flying && hasFlyingState)
            animator.CrossFadeInFixedTime(flyingStateHash, 0.15f, 0);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator != null && animatorParameters.Contains(parameterName))
            animator.SetBool(parameterName, value);
    }

    // Select this object to see where Tusk starts and lands.
    private void OnDrawGizmosSelected()
    {
        Vector3 landing = LandingPoint;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(landing, 0.5f);
        Gizmos.DrawLine(landing, landing + Vector3.up * startHeight);
        Gizmos.DrawWireSphere(landing + Vector3.up * startHeight, 0.5f);
    }
}
