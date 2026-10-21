using System;
using System.Collections;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using static ProjectLucid.Tests.TimelineTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SequencedMagnetism (TypeDef 0x0200061d with its iterator 0x0200061e, x86-64
    // 0x569340..0x5699EF), SequencedMaterial (0x0200061f/0x02000620, 0x5699F0..0x569E1F) with SequencedMaterialAlpha
    // (0x02000621, 0x569E20) and SequencedMaterialProperty (0x02000622, 0x569EA0), and SequencedPostProcessing
    // (0x02000626/0x02000627, 0x56A610..0x56A95F). Components are real, on owned inactive hosts that never wake. The
    // effects run through their public PlayEffect entry (open input gate); the coroutine bodies are stepped by
    // hand, because Edit Mode never ticks coroutines and an inactive host cannot start one. Time is the real
    // TimeManager component registered into its absent row; Time.fixedDeltaTime is set to 0.5 seconds (exact in
    // binary) for the case and restored.
    public sealed class SequencedMotionAndMaterialTests
    {
        private const float FixedStep = 0.5f;

        private static void Trigger(SequencedEffect effect, IEffectData data, Action onComplete) => effect.PlayEffect(in data, onComplete);

        // An Effects-category TimeManager at the given scale, with the fixed step set for the case.
        private static void WithClock(float scale, Action<Owned, TimeManager> body) => Owned.Run(owned =>
        {
            TimeManager manager = owned.Register(NewTimeManager(owned, TimeCategory.Effects, scale));
            float prior = Time.fixedDeltaTime;
            owned.Lease(new ReleaseAction(() => Time.fixedDeltaTime = prior));
            Time.fixedDeltaTime = FixedStep;
            body(owned, manager);
        });

        private static Transform NewTarget(Owned owned, Vector3 position, Quaternion rotation)
        {
            Transform target = owned.Host("Lucid magnetism target", false).transform;
            target.SetPositionAndRotation(position, rotation);
            return target;
        }

        private static void AssertPosition(Transform transform, Vector3 expected, string message)
        {
            Assert.That(transform.position.x, Is.EqualTo(expected.x).Within(1e-4f), message + " x");
            Assert.That(transform.position.y, Is.EqualTo(expected.y).Within(1e-4f), message + " y");
            Assert.That(transform.position.z, Is.EqualTo(expected.z).Within(1e-4f), message + " z");
        }

        // A magnetism effect on an inactive host; PlayEffect fetches the time manager (0x5693ad) and, because the
        // host is inactive, completes at once. The count is then cleared so only the stepped chase completes it.
        private static IEnumerator StartChase(Owned owned, Vector3 start, TargetedEffect data, out SequencedMagnetism effect,
            out Func<int> completions, Action<SerializedObject> author = null)
        {
            GameObject host = owned.Host("Lucid magnetism", false);
            host.transform.position = start;
            effect = host.AddComponent<SequencedMagnetism>();
            if (author != null) Author(effect, author);
            int completed = 0;
            Trigger(effect, data, () => completed++);
            Assert.That(completed, Is.EqualTo(1), "an inactive host completes without a chase");
            completed = 0;
            completions = () => completed;
            return (IEnumerator)Call(effect, "MoveToTransform", data);
        }

        // The initialisers at 0x569550 store speed 10 and minimum distance 1 (the pair at 0x29a78f0) and a
        // timeout of 2 seconds (0x40000000).
        [Test]
        public void MagnetismDefaultsToTenSpeedOneDistanceAndTwoSeconds()
        {
            WithClock(1f, (owned, manager) =>
            {
                var effect = owned.Host("Lucid magnetism", false).AddComponent<SequencedMagnetism>();
                Assert.That((Get<float>(effect, "m_speed"), Get<float>(effect, "m_minDistance"), Get<float>(effect, "m_timeoutSeconds")),
                    Is.EqualTo((10f, 1f, 2f)));
            });
        }

        // OnEffectTriggered 0x569340: an inactive host (get_activeInHierarchy 0x569408) completes through the
        // stored callback instead of starting the coroutine, so nothing moves.
        [Test]
        public void MagnetismOnAnInactiveHostCompletesWithoutMoving()
        {
            WithClock(1f, (owned, manager) =>
            {
                GameObject host = owned.Host("Lucid magnetism", false);
                host.transform.position = new Vector3(5f, 6f, 7f);
                var effect = host.AddComponent<SequencedMagnetism>();
                int completed = 0;
                Trigger(effect, new TargetedEffect { Target = NewTarget(owned, Vector3.zero, Quaternion.identity) }, () => completed++);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(host.transform.position, Is.EqualTo(new Vector3(5f, 6f, 7f)));
            });
        }

        // MoveNext 0x569590 toward a target 100 units away along Z: each fixed step moves speed * 0.5 and then
        // adds one to the speed (movlps pair at 0x56990b), so the steps are 5, 5.5, 6 and 6.5; the timeout falls by
        // 0.5 per step from 2 and the chase is judged complete once it reaches exactly zero (ucomiss/cmpleps).
        [Test]
        public void TheChaseSpeedsUpByOnePerStepUntilTheTimeoutEnds()
        {
            WithClock(1f, (owned, manager) =>
            {
                Transform target = NewTarget(owned, new Vector3(0f, 0f, 100f), Quaternion.identity);
                var data = new TargetedEffect { Target = target };
                IEnumerator steps = StartChase(owned, Vector3.zero, data, out SequencedMagnetism effect, out Func<int> completed);
                foreach (float z in new[] { 5f, 10.5f, 16.5f, 23f })
                {
                    Assert.That(steps.MoveNext(), Is.True);
                    Assert.That(steps.Current, Is.InstanceOf<WaitForFixedUpdate>());
                    AssertPosition(effect.transform, new Vector3(0f, 0f, z), "after the step to " + z);
                    Assert.That(completed(), Is.EqualTo(0));
                }
                Assert.That(steps.MoveNext(), Is.False, "the timeout ended the chase");
                Assert.That(completed(), Is.EqualTo(1));
                AssertPosition(effect.transform, new Vector3(0f, 0f, 23f), "no move after the last step");
            });
        }

        // The goal is target.position + target.up * Offset.y (0x5696f6..0x56977d: Transform.up and the +0x14 float);
        // the offset's X and Z are not used. A target turned -90 degrees about Z has up = +X, so the goal is (2,0,0).
        // MoveTowards stops exactly on the goal once it is within the step (5 then 5.5 units from 10).
        [Test]
        public void TheChaseGoesToThePointAlongTheTargetsUpAxisByTheOffsetsY()
        {
            WithClock(1f, (owned, manager) =>
            {
                Transform target = NewTarget(owned, Vector3.zero, Quaternion.Euler(0f, 0f, -90f));
                var data = new TargetedEffect { Target = target, Offset = new Vector3(7f, 2f, 9f) };
                IEnumerator steps = StartChase(owned, new Vector3(10f, 0f, 0f), data, out SequencedMagnetism effect, out Func<int> completed);
                Assert.That(steps.MoveNext(), Is.True);
                AssertPosition(effect.transform, new Vector3(5f, 0f, 0f), "first step");
                Assert.That(steps.MoveNext(), Is.True);
                AssertPosition(effect.transform, new Vector3(2f, 0f, 0f), "arrived at the goal");
                Assert.That(completed(), Is.EqualTo(0), "arrival is reported after the yield");
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(completed(), Is.EqualTo(1));
            });
        }

        // Arrival (0x569946 cmpltps against [r14+0x50]) compares the SQUARED distance to the minimum distance
        // itself. With 0.7 left, 0.49 < 0.5 ends the chase after the first step while 0.7 < 0.5 would not; with 3
        // left and a minimum of 4, 9 >= 4 keeps chasing while 3 < 4 would stop.
        [Test]
        public void ArrivalComparesTheSquaredDistanceWithTheMinimumDistance()
        {
            WithClock(1f, (owned, manager) =>
            {
                IEnumerator near = StartChase(owned, Vector3.zero,
                    new TargetedEffect { Target = NewTarget(owned, new Vector3(0f, 0f, 5.7f), Quaternion.identity) },
                    out _, out Func<int> nearCompleted, serialized => serialized.FindProperty("m_minDistance").floatValue = 0.5f);
                Assert.That(near.MoveNext(), Is.True);
                Assert.That(near.MoveNext(), Is.False, "0.49 < 0.5");
                Assert.That(nearCompleted(), Is.EqualTo(1));

                IEnumerator far = StartChase(owned, Vector3.zero,
                    new TargetedEffect { Target = NewTarget(owned, new Vector3(0f, 0f, 8f), Quaternion.identity) },
                    out _, out Func<int> farCompleted, serialized => serialized.FindProperty("m_minDistance").floatValue = 4f);
                Assert.That(far.MoveNext(), Is.True);
                Assert.That(far.MoveNext(), Is.True, "9 >= 4");
                Assert.That(farCompleted(), Is.EqualTo(0));
            });
        }

        // The timeout test is "less than or equal to zero" (cmpleps against zero): exactly zero ends the chase.
        [Test]
        public void ATimeoutThatReachesExactlyZeroEndsTheChase()
        {
            WithClock(1f, (owned, manager) =>
            {
                IEnumerator steps = StartChase(owned, Vector3.zero,
                    new TargetedEffect { Target = NewTarget(owned, new Vector3(0f, 0f, 100f), Quaternion.identity) },
                    out SequencedMagnetism effect, out Func<int> completed,
                    serialized => serialized.FindProperty("m_timeoutSeconds").floatValue = 0.5f);
                Assert.That(steps.MoveNext(), Is.True);
                AssertPosition(effect.transform, new Vector3(0f, 0f, 5f), "one step");
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(completed(), Is.EqualTo(1));
            });
        }

        // MoveNext 0x569671 resumes by or-ing "the target is gone" (UnityEngine.Object op_Equality 0x5696a9) into the
        // completion flag, so a destroyed target ends the chase without a further move.
        [Test]
        public void ADestroyedTargetEndsTheChaseAfterTheNextResume()
        {
            WithClock(1f, (owned, manager) =>
            {
                Transform target = NewTarget(owned, new Vector3(0f, 0f, 100f), Quaternion.identity);
                IEnumerator steps = StartChase(owned, Vector3.zero, new TargetedEffect { Target = target },
                    out SequencedMagnetism effect, out Func<int> completed);
                Assert.That(steps.MoveNext(), Is.True);
                Object.DestroyImmediate(target.gameObject);
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(completed(), Is.EqualTo(1));
                AssertPosition(effect.transform, new Vector3(0f, 0f, 5f), "only the first step moved");
            });
        }

        private static Material NewMaterial(Owned owned)
        {
            Shader shader = Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null, "the built-in Standard shader");
            return owned.Track(new Material(shader));
        }

        private static T NewMaterialEffect<T>(Owned owned, Material material, float blendTime, float start, float end) where T : SequencedMaterial
        {
            var effect = owned.Host("Lucid sequenced material", false).AddComponent<T>();
            Author(effect, serialized =>
            {
                serialized.FindProperty("m_material").objectReferenceValue = material;
                serialized.FindProperty("m_blendTime").floatValue = blendTime;
                serialized.FindProperty("m_blendValueStart").floatValue = start;
                serialized.FindProperty("m_blendValueEnd").floatValue = end;
            });
            return effect;
        }

        private static SequencedMaterialProperty NewPropertyEffect(Owned owned, Material material, float blendTime, float start, float end)
        {
            SequencedMaterialProperty effect = NewMaterialEffect<SequencedMaterialProperty>(owned, material, blendTime, start, end);
            Author(effect, serialized => serialized.FindProperty("m_propertyName").stringValue = "_BumpScale");
            Call(effect, "Awake");
            return effect;
        }

        // Awake 0x569EA0 hashes the property name (Shader.PropertyToID) and keeps the material's current value as the
        // reset value; ResetProperties 0x569B30 writes it back through the virtual SetBlendValue (0x569EE0).
        [Test]
        public void PropertyAwakeCapturesTheCurrentValueAndResetPropertiesRestoresIt()
        {
            WithClock(1f, (owned, manager) =>
            {
                Material material = NewMaterial(owned);
                material.SetFloat("_BumpScale", 0.25f);
                SequencedMaterialProperty effect = NewPropertyEffect(owned, material, 1f, 0f, 1f);
                Assert.That(Get<float>(effect, "m_blendValueReset"), Is.EqualTo(0.25f));
                Assert.That(Get<int>(effect, "m_propertyHash"), Is.EqualTo(Shader.PropertyToID("_BumpScale")));
                material.SetFloat("_BumpScale", 9f);
                effect.ResetProperties();
                Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(0.25f));
            });
        }

        // OnEffectTriggered 0x5699F0: a blend time of exactly zero (ucomiss/jne/jp) applies the end value and
        // completes at once, without waiting for any step.
        [Test]
        public void AZeroBlendTimeAppliesTheEndValueAndCompletesAtOnce()
        {
            WithClock(1f, (owned, manager) =>
            {
                Material material = NewMaterial(owned);
                SequencedMaterialProperty effect = NewPropertyEffect(owned, material, 0f, 3f, 7f);
                int completed = 0;
                Trigger(effect, null, () => completed++);
                Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(7f));
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // A fresh blending effect whose completion callback has been stored by an earlier zero-length trigger.
        private static SequencedMaterialProperty ArmedPropertyEffect(Owned owned, Material material, float blendTime, float start, float end, Action onComplete)
        {
            SequencedMaterialProperty effect = NewPropertyEffect(owned, material, 0f, start, end);
            Trigger(effect, null, onComplete);
            Author(effect, serialized => serialized.FindProperty("m_blendTime").floatValue = blendTime);
            return effect;
        }

        // MoveNext 0x569C00 with a blend time of 2 from 10 to 20 and a 0.5 step: the value is lerp(start, end, time /
        // blend) = 10, 12.5, 15, 17.5 (times 0, 0.5, 1, 1.5), then, when 2 > 2 fails, the end value is applied and
        // the effect completes.
        [Test]
        public void TheBlendAnimatesFromStartToEndOneFixedStepAtATime()
        {
            WithClock(1f, (owned, manager) =>
            {
                Material material = NewMaterial(owned);
                int completed = 0;
                SequencedMaterialProperty effect = ArmedPropertyEffect(owned, material, 2f, 10f, 20f, () => completed++);
                completed = 0;
                var steps = (IEnumerator)Call(effect, "WaitForAnimation");
                foreach (float value in new[] { 10f, 12.5f, 15f, 17.5f })
                {
                    Assert.That(steps.MoveNext(), Is.True);
                    Assert.That(steps.Current, Is.InstanceOf<WaitForFixedUpdate>());
                    Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(value).Within(1e-5f));
                    Assert.That(completed, Is.EqualTo(0));
                }
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(20f));
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // The loop test is blend > time (ucomiss/ja 0x569cc5), false for a negative or NaN blend time, so the first
        // step applies the end value, completes and yields nothing. Only an exactly zero blend time is handled
        // by OnEffectTriggered.
        [Test]
        public void ANegativeOrNaNBlendTimeFinishesOnTheFirstStep()
        {
            foreach (float blendTime in new[] { -1f, float.NaN })
                WithClock(1f, (owned, manager) =>
                {
                    Material material = NewMaterial(owned);
                    int completed = 0;
                    SequencedMaterialProperty effect = ArmedPropertyEffect(owned, material, blendTime, 10f, 20f, () => completed++);
                    completed = 0;
                    var steps = (IEnumerator)Call(effect, "WaitForAnimation");
                    Assert.That(steps.MoveNext(), Is.False, "blend time " + blendTime);
                    Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(20f));
                    Assert.That(completed, Is.EqualTo(1));
                });
        }

        // SequencedMaterialAlpha: Awake 0x569E20 keeps Material.color.a as the reset value (extractps lane 1) and
        // SetBlendValue 0x569E50 inserts the value into the alpha lane only.
        [Test]
        public void AlphaChangesOnlyTheAlphaChannelAndResetRestoresIt()
        {
            WithClock(1f, (owned, manager) =>
            {
                Material material = NewMaterial(owned);
                material.color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
                SequencedMaterialAlpha effect = NewMaterialEffect<SequencedMaterialAlpha>(owned, material, 0f, 0f, 0.9f);
                Call(effect, "Awake");
                Assert.That(Get<float>(effect, "m_blendValueReset"), Is.EqualTo(0.4f).Within(1e-6f));
                int completed = 0;
                Trigger(effect, null, () => completed++);
                Assert.That(completed, Is.EqualTo(1));
                Color applied = material.color;
                Assert.That(applied.r, Is.EqualTo(0.1f).Within(1e-6f));
                Assert.That(applied.g, Is.EqualTo(0.2f).Within(1e-6f));
                Assert.That(applied.b, Is.EqualTo(0.3f).Within(1e-6f));
                Assert.That(applied.a, Is.EqualTo(0.9f).Within(1e-6f));
                effect.ResetProperties();
                Assert.That(material.color.a, Is.EqualTo(0.4f).Within(1e-6f));
                Assert.That(material.color.r, Is.EqualTo(0.1f).Within(1e-6f));
            });
        }

        // The post processing coroutine reads the delta time of the Effects category (0x56a899, TimeExtensions.GetDeltaTime),
        // which is Time.deltaTime scaled; a scale of a million makes one resume pass any curve a second long.
        private static void WithPostProcessing(AnimationCurve curve, Action<SequencedPostProcessing, Volume, Func<int>> body) =>
            WithClock(1000000f, (owned, manager) =>
            {
                Assert.That(manager.GetDeltaTime(TimeCategory.Effects), Is.GreaterThan(1f), "precondition: one resume passes the curve");
                var volume = owned.Host("Lucid post process volume", false).AddComponent<Volume>();
                var effect = owned.Host("Lucid sequenced post processing", false).AddComponent<SequencedPostProcessing>();
                Author(effect, serialized =>
                {
                    serialized.FindProperty("m_curve").animationCurveValue = curve;
                    serialized.FindProperty("m_postProcessVolume").objectReferenceValue = volume;
                });
                // OnEffectTriggered (0x56a61a) also runs the coroutine on the shared coroutine host, which this case must
                // not create, so the two values it would set are written directly: the manager it fetches and the callback.
                int completed = 0;
                Set(effect, "m_timeManager", manager);
                Set(effect, "m_onComplete", (Action)(() => completed++));
                body(effect, volume, () => completed);
            });

        // MoveNext 0x56A7A0: the first resume samples the curve at zero into Volume.weight (field +0x2c) and yields
        // null; the next resume adds the (huge) delta, leaves the loop, sets the final key's value and completes.
        [Test]
        public void TheWeightFollowsTheCurveThenSettlesOnTheFinalKey()
        {
            WithPostProcessing(AnimationCurve.Linear(0f, 0.2f, 1f, 0.8f), (effect, volume, completed) =>
            {
                var steps = (IEnumerator)Call(effect, "ScalePostProcessWeight");
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.Null);
                Assert.That(volume.weight, Is.EqualTo(0.2f).Within(1e-6f));
                Assert.That(completed(), Is.EqualTo(0));
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(volume.weight, Is.EqualTo(0.8f).Within(1e-6f));
                Assert.That(completed(), Is.EqualTo(1));
            });
        }

        // The loop runs while elapsed <= end (ucomiss/jb 0x56a82f): a curve whose last key is at time zero is still
        // sampled once before the final value is applied.
        [Test]
        public void ACurveEndingAtTimeZeroIsStillSampledOnce()
        {
            WithPostProcessing(new AnimationCurve(new Keyframe(0f, 0.5f)), (effect, volume, completed) =>
            {
                volume.weight = 0.9f;
                var steps = (IEnumerator)Call(effect, "ScalePostProcessWeight");
                Assert.That(steps.MoveNext(), Is.True, "elapsed 0 <= end 0");
                Assert.That(volume.weight, Is.EqualTo(0.5f).Within(1e-6f));
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(volume.weight, Is.EqualTo(0.5f).Within(1e-6f));
                Assert.That(completed(), Is.EqualTo(1));
            });
        }
    }
}
