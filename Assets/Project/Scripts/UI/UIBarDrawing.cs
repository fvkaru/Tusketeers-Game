using UnityEngine;

// =============================================================================
// UIBarDrawing  (Scripts/UI)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Shared drawing code for the steam bars (PlayerHealthBar, EnemyHealthBar),
//   so both have the same steampunk look:
//     - a metal frame (brass or copper),
//     - a dark sepia inside,
//     - a fill whose color comes from a Gradient (empty on the left of the
//       gradient, full on the right),
//     - a thin light strip along the top of the fill, like shine on metal.
//
// IT IS NOT A COMPONENT: nothing to add to the scene.
// =============================================================================
public static class UIBarDrawing
{
    /// <summary>
    /// Builds a color gradient from (position, color) pairs.
    /// Position 0 = empty bar, position 1 = full bar.
    /// </summary>
    public static Gradient MakeGradient(params (float time, Color color)[] stops)
    {
        GradientColorKey[] colorKeys = new GradientColorKey[stops.Length];

        for (int i = 0; i < stops.Length; i++)
            colorKeys[i] = new GradientColorKey(stops[i].color, stops[i].time);

        GradientAlphaKey[] alphaKeys =
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        };

        Gradient gradient = new Gradient();
        gradient.SetKeys(colorKeys, alphaKeys);

        return gradient;
    }

    /// <summary>
    /// Draws one bar. Call it from OnGUI. 'fill' goes from 0 to 1.
    /// </summary>
    public static void DrawBar(
        Rect rect,
        float fill,
        Gradient fillColors,
        Color frameColor,
        Color backgroundColor,
        float alpha)
    {
        fill = Mathf.Clamp01(fill);

        float frame = Mathf.Max(2f, rect.height * 0.14f);

        // The metal frame.
        GUI.color = WithAlpha(frameColor, alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        // The dark inside.
        Rect inner = new Rect(
            rect.x + frame,
            rect.y + frame,
            rect.width - frame * 2f,
            rect.height - frame * 2f
        );

        GUI.color = WithAlpha(backgroundColor, alpha);
        GUI.DrawTexture(inner, Texture2D.whiteTexture);

        // The fill.
        float gap = Mathf.Max(1f, rect.height * 0.05f);

        Rect fillRect = new Rect(
            inner.x + gap,
            inner.y + gap,
            (inner.width - gap * 2f) * fill,
            inner.height - gap * 2f
        );

        if (fillRect.width > 0.5f)
        {
            GUI.color = WithAlpha(fillColors.Evaluate(fill), alpha);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

            // A lighter strip along the top: shine on metal.
            Rect shine = new Rect(
                fillRect.x, fillRect.y, fillRect.width, fillRect.height * 0.35f);

            GUI.color = new Color(1f, 1f, 1f, 0.18f * alpha);
            GUI.DrawTexture(shine, Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, color.a * alpha);
    }
}
