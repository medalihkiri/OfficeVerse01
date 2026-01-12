using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;

public class PrivateChat : MonoBehaviourPunCallbacks
{
    public static PrivateChat Instance;
    private bool isShuttingDown = false;

    [Header("Main UI Elements")]
    // removed menuPanel, slideDuration, slideDistance, toggleButton
    public GameObject chatListContent;
    public GameObject chatWindowContent;
    public GameObject newChatContent;

    [Header("Chat Window UI Elements")]
    public TextMeshProUGUI chatWindowTitle;
    public Image chatWindowAvatar;
    public Transform messageListContent;
    public ScrollRect scrollRect;
    public GameObject messagePrefab;
    public GameObject dateSeparatorPrefab;
    public TMP_InputField messageInput;
    public Button sendMessageButton;
    public Button chatBackButton;
    public Button chatCloseButton;

    [Header("Chat List UI Elements")]
    public Transform chatListTransform;
    public GameObject chatListItemPrefab;
    public Button chatListCloseButton;
    public Button newChatButton;

    [Header("New Chat Window")]
    public TMP_InputField newChatSearchInput;
    public Transform newChatParticipantListContent;
    public GameObject newChatParticipantPrefab;
    public Button createChatButton;
    public Button newChatBackButton;

    private List<Player> selectedParticipants = new List<Player>();
    private Dictionary<int, GameObject> participantObjects = new Dictionary<int, GameObject>();

    [HideInInspector]
    public bool isOpen = false;
    private Dictionary<string, List<ChatMessage>> chatHistory = new Dictionary<string, List<ChatMessage>>();
    private Dictionary<string, Player> emptyChatPartners = new Dictionary<string, Player>();
    private Dictionary<string, string> savedInputs = new Dictionary<string, string>();

    private Player currentChatPartner;

    private readonly Regex urlRegex = new Regex(@"(http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&:/~\+#]*[\w\-\@?^=%&/~\+#])?", RegexOptions.Compiled);
    private Dictionary<string, string> linkDictionary = new Dictionary<string, string>();

    [DllImport("__Internal")]
    private static extern void SetLinkHoverState(string gameObjectName, bool isHovering);

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Removed menuPanel positioning and toggleButton listener

        if (chatCloseButton != null) chatCloseButton.onClick.AddListener(CloseMenu);
        if (chatListCloseButton != null) chatListCloseButton.onClick.AddListener(CloseMenu);

        newChatButton.onClick.AddListener(ShowNewChatWindow);
        chatBackButton.onClick.AddListener(ShowChatList);
        newChatBackButton.onClick.AddListener(ShowChatList);
        createChatButton.onClick.AddListener(CreateNewChats);

        newChatSearchInput.onValueChanged.AddListener(OnNewChatSearchValueChanged);
        newChatSearchInput.onEndEdit.AddListener(OnNewChatEndEdit);
        newChatSearchInput.onSelect.AddListener(OnSearchInputFieldSelect);
        newChatSearchInput.onDeselect.AddListener(OnSearchInputFieldDeselect);

        messageInput.onEndEdit.AddListener(OnMessageInputEndEdit);
        messageInput.onSelect.AddListener(OnChatInputFieldSelect);
        messageInput.onDeselect.AddListener(OnChatInputFieldDeselect);
        sendMessageButton.onClick.AddListener(() => SendMessage());

        // Set initial internal state
        chatListContent.SetActive(true);
        chatWindowContent.SetActive(false);
        newChatContent.SetActive(false);
        createChatButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
    }

    // Removed ToggleMenu and SlideMenu methods

    public void CloseMenu()
    {
        GetComponent<ButtonHoverEffect>().enabled = true;
        isOpen = false;

        // Logic to actually hide the panel is now handled by your external script
        // or you can disable the gameObject here if this script is on the panel itself:
        // gameObject.SetActive(false);
    }

    private void ShowNewChatWindow()
    {
        chatListContent.SetActive(false);
        newChatContent.SetActive(true);
        createChatButton.gameObject.SetActive(false);
        newChatSearchInput.text = "";
        PopulateNewChatParticipantList();
    }

    private void PopulateNewChatParticipantList()
    {
        foreach (Transform child in newChatParticipantListContent)
        {
            Destroy(child.gameObject);
        }
        participantObjects.Clear();
        selectedParticipants.Clear();

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player != PhotonNetwork.LocalPlayer && !ChatExistsWithPlayer(player))
            {
                GameObject participantObj = Instantiate(newChatParticipantPrefab, newChatParticipantListContent);
                SetupNewChatParticipantObject(participantObj, player);
                participantObjects[player.ActorNumber] = participantObj;
            }
        }
    }

    private void SetupNewChatParticipantObject(GameObject participantObj, Player player)
    {
        Image avatarImage = participantObj.transform.Find("Avatar").GetComponent<Image>();
        TextMeshProUGUI nameText = participantObj.transform.Find("Name").GetComponent<TextMeshProUGUI>();
        Image statusCircle = participantObj.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
        Toggle selectionToggle = participantObj.GetComponentInChildren<Toggle>();

        if (player.CustomProperties.TryGetValue("avatar", out object avatarObj) && avatarObj is int avatarIndex)
        {
            avatarImage.sprite = Participants.Instance.avatarSprites[avatarIndex];
        }

        nameText.text = player.NickName;

        UpdateParticipantStatus(statusCircle, player);

        selectionToggle.onValueChanged.AddListener((isOn) => OnNewChatParticipantSelected(player, isOn));

        participantObj.name = $"Participant_{player.ActorNumber}";
    }

    private void UpdateParticipantStatus(Image statusCircle, Player player)
    {
        if (player.CustomProperties.TryGetValue("AvailabilityStatus", out object availabilityStatus))
        {
            AvailabilityManager.AvailabilityStatus status = (AvailabilityManager.AvailabilityStatus)availabilityStatus;
            Color statusColor = Participants.Instance.GetStatusColor(status);
            statusCircle.color = statusColor;
        }
    }

    private void OnNewChatParticipantSelected(Player player, bool isSelected)
    {
        if (isSelected)
        {
            if (!selectedParticipants.Contains(player))
            {
                selectedParticipants.Add(player);
            }
        }
        else
        {
            selectedParticipants.Remove(player);
        }
        createChatButton.gameObject.SetActive(selectedParticipants.Count > 0);
    }

    private bool ChatExistsWithPlayer(Player player)
    {
        string chatId = GetChatId(player);
        return chatHistory.ContainsKey(chatId) || emptyChatPartners.ContainsKey(chatId);
    }

    private void OnNewChatSearchValueChanged(string searchText)
    {
        searchText = searchText.ToLower();
        foreach (var kvp in participantObjects)
        {
            GameObject participantObj = kvp.Value;
            TextMeshProUGUI nameText = participantObj.transform.Find("Name").GetComponent<TextMeshProUGUI>();
            bool matchesSearch = nameText.text.ToLower().Contains(searchText);
            participantObj.SetActive(matchesSearch);
        }
    }

    private void OnNewChatEndEdit(string searchText)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Debug.Log("Enter key pressed!");
            int currentCaretPosition = newChatSearchInput.caretPosition;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(newChatSearchInput.gameObject);

            StartCoroutine(SetPosition());

            IEnumerator SetPosition()
            {
                int width = newChatSearchInput.caretWidth;
                newChatSearchInput.caretWidth = 0;

                yield return new WaitForEndOfFrame();

                newChatSearchInput.caretWidth = width;
                newChatSearchInput.caretPosition = currentCaretPosition;
            }
        }
    }

    private void ShowChatList()
    {
        chatListContent.SetActive(true);
        chatWindowContent.SetActive(false);
        newChatContent.SetActive(false);
        UpdateChatList();
    }

    private void CreateNewChats()
    {
        if (selectedParticipants.Count > 0)
        {
            Player firstParticipant = selectedParticipants[0];
            string chatId = GetChatId(firstParticipant);

            if (!chatHistory.ContainsKey(chatId) && !emptyChatPartners.ContainsKey(chatId))
            {
                emptyChatPartners[chatId] = firstParticipant;
            }

            selectedParticipants.Clear();
            ShowChatWindow(firstParticipant);
        }
    }

    private void ShowChatWindow(Player participant)
    {
        messageListContent.GetComponent<CanvasGroup>().gameObject.SetActive(false);
        StartCoroutine(wait());

        if (currentChatPartner != null)
        {
            string previousChatId = GetChatId(currentChatPartner);
            savedInputs[previousChatId] = messageInput.text;
        }

        chatListContent.SetActive(false);
        newChatContent.SetActive(false);
        chatWindowContent.SetActive(true);

        currentChatPartner = participant;
        isOpen = true; // Ensure logic knows window is requested open

        string localUserId = APIManager.Instance.userId;
        string participantUserId = participant.UserId;

        if (string.IsNullOrEmpty(localUserId) || string.IsNullOrEmpty(participantUserId))
        {
            Debug.LogError($"[PrivateChat] Cannot load chat history. UserIDs missing.");
            return;
        }

        ClearMessageUI();

        ChatBackendManager.Instance.LoadPrivateChat(
                localUserId,
                participantUserId,
                50,
                (loadedMessages) =>
                {
                    string chatId = GetChatId(participant);

                    // Convert DTOs to local ChatMessage objects
                    List<ChatMessage> incomingHistory = new List<ChatMessage>();
                    foreach (var msgDto in loadedMessages.OrderBy(m => DateTime.Parse(m.createdAt)))
                    {
                        incomingHistory.Add(new ChatMessage
                        {
                            Content = msgDto.text,
                            Timestamp = DateTime.Parse(msgDto.createdAt).ToUniversalTime(),
                            IsLocalPlayer = (msgDto.senderId == localUserId)
                        });
                    }

                    // Check if chatHistory already has entries (real-time messages received during fetch)
                    if (chatHistory.ContainsKey(chatId))
                    {
                        // Prepend loaded messages to existing real-time messages to avoid data loss
                        chatHistory[chatId].InsertRange(0, incomingHistory);
                    }
                    else
                    {
                        chatHistory[chatId] = incomingHistory;
                    }

                    // FIX DUPLICATION: If we have messages now, remove this chat from "emptyChatPartners"
                    // because it will now be rendered by the chatHistory loop in UpdateChatList.
                    if (chatHistory[chatId].Count > 0 && emptyChatPartners.ContainsKey(chatId))
                    {
                        emptyChatPartners.Remove(chatId);
                    }

                    UpdateChatMessagesUI();
                    UpdateChatList(); // Refresh the list to reflect the move from "Empty" to "History"
                });

        string chatIdCurrent = GetChatId(participant);
        messageInput.text = savedInputs.ContainsKey(chatIdCurrent) ? savedInputs[chatIdCurrent] : "";
        chatWindowTitle.text = participant.NickName;

        if (participant.CustomProperties.TryGetValue("avatar", out object avatarObj) && avatarObj is int avatarIndex)
        {
            chatWindowAvatar.sprite = Participants.Instance.avatarSprites[avatarIndex];
        }

        Image statusCircle = chatWindowAvatar.transform.Find("Status Circle/Status Circle").GetComponent<Image>();
        UpdateParticipantStatus(statusCircle, participant);

        UpdateChatMessages();
    }

    IEnumerator wait()
    {
        yield return new WaitForSeconds(0.05f);
        foreach (var item in messageListContent.GetComponentsInChildren<TextParagraph>())
        {
            item.cg.ForEach(cgc => cgc.ignoreParentGroups = false);
            item.UpdateTransform();
        }
        messageListContent.GetComponent<CanvasGroup>().gameObject.SetActive(true);
        messageListContent.GetComponent<CanvasGroup>().alpha = 0f;
        StartCoroutine(waitt());
    }
    IEnumerator waitt()
    {
        yield return new WaitForSeconds(0.01f);
        foreach (var item in messageListContent.GetComponentsInChildren<TextParagraph>())
        {
            item.UpdateTransform();
        }
        StartCoroutine(waittt());
    }
    IEnumerator waittt()
    {
        yield return new WaitForSeconds(0.01f);
        messageListContent.GetComponent<CanvasGroup>().alpha = 1f;
        foreach (var item in messageListContent.GetComponentsInChildren<TextParagraph>())
        {
            item.cg.ForEach(cgc => cgc.ignoreParentGroups = true);
        }
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void UpdateChatMessages()
    {
        foreach (Transform child in messageListContent)
        {
            Destroy(child.gameObject);
        }

        string chatId = GetChatId(currentChatPartner);
        if (chatHistory.ContainsKey(chatId))
        {
            DateTime lastDate = DateTime.MinValue;
            foreach (ChatMessage message in chatHistory[chatId])
            {
                if (message.Timestamp.Date != lastDate.Date)
                {
                    CreateDateSeparator(message.Timestamp);
                    lastDate = message.Timestamp.ToLocalTime();
                }
                CreateMessageObject(message);
            }
        }
    }

    private void CreateDateSeparator(DateTime date)
    {
        GameObject separatorObj = Instantiate(dateSeparatorPrefab, messageListContent);
        TextMeshProUGUI dateText = separatorObj.GetComponentInChildren<TextMeshProUGUI>();
        dateText.text = date.ToString("MMMM d, yyyy");
    }

    private void CreateMessageObject(ChatMessage message)
    {
        TextParagraph messageObj = Instantiate(messagePrefab, messageListContent).GetComponent<TextParagraph>();
        TextMeshProUGUI senderText = messageObj.senderText;
        Image avatarImage = messageObj.avatarImage;
        TMP_InputField contentText = messageObj.contentText;
        contentText.gameObject.name = "PrivateChatMessage_" + System.Guid.NewGuid().ToString();

        messageObj.gameObject.SetActive(true);

        TMPWebGLInputFieldExtension extension = contentText.gameObject.AddComponent<TMPWebGLInputFieldExtension>();
        extension.SetAsMessageField();

        TextMeshProUGUI timeText = messageObj.timeText;

        senderText.text = message.IsLocalPlayer ? "You" : currentChatPartner.NickName;

        string formattedContent = FormatMessageWithClickableLinks(message.Content);
        contentText.text = formattedContent;

        TimeZoneInfo localZone = TimeZoneInfo.Local;
        DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(message.Timestamp, localZone);
        timeText.text = localTime.ToString("HH:mm");

        if (message.IsLocalPlayer)
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("avatar", out object avatarObj) && avatarObj is int avatarIndex)
            {
                avatarImage.sprite = Participants.Instance.avatarSprites[avatarIndex];
            }
        }
        else
        {
            if (currentChatPartner.CustomProperties.TryGetValue("avatar", out object avatarObj) && avatarObj is int avatarIndex)
            {
                avatarImage.sprite = Participants.Instance.avatarSprites[avatarIndex];
            }
        }

        EventTrigger eventTrigger = contentText.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry clickEntry = new EventTrigger.Entry();
        clickEntry.eventID = EventTriggerType.PointerClick;
        clickEntry.callback.AddListener((eventData) => { OnLinkClicked((PointerEventData)eventData, contentText.textComponent); });
        eventTrigger.triggers.Add(clickEntry);

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((eventData) => { StartCursorCheck(contentText.textComponent); });
        eventTrigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((eventData) =>
        {
            if (cursorCheckCoroutine != null)
            {
                StopCoroutine(cursorCheckCoroutine);
                cursorCheckCoroutine = null;
            }
            UpdateCursor(contentText.textComponent);
        });
        eventTrigger.triggers.Add(exitEntry);

        contentText.text = contentText.text.Replace("\r\n", "");
        messageObj.UpdateTransform();
        StartCoroutine(WaitAndUpdate(messageObj));
        messageInput.text = "";
    }

    IEnumerator WaitAndUpdate(TextParagraph messageObj)
    {
        yield return new WaitForSeconds(0.0011f);
        messageObj.UpdateTransform();
        messageObj.GetComponent<CanvasGroup>().alpha = 1.0f;
        messageObj.gameObject.SetActive(true);
    }

    private string FormatMessageWithClickableLinks(string message)
    {
        linkDictionary.Clear();
        int linkCount = 0;
        return urlRegex.Replace(message, match =>
        {
            string escapedUrl = System.Web.HttpUtility.HtmlEncode(match.Value);
            linkCount++;
            string linkId = $"link_{linkCount}";
            linkDictionary[linkId] = match.Value;
            return $"<link=\"{linkId}\"><color=#00FFFF><u>{escapedUrl}</u></color></link>";
        });
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateChatList();
    }

    private void OnLinkClicked(PointerEventData eventData, TMP_Text textComponent)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, null);
            if (linkIndex != -1)
            {
                TMP_LinkInfo linkInfo = textComponent.textInfo.linkInfo[linkIndex];
                string linkId = linkInfo.GetLinkID();
                if (linkDictionary.TryGetValue(linkId, out string url))
                {
                    Application.OpenURL(url);
                }
            }
        }
    }

    private Coroutine cursorCheckCoroutine;

    private void StartCursorCheck(TMP_Text textComponent)
    {
        if (cursorCheckCoroutine != null)
        {
            StopCoroutine(cursorCheckCoroutine);
        }
        cursorCheckCoroutine = StartCoroutine(CheckCursorOverLink(textComponent));
    }

    private IEnumerator CheckCursorOverLink(TMP_Text textComponent)
    {
        while (true)
        {
            UpdateCursor(textComponent);
            yield return null;
        }
    }

    private void UpdateCursor(TMP_Text textComponent)
    {
        Vector2 mousePosition = Input.mousePosition;
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, mousePosition, null);
#if UNITY_WEBGL && !UNITY_EDITOR
    try
    {
        SetLinkHoverState(gameObject.name, linkIndex != -1);
    }
    catch (System.Exception e)
    {
        Debug.LogWarning("SetLinkHoverState call failed: " + e.Message);
    }
#endif
    }

    private void OnDisable()
    {
        if (cursorCheckCoroutine != null)
        {
            StopCoroutine(cursorCheckCoroutine);
            cursorCheckCoroutine = null;
        }
        // Safety check for DLL import
#if UNITY_WEBGL && !UNITY_EDITOR
        try { SetLinkHoverState(gameObject.name, false); } catch {}
#endif
    }

    private void OnMessageInputEndEdit(string value)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendMessage();
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(messageInput.gameObject);
        }
    }

    public void SendMessage()
    {
        string messageText = messageInput.text.Trim();
        if (string.IsNullOrEmpty(messageText) || currentChatPartner == null) return;

        string chatId = GetChatId(currentChatPartner);
        savedInputs.Remove(chatId);
        DateTime utcNow = DateTime.UtcNow;

        ChatMessage newMessage = new ChatMessage
        {
            Content = messageInput.text,
            Timestamp = DateTime.UtcNow,
            IsLocalPlayer = true
        };

        if (!chatHistory.ContainsKey(chatId))
        {
            chatHistory[chatId] = new List<ChatMessage>();
        }
        chatHistory[chatId].Add(newMessage);

        string recipientDatabaseId = currentChatPartner.UserId;

        emptyChatPartners.Remove(chatId);
        UpdateChatMessages();
        messageInput.text = "";
        ScrollToBottom();

        var dto = new ChatBackendManager.ChatMessageDTO
        {
            messageId = Guid.NewGuid().ToString(),
            senderId = APIManager.Instance.userId,
            senderName = PhotonNetwork.LocalPlayer.NickName,
            text = newMessage.Content,
            createdAt = newMessage.Timestamp.ToString("o"),
            recipientId = recipientDatabaseId,
            isPrivate = true
        };
        ChatBackendManager.Instance.SaveMessage(dto);

        photonView.RPC(nameof(ReceiveMessage), currentChatPartner, PhotonNetwork.LocalPlayer.ActorNumber, newMessage.Content, utcNow.Ticks);
    }

    public void SendMessageToPlayer(Player player, string message)
    {
        string chatId = GetChatId(player);
        string recipientDatabaseId = player.UserId;

        if (!chatHistory.ContainsKey(chatId) && !emptyChatPartners.ContainsKey(chatId))
        {
            emptyChatPartners[chatId] = player;
        }

        ShowChatWindow(player);

        ChatMessage newMessage = new ChatMessage
        {
            Content = message,
            Timestamp = DateTime.UtcNow,
            IsLocalPlayer = true
        };

        var dto = new ChatBackendManager.ChatMessageDTO
        {
            messageId = Guid.NewGuid().ToString(),
            senderId = APIManager.Instance.userId,
            senderName = PhotonNetwork.LocalPlayer.NickName,
            text = newMessage.Content,
            createdAt = newMessage.Timestamp.ToString("o"),
            recipientId = recipientDatabaseId,
            isPrivate = true
        };
        ChatBackendManager.Instance.SaveMessage(dto);

        if (!chatHistory.ContainsKey(chatId))
        {
            chatHistory[chatId] = new List<ChatMessage>();
        }
        chatHistory[chatId].Add(newMessage);

        emptyChatPartners.Remove(chatId);

        UpdateChatMessages();

        photonView.RPC("ReceiveMessage", player, PhotonNetwork.LocalPlayer.ActorNumber, newMessage.Content, newMessage.Timestamp.Ticks);

        UpdateChatList();
    }

    [PunRPC]
    private void ReceiveMessage(int senderActorNumber, string content, long ticks)
    {
        Player sender = PhotonNetwork.CurrentRoom.GetPlayer(senderActorNumber);
        if (sender == null) return;

        string chatId = GetChatId(sender);

        DateTime utcTime = new DateTime(ticks, DateTimeKind.Utc);

        ChatMessage newMessage = new ChatMessage
        {
            Content = content,
            Timestamp = utcTime,
            IsLocalPlayer = false
        };

        if (!chatHistory.ContainsKey(chatId))
        {
            chatHistory[chatId] = new List<ChatMessage>();
        }
        chatHistory[chatId].Add(newMessage);

        if (currentChatPartner != null && currentChatPartner.ActorNumber == sender.ActorNumber && chatWindowContent.activeSelf)
        {
            UpdateChatMessagesUI();
            ScrollToBottom();
        }

        UpdateChatList();

        NotificationManager.Instance.PlayPrivateMessageSound();
        NotificationManager.Instance.IncrementNotification();
    }

    private void UpdateChatList()
    {
        if (chatListTransform == null) return;

        foreach (Transform child in chatListTransform)
        {
            Destroy(child.gameObject);
        }
        chatListTransform.DetachChildren(); // Fixes duplication bug by clearing hierarchy logic immediately

        foreach (KeyValuePair<string, Player> emptyChat in emptyChatPartners)
        {
            CreateChatListItem(emptyChat.Value, null);
        }

        foreach (KeyValuePair<string, List<ChatMessage>> chat in chatHistory)
        {
            if (chat.Value.Count > 0)
            {
                Player chatPartner = GetPlayerFromChatId(chat.Key);
                ChatMessage lastMessage = chat.Value[chat.Value.Count - 1];
                CreateChatListItem(chatPartner, lastMessage);
            }
        }
    }

    private void CreateChatListItem(Player chatPartner, ChatMessage lastMessage)
    {
        if (chatPartner == null) return;

        GameObject chatListItem = Instantiate(chatListItemPrefab, chatListTransform);
        Image avatarImage = chatListItem.transform.Find("Avatar").GetComponent<Image>();
        Image statusCircle = chatListItem.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
        TextMeshProUGUI nameText = chatListItem.transform.Find("Name").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI messageText = chatListItem.transform.Find("Last Message").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI timeText = chatListItem.transform.Find("Time").GetComponent<TextMeshProUGUI>();

        if (chatPartner.CustomProperties.TryGetValue("avatar", out object avatarObj) && avatarObj is int avatarIndex)
        {
            avatarImage.sprite = Participants.Instance.avatarSprites[avatarIndex];
        }

        UpdateParticipantStatus(statusCircle, chatPartner);

        nameText.text = chatPartner.NickName;

        if (lastMessage != null)
        {
            messageText.text = (lastMessage.IsLocalPlayer ? "You: " : "") + lastMessage.Content;
            timeText.text = lastMessage.Timestamp.ToString("MMM dd");
        }
        else
        {
            messageText.text = "No messages yet";
            timeText.text = "";
        }

        Button chatButton = chatListItem.GetComponent<Button>();
        chatButton.onClick.AddListener(() => ShowChatWindow(chatPartner));
    }

    private string GetChatId(Player player)
    {
        string localUserId = PhotonNetwork.LocalPlayer.UserId;
        string otherPlayerId = player.UserId;

        return string.CompareOrdinal(localUserId, otherPlayerId) < 0
            ? $"{localUserId}-{otherPlayerId}"
            : $"{otherPlayerId}-{localUserId}";
    }

    private Player GetPlayerFromChatId(string chatId)
    {
        string[] playerIds = chatId.Split('-');
        string localUserId = PhotonNetwork.LocalPlayer.UserId;
        string otherPlayerId = playerIds[0] == localUserId ? playerIds[1] : playerIds[0];

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.UserId == otherPlayerId)
            {
                return player;
            }
        }
        return null;
    }

    private void ClearMessageUI()
    {
        foreach (Transform child in messageListContent)
        {
            Destroy(child.gameObject);
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("AvailabilityStatus"))
        {
            GameObject participantObj = GameObject.Find($"Participant_{targetPlayer.ActorNumber}");
            if (participantObj != null)
            {
                Image statusCircle = participantObj.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
                UpdateParticipantStatus(statusCircle, targetPlayer);
            }

            UpdateChatList();

            if (currentChatPartner == targetPlayer)
            {
                Image statusCircle = chatWindowAvatar.transform.Find("Status Circle/Status Circle").GetComponent<Image>();
                UpdateParticipantStatus(statusCircle, targetPlayer);
            }
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (isShuttingDown) return;

        string chatId = GetChatId(otherPlayer);
        chatHistory.Remove(chatId);
        emptyChatPartners.Remove(chatId);
        savedInputs.Remove(chatId);
        UpdateChatList();

        if (currentChatPartner == otherPlayer)
        {
            HandleDepartedChatPartner();
        }
    }

    private void HandleDepartedChatPartner()
    {
        if (chatWindowContent == null || !chatWindowContent.activeSelf) return;

        chatWindowTitle.text = $"{currentChatPartner.NickName} (Offline)";

        if (messageInput != null)
        {
            var placeholder = messageInput.placeholder.GetComponent<TMP_Text>();
            if (placeholder != null)
            {
                placeholder.text = "This user has left the room.";
            }
        }
        currentChatPartner = null;
    }

    public void OnChatInputFieldSelect(string text)
    {
        messageInput.placeholder.gameObject.SetActive(false);
        Debug.Log("Input field selected");
    }

    public void OnChatInputFieldDeselect(string text)
    {
        Debug.Log("Input field deselected");
        messageInput.placeholder.gameObject.SetActive(true);
    }

    public void OnSearchInputFieldSelect(string text)
    {
        newChatSearchInput.placeholder.gameObject.SetActive(false);
        Debug.Log("Input field selected");
    }

    public void OnSearchInputFieldDeselect(string text)
    {
        Debug.Log("Input field deselected");
        newChatSearchInput.placeholder.gameObject.SetActive(true);
    }

    private void ScrollToBottom()
    {
        StartCoroutine(SmoothScrollToBottom());
    }

    private IEnumerator SmoothScrollToBottom()
    {
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        ScrollRect scrollRect = messageListContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            float duration = 0.3f;
            float elapsedTime = 0f;
            Vector2 startPosition = scrollRect.normalizedPosition;
            Vector2 targetPosition = new Vector2(0, 0);

            yield return null;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                t = t * t * (3f - 2f * t);
                scrollRect.normalizedPosition = Vector2.Lerp(startPosition, targetPosition, t);
                yield return null;
            }
            scrollRect.normalizedPosition = targetPosition;
        }
    }

    public void OpenChatWithPlayer(Player targetPlayer)
    {
        if (targetPlayer == null)
        {
            Debug.LogError("OpenChatWithPlayer called with a null targetPlayer.");
            return;
        }
        string chatId = GetChatId(targetPlayer);

        if (!chatHistory.ContainsKey(chatId) && !emptyChatPartners.ContainsKey(chatId))
        {
            Debug.Log($"Creating new empty chat session with {targetPlayer.NickName}");
            emptyChatPartners[chatId] = targetPlayer;
        }

        UpdateChatList();
        ShowChatWindow(targetPlayer);
    }

    private void UpdateChatMessagesUI()
    {
        ClearMessageUI();

        string chatId = GetChatId(currentChatPartner);
        if (chatHistory.ContainsKey(chatId))
        {
            DateTime lastDate = DateTime.MinValue;
            foreach (ChatMessage message in chatHistory[chatId])
            {
                if (message.Timestamp.Date != lastDate.Date)
                {
                    CreateDateSeparator(message.Timestamp);
                    lastDate = message.Timestamp.ToLocalTime();
                }
                CreateMessageObject(message);
            }
        }
        ScrollToBottom();
    }
}

public class ChatMessage
{
    public string Content;
    public DateTime Timestamp;
    public bool IsLocalPlayer;
}