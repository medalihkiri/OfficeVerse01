// --- START OF FILE APIManager.cs ---
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class APIManager : MonoBehaviour
{
    public static APIManager Instance;

    [Header("Config")]
    public string apiBaseUrl = "http://localhost:3000";
    public float requestTimeout = 10f;

    [Header("Runtime Session Data")]
    public string authToken;
    public string username;
    public string userId;
    public string CurrentRoomDbId { get; private set; }
    public bool isLoggedIn => !string.IsNullOrEmpty(authToken);

    public static event Action OnTokenExpired;

    private const string TOKEN_KEY = "auth_token";
    private const string USERNAME_KEY = "username";
    private const string USERID_KEY = "user_id";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadToken();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetCurrentRoomDbId(string roomDbId)
    {
        CurrentRoomDbId = roomDbId;
        Debug.Log($"[APIManager] Session room ID set to: {roomDbId}");
    }

    #region Token Management
    public void SaveToken(string token, string username, string userId)
    {
        authToken = token;
        this.username = username;
        this.userId = userId;

        PlayerPrefs.SetString(TOKEN_KEY, token);
        PlayerPrefs.SetString(USERNAME_KEY, username);
        PlayerPrefs.SetString(USERID_KEY, userId);
        PlayerPrefs.Save();
    }

    public void LoadToken()
    {
        authToken = PlayerPrefs.GetString(TOKEN_KEY, "");
        username = PlayerPrefs.GetString(USERNAME_KEY, "");
        userId = PlayerPrefs.GetString(USERID_KEY, "");
    }

    public void ClearToken()
    {
        authToken = "";
        username = "";
        userId = "";
        CurrentRoomDbId = null;

        PlayerPrefs.DeleteKey(TOKEN_KEY);
        PlayerPrefs.DeleteKey(USERNAME_KEY);
        PlayerPrefs.DeleteKey(USERID_KEY);
        PlayerPrefs.Save();
        Debug.Log("🗑 Token and session data cleared.");
    }

    public void HandleSessionExpired()
    {
        Debug.LogWarning("⚠ Session has expired or is invalid. Forcing logout.");
        ClearToken();
        OnTokenExpired?.Invoke();
    }
    #endregion

    #region API Helpers
    private bool RequireAuthCheck(bool requireAuth)
    {
        if (requireAuth && !isLoggedIn)
        {
            Debug.LogWarning("⚠ API call blocked: authentication required but no token found.");
            return false;
        }
        return true;
    }

    public IEnumerator Get(string endpoint, Action<UnityWebRequest> callback, bool requireAuth = false)
    {
        if (!RequireAuthCheck(requireAuth)) { callback?.Invoke(null); yield break; }
        using UnityWebRequest req = UnityWebRequest.Get(apiBaseUrl + endpoint);
        req.SetRequestHeader("Content-Type", "application/json");
        if (requireAuth) req.SetRequestHeader("Authorization", "Bearer " + authToken);
        req.timeout = (int)requestTimeout;
        yield return req.SendWebRequest();
        LogRequestResult("GET", endpoint, req);
        callback?.Invoke(req);
    }

    public IEnumerator Post(string endpoint, string jsonBody, Action<UnityWebRequest> callback, bool requireAuth = false)
    {
        if (!RequireAuthCheck(requireAuth)) { callback?.Invoke(null); yield break; }
        using UnityWebRequest req = new UnityWebRequest(apiBaseUrl + endpoint, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (requireAuth) req.SetRequestHeader("Authorization", "Bearer " + authToken);
        req.timeout = (int)requestTimeout;
        yield return req.SendWebRequest();
        LogRequestResult("POST", endpoint, req);
        callback?.Invoke(req);
    }

    // --- NEW METHOD ---
    public IEnumerator Put(string endpoint, string jsonBody, Action<UnityWebRequest> callback, bool requireAuth = false)
    {
        if (!RequireAuthCheck(requireAuth)) { callback?.Invoke(null); yield break; }
        using UnityWebRequest req = new UnityWebRequest(apiBaseUrl + endpoint, "PUT");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (requireAuth) req.SetRequestHeader("Authorization", "Bearer " + authToken);
        req.timeout = (int)requestTimeout;
        yield return req.SendWebRequest();
        LogRequestResult("PUT", endpoint, req);
        callback?.Invoke(req);
    }

    // --- NEW METHOD ---
    public IEnumerator Delete(string endpoint, Action<UnityWebRequest> callback, bool requireAuth = false)
    {
        if (!RequireAuthCheck(requireAuth)) { callback?.Invoke(null); yield break; }
        using UnityWebRequest req = UnityWebRequest.Delete(apiBaseUrl + endpoint);
        req.downloadHandler = new DownloadHandlerBuffer(); // Useful for getting error messages
        if (requireAuth) req.SetRequestHeader("Authorization", "Bearer " + authToken);
        req.timeout = (int)requestTimeout;
        yield return req.SendWebRequest();
        LogRequestResult("DELETE", endpoint, req);
        callback?.Invoke(req);
    }

    private void LogRequestResult(string method, string endpoint, UnityWebRequest req)
    {
        if (req.result == UnityWebRequest.Result.Success) { Debug.Log($"✅ {method} {endpoint} -> {req.responseCode}"); }
        else { Debug.LogError($"❌ {method} {endpoint} failed -> Code: {req.responseCode}, Result: {req.result}, Error: {req.error}\nResponse: {req.downloadHandler.text}"); }
    }
    #endregion
}
// --- END OF FILE APIManager.cs ---