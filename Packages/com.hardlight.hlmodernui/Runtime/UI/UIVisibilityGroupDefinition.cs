using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "UIVisibilityGroupDefinition", menuName = "Hardlight/HLModernUI/UIVisibilityGroupDefinition", order = 2)]
    public class UIVisibilityGroupDefinition : ScriptableObjectWithGuid
    {
        [SerializeField] private bool m_initialVisibility;
        // Original06000003 field getter;06000004 is base-only and leaves false.
        public bool InitialVisibility => m_initialVisibility;
    }
}
