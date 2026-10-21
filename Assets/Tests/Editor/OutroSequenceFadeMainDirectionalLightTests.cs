using System;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime OutroSequenceFadeMainDirectionalLight (TypeDef 0x02000a8d, x86-64 0x6CE8B0..0x6CEA00). The
    // scene sun is a real directional Light on an owned host, installed through RenderSettings.sun and put back
    // afterwards. Time.deltaTime cannot be controlled in Edit Mode, so every update case but the accumulation one sets
    // the speed multiplier to zero and the accumulated lerp time directly; the intensity is then a pure function of it: default + (0.01 - default) * clamp01(lerpTime), with 0.01 the MinIntensity constant
    // (0x29a6c04). The case without a scene sun is not covered, because the engine may substitute another light.
    public sealed class OutroSequenceFadeMainDirectionalLightTests
    {
        private const float DefaultIntensity = 2f;

        private sealed class Scene
        {
            public OutroSequenceFadeMainDirectionalLight Fade;
            public Light Sun;
        }

        private static void WithFade(Action<Scene> body) => Owned.Run(owned =>
        {
            Light priorSun = RenderSettings.sun;
            owned.Lease(new ReleaseAction(() => RenderSettings.sun = priorSun));
            var scene = new Scene { Sun = owned.Host("Lucid owned scene sun", false).AddComponent<Light>() };
            scene.Sun.type = LightType.Directional;
            scene.Sun.intensity = DefaultIntensity;
            RenderSettings.sun = scene.Sun;
            scene.Fade = owned.Host("Lucid owned outro fade", false).AddComponent<OutroSequenceFadeMainDirectionalLight>();
            var serialized = new SerializedObject(scene.Fade);
            serialized.FindProperty("lerpSpeedMultiplier").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            body(scene);
        });

        // Constructor 0x6CEA00: lerpSpeedMultiplier 0.5f (0x3f000000), everything else zero.
        [Test]
        public void ConstructionStoresTheHalfSpeedMultiplier()
        {
            Owned.Run(owned =>
            {
                var fade = owned.Host("Lucid owned default outro fade", false).AddComponent<OutroSequenceFadeMainDirectionalLight>();
                Assert.That(new SerializedObject(fade).FindProperty("lerpSpeedMultiplier").floatValue, Is.EqualTo(0.5f));
                Assert.That(Get<float>(fade, "m_lerpTime"), Is.EqualTo(0f));
            });
        }

        // Start 0x6CE8B0: the sun comes from RenderSettings.sun (0x6ce8d8) and its intensity is remembered
        // (0x6ce92f..0x6ce934); the component stays enabled.
        [Test]
        public void StartRemembersTheScenesSunAndItsIntensity()
        {
            WithFade(scene =>
            {
                Call(scene.Fade, "Start");
                Assert.That(Get<Light>(scene.Fade, "m_directionalLight"), Is.SameAs(scene.Sun));
                Assert.That(Get<float>(scene.Fade, "m_defaultDirectionalLightIntensity"), Is.EqualTo(DefaultIntensity));
                Assert.That(scene.Fade.enabled, Is.True);
            });
        }

        // Update 0x6CE940: intensity = default + (0.01 - default) * clamp01(lerpTime). Half way is 1.005, a lerp time
        // above one clamps to 0.01 and one below zero to the default; the component stays enabled below one.
        [Test]
        public void UpdateLerpsTheIntensityTowardsTheMinimumWithAClampedTime()
        {
            WithFade(scene =>
            {
                Call(scene.Fade, "Start");
                foreach ((float lerpTime, float intensity) in new[] { (0.5f, 1.005f), (0.999f, 0.01199f), (3f, 0.01f), (-1f, 2f) })
                {
                    Set(scene.Fade, "m_lerpTime", lerpTime); // Reflection: the accumulated time, not reachable without a frame delta
                    scene.Fade.enabled = true;
                    Call(scene.Fade, "Update");
                    Assert.That(scene.Sun.intensity, Is.EqualTo(intensity).Within(1e-4f), "lerp time " + lerpTime);
                }
            });
        }

        // 0x6ce9b3..0x6ce9ba: the component disables itself once the lerp time reaches one (jae), and not below it.
        [Test]
        public void UpdateDisablesTheComponentOnceTheLerpTimeReachesOne()
        {
            WithFade(scene =>
            {
                Call(scene.Fade, "Start");
                Set(scene.Fade, "m_lerpTime", 0.999f); // Reflection: the accumulated time, not reachable without a frame delta
                scene.Fade.enabled = true;
                Call(scene.Fade, "Update");
                Assert.That(scene.Fade.enabled, Is.True);
                Set(scene.Fade, "m_lerpTime", 1f);
                Call(scene.Fade, "Update");
                Assert.That(scene.Fade.enabled, Is.False);
                Assert.That(scene.Sun.intensity, Is.EqualTo(0.01f).Within(1e-6f));
            });
        }

        // OnDestroy 0x6CE9E0 writes the remembered intensity back unconditionally.
        [Test]
        public void OnDestroyRestoresTheRememberedIntensity()
        {
            WithFade(scene =>
            {
                Call(scene.Fade, "Start");
                Set(scene.Fade, "m_lerpTime", 1f); // Reflection: the accumulated time, not reachable without a frame delta
                Call(scene.Fade, "Update");
                Assert.That(scene.Sun.intensity, Is.EqualTo(0.01f).Within(1e-6f));
                Call(scene.Fade, "OnDestroy");
                Assert.That(scene.Sun.intensity, Is.EqualTo(DefaultIntensity));
            });
        }

        // Update 0x6CE940 (0x6ce949..0x6ce96e): the lerp time grows by the speed multiplier times the frame delta
        // time, added to what it already holds.
        [Test]
        public void UpdateAddsTheMultipliedFrameDeltaToTheLerpTime()
        {
            Assume.That(Time.deltaTime, Is.GreaterThan(0f));
            WithFade(scene =>
            {
                var serialized = new SerializedObject(scene.Fade);
                serialized.FindProperty("lerpSpeedMultiplier").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Call(scene.Fade, "Start");
                Set(scene.Fade, "m_lerpTime", 0.25f); // Reflection: the accumulated time, not reachable without a frame delta
                Call(scene.Fade, "Update");
                Assert.That(Get<float>(scene.Fade, "m_lerpTime"), Is.EqualTo(0.25f + 0.5f * Time.deltaTime).Within(1e-6f));
            });
        }
    }
}
