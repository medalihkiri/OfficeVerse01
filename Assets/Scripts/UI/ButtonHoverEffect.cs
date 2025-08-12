using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool cursorHoverEffect = true;
    public Image targetImage;
    public float hoverDuration = 0.2f;

    [Tooltip("Text to display when hovering over the button")]
    public string tooltipText = "";

    [Tooltip("If true, tooltip appears above the button; if false, below")]
    public bool tooltipAbove = false;

    [Tooltip("Horizontal offset for the tooltip")]
    public float tooltipHorizontalOffset = 0f;


    [SerializeField] private GameObject tooltipPrefab;  // Assign your tooltip prefab here

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

        // Position horizontally at center with offset
        tooltipRect.anchorMin = new Vector2(0.5f, tooltipAbove ? 1 : 0);
        tooltipRect.anchorMax = new Vector2(0.5f, tooltipAbove ? 1 : 0);
        tooltipRect.pivot = new Vector2(0.5f, tooltipAbove ? 0 : 1);


        // Position vertically based on tooltipAbove setting with horizontal offset
        float spacing = 10f; // Space between button and tooltip
        float verticalPosition = tooltipAbove ? spacing : -spacing;

        tooltipRect.anchoredPosition = new Vector2(tooltipHorizontalOffset, verticalPosition);
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