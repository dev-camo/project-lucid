using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CrossPromoUIShared;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerValueProgressSetter (x86-64 0x696F60), UIContainerSequenceProgressSetter (0x696E40) and
    // UIContainerConditionalTriggerSetter (0x696AC0, 0x696C20, 0x696D40), which all reach their container through the
    // UIContainerValueSetter identifier (0x696BC0): the identifier must be loaded and its container must report progress.
    // The setters sit on inactive hosts, so their Unity messages are called directly.
    public sealed class UIContainerValueSetterTests
    {
        private static readonly FieldInfo TimeConfig = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);

        // ConditionalTriggerEvent is time scaled: its editor callbacks read the category lookup, so an owned empty one
        // is cached while it is added, and the TimeManager row its shutdown path reads is leased.
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

        // An identifier whose container is loaded when the progress container is registered, and not otherwise.
        private static ValueSetterRecordingProgressContainer Open(Owned owned, Component setter, bool loaded)
        {
            var identifier = owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
            var container = Add<ValueSetterRecordingProgressContainer>(owned, "Lucid progress container");
            if (loaded) identifier.RegisterContainer(container);
            Set(setter, "m_identifier", identifier);
            return container;
        }

        // 0x696BC0: IsLoaded (0x1a3af60) gates the loaded container, which is then type-tested for IUIContainerProgress.
        [Test]
        public void ASetterOnlyReachesALoadedContainer()
        {
            Owned.Run(owned =>
            {
                var setter = Add<UIContainerValueProgressSetter>(owned, "Lucid value setter");
                ValueSetterRecordingProgressContainer container = Open(owned, setter, false);
                setter.SetProgress(0.5f);
                Assert.That(container.Calls, Is.Empty, "an identifier without a loaded container is skipped");

                container = Open(owned, setter, true);
                setter.SetProgress(0.5f);
                Assert.That(container.Calls, Is.EqualTo(new[] { "SetProgress(0.5)" }));
            });
        }

        // 0x696F60 forwards the float overload (interface slot 1), unchanged.
        [Test]
        public void ValueProgressSetterForwardsTheFloatProgress()
        {
            Owned.Run(owned =>
            {
                var setter = Add<UIContainerValueProgressSetter>(owned, "Lucid value setter");
                ValueSetterRecordingProgressContainer container = Open(owned, setter, true);
                setter.SetProgress(0f);
                setter.SetProgress(1.25f);
                setter.SetProgress(-3f);
                Assert.That(container.Calls, Is.EqualTo(new[] { "SetProgress(0)", "SetProgress(1.25)", "SetProgress(-3)" }));
            });
        }

        // 0x696E40 reads the effect's Progress (SequencedEffect.get_Progress, 0x696ec5) only after the container test passed,
        // then forwards it through the float overload (interface slot 1).
        [Test]
        public void SequenceProgressSetterForwardsTheEffectProgressOnlyToALoadedContainer()
        {
            Owned.Run(owned =>
            {
                var setter = Add<UIContainerSequenceProgressSetter>(owned, "Lucid sequence setter");
                var effect = Add<ValueSetterFixedProgressEffect>(owned, "Lucid effect");
                effect.Value = 0.25f;
                Set(setter, "m_sequencedEffect", effect);

                ValueSetterRecordingProgressContainer container = Open(owned, setter, false);
                Call(setter, "Update");
                Assert.That(container.Calls, Is.Empty);
                Assert.That(effect.Reads, Is.EqualTo(0), "the effect is not read while no container is loaded");

                container = Open(owned, setter, true);
                Call(setter, "Update");
                effect.Value = 0.75f;
                Call(setter, "Update");
                Assert.That(container.Calls, Is.EqualTo(new[] { "SetProgress(0.25)", "SetProgress(0.75)" }));
                Assert.That(effect.Reads, Is.EqualTo(2));
            });
        }

        // OnEnable 0x696AC0 zeroes the shown count, takes the condition count (0x696af3) as the total and enables the
        // container (slot 0, argument 1); OnDisable 0x696D40 disables it (argument 0).
        [Test]
        public void ConditionalTriggerSetterEnablesAndDisablesTheContainerWithTheConditionCount()
        {
            WithOwned(owned =>
            {
                var probes = new[] { Add<TriggerConditionProbe>(owned, "Lucid probe 0"), Add<TriggerConditionProbe>(owned, "Lucid probe 1"), Add<TriggerConditionProbe>(owned, "Lucid probe 2") };
                var trigger = Add<ConditionalTriggerEvent>(owned, "Lucid trigger");
                Set(trigger, "m_conditions", probes);
                var setter = Add<UIContainerConditionalTriggerSetter>(owned, "Lucid conditional setter");
                Set(setter, "m_conditionalTrigger", trigger);
                ValueSetterRecordingProgressContainer container = Open(owned, setter, true);

                Call(setter, "OnEnable");
                Assert.That(Get<int>(setter, "m_progressCurrent"), Is.EqualTo(0));
                Assert.That(Get<int>(setter, "m_progressTotal"), Is.EqualTo(3));
                Call(setter, "OnDisable");
                Assert.That(container.Calls, Is.EqualTo(new[] { "SetEnabled(True)", "SetEnabled(False)" }));
            });
        }

        // Update 0x696C20 keeps the larger of the shown count and the number of conditions now passing (cmovg at 0x696c5a),
        // so the count never drops, and reports (shown, total) through the two-integer overload (slot 2). Rows:
        // passing answers, then the progress reported.
        [Test]
        public void ConditionalTriggerSetterNeverReportsFewerPassedConditionsThanBefore()
        {
            WithOwned(owned =>
            {
                var probes = new[] { Add<TriggerConditionProbe>(owned, "Lucid probe 0"), Add<TriggerConditionProbe>(owned, "Lucid probe 1") };
                var trigger = Add<ConditionalTriggerEvent>(owned, "Lucid trigger");
                Set(trigger, "m_conditions", probes);
                var setter = Add<UIContainerConditionalTriggerSetter>(owned, "Lucid conditional setter");
                Set(setter, "m_conditionalTrigger", trigger);
                ValueSetterRecordingProgressContainer container = Open(owned, setter, true);
                Call(setter, "OnEnable");

                foreach (string answers in new[] { "TF", "FF", "TT", "FT" })
                {
                    probes[0].Result = answers[0] == 'T';
                    probes[1].Result = answers[1] == 'T';
                    Call(setter, "Update");
                }
                Assert.That(container.Calls, Is.EqualTo(new[]
                {
                    "SetEnabled(True)", "SetProgress(1,2)", "SetProgress(1,2)", "SetProgress(2,2)", "SetProgress(2,2)",
                }));
            });
        }

        // The shown count restarts from zero each time the setter is enabled (0x696ae6), whatever it reached before.
        [Test]
        public void ConditionalTriggerSetterRestartsItsCountWhenEnabledAgain()
        {
            WithOwned(owned =>
            {
                var probe = Add<TriggerConditionProbe>(owned, "Lucid probe");
                probe.Result = true;
                var trigger = Add<ConditionalTriggerEvent>(owned, "Lucid trigger");
                Set(trigger, "m_conditions", new TriggerCondition[] { probe });
                var setter = Add<UIContainerConditionalTriggerSetter>(owned, "Lucid conditional setter");
                Set(setter, "m_conditionalTrigger", trigger);
                ValueSetterRecordingProgressContainer container = Open(owned, setter, true);
                Call(setter, "OnEnable");
                Call(setter, "Update");
                Assert.That(Get<int>(setter, "m_progressCurrent"), Is.EqualTo(1));
                probe.Result = false;
                Call(setter, "OnEnable");
                Call(setter, "Update");
                Assert.That(container.Calls, Is.EqualTo(new[] { "SetEnabled(True)", "SetProgress(1,1)", "SetEnabled(True)", "SetProgress(0,1)" }));
            });
        }
    }
}
