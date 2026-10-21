using System;
using System.Reflection;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.CinemachineMix_CharacterBase (TypeDef 0x02000274, x86-64 0x71E120..0x71E340), its nested
    // VirtualCameraWeighting (0x02000275) and the HalfPipe, Lateral and Speed mixers (0x71E340..0x71E830). The base
    // is exercised through a probe of the CharCore runtime test assembly; the mixers are real time-scaled
    // components on owned inactive hosts, driven through the public OnUpdate. The mixing camera and its two child
    // cameras are real Cinemachine components; liveness (CinemachineCore.Instance.IsLive) is answered by an
    // enabled brain whose solo camera is the mixing camera (CinemachineCameraTestRig.MakeLive). Every case reads the
    // weights back from the mixing camera. The weighting curves are Linear(-1, 0, 1, 1) for the first camera and
    // Linear(-1, 1, 1, 0) for the second, so a blend value v gives (v + 1) / 2 and 1 - (v + 1) / 2.
    public sealed class CinemachineMixCharacterTests
    {
        private const float Tolerance = 1e-4f;

        private sealed class Rig
        {
            public CinemachineMixingCamera Mixing;
            public CinemachineVirtualCamera First, Second;
            public OwnedTimeScaledObjects Time;

            public float FirstWeight => Mixing.GetWeight(First);
            public float SecondWeight => Mixing.GetWeight(Second);

            public IDisposable GoLive()
            {
                Time.Objects.Create("brain").AddComponent<CinemachineBrain>();
                return CinemachineCameraTestRig.MakeLive(Mixing);
            }
        }

        private static Rig NewRig(OwnedTimeScaledObjects time)
        {
            GameObject mixingHost = time.Objects.Create("mixing camera");
            mixingHost.SetActive(false);
            var rig = new Rig { Time = time, Mixing = mixingHost.AddComponent<CinemachineMixingCamera>() };
            rig.First = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(
                time.Objects.Create("first", Vector3.zero, Quaternion.identity, mixingHost.transform));
            rig.Second = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(
                time.Objects.Create("second", Vector3.zero, Quaternion.identity, mixingHost.transform));
            return rig;
        }

        private static void Weigh(CinemachineMix_CharacterBase mixer, Rig rig)
        {
            Set(mixer, "m_mixingCamera", rig.Mixing);
            Set(mixer, "m_cameraWeightings", new[]
            {
                new CinemachineMix_CharacterBase.VirtualCameraWeighting { ValueToWeightingCurve = AnimationCurve.Linear(-1f, 0f, 1f, 1f), VirtualCamera = rig.First },
                new CinemachineMix_CharacterBase.VirtualCameraWeighting { ValueToWeightingCurve = AnimationCurve.Linear(-1f, 1f, 1f, 0f), VirtualCamera = rig.Second },
            });
        }

        private static void WithMixer<T>(Action<Owned, CharacterManager, Character, Rig, T> body) where T : CinemachineMix_CharacterBase =>
            WithManager((owned, manager, character) =>
            {
                owned.LeaseRow<TimeManager>();
                OwnedTimeScaledObjects time = owned.Lease(new OwnedTimeScaledObjects("lucid-mix"));
                Rig rig = NewRig(time);
                GameObject host = time.Objects.Create("mixer");
                host.SetActive(false);
                var mixer = host.AddComponent<T>();
                Weigh(mixer, rig);
                body(owned, manager, character, rig, mixer);
            });

        private static void SetCharacterProperty(Character character, string name, object value) =>
            typeof(Character).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetSetMethod(true).Invoke(character, new[] { value });

        private static void Adopt(CinemachineMix_CharacterBase mixer, CharacterManager manager, Character character)
        {
            SetCurrent(manager, character, true);
            Call(mixer, "Start");
        }

        // ---- CinemachineMix_CharacterBase (0x71E120..0x71E340) ----

        [Test]
        public void TheBaseWeighsEveryCameraByItsCurveAtTheTargetValue()
        {
            WithMixer<CinemachineMixCharacterProbe>((owned, manager, character, rig, mixer) =>
            {
                mixer.RunUpdateBlendWeightings(0.25f, 0.1f);
                Assert.That(rig.FirstWeight, Is.EqualTo(0.625f).Within(Tolerance));
                Assert.That(rig.SecondWeight, Is.EqualTo(0.375f).Within(Tolerance));
                mixer.RunUpdateBlendWeightings(-1f, 0.1f);
                Assert.That(rig.FirstWeight, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(rig.SecondWeight, Is.EqualTo(1f).Within(Tolerance));
            });
        }

        // 0x71E24C: the smoothed value is the Mathf.SmoothDamp step from the held value (see the class comment of
        // the source tests): towards 1 from rest at zero with smoothTime 1 and dt 0.1 it is 0.0172634, which the
        // first curve maps to (0.0172634 + 1) / 2.
        [Test]
        public void APositiveSmoothingTimeDampsTheValueBeforeWeighing()
        {
            WithMixer<CinemachineMixCharacterProbe>((owned, manager, character, rig, mixer) =>
            {
                Set(mixer, "m_smoothingDampTimeSeconds", 1f);
                mixer.RunUpdateBlendWeightings(1f, 0.1f);
                Assert.That(Get<float>(mixer, "m_currentBlendValue"), Is.EqualTo(0.0172634f).Within(Tolerance));
                Assert.That(rig.FirstWeight, Is.EqualTo(0.5086317f).Within(Tolerance));
                Assert.That(rig.SecondWeight, Is.EqualTo(0.4913683f).Within(Tolerance));
                Assert.That(Get<float>(mixer, "m_blendVelocity"), Is.EqualTo(0.3275789f).Within(Tolerance));
            });
        }

        [Test]
        public void ANonPositiveSmoothingTimeWeighsTheTargetDirectly()
        {
            WithMixer<CinemachineMixCharacterProbe>((owned, manager, character, rig, mixer) =>
            {
                Set(mixer, "m_smoothingDampTimeSeconds", -1f);
                mixer.RunUpdateBlendWeightings(0.5f, 0.1f);
                Assert.That(Get<float>(mixer, "m_currentBlendValue"), Is.EqualTo(0.5f));
                Assert.That(rig.FirstWeight, Is.EqualTo(0.75f).Within(Tolerance));
            });
        }

        [Test]
        public void StartAdoptsTheRegisteredCharacterManager()
        {
            WithMixer<CinemachineMixCharacterProbe>((owned, manager, character, rig, mixer) =>
            {
                Assert.That(mixer.Manager, Is.Null);
                Call(mixer, "Start");
                Assert.That(mixer.Manager, Is.SameAs(manager));
            });
        }

        [Test]
        public void TheBaseInternalUpdateChangesNoWeights()
        {
            WithMixer<CinemachineMixCharacterProbe>((owned, manager, character, rig, mixer) =>
            {
                rig.Mixing.SetWeight(rig.First, 0.3f);
                rig.Mixing.SetWeight(rig.Second, 0.7f);
                mixer.RunInternalUpdate(0.1f);
                Assert.That(rig.FirstWeight, Is.EqualTo(0.3f));
                Assert.That(rig.SecondWeight, Is.EqualTo(0.7f));
            });
        }

        // 0x71E2A0 asks CinemachineCore.Instance.IsLive about the mixing camera.
        [Test]
        public void TheMixerIsLiveOnlyWhileABrainShowsItsMixingCamera()
        {
            WithMixer<CinemachineMixCharacterProbe>((owned, manager, character, rig, mixer) =>
            {
                Assert.That(mixer.RunIsLive(), Is.False);
                using (rig.GoLive()) Assert.That(mixer.RunIsLive(), Is.True);
                Assert.That(mixer.RunIsLive(), Is.False);
            });
        }

        // ---- CinemachineMix_CharacterHalfPipe (0x71E340) ----

        [Test]
        public void AMixerThatIsNotLiveChangesNoWeights()
        {
            WithMixer<CinemachineMix_CharacterHalfPipe>((owned, manager, character, rig, mixer) =>
            {
                Adopt(mixer, manager, character);
                character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderEntrySide, CharacterAbility_HalfPipeSide.Right);
                rig.Mixing.SetWeight(rig.First, 0.3f);
                rig.Mixing.SetWeight(rig.Second, 0.7f);
                mixer.OnUpdate(0.1f);
                Assert.That(rig.FirstWeight, Is.EqualTo(0.3f));
                Assert.That(rig.SecondWeight, Is.EqualTo(0.7f));
            });
        }

        // 0x71E479 and 0x71E483: the stored entry side 1 (right) blends towards +1, 0 (left) towards -1; the
        // storage default is the left side.
        [Test]
        public void HalfPipeBlendsTowardsTheSideTheCharacterEnteredFrom()
        {
            WithMixer<CinemachineMix_CharacterHalfPipe>((owned, manager, character, rig, mixer) =>
            {
                Adopt(mixer, manager, character);
                using (rig.GoLive())
                {
                    character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderEntrySide, CharacterAbility_HalfPipeSide.Right);
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(1f).Within(Tolerance));
                    Assert.That(rig.SecondWeight, Is.EqualTo(0f).Within(Tolerance));
                    character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderEntrySide, CharacterAbility_HalfPipeSide.Left);
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0f).Within(Tolerance));
                    Assert.That(rig.SecondWeight, Is.EqualTo(1f).Within(Tolerance));
                    character.Storage.RemoveValue<CharacterAbility_HalfPipeSide>(ActorFSMKeys.HalfPipeColliderEntrySide);
                    rig.Mixing.SetWeight(rig.First, 0.5f);
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0f).Within(Tolerance), "an unset side reads the left default");
                }
            });
        }

        [Test]
        public void HalfPipeFaultsOnAStoredSideThatIsNeitherLeftNorRight()
        {
            WithMixer<CinemachineMix_CharacterHalfPipe>((owned, manager, character, rig, mixer) =>
            {
                Adopt(mixer, manager, character);
                using (rig.GoLive())
                {
                    character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderEntrySide, (CharacterAbility_HalfPipeSide)5);
                    rig.Mixing.SetWeight(rig.First, 0.3f);
                    Assert.Throws<ArgumentOutOfRangeException>(() => mixer.OnUpdate(0.1f));
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.3f));
                }
            });
        }

        // ---- CinemachineMix_CharacterLateral (0x71E4F0) ----

        [Test]
        public void LateralKeepsTheLastInputByDefault()
        {
            WithMixer<CinemachineMix_CharacterLateral>((owned, manager, character, rig, mixer) =>
            {
                Assert.That(Get<bool>(mixer, "m_preserveLastInput"), Is.True);
            });
        }

        // With direct input the value is ControllerMovement.x over the maximum lateral speed (0x71E551); otherwise
        // the x component of the stored ground input direction (0x71E593).
        [Test]
        public void LateralBlendsTheNormalisedHorizontalInput()
        {
            WithMixer<CinemachineMix_CharacterLateral>((owned, manager, character, rig, mixer) =>
            {
                Adopt(mixer, manager, character);
                Set(mixer, "m_maxLateralSpeed", 2f);
                using (rig.GoLive())
                {
                    Set(mixer, "m_useDirectControllerInput", true);
                    SetCharacterProperty(character, "ControllerMovement", new Vector2(1f, 0.5f));
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.75f).Within(Tolerance), "1 / 2");
                    Assert.That(rig.SecondWeight, Is.EqualTo(0.25f).Within(Tolerance));

                    Set(mixer, "m_useDirectControllerInput", false);
                    Set(mixer, "m_maxLateralSpeed", 4f);
                    character.Storage.SetValue(ActorFSMKeys.GroundInputDirection, new Vector3(-1f, 7f, 7f));
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.375f).Within(Tolerance), "-1 / 4");
                }
            });
        }

        // 0x71E63F: a preserved exact-zero input skips the update; every other value updates.
        [Test]
        public void LateralSkipsAnExactZeroOnlyWhilePreservingTheLastInput()
        {
            WithMixer<CinemachineMix_CharacterLateral>((owned, manager, character, rig, mixer) =>
            {
                Adopt(mixer, manager, character);
                Set(mixer, "m_maxLateralSpeed", 2f);
                Set(mixer, "m_useDirectControllerInput", true);
                using (rig.GoLive())
                {
                    SetCharacterProperty(character, "ControllerMovement", new Vector2(1f, 0f));
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.75f).Within(Tolerance));
                    SetCharacterProperty(character, "ControllerMovement", Vector2.zero);
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.75f).Within(Tolerance), "preserved");
                    Set(mixer, "m_preserveLastInput", false);
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.5f).Within(Tolerance), "not preserved");
                }
            });
        }

        [Test]
        public void LateralThatIsNotLiveChangesNoWeights()
        {
            WithMixer<CinemachineMix_CharacterLateral>((owned, manager, character, rig, mixer) =>
            {
                Adopt(mixer, manager, character);
                Set(mixer, "m_maxLateralSpeed", 2f);
                Set(mixer, "m_useDirectControllerInput", true);
                SetCharacterProperty(character, "ControllerMovement", new Vector2(1f, 0f));
                rig.Mixing.SetWeight(rig.First, 0.3f);
                mixer.OnUpdate(0.1f);
                Assert.That(rig.FirstWeight, Is.EqualTo(0.3f));
            });
        }

        // ---- CinemachineMix_CharacterSpeed (0x71E690) ----

        [Test]
        public void SpeedBlendsTheNormalisedScaledLocalVelocity()
        {
            WithMixer<CinemachineMix_CharacterSpeed>((owned, manager, character, rig, mixer) =>
            {
                Adopt(mixer, manager, character);
                SetActorProperty(character, "LocalVelocity", new Vector3(3f, 4f, 0f));
                Set(mixer, "m_maxSpeed", 10f);
                using (rig.GoLive())
                {
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.75f).Within(Tolerance), "magnitude 5 / 10");
                    Set(mixer, "m_useAbsoluteValues", true);
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.85f).Within(Tolerance), "sum 7 / 10");
                    Set(mixer, "m_velocityAxisMultiplier", new Vector3(1f, 0f, 1f));
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0.65f).Within(Tolerance), "sum 3 / 10");
                }
            });
        }

        [Test]
        public void SpeedClampsTheNormalisedValueToPlusOrMinusOne()
        {
            WithMixer<CinemachineMix_CharacterSpeed>((owned, manager, character, rig, mixer) =>
            {
                Adopt(mixer, manager, character);
                Set(mixer, "m_maxSpeed", 2f);
                using (rig.GoLive())
                {
                    SetActorProperty(character, "LocalVelocity", new Vector3(3f, 4f, 0f));
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(1f).Within(Tolerance), "5 / 2");
                    Set(mixer, "m_useAbsoluteValues", true);
                    SetActorProperty(character, "LocalVelocity", new Vector3(-3f, -4f, 0f));
                    mixer.OnUpdate(0.1f);
                    Assert.That(rig.FirstWeight, Is.EqualTo(0f).Within(Tolerance), "-7 / 2");
                }
            });
        }

        [Test]
        public void SpeedDefaultsToUnitAxisMultipliersAndThatIsNotLiveChangesNoWeights()
        {
            WithMixer<CinemachineMix_CharacterSpeed>((owned, manager, character, rig, mixer) =>
            {
                Assert.That(Get<Vector3>(mixer, "m_velocityAxisMultiplier"), Is.EqualTo(Vector3.one));
                Adopt(mixer, manager, character);
                Set(mixer, "m_maxSpeed", 10f);
                SetActorProperty(character, "LocalVelocity", new Vector3(3f, 4f, 0f));
                rig.Mixing.SetWeight(rig.First, 0.3f);
                mixer.OnUpdate(0.1f);
                Assert.That(rig.FirstWeight, Is.EqualTo(0.3f));
            });
        }
    }
}
