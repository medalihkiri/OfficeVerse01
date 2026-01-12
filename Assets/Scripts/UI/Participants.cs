using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class Participants : MonoBehaviourPunCallbacks
{
    [HideInInspector]
    public static Participants Instance;

    private PhotonView _photonView;

    public float hoverDuration = 0.2f; // Kept for list item animations
    public GameObject participantPrefab;
    public Transform participantListContent;
    public Sprite[] avatarSprites;
    public TMP_InputField searchInputField;

    private Dictionary<int, GameObject> participantObjects = new Dictionary<int, GameObject>();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SetButtonHoverState(string gameObjectName, bool isHovering);

    // Profile fields
    public GameObject mainPlayerProfilePrefab;
    public GameObject otherPlayerProfilePrefab;
    private PlayerProfile currentProfile;

    public float profileWindowWidth = 400f;
    public Canvas mainCanvas;

    public TextMeshProUGUI participantsNumberText;

    // Notification fields
    public GameObject waveNotificationPrefab;
    public Transform notificationParent;
    private List<GameObject> activeNotifications = new List<GameObject>();

    private void Start()
    {
        Instance = this;
        _photonView = GetComponent<PhotonView>();

        // Removed ToggleButton listener and MenuPanel positioning

        searchInputField.onSelect.AddListener(OnSearchInputFieldSelect);
        searchInputField.onDeselect.AddListener(OnSearchInputFieldDeselect);

        // Initialize the participant list
        UpdateParticipantList();

        // Setup search functionality
        searchInputField.onValueChanged.AddListener(OnSearchValueChanged);
        searchInputField.onEndEdit.AddListener(OnSearchEndEdit);

        // Initialize the participant number
        UpdateParticipantsNumber();
    }

    private void Update()
    {
        // Logic for closing the Profile Window if clicking outside of it
        if (Input.GetMouseButtonDown(0) && currentProfile != null)
        {
            Vector2 mousePosition = Input.mousePosition;
            bool clickedOutside = !RectTransformUtility.RectangleContainsScreenPoint(currentProfile.GetComponent<RectTransform>(), mousePosition, null);

            // Removed check for MenuPanel click

            bool clickedOutsideEmoji = true;
            if (currentProfile.gameObject.name == "Main Player Profile(Clone)")
            {
                if (currentProfile.transform.Find("Status Bar/Icon").GetComponent<EmojiChatButtonController>().isExpanded)
                {
                    clickedOutsideEmoji = !RectTransformUtility.RectangleContainsScreenPoint(currentProfile.transform.Find("Status Bar/Icon").GetComponent<EmojiChatButtonController>().emojiSelectionWindow.GetComponent<RectTransform>(), mousePosition, null);
                }
            }
            else if (currentProfile.gameObject.name == "Other Player Profile(Clone)")
            {
                if (currentProfile.transform.Find("Message Bar/Icon").GetComponent<EmojiChatButtonController>().isExpanded)
                {
                    clickedOutsideEmoji = !RectTransformUtility.RectangleContainsScreenPoint(currentProfile.transform.Find("Message Bar/Icon").GetComponent<EmojiChatButtonController>().emojiSelectionWindow.GetComponent<RectTransform>(), mousePosition, null);
                }
            }

            if (clickedOutside && clickedOutsideEmoji)
            {
                CloseCurrentProfile();
            }
        }
    }

    // Removed ToggleMenu() and SlideMenu()

    private void CloseCurrentProfile()
    {
        if (currentProfile != null)
        {
            // Find the participant associated with the current profile
            foreach (var participantObj in participantObjects.Values)
            {
                ParticipantHoverEffect hoverEffect = participantObj.GetComponent<ParticipantHoverEffect>();
                if (hoverEffect != null)
                {
                    hoverEffect.SetProfileOpen(false);
                }
            }

            currentProfile.Hide();
            currentProfile = null;
        }
    }

    private void ShowProfile(Player player, RectTransform participantRect)
    {
        // Close the current profile if it exists
        CloseCurrentProfile();

        // Determine if this is the main player
        bool isMainPlayer = player == PhotonNetwork.LocalPlayer;

        // Instantiate the appropriate prefab
        GameObject profilePrefab = isMainPlayer ? mainPlayerProfilePrefab : otherPlayerProfilePrefab;
        GameObject profileObject = PhotonNetwork.Instantiate(profilePrefab.name, Vector3.zero, Quaternion.identity);
        profileObject.transform.SetParent(mainCanvas.transform, false);
        profileObject.SetActive(true);

        // Setup the profile
        PlayerProfile profile = profileObject.GetComponent<PlayerProfile>();
        profile.SetProfilePlayer(player);
        int avatarIndex = isMainPlayer ? PlayerDataManager.PlayerAvatar : (int)player.CustomProperties["avatar"];
        profile.SetupProfile(avatarSprites[avatarIndex], player.NickName, isMainPlayer);

        // Update the availability status
        if (player.CustomProperties.TryGetValue("AvailabilityStatus", out object status))
        {
            profile.UpdateAvailabilityStatus((AvailabilityManager.AvailabilityStatus)status);
        }

        // Position the profile
        RectTransform profileRect = profileObject.GetComponent<RectTransform>();
        profileRect.anchorMin = new Vector2(1, 0.5f);
        profileRect.anchorMax = new Vector2(1, 0.5f);
        profileRect.pivot = new Vector2(1, 0.5f);

        // Calculate the position next to the participant
        Canvas.ForceUpdateCanvases();

        Vector3 participantWorldPos = participantRect.transform.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.GetComponent<RectTransform>(), participantWorldPos, null, out Vector2 localPoint);

        float xOffset = profileWindowWidth + 10;
        float canvasHeight = mainCanvas.GetComponent<RectTransform>().rect.height;
        float yPosition = Mathf.Clamp(localPoint.y, profileRect.rect.height / 2 - canvasHeight / 2, canvasHeight / 2 - profileRect.rect.height / 2);

        profileRect.anchoredPosition = new Vector2(-xOffset, yPosition);

        // Show the profile
        profile.Show();

        // Set as current profile
        currentProfile = profile;

        // Set the profile open state for the participant
        ParticipantHoverEffect hoverEffect = participantRect.GetComponent<ParticipantHoverEffect>();
        if (hoverEffect != null)
        {
            hoverEffect.SetProfileOpen(true);
        }
    }

    public void UpdateParticipantList()
    {
        // Store current availability statuses
        Dictionary<int, Color> currentAvailabilityStatuses = new Dictionary<int, Color>();
        foreach (var kvp in participantObjects)
        {
            Image statusCircle = kvp.Value.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
            currentAvailabilityStatuses[kvp.Key] = statusCircle.color;
        }

        // Clear existing participant objects
        foreach (var obj in participantObjects.Values)
        {
            Destroy(obj);
        }
        participantObjects.Clear();

        // Add other players first
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player != PhotonNetwork.LocalPlayer)
            {
                AddParticipant(player, false);
            }
        }

        // Add local player last
        AddParticipant(PhotonNetwork.LocalPlayer, true);

        // Restore availability statuses
        foreach (var kvp in currentAvailabilityStatuses)
        {
            if (participantObjects.TryGetValue(kvp.Key, out GameObject participantObj))
            {
                Image statusCircle = participantObj.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
                statusCircle.color = kvp.Value;
            }
        }

        // Apply search filter
        OnSearchValueChanged(searchInputField.text);
    }

    private void AddParticipant(Player player, bool isLocalPlayer)
    {
        GameObject participantObj = Instantiate(participantPrefab, participantListContent);

        // Set avatar
        Image avatarImage = participantObj.transform.Find("Avatar").GetComponent<Image>();
        int avatarIndex = isLocalPlayer ? PlayerDataManager.PlayerAvatar : (int)player.CustomProperties["avatar"];

        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            Debug.LogError("The 'avatarSprites' array is empty. Please assign sprites in the Unity Inspector.");
            avatarImage.color = Color.magenta;
        }
        else
        {
            avatarIndex = 0; // Default to the first avatar.

            if (isLocalPlayer)
            {
                avatarIndex = PlayerDataManager.PlayerAvatar;
            }
            else if (player.CustomProperties.ContainsKey("avatar") && player.CustomProperties["avatar"] is int)
            {
                avatarIndex = (int)player.CustomProperties["avatar"];
            }
            else
            {
                Debug.LogWarning($"Player {player.NickName} does not have a valid 'avatar' custom property. Defaulting to index 0.");
            }

            if (avatarIndex >= 0 && avatarIndex < avatarSprites.Length)
            {
                avatarImage.sprite = avatarSprites[avatarIndex];
            }
            else
            {
                Debug.LogError($"Avatar index {avatarIndex} is out of bounds for player {player.NickName}. Defaulting to index 0.");
                avatarImage.sprite = avatarSprites[0];
            }
        }
        // Set name
        TextMeshProUGUI nameText = participantObj.transform.Find("Name").GetComponent<TextMeshProUGUI>();
        string playerName = player.NickName;
        nameText.text = playerName + (isLocalPlayer ? " (you)" : "");

        // Set status
        TextMeshProUGUI statusText = participantObj.transform.Find("Status").GetComponent<TextMeshProUGUI>();
        UpdateParticipantStatus(player, statusText);

        // Set availability status
        if (player.CustomProperties.TryGetValue("AvailabilityStatus", out object availabilityStatus))
        {
            AvailabilityManager.AvailabilityStatus status = (AvailabilityManager.AvailabilityStatus)availabilityStatus;
            Color statusColor = GetStatusColor(status);
            Image statusCircle = participantObj.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
            statusCircle.color = statusColor;
        }

        // Handle Wave and Message buttons
        Transform waveButton = participantObj.transform.Find("WaveButton");
        Transform messageButton = participantObj.transform.Find("MessageButton");

        foreach (PlayerController playerController in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.NickName == playerController.nicknameText.text)
            {
                playerController.GetComponent<PlayerInfo>().player = player;
                playerController.GetComponent<PlayerInfo>().isLocalPlayer = isLocalPlayer;
                playerController.GetComponent<PlayerInfo>().waveBtnAction = () => SendWave(player);
                playerController.GetComponent<PlayerInfo>().messageBtnAction = () => OpenMessageBox(player);
                playerController.GetComponent<PlayerInfo>().requestBtnAction = () => SendRequest(player);
            }
        }

        if (waveButton != null && messageButton != null)
        {
            // Only show wave and message buttons for other players
            waveButton.gameObject.SetActive(!isLocalPlayer);
            messageButton.gameObject.SetActive(!isLocalPlayer);

            if (!isLocalPlayer)
            {
                // Add wave button listener
                Button waveBtn = waveButton.GetComponent<Button>();
                if (waveBtn != null)
                {
                    waveBtn.onClick.AddListener(() => SendWave(player));
                }

                // Add message button listener
                Button msgBtn = messageButton.GetComponent<Button>();
                if (msgBtn != null)
                {
                    msgBtn.onClick.AddListener(() => OpenMessageBox(player));
                }
            }
        }

        // Add hover effect to participant object (list item)
        Image backgroundImage = participantObj.GetComponent<Image>();
        if (backgroundImage != null)
        {
            ParticipantHoverEffect hoverEffect = participantObj.AddComponent<ParticipantHoverEffect>();
            hoverEffect.targetImage = backgroundImage;
            hoverEffect.hoverDuration = hoverDuration;
        }

        // Add click listener for profile
        Button participantButton = participantObj.GetComponent<Button>();
        if (participantButton == null)
        {
            participantButton = participantObj.AddComponent<Button>();
        }
        RectTransform participantRect = participantObj.GetComponent<RectTransform>();
        participantButton.onClick.AddListener(() => ShowProfile(player, participantRect));

        // Store the participant object
        participantObjects[player.ActorNumber] = participantObj;

        // Set the sibling index to ensure local player is last
        if (isLocalPlayer)
        {
            participantObj.transform.SetAsLastSibling();
        }
    }

    private void UpdateParticipantStatus(Player player, TextMeshProUGUI statusText)
    {
        if (player.CustomProperties.TryGetValue("PlayerStatus", out object statusObj))
        {
            statusText.text = statusObj.ToString();
        }
        else
        {
            statusText.text = "";
        }
    }

    public void UpdateParticipantAvailability(Player player, Color statusColor)
    {
        if (participantObjects.TryGetValue(player.ActorNumber, out GameObject participantObj))
        {
            Image statusCircle = participantObj.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
            statusCircle.color = statusColor;
        }
    }
    private void UpdateParticipantCustomStatus(Player player, string customStatus)
    {
        if (participantObjects.TryGetValue(player.ActorNumber, out GameObject participantObj))
        {
            TextMeshProUGUI statusText = participantObj.transform.Find("Status").GetComponent<TextMeshProUGUI>();
            statusText.text = customStatus;
        }
    }
    public void UpdateParticipantStatus(Player player, string status, Color statusColor)
    {
        if (participantObjects.TryGetValue(player.ActorNumber, out GameObject participantObj))
        {
            Image statusCircle = participantObj.transform.Find("Avatar/Status Circle/Status Circle").GetComponent<Image>();
            TextMeshProUGUI statusText = participantObj.transform.Find("Status").GetComponent<TextMeshProUGUI>();

            statusCircle.color = statusColor;
            statusText.text = status;
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (changedProps.ContainsKey("AvailabilityStatus"))
        {
            int statusValue = (int)changedProps["AvailabilityStatus"];
            AvailabilityManager.AvailabilityStatus status = (AvailabilityManager.AvailabilityStatus)statusValue;
            Color statusColor = GetStatusColor(status);

            UpdateParticipantAvailability(targetPlayer, statusColor);
        }

        if (changedProps.ContainsKey("PlayerStatus"))
        {
            string customStatus = changedProps["PlayerStatus"] as string;
            UpdateParticipantCustomStatus(targetPlayer, customStatus);
        }
    }

    public Color GetStatusColor(AvailabilityManager.AvailabilityStatus status)
    {
        switch (status)
        {
            case AvailabilityManager.AvailabilityStatus.Available:
                return Color.green;
            case AvailabilityManager.AvailabilityStatus.Busy:
                return Color.yellow;
            case AvailabilityManager.AvailabilityStatus.DoNotDisturb:
                return Color.red;
            default:
                return Color.green;
        }
    }

    private string GetStatusString(AvailabilityManager.AvailabilityStatus status)
    {
        switch (status)
        {
            case AvailabilityManager.AvailabilityStatus.Available:
                return "Available";
            case AvailabilityManager.AvailabilityStatus.Busy:
                return "Busy";
            case AvailabilityManager.AvailabilityStatus.DoNotDisturb:
                return "Do Not Disturb";
            default:
                return "Available";
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AddParticipant(newPlayer, false);
        EnsureLocalPlayerLast();
        OnSearchValueChanged(searchInputField.text);
        UpdateParticipantsNumber();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (participantObjects.TryGetValue(otherPlayer.ActorNumber, out GameObject obj))
        {
            Destroy(obj);
            participantObjects.Remove(otherPlayer.ActorNumber);
        }
        OnSearchValueChanged(searchInputField.text);
        UpdateParticipantsNumber();
    }

    private void UpdateParticipantsNumber()
    {
        try
        {
            if (participantsNumberText != null)
            {
                participantsNumberText.text = PhotonNetwork.CurrentRoom.PlayerCount.ToString();
            }
        }
        catch { }
    }

    private void EnsureLocalPlayerLast()
    {
        if (participantObjects.TryGetValue(PhotonNetwork.LocalPlayer.ActorNumber, out GameObject localPlayerObj))
        {
            localPlayerObj.transform.SetAsLastSibling();
        }
    }

    private void OnSearchValueChanged(string searchText)
    {
        searchText = searchText.ToLower();
        foreach (var participantObj in participantObjects.Values)
        {
            TextMeshProUGUI nameText = participantObj.transform.Find("Name").GetComponent<TextMeshProUGUI>();
            bool matchesSearch = nameText.text.ToLower().Contains(searchText);
            if (participantObj.activeInHierarchy)
            {
                StartCoroutine(SmoothSetActive(participantObj, matchesSearch));
            }
            else
            {
                // If the object is inactive, just set its state directly.
                participantObj.SetActive(matchesSearch);
            }
        }
    }

    private void OnSearchEndEdit(string searchText)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Debug.Log("Enter key pressed!");
            int currentCaretPosition = searchInputField.caretPosition;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(searchInputField.gameObject);

            StartCoroutine(SetPosition());

            IEnumerator SetPosition()
            {
                int width = searchInputField.caretWidth;
                searchInputField.caretWidth = 0;

                yield return new WaitForEndOfFrame();

                searchInputField.caretWidth = width;
                searchInputField.caretPosition = currentCaretPosition;
            }
        }
    }

    private IEnumerator SmoothSetActive(GameObject obj, bool active)
    {
        if (obj == null) yield break;

        CanvasGroup canvasGroup = obj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = obj.AddComponent<CanvasGroup>();
        }

        float targetAlpha = active ? 1f : 0f;
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < hoverDuration && canvasGroup != null)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / hoverDuration;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            }
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = targetAlpha;
            obj.SetActive(active);
        }
    }

    public void OnSearchInputFieldSelect(string text)
    {
        searchInputField.placeholder.gameObject.SetActive(false);
        Debug.Log("Input field selected");
    }

    public void OnSearchInputFieldDeselect(string text)
    {
        Debug.Log("Input field deselected");
        searchInputField.placeholder.gameObject.SetActive(true);
    }

    // Wave and Message functionality
    private void SendWave(Player targetPlayer)
    {
        if (targetPlayer == null)
        {
            Debug.LogError("Cannot send wave: target player is null");
            return;
        }

        if (_photonView == null)
        {
            Debug.LogError("Cannot send wave: PhotonView component is missing!");
            return;
        }

        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("Cannot send wave: Not connected to Photon Network!");
            return;
        }

        try
        {
            // Send wave notification through Photon
            string message = $"{PhotonNetwork.LocalPlayer.NickName} waved to you!";
            _photonView.RPC("ReceiveWaveRPC", targetPlayer, message);
            Debug.Log("Wave sent to " + targetPlayer.NickName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to send wave: {e.Message}");
        }
    }
    private void SendRequest(Player targetPlayer)
    {
        if (targetPlayer == null)
        {
            Debug.LogError("Cannot send wave: target player is null");
            return;
        }

        if (_photonView == null)
        {
            Debug.LogError("Cannot send wave: PhotonView component is missing!");
            return;
        }

        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("Cannot send wave: Not connected to Photon Network!");
            return;
        }

        try
        {
            // Send wave notification through Photon
            string message = $"{PhotonNetwork.LocalPlayer.NickName} request to join you!";
            _photonView.RPC("ReceiveRequestRPC", targetPlayer, message);
            Debug.Log("Request sent to " + targetPlayer.NickName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to send Request: {e.Message}");
        }
    }

    [PunRPC]
    private void ReceiveWaveRPC(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("Received invalid wave message");
            return;
        }

        Debug.Log(message);

        // Create wave notification UI
        if (waveNotificationPrefab != null && notificationParent != null)
        {
            GameObject notification = Instantiate(waveNotificationPrefab, notificationParent);

            // Set up notification components
            Transform avatarTransform = notification.transform.Find("Avatar");
            Transform nameTransform = notification.transform.Find("Name");
            Transform timeTransform = notification.transform.Find("Time");
            Button closeButton = notification.transform.Find("Cross Button").GetComponent<Button>();

            // Get the sender's info from the message
            string senderName = message.Replace(" waved to you!", "");
            Player sender = GetPlayerByName(senderName);

            if (sender != null && sender.CustomProperties.TryGetValue("avatar", out object avatarObj))
            {
                // Set avatar
                if (avatarTransform != null)
                {
                    Image avatarImage = avatarTransform.GetComponent<Image>();
                    int avatarIndex = (int)avatarObj;
                    avatarImage.sprite = avatarSprites[avatarIndex];
                }
            }

            // Set name and status in the same field
            if (nameTransform != null)
            {
                TextMeshProUGUI nameText = nameTransform.GetComponent<TextMeshProUGUI>();
                nameText.text = $"{senderName} waved at you";
            }

            // Set time to "Right now"
            if (timeTransform != null)
            {
                TextMeshProUGUI timeText = timeTransform.GetComponent<TextMeshProUGUI>();
                timeText.text = "Right now";
            }

            // Add close button functionality
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() =>
                {
                    activeNotifications.Remove(notification);
                    Destroy(notification);
                });
            }

            // Add to active notifications
            activeNotifications.Add(notification);

            // Auto-destroy after 5 seconds
            StartCoroutine(DestroyNotificationAfterDelay(notification));
        }

        // Play notification sound
        if (NotificationManager.Instance != null)
        {
            NotificationManager.Instance.PlayWaveSound();
        }
    }
    [PunRPC]
    private void ReceiveRequestRPC(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("Received invalid wave message");
            return;
        }

        Debug.Log(message);

        // Create wave notification UI
        if (waveNotificationPrefab != null && notificationParent != null)
        {
            GameObject notification = Instantiate(waveNotificationPrefab, notificationParent);

            // Set up notification components
            Transform avatarTransform = notification.transform.Find("Avatar");
            Transform nameTransform = notification.transform.Find("Name");
            Transform timeTransform = notification.transform.Find("Time");
            Button closeButton = notification.transform.Find("Cross Button").GetComponent<Button>();

            // Get the sender's info from the message
            string senderName = message.Replace("request to join you!", "");
            Player sender = GetPlayerByName(senderName);

            if (sender != null && sender.CustomProperties.TryGetValue("avatar", out object avatarObj))
            {
                // Set avatar
                if (avatarTransform != null)
                {
                    Image avatarImage = avatarTransform.GetComponent<Image>();
                    int avatarIndex = (int)avatarObj;
                    avatarImage.sprite = avatarSprites[avatarIndex];
                }
            }

            // Set name and status in the same field
            if (nameTransform != null)
            {
                TextMeshProUGUI nameText = nameTransform.GetComponent<TextMeshProUGUI>();
                nameText.text = $"{senderName} request to join you!";
            }

            // Set time to "Right now"
            if (timeTransform != null)
            {
                TextMeshProUGUI timeText = timeTransform.GetComponent<TextMeshProUGUI>();
                timeText.text = "Right now";
            }

            // Add close button functionality
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() =>
                {
                    activeNotifications.Remove(notification);
                    Destroy(notification);
                });
            }

            // Add to active notifications
            activeNotifications.Add(notification);

            // Auto-destroy after 5 seconds
            StartCoroutine(DestroyNotificationAfterDelay(notification));
        }

        // Play notification sound
        if (NotificationManager.Instance != null)
        {
            NotificationManager.Instance.PlayWaveSound();
        }
    }

    private IEnumerator DestroyNotificationAfterDelay(GameObject notification)
    {
        yield return new WaitForSeconds(5f);
        if (notification != null && activeNotifications.Contains(notification))
        {
            activeNotifications.Remove(notification);
            Destroy(notification);
        }
    }

    private Player GetPlayerByName(string playerName)
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.NickName == playerName)
            {
                return player;
            }
        }
        return null;
    }

    private void OpenMessageBox(Player targetPlayer)
    {
        // Get reference to your chat system
        PrivateChat privateChat = PrivateChat.Instance;
        if (privateChat != null)
        {
            // Open chat with the selected player
            privateChat.OpenChatWithPlayer(targetPlayer);
        }
    }
}

// Participant hover effect for list items (kept as it is for UI elements inside the list)
public class ParticipantHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image targetImage;
    public float hoverDuration = 0.2f;

    private float currentAlpha = 0f;
    private bool isHovering = false;

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SetButtonHoverState(string gameObjectName, bool isHovering);

    private bool isProfileOpen = false;

    public void SetProfileOpen(bool open)
    {
        isProfileOpen = open;
        if (open)
        {
            currentAlpha = 1f;
        }
        else
        {
            currentAlpha = isHovering ? 1f : 0f;
        }
        UpdateImageAlpha();
    }

    private void Update()
    {
        if (!isProfileOpen)
        {
            if (isHovering && currentAlpha < 1f)
            {
                currentAlpha += Time.deltaTime / hoverDuration;
            }
            else if (!isHovering && currentAlpha > 0f)
            {
                currentAlpha -= Time.deltaTime / hoverDuration;
            }

            currentAlpha = Mathf.Clamp01(currentAlpha);
            UpdateImageAlpha();
        }
    }

    private void UpdateImageAlpha()
    {
        Color newColor = targetImage.color;
        newColor.a = currentAlpha;
        targetImage.color = newColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        SetButtonHoverState(gameObject.name, true);
#endif
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        SetButtonHoverState(gameObject.name, false);
#endif
    }
}