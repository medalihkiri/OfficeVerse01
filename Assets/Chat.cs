using UnityEngine;
using UnityEngine.UI;

public class Chat : MonoBehaviour
{
    public GameObject startChatButton;
    public GameObject chatPanel;

    private void Start()
    {
        if (startChatButton == null || chatPanel == null)
        {
            Debug.LogError("Missing references in Chat script!");
        }

        startChatButton.SetActive(false);
        chatPanel.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("OnTriggerEnter called by: " + other.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered trigger.");
            startChatButton.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Debug.Log("OnTriggerExit called by: " + other.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player exited trigger.");
            startChatButton.SetActive(false);
        }
    }

    public void OnStartChatClicked()
    {
        Debug.Log("Chat started.");
        chatPanel.SetActive(true);
        startChatButton.SetActive(false);
    }
}
