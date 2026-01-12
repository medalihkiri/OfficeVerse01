// --- START OF FILE PersistenceManager.cs ---
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;
using System.Collections.Generic;

public class PersistenceManager : MonoBehaviour
{
    public static PersistenceManager Instance { get; private set; }

    [Header("Retry Logic")]
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float initialRetryDelay = 1.0f;

    #region Data Structures
    [System.Serializable]
    public class Vector3Data { public float x, y, z; }
    [System.Serializable]
    public class QuaternionData { public float x, y, z, w; }
    [System.Serializable]
    public class PersistentObjectData { public string instanceId, roomId, ownerId, prefabName; public Vector3Data position; public QuaternionData rotation; }
    [System.Serializable]
    public class ObjectListResponse { public bool success; public List<PersistentObjectData> objects; }
    [System.Serializable]
    private class ObjectSaveRequest { public string instanceId, prefabName; public Vector3Data position; public QuaternionData rotation; }
    [System.Serializable]
    private class ObjectUpdateRequest { public Vector3Data position; public QuaternionData rotation; }
    #endregion

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    #region Public API Methods
    public void LoadRoomObjects(string roomId, Action<List<PersistentObjectData>> onComplete)
    {
        StartCoroutine(SendRequestWithRetry(
            BuildGetRequest($"/rooms/{roomId}/objects"),
            (webRequest) =>
            {
                if (webRequest == null || webRequest.result != UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(new List<PersistentObjectData>());
                    return;
                }
                ObjectListResponse response = JsonUtility.FromJson<ObjectListResponse>(webRequest.downloadHandler.text);
                onComplete?.Invoke(response?.objects ?? new List<PersistentObjectData>());
            })
        );
    }

    public void SaveNewObject(string roomId, string instanceId, string prefabName, Vector3 position, Quaternion rotation)
    {
        var requestData = new ObjectSaveRequest
        {
            instanceId = instanceId,
            prefabName = prefabName,
            position = new Vector3Data { x = position.x, y = position.y, z = position.z },
            rotation = new QuaternionData { x = rotation.x, y = rotation.y, z = rotation.z, w = rotation.w }
        };
        string jsonBody = JsonUtility.ToJson(requestData);
        StartCoroutine(SendRequestWithRetry(BuildPostRequest($"/rooms/{roomId}/objects", jsonBody)));
    }

    public void UpdateObjectTransform(string roomId, string instanceId, Vector3 position, Quaternion rotation)
    {
        var requestData = new ObjectUpdateRequest
        {
            position = new Vector3Data { x = position.x, y = position.y, z = position.z },
            rotation = new QuaternionData { x = rotation.x, y = rotation.y, z = rotation.z, w = rotation.w }
        };
        string jsonBody = JsonUtility.ToJson(requestData);
        StartCoroutine(SendRequestWithRetry(BuildPutRequest($"/rooms/{roomId}/objects/{instanceId}", jsonBody)));
    }

    // CHANGED: Added Action<bool> callback to know if delete succeeded
    public void DeleteObject(string roomId, string instanceId, Action<bool> onComplete = null)
    {
        StartCoroutine(SendRequestWithRetry(
            BuildDeleteRequest($"/rooms/{roomId}/objects/{instanceId}"),
            (webRequest) =>
            {
                // Check if request was successful (HTTP 200-299)
                bool success = webRequest != null && webRequest.result == UnityWebRequest.Result.Success;
                onComplete?.Invoke(success);
            }
        ));
    }
    #endregion

    #region Request Builders
    private UnityWebRequest BuildGetRequest(string uri) => BuildRequest(uri, "GET");
    private UnityWebRequest BuildDeleteRequest(string uri) => BuildRequest(uri, "DELETE");
    private UnityWebRequest BuildPostRequest(string uri, string jsonBody) => BuildRequest(uri, "POST", jsonBody);
    private UnityWebRequest BuildPutRequest(string uri, string jsonBody) => BuildRequest(uri, "PUT", jsonBody);

    private UnityWebRequest BuildRequest(string uri, string method, string jsonBody = null)
    {
        var request = new UnityWebRequest(APIManager.Instance.apiBaseUrl + uri, method);

        if (!string.IsNullOrEmpty(jsonBody))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
        }

        request.downloadHandler = new DownloadHandlerBuffer();

        string currentToken = APIManager.Instance.authToken;
        if (!string.IsNullOrEmpty(currentToken))
        {
            request.SetRequestHeader("Authorization", "Bearer " + currentToken);
        }

        return request;
    }
    #endregion

    #region Coroutine Logic
    private IEnumerator SendRequestWithRetry(UnityWebRequest request, Action<UnityWebRequest> onComplete = null)
    {
        float delay = initialRetryDelay;
        for (int i = 0; i < maxRetries; i++)
        {
            var requestAttempt = BuildRequest(request.uri.PathAndQuery, request.method, request.uploadHandler?.data != null ? Encoding.UTF8.GetString(request.uploadHandler.data) : null);

            using (requestAttempt)
            {
                // LogRequest(requestAttempt); // Optional logging
                yield return requestAttempt.SendWebRequest();

                if (requestAttempt.result == UnityWebRequest.Result.Success)
                {
                    // LogResponse(requestAttempt);
                    onComplete?.Invoke(requestAttempt);
                    yield break;
                }

                // LogResponse(requestAttempt, isError: true, attempt: i + 1);

                if (requestAttempt.result == UnityWebRequest.Result.ConnectionError || requestAttempt.result == UnityWebRequest.Result.DataProcessingError)
                {
                    if (i < maxRetries - 1)
                    {
                        Debug.LogWarning($"[Persistence] Retrying in {delay} seconds...");
                        yield return new WaitForSeconds(delay);
                        delay *= 2;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        Debug.LogError($"[Persistence] Request failed after {maxRetries} attempts.");
        onComplete?.Invoke(null);
    }
    #endregion
}
// --- END OF FILE PersistenceManager.cs ---