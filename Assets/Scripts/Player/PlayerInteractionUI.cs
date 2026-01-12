using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

public class PlayerInteractionUI : MonoBehaviour
{
    [Header("UI References")]
    public Button followButton;

    [Header("Settings")]
    public Vector3 offset = new Vector3(0, 2.5f, 0); // Adjust this to sit above head

    private static PlayerInteractionUI currentlyActiveUI; // Track the single active menu
    private PhotonView view;

    private void Awake()
    {
        view = GetComponent<PhotonView>();

        if (followButton != null)
        {
            followButton.onClick.AddListener(OnFollowButtonClicked);
            followButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Optional: Billboard effect (make button always face camera)
        /*if (followButton.gameObject.activeSelf)
        {
            followButton.transform.rotation = Camera.main.transform.rotation;
        }*/
    }

    public void ShowInteractionOptions()
    {
        // If we are clicking ourselves, do nothing
        if (view.IsMine) return;

        // Hide any other open menus
        /**if (currentlyActiveUI != null && currentlyActiveUI != this)
        {
            currentlyActiveUI.HideInteractionOptions();
        }*/

        currentlyActiveUI = this;

        if (followButton != null)
        {
            followButton.gameObject.SetActive(true);
        }
    }

    public void HideInteractionOptions()
    {
        if (followButton != null)
        {
            followButton.gameObject.SetActive(false);
        }

        if (currentlyActiveUI == this)
        {
            currentlyActiveUI = null;
        }
    }

    private void OnFollowButtonClicked()
    {
        // 1. Get Local Player Controller
        if (GameManager.Instance != null && GameManager.Instance.myPlayer != null)
        {
            Debug.Log($"[Interaction] Requesting Local Player to follow: {this.gameObject.name}");

            // 2. Command Local Player to start following THIS transform
            GameManager.Instance.myPlayer.StartFollowing(this.transform);
        }
        else
        {
            Debug.LogError("GameManager or Local Player not found!");
        }

        // 3. Hide the button after clicking
        HideInteractionOptions();
    }

    // Static helper to hide whatever menu is open (used by PlayerController)
    public static void HideAll()
    {
        if (currentlyActiveUI != null)
        {
            currentlyActiveUI.HideInteractionOptions();
        }
    }
}