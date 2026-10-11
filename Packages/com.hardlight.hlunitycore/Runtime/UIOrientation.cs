// Preserved Sonic Dream Team 1.10.1 original HLUnityCore.Runtime declarations.
// Original HLUnityCore.Runtime.dll four-owner reconstruction from complete ARM64/x86_64 evidence.
// C# spelling/local names and generated iterator names are inferred; compilation, provider algorithms,
// Unity/platform behavior and native fault equivalence remain held. No service body was executed.
// The orientation cache is never reset by any selected original method; polling yields before its first read.
// Load callbacks ignore the supplied list/new-file flag. Registration flags change only after callbacks return.
// Shutdown removes property callbacks; OnDestroy subsequently stops both coroutines before base teardown.
// OnOrientationChange remains null until a genuine subscriber assigns it; static FastAction.Invoke is genuine.
// UIOrientation/out-of-range enum values and square-screen Portrait selection are preserved without validation.
namespace Hardlight
{
    public enum UIOrientation
    {
        Uninitialised = 0,
        Portrait = 1,
        Landscape = 2
    }
}
