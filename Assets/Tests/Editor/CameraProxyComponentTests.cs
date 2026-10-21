using System;
using System.Collections.Generic;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.CameraProxyAndTriggerTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CameraProxyAssignTrigger (TypeDef 0x0200025c, x86-64 0x713450..0x71352F),
    // CameraProxySettingsOverrideTrigger (0x02000733, 0x5C97E0..0x5C9B15), CameraProxyTarget_Recentering (0x02000261,
    // 0x7146D0..0x714D29) and SmoothedTransformProxy_CameraWorldSpace (0x02000a28, 0x6B4C70..0x6B5243). The components
    // are real, on owned inactive hosts, beside a real CinemachineCameraManager registered into its absent row (which
    // holds a real CameraProxyTarget). Awake and OnDestroy of the TimeScaled proxy target need the time manager's
    // lifecycle and are not run; the character-following update of the proxy target (UpdateToCharacter) belongs to
    // CameraProxyTarget and is not part of this type.
    public sealed class CameraProxyComponentTests
    {
        private sealed class Scene
        {
            internal Owned Owned;
            internal Character Character;
            internal Rig Rig;
            internal CameraProxyTarget Proxy;
        }

        private static void WithScene(Action<Scene> body) =>
            WithManager((owned, characters, character) =>
            {
                var scene = new Scene { Owned = owned, Character = character, Rig = NewRig(owned, false, true) };
                scene.Proxy = EnemyAbilityTestSupport.AddTimeScaled<CameraProxyTarget>(owned.Host("Lucid camera proxy target", false));
                Set(scene.Rig.Manager, "m_proxyTarget", scene.Proxy);
                body(scene);
            });

        private static int ManagerPriority(Scene scene) => scene.Rig.Manager.GetCameraProxySettings().Priority;

        private static int ProxyPriority(Scene scene) => Get<CameraProxyTargetSettings>(scene.Proxy, "m_settings").Priority;

        // ---- CameraProxyAssignTrigger ----

        // get_SetNullOnExit 0x713450 is a constant true (mov al, 1) that ignores the serialised flag.
        [Test]
        public void ProxyAssignAlwaysClearsTheTargetOnExit()
        {
            WithScene(scene =>
            {
                var trigger = scene.Owned.Host("Lucid camera proxy assign", false).AddComponent<CameraProxyAssignTrigger>();

                Assert.That(Get<bool>(trigger, "m_setNullOnExit"), Is.False);
                Assert.That(Call(trigger, "get_SetNullOnExit"), Is.True);
            });
        }

        // GetTargetTransform 0x713460 answers the transform of the camera manager's proxy target (the +0x60 field read
        // at 0x713498 and Component.get_transform), never the collider's. The inherited enter and exit events therefore
        // receive the proxy on entry and null on exit.
        [Test]
        public void ProxyAssignHandsTheCameraManagersProxyTargetToTheEvents()
        {
            WithScene(scene =>
            {
                var trigger = scene.Owned.Host("Lucid camera proxy assign", false).AddComponent<CameraProxyAssignTrigger>();
                var entered = new List<Transform>();
                var exited = new List<Transform>();
                var onEnter = new UnityEvent<Transform>();
                onEnter.AddListener(entered.Add);
                var onExit = new UnityEvent<Transform>();
                onExit.AddListener(exited.Add);
                Set(trigger, "m_onTriggerEnter", onEnter);
                Set(trigger, "m_onTriggerExit", onExit);
                GameObject colliderHost = scene.Owned.Host("Lucid player collider", false);
                colliderHost.tag = "Player";
                var collider = colliderHost.AddComponent<BoxCollider>();

                Call(trigger, "OnTriggerEnter", collider);
                Call(trigger, "OnTriggerExit", collider);

                Assert.That(entered, Is.EqualTo(new[] { scene.Proxy.transform }));
                Assert.That(exited.Count, Is.EqualTo(1));
                Assert.That(exited[0], Is.Null);
            });
        }

        // ---- CameraProxySettingsOverrideTrigger ----

        // DoTriggerEnter 0x5C97E0 passes the authored settings and the handle field (+0x98) to
        // SetCameraProxySettings, which publishes them on the proxy targets; DoTriggerExit 0x5C98A0 removes the override
        // for the kept handle and then stores null (the 0x5c9926 store).
        [Test]
        public void EnteringPushesTheAuthoredSettingsToTheProxyTargetAndLeavingClearsThem()
        {
            WithScene(scene =>
            {
                ConfigureProxySettingRetrieval(scene.Rig.Manager);
                var trigger = AddCharacterTrigger<CameraProxySettingsOverrideTrigger>(scene.Owned, "Lucid proxy settings trigger");
                Set(trigger, "m_overrideSettings", ProxySettings(7));
                Assert.That(ManagerPriority(scene), Is.Zero);

                Call(trigger, "DoTriggerEnter", scene.Character);

                Assert.That(Get<StackableDataHandle>(trigger, "m_cameraOverrideHandle"), Is.Not.Null);
                Assert.That(ManagerPriority(scene), Is.EqualTo(7));
                Assert.That(ProxyPriority(scene), Is.EqualTo(7));

                Call(trigger, "DoTriggerExit", scene.Character);

                Assert.That(Get<StackableDataHandle>(trigger, "m_cameraOverrideHandle"), Is.Null);
                Assert.That(ManagerPriority(scene), Is.Zero);
                Assert.That(ProxyPriority(scene), Is.Zero);
            });
        }

        // SetCameraProxySettings clears the previous handle's values and installs a new override, so entering again
        // replaces the earlier settings rather than stacking another handle on top of them.
        [Test]
        public void EnteringAgainReplacesTheEarlierOverrideWithANewHandle()
        {
            WithScene(scene =>
            {
                ConfigureProxySettingRetrieval(scene.Rig.Manager);
                var trigger = AddCharacterTrigger<CameraProxySettingsOverrideTrigger>(scene.Owned, "Lucid proxy settings trigger");
                Set(trigger, "m_overrideSettings", ProxySettings(7));
                Call(trigger, "DoTriggerEnter", scene.Character);
                var first = Get<StackableDataHandle>(trigger, "m_cameraOverrideHandle");

                Set(trigger, "m_overrideSettings", ProxySettings(9));
                Call(trigger, "DoTriggerEnter", scene.Character);

                Assert.That(Get<StackableDataHandle>(trigger, "m_cameraOverrideHandle"), Is.Not.Null.And.Not.SameAs(first));
                Assert.That(ManagerPriority(scene), Is.EqualTo(9));
                Assert.That(ProxyPriority(scene), Is.EqualTo(9));
            });
        }

        // OnDisable 0x5C99E0 tests the handle before the manager reference and so does nothing for an idle trigger;
        // with an installed override it removes it and forgets the handle. ClearOverride 0x5C9940 does the same
        // removal on request.
        [Test]
        public void DisablingAndClearingRemoveAnInstalledOverride()
        {
            WithScene(scene =>
            {
                ConfigureProxySettingRetrieval(scene.Rig.Manager);
                var trigger = AddCharacterTrigger<CameraProxySettingsOverrideTrigger>(scene.Owned, "Lucid proxy settings trigger");
                Set(trigger, "m_overrideSettings", ProxySettings(7));

                Assert.DoesNotThrow(() => Call(trigger, "OnDisable"), "an idle trigger has no handle to remove");
                Assert.That(Get<StackableDataHandle>(trigger, "m_cameraOverrideHandle"), Is.Null);

                Call(trigger, "DoTriggerEnter", scene.Character);
                Call(trigger, "OnDisable");
                Assert.That(Get<StackableDataHandle>(trigger, "m_cameraOverrideHandle"), Is.Null);
                Assert.That(ManagerPriority(scene), Is.Zero, "disabled");

                Call(trigger, "DoTriggerEnter", scene.Character);
                Assert.That(ManagerPriority(scene), Is.EqualTo(7));
                trigger.ClearOverride();
                Assert.That(Get<StackableDataHandle>(trigger, "m_cameraOverrideHandle"), Is.Null);
                Assert.That(ManagerPriority(scene), Is.Zero, "cleared");
            });
        }

        // Every entry point tests the manager reference first (SystemRef.IsNull at 0x5c9837, 0x5c98ea, 0x5c9a2d,
        // 0x5c9983) and returns without touching the handle when no camera manager is registered.
        [Test]
        public void WithoutACameraManagerNothingIsInstalledOrRemoved()
        {
            WithManager((owned, characters, character) =>
            {
                owned.LeaseRow<HardlightProject.CinemachineCameraManager>();
                var trigger = AddCharacterTrigger<CameraProxySettingsOverrideTrigger>(owned, "Lucid proxy settings trigger");
                Set(trigger, "m_overrideSettings", ProxySettings(7));

                Assert.DoesNotThrow(() => Call(trigger, "DoTriggerEnter", character));
                Assert.That(Get<StackableDataHandle>(trigger, "m_cameraOverrideHandle"), Is.Null);
                Assert.DoesNotThrow(() => Call(trigger, "DoTriggerExit", character));
                Assert.DoesNotThrow(() => trigger.ClearOverride());
                Assert.DoesNotThrow(() => Call(trigger, "OnDisable"));
            });
        }

        // ---- CameraProxyTarget_Recentering ----

        private static CameraProxyTarget_Recentering AddRecentering(Scene scene) =>
            EnemyAbilityTestSupport.AddTimeScaled<CameraProxyTarget_Recentering>(scene.Owned.Host("Lucid recentering proxy", false));

        // A free-look camera that follows the given transform and carries the manual X axis heading extension on the
        // same object (the extension's field initialisers take the CameraYAxisProxyManager row).
        private static (CinemachineFreeLook camera, CinemachineRecenterHeadingExtension_FreeLookManualXAxis extension)
            AddFreeLookWithHeadingExtension(Scene scene, Transform follow)
        {
            scene.Owned.LeaseRow<CameraYAxisProxyManager>();
            GameObject host = scene.Owned.Host("Lucid heading free look", false);
            var camera = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineFreeLook>(host);
            var extension = host.AddComponent<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>();
            camera.Follow = follow;
            return (camera, extension);
        }

        // TrySetHeadingExtension 0x7149E0 takes the heading extension from a camera whose Follow is this proxy
        // (op_Inequality(Follow, transform) skips at 0x714ad5) and records that it has one (+0x108 = 1).
        [Test]
        public void RecenteringAdoptsTheHeadingExtensionOfACameraThatFollowsIt()
        {
            WithScene(scene =>
            {
                CameraProxyTarget_Recentering recentering = AddRecentering(scene);
                var (camera, extension) = AddFreeLookWithHeadingExtension(scene, recentering.transform);
                Assert.That(Get<bool>(recentering, "m_hasHeadingExtension"), Is.False);

                Call(recentering, "TrySetHeadingExtension", camera);

                Assert.That(Get<bool>(recentering, "m_hasHeadingExtension"), Is.True);
                Assert.That(Get<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>(recentering, "m_headingExtensionFreeLook"),
                    Is.SameAs(extension));
            });
        }

        [Test]
        public void RecenteringIgnoresACameraThatFollowsSomethingElse()
        {
            WithScene(scene =>
            {
                CameraProxyTarget_Recentering recentering = AddRecentering(scene);
                Transform other = scene.Owned.Host("Lucid other follow", false).transform;
                var (camera, _) = AddFreeLookWithHeadingExtension(scene, other);

                Call(recentering, "TrySetHeadingExtension", camera);

                Assert.That(Get<bool>(recentering, "m_hasHeadingExtension"), Is.False);
                Assert.That(Get<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>(recentering, "m_headingExtensionFreeLook"),
                    Is.Null);
            });
        }

        // The extension is only ever stored on success: a null camera, or a following camera without the component,
        // leaves an earlier extension in place (the stores at 0x714b63..0x714b6f sit behind both tests).
        [Test]
        public void RecenteringKeepsAnEarlierExtensionWhenALaterCameraHasNone()
        {
            WithScene(scene =>
            {
                CameraProxyTarget_Recentering recentering = AddRecentering(scene);
                var (camera, extension) = AddFreeLookWithHeadingExtension(scene, recentering.transform);
                var plain = scene.Owned.Host("Lucid plain camera", false).AddComponent<CinemachineMixingCamera>();
                plain.Follow = recentering.transform;
                Call(recentering, "TrySetHeadingExtension", camera);

                Call(recentering, "TrySetHeadingExtension", (object)null);
                Call(recentering, "TrySetHeadingExtension", plain);

                Assert.That(Get<bool>(recentering, "m_hasHeadingExtension"), Is.True);
                Assert.That(Get<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>(recentering, "m_headingExtensionFreeLook"),
                    Is.SameAs(extension));
            });
        }

        // OnCameraLive 0x714B90 is a tail jump to TrySetHeadingExtension with the incoming camera; the previous camera
        // is ignored.
        [Test]
        public void ACameraGoingLiveIsAdoptedLikeAnyOtherCamera()
        {
            WithScene(scene =>
            {
                CameraProxyTarget_Recentering recentering = AddRecentering(scene);
                var (camera, extension) = AddFreeLookWithHeadingExtension(scene, recentering.transform);

                Call(recentering, "OnCameraLive", camera, null);

                Assert.That(Get<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>(recentering, "m_headingExtensionFreeLook"),
                    Is.SameAs(extension));
            });
        }

        // OnCameraManagerValid 0x7148D0 combines OnCameraLive into the manager's event (Delegate.Combine at 0x71494b)
        // and then adopts the brain's active camera (0x7149b4, here none), so a camera that goes live later is still
        // adopted.
        [Test]
        public void TheManagerBecomingValidSubscribesToCamerasGoingLive()
        {
            WithScene(scene =>
            {
                CameraProxyTarget_Recentering recentering = AddRecentering(scene);
                var (camera, extension) = AddFreeLookWithHeadingExtension(scene, recentering.transform);
                Assume.That(scene.Rig.Manager.CinemachineBrain.ActiveVirtualCamera, Is.Null);

                Call(recentering, "OnCameraManagerValid", scene.Rig.Manager);

                Delegate[] subscribers = scene.Rig.Manager.OnCameraLive.GetInvocationList();
                Assert.That(subscribers.Length, Is.EqualTo(1));
                Assert.That(subscribers[0].Method.Name, Is.EqualTo("OnCameraLive"));
                Assert.That(subscribers[0].Target, Is.SameAs(recentering));
                Assert.That(Get<bool>(recentering, "m_hasHeadingExtension"), Is.False);

                scene.Rig.Manager.Action_OnCameraLive(camera, null);

                Assert.That(Get<bool>(recentering, "m_hasHeadingExtension"), Is.True);
                Assert.That(Get<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>(recentering, "m_headingExtensionFreeLook"),
                    Is.SameAs(extension));
                scene.Rig.Manager.OnCameraLive -= (Action<ICinemachineCamera, ICinemachineCamera>)subscribers[0];
            });
        }

        // UpdateSmoothedTransformProxy(Character) 0x714BA0 returns at once while there is no heading extension
        // (cmp [+0x108], 0 at 0x714ba6); the base class's update is not reached, so the missing smoothed proxy is not
        // touched.
        [Test]
        public void WithoutAHeadingExtensionTheCharacterUpdateDoesNothing()
        {
            WithScene(scene =>
            {
                CameraProxyTarget_Recentering recentering = AddRecentering(scene);
                SetActorProperty(scene.Character, "WorldRotation", Quaternion.identity);

                Assert.DoesNotThrow(() => Call(recentering, "UpdateSmoothedTransformProxy", scene.Character, 0.02f));
            });
        }

        // With an extension, the update forwards the stored target position and the character's world rotation (no
        // proxy flags are set, so GetWorldRotation answers the actor rotation) to the position-and-rotation overload,
        // which hands the extension's X axis value to the smoothed proxy as the forward angle (movss xmm4 from
        // [extension + 0x140] at 0x714c12). The proxy therefore ends at the target position, turned about its up
        // axis by that value.
        [Test]
        public void TheUpdateTurnsTheSmoothedProxyByTheExtensionsXAxisValue()
        {
            WithScene(scene =>
            {
                CameraProxyTarget_Recentering recentering = AddRecentering(scene);
                var (camera, extension) = AddFreeLookWithHeadingExtension(scene, recentering.transform);
                Call(recentering, "TrySetHeadingExtension", camera);
                var smoothed = scene.Owned.Host("Lucid smoothed proxy", false).AddComponent<SmoothedTransformProxy>();
                Call(smoothed, "Initialise");
                Set(recentering, "m_smoothedTransformProxy", smoothed);
                Set(recentering, "m_currentTargetPosition", new Vector3(1f, 2f, 3f));
                SetActorProperty(scene.Character, "WorldRotation", Quaternion.identity);

                var axis = new AxisState { Value = 30f };
                Set(extension, "m_xAxisState", axis);
                Call(recentering, "UpdateSmoothedTransformProxy", scene.Character, 0.02f);

                Assert.That(smoothed.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Quaternion.Angle(smoothed.transform.rotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(1e-3f));

                axis.Value = -45f;
                Set(extension, "m_xAxisState", axis);
                Call(recentering, "UpdateSmoothedTransformProxy", scene.Character, 0.02f);

                Assert.That(Quaternion.Angle(smoothed.transform.rotation, Quaternion.Euler(0f, -45f, 0f)), Is.LessThan(1e-3f));
            });
        }

        // ---- SmoothedTransformProxy_CameraWorldSpace ----

        private sealed class WorldSpaceScene
        {
            internal SmoothedTransformProxy_CameraWorldSpace Proxy;
            internal CameraYAxisProxyManager YAxis;
            internal Transform Child;
        }

        // The proxy's Awake (run by name) adopts the registered Y axis proxy manager through InvokeOnValid. The manager
        // reports a world forward of +Z and a 5 degree difference from its last forward.
        private static void WithWorldSpaceProxy(Action<Owned, WorldSpaceScene> body,
            Action<SmoothedTransformProxy_CameraWorldSpace> configureBeforeAwake = null) =>
            WithManager((owned, characters, character) =>
            {
                Rig rig = NewRig(owned, false, true);
                var scene = new WorldSpaceScene { YAxis = RegisterYAxisManager(owned, rig.Brain) };
                SetYAxisProperty(scene.YAxis, "TargetForward", Vector3.forward);
                SetYAxisProperty(scene.YAxis, "SignedAngleDifferenceFromLastForward", 5f);
                GameObject host = owned.Host("Lucid world space proxy", false);
                scene.Proxy = host.AddComponent<SmoothedTransformProxy_CameraWorldSpace>();
                scene.Child = owned.Host("Lucid forward rotation child", false, host.transform).transform;
                Set(scene.Proxy, "m_forwardRotationChild", scene.Child);
                configureBeforeAwake?.Invoke(scene.Proxy);
                Call(scene.Proxy, "Awake");
                body(owned, scene);
            });

        private static void AssertRotation(Quaternion actual, Quaternion expected, string why) =>
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(1e-3f), why);

        [Test]
        public void WorldSpaceAwakeAdoptsTheYAxisProxyManager()
        {
            WithWorldSpaceProxy((owned, scene) =>
                Assert.That(Get<CameraYAxisProxyManager>(scene.Proxy, "m_cameraYAxisProxyManager"), Is.SameAs(scene.YAxis)));
        }

        // UpdatePositionAndRotation 0x6B4D20 sets the position, applies the target rotation and then turns the child by
        // worldForwardAngle - SignedAngle(proxy forward projected on the proxy's up, transform forward, up) -
        // SignedAngleDifferenceFromLastForward. With a rotation of zero degrees the signed angle is 0, so the child
        // turns by 40 - 0 - 5 = 35 degrees about up.
        [Test]
        public void WorldSpaceUpdateSetsThePositionAndTurnsTheChildByTheCompensatedForwardAngle()
        {
            WithWorldSpaceProxy((owned, scene) =>
            {
                scene.Proxy.UpdatePositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.identity, 40f, 0.02f);

                Assert.That(scene.Proxy.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                AssertRotation(scene.Child.localRotation, Quaternion.Euler(0f, 35f, 0f), "child");
            });
        }

        // Turned 90 degrees about Y the proxy's forward is +X, so SignedAngle((0,0,1), (1,0,0), up) = +90 and the
        // child turns by 40 - 90 - 5 = -55 degrees. Smoothing is off so the target is applied exactly.
        [Test]
        public void WorldSpaceUpdateSubtractsTheProxysOwnHeadingFromTheForwardAngle()
        {
            WithWorldSpaceProxy((owned, scene) =>
            {
                scene.Proxy.ToggleSmoothing(false);

                scene.Proxy.UpdatePositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f), 40f, 0.02f);

                AssertRotation(scene.Proxy.transform.rotation, Quaternion.Euler(0f, 90f, 0f), "proxy");
                AssertRotation(scene.Child.localRotation, Quaternion.Euler(0f, -55f, 0f), "child");
            });
        }

        // With smoothing on, UpdatePositionAndRotation inlines the base GetTargetRotation (smoothing test at 0x6b4d9b, then
        // acos, the snap bounds, Evaluate, the RotationSlerpMultiplier and the Slerp at 0x6b4e7e) starting from the
        // proxy's current rotation. A 90 degree difference evaluates the constant curve (1) times the multiplier 25 and
        // the 0.02 frame time, so t = 0.5: the proxy turns to 45 degrees and the child by 40 - 45 - 5 = -10.
        [Test]
        public void WorldSpaceUpdateWithSmoothingSlerpsTheProxyTowardTheTargetFromItsCurrentRotation()
        {
            WithWorldSpaceProxy((owned, scene) =>
            {
                scene.Proxy.UpdatePositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f), 40f, 0.02f);

                AssertRotation(scene.Proxy.transform.rotation, Quaternion.Euler(0f, 45f, 0f), "proxy");
                AssertRotation(scene.Child.localRotation, Quaternion.Euler(0f, -10f, 0f), "child");
            }, proxy =>
            {
                Set(proxy, "m_rotationSlerpCurve", AnimationCurve.Constant(0f, 1f, 1f));
                Set(proxy, "m_rotationSlerpMultiplier", 25f);
            });
        }

        // The position is set first and the function stops once rotation updates are disabled (0x6b4d58..0x6b4d6c and
        // the final test at 0x6b4e9c), so the proxy and the child keep their rotations.
        [Test]
        public void WorldSpaceUpdateWithRotationDisabledOnlyMovesTheProxy()
        {
            WithWorldSpaceProxy((owned, scene) =>
            {
                scene.Proxy.ToggleDisableRotationUpdate(true);

                scene.Proxy.UpdatePositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f), 40f, 0.02f);

                Assert.That(scene.Proxy.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                AssertRotation(scene.Proxy.transform.rotation, Quaternion.identity, "proxy");
                AssertRotation(scene.Child.localRotation, Quaternion.identity, "child");
            });
        }

        // An overridden rotation also ends the update right after the position is set (cmp [+0x72] at 0x6b4d5a jumps to
        // the epilogue): the proxy keeps the rotation OverrideRotation gave it and the child is not turned.
        [Test]
        public void WorldSpaceUpdateWithRotationOverriddenOnlyMovesTheProxy()
        {
            WithWorldSpaceProxy((owned, scene) =>
            {
                scene.Proxy.OverrideRotation(Quaternion.Euler(0f, 30f, 0f));
                scene.Child.localRotation = Quaternion.Euler(0f, 20f, 0f);

                scene.Proxy.UpdatePositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f), 40f, 0.02f);

                Assert.That(scene.Proxy.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                AssertRotation(scene.Proxy.transform.rotation, Quaternion.Euler(0f, 30f, 0f), "proxy");
                AssertRotation(scene.Child.localRotation, Quaternion.Euler(0f, 20f, 0f), "child");
            });
        }

        // A destroyed proxy ignores the update altogether: the first test at 0x6b4d2b is the destroyed flag.
        [Test]
        public void WorldSpaceUpdateIsIgnoredOnceTheProxyIsDestroyed()
        {
            WithWorldSpaceProxy((owned, scene) =>
            {
                Call(scene.Proxy, "OnDestroy");

                scene.Proxy.UpdatePositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.identity, 40f, 0.02f);

                Assert.That(scene.Proxy.transform.position, Is.EqualTo(Vector3.zero));
                AssertRotation(scene.Child.localRotation, Quaternion.identity, "child");
            });
        }

        // SetRotation 0x6B50E0 applies the world rotation like the base setter, and then, only while rotation updates
        // are disabled (cmp [+0x73] at 0x6b5109), resets the child to the identity.
        [Test]
        public void WorldSpaceSetRotationResetsTheChildOnlyWhileRotationUpdatesAreDisabled()
        {
            WithWorldSpaceProxy((owned, scene) =>
            {
                scene.Child.localRotation = Quaternion.Euler(0f, 20f, 0f);

                Call(scene.Proxy, "SetRotation", Quaternion.Euler(0f, 50f, 0f));

                AssertRotation(scene.Proxy.transform.rotation, Quaternion.Euler(0f, 50f, 0f), "applied while enabled");
                AssertRotation(scene.Child.localRotation, Quaternion.Euler(0f, 20f, 0f), "child kept while enabled");

                scene.Proxy.ToggleDisableRotationUpdate(true);
                Call(scene.Proxy, "SetRotation", Quaternion.Euler(0f, 80f, 0f));

                AssertRotation(scene.Proxy.transform.rotation, Quaternion.Euler(0f, 50f, 0f), "not applied while disabled");
                AssertRotation(scene.Child.localRotation, Quaternion.identity, "child reset while disabled");
            });
        }
    }
}
