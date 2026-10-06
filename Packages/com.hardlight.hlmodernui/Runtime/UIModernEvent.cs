using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "UIModernEvent", menuName = "Hardlight/HLModernUI/UIModernEvent", order = 1)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIModernEvent : ScriptableObjectWithGuid
    {
        // Original HLModernUI.Runtime06000033, ARM0x1a419f8.
        public static UIModernEvent FindByName(string name) => ObjectUtils.FindByName<UIModernEvent>(name);
        // Original06000034 delegates the genuine maintained GUID base constructor.
    }
}
