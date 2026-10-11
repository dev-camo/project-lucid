// Preserved Sonic Dream Team 1.10.1, HLUnityCore.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// 0x0600050f 0x0, 0x0 (abstract declaration)
// 0x06000510 0x0, 0x0 (abstract declaration)
// 0x06000511 0x0, 0x0 (abstract declaration)
// 0x06000512 0x0, 0x0 (abstract declaration)
// 0x06000513 0x0, 0x0 (abstract declaration)
// 0x06000514 0x0, 0x0 (abstract declaration)
// 0x06000515 0x0, 0x0 (abstract declaration)
// 0x06000516 0x0, 0x0 (abstract declaration)
// 0x06000517 0x0, 0x0 (abstract declaration)
// 0x06000518 0x0, 0x0 (abstract declaration)
// 0x06000519 0x0, 0x0 (abstract declaration)
// 0x0600051a 0x0, 0x0 (abstract declaration)
// 0x0600051b 0x0, 0x0 (abstract declaration)
// 0x0600051c 0x0, 0x0 (abstract declaration)
// 0x0600051d 0x0, 0x0 (abstract declaration)
// 0x0600051e 0x0, 0x0 (abstract declaration)
// 0x0600051f 0x0, 0x0 (abstract declaration)
// 0x06000520 0x0, 0x0 (abstract declaration)
// 0x06000521 0x1ad2878, 0x1ace9e0
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class GameCenterAccessPointPlugin
    {
        // Original 0x0600050f; abstract slot 4 has two zero CodeGen pointers.
        public abstract void Show();

        // Original 0x06000510; abstract slot 5 has two zero CodeGen pointers.
        public abstract void ShowAchievements();

        // Original 0x06000511; abstract slot 6 has two zero CodeGen pointers.
        public abstract bool IsAccessPointAvailable();

        // Original 0x06000512; abstract slot 7 has two zero CodeGen pointers.
        public abstract void ShowAccessPoint(int location, bool showHighlights);

        // Original 0x06000513; abstract slot 8 has two zero CodeGen pointers.
        public abstract bool IsAccessPointShown();

        // Original 0x06000514; abstract slot 9 has two zero CodeGen pointers.
        public abstract void HideAccessPoint();

        // Original 0x06000515; abstract slot 10 has two zero CodeGen pointers.
        public abstract float GetAccessPointOriginX();

        // Original 0x06000516; abstract slot 11 has two zero CodeGen pointers.
        public abstract float GetAccessPointScreenCoordinateX();

        // Original 0x06000517; abstract slot 12 has two zero CodeGen pointers.
        public abstract float GetAccessPointOriginY();

        // Original 0x06000518; abstract slot 13 has two zero CodeGen pointers.
        public abstract float GetAccessPointScreenCoordinateY();

        // Original 0x06000519; abstract slot 14 has two zero CodeGen pointers.
        public abstract float GetAccessPointSizeHeight();

        // Original 0x0600051a; abstract slot 15 has two zero CodeGen pointers.
        public abstract float GetAccessPointSizeScreenCoordinateHeight();

        // Original 0x0600051b; abstract slot 16 has two zero CodeGen pointers.
        public abstract float GetAccessPointSizeWidth();

        // Original 0x0600051c; abstract slot 17 has two zero CodeGen pointers.
        public abstract float GetAccessPointSizeScreenCoordinateWidth();

        // Original 0x0600051d; abstract slot 18 has two zero CodeGen pointers.
        public abstract bool IsPresentingGameCenter();

        // Original 0x0600051e; abstract slot 19 has two zero CodeGen pointers.
        public abstract void SetAccessPointFocused(bool focused);

        // Original 0x0600051f; abstract slot 20 has two zero CodeGen pointers.
        public abstract bool IsAccessPointFocused();

        // Original 0x06000520; abstract slot 21 has two zero CodeGen pointers.
        public abstract void SetShowHighlights(bool showHighlights);

        // Original 0x06000521; dual CPU body evidence retained.
        protected GameCenterAccessPointPlugin()
        {
        }

    }
}
