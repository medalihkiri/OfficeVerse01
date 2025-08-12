// EmojiButtonController.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using Photon.Pun;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public class EmojiChatButtonController : MonoBehaviourPunCallbacks
{
    private RectTransform mainButton;

    [HideInInspector] public GameObject emojiSelectionWindow;
    [HideInInspector] public bool isExpanded = false;
    private const float animationDuration = 0.2f;
    public TMP_InputField inputField;
    public GameObject emojiSelectionWindowPrefab;

    public delegate void EmojiWindowStateChanged(bool isOpen);
    public static event EmojiWindowStateChanged OnEmojiWindowStateChanged;

    private Coroutine animationCoroutine; // Coroutine variable to manage animation

    void Start()
    {
        mainButton = GetComponent<RectTransform>();
        mainButton.GetComponent<Button>().onClick.AddListener(ToggleEmojiPanel);
    }

    void ToggleEmojiPanel()
    {
        isExpanded = !isExpanded;

        if (isExpanded)
        {
            if (emojiSelectionWindow == null)
            {
                emojiSelectionWindow = Instantiate(emojiSelectionWindowPrefab, mainCanvas.transform);
                emojiSelectionWindow.transform.Find("Emoji List/Viewport/Content").GetComponent<EmojiButtonClickHandler>().inputField = inputField;
                PositionEmojiWindow();

            }
            else
            {
                emojiSelectionWindow.SetActive(true); // Activate before animation
            }

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }
            animationCoroutine = StartCoroutine(AnimateEmojiPanel(true));
        }
        else
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }
            animationCoroutine = StartCoroutine(AnimateEmojiPanel(false));


        }
        // Raise the event
        OnEmojiWindowStateChanged?.Invoke(isExpanded);
    }

    private Canvas mainCanvas
    {
        get
        {
            if (_mainCanvas == null)
            {
                _mainCanvas = GameObject.Find("ControlsCanvas").GetComponent<Canvas>();
                if (_mainCanvas == null)
                {
                    Debug.LogError("No Canvas found in the scene!");
                }
            }
            return _mainCanvas;
        }
    }
    private Canvas _mainCanvas;

    private void PositionEmojiWindow()
    {
        RectTransform windowRect = emojiSelectionWindow.GetComponent<RectTransform>();
        RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();

        // Set anchor and pivot
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);

        // Get the world position of the main button
        Vector3 buttonWorldPos = mainButton.transform.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, buttonWorldPos, null, out Vector2 localButtonPoint);

        // Calculate the available space above and below the button
        float spaceAbove = canvasRect.rect.height / 2 - localButtonPoint.y - mainButton.rect.height / 2;
        float spaceBelow = canvasRect.rect.height / 2 + localButtonPoint.y - mainButton.rect.height / 2;

        // Determine whether to place the window above or below the button
        bool placeAbove = spaceAbove >= windowRect.rect.height || (spaceAbove > spaceBelow && spaceBelow < windowRect.rect.height);

        // Calculate Y position
        float yOffset = 0; // Adjust this value as needed
        float yPosition;
        if (placeAbove)
        {
            yPosition = localButtonPoint.y + mainButton.rect.height / 2 + windowRect.rect.height / 2 + yOffset + 50;
        }
        else
        {
            yPosition = localButtonPoint.y - mainButton.rect.height / 2 - windowRect.rect.height / 2 - yOffset;
        }

        // Adjust X position to fit within screen width
        float xPosition = localButtonPoint.x;
        float halfWindowWidth = windowRect.rect.width / 2;
        float leftEdge = -canvasRect.rect.width / 2;
        float rightEdge = canvasRect.rect.width / 2;

        if (xPosition - halfWindowWidth < leftEdge)
        {
            xPosition = leftEdge + halfWindowWidth;
        }
        else if (xPosition + halfWindowWidth > rightEdge)
        {
            xPosition = rightEdge - halfWindowWidth;
        }

        // Adjust Y position if it goes beyond screen edges
        float topEdge = canvasRect.rect.height / 2;
        float bottomEdge = -canvasRect.rect.height / 2;

        if (yPosition + windowRect.rect.height / 2 > topEdge)
        {
            yPosition = topEdge - windowRect.rect.height / 2;
        }
        else if (yPosition - windowRect.rect.height / 2 < bottomEdge)
        {
            yPosition = bottomEdge + windowRect.rect.height / 2;
        }

        // Set the final position
        windowRect.anchoredPosition = new Vector2(xPosition, yPosition);
    }

    IEnumerator AnimateEmojiPanel(bool open)
    {

        CanvasGroup canvasGroup = emojiSelectionWindow.GetComponent<CanvasGroup>();
        float startAlpha = open ? 0 : 1;
        float endAlpha = open ? 1 : 0;
        float elapsedTime = 0;


        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / animationDuration);
            t = t * t * (3f - 2f * t); // Smoothstep easing

            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

            yield return null;
        }

        canvasGroup.alpha = endAlpha;

        if (!open)
        {
            emojiSelectionWindow.SetActive(false); // Deactivate after animation completes
        }

        animationCoroutine = null; // Reset coroutine variable
    }

    private void Update()
    {
        if (emojiSelectionWindow != null && emojiSelectionWindow.activeSelf && Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(emojiSelectionWindow.GetComponent<RectTransform>(), Input.mousePosition, null))
            {
                ToggleEmojiPanel();
            }
        }
    }
}