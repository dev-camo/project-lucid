namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000114; genuine runtime-managed delegate.
    // Constructor/Invoke/BeginInvoke/EndInvoke are compiler/runtime emitted and
    // never credited as newly recovered authored native method implementations.
    public delegate void UnityHLLeaderboardChallengeIssuedCallback(bool success, string error);
}
