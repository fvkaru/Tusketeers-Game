using UnityEngine;

// =============================================================================
// TestDummy  (Scripts/Enemies)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   TEMPORARY punching bag for testing hits. It falls with gravity, gets
//   pushed by hits, slowly recovers steam, is never stunned, and returns to
//   its starting spot (with a full tank) if it falls off the arena.
//   Delete it once real enemies exist. Real enemies can copy its movement
//   code (the push + upward hop) and add their own behavior.
//
// PUT IT ON   A Capsule with: Character Controller (remove the Capsule
//             Collider), SteamTank, SteamKnockback.
// =============================================================================
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(SteamTank))]
[RequireComponent(typeof(SteamKnockback))]
public class TestDummy : MonoBehaviour
{
    [SerializeField] private float gravity = -20f;

    [Header("Steam Recovery")]
    [SerializeField] private float steamRegenPerSecond = 3f;
    [Tooltip("Seconds after being hit before steam starts coming back.")]
    [SerializeField] private float regenDelayAfterHit = 2f;

    [Header("Respawn")]
    [SerializeField] private float respawnBelowY = -30f;

    private CharacterController controller;
    private SteamTank tank;
    private SteamKnockback knockback;

    private Vector3 startPosition;
    private float verticalVelocity;
    private float lastHitTime = -100f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        tank = GetComponent<SteamTank>();
        knockback = GetComponent<SteamKnockback>();

        // The dummy never acts, so stun would only block your test hits.
        knockback.DisableStun();

        startPosition = transform.position;
    }

    private void OnEnable()
    {
        knockback.Hit += OnHit;
    }

    private void OnDisable()
    {
        knockback.Hit -= OnHit;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (Time.time >= lastHitTime + regenDelayAfterHit)
            tank.Gain(steamRegenPerSecond * deltaTime);

        float upward = knockback.TakeUpwardImpulse();

        if (upward > 0f)
            verticalVelocity = Mathf.Max(verticalVelocity, upward);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * deltaTime;

        Vector3 move = knockback.PushVelocity;
        move.y = verticalVelocity;

        controller.Move(move * deltaTime);

        if (transform.position.y < respawnBelowY)
            Respawn();
    }

    private void OnHit(Vector3 direction)
    {
        lastHitTime = Time.time;
    }

    private void Respawn()
    {
        // The controller must be off while teleporting, or it fights the move.
        controller.enabled = false;
        transform.position = startPosition;
        controller.enabled = true;

        verticalVelocity = 0f;
        knockback.ClearPush();
        tank.SetFull();
    }
}
