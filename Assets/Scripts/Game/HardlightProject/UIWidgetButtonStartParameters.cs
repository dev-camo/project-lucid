using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetButtonStartParameters : IUIWidgetParameters
    {
        public readonly bool Locked;
        public readonly bool IsNew;

        // Original 060037c9: base construction, Locked, then IsNew.
        public UIWidgetButtonStartParameters(bool locked, bool isNew)
        {
            Locked = locked;
            IsNew = isNew;
        }
    }
}
