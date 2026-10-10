using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/HLInputSupplier")]
    public class HLInputSupplier : InputSupplier
    {
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_input;

        // Original HLUnityUI.Runtime 06000316 reads the serialized field directly.
        public GameInput Input { get { return m_input; } }

        // Original 06000317: implicit public constructor only calls the base.
    }
}
