using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class TMPClipboardHandler : MonoBehaviour
{
    public bool privateMessage;
    private TMP_InputField inputField;
    private Queue<string> undoStack = new Queue<string>();
    private const int MaxUndoSteps = 20;
    [HideInInspector]
    public bool isShiftEnter;

    [DllImport("__Internal")]
    private static extern void EnableUndoSupport(string elementID);

    [DllImport("__Internal")]
    private static extern void SelectAllJS(string elementID);

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();

    }


    private void Update()
    {
        if (inputField == null || !inputField.isFocused) return;


        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            isShiftEnter = true;
            //AddNewLine();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!isShiftEnter)
            {
                if (privateMessage)
                {
                    PrivateChat.Instance.SendMessage();
                }
            }
            isShiftEnter = false;
        }
    }


    void OnGUI()
    {
        Event e = Event.current;
        if (e.control && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.C) { Debug.LogError("ctrlc"); CopySelectedText(); }
            else if (e.keyCode == KeyCode.V) { Debug.LogError("ctrlv"); PasteFromClipboard(); }
            else if (e.keyCode == KeyCode.A) { Debug.LogError("ctrla"); SelectAllText(); }
            else if (e.keyCode == KeyCode.Z) { Debug.LogError("ctrlz"); UndoLastChange(); }

            e.Use();
        }
    }


    private void AddNewLine()
    {
        StoreUndoState(inputField.text);
        inputField.text += "\n";
        inputField.caretPosition = inputField.text.Length;
    }

    public void SelectAllText()
    {
        if (inputField != null)
        {
            inputField.Select();
            inputField.caretPosition = inputField.text.Length;
        }
    }

    public void CopySelectedText()
    {
        TMPWebGLInputFieldExtension extension = GetComponent<TMPWebGLInputFieldExtension>();
        if (extension != null)
        {
            extension.CopySelectedText();
        }
    }

    private void PasteFromClipboard()
    {
        TMPWebGLInputFieldExtension extension = GetComponent<TMPWebGLInputFieldExtension>();
        //extension.PasteText(GUIUtility.systemCopyBuffer);
    }

    private void UndoLastChange()
    {
        if (undoStack.Count > 1)
        {
            undoStack.Dequeue();
            inputField.text = undoStack.Peek();
            inputField.caretPosition = inputField.text.Length;
        }
    }

    public void StoreUndoState(string currentText)
    {
        if (undoStack.Count >= MaxUndoSteps)
        {
            undoStack.Dequeue();
        }
        undoStack.Enqueue(currentText);
    }
}
