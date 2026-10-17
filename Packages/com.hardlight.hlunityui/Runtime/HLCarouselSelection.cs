using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 020000c6 (MethodDefs 0x06000560..564): the
    // HLScrollRectSelection policies for an HLCarousel. Targets are neither clamped
    // nor replaced when unresolved, the first/last item overrides are skipped and
    // the bookend search is widened by 0.1 (0x1B660D0).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLCarouselSelection : HLScrollRectSelection
    {
        protected override bool NormaliseWhenNegativeOne => false;
        protected override bool ClampTargetScrollOffset => false;
        protected override bool ShouldWrap => true;
        protected override float BookendOffset => 0.1f;

        public HLCarouselSelection() { }
    }
}
