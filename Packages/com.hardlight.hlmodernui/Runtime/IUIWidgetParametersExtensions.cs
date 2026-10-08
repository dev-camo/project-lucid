namespace Hardlight
{
    public static class IUIWidgetParametersExtensions
    {
        // Original 06000196: the shared reference-type native instantiation returns null for null.
        // An incompatible reference object throws; other generic instantiations remain held.
        // The original generic constraint is the interface only, with no class constraint.
        public static T GetAs<T>(this IUIWidgetParameters parameters) where T : IUIWidgetParameters
            => (T)parameters;
    }
}
