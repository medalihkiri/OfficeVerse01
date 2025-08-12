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

    private void Awake()
    {
        SP = this;
        chatInputField.onValueChanged.AddListener(OnInputValueChanged);
    }

    private void OnDestroy()
    {
        chatInputField.onValueChanged.RemoveListener(OnInputValueChanged);
    }

    public void Select(string text)
    {
        chatInputField.placeholder.gameObject.SetActive(false);

        Debug.Log("Input field selected");
    }

    public void Deselect(string text)
    {
        Debug.Log("Input field deselected");

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

        // Force the input field to lose focus
        if (EventSystem.current.currentSelectedGameObject == chatInputField.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void SendChat(string text)
    {
        view.RPC(nameof(SendChatMessage), RpcTarget.All, text);
    }

    [PunRPC]
    void SendChatMessage(string text, PhotonMessageInfo info)
    {
        // Play notification sound if message is from another player
        if (info.Sender != PhotonNetwork.LocalPlayer)
        {
            NotificationManager.Instance.PlayPublicMessageSound();
        }
        
        GameObject messageObj = Instantiate(chatMessagePrefab.gameObject, chatMessagesContent);
        messageObj.name = "ChatMessage_" + System.Guid.NewGuid().ToString();
        TMP_InputField messageInputField = messageObj.GetComponent<TMP_InputField>();
        TextMeshProUGUI timeText = messageObj.GetComponentsInChildren<TextMeshProUGUI>()
            .FirstOrDefault(t => t.name == "Time");

        if (messageInputField == null || timeText == null)
        {
            Debug.LogError("Message prefab is not set up correctly. Ensure it has TMP_InputField and TextMeshProUGUI components for message and time.");
            return;
        }
        linkDictionary.Clear(); // Clear previous links
        int linkCount = 0;
        string formattedText = urlRegex.Replace(text, match =>
        {
            linkCount++;
            string linkId = $"link_{linkCount}";
            linkDictionary[linkId] = match.Value;
            return $"<link=\"{linkId}\"><color=#00FFFF><u>{match.Value}</u></color></link>";
        });

        messageInputField.text = $"<b>{info.Sender.NickName}</b>\n{formattedText}";
        string timestamp = DateTime.UtcNow.ToLocalTime().ToString("HH:mm");
        timeText.text = timestamp;

        messageInputField.readOnly = true;

        messageObj.SetActive(true);

        TMPWebGLInputFieldExtension extension = messageObj.AddComponent<TMPWebGLInputFieldExtension>();
        extension.SetAsMessageField();

        EventTrigger eventTrigger = messageObj.AddComponent<EventTrigger>();

        EventTrigger.Entry clickEntry = new EventTrigger.Entry();
        clickEntry.eventID = EventTriggerType.PointerClick;
        clickEntry.callback.AddListener((eventData) => { OnLinkClicked((PointerEventData)eventData, messageInputField.textComponent); });
        eventTrigger.triggers.Add(clickEntry);

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((eventData) => { StartCursorCheck(messageInputField.textComponent); });
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
            UpdateCursor(messageInputField.textComponent);
        });
        eventTrigger.triggers.Add(exitEntry);

        messageObj.transform.SetAsFirstSibling();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        scrollRect.verticalNormalizedPosition = 0f;

        Debug.Log($"Message sent: {text}, Time: {timestamp}");
    }

    private void OnMessageClicked(PointerEventData eventData, TMP_Text textComponent)
    {
        Vector3 mousePosition = eventData.position;
        int linkIndex = TMP_TextUtilities.FindIntersectingWord(textComponent, mousePosition, Camera.main);

        if (linkIndex != -1)
        {
            TMP_WordInfo wordInfo = textComponent.textInfo.wordInfo[linkIndex];
            string word = textComponent.text.Substring(wordInfo.firstCharacterIndex, wordInfo.characterCount);

            if (urlRegex.IsMatch(word))
            {
                string decodedUrl = System.Web.HttpUtility.HtmlDecode(word);
                Application.OpenURL(decodedUrl);
            }
        }
    }

    private void UpdateCursor(TMP_Text textComponent)
    {
        Vector2 mousePosition = Input.mousePosition;
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, mousePosition, null);
        try { SetLinkHoverState(gameObject.name, linkIndex != -1); } catch { }
    }

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

    private string FormatMessageWithClickableLinks(string message)
    {
        int linkCount = 0;
        return urlRegex.Replace(message, match =>
        {
            string escapedUrl = System.Web.HttpUtility.HtmlEncode(match.Value);
            linkCount++;
            return $"<link=\"link_{linkCount}\"><color=#00FFFF><u>{escapedUrl}</u></color></link>";
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
