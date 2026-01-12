// Assets/Editor/DiagnoseTMPPrebuild_Reflect.cs
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Linq;
using TMPro;

public static class DiagnoseTMPPrebuild
{
    [MenuItem("Tools/TMP/Diagnose Prebuild (Reflect)")]
    public static void RunDiagnosis()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int total = guids.Length;
        Debug.Log($"[DiagnoseTMPPrebuild_Reflect] Found {total} TMP_FontAsset(s).");

        int failed = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null) continue;

            try
            {
                // Log atlasTexture and atlasTextures info
                Texture2D main = fa.atlasTexture;
                if (main != null)
                    Debug.Log($"TMP Asset: {path} -> atlasTexture: {main.width}x{main.height}");
                else
                    Debug.Log($"TMP Asset: {path} -> atlasTexture: null");

                if (fa.atlasTextures != null && fa.atlasTextures.Length > 0)
                {
                    for (int i = 0; i < fa.atlasTextures.Length; ++i)
                    {
                        var t = fa.atlasTextures[i];
                        if (t != null) Debug.Log($"    atlasTextures[{i}]: {t.width}x{t.height}");
                        else Debug.Log($"    atlasTextures[{i}]: null");
                    }
                }

                // Use reflection to find likely internal "clear" methods used at build time.
                var faType = typeof(TMP_FontAsset);
                MethodInfo[] methods = faType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                // Find candidate methods containing 'ClearAtlas' or 'ClearFontAssetData' or 'ClearAtlasTextures'
                MethodInfo candidate = methods.FirstOrDefault(m =>
                    m.Name.IndexOf("ClearAtlas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("ClearFontAssetData", StringComparison.OrdinalIgnoreCase) >= 0
                );

                if (candidate == null)
                {
                    Debug.LogWarning($"[DiagnoseTMPPrebuild_Reflect] No internal ClearAtlas/ClearFontAssetData method found on TMP_FontAsset for {path}. Method list:");
                    foreach (var m in methods)
                    {
                        Debug.Log($"   {m.Name} (params: {string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                    }
                }
                else
                {
                    Debug.Log($"[DiagnoseTMPPrebuild_Reflect] Invoking internal method '{candidate.Name}' on {path} ...");

                    // Prepare arguments if method expects a bool (many TMP internal methods take a bool)
                    var parms = candidate.GetParameters();
                    object[] args = null;
                    if (parms.Length == 1 && parms[0].ParameterType == typeof(bool))
                        args = new object[] { true };
                    else if (parms.Length == 0)
                        args = new object[0];
                    else
                    {
                        // If signature unexpected, try to call with default values.
                        args = parms.Select(p => p.HasDefaultValue ? p.DefaultValue : GetDefault(p.ParameterType)).ToArray();
                    }

                    // Invoke
                    candidate.Invoke(fa, args);

                    // Re-import asset to restore editor state
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[DiagnoseTMPPrebuild_Reflect] Successfully invoked '{candidate.Name}' on {path}.");
                }
            }
            catch (Exception ex)
            {
                failed++;
                // If it's a TargetInvocationException, unwrap inner
                Exception toShow = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                Debug.LogError($"[DiagnoseTMPPrebuild_Reflect] Exception when invoking internal clear on {path}:\n{toShow}\n", fa);
            }
        }

        if (failed == 0) Debug.Log("[DiagnoseTMPPrebuild_Reflect] No exceptions thrown when invoking TMP internal clear methods.");
        else Debug.Log($"[DiagnoseTMPPrebuild_Reflect] {failed} asset(s) threw exceptions - check errors above.");
    }

    static object GetDefault(Type t)
    {
        if (t.IsValueType) return Activator.CreateInstance(t);
        return null;
    }
}
