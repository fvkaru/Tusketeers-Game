using UnityEngine;

public class GearSpin : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 20f;

    private void Update()
    {
        transform.Rotate(
            0f,
            0f,
            rotationSpeed * Time.deltaTime,
            Space.Self
        );
    }
}