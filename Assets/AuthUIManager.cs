using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Runtime.InteropServices;

public class AuthUIManager : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RedirectToURL(string url);

    [DllImport("__Internal")]
    private static extern string GetURLParameter(string name);

    [DllImport("__Internal")]
    private static extern void CleanURLParameters();
#endif

    [Header("Google Login")]
    public Button googleLoginButton;

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

    // FIX: Keys for PlayerPrefs to remember input fields.
    private const string PREF_LOGIN_EMAIL = "pref_login_email";
    private const string PREF_REG_EMAIL = "pref_reg_email";
    private const string PREF_REG_USERNAME = "pref_reg_username";


    void Start()
    {
        // FIX: Load previously entered data into input fields.
        LoadInputFields();
        SetupEventListeners();

        if (APIManager.Instance == null)
        {
            Debug.LogError("❌ APIManager.Instance is null!");
            ShowLoginUI();
            statusText.text = "Error: API Manager not found.";
            return;
        }

        APIManager.OnTokenExpired += HandleTokenExpired;
        RoomManager.OnJoinedPhotonLobby += HandleJoinedPhotonLobby;


#if UNITY_WEBGL && !UNITY_EDITOR
        string tokenFromUrl = GetURLParameter("token");
        if (!string.IsNullOrEmpty(tokenFromUrl))
        {
            Debug.Log("🔑 Token found in URL from Google redirect. Verifying...");
            statusText.text = "Verifying Google login...";
            CleanURLParameters();
            StartCoroutine(LoginWithToken(tokenFromUrl));
            return;
        }
#endif

        if (APIManager.Instance.isLoggedIn)
        {
            Debug.Log("Token found. Verifying session with server...");
            statusText.text = "Verifying previous session...";
            StartCoroutine(VerifySessionAndLogin());
        }
        else
        {
            Debug.Log("No token found. Showing login UI.");
            ShowLoginUI();
        }
    }

    void OnDestroy()
    {
        APIManager.OnTokenExpired -= HandleTokenExpired;
        RoomManager.OnJoinedPhotonLobby -= HandleJoinedPhotonLobby;

        // FIX: Unsubscribe from input field events to prevent memory leaks.
        loginEmailInput.onValueChanged.RemoveListener(SaveLoginEmail);
        registerEmailInput.onValueChanged.RemoveListener(SaveRegisterEmail);
        registerUsernameInput.onValueChanged.RemoveListener(SaveRegisterUsername);
    }

    // FIX: New method to load saved values from PlayerPrefs.
    void LoadInputFields()
    {
        loginEmailInput.text = PlayerPrefs.GetString(PREF_LOGIN_EMAIL, "");
        registerEmailInput.text = PlayerPrefs.GetString(PREF_REG_EMAIL, "");
        registerUsernameInput.text = PlayerPrefs.GetString(PREF_REG_USERNAME, "");
    }

    // FIX: New methods to save input field values as they are changed.
    private void SaveLoginEmail(string value) => PlayerPrefs.SetString(PREF_LOGIN_EMAIL, value.Trim());
    private void SaveRegisterEmail(string value) => PlayerPrefs.SetString(PREF_REG_EMAIL, value.Trim());
    private void SaveRegisterUsername(string value) => PlayerPrefs.SetString(PREF_REG_USERNAME, value.Trim());

    private IEnumerator LoginWithToken(string token)
    {
        APIManager.Instance.authToken = token;
        yield return APIManager.Instance.Get("/users/me", (res) => {
            if (res != null && res.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Token from URL is valid. Logging in.");
                var data = JsonUtility.FromJson<UserMeResponse>(res.downloadHandler.text);
                APIManager.Instance.SaveToken(token, data.username, data.id);

                statusText.text = "Connecting to room network...";
                roomManager.ConnectToPhoton();
                ShowLoggedInUI();
            }
            else
            {
                Debug.LogError("Failed to verify token from URL.");
                APIManager.Instance.ClearToken();
                ShowLoginUI();
                statusText.text = "Google login failed. Please try again.";
            }
        }, requireAuth: true);
    }

    private IEnumerator VerifySessionAndLogin()
    {
        yield return APIManager.Instance.Get("/users/me", (res) =>
        {
            if (res != null && res.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Session is valid. Auto-logging in.");

                statusText.text = "Connecting to room network...";
                roomManager.ConnectToPhoton();
                ShowLoggedInUI();
            }
            else if (res != null && (res.responseCode == 401 || res.responseCode == 403))
            {
                Debug.LogWarning("Session verification failed. Token is invalid or expired.");
                APIManager.Instance.HandleSessionExpired();
            }
            else
            {
                Debug.LogError("Could not connect to the server to verify session.");
                statusText.text = "Connection error. Please try again later.";
                ShowLoginUI();
            }
        }, requireAuth: true);
    }

    void OnGoogleLogin()
    {
        statusText.text = "Redirecting to Google...";
        string googleAuthUrl = APIManager.Instance.apiBaseUrl + "/auth/google";

#if UNITY_WEBGL && !UNITY_EDITOR
        RedirectToURL(googleAuthUrl);
#else
        Debug.LogWarning("Google login is only available in WebGL builds.");
        statusText.text = "Google Login only works in a WebGL build.";
#endif
    }


    void SetupEventListeners()
    {
        googleLoginButton?.onClick.AddListener(OnGoogleLogin);
        registerButton?.onClick.AddListener(OnRegister);
        loginButton?.onClick.AddListener(OnLogin);
        openRegisterPanelButton?.onClick.AddListener(ShowRegisterUI);
        backToLoginButton?.onClick.AddListener(ShowLoginUI);
        logoutButton?.onClick.AddListener(OnLogout);
        guestLoginButton?.onClick.AddListener(OnGuestLogin);
        clearTokenButton?.onClick.AddListener(OnClearToken);

        // FIX: Add listeners to save input fields on change.
        loginEmailInput.onValueChanged.AddListener(SaveLoginEmail);
        registerEmailInput.onValueChanged.AddListener(SaveRegisterEmail);
        registerUsernameInput.onValueChanged.AddListener(SaveRegisterUsername);
    }

    void SetActivePanel(GameObject panelToShow)
    {
        loginPanel.SetActive(panelToShow == loginPanel);
        registerPanel.SetActive(panelToShow == registerPanel);
        loggedInPanel.SetActive(panelToShow == loggedInPanel);
        if (roomManager != null && panelToShow != loggedInPanel)
        {
            roomManager.HideAllRoomUI();
        }
    }

    void ShowLoginUI()
    {
        SetActivePanel(loginPanel);
        statusText.text = "Please log in or continue as a guest.";
    }

    void ShowRegisterUI()
    {
        SetActivePanel(registerPanel);
        statusText.text = "Create your account.";
    }

    void ShowLoggedInUI()
    {
        SetActivePanel(loggedInPanel);
        string welcomeName = APIManager.Instance.isLoggedIn ? APIManager.Instance.username : "Guest";
        welcomeText.text = $"Welcome, {welcomeName}!";
        roomManager.PrepareRoomUI();
    }

    void OnClearToken()
    {
        APIManager.Instance.ClearToken();
        statusText.text = "Token cleared from local storage.";
        ShowLoginUI();
    }

    void OnLogout()
    {
        StartCoroutine(APIManager.Instance.Post("/users/signout", "", (res) => {
            Debug.Log("Sign-out signal sent to server.");
        }, requireAuth: true));

        APIManager.Instance.ClearToken();
        if (roomManager != null) roomManager.ClearRoomList();
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();

        ShowLoginUI();
        statusText.text = "You have been signed out.";
    }

    void OnRegister()
    {
        string username = registerUsernameInput.text.Trim();
        string email = registerEmailInput.text.Trim();
        string password = registerPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            statusText.text = "Please fill in all fields.";
            return;
        }

        // FIX: Provide immediate feedback on button click.
        statusText.text = "Creating your account...";
        var payload = new RegisterPayload(username, email, password);
        string json = JsonUtility.ToJson(payload);

        StartCoroutine(APIManager.Instance.Post("/users/register", json, (res) =>
        {
            if (res != null && res.result == UnityWebRequest.Result.Success)
            {
                statusText.text = "Registration successful! Please log in.";
                // FIX: Also save username/email on successful registration for convenience.
                PlayerPrefs.SetString(PREF_LOGIN_EMAIL, email);
                loginEmailInput.text = email;
                ShowLoginUI();
            }
            else
            {
                statusText.text = "Registration failed. Username or email may be taken.";
            }
        }));
    }

    void OnLogin()
    {
        string email = loginEmailInput.text.Trim();
        string password = loginPasswordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            statusText.text = "Please enter both email and password.";
            return;
        }

        // FIX: Provide immediate feedback on button click.
        statusText.text = "Logging in...";
        var payload = new LoginPayload(email, password);
        string json = JsonUtility.ToJson(payload);

        StartCoroutine(APIManager.Instance.Post("/users/login", json, (res) =>
        {
            if (res != null && res.result == UnityWebRequest.Result.Success)
            {
                var data = JsonUtility.FromJson<LoginResponse>(res.downloadHandler.text);
                APIManager.Instance.SaveToken(data.token, data.username, data.userId);

                statusText.text = "Connecting to room network...";
                roomManager.ConnectToPhoton();
                ShowLoggedInUI();
            }
            else
            {
                statusText.text = "Login failed. Please check your email and password.";
            }
        }));
    }

    void OnGuestLogin()
    {
        statusText.text = "Connecting to room network...";
        roomManager.ConnectToPhoton();
        ShowLoggedInUI();
    }

    void HandleTokenExpired()
    {
        statusText.text = "Your session has expired. Please log in again.";
        ShowLoginUI();
    }

    void HandleJoinedPhotonLobby()
    {
        if (loggedInPanel.activeInHierarchy)
        {
            statusText.text = "Connected! Ready to create or join a room.";
        }
    }

    #region DTO Classes
    [System.Serializable]
    private class UserMeResponse { public string id; public string username; public string email; }
    [System.Serializable] private class LoginResponse { public string token; public string username; public string userId; }
    [System.Serializable] private class LoginPayload { public string email; public string password; public LoginPayload(string e, string p) { email = e; password = p; } }
    [System.Serializable] private class RegisterPayload { public string username; public string email; public string password; public RegisterPayload(string u, string e, string p) { username = u; email = e; password = p; } }
    #endregion
}