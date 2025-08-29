using UnityEngine;
using agora_gaming_rtc;
using Photon.Pun;

public class AgoraClientManager : MonoBehaviour
{
    [SerializeField] private string appId;

    [SerializeField] private string CHANNEL_NAME = "";

    public IRtcEngine mRtcEngine = null;

    private void Start()
    {
        if (!CheckAppId())
        {
            Debug.LogError("<color=red>[STOP] Please fill in your appId in your AppIDInfo Object!!!! \n (Assets/API-Example/_AppIDInfo/AppIDInfo)</color>");

            return;
        }

        InitEngine();
    }

    bool CheckAppId()
    {
        return appId.Length > 10;
    }

    void InitEngine()
    {
        mRtcEngine = IRtcEngine.GetEngine(appId);

        Debug.Log("########## Agora Engine version : " + IRtcEngine.GetSdkVersion());

        mRtcEngine.SetChannelProfile(CHANNEL_PROFILE.CHANNEL_PROFILE_LIVE_BROADCASTING);
        mRtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);

        // Initialize audio properly
        mRtcEngine.EnableAudio();
        mRtcEngine.EnableLocalAudio(true); // Enable local audio to trigger permission request
        mRtcEngine.MuteLocalAudioStream(true); // But mute it initially

        mRtcEngine.EnableVideo();
        mRtcEngine.EnableVideoObserver();

        mRtcEngine.OnJoinChannelSuccess += OnJoinChannelSuccessHandler;
        mRtcEngine.OnLeaveChannel += OnLeaveChannelHandler;

        mRtcEngine.OnScreenShareStarted += OnScreenShareStarted;
        mRtcEngine.OnScreenShareStopped += OnScreenShareStopped;
        mRtcEngine.OnScreenShareCanceled += OnScreenShareCancelled;

        mRtcEngine.OnRemoteAudioStateChanged += (id, state, reason, other) =>
        {
            Debug.Log("########## OnRemoteAudioStateChanged " + id + " state: " + state + " reason: " + reason);
        };

        mRtcEngine.OnRemoteVideoStateChanged += (id, state, reason, other) =>
        {
            Debug.Log("########## OnRemoteVideoStateChanged " + id + " state: " + state + " reason: " + reason);
        };

        mRtcEngine.OnLocalAudioStateChanged += OnLocalAudioStateChanged;
        mRtcEngine.OnLocalVideoStateChanged += OnLocalVideoStateChanged;

        mRtcEngine.OnUserMuteVideo += (id, muted) =>
        {
            Debug.Log("########## OnUserMuteVideo " + " id: " + id + " muted: " + muted);
        };

        mRtcEngine.OnUserJoined += OnUserJoinedHandler;
        mRtcEngine.OnUserOffline += OnUserOfflineHandler;

        mRtcEngine.OnError += OnErrorHandler;
    }

    public void JoinChannel()
    {
        CHANNEL_NAME = PhotonNetwork.CurrentRoom.Name;

        var options = new ChannelMediaOptions(true, true, true, true);

        mRtcEngine.JoinChannel(null, CHANNEL_NAME, "", (uint)GameManager.Instance.myPlayer.view.ViewID, options);
    }

    public void LeaveChannel()
    {
        // Stop screen sharing before leaving channel
        if (GameManager.Instance.myPlayer != null &&
            GameManager.Instance.myPlayer.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isSharing) &&
            (bool)isSharing)
        {
            SetScreenShareState(false);
            GameManager.Instance.myPlayer.SendScreenShareState(false);
        }

        mRtcEngine.LeaveChannel();
    }

    void OnJoinChannelSuccessHandler(string channelId, uint uid, int elapsed)
    {
        Debug.Log($"########## OnJoinChannelSuccess channelId: {CHANNEL_NAME}, uid: {uid}, elapsed: {elapsed}");

        SetLocalAudioState(false);
        SetLocalVideoState(false);

        SetVideoSurfaceForPlayer(uid, 0);

        // Set up video surfaces for existing players based on their stored state
        foreach (PlayerController player in GameManager.Instance.otherPlayers)
        {
            if (player != null && player.view != null && !player.view.IsMine)
            {
                if (player.view.Owner.CustomProperties.TryGetValue("isVideoEnabled", out object isEnabled))
                {
                    player.OnReceiveVideoState((bool)isEnabled);
                }
                else
                {
                    player.OnReceiveVideoState(false);
                }

                // Check and sync screen share state
                if (player.view.Owner.CustomProperties.TryGetValue("isScreenSharing", out object isSharing) && (bool)isSharing)
                {
                    player.SyncScreenShareState();
                }
            }
        }
    }

    void OnLeaveChannelHandler(RtcStats rtcStats)
    {
        Debug.Log($"########## OnLeaveChannelHandler channelId: {CHANNEL_NAME}");
    }

    void OnErrorHandler(int err, string message)
    {
        Debug.Log($"########## UserErrorHandler err: {err}, message: {message}");
    }

    void OnUserJoinedHandler(uint uid, int elapsed)
    {
        Debug.Log($"########## OnUserJoinedHandler channelId: {CHANNEL_NAME} uid: {uid} elapsed: {elapsed}");
        SetVideoSurfaceForPlayer(uid, 0);
    }

    void OnUserOfflineHandler(uint uid, USER_OFFLINE_REASON reason)
    {
        Debug.Log($"########## OnUserOffLine uid: ${uid}, reason: ${reason}");
    }

    void OnScreenShareStarted(string channelName, uint uid, int elapsed)
    {
        var userControls = GameObject.Find("ControlsCanvas").GetComponent<UserControlsUI>();
        userControls.ScreenShareEnabled = true;

        Debug.Log($"########## OnScreenShareStarted channel: {channelName} id: {uid} elapsed: {elapsed}");
        UpdateScreenShareState(true, uid);

        SetLocalVideoState(true);
    }

    void OnScreenShareStopped(string channelName, uint uid, int elapsed)
    {
        var userControls = GameObject.Find("ControlsCanvas").GetComponent<UserControlsUI>();
        userControls.ScreenShareEnabled = false;

        Debug.Log($"########## OnScreenShareStopped channel: {channelName} id: {uid} elapsed: {elapsed}");
        UpdateScreenShareState(false, uid);

        SetLocalVideoState(false);
    }

    void OnScreenShareCancelled(string channelName, uint uid, int elapsed)
    {
        var userControls = GameObject.Find("ControlsCanvas").GetComponent<UserControlsUI>();
        userControls.ScreenShareEnabled = false;

        Debug.Log($"########## OnScreenShareCancelled channel: {channelName} id: {uid} elapsed: {elapsed}");
        UpdateScreenShareState(false, uid);

        SetLocalVideoState(false);
    }

    void OnLocalAudioStateChanged(LOCAL_AUDIO_STREAM_STATE state, LOCAL_AUDIO_STREAM_ERROR reason)
    {
        // -------------------- MODIFICATION START --------------------
        // GUARD CLAUSE: Before trying to send a Photon message, we MUST ensure Photon is
        // in a state to send them. This prevents errors during the disconnect process.
        // We also check if GameManager and the player object exist to avoid NullReferenceExceptions.
        if (!PhotonNetwork.IsConnectedAndReady || GameManager.Instance == null || GameManager.Instance.myPlayer == null)
        {
            return;
        }
        // -------------------- MODIFICATION END --------------------

        Debug.Log($"########## OnLocalAudioStateChanged state: {state} reason: {reason}");

        bool enable = state == LOCAL_AUDIO_STREAM_STATE.LOCAL_AUDIO_STREAM_STATE_RECORDING || state == LOCAL_AUDIO_STREAM_STATE.LOCAL_AUDIO_STREAM_STATE_ENCODING;

        GameManager.Instance.myPlayer.SendAudioState(enable);
    }

    void OnLocalVideoStateChanged(LOCAL_VIDEO_STREAM_STATE state, LOCAL_VIDEO_STREAM_ERROR reason)
    {
        // -------------------- MODIFICATION START --------------------
        // GUARD CLAUSE: Add the same check here for video state changes.
        if (!PhotonNetwork.IsConnectedAndReady || GameManager.Instance == null || GameManager.Instance.myPlayer == null)
        {
            return;
        }
        // -------------------- MODIFICATION END --------------------

        Debug.Log($"########## OnLocalVideoStateChanged state: {state} reason: {reason}");

        bool enable = state == LOCAL_VIDEO_STREAM_STATE.LOCAL_VIDEO_STREAM_STATE_CAPTURING || state == LOCAL_VIDEO_STREAM_STATE.LOCAL_VIDEO_STREAM_STATE_ENCODING;

        GameManager.Instance.myPlayer.SendVideoState(enable);
    }

    //for enabling/disabling local video through IRtcEngine class.
    public void SetLocalVideoState(bool enable)
    {
        if (mRtcEngine == null) return;
        mRtcEngine.EnableLocalVideo(enable);

        // -------------------- MODIFICATION START --------------------
        // GUARD CLAUSE: Also protect manual calls that sync state over Photon.
        if (!PhotonNetwork.IsConnectedAndReady || GameManager.Instance == null || GameManager.Instance.myPlayer == null)
        {
            return;
        }
        // -------------------- MODIFICATION END --------------------

        GameManager.Instance.myPlayer.SendVideoState(enable);
    }

    public void SetLocalAudioState(bool enable)
    {
        if (mRtcEngine == null) return;
        mRtcEngine.EnableLocalAudio(enable);

        // -------------------- MODIFICATION START --------------------
        // GUARD CLAUSE: Protect this manual call as well.
        if (!PhotonNetwork.IsConnectedAndReady || GameManager.Instance == null || GameManager.Instance.myPlayer == null)
        {
            return;
        }
        // -------------------- MODIFICATION END --------------------

        GameManager.Instance.myPlayer.SendAudioState(enable);
    }

    //for starting/stopping a new screen share through IRtcEngine class.
    public void SetScreenShareState(bool enable)
    {
        if (enable)
        {
            mRtcEngine.StartScreenCaptureForWeb(false);
        }
        else
        {
            mRtcEngine.StopScreenCapture();
        }
    }

    private void UpdateScreenShareState(bool enable, uint uid)
    {
        // -------------------- MODIFICATION START --------------------
        // GUARD CLAUSE: Protect this method that sends a Photon message.
        if (!PhotonNetwork.IsConnectedAndReady || GameManager.Instance == null || GameManager.Instance.myPlayer == null)
        {
            return;
        }
        // -------------------- MODIFICATION END --------------------

        GameManager.Instance.myPlayer.SendScreenShareState(enable);
    }

    private void OnDestroy()
    {
        Debug.Log("OnDestroy");
        if (mRtcEngine != null)
        {
            LeaveChannel();
            mRtcEngine.DisableVideoObserver();
            IRtcEngine.Destroy();
        }

    }

    internal void SetVideoSurfaceForPlayer(uint uid, int elapsed)
    {
        PlayerController playerController = null;

        // -------------------- MODIFICATION START --------------------
        // SAFETY CHECK: Ensure GameManager and its list exist before trying to access them.
        if (GameManager.Instance == null || GameManager.Instance.otherPlayers == null)
        {
            Debug.LogWarning("SetVideoSurfaceForPlayer: GameManager instance or otherPlayers list is not available.");
            return;
        }
        // -------------------- MODIFICATION END --------------------

        for (int i = 0; i < GameManager.Instance.otherPlayers.Count; i++)
        {
            if (GameManager.Instance.otherPlayers[i].view.ViewID == uid)
            {
                playerController = GameManager.Instance.otherPlayers[i];
                break;
            }
        }

        if (playerController == null)
        {
            // It's possible the player object hasn't been fully initialized yet, so this might not be an error.
            Debug.LogWarning("SetVideoSurfaceForPlayer: Could not find PlayerController for UID: " + uid + ". It may not have spawned yet.");
            return;
        }

        Debug.Log("Set Video Surface : uid = " + uid + " elapsed = " + elapsed);

        VideoSurface videoSurface = playerController.playerVideoSurface;

        if (!ReferenceEquals(videoSurface, null))
        {
            if (uid == GameManager.Instance.myPlayer.view.ViewID)
            {
                Debug.Log("Self Player");
                videoSurface.SetForUser(0);
            }
            else
            {
                Debug.Log($"{uid} Other Player");
                videoSurface.SetForUser(uid);
            }

            videoSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
            videoSurface.SetEnable(true);
        }
    }
}