namespace Hardlight
{
    // Complete original HLUnityCore.Runtime02000142/060008ce. The cast keeps
    // original null propagation and invalid-cast failure; no Unity null check.
    public static class IGraphUserExtensions
    {
        public static T GetAs<T>(this IGraphUser user) where T : IGraphUser => (T)user;
    }
}
