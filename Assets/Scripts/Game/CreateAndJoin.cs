
using UnityEngine;
using Photon.Pun;
using TMPro;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class CreateAndJoin : MonoBehaviourPunCallbacks
{
    public TMP_InputField playerNameInput;
    public TMP_InputField joinRoomInput;

    public GameObject LoadingPanel;
    public TextMeshProUGUI loadingStatusText;

    public AvatarSelection avatarSelection;

    public int maxPlayers = 10;

    private RoomOptions roomOptions;
    private Hashtable playerProperties1;

    private float heartbeatInterval = 5f;
    private byte eventCode = 1;

    void Awake()
    {
        Application.runInBackground = true;

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        LoadingPanel.SetActive(true);

        playerNameInput.text = string.IsNullOrEmpty(PlayerDataManager.PlayerName) ? $"Guest{Random.Range(1000, 9999)}" : PlayerDataManager.PlayerName;
        joinRoomInput.text = string.IsNullOrEmpty(PlayerDataManager.PlayerRoomName) ? "testRoom" : PlayerDataManager.PlayerRoomName;

        roomOptions = new RoomOptions { MaxPlayers = maxPlayers };
        playerProperties1 = new Hashtable();

        InvokeRepeating(nameof(SendHeartbeat), heartbeatInterval, heartbeatInterval);
    }

    private void Update()
    {
        loadingStatusText.text = "Loading: " + PhotonNetwork.NetworkingClient.State;

        if (!PhotonNetwork.IsConnected && Application.internetReachability != NetworkReachability.NotReachable)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }


    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to master server.");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        LoadingPanel.SetActive(false);
    }

    public void OnJoinButton()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("Not connected to the master server.");
            return;
        }

        PlayerDataManager.PlayerName = playerNameInput.text;
        PlayerDataManager.PlayerRoomName = joinRoomInput.text;
        PlayerDataManager.PlayerAvatar = avatarSelection.selectedAvatarIndex;

        PhotonNetwork.LocalPlayer.NickName = playerNameInput.text;
        try
        {
            playerProperties1.Add("avatar", avatarSelection.selectedAvatarIndex);
        }
        catch { Debug.LogError("no it will not stuck on the lobby"); }
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties1);

        LoadingPanel.SetActive(true);

        PhotonNetwork.JoinOrCreateRoom(joinRoomInput.text, roomOptions, default);
    }

    public override void OnJoinedRoom()
    {
        PhotonNetwork.LoadLevel("GameScene");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        Debug.LogError("Join room failed: " + returnCode + " :: " + message);
        LoadingPanel.SetActive(false);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (cause == DisconnectCause.ClientTimeout || cause == DisconnectCause.ServerTimeout)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }


    void SendHeartbeat()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.RaiseEvent(eventCode, null, RaiseEventOptions.Default, SendOptions.SendUnreliable);
        }
    }
}