using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using System;

[System.Serializable]
public class RoomSceneMapping
{
    public string roomType;
    public string sceneName;
}


public class RoomManager : MonoBehaviourPunCallbacks
{
    public static event Action OnJoinedPhotonLobby;

    [Header("Scene per Room Type")]
    public List<RoomSceneMapping> roomSceneMappings;

    private Dictionary<string, string> roomTypeToScene = new Dictionary<string, string>();

    [Header("Room Popup")]
    public GameObject RoomPopup;

    [Header("Room List UI")]
    public GameObject roomButtonPrefab;
    public Transform roomListContainer;

    public string lastJoinedRoomName;

    [Header("Create Room UI")]
    public GameObject createRoomPopup;
    public TMP_InputField createRoomNameInput;
    public Toggle privateRoomToggle;
    public TMP_InputField createRoomPasswordInput;
    public TMP_InputField createMaxPlayersInput;
    public Button createRoomSubmitButton;

    [Header("Join Room UI")]
    public GameObject joinRoomPopup;
    public TMP_InputField joinRoomNameInput;
    public TMP_InputField joinRoomPasswordInput;
    public Button joinRoomSubmitButton;

    [Header("Character")]
    public GameObject characterCustomizationButton;

    [Header("Popup Toggle Buttons")]
    public Button openCreatePopupButton;
    public Button openJoinPopupButton;

    [Header("Main Popup")]
    public GameObject mainPopup;

    [Header("Loading UI")]
    public GameObject LoadingPanel;
    public TextMeshProUGUI loadingStatusText;
    public Button cancelLoadingButton;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    [Header("Room Type")]
    public TMP_Dropdown roomTypeDropdown;

    [Header("Player Info & Photon")]
    public TMP_InputField playerNameInput;
    public AvatarSelection avatarSelection;

    public int maxPlayersDefault = 10;

    private RoomOptions roomOptions;
    private ExitGames.Client.Photon.Hashtable playerProperties;

    private bool isLoading = false;
    private string pendingRoomName = "";
    private string pendingPassword = "";
    private string selectedRoomType;

    public static string CurrentRoomDbId { get; private set; }


    void Awake()
    {
        foreach (var mapping in roomSceneMappings)
        {
            if (!roomTypeToScene.ContainsKey(mapping.roomType.ToLower()))
                roomTypeToScene.Add(mapping.roomType.ToLower(), mapping.sceneName);
        }

        Application.runInBackground = true;
        HideAllRoomUI();

        roomOptions = new RoomOptions { MaxPlayers = (byte)maxPlayersDefault };
        playerProperties = new ExitGames.Client.Photon.Hashtable();

        createRoomSubmitButton.onClick.AddListener(OnCreateRoomSubmit);
        joinRoomSubmitButton.onClick.AddListener(OnJoinRoomSubmit);
        openCreatePopupButton.onClick.AddListener(() => OpenPopup(createRoomPopup));
        openJoinPopupButton.onClick.AddListener(() => OpenPopup(joinRoomPopup));
        if (cancelLoadingButton != null)
            cancelLoadingButton.onClick.AddListener(CancelLoading);
    }

    public void ConnectToPhoton()
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("[Auth] Already connected to Photon. Joining lobby.");
            PhotonNetwork.JoinLobby();
            return;
        }

        if (APIManager.Instance != null && APIManager.Instance.isLoggedIn)
        {
            string backendUserId = APIManager.Instance.userId;
            if (!string.IsNullOrEmpty(backendUserId))
            {
                Debug.Log($"[Auth] Setting Photon UserId to backend ID: {backendUserId}");
                PhotonNetwork.AuthValues = new AuthenticationValues(backendUserId);
            }
            PhotonNetwork.NickName = APIManager.Instance.username;
        }
        else
        {
            Debug.Log("[Auth] Not logged in. Connecting to Photon as a guest.");
        }

        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = "eu";
        Debug.Log($"Attempting to connect to Photon in fixed region: {PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion}...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Successfully left the Photon room. Now in Master Server lobby.");
        ShowRoomPopup();
    }

    void OpenPopup(GameObject popupToOpen)
    {
        joinRoomPopup.SetActive(false);
        createRoomPopup.SetActive(false);
        popupToOpen.SetActive(true);
        ClearStatus();
    }

    public void HideAllRoomUI()
    {
        RoomPopup.SetActive(false);
        createRoomPopup.SetActive(false);
        joinRoomPopup.SetActive(false);
        mainPopup.SetActive(false);
        LoadingPanel.SetActive(false);
    }

    public void ShowRoomPopup()
    {
        RoomPopup.SetActive(true);
        mainPopup.SetActive(false);
        createRoomPopup.SetActive(false);
        joinRoomPopup.SetActive(true);
        if (characterCustomizationButton != null)
            characterCustomizationButton.SetActive(true);

        // FIX: Set a personalized welcome message in the room panel status text.
        if (APIManager.Instance != null && APIManager.Instance.isLoggedIn)
        {
            statusText.text = $"Welcome back, {APIManager.Instance.username}!";
        }
        else
        {
            statusText.text = "Connected as Guest. Create or join a 'Casual' room.";
        }

        ClearRoomList();

        roomTypeDropdown.ClearOptions();
        if (APIManager.Instance.isLoggedIn)
        {
            roomTypeDropdown.AddOptions(new List<string> { "casual", "work", "classroom" });
            StartCoroutine(LoadUserRooms());
        }
        else
        {
            roomTypeDropdown.AddOptions(new List<string> { "casual" });
        }
    }

    void ClearInputs()
    {
        createRoomNameInput.text = "";
        createRoomPasswordInput.text = "";
        createMaxPlayersInput.text = maxPlayersDefault.ToString();
        joinRoomNameInput.text = "";
        joinRoomPasswordInput.text = "";
    }

    void ClearStatus() => statusText.text = "";

    void CancelLoading()
    {
        isLoading = false;
        LoadingPanel.SetActive(false);
        statusText.text = "Operation cancelled.";
        if (PhotonNetwork.IsConnected && !PhotonNetwork.InRoom)
            PhotonNetwork.Disconnect();
    }

    #region Create Room

    void OnCreateRoomSubmit()
    {
        APIManager.Instance.SetCurrentRoomDbId(null);

        string roomName = createRoomNameInput.text.Trim();
        bool isPrivate = privateRoomToggle.isOn;
        string password = createRoomPasswordInput.text;
        int maxPlayers = int.TryParse(createMaxPlayersInput.text, out var mp) ? Mathf.Clamp(mp, 1, 50) : maxPlayersDefault;
        selectedRoomType = roomTypeDropdown.options[roomTypeDropdown.value].text.ToLower();

        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "Room name cannot be empty.";
            return;
        }

        if (!APIManager.Instance.isLoggedIn)
        {
            if (isPrivate || selectedRoomType != "casual")
            {
                statusText.text = "Guests can only create public, Casual rooms.";
                return;
            }
            PhotonNetwork.AuthValues = new AuthenticationValues($"guest_{System.Guid.NewGuid()}");
            isLoading = true;
            LoadingPanel.SetActive(true);
            loadingStatusText.text = "Creating room...";
            StartPhotonCreateRoom(roomName, maxPlayers, isPrivate, password);
        }
        else
        {
            pendingRoomName = roomName;
            pendingPassword = password;
            isLoading = true;
            LoadingPanel.SetActive(true);
            loadingStatusText.text = "Registering room with server...";

            RoomCreatePayload payload = new RoomCreatePayload
            {
                name = roomName,
                isPrivate = isPrivate,
                password = isPrivate ? password : "",
                maxPlayers = maxPlayers,
                type = selectedRoomType
            };
            string json = JsonUtility.ToJson(payload);

            StartCoroutine(APIManager.Instance.Post("/rooms", json, (res) =>
            {
                if (res != null && res.result == UnityWebRequest.Result.Success)
                {
                    RoomResponseSingle roomResponse = JsonUtility.FromJson<RoomResponseSingle>(res.downloadHandler.text);
                    if (roomResponse != null && roomResponse.room != null)
                    {
                        APIManager.Instance.SetCurrentRoomDbId(roomResponse.room._id);
                    }
                    loadingStatusText.text = "Creating room on game network...";
                    StartPhotonCreateRoom(roomName, maxPlayers, isPrivate, password);
                }
                else
                {
                    if (res != null && res.responseCode == 409)
                    {
                        statusText.text = "A room with this name already exists.";
                    }
                    else if (res != null && res.responseCode == 401)
                    {
                        statusText.text = "Your session has expired. Please log in again.";
                        APIManager.Instance.HandleSessionExpired();
                    }
                    else
                    {
                        statusText.text = "Failed to create room on server.";
                    }
                    ResetLoadingState();
                }
            }, requireAuth: true));
        }
    }

    void StartPhotonCreateRoom(string roomName, int maxPlayers, bool isPrivate, string password)
    {
        if (!PhotonNetwork.IsConnected)
        {
            statusText.text = "Not connected to Photon. Please wait.";
            ResetLoadingState();
            ConnectToPhoton();
            return;
        }
        CreatePhotonRoomNow(roomName, maxPlayers, isPrivate, password, selectedRoomType);
    }

    void CreatePhotonRoomNow(string roomName, int maxPlayers, bool isPrivate, string password, string roomType)
    {
        string sceneName = GetSceneForRoomTypeSafe(roomType);

        roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)Mathf.Clamp(maxPlayers, 1, 50),
            PlayerTtl = 3000,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "isPrivate", isPrivate },
                { "roomType", roomType },
                { "scene", sceneName }
            },
            CustomRoomPropertiesForLobby = new string[] { "isPrivate", "roomType", "scene" },
            PublishUserId = true
        };

        UpdatePlayerData(roomName);
        if (!PhotonNetwork.CreateRoom(roomName, roomOptions, TypedLobby.Default))
        {
            ResetLoadingState();
            statusText.text = "Failed to create room on Photon.";
        }
    }

    #endregion

    #region Join Room

    void OnJoinRoomSubmit()
    {
        APIManager.Instance.SetCurrentRoomDbId(null);

        string roomName = joinRoomNameInput.text.Trim();
        string password = joinRoomPasswordInput.text;

        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "Room name cannot be empty.";
            return;
        }

        isLoading = true;
        LoadingPanel.SetActive(true);
        loadingStatusText.text = "Joining room...";

        if (!APIManager.Instance.isLoggedIn)
        {
            PhotonNetwork.AuthValues = new AuthenticationValues($"guest_{System.Guid.NewGuid()}");
            StartPhotonJoinRoom(roomName);
        }
        else
        {
            pendingRoomName = roomName;
            pendingPassword = password;
            StartCoroutine(FindRoomByNameAndJoin(roomName, password));
        }
    }

    IEnumerator LoadUserRooms()
    {
        ClearRoomList();
        yield return APIManager.Instance.Get("/rooms/user", (res) =>
        {
            if (res == null || res.result != UnityWebRequest.Result.Success) return;
            RoomListResponse roomList = JsonUtility.FromJson<RoomListResponse>(res.downloadHandler.text);
            if (roomList == null || roomList.rooms == null) return;

            foreach (RoomData room in roomList.rooms)
            {
                GameObject buttonObj = Instantiate(roomButtonPrefab, roomListContainer);
                var buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                var buttonComp = buttonObj.GetComponent<Button>();
                if (buttonText != null) buttonText.text = room.name;
                if (buttonComp != null)
                {
                    buttonComp.onClick.RemoveAllListeners();
                    buttonComp.onClick.AddListener(() =>
                    {
                        joinRoomNameInput.text = room.name;
                        joinRoomPasswordInput.text = "";
                        OnJoinRoomSubmit();
                    });
                }
            }
        }, requireAuth: true);
    }

    [System.Serializable]
    public class RoomListResponse { public List<RoomData> rooms; }

    IEnumerator FindRoomByNameAndJoin(string roomName, string password)
    {
        string encodedName = UnityWebRequest.EscapeURL(roomName);
        yield return APIManager.Instance.Get($"/rooms/find/{encodedName}", (res) =>
        {
            if (res == null || res.result != UnityWebRequest.Result.Success)
            {
                ResetLoadingState();
                statusText.text = "Room not found on server.";
                return;
            }

            RoomResponseSingle response = JsonUtility.FromJson<RoomResponseSingle>(res.downloadHandler.text);
            if (response == null || response.room == null)
            {
                ResetLoadingState();
                statusText.text = "Room data could not be parsed.";
                return;
            }
            APIManager.Instance.SetCurrentRoomDbId(response.room._id);

            if (!response.room.isPrivate)
            {
                StartPhotonJoinRoom(roomName);
                return;
            }

            string json = JsonUtility.ToJson(new JoinRoomPayload { password = password });
            StartCoroutine(APIManager.Instance.Post($"/rooms/{response.room._id}/join", json, (joinRes) =>
            {
                if (joinRes != null && joinRes.result == UnityWebRequest.Result.Success)
                {
                    StartPhotonJoinRoom(roomName);
                }
                else
                {
                    if (joinRes != null && joinRes.responseCode == 401)
                    {
                        statusText.text = "Invalid Password.";
                    }
                    else
                    {
                        statusText.text = "You do not have permission to join this room.";
                    }
                    ResetLoadingState();
                }
            }, requireAuth: true));
        }, requireAuth: true);
    }

    void StartPhotonJoinRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            ResetLoadingState();
            statusText.text = "Not connected to Photon.";
            ConnectToPhoton();
            return;
        }
        JoinPhotonRoomNow(roomName);
    }

    #endregion

    void JoinPhotonRoomNow(string roomName)
    {
        UpdatePlayerData(roomName);
        if (!PhotonNetwork.JoinRoom(roomName))
        {
            ResetLoadingState();
            statusText.text = "Failed to join room on Photon.";
        }
    }

    #region Player Data

    void UpdatePlayerData(string roomName)
    {
        string playerName;
        if (APIManager.Instance.isLoggedIn)
        {
            playerName = APIManager.Instance.username;
        }
        else
        {
            playerName = $"Guest{UnityEngine.Random.Range(1000, 9999)}";
        }
        PhotonNetwork.LocalPlayer.NickName = playerName;

        int avatarIndex = 0;
        if (avatarSelection != null)
        {
            avatarIndex = avatarSelection.selectedAvatarIndex;
        }
        else
        {
            Debug.LogWarning("AvatarSelection component not assigned in RoomManager Inspector. Defaulting to avatar 0.");
        }
        playerProperties["avatar"] = avatarIndex;


        if (APIManager.Instance.isLoggedIn)
        {
            playerProperties["backendUserId"] = APIManager.Instance.userId;
        }
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);
    }

    #endregion

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log($"Successfully connected to Photon Master Server in region: {PhotonNetwork.CloudRegion}. Now joining lobby...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        Debug.Log("Successfully joined the default lobby.");
        OnJoinedPhotonLobby?.Invoke();
    }

    public override void OnJoinedRoom()
    {
        lastJoinedRoomName = PhotonNetwork.CurrentRoom.Name;
        Debug.Log("[Photon] Joined room: " + lastJoinedRoomName);

        string sceneName = GetSceneFromRoomProperties();
        ResetLoadingState();
        HideAllRoomUI();
        PhotonNetwork.LoadLevel(sceneName);
    }

    private string GetSceneForRoomTypeSafe(string roomType)
    {
        if (string.IsNullOrEmpty(roomType)) return "GameScene_Default";
        string key = roomType.ToLower();
        if (roomTypeToScene.TryGetValue(key, out string scene)) return scene;
        return "GameScene_Default";
    }

    string GetSceneFromRoomProperties()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("scene", out object sceneObj))
        {
            return sceneObj.ToString();
        }
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("roomType", out object roomTypeObj))
        {
            return GetSceneForRoomTypeSafe(roomTypeObj.ToString());
        }
        return "GameScene_Default";
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        Debug.LogError($"JOIN ROOM FAILED: Code={returnCode}, Message='{message}'");
        const short GAME_DOES_NOT_EXIST = 32758;
        if (returnCode == GAME_DOES_NOT_EXIST && APIManager.Instance.isLoggedIn)
        {
            Debug.LogWarning("Room not found on Photon. Attempting to re-create from backend data.");
            StartCoroutine(HandleRoomNotFoundOnPhoton(pendingRoomName));
        }
        else
        {
            ResetLoadingState();
            statusText.text = $"Failed to join room: {message}";
        }
    }

    IEnumerator HandleRoomNotFoundOnPhoton(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            ResetLoadingState();
            statusText.text = "Cannot sync room: name is missing.";
            yield break;
        }

        isLoading = true;
        LoadingPanel.SetActive(true);
        loadingStatusText.text = "Syncing room with server...";
        string encodedName = UnityWebRequest.EscapeURL(roomName);

        yield return APIManager.Instance.Get($"/rooms/find/{encodedName}", (res) =>
        {
            if (res.result != UnityWebRequest.Result.Success)
            {
                ResetLoadingState();
                statusText.text = "Room not found on the main server.";
                return;
            }
            RoomResponseSingle response = JsonUtility.FromJson<RoomResponseSingle>(res.downloadHandler.text);
            if (response == null || response.room == null)
            {
                ResetLoadingState();
                statusText.text = "Room data could not be retrieved.";
                return;
            }

            loadingStatusText.text = "Re-creating room on network...";
            var roomData = response.room;
            CreatePhotonRoomNow(roomData.name, roomData.maxPlayers, roomData.isPrivate, pendingPassword, roomData.type);
        }, requireAuth: true);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);
        const short ROOM_ALREADY_EXISTS = 32766;
        if (returnCode == ROOM_ALREADY_EXISTS)
        {
            loadingStatusText.text = "Room already exists. Joining...";
            JoinPhotonRoomNow(pendingRoomName);
        }
        else
        {
            ResetLoadingState();
            statusText.text = $"Create room failed: {message}";
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        Debug.LogError($"Disconnected from Photon: {cause}.");
        ResetLoadingState();
        /*if (cause == DisconnectCause.Exception || cause == DisconnectCause.ClientTimeout || cause == DisconnectCause.ServerTimeout)
        {
            statusText.text = "Connection lost. Reconnecting...";
            ConnectToPhoton();
        }*/
        /*else
        {
            statusText.text = "Disconnected. Please check your connection.";
        }*/
    }

    public void PrepareRoomUI() { ShowRoomPopup(); }
    public void ClearRoomList() { foreach (Transform child in roomListContainer) Destroy(child.gameObject); }

    void ResetLoadingState()
    {
        isLoading = false;
        if (LoadingPanel != null) LoadingPanel.SetActive(false);
    }

    #region Payload Classes
    [System.Serializable]
    public class RoomResponseSingle { public RoomData room; }
    [System.Serializable]
    public class RoomCreatePayload { public string name; public bool isPrivate; public string password; public int maxPlayers; public string type; }
    [System.Serializable]
    public class JoinRoomPayload { public string password; }
    [System.Serializable]
    public class RoomData { public string _id; public string name; public bool isPrivate; public int maxPlayers; public string type; }
    #endregion 

}
    #endregion 