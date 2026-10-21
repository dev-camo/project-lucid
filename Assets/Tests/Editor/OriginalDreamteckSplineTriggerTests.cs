using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineTrigger and TriggerGroup. Expected values come from the x86-64 bodies (addresses in each comment).
    // Nothing here touches a scene object; listeners are invoked with a null SplineUser.
    public sealed class OriginalDreamteckSplineTriggerTests
    {
        private static readonly BindingFlags DeclaredMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Ctor 0x4e46c0: initialisers (name "Trigger", position 0.5, enabled, white) and then the type and a fresh event.
        [Test]
        public void ConstructorKeepsTheShippedDefaults()
        {
            SplineTrigger trigger = new SplineTrigger(SplineTrigger.Type.Backward);
            Assert.That(trigger.type, Is.EqualTo(SplineTrigger.Type.Backward));
            Assert.That(trigger.name, Is.EqualTo("Trigger"));
            Assert.That(trigger.position, Is.EqualTo(0.5));
            Assert.That(trigger.enabled, Is.True);
            Assert.That(trigger.workOnce, Is.False);
            Assert.That(trigger.color, Is.EqualTo(Color.white));
            Assert.That(trigger.onCross, Is.Not.Null);
            Assert.That((int)SplineTrigger.Type.Double, Is.EqualTo(0));
            Assert.That((int)SplineTrigger.Type.Forward, Is.EqualTo(1));
            Assert.That((int)SplineTrigger.Type.Backward, Is.EqualTo(2));
        }

        // Check 0x4e42c0 at position 0.5. Both comparisons are inclusive, so a trigger exactly on a touched end crosses in both
        // directions. Rows: (previous, current) = forward pass, backward pass, miss, standing exactly on the trigger.
        [Test]
        public void CheckFollowsTheNativeTruthTable()
        {
            double[][] passes = { new[] { 0.4, 0.6 }, new[] { 0.6, 0.4 }, new[] { 0.1, 0.2 }, new[] { 0.5, 0.5 } };
            bool[] doubleExpected = { true, true, false, true };
            bool[] forwardExpected = { true, false, false, true };
            bool[] backwardExpected = { false, true, false, true };
            for (int i = 0; i < passes.Length; i++)
            {
                string row = "(" + passes[i][0] + ", " + passes[i][1] + ")";
                Assert.That(new SplineTrigger(SplineTrigger.Type.Double).Check(passes[i][0], passes[i][1]), Is.EqualTo(doubleExpected[i]), "Double " + row);
                Assert.That(new SplineTrigger(SplineTrigger.Type.Forward).Check(passes[i][0], passes[i][1]), Is.EqualTo(forwardExpected[i]), "Forward " + row);
                Assert.That(new SplineTrigger(SplineTrigger.Type.Backward).Check(passes[i][0], passes[i][1]), Is.EqualTo(backwardExpected[i]), "Backward " + row);
            }

            // 0x4e42c0 compares with ucomisd/jb/jae, so an unordered comparison is never a crossing. The negated-less-than
            // reading (!(position < previous) && !(current < position)) would answer true for each of these.
            Assert.That(new SplineTrigger(SplineTrigger.Type.Forward).Check(double.NaN, 0.6), Is.False, "Forward NaN previous");
            Assert.That(new SplineTrigger(SplineTrigger.Type.Double).Check(double.NaN, 0.4), Is.False, "Double NaN previous");
            Assert.That(new SplineTrigger(SplineTrigger.Type.Backward).Check(0.6, double.NaN), Is.False, "Backward NaN current");

            SplineTrigger disabled = new SplineTrigger(SplineTrigger.Type.Double);
            disabled.enabled = false;
            Assert.That(disabled.Check(0.4, 0.6), Is.False, "a disabled trigger never crosses");
        }

        // Check 0x4e42c0 / Reset 0x4e43c0: only a crossing sets worked, and only a workOnce trigger consults it.
        [Test]
        public void WorkOnceBlocksUntilResetAndMissesDoNotCount()
        {
            SplineTrigger once = new SplineTrigger(SplineTrigger.Type.Forward);
            once.workOnce = true;
            Assert.That(once.Check(0.1, 0.2), Is.False, "a miss leaves the trigger unworked");
            Assert.That(once.Check(0.4, 0.6), Is.True);
            Assert.That(once.Check(0.4, 0.6), Is.False, "worked blocks a second crossing");
            once.Reset();
            Assert.That(once.Check(0.4, 0.6), Is.True, "Reset allows it again");

            SplineTrigger repeating = new SplineTrigger(SplineTrigger.Type.Forward);
            Assert.That(repeating.Check(0.4, 0.6), Is.True);
            Assert.That(repeating.Check(0.4, 0.6), Is.True, "without workOnce the worked flag is ignored");
        }

        // Invoke 0x4e4330, AddListener 0x4e4910 / 0x4e4960 (closure ignores the user), RemoveListener 0x4e4a50,
        // RemoveAllListeners 0x4e4aa0. This covers the EDITOR branch only: Invoke reads Application.isPlaying directly and returns
        // while not playing (the editor compile of the source). The shipped branch, the text after the #if UNITY_EDITOR block, is
        // covered by OriginalDreamteckSplineTriggerPlayModeTests.ListenersReceiveInvokeAndRemovalWorks. The listener side is
        // driven here through the public onCross event that Invoke forwards to, and Invoke itself is asserted to stay silent.
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
            trigger.Invoke(null);
            Assert.That(withUser, Is.EqualTo(0), "Invoke does not call the listeners while not playing");
            Assert.That(withoutUser, Is.EqualTo(0));
            trigger.onCross.Invoke(null);
            Assert.That(withUser, Is.EqualTo(1));
            Assert.That(withoutUser, Is.EqualTo(1));

            trigger.RemoveListener(userListener);
            trigger.onCross.Invoke(null);
            Assert.That(withUser, Is.EqualTo(1), "removed listener stays quiet");
            Assert.That(withoutUser, Is.EqualTo(2));

            trigger.RemoveAllListeners();
            trigger.onCross.Invoke(null);
            Assert.That(withoutUser, Is.EqualTo(2));
        }

        // Group Check 0x4e41a0: null triggers are skipped and the group's own enabled flag is not consulted. This covers the
        // EDITOR branch only (shipped branch: OriginalDreamteckSplineTriggerPlayModeTests.GroupChecksFireCrossedTriggersSkipNullsAnd
        // IgnoreTheGroupFlag). Each checked trigger that crosses sets its private worked flag before Invoke; Invoke returns while
        // not playing, so the crossing is observed through that flag and the listeners stay silent.
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
            Assert.That(Worked(crossed), Is.True, "the crossed trigger was checked although the group is disabled");
            Assert.That(Worked(missed), Is.False);
            Assert.That(crossedCount, Is.EqualTo(0), "Invoke does not call the listener while not playing");
            Assert.That(missedCount, Is.EqualTo(0));
        }

        private static bool Worked(SplineTrigger trigger)
        {
            return (bool)typeof(SplineTrigger).GetField("worked", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trigger);
        }

        // Group Reset 0x4e4380 resets every trigger.
        [Test]
        public void GroupResetRearmsWorkOnceTriggers()
        {
            TriggerGroup group = new TriggerGroup();
            SplineTrigger first = group.AddTrigger(0.5, SplineTrigger.Type.Forward);
            first.workOnce = true;
            Assert.That(first.Check(0.4, 0.6), Is.True);
            Assert.That(first.Check(0.4, 0.6), Is.False);
            group.Reset();
            Assert.That(first.Check(0.4, 0.6), Is.True);
        }

        // Group Reset 0x4e4380 does not skip null entries (test/je to the fault at 0x4e43a8), unlike Check and GetTriggers:
        // the entry before the null has already been reset when the fault happens.
        [Test]
        public void GroupResetFaultsOnANullEntryAfterResettingEarlierOnes()
        {
            TriggerGroup group = new TriggerGroup();
            SplineTrigger first = group.AddTrigger(0.5, SplineTrigger.Type.Forward);
            first.workOnce = true;
            Assert.That(first.Check(0.4, 0.6), Is.True);
            Assert.That(first.Check(0.4, 0.6), Is.False);
            Array.Resize(ref group.triggers, group.triggers.Length + 1);
            Assert.Throws<NullReferenceException>(group.Reset);
            Assert.That(first.Check(0.4, 0.6), Is.True, "element 0 was reset before the fault");
        }

        // GetTriggers 0x4e43d0: both ends of from..to are included and null entries are skipped.
        [Test]
        public void GetTriggersIncludesBothEnds()
        {
            TriggerGroup group = new TriggerGroup();
            SplineTrigger a = group.AddTrigger(0.25, SplineTrigger.Type.Double);
            SplineTrigger b = group.AddTrigger(0.5, SplineTrigger.Type.Double);
            SplineTrigger c = group.AddTrigger(0.75, SplineTrigger.Type.Double);
            Array.Resize(ref group.triggers, group.triggers.Length + 1);
            Assert.That(group.GetTriggers(0.25, 0.75), Is.EqualTo(new List<SplineTrigger> { a, b, c }));
            Assert.That(group.GetTriggers(0.26, 0.74), Is.EqualTo(new List<SplineTrigger> { b }));
            Assert.That(group.GetTriggers(0.8, 0.9), Is.Empty);
        }

        // AddTrigger 0x4e4560 / 0x4e45f0 and RemoveTrigger 0x4e47f0: the default name counts from one.
        [Test]
        public void GroupAddAndRemoveTriggers()
        {
            TriggerGroup group = new TriggerGroup();
            Assert.That(group.enabled, Is.True);
            Assert.That(group.name, Is.EqualTo(""));
            Assert.That(group.color, Is.EqualTo(Color.white));
            Assert.That(group.triggers, Is.Empty);

            SplineTrigger first = group.AddTrigger(0.1, SplineTrigger.Type.Forward);
            SplineTrigger second = group.AddTrigger(0.2, SplineTrigger.Type.Backward, "Named", Color.red);
            SplineTrigger third = group.AddTrigger(0.3, SplineTrigger.Type.Double);
            Assert.That(first.name, Is.EqualTo("Trigger 1"));
            Assert.That(first.color, Is.EqualTo(Color.white));
            Assert.That(second.name, Is.EqualTo("Named"));
            Assert.That(second.color, Is.EqualTo(Color.red));
            Assert.That(second.type, Is.EqualTo(SplineTrigger.Type.Backward));
            Assert.That(second.position, Is.EqualTo(0.2));
            Assert.That(third.name, Is.EqualTo("Trigger 3"));

            group.RemoveTrigger(1);
            Assert.That(group.triggers, Is.EqualTo(new[] { first, third }));
        }

        // The editor compile declares the public editor-only TriggerGroup.open ahead of enabled (editor-vs-ship declaration diff);
        // the shipped build has no such field.
        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            Assert.That(typeof(SplineTrigger).GetFields(DeclaredMembers).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "name", "type", "workOnce", "worked", "position", "enabled", "color", "onCross" }));
            Assert.That(typeof(TriggerGroup).GetFields(DeclaredMembers).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "open", "enabled", "name", "color", "triggers" }));
            Assert.That(Attribute.IsDefined(typeof(SplineTrigger), typeof(SerializableAttribute)), Is.True);
            Assert.That(Attribute.IsDefined(typeof(TriggerGroup), typeof(SerializableAttribute)), Is.True);
            Assert.That(Attribute.IsDefined(typeof(SplineTrigger.TriggerEvent), typeof(SerializableAttribute)), Is.True);
            Assert.That(typeof(SplineTrigger.TriggerEvent).BaseType, Is.EqualTo(typeof(UnityEvent<SplineUser>)));

            FieldInfo position = typeof(SplineTrigger).GetField("position");
            UnityEngine.RangeAttribute range = (UnityEngine.RangeAttribute)Attribute.GetCustomAttribute(position, typeof(UnityEngine.RangeAttribute));
            Assert.That(range.min, Is.EqualTo(0f));
            Assert.That(range.max, Is.EqualTo(1f));
            FieldInfo onCross = typeof(SplineTrigger).GetField("onCross");
            Assert.That(Attribute.IsDefined(onCross, typeof(HideInInspector)), Is.True);
            Assert.That(Attribute.IsDefined(onCross, typeof(SerializeField)), Is.True);
        }

        // The shipped assembly names the AddListener(UnityAction) closure <>c__DisplayClass11_0: eleven members precede it.
        [Test]
        public void ClosureKeepsTheShippedCompilerName()
        {
            string[] nested = typeof(SplineTrigger).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public).Select(t => t.Name).ToArray();
            Assert.That(nested, Does.Contain("<>c__DisplayClass11_0"));
            Assert.That(nested, Does.Contain("TriggerEvent"));
            Assert.That(nested, Does.Contain("Type"));
        }
    }
}
