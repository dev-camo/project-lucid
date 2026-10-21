using System;
using HardlightProject;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only OnScreenControlGroupListener that exposes the protected message handler and the
    // Show and Hide hooks, which it leaves exactly as shipped.
    public sealed class OnScreenControlGroupListenerProbe : OnScreenControlGroupListener
    {
        public void Toggle(ScreenControlGroupMessageData messageData, Action completed) => OnToggleScreenControlGroup(messageData, completed);

        public void ShowNow(Action completed) => Show(completed);

        public void HideNow(Action completed) => Hide(completed);
    }
}
