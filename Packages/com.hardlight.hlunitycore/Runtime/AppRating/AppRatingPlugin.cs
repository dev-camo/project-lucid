using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class AppRatingPlugin
    {
        // 060000ca is a genuine abstract slot, with no native method body.
        public abstract void RequestReview();

        // 060000cb: original Object constructor only.
        protected AppRatingPlugin() { }
    }
}
