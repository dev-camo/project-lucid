using System;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200045d, complete three-field/one-constructor type.
    // Form geometry defaults are stored before Object's constructor in native code.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FormTraits
    {
        public ActorFormType FormType = ActorFormType.Default;
        public float ColliderRadius = 0.5f;
        public float ColliderHeight = 1.2f;

        // Original 0x06001993. Field initializers preserve ordering and the exact
        // packed Single constants; no geometry validation or clamping is added.
        public FormTraits() { }
    }
}
