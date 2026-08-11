using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine.Networking;

public class FCCServerWindow : EditorWindow
{
    private Process serverProcess;
    private string serverOutput = "";
    private bool isServerRunning = false;
    private string port = "8000";
    private string directory = "";
    private Vector2 scrollPosition;
    private bool autoScroll = true;

    private string fccBaseUrl = "http://127.0.0.1:8082";
    private string fccRoute = "/health";
    private string fccPayload = "{\"message\":\"ping\"}";
    private string fccStatus = "Idle";
    private string fccLastResponse = "";
    private bool isFccRequestInFlight = false;
    private UnityWebRequest currentFccRequest;

    [MenuItem("Window/FCC Server")]
    public static void ShowWindow()
    {
        GetWindow<FCCServerWindow>("FCC Server");
    }

    private void OnEnable()
    {
        directory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        fccBaseUrl = EditorPrefs.GetString("FCCServerWindow.FccBaseUrl", "http://127.0.0.1:8082");
        fccRoute = EditorPrefs.GetString("FCCServerWindow.FccRoute", "/health");
        fccPayload = EditorPrefs.GetString("FCCServerWindow.FccPayload", "{\"message\":\"ping\"}");
    }

    private void OnGUI()
    {
        GUILayout.Label("FCC Server Control", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Port:", GUILayout.Width(40));
        port = EditorGUILayout.TextField(port);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Directory:", GUILayout.Width(60));
        directory = EditorGUILayout.TextField(directory);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selectedFolder = EditorUtility.OpenFolderPanel("Select Server Directory", directory, "");
            if (!string.IsNullOrEmpty(selectedFolder))
                directory = selectedFolder;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (isServerRunning)
        {
            if (GUILayout.Button("Stop Server"))
            {
                StopServer();
            }
        }
        else
        {
            if (GUILayout.Button("Start Server"))
            {
                StartServer();
            }
        }

        GUILayout.FlexibleSpace();

        autoScroll = EditorGUILayout.Toggle("Auto Scroll", autoScroll);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear Output"))
        {
            serverOutput = "";
        }

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("FCC Proxy", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Unity now targets the local FCC proxy at 127.0.0.1:8082 by default so the API key stays on the server side.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Base URL:", GUILayout.Width(80));
        fccBaseUrl = EditorGUILayout.TextField(fccBaseUrl);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Route:", GUILayout.Width(80));
        fccRoute = EditorGUILayout.TextField(fccRoute);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Payload:");
        fccPayload = EditorGUILayout.TextArea(fccPayload, GUILayout.MinHeight(80));

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(isFccRequestInFlight);
        if (GUILayout.Button("Test Connection"))
        {
            SendFccRequest("/health", "{\"message\":\"ping\"}");
        }
        if (GUILayout.Button("Send Request"))
        {
            SendFccRequest(fccRoute, fccPayload);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Status:", fccStatus);
        EditorGUILayout.LabelField("Last Response:", fccLastResponse);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        GUILayout.Label("Server Output:", EditorStyles.boldLabel);

        float outputHeight = position.height - 250;
        if (outputHeight < 100) outputHeight = 100;

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(outputHeight));
        EditorGUILayout.LabelField(serverOutput);
        EditorGUILayout.EndScrollView();

        if (autoScroll && Event.current.type == EventType.Repaint)
        {
            scrollPosition.y = float.MaxValue;
        }

        SaveFccPreferences();
    }

    private void SaveFccPreferences()
    {
        EditorPrefs.SetString("FCCServerWindow.FccBaseUrl", fccBaseUrl);
        EditorPrefs.SetString("FCCServerWindow.FccRoute", fccRoute);
        EditorPrefs.SetString("FCCServerWindow.FccPayload", fccPayload);
    }

    private void StartServer()
    {
        if (isServerRunning)
            return;

        if (!Directory.Exists(directory))
        {
            EditorUtility.DisplayDialog("Error", "Directory does not exist.", "OK");
            return;
        }

        try
        {
            string arguments = $"-m http.server {port}";
            serverProcess = new Process();
            serverProcess.StartInfo.FileName = "python";
            serverProcess.StartInfo.Arguments = arguments;
            serverProcess.StartInfo.WorkingDirectory = directory;
            serverProcess.StartInfo.UseShellExecute = false;
            serverProcess.StartInfo.RedirectStandardOutput = true;
            serverProcess.StartInfo.RedirectStandardError = true;
            serverProcess.StartInfo.CreateNoWindow = true;
            serverProcess.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            serverProcess.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);

            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();

            isServerRunning = true;
            serverOutput = $"Server started on port {port} in directory {directory}\n";
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to start server: {ex.Message}", "OK");
        }
    }

    private void StopServer()
    {
        if (!isServerRunning)
            return;

        if (serverProcess != null && !serverProcess.HasExited)
        {
            serverProcess.Kill();
            serverProcess.WaitForExit();
        }

        isServerRunning = false;
        serverOutput += "Server stopped.\n";
    }

    private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        if (!string.IsNullOrEmpty(outLine.Data))
        {
            EditorApplication.update += () =>
            {
                serverOutput += outLine.Data + "\n";
                if (serverOutput.Split('\n').Length > 1000)
                {
                    string[] lines = serverOutput.Split('\n');
                    serverOutput = string.Join("\n", lines, lines.Length - 1000, 1000);
                }
                Repaint();
            };
        }
    }

    private void SendFccRequest(string route, string payload)
    {
        if (isFccRequestInFlight)
        {
            fccStatus = "A request is already in progress.";
            return;
        }

        if (string.IsNullOrWhiteSpace(fccBaseUrl))
        {
            fccStatus = "Please configure a base URL for the FCC server.";
            return;
        }

        string normalizedBaseUrl = fccBaseUrl.TrimEnd('/');
        string normalizedRoute = string.IsNullOrWhiteSpace(route) ? "/" : (route.StartsWith("/") ? route : "/" + route);
        string url = normalizedBaseUrl + normalizedRoute;

        // Use GET for health checks, POST for actual payload requests
        string method = (route == "/health" || string.IsNullOrWhiteSpace(payload)) ? "GET" : "POST";
        
        currentFccRequest = new UnityWebRequest(url, method);
        
        if (method == "POST")
        {
            byte[] body = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            currentFccRequest.uploadHandler = new UploadHandlerRaw(body);
            currentFccRequest.SetRequestHeader("Content-Type", "application/json");
        }
        
        currentFccRequest.downloadHandler = new DownloadHandlerBuffer();
        currentFccRequest.SetRequestHeader("Accept", "application/json");

        isFccRequestInFlight = true;
        fccStatus = $"{method} {url}...";
        fccLastResponse = string.Empty;

        currentFccRequest.SendWebRequest();
        EditorApplication.update += HandleFccRequestCompletion;
    }

    private void HandleFccRequestCompletion()
    {
        if (!isFccRequestInFlight || currentFccRequest == null)
            return;

        if (!currentFccRequest.isDone)
            return;

        isFccRequestInFlight = false;
        EditorApplication.update -= HandleFccRequestCompletion;

        if (currentFccRequest.result == UnityWebRequest.Result.Success)
        {
            fccStatus = "Request succeeded.";
            fccLastResponse = currentFccRequest.downloadHandler.text;
        }
        else
        {
            fccStatus = $"Request failed: {currentFccRequest.error}";
            fccLastResponse = currentFccRequest.downloadHandler.text;
        }

        currentFccRequest.Dispose();
        currentFccRequest = null;
        Repaint();
    }

    private void OnDisable()
    {
        StopServer();
        if (currentFccRequest != null)
        {
            currentFccRequest.Abort();
            currentFccRequest.Dispose();
            currentFccRequest = null;
        }
    }

    private void OnDestroy()
    {
        StopServer();
    }
}