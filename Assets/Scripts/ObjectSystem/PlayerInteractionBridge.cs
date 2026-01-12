// --- START OF FILE PlayerInteractionBridge.cs ---
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A central hub for connecting UI events (like button clicks) to the gameplay systems.
/// Attach to a persistent manager GameObject.
/// </summary>
public class PlayerInteractionBridge : MonoBehaviour
{
    [Header("System Dependencies")]
    [Tooltip("Reference to the ObjectMoverTool script.")]
    [SerializeField] private ObjectMoverTool moverTool;
    [Tooltip("Reference to the ObjectCatalogUI script.")]
    [SerializeField] private ObjectCatalogUI catalogUI;

    void Start()
    {
        if (moverTool == null || catalogUI == null)
        {
            Debug.LogError("PlayerInteractionBridge is missing critical system references!");
        }
    }

    /// <summary>
    /// Called from a UI button to show or hide the object placement catalog.
    /// </summary>
    public void OnToggleCatalogClicked()
    {
        catalogUI?.Toggle();
    }

    /// <summary>
    /// Called from a UI button to enter 'delete mode'.
    /// </summary>
    public void OnActivateDeleteModeClicked()
    {
        moverTool?.SetDeleteMode(true);
    }
}
// --- END OF FILE PlayerInteractionBridge.cs ---