using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.TransformAnimationAccess;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.TransformMultiAxisRotator, TransformOscillator, TransformPanner,
    // TransformScaler (TypeDefs 0x02000820..0x0200082a, x86-64 0x604940..0x606811) and
    // ScaleToTarget (0x02000ad2, 0x6E96B0..0x6E9C49) on owned GameObjects. Start, Awake,
    // InternalUpdate, OnSynchronise, the OnEnable lambda and the scaling iterator are invoked
    // directly; the OnEnable and OnDisable bodies reach the shared TimeManager reference or
    // CoroutineUtils and are not run here.
    public sealed class OriginalTransformAnimatorTests
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const float Pi = 3.14159274f;

        [Test]
        public void DeclarationsKeepTheOriginalFlagsMembersAndAttributes()
        {
            Assert.That((int)typeof(TransformMultiAxisRotator).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(TransformMultiAxisRotator).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Assert.That(Fields(typeof(TransformMultiAxisRotator)), Is.EqualTo(new[]
            {
                "m_xSettings:AnimationSettings:0001", "m_ySettings:AnimationSettings:0001", "m_zSettings:AnimationSettings:0001",
                "m_xTimeSeconds:Single:0001", "m_yTimeSeconds:Single:0001", "m_zTimeSeconds:Single:0001",
                "m_initialRotation:Vector3:0001", "m_offset:Vector3:0001"
            }));
            Assert.That(AttributeRow(typeof(TransformMultiAxisRotator).GetField("m_xSettings", All)),
                Is.EqualTo("HeaderAttribute(Rotates object Per Axis Per Curve) SpaceAttribute(10) SerializeField()"));
            Assert.That(Methods(typeof(TransformMultiAxisRotator)), Is.EqualTo(new[] { "Start:0081", "OnSynchronise:00c4", "InternalUpdate:00c4", ".ctor:1886" }));
            Type settings = typeof(TransformMultiAxisRotator).GetNestedType("AnimationSettings", BindingFlags.NonPublic);
            Assert.That((int)settings.Attributes, Is.EqualTo(0x0010210b));
            Assert.That(Fields(settings), Is.EqualTo(new[]
            {
                "m_enableRotation:Boolean:0001", "m_rotationCuve:AnimationCurve:0001", "m_animationSpeed:Single:0001", "m_wrapMode:WrapMode:0001"
            }));
            Assert.That(Methods(settings), Is.EqualTo(new[] { "Initialise:0086", "get_RotationCurve:0886", "get_Enabled:0886", "get_AnimationSpeed:0886" }));

            Assert.That(Fields(typeof(TransformOscillator)), Is.EqualTo(new[]
            {
                "m_amplitude:Vector3:0001", "m_frequency:Vector3:0001", "m_offset:Vector3:0001", "m_useAbsoluteSine:Boolean:0001",
                "m_timerSeconds:Vector3:0001", "m_cachedLocalPosition:Vector3:0001", "m_shouldAnimate:Boolean:0001",
                "m_waitForUICoroutine:Coroutine:0001"
            }));
            Assert.That(Methods(typeof(TransformOscillator)), Is.EqualTo(new[]
                { "OnEnable:00c4", "OnDisable:00c4", "InternalUpdate:00c4", ".ctor:1886", "<OnEnable>b__8_0:0081" }));
            Assert.That(AttributeRow(typeof(TransformOscillator).GetMethod("<OnEnable>b__8_0", All)), Is.EqualTo("CompilerGeneratedAttribute()"));

            Assert.That(Fields(typeof(TransformPanner)), Is.EqualTo(new[]
            {
                "m_amountToPan:Vector2:0001", "m_speedToPan:Vector2:0001", "m_desiredX:Single:0001", "m_desiredY:Single:0001",
                "m_trackedX:Single:0001", "m_trackedY:Single:0001"
            }));
            Assert.That(AttributeRow(typeof(TransformPanner).GetField("m_amountToPan", All)), Is.EqualTo(
                "SpaceAttribute(10) SerializeField() HeaderAttribute(Moves an object by a fixed amount over a fixed length of time (also works with UI).)"));
            Assert.That(Methods(typeof(TransformPanner)), Is.EqualTo(new[] { "InternalUpdate:00c4", ".ctor:1886" }));

            Assert.That(AttributeRow(typeof(TransformScaler)), Is.EqualTo("Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)"));
            Assert.That(Fields(typeof(TransformScaler)), Is.EqualTo(new[]
            {
                "m_frequency:Vector3:0001", "m_offset:Vector3:0001", "m_minScale:Vector3:0001", "m_maxScale:Vector3:0001",
                "m_timerSeconds:Vector3:0001", "m_originalScale:Vector3:0001"
            }));
            Assert.That(Methods(typeof(TransformScaler)), Is.EqualTo(new[] { "OnEnable:00c4", "OnDisable:00c4", "InternalUpdate:00c4", ".ctor:1886" }));

            Assert.That(typeof(ScaleToTarget).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(Fields(typeof(ScaleToTarget)), Is.EqualTo(new[]
                { "m_curve:AnimationCurve:0001", "m_scaleTarget:Vector3:0001", "m_scaleOriginal:Vector3:0001" }));
            Assert.That(Methods(typeof(ScaleToTarget)), Is.EqualTo(new[]
                { "Awake:0081", "OnDisable:0081", "StartScaleToTarget:0086", "RevertScaleToOriginal:0086", "ScaleTransform:0081", ".ctor:1886" }));
            Type iterator = typeof(ScaleToTarget).GetNestedType("<ScaleTransform>d__7", BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null);
            Assert.That(Fields(iterator), Is.EqualTo(new[]
            {
                "<>1__state:Int32:0001", "<>2__current:Object:0001", "<>4__this:ScaleToTarget:0006", "start:Vector3:0006",
                "target:Vector3:0006", "<timeManager>5__2:TimeManager:0001", "<t>5__3:Single:0001", "<totalTime>5__4:Single:0001"
            }));
        }

        // Constructors: Oscillator 0x604DB0 (amplitude and frequency one, the rest zero), Panner
        // 0x6050F0 (Vector2.one twice), Scaler 0x6066C0 (frequency, minimum and maximum one).
        [Test]
        public void ConstructorsStoreTheNativeDefaults()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-defaults"))
            {
                var oscillator = owned.Add<TransformOscillator>("oscillator");
                Assert.That(Get(oscillator, "m_amplitude"), Is.EqualTo(Vector3.one));
                Assert.That(Get(oscillator, "m_frequency"), Is.EqualTo(Vector3.one));
                Assert.That(Get(oscillator, "m_offset"), Is.EqualTo(Vector3.zero));
                var panner = owned.Add<TransformPanner>("panner");
                Assert.That(Get(panner, "m_amountToPan"), Is.EqualTo(Vector2.one));
                Assert.That(Get(panner, "m_speedToPan"), Is.EqualTo(Vector2.one));
                var scaler = owned.Add<TransformScaler>("scaler");
                Assert.That(Get(scaler, "m_frequency"), Is.EqualTo(Vector3.one));
                Assert.That(Get(scaler, "m_offset"), Is.EqualTo(Vector3.zero));
                Assert.That(Get(scaler, "m_minScale"), Is.EqualTo(Vector3.one));
                Assert.That(Get(scaler, "m_maxScale"), Is.EqualTo(Vector3.one));
            }
        }

        // MultiAxis Start 0x604940 sets each axis curve's postWrapMode (0x604957, 0x60496a,
        // 0x604983) from its settings, then caches localEulerAngles (0x604997).
        [Test]
        public void MultiAxisStartSetsEachPostWrapModeAndTheInitialAngles()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-multi-start"))
            {
                var rotator = owned.Add<TransformMultiAxisRotator>("multi", Vector3.zero, Quaternion.Euler(0f, 30f, 0f));
                var curves = new AnimationCurve[3];
                string[] axes = { "m_xSettings", "m_ySettings", "m_zSettings" };
                WrapMode[] modes = { WrapMode.Loop, WrapMode.PingPong, WrapMode.ClampForever };
                for (int i = 0; i < 3; i++)
                {
                    curves[i] = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                    curves[i].preWrapMode = WrapMode.Once;
                    curves[i].postWrapMode = WrapMode.Once;
                    SetInStruct(rotator, axes[i], "m_rotationCuve", curves[i]);
                    SetInStruct(rotator, axes[i], "m_wrapMode", modes[i]);
                }
                Call(rotator, "Start");
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(curves[i].postWrapMode, Is.EqualTo(modes[i]), axes[i]);
                    Assert.That(curves[i].preWrapMode, Is.EqualTo(WrapMode.Once), axes[i] + " pre-wrap is untouched");
                }
                AreClose(new Vector3(0f, 30f, 0f), (Vector3)Get(rotator, "m_initialRotation"), "local euler angles");
            }
        }

        // MultiAxis OnSynchronise 0x6049E0 sets each axis time to its speed times the base
        // result; InternalUpdate 0x604A30 advances all three times by deltaTime * speed, samples
        // only the enabled axes into m_offset (0x604a85, 0x604aa6, 0x604acd) and sets
        // localEulerAngles = m_initialRotation + m_offset (0x604b12).
        [Test]
        public void MultiAxisAdvancesEveryAxisAndSamplesOnlyTheEnabledOnes()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-multi"))
            {
                var rotator = owned.Add<TransformMultiAxisRotator>("multi");
                string[] axes = { "m_xSettings", "m_ySettings", "m_zSettings" };
                for (int i = 0; i < 3; i++)
                {
                    SetInStruct(rotator, axes[i], "m_rotationCuve", AnimationCurve.Linear(0f, 0f, 10f, 100f));
                    SetInStruct(rotator, axes[i], "m_animationSpeed", i + 1f);
                }
                SetInStruct(rotator, "m_xSettings", "m_enableRotation", true);
                SetInStruct(rotator, "m_zSettings", "m_enableRotation", true);
                SetSynchronise(rotator, 1);
                Assert.That(Call(rotator, "OnSynchronise", 2f), Is.EqualTo(2f));
                Assert.That(new[] { Get(rotator, "m_xTimeSeconds"), Get(rotator, "m_yTimeSeconds"), Get(rotator, "m_zTimeSeconds") },
                    Is.EqualTo(new object[] { 2f, 4f, 6f }));

                Set(rotator, "m_xTimeSeconds", 0f);
                Set(rotator, "m_yTimeSeconds", 0f);
                Set(rotator, "m_zTimeSeconds", 0f);
                Set(rotator, "m_initialRotation", new Vector3(5f, 0f, 0f));
                Set(rotator, "m_offset", new Vector3(0f, 7f, 0f));
                Call(rotator, "InternalUpdate", 0.5f);
                Assert.That(new[] { Get(rotator, "m_xTimeSeconds"), Get(rotator, "m_yTimeSeconds"), Get(rotator, "m_zTimeSeconds") },
                    Is.EqualTo(new object[] { 0.5f, 1f, 1.5f }), "a disabled axis still advances");
                AreClose(new Vector3(5f, 7f, 15f), (Vector3)Get(rotator, "m_offset"), "y keeps its offset");
                AreClose(Quaternion.Euler(10f, 7f, 15f), rotator.transform.localRotation, "initial plus offset");
            }
        }

        // <OnEnable>b__8_0 0x604EA0, the WaitForUI callback: zero timer, then (op_Equality
        // 0x604f35 on transform) cache localPosition and start animating.
        [Test]
        public void OscillatorUiCallbackCachesThePositionAndStartsAnimating()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-oscillator-ui"))
            {
                var oscillator = owned.Add<TransformOscillator>("oscillator", new Vector3(1f, 2f, 3f), Quaternion.identity);
                Set(oscillator, "m_timerSeconds", new Vector3(4f, 5f, 6f));
                Call(oscillator, "<OnEnable>b__8_0");
                Assert.That(Get(oscillator, "m_timerSeconds"), Is.EqualTo(Vector3.zero));
                Assert.That(Get(oscillator, "m_cachedLocalPosition"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Get(oscillator, "m_shouldAnimate"), Is.True);
            }
        }

        // Oscillator InternalUpdate 0x604C50 returns unless m_shouldAnimate; timer +=
        // (deltaTime * pi) * frequency; phase = timer + offset * pi; three sinf calls; Vector3
        // Abs (0x604d50) when m_useAbsoluteSine; localPosition = cached + amplitude * sine.
        [Test]
        public void OscillatorOffsetsTheCachedPositionByASineWave()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-oscillator"))
            {
                var oscillator = owned.Add<TransformOscillator>("oscillator", new Vector3(9f, 9f, 9f), Quaternion.identity);
                Set(oscillator, "m_amplitude", new Vector3(2f, 3f, 4f));
                Set(oscillator, "m_frequency", new Vector3(1f, 0.5f, 2f));
                Set(oscillator, "m_offset", new Vector3(0f, 0.5f, 0f));
                Set(oscillator, "m_cachedLocalPosition", Vector3.one);

                Call(oscillator, "InternalUpdate", 0.25f);
                Assert.That(oscillator.transform.localPosition, Is.EqualTo(new Vector3(9f, 9f, 9f)), "not animating");
                Assert.That(Get(oscillator, "m_timerSeconds"), Is.EqualTo(Vector3.zero));

                Set(oscillator, "m_shouldAnimate", true);
                Call(oscillator, "InternalUpdate", 0.25f);
                AreClose(new Vector3(0.25f * Pi, 0.125f * Pi, 0.5f * Pi), (Vector3)Get(oscillator, "m_timerSeconds"), "timer");
                AreClose(new Vector3(2.4142136f, 3.7716386f, 5f), oscillator.transform.localPosition, "sine offsets");

                Set(oscillator, "m_timerSeconds", Vector3.zero);
                Set(oscillator, "m_offset", new Vector3(1f, 0f, 0f));
                Call(oscillator, "InternalUpdate", 0.25f);
                AreClose(new Vector3(-0.4142136f, 2.1480503f, 5f), oscillator.transform.localPosition, "signed sine");

                Set(oscillator, "m_timerSeconds", Vector3.zero);
                Set(oscillator, "m_useAbsoluteSine", true);
                Call(oscillator, "InternalUpdate", 0.25f);
                AreClose(new Vector3(2.4142136f, 2.1480503f, 5f), oscillator.transform.localPosition, "absolute sine");
            }
        }

        // The wrap tests (ucomiss against 2 pi at 0x29a73b8, jbe) use the phase, offset
        // included, and subtract 2 pi from the timer only; the sines use the unwrapped phase.
        [Test]
        public void OscillatorWrapsTheTimerOnThePhaseButSamplesTheUnwrappedPhase()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-oscillator-wrap"))
            {
                var oscillator = owned.Add<TransformOscillator>("oscillator");
                Set(oscillator, "m_amplitude", new Vector3(2f, 3f, 4f));
                Set(oscillator, "m_offset", new Vector3(0f, 0.2f, 0f));
                Set(oscillator, "m_cachedLocalPosition", Vector3.one);
                Set(oscillator, "m_timerSeconds", new Vector3(6.3f, 6f, 6.2f));
                Set(oscillator, "m_shouldAnimate", true);
                Call(oscillator, "InternalUpdate", 0f);
                AreClose(new Vector3(6.3f - 2f * Pi, 6f - 2f * Pi, 6.2f), (Vector3)Get(oscillator, "m_timerSeconds"), "x and y wrap, z stays below 2 pi");
                AreClose(new Vector3(1.0336278f, 2.0149661f, 0.6676424f), oscillator.transform.localPosition, "unwrapped sines", 1e-3f);
            }
        }

        // Panner InternalUpdate 0x604F70: desired = speed > 0 ? amount : -amount (blendvps),
        // tracked += speed * deltaTime, then tracked = fmodf(tracked, desired) only when
        // Math.Abs(desired) > float.Epsilon (0x29a6bf0, jbe); localPosition = (x, y, 0).
        [Test]
        public void PannerWrapsEachAxisAtTheSignedAmountAndZeroesZ()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-panner"))
            {
                var panner = owned.Add<TransformPanner>("panner", new Vector3(0f, 0f, 7f), Quaternion.identity);
                Set(panner, "m_amountToPan", new Vector2(5f, 4f));
                Set(panner, "m_speedToPan", new Vector2(2f, -3f));
                Call(panner, "InternalUpdate", 1f);
                Assert.That(Get(panner, "m_desiredX"), Is.EqualTo(5f));
                Assert.That(Get(panner, "m_desiredY"), Is.EqualTo(-4f));
                Assert.That(panner.transform.localPosition, Is.EqualTo(new Vector3(2f, -3f, 0f)));
                Call(panner, "InternalUpdate", 1f);
                Assert.That(panner.transform.localPosition, Is.EqualTo(new Vector3(4f, -2f, 0f)));
                Call(panner, "InternalUpdate", 1f);
                Assert.That(panner.transform.localPosition, Is.EqualTo(new Vector3(1f, -1f, 0f)));
            }
        }

        [Test]
        public void PannerNeverWrapsAnAmountWithinEpsilonAndNegatesForANonPositiveSpeed()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-panner-epsilon"))
            {
                var panner = owned.Add<TransformPanner>("panner");
                Set(panner, "m_amountToPan", new Vector2(float.Epsilon, 2.5f));
                Set(panner, "m_speedToPan", new Vector2(0f, 1f));
                Set(panner, "m_trackedX", 3f);
                Call(panner, "InternalUpdate", 4f);
                Assert.That(Get(panner, "m_desiredX"), Is.EqualTo(-float.Epsilon), "zero speed takes the negated amount");
                Assert.That(Get(panner, "m_trackedX"), Is.EqualTo(3f), "an epsilon amount is not a wrap");
                Assert.That(Get(panner, "m_trackedY"), Is.EqualTo(1.5f));

                Set(panner, "m_amountToPan", new Vector2(5f, 2.5f));
                Set(panner, "m_speedToPan", new Vector2(float.NaN, 1f));
                Call(panner, "InternalUpdate", 1f);
                Assert.That(Get(panner, "m_desiredX"), Is.EqualTo(-5f), "NaN speed takes the negated amount");
                Assert.That(float.IsNaN((float)Get(panner, "m_trackedX")), Is.True);
            }
        }

        // Scaler InternalUpdate 0x6064C0: the oscillator's timer, phase and wrap, then per axis
        // Mathf.Lerp(min, max, (1 + cos(phase)) * 0.5) (cosf 0x606576.., min/max/cmpnlt clamp)
        // into localScale. A zero phase is the maximum.
        [Test]
        public void ScalerBlendsTheScaleOnACosineWave()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-scaler"))
            {
                var scaler = owned.Add<TransformScaler>("scaler");
                Set(scaler, "m_minScale", new Vector3(1f, 2f, 3f));
                Set(scaler, "m_maxScale", new Vector3(3f, 6f, 9f));
                Call(scaler, "InternalUpdate", 0f);
                AreClose(new Vector3(3f, 6f, 9f), scaler.transform.localScale, "zero phase");

                Set(scaler, "m_offset", new Vector3(0f, 0.5f, 1f));
                Call(scaler, "InternalUpdate", 0f);
                AreClose(new Vector3(3f, 4f, 3f), scaler.transform.localScale, "offset phases");

                Call(scaler, "InternalUpdate", 0.5f);
                AreClose(new Vector3(0.5f * Pi, 0.5f * Pi, 0.5f * Pi), (Vector3)Get(scaler, "m_timerSeconds"), "timer");
                AreClose(new Vector3(2f, 2f, 6f), scaler.transform.localScale, "advanced phases");

                Set(scaler, "m_offset", new Vector3(0f, 0.5f, 0f));
                Set(scaler, "m_timerSeconds", new Vector3(0f, 1.6f * Pi, 0f));
                Call(scaler, "InternalUpdate", 0f);
                AreClose(new Vector3(0f, -0.4f * Pi, 0f), (Vector3)Get(scaler, "m_timerSeconds"), "y wraps on its phase");
                AreClose(new Vector3(3f, 5.9021130f, 9f), scaler.transform.localScale, "unwrapped cosine", 1e-3f);
            }
        }

        // ScaleToTarget Awake 0x6E96B0 caches localScale. <ScaleTransform>d__7.MoveNext
        // 0x6E99B0: GetSystem<TimeManager>() (0x6e9a3b), t = 0, totalTime = time of the last key
        // (0x6e9a83, 0x6e9a9e); while totalTime > t (ja 0x6e9ab3) it sets
        // localScale = Lerp(start, target, Evaluate(t) / totalTime), adds
        // GetDeltaTime(TimeCategory.System 0xc64a29f2) and yields null; then it sets the target.
        [Test]
        public void ScaleTransformLerpsOnTheCurveOverItsLastKeyTime()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-animator-scale-target"))
            using (var time = new OwnedSystemTime(owned.Objects, 3f))
            {
                var scale = owned.Add<ScaleToTarget>("scale");
                scale.transform.localScale = new Vector3(2f, 2f, 2f);
                Call(scale, "Awake");
                Assert.That(Get(scale, "m_scaleOriginal"), Is.EqualTo(new Vector3(2f, 2f, 2f)));

                Set(scale, "m_curve", AnimationCurve.Linear(0f, 0f, 2f, 1f));
                var routine = (IEnumerator)Call(scale, "ScaleTransform", Vector3.one, new Vector3(3f, 5f, 7f));
                time.Register();
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(Get(routine, "<timeManager>5__2"), Is.SameAs(time.Manager));
                Assert.That(Get(routine, "<totalTime>5__4"), Is.EqualTo(2f));
                Assert.That(scale.transform.localScale, Is.EqualTo(Vector3.one), "curve value 0");
                Assert.That(routine.Current, Is.Null);
                Assert.That(Get(routine, "<t>5__3"), Is.EqualTo(Time.deltaTime * 3f), "System delta time");

                Set(routine, "<t>5__3", 1f);
                Assert.That(routine.MoveNext(), Is.True);
                AreClose(new Vector3(1.5f, 2f, 2.5f), scale.transform.localScale, "a quarter of the way: 0.5 / 2");

                Set(scale, "m_curve", AnimationCurve.Linear(0f, 0f, 2f, 4f));
                Set(routine, "<t>5__3", 1.5f);
                Assert.That(routine.MoveNext(), Is.True);
                AreClose(new Vector3(3f, 5f, 7f), scale.transform.localScale, "3 / 2 is clamped by the lerp");

                Set(routine, "<t>5__3", 2f);
                scale.transform.localScale = Vector3.zero;
                Assert.That(routine.MoveNext(), Is.False, "t equal to the total time ends");
                Assert.That(scale.transform.localScale, Is.EqualTo(new Vector3(3f, 5f, 7f)));
            }
        }

        // A TimeManager whose System timescale is fixed, registered under its default name for
        // one case. It is uninitialised (no Awake or Reset, which need the shared
        // TimeCategoryConfiguration); the TimeExtensions category lookup it is read through is
        // leased. The row is admitted absent or idle and released after unregistering.
        private sealed class OwnedSystemTime : IDisposable
        {
            private static readonly FieldInfo LookupField = typeof(TimeExtensions).GetField("s_config_Internal", All);
            private readonly object m_priorLookup;
            private readonly VerificationFixtureOwnership.LifecycleRow m_row;
            private bool m_registered;
            public readonly TimeManager Manager;

            public OwnedSystemTime(OwnedWorldObjects objects, float timescale)
            {
                m_priorLookup = LookupField.GetValue(null);
                var category = objects.Own(ScriptableObject.CreateInstance<TimeCategoryObject>());
                Set(category, "m_guid", "lucid-transform-system-time");
                var lookup = objects.Own(ScriptableObject.CreateInstance<TimeCategoryLookup>());
                lookup.Dictionary.Add(TimeCategory.System, category);
                LookupField.SetValue(null, lookup);
                var setting = new TimeSetting();
                setting.SetCategory(category, timescale);
                var settings = new StackableData();
                settings.SetBaseValue((int)typeof(TimeManager).GetField("SettingId", All).GetRawConstantValue(), setting);
                Manager = (TimeManager)FormatterServices.GetUninitializedObject(typeof(TimeManager));
                Set(Manager, "m_timeSettings", settings);
                try { m_row = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>()); }
                catch
                {
                    LookupField.SetValue(null, m_priorLookup);
                    throw;
                }
            }

            public void Register()
            {
                ProcessManager.RegisterSystem(Manager, ProcessManager.GetDefaultName<TimeManager>());
                m_registered = true;
            }

            public void Dispose()
            {
                VerificationFixtureOwnership.RunThenCleanUp(
                    () => { if (m_registered) ProcessManager.UnregisterSystem(ProcessManager.GetDefaultName<TimeManager>()); },
                    () => m_row.Dispose(),
                    () => LookupField.SetValue(null, m_priorLookup));
            }
        }
    }
}
