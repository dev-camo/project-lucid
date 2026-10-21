using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCorruption (Game.Runtime 0200089f, x86-64 0x629530..0x629F50, closure 020008a0) as a real
    // component on an owned inactive host with a real Animation of legacy clips. The meter is never given an
    // Awake or OnDestroy: those use the type's static UIManager reference, whose registry row the type
    // initialiser would create permanently. Not covered either: the disable branch of SetEnabled, which hands the
    // two callbacks to CoroutineUtils.OnPredicateActive and so starts a coroutine on the process-wide host; its
    // predicate (the closure) is covered on its own.
    public sealed class UIContainerCorruptionTests
    {
        private sealed class Rig
        {
            public UIContainerCorruption Container;
            public GameObject Root;
            public Animation Animation;
            public AnimationClip Intro, Hit, Normal, Outro;
        }

        private static AnimationClip Clip(Owned owned, string name)
        {
            var clip = owned.Track(new AnimationClip { name = name, legacy = true });
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }

        private static void WithMeter(Action<Owned, Rig> body) => Owned.Run(owned =>
        {
            var rig = new Rig();
            rig.Root = owned.Host("Lucid corruption root", false);
            GameObject animated = owned.Host("Lucid corruption animation", true);
            rig.Animation = animated.AddComponent<Animation>();
            rig.Intro = Clip(owned, "Lucid corruption intro");
            rig.Hit = Clip(owned, "Lucid corruption hit");
            rig.Normal = Clip(owned, "Lucid corruption normal");
            rig.Outro = Clip(owned, "Lucid corruption outro");
            foreach (AnimationClip clip in new[] { rig.Intro, rig.Hit, rig.Normal, rig.Outro }) rig.Animation.AddClip(clip, clip.name);
            rig.Container = owned.Host("Lucid corruption container", false).AddComponent<UIContainerCorruption>();
            // Serialized authored references.
            Set(rig.Container, "m_progressRoot", rig.Root);
            Set(rig.Container, "m_animation", rig.Animation);
            Set(rig.Container, "m_clipIntro", rig.Intro);
            Set(rig.Container, "m_clipHit", rig.Hit);
            Set(rig.Container, "m_clipNormal", rig.Normal);
            Set(rig.Container, "m_clipOutro", rig.Outro);
            body(owned, rig);
        });

        // Starts the meter shown, past its intro, with the normal clip playing and progress 0.5.
        private static void ShowAtHalf(Rig rig)
        {
            rig.Container.SetEnabled(true);
            rig.Animation[rig.Intro.name].normalizedTime = 1f;
            rig.Container.SetProgress(0.5f);
            Assert.That(rig.Animation.clip, Is.SameAs(rig.Normal), "set-up: the half-way meter plays the normal clip");
        }

        // dump.cs TypeDefIndex 2207/2206 and the DummyDll rows (fields 0x0400222e..0x04002237).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerCorruption), 0x00100001,
                new (string, int)[]
                {
                    ("m_progressRoot", 0x0001),
                    ("m_animation", 0x0001),
                    ("m_clipIntro", 0x0001),
                    ("m_clipHit", 0x0001),
                    ("m_clipNormal", 0x0001),
                    ("m_clipOutro", 0x0001),
                    ("CorruptionProgress", 0x0026),
                    ("CorruptionCurrent", 0x0026),
                    ("CorruptionTotal", 0x0026),
                    ("m_uiManagerRef", 0x0031),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("SetEnabled", 0x00c6),
                    ("SetProgress", 0x01e6),
                    ("PlayAnimation", 0x0081),
                    ("SetProgress", 0x01e6),
                    (".ctor", 0x1886),
                    (".cctor", 0x1891),
                    ("<Awake>b__10_0", 0x0081),
                    ("<SetEnabled>b__12_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerCorruption), 1, 2);
            Assert.That(typeof(UIContainerCorruption).BaseType, Is.EqualTo(typeof(UIContainer)));
            Assert.That(typeof(UIContainerCorruption).GetInterfaces(), Does.Contain(typeof(IUIContainerProgress)));
            Type closure = typeof(UIContainerCorruption).GetNestedTypes(BindingFlags.NonPublic).Single();
            Assert.That(closure.Name, Is.EqualTo("<>c__DisplayClass12_0"));
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00100103), "NestedPrivate|Sealed|BeforeFieldInit");
            Assert.That(closure.GetFields().Select(field => field.FieldType), Is.EqualTo(new[] { typeof(AnimationState) }));
        }

        // The constructor 0x629cb0 creates the three bindables (+0x68, +0x70, +0x78) at their defaults.
        [Test]
        public void ConstructionCreatesZeroedBindables()
        {
            WithMeter((owned, rig) =>
            {
                Assert.That(rig.Container.CorruptionProgress.Value, Is.Zero);
                Assert.That(rig.Container.CorruptionCurrent.Value, Is.Zero);
                Assert.That(rig.Container.CorruptionTotal.Value, Is.Zero);
            });
        }

        // SetProgress(int, int) 0x629c70 stores the two counts and touches nothing else.
        [Test]
        public void CountsAreStoredWithoutTouchingTheProgressOrTheAnimation()
        {
            WithMeter((owned, rig) =>
            {
                rig.Container.CorruptionProgress.Value = 0.25f;

                rig.Container.SetProgress(3, 7);

                Assert.That(rig.Container.CorruptionCurrent.Value, Is.EqualTo(3));
                Assert.That(rig.Container.CorruptionTotal.Value, Is.EqualTo(7));
                Assert.That(rig.Container.CorruptionProgress.Value, Is.EqualTo(0.25f));
                Assert.That(rig.Animation.clip, Is.Null);
            });
        }

        // SetEnabled 0x6296a0: the base call comes first (no canvas here), then a missing progress root ends it
        // (op_Equality at 0x629740) before any bindable or animation is touched.
        [Test]
        public void EnablingWithoutAProgressRootChangesNothing()
        {
            WithMeter((owned, rig) =>
            {
                Set(rig.Container, "m_progressRoot", null);
                rig.Container.CorruptionProgress.Value = 0.5f;
                rig.Container.CorruptionCurrent.Value = 2;

                rig.Container.SetEnabled(true);

                Assert.That(rig.Container.CorruptionProgress.Value, Is.EqualTo(0.5f));
                Assert.That(rig.Container.CorruptionCurrent.Value, Is.EqualTo(2));
                Assert.That(rig.Animation.clip, Is.Null);
            });
        }

        // Enabling shows the root, zeroes the three bindables (0x629766..0x6297a3) and plays the intro clip
        // (set_clip 0x6297f8, Play 0x62980b).
        [Test]
        public void EnablingShowsTheMeterFromZeroAndPlaysTheIntro()
        {
            WithMeter((owned, rig) =>
            {
                rig.Container.CorruptionProgress.Value = 0.5f;
                rig.Container.CorruptionCurrent.Value = 2;
                rig.Container.CorruptionTotal.Value = 9;

                rig.Container.SetEnabled(true);

                Assert.That(rig.Root.activeSelf, Is.True);
                Assert.That(rig.Container.CorruptionProgress.Value, Is.Zero);
                Assert.That(rig.Container.CorruptionCurrent.Value, Is.Zero);
                Assert.That(rig.Container.CorruptionTotal.Value, Is.Zero);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Intro));
                Assert.That(rig.Animation.IsPlaying(rig.Intro.name), Is.True);
            });
        }

        // A missing intro clip is tested after the bindables are reset (0x6297e2..0x6297e9), so the meter still
        // shows and resets but starts no animation.
        [Test]
        public void EnablingWithoutAnIntroClipStillShowsTheMeter()
        {
            WithMeter((owned, rig) =>
            {
                Set(rig.Container, "m_clipIntro", null);
                rig.Container.CorruptionTotal.Value = 9;

                rig.Container.SetEnabled(true);

                Assert.That(rig.Root.activeSelf, Is.True);
                Assert.That(rig.Container.CorruptionTotal.Value, Is.Zero);
                Assert.That(rig.Animation.clip, Is.Null);
            });
        }

        // SetProgress(float) 0x629960 stores the progress, then a hidden meter (get_activeSelf 0x629a01) is enabled
        // through the virtual SetEnabled (0x629b25), which resets the progress it just stored to zero.
        [Test]
        public void ProgressOnAHiddenMeterEnablesItAndResetsTheProgress()
        {
            WithMeter((owned, rig) =>
            {
                rig.Container.SetProgress(0.75f);

                Assert.That(rig.Root.activeSelf, Is.True);
                Assert.That(rig.Container.CorruptionProgress.Value, Is.Zero);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Intro));
            });
        }

        // The intro is current and not finished (normalizedTime < 1.0, 0x629ace) so a progress change waits;
        // once its normalized time reaches 1 the same change goes through.
        [Test]
        public void ProgressChangesWaitForTheIntroToFinish()
        {
            WithMeter((owned, rig) =>
            {
                rig.Container.SetEnabled(true);

                rig.Container.SetProgress(0.5f);
                Assert.That(rig.Container.CorruptionProgress.Value, Is.EqualTo(0.5f), "the value is stored regardless");
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Intro), "the intro keeps playing");

                rig.Animation[rig.Intro.name].normalizedTime = 1f;
                rig.Container.SetProgress(0.6f);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Normal));
            });
        }

        // Zero plays the hit clip, whatever is current; a value equal to the stored one does nothing.
        [Test]
        public void ZeroPlaysTheHitClipAndAnUnchangedValueDoesNothing()
        {
            WithMeter((owned, rig) =>
            {
                ShowAtHalf(rig);

                rig.Container.SetProgress(0f);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Hit));

                rig.Animation.clip = rig.Normal;
                rig.Container.SetProgress(0f);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Normal), "no change, no animation");
            });
        }

        // Mathf.Approximately (0x629a52: 1e-6 times the larger magnitude against the difference) counts
        // 0.5 and 0.5000001 as the same value, so the second call returns before choosing a clip.
        [Test]
        public void NearlyEqualProgressIsTreatedAsUnchanged()
        {
            WithMeter((owned, rig) =>
            {
                ShowAtHalf(rig);
                rig.Animation.clip = rig.Hit;

                rig.Container.SetProgress(0.5000001f);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Hit));
                Assert.That(rig.Container.CorruptionProgress.Value, Is.EqualTo(0.5000001f), "the value is stored first");

                rig.Container.SetProgress(0.6f);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Normal), "control: a real change picks a clip");
            });
        }

        // Full progress (Approximately(progress, 1), 0x629b96) plays the outro.
        [Test]
        public void FullProgressPlaysTheOutro()
        {
            WithMeter((owned, rig) =>
            {
                ShowAtHalf(rig);

                rig.Container.SetProgress(1f);

                Assert.That(rig.Animation.clip, Is.SameAs(rig.Outro));
                Assert.That(rig.Container.CorruptionProgress.Value, Is.EqualTo(1f));
            });
        }

        // Anything else plays the normal clip only when it is not already the current one (op_Inequality at
        // 0x629c42): after the clip is stopped a second in-between value does not start it again.
        [Test]
        public void OtherProgressPlaysTheNormalClipOnlyWhenItIsNotCurrent()
        {
            WithMeter((owned, rig) =>
            {
                ShowAtHalf(rig);
                rig.Container.SetProgress(1f);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Outro), "set-up");

                rig.Container.SetProgress(0.4f);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Normal));

                rig.Animation.Stop();
                rig.Container.SetProgress(0.45f);
                Assert.That(rig.Animation.IsPlaying(rig.Normal.name), Is.False, "the current normal clip is not restarted");
            });
        }

        // PlayAnimation 0x6298e0 ignores a missing clip and otherwise makes it current and plays it.
        [Test]
        public void PlayAnimationIgnoresAMissingClip()
        {
            WithMeter((owned, rig) =>
            {
                Call(rig.Container, "PlayAnimation", new object[] { null });
                Assert.That(rig.Animation.clip, Is.Null);

                Call(rig.Container, "PlayAnimation", rig.Outro);
                Assert.That(rig.Animation.clip, Is.SameAs(rig.Outro));
                Assert.That(rig.Animation.IsPlaying(rig.Outro.name), Is.True);
            });
        }

        // The disable-time predicate <SetEnabled>b__1 (0x629e70): true once the state is no longer enabled, or when
        // its time has reached its length (Mathf.Approximately, 0x629f31); an enabled state part-way through is false.
        [Test]
        public void TheDisablePredicateWaitsForTheClipToFinish()
        {
            WithMeter((owned, rig) =>
            {
                AnimationState state = rig.Animation[rig.Outro.name];
                Type closure = typeof(UIContainerCorruption).GetNestedTypes(BindingFlags.NonPublic).Single();
                object instance = Activator.CreateInstance(closure);
                closure.GetField("animationState").SetValue(instance, state);
                MethodInfo predicate = closure.GetMethod("<SetEnabled>b__1", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                state.enabled = false;
                Assert.That((bool)predicate.Invoke(instance, null), Is.True, "a stopped state is finished");

                state.enabled = true;
                state.time = 0.25f;
                Assert.That((bool)predicate.Invoke(instance, null), Is.False, "a quarter through a one second clip");

                state.time = state.length;
                Assert.That((bool)predicate.Invoke(instance, null), Is.True);
            });
        }
    }
}
