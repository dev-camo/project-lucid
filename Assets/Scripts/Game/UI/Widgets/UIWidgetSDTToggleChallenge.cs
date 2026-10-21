using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009ca; bodies translated from the 1.10.1 x86-64 binary.
    // A challenge list entry toggle: new, complete, bonus and shadow markers.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetSDTToggleChallenge : UIWidgetToggle
    {
        public readonly Bindable<bool> IsNew = new Bindable<bool>();
        public readonly Bindable<bool> IsComplete = new Bindable<bool>();
        public readonly Bindable<bool> IsBonus = new Bindable<bool>();
        public readonly Bindable<bool> IsShadow = new Bindable<bool>();

        // 0x06003864 (0x6A2580)
        public UIToggleGroupElement GroupElement => m_toggle.GroupElement;

        // 0x06003865 (0x6A2590): the markers are published first; an entry that is already selected then reports
        // itself selected through the parameters' callback.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            var challengeParameters = (UIWidgetToggleChallengeParameters)parameters;
            IsNew.Value = challengeParameters.IsNew;
            IsComplete.Value = challengeParameters.IsComplete;
            IsBonus.Value = challengeParameters.IsBonus;
            IsShadow.Value = challengeParameters.IsShadow;
            if (IsSelected())
                challengeParameters.Callback?.Invoke(true);
        }

        // 0x06003866 (0x6A2680)
        public UIWidgetSDTToggleChallenge() { }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public sealed class UIWidgetToggleChallengeParameters : UIWidgetToggleParameters
        {
            public bool IsNew { get; }

            public bool IsComplete { get; }

            public bool IsBonus { get; }

            public bool IsShadow { get; }

            // 0x0600386b (0x6A27C0): the base constructor takes the string label overload.
            public UIWidgetToggleChallengeParameters(bool isNew, bool isComplete, string label, Action<bool> callback, bool isBonus = false, bool isShadow = false)
                : base(label, callback)
            {
                IsNew = isNew;
                IsComplete = isComplete;
                IsBonus = isBonus;
                IsShadow = isShadow;
            }
        }
    }
}
