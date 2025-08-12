using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeminiNPC : MonoBehaviour
{
    public GeminiPersona persona; // Drag your persona asset here
    public GeminiAPI geminiAPI;   // Assign this via inspector

    private string npcName;
    private string personaPrompt;
    private List<GeminiAPI.Content> conversationHistory = new();

    private void Awake()
    {
        npcName = persona.npcName;
        personaPrompt = persona.personaPrompt;

        // Start with the persona prompt
        conversationHistory.Add(geminiAPI.CreateContent("user", personaPrompt));
    }

    public IEnumerator SendMessageToNPC(string userMessage, System.Action<string> onReplyCallback)
    {
        // Append user message
        conversationHistory.Add(geminiAPI.CreateContent("user", userMessage));

        // Send to Gemini API
        yield return geminiAPI.SendRequest(conversationHistory, (reply) =>
        {
            conversationHistory.Add(geminiAPI.CreateContent("model", reply));
            onReplyCallback?.Invoke(reply);
        });
    }

    public void CreateNewConversation()
    {
        conversationHistory.Clear();
        conversationHistory.Add(geminiAPI.CreateContent("user", personaPrompt));
    }

    public void LoadConversationHistory(string savedJson)
    {
        GeminiAPI.ChatRequest chat = JsonUtility.FromJson<GeminiAPI.ChatRequest>(savedJson);
        conversationHistory = new List<GeminiAPI.Content>(chat.contents);
    }

    public string GetHistoryAsJson()
    {
        return JsonUtility.ToJson(new GeminiAPI.ChatRequest { contents = conversationHistory.ToArray() });
    }
}
