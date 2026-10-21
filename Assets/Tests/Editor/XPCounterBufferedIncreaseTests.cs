using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // XPCounterBufferedIncrease (Game.Runtime 0x02000857, x86-64 0x60EE20..0x60F7D0) with its nested EffectThresholds (0x02000858) and
    // iterator (0x02000859, MoveNext 0x60F6C0) as a real component on an inactive owned host over a real UIAnimatingCounter, the App
    // of OwnedCharacterManagers and a real ChallengeManager holding supplied save data. The component reads both services through
    // static readonly references created once per process, so the fixture binds them to its own App and manager (the way
    // OwnedChallengeRewardServices does for ChallengeReward). The counter's host is inactive, so a requested count-up shows as the
    // counter's animate-when-enabled flag. The serialized fields are authored data. Not covered: the count-up itself, which is
    // UIAnimatingCounter's own coroutine, and the running of the wait coroutine, which the owned coroutine host refuses.
    public sealed class XPCounterBufferedIncreaseTests
    {
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Rig
        {
            public App App;
            public ChallengeManager Manager;
            public SaveDataChallenges Save;
            public UIAnimatingCounter Counter;
            public XPCounterBufferedIncrease Xp;
            public readonly int[] Hits = new int[8];

            public int LastSeen => App.Storage.GetValue(AppFSMKeys.LastSeenXPValue, -1, false);
            public bool CountUpRequested => Get<bool>(Counter, "m_animateWhenEnabled");
            public float AnimationSeconds => Get<float>(Counter, "m_animationTimeSeconds");
        }

        // The static reference of the component is a typed reference of a registry row that an earlier case's row release may have
        // left empty; an empty one is filled through its own InternalReplaceSystem and revoked again.
        private static void Bind<T>(Owned owned, string field, T system) where T : class, ISystem
        {
            var reference = (SystemRef<T>)typeof(XPCounterBufferedIncrease).GetField(field, Statics).GetValue(null);
            if (reference.IsValid())
            {
                if (!ReferenceEquals(reference.GetSafe(), system))
                    throw new InvalidOperationException("Fixture requires an empty reference: " + field);
                return;
            }
            MethodInfo replace = typeof(SystemRef<T>).GetMethod("Hardlight.ISystemRef.InternalReplaceSystem", InstanceMembers);
            MethodInfo revoke = typeof(SystemRef<T>).GetMethod("Hardlight.ISystemRef.InternalRevokeSystem", InstanceMembers);
            FieldInfo startup = typeof(SystemRef<T>).GetField("OnSystemStartup", InstanceMembers);
            object priorObservers = startup.GetValue(reference);
            replace.Invoke(reference, new object[] { system });
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() =>
            {
                revoke.Invoke(reference, null);
                startup.SetValue(reference, priorObservers);
            }));
        }

        private static void Run(Action<Rig> body, int totalXp = 0, int bonusXp = 0, bool onOverview = false, int[] thresholds = null,
            float[] thresholdSeconds = null) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the App, DataManager and SaveManager rows.
            owned.Lease(new OwnedCharacterManagers());
            owned.LeaseRow<VisualQualityManager_SDT>();
            owned.LeaseRow<CutsceneManager>();
            owned.LeaseRow<OrnamentManager>();
            var rig = new Rig { App = ProcessManager.GetSystem<App>() };
            rig.Manager = owned.Register(owned.Lease(new OwnedTimeScaledObjects("lucid-xp-counter")).Add<ChallengeManager>("manager"));
            rig.Save = new SaveDataChallenges { TotalXPEarned = totalXp, BonusXPUnacknowledged = bonusXp };
            Set(rig.Manager, "m_saveDataChallenges", rig.Save); // Reflection: loaded from the open save and the App in the game
            Bind(owned, "s_challengeManagerRef", rig.Manager);
            Bind(owned, "s_appRef", rig.App);
            rig.Counter = owned.Host("Lucid xp animating counter", false).AddComponent<UIAnimatingCounter>();
            rig.Xp = owned.Host("Lucid xp counter", false).AddComponent<XPCounterBufferedIncrease>();
            Set(rig.Xp, "m_uiAnimatingCounter", rig.Counter);
            Set(rig.Xp, "m_isOnChallengesOverview", onOverview);
            if (thresholds != null)
            {
                var list = new List<XPCounterBufferedIncrease.EffectThresholds>();
                for (int index = 0; index < thresholds.Length; index++)
                {
                    int hit = index;
                    var effect = new XPCounterBufferedIncrease.EffectThresholds
                    {
                        Threshold = thresholds[index],
                        OnThresholdHit = new UnityEvent(),
                        CounterAnimationSeconds = thresholdSeconds != null ? thresholdSeconds[index] : 0f,
                    };
                    effect.OnThresholdHit.AddListener(() => rig.Hits[hit]++);
                    list.Add(effect);
                }
                Set(rig.Xp, "m_effectThresholds", list);
            }
            body(rig);
        });

        // dump.cs TypeDefIndex 2136 and the DummyDll rows (fields 0x04002009..0x0400200e, methods 0x0600305b..0x06003060).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(XPCounterBufferedIncrease), 0x00100001,
                new (string, int)[]
                {
                    ("m_uiAnimatingCounter", 0x0001), ("m_effectThresholds", 0x0001), ("m_isOnChallengesOverview", 0x0001),
                    ("s_challengeManagerRef", 0x0031), ("s_appRef", 0x0031), ("m_waitForBonusAcknowledgementCoroutine", 0x0001),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("Trigger", 0x0081), ("WaitForBonusAcknowledgement", 0x0081),
                    (".ctor", 0x1886), (".cctor", 0x1891),
                });
            AssertIl2CppOptions(typeof(XPCounterBufferedIncrease), 1, 2);
            Assert.That(typeof(XPCounterBufferedIncrease).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            foreach (string field in new[] { "m_uiAnimatingCounter", "m_effectThresholds", "m_isOnChallengesOverview" })
                Assert.That(AttributeTypes(typeof(XPCounterBufferedIncrease).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
            Assert.That(AttributeTypes(typeof(XPCounterBufferedIncrease).GetMethod("WaitForBonusAcknowledgement", Declared)),
                Is.EqualTo(new[] { typeof(IteratorStateMachineAttribute) }));
        }

        // dump.cs TypeDefIndex 2134 and the DummyDll rows (fields 0x0400200f..0x04002011, constructor 0x06003061).
        [Test]
        public void TheNestedEffectThresholdsMatchTheOriginalMetadata()
        {
            Type effect = typeof(XPCounterBufferedIncrease.EffectThresholds);
            AssertMembers(effect, 0x00102002,
                new (string, int)[] { ("Threshold", 0x0006), ("OnThresholdHit", 0x0006), ("CounterAnimationSeconds", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) });
            AssertIl2CppOptions(effect, 1, 2);
            Assert.That(effect.DeclaringType, Is.EqualTo(typeof(XPCounterBufferedIncrease)));
            Assert.That(effect.IsSerializable, Is.True);
        }

        // dump.cs TypeDefIndex 2135 and the DummyDll rows (fields 0x04002012..0x04002015, methods 0x06003062..0x06003067).
        [Test]
        public void TheIteratorStateMachineMatchesTheOriginalMetadata()
        {
            Type iterator = typeof(XPCounterBufferedIncrease).GetNestedType("<WaitForBonusAcknowledgement>d__10", BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null);
            AssertMembers(iterator, 0x00100103,
                new (string, int)[] { ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("<>4__this", 0x0006), ("<challengeManager>5__2", 0x0001) },
                new (string, int)[]
                {
                    (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1), ("MoveNext", 0x01e1),
                    ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1), ("System.Collections.IEnumerator.get_Current", 0x09e1),
                });
        }

        // Trigger 0x60F110: the gain is the current experience minus the last seen value (the key defaults to the current value
        // without storing it, so a first run gains nothing). The last threshold of the list that the gain reaches fires its event and
        // sets the count-up time; the counter then counts up from the last seen value to the current one.
        [Test]
        public void AGainFiresTheLastReachedThresholdAndCountsUpFromTheLastSeenValue()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 100);

                Call(rig.Xp, "Trigger");

                Assert.That(rig.Hits, Is.EqualTo(new[] { 0, 1, 0, 0, 0, 0, 0, 0 }), "a gain of 600 reaches the thresholds 100 and 500; the later one fires");
                Assert.That(rig.Counter.CurrentValue.Value, Is.EqualTo(100), "the counter starts at the last seen value");
                Assert.That(rig.AnimationSeconds, Is.EqualTo(3f));
                Assert.That(rig.CountUpRequested, Is.True);
                Assert.That(rig.LastSeen, Is.EqualTo(700), "the current value is stored as seen");
            }, totalXp: 700, thresholds: new[] { 100, 500, 1000 }, thresholdSeconds: new[] { 1.5f, 3f, 6f });
        }

        // The loop stops at the first threshold above the gain (0x60F40B, jge back only while the gain reaches it): a gain of 99
        // fires nothing with a threshold of 100, a gain of exactly 100 fires it.
        [TestCase(99, false, TestName = "AGainJustBelowTheThresholdFiresNothing")]
        [TestCase(100, true, TestName = "AGainEqualToTheThresholdFiresIt")]
        [TestCase(101, true, TestName = "AGainJustAboveTheThresholdFiresIt")]
        public void AThresholdFiresWhenTheGainReachesIt(int gain, bool fires)
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 1000);

                Call(rig.Xp, "Trigger");

                Assert.That(rig.Hits[0], Is.EqualTo(fires ? 1 : 0));
                Assert.That(rig.AnimationSeconds, Is.EqualTo(fires ? 4f : 2f), "the default count-up time is kept without a reached threshold");
                Assert.That(rig.CountUpRequested, Is.True);
            }, totalXp: 1000 + gain, thresholds: new[] { 100 }, thresholdSeconds: new[] { 4f });
        }

        // The thresholds are expected in ascending order: the first one above the gain ends the search even if a later one is
        // reached, so with 500, 100 and 300 a gain of 400 fires nothing.
        [Test]
        public void ThePassOverTheThresholdsStopsAtTheFirstOneAboveTheGain()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 0);

                Call(rig.Xp, "Trigger");

                Assert.That(rig.Hits, Is.All.EqualTo(0));
            }, totalXp: 400, thresholds: new[] { 500, 100, 300 });
        }

        // An unreached threshold list leaves the count-up at its own time; a missing hit event is skipped while the time is still taken.
        [Test]
        public void AReachedThresholdWithoutAnEventStillSetsTheCountUpTime()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 0);
                Get<List<XPCounterBufferedIncrease.EffectThresholds>>(rig.Xp, "m_effectThresholds")[0].OnThresholdHit = null;

                Assert.DoesNotThrow(() => Call(rig.Xp, "Trigger"));

                Assert.That(rig.AnimationSeconds, Is.EqualTo(5f));
                Assert.That(rig.CountUpRequested, Is.True);
            }, totalXp: 50, thresholds: new[] { 10 }, thresholdSeconds: new[] { 5f });
        }

        [Test]
        public void WithoutThresholdsAGainStillCountsUp()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 20);

                Call(rig.Xp, "Trigger");

                Assert.That(rig.Counter.CurrentValue.Value, Is.EqualTo(20));
                Assert.That(rig.AnimationSeconds, Is.EqualTo(2f));
                Assert.That(rig.CountUpRequested, Is.True);
                Assert.That(rig.LastSeen, Is.EqualTo(30));
            }, totalXp: 30);
        }

        // Without a gain (the experience is unchanged or was spent) the counter just shows the current value and no count-up is
        // asked for (0x60F4F0..0x60F520), no event fires and the current value is stored.
        [TestCase(30, 30, TestName = "NoGainShowsTheCurrentValueWithoutACountUp")]
        [TestCase(80, 30, TestName = "SpentExperienceShowsTheCurrentValueWithoutACountUp")]
        public void WithoutAGainTheCounterJustShowsTheCurrentValue(int lastSeen, int current)
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, lastSeen);

                Call(rig.Xp, "Trigger");

                Assert.That(rig.Counter.CurrentValue.Value, Is.EqualTo(current));
                Assert.That(rig.CountUpRequested, Is.False);
                Assert.That(rig.Hits, Is.All.EqualTo(0));
                Assert.That(rig.LastSeen, Is.EqualTo(current));
            }, totalXp: 30, thresholds: new[] { 1 });
        }

        [Test]
        public void ThePassWorksWithoutACounterToAnimate()
        {
            Run(rig =>
            {
                Set(rig.Xp, "m_uiAnimatingCounter", null); // Reflection: serialized reference left unassigned
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 0);

                Assert.DoesNotThrow(() => Call(rig.Xp, "Trigger"));

                Assert.That(rig.Hits[0], Is.EqualTo(1), "the effect still fires");
                Assert.That(rig.LastSeen, Is.EqualTo(40));
            }, totalXp: 40, thresholds: new[] { 5 });
        }

        // OnEnable 0x60EE20 outside the challenges overview triggers at once.
        [Test]
        public void EnablingOutsideTheOverviewTriggersAtOnce()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 10);

                Call(rig.Xp, "OnEnable");

                Assert.That(rig.LastSeen, Is.EqualTo(60));
                Assert.That(rig.CountUpRequested, Is.True);
            }, totalXp: 60, bonusXp: 25, onOverview: false);
        }

        // On the overview without unacknowledged bonus experience it triggers at once as well (jle 0x60EECA).
        [Test]
        public void EnablingOnTheOverviewWithoutBonusExperienceTriggersAtOnce()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 10);

                Call(rig.Xp, "OnEnable");

                Assert.That(rig.LastSeen, Is.EqualTo(60));
            }, totalXp: 60, bonusXp: 0, onOverview: true);
        }

        // On the overview with bonus experience still unacknowledged the counter is held at the last seen value, the stored value is
        // left alone and the wait coroutine is requested (the owned coroutine host refuses it and logs once).
        [Test]
        public void EnablingOnTheOverviewWithBonusExperienceHoldsTheLastSeenValue()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 10);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Call(rig.Xp, "OnEnable");

                Assert.That(rig.Counter.CurrentValue.Value, Is.EqualTo(10));
                Assert.That(rig.CountUpRequested, Is.False);
                Assert.That(rig.LastSeen, Is.EqualTo(10), "nothing is stored while the bonus waits");
                Assert.That(Get<Coroutine>(rig.Xp, "m_waitForBonusAcknowledgementCoroutine"), Is.Null, "the refused coroutine has no handle");
            }, totalXp: 60, bonusXp: 25, onOverview: true);
        }

        // The wait iterator (MoveNext 0x60F6C0) reads the bonus on every step: while it is above zero it yields null, and once it is
        // acknowledged the iterator triggers and ends.
        [Test]
        public void TheWaitStepsUntilTheBonusIsAcknowledgedAndThenTriggers()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.LastSeenXPValue, 10);
                var wait = (IEnumerator)Call(rig.Xp, "WaitForBonusAcknowledgement");

                Assert.That(wait.MoveNext(), Is.True);
                Assert.That(wait.Current, Is.Null);
                Assert.That(wait.MoveNext(), Is.True, "the bonus is still unacknowledged");
                Assert.That(rig.LastSeen, Is.EqualTo(10));

                rig.Save.BonusXPUnacknowledged = 0;
                Assert.That(wait.MoveNext(), Is.False);

                Assert.That(rig.LastSeen, Is.EqualTo(60), "the finished wait triggered");
                Assert.That(rig.CountUpRequested, Is.True);
            }, totalXp: 60, bonusXp: 25, onOverview: true);
        }

        [Test]
        public void ADisabledComponentWithNoWaitingCoroutineStopsNothing()
        {
            Run(rig => Assert.DoesNotThrow(() => Call(rig.Xp, "OnDisable")));
        }
    }
}
