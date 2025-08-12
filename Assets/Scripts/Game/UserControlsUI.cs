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

    public Button audioButton, videoButton, screenShareButton, leaveRoomButton, mapViewButton, meetingViewButton;
    [SerializeField] private Sprite enableAudio, disableAudio, enableVideo, disableVideo;

    [SerializeField] private GameObject uiElement1, uiElement2; 
    [SerializeField] private Image imageComponent; 

    [SerializeField] private RectTransform screenShareRectTransform; 

    private Vector2 originalScreenShareSizeDelta;
    private Vector2 originalScreenShareAnchorMin;
    private Vector2 originalScreenShareAnchorMax;
    private Vector2 originalScreenSharePivot;
    private Vector2 originalScreenShareAnchoredPosition;

    private Vector2 smallScreenShareSizeDelta = new Vector2(433, 266.6864f);
    private Vector2 smallScreenShareAnchorMin = new Vector2(0.5f, 1);
    private Vector2 smallScreenShareAnchorMax = new Vector2(0.5f, 1);
    private Vector2 smallScreenSharePivot = new Vector2(0.5f, 1);
    private Vector2 smallScreenShareAnchoredPosition = new Vector2(0, 50);

    public float animationTime = 0.5f; 

    public bool isMapViewActive = false;
    private bool audioEnabled, videoEnabled, screenShareEnabled;

    [SerializeField] private TextMeshProUGUI pingText;

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SetButtonHoverState(string gameObjectName, bool isHovering);

    public bool AudioEnabled
    {
        get 
        { 
            return audioEnabled; 
        }
        set
        {
            audioEnabled = value;
            SetAudioState();
        }
    }

    public bool VideoEnabled
    {
        get
        {
            return videoEnabled;
        }
        set
        {
            videoEnabled = value;
            SetVideoState();
        }
    }

    public bool ScreenShareEnabled
    {
        get
        {
            return screenShareEnabled;
        }
        set
        {
            screenShareEnabled = value;
            videoButton.interactable = !value; // Disable video button when screen sharing
            
            //if (value)
            //{
            //    // When enabling screen share, ensure video is off
            //    if (VideoEnabled)
            //    {
            //        videoButton.onClick.Invoke();
            //    }
            //}
            //else 
            //{
            //    // When disabling screen share, turn video back on
            //    if (!VideoEnabled)
            //    {
            //        videoButton.onClick.Invoke();
            //    }
            //}
        }
    }

    private void Start()
    {
        Application.runInBackground = true;

        AudioEnabled = false;
        VideoEnabled = false;
        ScreenShareEnabled = false;

        // Add right-click handler to screen share
        var rightClickHandler = screenShareRectTransform.gameObject.AddComponent<ScreenShareClickHandler>();
        rightClickHandler.userControlsUI = this;

        audioButton.onClick.RemoveAllListeners();
        videoButton.onClick.RemoveAllListeners();
        screenShareButton.onClick.RemoveAllListeners();
        leaveRoomButton.onClick.RemoveAllListeners();

        audioButton.onClick.AddListener(() =>
        {
            AudioEnabled = !AudioEnabled;
            GameManager.Instance.agoraClientManager.SetLocalAudioState(AudioEnabled);
        });

        videoButton.onClick.AddListener(() =>
        {
            VideoEnabled = !VideoEnabled;
            GameManager.Instance.agoraClientManager.SetLocalVideoState(VideoEnabled);
        });

        screenShareButton.onClick.AddListener(() =>
        {
            //ScreenShareEnabled = !ScreenShareEnabled;
            GameManager.Instance.agoraClientManager.SetScreenShareState(!ScreenShareEnabled);
        });

        leaveRoomButton.onClick.AddListener(() =>
        {
            GameManager.Instance.LeaveRoom();
        });

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

#if UNITY_WEBGL && !UNITY_EDITOR
        SetButtonHoverState(mapViewButton.gameObject.name, false);
#endif
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
            screenShareRectTransform.sizeDelta = Vector2.Lerp(startSizeDelta, smallScreenShareSizeDelta, (elapsedTime / animationTime));
            screenShareRectTransform.anchorMin = Vector2.Lerp(startAnchorMin, smallScreenShareAnchorMin, (elapsedTime / animationTime));
            screenShareRectTransform.anchorMax = Vector2.Lerp(startAnchorMax, smallScreenShareAnchorMax, (elapsedTime / animationTime));
            screenShareRectTransform.pivot = Vector2.Lerp(startPivot, smallScreenSharePivot, (elapsedTime / animationTime));
            screenShareRectTransform.anchoredPosition = Vector2.Lerp(startAnchoredPosition, smallScreenShareAnchoredPosition, (elapsedTime / animationTime));

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
            screenShareRectTransform.sizeDelta = Vector2.Lerp(startSizeDelta, originalScreenShareSizeDelta, (elapsedTime / animationTime));
            screenShareRectTransform.anchorMin = Vector2.Lerp(startAnchorMin, originalScreenShareAnchorMin, (elapsedTime / animationTime));
            screenShareRectTransform.anchorMax = Vector2.Lerp(startAnchorMax, originalScreenShareAnchorMax, (elapsedTime / animationTime));
            screenShareRectTransform.pivot = Vector2.Lerp(startPivot, originalScreenSharePivot, (elapsedTime / animationTime));
            screenShareRectTransform.anchoredPosition = Vector2.Lerp(startAnchoredPosition, originalScreenShareAnchoredPosition, (elapsedTime / animationTime));

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
        audioButton.transform.Find("Background").GetComponent<Image>().sprite = audioEnabled ? enableAudio : disableAudio;
    }

    private void SetVideoState()
    {
        videoButton.image.sprite = videoEnabled ? enableVideo : disableVideo;
        videoButton.transform.Find("Background").GetComponent<Image>().sprite = videoEnabled ? enableVideo : disableVideo;
    }

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
        Image lockIcon = lockRoomButton.transform.Find("Icon").GetComponent<Image>();
        ButtonHoverEffect hoverEffect = lockRoomButton.GetComponent<ButtonHoverEffect>();

        if (currentRoom.IsLocked)
        {
            lockIcon.sprite = lockedIcon;
            if (hoverEffect != null)
            {
                Destroy(hoverEffect.tooltipInstance);
                if (PhotonNetwork.LocalPlayer.ActorNumber == currentRoom.LockingPlayerId)
                {
                    hoverEffect.tooltipText = "Unlock meeting area";
                }
                else
                {
                    hoverEffect.tooltipText = "Unlock meeting area";
                }
                hoverEffect.CreateTooltip();
            }
            lockRoomButton.interactable = true;
        }
        else
        {
            lockIcon.sprite = unlockedIcon;
            lockRoomButton.interactable = true;
            if (hoverEffect != null)
            {
                Destroy(hoverEffect.tooltipInstance);
                hoverEffect.tooltipText = "Lock meeting area";
                hoverEffect.CreateTooltip();
            }
        }
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

    // Expose the meeting view button for external access
    public Button MeetingViewButton => meetingViewButton;
}
