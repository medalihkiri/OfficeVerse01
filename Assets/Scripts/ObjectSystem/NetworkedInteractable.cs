// --- START OF FILE NetworkedInteractable.cs ---
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkedInteractable : MonoBehaviour, IPunObservable, IPunInstantiateMagicCallback
{
    [Header("Network Smoothing")]
    [Tooltip("How quickly remote clients interpolate to the correct position.")]
    [SerializeField] private float positionLerpSpeed = 15f;
    [Tooltip("How quickly remote clients interpolate rotation.")]
    [SerializeField] private float rotationLerpSpeed = 15f;

    public string InstanceId { get; private set; }

    private PhotonView _photonView;
    private Vector3 _networkPosition;
    private Quaternion _networkRotation;

    // Prevent double-deletion attempts while waiting for server
    private bool _isPendingDestroy = false;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] instantiationData = info.photonView.InstantiationData;
        if (instantiationData != null && instantiationData.Length > 0)
        {
            this.InstanceId = (string)instantiationData[0];
        }
        else
        {
            Debug.LogWarning($"Object '{gameObject.name}' was instantiated without a persistent ID. It will not be saved.", this);
        }
    }

    void Awake()
    {
        _photonView = GetComponent<PhotonView>();
        _networkPosition = transform.position;
        _networkRotation = transform.rotation;
    }

    void Update()
    {
        if (!_photonView.IsMine)
        {
            transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * positionLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _networkRotation, Time.deltaTime * rotationLerpSpeed);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            _networkPosition = (Vector3)stream.ReceiveNext();
            _networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }

    public void OnStartDrag() { }

    public void OnEndDrag()
    {
        if (_photonView.IsMine && !_isPendingDestroy)
        {
            if (APIManager.Instance == null || PersistenceManager.Instance == null) return;

            string roomDbId = APIManager.Instance.CurrentRoomDbId;
            if (!string.IsNullOrEmpty(InstanceId) && !string.IsNullOrEmpty(roomDbId))
            {
                PersistenceManager.Instance.UpdateObjectTransform(roomDbId, this.InstanceId, transform.position, transform.rotation);
            }
        }
    }

    /// <summary>
    /// Initiates the delete process. 
    /// 1. Checks ownership. 
    /// 2. Calls DB delete. 
    /// 3. If DB success, destroys Photon object.
    /// </summary>
    /// <param name="onComplete">Callback with success (true) or failure (false)</param>
    public void RequestDestroy(System.Action<bool> onComplete = null)
    {
        if (_isPendingDestroy)
        {
            Debug.LogWarning("Deletion already in progress.");
            return;
        }

        if (_photonView.IsMine)
        {
            _isPendingDestroy = true;

            // 1. Check Dependencies
            if (APIManager.Instance == null || PersistenceManager.Instance == null)
            {
                Debug.LogError("[NetworkedInteractable] Missing APIManager or PersistenceManager.");
                _isPendingDestroy = false;
                onComplete?.Invoke(false);
                return;
            }

            string roomDbId = APIManager.Instance.CurrentRoomDbId;

            // 2. Perform DB Delete
            if (!string.IsNullOrEmpty(InstanceId) && !string.IsNullOrEmpty(roomDbId))
            {
                PersistenceManager.Instance.DeleteObject(roomDbId, this.InstanceId, (success) =>
                {
                    if (success)
                    {
                        // 3a. Success: Destroy network object
                        PhotonNetwork.Destroy(gameObject);
                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        // 3b. Failure: Abort and notify
                        Debug.LogError("[NetworkedInteractable] DB Delete Failed. Object remains.");
                        _isPendingDestroy = false; // Allow retry
                        onComplete?.Invoke(false);
                    }
                });
            }
            else
            {
                // Fallback for objects that were never saved to DB (e.g. freshly placed but bugged)
                Debug.LogWarning("[NetworkedInteractable] Deleting object without DB ID.");
                PhotonNetwork.Destroy(gameObject);
                onComplete?.Invoke(true);
            }
        }
        else
        {
            // Not Owner
            onComplete?.Invoke(false);
        }
    }

    public bool IsOwnedByLocalPlayer()
    {
        return _photonView.IsMine;
    }
}
// --- END OF FILE NetworkedInteractable.cs ---