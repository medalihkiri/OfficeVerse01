using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class InputFieldHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image FrameImage;
    public float hoverDuration = 0.2f;

    private TMP_InputField inputField;
    private Color FrameOriginalColor;

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SetInputFieldHoverState(string gameObjectName, bool isHovering);

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        if (inputField == null)
        {
            Debug.LogError("InputFieldHoverEffect requires a TMP_InputField component on the same GameObject.");
        }

        if (FrameImage != null)
        {
            // Setup search frame hover effect
            FrameOriginalColor = FrameImage.color;
            EventTrigger trigger = FrameImage.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) => { OnFrameHover(true); });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => { OnFrameHover(false); });
            trigger.triggers.Add(exitEntry);
        }
    }

    private void OnFrameHover(bool isHovering)
    {
        StartCoroutine(SmoothColorChange(FrameImage, isHovering ? Color.white : FrameOriginalColor));
    }

    private IEnumerator SmoothColorChange(Image image, Color targetColor)
    {
        Color startColor = image.color;
        float elapsedTime = 0f;

        while (elapsedTime < hoverDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / hoverDuration;
            image.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        image.color = targetColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SetInputFieldHoverState(gameObject.name, true);
#endif
    }

    public void OnPointerExit(PointerEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SetInputFieldHoverState(gameObject.name, false);
#endif
    }
}
