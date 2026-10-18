using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEditor.Animations;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.ViewTarget (TypeDef 0x02000853, x86-64 0x60DD70..0x60E600 and the constructor
    // 0x607800), ViewTargetParameters (0x02000854, 0x607940), AnimatedTargetIndicator (0x02000832,
    // 0x606EC0..0x6077E0) and AnimatedTargetIndicatorParameters (0x02000833, 0x607930). ViewTarget
    // is abstract; the probe in the CharCore assembly supplies its abstract members. Components sit
    // on owned objects (Edit Mode runs no Awake or OnEnable). The constructor takes the typed
    // CinemachineCameraManager reference, so every case leases that row. A case that needs the manager
    // to be present gives the component an owned reference holding a real CinemachineCameraManager
    // (added to an inactive host, the way the maintained camera cases do it) instead of registering
    // a system; the manager's main camera is the Camera of an owned CinemachineBrain.
    public sealed class OriginalViewTargetTests
    {
        private sealed class Rig : IDisposable
        {
            private readonly VerificationFixtureOwnership.LifecycleRow m_viewRow =
                new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<CinemachineCameraManager>());
            private VerificationFixtureOwnership.LifecycleRow m_saveRow;
            private VerificationFixtureOwnership.LifecycleRow m_stackRow;
            private OwnedTimeScaledObjects m_time;
            public Camera Camera;
            public CinemachineCameraManager Manager;

            public OwnedWorldObjects Objects => m_time.Objects;

            // withManagerRows leases the rows a CinemachineCameraManager constructor reads; a case that
            // never builds one, and runs under OwnedCharacterManagers (which registers the SaveManager),
            // leaves them out.
            public Rig(string prefix, bool withManagerRows = true)
            {
                try
                {
                    if (withManagerRows)
                    {
                        m_saveRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<SaveManager>());
                        m_stackRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<CameraStackManager>());
                    }
                    m_time = new OwnedTimeScaledObjects(prefix);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            // A manager whose main camera looks along +z from the origin.
            public void AddCamera()
            {
                GameObject managerHost = Objects.Create("camera manager");
                managerHost.SetActive(false);
                Manager = managerHost.AddComponent<CinemachineCameraManager>();
                GameObject cameraHost = Objects.Create("camera");
                cameraHost.SetActive(false);
                Camera = cameraHost.AddComponent<Camera>();
                Set(Manager, "m_cinemachineBrain", cameraHost.AddComponent<CinemachineBrain>());
            }

            public ViewTargetProbe NewView(bool withManager)
            {
                var view = Objects.Create("view").AddComponent<ViewTargetProbe>();
                if (withManager)
                    Set(view, "m_cameraManagerRef", new SystemRef<CinemachineCameraManager>(ProcessManager.GetDefaultName<CinemachineCameraManager>(), Manager));
                return view;
            }

            public void Dispose() => VerificationFixtureOwnership.RunThenCleanUp(() => { },
                () => m_time?.Dispose(), () => m_stackRow?.Dispose(), () => m_saveRow?.Dispose(), () => m_viewRow.Dispose());
        }

        private static readonly PropertyInfo TargetCollider = typeof(ViewTarget).GetProperty("m_targetCollider", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo TargetTransform = typeof(ViewTarget).GetProperty("m_targetTransform", BindingFlags.Instance | BindingFlags.NonPublic);

        private static AnimationCurve Curve(float timeStart, float valueStart, float timeEnd, float valueEnd) =>
            AnimationCurve.Linear(timeStart, valueStart, timeEnd, valueEnd);

        [Test]
        public void ViewTargetDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(ViewTarget).Attributes, Is.EqualTo(0x00100081));
            Assert.That(typeof(ViewTarget).BaseType, Is.EqualTo(typeof(ViewBase)));
            Assert.That(AttributeRow(typeof(ViewTarget)), Is.EqualTo("Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False)"));
            Assert.That(Fields(typeof(ViewTarget)), Is.EqualTo(new[]
            {
                "m_worldOffset:Vector3:0004", "m_fadeOverDistance:Boolean:0001", "m_fadeCurve:AnimationCurve:0001", "m_resizeOverDistance:Boolean:0001",
                "m_sizeCurve:AnimationCurve:0001", "m_minRenderDistance:Single:0001", "m_maxRenderDistance:Single:0001",
                "m_showForHomingTargetTypes:List`1:0004", "m_targetIsActiveBehindCamera:Boolean:0004", "m_targetWorldPosition:Nullable`1:0004",
                "m_cameraManagerRef:SystemRef`1:0024", "m_timer:Single:0001", "m_scale:Vector3:0001",
                "<m_targetCollider>k__BackingField:Collider:0001", "<m_targetTransform>k__BackingField:Transform:0001"
            }));
            Assert.That(Methods(typeof(ViewTarget)), Is.EqualTo(new[]
            {
                "get_IsActive:09c6", "get_TargetWorldPosition:0886", "get_m_targetCollider:0881", "set_m_targetCollider:0881",
                "get_m_targetTransform:0881", "set_m_targetTransform:0881", "get_TargetPosition:0dc4", "set_TargetPosition:0dc4",
                "get_TargetScale:0dc4", "set_TargetScale:0dc4", "get_TargetColour:0dc4", "set_TargetColour:0dc4",
                "get_ProjectToScreenSpace:0dc4", "get_MinRenderDistance:0884", "get_MaxRenderDistance:0884", "Awake:01c4", "OnSetup:00c4",
                "SetTargetCollider:01c6", "SetTargetTransform:01c6", "SetTargetPosition:0086", "ClearTarget:0086", "TargetInRange:01c4",
                "Refresh:01c4", "LateUpdate:01c4", "UpdateTargetInScreenSpace:0081", "UpdateTargetInWorldSpace:0081", "ShowTarget:05c4",
                "UpdateFromDetectorMessage:01c6", "UpdateFromTargetMessage:01c6", ".ctor:1884"
            }));
            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(typeof(ViewTarget).GetField("m_worldOffset", field)), Is.EqualTo(
                "TooltipAttribute(World offset applied to targeted position.) SerializeField()"));
            Assert.That(AttributeRow(typeof(ViewTarget).GetField("m_fadeCurve", field)), Is.EqualTo(
                "TooltipAttribute(Normalised distance alpha values.) SerializeField()"));
            Assert.That(AttributeRow(typeof(ViewTarget).GetField("m_maxRenderDistance", field)), Is.EqualTo("SerializeField()"));

            Assert.That((int)typeof(ViewTargetParameters).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(ViewTargetParameters).BaseType, Is.EqualTo(typeof(ViewParameters)));
            Assert.That(Fields(typeof(ViewTargetParameters)), Is.EqualTo(new[] { "WorldspaceTarget:Nullable`1:0006" }));
            Assert.That(Methods(typeof(ViewTargetParameters)), Is.EqualTo(new[] { ".ctor:1886" }));
            Assert.That((int)typeof(AnimatedTargetIndicatorParameters).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(AnimatedTargetIndicatorParameters).BaseType, Is.EqualTo(typeof(ViewTargetParameters)));
            Assert.That(Fields(typeof(AnimatedTargetIndicatorParameters)), Is.EqualTo(new[] { "OriginTarget:GameObject:0006" }));
            Assert.That(Methods(typeof(AnimatedTargetIndicatorParameters)), Is.EqualTo(new[] { ".ctor:1886" }));
        }

        // Constructor 0x607800: m_maxRenderDistance 0x42C80000 (100) at +0x64, an empty
        // List<HomingTargetType> at +0x68, m_targetIsActiveBehindCamera true at +0x70, the typed
        // CinemachineCameraManager reference from GetSystemRef(null, true) at +0x88 and Vector3.one in
        // m_scale (+0x94). get_IsActive 0x60DD70 is the game object's activeSelf; TargetWorldPosition
        // 0x60DD90 is the stored position or Vector3.zero.
        [Test]
        public void ConstructorStoresTheNativeDefaults()
        {
            using (var rig = new Rig("lucid-view-target-defaults"))
            {
                ViewTargetProbe view = rig.NewView(false);
                Assert.That((view.MinDistance, view.MaxDistance), Is.EqualTo((0f, 100f)));
                Assert.That(Get(view, "m_showForHomingTargetTypes"), Is.Empty.And.TypeOf<List<HomingTargetType>>());
                Assert.That(Get(view, "m_targetIsActiveBehindCamera"), Is.True);
                Assert.That((Vector3)Get(view, "m_scale"), Is.EqualTo(Vector3.one));
                Assert.That(Get(view, "m_fadeOverDistance"), Is.False);
                Assert.That(Get(view, "m_resizeOverDistance"), Is.False);
                Assert.That(Get(view, "m_timer"), Is.Zero);
                Assert.That(Get(view, "m_targetWorldPosition"), Is.Null);
                var reference = (SystemRef<CinemachineCameraManager>)Get(view, "m_cameraManagerRef");
                Assert.That(reference.SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<CinemachineCameraManager>()));
                Assert.That(view.TargetWorldPosition, Is.EqualTo(Vector3.zero));
                Assert.That(view.IsActive, Is.True);
                view.gameObject.SetActive(false);
                Assert.That(view.IsActive, Is.False);
            }
        }

        // Awake 0x60DEA0 tail-calls ShowTarget(false) through vtable slot 20; Refresh 0x60E060 zeroes
        // m_timer; TargetInRange 0x60E050 returns true.
        [Test]
        public void AwakeHidesTheTargetAndRefreshRestartsTheTimer()
        {
            using (var rig = new Rig("lucid-view-target-awake"))
            {
                ViewTargetProbe view = rig.NewView(false);
                view.RunAwake();
                Assert.That(view.Shown, Is.EqualTo(new[] { false }));
                Set(view, "m_timer", 3f);
                view.RunRefresh();
                Assert.That(Get(view, "m_timer"), Is.Zero);
                Assert.That(view.DefaultTargetInRange(new Vector3(-1f, -1f, -1f)), Is.True);
                Assert.DoesNotThrow(() => view.UpdateFromDetectorMessage(default(SurfaceDetectorUpdateMessage)));
                Assert.DoesNotThrow(() => view.UpdateFromTargetMessage(default(HomingTargetUpdateMessage)));
            }
        }

        // SetTargetPosition 0x60DFF0 stores the nullable; ClearTarget 0x60E000 nulls the collider,
        // the transform and the position; SetTargetTransform 0x60DF60 stores the transform and its
        // position; SetTargetCollider 0x60DEC0 stores the collider and its bounds centre.
        [Test]
        public void TheTargetSettersStoreTheTargetAndItsPosition()
        {
            using (var rig = new Rig("lucid-view-target-setters"))
            {
                ViewTargetProbe view = rig.NewView(false);
                view.SetTargetPosition(new Vector3(1f, 2f, 3f));
                Assert.That(view.TargetWorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                view.SetTargetPosition(null);
                Assert.That(Get(view, "m_targetWorldPosition"), Is.Null);

                Transform follow = rig.Objects.Create("follow", new Vector3(4f, 5f, 6f), Quaternion.identity).transform;
                view.SetTargetTransform(follow);
                Assert.That(TargetTransform.GetValue(view), Is.SameAs(follow));
                Assert.That(view.TargetWorldPosition, Is.EqualTo(new Vector3(4f, 5f, 6f)));

                var collider = rig.Objects.Create("collider", new Vector3(7f, 8f, 9f), Quaternion.identity).AddComponent<BoxCollider>();
                view.SetTargetCollider(collider);
                Assert.That(TargetCollider.GetValue(view), Is.SameAs(collider));
                Assert.That(view.TargetWorldPosition, Is.EqualTo(collider.bounds.center));

                view.ClearTarget();
                Assert.That(TargetCollider.GetValue(view), Is.Null);
                Assert.That(TargetTransform.GetValue(view), Is.Null);
                Assert.That(Get(view, "m_targetWorldPosition"), Is.Null);
                Assert.That(view.TargetWorldPosition, Is.EqualTo(Vector3.zero));
            }
        }

        // OnSetup 0x607070 reads the (unguarded) ViewTargetParameters cast: the position (+0x10, null
        // clears it) and the transform's local scale (m_scale). A plain ViewParameters is an invalid
        // cast and a null one is dereferenced.
        [Test]
        public void SetupTakesThePositionFromTheParametersAndTheScaleFromTheTransform()
        {
            using (var rig = new Rig("lucid-view-target-setup"))
            {
                ViewTargetProbe view = rig.NewView(false);
                view.transform.localScale = new Vector3(2f, 3f, 4f);
                Call(view, "OnSetup", new ViewTargetParameters { WorldspaceTarget = new Vector3(1f, 2f, 3f) });
                Assert.That(view.TargetWorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That((Vector3)Get(view, "m_scale"), Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Call(view, "OnSetup", new ViewTargetParameters());
                Assert.That(Get(view, "m_targetWorldPosition"), Is.Null);
                Assert.Throws<InvalidCastException>(() => Call(view, "OnSetup", new ViewParameters()));
                Assert.Throws<NullReferenceException>(() => Call(view, "OnSetup", new object[] { null }));
            }
        }

        // LateUpdate 0x60E070 hides the target without a position, and without the camera manager
        // even with one.
        [Test]
        public void LateUpdateHidesTheTargetWithoutAPositionOrACameraManager()
        {
            using (var rig = new Rig("lucid-view-target-hidden"))
            {
                ViewTargetProbe view = rig.NewView(false);
                view.RunLateUpdate();
                Assert.That(view.Shown, Is.EqualTo(new[] { false }), "no position");
                view.SetTargetPosition(new Vector3(0f, 0f, 5f));
                view.RunLateUpdate();
                Assert.That(view.Shown, Is.EqualTo(new[] { false, false }), "no camera manager");
                Assert.That(Get(view, "m_timer"), Is.Zero, "nothing else ran");
            }
        }

        // With a manager and a world-space target LateUpdate refreshes the position from the collider
        // when there is one (op_Inequality at 0x60E134), otherwise from the transform (0x60E17A),
        // plays the fade and size curves clamped to their last key (UpdateTargetInWorldSpace 0x60E430),
        // and shows the target when it is active and, unless m_targetIsActiveBehindCamera, in front of
        // the camera (screen depth above zero).
        [Test]
        public void WorldSpaceLateUpdateRefreshesThePositionAndPlaysTheCurves()
        {
            using (var rig = new Rig("lucid-view-target-world"))
            {
                rig.AddCamera();
                ViewTargetProbe view = rig.NewView(true);
                Transform follow = rig.Objects.Create("follow", new Vector3(1f, 2f, 3f), Quaternion.identity).transform;
                view.SetTargetTransform(follow);
                follow.position = new Vector3(4f, 5f, 6f);
                view.RunLateUpdate();
                Assert.That(view.TargetWorldPosition, Is.EqualTo(new Vector3(4f, 5f, 6f)), "the transform is followed");
                Assert.That(view.Shown, Is.EqualTo(new[] { true }));

                var collider = rig.Objects.Create("collider", new Vector3(-7f, 8f, 9f), Quaternion.identity).AddComponent<BoxCollider>();
                view.SetTargetCollider(collider);
                follow.position = new Vector3(40f, 50f, 60f);
                view.RunLateUpdate();
                Assert.That(view.TargetWorldPosition, Is.EqualTo(collider.bounds.center), "the collider wins over the transform");

                // Fade over a ping-pong curve: the playing time is capped at the last key (2), where the
                // value is 1; without the cap the time 100 would give 0.
                var fade = Curve(0f, 0f, 2f, 1f);
                fade.postWrapMode = WrapMode.PingPong;
                Set(view, "m_fadeCurve", fade);
                Set(view, "m_sizeCurve", AnimationCurve.Constant(0f, 1f, 0.5f));
                Set(view, "m_scale", new Vector3(2f, 4f, 6f));
                Set(view, "m_timer", 100f);
                view.StoredColour = new Color(0.1f, 0.2f, 0.3f, 0f);
                view.RunLateUpdate();
                Assert.That(view.StoredColour.a, Is.EqualTo(1f).Within(1e-4f));
                Assert.That((view.StoredColour.r, view.StoredColour.g, view.StoredColour.b), Is.EqualTo((0.1f, 0.2f, 0.3f)), "only alpha changes");
                AreClose(new Vector3(1f, 2f, 3f), view.StoredScale, "the cached scale times the curve value");
                Assert.That((float)Get(view, "m_timer"), Is.GreaterThanOrEqualTo(100f), "the frame time is added");

                // A curve whose last key is at time 0, and an empty or missing one, do nothing.
                view.StoredColour = new Color(0.1f, 0.2f, 0.3f, 0.4f);
                view.StoredScale = new Vector3(9f, 9f, 9f);
                foreach (AnimationCurve inert in new[] { AnimationCurve.Constant(0f, 0f, 0.7f), new AnimationCurve(), null })
                {
                    Set(view, "m_fadeCurve", inert);
                    Set(view, "m_sizeCurve", inert);
                    view.RunLateUpdate();
                    Assert.That(view.StoredColour.a, Is.EqualTo(0.4f), "inert fade curve");
                    Assert.That(view.StoredScale, Is.EqualTo(new Vector3(9f, 9f, 9f)), "inert size curve");
                }
            }
        }

        // The behind-camera test reads the abstract TargetPosition (slot 7): the target stays hidden at a
        // screen depth of zero or less and an inactive game object hides it whatever the depth.
        [Test]
        public void ATargetBehindTheCameraIsHiddenUnlessAllowed()
        {
            using (var rig = new Rig("lucid-view-target-behind"))
            {
                rig.AddCamera();
                ViewTargetProbe view = rig.NewView(true);
                view.SetTargetPosition(Vector3.zero);
                Set(view, "m_targetIsActiveBehindCamera", false);
                var expected = new List<bool>();
                foreach ((float depth, bool shown) in new[] { (1f, true), (0f, false), (-1f, false) })
                {
                    view.StoredPosition = new Vector3(0f, 0f, depth);
                    view.RunLateUpdate();
                    expected.Add(shown);
                    Assert.That(view.Shown, Is.EqualTo(expected), "depth " + depth);
                }
                Set(view, "m_targetIsActiveBehindCamera", true);
                view.RunLateUpdate();
                expected.Add(true);
                Assert.That(view.Shown, Is.EqualTo(expected), "allowed behind the camera");
                view.gameObject.SetActive(false);
                view.RunLateUpdate();
                expected.Add(false);
                Assert.That(view.Shown, Is.EqualTo(expected), "an inactive game object");
            }
        }

        // UpdateTargetInScreenSpace 0x60E270 projects the position plus m_worldOffset with the manager's
        // main camera (z = distance along the camera forward), stores it as TargetPosition, and inside a
        // positive render range asks TargetInRange(screen position) and evaluates both curves at
        // 1 - (z - min) / (max - min). The camera is at the origin looking along +z.
        [Test]
        public void ScreenSpaceLateUpdateProjectsTheTargetAndFadesOverTheRenderRange()
        {
            using (var rig = new Rig("lucid-view-target-screen"))
            {
                rig.AddCamera();
                ViewTargetProbe view = rig.NewView(true);
                view.ScreenSpace = true;
                view.SetTargetPosition(new Vector3(0f, 0f, 50f));
                Set(view, "m_minRenderDistance", 10f);
                Set(view, "m_maxRenderDistance", 110f);
                Set(view, "m_fadeOverDistance", true);
                Set(view, "m_resizeOverDistance", true);
                Set(view, "m_fadeCurve", Curve(0f, 0f, 1f, 1f));
                Set(view, "m_sizeCurve", Curve(0f, 1f, 1f, 3f));
                Set(view, "m_scale", new Vector3(5f, 5f, 5f));
                view.StoredColour = new Color(0.25f, 0.5f, 0.75f, 1f);
                view.RunLateUpdate();
                Assert.That(view.StoredPosition.z, Is.EqualTo(50f).Within(1e-3f));
                Assert.That(view.RangeQueries.Count, Is.EqualTo(1));
                Assert.That(view.RangeQueries[0].z, Is.EqualTo(50f).Within(1e-3f));
                // 1 - (50 - 10) / 100 = 0.6.
                Assert.That(view.StoredColour.a, Is.EqualTo(0.6f).Within(1e-4f));
                Assert.That((view.StoredColour.r, view.StoredColour.g, view.StoredColour.b), Is.EqualTo((0.25f, 0.5f, 0.75f)));
                AreClose(Vector3.one * 2.2f, view.StoredScale, "one times the size curve (not the cached scale)");
                Assert.That(view.Shown, Is.EqualTo(new[] { true }));

                // The world offset moves the projected point: 1 - (60 - 10) / 100 = 0.5.
                Set(view, "m_worldOffset", new Vector3(0f, 0f, 10f));
                view.RunLateUpdate();
                Assert.That(view.StoredPosition.z, Is.EqualTo(60f).Within(1e-3f));
                Assert.That(view.StoredColour.a, Is.EqualTo(0.5f).Within(1e-4f));
                AreClose(Vector3.one * 2f, view.StoredScale, "size curve at 0.5");

                // Out of range: hidden, and the curves are left alone.
                view.InRange = false;
                view.StoredColour = new Color(1f, 1f, 1f, 1f);
                view.StoredScale = new Vector3(9f, 9f, 9f);
                view.RunLateUpdate();
                Assert.That(view.Shown[view.Shown.Count - 1], Is.False);
                Assert.That(view.StoredColour.a, Is.EqualTo(1f));
                Assert.That(view.StoredScale, Is.EqualTo(new Vector3(9f, 9f, 9f)));
            }
        }

        // A render range that is not positive (max <= min, the jbe at 0x60E2EF) skips the range test
        // and the curves but still counts the target as in range. The in-range flags are combined with
        // a non-short-circuit and (0x60E215), so an inactive game object still runs the projection.
        [Test]
        public void ScreenSpaceWithoutARenderRangeStillProjectsAndNeverShortCircuits()
        {
            using (var rig = new Rig("lucid-view-target-screen-flat"))
            {
                rig.AddCamera();
                ViewTargetProbe view = rig.NewView(true);
                view.ScreenSpace = true;
                view.SetTargetPosition(new Vector3(0f, 0f, 30f));
                Set(view, "m_minRenderDistance", 10f);
                Set(view, "m_maxRenderDistance", 10f);
                Set(view, "m_fadeOverDistance", true);
                Set(view, "m_fadeCurve", Curve(0f, 0f, 1f, 1f));
                view.StoredColour = new Color(1f, 1f, 1f, 0.9f);
                view.RunLateUpdate();
                Assert.That(view.StoredPosition.z, Is.EqualTo(30f).Within(1e-3f));
                Assert.That(view.RangeQueries, Is.Empty);
                Assert.That(view.StoredColour.a, Is.EqualTo(0.9f));
                Assert.That(view.Shown, Is.EqualTo(new[] { true }));

                Set(view, "m_maxRenderDistance", 100f);
                view.gameObject.SetActive(false);
                view.RunLateUpdate();
                Assert.That(view.RangeQueries.Count, Is.EqualTo(1), "the projection still ran");
                Assert.That(view.Shown, Is.EqualTo(new[] { true, false }));
            }
        }

        // A screen depth behind the camera hides the target when it may not be active there.
        [Test]
        public void ScreenSpaceDepthBehindTheCameraHidesTheTargetUnlessAllowed()
        {
            using (var rig = new Rig("lucid-view-target-screen-behind"))
            {
                rig.AddCamera();
                ViewTargetProbe view = rig.NewView(true);
                view.ScreenSpace = true;
                view.SetTargetPosition(new Vector3(0f, 0f, -10f));
                view.RunLateUpdate();
                Assert.That(view.StoredPosition.z, Is.EqualTo(-10f).Within(1e-3f));
                Assert.That(view.Shown, Is.EqualTo(new[] { true }), "allowed behind the camera");
                Set(view, "m_targetIsActiveBehindCamera", false);
                view.RunLateUpdate();
                Assert.That(view.Shown, Is.EqualTo(new[] { true, false }));
            }
        }

        // ---- AnimatedTargetIndicator ----

        private sealed class Indicator : IDisposable
        {
            private readonly Rig m_rig;
            public readonly AnimatedTargetIndicator Component;
            public readonly Animator Animator;

            public Indicator(Rig rig, params (string name, AnimatorControllerParameterType type)[] parameters)
            {
                m_rig = rig;
                Component = rig.Objects.Create("indicator").AddComponent<AnimatedTargetIndicator>();
                GameObject animatorHost = rig.Objects.Create("animator", new Vector3(1f, 2f, 3f), Quaternion.identity);
                Animator = animatorHost.AddComponent<Animator>();
                Set(Component, "m_targetAnimator", Animator);
                if (parameters.Length > 0)
                {
                    var controller = rig.Objects.Own(new AnimatorController());
                    controller.AddLayer("Base");
                    foreach ((string name, AnimatorControllerParameterType type) parameter in parameters)
                        controller.AddParameter(parameter.name, parameter.type);
                    Animator.runtimeAnimatorController = controller;
                    Animator.Rebind();
                    Animator.Update(0f);
                }
            }

            // Names the wrapper's parameter so the indicator drives it.
            public void Bind(string field, string parameterName)
            {
                var wrapper = new AnimationParameterWrapper();
                Set(wrapper, "m_parameterName", parameterName);
                Set(Component, field, wrapper);
            }

            public void Dispose() { }
        }

        [Test]
        public void IndicatorDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(AnimatedTargetIndicator).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(AnimatedTargetIndicator).BaseType, Is.EqualTo(typeof(ViewTarget)));
            Assert.That(AttributeRow(typeof(AnimatedTargetIndicator)), Is.EqualTo("Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False)"));
            Assert.That(Fields(typeof(AnimatedTargetIndicator)), Is.EqualTo(new[]
            {
                "m_targetAnimator:Animator:0001", "m_visionBlockedParameter:AnimationParameterWrapper:0001",
                "m_vulnerableParameter:AnimationParameterWrapper:0001", "m_targetLockedParameter:AnimationParameterWrapper:0001",
                "m_attackTargetParameter:AnimationParameterWrapper:0001", "m_framesBetweenRaycast:Int32:0001", "m_raycastLayerMask:LayerMask:0001",
                "m_raycastFrameCount:Int32:0001", "m_emptyRaycastHit:RaycastHit:0021", "m_originTarget:GameObject:0001",
                "m_gameplayActive:Boolean:0001", "<TargetColour>k__BackingField:Color:0001"
            }));
            Assert.That(Methods(typeof(AnimatedTargetIndicator)), Is.EqualTo(new[]
            {
                "OnSetup:00c4", "OnOnGameplayControlActive:0081", "OnEnable:0081", "UpdateFromTargetMessage:00c6", "SetVulnerable:0086",
                "SetTargetLocked:0086", "SetAttack:0086", "SetHiddenBehindObject:0086", "ShowTarget:00c4", "get_TargetPosition:08c4",
                "set_TargetPosition:08c4", "get_TargetScale:08c4", "set_TargetScale:08c4", "get_TargetColour:08c4", "set_TargetColour:08c4",
                "get_ProjectToScreenSpace:08c4", "CheckIfVisible:0086", "OnDestroy:0081", ".ctor:1886"
            }));
            Assert.That(typeof(AnimatedTargetIndicator).GetMethod("CheckIfVisible").GetParameters()[0].IsOut, Is.True);
            Assert.That(AttributeRow(typeof(AnimatedTargetIndicator).GetField("m_framesBetweenRaycast", BindingFlags.Instance | BindingFlags.NonPublic)), Is.EqualTo(
                "TooltipAttribute(How many frames to wait between each raycast to check visibility? Higher number = more performant, less reactive.) SerializeField()"));
        }

        // Constructor 0x6077E0: m_framesBetweenRaycast 15 (+0xD8), m_gameplayActive true (+0x118), then
        // the ViewTarget constructor. get_ProjectToScreenSpace 0x6074F0 returns true.
        [Test]
        public void IndicatorConstructorStoresTheNativeDefaults()
        {
            using (var rig = new Rig("lucid-indicator-defaults"))
            {
                var indicator = new Indicator(rig);
                Assert.That(Get(indicator.Component, "m_framesBetweenRaycast"), Is.EqualTo(15));
                Assert.That(Get(indicator.Component, "m_gameplayActive"), Is.True);
                Assert.That(Get(indicator.Component, "m_raycastFrameCount"), Is.Zero);
                Assert.That(Call(indicator.Component, "get_ProjectToScreenSpace"), Is.True);
                Assert.That((float)Get(indicator.Component, "m_maxRenderDistance"), Is.EqualTo(100f), "the ViewTarget constructor ran");
            }
        }

        // The abstract members forward to the animator's transform (get_TargetPosition 0x607400,
        // set_TargetPosition 0x607420, get/set_TargetScale 0x607460/0x607480: position and local
        // scale), while the colour is an auto property. ShowTarget 0x6073B0 activates the animator's
        // game object only when the target is shown and gameplay control is active (m_gameplayActive,
        // written by OnOnGameplayControlActive 0x607110).
        [Test]
        public void IndicatorForwardsPoseToTheAnimatorAndShowsItOnlyDuringGameplay()
        {
            using (var rig = new Rig("lucid-indicator-pose"))
            {
                var indicator = new Indicator(rig);
                AreClose(new Vector3(1f, 2f, 3f), (Vector3)Call(indicator.Component, "get_TargetPosition"), "the animator's position");
                Call(indicator.Component, "set_TargetPosition", new Vector3(4f, 5f, 6f));
                AreClose(new Vector3(4f, 5f, 6f), indicator.Animator.transform.position, "set position");
                Call(indicator.Component, "set_TargetScale", new Vector3(2f, 3f, 4f));
                AreClose(new Vector3(2f, 3f, 4f), indicator.Animator.transform.localScale, "set scale");
                AreClose(new Vector3(2f, 3f, 4f), (Vector3)Call(indicator.Component, "get_TargetScale"), "get scale");
                Assert.That((Color)Call(indicator.Component, "get_TargetColour"), Is.EqualTo(default(Color)));
                Call(indicator.Component, "set_TargetColour", new Color(0.1f, 0.2f, 0.3f, 0.4f));
                Assert.That((Color)Call(indicator.Component, "get_TargetColour"), Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));

                GameObject shown = indicator.Animator.gameObject;
                Call(indicator.Component, "ShowTarget", false);
                Assert.That(shown.activeSelf, Is.False);
                Call(indicator.Component, "ShowTarget", true);
                Assert.That(shown.activeSelf, Is.True);
                Call(indicator.Component, "OnOnGameplayControlActive", false);
                Assert.That(Get(indicator.Component, "m_gameplayActive"), Is.False);
                Call(indicator.Component, "ShowTarget", true);
                Assert.That(shown.activeSelf, Is.False, "gameplay control is off");
                Call(indicator.Component, "OnOnGameplayControlActive", true);
                Call(indicator.Component, "ShowTarget", true);
                Assert.That(shown.activeSelf, Is.True);
            }
        }

        // OnSetup 0x606EC0 runs the ViewTarget setup, stores the origin target (+0x110) and subscribes
        // OnOnGameplayControlActive to the App's OnGameplayControlActive; OnDestroy 0x607720 unsubscribes
        // it. The App is the one OwnedCharacterManagers registers.
        [Test]
        public void IndicatorSubscribesToGameplayControlOnSetupAndLeavesOnDestroy()
        {
            using (var characters = new OwnedCharacterManagers())
            using (var rig = new Rig("lucid-indicator-setup", false))
            {
                var indicator = new Indicator(rig);
                GameObject origin = rig.Objects.Create("origin");
                indicator.Component.transform.localScale = new Vector3(2f, 3f, 4f);
                App app = ProcessManager.GetSystem<App>();
                int before = ((Delegate)Get(app, "OnGameplayControlActive")).GetInvocationList().Length;

                Call(indicator.Component, "OnSetup", new AnimatedTargetIndicatorParameters
                {
                    OriginTarget = origin, WorldspaceTarget = new Vector3(1f, 2f, 3f)
                });
                Assert.That(Get(indicator.Component, "m_originTarget"), Is.SameAs(origin));
                Assert.That(indicator.Component.TargetWorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)), "the base setup ran");
                Assert.That((Vector3)Get(indicator.Component, "m_scale"), Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Delegate[] handlers = ((Delegate)Get(app, "OnGameplayControlActive")).GetInvocationList();
                Assert.That(handlers.Length, Is.EqualTo(before + 1));
                Delegate ours = handlers[handlers.Length - 1];
                Assert.That(ours.Target, Is.SameAs(indicator.Component));
                Assert.That(ours.Method.Name, Is.EqualTo("OnOnGameplayControlActive"));
                ((Action<bool>)ours)(false);
                Assert.That(Get(indicator.Component, "m_gameplayActive"), Is.False);

                // A parameters object of the wrong type is an invalid cast after the base setup ran.
                Assert.Throws<InvalidCastException>(() => Call(indicator.Component, "OnSetup", new ViewTargetParameters()));

                Call(indicator.Component, "OnDestroy");
                Assert.That(((Delegate)Get(app, "OnGameplayControlActive")).GetInvocationList().Length, Is.EqualTo(before),
                    "the first subscription is removed; the failed setup subscribed nothing");
            }
        }

        private static readonly (string name, AnimatorControllerParameterType type)[] IndicatorParameters =
        {
            ("Vision", AnimatorControllerParameterType.Bool), ("Vulnerable", AnimatorControllerParameterType.Trigger),
            ("Locked", AnimatorControllerParameterType.Bool), ("Attack", AnimatorControllerParameterType.Trigger)
        };

        private static Indicator DrivenIndicator(Rig rig)
        {
            var indicator = new Indicator(rig, IndicatorParameters);
            indicator.Bind("m_visionBlockedParameter", "Vision");
            indicator.Bind("m_vulnerableParameter", "Vulnerable");
            indicator.Bind("m_targetLockedParameter", "Locked");
            indicator.Bind("m_attackTargetParameter", "Attack");
            return indicator;
        }

        private static bool Flag(Indicator indicator, string parameter) => indicator.Animator.GetBool(Animator.StringToHash(parameter));

        // SetHiddenBehindObject 0x607360 and SetTargetLocked 0x607290 call SetBool with the hash of
        // their own wrapper and the argument; SetVulnerable 0x607320 and SetAttack 0x6072E0 set the
        // triggers of theirs. The four parameters are told apart.
        [Test]
        public void IndicatorSettersDriveTheirOwnAnimatorParameters()
        {
            using (var rig = new Rig("lucid-indicator-setters"))
            {
                Indicator indicator = DrivenIndicator(rig);
                indicator.Component.SetHiddenBehindObject(true);
                Assert.That((Flag(indicator, "Vision"), Flag(indicator, "Locked"), Flag(indicator, "Vulnerable"), Flag(indicator, "Attack")),
                    Is.EqualTo((true, false, false, false)));
                indicator.Component.SetHiddenBehindObject(false);
                Assert.That(Flag(indicator, "Vision"), Is.False);
                indicator.Component.SetTargetLocked(true);
                Assert.That((Flag(indicator, "Vision"), Flag(indicator, "Locked")), Is.EqualTo((false, true)));
                indicator.Component.SetTargetLocked(false);
                Assert.That(Flag(indicator, "Locked"), Is.False);
                indicator.Component.SetVulnerable();
                Assert.That((Flag(indicator, "Vulnerable"), Flag(indicator, "Attack")), Is.EqualTo((true, false)));
                indicator.Component.SetAttack();
                Assert.That(Flag(indicator, "Attack"), Is.True);
            }
        }

        // OnEnable 0x607120 rebinds the animator (Rebind 0x607132) and updates it by zero seconds, which
        // returns every parameter to its default.
        [Test]
        public void IndicatorEnableRebindsTheAnimator()
        {
            using (var rig = new Rig("lucid-indicator-enable"))
            {
                Indicator indicator = DrivenIndicator(rig);
                indicator.Component.SetTargetLocked(true);
                Assert.That(Flag(indicator, "Locked"), Is.True);
                Call(indicator.Component, "OnEnable");
                Assert.That(Flag(indicator, "Locked"), Is.False);
            }
        }

        private static HomingTargetUpdateMessage Message(GameObject target, HomingTargetType type, bool isValid, bool isActive) =>
            new HomingTargetUpdateMessage
            {
                IsValid = isValid,
                IsActive = isActive,
                TargetValue = new CharacterHomingPool.PooledTarget
                {
                    Object = new CharacterTargetObjectData { Target = target, Type = type }
                }
            };

        // UpdateFromTargetMessage 0x607150: a valid message (byte +0 of the struct) for the origin
        // target (+0x10, op_Equality at 0x6071C5) whose type (+0x34) is in m_showForHomingTargetTypes
        // either sets the attack trigger (IsActive, +0x40; the jne at 0x6071F0) or sets the locked bool
        // true; every other message sets the locked bool false. The locked and attack parameters are
        // the only ones touched.
        [Test]
        public void IndicatorTargetMessagesLockOnAttackOrUnlock()
        {
            using (var rig = new Rig("lucid-indicator-message"))
            {
                Indicator indicator = DrivenIndicator(rig);
                GameObject origin = rig.Objects.Create("origin");
                GameObject other = rig.Objects.Create("other");
                Set(indicator.Component, "m_originTarget", origin);
                ((List<HomingTargetType>)Get(indicator.Component, "m_showForHomingTargetTypes")).Add(HomingTargetType.Enemy);

                indicator.Component.UpdateFromTargetMessage(Message(origin, HomingTargetType.Enemy, true, false));
                Assert.That((Flag(indicator, "Locked"), Flag(indicator, "Attack")), Is.EqualTo((true, false)), "locked on");
                indicator.Component.UpdateFromTargetMessage(Message(origin, HomingTargetType.Enemy, true, true));
                Assert.That((Flag(indicator, "Locked"), Flag(indicator, "Attack")), Is.EqualTo((true, true)), "active: attack, lock unchanged");

                foreach ((string label, HomingTargetUpdateMessage message) in new[]
                {
                    ("invalid", Message(origin, HomingTargetType.Enemy, false, false)),
                    ("another target", Message(other, HomingTargetType.Enemy, true, false)),
                    ("unlisted type", Message(origin, HomingTargetType.Boss, true, false))
                })
                {
                    indicator.Component.SetTargetLocked(true);
                    indicator.Component.UpdateFromTargetMessage(message);
                    Assert.That(Flag(indicator, "Locked"), Is.False, label);
                }
                Assert.That(Flag(indicator, "Vision") || Flag(indicator, "Vulnerable"), Is.False, "other parameters untouched");
            }
        }

        // CheckIfVisible 0x607500 counts calls (m_raycastFrameCount, +0xE0): until the count reaches
        // m_framesBetweenRaycast, and while the camera manager is missing, it returns true with the
        // empty hit and keeps counting; on the matching call it clears the count and returns the
        // Physics.Raycast of the main camera's ray through the target, limited to the camera's distance
        // to the supplied position and filtered by m_raycastLayerMask.
        [Test]
        public void CheckIfVisibleOnlyRaycastsEveryNthCallWithACameraManager()
        {
            using (var rig = new Rig("lucid-indicator-raycast-gating"))
            {
                var indicator = new Indicator(rig);
                Set(indicator.Component, "m_framesBetweenRaycast", 3);
                for (int call = 1; call <= 4; call++)
                {
                    bool visible = indicator.Component.CheckIfVisible(out RaycastHit hit, new Vector3(0f, 0f, 10f));
                    Assert.That(visible, Is.True, "call " + call);
                    Assert.That(hit.collider == null, Is.True, "call " + call);
                    Assert.That(Get(indicator.Component, "m_raycastFrameCount"), Is.EqualTo(call), "no manager: never cleared");
                }
            }
        }

        [Test]
        public void CheckIfVisibleCastsTheMainCameraRayThroughTheTarget()
        {
            using (var rig = new Rig("lucid-indicator-raycast"))
            {
                rig.AddCamera();
                var indicator = new Indicator(rig);
                Set(indicator.Component, "m_cameraManagerRef",
                    new SystemRef<CinemachineCameraManager>(ProcessManager.GetDefaultName<CinemachineCameraManager>(), rig.Manager));
                Set(indicator.Component, "m_framesBetweenRaycast", 2);
                Set(indicator.Component, "m_raycastLayerMask", (LayerMask)(-1));
                var target = new Vector3(0f, 0f, 10f);
                indicator.Component.SetTargetPosition(target);
                BoxCollider blocker = rig.Objects.Create("blocker", new Vector3(0f, 0f, 5f), Quaternion.identity).AddComponent<BoxCollider>();
                Physics.SyncTransforms();

                Assert.That(indicator.Component.CheckIfVisible(out RaycastHit first, target), Is.True);
                Assert.That(first.collider == null, Is.True, "the first call of two only counts");
                Assert.That(Get(indicator.Component, "m_raycastFrameCount"), Is.EqualTo(1));
                Assert.That(indicator.Component.CheckIfVisible(out RaycastHit second, target), Is.True, "the ray hits the blocker");
                Assert.That(second.collider, Is.SameAs(blocker));
                Assert.That(Get(indicator.Component, "m_raycastFrameCount"), Is.Zero, "cleared on the raycast");

                // Beyond the supplied position nothing is hit within the camera distance (10).
                blocker.transform.position = new Vector3(0f, 0f, 20f);
                Physics.SyncTransforms();
                indicator.Component.CheckIfVisible(out _, target);
                Assert.That(indicator.Component.CheckIfVisible(out RaycastHit third, target), Is.False);
                Assert.That(third.collider == null, Is.True);
            }
        }

        // The raycast distance is the camera's distance to the position argument (0x607604..0x607682),
        // while the ray still passes through the target's own world position. With the blocker at z = 5
        // and an argument at z = 4 the ray stops short of it; measuring to the stored target (z = 10)
        // would hit it.
        [Test]
        public void CheckIfVisibleLimitsTheRayToTheDistanceOfTheSuppliedPosition()
        {
            using (var rig = new Rig("lucid-indicator-raycast-distance"))
            {
                rig.AddCamera();
                var indicator = new Indicator(rig);
                Set(indicator.Component, "m_cameraManagerRef",
                    new SystemRef<CinemachineCameraManager>(ProcessManager.GetDefaultName<CinemachineCameraManager>(), rig.Manager));
                Set(indicator.Component, "m_framesBetweenRaycast", 2);
                Set(indicator.Component, "m_raycastLayerMask", (LayerMask)(-1));
                indicator.Component.SetTargetPosition(new Vector3(0f, 0f, 10f));
                rig.Objects.Create("blocker", new Vector3(0f, 0f, 5f), Quaternion.identity).AddComponent<BoxCollider>();
                Physics.SyncTransforms();

                var shortOfTheBlocker = new Vector3(0f, 0f, 4f);
                indicator.Component.CheckIfVisible(out _, shortOfTheBlocker);
                Assert.That(indicator.Component.CheckIfVisible(out RaycastHit hit, shortOfTheBlocker), Is.False);
                Assert.That(hit.collider == null, Is.True);
            }
        }

        // The gate is an inequality (the cmp/jne at 0x60756C): once the count is past
        // m_framesBetweenRaycast it never equals it again, so the ray is not cast and the empty hit is
        // returned as visible even with a blocker in the way.
        [Test]
        public void CheckIfVisibleNeverRaycastsOnceTheCountIsPastTheThreshold()
        {
            using (var rig = new Rig("lucid-indicator-raycast-past"))
            {
                rig.AddCamera();
                var indicator = new Indicator(rig);
                Set(indicator.Component, "m_cameraManagerRef",
                    new SystemRef<CinemachineCameraManager>(ProcessManager.GetDefaultName<CinemachineCameraManager>(), rig.Manager));
                Set(indicator.Component, "m_framesBetweenRaycast", 2);
                Set(indicator.Component, "m_raycastLayerMask", (LayerMask)(-1));
                var target = new Vector3(0f, 0f, 10f);
                indicator.Component.SetTargetPosition(target);
                rig.Objects.Create("blocker", new Vector3(0f, 0f, 5f), Quaternion.identity).AddComponent<BoxCollider>();
                Physics.SyncTransforms();

                Set(indicator.Component, "m_raycastFrameCount", 5);
                Assert.That(indicator.Component.CheckIfVisible(out RaycastHit hit, target), Is.True);
                Assert.That(hit.collider == null, Is.True);
                Assert.That(Get(indicator.Component, "m_raycastFrameCount"), Is.EqualTo(6));
            }
        }
    }
}
