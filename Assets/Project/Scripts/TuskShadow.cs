using UnityEngine;

public class TuskShadow : MonoBehaviour
{
    [Header("Shadow")]
    [SerializeField] private Transform shadowGear;
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField] private float rotationSpeed = 20f;

    [Header("Raycast")]
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private LayerMask groundMask;

    private void Update()
    {
        Vector3 rayOrigin =
            transform.position + Vector3.up * 0.1f;

        Ray ray = new Ray(
            rayOrigin,
            Vector3.down
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            raycastDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit closestHit = default;
        bool foundSurface = false;
        float closestDistance = Mathf.Infinity;

        foreach (RaycastHit hit in hits)
        {
            // Only accept surfaces that face upward.
            if (hit.normal.y <= 0.5f)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundSurface = true;
            }
        }

        if (foundSurface)
        {
            shadowGear.gameObject.SetActive(true);

            shadowGear.position =
                closestHit.point +
                Vector3.up * surfaceOffset;

            shadowGear.Rotate(
                Vector3.up,
                rotationSpeed * Time.deltaTime,
                Space.Self
            );
        }
        else
        {
            shadowGear.gameObject.SetActive(false);
        }
    }
}