using System;
using System.Reflection;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TimeScaledTestHarness;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime time-scaled components: TimeScaledShader (TypeDef 0x020007ff, x86-64 0x5FB630..0x5FB6BF),
    // TimeScaledCinemachineCore (0x020007fb, 0x5F9D20..0x5F9F9F), TimeScaledBlendShape (0x020007f8, 0x5F9410..0x5F977F),
    // TimeScaledAnimator (0x020007f7, 0x5F90F0..0x5F940F) and TimeScaledAnimation (0x020007f6, 0x5F8A80..0x5F90EF).
    // Components are real, on owned inactive hosts that never wake; the TimeManager holds the Environment category at
    // scale 2 (TimeScaledTestHarness). Updates go through the public OnUpdate, which the engine would call.
    public sealed class OriginalTimeScaledComponentTests
    {
        private const string ShaderProperty = "_LucidTimeScaledShaderTestTime";

        private static T Static<T>(Type type, string name) =>
            (T)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // 0x5fb6a4 stores 36000 (0x470ca000) into m_maxTime before the base constructor runs.
        [Test]
        public void EmissionCheck_ShaderDefaultsAndEditorTypeKeepTheirDeclarations()
        {
            WithManager(scene =>
            {
                var shader = Add<TimeScaledShader>(scene, scene.Owned.Host("Lucid owned time scaled shader", false));
                Assert.That(Get<float>(shader, "m_maxTime"), Is.EqualTo(36000f));
                Assert.That(Get<string>(shader, "m_timePropertyName"), Is.Null);
            });
            AssertMembers(typeof(TimeScaledShaderEditor), 0x00100001,
                new[] { ("m_timePropertyNames", 0x0011), ("m_maxTime", 0x8051) },
                new[] { (".ctor", 0x1886), (".cctor", 0x1891) });
            Assert.That(Static<string[]>(typeof(TimeScaledShaderEditor), "m_timePropertyNames"),
                Is.EqualTo(new[] { "_HLShaderTimeUI", "_HLShaderTimeGame" }), "0x5fb72d / 0x5fb744 store the UI name first");
        }

        // 0x5fb650: m_time += deltaTime, then the property is set to the (possibly reset) time.
        [Test]
        public void TheShaderPublishesTheAccumulatedScaledTime()
        {
            WithManager(scene =>
            {
                var shader = Add<TimeScaledShader>(scene, scene.Owned.Host("Lucid owned time scaled shader", false));
                Set(shader, "m_timePropertyName", ShaderProperty); // serialized in the original; no other way to name it
                try
                {
                    shader.OnUpdate(1.5f);
                    Assert.That(Shader.GetGlobalFloat(ShaderProperty), Is.EqualTo(1.5f));
                    shader.OnUpdate(1f);
                    Assert.That(Shader.GetGlobalFloat(ShaderProperty), Is.EqualTo(2.5f));
                }
                finally { Shader.SetGlobalFloat(ShaderProperty, 0f); }
            });
        }

        // 0x5fb663: ucomiss time, max; jbe skips the reset, so only a time strictly above the maximum restarts it.
        [Test]
        public void TheShaderRestartsFromZeroOnlyAfterPassingTheMaximum()
        {
            WithManager(scene =>
            {
                var shader = Add<TimeScaledShader>(scene, scene.Owned.Host("Lucid owned time scaled shader", false));
                Set(shader, "m_timePropertyName", ShaderProperty); // serialized in the original
                Set(shader, "m_maxTime", 10f); // serialized in the original
                try
                {
                    shader.OnUpdate(10f);
                    Assert.That(Shader.GetGlobalFloat(ShaderProperty), Is.EqualTo(10f), "equal to the maximum is not past it");
                    shader.OnUpdate(1f);
                    Assert.That(Shader.GetGlobalFloat(ShaderProperty), Is.EqualTo(0f), "11 passes 10: reset, then the zero is published");
                    Assert.That(Get<float>(shader, "m_time"), Is.EqualTo(0f));
                    shader.OnUpdate(3f);
                    Assert.That(Shader.GetGlobalFloat(ShaderProperty), Is.EqualTo(3f), "counting restarts from zero");
                }
                finally { Shader.SetGlobalFloat(ShaderProperty, 0f); }
            });
        }

        // 0x5fb630: the base ResetSynchronise (0x5fa360), then m_time = 0.
        [Test]
        public void TheShaderResetSynchroniseZeroesTheAccumulatedTime()
        {
            WithManager(scene =>
            {
                var shader = Add<TimeScaledShader>(scene, scene.Owned.Host("Lucid owned time scaled shader", false));
                Set(shader, "m_timePropertyName", ShaderProperty); // serialized in the original
                try
                {
                    shader.OnUpdate(4f);
                    Assert.That(Get<float>(shader, "m_time"), Is.EqualTo(4f));
                    shader.ResetSynchronise();
                    Assert.That(Get<float>(shader, "m_time"), Is.EqualTo(0f));
                }
                finally { Shader.SetGlobalFloat(ShaderProperty, 0f); }
            });
        }

        // 0x5fb663: an unordered compare (NaN maximum) also takes jbe, so the time is published without a reset.
        [Test]
        public void ANaNMaximumNeverRestartsTheShader()
        {
            WithManager(scene =>
            {
                var shader = Add<TimeScaledShader>(scene, scene.Owned.Host("Lucid owned time scaled shader", false));
                Set(shader, "m_timePropertyName", ShaderProperty); // serialized in the original
                Set(shader, "m_maxTime", float.NaN); // serialized in the original
                try
                {
                    shader.OnUpdate(1f);
                    Assert.That(Shader.GetGlobalFloat(ShaderProperty), Is.EqualTo(1f));
                    Assert.That(Get<float>(shader, "m_time"), Is.EqualTo(1f));
                }
                finally { Shader.SetGlobalFloat(ShaderProperty, 0f); }
            });
        }

        private static void WithCinemachineCore(Action<Scene, TimeScaledCinemachineCore, TimeScaledCinemachineCore> body) => WithManager(scene =>
        {
            FieldInfo instance = typeof(TimeScaledCinemachineCore).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object priorInstance = instance.GetValue(null);
            float priorDelta = CinemachineCore.UniformDeltaTimeOverride;
            float priorTime = CinemachineCore.CurrentTimeOverride;
            scene.Owned.Lease(new ReleaseAction(() =>
            {
                instance.SetValue(null, priorInstance);
                CinemachineCore.UniformDeltaTimeOverride = priorDelta;
                CinemachineCore.CurrentTimeOverride = priorTime;
            }));
            instance.SetValue(null, null);
            var first = Add<TimeScaledCinemachineCore>(scene, scene.Owned.Host("Lucid owned Cinemachine core one", false));
            var second = Add<TimeScaledCinemachineCore>(scene, scene.Owned.Host("Lucid owned Cinemachine core two", false));
            body(scene, first, second);
        });

        // 0x5f9d6b..0x5f9d94: an existing instance (Unity op_Inequality) returns before anything else, so only the
        // first component becomes the instance and subscribes.
        [Test]
        public void OnlyTheFirstCinemachineCoreBecomesTheInstanceAndSubscribes()
        {
            WithCinemachineCore((scene, first, second) =>
            {
                Call(first, "Initialise", scene.Manager);
                Assert.That(Static<TimeScaledCinemachineCore>(typeof(TimeScaledCinemachineCore), "s_instance"), Is.SameAs(first));
                Assert.That(Subscribed(scene, UpdateOn.FixedUpdate), Is.EqualTo(1));
                Call(second, "Initialise", scene.Manager);
                Assert.That(Static<TimeScaledCinemachineCore>(typeof(TimeScaledCinemachineCore), "s_instance"), Is.SameAs(first));
                Assert.That(Subscribed(scene, UpdateOn.FixedUpdate), Is.EqualTo(1), "the second component is not subscribed");
            });
        }

        // 0x5f9d8d: the guard is Unity's op_Inequality, so a destroyed instance lets the next component take over.
        [Test]
        public void ADestroyedCinemachineCoreInstanceIsReplaced()
        {
            WithCinemachineCore((scene, first, second) =>
            {
                Call(first, "Initialise", scene.Manager);
                UnityEngine.Object.DestroyImmediate(first); // OnDisable never ran on the inactive host, so the static still holds it
                Call(second, "Initialise", scene.Manager);
                Assert.That(Static<TimeScaledCinemachineCore>(typeof(TimeScaledCinemachineCore), "s_instance"), Is.SameAs(second));
            });
        }

        // 0x5f9e49..0x5f9e88: the overall time grows by the delta; Cinemachine's uniform delta takes the delta and
        // its current time the overall time (static fields +0x20 and +0x24).
        [Test]
        public void TheCinemachineCoreOverridesCinemachineTimeWithTheScaledTime()
        {
            WithCinemachineCore((scene, first, second) =>
            {
                first.OnUpdate(0.25f);
                Assert.That((CinemachineCore.UniformDeltaTimeOverride, CinemachineCore.CurrentTimeOverride), Is.EqualTo((0.25f, 0.25f)));
                first.OnUpdate(0.5f);
                Assert.That((CinemachineCore.UniformDeltaTimeOverride, CinemachineCore.CurrentTimeOverride), Is.EqualTo((0.5f, 0.75f)));
            });
        }

        // 0x5f9efd: one 8-byte store of 0xbf800000_bf800000 resets both overrides to -1; then s_instance = null.
        [Test]
        public void DisablingTheCinemachineCoreRestoresTheOverridesAndFreesTheInstance()
        {
            WithCinemachineCore((scene, first, second) =>
            {
                Call(first, "Initialise", scene.Manager);
                first.OnUpdate(0.25f);
                Call(first, "OnDisable");
                Assert.That((CinemachineCore.UniformDeltaTimeOverride, CinemachineCore.CurrentTimeOverride), Is.EqualTo((-1f, -1f)));
                Assert.That(Static<object>(typeof(TimeScaledCinemachineCore), "s_instance"), Is.Null);
                Assert.That(Subscribed(scene, UpdateOn.FixedUpdate), Is.EqualTo(0), "the base OnDisable unsubscribed it");
                Call(second, "Initialise", scene.Manager);
                Assert.That(Static<TimeScaledCinemachineCore>(typeof(TimeScaledCinemachineCore), "s_instance"), Is.SameAs(second), "a later component can take over");
            });
        }

        private static void WithBlendShape(Action<Scene, TimeScaledBlendShape, SkinnedMeshRenderer> body, bool curve = true) => WithManager(scene =>
        {
            GameObject host = scene.Owned.Host("Lucid owned blend shape", false);
            var mesh = scene.Owned.Track(new Mesh());
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.AddBlendShapeFrame("Lucid", 100f, new Vector3[3], null, null);
            var renderer = host.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            var blend = Add<TimeScaledBlendShape>(scene, host);
            Set(blend, "m_skinnedMeshRenderer", renderer); // serialized in the original
            if (curve) Set(blend, "m_blendAnim", AnimationCurve.Linear(0f, 0f, 2f, 1f)); // serialized in the original
            body(scene, blend, renderer);
        });

        // 0x5f9640: after CanValidate (always true) the length is the time of the curve's last key, only with a
        // renderer, a curve and at least one key.
        [Test]
        public void TheBlendShapeTakesItsLengthFromTheLastKeyWhenValidated()
        {
            WithBlendShape((scene, blend, renderer) =>
            {
                Call(blend, "OnValidate");
                Assert.That(Get<float>(blend, "m_blendLength"), Is.EqualTo(2f));
            });
            WithBlendShape((scene, blend, renderer) =>
            {
                Set(blend, "m_blendLength", 7f);
                Set(blend, "m_blendAnim", new AnimationCurve());
                Call(blend, "OnValidate");
                Assert.That(Get<float>(blend, "m_blendLength"), Is.EqualTo(7f), "a curve without keys keeps the length");
                Set(blend, "m_blendAnim", AnimationCurve.Linear(0f, 0f, 3f, 1f));
                Set(blend, "m_skinnedMeshRenderer", null);
                Call(blend, "OnValidate");
                Assert.That(Get<float>(blend, "m_blendLength"), Is.EqualTo(7f), "no renderer keeps the length");
            });
        }

        // 0x5f9580: m_time += delta, weight = curve(m_time) * 100 on shape 0, and the component disables itself once
        // the time is not below the length (0x5f9616 ucomiss length, time; jbe), including exactly equal.
        [Test]
        public void TheBlendShapeDrivesShapeZeroAndStopsAtTheLength()
        {
            WithBlendShape((scene, blend, renderer) =>
            {
                Call(blend, "OnValidate");
                blend.OnUpdate(0.5f);
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(25f).Within(1e-3f));
                Assert.That(blend.enabled, Is.True);
                blend.OnUpdate(1.25f);
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(87.5f).Within(1e-3f));
                Assert.That(blend.enabled, Is.True, "1.75 is below the length 2");
                blend.OnUpdate(0.25f);
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(100f).Within(1e-3f));
                Assert.That(blend.enabled, Is.False, "2 is not below 2");
            });
        }

        // 0x5f9611..0x5f9634: an unordered compare (NaN length) takes jbe too, so the component disables itself.
        [Test]
        public void ANaNLengthStopsTheBlendShape()
        {
            WithBlendShape((scene, blend, renderer) =>
            {
                Set(blend, "m_blendLength", float.NaN);
                blend.OnUpdate(0.5f);
                Assert.That(blend.enabled, Is.False);
            });
        }

        // 0x5f9410 enables the component and zeroes the time; 0x5f9440 (OnDisable) zeroes the time and
        // writes the curve's value at 0 times 100 (UpdateBlend 0x5f94e0 with the same guards).
        [Test]
        public void PlayRestartsTheBlendAndDisablingResetsTheShape()
        {
            WithBlendShape((scene, blend, renderer) =>
            {
                Call(blend, "OnValidate");
                blend.OnUpdate(2f);
                Assert.That(blend.enabled, Is.False);
                blend.Play();
                Assert.That(blend.enabled, Is.True);
                Assert.That(Get<float>(blend, "m_time"), Is.EqualTo(0f));
                blend.UpdateBlend(1f);
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(50f).Within(1e-3f));
                Call(blend, "OnDisable");
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(0f).Within(1e-3f));
                Assert.That(Get<float>(blend, "m_time"), Is.EqualTo(0f));
            });
        }

        // 0x5f9440 / 0x5f94e0 / 0x5f9580 test the curve for null before the renderer, so neither missing piece faults.
        [Test]
        public void TheBlendShapeDoesNothingWithoutACurveOrARenderer()
        {
            WithBlendShape((scene, blend, renderer) =>
            {
                Assert.DoesNotThrow(() => blend.UpdateBlend(1f));
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(0f));
                Assert.DoesNotThrow(() => Call(blend, "OnDisable"));
            }, curve: false);
            WithBlendShape((scene, blend, renderer) =>
            {
                Set(blend, "m_skinnedMeshRenderer", null);
                Assert.DoesNotThrow(() => blend.UpdateBlend(1f));
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(0f));
            });
        }

        // 0x5f91a0: Unity op_Inequality on the animator.
        [Test]
        public void TheAnimatorIsValidOnlyWhileItHasAnAnimator()
        {
            WithManager(scene =>
            {
                GameObject host = scene.Owned.Host("Lucid owned time scaled animator", false);
                var animator = host.AddComponent<Animator>();
                var scaled = Add<TimeScaledAnimator>(scene, host);
                Assert.That(Get<Animator>(scaled, "m_animator"), Is.SameAs(animator), "found by the editor OnValidate when added");
                Assert.That(scaled.CheckIsValid(), Is.True);
                Object.DestroyImmediate(animator);
                Assert.That(scaled.CheckIsValid(), Is.False, "a destroyed animator is Unity null");
                Call(scaled, "Initialise", scene.Manager);
                Assert.That(Subscribed(scene, UpdateOn.FixedUpdate), Is.EqualTo(0), "an invalid component subscribes nothing");
            });
            WithManager(scene =>
            {
                var scaled = Add<TimeScaledAnimator>(scene, scene.Owned.Host("Lucid owned lone time scaled animator", false));
                Assert.That(scaled.CheckIsValid(), Is.False);
                Call(scaled, "Initialise", scene.Manager);
                Assert.That(Subscribed(scene, UpdateOn.FixedUpdate), Is.EqualTo(0));
            });
        }

        [Test]
        public void AValidAnimatorSubscribesWhenInitialised()
        {
            WithManager(scene =>
            {
                GameObject host = scene.Owned.Host("Lucid owned time scaled animator", false);
                host.AddComponent<Animator>();
                var scaled = Add<TimeScaledAnimator>(scene, host);
                Call(scaled, "Initialise", scene.Manager);
                Assert.That(Subscribed(scene, UpdateOn.FixedUpdate), Is.EqualTo(1));
            });
        }

        // 0x5f9293..0x5f9328: the speed is written only when it is not Mathf.Approximately the cached one, and the
        // cache starts at zero.
        [Test]
        public void TheAnimatorSpeedIsWrittenOnlyWhenTheScaleChanges()
        {
            WithManager(scene =>
            {
                GameObject host = scene.Owned.Host("Lucid owned time scaled animator", false);
                var animator = host.AddComponent<Animator>();
                var scaled = Add<TimeScaledAnimator>(scene, host);
                scaled.OnUpdate(0f);
                Assert.That(animator.speed, Is.EqualTo(Scale));
                Assert.That(Get<float>(scaled, "m_speed"), Is.EqualTo(Scale));
                animator.speed = 1f;
                scaled.OnUpdate(0f);
                Assert.That(animator.speed, Is.EqualTo(1f), "the cached speed already equals the scale, so nothing is written");

                // Mathf.Approximately, not exact equality: a cached speed one ulp above the scale (2.0000002) is still approximately it.
                Set(scaled, "m_speed", 2.0000002f); // stands in for a scale that moved by less than Mathf.Approximately's tolerance since the last write
                scaled.OnUpdate(0f);
                Assert.That(animator.speed, Is.EqualTo(1f), "an approximately equal cached speed writes nothing");
            });
        }

        // 0x5f9330 stores the enabled state before disabling; 0x5f9370 re-enables only if it was enabled.
        [Test]
        public void PausingDisablesTheAnimatorAndResumingRestoresItOnlyIfItWasEnabled()
        {
            WithManager(scene =>
            {
                GameObject host = scene.Owned.Host("Lucid owned time scaled animator", false);
                var animator = host.AddComponent<Animator>();
                var scaled = Add<TimeScaledAnimator>(scene, host);
                scaled.OnPause();
                Assert.That(animator.enabled, Is.False);
                Assert.That(Get<bool>(scaled, "m_wasEnabledOnPause"), Is.True);
                scaled.OnResume();
                Assert.That(animator.enabled, Is.True);

                animator.enabled = false;
                scaled.OnPause();
                Assert.That(Get<bool>(scaled, "m_wasEnabledOnPause"), Is.False);
                scaled.OnResume();
                Assert.That(animator.enabled, Is.False, "an animator that was already off stays off");
            });
        }

        private static AnimationClip LegacyClip(Owned owned)
        {
            var clip = owned.Track(new AnimationClip { legacy = true });
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }

        // 0x5f8bc0 / 0x5f8c20: validity is Unity op_Inequality on the animation; an invalid component subscribes nothing.
        [Test]
        public void TheAnimationIsValidOnlyWhileItHasAnAnimationAndSubscribesThen()
        {
            WithManager(scene =>
            {
                GameObject host = scene.Owned.Host("Lucid owned time scaled animation", false);
                var animation = host.AddComponent<Animation>();
                var scaled = Add<TimeScaledAnimation>(scene, host);
                Assert.That(Get<Animation>(scaled, "m_animation"), Is.SameAs(animation), "found by the editor OnValidate when added");
                Assert.That(scaled.CheckIsValid(), Is.True);
                Call(scaled, "Initialise", scene.Manager);
                Assert.That(Subscribed(scene, UpdateOn.FixedUpdate), Is.EqualTo(1));
                Object.DestroyImmediate(animation);
                Assert.That(scaled.CheckIsValid(), Is.False);
            });
            WithManager(scene =>
            {
                var scaled = Add<TimeScaledAnimation>(scene, scene.Owned.Host("Lucid owned lone time scaled animation", false));
                Assert.That(scaled.CheckIsValid(), Is.False);
                Call(scaled, "Initialise", scene.Manager);
                Assert.That(Subscribed(scene, UpdateOn.FixedUpdate), Is.EqualTo(0));
            });
        }

        // 0x5f9050 assigns the clip and plays; 0x5f9090 starts the cached scale at 1.
        [Test]
        public void PlayClipAssignsTheClipAndTheScaleStartsAtOne()
        {
            WithManager(scene =>
            {
                GameObject host = scene.Owned.Host("Lucid owned time scaled animation", false);
                host.AddComponent<Animation>();
                var scaled = Add<TimeScaledAnimation>(scene, host);
                Assert.That(Get<float>(scaled, "m_currentTimescale"), Is.EqualTo(1f));
                AnimationClip clip = LegacyClip(scene.Owned);
                scaled.PlayClip(clip);
                Assert.That(host.GetComponent<Animation>().clip, Is.SameAs(clip));
            });
        }

        // 0x5f8d10 returns at once unless the animation is playing, so an idle animation keeps its states' speed and
        // the cached scale. (The playing branch needs the engine to play an Animation on an active object and is not
        // covered: its strict 1e-4 threshold at 0x5f8d8e and the cache update after the loop at 0x5f8f8f are unpinned.)
        [Test]
        public void AnIdleAnimationKeepsItsStateSpeeds()
        {
            WithManager(scene =>
            {
                GameObject host = scene.Owned.Host("Lucid owned time scaled animation", false);
                var animation = host.AddComponent<Animation>();
                animation.AddClip(LegacyClip(scene.Owned), "Lucid");
                var scaled = Add<TimeScaledAnimation>(scene, host);
                Assert.That(animation.isPlaying, Is.False);
                scaled.OnUpdate(0f);
                scaled.OnPause();
                Assert.That(animation["Lucid"].speed, Is.EqualTo(1f));
                Assert.That(Get<float>(scaled, "m_currentTimescale"), Is.EqualTo(1f));
            });
        }
    }
}
