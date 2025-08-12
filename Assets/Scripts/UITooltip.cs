using UnityEngine;
using UnityEngine.EventSystems;

public class UITooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipObject;

    public void OnPointerEnter(PointerEventData eventData)
    {
        tooltipObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipObject.SetActive(false);
    }
}