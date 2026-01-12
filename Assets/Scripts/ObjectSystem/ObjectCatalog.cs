// --- START OF FILE ObjectCatalog.cs ---
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A ScriptableObject that holds a list of all placeable CatalogItems.
/// Create one instance via Assets > Create > Multiplayer Object > Object Catalog.
/// </summary>
[CreateAssetMenu(fileName = "ObjectCatalog", menuName = "Multiplayer Object/Object Catalog")]
public class ObjectCatalog : ScriptableObject
{
    [Tooltip("The list of all items available for placement in the game.")]
    public List<CatalogItem> items;
}
// --- END OF FILE ObjectCatalog.cs ---