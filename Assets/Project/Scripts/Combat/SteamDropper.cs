using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// SteamDropper  (Scripts/Combat)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   When an enemy is knocked off the arena, steam drops burst out of it and
//   arc through the air to land on the platform, around the spot where the
//   enemy went flying (not on the very rim: they land a little inside, so
//   Tusk can grab them without falling).
//
//   How it knows: it remembers the last place the enemy stood on the floor.
//   Once the enemy is clearly below that spot (Fall Depth) and not touching
//   the floor, it counts as fallen and the drops are released, once.
//
// PUT IT ON   Every enemy that should drop steam (needs a CharacterController).
// SLOT        Drop Prefab: your steam drop model (for example the running dust
//             model turned into a prefab). It can be any model:
//               - its colliders are switched off automatically,
//               - a SteamPickup is added automatically if it has none.
// TUNING      How many drops, how much steam each, where and how they land.
// =============================================================================
[RequireComponent(typeof(CharacterController))]
public class SteamDropper : MonoBehaviour
{
    [Header("Drops")]
    [Tooltip("Your steam drop model, as a prefab.")]
    [SerializeField] private GameObject dropPrefab;
    [SerializeField] private int dropCount = 3;
    [Tooltip("Steam each drop gives to Tusk.")]
    [SerializeField] private float steamPerDrop = 10f;

    [Header("Where They Land")]
    [Tooltip("Drops land within about this distance of the spot where the " +
             "enemy left the platform.")]
    [SerializeField] private float scatterRadius = 3f;
    [Tooltip("Drops stay at least this far from the rim of the platform.")]
    [SerializeField] private float edgeMargin = 1.2f;
    [Tooltip("Drops stay at least this far from each other.")]
    [SerializeField] private float minSpacing = 1.2f;
    [Tooltip("Layers that count as floor. Everything is fine: Tusk and " +
             "enemies are ignored automatically.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Flight")]
    [Tooltip("Seconds the drops take to fly to their landing spot.")]
    [SerializeField] private float flightTime = 0.9f;
    [Tooltip("How high the arc goes.")]
    [SerializeField] private float arcHeight = 3f;
    [Tooltip("How high above the floor the drops hover once landed.")]
    [SerializeField] private float hoverHeight = 0.7f;

    [Header("When It Counts As Fallen")]
    [Tooltip("The enemy must be this far below the last floor it touched.")]
    [SerializeField] private float fallDepth = 2.5f;

    // Landing spots must be at about the same height as where it stood.
    private const float MaxFloorHeightDifference = 1.5f;

    private readonly RaycastHit[] floorHits = new RaycastHit[8];
    private readonly List<Vector3> chosenSpots = new List<Vector3>();

    private CharacterController controller;
    private Vector3 lastGroundedPosition;
    private bool hasStoodOnFloor;
    private bool released;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
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
            Vector3 landing = FindLandingSpot();
            chosenSpots.Add(landing);

            SpawnDrop(start, landing + Vector3.up * hoverHeight);
        }
    }

    private void SpawnDrop(Vector3 start, Vector3 landing)
    {
        GameObject drop = Instantiate(dropPrefab, start, Quaternion.identity);

        // A solid collider on a drop would block Tusk. Pickups don't need any.
        foreach (Collider c in drop.GetComponentsInChildren<Collider>())
            c.enabled = false;

        if (!drop.TryGetComponent(out SteamPickup pickup))
            pickup = drop.AddComponent<SteamPickup>();

        pickup.Launch(start, landing, flightTime, arcHeight, steamPerDrop);
    }

    // Picks a random spot near where the enemy left the platform that has
    // floor under it, is not on the rim, and is not too close to other drops.
    private Vector3 FindLandingSpot()
    {
        const int attempts = 24;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            // Start close to the exit point, then search a bit wider.
            float radius = scatterRadius *
                Mathf.Lerp(0.4f, 1.4f, attempt / (float)(attempts - 1));

            Vector2 circle = Random.insideUnitCircle * radius;

            Vector3 candidate = lastGroundedPosition +
                new Vector3(circle.x, 0f, circle.y);

            if (!TryGetFloor(candidate, out Vector3 floor))
                continue;

            if (Mathf.Abs(floor.y - lastGroundedPosition.y) >
                MaxFloorHeightDifference)
            {
                continue;
            }

            if (!HasFloorAround(floor) || IsTooCloseToOtherDrops(floor))
                continue;

            return floor;
        }

        // Nothing good found: use the exit point itself.
        if (TryGetFloor(lastGroundedPosition, out Vector3 fallback))
            return fallback;

        return lastGroundedPosition;
    }

    // Floor on all four sides at Edge Margin = not on the rim.
    private bool HasFloorAround(Vector3 floor)
    {
        return TryGetFloor(floor + Vector3.forward * edgeMargin, out _) &&
               TryGetFloor(floor + Vector3.back * edgeMargin, out _) &&
               TryGetFloor(floor + Vector3.left * edgeMargin, out _) &&
               TryGetFloor(floor + Vector3.right * edgeMargin, out _);
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
    private bool TryGetFloor(Vector3 spot, out Vector3 floorPoint)
    {
        Vector3 origin = new Vector3(
            spot.x, lastGroundedPosition.y + 3f, spot.z);

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
