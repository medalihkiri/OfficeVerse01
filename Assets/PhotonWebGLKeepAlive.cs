using UnityEngine;
using Photon.Pun;
using System.Runtime.InteropServices;
using TMPro;

public class PhotonWebGLKeepAlive : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void StartPhotonBackgroundPings();
    [DllImport("__Internal")] private static extern void StopPhotonBackgroundPings();
#endif

    [Header("UI Debug Logger")]
    [SerializeField] private TextMeshProUGUI logText;
    private static PhotonWebGLKeepAlive _instance;

    void Awake()
    {
        gameObject.name = "PhotonBridge"; // Required for JS SendMessage
        if (_instance == null) _instance = this;
    }

    void Start()
    {
        Log("PhotonWebGLKeepAlive started");

#if UNITY_WEBGL && !UNITY_EDITOR
        StartPhotonBackgroundPings();
        Log("JS KeepAlive started");
#endif
    }

    void OnDestroy()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        StopPhotonBackgroundPings();
        Log("JS KeepAlive stopped");
#endif
    }

    /// <summary>
    /// Called from JS while the tab is hidden.
    /// </summary>
    public void BackgroundTick()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.NetworkingClient != null)
        {
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendAcksOnly();
            Log("BackgroundTick -> ACK sent");
        }
        else
        {
            Log("BackgroundTick -> Not connected");
        }
    }

    /// <summary>
    /// Append text to on-screen log + console.
    /// </summary>
    public void Log(string msg)
    {
        string logEntry = $"[{System.DateTime.Now:HH:mm:ss}] {msg}";
        Debug.Log(logEntry);

        if (logText != null)
        {
            logText.text = logEntry + "\n" + logText.text;
            if (logText.text.Length > 2000) // prevent spam memory
                logText.text = logText.text.Substring(0, 2000);
        }
    }
}
