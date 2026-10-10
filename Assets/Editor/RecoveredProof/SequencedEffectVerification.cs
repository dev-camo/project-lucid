using System;
using Hardlight;
using HardlightProject;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Test-only observation of the original abstract base. This component is
    // never an original concrete effect or an input/coroutine/time provider.
    // These synchronous cases make no delayed-scheduling or gameplay claim.
    public sealed class SequencedEffectObservation : SequencedEffect
    {
        public int Triggers { get; private set; }
        public IEffectData LastData { get; private set; }
        public Action Completion => m_onComplete;
        public TimeCategory Category => m_timeCategory;
        public void SetDelay(float delay) { m_delay = delay; }
        protected override void OnEffectTriggered(IEffectData data)
        {
            Triggers++;
            LastData = data;
            base.OnEffectTriggered(data);
        }
    }

    public static class SequencedEffectVerification
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // Use the ordinary Unity serialized fields; leave real shared input
        // state, callbacks, registry, configuration and coroutine host intact.
        private static void Gate(SequencedEffect effect, InputType[] types, bool exclusive)
        {
            var serialized = new SerializedObject(effect);
            SerializedProperty array = serialized.FindProperty("m_triggerOnlyForInputTypes");
            array.arraySize = types.Length;
            for (int i = 0; i < types.Length; ++i)
                array.GetArrayElementAtIndex(i).intValue = (int)types[i];
            serialized.FindProperty("m_inputTypesExclusive").boolValue = exclusive;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void DefaultsAndNonPositiveDelaysKeepOriginalHooks()
        {
            var owner = new GameObject("ProjectLucid.SequencedEffect.OwnedObservation");
            try
            {
                var effect = owner.AddComponent<SequencedEffectObservation>();
                var data = new TargetedEffect();
                int completed = 0;
                Action callback = () => completed++;
                Check(effect.Progress == 0f && effect.Category == TimeCategory.Effects,
                    "Original default progress and Effects category");
                // Exercise the newly attached component's unconfigured input gate.
                effect.PlayEffect(data, callback);
                Check(effect.Triggers == 1 && ReferenceEquals(effect.LastData, data) && completed == 0,
                    "Original unconfigured gate dispatches data and empty base hook does not complete");
                foreach (float delay in new[] { 0f, -0f, -1f, float.NegativeInfinity, float.NaN })
                {
                    int before = effect.Triggers;
                    effect.SetDelay(delay);
                    effect.PlayEffect(data, callback);
                    Check(effect.Triggers == before + 1 && ReferenceEquals(effect.LastData, data),
                        "Every nonpositive or unordered delay dispatches synchronously");
                    Check(completed == 0 && ReferenceEquals(effect.Completion, callback),
                        "Successful original empty hook retains the current callback");
                }
                int prior = effect.Triggers;
                effect.OnReset();
                effect.CancelCoroutine();
                Check(effect.Triggers == prior && completed == 0 && effect.Progress == 0f &&
                      ReferenceEquals(effect.Completion, callback),
                    "Original empty reset and null-handle cancellation retain effect state");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        public static void InclusiveAndExclusiveGatesUseRealCurrentInput()
        {
            InputType current = ControlMapping.LastInputType;
            InputType other = (InputType)unchecked((int)current ^ int.MinValue);
            var owner = new GameObject("ProjectLucid.SequencedEffect.OwnedInputGate");
            try
            {
                var effect = owner.AddComponent<SequencedEffectObservation>();
                var data = new TargetedEffect();
                foreach (bool exclusive in new[] { false, true })
                foreach (InputType[] types in new[] {
                    new InputType[0], new[] { current }, new[] { other },
                    new[] { other, current, current }, new[] { other, other } })
                {
                    Check(ControlMapping.LastInputType == current,
                        "Synchronous owned context retains the real input provider value");
                    Gate(effect, types, exclusive);
                    int before = effect.Triggers;
                    int completed = 0;
                    Action callback = () => completed++;
                    bool expected = types.Length == 0 ||
                        (Array.IndexOf(types, current) >= 0 ? !exclusive : exclusive);
                    effect.PlayEffect(data, callback);
                    Check(effect.Triggers == before + (expected ? 1 : 0) &&
                          completed == (expected ? 0 : 1),
                        "Original inclusion, exclusion, empty and duplicate gates");
                    Check(ReferenceEquals(effect.Completion, callback),
                        "Accepted and rejected paths retain the original completion delegate");
                }
                Check(ControlMapping.LastInputType == current,
                    "No shared input replacement or update occurred");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        public static void RejectedCallbackReentryAndFaultKeepLatestCompletion()
        {
            var owner = new GameObject("ProjectLucid.SequencedEffect.OwnedCallback");
            try
            {
                var effect = owner.AddComponent<SequencedEffectObservation>();
                Gate(effect, new[] { ControlMapping.LastInputType }, true);
                var data = new TargetedEffect();
                var failure = new InvalidOperationException("Owned completion failure");
                int first = 0, second = 0;
                Action latest = () => { second++; throw failure; };
                Action initial = () => { first++; effect.PlayEffect(data, latest); };
                Exception observed = null;
                try { effect.PlayEffect(data, initial); }
                catch (Exception error) { observed = error; }
                Check(ReferenceEquals(observed, failure) && first == 1 && second == 1 && effect.Triggers == 0,
                    "Rejected completion dispatches synchronously and preserves nested callback failure");
                Check(ReferenceEquals(effect.Completion, latest),
                    "Reentrant PlayEffect stores the latest callback before rejection and retains it after failure");
                effect.OnReset();
                effect.CancelCoroutine();
                Check(ReferenceEquals(effect.Completion, latest),
                    "Original reset and null coroutine cancellation do not clear completion");
                effect.PlayEffect(data, null);
                Check(effect.Completion == null && effect.Triggers == 0 && first == 1 && second == 1,
                    "Rejected null completion is stored and safely skipped");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
    }
}
