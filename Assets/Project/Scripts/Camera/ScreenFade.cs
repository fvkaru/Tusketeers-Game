using System.Collections;
using UnityEngine;

// =============================================================================
// ScreenFade  (Scripts/Game)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   A full-screen fade, white by default, like a cloud passing over the game.
//   GameFlow uses it for the start of the game and for the respawn.
//     Alpha        0 = clear, 1 = fully white
//     SetAlpha()   jump to a value at once
//     FadeTo()     smooth fade (use it inside a coroutine with 'yield return')
//
// PUT IT ON   Nothing to do: GameFlow adds it by itself.
// NOTE        Drawn with Unity's simple OnGUI, like SteamDebugBar. It works on
//             phones too; replace it with a UI Canvas image when the real UI exists.
// =============================================================================
public class ScreenFade : MonoBehaviour
{
    [SerializeField] private Color color = Color.white;

    /// <summary>0 = clear, 1 = fully covered.</summary>
    public float Alpha { get; private set; }

    public void SetAlpha(float alpha)
    {
        Alpha = Mathf.Clamp01(alpha);
    }

    public IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = Alpha;
        targetAlpha = Mathf.Clamp01(targetAlpha);

        if (duration <= 0f)
        {
            Alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Smooth start and end, so it feels like a soft cloud.
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            Alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        Alpha = targetAlpha;
    }

    private void OnGUI()
    {
        if (Alpha <= 0.001f || Event.current.type != EventType.Repaint)
            return;

        Color oldColor = GUI.color;

        GUI.depth = -1000;   // draw on top of everything
        GUI.color = new Color(color.r, color.g, color.b, Alpha);

        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture
        );

        GUI.color = oldColor;
    }
}
