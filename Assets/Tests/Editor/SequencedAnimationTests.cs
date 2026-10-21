using System;
using System.Collections;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SequencedAnimation (TypeDef 0x0200060f, x86-64 0x567F60..0x568200, iterator MoveNext
    // 0x568250). The effect sits on an owned inactive host, so triggering it takes the real OnEffectTriggered path up to
    // StartCoroutine, which the engine refuses with its logged error; the iterator is then stepped directly. The
    // legacy Animation lives on an active host, with a two second clip that animates a child only, so the collector
    // pose is never overwritten by the sampled clip. The fixed time step is 0.25 and the Effects scale 2 (so the step
    // is told apart from the raw fixed time step), and with the length multiplier 0.5 each step advances the
    // animation by exactly 1 second.
    public sealed class SequencedAnimationTests
    {
        private sealed class Scene
        {
            public Owned Owned;
            public SequencedAnimation Effect;
            public Animation Animation;
            public AnimationClip Clip;
            public Transform AnimationRoot;
            public TargetedEffect Data;
            public int Completed;
        }

        private static void WithEffect(Action<Scene> body, float clipLength = 2f, float lengthMultiplier = 0.5f) => Owned.Run(owned =>
        {
            float priorStep = Time.fixedDeltaTime;
            owned.Lease(new ReleaseAction(() => Time.fixedDeltaTime = priorStep));
            Time.fixedDeltaTime = 0.25f;
            owned.Register(NewTimeManager(owned, TimeCategory.Effects, 2f));
            var scene = new Scene { Owned = owned };
            GameObject animationHost = owned.Host("Lucid owned sequenced animation", true);
            owned.Host("Lucid animated child", true, animationHost.transform);
            scene.Animation = animationHost.AddComponent<Animation>();
            scene.Clip = owned.Track(new AnimationClip { legacy = true, name = "LucidSequencedClip" });
            if (clipLength > 0f)
                scene.Clip.SetCurve("Lucid animated child", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, clipLength, 1f));
            scene.Animation.AddClip(scene.Clip, scene.Clip.name);
            scene.Animation.clip = scene.Clip;
            scene.Animation.playAutomatically = false;
            scene.Effect = owned.Host("Lucid owned sequenced animation effect", false).AddComponent<SequencedAnimation>();
            SetSerialized(scene.Effect, "m_animation", property => property.objectReferenceValue = scene.Animation);
            SetSerialized(scene.Effect, "m_lengthMultiplier", property => property.floatValue = lengthMultiplier);
            scene.AnimationRoot = owned.Host("Lucid collector root", false).transform;
            scene.Data = new TargetedEffect { Target = owned.Host("Lucid owned animation target", false).transform };
            body(scene);
        });

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Triggers the effect as the sequence does. The engine refuses the coroutine of an inactive host with one
        // logged error, after the effect has stored the time manager and the collector root; the real iterator is
        // then stepped by hand.
        private static IEnumerator Trigger(Scene scene)
        {
            LogAssert.Expect(LogType.Error, new Regex("Coroutine couldn't be started"));
            IEffectData data = scene.Data;
            scene.Effect.PlayEffect(in data, () => scene.Completed++);
            Assert.That(scene.Completed, Is.EqualTo(0), "triggering only starts the animation");
            return (IEnumerator)Call(scene.Effect, "WaitForAnimation", scene.Data);
        }

        // Constructor 0x568200: m_lengthMultiplier 1.0f (0x568204) and Effects (0x56820b, -2001650353).
        [Test]
        public void ConstructionStoresTheOriginalDefaults()
        {
            Owned.Run(owned =>
            {
                var effect = owned.Host("Lucid owned sequenced animation defaults", false).AddComponent<SequencedAnimation>();
                Assert.That(new SerializedObject(effect).FindProperty("m_lengthMultiplier").floatValue, Is.EqualTo(1f));
                Assert.That((int)Get<TimeCategory>(effect, "m_timeCategory"), Is.EqualTo(-2001650353));
                Assert.That(effect.Progress, Is.EqualTo(0f));
            });
        }

        // 0x567f64..0x567f76: the progress is the animation time over its length, and zero until the length is
        // positive. A step later it is half and then all of the two second clip.
        [Test]
        public void ProgressIsTheFractionOfTheClipAndResetsToZero()
        {
            WithEffect(scene =>
            {
                IEnumerator steps = Trigger(scene);
                Assert.That(scene.Effect.Progress, Is.EqualTo(0f), "no length yet");
                foreach (float expected in new[] { 0.5f, 1f })
                {
                    Assert.That(steps.MoveNext(), Is.True);
                    Assert.That(steps.Current, Is.InstanceOf<WaitForFixedUpdate>());
                    Assert.That(scene.Effect.Progress, Is.EqualTo(expected));
                }
                scene.Effect.OnReset();
                Assert.That(scene.Effect.Progress, Is.EqualTo(0f), "OnReset (0x5681c4) clears only the time");
            });
        }

        // 0x56832d..0x56833d divides the state's speed by the multiplier and 0x5684e9..0x5684f4 multiplies it back
        // once the clip is over (0x568394: strictly time < length), then stops the animation and completes
        // (0x568504..0x56851a).
        [Test]
        public void TheSpeedIsScaledWhilePlayingAndRestoredWhenTheClipEnds()
        {
            WithEffect(scene =>
            {
                IEnumerator steps = Trigger(scene);
                AnimationState state = scene.Animation[scene.Clip.name];
                Assert.That(state.speed, Is.EqualTo(1f));
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(state.speed, Is.EqualTo(2f), "1 / 0.5");
                Assert.That(state.time, Is.EqualTo(1f), "(0.25 * 2) / 0.5; the raw fixed step would give 0.5");
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(state.time, Is.EqualTo(2f));
                Assert.That(scene.Completed, Is.EqualTo(0));
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(state.speed, Is.EqualTo(1f), "2 * 0.5");
                Assert.That(scene.Completed, Is.EqualTo(1));
            });
        }

        // 0x568394..0x568397: a clip without length never enters the loop, so the first call restores the speed and
        // completes with no progress.
        [Test]
        public void AZeroLengthClipCompletesInTheFirstCall()
        {
            WithEffect(scene =>
            {
                IEnumerator steps = Trigger(scene);
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(scene.Completed, Is.EqualTo(1));
                Assert.That(scene.Effect.Progress, Is.EqualTo(0f));
            }, clipLength: 0f);
        }

        // 0x56804a..0x568091: with the collector flag the root defaults to the animation's own transform, but only
        // while it is Unity null; without the flag nothing is assigned.
        [Test]
        public void TheCollectorRootDefaultsToTheAnimationsTransformOnlyWhenUnassigned()
        {
            WithEffect(scene =>
            {
                Trigger(scene);
                Assert.That(Get<Transform>(scene.Effect, "m_animationRoot") == null, Is.True, "no collector flag");
            });
            WithEffect(scene =>
            {
                SetSerialized(scene.Effect, "m_playOnCollector", property => property.boolValue = true);
                Trigger(scene);
                Assert.That(Get<Transform>(scene.Effect, "m_animationRoot"), Is.SameAs(scene.Animation.transform));
            });
            WithEffect(scene =>
            {
                SetSerialized(scene.Effect, "m_playOnCollector", property => property.boolValue = true);
                SetSerialized(scene.Effect, "m_animationRoot", property => property.objectReferenceValue = scene.AnimationRoot);
                Trigger(scene);
                Assert.That(Get<Transform>(scene.Effect, "m_animationRoot"), Is.SameAs(scene.AnimationRoot));
            });
        }

        // 0x5683a2..0x568466: before each sample the root takes the target's position plus its up vector scaled by
        // the offset's Y, and the target's rotation (SetPositionAndRotation). The target turns about Z between the
        // two steps, so its up vector becomes (-1,0,0).
        [Test]
        public void TheCollectorRootFollowsTheTargetAlongItsUpVector()
        {
            WithEffect(scene =>
            {
                SetSerialized(scene.Effect, "m_playOnCollector", property => property.boolValue = true);
                SetSerialized(scene.Effect, "m_animationRoot", property => property.objectReferenceValue = scene.AnimationRoot);
                scene.Data.Target.position = new Vector3(1f, 2f, 3f);
                scene.Data.Offset = new Vector3(100f, 2f, 100f);
                IEnumerator steps = Trigger(scene);
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(scene.AnimationRoot.position, Is.EqualTo(new Vector3(1f, 4f, 3f)), "up is (0,1,0)");
                scene.Data.Target.rotation = Quaternion.Euler(0f, 0f, 90f);
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(scene.AnimationRoot.position.x, Is.EqualTo(-1f).Within(1e-5f));
                Assert.That(scene.AnimationRoot.position.y, Is.EqualTo(2f).Within(1e-5f));
                Assert.That(scene.AnimationRoot.position.z, Is.EqualTo(3f).Within(1e-5f));
                Assert.That(Quaternion.Angle(scene.AnimationRoot.rotation, Quaternion.Euler(0f, 0f, 90f)), Is.EqualTo(0f).Within(1e-3f));
            });
        }

        // Without the collector flag the root is never posed.
        [Test]
        public void WithoutTheCollectorFlagTheRootStaysWhereItIs()
        {
            WithEffect(scene =>
            {
                SetSerialized(scene.Effect, "m_animationRoot", property => property.objectReferenceValue = scene.AnimationRoot);
                scene.Data.Target.position = new Vector3(1f, 2f, 3f);
                IEnumerator steps = Trigger(scene);
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(scene.AnimationRoot.position, Is.EqualTo(Vector3.zero));
            });
        }
    }
}
