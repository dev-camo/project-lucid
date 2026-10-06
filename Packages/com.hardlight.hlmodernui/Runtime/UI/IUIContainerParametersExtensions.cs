namespace Hardlight
{
    public static class IUIContainerParametersExtensions
    {
        // Original06000012: castclass/unbox-any T; no class-only constraint,
        // fallback value or caught invalid cast. Shared native representations
        // belong to this one generic method definition.
        public static T GetAs<T>(this IUIContainerParameters parameters) where T : IUIContainerParameters => (T)parameters;
    }
}
