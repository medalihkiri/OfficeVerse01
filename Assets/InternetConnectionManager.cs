using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class InternetConnectionManager : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    [Tooltip("URL to ping for internet connectivity check.")]
    [SerializeField] private string pingURL = "https://www.google.com/generate_204";

    [Tooltip("How often (in seconds) to check internet connection.")]
    [SerializeField] private float checkInterval = 3f;

    [Tooltip("How long to wait before considering the player fully disconnected.")]
    [SerializeField] private float maxDisconnectTime = 15f;

    [Tooltip("How long (in seconds) Photon should try to keep the connection alive when the app is in the background. Default is 60.")]
    [SerializeField] private float keepAliveInBackgroundTime = 60f;

    private float disconnectTimer = 0f;
    private bool isInternetAvailable = true;
    private bool isReconnecting = false;

    void Start()
    {
        // Set how long Photon will try to keep the connection alive in the background.
        PhotonNetwork.KeepAliveInBackground = keepAliveInBackgroundTime;
        StartCoroutine(CheckInternetLoop());
    }

    private IEnumerator CheckInternetLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            yield return CheckInternetStatus();
        }
    }

    private IEnumerator CheckInternetStatus()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(pingURL))
        {
            www.timeout = 3; // fail fast if no internet
            yield return www.SendWebRequest();

            bool connectionOk = !(www.result == UnityWebRequest.Result.ConnectionError ||
                                  www.result == UnityWebRequest.Result.ProtocolError);

            if (connectionOk)
            {
                if (!isInternetAvailable)
                {
                    Debug.Log("[Internet] Restored internet connection.");
                    isInternetAvailable = true;
                    disconnectTimer = 0f;

                    // Attempt Photon reconnection if we were disconnected
                    if (!PhotonNetwork.IsConnected && !isReconnecting)
                        StartCoroutine(TryReconnect());
                }
            }
            else
            {
                if (isInternetAvailable)
                {
                    Debug.LogWarning("[Internet] Lost internet connection.");
                    isInternetAvailable = false;
                }

                disconnectTimer += checkInterval;
                if (disconnectTimer >= maxDisconnectTime)
                {
                    Debug.LogError("[Internet] Max disconnect time exceeded. Logging out.");
                    HandlePermanentDisconnect();
                }
            }
        }
    }

    private IEnumerator TryReconnect()
    {
        isReconnecting = true;
        bool rejoinAttempted = false;

        // For WebGL, we rely on PhotonNetwork.KeepAliveInBackground set in Start().
        // The custom Start/StopPhotonBackgroundPings methods are not needed.

        while (!PhotonNetwork.IsConnected && isInternetAvailable && disconnectTimer < maxDisconnectTime)
        {
            if (!rejoinAttempted && !string.IsNullOrEmpty(PhotonNetwork.CurrentRoom?.Name))
            {
                Debug.Log("[Internet] Trying ReconnectAndRejoin...");
                if (!PhotonNetwork.ReconnectAndRejoin())
                {
                    Debug.LogWarning("[Internet] ReconnectAndRejoin failed. Trying simple Reconnect.");
                    PhotonNetwork.Reconnect();
                }
                rejoinAttempted = true;
            }
            else
            {
                Debug.Log("[Internet] Trying Reconnect to Master...");
                PhotonNetwork.Reconnect();
            }

            float waitTime = 0f;
            while (waitTime < 5f && !PhotonNetwork.IsConnected)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }

            if (PhotonNetwork.IsConnected)
            {
                Debug.Log("[Internet] Reconnected to Photon.");
                // After reconnecting, Photon automatically tries to rejoin the last room if ReconnectAndRejoin was successful.
                // A manual JoinRoom call is often not necessary and can sometimes cause issues.
                // We'll rely on Photon's callbacks to confirm the room join.
                break;
            }

            yield return new WaitForSeconds(2f); // wait before retrying
        }

        isReconnecting = false;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[Internet] Photon Disconnected: {cause}");
        // Only attempt to reconnect if the internet is available and we aren't already in the process.
        if (isInternetAvailable && !isReconnecting)
        {
            StartCoroutine(TryReconnect());
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[Internet] Reconnected to Photon Master.");
        disconnectTimer = 0f;
        // If we were in a room, PUN will automatically attempt to rejoin it after connecting to Master.
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[Internet] Successfully rejoined Photon room.");
        disconnectTimer = 0f; // Reset timer on successful rejoin
    }

    private void HandlePermanentDisconnect()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        // TODO: Show a UI message and go back to menu
        SceneManager.LoadScene("Auth");
    }
}