using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// LiberationSans에는 한글이 없다. 장비 칸 글자 같은 한글은 맑은 고딕 fallback으로 그린다.
/// </summary>
[InitializeOnLoad]
public static class KoreanFontFallbackSetup
{
    const string FontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MalgunGothic SDF.asset";
    const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    static KoreanFontFallbackSetup()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("Tools/Magiduck/Create Korean Font Fallback")]
    public static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        TMP_FontAsset korean = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (korean == null)
        {
            korean = CreateKoreanFont();
            if (korean == null)
                return;
        }

        bool changed = false;
        changed |= AddFallback(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath), korean);

        if (TMP_Settings.instance != null)
        {
            if (TMP_Settings.fallbackFontAssets == null)
                TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();

            changed |= AddFallback(TMP_Settings.fallbackFontAssets, korean, TMP_Settings.instance);
        }

        if (changed)
            Debug.Log("한글이 없는 폰트에 맑은 고딕 fallback을 넣었습니다. 장비 칸 글자가 네모로 나오지 않습니다.", korean);
    }

    static TMP_FontAsset CreateKoreanFont()
    {
        TMP_FontAsset korean = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
        if (korean == null)
        {
            Debug.LogWarning("맑은 고딕(Malgun Gothic)을 찾지 못했습니다. 장비 칸 한글이 네모로 나옵니다.");
            return null;
        }

        korean.name = "MalgunGothic SDF";
        AssetDatabase.CreateAsset(korean, FontAssetPath);

        Texture2D[] atlases = korean.atlasTextures;
        if (atlases != null)
        {
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] != null)
                    AssetDatabase.AddObjectToAsset(atlases[i], korean);
            }
        }

        if (korean.material != null)
            AssetDatabase.AddObjectToAsset(korean.material, korean);

        EditorUtility.SetDirty(korean);
        AssetDatabase.SaveAssetIfDirty(korean);
        return korean;
    }

    static bool AddFallback(TMP_FontAsset font, TMP_FontAsset korean)
    {
        if (font == null)
            return false;

        if (font.fallbackFontAssetTable == null)
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();

        return AddFallback(font.fallbackFontAssetTable, korean, font);
    }

    static bool AddFallback(List<TMP_FontAsset> fallbacks, TMP_FontAsset korean, Object owner)
    {
        if (owner == null || korean == null)
            return false;

        if (fallbacks == null)
            return false;

        if (fallbacks.Contains(korean))
            return false;

        fallbacks.Add(korean);
        EditorUtility.SetDirty(owner);
        AssetDatabase.SaveAssetIfDirty(owner);
        return true;
    }
}
