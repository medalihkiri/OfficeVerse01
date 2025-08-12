using System.Collections;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using Photon.Realtime;
using System.Collections.Generic;
using agora_gaming_rtc;
using TMPro;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;

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
            
            // Synchronize screen share states when entering the game
            foreach (PlayerController player in otherPlayers)
            {
                if (player != null && player.view != null && 
                    player.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isSharing) && 
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



            // Also check local player's screen share
            if (myPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object localIsSharing) && 
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
        SpatialRoom.OnScreenShareVisibilityChanged -= HandleScreenShareVisibility;
    }

    private void HandleScreenShareVisibility(uint playerViewId, bool isVisible)
    {
        if (myPlayer.view.ViewID == playerViewId)
        {
            return; // Screen sharer always sees their own screen share
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

    private bool IsInSameRoom(PlayerController otherPlayer)
    {
        return SpatialRoom.PlayersInRooms.TryGetValue(myPlayer.view.Owner.ActorNumber, out int myRoomId) &&
               SpatialRoom.PlayersInRooms.TryGetValue(otherPlayer.view.Owner.ActorNumber, out int otherRoomId) &&
               myRoomId == otherRoomId;
    }

    private PlayerController GetPlayerByViewId(uint viewId)
    {
        if (myPlayer.view.ViewID == viewId) return myPlayer;
        return otherPlayers.Find(p => p.view.ViewID == viewId);
    }

    public PlayerController GetPlayerByActorNumber(int actorNumber)
    {
        if (myPlayer.view.Owner.ActorNumber == actorNumber) return myPlayer;
        return otherPlayers.Find(p => p.view.Owner.ActorNumber == actorNumber);
    }

    public void JoinAgoraChannel()
    {
        if (agoraClientManager != null && agoraClientManager.mRtcEngine != null)
        {
            agoraClientManager.JoinChannel();
            
            // Initialize audio states for all existing players
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
        Debug.Log("Joined a room.");
        StartGame();
        
        // Check for active screen shares when joining
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (player.CustomProperties.TryGetValue("isScreenSharing", out object isScreenSharing) && (bool)isScreenSharing)
            {
                PlayerController sharingPlayer = GetPlayerByActorNumber(player.ActorNumber);
                if (sharingPlayer != null)
                {
                    // Force update screen share state and visibility
                    sharingPlayer.OnReceiveScreenShareState(true);
                    SetScreenShareObjectState(
                        (uint)sharingPlayer.view.ViewID,
                        sharingPlayer.view.Owner.NickName,
                        true
                    );

                    // Ensure the screen share surface is properly configured
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

        // If I'm the new player, initialize my states to off and sync with existing players
        if (PhotonNetwork.LocalPlayer == newPlayer)
        {
            if (myPlayer != null)
            {
                myPlayer.SendVideoState(false);
                myPlayer.SendAudioState(false);
                myPlayer.SendScreenShareState(false);
            }

            // Force sync states for all existing players
            foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (player != newPlayer)
                {
                    PlayerController otherPlayer = GetPlayerByActorNumber(player.ActorNumber);
                    if (otherPlayer != null)
                    {
                        // Force UI updates for all states
                        if (player.CustomProperties.TryGetValue("isAudioEnabled", out object isAudioEnabled))
                        {
                            otherPlayer.OnReceiveAudioState((bool)isAudioEnabled);
                            otherPlayer.playerAudioObject.SetActive((bool)isAudioEnabled);
                        }
                        otherPlayer.SyncVideoState();
                        otherPlayer.SyncScreenShareState();
                    }
                }
            }
            foreach (PlayerController player in otherPlayers)
            {
                if (player != null && player.view != null && !player.view.IsMine)
                {
                    // Sync video state
                    if (player.view.Owner.CustomProperties.TryGetValue("isVideoEnabled", out object isVideoEnabled))
                    {
                        player.OnReceiveVideoState((bool)isVideoEnabled);
                    }
                    else
                    {
                        player.OnReceiveVideoState(false);
                    }

                    // Sync audio state
                    if (player.view.Owner.CustomProperties.TryGetValue("isAudioEnabled", out object isAudioEnabled))
                    {
                        player.OnReceiveAudioState((bool)isAudioEnabled);
                    }
                    else
                    {
                        player.OnReceiveAudioState(false);
                    }

                    // Sync screen share state
                    if (player.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isScreenSharing))
                    {
                        player.OnReceiveScreenShareState((bool)isScreenSharing);
                        if ((bool)isScreenSharing)
                        {
                            UpdateScreenShareVisibility(player);
                        }
                    }
                    else
                    {
                        player.OnReceiveScreenShareState(false);
                    }
                }
            }
        }
        // If I'm an existing player, send my current states to the new player
        else if (myPlayer != null)
        {
            // Send video state
            if (myPlayer.view.Owner.CustomProperties.TryGetValue("isVideoEnabled", out object isVideoEnabled))
            {
                myPlayer.OnReceiveVideoState((bool)isVideoEnabled);
            }
            else
            {
                myPlayer.OnReceiveVideoState(false);
            }

            // Send audio state
            if (myPlayer.view.Owner.CustomProperties.TryGetValue("isAudioEnabled", out object isAudioEnabled))
            {
                myPlayer.OnReceiveAudioState((bool)isAudioEnabled);
            }
            else
            {
                myPlayer.OnReceiveAudioState(false);
            }

            // Send screen share state
            if (myPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isScreenSharing))
            {
                myPlayer.OnReceiveScreenShareState((bool)isScreenSharing);
            }
            else
            {
                myPlayer.OnReceiveScreenShareState(false);
            }
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // It is best practice to call the base method first.
        base.OnPlayerLeftRoom(otherPlayer);
        UpdateTotalPlayers();
        Debug.Log($"EVENT: Player '{otherPlayer.NickName}' left room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        // If the leaving player was screen sharing, clean up their screen share state
        if (otherPlayer.CustomProperties.TryGetValue("isScreenSharing", out object isSharing) && (bool)isSharing)
        {
            screenShareObject.SetActive(false);
            screenShareSurface.SetEnable(false);
        }

        // Find the PlayerController associated with the player who left.
        PlayerController playerToDestroy = null;
        foreach (var player in otherPlayers)
        {
            if (player != null && player.view.Owner.ActorNumber == otherPlayer.ActorNumber)
            {
                playerToDestroy = player;
                break;
            }
        }

        // If we found their controller, remove it from the list and destroy their GameObject.
        if (playerToDestroy != null)
        {
            otherPlayers.Remove(playerToDestroy);
            Destroy(playerToDestroy.gameObject);
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
            if (hitsCount == 0)
            {
                break;
            }
            spawnPoint = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY)); // Modify this to add some variation to the spawn point.
            currentTry++;
            Debug.Log("Bebra");
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
        PhotonNetwork.LeaveRoom();
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
            // Screen sharer always sees their own screen share
            screenShareObject.SetActive(enable);
            screenShareSurface.SetForUser(0);
            screenShareSurface.SetEnable(enable);
            screenSharePlayerNameText.text = $"{name} is screen sharing.";
            
            // Hide meeting view button if screen share is stopped
            if (!enable)
            {
                userControlsUI?.HideMeetingViewButton();
            }
            // Show meeting view button if screen share is restarted in map view
            else if (userControlsUI != null && userControlsUI.isMapViewActive)
            {
                userControlsUI.meetingViewButton.gameObject.SetActive(true);
            }
            return;
        }

        bool shouldShowScreenShare = ShouldShowScreenShare(sharingPlayer);
        screenShareObject.SetActive(enable && shouldShowScreenShare);
        if (enable)
        {
            screenShareSurface.SetForUser(uid);
            screenShareSurface.SetEnable(shouldShowScreenShare);
            screenSharePlayerNameText.text = $"{name} is screen sharing.";
            
            // Ensure Agora video surface is properly configured
            if (shouldShowScreenShare)
            {
                screenShareSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
                screenShareSurface.SetEnable(true);
            }
        }

        // Always handle meeting view button visibility for all players
        if (userControlsUI != null && userControlsUI.isMapViewActive)
        {
            // Show meeting view button if screen share is started or restarted
            if (enable)
            {
                userControlsUI.meetingViewButton.gameObject.SetActive(true);
            }
            // Hide meeting view button if screen share is stopped
            else
            {
                userControlsUI.HideMeetingViewButton();
            }
        }
    }

    public void UpdateScreenShareVisibility(PlayerController sharingPlayer)
    {
        if (sharingPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isSharing) && (bool)isSharing)
        {
            SetScreenShareObjectState(
                (uint)sharingPlayer.view.ViewID,
                sharingPlayer.view.Owner.NickName,
                true
            );
        }
    }

    public bool ShouldShowScreenShare(PlayerController sharingPlayer)
    {
        bool viewerInRoom = SpatialRoom.PlayersInAnyRoom.TryGetValue(myPlayer.view.Owner.ActorNumber, out bool isViewerInRoom) && isViewerInRoom;
        bool sharerInRoom = SpatialRoom.PlayersInAnyRoom.TryGetValue(sharingPlayer.view.Owner.ActorNumber, out bool isSharerInRoom) && isSharerInRoom;

        // If both players are not in any room, screen share is visible
        if (!viewerInRoom && !sharerInRoom) return true;

        // If both players are in rooms, they must be in the same room
        if (viewerInRoom && sharerInRoom)
        {
            return SpatialRoom.PlayersInRooms.TryGetValue(myPlayer.view.Owner.ActorNumber, out int viewerRoomId) &&
                   SpatialRoom.PlayersInRooms.TryGetValue(sharingPlayer.view.Owner.ActorNumber, out int sharerRoomId) &&
                   viewerRoomId == sharerRoomId;
        }

        // If one player is in a room and the other isn't, screen share is not visible
        return false;
    }

    public void SetInteractionMessage(bool enable, string text = "")
    {
        interactionMessage.SetActive(enable);
        interactionMessageText.text = text;
    }
}
