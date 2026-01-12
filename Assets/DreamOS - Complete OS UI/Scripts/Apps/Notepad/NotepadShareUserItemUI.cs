// --- NotepadShareUserItemUI.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // For Action<>
using Michsky.DreamOS;

public class NotepadShareUserItemUI : MonoBehaviour
{
    public TextMeshProUGUI usernameText;
    public TMP_Dropdown permissionDropdown;
    public Button removeButton;

    public void Setup(NotepadLibrary.SharedUser user, bool isOwner, Action<string> onPermissionChange, Action onRemove)
    {
        usernameText.text = user.username;

        permissionDropdown.value = (user.permission == "editable") ? 1 : 0;
        removeButton.onClick.AddListener(() => onRemove());

        // Add a listener that fires the callback when the dropdown value changes
        permissionDropdown.onValueChanged.AddListener((index) => {
            string newPermission = index == 0 ? "readonly" : "editable";
            onPermissionChange(newPermission);
        });

        // The owner can change permissions and remove users. A viewer cannot.
        permissionDropdown.interactable = isOwner;
        removeButton.interactable = isOwner;
    }
}