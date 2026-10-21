using System;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine.Events;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineTrigger and TriggerGroup in play mode. SplineTrigger.Invoke reads Application.isPlaying directly
    // and the shipped player build always takes the branch after the #if UNITY_EDITOR block, which only a real play mode run
    // reaches; the editor-branch counterparts are in OriginalDreamteckSplineTriggerTests. Nothing here touches a scene object.
    public sealed class OriginalDreamteckSplineTriggerPlayModeTests
    {
        // Invoke 0x4e4330, AddListener 0x4e4910 / 0x4e4960 (closure ignores the user), RemoveListener 0x4e4a50,
        // RemoveAllListeners 0x4e4aa0.
        [Test]
        public void ListenersReceiveInvokeAndRemovalWorks()
        {
            SplineTrigger trigger = new SplineTrigger(SplineTrigger.Type.Double);
            int withUser = 0;
            int withoutUser = 0;
            UnityAction<SplineUser> userListener = delegate (SplineUser user)
            {
                Assert.That(user == null);
                withUser++;
            };
            trigger.AddListener(userListener);
            trigger.AddListener((UnityAction)delegate { withoutUser++; });
            trigger.Invoke();
            Assert.That(withUser, Is.EqualTo(1));
            Assert.That(withoutUser, Is.EqualTo(1));

            trigger.RemoveListener(userListener);
            trigger.Invoke(null);
            Assert.That(withUser, Is.EqualTo(1), "removed listener stays quiet");
            Assert.That(withoutUser, Is.EqualTo(2));

            trigger.RemoveAllListeners();
            trigger.Invoke();
            Assert.That(withoutUser, Is.EqualTo(2));
        }

        // Group Check 0x4e41a0: null triggers are skipped and the group's own enabled flag is not consulted.
        [Test]
        public void GroupChecksFireCrossedTriggersSkipNullsAndIgnoreTheGroupFlag()
        {
            TriggerGroup group = new TriggerGroup();
            group.enabled = false;
            SplineTrigger crossed = group.AddTrigger(0.5, SplineTrigger.Type.Forward);
            SplineTrigger missed = group.AddTrigger(0.9, SplineTrigger.Type.Forward);
            Array.Resize(ref group.triggers, group.triggers.Length + 1);
            int crossedCount = 0;
            int missedCount = 0;
            crossed.AddListener((UnityAction)delegate { crossedCount++; });
            missed.AddListener((UnityAction)delegate { missedCount++; });
            group.Check(0.4, 0.6);
            Assert.That(crossedCount, Is.EqualTo(1));
            Assert.That(missedCount, Is.EqualTo(0));
        }
    }
}
