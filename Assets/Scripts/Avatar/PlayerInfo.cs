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
    /*public GameObject playerInfoPanel;
    public GameObject playerHeader;*/
    public GameObject indicator;
    public GameObject parentIndicator;
    public GameObject mainPlayerProfile;
    public UnityAction waveBtnAction;
    public UnityAction messageBtnAction;
    public UnityAction requestBtnAction;
    /*public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerAvailabilityText;
    public Image playerIcon;
    public Image playerDotIcon;
    public Button waveBtn;
    public Button messageBtn;
    public Button requestBtn;
    public AudioSource waveClick;*/
    public Player player;
    [HideInInspector]
    public bool isLocalPlayer;

    //[Header("You")]
    //public GameObject mainPlayerProfile;

    [Header("Fetsh")]
    public TextMeshProUGUI playerName;
    public Image playerDot;
    public SpriteRenderer playerSprite;
    public Sprite[] playerSprites;

    //bool setUI = false;
    PlayerController playerController;

    private void Start()
    {
        playerController = GetComponent<PlayerController>();
    }
    public void Show()
    {
        /*if (player == null)
            return;
        if (playerInfoPanel == null || playerHeader == null)
            return;*/

        //setUI = true;
        //playerHeader.SetActive(false);
        bool isMainPlayer = player == PhotonNetwork.LocalPlayer;
        parentIndicator.SetActive(false);
        try { playerInfoUI.playerInfo = gameObject.GetComponent<PlayerInfo>(); }
        catch { Debug.LogError("Error"); }
        playerInfoUI.playerName = playerName;
        playerInfoUI.playerDot = playerDot;
        playerInfoUI.playerSprite = playerSprite;
        playerInfoUI.playerController = playerController;
        playerInfoUI.playerSprites = playerSprites;
        if (isMainPlayer)
        {
            if(playerInfoUI.mainPlayerProfile == null)
            {
                Debug.LogError("hi");
                mainPlayerProfile.transform.SetParent(playerInfoUI.transform);
                playerInfoUI.mainPlayerProfile = mainPlayerProfile.GetComponent<PlayerProfile>();
                MatchTransforms(playerInfoUI);
                playerInfoUI.mainPlayerProfile.GetComponent<AvailabilityManager>().closeBtn.onClick.AddListener(() => playerInfoUI.CloseBtn());
            }
            PlayerProfile profile = playerInfoUI.mainPlayerProfile;
            profile.SetProfilePlayer(player);
            int avatarIndex = isMainPlayer ? PlayerDataManager.PlayerAvatar : (int)player.CustomProperties["avatar"];
            profile.SetupProfile(playerInfoUI.playerSprites[avatarIndex], player.NickName, isMainPlayer);

            if (player.CustomProperties.TryGetValue("AvailabilityStatus", out object status))
            {
                profile.UpdateAvailabilityStatus((AvailabilityManager.AvailabilityStatus)status);
            }

            playerInfoUI.ShowMainPlayerInfo(localPoint);

            /*PlayerProfile profile = mainPlayerProfile.GetComponent<PlayerProfile>();
            profile.SetProfilePlayer(player);
            int avatarIndex = isMainPlayer ? PlayerDataManager.PlayerAvatar : (int)player.CustomProperties["avatar"];
            profile.SetupProfile(playerSprites[avatarIndex], player.NickName, isMainPlayer);

            if (player.CustomProperties.TryGetValue("AvailabilityStatus", out object status))
            {
                profile.UpdateAvailabilityStatus((AvailabilityManager.AvailabilityStatus)status);
            }

            mainPlayerProfile.SetActive(true);
            mainPlayerProfile.GetComponent<CanvasGroup>().alpha = 1f;
            playerInfoPanel.SetActive(false);*/
            //profile.Show();
        }
        else
        {
            //mainPlayerProfile.SetActive(false);
            //playerInfoPanel.SetActive(true);
            playerInfoUI.ShowPlayerInfo(localPoint);
        }
    }

    public void MatchTransforms(PlayerInfoUI playerInfoUI)
    {
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
        playerInfoUI = Object.FindAnyObjectByType<PlayerInfoUI>();
        Canvas canvas = playerInfoUI.canvas;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, eventData.position
            , canvas.worldCamera, out localPoint);
        Show();
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
        parentIndicator.SetActive(true);
    }

    /*void SetPlayerInfo()
    {
        if (playerNameText == null && playerAvailabilityText == null && playerIcon == null)
            return;

        try
        {
            int spriteIcon = int.Parse(playerSprite.sprite.name[9].ToString());
            playerIcon.sprite = playerSprites[spriteIcon - 1];
        }
        catch { Debug.LogError("Error When Parsing The PlayerIcon"); }

        playerNameText.text = playerName.text;
        playerDotIcon.color = playerDot.color;
        playerAvailabilityText.text = playerController.currentAvailability.ToString();

        if(playerController.currentAvailability == AvailabilityManager.AvailabilityStatus.DoNotDisturb)
            playerAvailabilityText.text = "Not Available";
    }

    private void Update()
    {
        if (setUI)
            SetPlayerInfo();
    }*/

    /*public void CloseBtn()
    {
        setUI = false;
        mainPlayerProfile.SetActive(false);
        playerInfoPanel.SetActive(false);
        //playerHeader.SetActive(true);
        parentIndicator.SetActive(true);
    }
    public void OnWaveButtonClick()
    {
        waveClick.Stop();
        waveClick.Play();
    }
    public void OnMessageButttonClick()
    {

    }
    public void OnRequestButtonClick()
    {
        
    }*/
}
