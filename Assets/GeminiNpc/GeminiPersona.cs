// GeminiPersona.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewGeminiPersona", menuName = "Gemini/NPC Persona")]
public class GeminiPersona : ScriptableObject
{
    public string npcName;
    [TextArea(3, 10)]
    public string personaPrompt;

    [Header("UI Prefabs")]
    public GameObject botMessagePrefab;
    public GameObject userMessagePrefab;
    public GameObject chatButtonPrefab;
}