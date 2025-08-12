using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class AutoResizePanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private float padding = 20f; // Additional space around the input field

    private RectTransform panelRectTransform;
    private RectTransform inputFieldRectTransform;

    private void Awake()
    {
        panelRectTransform = GetComponent<RectTransform>();
        inputFieldRectTransform = inputField.GetComponent<RectTransform>();

        // Subscribe to the input field's text changed event
        inputField.onValueChanged.AddListener(OnInputValueChanged);
    }

    private void OnInputValueChanged(string text)
    {
        // Get the preferred height of the input field based on its content
        float preferredHeight = LayoutUtility.GetPreferredHeight(inputFieldRectTransform);

        // Update the panel's height
        Vector2 size = panelRectTransform.sizeDelta;
        size.y = preferredHeight + padding * 2;
        panelRectTransform.sizeDelta = size;
    }

    private void OnDestroy()
    {
        // Unsubscribe from the event when the component is destroyed
        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(OnInputValueChanged);
        }
    }
}