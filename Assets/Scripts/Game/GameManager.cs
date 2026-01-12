using System.Collections;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using Photon.Realtime;
using System.Collections.Generic;
using agora_gaming_rtc;
using TMPro;
using System.Linq;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;

    private bool isShuttingDown = false;

    public float minX, maxX;
    public float minY, maxY;
    public string playerPrefabName = "Player";

    public AgoraClientManager agoraClientManager;

    public TextMeshProUGUI roomNameText, totalPlayersText;

    public CameraController cameraController;
    public GameObject interactionMessage;
    public TextMeshProUGUI interactionMessageText;

    public GameObject screenShareObject;
    public VideoSurface screenShareSurface;
    public TextMeshProUGUI screenSharePlayerNameText;

    public PlayerController myPlayer;
    public List<PlayerController> otherPlayers;

    private Vector2 RandomStartPosition => new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            StartGame();
            Debug.Log("Joined a room.");

            if (GameChatManager.SP != null)
            {
                GameChatManager.SP.OnJoinedRoom();
            }

            foreach (PlayerController player in otherPlayers)
            {
                if (player != null && player.view != null &&
                    player.view.Owner.CustomProperties.TryGetValue(PlayerController.IS_SCREEN_SHARING_PROP, out object isSharing) &&
                    (bool)isSharing)
                {
                    bool shouldShowScreenShare = ShouldShowScreenShare(player);
                    SetScreenShareObjectState(
                        (uint)player.view.ViewID,
                        player.view.Owner.NickName,
                        shouldShowScreenShare
                    );
                }
            }

            if (myPlayer.view.Owner.CustomProperties.TryGetValue(PlayerController.IS_SCREEN_SHARING_PROP, out object localIsSharing) &&
                (bool)localIsSharing)
            {
                bool shouldShowScreenShare = ShouldShowScreenShare(myPlayer);
                SetScreenShareObjectState(
                    (uint)myPlayer.view.ViewID,
                    myPlayer.view.Owner.NickName,
                    shouldShowScreenShare
                );
            }
        }
        SpatialRoom.OnScreenShareVisibilityChanged += HandleScreenShareVisibility;
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        SpatialRoom.OnScreenShareVisibilityChanged -= HandleScreenShareVisibility;
    }

    private void HandleScreenShareVisibility(uint playerViewId, bool isVisible)
    {
        if (myPlayer.view.ViewID == playerViewId)
        {
            return;
        }

        screenShareObject.SetActive(isVisible);
        if (isVisible)
        {
            screenShareSurface.SetForUser(playerViewId);
            screenShareSurface.SetEnable(true);

            PlayerController sharingPlayer = GetPlayerByViewId(playerViewId);
            if (sharingPlayer != null)
            {
                screenSharePlayerNameText.text = $"{sharingPlayer.view.Owner.NickName} is screen sharing.";
            }
        }
    }

    private PlayerController GetPlayerByViewId(uint viewId)
    {
        if (myPlayer.view.ViewID == viewId) return myPlayer;
        return otherPlayers.Find(p => p != null && p.view.ViewID == viewId);
    }

    public PlayerController GetPlayerByActorNumber(int actorNumber)
    {
        if (myPlayer.view.Owner.ActorNumber == actorNumber) return myPlayer;
        return otherPlayers.Find(p => p != null && p.view.Owner.ActorNumber == actorNumber);
    }

    public void JoinAgoraChannel()
    {
        if (agoraClientManager != null && agoraClientManager.mRtcEngine != null)
        {
            agoraClientManager.JoinChannel();
            foreach (PlayerController player in otherPlayers)
            {
                if (player != null && player.view != null)
                {
                    player.SyncAudioState();
                }
            }
        }
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (player.CustomProperties.TryGetValue(PlayerController.IS_SCREEN_SHARING_PROP, out object isScreenSharing) && (bool)isScreenSharing)
            {
                PlayerController sharingPlayer = GetPlayerByActorNumber(player.ActorNumber);
                if (sharingPlayer != null)
                {
                    sharingPlayer.OnReceiveScreenShareState(true);
                    SetScreenShareObjectState(
                        (uint)sharingPlayer.view.ViewID,
                        sharingPlayer.view.Owner.NickName,
                        true
                    );

                    screenShareSurface.SetForUser((uint)sharingPlayer.view.ViewID);
                    screenShareSurface.SetEnable(true);
                    screenShareSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
                }
            }
        }
    }

    public override void OnLeftRoom()
    {
        StartCoroutine(LoadSceneAfterLeftRoom());
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        UpdateTotalPlayers();
        Debug.Log($"EVENT: Player '{newPlayer.NickName}' entered room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");

        StartCoroutine(AddNewPlayerController(newPlayer));

        if (PhotonNetwork.LocalPlayer == newPlayer)
        {
            if (myPlayer != null)
            {
                myPlayer.SendVideoState(false);
                myPlayer.SendAudioState(false);
                myPlayer.SendScreenShareState(false);
            }

            foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (player != newPlayer)
                {
                    PlayerController otherPlayer = GetPlayerByActorNumber(player.ActorNumber);
                    if (otherPlayer != null)
                    {
                        if (player.CustomProperties.TryGetValue(PlayerController.IS_AUDIO_ENABLED_PROP, out object isAudioEnabled))
                        {
                            otherPlayer.OnReceiveAudioState((bool)isAudioEnabled);
                            otherPlayer.playerAudioObject.SetActive((bool)isAudioEnabled);
                        }
                        otherPlayer.SyncVideoState();
                        otherPlayer.SyncScreenShareState();
                    }
                }
            }
        }
    }

    private IEnumerator AddNewPlayerController(Player newPlayer)
    {
        GameObject playerGameObject = null;
        float timeout = Time.time + 5f;

        while (playerGameObject == null && Time.time < timeout)
        {
            foreach (var pv in FindObjectsOfType<PhotonView>())
            {
                if (pv.OwnerActorNr == newPlayer.ActorNumber)
                {
                    playerGameObject = pv.gameObject;
                    break;
                }
            }
            yield return null;
        }

        if (playerGameObject != null)
        {
            PlayerController newController = playerGameObject.GetComponent<PlayerController>();
            if (newController != null && !otherPlayers.Contains(newController))
            {
                otherPlayers.Add(newController);
                newController.SyncAudioState(true);
                newController.SyncVideoState();
                newController.SyncScreenShareState();
            }
        }
        else
        {
            Debug.LogError($"Failed to find PlayerController for new player '{newPlayer.NickName}'.");
        }
    }
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        UpdateTotalPlayers();
        Debug.Log($"<color=orange>[OnPlayerLeftRoom]</color> Event for player '{otherPlayer.NickName}' (ActorNr: {otherPlayer.ActorNumber}).");

        if (otherPlayer == null) return;

        if (otherPlayer.CustomProperties.TryGetValue(PlayerController.IS_SCREEN_SHARING_PROP, out object isSharing) && (bool)isSharing)
        {
            Debug.Log($"'{otherPlayer.NickName}' was screen sharing. Deactivating screen share UI.");
            screenShareObject.SetActive(false);
            screenShareSurface.SetEnable(false);
        }

        // Find the PlayerController instance for the departing player
        PlayerController departingPlayerController = otherPlayers.FirstOrDefault(p => p != null && p.view != null && p.view.OwnerActorNr == otherPlayer.ActorNumber);

        if (departingPlayerController != null)
        {
            // --- MODIFICATION START: GHOST PLAYER CLEANUP ---
            // As the Master Client, we are responsible for cleaning up the objects of departed players.
            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"<color=cyan>[MasterClient]</color> Player '{otherPlayer.NickName}' left. Destroying their objects.");
                PhotonNetwork.Destroy(departingPlayerController.gameObject);
            }
            // --- MODIFICATION END ---

            // Remove the reference from our local list regardless.
            otherPlayers.Remove(departingPlayerController);
            Debug.Log($"<color=green>SUCCESS:</color> Removed '{otherPlayer.NickName}' from the 'otherPlayers' C# list.");
        }
        else
        {
            Debug.LogWarning($"Could not find PlayerController for departing player '{otherPlayer.NickName}' to clean up.");
        }
    }

    private IEnumerator LoadSceneAfterLeftRoom()
    {
        while (PhotonNetwork.InRoom || !PhotonNetwork.IsConnected)
        {
            yield return null;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void StartGame()
    {
        Camera.main.farClipPlane = 1000;
        Vector2 spawnPoint = RandomStartPosition;
        int maxTries = 99;
        int currentTry = 0;

        while (currentTry < maxTries)
        {
            List<RaycastHit2D> hits = new List<RaycastHit2D>();
            ContactFilter2D contactFilter2D = new ContactFilter2D();
            contactFilter2D.useTriggers = false;
            int hitsCount = Physics2D.CircleCast(spawnPoint, 0.2f, Vector2.zero, contactFilter2D, hits);
            if (hitsCount == 0) break;
            spawnPoint = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            currentTry++;
        }

        if (currentTry < maxTries)
        {
            GameObject go = PhotonNetwork.Instantiate(playerPrefabName, spawnPoint, Quaternion.identity);
            myPlayer = go.GetComponent<PlayerController>();
            cameraController.player = go;
        }
        else
        {
            Debug.LogError("Failed to find a valid spawn point after " + maxTries + " tries.");
        }

        otherPlayers.Clear();
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in allPlayers)
        {
            if (!player.view.IsMine)
            {
                otherPlayers.Add(player);
            }
        }

        roomNameText.text = PhotonNetwork.CurrentRoom.Name;
        UpdateTotalPlayers();
    }

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        SceneManager.LoadScene("Auth");
    }

    public void UpdateTotalPlayers()
    {
        totalPlayersText.text = $"{PhotonNetwork.CurrentRoom.PlayerCount}";
    }

    public void SetScreenShareObjectState(uint uid, string name, bool enable)
    {
        PlayerController sharingPlayer = GetPlayerByViewId(uid);
        if (sharingPlayer == null) return;

        var userControlsUI = FindObjectOfType<UserControlsUI>();

        if (myPlayer.view.ViewID == uid)
        {
            screenShareObject.SetActive(enable);
            screenShareSurface.SetForUser(0);
            screenShareSurface.SetEnable(enable);
            screenSharePlayerNameText.text = $"{name} is screen sharing.";

            if (!enable) userControlsUI?.HideMeetingViewButton();
            else if (userControlsUI != null && userControlsUI.isMapViewActive) userControlsUI.meetingViewButton.gameObject.SetActive(true);

            return;
        }

        bool shouldShowScreenShare = ShouldShowScreenShare(sharingPlayer);
        screenShareObject.SetActive(enable && shouldShowScreenShare);

        if (enable && shouldShowScreenShare)
        {
            screenShareSurface.SetForUser(uid);
            screenShareSurface.SetEnable(true);
            screenShareSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
            screenSharePlayerNameText.text = $"{name} is screen sharing.";
        }

        if (userControlsUI != null && userControlsUI.isMapViewActive)
        {
            userControlsUI.meetingViewButton.gameObject.SetActive(enable);
        }
        if (!enable)
        {
            userControlsUI?.HideMeetingViewButton();
        }
    }

    public void UpdateVideoVisibility(PlayerController videoPlayer)
    {
        if (videoPlayer.view.Owner.CustomProperties.TryGetValue(PlayerController.IS_VIDEO_ENABLED_PROP, out object isEnabled) && (bool)isEnabled)
        {
            videoPlayer.OnReceiveVideoState(true);
        }
    }

    public void UpdateScreenShareVisibility(PlayerController sharingPlayer)
    {
        if (sharingPlayer.view.Owner.CustomProperties.TryGetValue(PlayerController.IS_SCREEN_SHARING_PROP, out object isSharing) && (bool)isSharing)
        {
            SetScreenShareObjectState(
                (uint)sharingPlayer.view.ViewID,
                sharingPlayer.view.Owner.NickName,
                true
            );
        }
    }

    public bool ShouldShowVideo(PlayerController videoPlayer)
    {
        // --- BROADCAST LOGIC ---
        if (videoPlayer.view.Owner.CustomProperties.TryGetValue(PlayerController.IS_BROADCASTING_PROP, out object isBroadcasting) && (bool)isBroadcasting)
        {
            return true; // If broadcasting, always show video.
        }

        // --- EXISTING ROOM LOGIC ---
        bool viewerInRoom = SpatialRoom.PlayersInAnyRoom.TryGetValue(myPlayer.view.Owner.ActorNumber, out bool isViewerInRoom) && isViewerInRoom;
        bool videoPlayerInRoom = SpatialRoom.PlayersInAnyRoom.TryGetValue(videoPlayer.view.Owner.ActorNumber, out bool isVideoPlayerInRoom) && isVideoPlayerInRoom;

        if (!viewerInRoom && !videoPlayerInRoom) return true;

        if (viewerInRoom && videoPlayerInRoom)
        {
            return SpatialRoom.PlayersInRooms.TryGetValue(myPlayer.view.Owner.ActorNumber, out int viewerRoomId) &&
                   SpatialRoom.PlayersInRooms.TryGetValue(videoPlayer.view.Owner.ActorNumber, out int sharerRoomId) &&
                   viewerRoomId == sharerRoomId;
        }

        return false;
    }

    public bool ShouldShowScreenShare(PlayerController sharingPlayer)
    {
        // --- BROADCAST LOGIC ---
        if (sharingPlayer.view.Owner.CustomProperties.TryGetValue(PlayerController.IS_BROADCASTING_PROP, out object isBroadcasting) && (bool)isBroadcasting)
        {
            return true; // If broadcasting, always show screen share.
        }

        // --- EXISTING ROOM LOGIC ---
        bool viewerInRoom = SpatialRoom.PlayersInAnyRoom.TryGetValue(myPlayer.view.Owner.ActorNumber, out bool isViewerInRoom) && isViewerInRoom;
        bool sharerInRoom = SpatialRoom.PlayersInAnyRoom.TryGetValue(sharingPlayer.view.Owner.ActorNumber, out bool isSharerInRoom) && isSharerInRoom;

        if (!viewerInRoom && !sharerInRoom) return true;

        if (viewerInRoom && sharerInRoom)
        {
            return SpatialRoom.PlayersInRooms.TryGetValue(myPlayer.view.Owner.ActorNumber, out int viewerRoomId) &&
                   SpatialRoom.PlayersInRooms.TryGetValue(sharingPlayer.view.Owner.ActorNumber, out int sharerRoomId) &&
                   viewerRoomId == sharerRoomId;
        }

        return false;
    }

    public void SetInteractionMessage(bool enable, string text = "")
    {
        if (isShuttingDown || interactionMessage == null) return;

        interactionMessage.SetActive(enable);
        interactionMessageText.text = text;
    }
}