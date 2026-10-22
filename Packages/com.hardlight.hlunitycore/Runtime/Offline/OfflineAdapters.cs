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
        public static bool Active => true;
    }
}
