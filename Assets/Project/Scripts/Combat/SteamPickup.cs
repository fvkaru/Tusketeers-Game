using UnityEngine;

// =============================================================================
// SteamPickup  (Scripts/Combat)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   A steam drop. It hovers, spins and bobs. When Tusk gets close it is
//   collected and gives her steam. It is also pulled toward her a little
//   (magnet), so grabbing drops near an edge feels forgiving.
//
//   - It is NOT collected while Tusk's tank is full (so steam is not wasted).
//     Turn on "Collect When Full" to change that.
//   - If nobody collects it, it blinks and disappears after Lifetime seconds.
//   - It needs no collider: it measures the distance to Tusk by itself.
//
//   Launch() makes it fly in an arc to its landing spot first (used by
//   SteamDropper). A pickup placed by hand in the scene just works at once.
//
// PUT IT ON   You usually do not have to: SteamDropper adds it automatically
//             to the drop model. Add it to the model yourself only if you want
//             to tune these numbers per drop type.
// =============================================================================
public class SteamPickup : MonoBehaviour
{
    [Header("Steam")]
    [SerializeField] private float steamAmount = 10f;
    [Tooltip("OFF: drops wait until Tusk is missing some steam.")]
    [SerializeField] private bool collectWhenFull = false;

    [Header("Collecting")]
    [Tooltip("Collected when Tusk's body center is this close.")]
    [SerializeField] private float collectRadius = 1.2f;
    [Tooltip("Closer than this, the drop drifts toward Tusk.")]
    [SerializeField] private float magnetRadius = 3.5f;
    [SerializeField] private float magnetSpeed = 14f;

    [Header("Lifetime")]
    [Tooltip("Seconds before it disappears. 0 = never.")]
    [SerializeField] private float lifetime = 20f;
    [Tooltip("It blinks during the last seconds.")]
    [SerializeField] private float blinkTime = 3f;

    [Header("Look")]
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 3f;

    [Header("Sound (optional)")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private float collectVolume = 1f;

    private Transform tuskTransform;
    private SteamTank tuskTank;
    private Renderer[] renderers;

    private Vector3 basePosition;
    private float bobPhase;
    private float age;

    // Arc flight (used after Launch).
    private bool flying;
    private Vector3 flightFrom;
    private Vector3 flightTo;
    private float flightTimer;
    private float flightDuration = 1f;
    private float flightArc = 3f;

    private void Awake()
    {
        basePosition = transform.position;
        renderers = GetComponentsInChildren<Renderer>(true);
        bobPhase = Random.value * Mathf.PI * 2f;
    }

    private void Start()
    {
        TuskMovement tusk = FindFirstObjectByType<TuskMovement>();

        if (tusk == null)
            return;

        tuskTransform = tusk.transform;
        tuskTank = tusk.GetComponent<SteamTank>();
    }

    /// <summary>
    /// Sends the drop flying in an arc from one point to another.
    /// It cannot be collected until it lands.
    /// </summary>
    public void Launch(
        Vector3 from, Vector3 to, float duration, float arcHeight, float amount)
    {
        steamAmount = amount;

        flightFrom = from;
        flightTo = to;
        flightDuration = Mathf.Max(duration, 0.05f);
        flightArc = arcHeight;
        flightTimer = 0f;
        flying = true;

        transform.position = from;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (flying)
        {
            UpdateFlight(deltaTime);
            return;
        }

        age += deltaTime;

        if (lifetime > 0f && age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        UpdateBlink();

        transform.Rotate(0f, spinSpeed * deltaTime, 0f, Space.World);

        TryCollectOrAttract(deltaTime);

        transform.position = basePosition + Vector3.up *
            (Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight);
    }

    private void UpdateFlight(float deltaTime)
    {
        flightTimer += deltaTime / flightDuration;

        float t = Mathf.Clamp01(flightTimer);

        Vector3 position = Vector3.Lerp(flightFrom, flightTo, t);

        // A simple arc: highest in the middle of the flight.
        position.y += flightArc * 4f * t * (1f - t);

        transform.position = position;

        if (t >= 1f)
        {
            flying = false;
            basePosition = flightTo;
            age = 0f;
        }
    }

    private void TryCollectOrAttract(float deltaTime)
    {
        if (tuskTransform == null || tuskTank == null)
            return;

        if (!collectWhenFull && tuskTank.Current >= tuskTank.Max)
            return;

        Vector3 tuskCenter = tuskTransform.position + Vector3.up;
        float distance = Vector3.Distance(transform.position, tuskCenter);

        if (distance <= collectRadius)
        {
            Collect();
            return;
        }

        if (distance <= magnetRadius)
        {
            basePosition = Vector3.MoveTowards(
                basePosition, tuskCenter, magnetSpeed * deltaTime);
        }
    }

    private void Collect()
    {
        tuskTank.Gain(steamAmount);

        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(
                collectSound, transform.position, collectVolume);
        }

        Destroy(gameObject);
    }

    // Blink during the last seconds of its life.
    private void UpdateBlink()
    {
        if (lifetime <= 0f || age < lifetime - blinkTime)
            return;

        bool visible = ((int)(age * 8f) % 2) == 0;

        foreach (Renderer r in renderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, collectRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}
