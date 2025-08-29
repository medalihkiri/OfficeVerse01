using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using agora_gaming_rtc;
using Photon.Realtime;

public class SpatialAudio : MonoBehaviourPunCallbacks
{
    // --- Existing Variables ---
    [SerializeField] private float radius;
    private PhotonView PV;
    private IRtcEngine agoraRtcEngine;
    private static readonly Dictionary<Player, SpatialAudio> spatialAudioFromPlayers = new Dictionary<Player, SpatialAudio>();
    private bool isInRoom = false;
    private int currentRoomId = 0;

    // -------------------- MODIFICATION START --------------------

    [Header("Radius Control & Visualization")]

    /// <summary>
    /// The UI GameObject for the circle visualizer. Assign this in the Inspector.
    /// It should be a child of the player prefab.
    /// </summary>
    [Tooltip("The UI GameObject for the circle visualizer. Assign this in the Inspector.")]
    public GameObject radiusVisualizer;

    /// <summary>
    /// How fast the radius changes when scrolling.
    /// </summary>
    [Tooltip("How fast the radius changes when scrolling.")]
    public float radiusChangeSpeed = 5f;

    /// <summary>
    /// The minimum allowed audio radius.
    /// </summary>
    [Tooltip("The minimum allowed audio radius.")]
    public float minRadius = 2f;

    /// <summary>
    /// The maximum allowed audio radius.
    /// </summary>
    [Tooltip("The maximum allowed audio radius.")]
    public float maxRadius = 25f;

    // Private reference to the local player's controller to check mic status.
    private PlayerController myPlayerController;

    // -------------------- MODIFICATION END --------------------


    void Awake()
    {
        PV = GetComponent<PhotonView>();
        spatialAudioFromPlayers[PV.Owner] = this;

        // -------------------- MODIFICATION START --------------------
        // Get the PlayerController component on this same GameObject.
        myPlayerController = GetComponent<PlayerController>();

        // Ensure the visualizer is assigned in the Inspector, but only for the local player.
        if (PV.IsMine)
        {
            if (radiusVisualizer == null)
            {
                Debug.LogError("[SpatialAudio] The 'Radius Visualizer' GameObject has not been assigned in the Inspector!");
            }
        }
        // -------------------- MODIFICATION END --------------------
    }

    void OnDestroy()
    {
        spatialAudioFromPlayers.Remove(PV.Owner);
    }

    void Update()
    {
        if (!PV.IsMine) return; // Only run on the local player

        // -------------------- MODIFICATION START --------------------
        // Handle the input for changing the radius and manage the visualizer's state.
        HandleRadiusInput();
        HandleVisualizer();
        // -------------------- MODIFICATION END --------------------

        // --- This existing spatial audio logic remains unchanged ---
        if (agoraRtcEngine == null)
        {
            agoraRtcEngine = GameManager.Instance.agoraClientManager.mRtcEngine;
        }

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player == PV.Owner) continue; // Skip local player
            if (!player.CustomProperties.TryGetValue("agoraID", out object agoraIDObj)) continue;
            uint agoraID = uint.Parse(agoraIDObj.ToString());

            int volume;
            if (isInRoom)
            {
                if (SpatialRoom.PlayersInRooms.TryGetValue(player.ActorNumber, out int playerRoomId))
                {
                    volume = (playerRoomId == currentRoomId) ? 100 : 0;
                }
                else
                {
                    volume = 0;
                }
            }
            else
            {
                if (SpatialRoom.PlayersInRooms.ContainsKey(player.ActorNumber))
                {
                    volume = 0;
                }
                else
                {
                    if (!player.CustomProperties.TryGetValue("Name", out object NameObj)) continue;
                    string objName = NameObj.ToString();
                    GameObject otherPlayerObj = GameObject.Find(objName);

                    if (otherPlayerObj != null)
                    {
                        Vector3 otherPosition = otherPlayerObj.transform.position;
                        float distance = Vector3.Distance(transform.position, otherPosition);
                        volume = GetVolume(distance);
                    }
                    else
                    {
                        volume = 0;
                    }
                }
            }
            agoraRtcEngine.AdjustUserPlaybackSignalVolume(agoraID, volume);
        }
    }

    // -------------------- MODIFICATION START --------------------
    /// <summary>
    /// Checks for user input (holding 'A' and scrolling) to adjust the audio radius.
    /// </summary>
    private void HandleRadiusInput()
    {
        // Check if the 'A' key is being held down.
        if (Input.GetKey(KeyCode.X))
        {
            // Get the mouse scroll wheel input.
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");

            // If there is any scroll input, adjust the radius.
            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                // Modify the radius based on scroll direction and speed.
                radius += scrollInput * radiusChangeSpeed;

                // Clamp the radius within the defined min/max values.
                radius = Mathf.Clamp(radius, minRadius, maxRadius);
            }
        }
    }

    /// <summary>
    /// Manages the visibility and scale of the radius visualization circle.
    /// </summary>
    private void HandleVisualizer()
    {
        if (radiusVisualizer == null || myPlayerController == null) return;

        // Condition 1: Is the user currently holding the 'A' key?
        bool isChangingRadius = Input.GetKey(KeyCode.A);

        // Condition 2: Is the player's microphone UI object active?
        bool isMicOn = myPlayerController.playerAudioObject.activeSelf;

        // The visualizer should be active if EITHER condition is true.
        bool shouldBeVisible = isChangingRadius || isMicOn;

        // Set the active state of the visualizer object.
        if (radiusVisualizer.activeSelf != shouldBeVisible)
        {
            radiusVisualizer.SetActive(shouldBeVisible);
        }

        // If the visualizer is visible, update its scale to match the current radius.
        if (shouldBeVisible)
        {
            // We multiply by 2 because scale is based on diameter, while our radius is... a radius.
            float diameter = radius * 2f;
            radiusVisualizer.transform.localScale = new Vector3(diameter, diameter, 1f);
        }
    }
    // -------------------- MODIFICATION END --------------------

    private int GetVolume(float distance)
    {
        if (distance <= 0)
        {
            return 100;
        }
        else if (distance >= radius)
        {
            return 0;
        }
        else
        {
            return (int)(100 * (1 - distance / radius));
        }
    }

    public void SetInRoom(bool inRoom, int roomId)
    {
        isInRoom = inRoom;
        currentRoomId = roomId;
    }
}