using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AppRatingPluginMacOS : AppRatingPlugin
    {
        // 060000cc retains the observed private static PreserveSig native import.
        // Both architectures jump directly to Unity_RequestReview. The stripped
        // metadata does not establish the original library string: __Internal is
        // a reconstruction inference for this statically linked entry point.
        // It remains unreachable from the observed AppRating provider selection.
        [DllImport("__Internal")]
        private static extern void Unity_RequestReview();

        // 060000cd: the original native call is retained, never an offline no-op.
        public override void RequestReview() => Unity_RequestReview();

        // 060000ce: original base constructor only.
        public AppRatingPluginMacOS() { }
    }
}
