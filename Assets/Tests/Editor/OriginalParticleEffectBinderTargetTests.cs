using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of HardlightProject.ParticleEffectBinderTarget (0x06000497..
    // 0x060004aa) against the shipped x86-64 bodies, on owned inactive
    // particle systems that are never played.
    [TestFixture]
    public sealed class OriginalParticleEffectBinderTargetTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly FieldInfo SystemField = typeof(ParticleEffectBinderTarget).GetField("m_particleSystem", Instance);

        static void WithTarget(System.Action<ParticleEffectBinderTarget, ParticleSystem> body)
        {
            var owner = new GameObject("Lucid owned particle binder target");
            try
            {
                owner.SetActive(false);
                var target = owner.AddComponent<ParticleEffectBinderTarget>();
                var system = owner.GetComponent<ParticleSystem>();
                Assert.That(system, Is.Not.Null, "RequireComponent(typeof(ParticleSystem))");
                SystemField.SetValue(target, system);
                body(target, system);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        // Reset 0x52aa60 returns while op_Inequality(m_particleSystem, null);
        // only then does it store GetComponent<ParticleSystem>().
        [Test]
        public void ResetFillsOnlyAMissingParticleSystem()
        {
            WithTarget((target, own) =>
            {
                var other = new GameObject("Lucid owned other particle system");
                try
                {
                    other.SetActive(false);
                    var assigned = other.AddComponent<ParticleSystem>();
                    SystemField.SetValue(target, assigned);
                    typeof(ParticleEffectBinderTarget).GetMethod("Reset", Instance).Invoke(target, null);
                    Assert.That(SystemField.GetValue(target), Is.SameAs(assigned));
                    SystemField.SetValue(target, null);
                    typeof(ParticleEffectBinderTarget).GetMethod("Reset", Instance).Invoke(target, null);
                    Assert.That(SystemField.GetValue(target), Is.SameAs(own));
                }
                finally { Object.DestroyImmediate(other); }
            });
        }

        // Each setter fetches the module and writes one property: the float
        // overloads of SetTransformScale (0x52aaf0) and SetEmitterVelocity
        // (0x52acb0) broadcast the value with movsldup to all three lanes.
        [Test]
        public void SettersWriteTheirModuleProperty()
        {
            WithTarget((target, system) =>
            {
                target.SetTransformScale(2.5f);
                Assert.That(target.transform.localScale, Is.EqualTo(new Vector3(2.5f, 2.5f, 2.5f)));
                target.SetTransformScale(new Vector3(1f, 2f, 3f));
                Assert.That(target.transform.localScale, Is.EqualTo(new Vector3(1f, 2f, 3f)));

                target.SetStartSpeed(3f);
                target.SetDuration(2.25f);
                target.SetMaxParticles(7);
                target.SetStartLifetime(4f);
                target.SetStartDelay(0.5f);
                target.SetStartSizeMultiplier(1.5f);
                target.SetEmissionRateOverTimeMultiplier(12f);
                ParticleSystem.MainModule main = system.main;
                Assert.That(main.startSpeed.mode, Is.EqualTo(ParticleSystemCurveMode.Constant));
                Assert.That(main.startSpeed.constant, Is.EqualTo(3f));
                Assert.That(main.duration, Is.EqualTo(2.25f));
                Assert.That(main.maxParticles, Is.EqualTo(7));
                Assert.That(main.startLifetime.constant, Is.EqualTo(4f));
                Assert.That(main.startDelay.constant, Is.EqualTo(0.5f));
                Assert.That(main.startSizeMultiplier, Is.EqualTo(1.5f));
                Assert.That(system.emission.rateOverTimeMultiplier, Is.EqualTo(12f));

                target.SetEmitterVelocity(4f);
                Assert.That(system.main.emitterVelocity, Is.EqualTo(new Vector3(4f, 4f, 4f)));
                target.SetEmitterVelocity(new Vector3(-1f, 0f, 6f));
                Assert.That(system.main.emitterVelocity, Is.EqualTo(new Vector3(-1f, 0f, 6f)));

                // SetEmission 0x52b370 writes only a changed state; the result is the requested state.
                target.SetEmission(false);
                Assert.That(system.emission.enabled, Is.False);
                target.SetEmission(false);
                Assert.That(system.emission.enabled, Is.False);
                target.SetEmission(true);
                Assert.That(system.emission.enabled, Is.True);
            });
        }

        // The channel setters (0x52aff0, 0x52b0d0, 0x52b1b0, 0x52b290) clamp
        // with minss(1, v) and a v < 0 mask (Mathf.Clamp01), then replace lane
        // 0, 1 of xmm0 or 0, 1 of xmm1 (r, g, b, a) of startColor.color and
        // write it back as a single-colour gradient.
        [Test]
        public void ChannelSettersClampOneChannelAndKeepTheOthers()
        {
            WithTarget((target, system) =>
            {
                target.SetStartColour(new Color(0.1f, 0.2f, 0.3f, 0.4f));
                Assert.That(system.main.startColor.color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));

                target.SetStartColourRedValue(1.5f);
                Assert.That(system.main.startColor.color, Is.EqualTo(new Color(1f, 0.2f, 0.3f, 0.4f)));
                target.SetStartColourGreenValue(-2f);
                Assert.That(system.main.startColor.color, Is.EqualTo(new Color(1f, 0f, 0.3f, 0.4f)));
                target.SetStartColourBlueValue(0.25f);
                Assert.That(system.main.startColor.color, Is.EqualTo(new Color(1f, 0f, 0.25f, 0.4f)));
                target.SetStartColourAlphaValue(0.75f);
                Assert.That(system.main.startColor.color, Is.EqualTo(new Color(1f, 0f, 0.25f, 0.75f)));
            });
        }

        // MinMaxGradient.color reads the max colour, and op_Implicit(Color)
        // rebuilds a Color-mode gradient, so a channel write flattens a
        // two-colour start colour to its edited max colour.
        [Test]
        public void AChannelWriteFlattensAGradientStartColour()
        {
            WithTarget((target, system) =>
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.red, Color.blue);
                Assert.That(system.main.startColor.mode, Is.EqualTo(ParticleSystemGradientMode.TwoColors));
                target.SetStartColourAlphaValue(0.5f);
                Assert.That(system.main.startColor.mode, Is.EqualTo(ParticleSystemGradientMode.Color));
                Assert.That(system.main.startColor.color, Is.EqualTo(new Color(0f, 0f, 1f, 0.5f)));
            });
        }
    }
}
