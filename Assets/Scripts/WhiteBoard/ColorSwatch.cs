using UnityEngine;
using UnityEngine.UI;


public class ColorSwatch : MonoBehaviour
{
    public Color color = Color.black;
    public WhiteboardUIManager uiManager;
    public Image previewImage;

    void Start()
    {
        if (previewImage != null) previewImage.color = color;
    }

    public void OnClick()
    {
        if (uiManager != null) uiManager.PickColor(color);
    }
}
