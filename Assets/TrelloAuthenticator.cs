using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using TMPro; // Using TextMeshPro. Replace with `using UnityEngine.UI;` if using legacy Text.

// A simple helper class to deserialize the JSON response from your server.
[System.Serializable]
public class TrelloAuthResponse
{
    public string url;
}

/// <summary>
/// Manages the Trello authentication flow by coordinating with the APIManager.
/// This script should be placed on a GameObject in the scene where Trello authentication can be initiated.
/// </summary>
public class TrelloAuthenticator : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The button the user clicks to start the Trello connection process.")]
    public Button trelloAuthButton;

    [Tooltip("The text field used to display status messages to the user.")]
    public TMP_Text statusText; // Use `public Text statusText;` for legacy UI.

    private const string TRELLO_START_ENDPOINT = "users/trello/start";

    void Start()
    {
        // Ensure the button and text references are set up correctly.
        if (trelloAuthButton == null || statusText == null)
        {
            Debug.LogError("TrelloAuthenticator is missing UI references! Please assign the button and status text in the Inspector.");
            return;
        }

        // Add a listener to the button's onClick event.
        trelloAuthButton.onClick.AddListener(InitiateTrelloAuth);

        // Initial check: The user must be logged in to connect their Trello account.
        if (APIManager.Instance != null && !APIManager.Instance.isLoggedIn)
        {
            trelloAuthButton.interactable = false;
            statusText.text = "You must be logged in to connect your Trello account.";
        }
        else
        {
            statusText.text = "Connect your Trello account to integrate your boards.";
        }
    }

    /// <summary>
    /// Public method called by the UI button to start the authentication process.
    /// </summary>
    public void InitiateTrelloAuth()
    {
        // Disable the button to prevent multiple clicks during the process.
        trelloAuthButton.interactable = false;
        statusText.text = "Initializing Trello connection...";

        // Start the coroutine that communicates with the API.
        StartCoroutine(RequestTrelloAuthUrl());
    }

    /// <summary>
    /// Coroutine that calls the APIManager to get the Trello authorization URL.
    /// </summary>
    private IEnumerator RequestTrelloAuthUrl()
    {
        // Use the APIManager to make an authenticated GET request.
        // The 'true' flag ensures the user's JWT token is sent in the header.
        yield return APIManager.Instance.Get(TRELLO_START_ENDPOINT, OnAuthUrlReceived, true);
    }

    /// <summary>
    /// Callback method that processes the response from the APIManager.
    /// </summary>
    /// <param name="request">The completed UnityWebRequest from the APIManager.</param>
    private void OnAuthUrlReceived(UnityWebRequest request)
    {
        // Check if the request failed (due to network error, server error, etc.).
        if (request == null || request.result != UnityWebRequest.Result.Success)
        {
            statusText.text = "Error: Could not connect to the server. Please try again later.";
            Debug.LogError($"Trello Auth Failed. Response Code: {request?.responseCode}, Error: {request?.error}");

            // Re-enable the button so the user can try again.
            trelloAuthButton.interactable = true;
            return;
        }

        // If the request was successful, attempt to parse the JSON response.
        string jsonResponse = request.downloadHandler.text;
        try
        {
            TrelloAuthResponse response = JsonUtility.FromJson<TrelloAuthResponse>(jsonResponse);

            // Check if we got a valid URL.
            if (response != null && !string.IsNullOrEmpty(response.url))
            {
                statusText.text = "Success! Opening Trello authorization in your browser...";

                // Open the URL in the system's default browser.
                Application.OpenURL(response.url); 
                
                // Provide final instructions to the user.
                statusText.text = "Please complete the authorization in your browser. The browser tab should close automatically when done.";
            }
            else
            {
                throw new System.Exception("Response JSON did not contain a valid URL.");
            }
        }
        catch (System.Exception ex)
        {
            // This catches errors from JSON parsing or if the URL is missing.
            statusText.text = "Error: Received an invalid response from the server.";
            Debug.LogError($"Failed to parse Trello auth URL from response: {jsonResponse}. Exception: {ex.Message}");
            trelloAuthButton.interactable = true;
        }
    }

    private void OnDestroy()
    {
        // Clean up the listener when the object is destroyed.
        if (trelloAuthButton != null)
        {
            trelloAuthButton.onClick.RemoveListener(InitiateTrelloAuth);
        }
    }
}