using UnityEngine;

/// <summary>
/// Where the menu's Talk button sends speech. Create one with
/// Assets > Create > InnerFrame > AI Proxy Settings, inside a Resources folder, named AIProxySettings.
/// Without one, the defaults below are used (a proxy on this computer).
/// </summary>
[CreateAssetMenu(menuName = "InnerFrame/AI Proxy Settings", fileName = "AIProxySettings")]
public class AIProxySettings : ScriptableObject
{
    [Tooltip("The proxy server, e.g. http://192.168.1.50:8000. On a Quest, localhost is the headset itself, so use the computer's IP.")]
    public string serverUrl = "http://localhost:8000";

    [Tooltip("Optional code the proxy checks (header X-Access-Code). It ships inside the app, so it only keeps out casual callers; never put an API key here.")]
    public string accessCode = "";

    [Tooltip("Longest recording, in seconds.")]
    public int maxSeconds = 20;

    static AIProxySettings current;

    public static AIProxySettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<AIProxySettings>("AIProxySettings");
            if (current == null) current = CreateInstance<AIProxySettings>();
            return current;
        }
    }
}
