using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Events;

public class PlayerInfo : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject indicator;
    public GameObject parentIndicator;
    public GameObject mainPlayerProfile;
    public UnityAction waveBtnAction;
    public UnityAction messageBtnAction;
    public UnityAction requestBtnAction;

    public Player player;
    [HideInInspector]
    public bool isLocalPlayer;

    [Header("Fetch")]
    public TextMeshProUGUI playerName;
    public Image playerDot;
    public SpriteRenderer playerSprite;
    public Sprite[] playerSprites;

    PlayerController playerController;

    private void Start()
    {
        playerController = GetComponent<PlayerController>();
    }

    public void Show()
    {
        bool isMainPlayer = player == PhotonNetwork.LocalPlayer;
        if (parentIndicator != null) parentIndicator.SetActive(false);

        if (playerInfoUI == null)
            playerInfoUI = Object.FindObjectOfType<PlayerInfoUI>();

        if (playerInfoUI == null)
        {
            Debug.LogError("PlayerInfoUI not found in the scene.");
            return;
        }

        try { playerInfoUI.playerInfo = gameObject.GetComponent<PlayerInfo>(); }
        catch { Debug.LogError("Error getting PlayerInfo component"); }

        playerInfoUI.playerName = playerName;
        playerInfoUI.playerDot = playerDot;
        playerInfoUI.playerSprite = playerSprite;
        playerInfoUI.playerController = playerController;
        playerInfoUI.playerSprites = playerSprites;

        if (isMainPlayer)
        {
            if (playerInfoUI.mainPlayerProfile == null)
            {
                if (mainPlayerProfile != null)
                {
                    mainPlayerProfile.transform.SetParent(playerInfoUI.transform);
                    playerInfoUI.mainPlayerProfile = mainPlayerProfile.GetComponent<PlayerProfile>();
                    MatchTransforms(playerInfoUI);

                    var availManager = playerInfoUI.mainPlayerProfile.GetComponent<AvailabilityManager>();
                    if (availManager != null && availManager.closeBtn != null)
                        availManager.closeBtn.onClick.AddListener(() => playerInfoUI.CloseBtn());
                }
            }

            PlayerProfile profile = playerInfoUI.mainPlayerProfile;
            if (profile != null)
            {
                profile.SetProfilePlayer(player);

                int avatarIndex = isMainPlayer ? PlayerDataManager.PlayerAvatar : (int)player.CustomProperties["avatar"];

                Sprite safeSprite = null;
                if (playerInfoUI.playerSprites != null && playerInfoUI.playerSprites.Length > 0)
                {
                    if (avatarIndex >= 0 && avatarIndex < playerInfoUI.playerSprites.Length)
                    {
                        safeSprite = playerInfoUI.playerSprites[avatarIndex];
                    }
                    else
                    {
                        Debug.LogWarning($"Avatar index {avatarIndex} out of bounds. Defaulting to 0.");
                        safeSprite = playerInfoUI.playerSprites[0];
                    }
                }

                string safeName = string.IsNullOrEmpty(player.NickName) ? "Guest" : player.NickName;

                profile.SetupProfile(safeSprite, safeName, isMainPlayer);

                if (player.CustomProperties.TryGetValue("AvailabilityStatus", out object status))
                {
                    profile.UpdateAvailabilityStatus((AvailabilityManager.AvailabilityStatus)status);
                }
            }

            playerInfoUI.ShowMainPlayerInfo(localPoint);
        }
        else
        {
            playerInfoUI.ShowPlayerInfo(localPoint);
        }
    }

    public void MatchTransforms(PlayerInfoUI playerInfoUI)
    {
        if (playerInfoUI.mainPlayerProfile == null || playerInfoUI.posMainPlayerProfile == null) return;

        RectTransform targetRectTransform = playerInfoUI.mainPlayerProfile.GetComponent<RectTransform>();
        RectTransform sourceRectTransform = playerInfoUI.posMainPlayerProfile;

        targetRectTransform.position = sourceRectTransform.position;
        targetRectTransform.rotation = sourceRectTransform.rotation;
        targetRectTransform.sizeDelta = sourceRectTransform.sizeDelta;
        targetRectTransform.anchorMin = sourceRectTransform.anchorMin;
        targetRectTransform.anchorMax = sourceRectTransform.anchorMax;
        targetRectTransform.pivot = sourceRectTransform.pivot;
        targetRectTransform.anchoredPosition = sourceRectTransform.anchoredPosition;
        targetRectTransform.localScale = sourceRectTransform.localScale;
    }

    Vector2 localPoint;
    PlayerInfoUI playerInfoUI;

    public void OnPointerClick(PointerEventData eventData)
    {
        // --- MODIFICATION START ---
        // Only allow Left Click to trigger the profile
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        // --- MODIFICATION END ---

        if (playerInfoUI == null)
            playerInfoUI = Object.FindObjectOfType<PlayerInfoUI>();

        if (playerInfoUI != null && playerInfoUI.canvas != null)
        {
            Camera cam = playerInfoUI.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : playerInfoUI.canvas.worldCamera;
            RectTransform parentRect = playerInfoUI.GetComponent<RectTransform>();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                cam,
                out localPoint
            );

            Show();
        }
        else
        {
            Debug.LogError("PlayerInfoUI or Canvas is missing.");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (indicator == null)
            return;
        indicator.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (indicator == null)
            return;
        indicator.SetActive(false);
    }

    public void CloseBtn()
    {
        if (parentIndicator != null)
            parentIndicator.SetActive(true);
    }
}