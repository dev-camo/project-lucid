using System;
using System.Collections;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SequencedParticleEffectSpawner (TypeDef 0x02000623, x86-64 0x569F10..0x56A12F) and
    // SequencedParticles (0x02000624 with its iterator 0x02000625, 0x56A130..0x56A60F). Components are real, on
    // owned hosts; the effects run through their public PlayEffect entry (open input gate), and the particle
    // iterator is stepped by hand because Edit Mode never ticks coroutines. Authored fields are written
    // through SerializedObject, as the inspector does. No ParticleEffectManager is registered, so the real
    // spawner reports completion itself.
    public sealed class SequencedParticleEffectTests
    {
        private sealed class OtherEffectData : IEffectData { }

        private static void Author(Object target, Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Trigger(SequencedEffect effect, IEffectData data, Action onComplete) => effect.PlayEffect(in data, onComplete);

        private static void WithSpawnerEffect(Action<Owned, ParticleEffectSpawner, SequencedParticleEffectSpawner> body, bool withSpawner = true) => Owned.Run(owned =>
        {
            // The spawner's initialiser takes a typed reference to this row.
            owned.LeaseRow<ParticleEffectManager>();
            GameObject host = owned.Host("Lucid sequenced particle spawner", false);
            ParticleEffectSpawner spawner = withSpawner ? host.AddComponent<ParticleEffectSpawner>() : null;
            var effect = host.AddComponent<SequencedParticleEffectSpawner>();
            body(owned, spawner, effect);
        });

        // Reset 0x56A030: a Unity-null spawner is replaced by the sibling's GetComponent, an assigned one is kept.
        [Test]
        public void ResetFindsTheSiblingSpawnerOnlyWhileNoneIsAssigned()
        {
            WithSpawnerEffect((owned, spawner, effect) =>
            {
                Assert.That(Get<ParticleEffectSpawner>(effect, "m_effectSpawner"), Is.SameAs(spawner), "found when added");
                ParticleEffectSpawner other = owned.Host("Lucid other particle spawner", false).AddComponent<ParticleEffectSpawner>();
                Author(effect, serialized => serialized.FindProperty("m_effectSpawner").objectReferenceValue = other);
                Call(effect, "Reset");
                Assert.That(Get<ParticleEffectSpawner>(effect, "m_effectSpawner"), Is.SameAs(other), "an assigned spawner is kept");
            });
            WithSpawnerEffect((owned, spawner, effect) =>
                Assert.That(Get<ParticleEffectSpawner>(effect, "m_effectSpawner"), Is.Null, "no sibling"), withSpawner: false);
        }

        // OnEffectTriggered 0x569F10 with the override on and targeted data holding a target: SetSpawnPoint (0x6d1b30)
        // receives the target and the offset scaled component-wise by the authored multiplier (mulps/mulss).
        [Test]
        public void TargetedDataMovesTheSpawnPointWithTheScaledOffset()
        {
            foreach (bool wait in new[] { false, true })
                WithSpawnerEffect((owned, spawner, effect) =>
                {
                    Author(effect, serialized =>
                    {
                        serialized.FindProperty("m_overrideSpawnWithTargetedEffect").boolValue = true;
                        serialized.FindProperty("m_waitForParticleEffectToFinish").boolValue = wait;
                        serialized.FindProperty("m_offsetMultiplier").vector3Value = new Vector3(2f, 3f, 4f);
                    });
                    Transform target = owned.Host("Lucid spawn target", false).transform;
                    int completed = 0;
                    Trigger(effect, new TargetedEffect { Target = target, Offset = new Vector3(5f, 7f, 11f) }, () => completed++);
                    Assert.That(Get<Transform>(spawner, "m_spawnPoint"), Is.SameAs(target));
                    Assert.That(Get<Vector3>(spawner, "m_spawnOffset"), Is.EqualTo(new Vector3(10f, 21f, 44f)));
                    Assert.That(completed, Is.EqualTo(1), "no manager: the spawner completes at once (wait " + wait + ")");
                });
        }

        // The override is skipped (0x569F48/0x569F52/0x569F6x/0x569FAB) when it is off, the data is missing or not
        // targeted, or the target is Unity-null; the effect still spawns and completes.
        [Test]
        public void TheSpawnPointIsLeftAloneWithoutAnApplicableOverride()
        {
            var cases = new (string name, bool overrideOn, Func<Owned, IEffectData> data)[]
            {
                ("override off", false, owned => new TargetedEffect { Target = owned.Host("Lucid unused target", false).transform, Offset = Vector3.one }),
                ("no data", true, owned => null),
                ("not targeted", true, owned => new OtherEffectData()),
                ("no target", true, owned => new TargetedEffect { Offset = Vector3.one }),
            };
            foreach (var testCase in cases)
                WithSpawnerEffect((owned, spawner, effect) =>
                {
                    Author(effect, serialized => serialized.FindProperty("m_overrideSpawnWithTargetedEffect").boolValue = testCase.overrideOn);
                    Transform ownSpawnPoint = Get<Transform>(spawner, "m_spawnPoint");
                    Assert.That(ownSpawnPoint, Is.SameAs(spawner.transform), testCase.name + ": the spawner's own transform from Reset");
                    int completed = 0;
                    Trigger(effect, testCase.data(owned), () => completed++);
                    Assert.That(Get<Transform>(spawner, "m_spawnPoint"), Is.SameAs(ownSpawnPoint), testCase.name);
                    Assert.That(Get<Vector3>(spawner, "m_spawnOffset"), Is.EqualTo(Vector3.zero), testCase.name);
                    Assert.That(completed, Is.EqualTo(1), testCase.name);
                });
        }

        // 0x569FFB: after a non-waiting spawn the completion is invoked through a null test.
        [Test]
        public void ANonWaitingSpawnWithoutACallbackDoesNotFault()
        {
            WithSpawnerEffect((owned, spawner, effect) =>
                Assert.DoesNotThrow(() => Trigger(effect, new TargetedEffect(), null)));
        }

        private static void WithParticles(Action<Owned, SequencedParticles, ParticleSystem> body) => Owned.Run(owned =>
        {
            ParticleSystem system = owned.Host("Lucid sequenced particle system", true).AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var effect = owned.Host("Lucid sequenced particles", false).AddComponent<SequencedParticles>();
            Author(effect, serialized => serialized.FindProperty("m_particleSystem").objectReferenceValue = system);
            body(owned, effect, system);
        });

        private static void SetWait(SequencedParticles effect, bool wait) =>
            Author(effect, serialized => serialized.FindProperty("m_waitForParticleCompletion").boolValue = wait);

        // The constructor 0x56A3C0 stores true to m_waitForParticleCompletion (+0x68).
        [Test]
        public void WaitingForTheParticlesIsTheDefault()
        {
            WithParticles((owned, effect, system) => Assert.That(Get<bool>(effect, "m_waitForParticleCompletion"), Is.True));
        }

        // OnEffectTriggered 0x56A130, plain system mode: Play(true) (0x56a1e1), then the targeted-effect cast
        // (castclass, so null passes), and an inactive host (get_activeInHierarchy 0x56a22c) skips the coroutine
        // and falls through to the completion call whether or not it waits.
        [Test]
        public void AnInactiveHostCompletesAtOnceWhetherOrNotItWaits()
        {
            foreach (bool wait in new[] { true, false })
                WithParticles((owned, effect, system) =>
                {
                    SetWait(effect, wait);
                    int completed = 0;
                    Trigger(effect, new TargetedEffect(), () => completed++);
                    Assert.That(completed, Is.EqualTo(1), "wait " + wait);
                    Trigger(effect, null, () => completed++);
                    Assert.That(completed, Is.EqualTo(2), "null data passes the cast (wait " + wait + ")");
                });
        }

        // The cast at 0x56a1f2..0x56a2cf raises InvalidCastException for any other effect data.
        [Test]
        public void DataThatIsNotTargetedIsRejectedInSystemMode()
        {
            WithParticles((owned, effect, system) =>
            {
                int completed = 0;
                Assert.Throws<InvalidCastException>(() => Trigger(effect, new OtherEffectData(), () => completed++));
                Assert.That(completed, Is.EqualTo(0));
            });
        }

        // MoveNext 0x56A410: a system that is not playing is stopped at once (0x56a571); the completion is then
        // invoked only when waiting (cmp [r14+0x68] at 0x56a576), and the iterator ends without yielding.
        [Test]
        public void ASystemThatIsNotPlayingEndsTheWaitAtOnce()
        {
            foreach (bool wait in new[] { true, false })
                WithParticles((owned, effect, system) =>
                {
                    SetWait(effect, wait);
                    int completed = 0;
                    // The trigger stores the completion callback (and starts the system, which is stopped again).
                    Trigger(effect, null, () => completed++);
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    completed = 0;
                    Assert.That(system.isPlaying, Is.False, "precondition: the owned system is stopped");
                    var steps = (IEnumerator)Call(effect, "WaitForParticles", new TargetedEffect { Target = effect.transform });
                    Assert.That(steps.MoveNext(), Is.False);
                    Assert.That(completed, Is.EqualTo(wait ? 1 : 0), "wait " + wait);
                });
        }

        // MoveNext 0x56A410 while the system plays: with m_playOnTarget the system's transform is placed at the
        // target's position plus its up axis scaled by the offset's Y (0x56a47f..0x56a541, Transform.up 0x56a4a9
        // and Offset.y at +0x14), with the target's rotation, and the step yields null. A target turned 90
        // degrees about Z has up = -X, so (1,2,3) + (-X) * 5 = (-4,2,3).
        [Test]
        public void APlayingSystemFollowsTheTargetsUpOffsetPointWhenPlayingOnTarget()
        {
            WithParticles((owned, effect, system) =>
            {
                Author(effect, serialized => serialized.FindProperty("m_playOnTarget").boolValue = true);
                Transform target = owned.Host("Lucid particle target", false).transform;
                target.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 0f, 90f));
                system.Play(true);
                Assert.That(system.isPlaying, Is.True, "precondition: Play starts the owned system");
                var steps = (IEnumerator)Call(effect, "WaitForParticles", new TargetedEffect { Target = target, Offset = new Vector3(9f, 5f, 9f) });
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.Null);
                Vector3 position = system.transform.position;
                Assert.That(position.x, Is.EqualTo(-4f).Within(1e-4f));
                Assert.That(position.y, Is.EqualTo(2f).Within(1e-4f));
                Assert.That(position.z, Is.EqualTo(3f).Within(1e-4f));
                Assert.That(Quaternion.Angle(system.transform.rotation, target.rotation), Is.LessThan(1e-3f));
            });
        }

        // Without m_playOnTarget (cmp [r14+0x58] at 0x56a458) the playing system stays where it is.
        [Test]
        public void APlayingSystemStaysPutWithoutPlayOnTarget()
        {
            WithParticles((owned, effect, system) =>
            {
                Transform target = owned.Host("Lucid particle target", false).transform;
                target.position = new Vector3(1f, 2f, 3f);
                system.transform.position = new Vector3(7f, 8f, 9f);
                system.Play(true);
                Assert.That(system.isPlaying, Is.True, "precondition: Play starts the owned system");
                var steps = (IEnumerator)Call(effect, "WaitForParticles", new TargetedEffect { Target = target });
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(system.transform.position, Is.EqualTo(new Vector3(7f, 8f, 9f)));
            });
        }

        // OnValidate 0x56A380 selects the field to clear with the mode flag: the particle system when wrapper mode
        // is on, otherwise the wrapper (which this test cannot assign, so only the kept system is observable).
        [Test]
        public void OnValidateClearsTheSystemOnlyInWrapperMode()
        {
            WithParticles((owned, effect, system) =>
            {
                Call(effect, "OnValidate");
                Assert.That(Get<ParticleSystem>(effect, "m_particleSystem"), Is.SameAs(system), "system mode keeps the system");
                Author(effect, serialized => serialized.FindProperty("m_useParticleEffectWrapper").boolValue = true);
                Call(effect, "OnValidate");
                Assert.That(Get<ParticleSystem>(effect, "m_particleSystem"), Is.Null, "wrapper mode drops it");
            });
        }
    }
}
