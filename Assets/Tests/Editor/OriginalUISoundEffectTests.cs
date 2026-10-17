using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.UISoundEffect (HLUnityUI.Runtime 0x060002f8..fa)
    // against the shipped x86-64 bodies. Each case owns its effect, an
    // UISoundEffectManager with an AudioSource, an HLUIEffectManager with its own
    // cue table and a generated clip. Both singleton Instance properties are
    // replaced for the case and restored afterwards; none of these components
    // receives Unity callbacks in edit mode. The played route is observed through
    // the pitch UISoundEffectManager writes before PlayOneShot.
    public sealed class OriginalUISoundEffectTests
    {
        const float BasePitch = 1.05946f;
        const float UntouchedPitch = 0.5f;
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Declared = Any | BindingFlags.Static | BindingFlags.DeclaredOnly;

        sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly UISoundEffect Effect;
            public readonly AudioSource Source;
            public readonly AudioClip Clip;
            readonly HLUIEffectManager m_effectManager;
            readonly UISoundEffectManager m_soundManager;
            readonly object m_previousSound;
            readonly object m_previousEffect;

            public Fixture(bool effectActive)
            {
                m_previousSound = InstanceProperty(typeof(UISoundEffectManager)).GetValue(null);
                m_previousEffect = InstanceProperty(typeof(HLUIEffectManager)).GetValue(null);
                Root = new GameObject("UISoundEffect fixture") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    Clip = AudioClip.Create("UISoundEffect fixture clip", 64, 1, 44100, false);
                    var managers = new GameObject("Managers") { hideFlags = HideFlags.HideAndDontSave };
                    managers.transform.SetParent(Root.transform, false);
                    Source = managers.AddComponent<AudioSource>();
                    Source.pitch = UntouchedPitch;
                    m_soundManager = managers.AddComponent<UISoundEffectManager>();
                    typeof(UISoundEffectManager).GetField("m_audioSource", Any).SetValue(m_soundManager, Source);
                    m_effectManager = managers.AddComponent<HLUIEffectManager>();
                    SetInstance(typeof(UISoundEffectManager), m_soundManager);
                    SetInstance(typeof(HLUIEffectManager), m_effectManager);

                    var owner = new GameObject("Effect") { hideFlags = HideFlags.HideAndDontSave };
                    owner.transform.SetParent(Root.transform, false);
                    owner.SetActive(effectActive);
                    Effect = owner.AddComponent<UISoundEffect>();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void AddCue(HLAudioTypes type) =>
                ((Dictionary<HLAudioTypes, AudioClip>)typeof(HLUIEffectManager).GetField("m_audioCues", Any).GetValue(m_effectManager))[type] = Clip;

            public void Set(string field, object value) => typeof(UISoundEffect).GetField(field, Any).SetValue(Effect, value);

            public void Dispose()
            {
                SetInstance(typeof(UISoundEffectManager), m_previousSound);
                SetInstance(typeof(HLUIEffectManager), m_previousEffect);
                Object.DestroyImmediate(Root);
                if (Clip != null) Object.DestroyImmediate(Clip);
            }
        }

        static PropertyInfo InstanceProperty(Type singleton) =>
            typeof(MonoSingleton<>).MakeGenericType(singleton).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

        static void SetInstance(Type singleton, object value) => InstanceProperty(singleton).GetSetMethod(true).Invoke(null, new[] { value });

        static object Get(object target, string field) => typeof(UISoundEffect).GetField(field, Any).GetValue(target);

        [Test]
        public void DeclarationKeepsOriginalOptionsAndSerializedFields()
        {
            Type type = typeof(UISoundEffect);
            Assert.That(type.FullName, Is.EqualTo("Hardlight.UISoundEffect"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            CustomAttributeData[] options = type.GetCustomAttributesData().ToArray();
            Assert.That(options.Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) }));
            // dump.cs TypeDefIndex 11926 lists option 2 (array bounds) before 1 (null checks).
            Assert.That(options.Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)).ToArray(), Is.EqualTo(new[] { 2, 1 }));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] {
                "m_audioClip", "m_audioType", "m_playOnAwake", "m_playOnlyWhenActive", "m_randomiseSemitonePitch", "m_semitoneOffsetRange" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] {
                typeof(AudioClip), typeof(HLAudioTypes), typeof(bool), typeof(bool), typeof(bool), typeof(RangePropertyInt) }));
            Assert.That(fields.All(f => f.IsPrivate && f.IsDefined(typeof(SerializeField), false)), Is.True);
            Assert.That(fields[1].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField), typeof(HashEnumAttribute) }));
            Assert.That(type.GetMethods(Declared).Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "OnEnable", "Play" }));
        }

        [Test]
        public void ConstructionSeedsASemitoneRangeFromMinusTwelveToTwo()
        {
            using (var fixture = new Fixture(true))
            {
                // 0x1B4FE6B..0x1B4FE77: RangePropertyInt(-12, 2).
                var range = (RangePropertyInt)Get(fixture.Effect, "m_semitoneOffsetRange");
                Assert.That(new[] { range.Min, range.Max }, Is.EqualTo(new[] { -12, 2 }));
                Assert.That(Get(fixture.Effect, "m_audioType"), Is.EqualTo((HLAudioTypes)0));
                Assert.That(new[] { "m_playOnAwake", "m_playOnlyWhenActive", "m_randomiseSemitonePitch" }.Select(f => (bool)Get(fixture.Effect, f)), Is.All.False);
            }
        }

        [Test]
        public void AssignedClipPlaysThroughTheSoundManagerAtItsBasePitch()
        {
            using (var fixture = new Fixture(true))
            {
                fixture.Set("m_audioClip", fixture.Clip);
                fixture.Effect.Play();
                Assert.That(fixture.Source.pitch, Is.EqualTo(BasePitch).Within(1e-5f));
                Assert.That(fixture.Source.volume, Is.EqualTo(1f));
            }
        }

        [Test]
        public void RandomisedClipUsesTheNextSemitoneOffset()
        {
            using (var fixture = new Fixture(true))
            {
                fixture.Set("m_audioClip", fixture.Clip);
                fixture.Set("m_randomiseSemitonePitch", true);
                fixture.Set("m_semitoneOffsetRange", new RangePropertyInt(3, 3));
                fixture.Effect.Play();
                Assert.That(fixture.Source.pitch, Is.EqualTo(Mathf.Pow(BasePitch, 3f)).Within(1e-5f));
            }
        }

        [Test]
        public void ClipWinsOverTheAudioType()
        {
            using (var fixture = new Fixture(true))
            {
                fixture.Set("m_audioClip", fixture.Clip);
                fixture.Set("m_audioType", (HLAudioTypes)17);
                fixture.Set("m_semitoneOffsetRange", new RangePropertyInt(-2, -2));
                fixture.Set("m_randomiseSemitonePitch", true);
                // The type route reads HLUIEffectManager.Instance without a null
                // check, so with no instance it would throw; the clip route never
                // reads it.
                SetInstance(typeof(HLUIEffectManager), null);
                Assert.DoesNotThrow(fixture.Effect.Play);
                Assert.That(fixture.Source.pitch, Is.EqualTo(Mathf.Pow(BasePitch, -2f)).Within(1e-5f));

                fixture.Set("m_audioClip", null);
                Assert.Throws<NullReferenceException>(fixture.Effect.Play);
            }
        }

        [Test]
        public void AudioTypeWithoutAClipPlaysThroughTheEffectManager()
        {
            using (var fixture = new Fixture(true))
            {
                fixture.AddCue((HLAudioTypes)17);
                fixture.Set("m_audioType", (HLAudioTypes)17);
                fixture.Effect.Play();
                Assert.That(fixture.Source.pitch, Is.EqualTo(BasePitch).Within(1e-5f));

                fixture.Source.pitch = UntouchedPitch;
                fixture.Set("m_randomiseSemitonePitch", true);
                fixture.Set("m_semitoneOffsetRange", new RangePropertyInt(-4, -4));
                fixture.Effect.Play();
                Assert.That(fixture.Source.pitch, Is.EqualTo(Mathf.Pow(BasePitch, -4f)).Within(1e-5f));
            }
        }

        [Test]
        public void ZeroAudioTypeIsSkippedEvenWithACueForIt()
        {
            using (var fixture = new Fixture(true))
            {
                // 0x1B4FCF9 tests the raw value against zero. HLUIEffectManager would
                // play this cue (it rejects only HLAudioTypes.None), so a guard
                // written against None instead of zero changes the pitch here.
                fixture.AddCue((HLAudioTypes)0);
                fixture.Effect.Play();
                Assert.That(fixture.Source.pitch, Is.EqualTo(UntouchedPitch));
            }
        }

        [Test]
        public void PlayOnlyWhenActiveSkipsAnInactiveComponent()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Set("m_audioClip", fixture.Clip);
                fixture.Set("m_playOnlyWhenActive", true);
                fixture.Effect.Play();
                Assert.That(fixture.Source.pitch, Is.EqualTo(UntouchedPitch));

                fixture.Set("m_playOnlyWhenActive", false);
                fixture.Effect.Play();
                Assert.That(fixture.Source.pitch, Is.EqualTo(BasePitch).Within(1e-5f));
            }
        }

        [Test]
        public void OnEnablePlaysOnlyWithPlayOnAwake()
        {
            using (var fixture = new Fixture(true))
            {
                fixture.Set("m_audioClip", fixture.Clip);
                MethodInfo onEnable = typeof(UISoundEffect).GetMethod("OnEnable", Any);
                onEnable.Invoke(fixture.Effect, null);
                Assert.That(fixture.Source.pitch, Is.EqualTo(UntouchedPitch));

                fixture.Set("m_playOnAwake", true);
                onEnable.Invoke(fixture.Effect, null);
                Assert.That(fixture.Source.pitch, Is.EqualTo(BasePitch).Within(1e-5f));
            }
        }
    }
}
