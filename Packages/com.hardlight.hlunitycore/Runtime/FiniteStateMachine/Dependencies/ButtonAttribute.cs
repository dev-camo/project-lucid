using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // HLUnityCore.Runtime.dll 0x0200025c, complete original two-field attribute.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ButtonAttribute : PropertyAttribute
    {
        public readonly bool ReplaceField;
        public readonly string ButtonText;

        // 0x06000f13: construct PropertyAttribute, then retain both arguments.
        public ButtonAttribute(bool replaceField = false, string buttonText = null) : base()
        {
            ReplaceField = replaceField;
            ButtonText = buttonText;
        }
    }
}
