using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AppRatingPluginUnsupported : AppRatingPlugin
    {
        private readonly string m_unsupportedReason;
        private readonly bool m_warning;

        // 060000cf: publish both fields before attempting the original log call.
        public AppRatingPluginUnsupported(string unsupportedReason, bool warning)
        {
            m_unsupportedReason = unsupportedReason;
            m_warning = warning;
            Log();
        }

        // 060000d0. The shipping warning path returns without emitting a message;
        // only the false branch calls HLOutput.LogError. A development warning
        // call may have been stripped, but its source cannot be inferred here.
        private void Log()
        {
            if (!m_warning) HLOutput.LogError(m_unsupportedReason);
        }

        // 060000d1: retry the same original logging path on each request.
        public override void RequestReview() => Log();
    }
}
