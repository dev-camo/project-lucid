using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ScreenResolutionSetting : VisualQualityConfiguration
    {
        [SerializeField] private Vector2 m_resolution;

        // Game.Runtime 0x06002db3: the supplied player changes only OSXPlayer.
        // Display dimensions precede clamping; fullscreen mode is read afterward.
        public override void Apply()
        {
            if (Application.platform != RuntimePlatform.OSXPlayer)
                return;
            DisplayInfo info = Screen.mainWindowDisplayInfo;
            Vector2Int resolution = GetClampedResolution(new Vector2Int(info.width, info.height));
            Screen.SetResolution(resolution.x, resolution.y, Screen.fullScreenMode);
        }

        // 0x06002db4: only authored X constrains the display width; authored Y
        // is unused. Keep the original early-return comparisons: NaN reaches the
        // conversions. Exceptional float-to-int behavior differs across native/CLR
        // targets and remains a qualification gap; no safety clamp is invented.
        public Vector2Int GetClampedResolution(Vector2Int displayResolution)
        {
            float width = m_resolution.x;
            if (width <= 0f || width >= displayResolution.x)
                return displayResolution;
            float scale = width / displayResolution.x;
            return new Vector2Int(unchecked((int)width), unchecked((int)(displayResolution.y * scale)));
        }

        // 0x06002db5: no authored resolution initializer beyond the zero vector.
        public ScreenResolutionSetting() { }
    }
}
