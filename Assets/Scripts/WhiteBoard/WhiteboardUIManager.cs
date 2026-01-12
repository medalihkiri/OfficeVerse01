using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI glue for the whiteboard:
/// - hooks sliders / dropdowns to controller
/// - registers color buttons (reads Image.color)
/// - assigns eraser cursor RectTransform to controller (so the controller can show/hide and size it)
/// - shape tool buttons: rectangle, ellipse, line
/// - optional smoothing controls
/// </summary>
public class WhiteboardUIManager : MonoBehaviour
{
    public WhiteboardController controller;
    public TMP_Dropdown toolDropdown;
    public Slider sizeSlider;
    public Button undoButton, redoButton, clearButton, exportButton;
    public Image colorPreview;

    [Header("Color swatches")]
    public Button[] colorButtons; // assign buttons whose Image color will be used as swatch

    [Header("Eraser cursor")]
    public RectTransform eraserCursorUI; // small UI Image used as eraser cursor. Assign in inspector.

    [Header("Smoothing UI (optional)")]
    public Toggle smoothingToggle;
    public Slider smoothingQualitySlider; // integer slider mapped to controller.smoothingSubdivisions

    [Header("Shape buttons")]
    public Button rectangleButton;
    public Button ellipseButton;
    public Button shapeLineButton;

    public string exportFileName = "whiteboard_export.png";

    void Start()
    {
        if (controller == null)
        {
            Debug.LogError("WhiteboardUIManager requires a controller reference.");
            return;
        }

        // brush size
        if (sizeSlider != null)
        {
            sizeSlider.onValueChanged.AddListener((v) => controller.SetBrushSize(v));
            sizeSlider.value = controller.brushSize;
        }

        // tool dropdown
        if (toolDropdown != null)
        {
            // ensure dropdown options match Tool enum ordering if using dropdown
            toolDropdown.onValueChanged.AddListener((i) => controller.SetTool((Tool)i));
            toolDropdown.value = (int)controller.currentTool;
        }

        // undo/redo/clear/export
        if (undoButton != null) undoButton.onClick.AddListener(controller.Undo);
        if (redoButton != null) redoButton.onClick.AddListener(controller.Redo);
        if (clearButton != null) clearButton.onClick.AddListener(controller.ClearBoard);
        if (exportButton != null) exportButton.onClick.AddListener(() => StartCoroutine(Export()));

        // color swatches
        if (colorButtons != null && colorButtons.Length > 0)
        {
            foreach (Button b in colorButtons)
            {
                if (b == null) continue;
                Image img = b.GetComponent<Image>();
                if (img == null) continue;
                Color col = img.color;
                Color captured = col;
                b.onClick.AddListener(() => PickColor(captured));
            }
        }

        if (colorPreview != null) colorPreview.color = controller.currentColor;

        // eraser cursor hookup
        if (eraserCursorUI != null)
        {
            controller.SetEraserCursor(eraserCursorUI);
        }

        // smoothing UI
        if (smoothingToggle != null)
        {
            smoothingToggle.isOn = controller.smoothingEnabled;
            smoothingToggle.onValueChanged.AddListener((v) => controller.SetSmoothingEnabled(v));
        }
        if (smoothingQualitySlider != null)
        {
            smoothingQualitySlider.wholeNumbers = true;
            smoothingQualitySlider.minValue = 0;
            smoothingQualitySlider.maxValue = 6;
            smoothingQualitySlider.value = controller.smoothingSubdivisions;
            smoothingQualitySlider.onValueChanged.AddListener((v) => controller.SetSmoothingSubdivisions((int)v));
        }

        // shape buttons
        if (rectangleButton != null) rectangleButton.onClick.AddListener(() => OnShapeButtonClicked(Tool.Rectangle));
        if (ellipseButton != null) ellipseButton.onClick.AddListener(() => OnShapeButtonClicked(Tool.Ellipse));
        if (shapeLineButton != null) shapeLineButton.onClick.AddListener(() => OnShapeButtonClicked(Tool.ShapeLine));
    }

    void OnShapeButtonClicked(Tool shapeTool)
    {
        controller.SetTool(shapeTool);
        // provide quick visual feedback by updating dropdown/preview if present
       /* if (toolDropdown != null)
        {
            toolDropdown.value = (int)shapeTool;
        }*/
    }

    public void PickColor(Color c)
    {
        controller.SetColor(c);
        if (colorPreview != null) colorPreview.color = c;
    }

    IEnumerator Export()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, exportFileName);
        yield return StartCoroutine(controller.ExportToPNG(path));
        Debug.Log("Exported to " + path);
    }
}
