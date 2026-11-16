using System.Collections.Generic;
using System.Text;
using UnityEngine;

// =============================================================================
// EnemyHealthBar  (Scripts/UI)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Shows ONE enemy's steam bar at the top center of the screen, with the
//   enemy's name UNDER the bar, lined up with the bar's right edge. Just the
//   name, nothing else.
//
//   Which enemy:
//     1. The enemy Tusk hit most recently stays on the bar for Hold Time
//        seconds. Hit a different enemy and the bar switches to that one.
//     2. Otherwise the bar shows the enemy closest to Tusk.
//     3. With no enemies in the scene, the bar fades away.
//
//   The name is the enemy's object name in the Hierarchy, with spaces added
//   ("GearWheel" becomes "Gear Wheel", "(Clone)" and copy numbers are removed).
//   Rename the object to change the name on the bar.
//
//   Look: a copper frame, a dark sepia inside and a red fill (editable
//   Gradient: left end = empty, right end = full), to stand out from Tusk's
//   beige-and-orange bar.
//
//   TARGET MARKER (optional): a flat model that sits on the floor under the
//   enemy whose bar is showing, like Tusk's gear shadow, so it is easy to tell
//   whose bar you are looking at. Give it a red tint to match the bar.
//
// PUT IT ON   One empty object in the scene (for example called EnemyHealthBar).
// SLOTS       None needed. Optional: Marker Object. An enemy is anything with a
//             SteamTank and a SteamKnockback that is not Tusk.
// NOTE        Drawn with Unity's simple OnGUI (temporary).
// =============================================================================
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Which Enemy")]
    [Tooltip("Seconds an enemy you just hit stays on the bar, before it goes " +
             "back to showing the closest enemy.")]
    [SerializeField] private float holdTime = 5f;
    [Tooltip("How often (in seconds) the list of enemies is refreshed.")]
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("Size and Position")]
    [Tooltip("Bar width as a fraction of the screen width.")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float barWidth = 0.32f;
    [Tooltip("Bar height as a fraction of the screen height.")]
    [Range(0.01f, 0.06f)]
    [SerializeField] private float barHeight = 0.025f;
    [Tooltip("Distance from the top of the screen, as a fraction of the screen height.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float topMargin = 0.04f;

    [Header("Look (steampunk)")]
    [Tooltip("Fill color by how full the enemy's tank is. Left end = empty, right end = full.")]
    [SerializeField] private Gradient fillColors = UIBarDrawing.MakeGradient(
        (0.00f, new Color(0.38f, 0.11f, 0.07f)),   // dark rust
        (0.50f, new Color(0.58f, 0.14f, 0.10f)),   // oxblood
        (1.00f, new Color(0.74f, 0.19f, 0.13f))    // brick red
    );
    [Tooltip("The metal frame around the bar.")]
    [SerializeField] private Color frameColor = new Color(0.66f, 0.38f, 0.22f, 1f);   // copper
    [Tooltip("The dark inside of the bar, behind the fill.")]
    [SerializeField] private Color barBackground = new Color(0.11f, 0.07f, 0.05f, 0.9f);   // sepia
    [SerializeField] private Color textColor = new Color(0.96f, 0.90f, 0.76f, 1f);   // cream
    [Tooltip("Optional: drag a font file (.ttf or .otf) here for the name.")]
    [SerializeField] private Font font;
    [Tooltip("How quickly the bar follows the real steam. Higher = faster.")]
    [SerializeField] private float fillSmoothing = 8f;

    [Header("Target Marker (optional)")]
    [Tooltip("A flat model that sits on the floor under the enemy whose bar is " +
             "showing, like Tusk's gear shadow. Drag in a copy of the gear " +
             "(a scene object or a prefab) with a red tint.")]
    [SerializeField] private GameObject markerObject;
    [SerializeField] private float markerSpinSpeed = 20f;
    [Tooltip("How far above the floor the marker sits (meters).")]
    [SerializeField] private float markerHeightOffset = 0.03f;
    [Tooltip("Layers that count as floor. Everything is fine: Tusk and enemies " +
             "are ignored automatically.")]
    [SerializeField] private LayerMask markerGroundLayers = ~0;

    private Transform tusk;
    private TuskAttack tuskAttack;

    private readonly List<SteamKnockback> enemies = new List<SteamKnockback>();
    private float refreshTimer;

    private SteamKnockback lastHitEnemy;
    private float lastHitTime = -100f;

    private SteamKnockback target;
    private SteamTank targetTank;
    private string targetName = "";

    private float shownFill;
    private float alpha;

    private GUIStyle nameStyle;

    private GameObject marker;
    private readonly RaycastHit[] markerHits = new RaycastHit[8];

    private void Start()
    {
        TuskMovement tuskMovement = FindFirstObjectByType<TuskMovement>();

        if (tuskMovement != null)
            tusk = tuskMovement.transform;

        tuskAttack = FindFirstObjectByType<TuskAttack>();

        if (tuskAttack != null)
            tuskAttack.HitLanded += OnEnemyHit;

        SetupMarker();
        RefreshEnemies();
    }

    private void OnDestroy()
    {
        if (tuskAttack != null)
            tuskAttack.HitLanded -= OnEnemyHit;
    }

    private void OnEnemyHit(SteamKnockback enemy)
    {
        lastHitEnemy = enemy;
        lastHitTime = Time.time;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        refreshTimer -= deltaTime;

        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            RefreshEnemies();
        }

        SteamKnockback wanted = ChooseTarget();

        if (wanted != target)
            SetTarget(wanted);

        alpha = Mathf.MoveTowards(alpha, target != null ? 1f : 0f, 4f * deltaTime);

        if (target != null && targetTank != null)
        {
            shownFill = Mathf.Lerp(
                shownFill,
                targetTank.Normalized,
                1f - Mathf.Exp(-fillSmoothing * deltaTime)
            );
        }
    }

    // The marker follows AFTER the enemies have moved this frame.
    private void LateUpdate()
    {
        UpdateMarker();
    }

    // ------------------------------------------------------------------
    // Choosing the enemy
    // ------------------------------------------------------------------

    private SteamKnockback ChooseTarget()
    {
        // The enemy you hit most recently stays on the bar for a while.
        if (lastHitEnemy != null && Time.time - lastHitTime <= holdTime)
            return lastHitEnemy;

        return FindClosestEnemy();
    }

    private SteamKnockback FindClosestEnemy()
    {
        SteamKnockback closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (SteamKnockback enemy in enemies)
        {
            if (enemy == null)
                continue;

            float distance = 0f;

            if (tusk != null)
            {
                Vector3 offset = enemy.transform.position - tusk.position;
                offset.y = 0f;
                distance = offset.magnitude;
            }

            if (closest == null || distance < closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }

        return closest;
    }

    // Collects every enemy: anything with a SteamTank and SteamKnockback
    // that is not Tusk.
    private void RefreshEnemies()
    {
        enemies.Clear();

        foreach (SteamKnockback knockback in
                 FindObjectsByType<SteamKnockback>(FindObjectsSortMode.None))
        {
            if (knockback.TryGetComponent<TuskMovement>(out _))
                continue;

            if (!knockback.TryGetComponent<SteamTank>(out _))
                continue;

            enemies.Add(knockback);
        }
    }

    private void SetTarget(SteamKnockback newTarget)
    {
        target = newTarget;

        if (target == null)
        {
            targetTank = null;
            return;
        }

        targetTank = target.GetComponent<SteamTank>();
        targetName = CleanName(target.gameObject.name);

        // Jump to the new enemy's value instead of sliding from the old one.
        shownFill = targetTank != null ? targetTank.Normalized : 0f;
    }

    // "GearWheel (Clone)" -> "Gear Wheel", "Test_Dummy (2)" -> "Test Dummy"
    private static string CleanName(string rawName)
    {
        string name = rawName.Replace("(Clone)", "").Trim();

        // Unity adds " (1)", " (2)"... to copies.
        int paren = name.LastIndexOf('(');

        if (paren > 0 && name.EndsWith(")"))
            name = name.Substring(0, paren).Trim();

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            if (c == '_')
            {
                builder.Append(' ');
                continue;
            }

            // A capital letter after a small letter or number starts a new word.
            if (i > 0 && char.IsUpper(c) &&
                (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    // ------------------------------------------------------------------
    // The floor marker
    // ------------------------------------------------------------------

    private void SetupMarker()
    {
        if (markerObject == null)
            return;

        // A scene object is used as it is. A prefab (a file in the Project
        // window) is copied into the scene.
        marker = markerObject.scene.IsValid()
            ? markerObject
            : Instantiate(markerObject);

        // A solid collider on the marker could block Tusk or the camera.
        foreach (Collider c in marker.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        marker.SetActive(false);
    }

    // Puts the marker on the floor under the shown enemy, spinning slowly.
    // Hidden when nothing is shown, or when the enemy is over the void.
    private void UpdateMarker()
    {
        if (marker == null)
            return;

        Vector3 floorPoint = default;

        bool show =
            target != null &&
            alpha > 0.5f &&
            TryFindFloor(target.transform.position, out floorPoint);

        if (marker.activeSelf != show)
            marker.SetActive(show);

        if (!show)
            return;

        marker.transform.position = floorPoint + Vector3.up * markerHeightOffset;

        marker.transform.Rotate(
            Vector3.up, markerSpinSpeed * Time.deltaTime, Space.World);
    }

    private bool TryFindFloor(Vector3 position, out Vector3 floorPoint)
    {
        int count = Physics.RaycastNonAlloc(
            position + Vector3.up,
            Vector3.down,
            markerHits,
            8f,
            markerGroundLayers,
            QueryTriggerInteraction.Ignore
        );

        float closest = Mathf.Infinity;
        floorPoint = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = markerHits[i];

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

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private void OnGUI()
    {
        if (alpha <= 0.01f ||
            target == null ||
            Event.current.type != EventType.Repaint)
        {
            return;
        }

        GUI.depth = -500;

        float width = Screen.width * barWidth;
        float height = Screen.height * barHeight;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height * topMargin;
        float padding = Mathf.Max(2f, height * 0.15f);

        Color oldColor = GUI.color;

        UIBarDrawing.DrawBar(
            new Rect(x, y, width, height),
            shownFill,
            fillColors,
            frameColor,
            barBackground,
            alpha
        );

        // The name, under the bar, lined up with its right edge.
        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
        }

        nameStyle.fontSize = Mathf.RoundToInt(height * 1.1f);

        if (font != null)
            nameStyle.font = font;

        GUI.color = Color.white;

        Rect nameRect = new Rect(
            x,
            y + height + padding * 0.5f,
            width,
            height * 1.6f
        );

        // A small shadow so the name is readable on any background.
        float shadow = Mathf.Max(1f, height * 0.06f);
        Rect shadowRect = nameRect;
        shadowRect.x += shadow;
        shadowRect.y += shadow;

        nameStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alpha);
        GUI.Label(shadowRect, targetName, nameStyle);

        nameStyle.normal.textColor = UIBarDrawing.WithAlpha(textColor, alpha);
        GUI.Label(nameRect, targetName, nameStyle);

        GUI.color = oldColor;
    }
}
