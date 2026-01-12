using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json; // Requires Newtonsoft package

public class LLMManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform chatContent;
    public TMP_InputField inputMessage;
    public Button btnSend;
    public ScrollRect scrollRect;

    [Header("Settings UI")]
    public GameObject settingsPanel;
    public TMP_InputField inputCustomKey;
    public Toggle toggleCustomKey;

    [Header("Prefabs")]
    public GameObject userMessagePrefab;
    public GameObject botMessagePrefab;

    // Chat History
    private List<MessageData> chatHistory = new List<MessageData>();
    private bool isProcessing = false;

    void Start()
    {
        // Load Settings
        inputCustomKey.text = PlayerPrefs.GetString("UserGroqKey", "");
        toggleCustomKey.isOn = PlayerPrefs.GetInt("UseCustomKey", 0) == 1;

        btnSend.onClick.AddListener(OnSendClicked);

        // Save settings when changed
        toggleCustomKey.onValueChanged.AddListener((val) => PlayerPrefs.SetInt("UseCustomKey", val ? 1 : 0));
        inputCustomKey.onEndEdit.AddListener((val) => PlayerPrefs.SetString("UserGroqKey", val));
    }

    public void ToggleSettings() => settingsPanel.SetActive(!settingsPanel.activeSelf);

    void OnSendClicked()
    {
        if (isProcessing || string.IsNullOrWhiteSpace(inputMessage.text)) return;
        StartCoroutine(ProcessChat(inputMessage.text));
    }

    IEnumerator ProcessChat(string userText)
    {
        isProcessing = true;
        inputMessage.text = ""; // Clear input
        AppendMessage(userText, true);

        string customKey = toggleCustomKey.isOn ? inputCustomKey.text : "";

        // DECISION: Custom Key (Direct) OR Server Proxy (Free)
        if (!string.IsNullOrEmpty(customKey) && toggleCustomKey.isOn)
        {
            yield return StartCoroutine(CallGroqDirect(userText, customKey));
        }
        else
        {
            yield return StartCoroutine(CallGameServerProxy(userText));
        }

        isProcessing = false;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    // --- OPTION A: PROXY (Uses your APIManager) ---
    IEnumerator CallGameServerProxy(string text)
    {
        // Prepare Payload
        var payload = new
        {
            message = text,
            history = chatHistory // Send history for context
        };
        string json = JsonConvert.SerializeObject(payload);

        bool requestComplete = false;

        // Use your existing APIManager to handle Auth Tokens automatically
        StartCoroutine(APIManager.Instance.Post("/api/llm/chat-proxy", json, (req) =>
        {
            if (req == null || req.result != UnityWebRequest.Result.Success)
            {
                string error = req != null ? req.error : "Unknown Error";
                AppendMessage($"Server Error: {error}. Try using a Custom Key in settings.", false);
                if (settingsPanel != null) settingsPanel.SetActive(true);
            }
            else
            {
                var res = JsonConvert.DeserializeObject<ProxyResponse>(req.downloadHandler.text);
                AddToHistory("user", text);
                AddToHistory("assistant", res.content);
                AppendMessage(res.content, false);
            }
            requestComplete = true;
        }, true)); // requireAuth = true

        // Wait for callback
        while (!requestComplete) yield return null;
    }

    // --- OPTION B: DIRECT (Bypasses Server, uses user key) ---
    IEnumerator CallGroqDirect(string text, string key)
    {
        AddToHistory("user", text);

        // Standard OpenAI Format
        var reqObj = new
        {
            model = "llama-3.3-70b-versatile",
            messages = chatHistory
        };
        string json = JsonConvert.SerializeObject(reqObj);

        // We use raw UnityWebRequest here because APIManager is hardcoded to your backend URL
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
                AppendMessage($"Groq Error: {webReq.error}. Check your key.", false);
                // Remove failed message from history
                chatHistory.RemoveAt(chatHistory.Count - 1);
            }
            else
            {
                var res = JsonConvert.DeserializeObject<GroqResponse>(webReq.downloadHandler.text);
                string botReply = res.choices[0].message.content;
                AddToHistory("assistant", botReply);
                AppendMessage(botReply, false);
            }
        }
    }

    void AddToHistory(string role, string content)
    {
        chatHistory.Add(new MessageData { role = role, content = content });
        // Keep last 10 messages max
        if (chatHistory.Count > 10) chatHistory.RemoveAt(0);
    }

    void AppendMessage(string text, bool isUser)
    {
        GameObject prefab = isUser ? userMessagePrefab : botMessagePrefab;
        GameObject msgObj = Instantiate(prefab, chatContent);
        msgObj.GetComponentInChildren<TMP_Text>().text = text;
    }

    // Data Classes
    public class MessageData { public string role; public string content; }
    public class ProxyResponse { public string content; }
    public class GroqResponse { public Choice[] choices; }
    public class Choice { public MessageData message; }
}