// GeminiNPCManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeminiNPCManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject chatPanel;
    public TMP_InputField inputField;
    public Transform chatContent;
    public GameObject userMessagePrefab;
    public GameObject botMessagePrefab;
    public GameObject chatButtonPrefab;

    public Transform chatListContent;

    [Header("Shared API Reference")]
    public GeminiAPI geminiAPI;

    private Dictionary<string, GeminiNPC> npcRegistry = new();
    private Dictionary<string, List<GeminiAPI.Content>> chatHistories = new();
    private string activeNPCName = "";

    private void Start()
    {
        RegisterAllNPCs();
        CreateInitialChats();
        chatPanel.SetActive(false);
    }

    private void RegisterAllNPCs()
    {
        GeminiNPC[] allNpcs = FindObjectsOfType<GeminiNPC>();

        foreach (GeminiNPC npc in allNpcs)
        {
            string npcName = npc.persona.npcName;
            if (!npcRegistry.ContainsKey(npcName))
            {
                npcRegistry[npcName] = npc;
                npc.CreateNewConversation();
            }
        }
    }

    public void CloseChatPanel()
    {
        chatPanel.SetActive(false);
    }

    private void CreateInitialChats()
    {
        foreach (var kvp in npcRegistry)
        {
            string npcName = kvp.Key;
            CreateChatButton(npcName);
            chatHistories[npcName] = new List<GeminiAPI.Content>();
        }
    }

    private void CreateChatButton(string npcName)
    {
        GameObject buttonGO = Instantiate(chatButtonPrefab, chatListContent);
        buttonGO.GetComponentInChildren<TMP_Text>().text = npcName;

        buttonGO.GetComponent<Button>().onClick.AddListener(() => LoadChat(npcName));
    }

    public void LoadChat(string npcName)
    {
        if (!npcRegistry.ContainsKey(npcName)) return;

        activeNPCName = npcName;
        ClearMessages();

        var history = chatHistories[npcName];
        foreach (var content in history)
        {
            AddMessageToPanel(content.parts[0].text, content.role == "user");
        }

        chatPanel.SetActive(true);
    }

    public void SendMessageFromUI()
    {
        string message = inputField.text;
        if (string.IsNullOrWhiteSpace(message) || !npcRegistry.ContainsKey(activeNPCName)) return;

        AddMessageToPanel(message, true);
        inputField.text = "";

        StartCoroutine(npcRegistry[activeNPCName].SendMessageToNPC(message, (reply) =>
        {
            AddMessageToPanel(reply, false);

            // Save history
            chatHistories[activeNPCName].Add(geminiAPI.CreateContent("user", message));
            chatHistories[activeNPCName].Add(geminiAPI.CreateContent("model", reply));
        }));
    }

    public void StartChatWithNPC(GeminiNPC npc)
    {
        string npcName = npc.persona.npcName;
        if (!npcRegistry.ContainsKey(npcName))
        {
            npcRegistry[npcName] = npc;
            npc.CreateNewConversation();
            CreateChatButton(npcName);
            chatHistories[npcName] = new List<GeminiAPI.Content>();
        }

        LoadChat(npcName);
    }

    private void AddMessageToPanel(string message, bool isUser)
    {
        GameObject prefab = isUser ? userMessagePrefab : botMessagePrefab;
        GameObject msgGO = Instantiate(prefab, chatContent);
        msgGO.GetComponentInChildren<TMP_Text>().text = message;
    }

    private void ClearMessages()
    {
        foreach (Transform child in chatContent)
        {
            Destroy(child.gameObject);
        }
    }
}
