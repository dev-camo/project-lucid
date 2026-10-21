using System.Collections.Generic;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineComputer in play mode, where Awake caches Application.isPlaying so that the queue paths of the
    // shipped player build run (the editor compile resamples at once while not playing; those branches are covered by
    // OriginalDreamteckSplineComputerTests). A synchronous test passes no frame, so the engine's own Update/LateUpdate/
    // FixedUpdate do not run in between; the callbacks are invoked by hand. Play mode does not run Reset on AddComponent, so a
    // SplineUser added next to the computer does not subscribe by itself. TearDown destroys every object the test made.
    public sealed class OriginalDreamteckSplineComputerPlayModeTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private SplineComputer NewComputer(string name, Vector3 position)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            owner.transform.position = position;
            return owner.AddComponent<SplineComputer>();
        }

        private static SplinePoint[] Corner(float x = 0f)
        {
            SplinePoint[] points = new SplinePoint[3];
            points[0] = new SplinePoint(new Vector3(x, 0f, 0f));
            points[1] = new SplinePoint(new Vector3(x + 10f, 0f, 0f));
            points[2] = new SplinePoint(new Vector3(x + 10f, 0f, 10f));
            return points;
        }

        private static void Call(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(target, null);
        }

        // Update/FixedUpdate/LateUpdate 0x4b3970/0x4b3760/0x4b3950: the callback that matches updateMode (or any of the three for
        // AllUpdate) runs the queued resample; None never queues one. Rows are callbacks, columns are modes.
        [Test]
        public void UpdateModeSelectsTheCallbacksThatResample()
        {
            string[] callbacks = { "Update", "FixedUpdate", "LateUpdate" };
            SplineComputer.UpdateMode[] modes =
            {
                SplineComputer.UpdateMode.Update, SplineComputer.UpdateMode.FixedUpdate, SplineComputer.UpdateMode.LateUpdate,
                SplineComputer.UpdateMode.AllUpdate, SplineComputer.UpdateMode.None
            };
            bool[][] runs =
            {
                new[] { true, false, false, true, false },
                new[] { false, true, false, true, false },
                new[] { false, false, true, true, false }
            };
            for (int callback = 0; callback < callbacks.Length; callback++)
            {
                for (int mode = 0; mode < modes.Length; mode++)
                {
                    SplineComputer computer = NewComputer("mode" + callback + mode, Vector3.zero);
                    computer.updateMode = modes[mode];
                    computer.type = Spline.Type.Linear;
                    computer.SetPoints(Corner());
                    Call(computer, callbacks[callback]);
                    Assert.That(computer.sampleCount, Is.EqualTo(runs[callback][mode] ? 3 : 0), callbacks[callback] + " in " + modes[mode]);
                }
            }
        }

        // RunUpdate 0x4b3780 / RebuildUsers 0x4b3b30: after a resample the subscribers rebuild (last first), destroyed ones are
        // dropped and onRebuild is raised once. In play mode SplineUser.OnDestroy does not unsubscribe (its editor-only branch
        // is gated on !_isPlaying), so the destroyed user stays listed until RebuildUsers drops it.
        [Test]
        public void ResampleRebuildsUsersAndRaisesOnRebuild()
        {
            SplineComputer computer = NewComputer("users", Vector3.zero);
            computer.type = Spline.Type.Linear;
            SplineUser alive = computer.gameObject.AddComponent<SplineUser>();
            GameObject doomedOwner = new GameObject("doomed");
            _owned.Add(doomedOwner);
            SplineUser doomed = doomedOwner.AddComponent<SplineUser>();
            alive.spline = computer;
            doomed.spline = computer;
            Assert.That(computer.subscriberCount, Is.EqualTo(2));

            int rebuilds = 0;
            int builds = 0;
            computer.onRebuild += delegate { rebuilds++; };
            alive.onPostBuild += delegate { builds++; };
            Object.DestroyImmediate(doomedOwner);
            computer.SetPoints(Corner());
            Call(computer, "Update");
            Assert.That(rebuilds, Is.EqualTo(1));
            Assert.That(computer.subscriberCount, Is.EqualTo(1), "the destroyed user is dropped");
            Call(alive, "Update");
            Assert.That(builds, Is.EqualTo(1), "the rebuild reached the live user");
            Assert.That(alive.sampleCount, Is.EqualTo(3));
        }

        // AddTrigger 0x4b8990 creates the missing groups; RemoveTrigger 0x4b8a80, CheckTriggers 0x4b8b60 / 0x4b8bd0 and
        // ResetTriggers 0x4b8cc0 / 0x4b8d10 log an error for a missing group. The user argument of CheckTriggers is not forwarded.
        // The editor-branch counterpart (Invoke returns while not playing) is in OriginalDreamteckSplineComputerTests.
        [Test]
        public void TriggerApisCreateGroupsLogMissingOnesAndDropTheUser()
        {
            SplineComputer computer = NewComputer("triggers", Vector3.zero);
            SplineTrigger trigger = computer.AddTrigger(2, 0.5, SplineTrigger.Type.Forward);
            Assert.That(computer.triggerGroups.Length, Is.EqualTo(3));
            Assert.That(trigger.name, Is.EqualTo("API Trigger"));
            Assert.That(trigger.color, Is.EqualTo(Color.white));
            Assert.That(computer.triggerGroups[2].triggers, Is.EqualTo(new[] { trigger }));
            Assert.That(computer.AddTriggerGroup(), Is.SameAs(computer.triggerGroups[3]));

            SplineUser user = computer.gameObject.AddComponent<SplineUser>();
            int calls = 0;
            SplineUser received = user;
            trigger.AddListener(delegate (SplineUser sender)
            {
                calls++;
                received = sender;
            });
            computer.CheckTriggers(0.4, 0.6, user);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(received == null, Is.True, "the user is not forwarded");
            computer.CheckTriggers(2, 0.4, 0.6);
            Assert.That(calls, Is.EqualTo(2));

            trigger.workOnce = true;
            computer.ResetTriggers(2);
            Assert.That(trigger.Check(0.4, 0.6), Is.True);
            Assert.That(trigger.Check(0.4, 0.6), Is.False);
            computer.ResetTriggers();
            Assert.That(trigger.Check(0.4, 0.6), Is.True);

            LogAssert.Expect(LogType.Error, "Trigger group 9 does not exist");
            computer.CheckTriggers(9, 0.4, 0.6);
            LogAssert.Expect(LogType.Error, "Trigger group -1 does not exist");
            computer.ResetTriggers(-1);
            // RemoveTrigger 0x4b8a80 prints the trigger index (its second argument, edx) where the message says "trigger group",
            // so the numbers differ from the missing group here.
            LogAssert.Expect(LogType.Error, "Cannot delete trigger - trigger group 3 does not exist");
            computer.RemoveTrigger(7, 3);
            LogAssert.Expect(LogType.Error, "Cannot delete trigger - trigger group 5 does not exist");
            computer.RemoveTrigger(-1, 5);
            computer.RemoveTrigger(2, 0);
            Assert.That(computer.triggerGroups[2].triggers, Is.Empty);
        }
    }
}
