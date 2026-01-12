using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json; // Ensure Newtonsoft.Json package is installed

public class LLMChatController : MonoBehaviour
{
    [Header("Backend Config")]
    // This connects to your specific endpoint in users.js/llm.js
    public string proxyEndpoint = "/api/llm/chat-proxy";

    [Header("Chat UI")]
    public GameObject chatPanel;
    public Transform messageContentParent; // The Content object in your ScrollView
    public ScrollRect chatScrollRect;
    public TMP_InputField inputMessage;
    public Button btnSend;
    public GameObject loadingIndicator; // A spinning icon or "..." text

    [Header("Settings UI")]
    public GameObject settingsPanel;
    public Button btnOpenSettings;
    public Button btnCloseSettings;
    public TMP_InputField inputCustomKey;
    public Toggle toggleUseCustomKey;
    public Button btnGetFreeKey; // <--- NEW: Redirects user to create key

    [Header("Prefabs")]
    public GameObject userMessagePrefab; // Right aligned
    public GameObject botMessagePrefab;  // Left aligned
    public GameObject errorSystemPrefab; // Center aligned (optional)

    // --- MEMORY SYSTEM ---
    // We store the conversation history here to send back to the AI for context
    private List<MessageData> conversationMemory = new List<MessageData>();
    private const int MAX_MEMORY_LENGTH = 12; // Keep last 12 messages for context

    void Start()
    {
        // 1. Setup UI Listeners
        btnSend.onClick.AddListener(OnSendMessage);
        btnOpenSettings.onClick.AddListener(() => TogglePanel(true));
        btnCloseSettings.onClick.AddListener(() => TogglePanel(false));

        // 2. Setup "Get Key" Button
        btnGetFreeKey.onClick.AddListener(() => {
            Application.OpenURL("https://console.groq.com/keys");
        });

        // 3. Load Saved Preferences
        if (PlayerPrefs.HasKey("UserGroqKey"))
            inputCustomKey.text = PlayerPrefs.GetString("UserGroqKey");

        toggleUseCustomKey.isOn = PlayerPrefs.GetInt("UseCustomKey", 0) == 1;

        // 4. Save Preferences on Change
        toggleUseCustomKey.onValueChanged.AddListener((val) => PlayerPrefs.SetInt("UseCustomKey", val ? 1 : 0));
        inputCustomKey.onEndEdit.AddListener((val) => PlayerPrefs.SetString("UserGroqKey", val));

        // 5. Initial State
        loadingIndicator.SetActive(false);
        settingsPanel.SetActive(false);
        chatPanel.SetActive(true);
    }

    void TogglePanel(bool showSettings)
    {
        settingsPanel.SetActive(showSettings);
        // Optional: Dim chat panel when settings are open
    }

    void OnSendMessage()
    {
        string text = inputMessage.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        // Clear input immediately for UX
        inputMessage.text = "";

        // 1. Show User Message
        SpawnMessage(text, true);

        // 2. Start Logic
        StartCoroutine(HandleChatRequest(text));
    }

    IEnumerator HandleChatRequest(string userText)
    {
        loadingIndicator.SetActive(true);
        btnSend.interactable = false;

        // Determine Mode
        bool useCustom = toggleUseCustomKey.isOn && !string.IsNullOrEmpty(inputCustomKey.text);

        if (useCustom)
        {
            yield return CallGroqDirect(userText, inputCustomKey.text);
        }
        else
        {
            yield return CallServerProxy(userText);
        }

        loadingIndicator.SetActive(false);
        btnSend.interactable = true;

        // Scroll to bottom
        Canvas.ForceUpdateCanvases();
        chatScrollRect.verticalNormalizedPosition = 0f;
    }

    // --- PROXY MODE (Free for User) ---
    IEnumerator CallServerProxy(string text)
    {
        // Payload includes history (Memory)
        var payload = new
        {
            message = text,
            history = conversationMemory
        };
        string json = JsonConvert.SerializeObject(payload);

        // Use APIManager.Instance to handle Auth Tokens automatically
        bool isDone = false;
        StartCoroutine(APIManager.Instance.Post(proxyEndpoint, json, (req) => {
            if (req == null || req.result != UnityWebRequest.Result.Success)
            {
                SpawnError("Server busy. Try adding your own key in Settings.");
                TogglePanel(true); // Auto-open settings on failure
            }
            else
            {
                var res = JsonConvert.DeserializeObject<ProxyResponse>(req.downloadHandler.text);
                UpdateMemory("user", text);
                UpdateMemory("assistant", res.content);
                SpawnMessage(res.content, false);
            }
            isDone = true;
        }, true)); // requireAuth = true

        while (!isDone) yield return null;
    }

    // --- DIRECT MODE (BYOK) ---
    IEnumerator CallGroqDirect(string text, string key)
    {
        UpdateMemory("user", text); // Add to memory temporarily for the request

        var reqObj = new
        {
            model = "llama-3.3-70b-versatile",
            messages = conversationMemory
        };
        string json = JsonConvert.SerializeObject(reqObj);

        using (UnityWebRequest webReq = new UnityWebRequest("https://api.groq.com/openai/v1/chat/completions", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            webReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webReq.downloadHandler = new DownloadHandlerBuffer();
            webReq.SetRequestHeader("Content-Type", "application/json");
            webReq.SetRequestHeader("Authorization", "Bearer " + key);

            yield return webReq.SendWebRequest();

            if (webReq.result != UnityWebRequest.Result.Success)
            {
                SpawnError($"Groq API Error: {webReq.error}");
                // Revert memory since it failed
                conversationMemory.RemoveAt(conversationMemory.Count - 1);
            }
            else
            {
                var res = JsonConvert.DeserializeObject<GroqResponse>(webReq.downloadHandler.text);
                string botReply = res.choices[0].message.content;
                UpdateMemory("assistant", botReply);
                SpawnMessage(botReply, false);
            }
        }
    }

    // --- HELPER FUNCTIONS ---

    void UpdateMemory(string role, string content)
    {
        conversationMemory.Add(new MessageData { role = role, content = content });
        // Sliding Window: Prevent memory from getting too large
        if (conversationMemory.Count > MAX_MEMORY_LENGTH)
        {
            conversationMemory.RemoveAt(0);
        }
    }

    void SpawnMessage(string content, bool isUser)
    {
        GameObject prefab = isUser ? userMessagePrefab : botMessagePrefab;
        GameObject msgObj = Instantiate(prefab, messageContentParent);

        // Assumes your prefab has a TMP_Text component either on root or child
        TMP_Text txt = msgObj.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = content;

        // Optional: Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageContentParent as RectTransform);
    }

    void SpawnError(string content)
    {
        if (errorSystemPrefab)
        {
            GameObject msgObj = Instantiate(errorSystemPrefab, messageContentParent);
            msgObj.GetComponentInChildren<TMP_Text>().text = content;
        }
        else
        {
            SpawnMessage(content, false); // Fallback
        }
    }

    // Data Structures
    public class MessageData { public string role; public string content; }
    public class ProxyResponse { public string content; }
    public class GroqResponse { public Choice[] choices; }
    public class Choice { public MessageData message; }
}