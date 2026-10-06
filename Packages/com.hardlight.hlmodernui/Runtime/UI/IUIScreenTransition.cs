using System;

namespace Hardlight
{
    // Original0200000a: exactly three abstract declarations, zero own bodies.
    public interface IUIScreenTransition
    {
        void StartTransition(Action onReachMidpointCallback, Action onCompletionCallback);
        void ContinueTransition();
        bool HasReachedMidpoint { get; }
    }
}
