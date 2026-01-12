// --- START OF FILE InternetManager.cs ---
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Runtime.InteropServices;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Manages connection state, persistent UI, and WebGL network events.
/// BEST PRACTICE: Attach this script to a root-level CANVAS GameObject.
/// This ensures the Offline Panel overlays all other scene UI.
/// </summary>
public class InternetManager : MonoBehaviourPunCallbacks
{
    public static InternetManager Instance { get; private set; }

    private enum ConnectionState { Online, Offline_GracePeriod, Offline_Failed }
    private ConnectionState currentState = ConnectionState.Online;

    // Static flags to persist state across scene reloads
    private static bool shouldAttemptAutoRejoin = false;
    private static string lastKnownRoomName;

    [Header("UI Configuration")]
    [Tooltip("The panel to show when offline. Must be a child of this GameObject's Canvas.")]
    public GameObject offlinePanel;
    [Tooltip("The text component to display status/countdown.")]
    public TextMeshProUGUI statusText;
    [Tooltip("Optional: specific sorting order to ensure this UI is always on top.")]
    [SerializeField] private int overlaySortingOrder = 30000;

    [Header("Reconnection Settings")]
    [Tooltip("Seconds to wait before declaring total failure.")]
    public float gracePeriod = 10f;

    private Coroutine gracePeriodCoroutine;
    private RoomManager roomManager;
    private CanvasGroup offlineCanvasGroup;

    // WebGL Plugin Imports
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsBrowserOnline();
    [DllImport("__Internal")]
    private static extern void RegisterOnlineOfflineCallbacks(string gameObjectName);
#endif

    void Awake()
    {
        // --- SINGLETON & PERSISTENCE ---
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // --- CANVAS SETUP ---
        // Ensure this overlay is always on top of other scene canvases
        Canvas c = GetComponent<Canvas>();
        if (c != null)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = overlaySortingOrder;
            c.targetDisplay = 0;
        }

        // Setup UI references
        if (offlinePanel != null)
        {
            offlineCanvasGroup = offlinePanel.GetComponent<CanvasGroup>();
            offlinePanel.SetActive(false); // Start hidden
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Register JS callbacks. Must match the GameObject name exactly.
        RegisterOnlineOfflineCallbacks(gameObject.name);
        
        // Initial Check
        bool isOnline = IsBrowserOnline() == 1;
        if (!isOnline) SetOfflineState(); // Trigger immediate UI if starting offline
#else
        // Editor fallback loop
        StartCoroutine(CheckInternetReachability());
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only run auto-rejoin logic in the Auth/Menu scene
        if (scene.name == "Auth")
        {
            roomManager = FindObjectOfType<RoomManager>();

            if (shouldAttemptAutoRejoin && !string.IsNullOrEmpty(lastKnownRoomName))
            {
                if (roomManager != null)
                {
                    StartCoroutine(AutoRejoinAfterSceneLoad());
                }
            }
            shouldAttemptAutoRejoin = false;
        }
    }

    private IEnumerator AutoRejoinAfterSceneLoad()
    {
        Debug.Log($"[InternetManager] Waiting to auto-rejoin room: '{lastKnownRoomName}'...");

        float waitTime = 20f;
        while (!PhotonNetwork.InLobby && waitTime > 0)
        {
            yield return new WaitForSeconds(0.5f);
            waitTime -= 0.5f;
        }

        if (PhotonNetwork.InLobby)
        {
            if (roomManager != null && roomManager.joinRoomNameInput != null)
            {
                roomManager.joinRoomNameInput.text = lastKnownRoomName;
                roomManager.SendMessage("OnJoinRoomSubmit", SendMessageOptions.DontRequireReceiver);
            }
        }
        else
        {
            Debug.LogError("[InternetManager] Lobby timeout. Cancelled auto-rejoin.");
        }
    }

    private IEnumerator CheckInternetReachability()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(3f);
            bool isReachable = Application.internetReachability != NetworkReachability.NotReachable;
            // Only toggle if state changes to avoid spam
            if (currentState == ConnectionState.Online && !isReachable) SetOfflineState();
            else if (currentState != ConnectionState.Online && isReachable) SetOnlineState(true);
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);

        // Treat unexpected Photon disconnects as internet issues
        if (currentState == ConnectionState.Online && cause != DisconnectCause.DisconnectByClientLogic)
        {
            Debug.LogWarning($"[InternetManager] Photon Disconnected ({cause}). Triggering Offline Mode.");
            SetOfflineState();
        }
    }

    // --- STATE MACHINE ---

    private void SetOnlineState(bool isOnline)
    {
        if (!isOnline) return; // Should not happen based on logic, but safety first

        if (currentState != ConnectionState.Online)
        {
            Debug.Log($"[InternetManager] Connection Restored. Recovering from {currentState}...");

            bool wasInGracePeriod = (currentState == ConnectionState.Offline_GracePeriod);

            if (gracePeriodCoroutine != null) StopCoroutine(gracePeriodCoroutine);

            currentState = ConnectionState.Online;

            // Hide UI
            if (offlinePanel != null) offlinePanel.SetActive(false);
            if (offlineCanvasGroup != null) offlineCanvasGroup.blocksRaycasts = false;

            // Logic: If we caught it early (Grace Period), try to rejoin. 
            // If it failed completely, user is already effectively kicked, so just reload Auth.
            shouldAttemptAutoRejoin = wasInGracePeriod;

            InitiateFullDisconnectAndSceneLoad();
        }
    }

    private void SetOfflineState()
    {
        if (currentState != ConnectionState.Online) return;

        Debug.LogWarning("[InternetManager] Connection LOST. Enabling Offline UI.");
        currentState = ConnectionState.Offline_GracePeriod;

        if (PhotonNetwork.InRoom)
        {
            lastKnownRoomName = PhotonNetwork.CurrentRoom.Name;
        }

        // 1. Show UI Immediately
        if (offlinePanel != null)
        {
            offlinePanel.SetActive(true);
            if (offlineCanvasGroup != null) offlineCanvasGroup.blocksRaycasts = true; // Block input to game

            if (statusText != null)
                statusText.text = "Connection Lost!\nAttempting to reconnect...";
        }

        // 2. Start Countdown
        if (gracePeriodCoroutine != null) StopCoroutine(gracePeriodCoroutine);
        gracePeriodCoroutine = StartCoroutine(GracePeriodCountdown());
    }

    private IEnumerator GracePeriodCountdown()
    {
        float timer = gracePeriod;
        while (timer > 0)
        {
            if (statusText != null)
                statusText.text = $"Connection Lost!\nReconnecting in {Mathf.CeilToInt(timer)}...";

            yield return new WaitForSecondsRealtime(1f);
            timer--;
        }

        // Timer expired
        Debug.LogError("[InternetManager] Grace period expired. Connection failed.");
        currentState = ConnectionState.Offline_Failed;

        if (statusText != null)
            statusText.text = "Connection Failed.\nPlease check your network.";
    }

    private void InitiateFullDisconnectAndSceneLoad()
    {
        StartCoroutine(SafeDisconnectAndLoadAuthScene());
    }

    private IEnumerator SafeDisconnectAndLoadAuthScene()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            while (PhotonNetwork.IsConnected) yield return null;
        }

        SceneManager.LoadScene("Auth");
    }

    #region WebGL Callbacks
    public void OnBrowserOnline(string unused) => SetOnlineState(true);
    public void OnBrowserOffline(string unused) => SetOfflineState();
    #endregion
}
// --- END OF FILE InternetManager.cs ---