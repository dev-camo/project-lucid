using Hardlight;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Test-only UIClickable whose virtual activity and interactability are set by the fixture,
    // so UIClickable's own Press can be exercised on an inactive owner.
    public sealed class UIClickableProbe : UIClickable
    {
        public bool ActiveResult;
        public bool InteractableResult;

        public override bool IsActive() => ActiveResult;
        public override bool IsInteractable() => InteractableResult;
    }
}
