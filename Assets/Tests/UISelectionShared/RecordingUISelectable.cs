using System.Collections.Generic;
using Hardlight;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Test-only UISelectable. Unity's lifecycle callbacks reach the original
    // bodies only while RunLifecycle is set, so a fixture can activate the object
    // without ProcessManager lookups or entries in the static selectable tables;
    // the Call* members run the original bodies on demand. IsActive can be forced
    // to drive the state machine on an inactive object. The other overrides
    // record the call and then run the original body.
    public sealed class RecordingUISelectable : UISelectable
    {
        public bool RunLifecycle;
        public bool? ActiveOverride;
        public bool AllowInteractionChange = true;
        public readonly List<string> Calls = new List<string>();

        public int CurrentState => (int)CurrentSelectionState;
        public int PreviousState => (int)PreviousSelectionState;

        public bool PointerInside
        {
            get => IsPointerInside;
            set => IsPointerInside = value;
        }

        public bool PointerDown
        {
            get => IsPointerDown;
            set => IsPointerDown = value;
        }

        public void SetStates(int current, int previous)
        {
            m_currentSelectionState = (SelectionState)current;
            m_previousSelectionState = (SelectionState)previous;
        }

        public void CallOnEnable() => base.OnEnable();
        public void CallOnDisable() => base.OnDisable();
        public UIEventInfo CallGetEventInfo(UIEventType type, bool createIfMissing) => GetEventInfo(type, createIfMissing);
        public UIEventInfo CallSetEventHandler(UIEventType type, UnityEngine.Events.UnityEvent handler) => SetEventHandler(type, handler);
        public void CallInvokeEvent(UIEventType type) => InvokeEvent(type);
        public void CallShouldHighlight(bool highlightEnabled) => ShouldHighlight(highlightEnabled);

        public override bool IsActive() => ActiveOverride ?? base.IsActive();

        protected override void Awake()
        {
            if (RunLifecycle) base.Awake();
        }

        protected override void OnEnable()
        {
            if (RunLifecycle) base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (RunLifecycle) base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (RunLifecycle) base.OnDestroy();
        }

        protected override bool InteractionOverride()
        {
            Calls.Add("InteractionOverride");
            return AllowInteractionChange;
        }

        protected override void OnInteractableChanged()
        {
            Calls.Add("OnInteractableChanged");
            base.OnInteractableChanged();
        }

        protected override void UpdateHighlight()
        {
            Calls.Add("UpdateHighlight");
            base.UpdateHighlight();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            Calls.Add("DoStateTransition " + (int)state + " " + instant);
            base.DoStateTransition(state, instant);
        }

        public override void Deselect()
        {
            Calls.Add("Deselect");
            base.Deselect();
        }
    }
}
