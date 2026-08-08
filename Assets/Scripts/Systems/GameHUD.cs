using UnityEngine;
using SoulsLike.Systems;

namespace SoulsLike.UI
{
    /// <summary>
    /// Minimal IMGUI health/stamina bars so the prototype is playable without
    /// building a Canvas by hand. Swap for real UI once art direction is locked.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public Health playerHealth;
        public Stamina playerStamina;

        private void OnGUI()
        {
            if (playerHealth == null || playerStamina == null) return;

            const int barWidth = 260;
            const int barHeight = 18;
            int x = 24;
            int y = Screen.height - 70;

            DrawBar(x, y, barWidth, barHeight, playerHealth.NormalizedHealth, new Color(0.65f, 0.08f, 0.08f));
            DrawBar(x, y + barHeight + 6, barWidth, barHeight - 6, playerStamina.Normalized, new Color(0.75f, 0.65f, 0.15f));

            if (playerHealth.IsDead)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 42,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = new Color(0.6f, 0.05f, 0.05f);
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 80), "YOU DIED", style);
            }
        }

        private void DrawBar(int x, int y, int width, int height, float normalized, Color color)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), Texture2D.whiteTexture);
            GUI.color = new Color(0.15f, 0.15f, 0.15f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(normalized), height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
