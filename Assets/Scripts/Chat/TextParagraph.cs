using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextParagraph : MonoBehaviour
{
    public TextMeshProUGUI senderText;
    public Image avatarImage;
    public TMP_InputField contentText;
    public TextMeshProUGUI timeText;

    public RectTransform message;
    public Transform parent;
    public RectTransform bg;

    public List<CanvasGroup> cg;

    public int dist;

    private void Start()
    {
        UpdateTransform();
    }

    public void UpdateTransform()
    {
        dist = (int)(message.rect.height / 27.93f) - 1;
        if (dist > 1)
        {
            bg.sizeDelta = new Vector2(bg.sizeDelta.x, bg.sizeDelta.y + (dist * 28));
            parent.localPosition = new Vector3(parent.localPosition.x, parent.localPosition.y + (dist * 14), parent.localPosition.z);
        }
        print("thankyou");
    }
}
