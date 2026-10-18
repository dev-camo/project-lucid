using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using ProjectLucid.Tests.WorldTriggersShared;
using static ProjectLucid.Tests.WorldMemberAccess;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // HardlightProject.TransformFaceCamera and TransformFacePlayer (TypeDefs 0x0200080e/f,
    // x86-64 0x5FD780..0x5FDF47), TransformAssignEventTrigger (0x02000772, 0x5D45B0..0x5D4AA2),
    // RotationGravity (0x02000a7d, 0x6C9990..0x6C9A6C), SnapToAxis (0x02000a2c, 0x6B5F20) and
    // VanishingPlatformEffects (0x02000ad4, 0x6E9C50..0x6E9CAC). Managers are uninitialised
    // objects; a registry row a body reads is owned for the case (absent or idle, released
    // after). Messages are invoked directly because EditMode sends none.
    public sealed class OriginalTransformFacingAndTriggerTests
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        private static Delegate[] Handlers(object target, string field) =>
            ((Delegate)Get(target, field))?.GetInvocationList() ?? Array.Empty<Delegate>();

        private static string[] HandlerNames(object target, string field) =>
            Handlers(target, field).Select(handler => handler.Method.Name).ToArray();

        [Test]
        public void DeclarationsKeepTheOriginalFlagsMembersAndAttributes()
        {
            Assert.That((int)typeof(TransformFaceCamera).Attributes, Is.EqualTo(0x00100001));
            Assert.That(Fields(typeof(TransformFaceCamera)), Is.EqualTo(new[]
                { "m_mainCameraTransform:Transform:0001", "m_cameraManagerRef:SystemRef`1:0001" }));
            Assert.That(Methods(typeof(TransformFaceCamera)), Is.EqualTo(new[]
            {
                "OnEnable:0081", "OnDisable:0081", "OnCameraInitialised:0081", "SetMainCamera:0081", "OnCameraManagerShutdown:0081",
                "RemoveCameraShutdownReference:0081", "LateUpdate:0081", ".ctor:1886"
            }));
            Assert.That(AttributeRow(typeof(TransformFacePlayer)), Is.EqualTo("Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)"));
            Assert.That(Fields(typeof(TransformFacePlayer)), Is.EqualTo(new[]
                { "m_lookAtTransform:Transform:0001", "m_characterManagerRef:SystemRef`1:0001", "m_characterManager:CharacterManager:0001" }));
            Assert.That(Methods(typeof(TransformFacePlayer)), Is.EqualTo(new[]
                { "OnEnable:0081", "RegisterCharacterManager:0081", "SetTarget:0081", "LateUpdate:0081", "OnDisable:0081", ".ctor:1886" }));

            Assert.That(Fields(typeof(TransformAssignEventTrigger)), Is.EqualTo(new[]
            {
                "m_requiredTag:String:0001", "m_onTriggerEnter:UnityEvent`1:0001", "m_onTriggerExit:UnityEvent`1:0001",
                "m_setNullOnExit:Boolean:0001", "m_levelManagerRef:SystemRef`1:0021", "m_hasTriggered:Boolean:0001"
            }));
            Assert.That(AttributeRow(typeof(TransformAssignEventTrigger).GetField("m_onTriggerEnter", All)),
                Is.EqualTo("SerializeField() TooltipAttribute(Colliding transform is passed as parameter.)"));
            Assert.That(AttributeRow(typeof(TransformAssignEventTrigger).GetField("m_onTriggerExit", All)),
                Is.EqualTo("TooltipAttribute(Colliding transform is passed as parameter unless 'set null on exit' is enabled.) SerializeField()"));
            Assert.That(Methods(typeof(TransformAssignEventTrigger)), Is.EqualTo(new[]
            {
                "get_SetNullOnExit:09c4", "Start:0081", "OnDestroy:0081", "OnTriggerEnter:0081", "OnTriggerExit:0081", "IsValidTag:0081",
                "GetTargetTransform:01c4", "OnLevelManagerValid:0081", "OnLevelActivated:0081", "InvokeOnTriggerExit:0081", ".ctor:1886"
            }));

            Assert.That(typeof(RotationGravity).BaseType, Is.EqualTo(typeof(GravityProvider)));
            Assert.That(AttributeRow(typeof(RotationGravity)), Is.EqualTo("Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)"));
            Assert.That(Fields(typeof(RotationGravity)), Is.EqualTo(new[] { "m_gravity:Vector3:0001" }));
            Assert.That(Methods(typeof(RotationGravity)), Is.EqualTo(new[]
                { "OnEnable:0081", "GetGravity:00c6", "Register:00c6", "Unregister:00c6", "UnregisterAll:00c4", ".ctor:1886" }));

            Assert.That((int)typeof(SnapToAxis).Attributes, Is.EqualTo(0x00102109));
            Assert.That(Fields(typeof(SnapToAxis)), Is.EqualTo(new[] { "SnapToX:Boolean:0006", "SnapToY:Boolean:0006", "SnapToZ:Boolean:0006" }));
            Assert.That(Methods(typeof(SnapToAxis)), Is.EqualTo(new[] { "Snap:0086" }));

            Assert.That(Fields(typeof(VanishingPlatformEffects)), Is.EqualTo(new[]
            {
                "m_platformStartAnimationAudio:HLAudioOneShotOnSource:0001", "m_platformLoopingAudio:HLAudioAmbientComponent:0001",
                "m_platformAudioWarning:HLAudioOneShotOnSource:0001"
            }));
            Assert.That(Methods(typeof(VanishingPlatformEffects)), Is.EqualTo(new[]
                { "Action_PlayPlatformAudio:0086", "Action_PlayPlatformWarning:0086", "Action_StopPlatformAudio:0086", ".ctor:1886" }));
        }

        // SnapToAxis.Snap 0x6B5F20: x and y blend on SnapToX/Y == 0 (pcmpeqb + blendvps keeps
        // current), z takes target when SnapToZ != 0. An enabled axis passes even NaN through.
        [Test]
        public void SnapTakesTheTargetOnExactlyTheEnabledAxes()
        {
            var current = new Vector3(1f, 2f, 3f);
            var target = new Vector3(4f, 5f, 6f);
            for (int mask = 0; mask < 8; mask++)
            {
                var snap = new SnapToAxis { SnapToX = (mask & 1) != 0, SnapToY = (mask & 2) != 0, SnapToZ = (mask & 4) != 0 };
                var expected = new Vector3((mask & 1) != 0 ? 4f : 1f, (mask & 2) != 0 ? 5f : 2f, (mask & 4) != 0 ? 6f : 3f);
                Assert.That(snap.Snap(current, target), Is.EqualTo(expected), "mask " + mask);
            }
            Vector3 nan = new Vector3(float.NaN, float.NaN, float.NaN);
            Assert.That(new SnapToAxis().Snap(current, nan), Is.EqualTo(current), "disabled axes ignore a NaN target");
            Vector3 taken = new SnapToAxis { SnapToX = true, SnapToY = true, SnapToZ = true }.Snap(current, nan);
            IsNaN(taken, "enabled axes take a NaN target");
        }

        // RotationGravity.OnEnable 0x6C9990 caches transform.rotation * Physics.gravity
        // (get_gravity 0x6c99ee, Quaternion * Vector3 0x6c9a03); GetGravity 0x6C9A20 returns the
        // cache for any position, and Register, Unregister and UnregisterAll are empty.
        [Test]
        public void RotationGravityCachesTheWorldRotatedGlobalGravityOnEnable()
        {
            using (var objects = new OwnedWorldObjects("lucid-rotation-gravity"))
            {
                Transform parent = objects.Create("parent", Vector3.zero, Quaternion.Euler(0f, 0f, 90f)).transform;
                var gravity = objects.Create("gravity", Vector3.zero, Quaternion.identity, parent).AddComponent<RotationGravity>();
                gravity.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                Assert.That(gravity.GetGravity(Vector3.zero), Is.EqualTo(Vector3.zero), "nothing before OnEnable");
                Call(gravity, "OnEnable");
                Vector3 expected = gravity.transform.rotation * Physics.gravity;
                AreClose(expected, gravity.GetGravity(new Vector3(5f, 6f, 7f)), "world rotation");
                Assert.That(Vector3.Distance(gravity.transform.localRotation * Physics.gravity, expected), Is.GreaterThan(1f), "the local rotation differs");

                gravity.transform.rotation = Quaternion.identity;
                AreClose(expected, gravity.GetGravity(Vector3.one), "cached until the next OnEnable");
                Assert.DoesNotThrow(() => gravity.Register(null));
                Assert.DoesNotThrow(() => gravity.Unregister(null));
                Assert.DoesNotThrow(() => gravity.RegisterSources(new GravitySource[] { null }));
                Assert.DoesNotThrow(() => Call(gravity, "OnDisable"));
                AreClose(expected, gravity.GetGravity(Vector3.zero), "OnDisable keeps the cache");
            }
        }

        // FaceCamera.OnCameraInitialised 0x5FD980 adds OnCameraManagerShutdown to the manager's
        // OnSystemShutdown event (0x5fda05) before testing MainCameraIsReady (+0xc0): when it is
        // not ready, SetMainCamera is combined into the OnMainCameraReady field (0x5fda76); when it
        // is, MainCamera (0x5fda18) is read at once. SetMainCamera 0x5FDAE0 enables, caches the
        // camera transform and removes itself from the field (0x5fdba7).
        [Test]
        public void FaceCameraWaitsForTheMainCameraAndUnsubscribesOnceSet()
        {
            using (var objects = new OwnedWorldObjects("lucid-face-camera"))
            {
                var face = objects.Create("face").AddComponent<TransformFaceCamera>();
                var manager = Raw<CinemachineCameraManager>();
                Action<Camera> prior = _ => { };
                manager.OnMainCameraReady = prior;
                Call(face, "OnCameraInitialised", manager);
                Assert.That(HandlerNames(manager, "OnSystemShutdown"), Is.EqualTo(new[] { "OnCameraManagerShutdown" }));
                Assert.That(Handlers(manager, "OnSystemShutdown")[0].Target, Is.SameAs(face));
                Assert.That(HandlerNames(manager, "OnMainCameraReady"), Is.EqualTo(new[] { prior.Method.Name, "SetMainCamera" }));

                Set(face, "m_cameraManagerRef", new SystemRef<CinemachineCameraManager>(ProcessManager.GetDefaultName<CinemachineCameraManager>(), manager));
                face.enabled = false;
                var camera = objects.Create("camera", new Vector3(10f, 0f, 0f), Quaternion.identity).AddComponent<Camera>();
                Call(face, "SetMainCamera", camera);
                Assert.That(face.enabled, Is.True);
                Assert.That(Get(face, "m_mainCameraTransform"), Is.SameAs(camera.transform));
                Assert.That(Handlers(manager, "OnMainCameraReady"), Is.EqualTo(new Delegate[] { prior }));

                Call(face, "LateUpdate");
                AreClose(Vector3.right, face.transform.forward, "looks at the camera");

                var ready = Raw<CinemachineCameraManager>();
                Set(ready, "<MainCameraIsReady>k__BackingField", true);
                var second = objects.Create("second").AddComponent<TransformFaceCamera>();
                Assert.Throws<NullReferenceException>(() => Call(second, "OnCameraInitialised", ready),
                    "a ready manager's MainCamera is read through its unset brain");
                Assert.That(HandlerNames(ready, "OnSystemShutdown"), Is.EqualTo(new[] { "OnCameraManagerShutdown" }), "subscribed first");
                Assert.That(Get(ready, "OnMainCameraReady"), Is.Null, "a ready camera is not waited for");
            }
        }

        // OnCameraManagerShutdown 0x5FDC10 disables (when this is not Unity-null, 0x5fdc54) and
        // calls RemoveCameraShutdownReference 0x5FD8B0, which returns for a null reference
        // (IsNull 0x5fd90b) and otherwise removes the handler from Get().OnSystemShutdown.
        // OnDisable 0x5FD8A0 is that removal alone.
        [Test]
        public void FaceCameraShutdownDisablesAndRemovesItsHandler()
        {
            using (var objects = new OwnedWorldObjects("lucid-face-camera-shutdown"))
            {
                var face = objects.Create("face").AddComponent<TransformFaceCamera>();
                var manager = Raw<CinemachineCameraManager>();
                Set(face, "m_cameraManagerRef", new SystemRef<CinemachineCameraManager>(ProcessManager.GetDefaultName<CinemachineCameraManager>(), manager));
                Call(face, "OnCameraInitialised", manager);
                Call(face, "OnCameraManagerShutdown");
                Assert.That(face.enabled, Is.False);
                Assert.That(Get(manager, "OnSystemShutdown"), Is.Null);

                Call(face, "OnCameraInitialised", manager);
                Call(face, "OnDisable");
                Assert.That(Get(manager, "OnSystemShutdown"), Is.Null);

                Set(face, "m_cameraManagerRef", new SystemRef<CinemachineCameraManager>(ProcessManager.GetDefaultName<CinemachineCameraManager>(), null));
                Assert.DoesNotThrow(() => Call(face, "OnDisable"), "a null reference is skipped");
            }
        }

        // FaceCamera.OnEnable 0x5FD780 stores GetSystemRef<CinemachineCameraManager>(null, true)
        // and waits on it (InvokeOnValid 0x5fd860, immediate for a registered manager), then
        // disables only when it is still null (0x5fd87a).
        [Test]
        public void FaceCameraEnableSubscribesToARegisteredManager()
        {
            var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CinemachineCameraManager>());
            var objects = new OwnedWorldObjects("lucid-face-camera-enable");
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                var manager = Raw<CinemachineCameraManager>();
                row.Register(manager);
                var face = objects.Create("face").AddComponent<TransformFaceCamera>();
                Call(face, "OnEnable");
                Assert.That(Get(face, "m_cameraManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<CinemachineCameraManager>()));
                Assert.That(face.enabled, Is.True);
                Assert.That(HandlerNames(manager, "OnSystemShutdown"), Is.EqualTo(new[] { "OnCameraManagerShutdown" }));
                Call(face, "OnDisable");
                Assert.That(Get(manager, "OnSystemShutdown"), Is.Null);
            }, objects.Dispose, row.Dispose);
        }

        // With no manager registered the reference stays null after InvokeOnValid, so OnEnable
        // disables (IsNull 0x5fd873, set_enabled(false) 0x5fd88d) and the deferred
        // OnCameraInitialised runs when a manager is registered later. GetSystemRef creates
        // the row, so it is admitted absent through LifecycleRow and released once idle.
        [Test]
        public void FaceCameraEnableDisablesUntilAManagerIsRegistered()
        {
            string name = ProcessManager.GetDefaultName<CinemachineCameraManager>();
            var row = new VerificationFixtureOwnership.LifecycleRow(name);
            var objects = new OwnedWorldObjects("lucid-face-camera-enable-missing");
            bool registered = false;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                var face = objects.Create("face").AddComponent<TransformFaceCamera>();
                Call(face, "OnEnable");
                Assert.That(face.enabled, Is.False, "no camera manager");
                var reference = (SystemRef<CinemachineCameraManager>)Get(face, "m_cameraManagerRef");
                Assert.That(reference, Is.SameAs(ProcessManager.GetSystemRef<CinemachineCameraManager>()));
                Assert.That(reference.IsNull(), Is.True);

                var manager = Raw<CinemachineCameraManager>();
                // Set first: a throwing deferred callback still leaves the system registered.
                registered = true;
                ProcessManager.RegisterSystem(manager, name);
                Assert.That(HandlerNames(manager, "OnSystemShutdown"), Is.EqualTo(new[] { "OnCameraManagerShutdown" }), "the deferred subscription ran");
                Assert.That(HandlerNames(manager, "OnMainCameraReady"), Is.EqualTo(new[] { "SetMainCamera" }), "the camera is not ready");
                Assert.That(face.enabled, Is.False, "re-enabled only by SetMainCamera");
                Call(face, "OnDisable");
                Assert.That(Get(manager, "OnSystemShutdown"), Is.Null);
            },
            () => { if (registered) ProcessManager.UnregisterSystem(name); },
            objects.Dispose, row.Dispose);
        }

        // FacePlayer.RegisterCharacterManager 0x5FDDB0 stores the manager and calls
        // RegisterOnCharacterChange(SetTarget, true) (edx 1, 0x5fde2b), so a valid character is
        // targeted at once; SetTarget 0x5FDE50 caches character.transform; OnDisable 0x5FDEB0
        // releases only for a non-null manager (test 0x5fdee9); LateUpdate 0x5FDE80 looks at it.
        [Test]
        public void FacePlayerTargetsTheCurrentCharacterAndReleasesOnDisable()
        {
            var islandRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>());
            var objects = new OwnedWorldObjects("lucid-face-player");
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                var face = objects.Create("face").AddComponent<TransformFacePlayer>();
                Assert.DoesNotThrow(() => Call(face, "OnDisable"), "no manager yet");

                var waiting = Raw<CharacterManager>();
                Call(face, "RegisterCharacterManager", waiting);
                Assert.That(Get(face, "m_characterManager"), Is.SameAs(waiting));
                Assert.That(HandlerNames(waiting, "m_onCharacterChange"), Is.EqualTo(new[] { "SetTarget" }));
                Assert.That(Get(face, "m_lookAtTransform"), Is.Null, "no valid character");
                Call(face, "OnDisable");
                Assert.That(Get(waiting, "m_onCharacterChange"), Is.Null);
                Assert.That(Get(face, "m_characterManager"), Is.SameAs(waiting), "the cached manager stays");

                GameObject host = objects.Create("character", new Vector3(0f, 0f, -10f), Quaternion.identity);
                host.SetActive(false);
                Character character = host.AddComponent<Character>();
                var valid = Raw<CharacterManager>();
                Set(valid, "<CharacterValid>k__BackingField", true);
                Set(valid, "m_currentCharacter", character);
                Call(face, "RegisterCharacterManager", valid);
                Assert.That(Get(face, "m_lookAtTransform"), Is.SameAs(character.transform));
                Assert.That(HandlerNames(valid, "m_onCharacterChange"), Is.EqualTo(new[] { "SetTarget" }));

                Call(face, "LateUpdate");
                AreClose(Vector3.back, face.transform.forward, "looks at the character");
            }, objects.Dispose, islandRow.Dispose);
        }

        // FacePlayer.OnEnable 0x5FDCC0 stores GetSystemRef<CharacterManager>(null, true) and
        // waits on it with RegisterCharacterManager (immediate for a registered manager).
        [Test]
        public void FacePlayerEnableRegistersWithARegisteredManager()
        {
            var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CharacterManager>());
            var objects = new OwnedWorldObjects("lucid-face-player-enable");
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                var manager = Raw<CharacterManager>();
                row.Register(manager);
                var face = objects.Create("face").AddComponent<TransformFacePlayer>();
                Call(face, "OnEnable");
                Assert.That(Get(face, "m_characterManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<CharacterManager>()));
                Assert.That(Get(face, "m_characterManager"), Is.SameAs(manager));
                Assert.That(HandlerNames(manager, "m_onCharacterChange"), Is.EqualTo(new[] { "SetTarget" }));
                Call(face, "OnDisable");
                Assert.That(Get(manager, "m_onCharacterChange"), Is.Null);
            }, objects.Dispose, row.Dispose);
        }

        private sealed class TriggerFixture : IDisposable
        {
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-transform-assign");
            public readonly List<Transform> Entered = new List<Transform>();
            public readonly List<Transform> Exited = new List<Transform>();
            public TransformAssignEventTrigger Trigger;

            // The constructor's GetSystemRef<LevelManager> row must already be owned.
            public TransformAssignEventTrigger Create() => Create<TransformAssignEventTrigger>();

            public T Create<T>() where T : TransformAssignEventTrigger
            {
                var trigger = Objects.Create("trigger").AddComponent<T>();
                Trigger = trigger;
                var enter = new UnityEvent<Transform>();
                enter.AddListener(Entered.Add);
                var exit = new UnityEvent<Transform>();
                exit.AddListener(Exited.Add);
                Set(Trigger, "m_onTriggerEnter", enter);
                Set(Trigger, "m_onTriggerExit", exit);
                return trigger;
            }

            public Collider Collider(string tag)
            {
                GameObject other = Objects.Create("other " + tag);
                other.tag = tag;
                return other.AddComponent<BoxCollider>();
            }

            public void Dispose() => Objects.Dispose();
        }

        // .ctor 0x5D4A00 stores "Player" and GetSystemRef<LevelManager>(null, true).
        // OnTriggerEnter 0x5D4700 and OnTriggerExit 0x5D47F0 read other.tag first and pass on
        // an empty required tag or an ordinal String.Equals (0x1BF3B90) match; enter sets
        // m_hasTriggered and sends GetTargetTransform (slot 5, other.transform); exit clears it
        // and sends null when SetNullOnExit (slot 4) holds, the transform otherwise.
        [Test]
        public void TriggerPassesTheMatchingCollidersTransformToEnterAndExit()
        {
            using (var levelRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<LevelManager>()))
            using (var fixture = new TriggerFixture())
            {
                TransformAssignEventTrigger trigger = fixture.Create();
                Assert.That(Get(trigger, "m_requiredTag"), Is.EqualTo("Player"));
                Assert.That(Get(trigger, "m_levelManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<LevelManager>()));
                Collider player = fixture.Collider("Player");
                Collider untagged = fixture.Collider("Untagged");

                Call(trigger, "OnTriggerEnter", untagged);
                Call(trigger, "OnTriggerExit", untagged);
                Assert.That(fixture.Entered, Is.Empty);
                Assert.That(fixture.Exited, Is.Empty);
                Assert.That(Get(trigger, "m_hasTriggered"), Is.False);

                Call(trigger, "OnTriggerEnter", player);
                Assert.That(fixture.Entered, Is.EqualTo(new[] { player.transform }));
                Assert.That(Get(trigger, "m_hasTriggered"), Is.True);
                Call(trigger, "OnTriggerExit", untagged);
                Assert.That(Get(trigger, "m_hasTriggered"), Is.True, "a mismatched exit changes nothing");
                Call(trigger, "OnTriggerExit", player);
                Assert.That(fixture.Exited, Is.EqualTo(new[] { player.transform }));
                Assert.That(Get(trigger, "m_hasTriggered"), Is.False);

                Set(trigger, "m_setNullOnExit", true);
                Call(trigger, "OnTriggerExit", player);
                Assert.That(fixture.Exited, Is.EqualTo(new[] { player.transform, null }));

                Set(trigger, "m_requiredTag", "player");
                Call(trigger, "OnTriggerEnter", player);
                Assert.That(fixture.Entered.Count, Is.EqualTo(1), "the tag match is case-sensitive");
                Set(trigger, "m_requiredTag", "");
                Call(trigger, "OnTriggerEnter", untagged);
                Set(trigger, "m_requiredTag", null);
                Call(trigger, "OnTriggerEnter", player);
                Assert.That(fixture.Entered, Is.EqualTo(new[] { player.transform, untagged.transform, player.transform }), "an empty tag takes any collider");
                Assert.That(Call(trigger, "GetTargetTransform", untagged), Is.SameAs(untagged.transform));
            }
        }

        // OnTriggerEnter sends GetTargetTransform through vtable slot 5 (call [rax+0x188] at
        // 0x5d4773); OnTriggerExit reads SetNullOnExit through slot 4 (0x5d483b) and calls slot 5
        // (0x5d4863) only when it is false. CameraProxyAssignTrigger (TypeDefIndex 603) and
        // CharacterProxyAssignTrigger (831) override both.
        [Test]
        public void TriggerDispatchesTheTargetAndNullOnExitVirtually()
        {
            using (var levelRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<LevelManager>()))
            using (var fixture = new TriggerFixture())
            {
                var trigger = fixture.Create<TransformAssignEventTriggerProbe>();
                trigger.Proxy = fixture.Objects.Create("proxy").transform;
                Collider player = fixture.Collider("Player");

                Call(trigger, "OnTriggerEnter", player);
                Assert.That(fixture.Entered, Is.EqualTo(new[] { trigger.Proxy }), "enter sends the override's transform");
                Assert.That(trigger.TargetRequests, Is.EqualTo(1));

                trigger.NullOnExit = true;
                Set(trigger, "m_setNullOnExit", false);
                Call(trigger, "OnTriggerExit", player);
                Assert.That(fixture.Exited, Is.EqualTo(new Transform[] { null }), "the overridden SetNullOnExit decides");
                Assert.That(trigger.TargetRequests, Is.EqualTo(1), "no target is requested for a null exit");

                trigger.NullOnExit = false;
                Set(trigger, "m_setNullOnExit", true);
                Call(trigger, "OnTriggerExit", player);
                Assert.That(fixture.Exited, Is.EqualTo(new[] { null, trigger.Proxy }), "the field is not read");
                Assert.That(trigger.TargetRequests, Is.EqualTo(2));
            }
        }

        // Start 0x5D45C0 waits on the level manager with OnLevelManagerValid 0x5D4920, which
        // calls InvokeOnLevelActivated(OnLevelActivated, true); OnLevelActivated 0x5D49A0 sends
        // the exit event with null only while triggered. OnDestroy 0x5D4650 removes the action
        // when TryGet finds the manager.
        [Test]
        public void LevelActivationSendsANullExitWhileTriggered()
        {
            var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<LevelManager>());
            var fixture = new TriggerFixture();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                var manager = Raw<LevelManager>();
                Set(manager, "<CurrentLevelValid>k__BackingField", true);
                Set(manager, "m_currentLevel", Raw<LevelManagerLevel>());
                row.Register(manager);
                TransformAssignEventTrigger trigger = fixture.Create();
                Set(trigger, "m_hasTriggered", true);
                Call(trigger, "Start");
                Assert.That(HandlerNames(manager, "m_actionOnLevelActivated"), Is.EqualTo(new[] { "OnLevelActivated" }));
                Assert.That(fixture.Exited, Is.EqualTo(new Transform[] { null }), "the current level activates at once");
                Assert.That(Get(trigger, "m_hasTriggered"), Is.False);

                Call(trigger, "OnLevelActivated", new object[] { null });
                Assert.That(fixture.Exited.Count, Is.EqualTo(1), "not triggered, no exit");

                Call(trigger, "OnDestroy");
                Assert.That(Get(manager, "m_actionOnLevelActivated"), Is.Null);
            }, fixture.Dispose, row.Dispose);
        }

        // Action_PlayPlatformAudio 0x6E9C50 plays the start one-shots, then the looping
        // ambient (PlayAudio 0x6ea3c0); Action_PlayPlatformWarning 0x6E9C80 plays the warning
        // one-shots; Action_StopPlatformAudio 0x6E9C90 stops the ambient. Nothing is null-checked.
        // Each one-shot here has null loaded clips, so its plays are counted by the null-clip warning.
        [Test]
        public void PlatformEffectsPlayTheStartThenTheLoopAndTheWarningAlone()
        {
            using (var owned = new OwnedAudioServices())
            using (var log = new UnityLogCounter())
            {
                HLAudioOneShotOnSource OneShot(int clips)
                {
                    HLAudioOneShotOnSource oneShot = owned.Add<HLAudioOneShotOnSource>();
                    OwnedAudioServices.Set(oneShot, "m_audioSource", oneShot.gameObject.AddComponent<AudioSource>());
                    OwnedAudioServices.Set(oneShot, "m_oneShotIds", new HLAudioClipIdentifier[clips]);
                    ((List<AudioClip>)OwnedAudioServices.Get(oneShot, "m_loadedClips")).AddRange(new AudioClip[clips]);
                    return oneShot;
                }

                HLAudioOneShotOnSource start = OneShot(1);
                HLAudioOneShotOnSource warning = OneShot(2);
                HLAudioAmbientComponent loop = owned.Add<HLAudioAmbientComponent>();
                AudioSource loopSource = loop.GetComponent<AudioSource>();
                OwnedAudioServices.Set(loop, "m_audioSource", loopSource);
                AudioClip clip = AudioClip.Create("lucid-vanishing-loop", 441, 1, 44100, false);
                owned.Own(clip);
                OwnedAudioServices.Set(loop, "m_clip", clip);
                var effects = owned.Add<VanishingPlatformEffects>();
                OwnedAudioServices.Set(effects, "m_platformLoopingAudio", loop);
                OwnedAudioServices.Set(effects, "m_platformAudioWarning", warning);

                Assert.Throws<NullReferenceException>(effects.Action_PlayPlatformAudio, "no start one-shot");
                Assert.That(loopSource.clip, Is.Null, "the loop is played after the start one-shots");

                OwnedAudioServices.Set(effects, "m_platformStartAnimationAudio", start);
                effects.Action_PlayPlatformAudio();
                Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(1), "start one-shots");
                Assert.That(loopSource.clip, Is.SameAs(clip));

                loopSource.clip = null;
                effects.Action_PlayPlatformWarning();
                Assert.That(log.Count(LogType.Warning, UnityLogCounter.NullOneShot), Is.EqualTo(3), "warning one-shots");
                Assert.That(loopSource.clip, Is.Null, "the warning leaves the loop alone");

                Assert.DoesNotThrow(effects.Action_StopPlatformAudio);
                OwnedAudioServices.Set(effects, "m_platformLoopingAudio", null);
                Assert.Throws<NullReferenceException>(effects.Action_StopPlatformAudio, "the loop is not null-checked");
            }
        }
    }
}
