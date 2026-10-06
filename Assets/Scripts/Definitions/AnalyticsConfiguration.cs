using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "AnalyticsConfiguration", menuName = "HardlightProject/DefinitionData/Definitions/AnalyticsConfiguration")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class AnalyticsConfiguration : ScriptableObject
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public sealed class AnalyticsBand
        {
            [SerializeField] private float m_minimum;
            [SerializeField] private float m_maximum;
            public float Minimum => m_minimum;
            public float Maximum => m_maximum;
        }

        [SerializeField] private AnalyticsBand[] m_blueCoinsBands;
        [SerializeField] private AnalyticsBand[] m_rewardTrackXPBands;
        [SerializeField] private UIModernEvent[] m_buttonsToTrack;

        // Original Game.Runtime 060019b9, ARM0x782178. Last upper bound is
        // checked before the first matching inclusive range; keep current culture.
        public string GetBlueCoinsBand(int realBlueCoinsTotal)
        {
            AnalyticsBand[] bands = m_blueCoinsBands;
            float maximum = bands[bands.Length - 1].Maximum;
            if (maximum <= realBlueCoinsTotal)
            {
                object maximumObject = maximum;
                return string.Format("{0},More", maximumObject);
            }
            foreach (AnalyticsBand band in bands)
            {
                float minimum = band.Minimum;
                if (minimum <= realBlueCoinsTotal && band.Maximum >= realBlueCoinsTotal)
                {
                    // Native first boxing uses the captured minimum, then reads
                    // the maximum again before its own boxing and formatting.
                    object minimumObject = minimum;
                    object maximumObject = band.Maximum;
                    return string.Format("{0},{1}", minimumObject, maximumObject);
                }
            }
            return "0,0";
        }

        // Original060019ba, ARM0x7822f8; same array capture and numeric order.
        public string GetXPBand(int xpTotal)
        {
            AnalyticsBand[] bands = m_rewardTrackXPBands;
            float maximum = bands[bands.Length - 1].Maximum;
            if (maximum <= xpTotal)
            {
                object maximumObject = maximum;
                return string.Format("{0},More", maximumObject);
            }
            foreach (AnalyticsBand band in bands)
            {
                float minimum = band.Minimum;
                if (minimum <= xpTotal && band.Maximum >= xpTotal)
                {
                    // Native first boxing uses the captured minimum, then reads
                    // the maximum again before its own boxing and formatting.
                    object minimumObject = minimum;
                    object maximumObject = band.Maximum;
                    return string.Format("{0},{1}", minimumObject, maximumObject);
                }
            }
            return "0,0";
        }

        // Original060019bb, ARM0x782478; captured array and original GUID equality.
        public bool IsButtonTracked(UIModernEvent buttonEvent)
        {
            UIModernEvent[] buttons = m_buttonsToTrack;
            foreach (UIModernEvent button in buttons)
                if (button == buttonEvent) return true;
            return false;
        }
        // Original060019bc and nested060019bf are genuine base-only constructors.
    }
}
