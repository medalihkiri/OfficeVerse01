// FILE: Assets/Scripts/UI/BroadcastPanelUI.cs
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using TMPro;

/// <summary>
/// Manages a UI panel that displays a list of all players currently broadcasting.
/// </summary>
public class BroadcastPanelUI : MonoBehaviourPunCallbacks
{
    [Tooltip("The parent GameObject of the entire broadcast panel UI.")]
    public GameObject panelContainer;

    [Tooltip("A UI prefab to instantiate for each broadcaster. Must have a TextMeshProUGUI component in its children.")]
    public GameObject broadcasterInfoPrefab;

    [Tooltip("The transform where the broadcaster info prefabs will be instantiated.")]
    public Transform contentParent;

    private readonly Dictionary<Player, GameObject> _broadcasterUIElements = new Dictionary<Player, GameObject>();

    void Start()
    {
        // Initial check for any players who are already broadcasting when we join.
        RefreshBroadcasterList();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        // Ensure the list is fresh when this object is enabled.
        RefreshBroadcasterList();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // We only care about updates to the 'isBroadcasting' property.
        if (changedProps.ContainsKey(PlayerController.IS_BROADCASTING_PROP))
        {
            RefreshBroadcasterList();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // If a player who was broadcasting leaves, make sure to clean up their UI.
        if (_broadcasterUIElements.ContainsKey(otherPlayer))
        {
            RefreshBroadcasterList();
        }
    }

    private void RefreshBroadcasterList()
    {
        if (!this.enabled || !gameObject.activeInHierarchy) return;

        // Clear all existing UI elements
        foreach (var uiObject in _broadcasterUIElements.Values)
        {
            Destroy(uiObject);
        }
        _broadcasterUIElements.Clear();

        // Find all players who are currently broadcasting
        var currentBroadcasters = PhotonNetwork.PlayerList.Where(p =>
            p.CustomProperties.TryGetValue(PlayerController.IS_BROADCASTING_PROP, out object isBroadcasting) && (bool)isBroadcasting
        ).ToList();

        // Update the main panel visibility
        if (panelContainer != null)
        {
            panelContainer.SetActive(currentBroadcasters.Any());
        }

        // Create new UI elements for each broadcaster
        foreach (var player in currentBroadcasters)
        {
            if (broadcasterInfoPrefab != null && contentParent != null)
            {
                GameObject infoInstance = Instantiate(broadcasterInfoPrefab, contentParent);
                // Assuming the prefab has a TextMeshProUGUI component to show the name
                var nameText = infoInstance.GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = player.NickName;
                }
                _broadcasterUIElements.Add(player, infoInstance);
            }
        }
    }
}