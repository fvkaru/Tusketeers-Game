using UnityEngine;

// =============================================================================
// SteamDebugBar  (Scripts/UI)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   TEMPORARY steam bar drawn on screen with Unity's simple OnGUI, so the
//   numbers can be watched while testing. Delete it when the real UI exists.
//
// SLOTS       Steam Tank to display. Add one per tank (Tusk, dummy) and give
//             each a different Position.
// =============================================================================
public class SteamDebugBar : MonoBehaviour
{
    [SerializeField] private SteamTank steamTank;
    [SerializeField] private Vector2 position = new Vector2(20f, 20f);
    [SerializeField] private Vector2 size = new Vector2(260f, 28f);

    private GUIStyle labelStyle;

    private void OnGUI()
    {
        if (steamTank == null)
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        Color oldColor = GUI.color;

        Rect back = new Rect(position, size);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(back, Texture2D.whiteTexture);

        float fill = steamTank.Normalized;

        Rect fillRect = new Rect(
            back.x + 3f,
            back.y + 3f,
            (back.width - 6f) * fill,
            back.height - 6f
        );

        GUI.color = steamTank.IsEmpty
            ? Color.red
            : Color.Lerp(
                new Color(1f, 0.55f, 0.1f),
                new Color(0.6f, 0.9f, 1f),
                fill
            );

        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.white;

        GUI.Label(
            back,
            steamTank.Current.ToString("0") + " / " +
            steamTank.Max.ToString("0"),
            labelStyle
        );

        GUI.color = oldColor;
    }
}
