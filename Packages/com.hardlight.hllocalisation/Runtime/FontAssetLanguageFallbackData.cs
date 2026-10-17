using System;
using System.Collections.Generic;
using Hardlight.Enums;
using TMPro;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

// Original HLLocalisation.Runtime, global namespace. The property name typo
// "AllLangaugeFallBacksCollection" is original.
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[Il2CppSetOption(Option.NullChecks, false)]
[CreateAssetMenu(fileName = "FontAssetLanguageFallbackData", menuName = "Hardlight/Localisation/FontAsset Language Fallback Data", order = 0)]
public sealed class FontAssetLanguageFallbackData : ScriptableObject
{
    [Serializable]
    public class FontAssetFallbackPairing
    {
        public Languages m_language;
        public List<string> m_pathToFallBackFonts;
    }

    [Serializable]
    public class FontAssetAndFallBacks
    {
        public TMP_FontAsset m_fontAsset;
        public List<FontAssetFallbackPairing> m_fallbackCollection;
    }

    [Serializable]
    public class AllLanguageFallBacks
    {
        public TMP_FontAsset m_fontAsset;
        public List<string> m_pathToFallBackFonts;
    }

    [SerializeField]
    private List<FontAssetAndFallBacks> m_fontAssetsAndFallBackCollection;
    [SerializeField]
    private List<AllLanguageFallBacks> m_allLanguageFallBacks;

    // 06000001
    public List<FontAssetAndFallBacks> FontAssetsAndFallBackCollection => m_fontAssetsAndFallBackCollection;
    // 06000002
    public List<AllLanguageFallBacks> AllLangaugeFallBacksCollection => m_allLanguageFallBacks;
}
