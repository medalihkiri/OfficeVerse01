using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using TMPro;
using ExitGames.Client.Photon;
using UnityEngine.EventSystems;

public class AvailabilityManager : MonoBehaviourPunCallbacks
{
    public enum AvailabilityStatus
    {
        Available,
        Busy,
        DoNotDisturb
    }

    public Button availabilityButton;
    public GameObject availabilityWindow;
    public Button availableButton;
    public Button busyButton;
    public Button doNotDisturbButton;
    public Image statusCircle;
    public TextMeshProUGUI statusText;

    public Color availableColor = Color.green;
    public Color busyColor = Color.yellow;
    public Color doNotDisturbColor = Color.red;

    public float hoverDuration = 0.2f;
    public float windowFadeDuration = 0.3f;
    public bool b = false;

    private CanvasGroup windowCanvasGroup;
    private AvailabilityStatus currentStatus = AvailabilityStatus.Available;
    public Button closeBtn;

    private void Start()
    {
        windowCanvasGroup = availabilityWindow.GetComponent<CanvasGroup>();
        if (windowCanvasGroup == null)
        {
            windowCanvasGroup = availabilityWindow.AddComponent<CanvasGroup>();
        }

        availabilityWindow.SetActive(false);

        availabilityButton.onClick.AddListener(ToggleAvailabilityWindow);

        availableButton.onClick.AddListener(() => SetStatus(AvailabilityStatus.Available));
        busyButton.onClick.AddListener(() => SetStatus(AvailabilityStatus.Busy));
        doNotDisturbButton.onClick.AddListener(() => SetStatus(AvailabilityStatus.DoNotDisturb));

        if (photonView.IsMine)
        {
            LoadSavedStatus();
        }
        UpdateStatusVisuals();
    }

    private void LoadSavedStatus()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("AvailabilityStatus", out object savedStatus))
        {
            currentStatus = (AvailabilityStatus)savedStatus;
        }
        else
        {
            currentStatus = AvailabilityStatus.Available;
        }
    }

    private void Update()
    {
        if (availabilityWindow.activeSelf && Input.GetMouseButtonDown(0) && !b)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                availabilityWindow.GetComponent<RectTransform>(),
                Input.mousePosition,
                null))
            {
                CloseAvailabilityWindow();
            }
        }
    }

    private void ToggleAvailabilityWindow()
    {
        if (availabilityWindow.activeSelf)
        {
            CloseAvailabilityWindow();
        }
        else
        {
            OpenAvailabilityWindow();
        }
    }
    private void OpenAvailabilityWindow()
    {
        availabilityWindow.SetActive(true);
        StartCoroutine(FadeWindow(0f, 1f));

        // Calculate position
        if (!b)
        {
            RectTransform profileRect = transform.parent.GetComponent<RectTransform>();
            RectTransform windowRect = availabilityWindow.GetComponent<RectTransform>();

            Vector3 profileWorldPos = profileRect.transform.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.parent.GetComponent<RectTransform>(), profileWorldPos, null, out Vector2 localPoint);

            float xOffset = 400;
            float yPosition = localPoint.y;

            windowRect.anchoredPosition = new Vector2(xOffset, yPosition);
        }
    }

    private void CloseAvailabilityWindow()
    {
        StartCoroutine(FadeWindow(1f, 0f));
    }

    private IEnumerator FadeWindow(float startAlpha, float endAlpha)
    {
        float elapsedTime = 0f;
        while (elapsedTime < windowFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / windowFadeDuration;
            windowCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        windowCanvasGroup.alpha = endAlpha;
        if (endAlpha == 0f)
        {
            availabilityWindow.SetActive(false);
        }
    }

    private void SetStatus(AvailabilityStatus status)
    {
        if (!photonView.IsMine) return;

        currentStatus = status;
        UpdateStatusVisuals();
        SaveStatus();

        // Update the PlayerController using the player's actor number
        photonView.RPC("UpdateAvailabilityStatusRPC", RpcTarget.AllBuffered, (int)status, PhotonNetwork.LocalPlayer.ActorNumber); // Pass actor number

        // Update all open profiles
        PlayerProfile[] openProfiles = FindObjectsOfType<PlayerProfile>();
        foreach (PlayerProfile profile in openProfiles)
        {
            if (profile.photonView.Owner == PhotonNetwork.LocalPlayer)
            {
                profile.UpdateAvailabilityStatus(currentStatus);
            }
        }
    }

    [PunRPC]
    private void UpdateAvailabilityStatusRPC(int statusInt, int playerActorNumber) // Add playerActorNumber
    {
        // Find the PlayerController for the specified player
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in players)
        {
            if (player.view.OwnerActorNr == playerActorNumber)
            {
                AvailabilityManager.AvailabilityStatus status = (AvailabilityManager.AvailabilityStatus)statusInt;
                player.SetAvailabilityStatus(status);
                break; // Important: Exit the loop once the player is found
            }
        }
    }

    private void SaveStatus()
    {
        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable { { "AvailabilityStatus", (int)currentStatus } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
    }

    private void UpdateStatusVisuals()
    {
        Color statusColor;
        string statusString;

        switch (currentStatus)
        {
            case AvailabilityStatus.Available:
                statusColor = availableColor;
                statusString = "Available";
                break;
            case AvailabilityStatus.Busy:
                statusColor = busyColor;
                statusString = "Busy";
                break;
            case AvailabilityStatus.DoNotDisturb:
                statusColor = doNotDisturbColor;
                statusString = "Do Not Disturb";
                break;
            default:
                statusColor = availableColor;
                statusString = "Available";
                break;
        }

        statusCircle.color = statusColor;
        statusText.text = statusString;

        // Update participant list
        Participants participantsList = FindObjectOfType<Participants>();
        if (participantsList != null)
        {
            participantsList.UpdateParticipantAvailability(PhotonNetwork.LocalPlayer, statusColor);
        }
    }


    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("AvailabilityStatus") && targetPlayer == PhotonNetwork.LocalPlayer)
        {
            currentStatus = (AvailabilityStatus)changedProps["AvailabilityStatus"];
            UpdateStatusVisuals();
        }
    }
}

