using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private TuskMovement playerMovement;
    [SerializeField] private TuskAFK afk;

    [Header("Look Ahead")]
    [SerializeField] private float lookAheadDistance = 2f;
    [SerializeField] private float lookAheadSmoothness = 1f;

    [Header("AFK Zoom")]
    [SerializeField] private float afkZoomSize = 3f;
    [SerializeField] private float afkZoomSpeed = 2f;

    private Vector3 offset;
    private Vector3 currentLookAhead;

    private Camera cam;
    private float normalZoomSize;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        normalZoomSize = cam.orthographicSize;
    }

    private void Start()
    {
        offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        // LOOK AHEAD
        Vector3 targetLookAhead =
            playerMovement.MovementDirection * lookAheadDistance;

        currentLookAhead = Vector3.Lerp(
            currentLookAhead,
            targetLookAhead,
            lookAheadSmoothness * Time.deltaTime
        );

        // CAMERA POSITION
        transform.position =
            target.position + offset + currentLookAhead;

        // AFK ZOOM
        float targetZoom =
            afk != null && afk.IsAFK
                ? afkZoomSize
                : normalZoomSize;

        cam.orthographicSize = Mathf.MoveTowards(
            cam.orthographicSize,
            targetZoom,
            afkZoomSpeed * Time.deltaTime
        );
    }
}