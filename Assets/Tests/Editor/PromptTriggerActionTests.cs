using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // PromptTriggerHideAll (x86-64 0x6968D0) and SequencedPromptTrigger (0x56A960, 0x56AA70, 0x56ABB0) over real
    // components on inactive hosts. A prompt is shown by a real PromptManager holding a prompt slot and a prompt
    // prefab (a UIContainerPrompt that is set up from its definition), and hidden through the manager.
    public sealed class PromptTriggerActionTests
    {
        // Edit Mode refuses the Destroy of a hidden prompt's container, logging this error once per hidden prompt.
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");

        private static PromptManager RegisteredPromptManager(Owned owned)
        {
            owned.LeaseRow<TimeManager>(); // PromptManager's field initialiser
            return owned.Register(owned.Host("Lucid prompt manager", false).AddComponent<PromptManager>());
        }

        // The validity test (0x696910) comes first; without a PromptManager nothing else is touched.
        [Test]
        public void HidingAllPromptsWithoutAPromptManagerDoesNothing()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<PromptManager>();
                var action = owned.Host("Lucid hide all prompts", false).AddComponent<PromptTriggerHideAll>();
                Assert.DoesNotThrow(action.Action_HideAllActivePrompts);
            });
        }

        // A registered manager with no active prompt: the loop at 0x696961 never runs.
        [Test]
        public void HidingAllPromptsOfAnIdlePromptManagerLeavesNoActivePrompt()
        {
            Owned.Run(owned =>
            {
                PromptManager manager = RegisteredPromptManager(owned);
                var action = owned.Host("Lucid hide all prompts", false).AddComponent<PromptTriggerHideAll>();
                Assert.DoesNotThrow(action.Action_HideAllActivePrompts);
                Assert.That(Get<IList>(manager, "m_activePrompts"), Is.Empty);
            });
        }

        // A manager that can show prompts: the rows read by the components it touches are leased, the
        // AddressableManager that UIContainerPrompt releases its image handle through is registered (the original
        // dereferences an absent one), and the manager's serialized visibility overriders and input disabler are
        // attached. Reflection stands in for the serializer; the overriders' Awake, which play mode would send, is
        // called so that their UI reference exists.
        private static PromptManager ShowingPromptManager(Owned owned)
        {
            owned.LeaseRow<UIManager>();
            owned.LeaseRow<InputSystem>();
            owned.LeaseRow<UIRuntimeConfiguration>();
            owned.LeaseRow<CinemachineCameraManager>();
            PromptManager manager = RegisteredPromptManager(owned);
            owned.Register(owned.Host("Lucid addressable manager", false).AddComponent<AddressableManager>());
            GameObject ui = owned.Host("Lucid prompt ui", false);
            UIVisibilityGroupOverrider standard = NewOverrider(ui);
            UIVisibilityGroupOverrider timeStopped = NewOverrider(ui);
            Set(manager, "m_uiVisibilityGroupOverriderStandard", standard);
            Set(manager, "m_uiVisibilityGroupOverriderTimeStopped", timeStopped);
            Set(manager, "m_gameInputDisabler", ui.AddComponent<GameInputDisabler>());
            return manager;
        }

        private static UIVisibilityGroupOverrider NewOverrider(GameObject host)
        {
            var overrider = host.AddComponent<UIVisibilityGroupOverrider>();
            Set(overrider, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride>());
            Call(overrider, "Awake");
            return overrider;
        }

        private static PromptSlot NewSlot(Owned owned, PromptManager manager, PromptSlotType type)
        {
            var slot = owned.Host("Lucid prompt slot", false).AddComponent<PromptSlot>();
            JsonUtility.FromJsonOverwrite("{\"m_slotType\":" + (int)type + "}", slot);
            manager.RegisterPromptSlot(slot);
            return slot;
        }

        // A trigger whose definition shows the prefab in the slot of that type (always displayed, never pausing time).
        private static PromptTrigger NewShowingTrigger(Owned owned, PromptSlotType type, string guid)
        {
            var prefab = owned.Host("Lucid prompt prefab", false).AddComponent<UIContainerPrompt>();
            var definition = Create<PromptDefinition>(owned, "{\"m_guid\":\"" + guid + "\",\"m_promptSlot\":" + (int)type + "}");
            Set(definition, "m_promptPrefab", prefab); // Reflection: serialized reference without a setter
            var trigger = owned.Host("Lucid prompt trigger", false).AddComponent<PromptTrigger>();
            Set(trigger, "m_promptDefinition", definition);
            return trigger;
        }

        // The loop at 0x696961..0x696994 ends m_activePrompts[0] with choice -1 (edx) and no acknowledgement (ecx 0)
        // until the list is empty, so every shown prompt is hidden and each trigger's hidden callback fires once.
        [Test]
        public void HidingAllPromptsHidesEveryActivePrompt()
        {
            Owned.Run(owned =>
            {
                PromptManager manager = ShowingPromptManager(owned);
                NewSlot(owned, manager, PromptSlotType.GameHUDFixedPosition);
                NewSlot(owned, manager, PromptSlotType.GameHUDLowerPosition);
                PromptTrigger first = NewShowingTrigger(owned, PromptSlotType.GameHUDFixedPosition, "lucid-hide-all-first");
                PromptTrigger second = NewShowingTrigger(owned, PromptSlotType.GameHUDLowerPosition, "lucid-hide-all-second");
                var hidden = new List<PromptTrigger>();
                first.OnHiddenAction += hidden.Add;
                second.OnHiddenAction += hidden.Add;
                Assert.That(first.TryShowWithResult(), Is.True);
                Assert.That(second.TryShowWithResult(), Is.True);
                Assert.That(Get<IList>(manager, "m_activePrompts"), Has.Count.EqualTo(2));

                var action = owned.Host("Lucid hide all prompts", false).AddComponent<PromptTriggerHideAll>();
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                action.Action_HideAllActivePrompts();

                Assert.That(Get<IList>(manager, "m_activePrompts"), Is.Empty);
                Assert.That(hidden, Is.EqualTo(new[] { first, second }), "each trigger is told once, in list order");
            });
        }

        private static SequencedPromptTrigger NewEffect(Owned owned, out PromptTrigger trigger)
        {
            GameObject host = owned.Host("Lucid sequenced prompt", false);
            trigger = host.AddComponent<PromptTrigger>();
            var effect = host.AddComponent<SequencedPromptTrigger>();
            Call(effect, "Reset"); // Reflection: the editor callback that AddComponent also runs
            return effect;
        }

        // Reset (0x56abe2) stores the sibling PromptTrigger; a refused prompt (TryShowWithResult false at
        // 0x56a9a4) completes the effect at once through the saved callback.
        [Test]
        public void AnEffectWhosePromptIsRefusedCompletesAtOnce()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<PromptManager>();
                SequencedPromptTrigger effect = NewEffect(owned, out PromptTrigger trigger);
                Assert.That(Get<PromptTrigger>(effect, "m_promptTrigger"), Is.SameAs(trigger));
                int completed = 0;
                IEffectData data = null;
                effect.PlayEffect(in data, () => completed++);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(trigger.OnHiddenAction, Is.Null, "no hidden subscription is made for a refused prompt");
            });
        }

        // The shown-prompt branch (0x56a9cb..0x56a9ea) subscribes OnPromptHidden to the trigger and does not complete
        // the effect. OnPromptHidden (0x56aada) ignores a different trigger; its own trigger (0x56ab32 Delegate.Remove,
        // then the saved callback) removes the subscription and completes the effect.
        [Test]
        public void TheEffectCompletesWhenItsOwnPromptIsHiddenAndIgnoresOthers()
        {
            Owned.Run(owned =>
            {
                PromptManager manager = ShowingPromptManager(owned);
                NewSlot(owned, manager, PromptSlotType.GameHUDFixedPosition);
                SequencedPromptTrigger effect = NewEffect(owned, out PromptTrigger trigger);
                var definition = Create<PromptDefinition>(owned, "{\"m_guid\":\"lucid-sequenced-prompt\"}");
                Set(definition, "m_promptPrefab", owned.Host("Lucid prompt prefab", false).AddComponent<UIContainerPrompt>()); // Reflection: serialized references without setters
                Set(trigger, "m_promptDefinition", definition);
                var other = owned.Host("Lucid other prompt", false).AddComponent<PromptTrigger>();
                int completed = 0;
                IEffectData data = null;

                effect.PlayEffect(in data, () => completed++);
                Assert.That(completed, Is.EqualTo(0), "a shown prompt does not complete the effect yet");
                Assert.That(trigger.OnHiddenAction, Is.Not.Null, "the effect subscribed to the trigger");

                trigger.OnHiddenAction(other);
                Assert.That(completed, Is.EqualTo(0), "another trigger's hidden callback is ignored");
                Assert.That(trigger.OnHiddenAction, Is.Not.Null, "and the subscription stays");
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                manager.TryHidePrompt(trigger);
                Assert.That(completed, Is.EqualTo(1), "the effect completes when its own prompt is hidden");
                Assert.That(trigger.OnHiddenAction, Is.Null, "and unsubscribes");
            });
        }
    }
}
