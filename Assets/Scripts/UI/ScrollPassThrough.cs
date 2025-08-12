using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ScrollPassThrough : MonoBehaviour
{
    private CanvasGroup targetCanvasGroup;
    private float scrollCooldown = 0.2f;
    private float lastScrollTime;

    private void Start()
    {
        targetCanvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        if (Input.mouseScrollDelta.y != 0)
        {
            targetCanvasGroup.blocksRaycasts = false;
            targetCanvasGroup.interactable = false;
            lastScrollTime = Time.time;
        }

        if (Time.time > lastScrollTime + scrollCooldown)
        {
            targetCanvasGroup.blocksRaycasts = true;
            targetCanvasGroup.interactable = true;
        }
    }
}
