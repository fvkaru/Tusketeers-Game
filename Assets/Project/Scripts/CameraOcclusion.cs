using UnityEngine;

public class CameraOcclusion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Camera cam;

    [Header("Fade")]
    [SerializeField] private float transparentAlpha = 0.25f;
    [SerializeField] private float fadeSpeed = 5f;

    private Renderer targetRenderer;
    private Material material;
    private Color originalColor;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();

        material = targetRenderer.material;
        originalColor = material.color;
    }

    private void Update()
    {
        if (target == null || cam == null)
        {
            return;
        }

        Vector3 direction =
            target.position - cam.transform.position;

        float distance = direction.magnitude;

        Ray ray = new Ray(
            cam.transform.position,
            direction.normalized
        );

        bool isBlocking = false;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            distance
        ))
        {
            if (hit.collider.gameObject == gameObject)
            {
                isBlocking = true;
            }
        }

        float targetAlpha =
            isBlocking
                ? transparentAlpha
                : 1f;

        Color color = material.color;

        color.a = Mathf.MoveTowards(
            color.a,
            targetAlpha,
            fadeSpeed * Time.deltaTime
        );

        material.color = color;
    }
}