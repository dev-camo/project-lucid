using System;
using HardlightProject;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only OnScreenControlGroupListenerButton that exposes the protected Hide hook.
    public sealed class OnScreenControlGroupListenerButtonProbe : OnScreenControlGroupListenerButton
    {
        public void HideNow(Action completed) => Hide(completed);
    }
}
