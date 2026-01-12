// --- START OF FILE ObjectCatalogUI.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populates a UI list from an ObjectCatalog asset.
/// Attach to your UI manager canvas.
/// </summary>
public class ObjectCatalogUI : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The catalog asset containing all placeable items.")]
    [SerializeField] private ObjectCatalog catalog;
    [Tooltip("The UI button prefab to use for each catalog item. Must have an Image and a TMP_Text child.")]
    [SerializeField] private GameObject buttonPrefab;
    [Tooltip("The parent transform where catalog buttons will be instantiated.")]
    [SerializeField] private RectTransform listContainer;

    [Header("Dependencies")]
    [Tooltip("Reference to the GhostPlacer system.")]
    [SerializeField] private GhostPlacer ghostPlacer;

    void Start()
    {
        if (catalog == null || buttonPrefab == null || listContainer == null || ghostPlacer == null)
        {
            Debug.LogError("ObjectCatalogUI is missing required references. Please assign them in the inspector.");
            return;
        }
        Populate();
        gameObject.SetActive(true); 
    }

    private void Populate()
    {
        // Clear any existing buttons
        foreach (Transform child in listContainer)
        {
            Destroy(child.gameObject);
        }

        // Create a button for each item in the catalog
        foreach (var item in catalog.items)
        {
            GameObject btnGO = Instantiate(buttonPrefab, listContainer);

            var img = btnGO.transform.Find("Icon")?.GetComponent<Image>();
            var txt = btnGO.transform.Find("Text")?.GetComponent<TMP_Text>();

            if (img != null && item.icon != null) img.sprite = item.icon;
            if (txt != null) txt.text = item.displayName;

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() =>
                {
                    ghostPlacer.BeginPlacing(item);
                    //gameObject.SetActive(false); // Hide catalog while placing
                });
            }
        }
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
}
// --- END OF FILE ObjectCatalogUI.cs ---