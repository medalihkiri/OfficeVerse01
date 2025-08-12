using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows;

public class ParagraphControle : MonoBehaviour
{
    public RectTransform input;
    public CanvasGroup scrollCG;
    public TextMeshProUGUI showText;
    public List<Image> viewPort;
    public Mask mask;
    public RectTransform trns;
    public RectTransform trns2;
    public CanvasGroup scroll;
    public float maxSize = 20;
    public float maxSize2 = 20;
    public float maxSize3 = 20;
    public float fadeSpeed = 10f;
    void Start()
    {
        input = GetComponent<RectTransform>();
    }

    bool b = false;
    void Update()
    {
        if (input.sizeDelta.y > maxSize)
        {
            viewPort.ForEach(vp => vp.enabled = true);
            mask.showMaskGraphic = true;
            if (b == false)
            {
                if (trns)
                {
                    trns2.gameObject.SetActive(true);
                    trns.gameObject.SetActive(false);
                    /*trns.SetParent(transform.parent);
                    trns.offsetMax = new Vector2(trns.offsetMax.x, -1.35f);*/
                }
                b = true;
            }
        }
        else
        {
            viewPort.ForEach(vp => vp.enabled = false);
            mask.showMaskGraphic = false;
            if (b) { b = false; if (trns)
                {
                    trns.gameObject.SetActive(true);
                    trns2.gameObject.SetActive(false);
                    /*trns.SetParent(transform);
                    trns.offsetMax = new Vector2(trns.offsetMax.x, 0);
                    trns.sizeDelta = new Vector2(trns.sizeDelta.x, 14);*/

                }
            }
        }
    }

    public void OnValueChange()
    {
        if (transform.GetComponent<TMP_InputField>().text.Length > 224)
        {
            scrollCG.gameObject.SetActive(true);
        }
        else
        {
            scrollCG.gameObject.SetActive(false);
        }
    }
    
    IEnumerator show()
    {
        yield return new WaitForSeconds(0.5f);
        scroll.alpha = 1f;
    }
}
