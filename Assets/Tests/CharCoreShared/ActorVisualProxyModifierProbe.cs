using HardlightProject;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only exposure of the protected helpers of ActorVisualProxyModifier, which has no
    // public entry point of its own.
    public sealed class ActorVisualProxyModifierProbe : ActorVisualProxyModifier
    {
        public void Show(Actor actor) => ShowVisualProxy(actor);
        public void Hide(Actor actor) => HideVisualProxy(actor);
        public void Disable(Actor actor) => DisableVisualProxy(actor);
        public void Enable(Actor actor) => EnableVisualProxy(actor);
    }
}
