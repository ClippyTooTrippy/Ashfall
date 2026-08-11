FCC Server Window for Unity Editor

This script creates a Unity Editor window that allows you to:
- Start/stop a Python HTTP server for local development
- Configure port and directory
- View server output
- Test connectivity to the FCC proxy server at 127.0.0.1:8082

To use:
1. Place the FCCServerWindow.cs file in any Editor folder (e.g., Assets/Editor).
2. In Unity, go to Window > FCC Server to open the window.
3. Set the desired port and directory (defaults to project root).
4. Click "Start Server" to launch the Python HTTP server.
5. The server output will be displayed in the window.
6. Click "Stop Server" to stop the server.

FCC Proxy Configuration:
- Base URL: http://127.0.0.1:8082 (or your FCC server address)
- Route: /health (or any endpoint on your FCC server)
- Payload: JSON body for POST requests

HTTP Methods:
- Health checks (/health) use GET (compatible with Python's http.server)
- Custom routes with payload use POST

Requirements:
- Python must be installed and accessible in the system PATH (the script uses "python" command).
- If your system uses "python3", you may need to edit the script to change the FileName.
- For FCC proxy endpoints, ensure your FCC server is running on the configured port.

Notes:
- The server output is limited to the last 1000 lines to prevent excessive memory usage.
- Auto-scroll can be toggled.
- The server is automatically stopped when the window is closed or when entering play mode.
- FCC Base URL, Route, and Payload settings are saved and persisted in EditorPrefs.

Troubleshooting:
- If the test connection fails: ensure Python HTTP server is running and FCC server is accessible.
- If POST requests fail: make sure your FCC server endpoint supports POST method.
- Check the console for any error messages.

Enjoy!