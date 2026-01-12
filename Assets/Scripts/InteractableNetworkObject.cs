using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Attach this component to the prefab that will be instantiated with PhotonNetwork.Instantiate.
/// Responsibilities:
/// - Provide PhotonView reference (ensure prefab includes PhotonView)
/// - Implement IPunObservable to sync transform when not owned
/// - RPCs for begin/end hold, initialize, and retention of placed state
/// - Exposes customSaveData for backend metadata
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class InteractableNetworkObject : MonoBehaviourPun, IPunObservable
{
    [Header("Meta")]
    [Tooltip("Name used when saving to backend. Set by manager on instantiate.")]
    public string prefabName = "UnnamedPrefab";

    [Tooltip("Optional JSON or short string with metadata (eg. item id, durability, custom attributes)")]
    [TextArea] public string customSaveData = "";

    // Internal smoothing state for non-owned clients
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;

    // For physics objects you might want to disable physics while held. Support both RigidBody2D and 3D.
    private Rigidbody rb3d;
    private Rigidbody2D rb2d;
    private Collider2D col;

    private void Awake()
    {
        rb3d = GetComponent<Rigidbody>();
        rb2d = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>() ;
        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    private void Start()
    {
        // If newly spawned and owner exists, assign lastSent* to avoid huge first update
        if (photonView.IsMine)
        {
            lastSentPosition = transform.position;
            lastSentRotation = transform.rotation;
        }
    }

    private void Update()
    {
        // If not owned, smoothly converge to network state
        if (!photonView.IsMine)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, 10f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, 10f * Time.deltaTime);
        }
    }

    #region Ownership / Hold RPCs

    [PunRPC]
    public void RPC_InitializeOwner(int ownerActorNumber)
    {
        // Can attach any owner-specific initialization here
        // Example: set tag, layer or special visuals depending on owner
    }

    /// <summary>
    /// Called when owner begins holding the object.
    /// We set physics to kinematic on all clients and record owner ID.
    /// </summary>
    [PunRPC]
    public void RPC_BeginHold(int holderActorNumber)
    {
        // turn off rigidbody physics while held (so movement is kinematic-follow)
        if (rb3d != null) { rb3d.isKinematic = true; rb3d.velocity = Vector3.zero; }
        if (rb2d != null) { rb2d.velocity = Vector2.zero; rb2d.bodyType = RigidbodyType2D.Kinematic; }
        if (col != null) col.enabled = false;

        // Optionally mark owner in custom properties or internal state for UI
        // (We don't touch PhotonPlayer props here to avoid flooding)
    }

    /// <summary>
    /// Called when owner ends hold and places the object.
    /// </summary>
    [PunRPC]
    public void RPC_EndHoldAndPlace(int placerActorNumber)
    {
        // re-enable physics if available
        if (rb3d != null) { rb3d.isKinematic = false; }
        if (rb2d != null) { rb2d.bodyType = RigidbodyType2D.Dynamic; }
        if (col != null) col.enabled = true;

        // Optionally set a persistent owner property (left as a place to hook your own system)
        // We could set a Photon Custom Room property or keep a server-side record (your call).
    }

    #endregion

    #region Public helpers for owner to call

    /// <summary>
    /// Called by the owner every frame (or when transform changed) to mark if the transform changed enough
    /// such that we should send network updates. This reduces bandwidth.
    /// </summary>
    public void MarkTransformDirtyIfNeeded(float posThreshold, float rotThreshold)
    {
        if (!photonView.IsMine) return;

        // Position change
        float posDelta = Vector3.Distance(transform.position, lastSentPosition);
        float rotDelta = Quaternion.Angle(transform.rotation, lastSentRotation);

        if (posDelta > posThreshold || rotDelta > rotThreshold)
        {
            // We want to propagate state immediately
            photonView.RPC(nameof(RPC_SendTransformState), RpcTarget.Others, transform.position, transform.rotation.eulerAngles);
            lastSentPosition = transform.position;
            lastSentRotation = transform.rotation;
        }
    }

    [PunRPC]
    private void RPC_SendTransformState(Vector3 pos, Vector3 eulerRot)
    {
        // Received an owner update: apply on remote clients
        networkPosition = pos;
        networkRotation = Quaternion.Euler(eulerRot);
    }

    #endregion

    #region IPunObservable - fallback continuous sync when needed

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Lightweight transform sync as fallback (owner writes, non-owner reads)
        if (stream.IsWriting)
        {
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            stream.SendNext(pos);
            stream.SendNext(rot.eulerAngles);
        }
        else
        {
            Vector3 pos = (Vector3)stream.ReceiveNext();
            Vector3 euler = (Vector3)stream.ReceiveNext();
            networkPosition = pos;
            networkRotation = Quaternion.Euler(euler);
        }
    }

    #endregion
}