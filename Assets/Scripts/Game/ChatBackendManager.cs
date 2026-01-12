using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Text;

public class ChatBackendManager : MonoBehaviour
{
    public static ChatBackendManager Instance;

    [Header("Config")]
    [SerializeField] private string backendBaseUrl = "https://officeverseback.onrender.com/rooms";

    public static event Action<string> OnRoomResolved;

    private string currentRoomId;
    private string AuthToken => APIManager.Instance.authToken;

    public string GetCurrentRoomId()
    {
        return currentRoomId;
    }

    [Serializable]
    public class ChatMessageDTO
    {
        public string messageId;
        public string senderId;

        public string senderName;
        public string text;
        public string createdAt;

        // NEW fields for private chat
        public string recipientId;  // optional
        public bool isPrivate;      // optional
    }

    [Serializable]
    public class RoomUserInfo
    {
        public string _id;
        public string username;
    }

    [Serializable]
    public class RoomUsersResponse
    {
        public bool success;
        public List<RoomUserInfo> users;
    }


    [Serializable]
    public class ChatHistoryResponse
    {
        public bool success;
        public List<ChatMessageDTO> messages;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- Public API ---

    public void ResolveRoom(string photonRoomName, int limit, Action<List<ChatMessageDTO>> onLoaded)
    {
        StartCoroutine(ResolveRoomIdAndLoadHistory(photonRoomName, limit, onLoaded));
    }

    public void SaveMessage(ChatMessageDTO dto)
    {
        if (string.IsNullOrEmpty(currentRoomId))
        {
            Debug.LogWarning("⚠ Cannot save message: no backend room resolved yet.");
            return;
        }
        StartCoroutine(SaveMessageToBackend(dto));
    }

    // --- Internals ---


    public void GetUsersInRoom(string roomId, Action<List<RoomUserInfo>> onLoaded)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogError("Cannot get users: Room ID is null or empty.");
            onLoaded?.Invoke(new List<RoomUserInfo>());
            return;
        }
        StartCoroutine(GetUsersInRoomRoutine(roomId, onLoaded));
    }

    private IEnumerator GetUsersInRoomRoutine(string roomId, Action<List<RoomUserInfo>> onLoaded)
    {
        // Note: We are using the "rooms" base URL from your existing config.
        string url = $"{backendBaseUrl}/{roomId}/users";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            // This endpoint requires authentication
            if (!string.IsNullOrEmpty(AuthToken))
                www.SetRequestHeader("Authorization", "Bearer " + AuthToken);
            else
            {
                Debug.LogError("Auth token is missing. Cannot fetch room users.");
                onLoaded?.Invoke(new List<RoomUserInfo>());
                yield break;
            }

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"⚠ GetUsersInRoom failed: {www.responseCode} - {www.error}");
                onLoaded?.Invoke(new List<RoomUserInfo>());
                yield break;
            }

            var resp = JsonConvert.DeserializeObject<RoomUsersResponse>(www.downloadHandler.text);
            if (resp != null && resp.success && resp.users != null)
                onLoaded?.Invoke(resp.users);
            else
                onLoaded?.Invoke(new List<RoomUserInfo>());
        }
    }
    private IEnumerator ResolveRoomIdAndLoadHistory(string photonRoomName, int limit, Action<List<ChatMessageDTO>> onLoaded)
    {
        string encoded = UnityWebRequest.EscapeURL(photonRoomName);
        string url = $"{backendBaseUrl}/find/{encoded}";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(AuthToken))
                www.SetRequestHeader("Authorization", "Bearer " + AuthToken);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(www.downloadHandler.text);
                if (dict.ContainsKey("room"))
                {
                    var roomObj = (Newtonsoft.Json.Linq.JObject)dict["room"];
                    currentRoomId = roomObj["_id"].ToString();
                    Debug.Log("✅ Resolved backend roomId: " + currentRoomId);
                    OnRoomResolved?.Invoke(currentRoomId);
                    StartCoroutine(LoadRecentMessages(limit, onLoaded));
                }
                else
                {
                    Debug.LogWarning("⚠ Backend did not return room object.");
                    onLoaded?.Invoke(new List<ChatMessageDTO>());
                }
            }
            else
            {
                Debug.LogWarning($"⚠ ResolveRoomId failed: {www.responseCode} - {www.error}");
                currentRoomId = null;
                onLoaded?.Invoke(new List<ChatMessageDTO>());
            }
        }
    }


    private IEnumerator SaveMessageToBackend(ChatMessageDTO dto)
    {
        if (string.IsNullOrEmpty(currentRoomId) && !dto.isPrivate)
        {
            Debug.LogError("❌ Cannot save public message: currentRoomId is not set. Was ResolveRoom ever called?");
            yield break;
        }

        // The backend endpoint requires a room ID in the URL, even for private messages.
        // We can use a placeholder or the current room ID. The backend will ignore it
        // when isPrivate is true.
        string roomIdForEndpoint = dto.isPrivate ? (currentRoomId ?? "private") : currentRoomId;


        Debug.Log($"🌐 Saving message... Private: {dto.isPrivate}, Text: {dto.text}");
        string url = $"{backendBaseUrl}/{roomIdForEndpoint}/messages";
        string json = JsonConvert.SerializeObject(dto);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(AuthToken))
                www.SetRequestHeader("Authorization", "Bearer " + AuthToken);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
                Debug.Log($"✅ Message saved successfully. Response: {www.downloadHandler.text}");
            else
                Debug.LogError($"❌ SaveMessageToBackend failed: {www.responseCode}, {www.error}\nURL: {url}\nBody: {json}");
        }
    }

    public void LoadPrivateChat(string userA, string userB, int limit, Action<List<ChatMessageDTO>> onLoaded)
    {
        StartCoroutine(LoadPrivateMessages(userA, userB, limit, onLoaded));
    }

    private IEnumerator LoadPrivateMessages(string userA, string userB, int limit, Action<List<ChatMessageDTO>> onLoaded)
    {
        string url = $"{backendBaseUrl}/private/{userA}/{userB}?limit={limit}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            if (!string.IsNullOrEmpty(AuthToken))
                www.SetRequestHeader("Authorization", "Bearer " + AuthToken);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"⚠ LoadPrivateMessages failed: {www.responseCode} - {www.error}");
                onLoaded?.Invoke(new List<ChatMessageDTO>());
                yield break;
            }

            var resp = JsonConvert.DeserializeObject<ChatHistoryResponse>(www.downloadHandler.text);
            if (resp.success && resp.messages != null)
                onLoaded?.Invoke(resp.messages);
            else
                onLoaded?.Invoke(new List<ChatMessageDTO>());
        }
    }

    private IEnumerator LoadRecentMessages(int limit, Action<List<ChatMessageDTO>> onLoaded)
    {
        string url = $"{backendBaseUrl}/{currentRoomId}/messages?limit={limit}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            if (!string.IsNullOrEmpty(AuthToken))
                www.SetRequestHeader("Authorization", "Bearer " + AuthToken);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"LoadRecentMessages failed: {www.responseCode} - {www.error}");
                onLoaded?.Invoke(new List<ChatMessageDTO>());
                yield break;
            }

            var resp = JsonConvert.DeserializeObject<ChatHistoryResponse>(www.downloadHandler.text);
            if (resp.success && resp.messages != null)
                onLoaded?.Invoke(resp.messages);
            else
                onLoaded?.Invoke(new List<ChatMessageDTO>());
        }
    }
}