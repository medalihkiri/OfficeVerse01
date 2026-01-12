
using UnityEngine;
using UnityEditor;
using TMPro;

public static class FindBrokenTMPFonts
{
    [MenuItem("Tools/Find Broken Font Assets")]
    public static void FindBroken()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int count = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset == null) continue;

            bool broken = false;
            if (fontAsset.atlasTexture == null) broken = true;
            else
            {
                var tex = fontAsset.atlasTexture;
                if (tex.width <= 0 || tex.height <= 0) broken = true;
            }

            if (broken)
            {
                Debug.LogError($"BROKEN TMP Font Asset: {path} (atlas missing or size <= 0)", fontAsset);
                count++;
            }
        }

        if (count == 0) Debug.Log("No broken TMP Font Assets found.");
        else Debug.Log($"Found {count} broken TMP Font Asset(s). Check console messages.");
    }
}
