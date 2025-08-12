using UnityEngine;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class TooltipResizer : MonoBehaviour
{
    private TextMeshProUGUI tooltipText;
    private RectTransform rectTransform;

    [SerializeField] private float horizontalPadding = 20f; // Padding on left and right
    [SerializeField] private float verticalPadding = 10f;   // Padding on top and bottom
    [SerializeField] private float characterWidth = 11f;    // Average width per character at font size 20
    [SerializeField] private float minWidth = 50f;          // Minimum width of the tooltip

    private void Awake()
    {
        tooltipText = GetComponentInChildren<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();

        if (tooltipText == null)
        {
            Debug.LogError("TooltipResizer: No TextMeshProUGUI component found in children!");
            return;
        }

        ResizeTooltip();
    }

    public void ResizeTooltip()
    {
        if (tooltipText == null || string.IsNullOrEmpty(tooltipText.text)) return;

        // Calculate the required width based on text length
        float textWidth = tooltipText.text.Length * characterWidth;
        float width = Mathf.Max(minWidth, textWidth + horizontalPadding * 2);

        // Set the width while maintaining the current height
        rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);
    }

    // Call this method when you need to update the tooltip text and resize
    public void UpdateTooltipText(string newText)
    {
        if (tooltipText != null)
        {
            tooltipText.text = newText;
            ResizeTooltip();
        }
    }

    // Optional: Method to adjust the character width if needed
    public void SetCharacterWidth(float width)
    {
        characterWidth = width;
        ResizeTooltip();
    }
}