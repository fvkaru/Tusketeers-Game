using UnityEngine;

public class TuskAFK : MonoBehaviour
{
    [Header("AFK")]
    [SerializeField] private float afkDelay = 10f;

    public bool IsAFK { get; private set; }

    private float afkTimer;

    private void Update()
    {
        afkTimer += Time.deltaTime;

        if (afkTimer >= afkDelay)
        {
            IsAFK = true;
        }
    }

    public void ResetAFK()
    {
        afkTimer = 0f;
        IsAFK = false;
    }
}