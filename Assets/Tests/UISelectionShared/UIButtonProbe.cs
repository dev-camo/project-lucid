using System;
using Hardlight;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Test-only UIButton whose virtual activity, interactability and interaction override are
    // set by the fixture, so the original virtual calls can be steered on an inactive owner
    // without running the UISelectable lifecycle. Pressed observes virtual Press dispatch before
    // the original UIButton.Press runs.
    public sealed class UIButtonProbe : UIButton
    {
        public bool ActiveResult;
        public bool InteractableResult;
        public bool InteractionOverrideResult = true;
        public Action Pressed;

        public override bool Press()
        {
            Pressed?.Invoke();
            return base.Press();
        }

        public override bool IsActive() => ActiveResult;
        public override bool IsInteractable() => InteractableResult;
        protected override bool InteractionOverride() => InteractionOverrideResult;
    }
}
