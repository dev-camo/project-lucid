// Original HLUnityUI.Runtime Hardlight.UIEvent 02000075 (MethodDefs
// 0x0600037b..7d), translated from the x86-64 bodies: a field-less asset whose
// lookups forward to the shared ObjectUtils name searches.
using Hardlight.Utils;
using UnityEngine;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "UIEvent", menuName = "Hardlight/HLUnityUI/UIEvent", order = 0)]
    public class UIEvent : ScriptableObject
    {
        public static UIEvent FindByName(string name)
        {
            return ObjectUtils.FindByName<UIEvent>(name);
        }

        public static UIEvent FindByNameSafe(string name)
        {
            return ObjectUtils.FindByNameSafe<UIEvent>(name);
        }
    }
}
