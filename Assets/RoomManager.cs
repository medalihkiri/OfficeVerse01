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

[System.Serializable]
public class RoomSceneMapping
{
    public string roomType;
    public string sceneName;
}


public class RoomManager : MonoBehaviourPunCallbacks
{

    [Header("Scene per Room Type")]
    public List<RoomSceneMapping> roomSceneMappings;

    private Dictionary<string, string> roomTypeToScene = new Dictionary<string, string>();

    [Header("Room Popup")]
    public GameObject RoomPopup;

    [Header("Room List UI")]
    public GameObject roomButtonPrefab;
    public Transform roomListContainer;

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

    private float heartbeatInterval = 5f;
    private byte eventCode = 1;

    private bool isLoading = false;
    private bool isCreatingRoom = false;
    private string pendingRoomName = "";
    private string pendingPassword = "";
    private int pendingMaxPlayers = 0;

    private string selectedRoomType;

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

        if (!PhotonNetwork.IsConnected)
            ConnectToPhoton();

        InvokeRepeating(nameof(SendHeartbeat), heartbeatInterval, heartbeatInterval);
    }

    void ConnectToPhoton()
    {
        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = "eu"; // Example: "us", "eu", "asia"

        Debug.Log($"Attempting to connect to Photon in fixed region: {PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion}...");
        PhotonNetwork.ConnectUsingSettings();
    }

    void Update()
    {
        if (!PhotonNetwork.IsConnected && Application.internetReachability != NetworkReachability.NotReachable)
            ConnectToPhoton();
    }

    void OpenPopup(GameObject popupToOpen)
    {
        if (popupToOpen == joinRoomPopup || popupToOpen == createRoomPopup)
        {
            joinRoomPopup.SetActive(false);
            createRoomPopup.SetActive(false);
            popupToOpen.SetActive(true);
        }
        else
        {
            mainPopup.SetActive(false);
            RoomPopup.SetActive(false);
            popupToOpen.SetActive(true);
        }

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

        ClearStatus();
        ClearRoomList();

        roomTypeDropdown.ClearOptions();
        if (PlayerDataManager.IsAuthenticated)
        {
            roomTypeDropdown.AddOptions(new List<string> { "Casual", "Work", "Conference" });
            StartCoroutine(LoadUserRooms());
        }
        else
        {
            roomTypeDropdown.AddOptions(new List<string> { "Casual" });
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

    // **FIXED**: Logic is now split. Guests go directly to Photon.
    void OnCreateRoomSubmit()
    {
        string roomName = createRoomNameInput.text.Trim();
        bool isPrivate = privateRoomToggle.isOn;
        string password = createRoomPasswordInput.text;
        int maxPlayers = int.TryParse(createMaxPlayersInput.text, out var mp) ? Mathf.Clamp(mp, 1, 50) : maxPlayersDefault;
        selectedRoomType = roomTypeDropdown.options[roomTypeDropdown.value].text.ToLower();

        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "⚠ Room name cannot be empty.";
            return;
        }

        // --- GUEST WORKFLOW ---
        if (!PlayerDataManager.IsAuthenticated)
        {
            if (isPrivate || selectedRoomType != "casual")
            {
                statusText.text = "⚠ Guests can only create public, Casual rooms.";
                return;
            }

            // *** FIX: Assign a new random UserId for this session to prevent conflicts ***
            // This makes every guest join a fresh one, avoiding the 'inactive UserId' error.
            PhotonNetwork.AuthValues = new AuthenticationValues($"guest_{System.Guid.NewGuid()}");

            isLoading = true;
            LoadingPanel.SetActive(true);
            loadingStatusText.text = "Creating room...";
            StartPhotonCreateRoom(roomName, maxPlayers, isPrivate, password);
        }
        // --- AUTHENTICATED USER WORKFLOW ---
        else
        {
            // ... (rest of the authenticated user logic is unchanged)
            isCreatingRoom = true;
            pendingRoomName = roomName;
            pendingPassword = password;
            pendingMaxPlayers = maxPlayers;

            isLoading = true;
            LoadingPanel.SetActive(true);
            loadingStatusText.text = "Registering room with server...";

            RoomCreatePayload payload = new RoomCreatePayload { /* ... */ };
            string json = JsonUtility.ToJson(payload);

            StartCoroutine(APIManager.Instance.Post("/rooms", json, (res) =>
            {
                if (res.result == UnityWebRequest.Result.Success)
                {
                    loadingStatusText.text = "Creating room on game network...";
                    StartPhotonCreateRoom(roomName, maxPlayers, isPrivate, password);
                }
                else
                {
                    if (res.responseCode == 409)
                    {
                        statusText.text = "❌ A room with this name already exists.";
                    }
                    else
                    {
                        statusText.text = "❌ Failed to create room on server.";
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
            statusText.text = "❌ Not connected to Photon.";
            ResetLoadingState();
            return;
        }

        if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
            PlayerPrefs.SetString("PendingRoomCreation", JsonUtility.ToJson(new PendingRoomData { roomName = roomName, maxPlayers = maxPlayers, isPrivate = isPrivate, password = password, roomType = selectedRoomType }));
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            PlayerPrefs.SetString("PendingRoomCreation", JsonUtility.ToJson(new PendingRoomData { roomName = roomName, maxPlayers = maxPlayers, isPrivate = isPrivate, password = password, roomType = selectedRoomType }));
            return;
        }

        CreatePhotonRoomNow(roomName, maxPlayers, isPrivate, password, selectedRoomType);
    }

    #endregion

    void CreatePhotonRoomNow(string roomName, int maxPlayers, bool isPrivate, string password, string roomType)
    {
        string passwordHash = isPrivate ? ComputeSHA256Hash(password) : "";
        string sceneName = GetSceneForRoomTypeSafe(roomType);

        roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)Mathf.Clamp(maxPlayers, 1, 50),
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "isPrivate", isPrivate }, { "passwordHash", passwordHash }, { "roomType", roomType }, { "scene", sceneName }
            },
            CustomRoomPropertiesForLobby = new string[] { "isPrivate", "roomType", "scene" },
            EmptyRoomTtl = 10000,
            PlayerTtl = 3000,
            PublishUserId = true
        };

        UpdatePlayerData(roomName);
        if (!PhotonNetwork.CreateRoom(roomName, roomOptions, TypedLobby.Default))
        {
            ResetLoadingState();
            statusText.text = "❌ Failed to create room.";
        }
    }

    #region Join Room

    // **FIXED**: Logic is now split. Guests go directly to Photon.
    void OnJoinRoomSubmit()
    {
        string roomName = joinRoomNameInput.text.Trim();
        string password = joinRoomPasswordInput.text;

        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "⚠ Room name cannot be empty.";
            return;
        }

        isLoading = true;
        LoadingPanel.SetActive(true);
        loadingStatusText.text = "Joining room...";

        // --- GUEST WORKFLOW ---
        if (!PlayerDataManager.IsAuthenticated)
        {
            // *** FIX: Assign a new random UserId for this session to prevent conflicts ***
            PhotonNetwork.AuthValues = new AuthenticationValues($"guest_{System.Guid.NewGuid()}");
            isLoading = true;
            LoadingPanel.SetActive(true);
            loadingStatusText.text = "Joining room...";
            StartPhotonJoinRoom(roomName, maxPlayersDefault, false, "");
        }
        // --- AUTHENTICATED USER WORKFLOW ---
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
            if (res.result != UnityWebRequest.Result.Success) return;

            RoomListResponse roomList = JsonUtility.FromJson<RoomListResponse>(res.downloadHandler.text);
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
    public class RoomListResponse { public bool success; public List<RoomData> rooms; }

    IEnumerator FindRoomByNameAndJoin(string roomName, string password)
    {
        string encodedName = UnityWebRequest.EscapeURL(roomName);

        yield return APIManager.Instance.Get($"/rooms/find/{encodedName}", (res) =>
        {
            if (res.result != UnityWebRequest.Result.Success)
            {
                ResetLoadingState();
                statusText.text = "❌ Room not found.";
                return;
            }

            RoomResponseSingle response = JsonUtility.FromJson<RoomResponseSingle>(res.downloadHandler.text);
            if (response == null || response.room == null)
            {
                ResetLoadingState();
                statusText.text = "❌ Room not found.";
                return;
            }

            if (!response.room.isPrivate)
            {
                StartPhotonJoinRoom(roomName, response.room.maxPlayers, false, "");
                return;
            }

            // This is the call that can fail for a wrong password
            string json = JsonUtility.ToJson(new JoinRoomPayload { password = password });
            StartCoroutine(APIManager.Instance.Post($"/rooms/{response.room._id}/join", json, (joinRes) =>
            {
                // **FIXED**: Better error handling in the callback.
                if (joinRes.result == UnityWebRequest.Result.Success)
                {
                    StartPhotonJoinRoom(roomName, response.room.maxPlayers, true, password);
                }
                else
                {
                    // Check for specific, recoverable 401 error
                    if (joinRes.responseCode == 401)
                    {
                        statusText.text = "❌ Invalid Password.";
                    }
                    else
                    {
                        statusText.text = "❌ You do not have permission to join this room.";
                    }
                    ResetLoadingState(); // Reset loading UI, but DO NOT log out.
                }
            }, requireAuth: true));

        }, requireAuth: true);
    }


    void StartPhotonJoinRoom(string roomName, int maxPlayers, bool isPrivate, string password)
    {
        if (!PhotonNetwork.IsConnected)
        {
            ResetLoadingState();
            statusText.text = "❌ Not connected to Photon.";
            return;
        }

        if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
            PlayerPrefs.SetString("PendingRoomJoin", JsonUtility.ToJson(new PendingRoomData { roomName = roomName, maxPlayers = maxPlayers, isPrivate = isPrivate, password = password }));
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            PlayerPrefs.SetString("PendingRoomJoin", JsonUtility.ToJson(new PendingRoomData { roomName = roomName, maxPlayers = maxPlayers, isPrivate = isPrivate, password = password }));
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
            statusText.text = "❌ Failed to join room.";
        }
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        Debug.Log("✅ Successfully joined the default lobby.");

        // Now check for any pending actions that were cached while connecting.
        string pendingCreation = PlayerPrefs.GetString("PendingRoomCreation", "");
        string pendingJoin = PlayerPrefs.GetString("PendingRoomJoin", "");

        if (!string.IsNullOrEmpty(pendingCreation))
        {
            PlayerPrefs.DeleteKey("PendingRoomCreation");
            PendingRoomData data = JsonUtility.FromJson<PendingRoomData>(pendingCreation);
            CreatePhotonRoomNow(data.roomName, data.maxPlayers, data.isPrivate, data.password, data.roomType);
        }
        else if (!string.IsNullOrEmpty(pendingJoin))
        {
            PlayerPrefs.DeleteKey("PendingRoomJoin");
            PendingRoomData data = JsonUtility.FromJson<PendingRoomData>(pendingJoin);
            JoinPhotonRoomNow(data.roomName);
        }
        else if (!isLoading)
        {
            LoadingPanel.SetActive(false);
            statusText.text = "Ready to join or create room!";
        }
    }

    #region Player Data

    void UpdatePlayerData(string roomName)
    {
        string playerName = "";

        // First try to get from the visible input field
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            playerName = playerNameInput.text.Trim();
        }
        // If empty, try saved PlayerDataManager
        else if (!string.IsNullOrWhiteSpace(PlayerDataManager.PlayerName))
        {
            playerName = PlayerDataManager.PlayerName;
        }

        // If still empty, fall back to Guest
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Guest" + Random.Range(1000, 9999);
        }

        // Save to PlayerDataManager
        PlayerDataManager.PlayerName = playerName;

        // Make sure UI matches
        if (playerNameInput != null)
            playerNameInput.text = playerName;

        // Store extra info
        PlayerDataManager.PlayerRoomName = roomName;
        PlayerDataManager.PlayerAvatar = avatarSelection.selectedAvatarIndex;

        // Apply to Photon
        PhotonNetwork.LocalPlayer.NickName = playerName;
        playerProperties["avatar"] = avatarSelection.selectedAvatarIndex;
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);
    }

    #endregion

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        // This log is CRITICAL. It confirms which region you actually connected to.
        Debug.Log($"✅ Successfully connected to Photon Master Server in region: {PhotonNetwork.CloudRegion}. Now joining lobby...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedRoom()
    {
        PlayerPrefs.DeleteKey("PendingRoomCreation");
        PlayerPrefs.DeleteKey("PendingRoomJoin");

        if (!ValidateGuestAccess()) return;

        if (PlayerDataManager.IsAuthenticated) StartCoroutine(LoadUserRooms());

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

    bool ValidateGuestAccess()
    {
        if (!PlayerDataManager.IsAuthenticated)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("roomType", out object roomTypeObj))
            {
                if (roomTypeObj.ToString().ToLower() != "casual")
                {
                    PhotonNetwork.LeaveRoom();
                    ResetLoadingState();
                    statusText.text = "❌ Guests can only join Casual rooms.";
                    return false;
                }
            }
        }
        return true;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        Debug.LogError($"❌ JOIN ROOM FAILED: Code={returnCode}, Message='{message}'");

        const short GAME_DOES_NOT_EXIST = 32758;

        // The self-healing logic now only applies to authenticated users,
        // as guests don't have a backend record to restore from.
        if (returnCode == GAME_DOES_NOT_EXIST && PlayerDataManager.IsAuthenticated)
        {
            Debug.LogWarning("Room not found on Photon. Attempting to re-create from backend data.");
            StartCoroutine(HandleRoomNotFoundOnPhoton(pendingRoomName));
        }
        else
        {
            ResetLoadingState();
            statusText.text = $"❌ Failed to join room: {message}";
        }
    }

    IEnumerator HandleRoomNotFoundOnPhoton(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            ResetLoadingState();
            statusText.text = "❌ Cannot sync room: name is missing.";
            yield break;
        }

        if (LoadingPanel != null) LoadingPanel.SetActive(true);
        isLoading = true;
        loadingStatusText.text = "Syncing room with server...";

        string encodedName = UnityWebRequest.EscapeURL(roomName);

        yield return APIManager.Instance.Get($"/rooms/find/{encodedName}", (res) =>
        {
            if (res.result != UnityWebRequest.Result.Success)
            {
                ResetLoadingState();
                statusText.text = "❌ Room not found.";
                return;
            }

            RoomResponseSingle response = JsonUtility.FromJson<RoomResponseSingle>(res.downloadHandler.text);
            if (response == null || response.room == null)
            {
                ResetLoadingState();
                statusText.text = "❌ Room not found.";
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

        if (returnCode == 32766) // ErrorCode.RoomAlreadyExists
        {
            loadingStatusText.text = "Joining existing room...";
            JoinPhotonRoomNow(pendingRoomName);
        }
        else
        {
            ResetLoadingState();
            statusText.text = $"❌ Create room failed: {message}";
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {

        base.OnDisconnected(cause);
        Debug.LogError($"❌ Disconnected from Photon: {cause}. Will attempt to reconnect via Update loop.");
        ResetLoadingState();
        if (cause == DisconnectCause.ClientTimeout || cause == DisconnectCause.ServerTimeout)
        {
            statusText.text = "Reconnecting...";
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            statusText.text = "Disconnected: " + cause.ToString();
        }
    }

    public void PrepareRoomUI() { ShowRoomPopup(); }
    public void ClearRoomList() { foreach (Transform child in roomListContainer) Destroy(child.gameObject); }

    void SendHeartbeat() { if (PhotonNetwork.IsConnected) PhotonNetwork.RaiseEvent(eventCode, null, RaiseEventOptions.Default, SendOptions.SendUnreliable); }

    #endregion

    #region Utilities

    string ComputeSHA256Hash(string rawData)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new StringBuilder();
            foreach (var b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }

    #endregion

    IEnumerator TimeoutProtection()
    {
        while (true)
        {
            yield return new WaitForSeconds(30f);
            if (isLoading && LoadingPanel.activeInHierarchy)
            {
                ResetLoadingState();
                statusText.text = "❌ Operation timed out. Please try again.";
            }
        }
    }

    void ResetLoadingState()
    {
        isLoading = false;
        isCreatingRoom = false;
        LoadingPanel.SetActive(false);
    }

    [System.Serializable]
    public class PendingRoomData { public string roomName; public int maxPlayers; public bool isPrivate; public string password; public string roomType; }

    #region Payload Classes

    [System.Serializable]
    public class RoomResponseSingle { public bool success; public RoomData room; }
    [System.Serializable]
    public class RoomCreatePayload { public string name; public bool isPrivate; public string password; public int maxPlayers; public string type; }
    [System.Serializable]
    public class JoinRoomPayload { public string password; }
    [System.Serializable]
    public class RoomData { public string _id; public string name; public bool isPrivate; public int maxPlayers; public string type; }

    #endregion
}