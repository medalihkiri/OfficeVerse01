using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;
using Photon.Pun;
using Photon.Realtime;
using System;

public class PlayerProfile : MonoBehaviourPunCallbacks
{
    public Image avatarImage;
    public TMP_InputField nameInputField;
    public Image availabilityCircle;
    public TextMeshProUGUI availabilityText;
    public TMP_InputField statusInputField;
    public Button editNameButton;
    public TMP_InputField messageInputField;

    private CanvasGroup canvasGroup;
    private bool isMainPlayer;
    private bool isEditingName = false;

    //private Color originalInputFieldColor;
    private float hoverDuration = 0.2f;

    private PhotonView photonView;
    private Player profilePlayer;
    public bool b = false;
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            Debug.LogError("PhotonView component is missing on the PlayerProfile object!");
        }

        if (photonView.IsMine)
        {
            LoadSavedStatus();
        }
        else
        {
            UpdateAvailabilityFromPlayerProperties(photonView.Owner);
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if (!b)
        {
            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
        }

        if (statusInputField != null) statusInputField.onValueChanged.AddListener(OnStatusChanged);
        if (statusInputField != null) statusInputField.onEndEdit.AddListener(OnStatusEndEdit);
        if (statusInputField != null) statusInputField.onSelect.AddListener(OnStatusInputFieldSelect);
        if (statusInputField != null) statusInputField.onDeselect.AddListener(OnStatusInputFieldDeselect);
        if (editNameButton != null) editNameButton.onClick.AddListener(ToggleNameEdit);
        if (nameInputField != null) nameInputField.readOnly = true;
        if (nameInputField != null) nameInputField.onEndEdit.AddListener(OnNameInputEndEdit);
        if (messageInputField != null) messageInputField.onEndEdit.AddListener(OnMessageInputEndEdit);
    }

    private void OnStatusInputFieldSelect(string arg0)
    {
        statusInputField.placeholder.gameObject.SetActive(false);

    }

    private void OnStatusInputFieldDeselect(string arg0)
    {
        statusInputField.placeholder.gameObject.SetActive(true);
    }

    public void SetProfilePlayer(Player player)
    {
        profilePlayer = player;
    }

    private void OnMessageInputEndEdit(string value)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendMessageToPrivateChat(value);
            messageInputField.text = "";
        }
    }

    private void SendMessageToPrivateChat(string message)
    {
        if (string.IsNullOrEmpty(message) || profilePlayer == null) return;

        PrivateChat.Instance.SendMessageToPlayer(profilePlayer, message);
        Hide();
    }

    private void LoadSavedStatus()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("AvailabilityStatus", out object savedStatus))
        {
            UpdateAvailabilityStatus((AvailabilityManager.AvailabilityStatus)savedStatus);
        }
    }

    private void UpdateAvailabilityFromPlayerProperties(Player player)
    {
        if (player.CustomProperties.TryGetValue("AvailabilityStatus", out object status))
        {
            UpdateAvailabilityStatus((AvailabilityManager.AvailabilityStatus)status);
        }
    }

    public void UpdateAvailabilityStatus(AvailabilityManager.AvailabilityStatus status)
    {
        Color statusColor;
        string statusString;

        switch (status)
        {
            case AvailabilityManager.AvailabilityStatus.Available:
                statusColor = Color.green;
                statusString = "Available";
                break;
            case AvailabilityManager.AvailabilityStatus.Busy:
                statusColor = Color.yellow;
                statusString = "Busy";
                break;
            case AvailabilityManager.AvailabilityStatus.DoNotDisturb:
                statusColor = Color.red;
                statusString = "Do Not Disturb";
                break;
            default:
                statusColor = Color.green;
                statusString = "Available";
                break;
        }

        if (availabilityCircle != null)
        {
            availabilityCircle.color = statusColor;
        }

        if (availabilityText != null)
        {
            availabilityText.text = statusString;
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (changedProps.ContainsKey("AvailabilityStatus") && targetPlayer == photonView.Owner)
        {
            int statusValue = (int)changedProps["AvailabilityStatus"];
            UpdateAvailabilityStatus((AvailabilityManager.AvailabilityStatus)statusValue);
        }
    }

    private void OnStatusChanged(string newStatus)
    {
        photonView.RPC("UpdatePlayerStatus", RpcTarget.All, newStatus);
    }

    private void OnStatusEndEdit(string searchText)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Debug.Log("Enter key pressed!");
            int currentCaretPosition = statusInputField.caretPosition;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(statusInputField.gameObject);

            StartCoroutine(SetPosition());

            IEnumerator SetPosition()
            {
                int width = statusInputField.caretWidth;
                statusInputField.caretWidth = 0;

                yield return new WaitForEndOfFrame();

                statusInputField.caretWidth = width;
                statusInputField.caretPosition = currentCaretPosition;
            }
        }
    }

    [PunRPC]
    private void UpdatePlayerStatus(string newStatus)
    {
        if (photonView.IsMine)
        {
            ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
            {
                { "PlayerStatus", newStatus }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
        }
    }

    private void Update()
    {
        if (isEditingName && Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    nameInputField.GetComponent<RectTransform>(),
                    Input.mousePosition,
                    null) &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    editNameButton.GetComponent<RectTransform>(),
                    Input.mousePosition,
                    null))
            {
                EndNameEdit();
            }
        }
    }

    private IEnumerator ChangeInputFieldColor(TMP_InputField inputField, Color targetColor)
    {
        Image inputFieldImage = inputField.GetComponentInParent<Image>();
        Color startColor = inputFieldImage.color;
        float elapsedTime = 0f;

        while (elapsedTime < hoverDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / hoverDuration;
            inputFieldImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        inputFieldImage.color = targetColor;
    }

    public void SetupProfile(Sprite avatar, string playerName, bool isMain)
    {
        avatarImage.sprite = avatar;
        nameInputField.text = playerName;
        isMainPlayer = isMain;

        if (isMainPlayer)
        {
            if (statusInputField != null) statusInputField.gameObject.SetActive(true);
            if (editNameButton != null) editNameButton.gameObject.SetActive(true);
            if (messageInputField != null) messageInputField.gameObject.SetActive(false);
        }
        else
        {
            if (statusInputField != null) statusInputField.gameObject.SetActive(false);
            if (editNameButton != null) editNameButton.gameObject.SetActive(false);
            if (messageInputField != null) messageInputField.gameObject.SetActive(true);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);

        StartCoroutine(FadeIn());
    }

    public void Hide()
    {
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float duration = 0.3f;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1, 0, elapsedTime / duration);
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator FadeIn()
    {
        float duration = 0.3f;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0, 1, elapsedTime / duration);
            yield return null;
        }

        canvasGroup.alpha = 1;

        // Set alpha for text fields
        if (statusInputField != null) statusInputField.placeholder.gameObject.GetComponent<TMP_Text>().alpha = 0.5f;
        if (editNameButton != null) editNameButton.GetComponentInChildren<TMP_Text>().alpha = 0.5f;
        if (messageInputField != null) messageInputField.placeholder.gameObject.GetComponent<TMP_Text>().alpha = 0.5f;
    }

    public bool IsPointInside(Vector2 point)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(GetComponent<RectTransform>(), point, null);
    }

    private IEnumerator ToggleNameEditDelayed()
    {
        // Wait for a short moment to allow deselection event to process
        yield return new WaitForSeconds(0.05f);

        ToggleNameEdit();
    }

    private void ToggleNameEdit()
    {
        isEditingName = !isEditingName;
        nameInputField.readOnly = !isEditingName;
        if (isEditingName)
        {
            nameInputField.Select();
            nameInputField.ActivateInputField();
        }
        else
        {
            isEditingName = !isEditingName;
            EndNameEdit();
        }
    }


    private void OnNameInputEndEdit(string value)
    {
        if (isEditingName && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            EndNameEdit();
        }
    }


    private void SaveNameChanges()
    {
        string newName = nameInputField.text.Trim();
        if (!string.IsNullOrEmpty(newName) && newName != PhotonNetwork.NickName)
        {
            photonView.RPC("UpdatePlayerNameRPC", RpcTarget.AllBuffered, newName, photonView.Owner.ActorNumber);
        }
    }



    private void EndNameEdit()
    {
        if (isEditingName)
        {
            SaveNameChanges();
            isEditingName = false;
            nameInputField.readOnly = true;
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    [PunRPC]
    private void UpdatePlayerNameRPC(string newName, int actorNumber)
    {
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
        if (player != null)
        {
            player.NickName = newName;
            if (player.IsLocal)
            {
                PhotonNetwork.NickName = newName;
                PlayerDataManager.PlayerName = newName;
            }

            nameInputField.text = newName;

            // Update the player's GameObject name
            GameObject playerObj = GameObject.Find($"{player.NickName}_{player.CustomProperties["avatar"]}_Player");
            if (playerObj != null)
            {
                playerObj.name = $"{newName}_{player.CustomProperties["avatar"]}_Player";
            }

            // Update other UI elements or game logic here
            FindObjectOfType<Participants>().UpdateParticipantList();

            // Update the PlayerController
            PlayerController[] playerControllers = FindObjectsOfType<PlayerController>();
            foreach (PlayerController pc in playerControllers)
            {
                if (pc.view.Owner == player)
                {
                    pc.UpdatePlayerName(newName);
                    break;
                }
            }
        }
    }

    public void UpdateAvailabilityStatus(Color statusColor, string statusText)
    {
        if (availabilityCircle != null)
        {
            availabilityCircle.color = statusColor;
        }

        if (availabilityText != null)
        {
            availabilityText.text = statusText;
        }
    }

}