using System;
using System.Reflection;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.CinemachineRecenterHeadingExtension_FreeLookBase (TypeDef 0x0200028a, x86-64
    // 0x721DA0..0x722F70), the CameraXAxis (0x722F70..0x723190) and ManualXAxis (0x723190..0x723C90) extensions. The
    // extension is a real component on an owned inactive host next to a real CinemachineFreeLook; the camera
    // manager, its brain and its proxy target are real components, the CharacterManager is built by its real
    // constructor and registered, and the Character is a real component whose Actor state is set as the owning
    // class does. Awake is run as Unity would run it, so the extension adopts the manager and its default
    // settings. Expected values are written from the native predicates (cited per case) and from geometry. Not
    // run here: the live-camera branch of ClearSnapToCharacter, the camera sensitivity overrides of
    // ManualXAxis, its clamped OnUpdate branch (it needs registered proxy-setting operations) and RecenterImmediately.
    // Reflection (Set/Get): the authored, serialized fields (settings assets, axis defaults, events, the manager's brain and
    // proxy target) have no setters, and a few private frame-state fields (named at their sites) are placed directly so a
    // single evaluation can be checked; the registry references are filled by registering the managers instead.
    public sealed class CinemachineFreeLookRecenterTests
    {
        private const float Tolerance = 1e-3f;

        private sealed class Rig<T> where T : CinemachineRecenterHeadingExtension_FreeLookBase
        {
            public CinemachineCameraManager Manager;
            public CinemachineBrain Brain;
            public CameraProxyTarget Proxy;
            public CinemachineFreeLook FreeLook;
            public T Extension;
            public CameraRecenterHeadingDefinition_FreeLook Settings;
            public CharacterManager Characters;
            public Character Character;
            public OwnedTimeScaledObjects Time;
            public int StartEvents, EndEvents;
        }

        private static void WithRig<T>(Action<Owned, Rig<T>> body, bool awake = true, Action<Rig<T>> beforeAwake = null) where T : CinemachineRecenterHeadingExtension_FreeLookBase =>
            WithManager((owned, characters, character) =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<CinemachineCameraManager>();
                owned.LeaseRow<CameraStackManager>();
                owned.LeaseRow<CameraYAxisProxyManager>();
                var rig = new Rig<T> { Characters = characters, Character = character };
                rig.Time = owned.Lease(new OwnedTimeScaledObjects("lucid-free-look-recenter"));
                // Registered before the extension is added, so its field initialiser takes the row's reference.
                rig.Manager = owned.Register(Inactive<CinemachineCameraManager>(rig.Time, "camera manager"));
                rig.Brain = Inactive<CinemachineBrain>(rig.Time, "brain");
                Set(rig.Manager, "m_cinemachineBrain", rig.Brain);
                rig.Proxy = Inactive<CameraProxyTarget>(rig.Time, "proxy target");
                Set(rig.Manager, "m_proxyTarget", rig.Proxy);

                GameObject host = rig.Time.Objects.Create("free look camera");
                host.SetActive(false);
                rig.FreeLook = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineFreeLook>(host);
                rig.Extension = host.AddComponent<T>();
                rig.Settings = rig.Time.Objects.Own(ScriptableObject.CreateInstance<CameraRecenterHeadingDefinition_FreeLook>());
                Set(rig.Settings, "m_velocityToRecenteringTimeCurve", AnimationCurve.Constant(0f, 10f, 2f));
                Set(rig.Settings, "m_angleRecenteringTimeMultiplierCurve", AnimationCurve.Constant(0f, 180f, 1f));
                Set(rig.Settings, "m_angleMultiplierLimitByVelocityCurve", AnimationCurve.Constant(0f, 10f, 0f));
                Set(rig.Extension, "m_defaultSettingsDefinition", rig.Settings);
                Set(rig.Extension, "m_onYAxisHeadingOverrideStart", Counting(() => rig.StartEvents++));
                Set(rig.Extension, "m_onYAxisHeadingOverrideEnd", Counting(() => rig.EndEvents++));
                SetActorProperty(character, "UpDirection", Vector3.up);
                SetActorProperty(character, "ForwardDirection", Vector3.forward);
                beforeAwake?.Invoke(rig);
                if (awake) Call(rig.Extension, "Awake");
                body(owned, rig);
            });

        private static T Inactive<T>(OwnedTimeScaledObjects time, string name) where T : Component
        {
            GameObject host = time.Objects.Create(name);
            host.SetActive(false);
            return TimeScaledReferenceLease.Add<T>(host);
        }

        private static UnityEvent Counting(Action onInvoke)
        {
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => onInvoke());
            return unityEvent;
        }

        private static void SetMode(CameraRecenterHeadingDefinition_FreeLook settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode mode) =>
            Set(settings, "m_recenterMode", mode);

        private static void SetCharacterProperty(Character character, string name, object value) =>
            typeof(Character).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetSetMethod(true).Invoke(character, new[] { value });

        private static void SetCameraMovement(CinemachineCameraManager manager, Vector2 movement) =>
            typeof(CinemachineCameraManager).GetProperty("ControllerMovement").GetSetMethod(true).Invoke(manager, new object[] { movement });

        // Snapping needs a valid current character; the case then makes it invalid again so that ending the snap
        // stops at the manager's CharacterValid test (the live-camera query is not run here).
        private static void BeginSnap<T>(Rig<T> rig) where T : CinemachineRecenterHeadingExtension_FreeLookBase
        {
            SetCurrent(rig.Characters, rig.Character, true);
            rig.Extension.SnapToCharacter();
            Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.True);
            SetCurrent(rig.Characters, rig.Character, false);
        }

        private static AxisState.Recentering Evaluate<T>(Rig<T> rig, AxisState.Recentering recentering, float deltaTime, float forwardAngle)
            where T : CinemachineRecenterHeadingExtension_FreeLookBase
        {
            object[] arguments = { rig.Character, recentering, deltaTime, forwardAngle };
            Call(rig.Extension, "EvaluateRecentering", arguments);
            return (AxisState.Recentering)arguments[1];
        }

        private static AxisState.Recentering Enabled(bool enabled, float time = 1f) =>
            new AxisState.Recentering(enabled, 0f, time);

        // ---- construction, Awake and the small members ----

        [Test]
        public void TheDefaultYAxisValueAndRevertSpeedStartAtOneHalf()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Assert.That(Get<float>(rig.Extension, "m_defaultYAxisValue"), Is.EqualTo(0.5f));
                Assert.That(Get<float>(rig.Extension, "m_defaultYAxisRevertSpeed"), Is.EqualTo(0.5f));
            }, false);
        }

        // 0x721E20 runs the original Awake (which registers the camera for snapping and adopts the manager),
        // then takes the default settings as the active ones.
        [Test]
        public void AwakeAdoptsTheManagerRegistersTheCameraAndTakesTheDefaultSettings()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Assert.That(Call(rig.Extension, "GetSettingsDefinition"), Is.Null);
                Call(rig.Extension, "Awake");
                Assert.That(Call(rig.Extension, "GetSettingsDefinition"), Is.SameAs(rig.Settings));
                Assert.That(Get<CinemachineCameraManager>(rig.Extension, "m_cameraManager"), Is.SameAs(rig.Manager));
                Assert.That(rig.Manager.CamerasWithSnapLookup[rig.FreeLook], Is.SameAs(rig.Extension));
            }, false);
        }

        // 0x722720: the Y axis moves towards the target by at most speed * deltaTime (Mathf.MoveTowards) and snaps
        // onto it when the remaining distance is within one step.
        [Test]
        public void TheYAxisMovesTowardsTheTargetBySpeedTimesDeltaTime()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                rig.FreeLook.m_YAxis.Value = 0.5f;
                Call(rig.Extension, "MoveYAxisToTarget", 1f, 2f, 0.1f);
                Assert.That(rig.FreeLook.m_YAxis.Value, Is.EqualTo(0.7f).Within(Tolerance));
                Call(rig.Extension, "MoveYAxisToTarget", 0f, 2f, 0.1f);
                Assert.That(rig.FreeLook.m_YAxis.Value, Is.EqualTo(0.5f).Within(Tolerance));
                Call(rig.Extension, "MoveYAxisToTarget", 0.55f, 2f, 0.1f);
                Assert.That(rig.FreeLook.m_YAxis.Value, Is.EqualTo(0.55f).Within(Tolerance), "within one step");
            });
        }

        // 0x722870 stores the flag, then raises the start event for true and the end event for false.
        [Test]
        public void TheYAxisOverrideFlagRaisesTheStartAndEndEvents()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Call(rig.Extension, "SetOverrideYHeadingAxis", true);
                Assert.That(Get<bool>(rig.Extension, "m_isOverridingYAxis"), Is.True);
                Assert.That((rig.StartEvents, rig.EndEvents), Is.EqualTo((1, 0)));
                Call(rig.Extension, "SetOverrideYHeadingAxis", false);
                Assert.That(Get<bool>(rig.Extension, "m_isOverridingYAxis"), Is.False);
                Assert.That((rig.StartEvents, rig.EndEvents), Is.EqualTo((1, 1)));
            });
        }

        [Test]
        public void AnUnsetYAxisOverrideEventIsTolerated()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Set(rig.Extension, "m_onYAxisHeadingOverrideStart", null);
                Set(rig.Extension, "m_onYAxisHeadingOverrideEnd", null);
                Assert.DoesNotThrow(() => Call(rig.Extension, "SetOverrideYHeadingAxis", true));
                Assert.DoesNotThrow(() => Call(rig.Extension, "SetOverrideYHeadingAxis", false));
            });
        }

        // 0x722A30 enables the recentering, starts it, and takes the snap recentering time (+0x50, default 0.1).
        [Test]
        public void RecenterNowEnablesTheRecenteringWithTheSnapTime()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                object[] arguments = { Enabled(false, 7f) };
                Call(rig.Extension, "RecenterNow", arguments);
                var recentering = (AxisState.Recentering)arguments[0];
                Assert.That(recentering.m_enabled, Is.True);
                Assert.That(recentering.m_RecenteringTime, Is.EqualTo(0.1f).Within(Tolerance));
            });
        }

        // ---- SnapToCharacter ----

        // 0x723070: the base snap runs only for a valid character, but the free-look heading recentering is
        // enabled and started either way.
        [Test]
        public void CameraXAxisSnapRecentersTheCameraHeadingWhetherOrNotACharacterIsValid()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetCurrent(rig.Characters, rig.Character, false);
                rig.Extension.SnapToCharacter();
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.False);
                Assert.That(rig.FreeLook.m_RecenterToTargetHeading.m_enabled, Is.True);
                Assert.That(rig.FreeLook.m_RecenterToTargetHeading.m_RecenteringTime, Is.EqualTo(0.1f).Within(Tolerance));

                rig.FreeLook.m_RecenterToTargetHeading.m_enabled = false;
                SetCurrent(rig.Characters, rig.Character, true);
                rig.Extension.SnapToCharacter();
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.True);
                Assert.That(Get<float>(rig.Extension, "m_timeoutTimerSeconds"), Is.EqualTo(0f));
                Assert.That(rig.FreeLook.m_RecenterToTargetHeading.m_enabled, Is.True);
            });
        }

        // ---- FixedUpdate (0x721EC0) ----

        [Test]
        public void FixedUpdateAdoptsTheCharactersHeadingOverrideAndRemembersItsPosition()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                var overrideSettings = rig.Time.Objects.Own(ScriptableObject.CreateInstance<CameraRecenterHeadingDefinition_FreeLook>());
                SetMode(overrideSettings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                SetActorProperty(rig.Character, "WorldPosition", new Vector3(3f, 4f, 5f));
                SetCurrent(rig.Characters, rig.Character, true);

                Call(rig.Extension, "FixedUpdate");
                Assert.That(Call(rig.Extension, "GetSettingsDefinition"), Is.SameAs(rig.Settings), "no override: the default settings");
                Assert.That(Get<Vector3>(rig.Extension, "m_previousTargetPosition"), Is.EqualTo(new Vector3(3f, 4f, 5f)));

                Get<StackableData>(rig.Character, "m_modifiers").AddOverride((int)GameplayModifierType.FreeLookHeadingOverride, overrideSettings);
                Call(rig.Extension, "FixedUpdate");
                Assert.That(Call(rig.Extension, "GetSettingsDefinition"), Is.SameAs(overrideSettings));
            });
        }

        [Test]
        public void FixedUpdateWithoutACurrentCharacterLeavesThePreviousPosition()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetActorProperty(rig.Character, "WorldPosition", new Vector3(3f, 4f, 5f));
                SetCurrent(rig.Characters, rig.Character, false);
                Call(rig.Extension, "FixedUpdate");
                Assert.That(Get<Vector3>(rig.Extension, "m_previousTargetPosition"), Is.EqualTo(Vector3.zero));
            });
        }

        // The base FixedUpdate runs first: with a zero timeout the snap times out at once and is cleared.
        [Test]
        public void FixedUpdateRunsTheBaseSnapTimeout()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Set(rig.Settings, "m_timeoutSeconds", 0f);
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                SetCurrent(rig.Characters, rig.Character, true);
                rig.Extension.SnapToCharacter();
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.True);
                Call(rig.Extension, "FixedUpdate");
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.False);
                Assert.That(Get<float>(rig.Extension, "m_timeoutTimerSeconds"), Is.EqualTo(0f));
            });
        }

        // ---- GetTargetDirections (0x722A70) ----

        private static (Vector3 forward, Vector3 up) Directions<T>(Rig<T> rig) where T : CinemachineRecenterHeadingExtension_FreeLookBase
        {
            object[] arguments = { rig.Character, null, null };
            Call(rig.Extension, "GetTargetDirections", arguments);
            return ((Vector3)arguments[1], (Vector3)arguments[2]);
        }

        private static void SetHeading(CameraRecenterHeadingDefinition_FreeLook settings, CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition heading) =>
            Set(settings, "m_headingDefinitionType", heading);

        [Test]
        public void TheTargetForwardHeadingIsTheCharactersForwardWhenNoProxyOrientsIt()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetActorProperty(rig.Character, "ForwardDirection", new Vector3(0f, 0f, 1f));
                SetActorProperty(rig.Character, "UpDirection", new Vector3(0f, 1f, 0f));
                (Vector3 forward, Vector3 up) = Directions(rig);
                Assert.That(forward, Is.EqualTo(new Vector3(0f, 0f, 1f)));
                Assert.That(up, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            });
        }

        // 0x722bd3..0x722c12: the velocity heading is the normalised velocity flattened onto the plane of the up
        // vector, unless it is within the stored cosine (cos 5 degrees, 0x722c05) of the up axis, when the
        // character's forward is used.
        [Test]
        public void TheVelocityHeadingIsTheFlattenedVelocityUnlessItIsNearlyParallelToUp()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetHeading(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition.Velocity);
                SetActorProperty(rig.Character, "WorldVelocityNormalised", new Vector3(0.6f, 0.8f, 0f));
                (Vector3 forward, Vector3 up) = Directions(rig);
                Assert.That(Vector3.Distance(forward, new Vector3(0.6f, 0f, 0f)), Is.LessThan(Tolerance), "flattened, not renormalised");
                Assert.That(up, Is.EqualTo(Vector3.up));

                SetActorProperty(rig.Character, "WorldVelocityNormalised", new Vector3(0f, 1f, 0f));
                (forward, _) = Directions(rig);
                Assert.That(forward, Is.EqualTo(Vector3.forward), "parallel to up: the character's forward");
            });
        }

        // 0x722b7d: the position delta from the previous target position, flattened; a zero delta falls back to
        // the character's forward.
        [Test]
        public void ThePositionDeltaHeadingIsTheFlattenedMoveSinceTheLastFixedUpdate()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetHeading(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition.PositionDelta);
                // Reflection: the position FixedUpdate remembers from the previous frame (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_previousTargetPosition", new Vector3(3f, 0f, 0f));
                SetActorProperty(rig.Character, "WorldPosition", new Vector3(3f, 2f, 4f));
                (Vector3 forward, _) = Directions(rig);
                Assert.That(Vector3.Distance(forward, new Vector3(0f, 0f, 4f)), Is.LessThan(Tolerance));

                SetActorProperty(rig.Character, "WorldPosition", new Vector3(3f, 0f, 0f));
                (forward, _) = Directions(rig);
                Assert.That(forward, Is.EqualTo(Vector3.forward), "no movement: the character's forward");
            });
        }

        [Test]
        public void AnUnknownHeadingTypeUsesTheCharactersOwnForwardAndUp()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Set(rig.Settings, "m_headingDefinitionType", (CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition)7);
                SetActorProperty(rig.Character, "ForwardDirection", new Vector3(1f, 0f, 0f));
                SetActorProperty(rig.Character, "UpDirection", new Vector3(0f, 0f, 1f));
                (Vector3 forward, Vector3 up) = Directions(rig);
                Assert.That(forward, Is.EqualTo(new Vector3(1f, 0f, 0f)));
                Assert.That(up, Is.EqualTo(new Vector3(0f, 0f, 1f)));
            });
        }

        // 0x722ab7: an active heading override whose settings use the target as the X axis override supplies the
        // target's own forward and up; one that does not leaves the proxy settings to decide.
        [Test]
        public void AnActiveHeadingOverrideSuppliesItsTargetsOrientation()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Transform target = owned.Host("Lucid heading target").transform;
                target.rotation = Quaternion.Euler(0f, 90f, 0f);
                var headingOverride = new CameraHeadingOverride { Target = target, Settings = new CameraHeadingOverride.CameraHeadingOverrideSettings() };
                rig.Manager.ApplyCameraSettingOverride(CameraSettingType.HeadingOverride, headingOverride);
                // Reflection: a flag an earlier override frame raised (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_headingOverrideActive", true); // EvaluateRecentering raises it when an override applies.

                (Vector3 forward, Vector3 up) = Directions(rig);
                Assert.That(Vector3.Distance(forward, Vector3.right), Is.LessThan(Tolerance));
                Assert.That(Vector3.Distance(up, Vector3.up), Is.LessThan(Tolerance));

                headingOverride.Settings.UseTargetAsXAxisOverride = false;
                (forward, _) = Directions(rig);
                Assert.That(forward, Is.EqualTo(Vector3.forward), "proxy settings decide");
            });
        }

        // ---- GetCameraForwardFromTargetAngle (0x722C80) ----

        [Test]
        public void TheCameraForwardAngleIsMeasuredFromTheTargetForward()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Assert.That((float)Call(rig.Extension, "GetCameraForwardFromTargetAngle", Vector3.right, Vector3.up, false), Is.EqualTo(90f).Within(Tolerance));
                Assert.That((float)Call(rig.Extension, "GetCameraForwardFromTargetAngle", Vector3.right, Vector3.up, true), Is.EqualTo(-90f).Within(Tolerance));
                Assert.That((float)Call(rig.Extension, "GetCameraForwardFromTargetAngle", Vector3.left, Vector3.up, true), Is.EqualTo(90f).Within(Tolerance));
                rig.Brain.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Assert.That((float)Call(rig.Extension, "GetCameraForwardFromTargetAngle", Vector3.right, Vector3.up, false), Is.EqualTo(0f).Within(Tolerance));

                // 0x722CA3..0x722CF7 re-aim the brain's up onto the target's up first, so a pitched camera still
                // looks along the target's forward.
                rig.Brain.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
                Assert.That((float)Call(rig.Extension, "GetCameraForwardFromTargetAngle", Vector3.forward, Vector3.up, false), Is.EqualTo(0f).Within(Tolerance));
            });
        }

        // ---- GetMaintainHeading (0x7228A0) ----

        private static bool MaintainHeading<T>(Rig<T> rig, bool recenteringEnabled) where T : CinemachineRecenterHeadingExtension_FreeLookBase =>
            (bool)Call(rig.Extension, "GetMaintainHeading", rig.Character, recenteringEnabled);

        [Test]
        public void TheHeadingIsMaintainedWhileKeptForTheIdleCharacterOrInsideTheStoppedInputWindow()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Set(rig.Settings, "m_stoppedInputDisableRecenterSeconds", 1f);
                // Reflection: the time stamp a prior fixed frame took (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_stopIdleRecenterTimeStamp", Time.fixedTime - 1000f);
                Assert.That(MaintainHeading(rig, true), Is.False, "window expired, recentering already on");
                // Reflection: the time stamp a prior fixed frame took (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_stopIdleRecenterTimeStamp", Time.fixedTime);
                Assert.That(MaintainHeading(rig, true), Is.True, "inside the window");
                // Reflection: the time stamp a prior fixed frame took (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_stopIdleRecenterTimeStamp", Time.fixedTime - 1000f);
                Set(rig.Extension, "m_keepCameraHeadingWhileIdle", true);
                Assert.That(MaintainHeading(rig, true), Is.True, "kept while idle");
            });
        }

        // Window expired and recentering off: the heading is maintained while the camera's forward on the
        // character plane is within the cosine of the stopped-input angle (cos 45 degrees = 0.7071) of the character's.
        [Test]
        public void AnExpiredWindowMaintainsTheHeadingOnlyWhileTheCameraIsWithinTheStoppedInputAngle()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Set(rig.Settings, "m_stoppedInputDisableRecenterSeconds", 1f);
                // Reflection: the time stamp a prior fixed frame took (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_stopIdleRecenterTimeStamp", Time.fixedTime - 1000f);
                Set(rig.Character, "m_cameraForwardOnCharacterPlane", Vector3.forward);
                Assert.That(MaintainHeading(rig, false), Is.True, "aligned: dot 1");
                // Reflection: the camera forward the camera pipeline writes each frame (no setter) (private frame state, placed to check one evaluation).
                Set(rig.Character, "m_cameraForwardOnCharacterPlane", Vector3.right);
                Assert.That(MaintainHeading(rig, false), Is.False, "perpendicular: dot 0");
                // Reflection: the camera forward the camera pipeline writes each frame (no setter) (private frame state, placed to check one evaluation).
                Set(rig.Character, "m_cameraForwardOnCharacterPlane", new Vector3(0.8f, 0f, 0.6f));
                Assert.That(MaintainHeading(rig, false), Is.False, "dot 0.6 is below 0.7071");
                // Reflection: the camera forward the camera pipeline writes each frame (no setter) (private frame state, placed to check one evaluation).
                Set(rig.Character, "m_cameraForwardOnCharacterPlane", new Vector3(0.6f, 0f, 0.8f));
                Assert.That(MaintainHeading(rig, false), Is.True, "dot 0.8 is above 0.7071");
            });
        }

        // ---- EvaluateRecentering (0x7220D0) ----

        [Test]
        public void WhileSnappingAnAngleAtOrAboveTheExitThresholdChangesNothing()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Set(rig.Settings, "m_forwardAngleExitSnapThreshold", 5f);
                BeginSnap(rig);
                AxisState.Recentering result = Evaluate(rig, Enabled(true, 7f), 0.1f, 10f);
                Assert.That(result.m_enabled, Is.True);
                Assert.That(result.m_RecenteringTime, Is.EqualTo(7f));
                Evaluate(rig, Enabled(true), 0.1f, 5f);
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.True, "exactly at the threshold");
            });
        }

        // 0x722160: only a comparison that is false (including NaN) ends the snap.
        [Test]
        public void WhileSnappingASmallerOrUnorderedAngleEndsTheSnap()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Set(rig.Settings, "m_forwardAngleExitSnapThreshold", 5f);
                BeginSnap(rig);
                Evaluate(rig, Enabled(true), 0.1f, 3f);
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.False);
                BeginSnap(rig);
                Evaluate(rig, Enabled(true), 0.1f, float.NaN);
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.False);
            });
        }

        [Test]
        public void NeverModeTurnsTheRecenteringOff()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, 10f).m_enabled, Is.False);
            });
        }

        [Test]
        public void CameraInputOrAHeldMaintainHeadingStopsRecenteringAndStampsTheTime()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetCameraMovement(rig.Manager, new Vector2(0f, 0.5f));
                AxisState.Recentering result = Evaluate(rig, Enabled(true), 0.1f, 10f);
                Assert.That(result.m_enabled, Is.False);
                Assert.That(Get<float>(rig.Extension, "m_stopIdleRecenterTimeStamp"), Is.EqualTo(Time.fixedTime));
                Assert.That(Get<bool>(rig.Extension, "m_keepCameraHeadingWhileIdle"), Is.False, "not idle");

                SetCameraMovement(rig.Manager, Vector2.zero);
                typeof(CameraInputBrain).GetProperty("MaintainHeading").GetSetMethod(true).Invoke(rig.Manager.InputBrain, new object[] { true });
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, 10f).m_enabled, Is.False, "held maintain-heading button");
            });
        }

        // The idle flag is read from storage only when the minimum recentering velocity is exactly zero (0x7223ac).
        [Test]
        public void AnIdleCharacterKeepsItsHeadingThroughCameraInputAndRecentersWithoutIt()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Set(rig.Settings, "m_minimumRequiredVelocityForRecenter", 0f);
                rig.Character.Storage.SetValue(ActorFSMKeys.IsIdle, true);
                SetCameraMovement(rig.Manager, new Vector2(0f, 0.5f));
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, 10f).m_enabled, Is.False);
                Assert.That(Get<bool>(rig.Extension, "m_keepCameraHeadingWhileIdle"), Is.True);

                SetCameraMovement(rig.Manager, Vector2.zero);
                AxisState.Recentering result = Evaluate(rig, Enabled(false, 9f), 0.1f, 10f);
                Assert.That(Get<bool>(rig.Extension, "m_keepCameraHeadingWhileIdle"), Is.False, "no stopped-input window: cleared");
                Assert.That(result.m_enabled, Is.True);
                Assert.That(result.m_RecenteringTime, Is.EqualTo(2f), "velocity curve 2 * max(angle multiplier 1, limit 0)");
            });
        }

        [Test]
        public void AlwaysModeRecentersWithoutInputUsingTheCalculatedTime()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Always);
                AxisState.Recentering result = Evaluate(rig, Enabled(false, 9f), 0.1f, 170f);
                Assert.That(result.m_enabled, Is.True);
                Assert.That(result.m_RecenteringTime, Is.EqualTo(2f));
                Assert.That(Get<float>(rig.Extension, "m_targetRecenteringTime"), Is.EqualTo(2f));
            });
        }

        // 0x7229c9: a non-zero smoothing value moves the recentering time towards the target by at most
        // smoothing * deltaTime (Mathf.MoveTowards): 5 towards 2 by 10 * 0.1 = 1 gives 4.
        [Test]
        public void ASmoothingValueEasesTheRecenteringTimeTowardsTheTarget()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Always);
                Set(rig.Settings, "m_recenterTimeSmoothingValue", 10f);
                Assert.That(Evaluate(rig, Enabled(false, 5f), 0.1f, 0f).m_RecenteringTime, Is.EqualTo(4f).Within(Tolerance));
                Assert.That(Evaluate(rig, Enabled(false, 2.5f), 0.1f, 0f).m_RecenteringTime, Is.EqualTo(2f).Within(Tolerance), "within one step");
            });
        }

        // 0x722562..0x7225a8 for the default mode: a camera proxy that is not the character disables; a character
        // slower than the minimum velocity disables; otherwise recentering turns on at or below the re-enable
        // angle (90) and, once on, stays on up to the disable angle (100).
        [Test]
        public void TheDefaultModeRecentersWithHysteresisBetweenTheReEnableAndDisableAngles()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, 10f).m_enabled, Is.False, "proxy target is not the character");

                rig.Proxy.SetCharacterAsTarget();
                SetActorProperty(rig.Character, "WorldVelocityMagnitude", 0.5f);
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, 10f).m_enabled, Is.False, "slower than the minimum velocity 1");

                SetActorProperty(rig.Character, "WorldVelocityMagnitude", 2f);
                Assert.That(Evaluate(rig, Enabled(false), 0.1f, 80f).m_enabled, Is.True, "off: 80 <= 90");
                Assert.That(Evaluate(rig, Enabled(false), 0.1f, 90f).m_enabled, Is.True, "off: 90 <= 90");
                Assert.That(Evaluate(rig, Enabled(false), 0.1f, 95f).m_enabled, Is.False, "off: 95 > 90");
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, 95f).m_enabled, Is.True, "on: 95 <= 100");
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, 100f).m_enabled, Is.True, "on: 100 <= 100");
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, 105f).m_enabled, Is.False, "on: 105 > 100");
                Assert.That(Evaluate(rig, Enabled(true), 0.1f, float.NaN).m_enabled, Is.False, "unordered angle");
            });
        }

        // 0x72228a..0x722377: a Y axis override that is no longer wanted walks back to the default value at the
        // revert speed, and ends (raising the end event) once it is there or the player pushes the camera.
        [Test]
        public void TheYAxisOverrideRevertsToTheDefaultValueThenEnds()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                Call(rig.Extension, "SetOverrideYHeadingAxis", true);
                rig.FreeLook.m_YAxis.Value = 0.9f;
                Evaluate(rig, Enabled(false), 0.1f, 0f);
                Assert.That(rig.FreeLook.m_YAxis.Value, Is.EqualTo(0.85f).Within(Tolerance), "0.5 * 0.1 towards 0.5");
                Assert.That(Get<bool>(rig.Extension, "m_isOverridingYAxis"), Is.True);
                Assert.That(rig.EndEvents, Is.EqualTo(0));

                rig.FreeLook.m_YAxis.Value = 0.5f;
                Evaluate(rig, Enabled(false), 0.1f, 0f);
                Assert.That(Get<bool>(rig.Extension, "m_isOverridingYAxis"), Is.False, "reached the default");
                Assert.That(rig.EndEvents, Is.EqualTo(1));
            });
        }

        [Test]
        public void VerticalCameraInputEndsTheYAxisOverrideWithoutMovingTheAxis()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                Call(rig.Extension, "SetOverrideYHeadingAxis", true);
                rig.FreeLook.m_YAxis.Value = 0.9f;
                SetCameraMovement(rig.Manager, new Vector2(0f, 0.001f));
                Evaluate(rig, Enabled(false), 0.1f, 0f);
                Assert.That(rig.FreeLook.m_YAxis.Value, Is.EqualTo(0.9f).Within(Tolerance));
                Assert.That(Get<bool>(rig.Extension, "m_isOverridingYAxis"), Is.False);
                Assert.That(rig.EndEvents, Is.EqualTo(1));
            });
        }

        [Test]
        public void WithoutAHeadingOverrideTheWaitAfterInputStateIsCleared()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                // Reflection: a flag an earlier override frame raised (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_overrideWaitAfterInput", true);
                Set(rig.Extension, "m_overrideResetAfterInputTimer", 5f);
                Set(rig.Extension, "m_headingOverrideActive", true);
                Evaluate(rig, Enabled(false), 0.1f, 0f);
                Assert.That(Get<bool>(rig.Extension, "m_overrideWaitAfterInput"), Is.False);
                Assert.That(Get<float>(rig.Extension, "m_overrideResetAfterInputTimer"), Is.EqualTo(0f));
                Assert.That(Get<bool>(rig.Extension, "m_headingOverrideActive"), Is.False);
            });
        }

        private static CameraHeadingOverride NewOverride<T>(Rig<T> rig, Action<CameraHeadingOverride.CameraHeadingOverrideSettings> configure)
            where T : CinemachineRecenterHeadingExtension_FreeLookBase
        {
            var headingOverride = new CameraHeadingOverride { Target = rig.Extension.transform, Settings = new CameraHeadingOverride.CameraHeadingOverrideSettings() };
            configure(headingOverride.Settings);
            rig.Manager.ApplyCameraSettingOverride(CameraSettingType.HeadingOverride, headingOverride);
            return headingOverride;
        }

        // 0x7221ee..0x722222: an applied heading override recentres on its own terms and, when it uses the target
        // as the X axis override, returns before the recentering mode is consulted.
        [Test]
        public void AnAppliedHeadingOverrideRecentersAtItsOwnTimeAndSkipsTheMode()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                NewOverride(rig, settings =>
                {
                    settings.UseTargetAsXAxisOverride = true;
                    settings.OverrideXAxisRecenteringTime = true;
                    settings.XAxisRecenteringTime = 0.5f;
                });
                AxisState.Recentering result = Evaluate(rig, Enabled(false, 9f), 0.1f, 10f);
                Assert.That(result.m_enabled, Is.True);
                Assert.That(result.m_RecenteringTime, Is.EqualTo(0.5f));
                Assert.That(Get<bool>(rig.Extension, "m_headingOverrideActive"), Is.True);
            });
        }

        // A character whose storage disables the camera heading override ignores it (0x7221db..0x722237).
        [Test]
        public void ADisabledHeadingOverrideIsIgnored()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                NewOverride(rig, settings =>
                {
                    settings.UseTargetAsXAxisOverride = true;
                    settings.OverrideXAxisRecenteringTime = true;
                    settings.XAxisRecenteringTime = 0.5f;
                });
                rig.Character.Storage.SetValue(ActorFSMKeys.CameraHeadingOverrideDisabled, true);
                AxisState.Recentering result = Evaluate(rig, Enabled(true, 9f), 0.1f, 10f);
                Assert.That(result.m_enabled, Is.False, "the Never mode applies");
                Assert.That(Get<bool>(rig.Extension, "m_headingOverrideActive"), Is.False);
            });
        }

        // ---- UpdateWithHeadingOverride (0x7225B0) ----

        private static bool UpdateWithOverride<T>(Rig<T> rig, CameraHeadingOverride headingOverride, ref AxisState.Recentering recentering, float deltaTime)
            where T : CinemachineRecenterHeadingExtension_FreeLookBase
        {
            object[] arguments = { rig.Manager, rig.Character, recentering, deltaTime, headingOverride };
            bool result = (bool)Call(rig.Extension, "UpdateWithHeadingOverride", arguments);
            recentering = (AxisState.Recentering)arguments[2];
            return result;
        }

        [Test]
        public void CameraInputDuringAHeadingOverrideStartsTheWaitAndSuspendsOrKeepsTheRecentering()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                CameraHeadingOverride headingOverride = NewOverride(rig, settings => settings.ResetAfterCameraInputTimeSeconds = 1f);
                SetCameraMovement(rig.Manager, new Vector2(1f, 0f));
                AxisState.Recentering recentering = Enabled(true);
                Assert.That(UpdateWithOverride(rig, headingOverride, ref recentering, 0.1f), Is.True);
                Assert.That(recentering.m_enabled, Is.False, "recentering suspended");
                Assert.That(Get<bool>(rig.Extension, "m_overrideWaitAfterInput"), Is.True);
                Assert.That(Get<float>(rig.Extension, "m_overrideResetAfterInputTimer"), Is.EqualTo(0f));

                headingOverride.Settings.RecenterToCameraTargetOnCameraInput = true;
                recentering = Enabled(true);
                Assert.That(UpdateWithOverride(rig, headingOverride, ref recentering, 0.1f), Is.False);
                Assert.That(recentering.m_enabled, Is.True, "untouched");
            });
        }

        [Test]
        public void TheWaitAfterInputCountsUpToTheResetTimeThenResumes()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                CameraHeadingOverride headingOverride = NewOverride(rig, settings =>
                {
                    settings.ResetAfterCameraInputTimeSeconds = 1f;
                    settings.UseTargetAsXAxisOverride = true;
                    settings.OverrideXAxisRecenteringTime = true;
                    settings.XAxisRecenteringTime = 0.5f;
                });
                // Reflection: a flag an earlier override frame raised (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_overrideWaitAfterInput", true);
                Set(rig.Extension, "m_overrideResetAfterInputTimer", 0.2f);
                AxisState.Recentering recentering = Enabled(false, 9f);
                Assert.That(UpdateWithOverride(rig, headingOverride, ref recentering, 0.1f), Is.True);
                Assert.That(Get<float>(rig.Extension, "m_overrideResetAfterInputTimer"), Is.EqualTo(0.3f).Within(Tolerance));
                Assert.That(recentering.m_enabled, Is.False, "still waiting");

                // Reflection: a timer earlier override frames accumulated (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_overrideResetAfterInputTimer", 2f);
                Assert.That(UpdateWithOverride(rig, headingOverride, ref recentering, 0.1f), Is.True);
                Assert.That(Get<bool>(rig.Extension, "m_overrideWaitAfterInput"), Is.False);
                Assert.That(Get<float>(rig.Extension, "m_overrideResetAfterInputTimer"), Is.EqualTo(0f));
                Assert.That(recentering.m_enabled, Is.True);
                Assert.That(recentering.m_RecenteringTime, Is.EqualTo(0.5f));
            });
        }

        [Test]
        public void AHeadingOverrideThatDoesNotUseTheTargetLeavesTheXRecenteringAlone()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                CameraHeadingOverride headingOverride = NewOverride(rig, settings => settings.UseTargetAsXAxisOverride = false);
                AxisState.Recentering recentering = Enabled(false, 9f);
                Assert.That(UpdateWithOverride(rig, headingOverride, ref recentering, 0.1f), Is.True);
                Assert.That(recentering.m_enabled, Is.False);
                Assert.That(recentering.m_RecenteringTime, Is.EqualTo(9f));
                Assert.That(rig.EndEvents, Is.EqualTo(1), "no Y override either: the end event");
            });
        }

        [Test]
        public void AHeadingOverrideWithoutAnExplicitTimeCalculatesTheRecenteringTime()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                CameraHeadingOverride headingOverride = NewOverride(rig, settings => settings.UseTargetAsXAxisOverride = true);
                AxisState.Recentering recentering = Enabled(false, 9f);
                UpdateWithOverride(rig, headingOverride, ref recentering, 0.1f);
                Assert.That(recentering.m_enabled, Is.True);
                Assert.That(recentering.m_RecenteringTime, Is.EqualTo(2f));
            });
        }

        [Test]
        public void AHeadingOverrideMovesTheYAxisToItsValueUnlessTheCameraIsPushedUp()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                CameraHeadingOverride headingOverride = NewOverride(rig, settings =>
                {
                    settings.UseTargetAsXAxisOverride = false;
                    settings.OverrideYAxisValue = true;
                    settings.YAxisValue = 0.8f;
                    settings.YAxisSpeed = 2f;
                });
                rig.FreeLook.m_YAxis.Value = 0.5f;
                AxisState.Recentering recentering = Enabled(false);
                Assert.That(UpdateWithOverride(rig, headingOverride, ref recentering, 0.1f), Is.True);
                Assert.That(rig.FreeLook.m_YAxis.Value, Is.EqualTo(0.7f).Within(Tolerance));
                Assert.That(Get<bool>(rig.Extension, "m_isOverridingYAxis"), Is.True);
                Assert.That((rig.StartEvents, rig.EndEvents), Is.EqualTo((1, 0)));

                SetCameraMovement(rig.Manager, new Vector2(0f, 1f));
                UpdateWithOverride(rig, headingOverride, ref recentering, 0.1f);
                Assert.That(rig.FreeLook.m_YAxis.Value, Is.EqualTo(0.7f).Within(Tolerance), "not moved while pushed");
                Assert.That(Get<bool>(rig.Extension, "m_isOverridingYAxis"), Is.False);
                Assert.That((rig.StartEvents, rig.EndEvents), Is.EqualTo((1, 1)));
            });
        }

        // ---- CameraXAxis OnUpdate (0x722F70) ----

        [Test]
        public void CameraXAxisOnUpdateEvaluatesTheFreeLookHeadingRecentering()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookCameraXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                rig.FreeLook.m_RecenterToTargetHeading.m_enabled = true;
                Call(rig.Extension, "OnUpdate", rig.Character, 0.1f);
                Assert.That(rig.FreeLook.m_RecenterToTargetHeading.m_enabled, Is.False);

                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Always);
                Call(rig.Extension, "OnUpdate", rig.Character, 0.1f);
                Assert.That(rig.FreeLook.m_RecenterToTargetHeading.m_enabled, Is.True);
                Assert.That(rig.FreeLook.m_RecenterToTargetHeading.m_RecenteringTime, Is.EqualTo(2f));
            });
        }

        // ---- ManualXAxis ----

        private static AxisState AuthoredAxis(float value)
        {
            AxisState axis = default;
            axis.Value = value;
            axis.m_MinValue = -180f;
            axis.m_MaxValue = 180f;
            return axis;
        }

        // Awake registers the camera with the manager, whose ready callback (0x7231F0) zeroes the free-look X
        // axis and copies the authored default axis into the live one.
        [Test]
        public void ManualXAxisTakesOverTheFreeLookXAxisWhenTheManagerIsReady()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>((owned, rig) =>
            {
                Assert.That(rig.Extension.XAxisState.Value, Is.EqualTo(50f));
                Assert.That(rig.Extension.XAxisState.m_MaxValue, Is.EqualTo(180f));
                Assert.That(rig.FreeLook.m_XAxis.m_MaxSpeed, Is.EqualTo(0f), "the free-look X axis is zeroed");
                Assert.That(Get<CinemachineCameraManager>(rig.Extension, "m_cameraManager"), Is.SameAs(rig.Manager));
            }, true, rig =>
            {
                rig.FreeLook.m_XAxis.m_MaxSpeed = 5f;
                Set(rig.Extension, "m_xAxisStateDefault", AuthoredAxis(50f));
            });
        }

        [Test]
        public void ManualXAxisSnapEnablesItsOwnAxisRecenteringWithTheSnapTime()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>((owned, rig) =>
            {
                SetCurrent(rig.Characters, rig.Character, true);
                rig.Extension.SnapToCharacter();
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.True);
                Assert.That(rig.Extension.XAxisState.m_Recentering.m_enabled, Is.True);
                Assert.That(rig.Extension.XAxisState.m_Recentering.m_RecenteringTime, Is.EqualTo(0.1f).Within(Tolerance));
                Assert.That(rig.FreeLook.m_RecenterToTargetHeading.m_enabled, Is.False, "the free-look recentering is not used");
            });
        }

        // `register` puts the proxy into the CameraYAxisProxyManager row the extension's reference reads (one proxy per case).
        private static CameraYAxisProxyManager NewYAxisProxy<T>(Rig<T> rig, Vector3 targetForward, Owned register = null) where T : CinemachineRecenterHeadingExtension_FreeLookBase
        {
            var proxy = Inactive<CameraYAxisProxyManager>(rig.Time, "y axis proxy");
            if (register != null) register.Register(proxy);
            typeof(CameraYAxisProxyManager).GetProperty("TargetForward").GetSetMethod(true).Invoke(proxy, new object[] { targetForward });
            return proxy;
        }

        private static float RecenteringHeading<T>(Rig<T> rig, Vector3 characterForward, Vector3 characterUp, CameraYAxisProxyManager proxy)
            where T : CinemachineRecenterHeadingExtension_FreeLookBase =>
            (float)Call(rig.Extension, "GetRecenteringHeading", characterForward, characterUp, proxy);

        // 0x7237A0: the signed angle from the Y-axis proxy's forward (flattened onto the character plane) to the
        // character's forward; a degenerate projection reuses the last valid world forward (initially forward).
        [Test]
        public void TheRecenteringHeadingIsTheSignedAngleFromTheProxyForwardToTheCharacterForward()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>((owned, rig) =>
            {
                CameraYAxisProxyManager proxy = NewYAxisProxy(rig, Vector3.forward);
                Assert.That(RecenteringHeading(rig, Vector3.right, Vector3.up, proxy), Is.EqualTo(90f).Within(Tolerance));
                Assert.That(RecenteringHeading(rig, Vector3.left, Vector3.up, proxy), Is.EqualTo(-90f).Within(Tolerance));
                Assert.That(Get<Vector3>(rig.Extension, "m_lastValidWorldForward"), Is.EqualTo(Vector3.forward));
            });
        }

        [Test]
        public void ADegenerateProxyForwardReusesTheLastValidWorldForward()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>((owned, rig) =>
            {
                Assert.That(Get<Vector3>(rig.Extension, "m_lastValidWorldForward"), Is.EqualTo(Vector3.forward), "initial value");
                CameraYAxisProxyManager parallel = NewYAxisProxy(rig, Vector3.up);
                Assert.That(RecenteringHeading(rig, Vector3.right, Vector3.up, parallel), Is.EqualTo(90f).Within(Tolerance), "reuses forward");

                CameraYAxisProxyManager behind = NewYAxisProxy(rig, Vector3.back);
                Assert.That(RecenteringHeading(rig, Vector3.right, Vector3.up, behind), Is.EqualTo(-90f).Within(Tolerance));
                Assert.That(RecenteringHeading(rig, Vector3.right, Vector3.up, parallel), Is.EqualTo(-90f).Within(Tolerance), "reuses back");
            });
        }

        // 0x723840: the half range moves towards maxAngle at deltaTime * rate; the axis range becomes
        // heading -/+ that half range, without wrapping; and the value is clamped to heading -/+ maxAngle with
        // wrapping (CameraUtilities.ClampAxisStateValue) before being re-expressed around the heading.
        // heading 30, step 0.1 * 100, from 0 towards 90 gives a half range of 10 and the range [20, 40]. A value of
        // 200 wraps into [-60, 120] as 20, and 20 is already the shortest angle from the heading.
        [Test]
        public void TheXAxisLimitNarrowsTheRangeAroundTheRecenteringHeading()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>((owned, rig) =>
            {
                Call(rig.Extension, "EvaluateXAxisLimit", 30f, 0.1f, 90f, 100f);
                Assert.That(Get<float>(rig.Extension, "m_currentMaxXAxisAngle"), Is.EqualTo(10f).Within(Tolerance));
                AxisState axis = rig.Extension.XAxisState;
                Assert.That(axis.m_MinValue, Is.EqualTo(20f).Within(Tolerance));
                Assert.That(axis.m_MaxValue, Is.EqualTo(40f).Within(Tolerance));
                Assert.That(axis.m_Wrap, Is.False);
                Assert.That(axis.Value, Is.EqualTo(20f).Within(Tolerance));
            }, true, rig => Set(rig.Extension, "m_xAxisStateDefault", AuthoredAxis(200f)));
        }

        // Once the half range has reached maxAngle (Mathf.Approximately) the clamp is skipped; the value is
        // still re-expressed as the shortest angle around the heading: 350 around heading 10 is -10 from it.
        [Test]
        public void ARangeThatHasReachedTheLimitOnlyReExpressesTheValueAroundTheHeading()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>((owned, rig) =>
            {
                // Reflection: the range an earlier OnUpdate eased towards (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_currentMaxXAxisAngle", 90f);
                Call(rig.Extension, "EvaluateXAxisLimit", 10f, 0.1f, 90f, 100f);
                AxisState axis = rig.Extension.XAxisState;
                Assert.That(axis.m_MinValue, Is.EqualTo(-80f).Within(Tolerance));
                Assert.That(axis.m_MaxValue, Is.EqualTo(100f).Within(Tolerance));
                Assert.That(axis.Value, Is.EqualTo(-10f).Within(Tolerance));
            }, true, rig => Set(rig.Extension, "m_xAxisStateDefault", AuthoredAxis(350f)));
        }

        // 0x723530 without a limit: the proxy's forward change is subtracted from the value, the default range and
        // wrap are restored and the half range returns to 180 (0x43340000).
        [Test]
        public void ManualXAxisOnUpdateCompensatesTheProxyRotationAndRestoresTheDefaultRange()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                CameraYAxisProxyManager proxy = NewYAxisProxy(rig, Vector3.forward, owned);
                typeof(CameraYAxisProxyManager).GetProperty("SignedAngleDifferenceFromLastForward").GetSetMethod(true).Invoke(proxy, new object[] { 10f });
                // Reflection: the range an earlier OnUpdate eased towards (private frame state, placed to check one evaluation).
                Set(rig.Extension, "m_currentMaxXAxisAngle", 33f);

                Call(rig.Extension, "OnUpdate", rig.Character, 0.1f);
                AxisState axis = rig.Extension.XAxisState;
                Assert.That(axis.Value, Is.EqualTo(40f).Within(Tolerance));
                Assert.That(axis.m_MinValue, Is.EqualTo(-180f));
                Assert.That(axis.m_MaxValue, Is.EqualTo(180f));
                Assert.That(Get<float>(rig.Extension, "m_currentMaxXAxisAngle"), Is.EqualTo(180f));
                Assert.That(axis.m_Recentering.m_enabled, Is.False);
            }, true, rig => Set(rig.Extension, "m_xAxisStateDefault", AuthoredAxis(50f)));
        }

        // 0x723657..0x72366B: recentering that was enabled before the evaluation and is disabled after it (mode
        // Never) cancels the axis recentering, which stamps the real time on its last-input field (-1 after
        // RecenterNow, which SnapToCharacter performs).
        [Test]
        public void ManualXAxisCancelsTheAxisRecenteringWhenTheEvaluationSwitchesItOff()
        {
            WithRig<CinemachineRecenterHeadingExtension_FreeLookManualXAxis>((owned, rig) =>
            {
                SetMode(rig.Settings, CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);
                NewYAxisProxy(rig, Vector3.forward, owned);
                // SnapToCharacter enables the axis recentering and starts it (RecenterNow, last input -1); the snap itself is
                // then ended, as it is once the character is close enough, so that the evaluation reaches the mode test.
                BeginSnap(rig);
                rig.Extension.ClearSnapToCharacter();
                var lastInput = typeof(AxisState.Recentering).GetField("mLastAxisInputTime", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That((float)lastInput.GetValue(rig.Extension.XAxisState.m_Recentering), Is.EqualTo(-1f), "precondition");
                Assert.That(rig.Extension.XAxisState.m_Recentering.m_enabled, Is.True, "precondition");

                Call(rig.Extension, "OnUpdate", rig.Character, 0.1f);
                Assert.That(rig.Extension.XAxisState.m_Recentering.m_enabled, Is.False);
                Assert.That((float)lastInput.GetValue(rig.Extension.XAxisState.m_Recentering), Is.GreaterThan(0f));
            }, true, rig => Set(rig.Extension, "m_xAxisStateDefault", AuthoredAxis(50f)));
        }
    }
}
