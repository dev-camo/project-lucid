// Preserved Sonic Dream Team 1.10.1, HLUnityCore.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// 0x060004fc 0x1ad1ef8, 0x1ace0c0
// 0x060004fd 0x1ad1f88, 0x1ace150
// 0x060004fe 0x1ad1fec, 0x1ace1b0
// 0x060004ff 0x1ad2050, 0x1ace210
// 0x06000500 0x1ad20b4, 0x1ace270
// 0x06000501 0x1ad22bc, 0x1ace480
// 0x06000502 0x1ad2320, 0x1ace4e0
// 0x06000503 0x1ad2384, 0x1ace540
// 0x06000504 0x1ad23e8, 0x1ace5a0
// 0x06000505 0x1ad244c, 0x1ace600
// 0x06000506 0x1ad24b0, 0x1ace660
// 0x06000507 0x1ad2518, 0x1ace6c0
// 0x06000508 0x1ad2580, 0x1ace720
// 0x06000509 0x1ad25e8, 0x1ace780
// 0x0600050a 0x1ad2650, 0x1ace7e0
// 0x0600050b 0x1ad26b8, 0x1ace840
// 0x0600050c 0x1ad2720, 0x1ace8a0
// 0x0600050d 0x1ad2798, 0x1ace910
// 0x0600050e 0x1ad2800, 0x1ace970
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class GameCenterAccessPoint
    {
        // Original nested owner 0x020000ed; literal values and order are preserved.
        public enum AccessPointLocation
        {
            TopLeading = 0,
            TopTrailing = 1,
            BottomLeading = 2,
            BottomTrailing = 3
        }

        private static readonly GameCenterAccessPointPlugin s_gameCenterAccessPointPlugin;

        // Original 0x060004fc; dual CPU body evidence retained.
        static GameCenterAccessPoint()
        {
#if PROJECT_LUCID_ORIGINAL_GAME_CENTER_ACCESS_POINT
            s_gameCenterAccessPointPlugin = new GameCenterAccessPointPluginMacOS();
#else
            // Owned offline selection; every original facade method remains intact.
            s_gameCenterAccessPointPlugin = new ProjectLucid.Offline.LocalAccessPointPlugin();
#endif
        }

        // Original 0x060004fd; dual CPU body evidence retained.
        public static void ShowGameCenter()
        {
            s_gameCenterAccessPointPlugin.Show();
        }

        // Original 0x060004fe; dual CPU body evidence retained.
        public static void ShowAchievements()
        {
            s_gameCenterAccessPointPlugin.ShowAchievements();
        }

        // Original 0x060004ff; dual CPU body evidence retained.
        public static bool IsAccessPointAvailable()
        {
            return s_gameCenterAccessPointPlugin.IsAccessPointAvailable();
        }

        // Original 0x06000500; dual CPU body evidence retained.
        public static void ShowAccessPoint(AccessPointLocation accessPointLocation, bool showHighlights)
        {
            // The original Arabic invalid-enum branch logs and dispatches twice.
            // This switch is a source reconstruction of both physical [1,0,3,2] tables.
            if (Application.systemLanguage == SystemLanguage.Arabic)
            {
                switch (accessPointLocation)
                {
                    case AccessPointLocation.TopLeading: accessPointLocation = AccessPointLocation.TopTrailing; break;
                    case AccessPointLocation.TopTrailing: accessPointLocation = AccessPointLocation.TopLeading; break;
                    case AccessPointLocation.BottomLeading: accessPointLocation = AccessPointLocation.BottomTrailing; break;
                    case AccessPointLocation.BottomTrailing: accessPointLocation = AccessPointLocation.BottomLeading; break;
                    default:
                        HLOutput.LogError(string.Format("GameCenterAccessPoint is missing a switch case for {0}. Please extend the switch.", accessPointLocation), null);
                        s_gameCenterAccessPointPlugin.ShowAccessPoint((int)accessPointLocation, showHighlights);
                        accessPointLocation = AccessPointLocation.TopTrailing;
                        break;
                }
            }
            s_gameCenterAccessPointPlugin.ShowAccessPoint((int)accessPointLocation, showHighlights);
        }

        // Original 0x06000501; dual CPU body evidence retained.
        public static bool IsAccessPointShown()
        {
            return s_gameCenterAccessPointPlugin.IsAccessPointShown();
        }

        // Original 0x06000502; dual CPU body evidence retained.
        public static void HideAccessPoint()
        {
            s_gameCenterAccessPointPlugin.HideAccessPoint();
        }

        // Original 0x06000503; dual CPU body evidence retained.
        public static float GetAccessPointOriginX()
        {
            return s_gameCenterAccessPointPlugin.GetAccessPointOriginX();
        }

        // Original 0x06000504; dual CPU body evidence retained.
        public static float GetAccessPointOriginScreenCoordinateX()
        {
            return s_gameCenterAccessPointPlugin.GetAccessPointScreenCoordinateX();
        }

        // Original 0x06000505; dual CPU body evidence retained.
        public static float GetAccessPointOriginY()
        {
            return s_gameCenterAccessPointPlugin.GetAccessPointOriginY();
        }

        // Original 0x06000506; dual CPU body evidence retained.
        public static float GetAccessPointScreenCoordinateY()
        {
            return s_gameCenterAccessPointPlugin.GetAccessPointScreenCoordinateY();
        }

        // Original 0x06000507; dual CPU body evidence retained.
        public static float GetAccessPointSizeHeight()
        {
            return s_gameCenterAccessPointPlugin.GetAccessPointSizeHeight();
        }

        // Original 0x06000508; dual CPU body evidence retained.
        public static float GetAccessPointSizeScreenCoordinateHeight()
        {
            return s_gameCenterAccessPointPlugin.GetAccessPointSizeScreenCoordinateHeight();
        }

        // Original 0x06000509; dual CPU body evidence retained.
        public static float GetAccessPointSizeWidth()
        {
            return s_gameCenterAccessPointPlugin.GetAccessPointSizeWidth();
        }

        // Original 0x0600050a; dual CPU body evidence retained.
        public static float GetAccessPointSizeScreenCoordinateWidth()
        {
            // Both original bodies dispatch slot 16, preserving the width forwarding quirk.
            return s_gameCenterAccessPointPlugin.GetAccessPointSizeWidth();
        }

        // Original 0x0600050b; dual CPU body evidence retained.
        public static bool IsPresentingGameCenter()
        {
            return s_gameCenterAccessPointPlugin.IsPresentingGameCenter();
        }

        // Original 0x0600050c; dual CPU body evidence retained.
        public static void SetAccessPointFocused(bool focused)
        {
            s_gameCenterAccessPointPlugin.SetAccessPointFocused(focused);
        }

        // Original 0x0600050d; dual CPU body evidence retained.
        public static bool IsAccessPointFocused()
        {
            return s_gameCenterAccessPointPlugin.IsAccessPointFocused();
        }

        // Original 0x0600050e; dual CPU body evidence retained.
        public static void SetShowHighlights(bool showHighlights)
        {
            s_gameCenterAccessPointPlugin.SetShowHighlights(showHighlights);
        }

    }
}
