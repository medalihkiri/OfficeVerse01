using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInfoUI : MonoBehaviour
{
    [Header("PLayerInfoPanel")]
    public GameObject closePanel;
    public GameObject playerInfoPanel;
    public GameObject availabilityPanel;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerAvailabilityText;
    public Image playerIcon;
    public Image playerDotIcon;
    public Button waveBtn;
    public Button messageBtn;
    public Button requestBtn;
    public AudioSource waveClick;

    [Header("You")]
    public PlayerProfile mainPlayerProfile;
    public RectTransform posMainPlayerProfile;
    public Vector2 dist;

    [Header("Fetsh")]
    [HideInInspector] public PlayerInfo playerInfo;
    [HideInInspector] public TextMeshProUGUI playerName;
    [HideInInspector] public Image playerDot;
    [HideInInspector] public SpriteRenderer playerSprite;
    [HideInInspector] public PlayerController playerController;
    [HideInInspector] public Sprite[] playerSprites;
    public Canvas canvas;

    bool setUI = false;

    public void ShowPlayerInfo(Vector2 clickPoint)
    {
        closePanel.SetActive(true);
        playerInfoPanel.GetComponent<RectTransform>().anchoredPosition = clickPoint + dist;
        try { mainPlayerProfile.gameObject.SetActive(false); } catch { Debug.LogError("SameError"); }
        playerInfoPanel.SetActive(true);
        setUI = true;
        waveBtn.onClick.AddListener(() => playerInfo.waveBtnAction?.Invoke());
        messageBtn.onClick.AddListener(() => playerInfo.messageBtnAction?.Invoke());
        requestBtn.onClick.AddListener(() => playerInfo.requestBtnAction?.Invoke());
    }
    public void ShowMainPlayerInfo(Vector2 clickPoint)
    {
        closePanel.SetActive(true);
        mainPlayerProfile.GetComponent<RectTransform>().anchoredPosition = clickPoint + dist;
        try { mainPlayerProfile.gameObject.SetActive(true); } catch { Debug.LogError("SameError"); }
        mainPlayerProfile.GetComponent<CanvasGroup>().alpha = 1f;
        playerInfoPanel.SetActive(false);
    }

    void SetPlayerInfo()
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

        if (playerController.currentAvailability == AvailabilityManager.AvailabilityStatus.DoNotDisturb)
            playerAvailabilityText.text = "Not Available";
    }
    private void Update()
    {
        if (setUI)
            SetPlayerInfo();
    }

    public void CloseBtn()
    {
        try { setUI = false; } catch { }
        try { mainPlayerProfile.gameObject.SetActive(false); } catch { }
        try { playerInfoPanel.SetActive(false); } catch { }
        try { closePanel.SetActive(false); } catch { }
        try { availabilityPanel.SetActive(false); } catch { }
        try { playerInfo.CloseBtn(); } catch { }
    }
    public void OnWaveButtonClick()
    {
        NotificationManager.Instance.PlayWaveSound();
    }
    public void OnMessageButttonClick()
    {
        CloseBtn();
    }
    public void OnRequestButtonClick()
    {
        NotificationManager.Instance.PlayWaveSound();
    }

}
