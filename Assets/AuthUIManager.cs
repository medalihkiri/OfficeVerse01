using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class AuthUIManager : MonoBehaviour
{
    [Header("Register UI")]
    public GameObject registerPanel;
    public TMP_InputField registerUsernameInput;
    public TMP_InputField registerEmailInput;
    public TMP_InputField registerPasswordInput;
    public Button registerButton;
    public Button backToLoginButton;

    [Header("Login UI")]
    public GameObject loginPanel;
    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;
    public Button loginButton;
    public Button openRegisterPanelButton;

    [Header("Post-Login UI")]
    public GameObject loggedInPanel;
    public TextMeshProUGUI welcomeText;
    public Button logoutButton;

    [Header("Guest Mode")]
    public Button guestLoginButton;

    [Header("Status Display")]
    public TextMeshProUGUI statusText;

    [Header("Room Manager Reference")]
    public RoomManager roomManager;

    [Header("Debug / Utilities")]
    public Button clearTokenButton;


    void Awake()
    {
        Debug.Log("✅ AuthUIManager Awake called");
    }

    void Start()
    {
        Debug.Log("AuthUIManager Start called.");

        SetupEventListeners();

        if (APIManager.Instance == null)
        {
            Debug.LogError("❌ APIManager.Instance is null! Ensure APIManager is in the scene and loads before AuthUIManager.");
            ShowLoginUI_Fallback();
            return;
        }

        // This subscription is now for handling a confirmed expired session.
        APIManager.OnTokenExpired += HandleTokenExpired;

        // **FIXED**: Instead of assuming a token is valid, we now verify it.
        if (APIManager.Instance.isLoggedIn)
        {
            Debug.Log("🔑 Token found. Verifying session with server...");
            StartCoroutine(VerifySessionAndLogin());
        }
        else
        {
            Debug.Log("🔓 No token found. Showing login UI.");
            ShowLoginUI();
        }
    }

    // **NEW**: This coroutine handles the robust session check on startup.
    private IEnumerator VerifySessionAndLogin()
    {
        // We check a simple, protected endpoint. '/users/me' is perfect.
        yield return APIManager.Instance.Get("/users/me", (res) =>
        {
            // If the request was successful, the token is valid.
            if (res.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ Session is valid. Logging in.");
                PlayerDataManager.IsAuthenticated = true;
                PlayerDataManager.PlayerName = APIManager.Instance.username;
                ShowLoggedInUI();
            }
            // If we get a 401, the token is definitively expired or invalid.
            else if (res.responseCode == 401 || res.responseCode == 403)
            {
                Debug.LogWarning("⚠ Session verification failed. Token is invalid or expired.");
                APIManager.Instance.HandleSessionExpired(); // This triggers the logout flow.
            }
            // Handle other errors like no internet connection.
            else
            {
                Debug.LogError("❌ Could not connect to the server to verify session. Please check your connection.");
                statusText.text = "❌ Connection error. Please restart.";
                ShowLoginUI_Fallback(); // Show login UI but with a connection error message
            }
        }, requireAuth: true);
    }

    void SetupEventListeners()
    {
        registerButton?.onClick.AddListener(OnRegister);
        loginButton?.onClick.AddListener(OnLogin);
        openRegisterPanelButton?.onClick.AddListener(ShowRegisterUI);
        backToLoginButton?.onClick.AddListener(ShowLoginUI);
        logoutButton?.onClick.AddListener(OnLogout);
        guestLoginButton?.onClick.AddListener(OnGuestLogin);
        clearTokenButton?.onClick.AddListener(OnClearToken);
    }

    void ShowLoginUI_Fallback()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        if (loggedInPanel != null) loggedInPanel.SetActive(false);
        if (roomManager != null) roomManager.HideAllRoomUI();
    }


    void ShowLoginUI()
    {
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        loggedInPanel.SetActive(false);
        if (roomManager != null) roomManager.HideAllRoomUI();

        statusText.text = "🔐 Please log in or continue as guest.";
    }


    void ShowRegisterUI()
    {
        registerPanel.SetActive(true);
        loginPanel.SetActive(false);
        loggedInPanel.SetActive(false);
        roomManager.HideAllRoomUI();
        statusText.text = "📝 Create your account.";
    }

    void ShowLoggedInUI()
    {
        registerPanel.SetActive(false);
        loginPanel.SetActive(false);
        loggedInPanel.SetActive(true);

        welcomeText.text = $"👋 Welcome, {PlayerDataManager.PlayerName}!";
        statusText.text = "✅ You're logged in.";

        roomManager.PrepareRoomUI();
    }

    void OnClearToken()
    {
        APIManager.Instance.ClearToken();
        PlayerDataManager.IsAuthenticated = false;
        PlayerDataManager.PlayerName = "";
        PlayerDataManager.PlayerRoomName = "";
        statusText.text = "🗑️ Token cleared from local storage.";
        ShowLoginUI();
    }

    void OnLogout()
    {
        // Send a "fire and forget" request to the server.
        StartCoroutine(APIManager.Instance.Post("/users/signout", "", (res) => {
            Debug.Log("Signout signal sent to server.");
        }, requireAuth: true));

        // Immediately log out on the client.
        statusText.text = "✅ Signed out successfully.";
        APIManager.Instance.ClearToken();
        PlayerDataManager.IsAuthenticated = false;
        PlayerDataManager.PlayerName = "";
        PlayerDataManager.PlayerRoomName = "";
        roomManager?.ClearRoomList();
        ShowLoginUI();
    }

    void OnRegister()
    {
        string username = registerUsernameInput.text.Trim();
        string email = registerEmailInput.text.Trim();
        string password = registerPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            statusText.text = "⚠ Please fill in all fields.";
            return;
        }

        var payload = new RegisterPayload(username, email, password);
        string json = JsonUtility.ToJson(payload);

        StartCoroutine(APIManager.Instance.Post("/users/register", json, (res) =>
        {
            if (res.result == UnityWebRequest.Result.Success)
            {
                statusText.text = "✅ Registration successful. Please log in.";
                ShowLoginUI();
            }
            else
            {
                statusText.text = "❌ Registration failed. The username or email may already be in use.";
            }
        }));
    }

    void OnLogin()
    {
        string email = loginEmailInput.text.Trim();
        string password = loginPasswordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            statusText.text = "⚠ Please enter both email and password.";
            return;
        }

        var payload = new LoginPayload(email, password);
        string json = JsonUtility.ToJson(payload);

        StartCoroutine(APIManager.Instance.Post("/users/login", json, (res) =>
        {
            if (res.result == UnityWebRequest.Result.Success)
            {
                var data = JsonUtility.FromJson<LoginResponse>(res.downloadHandler.text);
                APIManager.Instance.SaveToken(data.token, data.username, data.userId);
                PlayerDataManager.IsAuthenticated = true;
                PlayerDataManager.PlayerName = data.username;
                ShowLoggedInUI();
            }
            else
            {
                statusText.text = "❌ Login failed. Please check your email and password.";
            }
        }));
    }

    void OnGuestLogin()
    {
        PlayerDataManager.IsAuthenticated = false;
        PlayerDataManager.PlayerName = $"Guest{Random.Range(1000, 9999)}";

        statusText.text = $"👤 Continuing as {PlayerDataManager.PlayerName} (Guest)";
        ShowLoggedInUI();
    }

    // This is now only called when a session is confirmed to be expired.
    void HandleTokenExpired()
    {
        statusText.text = "⚠ Session expired. Please log in again.";
        ShowLoginUI();
    }

    [System.Serializable] private class LoginResponse { public string token, username, userId; }
    [System.Serializable] private class LoginPayload { public string email, password; public LoginPayload(string e, string p) { email = e; password = p; } }
    [System.Serializable] private class RegisterPayload { public string username, email, password; public RegisterPayload(string u, string e, string p) { username = u; email = e; password = p; } }
}