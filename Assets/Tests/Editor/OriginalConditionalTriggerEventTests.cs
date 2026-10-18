using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime ConditionalTriggerEvent (TypeDef 0x020009f3, x86-64 0x6AE9B0..0x6AECAF, with
    // its private CheckType enum 0x020009f4) and ConditionalTriggerEventPersistentObjectBooleans
    // (0x020009f5, 0x6AECB0..0x6AF3CF). The Editor may send OnValidate while a component is added, and
    // TimeScaledComponent_SDT.OnValidate reads its s_config lookup, so each case installs an owned,
    // empty TimeCategoryLookup in that cache and restores the prior value. The hooks are called
    // directly; m_ignoreEnable is set so the inherited OnEnable returns before asking for the
    // TimeManager, and the TimeManager row is leased because the shutdown path reads its static reference.
    public sealed class OriginalConditionalTriggerEventTests
    {
        private static readonly Type CheckTypeEnum = typeof(ConditionalTriggerEvent).GetNestedType("CheckType", BindingFlags.NonPublic);
        private static readonly FieldInfo TimeConfig = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);

        private static void WithOwned(Action<Owned> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<TimeManager>();
            object prior = TimeConfig.GetValue(null);
            try
            {
                TimeConfig.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                body(owned);
            }
            finally
            {
                TimeConfig.SetValue(null, prior);
            }
        });

        private static T Add<T>(Owned owned, string name) where T : Component => owned.Host(name, false).AddComponent<T>();

        // AddComponent in Edit Mode leaves the four UnityEvent fields and m_conditions null (run30: CheckIsValid
        // 0x6AEC30 reads all four with GetPersistentEventCount and faulted). The restored bodies follow the native
        // ones, so the cases give the events a fresh UnityEvent first. The Editor also sends OnValidate during
        // AddComponent, and 0x6AECB0 walks the still-null m_conditions with no null test; that NullReferenceException
        // is expected and logged once per component.
        private static ConditionalTriggerEventPersistentObjectBooleans AddPersistentTrigger(Owned owned, string name)
        {
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));
            var trigger = Add<ConditionalTriggerEventPersistentObjectBooleans>(owned, name);
            foreach (string field in new[] { "m_whileConditionsNotMet", "m_onConditionsMet", "m_whileConditionsRemainMet", "m_onConditionsNoLongerMet" })
                if (Get<UnityEvent>(trigger, field) == null) Set(trigger, field, new UnityEvent());
            return trigger;
        }

        private static UnityEvent Logged(List<string> log, string label, Func<string> detail = null)
        {
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => log.Add(detail == null ? label : label + "(" + detail() + ")"));
            return unityEvent;
        }

        private static void SetCheckType(ConditionalTriggerEvent trigger, int value) => Set(trigger, "m_checkType", Enum.ToObject(CheckTypeEnum, value));

        // The probes stand in for the conditions; pattern "TF" makes the first answer true and the second false.
        private static void SetConditions(ConditionalTriggerEvent trigger, TriggerConditionProbe[] pool, string pattern)
        {
            foreach (TriggerConditionProbe probe in pool) probe.Evaluations = 0;
            var conditions = new TriggerCondition[pattern.Length];
            for (int index = 0; index < pattern.Length; index++)
            {
                pool[index].Result = pattern[index] == 'T';
                conditions[index] = pool[index];
            }
            Set(trigger, "m_conditions", conditions);
        }

        private static TriggerConditionProbe[] Probes(Owned owned) => new[] { Add<TriggerConditionProbe>(owned, "Lucid probe 0"), Add<TriggerConditionProbe>(owned, "Lucid probe 1") };

        private static int Evaluations(TriggerConditionProbe[] pool) => pool.Sum(probe => probe.Evaluations);

        // Metadata of the three TypeDefs, including the private enum's literals.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            AssertMembers(typeof(ConditionalTriggerEvent), 0x00100001,
                new[] { ("m_whileConditionsNotMet", 0x0004), ("m_onConditionsMet", 0x0004), ("m_whileConditionsRemainMet", 0x0004),
                        ("m_onConditionsNoLongerMet", 0x0004), ("m_triggerConditionsNoLongerMetOnDestroy", 0x0001), ("m_checkType", 0x0001),
                        ("m_conditions", 0x0004), ("m_conditionsMet", 0x0001) },
                new[] { ("Awake", 0x00c4), ("InternalUpdate", 0x00c4), ("Evaluate", 0x0084), ("EvaluateConditions", 0x0081),
                        ("GetConditionsCount", 0x0086), ("GetConditionsPassedCount", 0x0086), ("OnEnable", 0x00c4), ("OnDestroy", 0x00c6),
                        ("CheckIsValid", 0x00c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(ConditionalTriggerEvent), 1, 2);
            Assert.That(typeof(ConditionalTriggerEvent).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Assert.That(CheckTypeEnum, Is.Not.Null);
            AssertMembers(CheckTypeEnum, 0x00000103, new[] { ("value__", 0x0606), ("All", 0x8056), ("Any", 0x8056) }, new (string, int)[0]);
            Assert.That(Field(CheckTypeEnum, "All").GetRawConstantValue(), Is.EqualTo(0));
            Assert.That(Field(CheckTypeEnum, "Any").GetRawConstantValue(), Is.EqualTo(1));

            AssertMembers(typeof(ConditionalTriggerEventPersistentObjectBooleans), 0x00100001,
                new[] { ("m_persistentConditions", 0x0021), ("m_levelManager", 0x0001) },
                new[] { ("OnValidate", 0x00c4), ("OnEnable", 0x00c4), ("LevelManagerValid", 0x0081), ("Evaluate", 0x0081), ("OnDisable", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(ConditionalTriggerEventPersistentObjectBooleans), 2, 1);
            Assert.That(typeof(ConditionalTriggerEventPersistentObjectBooleans).BaseType, Is.EqualTo(typeof(ConditionalTriggerEvent)));
        }

        // EvaluateConditions 0x6AEAD0 walks the list once. All stops at the first condition that fails
        // and Any at the first that passes; a check type outside both passes on the first condition
        // (0x6AEB1E falls into 0x6AEB20), and an empty list passes for every type. The rows are
        // (check type, answers, result, conditions evaluated).
        [Test]
        public void ConditionsAreMetByAllByAnyAndByAnUnknownCheckType()
        {
            WithOwned(owned =>
            {
                var trigger = Add<ConditionalTriggerEvent>(owned, "Lucid conditional trigger");
                TriggerConditionProbe[] pool = Probes(owned);
                foreach (var row in new[]
                {
                    (type: 0, answers: "", result: true, evaluated: 0),
                    (type: 0, answers: "T", result: true, evaluated: 1),
                    (type: 0, answers: "F", result: false, evaluated: 1),
                    (type: 0, answers: "TF", result: false, evaluated: 2),
                    (type: 0, answers: "FT", result: false, evaluated: 1),
                    (type: 0, answers: "FF", result: false, evaluated: 1),
                    (type: 0, answers: "TT", result: true, evaluated: 2),
                    (type: 1, answers: "", result: true, evaluated: 0),
                    (type: 1, answers: "T", result: true, evaluated: 1),
                    (type: 1, answers: "F", result: false, evaluated: 1),
                    (type: 1, answers: "TF", result: true, evaluated: 1),
                    (type: 1, answers: "FT", result: true, evaluated: 2),
                    (type: 1, answers: "FF", result: false, evaluated: 2),
                    (type: 1, answers: "TT", result: true, evaluated: 1),
                    (type: 2, answers: "", result: true, evaluated: 0),
                    (type: 2, answers: "T", result: true, evaluated: 1),
                    (type: 2, answers: "F", result: true, evaluated: 1),
                    (type: 2, answers: "FF", result: true, evaluated: 1),
                    (type: 2, answers: "TF", result: true, evaluated: 1),
                })
                {
                    SetCheckType(trigger, row.type);
                    SetConditions(trigger, pool, row.answers);
                    Assert.That((bool)Call(trigger, "EvaluateConditions"), Is.EqualTo(row.result), row.ToString());
                    Assert.That(Evaluations(pool), Is.EqualTo(row.evaluated), row + " evaluations");
                }
            });
        }

        // GetConditionsCount 0x6AEB40 is the array length; GetConditionsPassedCount 0x6AEB50 evaluates
        // every condition, with no early exit, and counts the passing ones.
        [Test]
        public void CountsReportTheLengthAndEveryPassingCondition()
        {
            WithOwned(owned =>
            {
                var trigger = Add<ConditionalTriggerEvent>(owned, "Lucid conditional trigger");
                TriggerConditionProbe[] pool = Probes(owned);
                foreach (int checkType in new[] { 0, 1 })
                {
                    SetCheckType(trigger, checkType);
                    foreach (var row in new[] { (answers: "", passed: 0), (answers: "F", passed: 0), (answers: "TF", passed: 1), (answers: "FT", passed: 1), (answers: "TT", passed: 2) })
                    {
                        SetConditions(trigger, pool, row.answers);
                        Assert.That(trigger.GetConditionsCount(), Is.EqualTo(row.answers.Length), row.ToString());
                        Assert.That(trigger.GetConditionsPassedCount(), Is.EqualTo(row.passed), row.ToString());
                        Assert.That(Evaluations(pool), Is.EqualTo(row.answers.Length), row + " every condition is evaluated once");
                    }
                }
                Set(trigger, "m_conditions", null);
                Assert.Throws<NullReferenceException>(() => trigger.GetConditionsCount());
            });
        }

        // Evaluate 0x6AEA00 and InternalUpdate 0x6AE9F0 (a tail call into it). Meeting the conditions
        // raises m_onConditionsMet before the flag is set, then m_whileConditionsRemainMet, which also
        // runs on every later met update. Losing them clears the flag, then raises
        // m_onConditionsNoLongerMet alone; an unmet update with the flag clear raises
        // m_whileConditionsNotMet. A missing event is skipped.
        [Test]
        public void EvaluateRaisesTheEventsOfTheMetStateInTheOriginalOrder()
        {
            WithOwned(owned =>
            {
                var trigger = Add<ConditionalTriggerEvent>(owned, "Lucid conditional trigger");
                TriggerConditionProbe probe = Probes(owned)[0];
                var log = new List<string>();
                Func<string> flag = () => Get<bool>(trigger, "m_conditionsMet").ToString();
                Set(trigger, "m_whileConditionsNotMet", Logged(log, "notMet", flag));
                Set(trigger, "m_onConditionsMet", Logged(log, "met", flag));
                Set(trigger, "m_whileConditionsRemainMet", Logged(log, "remain", flag));
                Set(trigger, "m_onConditionsNoLongerMet", Logged(log, "noLonger", flag));
                Set(trigger, "m_conditions", new TriggerCondition[] { probe });

                foreach (var step in new[]
                {
                    (answer: false, expected: new[] { "notMet(False)" }),
                    (answer: false, expected: new[] { "notMet(False)" }),
                    (answer: true, expected: new[] { "met(False)", "remain(True)" }),
                    (answer: true, expected: new[] { "remain(True)" }),
                    (answer: false, expected: new[] { "noLonger(False)" }),
                    (answer: false, expected: new[] { "notMet(False)" }),
                })
                {
                    probe.Result = step.answer;
                    log.Clear();
                    Call(trigger, "InternalUpdate", 0.1f);
                    Assert.That(log, Is.EqualTo(step.expected), "answer " + step.answer);
                }

                foreach (string field in new[] { "m_whileConditionsNotMet", "m_onConditionsMet", "m_whileConditionsRemainMet", "m_onConditionsNoLongerMet" })
                    Set(trigger, field, null);
                foreach (bool answer in new[] { false, true, true, false, false })
                {
                    probe.Result = answer;
                    Assert.DoesNotThrow(() => Call(trigger, "Evaluate"));
                }
            });
        }

        // OnDestroy 0x6AEBE0 runs the inherited shutdown, then, only with
        // m_triggerConditionsNoLongerMetOnDestroy (+0x70) set and the conditions met (+0x80), clears
        // the flag (0x6AEBFF) and then raises m_onConditionsNoLongerMet (0x6AEC17), which sees it clear.
        [Test]
        public void DestroyRaisesTheNoLongerMetEventOnlyWhenRequestedAndMet()
        {
            WithOwned(owned =>
            {
                foreach (var row in new[]
                {
                    (onDestroy: true, met: true, raised: new[] { "noLonger(False)" }, flagAfter: false),
                    (onDestroy: true, met: false, raised: new string[0], flagAfter: false),
                    (onDestroy: false, met: true, raised: new string[0], flagAfter: true),
                    (onDestroy: false, met: false, raised: new string[0], flagAfter: false),
                })
                {
                    var trigger = Add<ConditionalTriggerEvent>(owned, "Lucid conditional trigger");
                    var log = new List<string>();
                    Set(trigger, "m_onConditionsNoLongerMet", Logged(log, "noLonger", () => Get<bool>(trigger, "m_conditionsMet").ToString()));
                    Set(trigger, "m_triggerConditionsNoLongerMetOnDestroy", row.onDestroy);
                    Set(trigger, "m_conditionsMet", row.met);
                    trigger.OnDestroy();
                    Assert.That(log, Is.EqualTo(row.raised), row.ToString());
                    Assert.That(Get<bool>(trigger, "m_conditionsMet"), Is.EqualTo(row.flagAfter), row.ToString());
                }
            });
        }

        // CheckIsValid 0x6AEC30 needs the inherited check and a persistent (inspector) listener on
        // any of the four events; listeners added in code do not count. Awake 0x6AE9B0 and OnEnable
        // 0x6AEBA0 disable the component when it is not valid.
        [Test]
        public void OnlyAPersistentListenerMakesTheTriggerValid()
        {
            WithOwned(owned =>
            {
                GameObject targetHost = owned.Host("Lucid persistent listener target", false);
                var target = targetHost.AddComponent<EventInvocationTargetProbe>();
                string[] events = { "m_whileConditionsNotMet", "m_onConditionsMet", "m_whileConditionsRemainMet", "m_onConditionsNoLongerMet" };
                foreach (string persistentEvent in events.Concat(new string[] { null }))
                {
                    var trigger = Add<ConditionalTriggerEvent>(owned, "Lucid conditional trigger");
                    foreach (string name in events)
                    {
                        var unityEvent = new UnityEvent();
                        unityEvent.AddListener(() => { });
                        if (name == persistentEvent) UnityEventTools.AddPersistentListener(unityEvent, target.NoArguments);
                        Set(trigger, name, unityEvent);
                    }
                    bool valid = persistentEvent != null;
                    Assert.That(trigger.CheckIsValid(), Is.EqualTo(valid), persistentEvent ?? "runtime listeners only");

                    Call(trigger, "Awake");
                    Assert.That(trigger.enabled, Is.EqualTo(valid), "Awake " + persistentEvent);
                    trigger.enabled = true;
                    Set(trigger, "m_ignoreEnable", true);
                    Call(trigger, "OnEnable");
                    Assert.That(trigger.enabled, Is.EqualTo(valid), "OnEnable " + persistentEvent);
                }
            });
        }

        private static LevelManager NewLevelManager(Owned owned)
        {
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<AudioManager>();
            owned.LeaseRow<SaveManager>();
            var levelManager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
            typeof(LevelManager).GetProperty("CurrentLevelValid").SetValue(levelManager, false);
            return owned.Register(levelManager);
        }

        private static Delegate[] LevelActivated(LevelManager levelManager) =>
            Get<Action<LevelManagerLevel>>(levelManager, "m_actionOnLevelActivated")?.GetInvocationList() ?? new Delegate[0];

        // OnEnable 0x6AEDB0 sets m_updateOn to None (0xB6FA1284) before the inherited OnEnable and
        // subscribes Evaluate to every condition's persistent object, listing the conditions; then it
        // evaluates once and asks the registered LevelManager for LevelManagerValid, which stores it
        // and follows level activation. OnDisable 0x6AF100 removes the level action first (a missing
        // manager faults before any unsubscription), then the object subscriptions, and clears the list.
        [Test]
        public void EnablingFollowsThePersistentObjectsAndLevelActivationUntilDisabled()
        {
            WithOwned(owned =>
            {
                LevelManager levelManager = NewLevelManager(owned);
                var persistent = new[]
                {
                    Add<PersistentLevelObjectBoolean>(owned, "Lucid persistent 0"),
                    Add<PersistentLevelObjectBoolean>(owned, "Lucid persistent 1"),
                };
                var conditions = new TriggerCondition[2];
                for (int index = 0; index < 2; index++)
                {
                    var condition = Add<TriggerCondition_PersistentLevelObjectBoolean>(owned, "Lucid persistent condition " + index);
                    Set(condition, "m_persistentLevelObject", persistent[index]);
                    conditions[index] = condition;
                }
                Set(persistent[0], "m_currentValue", true);

                var trigger = AddPersistentTrigger(owned, "Lucid persistent trigger");
                var log = new List<string>();
                Set(trigger, "m_whileConditionsNotMet", Logged(log, "notMet"));
                Set(trigger, "m_onConditionsMet", Logged(log, "met"));
                Set(trigger, "m_whileConditionsRemainMet", Logged(log, "remain"));
                Set(trigger, "m_conditions", conditions);
                Set(trigger, "m_ignoreEnable", true);
                Assert.That(Get<UpdateOn>(trigger, "m_updateOn"), Is.EqualTo(UpdateOn.FixedUpdate));

                Call(trigger, "OnEnable");
                Assert.That(Get<UpdateOn>(trigger, "m_updateOn"), Is.EqualTo(UpdateOn.None));
                Assert.That(Get<List<TriggerCondition_PersistentLevelObjectBoolean>>(trigger, "m_persistentConditions"), Is.EqualTo(conditions));
                Assert.That(log, Is.EqualTo(new[] { "notMet" }), "one evaluation: the first object is set, the second is not");
                Assert.That(Get<LevelManager>(trigger, "m_levelManager"), Is.SameAs(levelManager));
                Assert.That(LevelActivated(levelManager).Count(callback => ReferenceEquals(callback.Target, trigger)), Is.EqualTo(1));

                log.Clear();
                Set(persistent[1], "m_currentValue", true);
                Call(persistent[1], "OnStateChanged", true);
                Assert.That(log, Is.EqualTo(new[] { "met", "remain" }));
                log.Clear();
                Get<Action<LevelManagerLevel>>(levelManager, "m_actionOnLevelActivated")(null);
                Assert.That(log, Is.EqualTo(new[] { "remain" }), "a level activation evaluates again");

                Set(trigger, "m_levelManager", null);
                Assert.Throws<NullReferenceException>(() => Call(trigger, "OnDisable"));
                Assert.That(Get<List<TriggerCondition_PersistentLevelObjectBoolean>>(trigger, "m_persistentConditions"), Has.Count.EqualTo(2),
                    "the fault precedes the unsubscription");
                Set(trigger, "m_levelManager", levelManager);

                Call(trigger, "OnDisable");
                Assert.That(Get<List<TriggerCondition_PersistentLevelObjectBoolean>>(trigger, "m_persistentConditions"), Is.Empty);
                Assert.That(LevelActivated(levelManager).Any(callback => ReferenceEquals(callback.Target, trigger)), Is.False);
                log.Clear();
                Call(persistent[1], "OnStateChanged", false);
                Assert.That(log, Is.Empty, "the objects no longer reach the trigger");
            });
        }

        private static (ConditionalTriggerEventPersistentObjectBooleans trigger, TriggerCondition_PersistentLevelObjectBoolean condition, List<string> log) NewPersistentTrigger(Owned owned)
        {
            var persistent = Add<PersistentLevelObjectBoolean>(owned, "Lucid persistent");
            var condition = Add<TriggerCondition_PersistentLevelObjectBoolean>(owned, "Lucid persistent condition");
            Set(condition, "m_persistentLevelObject", persistent);
            var trigger = AddPersistentTrigger(owned, "Lucid persistent trigger");
            var log = new List<string>();
            Set(trigger, "m_whileConditionsNotMet", Logged(log, "notMet"));
            Set(trigger, "m_ignoreEnable", true);
            return (trigger, condition, log);
        }

        // LevelManagerValid 0x6AF050 passes triggerImmediately = true (edx = 1 at 0x6AF0D4), so a manager
        // whose current level is valid evaluates the conditions a second time at once.
        [Test]
        public void ALevelThatIsAlreadyActiveEvaluatesAgainWhenFollowed()
        {
            WithOwned(owned =>
            {
                LevelManager levelManager = NewLevelManager(owned);
                typeof(LevelManager).GetProperty("CurrentLevelValid").SetValue(levelManager, true);
                var definition = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(levelManager, "m_currentLevel", new LevelManagerLevel(definition, definition));
                var (trigger, condition, log) = NewPersistentTrigger(owned);
                Set(trigger, "m_conditions", new TriggerCondition[] { condition });
                Call(trigger, "OnEnable");
                Assert.That(log, Is.EqualTo(new[] { "notMet", "notMet" }), "the direct evaluation, then the immediate level-activated one");
            });
        }

        // The loop casts each condition to TriggerCondition_PersistentLevelObjectBoolean (0x6AEEEF and
        // 0x6AEF01 jump to RaiseInvalidCastException 0x6AF042) and reads its object (+0x28, 0x6AEF07)
        // without a type test, so a foreign condition faults before anything is subscribed and a
        // null entry faults after the entries before it.
        [Test]
        public void EnablingFaultsOnAConditionThatIsNotAPersistentBoolean()
        {
            WithOwned(owned =>
            {
                // PersistentLevelObject's field initialiser asks for GetSystemRef<MissionManager>(), which creates the row.
                owned.LeaseRow<MissionManager>();
                var (trigger, condition, log) = NewPersistentTrigger(owned);
                Set(trigger, "m_conditions", new TriggerCondition[] { Add<TriggerConditionProbe>(owned, "Lucid other condition") });
                Assert.Throws<InvalidCastException>(() => Call(trigger, "OnEnable"));
                Assert.That(Get<List<TriggerCondition_PersistentLevelObjectBoolean>>(trigger, "m_persistentConditions"), Is.Empty);
                Assert.That(log, Is.Empty);

                Set(trigger, "m_conditions", new TriggerCondition[] { condition, null });
                Assert.Throws<NullReferenceException>(() => Call(trigger, "OnEnable"));
                Assert.That(Get<List<TriggerCondition_PersistentLevelObjectBoolean>>(trigger, "m_persistentConditions"), Is.EqualTo(new[] { condition }));
                Assert.That(log, Is.Empty);
            });
        }

        // OnValidate 0x6AECB0 runs the inherited validation, then logs one error through HLOutput for
        // every entry that is not a TriggerCondition_PersistentLevelObjectBoolean, a null entry included.
        [Test]
        public void ValidationReportsEveryConditionThatIsNotAPersistentBoolean()
        {
            const string Message = "ConditionalTriggerEventPersistentObjectBooleans expects all conditions to be of the type TriggerCondition_PersistentLevelObjectBoolean";
            WithOwned(owned =>
            {
                var trigger = AddPersistentTrigger(owned, "Lucid persistent trigger");
                var persistentCondition = Add<TriggerCondition_PersistentLevelObjectBoolean>(owned, "Lucid persistent condition");
                var other = Add<TriggerConditionProbe>(owned, "Lucid other condition");

                Set(trigger, "m_conditions", new TriggerCondition[] { persistentCondition });
                Assert.That(HLOutputDiagnosticCapture.CaptureErrors(() => Call(trigger, "OnValidate")), Is.Empty);
                Set(trigger, "m_conditions", new TriggerCondition[0]);
                Assert.That(HLOutputDiagnosticCapture.CaptureErrors(() => Call(trigger, "OnValidate")), Is.Empty);
                Set(trigger, "m_conditions", new TriggerCondition[] { persistentCondition, other, null, persistentCondition });
                Assert.That(HLOutputDiagnosticCapture.CaptureErrors(() => Call(trigger, "OnValidate")), Is.EqualTo(new[] { Message + ":0", Message + ":0" }));
            });
        }
    }
}
