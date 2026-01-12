using UnityEngine;
using UnityEngine.UI;

public class CatalogManager : MonoBehaviour
{
    public GameObject catalogItemPrefab;
    public Transform catalogContent;

    public void AddCatalogItem(Sprite icon, GameObject objectPrefab)
    {
        GameObject catalogItem = Instantiate(catalogItemPrefab, catalogContent);

        Image itemIcon = catalogItem.transform.Find("Icon").GetComponent<Image>();
        itemIcon.sprite = icon;

        Button itemButton = catalogItem.GetComponent<Button>();
        itemButton.onClick.AddListener(() =>
        {
            Instantiate(objectPrefab);
        });
    }
}