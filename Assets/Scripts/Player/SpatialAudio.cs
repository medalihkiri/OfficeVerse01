// --- START OF FILE SpatialAudio.cs ---

using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using agora_gaming_rtc;
using Photon.Realtime;

public class SpatialAudio : MonoBehaviourPunCallbacks
{
    // --- Constants ---
    public const string AUDIO_RADIUS_PROP = "AudioRadius";

    // --- Serialized Fields ---
    [Header("Settings")]
    [Tooltip("Initial radius.")]
    [SerializeField] private float radius = 5f;

    [Tooltip("How fast the radius changes when scrolling.")]
    public float radiusChangeSpeed = 5f;

    [Tooltip("The minimum allowed audio radius.")]
    public float minRadius = 2f;

    [Tooltip("The maximum allowed audio radius.")]
    public float maxRadius = 25f;

    [Header("Visuals")]
    [Tooltip("The UI GameObject for the circle visualizer. Assign this in the Inspector.")]
    public GameObject radiusVisualizer;

    [Tooltip("Calibration factor. Set to 1 if your sprite is exactly 1 unit wide. Adjust if sprite is smaller/larger.")]
    public float visualizerScaleMultiplier = 1f;

    // --- Private Variables ---
    private PhotonView PV;
    private IRtcEngine agoraRtcEngine;
    private PlayerController myPlayerController;
    private Transform _myTransform;

    // State
    private bool isInRoom = false;
    private int currentRoomId = 0;

    // Caching for Network Optimization
    private float lastSyncedRadius = -1f;
    private float lastScrollTime = 0f;
    private const float SYNC_DELAY = 0.1f; // Faster sync response

    void Awake()
    {
        PV = GetComponent<PhotonView>();
        _myTransform = transform;
        myPlayerController = GetComponent<PlayerController>();

        if (radiusVisualizer == null)
        {
            var viz = transform.Find("RadiusVisualizer");
            if (viz != null) radiusVisualizer = viz.gameObject;
        }
    }

    void Start()
    {
        if (PV.IsMine)
        {
            SyncRadius(true);
        }
    }

    void Update()
    {
        // 1. Input Handling (Local Only)
        if (PV.IsMine)
        {
            HandleRadiusInput();
            UpdateSpatialVolumes();
        }

        // 2. Visualization (All Clients)
        HandleVisualizer();
    }

    private void HandleRadiusInput()
    {
        // Only allow changing radius if mic is actually enabled (optional UX choice, keeps it clean)
        // or always allow it. Let's always allow it so user can prep their radius.
        if (Input.GetKey(KeyCode.X))
        {
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                radius += scrollInput * radiusChangeSpeed;
                radius = Mathf.Clamp(radius, minRadius, maxRadius);
                lastScrollTime = Time.time;
            }
        }

        // Sync radius over network if changed
        if (Mathf.Abs(radius - lastSyncedRadius) > 0.01f && (Time.time - lastScrollTime > SYNC_DELAY))
        {
            SyncRadius();
        }
    }

    private void SyncRadius(bool force = false)
    {
        if (!PV.IsMine) return;

        lastSyncedRadius = radius;

        var props = new ExitGames.Client.Photon.Hashtable();
        props[AUDIO_RADIUS_PROP] = radius;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private float GetRemoteRadius(Player player)
    {
        if (player.CustomProperties.TryGetValue(AUDIO_RADIUS_PROP, out object radiusObj))
        {
            return System.Convert.ToSingle(radiusObj);
        }
        return radius; // Fallback
    }

    private void HandleVisualizer()
    {
        if (radiusVisualizer == null) return;

        // If in a Room (SpatialRoom), radius logic is overridden by Room Logic (Infinite/Global).
        // Hiding the radius circle avoids confusion since distance limits don't apply inside the room.
        if (isInRoom)
        {
            if (radiusVisualizer.activeSelf) radiusVisualizer.SetActive(false);
            return;
        }

        bool isMicOn = false;
        float currentDisplayRadius = radius;
        bool isMine = PV.IsMine;

        // --- Step A: Determine State ---
        if (isMine)
        {
            bool isAdjusting = Input.GetKey(KeyCode.X);
            isMicOn = myPlayerController != null && myPlayerController.playerAudioObject.activeSelf;
            currentDisplayRadius = radius;

            // Show if Mic is ON or if user is actively adjusting settings
            radiusVisualizer.SetActive(isMicOn || isAdjusting);
        }
        else
        {
            if (PV.Owner.CustomProperties.TryGetValue(PlayerController.IS_AUDIO_ENABLED_PROP, out object isAudioEnabledObj))
            {
                isMicOn = (bool)isAudioEnabledObj;
            }
            currentDisplayRadius = GetRemoteRadius(PV.Owner);

            // For remote players, only show if they are actually talking (Mic On)
            radiusVisualizer.SetActive(isMicOn);
        }

        // --- Step B: Precise Scaling ---
        if (radiusVisualizer.activeSelf)
        {
            // 1. Calculate Target World Diameter
            float worldDiameter = currentDisplayRadius * 2f;

            // 2. Compensate for Parent Scale
            // If Parent is scaled (0.5), child needs scale (20) to be world size (10).
            // Formula: RequiredLocalScale = TargetWorldSize / ParentWorldScale
            float parentScale = transform.lossyScale.x;
            if (Mathf.Abs(parentScale) < 0.001f) parentScale = 1f; // Prevent div by zero

            float compensatedDiameter = (worldDiameter / parentScale) * visualizerScaleMultiplier;

            // 3. Animation (Breathing)
            if (isMicOn)
            {
                float pulse = 1f + (Mathf.Sin(Time.time * 4f) * 0.03f); // Subtle 3% pulse
                compensatedDiameter *= pulse;
            }

            // 4. Color/Opacity Logic (Optional - helps visualize interaction)
            // If I am inside this remote player's radius, maybe tint it?
            // For now, keeping it simple as requested: just correct sizing.

            radiusVisualizer.transform.localScale = new Vector3(compensatedDiameter, compensatedDiameter, 1f);
        }
    }

    private int GetVolumeForDistance(float distance, float emitterRadius)
    {
        if (distance <= 0) return 100;
        if (distance >= emitterRadius) return 0;
        return (int)(100 * (1 - distance / emitterRadius));
    }

    private void UpdateSpatialVolumes()
    {
        if (!PV.IsMine) return;

        if (agoraRtcEngine == null)
            agoraRtcEngine = GameManager.Instance.agoraClientManager.mRtcEngine;

        if (agoraRtcEngine == null) return;

        bool localPlayerIsInRoom = isInRoom;
        int localPlayerRoomId = currentRoomId;

        foreach (Player otherPlayer in PhotonNetwork.PlayerListOthers)
        {
            if (!otherPlayer.CustomProperties.TryGetValue("agoraID", out object agoraIDObj)) continue;
            uint agoraID = uint.Parse(agoraIDObj.ToString());

            bool shouldBeAudible = false;
            int finalVolume = 0;

            bool isRemoteMicIntentionallyOn = otherPlayer.CustomProperties.TryGetValue(PlayerController.IS_AUDIO_ENABLED_PROP, out object isMicOnObj) && (bool)isMicOnObj;
            bool isRemoteBroadcasting = otherPlayer.CustomProperties.TryGetValue(PlayerController.IS_BROADCASTING_PROP, out object isBroadcastingObj) && (bool)isBroadcastingObj;

            if (isRemoteBroadcasting)
            {
                if (isRemoteMicIntentionallyOn)
                {
                    shouldBeAudible = true;
                    finalVolume = 100;
                }
            }
            else if (isRemoteMicIntentionallyOn)
            {
                int otherPlayerRoomId = 0;
                if (otherPlayer.CustomProperties.TryGetValue("inRoom", out object otherRoomIdObj))
                {
                    otherPlayerRoomId = (int)otherRoomIdObj;
                }
                bool otherPlayerIsInRoom = otherPlayerRoomId != 0;

                if (localPlayerIsInRoom && otherPlayerIsInRoom)
                {
                    if (localPlayerRoomId == otherPlayerRoomId)
                    {
                        shouldBeAudible = true;
                        finalVolume = 100;
                    }
                }
                else if (!localPlayerIsInRoom && !otherPlayerIsInRoom)
                {
                    PlayerController otherPlayerController = GameManager.Instance.GetPlayerByActorNumber(otherPlayer.ActorNumber);
                    if (otherPlayerController != null)
                    {
                        float distance = Vector3.Distance(transform.position, otherPlayerController.transform.position);

                        // CRITICAL: Use the Remote Player's Broadcasted Radius
                        float emitterRadius = GetRemoteRadius(otherPlayer);

                        finalVolume = GetVolumeForDistance(distance, emitterRadius);

                        if (finalVolume > 0)
                        {
                            shouldBeAudible = true;
                        }
                    }
                }
            }

            agoraRtcEngine.AdjustUserPlaybackSignalVolume(agoraID, finalVolume);
            agoraRtcEngine.MuteRemoteAudioStream(agoraID, !shouldBeAudible);
        }
    }

    public void ForceUpdateSpatialVolumes()
    {
        if (!PV.IsMine) return;
        UpdateSpatialVolumes();
    }

    public void SetInRoom(bool inRoom, int roomId)
    {
        isInRoom = inRoom;
        currentRoomId = roomId;
    }

    public float Radius => radius;
}
// --- END OF FILE SpatialAudio.cs ---