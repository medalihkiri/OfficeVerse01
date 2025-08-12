using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TMPWebGLInputFieldExtension : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TMP_InputField inputField;
    private bool isMessageField = false;
    private Stack<string> undoStack = new Stack<string>();  // Changed from Queue to Stack
    private const int MaxUndoSteps = 20;

    [DllImport("__Internal")]
    private static extern void ShowContextMenu(string gameObjectName, float x, float y, bool isMessageField);

    [DllImport("__Internal")]
    private static extern void CopyToClipboard(string text);

    [DllImport("__Internal")]
    private static extern void RequestPasteFromClipboard(string gameObjectName);

    [DllImport("__Internal")]
    private static extern void DisableDefaultCopyPaste();

    [DllImport("__Internal")]
    private static extern void SetInputFieldHoverState(string gameObjectName, bool isHovering);

    [DllImport("__Internal")]
    private static extern void DetectKeyEvents(string gameObjectName);

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        if (inputField == null)
        {
            Debug.LogError("TMPWebGLInputFieldExtension requires a TMP_InputField component on the same GameObject.");
            return;
        }

        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(StoreUndoState);
            StoreUndoState(inputField.text);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        DetectKeyEvents(gameObject.name);
        gameObject.AddComponent<RightClickHandler>().OnRightClick += ShowCustomContextMenu;
#endif
    }
    bool wait = false;
    public void OnCtrlKey(string key)
    {
        if (inputField == null || !inputField.isFocused) return;
        if(wait)
            return;

        switch (key)
        {
            case "C": CopySelectedText(); break;
            case "V": RequestPaste(); break;
            case "A": SelectAllText(); break;
            case "Z": UndoLastChange(); break;
        }
    }

    private void SelectAllText()
    {
        inputField.selectionStringAnchorPosition = 0;
        inputField.selectionStringFocusPosition = inputField.text.Length;
        inputField.ForceLabelUpdate();
    }

    public void UndoLastChange()
    {
        if (undoStack.Count > 1)
        {
            undoStack.Pop();
            string previousText = undoStack.Peek();
            inputField.text = previousText;
            inputField.caretPosition = previousText.Length;
        }
        else
        {
            Debug.Log("Undo stack is empty.");
        }
    }

    public void StoreUndoState(string currentText)
    {
        if (undoStack.Count == 0 || undoStack.Peek() != currentText)
        {
            if (undoStack.Count >= MaxUndoSteps)
            {
                Stack<string> tempStack = new Stack<string>(undoStack.Reverse());
                tempStack.Pop();
                undoStack = new Stack<string>(tempStack.Reverse());
            }

            undoStack.Push(currentText);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!inputField.readOnly)
        {
            SetInputFieldHoverState(gameObject.name, true);
        }
#endif
    }

    public void OnPointerExit(PointerEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SetInputFieldHoverState(gameObject.name, false);
#endif
    }

    public void SetAsMessageField()
    {
        isMessageField = true;
    }

    private void ShowCustomContextMenu(Vector2 position)
    {
        ShowContextMenu(gameObject.name, position.x, position.y, isMessageField);
    }

    public void CopySelectedText()
    {
        if (inputField.selectionStringAnchorPosition != inputField.selectionStringFocusPosition)
        {
            int startIndex = Mathf.Min(inputField.selectionStringAnchorPosition, inputField.selectionStringFocusPosition);
            int endIndex = Mathf.Max(inputField.selectionStringAnchorPosition, inputField.selectionStringFocusPosition);
            string selectedText = inputField.text.Substring(startIndex, endIndex - startIndex);
            CopyToClipboard(selectedText);
        }
    }

    public void RequestPaste()
    {
        Debug.Log("111111111111111111111");
        wait = true;
        RequestPasteFromClipboard(gameObject.name);
        StartCoroutine(SetWait());
    }

    IEnumerator SetWait()
    {
        yield return new WaitForSeconds(0.7f);
        wait = false;
    }

    public void PasteText(string clipboardText)
    {
        Debug.Log("111111111111111111111");
        if (string.IsNullOrEmpty(clipboardText)) return;

        clipboardText = clipboardText.Replace("\n", "").Replace("\r", "");

        int caretPosition = inputField.caretPosition;
        string currentText = inputField.text;
        string newText = currentText.Insert(caretPosition, clipboardText);
        inputField.text = newText;
        StartCoroutine(SetCaretPosition(caretPosition + clipboardText.Length));
        inputField.ForceLabelUpdate();
        Debug.Log("111111111111111111111");
    }

    private IEnumerator SetCaretPosition(int caretIndex)
    {
        int width = inputField.caretWidth;
        inputField.caretWidth = 0;
        yield return new WaitForEndOfFrame();
        inputField.caretWidth = width;
        inputField.caretPosition = caretIndex;
    }
}

public class RightClickHandler : MonoBehaviour
{
    public event System.Action<Vector2> OnRightClick;
    private TMP_InputField inputField;

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1)) // Right mouse button
        {
            Vector2 mousePosition = Input.mousePosition;
            if (IsMouseOverInputField(mousePosition))
            {
                OnRightClick?.Invoke(mousePosition);
            }
        }
    }

    private bool IsMouseOverInputField(Vector2 mousePosition)
    {
        RectTransform rectTransform = inputField.GetComponent<RectTransform>();
        Vector2 localMousePosition = rectTransform.InverseTransformPoint(mousePosition);
        return rectTransform.rect.Contains(localMousePosition);
    }
}