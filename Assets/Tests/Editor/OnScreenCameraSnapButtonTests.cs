using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UISelectionShared;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OnScreenCameraSnapButton (0200096e) against its 1.10.1 x86-64 bodies (0x68C8C0..0x68CB30). The manager
    // is a real CinemachineCameraManager on an owned inactive host with a brain on a real Camera, registered
    // before the button is built, since the button's field initialiser caches the reference. Not covered: the
    // answer true of LiveCameraIsUsingSnapExtension, which needs a live virtual camera that has a snap
    // extension; with no live camera the manager answers false.
    public sealed class OnScreenCameraSnapButtonTests
    {
        private static void WithButton(System.Action<Owned, Rig, OnScreenCameraSnapButton, UIButtonProbe> body) => Owned.Run(owned =>
        {
            Rig rig = NewRig(owned, true, true);
            var button = owned.Host("Lucid owned camera snap button", false).AddComponent<OnScreenCameraSnapButton>();
            var uiButton = owned.Host("Lucid owned camera snap ui button", false).AddComponent<UIButtonProbe>();
            Set(button, "m_button", uiButton); // serialized in the original
            body(owned, rig, button, uiButton);
        });

        // OnEnable 0x68C8C0 hands OnCameraManagerReady to the reference's InvokeOnValid, which runs at once for a
        // registered manager; 0x68C950 adds OnCameraTypeChanged to the brain's camera-activated event (+0x68 of the
        // brain). OnCameraTypeChanged 0x68CAD0 then sets the button's Interactable to the manager's snap answer.
        [Test]
        public void EnablingListensToTheBrainAndTheEventDrivesTheButton()
        {
            WithButton((owned, rig, button, uiButton) =>
            {
                Assert.That(uiButton.Interactable, Is.True, "interactable by default");
                rig.Brain.m_CameraActivatedEvent.Invoke(null, null);
                Assert.That(uiButton.Interactable, Is.True, "nothing listens before OnEnable");

                Call(button, "OnEnable");
                rig.Brain.m_CameraActivatedEvent.Invoke(null, null);
                Assert.That(rig.Manager.LiveCameraIsUsingSnapExtension(), Is.False, "no live camera");
                Assert.That(uiButton.Interactable, Is.False, "the button follows the manager's answer");
            });
        }

        // OnDisable 0x68C9F0 removes the same handler from the brain's event while the reference is not null.
        [Test]
        public void DisablingStopsListeningToTheBrain()
        {
            WithButton((owned, rig, button, uiButton) =>
            {
                Call(button, "OnEnable");
                Call(button, "OnDisable");
                rig.Brain.m_CameraActivatedEvent.Invoke(null, null);
                Assert.That(uiButton.Interactable, Is.True, "the removed handler is not called");
            });
        }

        // OnDisable 0x68C9F0 returns at once when the manager reference IsNull (0x68CA5C), so a button that was
        // never given a manager leaves nothing to remove.
        [Test]
        public void DisablingWithoutACameraManagerDoesNothing()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CinemachineCameraManager>();
                var button = owned.Host("Lucid owned camera snap button", false).AddComponent<OnScreenCameraSnapButton>();
                Assert.DoesNotThrow(() => Call(button, "OnDisable"));
            });
        }
    }
}
