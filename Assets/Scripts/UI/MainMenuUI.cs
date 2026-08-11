using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulsLike.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        public string gameSceneName = "no wolf";
        public string title = "ASHFALL";

        private void OnGUI()
        {
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.85f, 0.82f, 0.78f);
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 100), title, titleStyle);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            float buttonWidth = 240;
            float buttonHeight = 50;
            float x = Screen.width * 0.5f - buttonWidth * 0.5f;
            float y = Screen.height * 0.55f;

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Play", buttonStyle))
                SceneManager.LoadScene(gameSceneName);

            if (GUI.Button(new Rect(x, y + buttonHeight + 16, buttonWidth, buttonHeight), "Quit", buttonStyle))
                Quit();
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
