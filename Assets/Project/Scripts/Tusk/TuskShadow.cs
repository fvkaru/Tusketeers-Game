using UnityEngine;

// =============================================================================
// TuskShadow  (Scripts/Tusk)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Keeps a spinning gear 'shadow' on the ground under Tusk so the player
//   can judge where they will land. It shoots a ray straight down, finds the
//   closest upward-facing surface, and places the gear there. Over the void
//   (no ground) the gear is hidden.
//
// PUT IT ON   Tusk_Player.
// SLOTS       Shadow Gear (must NOT be a child that moves with Tusk's model),
//             Ground Mask (the layers that count as ground).
// =============================================================================
public class TuskShadow : MonoBehaviour
{
    [Header("Shadow")]
    [SerializeField] private Transform shadowGear;
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField] private float rotationSpeed = 20f;

    [Header("Raycast")]
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private LayerMask groundMask;

    private const float RayStartHeight = 0.1f;
    private const float MinimumUpwardNormal = 0.5f;

    // Reused every frame so we don't create garbage.
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    private void Awake()
    {
        if (shadowGear == null)
        {
            Debug.LogError("TuskShadow: Shadow Gear is not assigned.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (TryFindSurface(out RaycastHit surface))
        {
            SetShadowVisible(true);

            shadowGear.position =
                surface.point + Vector3.up * surfaceOffset;

            shadowGear.Rotate(
                Vector3.up,
                rotationSpeed * Time.deltaTime,
                Space.Self
            );
        }
        else
        {
            SetShadowVisible(false);
        }
    }

    // Finds the closest upward-facing surface below Tusk.
    private bool TryFindSurface(out RaycastHit closestHit)
    {
        Vector3 rayOrigin = transform.position + Vector3.up * RayStartHeight;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            hitBuffer,
            raycastDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        closestHit = default;
        float closestDistance = Mathf.Infinity;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];

            // Only accept surfaces that face upward.
            if (hit.normal.y <= MinimumUpwardNormal)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private void SetShadowVisible(bool visible)
    {
        if (shadowGear.gameObject.activeSelf != visible)
            shadowGear.gameObject.SetActive(visible);
    }
}
