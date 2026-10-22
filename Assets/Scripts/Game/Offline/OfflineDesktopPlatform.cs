using UnityEngine;

namespace ProjectLucid.Offline
{
    // Offline hooks that exist only for the Apple Arcade launch (the splash video) apply on every
    // desktop target, macOS included (owner decision D7, 2026-10-22), in the player and the editor.
    public static class OfflineDesktopPlatform
    {
        public static bool IsDesktop(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxEditor:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsCurrent => IsDesktop(Application.platform);
    }
}
