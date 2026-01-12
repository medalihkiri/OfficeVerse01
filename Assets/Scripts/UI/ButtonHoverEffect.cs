using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Settings")]
    public bool cursorHoverEffect = true;
    public Image targetImage;
    public float hoverDuration = 0.2f;

    [Header("Tooltip Content")]
    [Tooltip("Text to display when hovering over the button")]
    public string tooltipText = "";
    [SerializeField] private GameObject tooltipPrefab;

    [Header("Tooltip Positioning")]
    public TooltipPosition position = TooltipPosition.Top;

    [Tooltip("Distance from the button edge")]
    public float spacing = 10f;

    [Tooltip("Fine-tune the position (X, Y)")]
    public Vector2 additionalOffset = Vector2.zero;

    // Advanced settings only shown/used if 'Custom' is selected
    [Header("Custom Settings (Only if Position is Custom)")]
    public Vector2 customAnchor = new Vector2(0.5f, 1f);
    public Vector2 customPivot = new Vector2(0.5f, 0f);

    public enum TooltipPosition
    {
        Top,
        Bottom,
        Left,
        Right,
        Custom
    }

    private float currentAlpha = 0f;
    [HideInInspector] public bool isHovering = false;
    [HideInInspector] public GameObject tooltipInstance;
    private RectTransform tooltipRect;
    private TooltipResizer tooltipResizer;

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SetButtonHoverState(string gameObjectName, bool isHovering);

    private void Awake()
    {
        if (targetImage != null)
        {
            // Set initial alpha to 0
            Color initialColor = targetImage.color;
            initialColor.a = 0f;
            targetImage.color = initialColor;
        }

        // Create tooltip if text is provided
        if (!string.IsNullOrEmpty(tooltipText) && tooltipPrefab != null)
        {
            CreateTooltip();
        }
    }

    public void CreateTooltip()
    {
        // Instantiate tooltip prefab
        tooltipInstance = Instantiate(tooltipPrefab, transform);
        tooltipRect = tooltipInstance.GetComponent<RectTransform>();
        tooltipResizer = tooltipInstance.GetComponent<TooltipResizer>();

        if (tooltipResizer != null)
        {
            tooltipResizer.UpdateTooltipText(tooltipText);
        }

        PositionTooltip();
        tooltipInstance.SetActive(false);
    }

    private void PositionTooltip()
    {
        if (tooltipRect == null) return;

        Vector2 finalAnchorMin = Vector2.zero;
        Vector2 finalAnchorMax = Vector2.zero;
        Vector2 finalPivot = Vector2.zero;
        Vector2 basePosition = Vector2.zero;

        switch (position)
        {
            case TooltipPosition.Top:
                // Anchor to Top-Center of button
                finalAnchorMin = new Vector2(0.5f, 1);
                finalAnchorMax = new Vector2(0.5f, 1);
                // Pivot at Bottom-Center of tooltip
                finalPivot = new Vector2(0.5f, 0);
                basePosition = new Vector2(0, spacing);
                break;

            case TooltipPosition.Bottom:
                // Anchor to Bottom-Center of button
                finalAnchorMin = new Vector2(0.5f, 0);
                finalAnchorMax = new Vector2(0.5f, 0);
                // Pivot at Top-Center of tooltip
                finalPivot = new Vector2(0.5f, 1);
                basePosition = new Vector2(0, -spacing);
                break;

            case TooltipPosition.Left:
                // Anchor to Left-Center of button
                finalAnchorMin = new Vector2(0, 0.5f);
                finalAnchorMax = new Vector2(0, 0.5f);
                // Pivot at Right-Center of tooltip
                finalPivot = new Vector2(1, 0.5f);
                basePosition = new Vector2(-spacing, 0);
                break;

            case TooltipPosition.Right:
                // Anchor to Right-Center of button
                finalAnchorMin = new Vector2(1, 0.5f);
                finalAnchorMax = new Vector2(1, 0.5f);
                // Pivot at Left-Center of tooltip
                finalPivot = new Vector2(0, 0.5f);
                basePosition = new Vector2(spacing, 0);
                break;

            case TooltipPosition.Custom:
                finalAnchorMin = customAnchor;
                finalAnchorMax = customAnchor;
                finalPivot = customPivot;
                basePosition = Vector2.zero;
                break;
        }

        // Apply calculated values
        tooltipRect.anchorMin = finalAnchorMin;
        tooltipRect.anchorMax = finalAnchorMax;
        tooltipRect.pivot = finalPivot;

        // Apply Spacing + Custom Offsets
        tooltipRect.anchoredPosition = basePosition + additionalOffset;
    }

    private void Update()
    {
        if (targetImage == null)
        {
            return;
        }
        if (isHovering && currentAlpha < 1f)
        {
            currentAlpha += Time.deltaTime / hoverDuration;
        }
        else if (!isHovering && currentAlpha > 0f)
        {
            currentAlpha -= Time.deltaTime / hoverDuration;
        }
        currentAlpha = Mathf.Clamp01(currentAlpha);
        Color newColor = targetImage.color;
        newColor.a = currentAlpha;
        targetImage.color = newColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!cursorHoverEffect) return;
        isHovering = true;
        if (tooltipInstance != null)
        {
            tooltipInstance.SetActive(true);
            // Optional: Reposition on show in case layout changed
            // PositionTooltip(); 
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        SetButtonHoverState(gameObject.name, true);
#endif
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!cursorHoverEffect) return;
        isHovering = false;
        if (tooltipInstance != null)
        {
            tooltipInstance.SetActive(false);
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        SetButtonHoverState(gameObject.name, false);
#endif
    }

    private void OnDisable()
    {
        if (tooltipInstance != null)
        {
            tooltipInstance.SetActive(false);
        }
    }

    private void OnEnable()
    {
        isHovering = false;
    }
}