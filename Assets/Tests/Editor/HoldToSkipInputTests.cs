using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime HoldToSkipInput (TypeDef 0x02000967, x86-64 0x68AAF0..0x68B5A0, iterators ProcessHeldTouch
    // MoveNext 0x68B7D0 and FadeOutPrompt MoveNext 0x68B670). Everything it reads is real: a registered App and
    // DataManager (OwnedCharacterManagers, whose inactive CoroutineUtils host also refuses started coroutines with the
    // engine's logged error, expected wherever the original starts one), a TimeManager holding the System category at
    // scale 2, a SystemConfiguration holding a CoreGameConfiguration, and a DataManager input definition. The component
    // and its widgets are on owned inactive hosts; Start and the other callbacks are called directly.
    //
    // Time.deltaTime cannot be controlled in Edit Mode, so the expectations that depend on it are the engine's own
    // delta times the System category scale of 2 (TimeManager.GetDeltaTime, 0x5f7860), and the cases that pin an exact
    // value use a hold requirement or modifier delay that makes the outcome independent of it. The ControlMapping
    // subscriptions made by Start and removed by OnDestroy are not observed here; a lease restores the tables.
    public sealed class HoldToSkipInputTests
    {
        // Undefined, so no other component's subscription can share the key Start makes for m_gameInput.
        private const GameInput OwnedInput = (GameInput)(int.MinValue + 0x5348);

        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Scene
        {
            public Owned Owned;
            public HoldToSkipInput Input;
            public UIWidgetButton Button;
            public Slider Progress;
            public CanvasGroup Prompt;
            public LocalisedUIString Label;
            public ModifierDelayUntil Modifier;
            public GlobalConstantDefinition_Input Definition;
            public CoreGameConfiguration Config;
            public App App;
            public DataManager Data;
            public int FullSkips;
            public int SingleSkips;
            public readonly List<bool> CursorReleases = new List<bool>();
        }

        // Start subscribes on two inputs (m_gameInput and ReleaseCursor), and ControlMapping.Unsubscribe removes the
        // listener but never the dictionary key. This lease records every ControlMapping table before the case, drops
        // the keys the case added and fails if a pre-existing entry was replaced or its listeners changed. It does
        // not depend on the component's own OnDestroy.
        private sealed class ControlMappingLease : IDisposable
        {
            private readonly Dictionary<FieldInfo, Dictionary<object, (object Action, Delegate[] Listeners)>> m_tables =
                new Dictionary<FieldInfo, Dictionary<object, (object Action, Delegate[] Listeners)>>();
            private readonly HashSet<GameInput> m_exclusive;

            public ControlMappingLease()
            {
                foreach (FieldInfo field in typeof(ControlMapping).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                {
                    if (!typeof(IDictionary).IsAssignableFrom(field.FieldType)) continue;
                    var rows = new Dictionary<object, (object Action, Delegate[] Listeners)>();
                    foreach (DictionaryEntry row in (IDictionary)field.GetValue(null)) rows.Add(row.Key, (row.Value, Listeners(row.Value)));
                    m_tables.Add(field, rows);
                }
                m_exclusive = new HashSet<GameInput>(OwnedControlInputs.Exclusive());
            }

            private static Delegate[] Listeners(object action) =>
                ((IEnumerable)action.GetType().GetMethod("GetInvocationList").Invoke(action, null)).Cast<Delegate>().ToArray();

            public void Dispose()
            {
                var changed = new List<string>();
                foreach (KeyValuePair<FieldInfo, Dictionary<object, (object Action, Delegate[] Listeners)>> table in m_tables)
                {
                    var live = (IDictionary)table.Key.GetValue(null);
                    foreach (object key in live.Keys.Cast<object>().Where(key => !table.Value.ContainsKey(key)).ToArray()) live.Remove(key);
                    bool same = live.Count == table.Value.Count && table.Value.All(row =>
                        live.Contains(row.Key) && ReferenceEquals(live[row.Key], row.Value.Action) && Listeners(live[row.Key]).SequenceEqual(row.Value.Listeners));
                    if (!same) changed.Add(table.Key.Name);
                }
                if (!OwnedControlInputs.Exclusive().SetEquals(m_exclusive)) changed.Add("s_exclusiveGameInputs");
                if (changed.Count != 0) throw new InvalidOperationException("Shared ControlMapping state changed: " + string.Join(", ", changed));
            }
        }

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WithInput(Action<Scene> body, bool tapToSkip = true, float required = 1f, float maxTap = 0.3f,
            float fadeOut = 0f, float modifierDelay = -1f, bool withModifier = true) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: its rows and coroutine host outlive the case's.
            owned.Lease(new OwnedCharacterManagers());
            owned.Lease(new ControlMappingLease());
            // GetConfig finds the asset by the CRC of its type name in this registered system's stack.
            SystemConfiguration configuration = owned.Register(new SystemConfiguration());
            var scene = new Scene { Owned = owned };
            scene.Config = owned.Track(ScriptableObject.CreateInstance<CoreGameConfiguration>());
            SetSerialized(scene.Config, "m_tapToSkipEnabled", property => property.boolValue = tapToSkip);
            configuration.StackableData.AddOverride(HLCRC32.GenerateInt(nameof(CoreGameConfiguration)), (SystemConfigurationAsset)scene.Config);
            owned.Register(NewTimeManager(owned, TimeCategory.System, 2f));
            scene.App = ProcessManager.GetSystem<App>();
            scene.App.OnForceReleaseCursor = value => scene.CursorReleases.Add(value);
            scene.Data = ProcessManager.GetSystem<DataManager>();
            scene.Modifier = owned.Track(ScriptableObject.CreateInstance<ModifierDelayUntil>());
            SetSerialized(scene.Modifier, "m_delaySeconds", property => property.floatValue = modifierDelay);
            scene.Definition = owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Input>());
            scene.Definition.DelayUntilModifier = withModifier ? scene.Modifier : null;
            scene.Data.GlobalConstantDefinitions[GlobalConstantType.Input] = scene.Definition;

            scene.Input = owned.Host("Lucid owned hold to skip", false).AddComponent<HoldToSkipInput>();
            GameObject widgets = owned.Host("Lucid owned hold to skip widgets", false);
            scene.Button = widgets.AddComponent<UIWidgetButton>();
            scene.Progress = widgets.AddComponent<Slider>();
            scene.Prompt = widgets.AddComponent<CanvasGroup>();
            scene.Label = widgets.AddComponent<LocalisedUIString>();
            SetSerialized(scene.Input, "m_button", property => property.objectReferenceValue = scene.Button);
            SetSerialized(scene.Input, "m_progress", property => property.objectReferenceValue = scene.Progress);
            SetSerialized(scene.Input, "m_visualPrompt", property => property.objectReferenceValue = scene.Prompt);
            SetSerialized(scene.Input, "m_labelString", property => property.objectReferenceValue = scene.Label);
            SetSerialized(scene.Input, "m_gameInput", property => property.intValue = (int)OwnedInput);
            SetSerialized(scene.Input, "m_requiredHoldTime", property => property.floatValue = required);
            SetSerialized(scene.Input, "m_maxTapDuration", property => property.floatValue = maxTap);
            SetSerialized(scene.Input, "m_promptFadeoutTime", property => property.floatValue = fadeOut);
            Get<UnityEvent>(scene.Input, "m_onFullSkipEvent").AddListener(() => scene.FullSkips++);
            Get<UnityEvent>(scene.Input, "m_onSkipOnceEvent").AddListener(() => scene.SingleSkips++);
            // OnDestroy is the original's own teardown; the ControlMappingLease restores the shared tables.
            try { body(scene); }
            finally { Call(scene.Input, "OnDestroy"); }
        });

        // The engine's delta time times the System category scale.
        private static float Step => Time.deltaTime * 2f;

        private static float Held(Scene scene) => Get<float>(scene.Input, "m_inputHeldSeconds");
        private static float Last(Scene scene) => Get<float>(scene.Input, "m_lastHeldInputValue");
        private static bool Holding(Scene scene) => Get<bool>(scene.Input, "m_holdingInput");

        private static void Supply(Scene scene, float value) => Call(scene.Input, "SupplyHeldInput", value);
        private static void End(Scene scene) => Call(scene.Input, "EndHeldInput", 0f);

        private static void ExpectRefusedCoroutine() => LogAssert.Expect(LogType.Error, RefusedCoroutine);

        // Constructor 0x68B5A0: 0.3f and 1.0f (the 8 bytes at 0x29a7af0), nothing else serialized, and the App and
        // DataManager references read from their rows.
        [Test]
        public void ConstructionStoresTheOriginalDefaults()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                var input = owned.Host("Lucid owned default hold to skip", false).AddComponent<HoldToSkipInput>();
                var serialized = new SerializedObject(input);
                Assert.That(serialized.FindProperty("m_maxTapDuration").floatValue, Is.EqualTo(0.3f));
                Assert.That(serialized.FindProperty("m_requiredHoldTime").floatValue, Is.EqualTo(1f));
                Assert.That(serialized.FindProperty("m_promptFadeoutTime").floatValue, Is.EqualTo(0f));
                Assert.That(Get<SystemRef<App>>(input, "m_appRef").GetSafe(), Is.SameAs(ProcessManager.GetSystem<App>()));
                Assert.That(Get<SystemRef<DataManager>>(input, "m_dataManagerRef").GetSafe(), Is.SameAs(ProcessManager.GetSystem<DataManager>()));
            });
        }

        // SetLabelString 0x68AFF0 forwards to LocalisedUIString.set_Identifier (0x1a35860).
        [Test]
        public void SetLabelStringStoresTheIdentifierOnTheLabel()
        {
            WithInput(scene =>
            {
                scene.Input.SetLabelString(Strings.MISSING);
                Assert.That(scene.Label.Identifier, Is.EqualTo(Strings.MISSING));
            });
        }

        // Start 0x68AAF0: stores the time manager (0x68abf2) and the CoreGameConfiguration (0x68ac17), hides the
        // widgets (zero held time, bar 0 through Slider.set_value slot 47, prompt alpha 0) and takes the modifier from
        // the Input global constant definition (0x68ae60..0x68aec4).
        [Test]
        public void StartResetsTheDisplayAndReadsItsSystems()
        {
            WithInput(scene =>
            {
                scene.Progress.value = 0.7f;
                scene.Prompt.alpha = 0.9f;
                Call(scene.Input, "Start");
                Assert.That(scene.Progress.value, Is.EqualTo(0f));
                Assert.That(scene.Prompt.alpha, Is.EqualTo(0f));
                Assert.That(Held(scene), Is.EqualTo(0f));
                Assert.That(Get<TimeManager>(scene.Input, "m_timeManager"), Is.SameAs(ProcessManager.GetSystem<TimeManager>()));
                Assert.That(Get<CoreGameConfiguration>(scene.Input, "m_coreGameConfiguration"), Is.SameAs(scene.Config));
                Assert.That(Get<ModifierDelayUntil>(scene.Input, "m_inputModifier"), Is.SameAs(scene.Modifier));
            });
        }

        // 0x68aea9..0x68aec4: the modifier is read only from an input definition that has one; the dictionary lookup
        // (0x68ae65) faults when the Input definition is missing.
        [Test]
        public void StartWithoutAModifierKeepsItNullAndWithoutTheDefinitionFaults()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                Assert.That(Get<ModifierDelayUntil>(scene.Input, "m_inputModifier") == null, Is.True);
                scene.Data.GlobalConstantDefinitions.Remove(GlobalConstantType.Input);
                Assert.Throws<KeyNotFoundException>(() => Call(scene.Input, "Start"));
            }, withModifier: false);
        }

        // Start 0x68ada3..0x68ae0b registers OnButtonHoldBegin and OnButtonHoldEnd on the button. A pointer down holds
        // and starts the touch coroutine (0x68b063; the refused start is the logged error), the pointer up releases it
        // (0x68b109) through EndHeldInput, which starts no fade without any held input.
        [Test]
        public void TheButtonsPointerEventsHoldAndReleaseTheInput()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                ExpectRefusedCoroutine();
                scene.Button.OnPointerDown(null);
                Assert.That(Holding(scene), Is.True);
                scene.Button.OnPointerUp(null);
                Assert.That(Holding(scene), Is.False);
            });
        }

        // SupplyHeldInput 0x68B220: the value is recorded first and a zero (jne/jp at 0x68b231..0x68b233) stops there,
        // so nothing accumulates and the prompt stays hidden; NaN is not zero and lights the prompt.
        [Test]
        public void AZeroInputOnlyRecordsItselfAndAnyOtherLightsThePrompt()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                Supply(scene, 0f);
                Assert.That((Last(scene), Held(scene), scene.Prompt.alpha, scene.FullSkips), Is.EqualTo((0f, 0f, 0f, 0)));
                Supply(scene, float.NaN);
                Assert.That(Last(scene), Is.NaN);
                Assert.That(scene.Prompt.alpha, Is.EqualTo(1f));
                Supply(scene, 0.5f);
                Assert.That(Last(scene), Is.EqualTo(0.5f));
            }, required: 100f);
        }

        // 0x68b25c..0x68b266: the held time grows by the System category's delta time per supplied update, and the bar
        // shows held / required clamped to [0,1] (0x68b28a..0x68b2b3).
        [Test]
        public void TheHeldTimeAndBarFollowTheCategorysDeltaTime()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                Supply(scene, 1f);
                Assert.That(Held(scene), Is.EqualTo(Step).Within(1e-6f));
                Assert.That(scene.Progress.value, Is.EqualTo(Step / 100f).Within(1e-6f));
                Supply(scene, 1f);
                Assert.That(Held(scene), Is.EqualTo(Step * 2f).Within(1e-6f));
                Assert.That(scene.Prompt.alpha, Is.EqualTo(1f));
            }, required: 100f);
        }

        // 0x68b270..0x68b27b: the full-skip event fires on every update whose held time is strictly above the
        // requirement (jbe at 0x68b26e). A negative requirement is always exceeded; a large one never is.
        [Test]
        public void TheFullSkipEventFiresOnEveryUpdatePastTheRequirement()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                Supply(scene, 1f);
                Supply(scene, 1f);
                Assert.That(scene.FullSkips, Is.EqualTo(2));
            }, required: -1f);
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                Supply(scene, 1f);
                Supply(scene, 1f);
                Assert.That(scene.FullSkips, Is.EqualTo(0));
            }, required: 100f);
        }

        // EndHeldInput 0x68B130: a release shorter than the maximum tap duration (0x68b1b6: strictly greater than the
        // held time) fires the single-skip event, but only while tap to skip is enabled (config +0x43, 0x68b1c3).
        [Test]
        public void AShortReleaseSkipsOnceOnlyWhenTapToSkipIsEnabled()
        {
            foreach ((bool tapToSkip, float maxTap, int skips) in new[] { (true, 0.3f, 1), (false, 0.3f, 0), (true, 0f, 0) })
            {
                WithInput(scene =>
                {
                    Call(scene.Input, "Start");
                    End(scene);
                    Assert.That(scene.SingleSkips, Is.EqualTo(skips), "tap to skip " + tapToSkip + ", max tap " + maxTap);
                }, tapToSkip: tapToSkip, maxTap: maxTap);
            }
        }

        // 0x68b140..0x68b1f8: the last held input is cleared, the modifier is reset (virtual Reset, slot 7), the held
        // time and bar are cleared, and the prompt fades out only if the last held input was not zero (cmpneqss), by
        // starting FadeOutPrompt on the coroutine host - the logged refusal here.
        [Test]
        public void EndingClearsTheStateAndFadesThePromptOnlyAfterHeldInput()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                End(scene);
                Assert.That((Last(scene), Held(scene), scene.Progress.value), Is.EqualTo((0f, 0f, 0f)), "no held input: no fade, so no coroutine start");

                Supply(scene, 0.5f);
                ExpectRefusedCoroutine();
                End(scene);
                Assert.That((Last(scene), Held(scene), scene.Progress.value), Is.EqualTo((0f, 0f, 0f)));
            }, required: 100f);
        }

        // 0x68b1a4: the delay modifier is told to Reset on every release, which empties its per-input times.
        [Test]
        public void EndingResetsTheDelayModifier()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                scene.Modifier.Modify(1f, 0f, OwnedInput);
                var times = Get<IDictionary>(scene.Modifier, "m_cachedTimeByInput");
                Assert.That(times.Count, Is.EqualTo(1));
                End(scene);
                Assert.That(times.Count, Is.EqualTo(0));
            });
        }

        // ProcessHeldTouch MoveNext 0x68B7D0, while holding: with no modifier a touch counts as 1 (0x68b849), with a
        // modifier it is the modifier's result for the bound input (virtual Modify, 0x68b88d) - 0 while the
        // delay has not elapsed. Each step yields null (0x68b93d). Once released the iterator ends (0x68b853).
        [Test]
        public void AHeldTouchSuppliesFullStrengthOrTheModifiersResultEveryStepUntilReleased()
        {
            foreach ((bool withModifier, float delay, float expected) in new[] { (false, -1f, 1f), (true, -1f, 1f), (true, 1e9f, 0f) })
            {
                WithInput(scene =>
                {
                    Call(scene.Input, "Start");
                    ExpectRefusedCoroutine();
                    scene.Button.OnPointerDown(null);
                    var steps = (IEnumerator)Call(scene.Input, "ProcessHeldTouch");
                    Assert.That(steps.MoveNext(), Is.True);
                    Assert.That(steps.Current, Is.Null);
                    Assert.That(Last(scene), Is.EqualTo(expected), "modifier " + withModifier + " delay " + delay);
                    Assert.That(scene.Prompt.alpha, Is.EqualTo(expected), "the prompt is lit by a nonzero input only");
                    Assert.That(steps.MoveNext(), Is.True);
                    if (expected != 0f) ExpectRefusedCoroutine();
                    scene.Button.OnPointerUp(null);
                    Assert.That(steps.MoveNext(), Is.False);
                }, withModifier: withModifier, modifierDelay: delay, required: 100f);
            }
        }

        // FadeOutPrompt MoveNext 0x68B670: with no fade time the prompt is hidden at once; otherwise each step adds
        // the category's delta time and sets the alpha to 1 - elapsed / fade time (0x68b6f4..0x68b708), yielding null.
        [Test]
        public void TheFadeOutHidesAtOnceWithoutATimeAndOtherwiseStepsTheAlphaDown()
        {
            WithInput(scene =>
            {
                scene.Prompt.alpha = 1f;
                var steps = (IEnumerator)Call(scene.Input, "FadeOutPrompt");
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(scene.Prompt.alpha, Is.EqualTo(0f));
            }, fadeOut: 0f);
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                var steps = (IEnumerator)Call(scene.Input, "FadeOutPrompt");
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.Null);
                Assert.That(scene.Prompt.alpha, Is.EqualTo(1f - Step / 1e9f).Within(1e-6f));
            }, fadeOut: 1e9f);
        }

        // ForceReleaseCursor 0x68B370 calls the App's OnForceReleaseCursor delegate with true (esi=1).
        [Test]
        public void ForceReleaseCursorRequestsTheReleaseFromTheApp()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "ForceReleaseCursor", 1f);
                Assert.That(scene.CursorReleases, Is.EqualTo(new[] { true }));
            });
        }

        // OnDestroy 0x68B3D0: the bar and held time are cleared, the cursor release is revoked (delegate with false,
        // through SystemRef.TryGet) and the button listeners are cleared - but ClearPointerDownListeners replaces the
        // pointer-UP field in both shipping builds (UIWidgetButton 0x060001a1), so afterwards a pointer down still
        // holds while a pointer up no longer releases.
        [Test]
        public void OnDestroyClearsTheDisplayAndRevokesTheCursorButOnlyTheUpListenersAreRemoved()
        {
            WithInput(scene =>
            {
                Call(scene.Input, "Start");
                Supply(scene, 1f);
                Call(scene.Input, "OnDestroy");
                Assert.That((Held(scene), scene.Progress.value), Is.EqualTo((0f, 0f)));
                Assert.That(scene.CursorReleases, Is.EqualTo(new[] { false }));
                ExpectRefusedCoroutine();
                scene.Button.OnPointerDown(null);
                Assert.That(Holding(scene), Is.True, "the down listener survived");
                scene.Button.OnPointerUp(null);
                Assert.That(Holding(scene), Is.True, "the up listeners were cleared");
            }, required: 100f);
        }
    }
}
