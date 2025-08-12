using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AutoWrapText : MonoBehaviour
{
    private TMP_InputField textMeshPro;
    public TextMeshProUGUI setText;
    private string lastText = "";

    private void Start()
    {
        textMeshPro = GetComponent<TMP_InputField>();
    }

    void Update()
    {
        if (!textMeshPro.text.EndsWith("\r\n"))
        {
            textMeshPro.text += "\r\n";
        }
    }
}
