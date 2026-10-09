using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ModifierGameInputState : InputModifier
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class GameInputState
        {
            public bool StateChange;
            public float Timer;
            public GameInputState() { } // 06000157: original Object base-only ctor.
        }

        protected bool m_stateChange;
        protected float m_timer;
        protected readonly Dictionary<GameInput, GameInputState> m_gameInputStates =
            new Dictionary<GameInput, GameInputState>(HardlightEnumComparers.GameInputComparer);

        protected ModifierGameInputState() { } // 06000156: dictionary before actual InputModifier base.
    }
}
