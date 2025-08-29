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

    private string currentRoomId;
    private string AuthToken => APIManager.Instance.authToken;

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
        Debug.Log("🌐 SaveMessageToBackend called for " + dto.text + " (private=" + dto.isPrivate + ")");
        string url = $"{backendBaseUrl}/{currentRoomId}/messages";
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
                Debug.Log($"✅ Message saved: {www.downloadHandler.text}");
            else
                Debug.LogError($"❌ SaveMessageToBackend failed: {www.responseCode}, {www.error}, body={json}");
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
