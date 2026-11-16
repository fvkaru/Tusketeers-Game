using UnityEngine;

// =============================================================================
// PlayerHealthBar  (Scripts/UI)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Tusk's steam bar, at the bottom center of the screen. It shows the numbers
//   above the bar, lined up with its left edge, like "87/120":
//       current steam / maximum steam
//   Both numbers update by themselves: when Tusk takes damage the bar goes
//   down, and when her tank grows (a level up calls SteamTank.IncreaseMax) the
//   second number grows too ("100/100" becomes "120/120"). It shows no name.
//
//   Look: a brass frame, a dark sepia inside, and a fill that goes from beige
//   (full) to orange (low). The colors are a Gradient you can edit in the
//   Inspector: the LEFT end is an empty tank, the RIGHT end is a full tank.
//
// PUT IT ON   One empty object in the scene (for example called PlayerHealthBar).
// SLOTS       None needed: it finds Tusk's SteamTank by itself.
// NOTE        Drawn with Unity's simple OnGUI (temporary).
// =============================================================================
public class PlayerHealthBar : MonoBehaviour
{
    [Header("Size and Position")]
    [Tooltip("Bar width as a fraction of the screen width.")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float barWidth = 0.32f;
    [Tooltip("Bar height as a fraction of the screen height.")]
    [Range(0.01f, 0.06f)]
    [SerializeField] private float barHeight = 0.03f;
    [Tooltip("Distance from the bottom of the screen, as a fraction of the screen height.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float bottomMargin = 0.05f;

    [Header("Look (steampunk)")]
    [Tooltip("Fill color by how full the tank is. Left end = empty, right end = full.")]
    [SerializeField] private Gradient fillColors = UIBarDrawing.MakeGradient(
        (0.00f, new Color(0.82f, 0.32f, 0.06f)),   // rust orange
        (0.40f, new Color(0.92f, 0.58f, 0.16f)),   // amber
        (0.70f, new Color(0.90f, 0.74f, 0.46f)),   // tan
        (1.00f, new Color(0.93f, 0.84f, 0.66f))    // beige
    );
    [Tooltip("The metal frame around the bar.")]
    [SerializeField] private Color frameColor = new Color(0.78f, 0.60f, 0.28f, 1f);   // brass
    [Tooltip("The dark inside of the bar, behind the fill.")]
    [SerializeField] private Color barBackground = new Color(0.11f, 0.07f, 0.05f, 0.9f);   // sepia
    [SerializeField] private Color textColor = new Color(0.96f, 0.90f, 0.76f, 1f);   // cream
    [Tooltip("Optional: drag a font file (.ttf or .otf) here for the numbers.")]
    [SerializeField] private Font font;
    [Tooltip("How quickly the bar follows the real steam. Higher = faster.")]
    [SerializeField] private float fillSmoothing = 8f;

    private SteamTank tank;
    private float shownFill;
    private GUIStyle numberStyle;

    private void Start()
    {
        TuskMovement tusk = FindFirstObjectByType<TuskMovement>();

        if (tusk != null)
            tank = tusk.GetComponent<SteamTank>();

        if (tank == null)
        {
            Debug.LogWarning(
                "PlayerHealthBar: Tusk's SteamTank was not found.", this);

            enabled = false;
            return;
        }

        shownFill = tank.Normalized;
    }

    private void Update()
    {
        // The bar slides smoothly; the numbers are always exact.
        shownFill = Mathf.Lerp(
            shownFill,
            tank.Normalized,
            1f - Mathf.Exp(-fillSmoothing * Time.deltaTime)
        );
    }

    private void OnGUI()
    {
        if (tank == null || Event.current.type != EventType.Repaint)
            return;

        GUI.depth = -400;

        float width = Screen.width * barWidth;
        float height = Screen.height * barHeight;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height * (1f - bottomMargin) - height;
        float padding = Mathf.Max(2f, height * 0.15f);

        Color oldColor = GUI.color;

        UIBarDrawing.DrawBar(
            new Rect(x, y, width, height),
            shownFill,
            fillColors,
            frameColor,
            barBackground,
            1f
        );

        // The numbers, above the bar, lined up with its left edge.
        if (numberStyle == null)
        {
            numberStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerLeft,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
        }

        numberStyle.fontSize = Mathf.RoundToInt(height * 0.9f);

        if (font != null)
            numberStyle.font = font;

        // Round the current steam UP, so a living Tusk never shows 0.
        string text =
            Mathf.CeilToInt(tank.Current) + "/" + Mathf.RoundToInt(tank.Max);

        float textHeight = height * 1.6f;

        Rect numberRect = new Rect(
            x,
            y - textHeight - padding * 0.5f,
            width,
            textHeight
        );

        // A small shadow so the numbers are readable on any background.
        float shadow = Mathf.Max(1f, height * 0.06f);
        Rect shadowRect = numberRect;
        shadowRect.x += shadow;
        shadowRect.y += shadow;

        GUI.color = Color.white;

        numberStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(shadowRect, text, numberStyle);

        numberStyle.normal.textColor = textColor;
        GUI.Label(numberRect, text, numberStyle);

        GUI.color = oldColor;
    }
}
