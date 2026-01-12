using Photon.Pun;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserControlsUI : MonoBehaviour
{
    public Button lockRoomButton;
    private SpatialRoom currentRoom;
    public Sprite lockedIcon;
    public Sprite unlockedIcon;
    private bool isMicInitialized = false;

    // --- MODIFICATION START ---
    [Header("Control Buttons")]
    public Button audioButton, videoButton, screenShareButton, leaveRoomButton, mapViewButton, meetingViewButton, broadcastButton;
    [SerializeField] private Sprite enableAudio, disableAudio, enableVideo, disableVideo, enableBroadcast, disableBroadcast;
    // --- MODIFICATION END ---

    [SerializeField] private GameObject uiElement1, uiElement2;
    [SerializeField] private Image imageComponent;

    [SerializeField] private RectTransform screenShareRectTransform;

    private Vector2 originalScreenShareSizeDelta, originalScreenShareAnchorMin, originalScreenShareAnchorMax, originalScreenSharePivot, originalScreenShareAnchoredPosition;
    private Vector2 smallScreenShareSizeDelta = new Vector2(433, 266.6864f);
    private Vector2 smallScreenShareAnchorMin = new Vector2(0.5f, 1);
    private Vector2 smallScreenShareAnchorMax = new Vector2(0.5f, 1);
    private Vector2 smallScreenSharePivot = new Vector2(0.5f, 1);
    private Vector2 smallScreenShareAnchoredPosition = new Vector2(0, 50);

    public float animationTime = 0.5f;

    public bool isMapViewActive = false;
    private bool audioEnabled, videoEnabled, screenShareEnabled, broadcastEnabled;

    [SerializeField] private TextMeshProUGUI pingText;

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SetButtonHoverState(string gameObjectName, bool isHovering);

    public bool AudioEnabled { get { return audioEnabled; } set { audioEnabled = value; SetAudioState(); } }
    public bool VideoEnabled { get { return videoEnabled; } set { videoEnabled = value; SetVideoState(); } }
    public bool BroadcastEnabled { get { return broadcastEnabled; } set { broadcastEnabled = value; SetBroadcastState(); } }

    public bool ScreenShareEnabled
    {
        get { return screenShareEnabled; }
        set
        {
            screenShareEnabled = value;
            videoButton.interactable = !value;
        }
    }

    private void Start()
    {
        Application.runInBackground = true;

        AudioEnabled = false;
        VideoEnabled = false;
        ScreenShareEnabled = false;
        BroadcastEnabled = false;

        var rightClickHandler = screenShareRectTransform.gameObject.AddComponent<ScreenShareClickHandler>();
        rightClickHandler.userControlsUI = this;

        audioButton.onClick.AddListener(() =>
        {
            if (!isMicInitialized)
            {
                GameManager.Instance.agoraClientManager.InitializeAndEnableLocalAudio();
                isMicInitialized = true;
            }
            AudioEnabled = !AudioEnabled;
            GameManager.Instance.myPlayer.SendAudioState(AudioEnabled);
        });

        videoButton.onClick.AddListener(() =>
        {
            VideoEnabled = !VideoEnabled;
            GameManager.Instance.agoraClientManager.SetLocalVideoState(VideoEnabled);
        });

        screenShareButton.onClick.AddListener(() =>
        {
            GameManager.Instance.agoraClientManager.SetScreenShareState(!ScreenShareEnabled);
        });

        // --- MODIFICATION START ---
        broadcastButton.onClick.AddListener(() =>
        {
            BroadcastEnabled = !BroadcastEnabled;
            GameManager.Instance.myPlayer.SetBroadcastState(BroadcastEnabled);
        });
        // --- MODIFICATION END ---

        leaveRoomButton.onClick.AddListener(() => GameManager.Instance.LeaveRoom());

        originalScreenShareSizeDelta = screenShareRectTransform.sizeDelta;
        originalScreenShareAnchorMin = screenShareRectTransform.anchorMin;
        originalScreenShareAnchorMax = screenShareRectTransform.anchorMax;
        originalScreenSharePivot = screenShareRectTransform.pivot;
        originalScreenShareAnchoredPosition = screenShareRectTransform.anchoredPosition;

        mapViewButton.onClick.AddListener(OnMapViewButtonClicked);
        meetingViewButton.onClick.AddListener(OnMeetingViewButtonClicked);
        meetingViewButton.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (PhotonNetwork.IsConnected && pingText)
        {
            pingText.text = "Ping: " + PhotonNetwork.GetPing() + " ms";
        }
    }

    private void OnMapViewButtonClicked()
    {
        if (!isMapViewActive)
        {
            isMapViewActive = true;
            meetingViewButton.gameObject.SetActive(true);
            StartCoroutine(AnimateToMapView());
        }
    }

    public void OnMeetingViewButtonClicked()
    {
        if (isMapViewActive)
        {
            isMapViewActive = false;
            StartCoroutine(AnimateToMeetingView());
        }
    }

    private IEnumerator AnimateToMapView()
    {
        uiElement1.SetActive(false);
        uiElement2.SetActive(false);
        imageComponent.enabled = false;

        float elapsedTime = 0;
        Vector2 startSizeDelta = screenShareRectTransform.sizeDelta;
        Vector2 startAnchorMin = screenShareRectTransform.anchorMin;
        Vector2 startAnchorMax = screenShareRectTransform.anchorMax;
        Vector2 startPivot = screenShareRectTransform.pivot;
        Vector2 startAnchoredPosition = screenShareRectTransform.anchoredPosition;

        while (elapsedTime < animationTime)
        {
            float ratio = elapsedTime / animationTime;
            screenShareRectTransform.sizeDelta = Vector2.Lerp(startSizeDelta, smallScreenShareSizeDelta, ratio);
            screenShareRectTransform.anchorMin = Vector2.Lerp(startAnchorMin, smallScreenShareAnchorMin, ratio);
            screenShareRectTransform.anchorMax = Vector2.Lerp(startAnchorMax, smallScreenShareAnchorMax, ratio);
            screenShareRectTransform.pivot = Vector2.Lerp(startPivot, smallScreenSharePivot, ratio);
            screenShareRectTransform.anchoredPosition = Vector2.Lerp(startAnchoredPosition, smallScreenShareAnchoredPosition, ratio);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        screenShareRectTransform.sizeDelta = smallScreenShareSizeDelta;
        screenShareRectTransform.anchorMin = smallScreenShareAnchorMin;
        screenShareRectTransform.anchorMax = smallScreenShareAnchorMax;
        screenShareRectTransform.pivot = smallScreenSharePivot;
        screenShareRectTransform.anchoredPosition = smallScreenShareAnchoredPosition;
    }

    private IEnumerator AnimateToMeetingView()
    {
        float elapsedTime = 0;
        Vector2 startSizeDelta = screenShareRectTransform.sizeDelta;
        Vector2 startAnchorMin = screenShareRectTransform.anchorMin;
        Vector2 startAnchorMax = screenShareRectTransform.anchorMax;
        Vector2 startPivot = screenShareRectTransform.pivot;
        Vector2 startAnchoredPosition = screenShareRectTransform.anchoredPosition;

        while (elapsedTime < animationTime)
        {
            float ratio = elapsedTime / animationTime;
            screenShareRectTransform.sizeDelta = Vector2.Lerp(startSizeDelta, originalScreenShareSizeDelta, ratio);
            screenShareRectTransform.anchorMin = Vector2.Lerp(startAnchorMin, originalScreenShareAnchorMin, ratio);
            screenShareRectTransform.anchorMax = Vector2.Lerp(startAnchorMax, originalScreenShareAnchorMax, ratio);
            screenShareRectTransform.pivot = Vector2.Lerp(startPivot, originalScreenSharePivot, ratio);
            screenShareRectTransform.anchoredPosition = Vector2.Lerp(startAnchoredPosition, originalScreenShareAnchoredPosition, ratio);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        screenShareRectTransform.sizeDelta = originalScreenShareSizeDelta;
        screenShareRectTransform.anchorMin = originalScreenShareAnchorMin;
        screenShareRectTransform.anchorMax = originalScreenShareAnchorMax;
        screenShareRectTransform.pivot = originalScreenSharePivot;
        screenShareRectTransform.anchoredPosition = originalScreenShareAnchoredPosition;

        uiElement1.SetActive(true);
        uiElement2.SetActive(true);
        imageComponent.enabled = true;
        meetingViewButton.gameObject.SetActive(false);
    }

    private void SetAudioState()
    {
        audioButton.image.sprite = audioEnabled ? enableAudio : disableAudio;
    }

    private void SetVideoState()
    {
        videoButton.image.sprite = videoEnabled ? enableVideo : disableVideo;
    }

    // --- MODIFICATION START ---
    private void SetBroadcastState()
    {
        if (broadcastButton == null) return;
        broadcastButton.image.sprite = broadcastEnabled ? enableBroadcast : disableBroadcast;
    }
    // --- MODIFICATION END ---

    public void SetScreenShareButtonState(bool interactable)
    {
        screenShareButton.interactable = interactable;
    }

    public void SetCurrentRoom(SpatialRoom room)
    {
        currentRoom = room;
        UpdateLockButtonState();
    }

    public void UpdateLockButtonState()
    {
        if (currentRoom == null)
        {
            lockRoomButton.gameObject.SetActive(false);
            return;
        }

        lockRoomButton.gameObject.SetActive(true);
        Image lockIconImage = lockRoomButton.transform.Find("Icon").GetComponent<Image>();

        lockIconImage.sprite = currentRoom.IsLocked ? lockedIcon : unlockedIcon;
        lockRoomButton.interactable = true;
    }

    public void SetVideoButtonState(bool interactable)
    {
        videoButton.interactable = interactable;
    }

    public void HideMeetingViewButton()
    {
        if (isMapViewActive)
        {
            meetingViewButton.gameObject.SetActive(false);
        }
    }

    public Button MeetingViewButton => meetingViewButton;
}