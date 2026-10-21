using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000993; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class UIContainerValueSetter : MonoBehaviour
    {
        [SerializeField]
        private UIContainerIdentifier m_identifier;

        // 0600374e (1.10.1 RVA 0x696BC0): the identifier's loaded container, when it is
        // currently open and reports progress.
        protected IUIContainerProgress UIContainer => m_identifier.IsLoaded ? m_identifier.LoadedUIContainer as IUIContainerProgress : null;

        // 0600374f (1.10.1 RVA 0x696E30)
        protected UIContainerValueSetter()
        {
        }
    }
}
