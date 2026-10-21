using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.CameraProxyAndTriggerTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using CameraType = HardlightProject.CameraType;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CinemachineRegisterForGimbalLock (TypeDef 0x0200028d, x86-64 0x723C90..0x723FDA),
    // CinemachineSceneTriggerCamera (0x02000291, 0x7240E0..0x724889), CinemachineTargetGroupAssigner_CameraProxy
    // (0x02000296, 0x725300..0x72560F), CinemachineTargetGroupAssigner_PlayerCharacter (0x02000297, 0x725610..0x7259D4),
    // CinemachineYUpOverrideTrigger (0x02000298, 0x7259E0..0x725BDB), FTUECameraSection (0x02000a69, 0x6C43C0..0x6C451F),
    // SequencedCamera (0x02000611, 0x568590..0x56861E) and SnapCameraToCharacterHeadingTrigger (0x0200073e,
    // 0x5CB180..0x5CB382). Every component is real, on an owned inactive host, beside a real CinemachineCameraManager
    // (and, where the original talks to it, a real CameraYAxisProxyManager or CharacterManager) registered into its
    // absent row. Not covered here: the level-manager subscriptions of CinemachineSceneTriggerCamera.Start and
    // OnDestroy and the destruction after the exit (they need a LevelManager and play mode), the delayed (coroutine)
    // snap of SnapCameraToCharacterHeadingTrigger, and the coroutine start of the scene camera's timeout; the timeout
    // iterator itself is stepped by hand.
    public sealed class CameraTriggerAndAssignerTests
    {
        private static string[] Members(CinemachineTargetGroup group) =>
            group.m_Targets.Select(target => target.target.name).ToArray();

        // ---- CinemachineSceneTriggerCamera ----

        private static CinemachineSceneTriggerCamera NewSceneTrigger(Owned owned, int cameraPriority, out CinemachineVirtualCamera camera)
        {
            camera = AddCinemachineCamera<CinemachineVirtualCamera>(owned.Host("Lucid scene camera", false));
            camera.Priority = cameraPriority;
            var trigger = AddCharacterTrigger<CinemachineSceneTriggerCamera>(owned, "Lucid scene trigger camera");
            Set(trigger, "m_virtualCammera", camera);
            return trigger;
        }

        // The constructor 0x7245A0 stores -1.0f at +0x5C and 100 at +0x64; the other authored fields stay zero. The
        // disabled priority is the constant -1.
        [Test]
        public void ASceneTriggerCameraStartsWithoutATimeoutAndWithPriorityOneHundred()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = NewSceneTrigger(owned, 5, out _);

                Assert.That(Get<float>(trigger, "m_exitAfterSeconds"), Is.EqualTo(-1f));
                Assert.That(Get<int>(trigger, "m_onTriggerCameraPriority"), Is.EqualTo(100));
                Assert.That(Get<bool>(trigger, "m_destroyOnExit"), Is.False);
                Assert.That(Get<bool>(trigger, "m_lookAtCharacter"), Is.False);
                Assert.That(Field(typeof(CinemachineSceneTriggerCamera), "DisabledPriority").GetRawConstantValue(), Is.EqualTo(-1));
            });
        }

        // DisableCamera 0x7241C0 and OnLevelActivated 0x7242C0 both store the disabled priority (mov [+0x3c], -1) when the
        // camera reference is alive; a destroyed or missing camera is skipped by the op_Equality test.
        [Test]
        public void DisablingTheSceneCameraDropsItsPriorityToMinusOne()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = NewSceneTrigger(owned, 50, out CinemachineVirtualCamera camera);

                Call(trigger, "OnLevelActivated", (object)null);
                Assert.That(camera.Priority, Is.EqualTo(-1), "level activated");

                camera.Priority = 50;
                Call(trigger, "DisableCamera");
                Assert.That(camera.Priority, Is.EqualTo(-1), "disabled");

                Set(trigger, "m_virtualCammera", null);
                Assert.DoesNotThrow(() => Call(trigger, "DisableCamera"), "no camera");
            });
        }

        // DoTriggerEnter 0x724330 raises the camera to the authored trigger priority, looks at the character only when
        // asked to, and (with the default exit-after of -1) starts no timeout.
        [Test]
        public void EnteringRaisesTheCameraAndOptionallyLooksAtTheCharacter()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = NewSceneTrigger(owned, -1, out CinemachineVirtualCamera camera);
                Set(trigger, "m_onTriggerCameraPriority", 42);

                Call(trigger, "DoTriggerEnter", character);
                Assert.That(camera.Priority, Is.EqualTo(42));
                Assert.That(camera.LookAt, Is.Null, "no look-at without the flag");

                camera.Priority = -1;
                Set(trigger, "m_lookAtCharacter", true);
                Call(trigger, "DoTriggerEnter", character);
                Assert.That(camera.Priority, Is.EqualTo(42));
                Assert.That(camera.LookAt, Is.SameAs(character.transform));
            });
        }

        // DoTriggerEnter starts the timeout coroutine unless the exit-after is exactly -1.0f (ucomiss against the constant at
        // 0x29a6df0 with jne/jp, 0x72437d..0x724386), so zero, other negatives and NaN all start it. The trigger's host
        // is inactive here, so starting it is reported by Unity.
        [Test]
        public void EnteringStartsTheTimeoutUnlessTheExitAfterIsExactlyMinusOne()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = NewSceneTrigger(owned, -1, out _);
                Set(trigger, "m_exitAfterSeconds", -1f);
                Call(trigger, "DoTriggerEnter", character);
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();

                foreach (float exitAfter in new[] { 0f, -2f, float.NaN })
                {
                    Set(trigger, "m_exitAfterSeconds", exitAfter);
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                        new System.Text.RegularExpressions.Regex("Coroutine couldn't be started"));

                    Call(trigger, "DoTriggerEnter", character);

                    UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                }
            });
        }

        // DoTriggerExit 0x724470 only acts when the exit-after is exactly -1 (ucomiss against the constant -1.0f at
        // 0x29a6df0, with the unordered result also skipping the exit). A timeout leaves the exit to the timeout.
        [Test]
        public void LeavingDropsTheCameraOnlyWhenNoTimeoutIsConfigured()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = NewSceneTrigger(owned, 42, out CinemachineVirtualCamera camera);
                Set(trigger, "m_exitAfterSeconds", 2f);

                Call(trigger, "DoTriggerExit", character);
                Assert.That(camera.Priority, Is.EqualTo(42), "a timeout configured");

                Set(trigger, "m_exitAfterSeconds", float.NaN);
                Call(trigger, "DoTriggerExit", character);
                Assert.That(camera.Priority, Is.EqualTo(42), "an unordered timeout");

                Set(trigger, "m_exitAfterSeconds", -1f);
                Call(trigger, "DoTriggerExit", character);
                Assert.That(camera.Priority, Is.EqualTo(-1), "no timeout");
            });
        }

        // ExitCamera 0x724500 disables a camera that is not yet disabled and then destroys the trigger's object if asked
        // to; a camera already at -1 is left alone and nothing is destroyed.
        [Test]
        public void ExitingAnAlreadyDisabledCameraDestroysNothing()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = NewSceneTrigger(owned, -1, out CinemachineVirtualCamera camera);
                Set(trigger, "m_destroyOnExit", true);

                trigger.ExitCamera();

                Assert.That(camera.Priority, Is.EqualTo(-1));
                Assert.That(trigger != null, Is.True, "the trigger's object is kept");
            });
        }

        [Test]
        public void ExitingAnActiveCameraDisablesIt()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = NewSceneTrigger(owned, 42, out CinemachineVirtualCamera camera);

                trigger.ExitCamera();

                Assert.That(camera.Priority, Is.EqualTo(-1));
            });
        }

        // The timeout iterator (d__13, MoveNext 0x724740) yields one fixed-seconds wait on the Camera time category
        // (edi = 0x341bd67c) for the authored seconds, and then exits the camera.
        [Test]
        public void TheTimeoutWaitsForTheAuthoredSecondsOnTheCameraCategoryAndThenExits()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = NewSceneTrigger(owned, 42, out CinemachineVirtualCamera camera);
                Set(trigger, "m_exitAfterSeconds", 2.5f);
                var timeout = (IEnumerator)Call(trigger, "OnTriggerTimeout");

                Assert.That(timeout.MoveNext(), Is.True);
                (float seconds, object category) = WaitArguments(timeout.Current);
                Assert.That(seconds, Is.EqualTo(2.5f));
                Assert.That(category, Is.EqualTo(TimeCategory.Camera));
                Assert.That(camera.Priority, Is.EqualTo(42), "still waiting");

                Assert.That(timeout.MoveNext(), Is.False);
                Assert.That(camera.Priority, Is.EqualTo(-1), "exited after the wait");
            });
        }

        // ---- CinemachineYUpOverrideTrigger ----

        private sealed class YUpScene
        {
            internal CharacterManager Characters;
            internal Character Character;
            internal CameraYAxisProxyManager YAxis;
            internal CinemachineYUpOverrideTrigger Trigger;
        }

        private const CameraYOverrideLayer OverrideLayer = CameraYOverrideLayer.Override;

        private static void WithYUpTrigger(Action<Owned, YUpScene> body) =>
            WithManager((owned, characters, character) =>
            {
                Rig rig = NewRig(owned, false, true);
                var scene = new YUpScene { Characters = characters, Character = character, YAxis = RegisterYAxisManager(owned, rig.Brain) };
                scene.Trigger = AddCharacterTrigger<CinemachineYUpOverrideTrigger>(owned, "Lucid y up override trigger");
                Set(scene.Trigger, "m_targetData", new CameraYAxisProxyManager.TargetData { Layer = OverrideLayer });
                body(owned, scene);
            });

        private static bool LayerHasOverride(YUpScene scene) =>
            Get<StackableData>(scene.YAxis, "m_layerStacks").HasData((int)OverrideLayer);

        private static StackableDataHandle Handle(YUpScene scene) => Get<StackableDataHandle>(scene.Trigger, "m_overrideHandle");

        // The constructor 0x725BD0 sets the remove-on-disable flag (+0x58 = 1).
        [Test]
        public void AYUpOverrideTriggerRemovesItsOverrideOnDisableByDefault()
        {
            WithYUpTrigger((owned, scene) =>
            {
                Assert.That(Get<bool>(scene.Trigger, "m_removeOnDisable"), Is.True);
                Assert.That(Handle(scene), Is.Null);
            });
        }

        // DoTriggerEnter 0x7259E0 adds the authored target data to the Y axis proxy manager and keeps the handle;
        // DoTriggerExit 0x725B90 removes that override again but keeps the stale handle (RemoveCameraYUpOverride
        // 0x725AD0 never clears it).
        [Test]
        public void EnteringAddsTheYUpOverrideAndLeavingRemovesItButKeepsTheHandle()
        {
            WithYUpTrigger((owned, scene) =>
            {
                Call(scene.Trigger, "DoTriggerEnter", scene.Character);

                Assert.That(Handle(scene), Is.Not.Null);
                Assert.That(LayerHasOverride(scene), Is.True, "entered");

                StackableDataHandle handle = Handle(scene);
                Call(scene.Trigger, "DoTriggerExit", scene.Character);

                Assert.That(LayerHasOverride(scene), Is.False, "left");
                Assert.That(Handle(scene), Is.SameAs(handle));
            });
        }

        // Entering with a handle already held first removes that override (0x725aa0 call), so the old override does not
        // outlive the new one: after one exit nothing is left.
        [Test]
        public void EnteringAgainReplacesTheEarlierOverride()
        {
            WithYUpTrigger((owned, scene) =>
            {
                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                StackableDataHandle first = Handle(scene);

                Call(scene.Trigger, "DoTriggerEnter", scene.Character);

                Assert.That(Handle(scene), Is.Not.SameAs(first));
                Assert.That(LayerHasOverride(scene), Is.True);
                Call(scene.Trigger, "DoTriggerExit", scene.Character);
                Assert.That(LayerHasOverride(scene), Is.False, "the first override was removed on the second entry");
            });
        }

        // OnDisable 0x725BB0 removes the override only while the flag is set.
        [Test]
        public void DisablingRemovesTheYUpOverrideOnlyWhenTheFlagIsSet()
        {
            WithYUpTrigger((owned, scene) =>
            {
                Set(scene.Trigger, "m_removeOnDisable", false);
                Call(scene.Trigger, "DoTriggerEnter", scene.Character);

                Call(scene.Trigger, "OnDisable");
                Assert.That(LayerHasOverride(scene), Is.True, "flag off keeps the override");

                Set(scene.Trigger, "m_removeOnDisable", true);
                Call(scene.Trigger, "OnDisable");
                Assert.That(LayerHasOverride(scene), Is.False, "flag on removes it");
            });
        }

        // GetSystemSafe answers null while no Y axis proxy manager is registered (op_Equality at 0x725a81 and 0x725b65),
        // so entering installs nothing and leaving or disabling does nothing.
        [Test]
        public void WithoutAYAxisProxyManagerTheYUpTriggerDoesNothing()
        {
            WithManager((owned, characters, character) =>
            {
                owned.LeaseRow<CameraYAxisProxyManager>();
                var trigger = AddCharacterTrigger<CinemachineYUpOverrideTrigger>(owned, "Lucid y up override trigger");
                Set(trigger, "m_targetData", new CameraYAxisProxyManager.TargetData { Layer = OverrideLayer });

                Assert.DoesNotThrow(() => Call(trigger, "DoTriggerEnter", character));
                Assert.That(Get<StackableDataHandle>(trigger, "m_overrideHandle"), Is.Null);
                Assert.DoesNotThrow(() => Call(trigger, "DoTriggerExit", character));
                Assert.DoesNotThrow(() => Call(trigger, "OnDisable"));
            });
        }

        // ---- CinemachineRegisterForGimbalLock ----

        // Start 0x723C90 (iterator d__1, MoveNext 0x723DA0) waits until the Y axis proxy manager is valid and then
        // registers the authored virtual camera with it.
        [Test]
        public void TheGimbalLockCameraRegistersOnceTheYAxisProxyManagerIsValid()
        {
            WithManager((owned, characters, character) =>
            {
                Rig rig = NewRig(owned, false, true);
                CameraYAxisProxyManager yAxis = RegisterYAxisManager(owned, rig.Brain);
                var camera = owned.Host("Lucid gimbal camera", false).AddComponent<CinemachineMixingCamera>();
                var gimbal = owned.Host("Lucid gimbal lock", false).AddComponent<CinemachineRegisterForGimbalLock>();
                Set(gimbal, "m_virtualCamera", camera);
                var start = (IEnumerator)Call(gimbal, "Start");

                Assert.That(start.MoveNext(), Is.True);
                Assert.That(((WaitUntil)start.Current).keepWaiting, Is.False, "the manager is valid");
                Assert.That(Get<HashSet<CinemachineVirtualCameraBase>>(yAxis, "m_gimbalLockCameras"), Is.Empty);

                Assert.That(start.MoveNext(), Is.False);
                Assert.That(Get<HashSet<CinemachineVirtualCameraBase>>(yAxis, "m_gimbalLockCameras"), Is.EquivalentTo(new[] { camera }));
            });
        }

        [Test]
        public void TheGimbalLockCameraKeepsWaitingWhileNoYAxisProxyManagerIsRegistered()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CameraYAxisProxyManager>();
                var gimbal = owned.Host("Lucid gimbal lock", false).AddComponent<CinemachineRegisterForGimbalLock>();
                var start = (IEnumerator)Call(gimbal, "Start");

                Assert.That(start.MoveNext(), Is.True);
                Assert.That(((WaitUntil)start.Current).keepWaiting, Is.True);
            });
        }

        // ---- CinemachineTargetGroupAssigner_CameraProxy ----

        private sealed class ProxyGroupScene
        {
            internal Rig Rig;
            internal CameraProxyTarget Proxy;
            internal CinemachineTargetGroup Group;
            internal CinemachineTargetGroupAssigner_CameraProxy Assigner;
        }

        private static void WithProxyAssigner(Action<Owned, ProxyGroupScene> body) =>
            Owned.Run(owned =>
            {
                owned.LeaseRow<CharacterManager>();
                var scene = new ProxyGroupScene { Rig = NewRig(owned, true, true) };
                scene.Proxy = EnemyAbilityTestSupport.AddTimeScaled<CameraProxyTarget>(owned.Host("Lucid assigner proxy target", false));
                Set(scene.Rig.Manager, "m_proxyTarget", scene.Proxy);
                GameObject host = owned.Host("Lucid proxy target group", false);
                scene.Group = host.AddComponent<CinemachineTargetGroup>();
                scene.Assigner = host.AddComponent<CinemachineTargetGroupAssigner_CameraProxy>();
                Set(scene.Assigner, "m_targetGroup", scene.Group);
                Set(scene.Assigner, "m_weight", 2f);
                Set(scene.Assigner, "m_radius", 3f);
                body(owned, scene);
            });

        private static string[] StartupHandlers(ProxyGroupScene scene) =>
            HandlerNames(Get<Delegate>(Get<SystemRef<CinemachineCameraManager>>(scene.Assigner, "m_cameraManagerRef"), "OnSystemStartup"));

        // OnEnable 0x725300 subscribes to the manager's startup and, for a live manager, adds the proxy target's
        // transform with the authored weight and radius. OnDisable 0x725440 removes it and the subscription again.
        [Test]
        public void TheCameraProxyJoinsTheTargetGroupWhileEnabled()
        {
            WithProxyAssigner((owned, scene) =>
            {
                Call(scene.Assigner, "OnEnable");

                Assert.That(Members(scene.Group), Is.EqualTo(new[] { scene.Proxy.name }));
                CinemachineTargetGroup.Target joined = scene.Group.m_Targets[0];
                Assert.That((joined.target, joined.weight, joined.radius), Is.EqualTo((scene.Proxy.transform, 2f, 3f)));
                Assert.That(StartupHandlers(scene), Is.EqualTo(new[] { "OnCameraManagerValid" }));

                Call(scene.Assigner, "OnDisable");

                Assert.That(scene.Group.m_Targets, Is.Empty);
                Assert.That(StartupHandlers(scene), Is.Empty);
            });
        }

        // OnCameraManagerValid 0x725400 adds the proxy target of the manager it is given.
        [Test]
        public void TheStartupCallbackAddsTheManagersProxyTarget()
        {
            WithProxyAssigner((owned, scene) =>
            {
                Call(scene.Assigner, "OnCameraManagerValid", scene.Rig.Manager);

                Assert.That(Members(scene.Group), Is.EqualTo(new[] { scene.Proxy.name }));
            });
        }

        // The removal tests the proxy target with op_Inequality (0x7254f9), so a manager without one leaves the group as
        // it is.
        [Test]
        public void DisablingWithoutAProxyTargetRemovesNothing()
        {
            WithProxyAssigner((owned, scene) =>
            {
                Transform other = owned.Host("Lucid other member", false).transform;
                scene.Group.AddMember(other, 1f, 1f);
                Set(scene.Rig.Manager, "m_proxyTarget", null);

                Assert.DoesNotThrow(() => Call(scene.Assigner, "OnDisable"));

                Assert.That(Members(scene.Group), Is.EqualTo(new[] { other.name }));
            });
        }

        // ---- CinemachineTargetGroupAssigner_PlayerCharacter ----

        private sealed class PlayerGroupScene
        {
            internal CharacterManager Characters;
            internal Character Character;
            internal SmoothedTransformProxy VisualProxy;
            internal CinemachineTargetGroup Group;
            internal CinemachineTargetGroupAssigner_PlayerCharacter Assigner;
        }

        private static void WithPlayerAssigner(Action<Owned, PlayerGroupScene> body) =>
            WithManager((owned, characters, character) =>
            {
                var scene = new PlayerGroupScene { Characters = characters, Character = character };
                scene.VisualProxy = owned.Host("Lucid character visual proxy", false).AddComponent<SmoothedTransformProxy>();
                SetActorProperty(character, "VisualProxy", scene.VisualProxy);
                GameObject host = owned.Host("Lucid player target group", false);
                scene.Group = host.AddComponent<CinemachineTargetGroup>();
                scene.Assigner = host.AddComponent<CinemachineTargetGroupAssigner_PlayerCharacter>();
                Set(scene.Assigner, "m_targetGroup", scene.Group);
                Set(scene.Assigner, "m_weight", 2f);
                Set(scene.Assigner, "m_radius", 3f);
                body(owned, scene);
            });

        private static Delegate[] OwnedBy(Delegate handlers, object owner) =>
            handlers == null ? new Delegate[0] : handlers.GetInvocationList().Where(handler => ReferenceEquals(handler.Target, owner)).ToArray();

        // Start 0x725610 takes the character manager reference and, through the valid-callback 0x725700, registers
        // OnCharacterChange (running it at once for a valid current character, which adds that character's visual
        // proxy) and the pre-close handler. OnCharacterPreClose 0x725850 removes the current character's visual proxy
        // and OnDestroy 0x7258B0 releases both registrations.
        [Test]
        public void ThePlayerCharactersVisualProxyJoinsAndLeavesTheTargetGroup()
        {
            WithPlayerAssigner((owned, scene) =>
            {
                SetCurrent(scene.Characters, scene.Character, true);

                Call(scene.Assigner, "Start");

                Assert.That(Members(scene.Group), Is.EqualTo(new[] { scene.VisualProxy.name }));
                CinemachineTargetGroup.Target joined = scene.Group.m_Targets[0];
                Assert.That((joined.target, joined.weight, joined.radius), Is.EqualTo((scene.VisualProxy.transform, 2f, 3f)));
                Assert.That(OwnedBy(Get<Delegate>(scene.Characters, "m_onCharacterChange"), scene.Assigner).Select(handler => handler.Method.Name),
                    Is.EqualTo(new[] { "OnCharacterChange" }));
                Assert.That(OwnedBy(Get<Delegate>(scene.Characters, "OnCharacterPreClose"), scene.Assigner).Select(handler => handler.Method.Name),
                    Is.EqualTo(new[] { "OnCharacterPreClose" }));

                Call(scene.Assigner, "OnCharacterPreClose");
                Assert.That(scene.Group.m_Targets, Is.Empty);

                Call(scene.Assigner, "OnDestroy");
                Assert.That(OwnedBy(Get<Delegate>(scene.Characters, "m_onCharacterChange"), scene.Assigner), Is.Empty);
                Assert.That(OwnedBy(Get<Delegate>(scene.Characters, "OnCharacterPreClose"), scene.Assigner), Is.Empty);
            });
        }

        // OnCharacterChange 0x7257D0 and SetCharacter 0x725810 both add the given character's visual proxy; with no valid
        // current character Start adds nothing.
        [Test]
        public void ACharacterChangeAddsTheNewCharactersVisualProxy()
        {
            WithPlayerAssigner((owned, scene) =>
            {
                Call(scene.Assigner, "Start");
                Assert.That(scene.Group.m_Targets, Is.Empty, "no valid current character");

                Call(scene.Assigner, "OnCharacterChange", scene.Character);
                Assert.That(Members(scene.Group), Is.EqualTo(new[] { scene.VisualProxy.name }));

                scene.Group.RemoveMember(scene.VisualProxy.transform);
                Call(scene.Assigner, "SetCharacter", scene.Character);
                Assert.That(Members(scene.Group), Is.EqualTo(new[] { scene.VisualProxy.name }));

                Call(scene.Assigner, "OnDestroy");
            });
        }

        // The pre-close removal reads the manager's current character without testing its validity flag (the +0x38 load
        // at 0x725888), so an invalid current character is still removed.
        [Test]
        public void ThePreCloseRemovalIgnoresTheCharacterValidityFlag()
        {
            WithPlayerAssigner((owned, scene) =>
            {
                SetCurrent(scene.Characters, scene.Character, true);
                Call(scene.Assigner, "Start");
                SetCurrent(scene.Characters, scene.Character, false);

                Call(scene.Assigner, "OnCharacterPreClose");

                Assert.That(scene.Group.m_Targets, Is.Empty);
                Call(scene.Assigner, "OnDestroy");
            });
        }

        // ---- FTUECameraSection ----

        private static CinemachineVirtualCameraBase RegisterDefaultCamera(Rig rig, CinemachineVirtualCameraBase camera)
        {
            rig.Manager.RegisterCameraByType(new[]
            {
                new CinemachineCameraManager.VirtualCameraByType { Type = CameraType.Default, VirtualCamera = camera }
            });
            return camera;
        }

        // ToggleRequiredSettings 0x6C43C0 reads the Default camera, switches its CinemachineCameraResetYRecenter component
        // (Behaviour.set_enabled) and, for a free-look camera, its transition's inherit-position flag (the byte at
        // +0xF4 of the CinemachineFreeLook).
        [Test]
        public void ToggleRequiredSettingsSwitchesTheDefaultCamerasRecenteringAndInheritedPosition()
        {
            WithManager((owned, characters, character) =>
            {
                Rig rig = NewRig(owned, false, true);
                GameObject host = owned.Host("Lucid default free look", false);
                var freeLook = AddCinemachineCamera<CinemachineFreeLook>(host);
                var recenter = host.AddComponent<CinemachineCameraResetYRecenter>();
                RegisterDefaultCamera(rig, freeLook);
                var section = AddCharacterTrigger<FTUECameraSection>(owned, "Lucid ftue camera section");

                section.ToggleRequiredSettings(false);
                Assert.That((recenter.enabled, freeLook.m_Transitions.m_InheritPosition), Is.EqualTo((false, false)), "off");

                section.ToggleRequiredSettings(true);
                Assert.That((recenter.enabled, freeLook.m_Transitions.m_InheritPosition), Is.EqualTo((true, true)), "on");
            });
        }

        // The two switches are independent: a free-look camera without the recenter component only changes its
        // transition (TryGetComponent fails at 0x6c444a), and another kind of camera only its recenter component (the
        // type test at 0x6c4487).
        [Test]
        public void ToggleRequiredSettingsHandlesCamerasWithOnlyOneOfTheTwoParts()
        {
            WithManager((owned, characters, character) =>
            {
                Rig rig = NewRig(owned, false, true);
                var freeLook = AddCinemachineCamera<CinemachineFreeLook>(owned.Host("Lucid bare free look", false));
                RegisterDefaultCamera(rig, freeLook);
                var section = AddCharacterTrigger<FTUECameraSection>(owned, "Lucid ftue camera section");

                section.ToggleRequiredSettings(true);
                Assert.That(freeLook.m_Transitions.m_InheritPosition, Is.True);
                section.ToggleRequiredSettings(false);
                Assert.That(freeLook.m_Transitions.m_InheritPosition, Is.False);
            });
        }

        [Test]
        public void ToggleRequiredSettingsSwitchesOnlyTheRecenterComponentOfANonFreeLookCamera()
        {
            WithManager((owned, characters, character) =>
            {
                Rig rig = NewRig(owned, false, true);
                GameObject host = owned.Host("Lucid default mixing camera", false);
                var mixing = host.AddComponent<CinemachineMixingCamera>();
                var recenter = host.AddComponent<CinemachineCameraResetYRecenter>();
                RegisterDefaultCamera(rig, mixing);
                var section = AddCharacterTrigger<FTUECameraSection>(owned, "Lucid ftue camera section");

                section.ToggleRequiredSettings(false);
                Assert.That(recenter.enabled, Is.False);
                section.ToggleRequiredSettings(true);
                Assert.That(recenter.enabled, Is.True);
            });
        }

        // ---- SnapCameraToCharacterHeadingTrigger ----

        private sealed class SnapScene
        {
            internal CharacterManager Characters;
            internal Character Character;
            internal Rig Rig;
            internal CinemachineRecenterHeadingExtension_Basic Extension;
            internal SnapCameraToCharacterHeadingTrigger Trigger;
        }

        // The manager's snap request reaches every extension in its snap lookup; a heading extension snaps (sets its
        // flag) only for a valid current character.
        private static void WithSnapTrigger(Action<Owned, SnapScene> body) =>
            WithManager((owned, characters, character) =>
            {
                var scene = new SnapScene { Characters = characters, Character = character, Rig = NewRig(owned, false, true) };
                GameObject host = owned.Host("Lucid snap camera", false);
                var camera = host.AddComponent<CinemachineMixingCamera>();
                scene.Extension = host.AddComponent<CinemachineRecenterHeadingExtension_Basic>();
                scene.Rig.Manager.CamerasWithSnapLookup.Add(camera, scene.Extension);
                SetCurrent(characters, character, true);
                scene.Trigger = AddCharacterTrigger<SnapCameraToCharacterHeadingTrigger>(owned, "Lucid snap trigger");
                Set(scene.Trigger, "m_snapDelaySeconds", 0f);
                body(owned, scene);
            });

        private static bool Snapped(SnapScene scene) => Get<bool>(scene.Extension, "m_snapToCharacter");

        // The constructor 0x5CB370 stores 0.2f at +0x4C and 1 at +0x51: a delayed snap on enter only.
        [Test]
        public void ASnapTriggerStartsWithTheTwoTenthsSecondDelayAndSnapsOnEnterOnly()
        {
            WithManager((owned, characters, character) =>
            {
                var trigger = AddCharacterTrigger<SnapCameraToCharacterHeadingTrigger>(owned, "Lucid snap trigger");

                Assert.That(Get<float>(trigger, "m_snapDelaySeconds"), Is.EqualTo(0.2f));
                Assert.That(Get<bool>(trigger, "m_triggerOnEnter"), Is.True);
                Assert.That(Get<bool>(trigger, "m_triggerOnExit"), Is.False);
                Assert.That(Get<bool>(trigger, "m_allowStickyControls"), Is.False);
                Assert.That(Get<Coroutine>(trigger, "m_delayRoutine"), Is.Null);
            });
        }

        // DoTriggerEnter 0x5CB180 and DoTriggerExit 0x5CB280 each snap only when their own flag is set.
        [Test]
        public void EachTriggerDirectionSnapsOnlyWhenItsFlagIsSet()
        {
            WithSnapTrigger((owned, scene) =>
            {
                Call(scene.Trigger, "DoTriggerExit", scene.Character);
                Assert.That(Snapped(scene), Is.False, "exit with the exit flag off");

                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                Assert.That(Snapped(scene), Is.True, "enter with the enter flag on");

                Set(scene.Extension, "m_snapToCharacter", false);
                Set(scene.Trigger, "m_triggerOnEnter", false);
                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                Assert.That(Snapped(scene), Is.False, "enter with the enter flag off");

                Set(scene.Trigger, "m_triggerOnExit", true);
                Call(scene.Trigger, "DoTriggerExit", scene.Character);
                Assert.That(Snapped(scene), Is.True, "exit with the exit flag on");
            });
        }

        // DoTrigger 0x5CB1B0 delays only for a positive time (ucomiss against 0; jbe at 0x5cb1f0 also takes the unordered
        // result), so zero, negative and NaN delays snap in the same call, without a coroutine.
        [Test]
        public void ADelayThatIsNotPositiveSnapsAtOnce()
        {
            WithSnapTrigger((owned, scene) =>
            {
                foreach (float delay in new[] { 0f, -1f, float.NaN })
                {
                    Set(scene.Extension, "m_snapToCharacter", false);
                    Set(scene.Trigger, "m_snapDelaySeconds", delay);

                    Call(scene.Trigger, "DoTriggerEnter", scene.Character);

                    Assert.That(Snapped(scene), Is.True, "delay " + delay);
                    Assert.That(Get<Coroutine>(scene.Trigger, "m_delayRoutine"), Is.Null, "delay " + delay);
                }
            });
        }

        // SnapToCharacterHeading 0x5CB2B0 returns when no camera manager is registered.
        [Test]
        public void WithoutACameraManagerNothingSnaps()
        {
            WithManager((owned, characters, character) =>
            {
                owned.LeaseRow<CinemachineCameraManager>();
                var trigger = AddCharacterTrigger<SnapCameraToCharacterHeadingTrigger>(owned, "Lucid snap trigger");
                Set(trigger, "m_snapDelaySeconds", 0f);

                Assert.DoesNotThrow(() => Call(trigger, "DoTriggerEnter", character));
            });
        }

        // ---- SequencedCamera ----

        private static readonly Type TriggerParameter = typeof(SequencedCamera).GetNestedType("TriggerParameter", BindingFlags.NonPublic);

        private static CameraTypeManipulator NewManipulator(Owned owned)
        {
            var manipulator = owned.Host("Lucid camera type manipulator", false).AddComponent<CameraTypeManipulator>();
            Set(manipulator, "m_cameraType", CameraType.Rail);
            Call(manipulator, "Awake");
            return manipulator;
        }

        private static SequencedCamera NewSequencedCamera(Owned owned, CameraTypeManipulator manipulator, int triggerParameter)
        {
            var effect = owned.Host("Lucid sequenced camera", false).AddComponent<SequencedCamera>();
            Set(effect, "m_cameraTypeManipulator", manipulator);
            Set(effect, "m_triggerParameter", Enum.ToObject(TriggerParameter, triggerParameter));
            return effect;
        }

        // The nested enum is private with Start = 0 and End = 1 (0x02000612).
        [Test]
        public void TheTriggerParameterHasStartAndEndAndTheEffectStartsInTheEffectsCategory()
        {
            Assert.That(TriggerParameter.IsEnum, Is.True);
            Assert.That(Enum.GetNames(TriggerParameter), Is.EqualTo(new[] { "Start", "End" }));
            Assert.That(Enum.GetValues(TriggerParameter).Cast<object>().Select(Convert.ToInt32), Is.EqualTo(new[] { 0, 1 }));
            // The constructor 0x060020a6 inlines the base initialiser for the Effects time category (-2001650353).
            Owned.Run(owned => Assert.That((int)Get<TimeCategory>(NewSequencedCamera(owned, null, 0), "m_timeCategory"), Is.EqualTo(-2001650353)));
        }

        // OnEffectTriggered 0x568590 applies the camera type for Start (a tail jump to ApplyCameraType with the stored
        // completion callback) and removes it for End; both complete through the manipulator.
        [Test]
        public void StartAppliesTheCameraTypeAndEndRemovesIt()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, true, true);
                CinemachineMixingCamera rail = AddCamera(owned, rig, CameraType.Rail, 20);
                CameraTypeManipulator manipulator = NewManipulator(owned);
                SequencedCamera start = NewSequencedCamera(owned, manipulator, 0);
                SequencedCamera end = NewSequencedCamera(owned, manipulator, 1);
                IEffectData data = null;
                int completed = 0;
                Assert.That(rail.Priority, Is.EqualTo(-1), "registered with nothing on the camera stack");

                start.PlayEffect(in data, () => completed++);
                Assert.That(rail.Priority, Is.EqualTo(20), "applied");
                Assert.That(completed, Is.EqualTo(1));

                end.PlayEffect(in data, () => completed++);
                Assert.That(rail.Priority, Is.EqualTo(-1), "removed");
                Assert.That(completed, Is.EqualTo(2));
            });
        }

        // The switch's default arm (0x5685cc) throws ArgumentOutOfRangeException for any other value.
        [Test]
        public void AnUnknownTriggerParameterThrows()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, true, true);
                SequencedCamera effect = NewSequencedCamera(owned, NewManipulator(owned), 2);
                IEffectData data = null;
                int completed = 0;

                Assert.Throws<ArgumentOutOfRangeException>(() => effect.PlayEffect(in data, () => completed++));

                Assert.That(completed, Is.Zero);
            });
        }
    }
}
