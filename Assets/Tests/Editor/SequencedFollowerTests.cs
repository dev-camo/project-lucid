using System;
using System.Collections;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SequencedFollower (TypeDef 0x0200061b, x86-64 0x568C60..0x568E20, iterator MoveNext
    // 0x568F40). Components are real, on owned inactive hosts. The TimeManager is a real component holding the
    // Effects category at scale 2 with the fixed time step set to 0.25, so every fixed step is exactly 0.5 seconds
    // and every expected position below is exact. The iterator is stepped directly: starting it needs a running
    // coroutine host, so the active-host dispatch through StartCoroutine is not covered here.
    public sealed class SequencedFollowerTests
    {
        private sealed class UnrelatedEffectData : IEffectData { }

        private sealed class Scene
        {
            public SequencedFollower Follower;
            public Transform Target;
            public TargetedEffect Data;
            public int Completed;
        }

        private static void WithFollower(Action<Scene> body) => Owned.Run(owned =>
        {
            float priorStep = Time.fixedDeltaTime;
            owned.Lease(new ReleaseAction(() => Time.fixedDeltaTime = priorStep));
            Time.fixedDeltaTime = 0.25f;
            owned.Register(NewTimeManager(owned, TimeCategory.Effects, 2f));
            var scene = new Scene();
            scene.Follower = owned.Host("Lucid owned sequenced follower", false).AddComponent<SequencedFollower>();
            scene.Target = owned.Host("Lucid owned follow target", false).transform;
            scene.Target.position = new Vector3(10f, 0f, 0f);
            scene.Data = new TargetedEffect { Target = scene.Target };
            // PlayEffect is the only public way to store the completion callback, but it also triggers the
            // effect, so the stepped cases store it directly.
            Set(scene.Follower, "m_onComplete", (Action)(() => scene.Completed++));
            body(scene);
        });

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator Steps(Scene scene) => (IEnumerator)Call(scene.Follower, "MoveToTransform", scene.Data);

        // Constructor 0x568E20: Vector3.zero (0x568e88), true (0x568e92), 1.0f (0x568e96), Vector3.one (0x568ec9),
        // the manager reference (0x568eeb..0x568f05) and Effects (0x568f11, -2001650353).
        [Test]
        public void ConstructionStoresTheOriginalDefaults()
        {
            WithFollower(scene =>
            {
                var serialized = new SerializedObject(scene.Follower);
                Assert.That(serialized.FindProperty("m_offsetLocal").vector3Value, Is.EqualTo(Vector3.zero));
                Assert.That(serialized.FindProperty("m_offsetStatic").boolValue, Is.True);
                Assert.That(serialized.FindProperty("m_duration").floatValue, Is.EqualTo(1f));
                Assert.That(serialized.FindProperty("m_scalingApplied").boolValue, Is.False);
                Assert.That(serialized.FindProperty("m_scalingTarget").vector3Value, Is.EqualTo(Vector3.one));
                Assert.That((int)Get<TimeCategory>(scene.Follower, "m_timeCategory"), Is.EqualTo(-2001650353));
                Assert.That(Get<SystemRef<TimeManager>>(scene.Follower, "m_timeManagerRef").GetSafe(), Is.Not.Null);
            });
        }

        // 0x568cc8..0x568cd9: an object that is not active in the hierarchy completes at once, through the stored
        // callback (0x568d52..0x568d6c), without moving.
        [Test]
        public void AnInactiveObjectCompletesImmediatelyWithoutMoving()
        {
            WithFollower(scene =>
            {
                scene.Follower.transform.position = Vector3.zero;
                IEffectData data = scene.Data;
                scene.Follower.PlayEffect(in data, () => scene.Completed++);
                Assert.That(scene.Completed, Is.EqualTo(1));
                Assert.That(scene.Follower.transform.position, Is.EqualTo(Vector3.zero));
            });
        }

        // 0x568c8c..0x568cbd: the TargetedEffect cast comes first and faults for any other effect data; a null
        // reference passes the cast (0x568c8c) and, on an inactive object, completes.
        [Test]
        public void TheDataIsCastBeforeTheActivityCheck()
        {
            WithFollower(scene =>
            {
                IEffectData unrelated = new UnrelatedEffectData();
                Assert.Throws<InvalidCastException>(() => scene.Follower.PlayEffect(in unrelated, () => scene.Completed++));
                Assert.That(scene.Completed, Is.EqualTo(0));
                IEffectData none = null;
                scene.Follower.PlayEffect(in none, () => scene.Completed++);
                Assert.That(scene.Completed, Is.EqualTo(1));
            });
        }

        // 0x568f40: start offset -10 (follower at the origin, target at x=10); each step takes 0.5 of the 1 second
        // duration, so the elapsed fraction is 0.5 and then 1 (0x5691f4..0x569256), then the loop ends and the
        // callback runs (0x5690d4..0x5690f2).
        [Test]
        public void TheFollowerReachesTheTargetInStepsThenCompletes()
        {
            WithFollower(scene =>
            {
                IEnumerator steps = Steps(scene);
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.InstanceOf<WaitForFixedUpdate>());
                Assert.That(scene.Follower.transform.position, Is.EqualTo(new Vector3(5f, 0f, 0f)));
                Assert.That(scene.Completed, Is.EqualTo(0));
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(scene.Follower.transform.position, Is.EqualTo(new Vector3(10f, 0f, 0f)));
                Assert.That(scene.Completed, Is.EqualTo(0));
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(scene.Completed, Is.EqualTo(1));
                Assert.That(scene.Follower.transform.position, Is.EqualTo(new Vector3(10f, 0f, 0f)));
            });
        }

        // 0x569128..0x569138: the remaining time is clamped at zero (maxss), so a duration shorter than one step
        // arrives in the first step.
        [Test]
        public void ADurationShorterThanAStepArrivesInTheFirstStep()
        {
            WithFollower(scene =>
            {
                SetSerialized(scene.Follower, "m_duration", property => property.floatValue = 0.25f);
                IEnumerator steps = Steps(scene);
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(scene.Follower.transform.position, Is.EqualTo(new Vector3(10f, 0f, 0f)));
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(scene.Completed, Is.EqualTo(1));
            });
        }

        // 0x5690cc..0x5690d2: the loop runs only for a strictly positive remaining time, so a zero or negative
        // duration completes in the first call without any movement.
        [Test]
        public void ANonPositiveDurationCompletesWithoutMoving()
        {
            foreach (float duration in new[] { 0f, -1f })
            {
                WithFollower(scene =>
                {
                    SetSerialized(scene.Follower, "m_duration", property => property.floatValue = duration);
                    scene.Follower.transform.position = new Vector3(1f, 2f, 3f);
                    Assert.That(Steps(scene).MoveNext(), Is.False, "duration " + duration);
                    Assert.That(scene.Completed, Is.EqualTo(1));
                    Assert.That(scene.Follower.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                });
            }
        }

        // 0x569085..0x569089 rotates the offset once at the start; 0x569143 (m_offsetStatic) selects whether the
        // loop keeps it (0x56914f) or rotates it again by the target's current rotation (0x569180..0x5691a5). The
        // target turns 90 degrees about Y between the two steps; the final offset is the unrotated (0,0,1) when
        // static and the rotated (1,0,0) otherwise.
        [Test]
        public void ANonStaticOffsetFollowsTheTargetsRotation()
        {
            foreach ((bool offsetStatic, Vector3 expected) in new[]
            {
                (true, new Vector3(10f, 0f, 1f)),
                (false, new Vector3(11f, 0f, 0f)),
            })
            {
                WithFollower(scene =>
                {
                    SetSerialized(scene.Follower, "m_offsetLocal", property => property.vector3Value = new Vector3(0f, 0f, 1f));
                    SetSerialized(scene.Follower, "m_offsetStatic", property => property.boolValue = offsetStatic);
                    IEnumerator steps = Steps(scene);
                    Assert.That(steps.MoveNext(), Is.True);
                    Assert.That(scene.Follower.transform.position, Is.EqualTo(new Vector3(5f, 0f, 0.5f)), "unrotated first step");
                    scene.Target.rotation = Quaternion.Euler(0f, 90f, 0f);
                    Assert.That(steps.MoveNext(), Is.True);
                    Assert.That(scene.Follower.transform.position.x, Is.EqualTo(expected.x).Within(1e-5f), "static " + offsetStatic);
                    Assert.That(scene.Follower.transform.position.y, Is.EqualTo(expected.y).Within(1e-5f), "static " + offsetStatic);
                    Assert.That(scene.Follower.transform.position.z, Is.EqualTo(expected.z).Within(1e-5f), "static " + offsetStatic);
                });
            }
        }

        // 0x56925d (m_scalingApplied): the local scale lerps from its start value to the target by the same
        // elapsed fraction (0x569273..0x5692a6) and is left alone when scaling is not applied.
        [Test]
        public void ScalingFollowsTheSameFractionOnlyWhenApplied()
        {
            foreach (bool applied in new[] { true, false })
            {
                WithFollower(scene =>
                {
                    SetSerialized(scene.Follower, "m_scalingApplied", property => property.boolValue = applied);
                    SetSerialized(scene.Follower, "m_scalingTarget", property => property.vector3Value = new Vector3(3f, 5f, 7f));
                    IEnumerator steps = Steps(scene);
                    Assert.That(steps.MoveNext(), Is.True);
                    Assert.That(scene.Follower.transform.localScale, Is.EqualTo(applied ? new Vector3(2f, 3f, 4f) : Vector3.one));
                    Assert.That(steps.MoveNext(), Is.True);
                    Assert.That(scene.Follower.transform.localScale, Is.EqualTo(applied ? new Vector3(3f, 5f, 7f) : Vector3.one));
                });
            }
        }
    }
}
