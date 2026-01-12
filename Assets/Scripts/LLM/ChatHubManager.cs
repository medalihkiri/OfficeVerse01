using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices; // Required for WebGL Bridge
using Newtonsoft.Json;

public class ChatHubManager : MonoBehaviour
{
    #region UI References

    [Header("1. Sidebar UI")]
    public Transform conversationListParent;
    public GameObject conversationButtonPrefab;
    public Button btnCreateNew;
    public Button btnOpenSettings;

    [Header("2. Create Popup UI")]
    public GameObject createPanel;
    public TMP_InputField inputTitle;
    public TMP_InputField inputContext;
    public Button btnUploadFile;
    public TextMeshProUGUI txtFileName;
    public Button btnConfirmCreate;
    public Button btnCancelCreate;
    public GameObject createLoadingSpinner;

    [Header("3. Chat View UI")]
    public GameObject chatPanel;
    public Transform messageContentParent;
    public GameObject userMsgPrefab;
    public GameObject botMsgPrefab;
    public TMP_InputField inputChat;
    public Button btnSend;
    // btnBack removed or handled externally if needed
    public Button btnDelete;
    public TextMeshProUGUI txtCurrentChatTitle;

    [Header("4. Settings UI")]
    public GameObject settingsPanel;
    public Button btnCloseSettings;
    public Button btnGetFreeKey;
    public TMP_InputField inputCustomKey;
    public Toggle toggleCustomKey;

    #endregion

    #region Internal State

    private string currentConvoId = null;
    private int totalConversations = 0;

    // File Upload State
    private bool hasFile = false; // We only track IF we have a file, JS holds the data

    #endregion

    #region WebGL Bridge (Direct Upload Pattern)

    [DllImport("__Internal")]
    private static extern void BrowserFileSelect(string gameObjectName, string methodName);

    [DllImport("__Internal")]
    private static extern void BrowserFileSubmit(string url, string token, string title, string context, string gameObjectName, string callbackMethod);

    #endregion

    void Start()
    {
        // --- Sidebar Listeners ---
        btnCreateNew.onClick.AddListener(OpenCreatePanel);
        btnOpenSettings.onClick.AddListener(() => settingsPanel.SetActive(true));

        // --- Create Popup Listeners ---
        btnUploadFile.onClick.AddListener(OpenFileBrowser);
        btnConfirmCreate.onClick.AddListener(CreateConversation);
        btnCancelCreate.onClick.AddListener(() => createPanel.SetActive(false));

        // --- Chat View Listeners ---
        btnSend.onClick.AddListener(SendMessage);
        btnDelete.onClick.AddListener(DeleteCurrentConversation);

        // --- Settings Listeners ---
        btnCloseSettings.onClick.AddListener(() => settingsPanel.SetActive(false));
        btnGetFreeKey.onClick.AddListener(() => Application.OpenURL("https://console.groq.com/keys"));

        // Save/Load Settings
        inputCustomKey.text = PlayerPrefs.GetString("UserGroqKey", "");
        toggleCustomKey.isOn = PlayerPrefs.GetInt("UseCustomKey", 0) == 1;

        toggleCustomKey.onValueChanged.AddListener((val) => PlayerPrefs.SetInt("UseCustomKey", val ? 1 : 0));
        inputCustomKey.onEndEdit.AddListener((val) => PlayerPrefs.SetString("UserGroqKey", val));

        // --- Initialization ---
        createPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (createLoadingSpinner) createLoadingSpinner.SetActive(false);

        // Load initial data
        LoadConversationList(false);

        // Reset state
        currentConvoId = null;
        if (txtCurrentChatTitle) txtCurrentChatTitle.text = "New Conversation";
        foreach (Transform child in messageContentParent) Destroy(child.gameObject);
    }

    // =================================================================================
    // 1. FILE UPLOAD LOGIC (WebGL Bridge)
    // =================================================================================

    public void OpenFileBrowser()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 1. Ask JS to open picker
            BrowserFileSelect(gameObject.name, "OnFileMetadataSelected");
#else
        Debug.Log("Editor: Simulating File Selection");
        OnFileMetadataSelected("Editor_Mock_File.pdf|application/pdf|1024");
#endif
    }

    // ---------------------------------------------------------
    // Called from JavaScript
    // ---------------------------------------------------------
    public void OnFileMetadataSelected(string payload)
    {
        // New Payload Format: "Count|FileNames|TotalSize"
        // Example: "3|doc.pdf, image.png, notes.txt|1500000"

        string[] parts = payload.Split('|');
        if (parts.Length < 3) return;

        string countStr = parts[0];
        string namesStr = parts[1];
        string sizeStr = parts[2];

        // Format size to MB
        if (long.TryParse(sizeStr, out long totalBytes))
        {
            string readableSize = (totalBytes / 1024f / 1024f).ToString("0.00") + " MB";

            // Update UI text
            txtFileName.text = $"{countStr} Files: {namesStr} ({readableSize})";

            // Set flag so we know files are ready to send via JS
            hasFile = true;
        }
        else
        {
            Debug.LogError("Failed to parse file size.");
        }
    }

    // =================================================================================
    // 2. CONVERSATION MANAGEMENT
    // =================================================================================

    void LoadConversationList(bool hideChatPanel = true)
    {
        if (hideChatPanel) chatPanel.SetActive(false);
        createPanel.SetActive(false);

        foreach (Transform child in conversationListParent) Destroy(child.gameObject);

        StartCoroutine(APIManager.Instance.Get("/api/conversations", (req) => {
            if (req != null && req.result == UnityWebRequest.Result.Success)
            {
                var list = JsonConvert.DeserializeObject<List<ConversationSummary>>(req.downloadHandler.text);
                totalConversations = list.Count;

                foreach (var convo in list)
                {
                    GameObject btnObj = Instantiate(conversationButtonPrefab, conversationListParent);
                    btnObj.GetComponentInChildren<TMP_Text>().text = convo.title;
                    btnObj.GetComponent<Button>().onClick.AddListener(() => LoadChat(convo._id, convo.title));
                }
            }
        }, true));
    }

    void OpenCreatePanel()
    {
        inputTitle.text = "";
        inputContext.text = "";
        txtFileName.text = "No file selected";
        hasFile = false;
        createPanel.SetActive(true);
    }

    void CreateConversation()
    {
        if (createLoadingSpinner) createLoadingSpinner.SetActive(true);
        btnConfirmCreate.interactable = false;

        string title = string.IsNullOrEmpty(inputTitle.text) ? "New Chat" : inputTitle.text;
        string context = inputContext.text;

#if UNITY_WEBGL && !UNITY_EDITOR
            // WEBGL: Delegate upload to JavaScript (fetch)
            string url = APIManager.Instance.apiBaseUrl + "/api/conversations";
            string token = APIManager.Instance.authToken;
            BrowserFileSubmit(url, token, title, context, gameObject.name, "OnCreateComplete");
#else
        // EDITOR: Fallback (No file support in Editor)
        Debug.Log("Editor: Creating chat (No File Upload support in Editor)");
        StartCoroutine(EditorCreateRoutine(title, context));
#endif
    }

    // Called from JavaScript after fetch() completes
    public void OnCreateComplete(string payload)
    {
        // Payload: "SUCCESS|{json}" or "ERROR|message"
        if (createLoadingSpinner) createLoadingSpinner.SetActive(false);
        btnConfirmCreate.interactable = true;

        if (payload.StartsWith("SUCCESS|"))
        {
            string json = payload.Substring(8);
            try
            {
                var newConvo = JsonConvert.DeserializeObject<ConversationSummary>(json);

                // FEATURE: Auto-Start
                createPanel.SetActive(false);
                LoadConversationList(false); // Refresh list silently
                LoadChat(newConvo._id, newConvo.title); // Enter chat immediately
            }
            catch
            {
                Debug.LogError("JSON Parse Error on Create: " + json);
            }
        }
        else
        {
            string error = payload.Substring(6);
            Debug.LogError("Creation Failed: " + error);
            txtFileName.text = "Error: " + error;
        }
    }

    // Editor Fallback
    IEnumerator EditorCreateRoutine(string title, string context)
    {
        WWWForm form = new WWWForm();
        form.AddField("title", title);
        form.AddField("contextText", context);

        using (UnityWebRequest req = UnityWebRequest.Post(APIManager.Instance.apiBaseUrl + "/api/conversations", form))
        {
            req.SetRequestHeader("Authorization", "Bearer " + APIManager.Instance.authToken);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                OnCreateComplete("SUCCESS|" + req.downloadHandler.text);
            else
                OnCreateComplete("ERROR|" + req.error);
        }
    }

    void DeleteCurrentConversation()
    {
        if (string.IsNullOrEmpty(currentConvoId)) return;

        StartCoroutine(APIManager.Instance.Delete($"/api/conversations/{currentConvoId}", (req) => {
            if (req != null && req.result == UnityWebRequest.Result.Success)
            {
                LoadConversationList(true); // Return to list, hide chat
                currentConvoId = null;
            }
        }, true));
    }

    // =================================================================================
    // 3. CHAT INTERACTION
    // =================================================================================

    void LoadChat(string id, string title)
    {
        currentConvoId = id;
        if (txtCurrentChatTitle) txtCurrentChatTitle.text = title;
        chatPanel.SetActive(true);

        foreach (Transform child in messageContentParent) Destroy(child.gameObject);

        StartCoroutine(APIManager.Instance.Get($"/api/conversations/{id}", (req) => {
            if (req != null && req.result == UnityWebRequest.Result.Success)
            {
                var convo = JsonConvert.DeserializeObject<FullConversation>(req.downloadHandler.text);
                foreach (var msg in convo.messages)
                {
                    if (msg.role == "user" || msg.role == "assistant")
                    {
                        SpawnMessage(msg.role, msg.content);
                    }
                }
                ScrollToBottom();
            }
        }, true));
    }

    void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(inputChat.text)) return;
        string userText = inputChat.text;

        inputChat.text = "";
        SpawnMessage("user", userText);
        ScrollToBottom();
        btnSend.interactable = false;

        // FEATURE: Auto-Create if no chat selected
        if (string.IsNullOrEmpty(currentConvoId))
        {
            string autoTitle = "Conversation " + (totalConversations + 1);
            StartCoroutine(AutoCreateAndSend(autoTitle, userText));
        }
        else
        {
            StartCoroutine(SendToBackend(userText));
        }
    }

    IEnumerator AutoCreateAndSend(string title, string initialUserMessage)
    {
        // Simple creation (Text only, no file) for quick start
        WWWForm form = new WWWForm();
        form.AddField("title", title);
        form.AddField("contextText", "You are a helpful assistant.");

        using (UnityWebRequest req = UnityWebRequest.Post(APIManager.Instance.apiBaseUrl + "/api/conversations", form))
        {
            req.SetRequestHeader("Authorization", "Bearer " + APIManager.Instance.authToken);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var newConvo = JsonConvert.DeserializeObject<ConversationSummary>(req.downloadHandler.text);

                currentConvoId = newConvo._id;
                if (txtCurrentChatTitle) txtCurrentChatTitle.text = newConvo.title;
                totalConversations++;

                LoadConversationList(false); // Update sidebar

                // Now send the message
                yield return StartCoroutine(SendToBackend(initialUserMessage));
            }
            else
            {
                SpawnMessage("assistant", "[Error: Failed to auto-create conversation.]");
                btnSend.interactable = true;
            }
        }
    }

    IEnumerator SendToBackend(string text)
    {
        string customKey = (toggleCustomKey.isOn) ? inputCustomKey.text : "";
        var payload = new { message = text, customKey = customKey };
        string json = JsonConvert.SerializeObject(payload);

        yield return StartCoroutine(APIManager.Instance.Post($"/api/conversations/{currentConvoId}/message", json, (req) => {
            btnSend.interactable = true;
            if (req != null && req.result == UnityWebRequest.Result.Success)
            {
                var res = JsonConvert.DeserializeObject<MessageData>(req.downloadHandler.text);
                SpawnMessage("assistant", res.content);
                ScrollToBottom();
            }
            else
            {
                SpawnMessage("assistant", "[Error sending message]");
            }
        }, true));
    }

    // =================================================================================
    // 4. HELPERS
    // =================================================================================

    void SpawnMessage(string role, string content)
    {
        GameObject prefab = (role == "user") ? userMsgPrefab : botMsgPrefab;
        GameObject msgObj = Instantiate(prefab, messageContentParent);

        TMP_Text txt = msgObj.GetComponentInChildren<TMP_Text>();
        if (txt) txt.text = content;

        LayoutRebuilder.ForceRebuildLayoutImmediate(msgObj.transform as RectTransform);
    }

    void ScrollToBottom()
    {
        StartCoroutine(ScrollFrame());
    }

    IEnumerator ScrollFrame()
    {
        yield return new WaitForEndOfFrame();
        ScrollRect sr = chatPanel.GetComponentInChildren<ScrollRect>();
        if (sr) sr.verticalNormalizedPosition = 0f;
    }

    // =================================================================================
    // 5. DATA CLASSES
    // =================================================================================

    [System.Serializable]
    public class ConversationSummary
    {
        public string _id;
        public string title;
    }

    [System.Serializable]
    public class FullConversation
    {
        public string _id;
        public List<MessageData> messages;
    }

    [System.Serializable]
    public class MessageData
    {
        public string role;
        public string content;
    }
}