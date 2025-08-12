using UnityEngine;
using UnityEngine.EventSystems;

public class ScreenShareClickHandler : MonoBehaviour
{
    public UserControlsUI userControlsUI;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left mouse button
        {
            if (userControlsUI.isMapViewActive && IsMouseOverScreenShare())
            {
                userControlsUI.OnMeetingViewButtonClicked();
            }
        }
    }

    public bool IsMouseOverScreenShare()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        Vector2 localMousePosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            Input.mousePosition,
            null,
            out localMousePosition))
        {
            return rectTransform.rect.Contains(localMousePosition);
        }
        return false;
    }
}
