using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public class EmojiButtonController : MonoBehaviourPunCallbacks
{
    public RectTransform mainButton;
    public RectTransform emojiPanel;
    public RectTransform[] emojiButtons;

    [System.Serializable]
    public class EmojiAnimation
    {
        public string emojiName;
        public Animator animator;
    }

    public EmojiAnimation[] emojiAnimations;

    [SerializeField] private GameObject speechBubblePrefab;
    [SerializeField] private float emojiDisplayDuration = 3f;
    [SerializeField] private Vector3 speechBubbleOffset = new Vector3(0, 1, 0);

    private bool isExpanded = false;
    private float expandedWidth;

    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float emojiSpacing = 10f;

    private Dictionary<int, GameObject> activeSpeechBubbles = new Dictionary<int, GameObject>();
    private PhotonView photonView;

    void Start()
    {
        photonView = GetComponent<PhotonView>();
        expandedWidth = emojiPanel.rect.width;
        emojiPanel.sizeDelta = new Vector2(0, emojiPanel.sizeDelta.y);
        emojiPanel.gameObject.SetActive(false);
        SetEmojisAlpha(0);
        mainButton.GetComponent<Button>().onClick.AddListener(ToggleEmojiPanel);

        SetupEmojiButtons();
    }

    void SetupEmojiButtons()
    {
        for (int i = 0; i < emojiButtons.Length; i++)
        {
            int index = i;
            EventTrigger trigger = emojiButtons[i].gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) => { OnEmojiHoverEnter(index); });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => { OnEmojiHoverExit(index); });
            trigger.triggers.Add(exitEntry);

            EventTrigger.Entry clickEntry = new EventTrigger.Entry();
            clickEntry.eventID = EventTriggerType.PointerClick;
            clickEntry.callback.AddListener((data) => { OnEmojiClick(index); });
            trigger.triggers.Add(clickEntry);
        }
    }

    void OnEmojiHoverEnter(int index)
    {
        if (isExpanded && index < emojiAnimations.Length && emojiAnimations[index].animator != null)
        {
            emojiAnimations[index].animator.SetBool("IsHovering", true);
        }
    }

    void OnEmojiHoverExit(int index)
    {
        if (isExpanded && index < emojiAnimations.Length && emojiAnimations[index].animator != null)
        {
            emojiAnimations[index].animator.SetBool("IsHovering", false);
        }
    }

    void OnEmojiClick(int index)
    {
        if (isExpanded && index < emojiAnimations.Length)
        {
            photonView.RPC("DisplayEmojiRPC", RpcTarget.All, index, PhotonNetwork.LocalPlayer.ActorNumber);
            //ToggleEmojiPanel(); // Close the panel after selecting an emoji
        }
    }

    [PunRPC]
    void DisplayEmojiRPC(int emojiIndex, int actorNumber)
    {
        Debug.Log($"DisplayEmojiRPC called. Emoji: {emojiIndex}, Actor: {actorNumber}");

        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
        if (player == null)
        {
            Debug.LogError($"Player with ActorNumber {actorNumber} not found in the room.");
            return;
        }

        if (!player.CustomProperties.TryGetValue("Name", out object nameObj) || !(nameObj is string playerName))
        {
            Debug.LogError($"Custom property 'Name' not found or invalid for player {actorNumber}.");
            return;
        }

        GameObject playerObject = GameObject.Find(playerName);
        if (playerObject == null)
        {
            Debug.LogError($"Player object with name '{playerName}' not found in the scene.");
            return;
        }

        Canvas playerCanvas = playerObject.GetComponentInChildren<Canvas>();
        if (playerCanvas == null)
        {
            Debug.LogError($"Canvas component not found on player '{playerName}' or its children.");
            return;
        }

        StartCoroutine(DisplayEmojiCoroutine(playerCanvas, emojiIndex, actorNumber));
    }

    IEnumerator DisplayEmojiCoroutine(Canvas playerCanvas, int emojiIndex, int actorNumber)
    {
        // Remove any existing speech bubble for this player
        if (activeSpeechBubbles.TryGetValue(actorNumber, out GameObject existingBubble))
        {
            Destroy(existingBubble);
            activeSpeechBubbles.Remove(actorNumber);
        }

        GameObject speechBubble = Instantiate(speechBubblePrefab, playerCanvas.transform);
        activeSpeechBubbles[actorNumber] = speechBubble;

        speechBubble.transform.localPosition = speechBubbleOffset;

        Animator emojiAnimator = speechBubble.GetComponentInChildren<Animator>();
        if (emojiAnimator != null)
        {
            emojiAnimator.runtimeAnimatorController = emojiAnimations[emojiIndex].animator.runtimeAnimatorController;
            emojiAnimator.SetBool("IsHovering", true); // Start the animation
        }
        else
        {
            Debug.LogError("Emoji Animator not found in speech bubble!");
        }

        yield return new WaitForSeconds(emojiDisplayDuration);

        Destroy(speechBubble);
        activeSpeechBubbles.Remove(actorNumber);
    }

    void Update()
    {
        UpdateSpeechBubblePositions();
    }

    void UpdateSpeechBubblePositions()
    {
        foreach (var kvp in activeSpeechBubbles)
        {
            int actorNumber = kvp.Key;
            GameObject speechBubble = kvp.Value;

            Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            if (player == null || !player.CustomProperties.TryGetValue("Name", out object nameObj) || !(nameObj is string playerName))
            {
                continue;
            }

            GameObject playerObject = GameObject.Find(playerName);
            if (playerObject != null && speechBubble != null)
            {
                Vector3 worldPosition = playerObject.transform.position + speechBubbleOffset;
                speechBubble.transform.position = worldPosition;
            }
        }
    }

    void ToggleEmojiPanel()
    {
        isExpanded = !isExpanded;
        emojiPanel.gameObject.SetActive(true);
        StartCoroutine(AnimateEmojiPanel());
    }

    IEnumerator AnimateEmojiPanel()
    {
        float startWidth = isExpanded ? 0 : expandedWidth;
        float endWidth = isExpanded ? expandedWidth : 0;
        float elapsedTime = 0;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / animationDuration);
            t = t * t * (3f - 2f * t); // Smoothstep easing

            float currentWidth = Mathf.Lerp(startWidth, endWidth, t);
            emojiPanel.sizeDelta = new Vector2(currentWidth, emojiPanel.sizeDelta.y);

            UpdateEmojisVisibility(currentWidth);
            yield return null;
        }

        emojiPanel.sizeDelta = new Vector2(endWidth, emojiPanel.sizeDelta.y);

        if (!isExpanded)
        {
            emojiPanel.gameObject.SetActive(false);
            SetEmojisAlpha(0);
            ResetAllAnimators();
        }
        else
        {
            SetEmojisAlpha(1);
        }
    }

    void UpdateEmojisVisibility(float currentWidth)
    {
        for (int i = 0; i < emojiButtons.Length; i++)
        {
            float emojiPosition = i * (emojiButtons[i].rect.width + emojiSpacing);
            float alpha = Mathf.Clamp01((currentWidth - emojiPosition) / emojiButtons[i].rect.width);
            SetEmojiAlpha(emojiButtons[i], alpha);
        }
    }

    void SetEmojisAlpha(float alpha)
    {
        foreach (var emojiButton in emojiButtons)
        {
            SetEmojiAlpha(emojiButton, alpha);
        }
    }

    void SetEmojiAlpha(RectTransform emojiButton, float alpha)
    {
        Image image = emojiButton.GetComponent<Image>();
        if (image != null)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }

    void ResetAllAnimators()
    {
        foreach (var emojiAnim in emojiAnimations)
        {
            if (emojiAnim.animator != null)
            {
                emojiAnim.animator.SetBool("IsHovering", false);
            }
        }
    }
}