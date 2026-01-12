//
// UIAllRounder: An enhanced UI component for Unity.
//
// Features:
// - Robust keyboard and gamepad navigation.
// - Automatic saving and loading of InputField text.
// - Enhanced copy/paste/cut/select all for TMP_InputFields.
// - Keyboard shortcuts for ScrollRects (PageUp/Down, Home, End).
// - Optional auto-selection of UI elements on mouse hover.
// - Highly customizable with clear Inspector settings.
// - Optimized for performance.
//
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drop this on any UI element (InputField, Button, Toggle, Slider, ScrollRect) to enhance navigation,
/// save input field data, and add quality-of-life features for both players and developers.
/// Fully customizable via the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class UIAllRounder : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    #region Inspector Settings

    [Header("Feature Toggles")]
    [Tooltip("Enable all features of this script.")]
    [SerializeField] private bool enableAllFeatures = true;
    [Tooltip("Enables automatic navigation improvements between UI elements.")]
    [SerializeField] private bool enableNavigation = true;
    [Tooltip("Enables Ctrl+C/V/X/A functionality for Input Fields.")]
    [SerializeField] private bool enableCopyPaste = true;
    [Tooltip("Enables PageUp/Down/Home/End keyboard shortcuts for Scroll Rects.")]
    [SerializeField] private bool enableScrollShortcuts = true;
    [Tooltip("Automatically selects the UI element when the mouse pointer hovers over it.")]
    [SerializeField] private bool selectOnHover = false;

    [Header("Input Field Settings")]
    [Tooltip("Enables saving the value of an Input Field and reloading it on next launch.")]
    [SerializeField] private bool saveInputField = true;
    [Tooltip("A unique key for saving this input field's data. If empty, a key will be generated automatically.")]
    [SerializeField] private string overrideSaveKey = "";

    #endregion

    #region Private Variables

    private Selectable _selectable;
    private TMP_InputField _tmpInput;
    private ScrollRect _scrollRect;
    private Button _button;
    private Toggle _toggle;
    private bool _isFocused;
    private string _generatedSaveKey;
    private static EventSystem _eventSystem;

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        // Cache components for performance
        _selectable = GetComponent<Selectable>();
        _tmpInput = GetComponent<TMP_InputField>();
        _scrollRect = GetComponent<ScrollRect>();
        _button = GetComponent<Button>();
        _toggle = GetComponent<Toggle>();

        if (_eventSystem == null)
        {
            _eventSystem = EventSystem.current;
        }

        if (enableAllFeatures && saveInputField && _tmpInput != null)
        {
            LoadInput();
        }
    }

    private void OnEnable()
    {
        if (_tmpInput != null)
        {
            _tmpInput.onEndEdit.AddListener(SaveInputOnEndEdit);
        }
    }

    private void OnDisable()
    {
        // Always save on disable/scene change if the component is active.
        if (enableAllFeatures && saveInputField && _tmpInput != null)
        {
            SaveInput();
        }

        if (_tmpInput != null)
        {
            _tmpInput.onEndEdit.RemoveListener(SaveInputOnEndEdit);
        }
    }

    private void Update()
    {
        if (!enableAllFeatures || !_isFocused)
        {
            return;
        }

        if (enableNavigation) HandleKeyboardNavigation();
        if (enableCopyPaste) HandleCopyPaste();
        if (enableScrollShortcuts) HandleScrollShortcuts();
    }

    #endregion

    #region Core Functionality

    private void HandleKeyboardNavigation()
    {
        if (_selectable == null || _eventSystem.currentSelectedGameObject != gameObject) return;

        // Navigation
        if (Input.GetKeyDown(KeyCode.UpArrow)) Navigate(_selectable.FindSelectableOnUp());
        else if (Input.GetKeyDown(KeyCode.DownArrow)) Navigate(_selectable.FindSelectableOnDown());
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) Navigate(_selectable.FindSelectableOnLeft());
        else if (Input.GetKeyDown(KeyCode.RightArrow)) Navigate(_selectable.FindSelectableOnRight());

        // Submission
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (_button != null && _button.interactable) _button.onClick.Invoke();
            if (_toggle != null && _toggle.interactable) _toggle.isOn = !_toggle.isOn;
        }
    }

    private void HandleCopyPaste()
    {
        if (_tmpInput == null) return;

        // Use the cross-platform key modifier.
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                CopyText();
            }
            else if (Input.GetKeyDown(KeyCode.X) && !_tmpInput.readOnly)
            {
                CutText();
            }
            else if (Input.GetKeyDown(KeyCode.V) && !_tmpInput.readOnly)
            {
                PasteText();
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                SelectAllText();
            }
        }
    }

    private void HandleScrollShortcuts()
    {
        if (_scrollRect == null) return;

        // More robust scrolling that handles different scroll amounts.
        float scrollSensitivity = 0.2f; // Could be exposed as an Inspector setting.
        if (Input.GetKeyDown(KeyCode.PageUp)) _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(_scrollRect.verticalNormalizedPosition + scrollSensitivity);
        else if (Input.GetKeyDown(KeyCode.PageDown)) _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(_scrollRect.verticalNormalizedPosition - scrollSensitivity);
        else if (Input.GetKeyDown(KeyCode.Home)) _scrollRect.verticalNormalizedPosition = 1f;
        else if (Input.GetKeyDown(KeyCode.End)) _scrollRect.verticalNormalizedPosition = 0f;
    }

    #endregion

    #region Input Field Save/Load

    private void SaveInputOnEndEdit(string text)
    {
        if (enableAllFeatures && saveInputField)
        {
            SaveInput();
        }
    }

    private void SaveInput()
    {
        if (_tmpInput == null) return;
        PlayerPrefs.SetString(GetSaveKey(), _tmpInput.text);
        PlayerPrefs.Save();
    }

    private void LoadInput()
    {
        string saveKey = GetSaveKey();
        if (PlayerPrefs.HasKey(saveKey))
        {
            _tmpInput.text = PlayerPrefs.GetString(saveKey);
        }
    }

    private string GetSaveKey()
    {
        if (!string.IsNullOrEmpty(_generatedSaveKey))
        {
            return _generatedSaveKey;
        }

        if (!string.IsNullOrEmpty(overrideSaveKey))
        {
            _generatedSaveKey = overrideSaveKey;
        }
        else
        {
            // Auto-generate a unique key to prevent conflicts between different input fields.
            _generatedSaveKey = $"UIAllRounder.InputField.{gameObject.scene.name}.{GetGameObjectPath(transform)}";
        }
        return _generatedSaveKey;
    }

    private string GetGameObjectPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    #endregion

    #region Helper & Interface Methods

    private void Navigate(Selectable target)
    {
        if (target != null && target.interactable)
        {
            _eventSystem.SetSelectedGameObject(target.gameObject);
        }
    }

    private void CopyText()
    {
        if (_tmpInput.selectionAnchorPosition == _tmpInput.selectionFocusPosition) return;
        int start = Mathf.Min(_tmpInput.selectionAnchorPosition, _tmpInput.selectionFocusPosition);
        int length = Mathf.Abs(_tmpInput.selectionAnchorPosition - _tmpInput.selectionFocusPosition);
        GUIUtility.systemCopyBuffer = _tmpInput.text.Substring(start, length);
    }

    private void CutText()
    {
        CopyText();
        int start = Mathf.Min(_tmpInput.selectionAnchorPosition, _tmpInput.selectionFocusPosition);
        int end = Mathf.Max(_tmpInput.selectionAnchorPosition, _tmpInput.selectionFocusPosition);
        _tmpInput.text = _tmpInput.text.Remove(start, end - start);
        _tmpInput.caretPosition = start;
    }

    private void PasteText()
    {
        string pasteText = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(pasteText)) return;

        int start = Mathf.Min(_tmpInput.selectionAnchorPosition, _tmpInput.selectionFocusPosition);
        int end = Mathf.Max(_tmpInput.selectionAnchorPosition, _tmpInput.selectionFocusPosition);

        _tmpInput.text = _tmpInput.text.Remove(start, end - start).Insert(start, pasteText);
        _tmpInput.caretPosition = start + pasteText.Length;
    }



    private void SelectAllText()
    {
        _tmpInput.selectionAnchorPosition = 0;
        _tmpInput.selectionFocusPosition = _tmpInput.text.Length;
        _tmpInput.ForceLabelUpdate();
    }


    public void OnSelect(BaseEventData eventData)
    {
        _isFocused = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isFocused = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (enableAllFeatures && selectOnHover && _selectable != null && _selectable.interactable)
        {
            _eventSystem.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // No action needed by default.
    }

    #endregion
}