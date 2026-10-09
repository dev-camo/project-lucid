using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class ModifierGameInputState<T> : InputModifier
    {
        protected T m_state;
        protected readonly Dictionary<GameInput, T> m_gameInputStates =
            new Dictionary<GameInput, T>(HardlightEnumComparers.GameInputComparer);

        // 06000155: dictionary initializer runs before base. No allocation/store
        // to m_state appears in the original ctor, even when T is Repeat.State.
        protected ModifierGameInputState() { }
    }
}
