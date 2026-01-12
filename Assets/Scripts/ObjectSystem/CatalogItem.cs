// --- START OF FILE CatalogItem.cs ---
using UnityEngine;

/// <summary>
/// A ScriptableObject representing a single item that can be placed in the world.
/// Create instances via Assets > Create > Multiplayer Object > Catalog Item.
/// </summary>
[CreateAssetMenu(fileName = "NewCatalogItem", menuName = "Multiplayer Object/Catalog Item")]
public class CatalogItem : ScriptableObject
{
    [Tooltip("A unique identifier for this item.")]
    public string id = "unique-item-id";

    [Tooltip("The display name for the UI.")]
    public string displayName = "New Item";

    [Tooltip("The icon to show in the catalog UI.")]
    public Sprite icon;

    [Tooltip("The network prefab to be instantiated. Must be in a 'Resources' folder and have a PhotonView component.")]
    public GameObject networkPrefab;
}
// --- END OF FILE CatalogItem.cs ---