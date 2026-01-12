// FILE: Assets/Scripts/BroadcastZone.cs
using UnityEngine;
using Photon.Pun;

/// <summary>
/// A trigger zone that automatically puts a player into broadcast mode upon entering
/// and removes them from broadcast mode upon exiting.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BroadcastZone : MonoBehaviour
{
    private void Awake()
    {
        // Ensure the collider is set to be a trigger.
        var col = GetComponent<Collider2D>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"Collider on {gameObject.name} is not set to 'Is Trigger'. Forcing it for BroadcastZone to work.", this);
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object that entered is a player and if it's the local player.
        if (other.TryGetComponent(out PlayerController player) && player.view.IsMine)
        {
            Debug.Log("[BroadcastZone] Local player entered, starting broadcast.");
            player.SetBroadcastState(true);

            // Optional: Update UI button state if you have one
            var uiControls = FindObjectOfType<UserControlsUI>();
            if (uiControls != null) uiControls.BroadcastEnabled = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Check if the object that exited is a player and if it's the local player.
        if (other.TryGetComponent(out PlayerController player) && player.view.IsMine)
        {
            Debug.Log("[BroadcastZone] Local player exited, stopping broadcast.");
            player.SetBroadcastState(false);

            // Optional: Update UI button state if you have one
            var uiControls = FindObjectOfType<UserControlsUI>();
            if (uiControls != null) uiControls.BroadcastEnabled = false;
        }
    }
}