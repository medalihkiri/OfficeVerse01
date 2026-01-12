using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using TMPro;

/// <summary>
/// A persistent singleton that handles automatic reconnection to a Photon room
/// if a network error occurs while in-game. This script should be placed on a
/// persistent GameObject in your initial scene.
/// It inherits from MonoBehaviourPunCallbacks to get all Photon event callbacks.
/// </summary>
public class PhotonAutoRejoin : MonoBehaviourPunCallbacks // <-- FIX: Inherit from MonoBehaviourPunCallbacks
{
    public static PhotonAutoRejoin Instance;

    [Header("UI Feedback")]
    [Tooltip("The parent UI object that is enabled to show reconnection status.")]
    [SerializeField] private GameObject reconnectingPanel;
    [Tooltip("Optional text to display the current status, e.g., 'Connection lost. Reconnecting...'")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Reconnection Settings")]
    [Tooltip("The maximum number of times to attempt reconnection before giving up.")]
    [SerializeField] private int maxReconnectAttempts = 5;
    [Tooltip("The delay in seconds between each reconnection attempt.")]
    [SerializeField] private float reconnectAttemptDelay = 5.0f;

    [Header("Scene Management")]
    [Tooltip("The name of your main menu or lobby scene to load if reconnection fails permanently.")]
    [SerializeField] private string menuSceneName = "LobbyScene"; // <-- IMPORTANT: Set this to your menu scene name

    private bool _isReconnecting = false;
    private bool _wasInRoom = false;
    private int _reconnectAttempts = 0;

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Hide the UI panel at start
        if (reconnectingPanel != null)
        {
            reconnectingPanel.SetActive(false);
        }
    }

    // OnEnable and OnDisable are automatically handled by MonoBehaviourPunCallbacks
    // for registering/unregistering from Photon callbacks, so we don't need them here.

    #endregion

    #region Photon Callbacks (IConnectionCallbacks, IMatchmakingCallbacks, etc.)

    /// <summary>
    /// Called when the client is connected to the Master Server and ready for matchmaking.
    /// This is a good moment to see if we need to rejoin a room.
    /// </summary>
    public override void OnConnectedToMaster()
    {
        // If we are in the "reconnecting" state, it means we successfully re-established
        // a base connection, and now Photon will automatically try to rejoin the room.
        if (_isReconnecting)
        {
            UpdateStatus("Connection re-established. Rejoining room...");
            // Photon's ReconnectAndRejoin() automatically handles rejoining from here.
        }
    }

    /// <summary>
    /// This is the core callback that detects network interruptions.
    /// </summary>
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[PhotonAutoRejoin] Disconnected from server. Cause: {cause}");

        // If we were not in a room, or if this is a deliberate disconnect, do nothing.
        if (!_wasInRoom || cause == DisconnectCause.DisconnectByClientLogic)
        {
            _wasInRoom = false; // Reset state
            return;
        }

        // If we are already running the reconnect routine, don't start another one.
        if (_isReconnecting)
        {
            return;
        }

        // Only attempt to reconnect for recoverable, temporary network errors.
        switch (cause)
        {
            case DisconnectCause.Exception:
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.ExceptionOnConnect:
                Debug.Log("[PhotonAutoRejoin] A recoverable network error occurred. Starting reconnect process...");
                StartCoroutine(ReconnectRoutine());
                break;

            default:
                Debug.LogError($"[PhotonAutoRejoin] Unrecoverable disconnect cause: {cause}. Returning to menu.");
                HandlePermanentFailure("Connection lost. Please check your network.");
                break;
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[PhotonAutoRejoin] Successfully joined room.");
        _wasInRoom = true;

        // If we were in the process of reconnecting, this signifies success.
        if (_isReconnecting)
        {
            Debug.Log("[PhotonAutoRejoin] Rejoin successful!");
            if (PhotonNetwork.LocalPlayer.HasRejoined)
            {
                Debug.Log("[PhotonAutoRejoin] Player state has been restored by the server.");
            }

            // Cleanup and hide UI
            _isReconnecting = false;
            _reconnectAttempts = 0;
            if (reconnectingPanel != null)
            {
                reconnectingPanel.SetActive(false);
            }
        }
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[PhotonAutoRejoin] Left room.");
        // We are no longer in a room, so disable auto-rejoin logic until we join another one.
        _wasInRoom = false;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        // This can be called after a reconnect attempt if the room is no longer available
        // (e.g., PlayerTTL expired, room was closed).
        if (_isReconnecting)
        {
            Debug.LogError($"[PhotonAutoRejoin] Rejoin failed. Code: {returnCode}, Message: {message}. Returning to menu.");
            HandlePermanentFailure($"Could not rejoin room: {message}");
        }
    }

    #endregion

    #region Reconnection Logic

    private IEnumerator ReconnectRoutine()
    {
        _isReconnecting = true;
        _reconnectAttempts = 0;

        // Show the UI to the player
        if (reconnectingPanel != null)
        {
            reconnectingPanel.SetActive(true);
        }

        while (_reconnectAttempts < maxReconnectAttempts)
        {
            _reconnectAttempts++;
            UpdateStatus($"Connection lost. Reconnecting... (Attempt {_reconnectAttempts}/{maxReconnectAttempts})");
            Debug.Log($"[PhotonAutoRejoin] Attempting to reconnect and rejoin... Attempt {_reconnectAttempts}");

            // This is Photon's magic function that handles everything.
            if (PhotonNetwork.ReconnectAndRejoin())
            {
                Debug.Log("[PhotonAutoRejoin] ReconnectAndRejoin process initiated successfully.");
                // We've successfully started the process. Now we wait for callbacks
                // (OnConnectedToMaster, OnJoinedRoom, or OnJoinRoomFailed).
                yield break;
            }

            Debug.LogWarning("[PhotonAutoRejoin] ReconnectAndRejoin failed to start. Retrying after delay.");
            yield return new WaitForSeconds(reconnectAttemptDelay);
        }

        // If the loop completes, all attempts have failed.
        Debug.LogError("[PhotonAutoRejoin] All reconnection attempts failed.");
        HandlePermanentFailure("Failed to reconnect to the server.");
    }

    private void HandlePermanentFailure(string message)
    {
        _isReconnecting = false;
        _wasInRoom = false;

        // In case we are still connected somehow but not in a room, disconnect cleanly.
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        // Hide the reconnecting panel
        if (reconnectingPanel != null)
        {
            reconnectingPanel.SetActive(false);
        }

        // Here you could show a final "Disconnected" popup before loading the menu.

        // Load the main menu scene.
        SceneManager.LoadScene(menuSceneName);
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    #endregion
}