using System;

namespace ProjectLucid.Offline
{
    // The one switch for every offline hook (decision D2). A hook is the check
    // `if (OfflineAdapters.Active) { <adapter call>; return ...; }` at the top of the hooked
    // original member, with the original body kept verbatim after it. The original code is
    // therefore always compiled and type-checked, and its compiler-generated names stay exact.
    // Every hook is listed in docs/offline-hooks.md
    // and found by searching for "OfflineAdapters.Active".
    //
    // This type lives in HLUnityCore.Runtime because that assembly references nothing and every
    // hooked assembly (HLUnityCore, HLAnalytics, HLInput, Game.Runtime) already references it,
    // so no new asmdef reference and no cycle is needed. It is a non-const property on purpose:
    // a constant would make the original bodies constant-folded dead code (CS0162) and risk
    // dropping their lambdas and iterators.
    public static class OfflineAdapters
    {
        private static bool? s_override;

        public static bool Active => s_override ?? true;

        // Test seam only: runs the retained original bodies of hooked members in tests. Never call it from product code.
        public static IDisposable Override(bool active)
        {
            bool? previous = s_override;
            s_override = active;
            return new Restore(previous);
        }

        private sealed class Restore : IDisposable
        {
            private readonly bool? m_previous;
            private bool m_disposed;

            internal Restore(bool? previous) { m_previous = previous; }

            public void Dispose()
            {
                if (m_disposed) return;
                m_disposed = true;
                s_override = m_previous;
            }
        }
    }
}
