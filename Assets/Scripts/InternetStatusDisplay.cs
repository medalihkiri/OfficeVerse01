using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Runtime.InteropServices;

public class InternetStatusDisplay : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI statusText;

    [Header("Colors")]
    public Color unknownColor = Color.yellow;
    public Color onlineColor = Color.green;
    public Color offlineColor = Color.red;

    [Header("Ping (optional verification)")]
    [Tooltip("If true, the script will try a small GET to PingUrl to confirm connectivity.")]
    public bool usePing = false;
    public string pingUrl = "https://clients3.google.com/generate_204"; // small quick 204 response
    public float pingInterval = 5f;

    [Header("Auto refresh")]
    public float uiRefreshInterval = 0.5f;

    bool isOnline = false;
    bool lastKnownState = false;
    string lastMessage = "Unknown";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsBrowserOnline(); // returns 1 or 0

    [DllImport("__Internal")]
    private static extern void RegisterOnlineOfflineCallbacks(string gameObjectName);
#endif

    void Start()
    {
        if (statusText == null)
        {
            Debug.LogError("InternetStatusDisplay: statusText (TextMeshProUGUI) is not assigned.");
            enabled = false;
            return;
        }

        // Initial check and register callbacks if WebGL
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            // Register browser online/offline event listeners so we get instant callbacks.
            RegisterOnlineOfflineCallbacks(gameObject.name);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("InternetStatusDisplay: failed to register webgl callbacks: " + ex.Message);
        }

        // read initial navigator.onLine
        isOnline = (IsBrowserOnline() == 1);
#else
        isOnline = Application.internetReachability != NetworkReachability.NotReachable;
#endif

        lastKnownState = isOnline;
        UpdateUIImmediate(isOnline);

        // start polling coroutines
        StartCoroutine(UIRefreshLoop());
        if (usePing) StartCoroutine(PingLoop());
        else StartCoroutine(ReachabilityPollLoop());
    }

    IEnumerator UIRefreshLoop()
    {
        while (true)
        {
            UpdateUIImmediate(isOnline);
            yield return new WaitForSeconds(uiRefreshInterval);
        }
    }

    IEnumerator ReachabilityPollLoop()
    {
        // for non-ping mode we do a periodic reachability check (useful for non-webgl builds)
        while (true)
        {
            bool now;
#if UNITY_WEBGL && !UNITY_EDITOR
            now = (IsBrowserOnline() == 1);
#else
            now = Application.internetReachability != NetworkReachability.NotReachable;
#endif
            if (now != isOnline) OnConnectionStateChanged(now);
            yield return new WaitForSeconds(pingInterval);
        }
    }

    IEnumerator PingLoop()
    {
        var wait = new WaitForSeconds(pingInterval);
        while (true)
        {
            // small GET request to confirm real connectivity
            using (UnityWebRequest www = UnityWebRequest.Get(pingUrl))
            {
                www.timeout = 5; // seconds
                yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                bool requestError = www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError;
#else
                bool requestError = www.isNetworkError || www.isHttpError;
#endif
                bool success = !requestError && (www.responseCode == 200 || www.responseCode == 204);

                // On some hosts 302/200 may be okay — treat 200 or 204 as success, else still consider success if we got any response and no connection error.
                if (!requestError && (www.responseCode >= 200 && www.responseCode < 400))
                    success = true;

                if (success != isOnline)
                    OnConnectionStateChanged(success);
            }

            yield return wait;
        }
    }

    void OnConnectionStateChanged(bool online)
    {
        isOnline = online;
        // you can also raise Unity events here or call other systems
        if (isOnline != lastKnownState)
        {
            lastKnownState = isOnline;
            if (isOnline) OnBecameOnline();
            else OnBecameOffline();
        }
    }

    void OnBecameOnline()
    {
        lastMessage = "Online";
        Debug.Log("InternetStatusDisplay: Became ONLINE");
    }

    void OnBecameOffline()
    {
        lastMessage = "Offline";
        Debug.Log("InternetStatusDisplay: Became OFFLINE");
    }

    // Called from JS (WebGL) when browser fires 'online' event
    public void OnBrowserOnline(string unused)
    {
        OnConnectionStateChanged(true);
    }

    // Called from JS (WebGL) when browser fires 'offline' event
    public void OnBrowserOffline(string unused)
    {
        OnConnectionStateChanged(false);
    }

    void UpdateUIImmediate(bool online)
    {
        if (statusText == null) return;

        if (online)
        {
            statusText.text = "Internet: ONLINE";
            statusText.color = onlineColor;
        }
        else
        {
            statusText.text = "Internet: OFFLINE";
            statusText.color = offlineColor;
        }
    }

    // Optional helper for other scripts:
    public bool IsOnline() => isOnline;
}
