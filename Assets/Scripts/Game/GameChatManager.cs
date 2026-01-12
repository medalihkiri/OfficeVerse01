using UnityEngine;
using Photon.Pun;
using TMPro;
using System;
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;


public class GameChatManager : MonoBehaviourPunCallbacks
{
    public static GameChatManager SP;
    public PhotonView view;
    public TMP_InputField chatMessagePrefab;
    public RectTransform chatMessagesContent;
    public TMP_InputField chatInputField;
    public TMP_InputField searchInputField;
    public ScrollRect scrollRect;
    public TMP_Text time;

    private bool isOverLink = false;
    private Coroutine cursorCheckCoroutine;

    private readonly Regex urlRegex = new Regex(@"(http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?", RegexOptions.Compiled);
    private Dictionary<string, string> linkDictionary = new Dictionary<string, string>();

    [DllImport("__Internal")]
    private static extern void SetLinkHoverState(string gameObjectName, bool isHovering);


    [Serializable]
    public class ChatMessageDTO
    {
        public string messageId;
        public string senderName;
        public string text;
        public string createdAt;
    }

    [Serializable]
    public class ChatHistoryResponse
    {
        public bool success;
        public List<ChatMessageDTO> messages;
    }

    private HashSet<string> renderedMessageIds = new HashSet<string>();
    private string backendBaseUrl = "https://officeverseback.onrender.com/rooms";
    private string authToken => APIManager.Instance.authToken;

    // NEW: store backend roomId
    private string currentRoomId;


    private void Awake()
    {
        SP = this;
        chatInputField.onValueChanged.AddListener(OnInputValueChanged);
    }

    private void OnDestroy()
    {
        chatInputField.onValueChanged.RemoveListener(OnInputValueChanged);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        Debug.Log("🚀 GameChatManager.OnJoinedRoom fired. Photon room: " + PhotonNetwork.CurrentRoom.Name);

        renderedMessageIds.Clear();
        if (ChatBackendManager.Instance == null)
        {
            Debug.LogError("❌ ChatBackendManager.Instance is NULL!");
            return;
        }

        ChatBackendManager.Instance.ResolveRoom(
            PhotonNetwork.CurrentRoom.Name,
            50,
            (messages) =>
            {
                Debug.Log("📥 Loaded " + messages.Count + " messages from backend.");
                foreach (var m in messages)
                    RenderChatBubble(m.senderName, m.text, m.createdAt, m.messageId);
            });
    }


    private IEnumerator ResolveRoomIdAndLoadHistory(string photonRoomName, int limit)
    {
        string encoded = UnityWebRequest.EscapeURL(photonRoomName);
        string url = $"{backendBaseUrl}/find/{encoded}";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(authToken))
                www.SetRequestHeader("Authorization", "Bearer " + authToken);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(www.downloadHandler.text);
                if (dict.ContainsKey("room"))
                {
                    var roomObj = (Newtonsoft.Json.Linq.JObject)dict["room"];
                    currentRoomId = roomObj["_id"].ToString();
                    Debug.Log("✅ Resolved backend roomId: " + currentRoomId);

                    StartCoroutine(LoadRecentMessages(currentRoomId, limit));
                }
                else
                {
                    Debug.LogWarning("⚠ Backend did not return room object. Photon-only chat.");
                }
            }
            else
            {
                Debug.LogWarning($"⚠ ResolveRoomId failed: {www.responseCode} - {www.error}");
                currentRoomId = null;
            }
        }
    }

    public void Select(string text)
    {
        chatInputField.placeholder.gameObject.SetActive(false);
    }

    public void Deselect(string text)
    {
        chatInputField.placeholder.gameObject.SetActive(true);
    }

    public void OnInputValueChanged(string text)
    {
        if (text.Contains("\n"))
        {
            string trimmedText = text.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedText))
            {
                SendChat(trimmedText);
                chatInputField.text = "";
            }
            else
            {
                chatInputField.text = "";
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(chatInputField.gameObject);
        }
    }

    private void DeselectInputField()
    {
        chatInputField.DeactivateInputField();
        EventSystem.current.SetSelectedGameObject(null);

        if (EventSystem.current.currentSelectedGameObject == chatInputField.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void SendChat(string text)
    {
        string messageId = Guid.NewGuid().ToString();
        string senderName = PhotonNetwork.LocalPlayer.NickName;
        string createdAt = DateTime.UtcNow.ToString("o");
        string senderId = PhotonNetwork.LocalPlayer.UserId;


        view.RPC(nameof(SendChatMessage), RpcTarget.All, text, senderName, messageId, createdAt, senderId);
    }


    [PunRPC]
    void SendChatMessage(string text, string senderName, string messageId, string createdAt, string senderId, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.LocalPlayer)
            NotificationManager.Instance.PlayPublicMessageSound();

        RenderChatBubble(senderName, text, createdAt, messageId);

        ChatBackendManager.Instance.SaveMessage(new ChatBackendManager.ChatMessageDTO
        {
            messageId = messageId,
            senderId = senderId,
            senderName = senderName,
            text = text,
            createdAt = createdAt
        });
    }

    private void RenderChatBubble(string senderName, string text, string createdAtIso, string messageId)
    {
        if (renderedMessageIds.Contains(messageId)) return;
        renderedMessageIds.Add(messageId);

        GameObject messageObj = Instantiate(chatMessagePrefab.gameObject, chatMessagesContent);
        messageObj.name = "ChatMessage_" + messageId;

        TMP_InputField messageInputField = messageObj.GetComponent<TMP_InputField>();
        TextMeshProUGUI timeText = messageObj.GetComponentsInChildren<TextMeshProUGUI>()
            .FirstOrDefault(t => t.name == "Time");

        if (messageInputField == null || timeText == null)
        {
            Debug.LogError("Message prefab is not set up correctly.");
            return;
        }

        linkDictionary.Clear();
        int linkCount = 0;
        string formattedText = urlRegex.Replace(text, match =>
        {
            linkCount++;
            string linkId = $"link_{linkCount}";
            linkDictionary[linkId] = match.Value;
            return $"<link=\"{linkId}\"><color=#00FFFF><u>{match.Value}</u></color></link>";
        });

        messageInputField.text = $"<b>{senderName}</b>\n{formattedText}";

        DateTime dt;
        if (!DateTime.TryParse(createdAtIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out dt))
            dt = DateTime.UtcNow;

        string timestamp = dt.ToLocalTime().ToString("HH:mm");
        timeText.text = timestamp;

        messageInputField.readOnly = true;
        messageObj.SetActive(true);

        TMPWebGLInputFieldExtension extension = messageObj.AddComponent<TMPWebGLInputFieldExtension>();
        extension.SetAsMessageField();

        EventTrigger eventTrigger = messageObj.AddComponent<EventTrigger>();

        var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clickEntry.callback.AddListener((eventData) => { OnLinkClicked((PointerEventData)eventData, messageInputField.textComponent); });
        eventTrigger.triggers.Add(clickEntry);

        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((eventData) => { StartCursorCheck(messageInputField.textComponent); });
        eventTrigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((eventData) =>
        {
            if (cursorCheckCoroutine != null)
            {
                StopCoroutine(cursorCheckCoroutine);
                cursorCheckCoroutine = null;
            }
            UpdateCursor(messageInputField.textComponent);
        });
        eventTrigger.triggers.Add(exitEntry);

        messageObj.transform.SetAsFirstSibling();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private IEnumerator SaveMessageToBackend(ChatMessageDTO dto)
    {
        if (string.IsNullOrEmpty(currentRoomId))
            yield break; // skip if no backend room

        string url = $"{backendBaseUrl}/{currentRoomId}/messages";
        string json = JsonConvert.SerializeObject(dto);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(authToken))
                www.SetRequestHeader("Authorization", "Bearer " + authToken);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"SaveMessageToBackend failed: {www.responseCode} - {www.error}");
        }
    }

    private IEnumerator LoadRecentMessages(string roomId, int limit)
    {
        string url = $"{backendBaseUrl}/{roomId}/messages?limit={limit}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            if (!string.IsNullOrEmpty(authToken))
                www.SetRequestHeader("Authorization", "Bearer " + authToken);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"LoadRecentMessages failed: {www.responseCode} - {www.error}");
                yield break;
            }

            var resp = JsonConvert.DeserializeObject<ChatHistoryResponse>(www.downloadHandler.text);
            if (resp.success && resp.messages != null)
            {
                foreach (var m in resp.messages)
                    RenderChatBubble(m.senderName, m.text, m.createdAt, m.messageId);
            }
        }
    }

    // --- Existing link/cursor handlers remain unchanged ---
    private void OnMessageClicked(PointerEventData eventData, TMP_Text textComponent) { /* unchanged */ }
    private void UpdateCursor(TMP_Text textComponent) { /* unchanged */ }
    private void StartCursorCheck(TMP_Text textComponent) { /* unchanged */ }
    // private IEnumerator CheckCursorOverLink(TMP_Text textComponent) { /* unchanged */ }
    // private string FormatMessageWithClickableLinks(string message) { /* unchanged */ }
    private void OnLinkClicked(PointerEventData eventData, TMP_Text textComponent) { /* unchanged */ }

    private void OnDisable()
    {
        if (cursorCheckCoroutine != null)
        {
            StopCoroutine(cursorCheckCoroutine);
            cursorCheckCoroutine = null;
        }
        SetLinkHoverState(gameObject.name, false);
    }
}