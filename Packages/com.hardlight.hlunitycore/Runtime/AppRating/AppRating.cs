using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class AppRating
    {
        private static readonly AppRatingPlugin s_appRatingPlugin;

        // HLUnityCore.Runtime 060000c8. Both shipping architectures select this
        // provider unconditionally. The presence of a MacOS plugin is not evidence
        // that this release selects it. Keep the observed selection for research.
        static AppRating()
        {
            s_appRatingPlugin = new AppRatingPluginUnsupported(
                string.Format("App Rating is not supported in platform {0}.", Application.platform), true);
        }

        // 060000c9: dispatch through the original plugin's virtual API.
        public static void RequestReview() => s_appRatingPlugin.RequestReview();
    }
}
