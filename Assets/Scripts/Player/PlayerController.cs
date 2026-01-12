// --- START OF FILE PlayerController.cs ---

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
using System.Collections.Generic;

public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    // --- MODIFICATION START: Photon Property Keys ---
    public const string IS_BROADCASTING_PROP = "isBroadcasting";
    public const string IS_AUDIO_ENABLED_PROP = "isAudioEnabled";
    public const string IS_VIDEO_ENABLED_PROP = "isVideoEnabled";
    public const string IS_SCREEN_SHARING_PROP = "isScreenSharing";
    // --- MODIFICATION END ---

    // --- Public Variables ---
    public float moveSpeed = 5f;
    public LayerMask solidObjectsLayer; // IMPORTANT: Assign this in Inspector (Walls/Obstacles)
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

    // --- FOLLOWER SYSTEM VARIABLES ---
    [Header("Follower System")]
    public float followingStoppingDistance = 1.2f; // Closer stopping distance
    public float directFollowSpeedMultiplier = 1.1f; // Slightly faster to catch up
    private Transform currentFollowTarget;
    private Coroutine followCoroutine;
    private bool isFollowing = false;
    // -------------------------------------

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

    private bool isOriginalOwner = false;


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

    public Image availabilityIndicator;
    [HideInInspector] public AvailabilityManager.AvailabilityStatus currentAvailability = AvailabilityManager.AvailabilityStatus.Available;

    private Camera _mainCamera;
    private ScreenShareClickHandler _cachedScreenShareHandler;
    private GameObject _lastSelectedObj;
    private bool _lastFocusResult;
    private readonly ExitGames.Client.Photon.Hashtable _propCache = new ExitGames.Client.Photon.Hashtable(1);

    private void Awake()
    {
        view = GetComponent<PhotonView>();
        // We set this flag only once. If we are the original creator, this will be true forever for this instance.
        isOriginalOwner = view.IsMine;
    }

    private IEnumerator Start()
    {
        _mainCamera = Camera.main;
        _cachedScreenShareHandler = FindObjectOfType<ScreenShareClickHandler>();

        if (GameChatManager.SP != null)
        {
            chatInputField = GameChatManager.SP.chatInputField;
            navigation = new Navigation { mode = Navigation.Mode.None };
            chatInputField.navigation = navigation;
        }

        if (view.IsMine)
        {
            SendVideoState(false);
        }
        else if (playerVideoObject != null && playerVideoSurface != null)
        {
            if (view.Owner.CustomProperties.TryGetValue(IS_VIDEO_ENABLED_PROP, out object isEnabled))
            {
                OnReceiveVideoState((bool)isEnabled);
            }
            else
            {
                OnReceiveVideoState(false);
            }
        }

        if (view.Owner.CustomProperties.TryGetValue("avatar", out object playerAvatarObj) && playerAvatarObj != null)
        {
            playerAvatar = (int)playerAvatarObj;
        }

        if (CharacterManager.Instance != null && CharacterManager.Instance.CharacterSpriteLibraries.Count > playerAvatar && playerAvatar >= 0)
        {
            spriteLibraryAsset = CharacterManager.Instance.CharacterSpriteLibraries[playerAvatar];
            spriteLibrary.spriteLibraryAsset = spriteLibraryAsset;
        }

        if (view.IsMine)
        {
            nicknameText.text = view.Owner.NickName;
            pointerObject = Instantiate(pointerPrefab);
            pointerObject.SetActive(false);

            yield return new WaitForSecondsRealtime(0f);

            if (GameManager.Instance?.agoraClientManager?.mRtcEngine != null)
            {
                GameManager.Instance.agoraClientManager.mRtcEngine.OnJoinChannelSuccess += OnJoinChannelSuccessHandler;
                GameManager.Instance.JoinAgoraChannel();
            }

            seeker = GetComponent<Seeker>();
            PhotonNetwork.LocalPlayer.NickName = PlayerDataManager.PlayerName;
            SetPlayerName(PlayerDataManager.PlayerName);
        }
        else
        {
            nicknameText.text = view.Owner.NickName;
        }

        gameObject.name = $"{view.Owner.NickName}_{playerAvatar}_Player";
        var nameIDProp = new ExitGames.Client.Photon.Hashtable { { "Name", gameObject.name } };
        view.Owner.SetCustomProperties(nameIDProp);

        if (!view.IsMine)
        {
            GameManager.Instance?.otherPlayers.Add(this);
        }

        photonAnimatorView = GetComponentInChildren<PhotonAnimatorView>();
    }

    public void SetBroadcastState(bool isBroadcasting)
    {
        if (!view.IsMine) return;

        var props = new ExitGames.Client.Photon.Hashtable { { IS_BROADCASTING_PROP, isBroadcasting } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        GetComponent<SpatialAudio>()?.ForceUpdateSpatialVolumes();

        if (view.Owner.CustomProperties.TryGetValue(IS_VIDEO_ENABLED_PROP, out object isVideoOn) && (bool)isVideoOn)
        {
            GameManager.Instance.UpdateVideoVisibility(this);
        }
        if (view.Owner.CustomProperties.TryGetValue(IS_SCREEN_SHARING_PROP, out object isScreenOn) && (bool)isScreenOn)
        {
            GameManager.Instance.UpdateScreenShareVisibility(this);
        }
    }
    // -------------------- MODIFICATION END --------------------


    public void SetAvailabilityStatus(AvailabilityManager.AvailabilityStatus status)
    {
        currentAvailability = status;
        UpdateAvailabilityIndicator();
    }

    private void UpdateAvailabilityIndicator()
    {
        if (availabilityIndicator != null && Participants.Instance != null)
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

            _propCache.Clear();
            _propCache.Add("Name", gameObject.name);
            view.Owner.SetCustomProperties(_propCache);

            Participants p = FindObjectOfType<Participants>();
            if (p != null) p.UpdateParticipantList();
        }
    }

    private bool IsAnyInputFieldFocused()
    {
        GameObject currentObj = EventSystem.current.currentSelectedGameObject;
        if (currentObj == null)
        {
            _lastSelectedObj = null;
            return false;
        }

        if (currentObj != _lastSelectedObj)
        {
            _lastSelectedObj = currentObj;
            TMP_InputField inputField = currentObj.GetComponent<TMP_InputField>();
            _lastFocusResult = (inputField != null);
        }

        return _lastFocusResult;
    }

    private void Update()
    {
        if (view.IsMine && isOriginalOwner)
        {
            // This is the local player's own character, proceed with input.
            if (IsAnyInputFieldFocused())
            {
                StopMovingAlongPath();
                rb.velocity = Vector2.zero;
                return;
            }

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
            // This is a remote player's character.
            HandleRemotePlayerMovement();
            ControlRemotePlayerAnimation();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.otherPlayers.Remove(this);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(moveHorizontal);
            stream.SendNext(moveVertical);
        }
        else
        {
            moveHorizontal = (float)stream.ReceiveNext();
            moveVertical = (float)stream.ReceiveNext();
        }
    }

    private void OnJoinChannelSuccessHandler(string channelName, uint uid, int elapsed)
    {
        localAgoraID = uid;
        _propCache.Clear();
        _propCache.Add("agoraID", localAgoraID.ToString());
        view.Owner.SetCustomProperties(_propCache);
    }

    private void HandleLocalPlayerInput()
    {
        // --- MODIFICATION START ---
        // Capture input into local variables first.
        float inputH = Input.GetAxis("Horizontal");
        float inputV = Input.GetAxis("Vertical");

        // ONLY overwrite the class-level animation variables if we are NOT following.
        // If we ARE following, the Coroutine controls these values to match the movement direction.
        if (!isFollowing)
        {
            moveHorizontal = inputH;
            moveVertical = inputV;
        }

        // Use local input vars to check for movement cancellation
        if (Mathf.Abs(inputH) > 0.01f || Mathf.Abs(inputV) > 0.01f)
        {
            if (isPlayerMovingUsingMouse)
            {
                StopMovingAlongPath();
            }
        }
        // --- MODIFICATION END ---

        if (!isPlayerMovingUsingMouse && !isCalculatingPath && !isFollowing)
        {
            animator.enabled = true;
            // Use member vars here (which match input if !isFollowing)
            movement = new Vector2(moveHorizontal, moveVertical);
            rb.velocity = movement * moveSpeed;
        }

        if (_mainCamera == null) _mainCamera = Camera.main;

        bool interactionInput = Input.GetMouseButtonDown(1) || (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(0));

        if (interactionInput)
        {
            Vector3 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, Vector2.zero, Mathf.Infinity);

            bool foundSomething = false;

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;

                PlayerInteractionUI targetInteraction = hit.collider.GetComponentInParent<PlayerInteractionUI>();

                if (targetInteraction != null)
                {
                    targetInteraction.ShowInteractionOptions();
                    foundSomething = true;
                    break;
                }
            }

            if (foundSomething) return;
        }

        if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftShift))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            RaycastHit2D hit = Physics2D.Raycast(_mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero, Mathf.Infinity);

            if (_cachedScreenShareHandler == null) _cachedScreenShareHandler = FindObjectOfType<ScreenShareClickHandler>();

            if (_cachedScreenShareHandler != null && _cachedScreenShareHandler.IsMouseOverScreenShare()) return;

            // Stop following logic moved UP, outside the "hit.collider" check.
            if (isFollowing)
            {
                StopFollowing();
            }

            PlayerInteractionUI.HideAll();

            if (hit.collider == null || hit.collider.isTrigger)
            {
                float clickTime = Time.time;
                if (clickTime - lastClickTime < doubleClickTimeThreshold)
                {
                    isPlayerMovingUsingMouse = true;
                    target = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    target.z = transform.position.z;

                    if (!isCalculatingPath)
                    {
                        isCalculatingPath = true;
                        FindPathToTarget();
                    }
                }
                lastClickTime = clickTime;
            }
        }

        if (isPlayerMovingUsingMouse)
        {
            animator.enabled = true;
            MoveAlongPath();

            if (Vector3.Distance(transform.position, target) < 0.2f)
            {
                StopMovingAlongPath();
            }
        }
    }

    public void StartFollowing(Transform targetTransform)
    {
        if (targetTransform == null) return;

        StopMovingAlongPath();

        Debug.Log($"[PlayerController] Started following: {targetTransform.name}");
        currentFollowTarget = targetTransform;
        isFollowing = true;
        isPlayerMovingUsingMouse = false;

        if (followCoroutine != null) StopCoroutine(followCoroutine);
        followCoroutine = StartCoroutine(FollowTargetRoutine());
    }

    public void StopFollowing()
    {
        isFollowing = false;
        currentFollowTarget = null;
        if (followCoroutine != null) StopCoroutine(followCoroutine);

        StopMovingAlongPath();
    }

    private IEnumerator FollowTargetRoutine()
    {
        // Using FixedUpdate for smooth physics movement
        WaitForFixedUpdate fixedWait = new WaitForFixedUpdate();
        float nextPathTime = 0;

        while (isFollowing && currentFollowTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentFollowTarget.position);

            if (dist > followingStoppingDistance)
            {
                bool hasLineOfSight = !Physics2D.Linecast(transform.position, currentFollowTarget.position, solidObjectsLayer);

                if (hasLineOfSight && dist < 10f)
                {
                    Vector3 direction = (currentFollowTarget.position - transform.position).normalized;
                    rb.velocity = direction * (moveSpeed * directFollowSpeedMultiplier);

                    // Directly sync animation variables to the normalized vector
                    // HandleLocalPlayerInput will now respect these values because isFollowing is true
                    moveHorizontal = direction.x;
                    moveVertical = direction.y;
                }
                else
                {
                    if (Time.time > nextPathTime)
                    {
                        target = currentFollowTarget.position;
                        target.z = transform.position.z;
                        if (!isCalculatingPath)
                        {
                            isCalculatingPath = true;
                            FindPathToTarget();
                        }
                        // Faster path recalculation for snappier response
                        nextPathTime = Time.time + 0.1f;
                    }

                    if (path != null && currentWaypoint < path.vectorPath.Count)
                    {
                        Vector3 dir = (path.vectorPath[currentWaypoint] - transform.position).normalized;
                        rb.velocity = dir * moveSpeed;
                        moveHorizontal = dir.x;
                        moveVertical = dir.y;

                        if (Vector3.Distance(transform.position, path.vectorPath[currentWaypoint]) < 0.2f)
                        {
                            currentWaypoint++;
                        }
                    }
                }
            }
            else
            {
                rb.velocity = Vector2.zero;
                moveHorizontal = 0;
                moveVertical = 0;
            }

            yield return fixedWait;
        }

        if (currentFollowTarget == null && isFollowing)
        {
            StopFollowing();
        }
    }

    private void HandleRemotePlayerMovement()
    {
        movement = new Vector2(moveHorizontal, moveVertical);
        rb.velocity = movement * moveSpeed;
    }

    private void ControlLocalPlayerAnimation()
    {
        if (isFollowing)
        {
            isMoving = rb.velocity.sqrMagnitude > 0.1f;
        }
        else
        {
            isMoving = (movement != Vector2.zero && !isPlayerMovingUsingMouse) || (isPlayerMovingUsingMouse && path != null);
        }

        ControlAnimation(moveHorizontal, moveVertical, isMoving);
    }

    private void ControlRemotePlayerAnimation()
    {
        isMoving = movement != Vector2.zero;
        ControlAnimation(moveHorizontal, moveVertical, isMoving);
    }

    private void FindPathToTarget()
    {
        if (seeker != null && seeker.IsDone())
            seeker.StartPath(transform.position, target, OnPathComplete);
        else
            isCalculatingPath = false;
    }

    private void OnPathComplete(Path p)
    {
        isCalculatingPath = false;

        if (!p.error)
        {
            path = p;
            currentWaypoint = 0;

            if (isFollowing)
            {
                if (pointerObject != null) pointerObject.SetActive(false);
            }
            else
            {
                if (pointerObject != null)
                {
                    pointerObject.transform.position = target;
                    pointerObject.SetActive(true);
                }
            }
        }
        else
        {
            if (!isFollowing) StopMovingAlongPath();
        }
    }

    private void StopMovingAlongPath()
    {
        isPlayerMovingUsingMouse = false;

        isFollowing = false;
        if (followCoroutine != null) StopCoroutine(followCoroutine);
        currentFollowTarget = null;

        path = null;
        currentWaypoint = 0;

        if (pointerObject != null && pointerObject.activeSelf) pointerObject.SetActive(false);

        moveHorizontal = 0;
        moveVertical = 0;
        isMoving = false;
        rb.velocity = Vector2.zero;

        ControlAnimation(0, 0, false);
    }

    private void MoveAlongPath()
    {
        if (path == null) return;

        if (currentWaypoint >= path.vectorPath.Count)
        {
            StopMovingAlongPath();
            return;
        }

        Vector3 direction = (path.vectorPath[currentWaypoint] - transform.position).normalized;

        rb.velocity = direction * moveSpeed;
        transform.position = Vector3.MoveTowards(transform.position, path.vectorPath[currentWaypoint], moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, path.vectorPath[currentWaypoint]) < 0.17f)
        {
            currentWaypoint++;
        }

        if (currentWaypoint == 1)
        {
            moveHorizontal = -direction.x;
            moveVertical = -direction.y;
        }
        else
        {
            moveHorizontal = direction.x;
            moveVertical = direction.y;
        }
    }

    [System.Obsolete]
    public void ControlAnimation(float x, float y, bool isMoving)
    {
        if (isMoving)
        {
            CameraDrag drag = FindObjectOfType<CameraDrag>();
            if (drag != null) drag.endDrag = false;

            Direction currentDirection = GetDirection(x, y);
            int targetAnimationState = GetAnimationState(currentDirection);

            bool isIdle = animator.GetCurrentAnimatorStateInfo(0).IsName("IdleFront") ||
                          animator.GetCurrentAnimatorStateInfo(0).IsName("IdleBack") ||
                          animator.GetCurrentAnimatorStateInfo(0).IsName("IdleLeft") ||
                          animator.GetCurrentAnimatorStateInfo(0).IsName("IdleRight");

            if (currentDirection != lastDirection || isIdle)
            {
                animator.CrossFade(targetAnimationState, 0.0f, 0);
            }
            lastDirection = currentDirection;
        }
        else
        {
            switch (lastDirection)
            {
                case Direction.Idle: animator.CrossFade(IdleFront, 0, 0); break;
                case Direction.Left: animator.CrossFade(IdleLeft, 0, 0); break;
                case Direction.Right: animator.CrossFade(IdleRight, 0, 0); break;
                case Direction.Front: animator.CrossFade(IdleFront, 0, 0); break;
                case Direction.Back: animator.CrossFade(IdleBack, 0, 0); break;
            }
        }
    }

    private int IdleFront = Animator.StringToHash("IdleFront");
    private int IdleBack = Animator.StringToHash("IdleBack");
    private int IdleLeft = Animator.StringToHash("IdleLeft");
    private int IdleRight = Animator.StringToHash("IdleRight");

    private int GetAnimationState(Direction direction)
    {
        switch (direction)
        {
            case Direction.Idle: return IdleFront;
            case Direction.Left: return WalkLeft;
            case Direction.Right: return WalkRight;
            case Direction.Front: return WalkFront;
            case Direction.Back: return WalkBack;
            default: return IdleFront;
        }
    }

    private Direction GetDirection(float x, float y)
    {
        if (Mathf.Abs(x) < 0.01f && Mathf.Abs(y) < 0.01f)
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

    public void SendVideoState(bool enable)
    {
        if (enable && ScreenShareEnabled)
        {
            GameManager.Instance.agoraClientManager.SetScreenShareState(false);
        }

        _propCache.Clear();
        _propCache.Add(IS_VIDEO_ENABLED_PROP, enable);
        view.Owner.SetCustomProperties(_propCache);

        view.RPC(nameof(OnReceiveVideoState), RpcTarget.All, enable);
    }

    public void SyncVideoState()
    {
        if (view.Owner.CustomProperties.TryGetValue(IS_VIDEO_ENABLED_PROP, out object isEnabled))
        {
            OnReceiveVideoState((bool)isEnabled);
        }
        else
        {
            OnReceiveVideoState(false);
        }

        if (view.Owner.CustomProperties.TryGetValue(IS_SCREEN_SHARING_PROP, out object isScreenSharing))
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
        if (view.Owner.CustomProperties.TryGetValue(IS_SCREEN_SHARING_PROP, out object isSharing))
        {
            bool screenShareEnabled = (bool)isSharing;
            OnReceiveScreenShareState(screenShareEnabled);

            if (screenShareEnabled)
            {
                bool shouldShow = GameManager.Instance.ShouldShowScreenShare(this);

                playerScreenObject.SetActive(shouldShow || view.IsMine);
                playerScreenSurface.SetForUser((uint)view.ViewID);
                playerScreenSurface.SetEnable(shouldShow || view.IsMine);
                playerScreenSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);

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
        bool shouldShow = GameManager.Instance.ShouldShowVideo(this);

        playerVideoObject.SetActive(enable && (view.IsMine || shouldShow));
        playerVideoSurface.SetForUser(view.ViewID == GameManager.Instance.myPlayer.view.ViewID ? 0 : (uint)view.ViewID);
        playerVideoSurface.SetEnable(enable && (view.IsMine || shouldShow));

        if (enable && playerScreenObject.activeSelf)
        {
            playerScreenObject.SetActive(false);
            playerScreenSurface.SetEnable(false);
        }
    }

    [PunRPC]
    public void DestroyPlayerRPC()
    {
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    public void SendAudioState(bool enable)
    {
        if (!view.IsMine) return;

        _propCache.Clear();
        _propCache.Add(IS_AUDIO_ENABLED_PROP, enable);
        view.Owner.SetCustomProperties(_propCache);

        GameManager.Instance.agoraClientManager.mRtcEngine.EnableLocalAudio(enable);
        GameManager.Instance.agoraClientManager.mRtcEngine.MuteLocalAudioStream(!enable);

        view.RPC(nameof(OnReceiveAudioState), RpcTarget.All, enable);
    }

    public void SyncAudioState(bool forceUIUpdate = false)
    {
        if (view.Owner.CustomProperties.TryGetValue(IS_AUDIO_ENABLED_PROP, out object isEnabled))
        {
            bool audioEnabled = (bool)isEnabled;
            OnReceiveAudioState(audioEnabled);

            if (forceUIUpdate)
            {
                playerAudioObject.SetActive(audioEnabled);
            }
        }
        else
        {
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
        playerAudioObject.SetActive(enable);
    }

    public void SendScreenShareState(bool enable)
    {
        _propCache.Clear();
        _propCache.Add(IS_SCREEN_SHARING_PROP, enable);
        view.Owner.SetCustomProperties(_propCache);

        view.RPC(nameof(OnReceiveScreenShareState), RpcTarget.All, enable);
    }

    [PunRPC]
    public void OnReceiveScreenShareState(bool enable)
    {
        bool shouldShow = GameManager.Instance.ShouldShowScreenShare(this);

        playerScreenObject.SetActive(enable && (view.IsMine || shouldShow));
        playerScreenSurface.SetForUser(view.ViewID == GameManager.Instance.myPlayer.view.ViewID ? 0 : (uint)view.ViewID);
        playerScreenSurface.SetEnable(enable && (view.IsMine || shouldShow));

        if (enable && (view.IsMine || shouldShow))
        {
            playerScreenSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
        }

        GameManager.Instance.SetScreenShareObjectState(
            (uint)view.ViewID,
            view.Owner.NickName,
            enable && (view.IsMine || shouldShow)
        );
    }
}
// --- END OF FILE PlayerController.cs ---