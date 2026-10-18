using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterMuzzle (TypeDef 0x0200033f, x86-64 0x76A760..0x76A88F) and
    // CharacterSideMuzzle (0x02000346, 0x76D260..0x76E61F). Both are real: the muzzle is built by
    // its constructor around a transform, and the side muzzle is a component on an owned host
    // whose rows (CinemachineCameraManager, CutsceneManager) are leased for the case. The Editor
    // sends Reset on AddComponent but no Awake or OnEnable, so each message a case needs is
    // invoked directly. Only the paths without a valid manager are run: OnCutsceneFinishing (0x76DAE0)
    // with a ready camera manager stores MainCamera.transform in m_mainCamera and returns at once
    // (0x76db58) without UpdateMuzzleSide, m_isActive or StopAllCoroutines; OnCameraManagerShutdown
    // (0x76D970) during a cutscene forwards to OnCutsceneStarted (0x76d9e2); OnCameraManagerStartup
    // (0x76D690) subscribes AssignCamera to OnMainCameraReady and calls it at once only when the main
    // camera is ready. These need a CinemachineCameraManager and CutsceneManager that cannot be built here.
    public sealed class OriginalCharacterMuzzleTests
    {
        private static void WithSideMuzzle(Action<OwnedWorldObjects, CharacterSideMuzzle> body)
        {
            using (var cameraRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<CinemachineCameraManager>()))
            using (var cutsceneRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<CutsceneManager>()))
            using (var objects = new OwnedWorldObjects("lucid-side-muzzle"))
                body(objects, objects.Create("side muzzle").AddComponent<CharacterSideMuzzle>());
        }

        // A side muzzle that tracks root and camera transforms and drives one muzzle transform.
        private sealed class Tracking
        {
            public Transform Root;
            public Transform Camera;
            public Transform Muzzle;
        }

        private static Tracking Track(OwnedWorldObjects objects, CharacterSideMuzzle component)
        {
            var tracking = new Tracking
            {
                Root = objects.Create("root").transform,
                Camera = objects.Create("camera").transform,
                Muzzle = objects.Create("muzzle").transform
            };
            Set(component, "m_characterRoot", tracking.Root);
            Set(component, "m_mainCamera", tracking.Camera);
            Set(component, "m_muzzleComponents", new[] { tracking.Muzzle });
            Call(component, "Awake");
            return tracking;
        }

        [Test]
        public void DeclarationsKeepTheOriginalFlagsMembersAndAttributes()
        {
            Assert.That((int)typeof(CharacterMuzzle).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(CharacterMuzzle).BaseType, Is.EqualTo(typeof(object)));
            Assert.That(AttributeRow(typeof(CharacterMuzzle)), Is.EqualTo("Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False)"));
            Assert.That(Fields(typeof(CharacterMuzzle)), Is.EqualTo(new[]
            {
                "m_transform:Transform:0021", "m_leftSideRotation:Vector3:0021", "m_leftSideScale:Vector3:0021",
                "m_rightSideRotation:Vector3:0021", "m_rightSideScale:Vector3:0021",
                "m_rotateMultiplier:Vector3:0021", "m_scaleMultiplier:Vector3:0021"
            }));
            Assert.That(Methods(typeof(CharacterMuzzle)), Is.EqualTo(new[] { ".ctor:1886", "SetMuzzleSide:0086" }));

            Type side = typeof(CharacterSideMuzzle);
            Assert.That((int)side.Attributes, Is.EqualTo(0x00100001));
            Assert.That(side.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeRow(side), Is.EqualTo(
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) DisallowMultipleComponent()"));
            Assert.That(Fields(side), Is.EqualTo(new[]
            {
                "DefaultHeadName:String:8051", "DefaultMouthName:String:8051", "DefaultMuzzleName:String:8051",
                "m_characterRoot:Transform:0001", "m_muzzleComponents:Transform[]:0001", "m_switchThreshold:Single:0001",
                "m_muzzleSettings:List`1:0021", "m_cameraManagerRef:SystemRef`1:0021", "m_cutsceneManagerRef:SystemRef`1:0021",
                "m_mainCamera:Transform:0001", "m_isActive:Boolean:0001"
            }));
            Assert.That(side.GetField("DefaultHeadName", All).GetRawConstantValue(), Is.EqualTo("Head_M"));
            Assert.That(side.GetField("DefaultMouthName", All).GetRawConstantValue(), Is.EqualTo("MouthMover_M"));
            Assert.That(side.GetField("DefaultMuzzleName", All).GetRawConstantValue(), Is.EqualTo("MuzzleMid_M"));
            foreach (string serialised in new[] { "m_characterRoot", "m_muzzleComponents", "m_switchThreshold" })
                Assert.That(AttributeRow(side.GetField(serialised, All)), Is.EqualTo("SerializeField()"), serialised);
            Assert.That(Methods(side), Is.EqualTo(new[]
            {
                "Awake:0081", "OnEnable:0081", "OnCameraManagerStartup:0081", "OnCameraManagerShutdown:0081",
                "OnCutsceneManagerStartup:0081", "OnCutsceneStarted:0081", "OnCutsceneFinishing:0081",
                "OnCutsceneManagerShutdown:0081", "OnDisable:0081", "AssignCamera:0081", "LateUpdate:0081",
                "UpdateMuzzleSide:0081", "Reset:0081", "GetNamedChild:0081", ".ctor:1886"
            }));
        }

        private const System.Reflection.BindingFlags All = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        // .ctor 0x76A760 multiplies the captured right-side Euler angles by (-1, -1, 1) and the scale by
        // (1, 1, -1), the constants at 0x29a7d40/0x29a7d50; SetMuzzleSide 0x76A810 selects the
        // right pose for a non-zero argument and the left pose otherwise.
        [Test]
        public void MuzzleMirrorsTheCapturedRightPoseOntoTheLeftAndKeepsItsOwnCopy()
        {
            using (var objects = new OwnedWorldObjects("lucid-muzzle"))
            {
                Transform transform = objects.Create("muzzle").transform;
                transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
                transform.localScale = new Vector3(2f, 3f, 4f);
                Vector3 right = transform.localEulerAngles;
                var muzzle = new CharacterMuzzle(transform);

                // The pose is captured at construction, not read again later.
                transform.localRotation = Quaternion.Euler(70f, 80f, 90f);
                transform.localScale = Vector3.one;

                muzzle.SetMuzzleSide(false);
                TransformAnimationAccess.AreClose(Quaternion.Euler(-right.x, -right.y, right.z), transform.localRotation, "left rotation");
                WorldMemberAccess.AreClose(new Vector3(2f, 3f, -4f), transform.localScale, "left scale");

                muzzle.SetMuzzleSide(true);
                TransformAnimationAccess.AreClose(Quaternion.Euler(right), transform.localRotation, "right rotation");
                WorldMemberAccess.AreClose(new Vector3(2f, 3f, 4f), transform.localScale, "right scale");

                muzzle.SetMuzzleSide(false);
                Assert.That(Quaternion.Angle(Quaternion.Euler(right), transform.localRotation), Is.GreaterThan(1f),
                    "the left pose differs from the right pose");
            }
        }

        // The left z rotation keeps its sign while x and y are negated, and only the scale z is negated.
        [Test]
        public void MuzzleNegatesExactlyTheXAndYRotationAndTheZScale()
        {
            using (var objects = new OwnedWorldObjects("lucid-muzzle-axes"))
            {
                Transform transform = objects.Create("muzzle").transform;
                transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                transform.localScale = new Vector3(5f, 6f, 7f);
                var muzzle = new CharacterMuzzle(transform);

                muzzle.SetMuzzleSide(false);
                TransformAnimationAccess.AreClose(Quaternion.Euler(0f, 0f, 45f), transform.localRotation, "z rotation is not negated");
                WorldMemberAccess.AreClose(new Vector3(5f, 6f, -7f), transform.localScale, "only scale z is negated");

                transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
                var other = new CharacterMuzzle(transform);
                other.SetMuzzleSide(false);
                TransformAnimationAccess.AreClose(Quaternion.Euler(-25f, 0f, 0f), transform.localRotation, "x rotation is negated");
            }
        }

        // OwnedWorldObjects prefixes every name; Reset looks children up by their exact names.
        private static GameObject Named(OwnedWorldObjects objects, string name, Transform parent)
        {
            GameObject child = objects.Create(name, Vector3.zero, Quaternion.identity, parent);
            child.name = name;
            return child;
        }

        // Reset 0x76E1B0 stores the first transform of that name in depth-first order for
        // Head_M, then a new two-element array with MouthMover_M and MuzzleMid_M; GetNamedChild
        // 0x76E470 returns null when nothing matches.
        [Test]
        public void ResetFindsTheFirstChildWithEachDefaultNameAndLeavesMissingOnesNull()
        {
            WithSideMuzzle((objects, component) =>
            {
                Transform host = component.transform;
                Transform rig = Named(objects, "Rig", host).transform;
                Transform mouth = Named(objects, "MouthMover_M", rig).transform;
                Transform head = Named(objects, "Head_M", host).transform;
                Transform firstMuzzle = Named(objects, "MuzzleMid_M", host).transform;
                Transform extra = Named(objects, "Extra", host).transform;
                Named(objects, "MuzzleMid_M", extra);
                Named(objects, "Head_M", extra);

                Set(component, "m_characterRoot", null);
                Set(component, "m_muzzleComponents", null);
                Call(component, "Reset");

                Assert.That(Get(component, "m_characterRoot"), Is.SameAs(head));
                var components = (Transform[])Get(component, "m_muzzleComponents");
                Assert.That(components, Is.EqualTo(new[] { mouth, firstMuzzle }));

                Assert.That(Call(component, "GetNamedChild", "Rig"), Is.SameAs(rig));
                Assert.That(Call(component, "GetNamedChild", "Nothing"), Is.Null);
                Assert.That(Call(component, "GetNamedChild", "Head_M"), Is.SameAs(head), "the first match in hierarchy order");
            });
        }

        [Test]
        public void ResetOnAHostWithoutThoseChildrenLeavesATwoSlotArrayOfNulls()
        {
            WithSideMuzzle((objects, component) =>
            {
                Set(component, "m_characterRoot", component.transform);
                Set(component, "m_muzzleComponents", null);
                Call(component, "Reset");

                Assert.That(Get(component, "m_characterRoot"), Is.Null);
                var components = (Transform[])Get(component, "m_muzzleComponents");
                Assert.That(components, Is.Not.Null);
                Assert.That(components.Length, Is.EqualTo(2));
                Assert.That(components, Is.All.Null);
            });
        }

        // The constructor stores 0.1 (0x3dcccccd) in the switch threshold and an empty list; both
        // manager references name their rows.
        [Test]
        public void ConstructorDefaultsTheThresholdToATenthAndResolvesBothManagerReferences()
        {
            WithSideMuzzle((objects, component) =>
            {
                Assert.That(Get(component, "m_switchThreshold"), Is.EqualTo(0.1f));
                Assert.That(((List<CharacterMuzzle>)Get(component, "m_muzzleSettings")), Is.Empty);
                Assert.That(((SystemRef<CinemachineCameraManager>)Get(component, "m_cameraManagerRef")).SystemName(),
                    Is.EqualTo(ProcessManager.GetDefaultName<CinemachineCameraManager>()));
                Assert.That(((SystemRef<CutsceneManager>)Get(component, "m_cutsceneManagerRef")).SystemName(),
                    Is.EqualTo(ProcessManager.GetDefaultName<CutsceneManager>()));
                Assert.That(Get(component, "m_isActive"), Is.False);
                Assert.That(Get(component, "m_mainCamera"), Is.Null);
            });
        }

        // Awake 0x76D260 clears the list and adds one CharacterMuzzle per component, so a second
        // Awake rebuilds from scratch rather than appending.
        [Test]
        public void AwakeBuildsOneMuzzlePerComponentFromScratch()
        {
            WithSideMuzzle((objects, component) =>
            {
                Transform first = objects.Create("first").transform;
                Transform second = objects.Create("second").transform;
                Set(component, "m_muzzleComponents", new[] { first, second });

                Call(component, "Awake");
                var settings = (List<CharacterMuzzle>)Get(component, "m_muzzleSettings");
                Assert.That(settings.Select(muzzle => Get(muzzle, "m_transform")).ToArray(), Is.EqualTo(new[] { first, second }));
                CharacterMuzzle[] built = settings.ToArray();

                Call(component, "Awake");
                Assert.That(settings.Count, Is.EqualTo(2));
                Assert.That(settings[0], Is.Not.SameAs(built[0]), "rebuilt, not appended");
                Assert.That(settings[1], Is.Not.SameAs(built[1]));
            });
        }

        // UpdateMuzzleSide 0x76DF20: side = Dot(-root.forward, camera - root); it switches only when
        // |side| > threshold (an equal value stays), to the right pose when side > 0.
        [Test]
        public void TheMuzzleSwitchesOnlyWhenTheCameraOffsetAlongForwardExceedsTheThreshold()
        {
            WithSideMuzzle((objects, component) =>
            {
                Tracking tracking = Track(objects, component);
                float ScaleZ() => tracking.Muzzle.localScale.z;
                void Update(float cameraZ, float threshold)
                {
                    tracking.Camera.position = new Vector3(0f, 0f, cameraZ);
                    Call(component, "UpdateMuzzleSide", threshold);
                }

                Assert.That(ScaleZ(), Is.EqualTo(1f), "the authored pose is the right side");
                Update(5f, 0.1f);
                Assert.That(ScaleZ(), Is.EqualTo(-1f), "a camera ahead of the root selects the left pose");
                Update(-5f, 0.1f);
                Assert.That(ScaleZ(), Is.EqualTo(1f), "a camera behind it selects the right pose");
                Update(5f, 0.1f);
                Assert.That(ScaleZ(), Is.EqualTo(-1f));
                Update(-0.05f, 0.1f);
                Assert.That(ScaleZ(), Is.EqualTo(-1f), "inside the threshold the pose is kept");
                Update(-0.1f, 0.1f);
                Assert.That(ScaleZ(), Is.EqualTo(-1f), "an offset equal to the threshold is not beyond it");
                Update(-0.1001f, 0.1f);
                Assert.That(ScaleZ(), Is.EqualTo(1f), "just beyond the threshold switches");
            });
        }

        [Test]
        public void ASideOffsetOrAZeroOffsetNeverSwitchesAndTheRootsForwardDecidesTheSign()
        {
            WithSideMuzzle((objects, component) =>
            {
                Tracking tracking = Track(objects, component);
                float ScaleZ() => tracking.Muzzle.localScale.z;

                // Right pose, camera purely to the side: the forward offset is zero, so a threshold of
                // zero still does not switch (a greater-or-equal test would flip it to the left).
                tracking.Camera.position = new Vector3(5f, 0f, 0f);
                Call(component, "UpdateMuzzleSide", 0f);
                Assert.That(ScaleZ(), Is.EqualTo(1f));

                // Turning the root a quarter turn makes +X its forward direction.
                tracking.Root.rotation = Quaternion.Euler(0f, 90f, 0f);
                Call(component, "UpdateMuzzleSide", 0f);
                Assert.That(ScaleZ(), Is.EqualTo(-1f), "the camera is now ahead of the root");

                tracking.Camera.position = new Vector3(-5f, 0f, 0f);
                Call(component, "UpdateMuzzleSide", 0f);
                Assert.That(ScaleZ(), Is.EqualTo(1f), "and now behind it");
            });
        }

        // AssignCamera 0x76D8F0 guards on this component (Unity null test), stores the camera
        // transform, applies UpdateMuzzleSide(0) and only then marks the component active.
        [Test]
        public void AssignCameraStoresTheCameraAppliesTheSideAtOnceAndActivates()
        {
            WithSideMuzzle((objects, component) =>
            {
                Tracking tracking = Track(objects, component);
                var camera = objects.Create("camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, 5f);

                Call(component, "AssignCamera", camera);

                Assert.That(Get(component, "m_mainCamera"), Is.SameAs(camera.transform));
                Assert.That(Get(component, "m_isActive"), Is.True);
                Assert.That(tracking.Muzzle.localScale.z, Is.EqualTo(-1f), "a zero threshold switches at once");
            });
        }

        [Test]
        public void AssignCameraWithTheCameraOnTheRootKeepsThePose()
        {
            WithSideMuzzle((objects, component) =>
            {
                Tracking tracking = Track(objects, component);
                var camera = objects.Create("camera").AddComponent<Camera>();

                Call(component, "AssignCamera", camera);

                Assert.That(Get(component, "m_isActive"), Is.True);
                Assert.That(tracking.Muzzle.localScale.z, Is.EqualTo(1f), "an offset of zero is not beyond a zero threshold");
            });
        }

        [Test]
        public void AssignCameraOnADestroyedComponentChangesNothing()
        {
            WithSideMuzzle((objects, component) =>
            {
                var camera = objects.Create("camera").AddComponent<Camera>();
                UnityEngine.Object.DestroyImmediate(component);

                Call(component, "AssignCamera", camera);

                Assert.That(Get(component, "m_mainCamera"), Is.Null);
                Assert.That(Get(component, "m_isActive"), Is.False);
            });
        }

        // LateUpdate 0x76E190 runs UpdateMuzzleSide with the serialised threshold only while active.
        [Test]
        public void LateUpdateAppliesTheSerialisedThresholdOnlyWhileActive()
        {
            WithSideMuzzle((objects, component) =>
            {
                Tracking tracking = Track(objects, component);
                float ScaleZ() => tracking.Muzzle.localScale.z;
                tracking.Camera.position = new Vector3(0f, 0f, 5f);

                Call(component, "LateUpdate");
                Assert.That(ScaleZ(), Is.EqualTo(1f), "an inactive component does nothing");

                Set(component, "m_isActive", true);
                Call(component, "LateUpdate");
                Assert.That(ScaleZ(), Is.EqualTo(-1f));

                Set(component, "m_switchThreshold", 1f);
                tracking.Camera.position = new Vector3(0f, 0f, -0.5f);
                Call(component, "LateUpdate");
                Assert.That(ScaleZ(), Is.EqualTo(-1f), "inside the serialised threshold");
                tracking.Camera.position = new Vector3(0f, 0f, -1.5f);
                Call(component, "LateUpdate");
                Assert.That(ScaleZ(), Is.EqualTo(1f), "beyond it");
            });
        }

        // With neither manager valid: the camera shutdown clears m_isActive (0x76D9E7), the cutscene
        // finishing stops coroutines and clears it (0x76DB71), and the cutscene shutdown forwards to it.
        [Test]
        public void WithoutValidManagersShutdownAndFinishingStopTracking()
        {
            WithSideMuzzle((objects, component) =>
            {
                Transform camera = objects.Create("camera").transform;
                Set(component, "m_mainCamera", camera);

                Set(component, "m_isActive", true);
                Call(component, "OnCameraManagerShutdown", new object[] { null });
                Assert.That(Get(component, "m_isActive"), Is.False);

                Set(component, "m_isActive", true);
                Call(component, "OnCutsceneFinishing");
                Assert.That(Get(component, "m_isActive"), Is.False);

                Set(component, "m_isActive", true);
                Call(component, "OnCutsceneManagerShutdown", new object[] { null });
                Assert.That(Get(component, "m_isActive"), Is.False);
                Assert.That(Get(component, "m_mainCamera"), Is.SameAs(camera), "the camera reference is kept");
            });
        }

        // OnEnable 0x76D450 subscribes the four handlers to the two references (invoking neither
        // startup handler while the managers are absent) and OnDisable 0x76DBA0 clears m_isActive
        // and removes them again, leaving both rows idle.
        [Test]
        public void EnableSubscribesToBothManagerReferencesAndDisableUnsubscribes()
        {
            WithSideMuzzle((objects, component) =>
            {
                object cameraRef = Get(component, "m_cameraManagerRef");
                object cutsceneRef = Get(component, "m_cutsceneManagerRef");
                Set(component, "m_isActive", true);

                Call(component, "OnEnable");
                try
                {
                    Assert.That(HandlerNames(cameraRef, "OnSystemStartup"), Is.EqualTo(new[] { "OnCameraManagerStartup" }));
                    Assert.That(HandlerNames(cameraRef, "OnSystemShutdown"), Is.EqualTo(new[] { "OnCameraManagerShutdown" }));
                    Assert.That(HandlerNames(cutsceneRef, "OnSystemStartup"), Is.EqualTo(new[] { "OnCutsceneManagerStartup" }));
                    Assert.That(HandlerNames(cutsceneRef, "OnSystemShutdown"), Is.EqualTo(new[] { "OnCutsceneManagerShutdown" }));
                    Assert.That(Get(component, "m_isActive"), Is.True, "no startup handler ran");
                }
                finally
                {
                    // The rows' references are shared, so the handlers must not outlive a failed assertion.
                    Call(component, "OnDisable");
                }
                Assert.That(HandlerNames(cameraRef, "OnSystemStartup"), Is.Empty);
                Assert.That(HandlerNames(cameraRef, "OnSystemShutdown"), Is.Empty);
                Assert.That(HandlerNames(cutsceneRef, "OnSystemStartup"), Is.Empty);
                Assert.That(HandlerNames(cutsceneRef, "OnSystemShutdown"), Is.Empty);
                Assert.That(Get(component, "m_isActive"), Is.False);
            });
        }

        private static string[] HandlerNames(object reference, string field)
        {
            var handlers = (Delegate)Get(reference, field);
            return handlers == null ? new string[0] : handlers.GetInvocationList().Select(handler => handler.Method.Name).ToArray();
        }
    }
}
