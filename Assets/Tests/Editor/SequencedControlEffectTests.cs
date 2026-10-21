using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using static ProjectLucid.Tests.TimelineTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SequencedWaitForContinue (TypeDef 0x0200062c, x86-64 0x56B1C0..0x56B26F),
    // SequencedUnityEvent (0x0200062b, 0x56AF10..0x56B1BF) and SequencedTransformSurfaceFollower (0x0200062a,
    // 0x56ACB0..0x56AF0F). The components are real, on owned inactive hosts that never wake; the effects run
    // through their public PlayEffect entry, whose input gate is open when no input types are authored. Authored
    // fields are written through SerializedObject, as the inspector does.
    //
    // Not covered: the positive m_lengthOverrideSeconds of SequencedUnityEvent (jbe past zero, then
    // DelayFixedSeconds(override, m_timeCategory, onComplete) at 0x56AF7B, 0x56AFB7, 0x56B019 and 0x56B172). It needs the
    // shared CoroutineUtils host (its Awake does not run in Edit Mode) plus a registered TimeManager whose typed reference
    // TimeScaledUtilities caches once, and neither can be provided without forcing static host state. Expectation for a
    // genuine fixture: a positive override leaves a trigger, an OnSequenceEnd and a BeginSequence uncompleted until the
    // scaled fixed time has passed (about 0.5 s for 0.5), while a negative override completes synchronously like zero.
    public sealed class SequencedControlEffectTests
    {
        private static void Trigger(SequencedEffect effect, Action onComplete)
        {
            IEffectData data = new TargetedEffect();
            effect.PlayEffect(in data, onComplete);
        }

        private static SequencedWaitForContinue NewWait(Owned owned, bool allowContinueBeforeTriggered, bool onlyTriggerOnce)
        {
            var effect = owned.Host("Lucid wait for continue", false).AddComponent<SequencedWaitForContinue>();
            Author(effect, serialized =>
            {
                serialized.FindProperty("m_allowContinueBeforeTriggered").boolValue = allowContinueBeforeTriggered;
                serialized.FindProperty("m_onlyTriggerOnce").boolValue = onlyTriggerOnce;
            });
            return effect;
        }

        // OnEffectTriggered 0x56B1C0 and DoContinue 0x56B210, default flags (neither authored). A trigger waits
        // (clearing any stored continue); a continue after the trigger completes the effect and, because
        // "only once" is off, re-arms the wait by clearing the triggered flag; a second continue is ignored
        // while the first is still held.
        [Test]
        public void ContinueAfterTheTriggerCompletesOnceAndTheNextTriggerWaitsAgain()
        {
            Owned.Run(owned =>
            {
                SequencedWaitForContinue effect = NewWait(owned, false, false);
                int completed = 0;
                Action done = () => completed++;
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(0), "the trigger waits");
                effect.DoContinue();
                Assert.That(completed, Is.EqualTo(1), "continue completes");
                effect.DoContinue();
                Assert.That(completed, Is.EqualTo(1), "a continue that was already taken is ignored");
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(1), "the next trigger waits again");
                effect.DoContinue();
                Assert.That(completed, Is.EqualTo(2));
            });
        }

        // DoContinue 0x56B210 before any trigger: stored only when m_allowContinueBeforeTriggered; the trigger
        // 0x56B1C0 then resolves at once and clears the stored continue.
        [Test]
        public void ContinueBeforeTheTriggerIsIgnoredUnlessAllowed()
        {
            Owned.Run(owned =>
            {
                SequencedWaitForContinue refused = NewWait(owned, false, false);
                int completed = 0;
                Action done = () => completed++;
                refused.DoContinue();
                Trigger(refused, done);
                Assert.That(completed, Is.EqualTo(0), "an early continue is dropped");
                refused.DoContinue();
                Assert.That(completed, Is.EqualTo(1));

                SequencedWaitForContinue allowed = NewWait(owned, true, false);
                completed = 0;
                allowed.DoContinue();
                Assert.That(completed, Is.EqualTo(0), "stored, nothing to complete yet");
                Trigger(allowed, done);
                Assert.That(completed, Is.EqualTo(1), "the stored continue resolves the trigger at once");
                Trigger(allowed, done);
                Assert.That(completed, Is.EqualTo(1), "and was consumed by that trigger");
                allowed.DoContinue();
                Assert.That(completed, Is.EqualTo(2));
            });
        }

        // OnEffectTriggered 0x56B1C0 with "only once": after the first trigger the guard
        // (triggered && onlyTriggerOnce) skips the wait and completes at once; DoContinue 0x56B210 keeps the held
        // continue (it does not clear the triggered flag), so a second continue is ignored.
        [Test]
        public void OnlyTriggerOnceResolvesLaterTriggersImmediately()
        {
            Owned.Run(owned =>
            {
                SequencedWaitForContinue effect = NewWait(owned, false, true);
                int completed = 0;
                Action done = () => completed++;
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(0), "the first trigger waits");
                effect.DoContinue();
                Assert.That(completed, Is.EqualTo(1));
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(2), "later triggers do not wait");
                effect.DoContinue();
                Assert.That(completed, Is.EqualTo(2), "the held continue is ignored");
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(3));
            });
        }

        // Both flags set: an early continue is stored; the first trigger resolves at once but, because the effect
        // only triggers once, keeps the continue held (0x56B1E0 skips the clear).
        [Test]
        public void AnEarlyContinueIsKeptWhenOnlyTriggeringOnce()
        {
            Owned.Run(owned =>
            {
                SequencedWaitForContinue effect = NewWait(owned, true, true);
                int completed = 0;
                Action done = () => completed++;
                effect.DoContinue();
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(1));
                effect.DoContinue();
                Assert.That(completed, Is.EqualTo(1), "the continue is still held");
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(2));
            });
        }

        // DoContinue 0x56B210 completes through a null-guarded call (test rax, rax).
        [Test]
        public void ContinueWithoutACompletionCallbackDoesNotFault()
        {
            Owned.Run(owned =>
            {
                SequencedWaitForContinue effect = NewWait(owned, false, false);
                Trigger(effect, null);
                Assert.DoesNotThrow(() => effect.DoContinue());
            });
        }

        private static SequencedUnityEvent NewEvent(Owned owned, out List<ISequencedEffectWithCallback> received, Action<ISequencedEffectWithCallback> listener = null)
        {
            var effect = owned.Host("Lucid unity event effect", false).AddComponent<SequencedUnityEvent>();
            var unityEvent = Get<UnityEvent<ISequencedEffectWithCallback>>(effect, "m_unityEvent");
            var log = new List<ISequencedEffectWithCallback>();
            unityEvent.AddListener(argument =>
            {
                log.Add(argument);
                listener?.Invoke(argument);
            });
            received = log;
            return effect;
        }

        private sealed class StartEndRecorder : ISequencedEffectWithCallback
        {
            public readonly List<string> Log = new List<string>();
            public void OnSequenceStart() => Log.Add("start");
            public void OnSequenceEnd() => Log.Add("end");
        }

        // OnEffectTriggered 0x56AF10: the wait count is zeroed, the event is invoked with the effect itself, and a
        // count that is still zero completes (no length override authored, so the callback is invoked directly).
        [Test]
        public void ListenersReceiveTheEffectAndANonWaitingEventCompletesAtOnce()
        {
            Owned.Run(owned =>
            {
                SequencedUnityEvent effect = NewEvent(owned, out List<ISequencedEffectWithCallback> received);
                int completed = 0;
                Trigger(effect, () => completed++);
                Assert.That(received, Has.Count.EqualTo(1));
                Assert.That(received[0], Is.SameAs(effect));
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // OnSequenceStart 0x56AFE0 increments, OnSequenceEnd 0x56AFF0 decrements and completes at a count of
        // zero or below.
        [Test]
        public void WaitersKeepTheEffectOpenUntilTheLastOneEndsIt()
        {
            Owned.Run(owned =>
            {
                SequencedUnityEvent effect = NewEvent(owned, out _, argument =>
                {
                    argument.OnSequenceStart();
                    argument.OnSequenceStart();
                });
                int completed = 0;
                Trigger(effect, () => completed++);
                Assert.That(completed, Is.EqualTo(0), "two waiters hold the effect open");
                effect.OnSequenceEnd();
                Assert.That(completed, Is.EqualTo(0));
                effect.OnSequenceEnd();
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // OnEffectTriggered 0x56AF10 stores zero to the wait count before invoking the event, so a start left over
        // from an earlier trigger does not hold a later one open.
        [Test]
        public void EachTriggerResetsTheWaitCount()
        {
            Owned.Run(owned =>
            {
                bool wait = true;
                SequencedUnityEvent effect = NewEvent(owned, out _, argument => { if (wait) argument.OnSequenceStart(); });
                int completed = 0;
                Action done = () => completed++;
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(0), "held open by the first trigger");
                wait = false;
                Trigger(effect, done);
                Assert.That(completed, Is.EqualTo(1), "the second trigger starts from a clean count");
            });
        }

        // OnSequenceEnd 0x56AFF0 decrements before the test, so an end without a start (count -1) completes.
        [Test]
        public void AnEndWithoutAStartStillCompletes()
        {
            Owned.Run(owned =>
            {
                SequencedUnityEvent effect = NewEvent(owned, out _);
                int completed = 0;
                Trigger(effect, () => completed++);
                Assert.That(completed, Is.EqualTo(1));
                effect.OnSequenceEnd();
                Assert.That(completed, Is.EqualTo(2));
            });
        }

        // BeginSequence 0x56B040: the caller's OnSequenceStart runs first, then its OnSequenceEnd is the
        // completion (invoked directly without a length override); the effect's own callback is not used.
        [Test]
        public void BeginSequenceStartsTheCallerThenEndsItThroughItsOwnDelegate()
        {
            Owned.Run(owned =>
            {
                SequencedUnityEvent effect = NewEvent(owned, out _);
                int completed = 0;
                Trigger(effect, () => completed++);
                var caller = new StartEndRecorder();
                effect.BeginSequence(caller);
                Assert.That(caller.Log, Is.EqualTo(new[] { "start", "end" }));
                Assert.That(completed, Is.EqualTo(1), "only the trigger completed the effect's own callback");
            });
        }

        // The follower is a TimeScaledComponent_SDT: its editor Reset/OnValidate read the category lookup, so the
        // component is added while an owned empty lookup is cached, which is put back afterwards. The shared
        // typed TimeManager reference of TimeScaledComponent is cached from a registered Environment manager.
        private static T AddFollower<T>(Owned owned, string name) where T : Component
        {
            FieldInfo config = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = config.GetValue(null);
            var emptyLookup = ScriptableObject.CreateInstance<TimeCategoryLookup>();
            config.SetValue(null, emptyLookup);
            try { return owned.Host(name, false).AddComponent<T>(); }
            finally
            {
                config.SetValue(null, prior);
                Object.DestroyImmediate(emptyLookup);
            }
        }

        private static void WithFollowerEffect(Action<TransformSurfaceFollower, SequencedTransformSurfaceFollower, Owned> body) => Owned.Run(owned =>
        {
            owned.Register(NewTimeManager(owned, TimeCategory.Environment, 1f));
            TransformSurfaceFollower follower = AddFollower<TransformSurfaceFollower>(owned, "Lucid surface follower");
            var effect = owned.Host("Lucid sequenced surface follower", false).AddComponent<SequencedTransformSurfaceFollower>();
            Author(effect, serialized => serialized.FindProperty("m_transformSurfaceFollower").objectReferenceValue = follower);
            body(follower, effect, owned);
        });

        // OnEffectTriggered 0x56ACB0: Delegate.Combine adds OnComplete to OnCompleteAction, then SetEnabled(true)
        // (0x601B90) turns the follower on.
        [Test]
        public void TriggerSubscribesToTheFollowerAndEnablesIt()
        {
            WithFollowerEffect((follower, effect, owned) =>
            {
                follower.enabled = false;
                Assert.That(follower.OnCompleteAction, Is.Null);
                Trigger(effect, () => { });
                Assert.That(follower.enabled, Is.True);
                Delegate[] handlers = follower.OnCompleteAction.GetInvocationList();
                Assert.That(handlers, Has.Length.EqualTo(1));
                Assert.That(handlers[0].Target, Is.SameAs(effect));
                Assert.That(handlers[0].Method.Name, Is.EqualTo("OnComplete"));
            });
        }

        // OnComplete 0x56ADA0: the subscription is kept and nothing completes unless the follower that finished is
        // the authored one (op_Inequality against m_transformSurfaceFollower).
        [Test]
        public void AnotherFollowersCompletionIsIgnored()
        {
            WithFollowerEffect((follower, effect, owned) =>
            {
                TransformSurfaceFollower other = AddFollower<TransformSurfaceFollower>(owned, "Lucid other surface follower");
                int completed = 0;
                Trigger(effect, () => completed++);
                follower.OnCompleteAction(other);
                Assert.That(completed, Is.EqualTo(0));
                Assert.That(follower.OnCompleteAction.GetInvocationList(), Has.Length.EqualTo(1));
            });
        }

        // OnComplete 0x56ADA0: the authored follower finishing removes the handler (Delegate.Remove) and then
        // invokes the stored completion once.
        [Test]
        public void TheAuthoredFollowersCompletionUnsubscribesAndCompletesOnce()
        {
            WithFollowerEffect((follower, effect, owned) =>
            {
                int completed = 0;
                Trigger(effect, () => completed++);
                follower.OnCompleteAction(follower);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(follower.OnCompleteAction, Is.Null);
            });
        }

        // OnComplete 0x56ADA0 tests the stored completion before invoking it.
        [Test]
        public void TheAuthoredFollowersCompletionWithoutACallbackDoesNotFault()
        {
            WithFollowerEffect((follower, effect, owned) =>
            {
                Trigger(effect, null);
                Assert.DoesNotThrow(() => follower.OnCompleteAction(follower));
                Assert.That(follower.OnCompleteAction, Is.Null);
            });
        }
    }
}
