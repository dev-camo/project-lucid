using System;
using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CameraFacingListener (TypeDef 0x02000257, x86-64 0x711F60..0x7123AF). The listener is a
    // real component on an owned inactive host; the CharacterManager and the CinemachineCameraManager are real systems
    // registered into their absent rows before the listener's Awake caches the references. The camera is the manager's
    // own brain camera, rotated to the direction under test; the character stands at (5, 0, 0) and the look target at
    // (5, 0, 10), so the direction from the character to the target is exactly (0, 0, 1).
    public sealed class CameraFacingListenerTests
    {
        private sealed class Case
        {
            internal CameraFacingListener Listener;
            internal Rig Rig;
            internal CharacterManager Manager;
            internal Character Character;
            internal Transform Target;
            internal readonly List<string> Log = new List<string>();
        }

        private static void WithListener(float enter, float leave, Action<Owned, Case> body) =>
            WithManager((owned, manager, character) =>
            {
                var scene = new Case { Manager = manager, Character = character, Rig = NewRig(owned, false, true) };
                SetCurrent(manager, character, true);
                SetActorProperty(character, "WorldPosition", new Vector3(5f, 0f, 0f));
                scene.Target = owned.Host("Lucid look target", false).transform;
                scene.Target.position = new Vector3(5f, 0f, 10f);
                scene.Listener = owned.Host("Lucid camera facing listener", false).AddComponent<CameraFacingListener>();
                Set(scene.Listener, "m_lookTarget", scene.Target);
                Set(scene.Listener, "m_alignmentThresholdEnter", enter);
                Set(scene.Listener, "m_alignmentThresholdLeave", leave);
                foreach ((string field, string name) in new[]
                         { ("m_onCameraFacingEnter", "enter"), ("m_onCameraFacingStay", "stay"), ("m_onCameraFacingLeave", "leave") })
                {
                    var unityEvent = new UnityEvent();
                    unityEvent.AddListener(() => scene.Log.Add(name));
                    Set(scene.Listener, field, unityEvent);
                }
                Call(scene.Listener, "Awake");
                body(owned, scene);
            });

        // Turns the camera towards the direction and runs one Update, answering the events it raised.
        private static string[] Step(Case scene, Vector3 cameraForward)
        {
            scene.Rig.Brain.transform.rotation = Quaternion.LookRotation(cameraForward);
            scene.Log.Clear();
            Call(scene.Listener, "Update");
            return scene.Log.ToArray();
        }

        private static readonly Vector3 Towards = Vector3.forward;
        private static readonly Vector3 Away = Vector3.back;
        // A unit vector whose dot product with (0, 0, 1) is 0.3.
        private static readonly Vector3 Partly = new Vector3(0.9539392f, 0f, 0.3f);

        // Update 0x712020 takes the dot product of the camera's forward with the normalised direction from the
        // character to the look target. At or above the enter threshold (jae, 0x712274) it enters, once; below the leave
        // threshold (ucomiss 0x71227f) it leaves, once; in between the state is kept. Stay is raised when the state was
        // entered before the update and still is afterwards (0x7122ac..0x7122b7), never in the update that enters.
        [Test]
        public void EnteringStayingAndLeavingFollowTheAlignmentThresholds()
        {
            WithListener(0.5f, 0.2f, (owned, scene) =>
            {
                Assert.That(Step(scene, Away), Is.Empty, "not entered below the enter threshold");
                Assert.That(Step(scene, Partly), Is.Empty, "between the thresholds while outside");
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "enter" }), "entering does not also stay");
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "stay" }));
                Assert.That(Step(scene, Partly), Is.EqualTo(new[] { "stay" }), "between the thresholds while inside");
                Assert.That(Step(scene, Away), Is.EqualTo(new[] { "leave" }), "leaving does not also stay");
                Assert.That(Step(scene, Away), Is.Empty);
            });
        }

        // The enter test is >= (jae after ucomiss, 0x712274): an alignment of exactly 1 meets an enter threshold of 1.
        [Test]
        public void AnAlignmentEqualToTheEnterThresholdEnters()
        {
            WithListener(1f, 0f, (owned, scene) =>
            {
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "enter" }));
            });
        }

        // The leave test is strict (the leave threshold must be above the alignment, jbe at 0x712282): with the
        // threshold equal to the alignment the listener stays, and only a higher threshold leaves.
        [Test]
        public void AnAlignmentEqualToTheLeaveThresholdStays()
        {
            WithListener(0.5f, 0.2f, (owned, scene) =>
            {
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "enter" }));
                Set(scene.Listener, "m_alignmentThresholdEnter", 2f);
                Set(scene.Listener, "m_alignmentThresholdLeave", 1f);
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "stay" }), "alignment 1 is not below 1");
                Set(scene.Listener, "m_alignmentThresholdLeave", 1.5f);
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "leave" }));
            });
        }

        // The first test of Update 0x712020 (0x71207e..0x7120ee): no valid character, or no look target, ends the
        // entered state with a leave event (0x7120f2..0x712115) and raises nothing else, not even stay.
        [Test]
        public void AnInvalidCharacterOrALostLookTargetLeavesOnce()
        {
            WithListener(0.5f, 0.2f, (owned, scene) =>
            {
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "enter" }));
                SetCurrent(scene.Manager, scene.Character, false);
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "leave" }), "the character is no longer valid");
                Assert.That(Step(scene, Towards), Is.Empty, "already left");

                SetCurrent(scene.Manager, scene.Character, true);
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "enter" }));
                UnityEngine.Object.DestroyImmediate(scene.Target.gameObject);
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "leave" }), "the look target is gone");
            });
        }

        // OnEnable 0x712010 clears the entered flag, so the next qualifying update enters again.
        [Test]
        public void EnablingForgetsTheEnteredState()
        {
            WithListener(0.5f, 0.2f, (owned, scene) =>
            {
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "enter" }));
                Call(scene.Listener, "OnEnable");
                Assert.That(Step(scene, Towards), Is.EqualTo(new[] { "enter" }));
            });
        }

        // Reset 0x712320 fills an empty look target with the listener's own transform and keeps an assigned one.
        [Test]
        public void ResetDefaultsTheLookTargetToTheListenerItself()
        {
            WithListener(0.5f, 0.2f, (owned, scene) =>
            {
                Call(scene.Listener, "Reset");
                Assert.That(Get<Transform>(scene.Listener, "m_lookTarget"), Is.SameAs(scene.Target), "an assigned target is kept");

                Set(scene.Listener, "m_lookTarget", null);
                Call(scene.Listener, "Reset");
                Assert.That(Get<Transform>(scene.Listener, "m_lookTarget"), Is.SameAs(scene.Listener.transform));
            });
        }
    }
}
