// --- NotepadShareUI.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Michsky.DreamOS;
using System.Collections.Generic;

public class NotepadShareUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject sharePanel;
    public TMP_InputField usernameInputField;
    public TMP_Dropdown permissionsDropdown;
    public Button confirmShareButton;
    public Button closeButton;
    public Transform userListParent;
    public GameObject userListItemPrefab; // Prefab for displaying a shared user

    [Header("Dependencies")]
    [Tooltip("Assign the NotepadStoring object from your scene.")]
    public NotepadStoring notepadStoring;
    private NotepadLibrary.NoteItem currentNote;

    void Start()
    {
        confirmShareButton.onClick.AddListener(OnConfirmShare);
        closeButton.onClick.AddListener(ClosePanel);
        sharePanel.SetActive(false);
    }

    public void OpenPanel(NotepadLibrary.NoteItem note)
    {
        currentNote = note;
        usernameInputField.text = "";

        // CRITICAL: Only the owner can manage sharing.
        bool isOwner = (currentNote.ownerId == APIManager.Instance.userId);
        usernameInputField.interactable = isOwner;
        permissionsDropdown.interactable = isOwner;
        confirmShareButton.interactable = isOwner;

        RefreshUserList(isOwner);
        sharePanel.SetActive(true);
    }

    public void ClosePanel()
    {
        sharePanel.SetActive(false);
    }

    private void RefreshUserList(bool isOwner)
    {
        // Clear the previous list
        foreach (Transform child in userListParent) { Destroy(child.gameObject); }

        // Populate the list with users this note is shared with
        foreach (var user in currentNote.sharedWith)
        {
            GameObject itemGO = Instantiate(userListItemPrefab, userListParent);
            var itemUI = itemGO.GetComponent<NotepadShareUserItemUI>(); // Get the helper script
            if (itemUI != null)
            {
                itemUI.Setup(user, isOwner,
                    // Action for changing permission
                    (newPermission) => { OnChangePermission(user.userId, newPermission); },
                    // Action for removing user
                    () => { OnRemoveUser(user.userId); }
                );
            }
        }
    }

    private void OnConfirmShare()
    {
        // NOTE: This requires a backend endpoint to find a user's ID by their username.
        // For now, this is a placeholder. You would first call e.g., GET /api/users/find/UserB
        string username = usernameInputField.text;
        if (string.IsNullOrEmpty(username)) return;

        Debug.LogWarning($"Feature Required: Need to implement API call to get user ID for username '{username}'.");
        string targetUserId = "PLACEHOLDER_USER_ID_FROM_USERNAME_LOOKUP";

        string permission = permissionsDropdown.value == 0 ? "readonly" : "editable";

        notepadStoring.ShareNoteOnServer(currentNote._id, targetUserId, permission, (success) =>
        {
            if (success)
            {
                Debug.Log("Share successful!");
                // To see the change, the owner would need to re-fetch the note data.
                // For a smooth UX, the server should return the updated note object.
                ClosePanel();
            }
        });
    }

    private void OnChangePermission(string targetUserId, string newPermission)
    {
        notepadStoring.ShareNoteOnServer(currentNote._id, targetUserId, newPermission, (success) => {
            if (success)
            {
                Debug.Log($"Permission for {targetUserId} updated to {newPermission}");
                // Update local data to reflect the change immediately
                var user = currentNote.sharedWith.Find(u => u.userId == targetUserId);
                if (user != null) user.permission = newPermission;
            }
        });
    }

    private void OnRemoveUser(string targetUserId)
    {
        notepadStoring.StopSharingNoteOnServer(currentNote._id, targetUserId, (success) =>
        {
            if (success)
            {
                Debug.Log($"Stopped sharing with {targetUserId}");
                // Remove user from local list and refresh UI
                currentNote.sharedWith.RemoveAll(u => u.userId == targetUserId);
                RefreshUserList(true); // isOwner must be true to get here
            }
        });
    }
}