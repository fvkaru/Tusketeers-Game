using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// SteamDropper  (Scripts/Combat)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Makes an enemy drop steam for Tusk, in two situations:
//
//   1. WHEN IT IS HIT: a small steam drop pops out of it every time it is hit.
//      It holds a fraction of the steam the enemy just lost, and lands close to
//      the enemy, where Tusk's magnet pulls it in. Fighting keeps her topped
//      up, which rewards playing aggressively.
//
//   2. WHEN IT IS KNOCKED OFF THE ARENA: bigger drops burst out of it and arc
//      through the air to land on the platform around the spot where it left
//      (not on the very rim, so Tusk can grab them without falling).
//      How it knows: it remembers the last place the enemy stood on the floor.
//      Once the enemy is clearly below that spot (Fall Depth) and not touching
//      the floor, it counts as fallen and the drops are released, once.
//
// PUT IT ON   Every enemy that should drop steam (needs a CharacterController).
// SLOT        Drop Prefab: your steam drop model (for example the running dust
//             model turned into a prefab). It can be any model:
//               - its colliders are switched off automatically,
//               - a SteamPickup is added automatically if it has none.
// =============================================================================
[RequireComponent(typeof(CharacterController))]
public class SteamDropper : MonoBehaviour
{
    [Header("Drops")]
    [Tooltip("Your steam drop model, as a prefab.")]
    [SerializeField] private GameObject dropPrefab;

    [Header("Knocked Off The Arena")]
    [SerializeField] private int dropCount = 3;
    [Tooltip("Steam each of these drops gives to Tusk.")]
    [SerializeField] private float steamPerDrop = 10f;
    [Tooltip("Drops land within about this distance of the spot where the " +
             "enemy left the platform.")]
    [SerializeField] private float scatterRadius = 3f;
    [Tooltip("Drops stay at least this far from the rim of the platform.")]
    [SerializeField] private float edgeMargin = 1.2f;
    [Tooltip("Drops stay at least this far from each other.")]
    [SerializeField] private float minSpacing = 1.2f;
    [Tooltip("The enemy must be this far below the last floor it touched.")]
    [SerializeField] private float fallDepth = 2.5f;

    [Header("When It Is Hit")]
    [Tooltip("ON: every hit makes a small steam drop pop out of the enemy.")]
    [SerializeField] private bool dropWhenHit = true;
    [Tooltip("The drop holds this fraction of the steam the enemy just lost " +
             "(0.3 = 30%). Raise it to make fighting give more steam.")]
    [Range(0f, 1f)]
    [SerializeField] private float hitDropFraction = 0.3f;
    [Tooltip("A hit drop lands within this distance of the enemy (meters).")]
    [SerializeField] private float hitDropScatter = 1.5f;
    [Tooltip("Size of a hit drop compared to a normal drop (1 = same size).")]
    [SerializeField] private float hitDropScale = 0.6f;

    [Header("Flight")]
    [Tooltip("Seconds the big drops take to fly to their landing spot.")]
    [SerializeField] private float flightTime = 0.9f;
    [Tooltip("How high the big drops' arc goes.")]
    [SerializeField] private float arcHeight = 3f;
    [Tooltip("How high above the floor the drops hover once landed.")]
    [SerializeField] private float hoverHeight = 0.7f;

    [Header("Floor")]
    [Tooltip("Layers that count as floor. Everything is fine: Tusk and " +
             "enemies are ignored automatically.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    // Landing spots must be at about the same height as where it stood.
    private const float MaxFloorHeightDifference = 1.5f;

    private readonly RaycastHit[] floorHits = new RaycastHit[8];
    private readonly List<Vector3> chosenSpots = new List<Vector3>();

    private CharacterController controller;
    private SteamKnockback knockback;
    private Vector3 lastGroundedPosition;
    private bool hasStoodOnFloor;
    private bool released;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (TryGetComponent(out knockback))
            knockback.HitDetailed += OnHit;
    }

    private void OnDisable()
    {
        if (knockback != null)
            knockback.HitDetailed -= OnHit;
    }

    private void Update()
    {
        // Standing on the floor: remember where, and get ready again.
        if (controller.isGrounded)
        {
            lastGroundedPosition = transform.position;
            hasStoodOnFloor = true;
            released = false;
            return;
        }

        if (released || !hasStoodOnFloor)
            return;

        if (transform.position.y < lastGroundedPosition.y - fallDepth)
            ReleaseDrops();
    }

    // ------------------------------------------------------------------
    // Drops when hit
    // ------------------------------------------------------------------

    private void OnHit(Vector3 pushDirection, float steamLost)
    {
        if (!dropWhenHit || dropPrefab == null)
            return;

        float amount = steamLost * hitDropFraction;

        if (amount <= 0.01f)
            return;

        // A hit that sends the enemy flying still drops its steam near the
        // floor where it stood.
        Vector3 center =
            (controller.isGrounded || !hasStoodOnFloor)
                ? transform.position
                : lastGroundedPosition;

        chosenSpots.Clear();

        Vector3 landing = FindLandingSpot(center, hitDropScatter, false);
        Vector3 start = transform.position + Vector3.up * 0.8f;

        SpawnDrop(
            start,
            landing + Vector3.up * hoverHeight,
            amount,
            hitDropScale,
            flightTime * 0.6f,
            arcHeight * 0.5f
        );
    }

    // ------------------------------------------------------------------
    // Drops when knocked off
    // ------------------------------------------------------------------

    private void ReleaseDrops()
    {
        released = true;

        if (dropPrefab == null)
        {
            Debug.LogWarning("SteamDropper: no Drop Prefab assigned.", this);
            return;
        }

        chosenSpots.Clear();

        Vector3 start = transform.position;

        for (int i = 0; i < dropCount; i++)
        {
            Vector3 landing = FindLandingSpot(lastGroundedPosition, scatterRadius, true);
            chosenSpots.Add(landing);

            SpawnDrop(
                start,
                landing + Vector3.up * hoverHeight,
                steamPerDrop,
                1f,
                flightTime,
                arcHeight
            );
        }
    }

    private void SpawnDrop(
        Vector3 start,
        Vector3 landing,
        float amount,
        float scale,
        float duration,
        float arc)
    {
        GameObject drop = Instantiate(dropPrefab, start, Quaternion.identity);

        if (!Mathf.Approximately(scale, 1f))
            drop.transform.localScale *= scale;

        // A solid collider on a drop would block Tusk. Pickups don't need any.
        foreach (Collider c in drop.GetComponentsInChildren<Collider>())
            c.enabled = false;

        if (!drop.TryGetComponent(out SteamPickup pickup))
            pickup = drop.AddComponent<SteamPickup>();

        pickup.Launch(start, landing, duration, arc, amount);
    }

    // ------------------------------------------------------------------
    // Finding a place to land
    // ------------------------------------------------------------------

    // Picks a random spot near 'center' that has floor under it, is not too
    // close to other drops, and (optionally) is not on the rim of the platform.
    private Vector3 FindLandingSpot(Vector3 center, float radius, bool keepAwayFromRim)
    {
        const int attempts = 24;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            // Start close to the center, then search a bit wider.
            float searchRadius = radius *
                Mathf.Lerp(0.4f, 1.4f, attempt / (float)(attempts - 1));

            Vector2 circle = Random.insideUnitCircle * searchRadius;

            Vector3 candidate = center + new Vector3(circle.x, 0f, circle.y);

            if (!TryGetFloor(candidate, center.y, out Vector3 floor))
                continue;

            if (Mathf.Abs(floor.y - center.y) > MaxFloorHeightDifference)
                continue;

            if (keepAwayFromRim && !HasFloorAround(floor, center.y))
                continue;

            if (IsTooCloseToOtherDrops(floor))
                continue;

            return floor;
        }

        // Nothing good found: use the center itself.
        if (TryGetFloor(center, center.y, out Vector3 fallback))
            return fallback;

        return center;
    }

    // Floor on all four sides at Edge Margin = not on the rim.
    private bool HasFloorAround(Vector3 floor, float referenceY)
    {
        return TryGetFloor(floor + Vector3.forward * edgeMargin, referenceY, out _) &&
               TryGetFloor(floor + Vector3.back * edgeMargin, referenceY, out _) &&
               TryGetFloor(floor + Vector3.left * edgeMargin, referenceY, out _) &&
               TryGetFloor(floor + Vector3.right * edgeMargin, referenceY, out _);
    }

    private bool IsTooCloseToOtherDrops(Vector3 spot)
    {
        foreach (Vector3 other in chosenSpots)
        {
            if (Vector3.Distance(spot, other) < minSpacing)
                return true;
        }

        return false;
    }

    // Looks straight down at this spot for walkable floor.
    private bool TryGetFloor(Vector3 spot, float referenceY, out Vector3 floorPoint)
    {
        Vector3 origin = new Vector3(spot.x, referenceY + 3f, spot.z);

        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            floorHits,
            8f,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        float closest = Mathf.Infinity;
        floorPoint = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = floorHits[i];

            // Tusk and the enemies are not floor.
            if (hit.collider is CharacterController)
                continue;

            if (hit.normal.y < 0.5f)
                continue;

            if (hit.distance < closest)
            {
                closest = hit.distance;
                floorPoint = hit.point;
                found = true;
            }
        }

        return found;
    }
}
