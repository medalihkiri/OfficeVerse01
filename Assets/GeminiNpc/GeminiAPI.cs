using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class GeminiAPI : MonoBehaviour
{
    [Header("API Key")]
    [Tooltip("Drag and drop a JSON file formatted as { \"apiKey\": \"YOUR_KEY_HERE\" }")]
    public TextAsset apiKeyFile;

    private string apiKey = "";
    private string apiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent";

    [System.Serializable] public class Part { public string text; }
    [System.Serializable] public class Content { public string role; public Part[] parts; }
    [System.Serializable] public class ChatRequest { public Content[] contents; }
    [System.Serializable] public class Candidate { public Content content; }
    [System.Serializable] public class Response { public Candidate[] candidates; }
    [System.Serializable] private class APIKeyWrapper { public string apiKey; }

    private void Awake()
    {
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        if (apiKeyFile == null)
        {
            Debug.LogError("GeminiAPI: No API key file assigned!");
            return;
        }

        try
        {
            APIKeyWrapper wrapper = JsonUtility.FromJson<APIKeyWrapper>(apiKeyFile.text);
            apiKey = wrapper.apiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("GeminiAPI: API key is empty.");
            }
        }
        catch
        {
            Debug.LogError("GeminiAPI: Failed to parse API key file. Make sure it’s formatted as { \"apiKey\": \"YOUR_KEY\" }");
        }
    }

    public IEnumerator SendRequest(List<Content> history, System.Action<string> onResponse)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("GeminiAPI: Cannot send request — API key is missing.");
            yield break;
        }

        ChatRequest chatRequest = new ChatRequest { contents = history.ToArray() };
        string jsonData = JsonUtility.ToJson(chatRequest);
        byte[] jsonToSend = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest www = new UnityWebRequest($"{apiEndpoint}?key={apiKey}", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                string reply = response.candidates[0].content.parts[0].text;
                onResponse?.Invoke(reply);
            }
            else
            {
                Debug.LogError("Gemini API Error: " + www.error);
            }
        }
    }

    public Content CreateContent(string role, string text)
    {
        return new Content { role = role, parts = new Part[] { new Part { text = text } } };
    }
}
