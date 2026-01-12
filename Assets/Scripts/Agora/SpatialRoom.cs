using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using agora_gaming_rtc;
using FunkyCode;
using Photon.Realtime;
using UnityEngine.UI;
using Pathfinding;
using System.Linq;
using ExitGames.Client.Photon; // Required for Hashtable


public class SpatialRoom : MonoBehaviourPunCallbacks
{
    private UserControlsUI userControlsUI;

    private IRtcEngine agoraRtcEngine;
    private List<PlayerController> playersInside = new List<PlayerController>();
    private Light2D roomLight;
    private Light2D activeGlobalLight;
    //private Light2D disabledGlobalLight;
    public const string IN_ROOM_PROPERTY_KEY = "inRoom";

    public static Dictionary<int, int> PlayersInRooms = new Dictionary<int, int>();
    private static int nextRoomId = 1;
    private int roomId;

    private HashSet<PlayerController> playersWhoCanSeeScreenShare = new HashSet<PlayerController>();

    public static event System.Action<uint, bool> OnScreenShareVisibilityChanged;

    public static Dictionary<int, bool> PlayersInAnyRoom = new Dictionary<int, bool>();

    private bool isLocked = false;
    private BoxCollider2D roomCollider;
    private GameObject[] blockingColliders;
    private PhotonView photonView;
    private int lockingPlayerId = -1; // Store the actor number of the player who locked the room

    public bool IsLocked => isLocked;
    public int LockingPlayerId => lockingPlayerId;
    //public GameObject mainLight;

    private void Awake()
    {
        roomId = nextRoomId++;
        roomCollider = GetComponent<BoxCollider2D>();
        photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            Debug.LogError("PhotonView component is missing on SpatialRoom GameObject!");
        }
    }

    private void Start()
    {
        roomLight = GetComponent<Light2D>();
        activeGlobalLight = GameObject.Find("Global Light 2D On").GetComponent<Light2D>();
        //disabledGlobalLight = GameObject.Find("Global Light 2D Off").GetComponent<Light2D>();
        agoraRtcEngine = GameManager.Instance.agoraClientManager.mRtcEngine;

        // Find UserControlsUI - improved robustness
        if (userControlsUI == null)
        {
            userControlsUI = FindObjectOfType<UserControlsUI>();
            if (userControlsUI == null)
            {
                Debug.LogError("UserControlsUI not found in the scene!");
            }
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // When a player disconnects, remove them from our tracking dictionaries
        // to prevent "ghost" players from breaking the room logic.
        if (PlayersInRooms.ContainsKey(otherPlayer.ActorNumber))
        {
            PlayersInRooms.Remove(otherPlayer.ActorNumber);
        }
        if (PlayersInAnyRoom.ContainsKey(otherPlayer.ActorNumber))
        {
            PlayersInAnyRoom.Remove(otherPlayer.ActorNumber);
        }
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player))
        {
            if (player.view.IsMine)
            {
                var spatialAudio = player.GetComponent<SpatialAudio>();
                spatialAudio.SetInRoom(true, roomId);
                var props = new Hashtable { { IN_ROOM_PROPERTY_KEY, roomId } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                spatialAudio.ForceUpdateSpatialVolumes(); // <--- ADD THIS LINE
                ControlRoomLightAsync(true);
                ControlGlobalLightAsync(false);

                if (userControlsUI != null)
                {
                    userControlsUI.SetCurrentRoom(this);
                    userControlsUI.lockRoomButton.onClick.AddListener(ToggleRoomLock);
                }
            }
            playersInside.Add(player);
            PlayersInRooms[player.view.OwnerActorNr] = roomId;
            PlayersInAnyRoom[player.view.OwnerActorNr] = true;
            //UpdateAudioSettings();
            UpdateScreenShareVisibility(player, true);
            
            // Update video visibility for all players when someone enters a room
            /*foreach (PlayerController otherPlayer in GameManager.Instance.otherPlayers)
            {
                if (otherPlayer.view.Owner.CustomProperties.TryGetValue("isVideoEnabled", out object isEnabled) && (bool)isEnabled)
                {
                    otherPlayer.OnReceiveVideoState(true);
                }
                
                // Update screen share visibility when someone enters a room
                if (otherPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isSharing) && (bool)isSharing)
                {
                    GameManager.Instance.UpdateScreenShareVisibility(otherPlayer);
                }
            }*/

            // Also check local player's screen share
            if (GameManager.Instance.myPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object localIsSharing) && (bool)localIsSharing)
            {
                GameManager.Instance.UpdateScreenShareVisibility(GameManager.Instance.myPlayer);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player))
        {
            if (player.view.IsMine)
            {
                var spatialAudio = player.GetComponent<SpatialAudio>();
                spatialAudio.SetInRoom(false, 0);
                var props = new Hashtable { { IN_ROOM_PROPERTY_KEY, 0 } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                spatialAudio.ForceUpdateSpatialVolumes(); // <--- ADD THIS LINE
                ControlGlobalLightAsync(true);
                ControlRoomLightAsync(false);

                if (userControlsUI != null)
                {
                    userControlsUI.lockRoomButton.onClick.RemoveListener(ToggleRoomLock);
                    userControlsUI.SetCurrentRoom(null);
                }
            }

            playersInside.Remove(player);
            PlayersInRooms.Remove(player.view.OwnerActorNr);
            PlayersInAnyRoom[player.view.OwnerActorNr] = false;
            //UpdateAudioSettings();
            UpdateScreenShareVisibility(player, false);

            // Update video visibility for all players when someone leaves a room
            /*foreach (PlayerController otherPlayer in GameManager.Instance.otherPlayers)
            {
                if (otherPlayer.view.Owner.CustomProperties.TryGetValue("isVideoEnabled", out object isEnabled) && (bool)isEnabled)
                {
                    otherPlayer.OnReceiveVideoState(true);
                }
                
                // Update screen share visibility when someone leaves a room
                if (otherPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isSharing) && (bool)isSharing)
                {
                    GameManager.Instance.UpdateScreenShareVisibility(otherPlayer);
                }
            }*/

            // Also check local player's screen share
            if (GameManager.Instance.myPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object localIsSharing) && (bool)localIsSharing)
            {
                GameManager.Instance.UpdateScreenShareVisibility(GameManager.Instance.myPlayer);
            }
        }
    }

    public void ToggleRoomLock()
    {
        if (photonView == null) return;

        // Check if the player trying to toggle lock is inside the room
        if (!playersInside.Any(p => p.view.IsMine))
        {
            return;
        }

        isLocked = !isLocked;

        if (isLocked)
        {
            lockingPlayerId = PhotonNetwork.LocalPlayer.ActorNumber;
        }
        else
        {
            lockingPlayerId = -1;
        }

        photonView.RPC("RPC_UpdateRoomLockState", RpcTarget.AllBuffered, isLocked, lockingPlayerId);
    }

    [PunRPC]
    private void RPC_UpdateRoomLockState(bool locked, int lockingPlayer)
    {
        isLocked = locked;
        lockingPlayerId = lockingPlayer;

        if (userControlsUI != null)
        {
            userControlsUI.UpdateLockButtonState();
        }

        if (blockingColliders == null)
        {
            blockingColliders = new GameObject[4];
        }

        if (locked)
        {
            if (roomLight == null || roomLight.freeFormPoints == null || roomLight.freeFormPoints.points == null || roomLight.freeFormPoints.points.Count != 4)
            {
                Debug.LogError("Room light or its free form points are not set up correctly!");
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                blockingColliders[i] = new GameObject("BlockingCollider_" + i);
                blockingColliders[i].transform.parent = transform;
                BoxCollider2D collider = blockingColliders[i].AddComponent<BoxCollider2D>();
                collider.isTrigger = false;

                Vector2 p1 = roomLight.freeFormPoints.points[i];
                Vector2 p2 = roomLight.freeFormPoints.points[(i + 1) % 4];

                Vector2 center = (p1 + p2) / 2f;
                float width = 0.01f;
                float length = Vector2.Distance(p1, p2);
                float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

                collider.size = new Vector2(length, width);
                blockingColliders[i].transform.localPosition = center;
                blockingColliders[i].transform.localRotation = Quaternion.Euler(0, 0, angle);
            }

            AstarPath.active.Scan();
        }
        else
        {
            if (blockingColliders != null)
            {
                foreach (GameObject collider in blockingColliders)
                {
                    if (collider != null)
                    {
                        Destroy(collider);
                    }
                }
                AstarPath.active.Scan();
            }
        }
    }

    private void ControlRoomLightAsync(bool active) => roomLight.enabled = active;
    //private void ControlRoomLightAsync(bool active)
    //{
    //    if (active)
    //        mainLight.transform.position = this.gameObject.transform.position;
    //    else if (!active)
    //        mainLight.transform.position = new Vector3(100, 100, 0);
    //}

    private void ControlGlobalLightAsync(bool active)
    {
        if (active)
            activeGlobalLight.GetComponent<Light2D>().color.a = 1f;
        else if (!active)
            activeGlobalLight.GetComponent<Light2D>().color.a = 0.1960784f;

        //activeGlobalLight.enabled = active;
        //disabledGlobalLight.enabled = !active;
    }

    /*private void UpdateAudioSettings()
    {
       /* foreach (PlayerController p1 in playersInside)
        {
            uint agoraID1 = GetAgoraID(p1);

            foreach (PlayerController p2 in playersInside)
            {
                if (p1 != p2)
                {
                    uint agoraID2 = GetAgoraID(p2);

                    if (agoraID1 != 0 && agoraID2 != 0 && agoraRtcEngine != null)
                    {
                        agoraRtcEngine.AdjustUserPlaybackSignalVolume(agoraID1, 100);
                        agoraRtcEngine.AdjustUserPlaybackSignalVolume(agoraID2, 100);
                    }
                }
            }
        }
    }*/

    private void UpdateScreenShareVisibility(PlayerController player, bool entering)
    {
        if (entering)
        {
            playersWhoCanSeeScreenShare.Add(player);
        }
        else
        {
            playersWhoCanSeeScreenShare.Remove(player);
        }

        // Update screen share visibility for players in the same room
        foreach (PlayerController p in GameManager.Instance.otherPlayers)
        {
            if (p.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isSharing) && (bool)isSharing)
            {
                bool shouldSeeScreenShare = GameManager.Instance.ShouldShowScreenShare(p);
                OnScreenShareVisibilityChanged?.Invoke((uint)p.view.ViewID, shouldSeeScreenShare);
            }
        }

        // Also check the local player's screen share
        if (GameManager.Instance.myPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object localIsSharing) && (bool)localIsSharing)
        {
            bool shouldSeeScreenShare = GameManager.Instance.ShouldShowScreenShare(GameManager.Instance.myPlayer);
            OnScreenShareVisibilityChanged?.Invoke((uint)GameManager.Instance.myPlayer.view.ViewID, shouldSeeScreenShare);
        }
    }


    private uint GetAgoraID(PlayerController player)
    {
        if (player.view.Owner.CustomProperties.TryGetValue("agoraID", out object agoraIDObj))
        {
            return uint.Parse(agoraIDObj.ToString());
        }
        return 0;
    }
}
