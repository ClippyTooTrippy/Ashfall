using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NimClient : MonoBehaviour
{
    // TODO: Replace these with your real values from NIM docs
    private const string NIM_BASE_URL = "https://YOUR_NIM_BASE_URL_HERE";
    private const string MODEL = "YOUR_MODEL_HERE";

    // WARNING: Putting API keys in client-side Unity builds is not secure.
    // For quick testing it works; for production, proxy through your server.
    private const string NVIDIA_NIM_API_KEY = "YOUR_NVIDIA_NIM_API_KEY_HERE";

    [Header("Test Prompt")]
    public string prompt = "Write a short haiku about the ocean.";

    // Call this from Start or a UI button
    public void SendPrompt()
    {
        StartCoroutine(GenerateTextCoroutine(prompt));
    }

    private IEnumerator GenerateTextCoroutine(string userPrompt)
    {
        // Example OpenAI-compatible request body pattern
        // Some NIM deployments may differ—adjust fields to match the docs you’re using.
        var requestObj = new
        {
            model = MODEL,
            messages = new object[]
            {
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7
        };

        string json = JsonUtility.ToJson(requestObj);
        // JsonUtility can't serialize nested arrays/anonymous types well.
        // So we’ll use a manual JSON builder below instead.
        json = BuildOpenAIStyleChatRequestJson(MODEL, userPrompt);

        using (var request = new UnityWebRequest(NIM_BASE_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {NVIDIA_NIM_API_KEY}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"NIM request failed: {request.responseCode}\n{request.error}\n{request.downloadHandler.text}");
                yield break;
            }

            string responseText = request.downloadHandler.text;
            string generated = ExtractGeneratedTextFromResponse(responseText);

            Debug.Log($"NIM response:\n{responseText}");
            Debug.Log($"Generated text:\n{generated}");
        }
    }

    // Build JSON manually to avoid Unity JsonUtility limitations
    private string BuildOpenAIStyleChatRequestJson(string model, string userPrompt)
    {
        // Adjust endpoint path/fields if your NIM deployment differs.
        // This format matches common OpenAI chat completions style.
        return $@"
{{
  ""model"": ""{EscapeJson(model)}"",
  ""messages"": [
    {{ ""role"": ""user"", ""content"": ""{EscapeJson(userPrompt)}"" }}
  ],
  ""temperature"": 0.7
}}";
    }

    private string ExtractGeneratedTextFromResponse(string responseText)
    {
        // Common OpenAI-like structure:
        // { "choices": [ { "message": { "content": "..." } } ] }
        // We'll do a very small extraction fallback (not a full JSON parser).
        try
        {
            // If you prefer, swap this for a proper JSON parser (e.g., Newtonsoft).
            // Keeping it dependency-free here.
            int idx = responseText.IndexOf("\"content\"");
            if (idx < 0) return responseText;

            int firstQuote = responseText.IndexOf('"', idx + "\"content\"".Length);
            if (firstQuote < 0) return responseText;

            int secondQuote = responseText.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return responseText;

            // This is imperfect if content contains escaped quotes.
            // Better approach: use a JSON library.
            // We'll do a slightly safer approach:
            // Find the colon after "content", then parse the string after it.
            int colon = responseText.IndexOf(':', idx);
            if (colon < 0) return responseText;

            int start = responseText.IndexOf('"', colon);
            if (start < 0) return responseText;

            start++; // move past opening quote
            bool escaping = false;
            for (int i = start; i < responseText.Length; i++)
            {
                char c = responseText[i];
                if (escaping)
                {
                    escaping = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaping = true;
                    continue;
                }
                if (c == '"')
                {
                    return UnescapeJsonString(responseText.Substring(start, i - start));
                }
            }
        }
        catch { /* ignore */ }

        return responseText;
    }

    private string EscapeJson(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }

    private string UnescapeJsonString(string s)
    {
        if (s == null) return "";
        // Minimal unescape for common sequences
        return s.Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
    }

    private void Start()
    {
        // Auto-test; comment out if you want manual trigger
        SendPrompt();
    }
}
