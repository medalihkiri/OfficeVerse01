using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Photon.Pun;
using TMPro;
using agora_gaming_rtc;
using UnityEngine.U2D.Animation;
using ExitGames.Client.Photon.StructWrapping;
using Pathfinding;
using Photon.Realtime;
using System;

public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    // --- Public Variables ---
    public float moveSpeed = 5f;
    public LayerMask solidObjectsLayer;
    public LayerMask grassLayer;

    public TextMeshProUGUI nicknameText;
    public GameObject playerVideoObject;
    public VideoSurface playerVideoSurface;
    public GameObject playerScreenObject;
    public VideoSurface playerScreenSurface;
    public GameObject playerAudioObject;

    public SpriteLibrary spriteLibrary;
    public SpriteLibraryAsset spriteLibraryAsset;

    public SpriteResolver spriteResolver;

    public Animator animator;
    public Rigidbody2D rb;

    public GameObject pointerPrefab;

    public PhotonView view;

    public int currentRoomID;

    public uint agoraID;

    public bool isPlayerMovingUsingMouse = false;

    // --- Private Variables ---
    private Vector3 target;
    private GameObject pointerObject;
    private int playerAvatar = -1;
    private float moveHorizontal, moveVertical;
    private Vector2 movement;
    private uint localAgoraID;
    private TMP_InputField chatInputField;

    // --- Photon Animator View ---
    private PhotonAnimatorView photonAnimatorView;

    // --- Pathfinding ---
    private Seeker seeker;
    private Path path;
    private int currentWaypoint = 0;
    private bool isCalculatingPath = false;

    // --- Animation ---
    private bool isMoving = false;

    // --- Input ---
    private float lastClickTime = 0;
    private float doubleClickTimeThreshold = 0.25f;

    // --- Animation Hashes ---
    private int WalkLeft = Animator.StringToHash("WalkLeft");
    private int WalkRight = Animator.StringToHash("WalkRight");
    private int WalkFront = Animator.StringToHash("WalkFront");
    private int WalkBack = Animator.StringToHash("WalkBack");
    public bool isNearby;

    // --- Enums ---
    public enum Direction
    {
        Idle,
        Left,
        Right,
        Front,
        Back
    }
    public Direction lastDirection = Direction.Idle;
    Navigation navigation;
    private AvailabilityManager availabilityManager;
    private bool screenShareEnabled;

    public bool ScreenShareEnabled
    {
        get { return screenShareEnabled; }
        set { screenShareEnabled = value; }
    }

    public Image availabilityIndicator; // Add a UI element for the indicator (e.g., a colored circle above the player)
    [HideInInspector] public AvailabilityManager.AvailabilityStatus currentAvailability = AvailabilityManager.AvailabilityStatus.Available;

    // --- MonoBehaviour Methods ---
    private IEnumerator Start()
    {
        chatInputField = GameChatManager.SP.chatInputField;
        navigation = new Navigation();
        navigation.mode = Navigation.Mode.None;
        chatInputField.navigation = navigation;

        // Initialize video state to off
        if (view.IsMine)
        {
            SendVideoState(false);
        }
        else if (playerVideoObject != null && playerVideoSurface != null)
        {
            // For remote players, initialize based on their stored state
            if (view.Owner.CustomProperties.TryGetValue("isVideoEnabled", out object isEnabled))
            {
                OnReceiveVideoState((bool)isEnabled);
            }
            else
            {
                OnReceiveVideoState(false);
            }
        }

        // Get the player's selected avatar from Photon's custom properties.
        view.Owner.CustomProperties.TryGetValue("avatar", out playerAvatar);

        // Set the sprite library asset based on the selected avatar.
        spriteLibraryAsset = CharacterManager.Instance.CharacterSpriteLibraries[playerAvatar];
        spriteLibrary.spriteLibraryAsset = spriteLibraryAsset;


        // If this PlayerController belongs to another player in the room...
       /* if (!view.IsMine)
        {
            // Add this controller to the GameManager's list of other players.
            GameManager.Instance.otherPlayers.Add(this);
        }*/

        // Set the player's nickname based on whether it's the local player or a remote player.
        if (view.IsMine)
        {
            nicknameText.text = view.Owner.NickName;
            Debug.Log($"Self Player: {view.Owner.NickName}");

            // Instantiate the pointer object for the local player.
            pointerObject = Instantiate(pointerPrefab);
            pointerObject.SetActive(false);

            // Wait for a short delay before joining the Agora channel.
            yield return new WaitForSecondsRealtime(0f);

            // Subscribe to the OnJoinChannelSuccess event.
            IRtcEngine rtcEngine = GameManager.Instance.agoraClientManager.mRtcEngine;
            rtcEngine.OnJoinChannelSuccess += OnJoinChannelSuccessHandler;

            // Join the Agora channel.
            GameManager.Instance.JoinAgoraChannel();

            // Get the Seeker component for pathfinding.
            seeker = GetComponent<Seeker>();

            PhotonNetwork.LocalPlayer.NickName = PlayerDataManager.PlayerName;

            // Set the initial name
            SetPlayerName(PlayerDataManager.PlayerName);
        }
        else
        {
            nicknameText.text = view.Owner.NickName;
            Debug.Log($"Other Player {view.Owner.NickName}");
        }

        // Set the GameObject's name and update Photon's custom properties.
        gameObject.name = $"{view.Owner.NickName}_{playerAvatar}_Player";
        ExitGames.Client.Photon.Hashtable nameIDProp = new ExitGames.Client.Photon.Hashtable();
        nameIDProp.Add("Name", gameObject.name);
        view.Owner.SetCustomProperties(nameIDProp);

        // Add the player to the list of other players in the game.
        if (!view.IsMine)
        {
            if (GameManager.Instance != null && !GameManager.Instance.otherPlayers.Contains(this))
                GameManager.Instance.otherPlayers.Add(this);
        }
        // Initialize the Photon Animator View.
        /*
        if (*/
        photonAnimatorView = GetComponentsInChildren<PhotonAnimatorView>()[0];
    }

    // New method to set the availability status and visuals
    public void SetAvailabilityStatus(AvailabilityManager.AvailabilityStatus status)
    {
        currentAvailability = status;
        UpdateAvailabilityIndicator();
    }

    private void UpdateAvailabilityIndicator()
    {
        if (availabilityIndicator != null) // Make sure the indicator is assigned in the Inspector
        {
            Color statusColor = Participants.Instance.GetStatusColor(currentAvailability);
            availabilityIndicator.color = statusColor;
        }
    }
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (changedProps.ContainsKey("AvailabilityStatus") && !photonView.IsMine)
        {
            int statusValue = (int)changedProps["AvailabilityStatus"];
            AvailabilityManager.AvailabilityStatus status = (AvailabilityManager.AvailabilityStatus)statusValue;
            UpdateAvailabilityVisuals(status);
        }
    }

    private void UpdateAvailabilityVisuals(AvailabilityManager.AvailabilityStatus status)
    {
        Color statusColor;
        string statusString;

        switch (status)
        {
            case AvailabilityManager.AvailabilityStatus.Available:
                statusColor = Color.green;
                statusString = "Available";
                break;
            case AvailabilityManager.AvailabilityStatus.Busy:
                statusColor = Color.yellow;
                statusString = "Busy";
                break;
            case AvailabilityManager.AvailabilityStatus.DoNotDisturb:
                statusColor = Color.red;
                statusString = "Do Not Disturb";
                break;
            default:
                statusColor = Color.green;
                statusString = "Available";
                break;
        }
        // Update the player's visual representation (e.g., a status indicator above the player's head)
        // You'll need to implement this part based on your game's specific visuals

        // Update the player's profile if it's open
        PlayerProfile profile = GetComponent<PlayerProfile>();
        if (profile != null)
        {
            profile.UpdateAvailabilityStatus(statusColor, statusString);
        }
    }
    public void SetPlayerName(string newName)
    {
        if (view.IsMine)
        {
            photonView.RPC("UpdatePlayerName", RpcTarget.AllBuffered, newName);
        }
    }

    [PunRPC]
    public void UpdatePlayerName(string newName)
    {
        if (view.IsMine)
        {
            photonView.RPC("UpdatePlayerNameRPC", RpcTarget.AllBuffered, newName, view.OwnerActorNr);
        }
    }

    [PunRPC]
    private void UpdatePlayerNameRPC(string newName, int actorNumber)
    {
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
        if (player != null && player == view.Owner)
        {
            nicknameText.text = newName;
            gameObject.name = $"{newName}_{playerAvatar}_Player";

            if (view.IsMine)
            {
                PhotonNetwork.NickName = newName;
                PlayerDataManager.PlayerName = newName;
            }

            // Update custom properties
            ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
            {
                { "Name", gameObject.name }
            };
            view.Owner.SetCustomProperties(properties);

            // Update participants list
            FindObjectOfType<Participants>().UpdateParticipantList();
        }
    }

    private bool IsAnyInputFieldFocused()
    {
        // Check if any input field is focused
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            TMP_InputField inputField = EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>();
            if (inputField != null && inputField.isFocused)
            {
                return true;
            }
        }
        return false;
    }

    private void Update()
    {
        // Handle input and animation for the local player.
        if (view.IsMine)
        {
            // Check if an input field is selected. If so, return early.
            // Check if the chat input field is focused. If so, stop any ongoing movement and return early.
            if (IsAnyInputFieldFocused())
            {
                StopMovingAlongPath();
                rb.velocity = Vector2.zero;
                return;
            }

            // Check if there is keyboard input and if the player is currently moving using the mouse.
            // If so, stop the pathfinding.
            bool hasKeyboardInput = Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f;
            if (isPlayerMovingUsingMouse && hasKeyboardInput && path != null)
            {
                StopMovingAlongPath();
            }

            HandleLocalPlayerInput();
            ControlLocalPlayerAnimation();
        }
        else
        {
            // Handle movement and animation for remote players.
            HandleRemotePlayerMovement();
            ControlRemotePlayerAnimation();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.otherPlayers.Remove(this);
    }


    // --- IPunObservable Implementation ---
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Synchronize the player's movement across the network.
        if (stream.IsWriting)
        {
            stream.SendNext(moveHorizontal);
            stream.SendNext(moveVertical);
            //stream.SendNext(isMoving);
        }
        else
        {
            moveHorizontal = (float)stream.ReceiveNext();
            moveVertical = (float)stream.ReceiveNext();
            //isMoving = (bool)stream.ReceiveNext();
        }
    }

    // --- Private Methods ---
    private void OnJoinChannelSuccessHandler(string channelName, uint uid, int elapsed)
    {
        // Set the local Agora ID and update Photon's custom properties.
        localAgoraID = uid;
        ExitGames.Client.Photon.Hashtable agoraIDProp = new ExitGames.Client.Photon.Hashtable();
        agoraIDProp.Add("agoraID", localAgoraID.ToString());
        view.Owner.SetCustomProperties(agoraIDProp);
    }

    private void HandleLocalPlayerInput()
    {
        // Get the horizontal and vertical input axes.
        moveHorizontal = Input.GetAxis("Horizontal");
        moveVertical = Input.GetAxis("Vertical");

        // If there is keyboard input and the player is currently moving using the mouse, stop the pathfinding.
        if (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f)
        {
            if (isPlayerMovingUsingMouse)
            {
                StopMovingAlongPath();
            }
        }

        // Apply velocity for keyboard movement if the player is not moving using the mouse and a path is not being calculated.
        if (!isPlayerMovingUsingMouse && !isCalculatingPath)
        {
            animator.enabled = true;
            movement = new Vector2(moveHorizontal, moveVertical);
            rb.velocity = movement * moveSpeed;
        }

        // Handle mouse click input for movement.
        if (Input.GetMouseButtonDown(0))
        {
            // Check if the click is over a UI element
            if (EventSystem.current.IsPointerOverGameObject())
            {
                // Click is over a UI element, don't start pathfinding
                return;
            }

            // Perform a raycast to check if the click is on a collider.
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero, Mathf.Infinity);

            // Check if we're clicking on the screen share area
            var screenShare = FindObjectOfType<ScreenShareClickHandler>();
            if (screenShare != null && screenShare.IsMouseOverScreenShare())
            {
                // Don't start pathfinding if clicking on screen share
                return;
            }

            // If the click is not on a collider or the collider is a trigger, handle it as a potential double click for movement.
            if (hit.collider == null || hit.collider.isTrigger)
            {
                float clickTime = Time.time;
                if (clickTime - lastClickTime < doubleClickTimeThreshold)
                {
                    // Double click detected.
                    isPlayerMovingUsingMouse = true;
                    target = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    target.z = transform.position.z;

                    // Calculate a new path if one is not already being calculated.
                    if (!isCalculatingPath)
                    {
                        isCalculatingPath = true;
                        FindPathToTarget();
                    }
                }
                lastClickTime = clickTime;
            }
            else
            {
                Debug.Log("Click is on a non-trigger collider, ignoring.");
                GetComponent<PlayerInfo>().Show();
            }
        }

        // Move the player along the calculated path if mouse movement is enabled.
        if (isPlayerMovingUsingMouse)
        {
            animator.enabled = true;
            MoveAlongPath();

            // Stop moving if the player has reached the target destination.
            if (Vector3.Distance(transform.position, target) < 0.2f)
            {
                StopMovingAlongPath();
            }
        }
    }

    private void HandleRemotePlayerMovement()
    {
        // Apply velocity for remote players based on the received movement data.
        movement = new Vector2(moveHorizontal, moveVertical);
        rb.velocity = movement * moveSpeed;
    }

    private void ControlLocalPlayerAnimation()
    {
        // Set the isMoving flag based on the player's velocity or pathfinding status.
        isMoving = (movement != Vector2.zero && !isPlayerMovingUsingMouse) || (isPlayerMovingUsingMouse && path != null);

        // Control the player's animation based on its movement.
        ControlAnimation(moveHorizontal, moveVertical, isMoving);
    }

    private void ControlRemotePlayerAnimation()
    {
        // Set the isMoving flag for remote players based on the received movement data.
        isMoving = movement != Vector2.zero;

        // Control the remote player's animation based on its movement.
        ControlAnimation(moveHorizontal, moveVertical, isMoving);
    }

    private void FindPathToTarget()
    {
        // Use the A* pathfinding algorithm to find a path to the target destination.
        seeker.StartPath(transform.position, target, OnPathComplete);
    }

    private void OnPathComplete(Path p)
    {
        // Reset the isCalculatingPath flag.
        isCalculatingPath = false;

        // If a valid path was found, update the pathfinding variables and activate the pointer object.
        if (!p.error)
        {
            path = p;
            currentWaypoint = 0;
            pointerObject.transform.position = target;
            pointerObject.SetActive(true);
        }
        else
        {
            Debug.LogError("Pathfinding error: " + p.errorLog);
            StopMovingAlongPath();
        }
    }



    private void StopMovingAlongPath()
    {
        // Stop the player's movement and reset the pathfinding variables.
        isPlayerMovingUsingMouse = false;
        path = null;
        currentWaypoint = 0;
        Debug.Log(pointerObject);
        if (pointerObject.active) pointerObject.SetActive(false);

        // Reset the movement and animation variables.
        moveHorizontal = 0;
        moveVertical = 0;
        isMoving = false;

        // Reset the animation to the idle state.
        ControlAnimation(0, 0, false);
        //Debug.Log("Stop!");
    }

    private void MoveAlongPath()
    {
        // If there is no path, return early.
        if (path == null) return;

        // If the player has reached the end of the path, stop moving.
        if (currentWaypoint >= path.vectorPath.Count)
        {
            StopMovingAlongPath();
            return;
        }

        // Calculate the direction to the next waypoint and move the player towards it.
        Vector3 direction = (path.vectorPath[currentWaypoint] - transform.position).normalized;
        transform.position = Vector3.MoveTowards(transform.position, path.vectorPath[currentWaypoint], moveSpeed * Time.deltaTime);

        // If the player is close enough to the current waypoint, move to the next one.
        if (Vector3.Distance(transform.position, path.vectorPath[currentWaypoint]) < 0.17f)
        {
            currentWaypoint++;
        }

        if (currentWaypoint == 1)
        {
            moveHorizontal = -direction.x;
            moveVertical = -direction.y;
            return;
        }
        // Update the movement and animation variables based on the current direction.
        moveHorizontal = direction.x;
        moveVertical = direction.y;

    }

    [System.Obsolete]
    public void ControlAnimation(float x, float y, bool isMoving)
    {
        // If the player is moving, determine the current direction and animation state.
        if (isMoving)
        {
            FindObjectOfType<CameraDrag>().endDrag = false;
            Direction currentDirection = GetDirection(x, y);
            Debug.Log("Direction: " + currentDirection.ToString() + " " + "x: " + x + " " + "y: " + y);
            int targetAnimationState = GetAnimationState(currentDirection);

            if (currentDirection != lastDirection ||
                                     animator.GetCurrentAnimatorStateInfo(0).IsName("IdleFront") || animator.GetCurrentAnimatorStateInfo(0).IsName("IdleBack") || animator.GetCurrentAnimatorStateInfo(0).IsName("IdleLeft") || animator.GetCurrentAnimatorStateInfo(0).IsName("IdleRight"))
            {
                animator.CrossFade(targetAnimationState, 0.0f, 0);
            }


            lastDirection = currentDirection;
        }
        else
        {
            // Player is not moving. Set the idle animation based on the previous direction.
            switch (lastDirection)
            {
                case Direction.Idle:
                    animator.CrossFade(IdleFront, 0, 0);
                    break;
                case Direction.Left:
                    animator.CrossFade(IdleLeft, 0, 0);
                    break;
                case Direction.Right:
                    animator.CrossFade(IdleRight, 0, 0);
                    break;
                case Direction.Front:
                    animator.CrossFade(IdleFront, 0, 0);
                    break;
                case Direction.Back:
                    animator.CrossFade(IdleBack, 0, 0);
                    break;
            }
        }
    }

    // Define the idle animations for different directions.
    private int IdleFront = Animator.StringToHash("IdleFront");
    private int IdleBack = Animator.StringToHash("IdleBack");
    private int IdleLeft = Animator.StringToHash("IdleLeft");
    private int IdleRight = Animator.StringToHash("IdleRight");

    private int GetAnimationState(Direction direction)
    {
        // Return the appropriate animation state based on the given direction.
        switch (direction)
        {
            case Direction.Idle:
                return IdleFront;
            case Direction.Left:
                return WalkLeft;
            case Direction.Right:
                return WalkRight;
            case Direction.Front:
                return WalkFront;
            case Direction.Back:
                return WalkBack;
            default:
                return IdleFront;
        }
    }

    private void ApplyAnimationState(Direction direction)
    {
        // Apply the animation state based on the given direction.
        switch (direction)
        {
            case Direction.Idle:
                animator.CrossFade(IdleFront, 0, 0);
                break;
            case Direction.Left:
                animator.CrossFade(WalkLeft, 0, 0);
                break;
            case Direction.Right:
                animator.CrossFade(WalkRight, 0, 0);
                break;
            case Direction.Front:
                animator.CrossFade(WalkFront, 0, 0);
                break;
            case Direction.Back:
                animator.CrossFade(WalkBack, 0, 0);
                break;
        }
    }

    private Direction GetDirection(float x, float y)
    {
        // Determine the movement direction based on the given x and y values.
        if (x == 0 && y == 0)
        {
            return Direction.Idle;
        }
        else if (Mathf.Abs(y) > Mathf.Abs(x))
        {
            return y < 0 ? Direction.Front : Direction.Back;
        }
        else
        {
            return x < 0 ? Direction.Left : Direction.Right;
        }
    }

    // --- Public Methods ---
    public void SendVideoState(bool enable)
    {
        // If enabling video while screen sharing, disable screen share first
        if (enable && ScreenShareEnabled)
        {
            GameManager.Instance.agoraClientManager.SetScreenShareState(false);
        }

        // Store video state in custom properties
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { "isVideoEnabled", enable }
        };
        view.Owner.SetCustomProperties(props);
        
        // Send an RPC to all clients to update the player's video state.
        view.RPC(nameof(OnReceiveVideoState), RpcTarget.All, enable);
    }

    public void SyncVideoState()
    {
        if (view.Owner.CustomProperties.TryGetValue("isVideoEnabled", out object isEnabled))
        {
            bool videoEnabled = (bool)isEnabled;
            OnReceiveVideoState(videoEnabled);
        }
        else
        {
            OnReceiveVideoState(false);
        }

        // Also sync screen state
        if (view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isScreenSharing))
        {
            bool screenEnabled = (bool)isScreenSharing;
            playerScreenObject.SetActive(screenEnabled);
            if (screenEnabled)
            {
                playerScreenSurface.SetForUser((uint)view.ViewID);
                playerScreenSurface.SetEnable(true);
            }
        }
    }

    public void SyncScreenShareState()
    {
        if (view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isSharing))
        {
            bool screenShareEnabled = (bool)isSharing;
            
            // First update the local screen share state
            OnReceiveScreenShareState(screenShareEnabled);
            
            if (screenShareEnabled)
            {
                // Force update visibility based on current room status
                bool shouldShow = GameManager.Instance.ShouldShowScreenShare(this);
                
                // Configure screen share surface first
                playerScreenObject.SetActive(shouldShow || view.IsMine);
                playerScreenSurface.SetForUser((uint)view.ViewID);
                playerScreenSurface.SetEnable(shouldShow || view.IsMine);
                playerScreenSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
                
                // Then update game manager state
                GameManager.Instance.SetScreenShareObjectState(
                    (uint)view.ViewID, 
                    view.Owner.NickName, 
                    shouldShow || view.IsMine
                );
            }
            else
            {
                playerScreenObject.SetActive(false);
                playerScreenSurface.SetEnable(false);
                GameManager.Instance.SetScreenShareObjectState(
                    (uint)view.ViewID,
                    view.Owner.NickName,
                    false
                );
            }
        }
        else
        {
            OnReceiveScreenShareState(false);
        }
    }

    [PunRPC]
    public void OnReceiveVideoState(bool enable)
    {
        bool shouldShow = GameManager.Instance.ShouldShowScreenShare(this);
        
        // Update the player's video object and surface based on the received video state.
        playerVideoObject.SetActive(enable && (view.IsMine || shouldShow));
        playerVideoSurface.SetForUser(view.ViewID == GameManager.Instance.myPlayer.view.ViewID ? 0 : (uint)view.ViewID);
        playerVideoSurface.SetEnable(enable && (view.IsMine || shouldShow));

        // If video is enabled, ensure screen share is disabled
        if (enable && playerScreenObject.activeSelf)
        {
            playerScreenObject.SetActive(false);
            playerScreenSurface.SetEnable(false);
        }
    }

    [PunRPC]
    public void DestroyPlayerRPC()
    {
        // This code will now execute on EVERY client, including the master.
        // It's a direct command to destroy this specific GameObject.
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    public void SendAudioState(bool enable)
    {
        if (!view.IsMine) return;

        // Store audio state in custom properties
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { "isAudioEnabled", enable }
        };
        view.Owner.SetCustomProperties(props);
        
        // Update local Agora audio state
        GameManager.Instance.agoraClientManager.mRtcEngine.EnableLocalAudio(enable);
        GameManager.Instance.agoraClientManager.mRtcEngine.MuteLocalAudioStream(!enable);
        
        // Send an RPC to all clients to update the player's audio state
        view.RPC(nameof(OnReceiveAudioState), RpcTarget.All, enable);
    }

    public void SyncAudioState(bool forceUIUpdate = false)
    {
        if (view.Owner.CustomProperties.TryGetValue("isAudioEnabled", out object isEnabled))
        {
            bool audioEnabled = (bool)isEnabled;
            OnReceiveAudioState(audioEnabled);
            
            // Force UI update if requested (for new players joining)
            if (forceUIUpdate)
            {
                playerAudioObject.SetActive(audioEnabled);
            }
        }
        else
        {
            // Fallback to disabled state if no property exists
            OnReceiveAudioState(false);
            if (forceUIUpdate)
            {
                playerAudioObject.SetActive(false);
            }
        }
    }

    [PunRPC]
    public void OnReceiveAudioState(bool enable)
    {
        // Update the player's audio object based on the received audio state.
        playerAudioObject.SetActive(enable);
    }

    public void SendScreenShareState(bool enable)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { "isScreenSharing", enable }
        };
        view.Owner.SetCustomProperties(props);

        view.RPC(nameof(OnReceiveScreenShareState), RpcTarget.All, enable);
    }

    [PunRPC]
    public void OnReceiveScreenShareState(bool enable)
    {
        bool shouldShow = GameManager.Instance.ShouldShowScreenShare(this);

        // Update the player's video object and surface based on the received video state.
        playerScreenObject.SetActive(enable && (view.IsMine || shouldShow));
        playerScreenSurface.SetForUser(view.ViewID == GameManager.Instance.myPlayer.view.ViewID ? 0 : (uint)view.ViewID);
        playerScreenSurface.SetEnable(enable && (view.IsMine || shouldShow));

        if (enable && (view.IsMine || shouldShow))
        {
            playerScreenSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
        }

        // Update the screen share object state in the game manager
        GameManager.Instance.SetScreenShareObjectState(
            (uint)view.ViewID,
            view.Owner.NickName,
            enable && (view.IsMine || shouldShow)
        );

        // Force immediate visibility update
        //if (enable)
        //{
        //    GameManager.Instance.UpdateScreenShareVisibility(this);
        //}
    }
}
