using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private TuskMovement playerMovement;
    [SerializeField] private float lookAheadDistance = 2f;
    [SerializeField] private float lookAheadSmoothness = 5f;

    private Vector3 offset;
    private Vector3 currentLookAhead;

    private void Start()
    {
        offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        Vector3 targetLookAhead =
            playerMovement.MovementDirection * lookAheadDistance;

        currentLookAhead = Vector3.Lerp(
            currentLookAhead,
            targetLookAhead,
            lookAheadSmoothness * Time.deltaTime
        );

        transform.position =
            target.position + offset + currentLookAhead;
    }
}