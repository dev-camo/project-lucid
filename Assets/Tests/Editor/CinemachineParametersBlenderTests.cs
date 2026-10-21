using System;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.CinemachineParametersBlender (TypeDef 0x02000279, x86-64 0x71E830..0x71F4E0), its nested
    // BlendParameters and Parameter`1, and the Basic, FreeLook and FreeLookBoost blenders (0x71FD40..0x720F90).
    // The abstract base is exercised through the CinemachineParametersBlenderProbe of the CharCore runtime test
    // assembly; every component is a real component on an owned inactive host (Edit Mode runs no Awake). Expected
    // values are written out from the native predicates: the blend sign selects the end value (0x71F2C0: the
    // unordered and negative values take the negative end), the lerp factor is the clamped magnitude, and the
    // result is start + (end - start) * factor. Not run here: the boosting half of FreeLookBoost (it needs a
    // character with a BoostStamina), the FreeLook Awake registration and the Cinemachine pipeline update itself.
    public sealed class CinemachineParametersBlenderTests
    {
        private const float Tolerance = 1e-4f;

        // The source component is time scaled, so AddComponent sends the editor Reset and OnValidate that read
        // the category lookup; an owned empty one is installed for the case. Its static TimeManager reference
        // row is leased as well.
        private static void Run(Action<Owned, OwnedTimeScaledObjects> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<TimeManager>();
            body(owned, owned.Lease(new OwnedTimeScaledObjects("lucid-blender")));
        });

        private static GameObject Inactive(OwnedTimeScaledObjects time, string name, Transform parent = null)
        {
            GameObject host = time.Objects.Create(name, Vector3.zero, Quaternion.identity, parent);
            host.SetActive(false);
            return host;
        }

        private static CinemachineParametersBlenderSourceProbe NewSource(OwnedTimeScaledObjects time, float blendValue)
        {
            var source = Inactive(time, "source").AddComponent<CinemachineParametersBlenderSourceProbe>();
            source.RunUpdateBlendValue(blendValue, 0.02f);
            Assert.That(source.BlendValue, Is.EqualTo(blendValue), "a source without smoothing holds the supplied value");
            return source;
        }

        // ---- BlendParameter, LerpWithNegative (0x71F2C0, 0x71F340, 0x71F420) ----

        // start 2, positive end 10, negative end -6. Zero and positive values lerp towards the positive end by the
        // value clamped to one (0x29a6be0); negative values towards the negative end by their magnitude. A lerp
        // that is not clamped would give 18 for 2, and one that ignored the sign would give 10 for -1.
        private static readonly (float blend, float expected)[] FloatTable =
        {
            (0f, 2f), (0.5f, 6f), (1f, 10f), (2f, 10f), (-0.25f, 0f), (-1f, -6f), (-3f, -6f), (float.NaN, float.NaN),
        };

        [Test]
        public void AnInactiveFloatParameterContributesZero()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                foreach (float blend in new[] { -1f, 0f, 0.5f, 3f, float.NaN })
                    Assert.That(probe.RunBlendFloat(false, -6f, 2f, 10f, blend), Is.EqualTo(0f), "blend " + blend);
            });
        }

        [Test]
        public void AFloatParameterLerpsTowardsTheEndValueNamedByTheSignOfTheBlend()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                foreach ((float blend, float expected) in FloatTable)
                {
                    float actual = probe.RunBlendFloat(true, -6f, 2f, 10f, blend);
                    if (float.IsNaN(expected)) Assert.That(actual, Is.NaN, "blend NaN");
                    else Assert.That(actual, Is.EqualTo(expected).Within(Tolerance), "blend " + blend);
                }
            });
        }

        [Test]
        public void LerpWithNegativeTakesTheSameTableWithTheEndValuesAsArguments()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                foreach ((float blend, float expected) in FloatTable)
                {
                    float actual = probe.RunLerpWithNegative(2f, 10f, -6f, blend);
                    if (float.IsNaN(expected)) Assert.That(actual, Is.NaN, "blend NaN");
                    else Assert.That(actual, Is.EqualTo(expected).Within(Tolerance), "blend " + blend);
                }
            });
        }

        [Test]
        public void AVectorParameterLerpsEachComponentAndAnInactiveOneIsZero()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                Vector3 negative = new Vector3(-3f, -7f, -11f), start = new Vector3(1f, 1f, 1f), positive = new Vector3(5f, 9f, 13f);
                (float blend, Vector3 expected)[] table =
                {
                    (0f, start), (0.5f, new Vector3(3f, 5f, 7f)), (5f, positive), (-0.5f, new Vector3(-1f, -3f, -5f)), (-5f, negative),
                };
                foreach ((float blend, Vector3 expected) in table)
                {
                    Vector3 actual = probe.RunBlendVector(true, negative, start, positive, blend);
                    Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), "blend " + blend + " gave " + actual);
                }
                Assert.That(probe.RunBlendVector(false, negative, start, positive, 0.5f), Is.EqualTo(Vector3.zero));
                Vector3 unordered = probe.RunBlendVector(true, negative, start, positive, float.NaN);
                Assert.That(float.IsNaN(unordered.x) && float.IsNaN(unordered.y) && float.IsNaN(unordered.z), Is.True, "NaN blend");
            });
        }

        // ---- blend curve and callback (0x71E870, 0x71E8D0, 0x71E8F0) ----

        // 0x71E8D0 and 0x71E8F0 read the source's BlendValue (+0x54) and ignore the frame time.
        [Test]
        public void TheBlendCurveIsSampledAtTheSourceBlendValueWhateverTheFrameTime()
        {
            Run((owned, time) =>
            {
                var probe = Inactive(time, "blender").AddComponent<CinemachineParametersBlenderProbe>();
                Set(probe, "m_blendValueSource", NewSource(time, 0.25f));
                Set(probe, "m_blendCurve", AnimationCurve.Linear(0f, 10f, 1f, 20f));
                Assert.That(probe.RunEvaluateBlendCurve(99f), Is.EqualTo(12.5f).Within(Tolerance));
                Assert.That(probe.RunEvaluateBlendCurve(0f), Is.EqualTo(12.5f).Within(Tolerance));
                Assert.That(probe.RunGetBlendSourceValue(), Is.EqualTo(0.25f));
            });
        }

        // The constructor's initialiser (0x71F490) is AnimationCurve.Linear(0, 0, 1, 1).
        [Test]
        public void TheDefaultBlendCurveIsTheIdentityRamp()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                var curve = Get<AnimationCurve>(probe, "m_blendCurve");
                Assert.That(curve.keys.Length, Is.EqualTo(2));
                Assert.That(curve.Evaluate(0f), Is.EqualTo(0f).Within(Tolerance));
                Assert.That(curve.Evaluate(0.3f), Is.EqualTo(0.3f).Within(Tolerance));
                Assert.That(curve.Evaluate(1f), Is.EqualTo(1f).Within(Tolerance));
            });
        }

        // 0x71E870 clamps the frame time with Mathf.Max(0, deltaTime) (maxss against zero, so a NaN passes
        // through), asks the virtual EvaluateBlendCurve for the blend value, then evaluates with it.
        [Test]
        public void TheCallbackClampsTheFrameTimeAtZeroAndEvaluatesWithTheCurveResult()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                probe.Record = true;
                probe.CurveResult = 0.75f;
                CameraState state = CameraState.Default;
                probe.PrePipelineMutateCameraStateCallback(null, ref state, -0.5f);
                probe.PrePipelineMutateCameraStateCallback(null, ref state, 0.25f);
                probe.PrePipelineMutateCameraStateCallback(null, ref state, float.NaN);
                Assert.That(probe.CurveDeltaTimes.Count, Is.EqualTo(3));
                Assert.That(probe.CurveDeltaTimes[0], Is.EqualTo(0f), "negative frame time");
                Assert.That(probe.CurveDeltaTimes[1], Is.EqualTo(0.25f));
                Assert.That(probe.CurveDeltaTimes[2], Is.NaN);
                Assert.That(probe.EvaluatedBlendValues, Is.EqualTo(new[] { 0.75f, 0.75f, 0.75f }));
            });
        }

        // 0x71E860 is a bare return: the post-pipeline callback leaves the state alone.
        [Test]
        public void ThePostPipelineCallbackDoesNothing()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                CameraState state = CameraState.Default;
                state.Lens.FieldOfView = 33f;
                probe.InvokePostPipelineStageCallback(null, CinemachineCore.Stage.Aim, ref state, 0.1f);
                Assert.That(state.Lens.FieldOfView, Is.EqualTo(33f));
            });
        }

        // 0x71E830 runs the Cinemachine extension Awake, then the virtual InitialiseCinemachineComponents (slot 15).
        [Test]
        public void AwakeConnectsTheExtensionThenInitialisesTheComponents()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("camera", false);
                CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
                var probe = host.AddComponent<CinemachineParametersBlenderProbe>();
                probe.RunAwake();
                Assert.That(probe.InitialiseCalls, Is.EqualTo(1));
            });
        }

        // ---- Evaluate (0x71E900) ----

        [Test]
        public void EvaluateBlendsTheFieldOfViewAndTheDutchAngleOnlyWhenActive()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                probe.UseComponents(new CinemachineTransposer[0], new CinemachineComposer[0]);
                probe.SetFloat("FOV", true, 20f, 40f, 60f);
                probe.SetFloat("DutchAngle", true, -10f, 0f, 10f);
                CameraState state = CameraState.Default;
                state.Lens.FieldOfView = 90f;
                state.Lens.Dutch = 7f;
                probe.RunEvaluate(ref state, 0.5f);
                Assert.That(state.Lens.FieldOfView, Is.EqualTo(50f).Within(Tolerance));
                Assert.That(state.Lens.Dutch, Is.EqualTo(5f).Within(Tolerance));
                probe.RunEvaluate(ref state, -0.5f);
                Assert.That(state.Lens.FieldOfView, Is.EqualTo(30f).Within(Tolerance));
                Assert.That(state.Lens.Dutch, Is.EqualTo(-5f).Within(Tolerance));

                probe.SetFloat("FOV", false, 20f, 40f, 60f);
                probe.SetFloat("DutchAngle", false, -10f, 0f, 10f);
                state.Lens.FieldOfView = 90f;
                state.Lens.Dutch = 7f;
                probe.RunEvaluate(ref state, 0.5f);
                Assert.That(state.Lens.FieldOfView, Is.EqualTo(90f));
                Assert.That(state.Lens.Dutch, Is.EqualTo(7f));
            });
        }

        // 0x71EA1F: a physical camera reads the blended value as a focal length, floored at 0.0001 (0x29a6dfc),
        // and converts it with the lens sensor height (+0x2c): 2 * atan(24 / (2 * 50)) = 26.9915 degrees, and a
        // floored zero gives 2 * atan(24 / 0.0002) = 179.99905 degrees.
        [Test]
        public void APhysicalCameraTurnsTheBlendedFieldOfViewIntoAFocalLength()
        {
            Owned.Run(owned =>
            {
                var camera = owned.Host("camera", false).AddComponent<Camera>();
                camera.usePhysicalProperties = true;
                camera.sensorSize = new Vector2(36f, 24f);
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                probe.UseComponents(new CinemachineTransposer[0], new CinemachineComposer[0]);
                CameraState state = CameraState.Default;
                state.Lens = LensSettings.FromCamera(camera);
                Assert.That(state.Lens.IsPhysicalCamera, Is.True);
                Assert.That(state.Lens.SensorSize.y, Is.EqualTo(24f));

                probe.SetFloat("FOV", true, 0f, 50f, 50f);
                probe.RunEvaluate(ref state, 0.5f);
                Assert.That(state.Lens.FieldOfView, Is.EqualTo(26.9915f).Within(1e-3f));
                probe.SetFloat("FOV", true, 0f, 0f, 0f);
                probe.RunEvaluate(ref state, 0.5f);
                Assert.That(state.Lens.FieldOfView, Is.EqualTo(179.99905f).Within(1e-3f));
            });
        }

        // 0x71EBF2 onwards: the damping fields are written per transposer only for the active rows; the
        // tracked offset and the look-ahead time per composer.
        [Test]
        public void EvaluateWritesTheActiveDampingOffsetAndLookaheadRowsToEveryComponent()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                var transposers = new[]
                {
                    owned.Host("transposer a", false).AddComponent<CinemachineTransposer>(),
                    owned.Host("transposer b", false).AddComponent<CinemachineTransposer>(),
                };
                var composers = new[]
                {
                    owned.Host("composer a", false).AddComponent<CinemachineComposer>(),
                    owned.Host("composer b", false).AddComponent<CinemachineComposer>(),
                };
                probe.UseComponents(transposers, composers);
                probe.SetFloat("YDamping", true, 0f, 1f, 3f);
                probe.SetFloat("ZDamping", false, 9f, 9f, 9f);
                probe.SetFloat("YawDamping", true, 0f, 0.2f, 0.6f);
                probe.SetAimTrackedOffset(true, Vector3.zero, Vector3.zero, new Vector3(2f, 4f, 6f));
                probe.SetFloat("LookaheadTime", true, 0f, 0f, 1f);
                float zBefore = transposers[0].m_ZDamping;
                CameraState state = CameraState.Default;
                probe.RunEvaluate(ref state, 0.5f);
                foreach (CinemachineTransposer transposer in transposers)
                {
                    Assert.That(transposer.m_YDamping, Is.EqualTo(2f).Within(Tolerance));
                    Assert.That(transposer.m_YawDamping, Is.EqualTo(0.4f).Within(Tolerance));
                    Assert.That(transposer.m_ZDamping, Is.EqualTo(zBefore), "an inactive row writes nothing");
                }
                foreach (CinemachineComposer composer in composers)
                {
                    Assert.That(Vector3.Distance(composer.m_TrackedObjectOffset, new Vector3(1f, 2f, 3f)), Is.LessThan(Tolerance));
                    Assert.That(composer.m_LookaheadTime, Is.EqualTo(0.5f).Within(Tolerance));
                }
            });
        }

        [Test]
        public void EvaluateWithNothingActiveLeavesTheComponentsAndTheStateUntouched()
        {
            Owned.Run(owned =>
            {
                var probe = owned.Host("probe", false).AddComponent<CinemachineParametersBlenderProbe>();
                var transposer = owned.Host("transposer", false).AddComponent<CinemachineTransposer>();
                var composer = owned.Host("composer", false).AddComponent<CinemachineComposer>();
                probe.UseComponents(new[] { transposer }, new[] { composer });
                transposer.m_YDamping = 4f;
                transposer.m_YawDamping = 5f;
                composer.m_TrackedObjectOffset = new Vector3(7f, 8f, 9f);
                composer.m_LookaheadTime = 3f;
                CameraState state = CameraState.Default;
                state.Lens.FieldOfView = 61f;
                probe.RunEvaluate(ref state, 0.9f);
                Assert.That(transposer.m_YDamping, Is.EqualTo(4f));
                Assert.That(transposer.m_YawDamping, Is.EqualTo(5f));
                Assert.That(composer.m_TrackedObjectOffset, Is.EqualTo(new Vector3(7f, 8f, 9f)));
                Assert.That(composer.m_LookaheadTime, Is.EqualTo(3f));
                Assert.That(state.Lens.FieldOfView, Is.EqualTo(61f));
            });
        }

        // ---- CinemachineParametersBlender_Basic (0x71FD40..0x720190) ----

        [Test]
        public void TheBasicBlenderTakesOneTransposerAndComposerFromTheVirtualCamera()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("camera", false);
                var camera = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
                var transposer = camera.AddCinemachineComponent<CinemachineTransposer>();
                var composer = camera.AddCinemachineComponent<CinemachineComposer>();
                var blender = host.AddComponent<CinemachineParametersBlenderBasicProbe>();
                blender.RunInitialise();
                Assert.That(blender.Transposers, Is.EqualTo(new[] { transposer }));
                Assert.That(blender.Composers, Is.EqualTo(new[] { composer }));
            });
        }

        [Test]
        public void TheBasicBlenderLeavesNullEntriesWhenTheCameraHasNoComponents()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("camera", false);
                CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
                var blender = host.AddComponent<CinemachineParametersBlenderBasicProbe>();
                blender.RunInitialise();
                Assert.That(blender.Transposers.Length, Is.EqualTo(1));
                Assert.That(blender.Transposers[0], Is.Null);
                Assert.That(blender.Composers.Length, Is.EqualTo(1));
                Assert.That(blender.Composers[0], Is.Null);
            });
        }

        [Test]
        public void TheBasicBlenderWritesTheFollowOffsetOnlyWhenItsRowIsActive()
        {
            Owned.Run(owned =>
            {
                var blender = owned.Host("blender", false).AddComponent<CinemachineParametersBlenderBasicProbe>();
                var transposer = owned.Host("transposer", false).AddComponent<CinemachineTransposer>();
                blender.UseComponents(new[] { transposer }, new CinemachineComposer[0]);
                transposer.m_FollowOffset = new Vector3(9f, 9f, 9f);
                CameraState state = CameraState.Default;

                Set(blender, "m_parameters", blender.NewParameters(false, new Vector3(-2f, -4f, -6f), Vector3.zero, new Vector3(2f, 4f, 6f)));
                blender.RunEvaluate(ref state, 0.5f);
                Assert.That(transposer.m_FollowOffset, Is.EqualTo(new Vector3(9f, 9f, 9f)), "inactive row");

                Set(blender, "m_parameters", blender.NewParameters(true, new Vector3(-2f, -4f, -6f), Vector3.zero, new Vector3(2f, 4f, 6f)));
                blender.RunEvaluate(ref state, 0.5f);
                Assert.That(Vector3.Distance(transposer.m_FollowOffset, new Vector3(1f, 2f, 3f)), Is.LessThan(Tolerance));
                blender.RunEvaluate(ref state, -0.5f);
                Assert.That(Vector3.Distance(transposer.m_FollowOffset, new Vector3(-1f, -2f, -3f)), Is.LessThan(Tolerance));
            });
        }

        [Test]
        public void TheBasicBlenderSuppliesItsAuthoredParameters()
        {
            Owned.Run(owned =>
            {
                var blender = owned.Host("blender", false).AddComponent<CinemachineParametersBlenderBasicProbe>();
                object parameters = blender.NewParameters(true, Vector3.zero, Vector3.zero, Vector3.one);
                Set(blender, "m_parameters", parameters);
                Assert.That(blender.RunGetBlendParameters(), Is.SameAs(parameters));
            });
        }

        // ---- CinemachineParametersBlender_FreeLook (0x7201A0..0x720C90) ----

        private static CinemachineParametersBlenderFreeLookProbe.OrbitRow Orbit(bool active, float negH, float negR, float startH, float startR, float posH, float posR) =>
            new CinemachineParametersBlenderFreeLookProbe.OrbitRow
            {
                Active = active,
                Negative = new CinemachineFreeLook.Orbit(negH, negR),
                Start = new CinemachineFreeLook.Orbit(startH, startR),
                Positive = new CinemachineFreeLook.Orbit(posH, posR),
            };

        private static CinemachineParametersBlenderFreeLookProbe.OffsetRow Offset(bool active, Vector3 start, Vector3 positive) =>
            new CinemachineParametersBlenderFreeLookProbe.OffsetRow { Active = active, Negative = -positive, Start = start, Positive = positive };

        [Test]
        public void TheFreeLookBlenderTakesATransposerAndComposerFromEachRig()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("free look", false);
                var blender = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineParametersBlenderFreeLookProbe>(host);
                var freeLook = host.GetComponent<CinemachineFreeLook>();
                Assert.That(freeLook, Is.Not.Null, "RequireComponent adds the free-look camera");
                blender.RunInitialise();
                Assert.That(blender.Transposers.Length, Is.EqualTo(3));
                Assert.That(blender.Composers.Length, Is.EqualTo(3));
                for (int rig = 0; rig < 3; rig++)
                {
                    Assert.That(blender.Transposers[rig], Is.SameAs(freeLook.GetRig(rig).GetCinemachineComponent<CinemachineTransposer>()), "rig " + rig);
                    Assert.That(blender.Composers[rig], Is.SameAs(freeLook.GetRig(rig).GetCinemachineComponent<CinemachineComposer>()), "rig " + rig);
                }
            });
        }

        // 0x720A20 stores the blended orbit in m_Orbits[rig] only for an active row, lerping both height and radius.
        [Test]
        public void TheFreeLookBlenderWritesTheActiveRigOrbitsAndLeavesTheOthers()
        {
            Owned.Run(owned =>
            {
                var blender = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineParametersBlenderFreeLookProbe>(owned.Host("free look", false));
                var freeLook = blender.GetComponent<CinemachineFreeLook>();
                blender.UseComponents(new CinemachineTransposer[3], new CinemachineComposer[3]);
                CinemachineParametersBlenderFreeLookProbe.OffsetRow none = default;
                Set(blender, "m_parameters", blender.NewParameters(
                    Orbit(true, -2f, 0f, 2f, 4f, 6f, 12f), Orbit(false, 0f, 0f, 100f, 100f, 100f, 100f), Orbit(true, -10f, -10f, 0f, 0f, 10f, 10f), none, none, none));
                CinemachineFreeLook.Orbit middle = freeLook.m_Orbits[1];
                CameraState state = CameraState.Default;

                blender.RunEvaluate(ref state, 0.5f);
                Assert.That(freeLook.m_Orbits[0].m_Height, Is.EqualTo(4f).Within(Tolerance));
                Assert.That(freeLook.m_Orbits[0].m_Radius, Is.EqualTo(8f).Within(Tolerance));
                Assert.That(freeLook.m_Orbits[1].m_Height, Is.EqualTo(middle.m_Height), "inactive rig orbit");
                Assert.That(freeLook.m_Orbits[1].m_Radius, Is.EqualTo(middle.m_Radius));
                Assert.That(freeLook.m_Orbits[2].m_Height, Is.EqualTo(5f).Within(Tolerance));
                Assert.That(freeLook.m_Orbits[2].m_Radius, Is.EqualTo(5f).Within(Tolerance));

                blender.RunEvaluate(ref state, -0.5f);
                Assert.That(freeLook.m_Orbits[0].m_Height, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(freeLook.m_Orbits[0].m_Radius, Is.EqualTo(2f).Within(Tolerance));
                Assert.That(freeLook.m_Orbits[2].m_Height, Is.EqualTo(-5f).Within(Tolerance));
            });
        }

        // 0x720B30 and the inlined copies in 0x720490 test the top rig's Active flag (+0xec of the parameters)
        // after the row's own: the top row gates the tracked offset of every rig.
        [Test]
        public void TheFreeLookTrackedOffsetsAreGatedByTheTopRowsActiveFlag()
        {
            Owned.Run(owned =>
            {
                var blender = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineParametersBlenderFreeLookProbe>(owned.Host("free look", false));
                var composers = new[]
                {
                    owned.Host("composer 0", false).AddComponent<CinemachineComposer>(),
                    owned.Host("composer 1", false).AddComponent<CinemachineComposer>(),
                    owned.Host("composer 2", false).AddComponent<CinemachineComposer>(),
                };
                blender.UseComponents(new CinemachineTransposer[3], composers);
                CinemachineParametersBlenderFreeLookProbe.OrbitRow noOrbit = default;
                CinemachineParametersBlenderFreeLookProbe.OffsetRow off = default;
                CameraState state = CameraState.Default;
                Vector3 one = Vector3.one, ten = new Vector3(10f, 10f, 10f);

                // The top row inactive: an active middle and bottom row write nothing.
                Set(blender, "m_parameters", blender.NewParameters(noOrbit, noOrbit, noOrbit, off, Offset(true, Vector3.zero, ten), Offset(true, Vector3.zero, ten)));
                blender.RunEvaluate(ref state, 0.5f);
                foreach (CinemachineComposer composer in composers) Assert.That(composer.m_TrackedObjectOffset, Is.EqualTo(Vector3.zero), "top row inactive");

                // The top row active with the others inactive: only the top composer is written.
                Set(blender, "m_parameters", blender.NewParameters(noOrbit, noOrbit, noOrbit, Offset(true, Vector3.zero, one), off, off));
                blender.RunEvaluate(ref state, 0.5f);
                Assert.That(Vector3.Distance(composers[0].m_TrackedObjectOffset, new Vector3(0.5f, 0.5f, 0.5f)), Is.LessThan(Tolerance));
                Assert.That(composers[1].m_TrackedObjectOffset, Is.EqualTo(Vector3.zero));
                Assert.That(composers[2].m_TrackedObjectOffset, Is.EqualTo(Vector3.zero));

                // All three active: each composer takes its own row.
                Set(blender, "m_parameters", blender.NewParameters(noOrbit, noOrbit, noOrbit,
                    Offset(true, Vector3.zero, one), Offset(true, Vector3.zero, ten), Offset(true, Vector3.zero, 2f * ten)));
                blender.RunEvaluate(ref state, 0.5f);
                Assert.That(Vector3.Distance(composers[0].m_TrackedObjectOffset, new Vector3(0.5f, 0.5f, 0.5f)), Is.LessThan(Tolerance));
                Assert.That(Vector3.Distance(composers[1].m_TrackedObjectOffset, new Vector3(5f, 5f, 5f)), Is.LessThan(Tolerance));
                Assert.That(Vector3.Distance(composers[2].m_TrackedObjectOffset, new Vector3(10f, 10f, 10f)), Is.LessThan(Tolerance));
            });
        }

        [Test]
        public void TheFreeLookBlenderSuppliesItsAuthoredParameters()
        {
            Owned.Run(owned =>
            {
                var blender = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineParametersBlenderFreeLookProbe>(owned.Host("free look", false));
                CinemachineParametersBlenderFreeLookProbe.OrbitRow noOrbit = default;
                CinemachineParametersBlenderFreeLookProbe.OffsetRow off = default;
                object parameters = blender.NewParameters(noOrbit, noOrbit, noOrbit, off, off, off);
                Set(blender, "m_parameters", parameters);
                Assert.That(blender.RunGetBlendParameters(), Is.SameAs(parameters));
            });
        }

        // ---- CinemachineParametersBlender_FreeLookBoost (0x720CA0..0x720EE0) ----

        // Without a registered CharacterManager the blender is not boosting (0x720d04): the boost timer moves by
        // -deltaTime * blendOutSpeed and is clamped at zero, then the default curve (sampled at the source value)
        // is lerped towards the boosting curve by the transition curve sampled at the timer.
        // Default 0.5 + (15 - 0.5) * 0.25 = 4.125 with the transition curve starting at 0.25.
        [Test]
        public void WithoutABoostingCharacterTheBoostBlendStaysAtTheTransitionCurveStart()
        {
            Run((owned, time) =>
            {
                owned.LeaseRow<CharacterManager>();
                var blender = Inactive(time, "blender").AddComponent<CinemachineParametersBlenderFreeLookBoostProbe>();
                Set(blender, "m_blendValueSource", NewSource(time, 0.5f));
                Set(blender, "m_blendCurveBoosting", AnimationCurve.Linear(0f, 10f, 1f, 20f));
                Set(blender, "m_blendBetweenDefaultAndBoostCurve", AnimationCurve.Linear(0f, 0.25f, 1f, 1f));
                Assert.That(blender.RunEvaluateBlendCurve(0.1f), Is.EqualTo(4.125f).Within(Tolerance));
                Assert.That(Get<float>(blender, "m_boostToDefaultTime"), Is.EqualTo(0f), "the timer cannot go below zero");
                Set(blender, "m_blendBetweenDefaultAndBoostCurve", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                Assert.That(blender.RunEvaluateBlendCurve(0.1f), Is.EqualTo(0.5f).Within(Tolerance), "weight zero keeps the default curve");
                Set(blender, "m_blendBetweenDefaultAndBoostCurve", AnimationCurve.Linear(0f, 1f, 1f, 1f));
                Assert.That(blender.RunEvaluateBlendCurve(0.1f), Is.EqualTo(15f).Within(Tolerance), "weight one takes the boosting curve");
            });
        }

        [Test]
        public void TheBoostBlenderDefaultsToUnitSpeedsAndAConstantTransitionCurve()
        {
            Run((owned, time) =>
            {
                owned.LeaseRow<CharacterManager>();
                var blender = Inactive(time, "blender").AddComponent<CinemachineParametersBlenderFreeLookBoostProbe>();
                Assert.That(Get<float>(blender, "m_blendInSpeed"), Is.EqualTo(1f));
                Assert.That(Get<float>(blender, "m_blendOutSpeed"), Is.EqualTo(1f));
                var transition = Get<AnimationCurve>(blender, "m_blendBetweenDefaultAndBoostCurve");
                Assert.That(transition.Evaluate(0f), Is.EqualTo(1f));
                Assert.That(transition.Evaluate(1f), Is.EqualTo(1f));
                Assert.That(Get<SystemRef<CharacterManager>>(blender, "m_characterManagerRef"), Is.Not.Null);
            });
        }
    }
}
