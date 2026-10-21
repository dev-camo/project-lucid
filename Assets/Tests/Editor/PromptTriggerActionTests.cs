using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // PromptTriggerHideAll (x86-64 0x6968D0) and SequencedPromptTrigger (0x56A960, 0x56AA70, 0x56ABB0) over real
    // components on inactive hosts. A prompt is only shown by a PromptManager holding a prompt prefab and slot,
    // so the shown-prompt branches are driven the way the manager drives them: through the trigger's own
    // OnHiddenAction callback. Hiding a prompt that is really on screen is not covered.
    public sealed class PromptTriggerActionTests
    {
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

        // OnPromptHidden (0x56aada) ignores a different trigger; its own trigger (0x56ab32 Delegate.Remove, then
        // the saved callback) removes the subscription and completes the effect.
        [Test]
        public void TheEffectCompletesWhenItsOwnPromptIsHiddenAndIgnoresOthers()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<PromptManager>();
                SequencedPromptTrigger effect = NewEffect(owned, out PromptTrigger trigger);
                var other = owned.Host("Lucid other prompt", false).AddComponent<PromptTrigger>();
                int completed = 0;
                IEffectData data = null;
                effect.PlayEffect(in data, () => completed++);
                Assert.That(completed, Is.EqualTo(1), "the refused prompt completed once");

                // The subscription the shown-prompt branch makes (0x56a9cb..0x56a9ea), made over the effect's own method.
                MethodInfo hidden = typeof(SequencedPromptTrigger).GetMethod("OnPromptHidden", BindingFlags.Instance | BindingFlags.NonPublic);
                trigger.OnHiddenAction += (Action<PromptTrigger>)Delegate.CreateDelegate(typeof(Action<PromptTrigger>), effect, hidden);

                trigger.OnHiddenAction(other);
                Assert.That(completed, Is.EqualTo(1), "another trigger's hidden callback is ignored");
                Assert.That(trigger.OnHiddenAction, Is.Not.Null, "and the subscription stays");
                trigger.OnHiddenAction(trigger);
                Assert.That(completed, Is.EqualTo(2), "the effect completes when its own prompt is hidden");
                Assert.That(trigger.OnHiddenAction, Is.Null, "and unsubscribes");
            });
        }
    }
}
