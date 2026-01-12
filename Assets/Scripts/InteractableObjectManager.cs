using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Networking;
using Newtonsoft.Json; // optional: if you don't have it, you can remove JSON usage or use Unity's JsonUtility

/// <summary>
/// Single-file system for:
/// - Instantiating networked prefabs (Photon PUN2)
/// - Picking up / carrying / rotating / placing them (owner transfer)
/// - Syncing transforms over the network (IPunObservable)
/// - Producing a serializable "PlacedObjectData" for backend saving + built-in coroutine to send to a REST endpoint
///
/// How to use (quick):
/// 1) Put your networkable prefabs in Resources/NetworkPrefabs and add a PhotonView + InteractableNetworkObject component to each prefab.
/// 2) Attach InteractableObjectManager to a GameObject in scene (GameManager is fine).
/// 3) Assign available prefabs in the Inspector (prefab names used must match Resources path).
/// 4) Call InstantiateNetworked(prefabIndex, worldPos, Quaternion.identity) from your UI or PlayerController.
/// 5) Local player picks objects with the configured input (default: E to pickup/drop, left mouse to pick by click).
/// </summary>
public class InteractableObjectManager : MonoBehaviourPun
{

    public static InteractableObjectManager Instance;


    [Header("Prefabs (Must be in Resources/NetworkPrefabs by name)")]
    [Tooltip("Names of prefabs (string names) to instantiate from Resources/NetworkPrefabs")]
    public string[] networkPrefabNames;

    [Header("Carry settings")]
    public Transform holdPoint; // Assign a child transform of player or camera where the object will be held
    public float followLerp = 25f;
    public float rotationSpeed = 150f; // degrees/sec when rotating with Q/E
    public KeyCode pickupKey = KeyCode.E; // quick pickup/drop key
    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E; // if E used for pickup, change in inspector

    [Header("Networking")]
    [Tooltip("Threshold to send updates (meters). Reduces bandwidth.")]
    public float positionSendThreshold = 0.01f;
    [Tooltip("Threshold to send rotation updates (degrees).")]
    public float rotationSendThreshold = 0.5f;

    [Header("Backend (optional)")]
    [Tooltip("If non-empty, OnPlace will attempt to POST placed data JSON to this URL (POST JSON).")]
    public string backendSaveUrl = "";

    // Internal runtime
    private InteractableNetworkObject heldObject; // object currently held by this local player
    private Camera mainCam;
    private PlayerController localPlayerController;

    private void Awake()
    {

        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mainCam = Camera.main;
        if (holdPoint == null)
        {
            // Create a temporary hold point at camera if none provided
            GameObject hp = new GameObject("[HoldPoint]");
            hp.transform.SetParent(mainCam.transform, false);
            hp.transform.localPosition = new Vector3(0f, -0.3f, 1.0f);
            holdPoint = hp.transform;
        }
    }

    private void Start()
    {
        // Try to find local PlayerController if it's present (from your GameManager)
        localPlayerController = FindLocalPlayerController();
    }

    private PlayerController FindLocalPlayerController()
    {
        // Best-effort: your GameManager exposes myPlayer
        if (GameManager.Instance != null && GameManager.Instance.myPlayer != null)
            return GameManager.Instance.myPlayer;
        // otherwise try to find PhotonView owned PlayerController in scene
        PlayerController[] pcs = FindObjectsOfType<PlayerController>();
        foreach (var p in pcs) if (p.view != null && p.view.IsMine) return p;
        return null;
    }

    private void Update()
    {
        // Only local player controls picking/carrying
        if (!PhotonNetwork.InRoom) return;
        if (PhotonNetwork.LocalPlayer == null) return;

        HandlePickupInput();
        HandleCarryLogic();
    }

    private void HandlePickupInput()
    {
        // Primary pickup by key
        if (Input.GetKeyDown(pickupKey))
        {
            if (heldObject != null)
            {
                // Drop/place
                PlaceHeldObject();
            }
            else
            {
                // Try pick nearest object in front of the player (raycast)
                TryPickObjectUnderCursor();
            }
        }

        // Left mouse click to pick by pointing
        /*if (Input.GetMouseButtonDown(0) && heldObject == null)
        {
            if (EventSystems.EventSystem.current != null && EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                // clicked on UI, ignore
            }
            else
            {
                TryPickObjectUnderCursor();
            }
        }*/
    }

    private void TryPickObjectUnderCursor()
    {
        Vector2 worldPoint = mainCam.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
        if (hit.collider == null) return;

        InteractableNetworkObject ino = hit.collider.GetComponentInParent<InteractableNetworkObject>();
        if (ino == null) return;

        // Request ownership and pick if allowed
        StartCoroutine(RequestAndPickup(ino));
    }

    private IEnumerator RequestAndPickup(InteractableNetworkObject ino)
    {
        if (ino == null) yield break;

        PhotonView pv = ino.photonView;
        if (pv == null) yield break;

        // Request ownership
        pv.RequestOwnership();
        // Wait a frame for Photon ownership transfer (non-blocking, WebGL-safe)
        yield return null;

        // Double-check ownership
        if (pv.IsMine)
        {
            // Tell the object to become "held" on all clients (so its state becomes kinematic/ignored by physics)
            pv.RPC(nameof(InteractableNetworkObject.RPC_BeginHold), RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.ActorNumber);
            heldObject = ino;
        }
    }

    private void HandleCarryLogic()
    {
        if (heldObject == null) return;
        if (heldObject.photonView == null) return;

        // If we lost ownership (someone else took it) clear local references
        if (!heldObject.photonView.IsMine)
        {
            heldObject = null;
            return;
        }

        // Position and rotation control while holding (smooth follow)
        Vector3 desiredPos = holdPoint.position;
        // smooth movement
        heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, desiredPos, followLerp * Time.deltaTime);

        // Rotation control: rotate with Q/E keys
        float rot = 0f;
        if (Input.GetKey(rotateLeftKey)) rot += rotationSpeed * Time.deltaTime;
        if (Input.GetKey(rotateRightKey)) rot -= rotationSpeed * Time.deltaTime;
        if (Mathf.Abs(rot) > Mathf.Epsilon)
            heldObject.transform.Rotate(Vector3.forward, rot, Space.World);

        // Update held object's server-side transform authoritative values (only owner sends)
        heldObject.MarkTransformDirtyIfNeeded(positionSendThreshold, rotationSendThreshold);
    }

    /// <summary>
    /// Finalize placement: release ownership and broadcast final state to everyone.
    /// Saves to backend if URL present.
    /// </summary>
    public void PlaceHeldObject()
    {
        if (heldObject == null) return;
        PhotonView pv = heldObject.photonView;
        if (pv == null) return;

        // Inform all clients that this object is placed and who placed it
        pv.RPC(nameof(InteractableNetworkObject.RPC_EndHoldAndPlace), RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.ActorNumber);

        // Build save data and start backend coroutine (non-blocking)
        PlacedObjectData data = new PlacedObjectData()
        {
            prefabName = heldObject.prefabName,
            viewId = pv.ViewID,
            ownerActorNumber = PhotonNetwork.LocalPlayer.ActorNumber,
            position = heldObject.transform.position,
            rotation = heldObject.transform.rotation.eulerAngles,
            scale = heldObject.transform.localScale,
            customData = heldObject.customSaveData
        };

        if (!string.IsNullOrEmpty(backendSaveUrl))
        {
            StartCoroutine(SendToBackend(backendSaveUrl, data));
        }
        else
        {
            // Local cache fallback (PlayerPrefs) - small convenience
            string key = $"PlacedObject_{pv.ViewID}";
            try
            {
                string json = JsonConvert.SerializeObject(data);
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
            }
        }

        // Release local reference
        heldObject = null;
    }

    /// <summary>
    /// Instantiate a named prefab from Resources/NetworkPrefabs by index (safe for WebGL).
    /// Returns PhotonView of the instantiated object (or null).
    /// </summary>
    public PhotonView InstantiateNetworked(int prefabIndex, Vector3 position, Quaternion rotation)
    {
        if (prefabIndex < 0 || prefabIndex >= networkPrefabNames.Length)
        {
            Debug.LogError("[InteractableObjectManager] prefabIndex out of range");
            return null;
        }

        string prefabName = networkPrefabNames[prefabIndex];
        // Photon requires that a prefab with this exact name exists in Resources
        GameObject go = PhotonNetwork.Instantiate($"NetworkPrefabs/{prefabName}", position, rotation);
        if (go == null)
        {
            Debug.LogError($"Failed to instantiate prefab {prefabName}. Ensure prefab is in Resources/NetworkPrefabs and the name matches.");
            return null;
        }

        InteractableNetworkObject ino = go.GetComponent<InteractableNetworkObject>();
        if (ino == null)
        {
            Debug.LogWarning("Instantiated object does not have InteractableNetworkObject component. Adding one automatically.");
            ino = go.AddComponent<InteractableNetworkObject>();
            // note: PhotonView should already be present when instantiating via Photon
        }

        ino.prefabName = networkPrefabNames[prefabIndex];
        // Init on owner
        if (go.GetPhotonView() != null && go.GetPhotonView().IsMine)
        {
            go.GetPhotonView().RPC(nameof(InteractableNetworkObject.RPC_InitializeOwner), RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        return go.GetPhotonView();
    }

    /// <summary>
    /// Sends placed object data JSON to backend url via POST. Non-blocking (coroutine).
    /// Expects server to accept JSON body. Uses UnityWebRequest which works in WebGL.
    /// </summary>
    private IEnumerator SendToBackend(string url, PlacedObjectData data)
    {
        string json;
        try { json = JsonConvert.SerializeObject(data); } // using Newtonsoft if present
        catch { json = JsonUtility.ToJson(data); }

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError)
#else
        if (req.isNetworkError || req.isHttpError)
#endif
        {
            Debug.LogError($"[InteractableObjectManager] Backend save failed: {req.error}");
        }
        else
        {
            Debug.Log($"[InteractableObjectManager] Backend save success: {req.downloadHandler.text}");
        }
    }

    #region DTO
    [Serializable]
    public class PlacedObjectData
    {
        public string prefabName;
        public int viewId;
        public int ownerActorNumber;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public string customData; // optional freeform JSON or string for game-specific fields
    }
    #endregion
}