using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InputParagraph : MonoBehaviour
{
    public RectTransform scroll;
    public ScrollRect scrollRect;
    public RectTransform input;
    public TMP_InputField tmpInput;
    public TextMeshProUGUI textShow;
    float distance = 0f;
    float initiale = -10f;
    public float speed;

    void Start()
    {
        if (!scroll && !input)
            return;
        distance = scroll.rect.height - input.rect.height;
        initiale = input.rect.height;

        if (GetComponent<EventTrigger>()) { GetComponent<EventTrigger>().enabled = false; }
    }

    public void Update()
    {
        if (!scroll && !input)
            return;
        if (input.rect.height > 260)
            return;

        /*if (input.rect.height != initiale)
        {
            textShow.alpha = 0f;
            StartCoroutine(Show());
        }*/
        scroll.sizeDelta = Vector2.Lerp(scroll.sizeDelta, new Vector2(scroll.sizeDelta.x, input.rect.height + distance), Time.deltaTime * speed);
    }

    IEnumerator Show()
    {
        yield return new WaitForSeconds(0.001f);
        textShow.alpha = 1f;
        initiale = input.rect.height;
    }

    public void DownScroll()
    {
        if (scroll.rect.height < 260)
            return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
