// Preserved Sonic Dream Team 1.10.1, HLUnityCore.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// 0x06000522 0x1ad2880, 0x1ace9f0
// 0x06000523 0x1ad2884, 0x1acea00
// 0x06000524 0x1ad2888, 0x1acea10
// 0x06000525 0x1ad28a4, 0x1acea20
// 0x06000526 0x1ad28a8, 0x1acea30
// 0x06000527 0x1ad28c4, 0x1acea40
// 0x06000528 0x1ad28c8, 0x1acea50
// 0x06000529 0x1ad28cc, 0x1acea60
// 0x0600052a 0x1ad28d0, 0x1acea70
// 0x0600052b 0x1ad28d4, 0x1acea80
// 0x0600052c 0x1ad28d8, 0x1acea90
// 0x0600052d 0x1ad28dc, 0x1aceaa0
// 0x0600052e 0x1ad28e0, 0x1aceab0
// 0x0600052f 0x1ad28e4, 0x1aceac0
// 0x06000530 0x1ad28e8, 0x1acead0
// 0x06000531 0x1ad2904, 0x1aceae0
// 0x06000532 0x1ad2908, 0x1aceaf0
// 0x06000533 0x1ad290c, 0x1aceb00
// 0x06000534 0x1ad2910, 0x1aceb10
// 0x06000535 0x1ad292c, 0x1aceb20
// 0x06000536 0x1ad2938, 0x1aceb30
// 0x06000537 0x1ad2954, 0x1aceb40
// 0x06000538 0x1ad2958, 0x1aceb50
// 0x06000539 0x1ad295c, 0x1aceb60
// 0x0600053a 0x1ad2960, 0x1aceb70
// 0x0600053b 0x1ad2964, 0x1aceb80
// 0x0600053c 0x1ad2968, 0x1aceb90
// 0x0600053d 0x1ad296c, 0x1aceba0
// 0x0600053e 0x1ad2970, 0x1acebb0
// 0x0600053f 0x1ad2974, 0x1acebc0
// 0x06000540 0x1ad2978, 0x1acebd0
// 0x06000541 0x1ad2994, 0x1acebe0
// 0x06000542 0x1ad2998, 0x1acebf0
// 0x06000543 0x1ad29a0, 0x1acec00
// 0x06000544 0x1ad1f80, 0x1ace140
#if PROJECT_LUCID_ORIGINAL_GAME_CENTER_ACCESS_POINT
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
#error Original PInvoke library and import descriptors are not yet qualified; research source only.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameCenterAccessPointPluginMacOS : GameCenterAccessPointPlugin
    {
        // Original 0x06000522; dual CPU body evidence retained.
        private static extern void Unity_ShowGameCenter();

        // Original 0x06000523; dual CPU body evidence retained.
        private static extern void Unity_ShowAchievements();

        // Original 0x06000524; dual CPU body evidence retained.
        private static extern bool Unity_IsAccessPointAvailable();

        // Original 0x06000525; dual CPU body evidence retained.
        private static extern void Unity_ShowAccessPoint(int location, bool showHighlights);

        // Original 0x06000526; dual CPU body evidence retained.
        private static extern bool Unity_IsAccessPointShown();

        // Original 0x06000527; dual CPU body evidence retained.
        private static extern void Unity_HideAccessPoint();

        // Original 0x06000528; dual CPU body evidence retained.
        private static extern float Unity_GetAccessPointScreenCoordinateX();

        // Original 0x06000529; dual CPU body evidence retained.
        private static extern float Unity_GetAccessPointOriginX();

        // Original 0x0600052a; dual CPU body evidence retained.
        private static extern float Unity_GetAccessPointScreenCoordinateY();

        // Original 0x0600052b; dual CPU body evidence retained.
        private static extern float Unity_GetAccessPointOriginY();

        // Original 0x0600052c; dual CPU body evidence retained.
        private static extern float Unity_GetAccessPointScreenCoordinateHeight();

        // Original 0x0600052d; dual CPU body evidence retained.
        private static extern float Unity_GetAccessPointSizeHeight();

        // Original 0x0600052e; dual CPU body evidence retained.
        private static extern float Unity_GetAccessPointScreenCoordinateWidth();

        // Original 0x0600052f; dual CPU body evidence retained.
        private static extern float Unity_GetAccessPointSizeWidth();

        // Original 0x06000530; dual CPU body evidence retained.
        private static extern bool Unity_IsPresentingGameCenter();

        // Original 0x06000531; dual CPU body evidence retained.
        private static extern void Unity_SetShowHighlights(bool showHighlights);

        // Original 0x06000532; dual CPU body evidence retained.
        public override void Show()
        {
            Unity_ShowGameCenter();
        }

        // Original 0x06000533; dual CPU body evidence retained.
        public override void ShowAchievements()
        {
            Unity_ShowAchievements();
        }

        // Original 0x06000534; dual CPU body evidence retained.
        public override bool IsAccessPointAvailable()
        {
            return Unity_IsAccessPointAvailable();
        }

        // Original 0x06000535; dual CPU body evidence retained.
        public override void ShowAccessPoint(int location, bool showHighlights)
        {
            Unity_ShowAccessPoint(location, showHighlights);
        }

        // Original 0x06000536; dual CPU body evidence retained.
        public override bool IsAccessPointShown()
        {
            return Unity_IsAccessPointShown();
        }

        // Original 0x06000537; dual CPU body evidence retained.
        public override void HideAccessPoint()
        {
            Unity_HideAccessPoint();
        }

        // Original 0x06000538; dual CPU body evidence retained.
        public override float GetAccessPointScreenCoordinateX()
        {
            return Unity_GetAccessPointScreenCoordinateX();
        }

        // Original 0x06000539; dual CPU body evidence retained.
        public override float GetAccessPointOriginX()
        {
            return Unity_GetAccessPointOriginX();
        }

        // Original 0x0600053a; dual CPU body evidence retained.
        public override float GetAccessPointScreenCoordinateY()
        {
            return Unity_GetAccessPointScreenCoordinateY();
        }

        // Original 0x0600053b; dual CPU body evidence retained.
        public override float GetAccessPointOriginY()
        {
            return Unity_GetAccessPointOriginY();
        }

        // Original 0x0600053c; dual CPU body evidence retained.
        public override float GetAccessPointSizeScreenCoordinateHeight()
        {
            return Unity_GetAccessPointSizeScreenCoordinateHeight();
        }

        // Original 0x0600053d; dual CPU body evidence retained.
        public override float GetAccessPointSizeHeight()
        {
            return Unity_GetAccessPointSizeHeight();
        }

        // Original 0x0600053e; dual CPU body evidence retained.
        public override float GetAccessPointSizeScreenCoordinateWidth()
        {
            return Unity_GetAccessPointSizeScreenCoordinateWidth();
        }

        // Original 0x0600053f; dual CPU body evidence retained.
        public override float GetAccessPointSizeWidth()
        {
            return Unity_GetAccessPointSizeWidth();
        }

        // Original 0x06000540; dual CPU body evidence retained.
        public override bool IsPresentingGameCenter()
        {
            return Unity_IsPresentingGameCenter();
        }

        // Original 0x06000541; dual CPU body evidence retained.
        public override void SetAccessPointFocused(bool focused)
        {
            // Original body is empty.
        }

        // Original 0x06000542; dual CPU body evidence retained.
        public override bool IsAccessPointFocused()
        {
            return false;
        }

        // Original 0x06000543; dual CPU body evidence retained.
        public override void SetShowHighlights(bool showHighlights)
        {
            Unity_SetShowHighlights(showHighlights);
        }

        // Original 0x06000544; dual CPU body evidence retained.
        public GameCenterAccessPointPluginMacOS()
        {
        }

    }
}
#endif
