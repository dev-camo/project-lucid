// Preserved Sonic Dream Team 1.10.1, HLUnityCore.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// 0x06000545 0x1ad29a8, 0x1acec10
// 0x06000546 0x1ad2a54, 0x1aceca0
// 0x06000547 0x1ad2ae4, 0x1aced00
// 0x06000548 0x1ad2b74, 0x1aced60
// 0x06000549 0x1ad2c04, 0x1acedc0
// 0x0600054a 0x1ad2c90, 0x1acee20
// 0x0600054b 0x1ad2d20, 0x1acee80
// 0x0600054c 0x1ad2dac, 0x1aceee0
// 0x0600054d 0x1ad2e3c, 0x1acef40
// 0x0600054e 0x1ad2ec8, 0x1acefb0
// 0x0600054f 0x1ad2f54, 0x1acf020
// 0x06000550 0x1ad2fe0, 0x1acf090
// 0x06000551 0x1ad306c, 0x1acf100
// 0x06000552 0x1ad30f8, 0x1acf170
// 0x06000553 0x1ad3184, 0x1acf1e0
// 0x06000554 0x1ad3210, 0x1acf250
// 0x06000555 0x1ad329c, 0x1acf2c0
// 0x06000556 0x1ad3328, 0x1acf320
// 0x06000557 0x1ad33b8, 0x1acf380
// 0x06000558 0x1ad3444, 0x1acf3e0
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenterAccessPointPluginUnsupported : GameCenterAccessPointPlugin
    {
        private readonly string m_unsupportedReason;
        private readonly bool m_warning;

        // Original 0x06000545; dual CPU body evidence retained.
        public GameCenterAccessPointPluginUnsupported(string unsupportedReason, bool warning)
        {
            m_unsupportedReason = unsupportedReason;
            m_warning = warning;
            Log();
        }

        // Original 0x06000546; dual CPU body evidence retained.
        private void Log()
        {
            // The warning flag suppresses logging in both original images.
            if (!m_warning) HLOutput.LogError(m_unsupportedReason, null);
        }

        // Original 0x06000547; dual CPU body evidence retained.
        public override void Show()
        {
            Log();
        }

        // Original 0x06000548; dual CPU body evidence retained.
        public override void ShowAchievements()
        {
            Log();
        }

        // Original 0x06000549; dual CPU body evidence retained.
        public override bool IsAccessPointAvailable()
        {
            Log();
            return false;
        }

        // Original 0x0600054a; dual CPU body evidence retained.
        public override void ShowAccessPoint(int location, bool showHightlights)
        {
            Log();
        }

        // Original 0x0600054b; dual CPU body evidence retained.
        public override bool IsAccessPointShown()
        {
            Log();
            return false;
        }

        // Original 0x0600054c; dual CPU body evidence retained.
        public override void HideAccessPoint()
        {
            Log();
        }

        // Original 0x0600054d; dual CPU body evidence retained.
        public override float GetAccessPointOriginX()
        {
            Log();
            return -1f;
        }

        // Original 0x0600054e; dual CPU body evidence retained.
        public override float GetAccessPointScreenCoordinateX()
        {
            Log();
            return -1f;
        }

        // Original 0x0600054f; dual CPU body evidence retained.
        public override float GetAccessPointOriginY()
        {
            Log();
            return -1f;
        }

        // Original 0x06000550; dual CPU body evidence retained.
        public override float GetAccessPointScreenCoordinateY()
        {
            Log();
            return -1f;
        }

        // Original 0x06000551; dual CPU body evidence retained.
        public override float GetAccessPointSizeHeight()
        {
            Log();
            return -1f;
        }

        // Original 0x06000552; dual CPU body evidence retained.
        public override float GetAccessPointSizeScreenCoordinateHeight()
        {
            Log();
            return -1f;
        }

        // Original 0x06000553; dual CPU body evidence retained.
        public override float GetAccessPointSizeWidth()
        {
            Log();
            return -1f;
        }

        // Original 0x06000554; dual CPU body evidence retained.
        public override float GetAccessPointSizeScreenCoordinateWidth()
        {
            Log();
            return -1f;
        }

        // Original 0x06000555; dual CPU body evidence retained.
        public override bool IsPresentingGameCenter()
        {
            Log();
            return false;
        }

        // Original 0x06000556; dual CPU body evidence retained.
        public override void SetAccessPointFocused(bool focused)
        {
            Log();
        }

        // Original 0x06000557; dual CPU body evidence retained.
        public override bool IsAccessPointFocused()
        {
            Log();
            return false;
        }

        // Original 0x06000558; dual CPU body evidence retained.
        public override void SetShowHighlights(bool showHighlights)
        {
            Log();
        }

    }
}
