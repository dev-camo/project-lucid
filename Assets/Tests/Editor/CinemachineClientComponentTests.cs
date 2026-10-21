using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using CameraType = HardlightProject.CameraType;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CinemachineAssignTargetToCameraType (TypeDef 0x02000269, x86-64 0x717EE0..0x7183FF),
    // CinemachineCameraResetYRecenter (0x0200026F, 0x71C8A0..0x71D1A7) and CinemachineCameraYAxisOverride (0x02000270,
    // 0x71D1B0..0x71D42F). Each component is real, on an owned inactive host, beside a real CinemachineCameraManager
    // registered into its absent row; the free-look camera is a real CinemachineFreeLook. Not covered: the OnEnable of the
    // recenter component, which subscribes to the LevelManager's level events; its OnLevelActivated, ResetYAxisValue and
    // OnDisable are called directly with an idle LevelManager row.
    public sealed class CinemachineClientComponentTests
    {
        // ---- CinemachineAssignTargetToCameraType ----

        private static Delegate[] MainCameraReadyHandlers(CinemachineCameraManager manager, object owner) =>
            (manager.OnMainCameraReady == null ? new Delegate[0] : manager.OnMainCameraReady.GetInvocationList())
                .Where(handler => ReferenceEquals(handler.Target, owner) && handler.Method.Name == "OnMainCameraReady").ToArray();

        private static CinemachineAssignTargetToCameraType NewAssign(Owned owned, CameraType type, Transform target, bool lookAt, bool follow)
        {
            var assign = owned.Host("Lucid assign camera target", false).AddComponent<CinemachineAssignTargetToCameraType>();
            Set(assign, "m_cameraType", type);
            Set(assign, "m_target", target);
            Set(assign, "m_setAsLookAtTarget", lookAt);
            Set(assign, "m_setAsFollowTarget", follow);
            return assign;
        }

        // OnEnable 0x717EE0 subscribes to the system startup and, for a live manager, runs the startup callback at once
        // (0x717f89). That callback 0x717FD0 subscribes to the manager's OnMainCameraReady and runs the ready callback
        // only if the camera is already ready (the byte at +0xC0). The ready callback 0x7181B0 unsubscribes itself and
        // sets the targets of the type's camera. A target flag that is off passes null, which leaves that target of the
        // camera as it was (SetVirtualCameraTargets only assigns non-null values).
        [Test]
        public void TheTargetIsAssignedOnceWhenTheMainCameraBecomesReady()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, true, true);
                CinemachineMixingCamera camera = AddCamera(owned, rig, CameraType.Rail, 5);
                Transform keptFollow = owned.Host("Lucid kept follow", false).transform;
                Transform keptLookAt = owned.Host("Lucid kept look at", false).transform;
                Transform target = owned.Host("Lucid assigned target", false).transform;
                camera.Follow = keptFollow;
                camera.LookAt = keptLookAt;
                CinemachineAssignTargetToCameraType assign = NewAssign(owned, CameraType.Rail, target, true, false);

                Call(assign, "OnEnable");
                Assert.That(MainCameraReadyHandlers(rig.Manager, assign).Length, Is.EqualTo(1), "waiting for the main camera");
                Assert.That((camera.Follow, camera.LookAt), Is.EqualTo((keptFollow, keptLookAt)), "nothing is assigned yet");

                rig.Manager.OnMainCameraReady(rig.Camera);

                Assert.That((camera.Follow, camera.LookAt), Is.EqualTo((keptFollow, target)), "only the look-at flag is set");
                Assert.That(MainCameraReadyHandlers(rig.Manager, assign), Is.Empty, "the ready callback unsubscribed itself");

                camera.LookAt = keptLookAt;
                rig.Manager.OnMainCameraReady(rig.Camera);
                Assert.That(camera.LookAt, Is.SameAs(keptLookAt), "a second ready event assigns nothing");
            });
        }

        // OnCameraManagerValid 0x717FD0 tests the manager's ready byte (+0xC0, 0x71808e) and, when set, runs the ready
        // callback with the brain's output camera (0x718098..0x7180b5) instead of waiting for the event. The manager's
        // own WaitForMainCamera iterator is stepped by hand to raise the readiness as the original does.
        [Test]
        public void AnAlreadyReadyMainCameraAssignsTheTargetAtOnce()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, true, true);
                CinemachineMixingCamera camera = AddCamera(owned, rig, CameraType.Rail, 5);
                Transform target = owned.Host("Lucid assigned target", false).transform;
                var waiting = (IEnumerator)Call(rig.Manager, "WaitForMainCamera");
                Assert.That(waiting.MoveNext(), Is.True);
                Assert.That(waiting.MoveNext(), Is.False);
                Assert.That(rig.Manager.MainCameraIsReady, Is.True);
                CinemachineAssignTargetToCameraType assign = NewAssign(owned, CameraType.Rail, target, true, false);

                Call(assign, "OnEnable");

                Assert.That(camera.LookAt, Is.SameAs(target));
                Assert.That(MainCameraReadyHandlers(rig.Manager, assign), Is.Empty, "the immediate call unsubscribed itself");
            });
        }

        [Test]
        public void EachTargetFlagControlsItsOwnTarget()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, true, true);
                Transform keptFollow = owned.Host("Lucid kept follow", false).transform;
                Transform keptLookAt = owned.Host("Lucid kept look at", false).transform;
                Transform target = owned.Host("Lucid assigned target", false).transform;
                foreach ((bool follow, bool lookAt, Transform expectedFollow, Transform expectedLookAt) in new[]
                         {
                             (false, false, keptFollow, keptLookAt),
                             (true, false, target, keptLookAt),
                             (false, true, keptFollow, target),
                             (true, true, target, target),
                         })
                {
                    CinemachineMixingCamera camera = AddCamera(owned, rig, CameraType.Rail, 5);
                    camera.Follow = keptFollow;
                    camera.LookAt = keptLookAt;
                    CinemachineAssignTargetToCameraType assign = NewAssign(owned, CameraType.Rail, target, lookAt, follow);

                    Call(assign, "OnMainCameraReady", rig.Camera);

                    Assert.That((camera.Follow, camera.LookAt), Is.EqualTo((expectedFollow, expectedLookAt)), "follow " + follow + ", look at " + lookAt);
                    rig.Manager.UnregisterCameraByType(CameraType.Rail);
                }
            });
        }

        // OnDisable 0x7180F0 unsubscribes from the system startup only while the manager is live (the IsValid test at
        // 0x71814b), so a disable before the manager exists leaves the subscription in place, and the manager's later
        // registration then runs the startup callback (the OnSystemStartup event). A disable with a live manager removes it.
        [Test]
        public void DisablingBeforeTheManagerExistsLeavesTheStartupSubscriptionInPlace()
        {
            GameInputScope.Run(scope => Owned.Run(owned =>
            {
                scope.Row<CinemachineCameraManager>();
                CinemachineAssignTargetToCameraType assign = NewAssign(owned, CameraType.Rail, null, false, false);
                Call(assign, "OnEnable");
                Call(assign, "OnDisable");

                Rig rig = NewRig(owned, true, false);
                scope.Register(rig.Manager);
                Assert.That(MainCameraReadyHandlers(rig.Manager, assign).Length, Is.EqualTo(1), "the startup subscription survived the disable");

                // The manager is live now, so this disable removes the subscription: registering the manager again
                // does not subscribe a second time.
                Call(assign, "OnDisable");
                scope.Unregister(rig.Manager);
                scope.Register(rig.Manager);
                Assert.That(MainCameraReadyHandlers(rig.Manager, assign).Length, Is.EqualTo(1), "no startup callback after the second disable");
            }));
        }

        // ---- CinemachineCameraResetYRecenter and CinemachineCameraYAxisOverride ----

        private static CinemachineCameraResetYRecenter NewRecenter(Owned owned, CinemachineFreeLook freeLook, float resetValue)
        {
            var recenter = owned.Host("Lucid y recenter", false).AddComponent<CinemachineCameraResetYRecenter>();
            Set(recenter, "m_targetCamera", freeLook);
            Set(recenter, "m_resetValue", resetValue);
            return recenter;
        }

        // The constructor 0x71D0A0 stores 0.5f (0x3f000000) at +0x28, true at +0x48 and a new empty list at +0x50.
        [Test]
        public void ARecenterComponentResetsToHalfAndAllowsResetOnCameraLiveByDefault()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CinemachineCameraManager>();
                owned.LeaseRow<LevelManager>();
                var recenter = owned.Host("Lucid y recenter", false).AddComponent<CinemachineCameraResetYRecenter>();

                Assert.That(Get<float>(recenter, "m_resetValue"), Is.EqualTo(0.5f));
                Assert.That(Get<bool>(recenter, "m_allowResetOnCameraLive"), Is.True);
                Assert.That(Get<List<ICinemachineCamera>>(recenter, "m_virtualCamerasToAlwaysResetFrom"), Is.Empty);
            });
        }

        // OnLevelActivated 0x71CA20 and ResetYAxisValue 0x71CA40 each store the reset value in the free-look camera's
        // y axis (the float at +0x108 of the camera, the Value of its m_YAxis).
        [Test]
        public void ActivatingALevelStoresTheResetValueInTheYAxis()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CinemachineCameraManager>();
                owned.LeaseRow<LevelManager>();
                CinemachineFreeLook freeLook = NewFreeLook(owned, 0.9f);
                CinemachineCameraResetYRecenter recenter = NewRecenter(owned, freeLook, 0.25f);

                Call(recenter, "OnLevelActivated", (object)null);
                Assert.That(freeLook.m_YAxis.Value, Is.EqualTo(0.25f));

                freeLook.m_YAxis.Value = 0.8f;
                Call(recenter, "ResetYAxisValue");
                Assert.That(freeLook.m_YAxis.Value, Is.EqualTo(0.25f));
            });
        }

        private static void WithRecenter(Action<Owned, Rig, CinemachineFreeLook, CinemachineCameraResetYRecenter, CinemachineMixingCamera> body) =>
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, true, true);
                CinemachineMixingCamera rail = AddCamera(owned, rig, CameraType.Rail, 5);
                owned.LeaseRow<LevelManager>();
                CinemachineFreeLook freeLook = NewFreeLook(owned, 0.9f);
                CinemachineCameraResetYRecenter recenter = NewRecenter(owned, freeLook, 0.25f);
                Set(recenter, "m_cameraTypesToAllowsResetFrom", new List<CameraType> { CameraType.Rail });
                Call(recenter, "CameraManagerReady", rig.Manager);
                body(owned, rig, freeLook, recenter, rail);
            });

        // CameraManagerReady 0x71CCD0 subscribes the camera-live and y-axis-recenter callbacks to the manager and
        // collects the virtual camera of every listed type. OnCameraLive 0x71D010 resets only when the target camera
        // is the one going live (0x71d03f), and then when resets are allowed (0x71d045) or the camera it replaces is
        // one of the collected ones (the List.Contains at 0x71d05c).
        [Test]
        public void ACameraGoingLiveResetsTheYAxisWhenAllowedOrComingFromAListedCamera()
        {
            WithRecenter((owned, rig, freeLook, recenter, rail) =>
            {
                CinemachineMixingCamera other = AddCamera(owned, rig, CameraType.Default, 6);
                (bool allowed, ICinemachineCamera live, ICinemachineCamera previous, bool expectedReset, string label)[] table =
                {
                    (true, freeLook, other, true, "allowed"),
                    (true, other, freeLook, false, "another camera goes live"),
                    (false, freeLook, other, false, "not allowed and not coming from a listed camera"),
                    (false, freeLook, rail, true, "not allowed but coming from a listed camera"),
                    (false, other, rail, false, "another camera goes live, even from a listed camera"),
                };
                foreach ((bool allowed, ICinemachineCamera live, ICinemachineCamera previous, bool expectedReset, string label) in table)
                {
                    recenter.Action_SetAllowResetOnCameraLive(allowed);
                    freeLook.m_YAxis.Value = 0.9f;

                    rig.Manager.Action_OnCameraLive(live, previous);

                    Assert.That(freeLook.m_YAxis.Value, Is.EqualTo(expectedReset ? 0.25f : 0.9f), label);
                }
            });
        }

        [Test]
        public void TheManagersYAxisRecenterRequestResetsTheAxisUntilTheComponentIsDisabled()
        {
            WithRecenter((owned, rig, freeLook, recenter, rail) =>
            {
                rig.Manager.ResetYAxisRecenteringOnAllCameras();
                Assert.That(freeLook.m_YAxis.Value, Is.EqualTo(0.25f));

                // OnDisable 0x71CA60 unsubscribes both callbacks from a live manager.
                Call(recenter, "OnDisable");
                freeLook.m_YAxis.Value = 0.9f;
                rig.Manager.ResetYAxisRecenteringOnAllCameras();
                rig.Manager.Action_OnCameraLive(freeLook, rail);
                Assert.That(freeLook.m_YAxis.Value, Is.EqualTo(0.9f));
            });
        }

        // ---- CinemachineCameraYAxisOverride ----

        private static CameraAxisStateDefinition AxisDefinition(Owned owned, AxisState.SpeedMode mode, float maxSpeed, float accel, float decel)
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<CameraAxisStateDefinition>());
            Set(definition, "m_speedMode", mode);
            Set(definition, "m_maxSpeed", maxSpeed);
            Set(definition, "m_accelTime", accel);
            Set(definition, "m_decelTime", decel);
            return definition;
        }

        private static void WithYAxisOverride(Action<Owned, Rig, CinemachineFreeLook, CinemachineCameraYAxisOverride> body) =>
            WithManager((owned, manager, character) =>
            {
                Rig rig = NewRig(owned, false, true);
                CinemachineFreeLook freeLook = NewFreeLook(owned, 0.9f);
                var overrides = owned.Track(ScriptableObject.CreateInstance<CameraAxisStateOverrides>());
                Get<SerializableDictionary<CameraSensitivityType, CameraAxisStateDefinition>>(overrides, "m_overrides")
                    .Add(CameraSensitivityType.High, AxisDefinition(owned, AxisState.SpeedMode.InputValueGain, 7f, 0.3f, 0.4f));
                Get<SerializableDictionary<CameraSensitivityType, CameraAxisStateDefinition>>(overrides, "m_overrides")
                    .Add(CameraSensitivityType.Low, AxisDefinition(owned, AxisState.SpeedMode.MaxSpeed, 1f, 0.1f, 0.2f));
                var component = owned.Host("Lucid y axis override", false).AddComponent<CinemachineCameraYAxisOverride>();
                Set(component, "m_targetCamera", freeLook);
                Set(component, "m_yAxisStateOverrides", overrides);
                body(owned, rig, freeLook, component);
            });

        // OnCameraSensitivityChanged 0x71D2C0 forwards the sensitivity and the camera's y axis to the override table
        // (SetOverride, argument +0x108 of the camera). A defined sensitivity stores the four axis parameters; the
        // axis value and limits are kept, and an undefined sensitivity leaves the axis alone.
        [Test]
        public void ASensitivityWithADefinitionSetsTheFourAxisParametersOnly()
        {
            WithYAxisOverride((owned, rig, freeLook, component) =>
            {
                Call(component, "OnCameraSensitivityChanged", CameraSensitivityType.High);

                Assert.That((freeLook.m_YAxis.m_SpeedMode, freeLook.m_YAxis.m_MaxSpeed, freeLook.m_YAxis.m_AccelTime, freeLook.m_YAxis.m_DecelTime),
                    Is.EqualTo((AxisState.SpeedMode.InputValueGain, 7f, 0.3f, 0.4f)));
                Assert.That(freeLook.m_YAxis.Value, Is.EqualTo(0.9f), "the axis value is kept");

                Call(component, "OnCameraSensitivityChanged", CameraSensitivityType.Medium);
                Assert.That(freeLook.m_YAxis.m_MaxSpeed, Is.EqualTo(7f), "no definition for medium");

                Call(component, "OnCameraSensitivityChanged", CameraSensitivityType.Low);
                Assert.That((freeLook.m_YAxis.m_SpeedMode, freeLook.m_YAxis.m_MaxSpeed), Is.EqualTo((AxisState.SpeedMode.MaxSpeed, 1f)));
            });
        }

        // OnCameraManagerReady 0x71D240 registers the sensitivity callback with the manager and OnDestroy 0x71D2E0
        // removes it again while the manager is live.
        [Test]
        public void TheManagersSensitivityChangesReachTheComponentUntilItIsDestroyed()
        {
            WithYAxisOverride((owned, rig, freeLook, component) =>
            {
                // Subscribing reads the saved sensitivity (0x71D240 -> InvokeOnSensitivityChanged) from the settings of the
                // SaveManager the character fixture registered, which has no settings until the save flow loads them.
                Set(ProcessManager.GetSystem<SaveManager>(null, true), "m_saveSettings", new SaveDataSettings()); // loaded by the save flow in the original
                Call(component, "OnCameraManagerReady", rig.Manager);
                // Subscribing also applies the saved sensitivity at once, so the axis is reset to a known state first.
                freeLook.m_YAxis.m_MaxSpeed = 99f;

                Call(rig.Manager, "OnSensitivityChanged", CameraSensitivityType.High);
                Assert.That(freeLook.m_YAxis.m_MaxSpeed, Is.EqualTo(7f), "subscribed");

                freeLook.m_YAxis.m_MaxSpeed = 99f;
                Call(component, "OnDestroy");
                Call(rig.Manager, "OnSensitivityChanged", CameraSensitivityType.High);
                Assert.That(freeLook.m_YAxis.m_MaxSpeed, Is.EqualTo(99f), "unsubscribed");
            });
        }
    }
}
