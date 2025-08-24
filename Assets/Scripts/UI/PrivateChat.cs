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

    [Header("Main UI Elements")]
    public RectTransform menuPanel;
    public float slideDuration = 0.5f;
    public float slideDistance = 400f;
    public Button toggleButton;
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
        menuPanel.anchoredPosition = menuPanel.anchoredPosition + new Vector2(slideDistance, 0);

        toggleButton.onClick.AddListener(ToggleMenu);
        chatCloseButton.onClick.AddListener(CloseMenu);
        chatListCloseButton.onClick.AddListener(CloseMenu);
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

        menuPanel.gameObject.SetActive(false);
        chatListContent.SetActive(true);
        chatWindowContent.SetActive(false);
        newChatContent.SetActive(false);
        createChatButton.gameObject.SetActive(false);
    }

    public void ToggleMenu()
    {
        if (!isOpen)
        {
            OpenMenu();
        }
        else
        {
            CloseMenu();
        }
    }

    public void OpenMenu()
    {
        GetComponent<ButtonHoverEffect>().enabled = false;
        GetComponent<ButtonHoverEffect>().targetImage.color = new Color(GetComponent<ButtonHoverEffect>().targetImage.color.r, GetComponent<ButtonHoverEffect>().targetImage.color.g, GetComponent<ButtonHoverEffect>().targetImage.color.b, 100f);

        Participants participantsMenu = Participants.Instance;
        if (participantsMenu != null && participantsMenu.isOpen)
        {
            participantsMenu.ToggleMenu();
        }

        menuPanel.gameObject.SetActive(true);
        StartCoroutine(SlideMenu(true));
        isOpen = true;
        UpdateChatList();

        // Clear notifications when opening chat
        NotificationManager.Instance.ClearNotifications();
    }

    public void CloseMenu()
    {
        GetComponent<ButtonHoverEffect>().enabled = true;
        //GetComponent<ButtonHoverEffect>().isHovering = false;

        StartCoroutine(SlideMenu(false));
        isOpen = false;
    }

    private IEnumerator SlideMenu(bool open)
    {
        toggleButton.interactable = false;
        chatCloseButton.interactable = false;
        chatListCloseButton.interactable = false;

        float elapsedTime = 0f;
        Vector2 startPosition = menuPanel.anchoredPosition;
        Vector2 endPosition = open ? startPosition - new Vector2(slideDistance, 0) : startPosition + new Vector2(slideDistance, 0);

        while (elapsedTime < slideDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsedTime / slideDuration);
            menuPanel.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
            yield return null;
        }

        menuPanel.anchoredPosition = endPosition;

        if (!open)
        {
            menuPanel.gameObject.SetActive(false);
        }

        toggleButton.interactable = true;
        chatCloseButton.interactable = true;
        chatListCloseButton.interactable = true;
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
            if (player != PhotonNetwork.LocalPlayer && !ChatExistsWithPlayer(player)) // Check if chat already exists
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

        // Store references for status updates
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
        createChatButton.gameObject.SetActive(selectedParticipants.Count > 0); // Update create button state
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
            Player firstParticipant = selectedParticipants[0]; // Take only the first participant
            string chatId = GetChatId(firstParticipant);

            if (!chatHistory.ContainsKey(chatId) && !emptyChatPartners.ContainsKey(chatId))
            {
                emptyChatPartners[chatId] = firstParticipant;
            }

            selectedParticipants.Clear(); // Clear the list after creating the chat

            ShowChatWindow(firstParticipant); // Directly open the chat window
        }
    }

    private void ShowChatWindow(Player participant)
    {
        messageListContent.GetComponent<CanvasGroup>().gameObject.SetActive(false);
        StartCoroutine(wait());
        // Save current input if we're switching from another chat
        if (currentChatPartner != null)
        {
            string currentChatId = GetChatId(currentChatPartner);
            savedInputs[currentChatId] = messageInput.text;
        }

        chatListContent.SetActive(false);
        newChatContent.SetActive(false);
        chatWindowContent.SetActive(true);

        currentChatPartner = participant;
        // Load history from backend
        ChatBackendManager.Instance.LoadPrivateChat(
            PhotonNetwork.LocalPlayer.UserId,  // or UserId
            participant.UserId,                  // must match what you send as recipientId
            50,
            (messages) =>
            {
                string chatId = GetChatId(participant);
                chatHistory[chatId] = new List<ChatMessage>();

                foreach (var m in messages.OrderBy(m => DateTime.Parse(m.createdAt)))
                {
                    chatHistory[chatId].Add(new ChatMessage
                    {
                        Content = m.text,
                        Timestamp = DateTime.Parse(m.createdAt).ToUniversalTime(),
                        IsLocalPlayer = (m.senderName == PhotonNetwork.LocalPlayer.NickName)
                    });
                }

                UpdateChatMessages();
            });
        // Restore saved input for this chat
        string chatId = GetChatId(participant);
        messageInput.text = savedInputs.ContainsKey(chatId) ? savedInputs[chatId] : "";
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

        // Format message content with clickable links
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

        // Add link handling
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
        //contentText.text += "\r\n";
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
    IEnumerator WaitAndUpdatee(TextParagraph messageObj)
    {
        yield return new WaitForSeconds(0.1f);
        messageObj.GetComponent<CanvasGroup>().alpha = 1.0f;
        messageObj.cg.ForEach(cgs => cgs.ignoreParentGroups = true);
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
        try { SetLinkHoverState(gameObject.name, linkIndex != -1); } catch { }
    }

    private void OnDisable()
    {
        if (cursorCheckCoroutine != null)
        {
            StopCoroutine(cursorCheckCoroutine);
            cursorCheckCoroutine = null;
        }
        SetLinkHoverState(gameObject.name, false);
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
        if (string.IsNullOrEmpty(messageInput.text)) return;

        string chatId = GetChatId(currentChatPartner);
        savedInputs.Remove(chatId); // Clear saved input after sending
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

        // Remove from emptyChatPartners if it exists
        emptyChatPartners.Remove(chatId);
        UpdateChatMessages();
        messageInput.text = "";
        ScrollToBottom();
        var dto = new ChatBackendManager.ChatMessageDTO
        {
            messageId = Guid.NewGuid().ToString(),
            senderName = PhotonNetwork.LocalPlayer.NickName,
            text = newMessage.Content,
            createdAt = newMessage.Timestamp.ToString("o"),
            recipientId = currentChatPartner.UserId, // store Photon userId or ActorNumber
            isPrivate = true
        };
        ChatBackendManager.Instance.SaveMessage(dto);

        // Send message to other player using Photon
        photonView.RPC("ReceiveMessage", currentChatPartner, PhotonNetwork.LocalPlayer.ActorNumber, newMessage.Content, newMessage.Timestamp.Ticks);
    }

    public void SendMessageToPlayer(Player player, string message)
    {
        string chatId = GetChatId(player);



        // Create a new chat if it doesn't exist
        if (!chatHistory.ContainsKey(chatId) && !emptyChatPartners.ContainsKey(chatId))
        {
            emptyChatPartners[chatId] = player;
        }

        // Show the chat window
        ShowChatWindow(player);

        if (!isOpen)
        {
            OpenMenu();
        }

        // Send the message
        ChatMessage newMessage = new ChatMessage
        {
            Content = message,
            Timestamp = DateTime.UtcNow,
            IsLocalPlayer = true
        };

        // ✅ Save to backend
        var dto = new ChatBackendManager.ChatMessageDTO
        {
            messageId = Guid.NewGuid().ToString(),
            senderName = PhotonNetwork.LocalPlayer.NickName,
            text = newMessage.Content,
            createdAt = newMessage.Timestamp.ToString("o"),
            recipientId = currentChatPartner.UserId, // store Photon userId or ActorNumber
            isPrivate = true
        };
        ChatBackendManager.Instance.SaveMessage(dto);

        if (!chatHistory.ContainsKey(chatId))
        {
            chatHistory[chatId] = new List<ChatMessage>();
        }
        chatHistory[chatId].Add(newMessage);


        // Remove from emptyChatPartners if it exists
        emptyChatPartners.Remove(chatId);


        UpdateChatMessages();


        // Send message to other player using Photon
        photonView.RPC("ReceiveMessage", player, PhotonNetwork.LocalPlayer.ActorNumber, newMessage.Content, newMessage.Timestamp.Ticks);

        // Update the chat list
        UpdateChatList();
    }


    [PunRPC]
    private void ReceiveMessage(int senderActorNumber, string content, long ticks)
    {
        Player sender = PhotonNetwork.CurrentRoom.GetPlayer(senderActorNumber);
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

        // Remove from emptyChatPartners if it exists
        emptyChatPartners.Remove(chatId);

        if (currentChatPartner == sender && chatWindowContent.activeSelf)
        {
            UpdateChatMessages();
            ScrollToBottom();
        }

        UpdateChatList();

        // Play sound and show notification
        NotificationManager.Instance.PlayPrivateMessageSound();
        NotificationManager.Instance.IncrementNotification();
    }

    private void UpdateChatList()
    {
        foreach (Transform child in chatListTransform)
        {
            Destroy(child.gameObject);
        }

        // Add empty chats
        foreach (KeyValuePair<string, Player> emptyChat in emptyChatPartners)
        {
            CreateChatListItem(emptyChat.Value, null);
        }

        // Add chats with messages
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
        GameObject chatListItem = Instantiate(chatListItemPrefab, chatListTransform);
        Image avatarImage = chatListItem.transform.Find("Avatar").GetComponent<Image>();
        Image statusCircle = chatListItem.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
        TextMeshProUGUI nameText = chatListItem.transform.Find("Name").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI messageText = chatListItem.transform.Find("Last Message").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI timeText = chatListItem.transform.Find("Time").GetComponent<TextMeshProUGUI>();

        // Set avatar
        if (chatPartner.CustomProperties.TryGetValue("avatar", out object avatarObj) && avatarObj is int avatarIndex)
        {
            avatarImage.sprite = Participants.Instance.avatarSprites[avatarIndex];
        }

        // Set availability status
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
        int localPlayerId = PhotonNetwork.LocalPlayer.ActorNumber;
        int otherPlayerId = player.ActorNumber;
        return localPlayerId < otherPlayerId ? $"{localPlayerId}-{otherPlayerId}" : $"{otherPlayerId}-{localPlayerId}";
    }

    private Player GetPlayerFromChatId(string chatId)
    {
        string[] playerIds = chatId.Split('-');
        int otherPlayerId = int.Parse(playerIds[0]) == PhotonNetwork.LocalPlayer.ActorNumber ? int.Parse(playerIds[1]) : int.Parse(playerIds[0]);
        return PhotonNetwork.CurrentRoom.GetPlayer(otherPlayerId);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("AvailabilityStatus"))
        {
            // Update status in new chat window
            GameObject participantObj = GameObject.Find($"Participant_{targetPlayer.ActorNumber}");
            if (participantObj != null)
            {
                Image statusCircle = participantObj.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
                UpdateParticipantStatus(statusCircle, targetPlayer);
            }

            // Update status in chat list
            UpdateChatList();

            // Update status in current chat window if it's the current chat partner
            if (currentChatPartner == targetPlayer)
            {
                Image statusCircle = chatWindowAvatar.transform.Find("Status Circle/Status Circle").GetComponent<Image>();
                UpdateParticipantStatus(statusCircle, targetPlayer);
            }
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        DeleteChat(otherPlayer);
        photonView.RPC("DeleteChatRPC", RpcTarget.All, otherPlayer.ActorNumber);
    }

    private void DeleteChat(Player player)
    {
        string chatId = GetChatId(player);
        chatHistory.Remove(chatId);
        emptyChatPartners.Remove(chatId);
        savedInputs.Remove(chatId);
        UpdateChatList();

        if (currentChatPartner == player)
        {
            ShowChatList();
        }
    }

    [PunRPC]
    private void DeleteChatRPC(int playerActorNumber)
    {
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(playerActorNumber);
        if (player != null)
        {
            DeleteChat(player);
        }
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
        // Wait for end of frame to ensure all layout calculations are complete
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        ScrollRect scrollRect = messageListContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            // Get the actual content height after layout update
            RectTransform contentRectTransform = messageListContent as RectTransform;
            float contentHeight = contentRectTransform.sizeDelta.y;

            float duration = 0.3f;
            float elapsedTime = 0f;
            Vector2 startPosition = scrollRect.normalizedPosition;
            Vector2 targetPosition = new Vector2(0, 0);

            // Wait one more frame to ensure scroll values are updated
            yield return null;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                // Use smooth step for more natural easing
                t = t * t * (3f - 2f * t);
                scrollRect.normalizedPosition = Vector2.Lerp(startPosition, targetPosition, t);
                yield return null;
            }
            scrollRect.normalizedPosition = targetPosition;
        }
    }

    // Add OpenChatWithPlayer method
    public void OpenChatWithPlayer(Player targetPlayer)
    {
        if (!isOpen)
        {
            OpenMenu();
        }
        ShowChatWindow(targetPlayer);
    }
}

public class ChatMessage
{
    public string Content;
    public DateTime Timestamp;
    public bool IsLocalPlayer;
}
