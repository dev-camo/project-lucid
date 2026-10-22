using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using ProjectLucid.Tests.WorldSurfaceShared;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The enemy brain transitions: the shared base, the nine transitions of the brain's own state and the one that
    // reads the fire-projectile ability of the brain's enemy (EnemyBrainTransition_FireProjectileTriggered).
    // Expected values come from the 1.10.1 x86-64 bodies cited per test. Each transition is built through its own
    // ConstructInstance and updated against a real EnemyBrain with its own storage. An Enemy that has not started
    // holds no definition (Start reads it from the DataManager), so the traits-reading bodies run against a real
    // EnemyDefinition handed to the Enemy's definition field. An unstarted Enemy cannot be moved (its SetWorldPosition
    // needs the transform Start caches), so every enemy stands at the origin and the targets are test actors.
    public sealed class EnemyBrainTransitionTests : OwnedGraphNamesFixture
    {
        public EnemyBrainTransitionTests() : base("Lucid-enemy-") { }

        private delegate IFSMTransition Factory(FiniteStateMachine fsm, FSMIdentifier id, string json);

        private static FSMIdentifier Id() => new FSMIdentifier(Name());

        private static IFSMTransition Build(Factory factory, string json = "{}") => factory(NewFsm(), Id(), json);

        // The four transitions that read one boolean flag of the brain storage, with the key each reads.
        private static readonly (string node, Factory factory, GraphStorageKey key)[] FlagTransitions =
        {
            (nameof(EnemyBrainTransition_AttackCycleComplete), EnemyBrainTransition_AttackCycleComplete.ConstructInstance, EnemyBrainFSMKeys.AttackAnimationCompleted),
            (nameof(EnemyBrainTransition_DestinationReached), EnemyBrainTransition_DestinationReached.ConstructInstance, EnemyBrainFSMKeys.MoveToDestinationReached),
            (nameof(EnemyBrainTransition_TargetReached), EnemyBrainTransition_TargetReached.ConstructInstance, EnemyBrainFSMKeys.TargetReached),
            (nameof(EnemyBrainTransition_WindUpCycleComplete), EnemyBrainTransition_WindUpCycleComplete.ConstructInstance, EnemyBrainFSMKeys.AttackWindUpComplete),
        };

        private static readonly (Type type, Factory factory)[] ConcreteTransitions =
        {
            (typeof(EnemyBrainTransition_AttackCycleComplete), EnemyBrainTransition_AttackCycleComplete.ConstructInstance),
            (typeof(EnemyBrainTransition_DestinationReached), EnemyBrainTransition_DestinationReached.ConstructInstance),
            (typeof(EnemyBrainTransition_IdleToMove), EnemyBrainTransition_IdleToMove.ConstructInstance),
            (typeof(EnemyBrainTransition_TargetLost), EnemyBrainTransition_TargetLost.ConstructInstance),
            (typeof(EnemyBrainTransition_TargetOutOfSightRange), EnemyBrainTransition_TargetOutOfSightRange.ConstructInstance),
            (typeof(EnemyBrainTransition_TargetReached), EnemyBrainTransition_TargetReached.ConstructInstance),
            (typeof(EnemyBrainTransition_TargetSpotted), EnemyBrainTransition_TargetSpotted.ConstructInstance),
            (typeof(EnemyBrainTransition_WatchToChaseTimeout), EnemyBrainTransition_WatchToChaseTimeout.ConstructInstance),
            (typeof(EnemyBrainTransition_WindUpCycleComplete), EnemyBrainTransition_WindUpCycleComplete.ConstructInstance),
        };

        // ---- the four flag transitions ----

        // 0x5744F0, 0x574710, 0x575660 and 0x576400 each end in a tail call of IGraphStorage.GetValue<bool> with one
        // key out of EnemyBrainFSMKeys (static offsets +0xD0, +0xE0, +0xF0 and +0xB0) and no other test: the stored flag
        // is the result. With the other three flags true and its own false each one is false, and with only its own true
        // it is true, so each reads exactly its own key.
        [Test]
        public void EachFlagTransitionReadsOnlyItsOwnStorageKey()
        {
            WithBrain((owned, brain, enemy) =>
            {
                foreach ((string node, Factory factory, GraphStorageKey key) in FlagTransitions)
                {
                    IFSMTransition transition = Build(factory);
                    foreach ((string otherNode, Factory otherFactory, GraphStorageKey otherKey) in FlagTransitions)
                        brain.Storage.SetValue(otherKey, otherNode != node);
                    Assert.That(transition.Update(brain, Context()), Is.False, node + " with its own flag false and the others true");
                    brain.Storage.SetValue(key, true);
                    Assert.That(transition.Update(brain, Context()), Is.True, node + " with its own flag true");
                    brain.Storage.SetValue(key, false);
                    Assert.That(transition.Update(brain, Context()), Is.False, node + " with its own flag false again");
                }
            });
        }

        // The tail call passes useDefault = true (r8d = 1 at 0x574659, 0x574808, 0x5757C9 and 0x576569) and the default
        // false (ecx = 0), so a missing flag is false and the read stores that default.
        [Test]
        public void AMissingFlagReadsFalseAndStoresTheDefault()
        {
            foreach ((string node, Factory factory, GraphStorageKey key) in FlagTransitions)
            {
                WithBrain((owned, brain, enemy) =>
                {
                    Assert.That(brain.Storage.HasValue(key), Is.False, node + " starts without the flag");
                    Assert.That(Build(factory).Update(brain, Context()), Is.False, node);
                    Assert.That(brain.Storage.HasValue(key), Is.True, node + " stores the default it read");
                    Assert.That(brain.Storage.GetValue(key, true, false), Is.False, node + " stored false");
                });
            }
        }

        // ---- the focus target transitions ----

        // 0x574C40 and 0x5752F0 read the focus target with ecx = 0 (null) and r8d = 0 (do not store) and finish in a
        // tail call of UnityEngine.Object.op_Equality whose result the void method drops. The one effect is therefore
        // that nothing is stored for a missing key and that a stored target is left in place. The read itself and the
        // dropped comparison have no observable effect, so this test cannot tell them from an empty body.
        [Test]
        public void EnteringTheFocusTransitionsStoresNoDefaultAndKeepsAStoredTarget()
        {
            foreach (Factory factory in new Factory[] { EnemyBrainTransition_TargetLost.ConstructInstance, EnemyBrainTransition_TargetOutOfSightRange.ConstructInstance })
            {
                WithBrain((owned, brain, enemy) =>
                {
                    IFSMTransition transition = Build(factory);
                    transition.OnEnter(brain, null);
                    Assert.That(brain.Storage.HasValue(EnemyBrainFSMKeys.FocusTarget), Is.False, "nothing is stored for a missing target");
                    brain.Storage.SetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, enemy);
                    transition.OnEnter(brain, null);
                    Assert.That(brain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, null, false), Is.SameAs(enemy), "a stored target is left alone");
                });
            }
        }

        // 0x574F43 tests the focus target read with r8d = 1 (0x574F15) against null and returns true (r12b = 1 at
        // 0x574F48, jne 0x575235): a brain without a focus target has lost it. The read stores the null default, which
        // the other transitions' entry reads do not.
        [Test]
        public void TargetLostIsTrueWithoutAFocusTargetAndStoresTheNullDefault()
        {
            WithBrain((owned, brain, enemy) =>
            {
                IFSMTransition transition = Build(EnemyBrainTransition_TargetLost.ConstructInstance);
                Assert.That(transition.Update(brain, Context()), Is.True);
                Assert.That(brain.Storage.HasValue(EnemyBrainFSMKeys.FocusTarget), Is.True, "useDefault was true");
                Assert.That(brain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, enemy, false), Is.Null);
                Assert.That(transition.Update(brain, Context()), Is.True, "a stored null target is still lost");
            });
        }

        // 0x575880 removes the focus target (RemoveValue<Actor> at 0x5759E2): a spotting starts without one.
        [Test]
        public void TargetSpottedClearsTheFocusTargetOnEntry()
        {
            WithBrain((owned, brain, enemy) =>
            {
                IFSMTransition transition = Build(EnemyBrainTransition_TargetSpotted.ConstructInstance);
                brain.Storage.SetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, enemy);
                transition.OnEnter(brain, null);
                Assert.That(brain.Storage.HasValue(EnemyBrainFSMKeys.FocusTarget), Is.False);
                Assert.That(() => transition.OnEnter(brain, null), Throws.Nothing, "removing a missing key is harmless");
            });
        }

        // 0x575AA2 compares the brain's sensed target with null (op_Equality, test al, jne 0x575E3E with ebx = 0): no
        // target means nothing was spotted, and nothing is stored, neither the focus target nor a cached sighting.
        [Test]
        public void TargetSpottedSeesNothingWithoutASensedTarget()
        {
            WithBrain((owned, brain, enemy) =>
            {
                Assert.That(brain.Target, Is.Null);
                Assert.That(Build(EnemyBrainTransition_TargetSpotted.ConstructInstance).Update(brain, Context()), Is.False);
                Assert.That(brain.Storage.HasValue(EnemyBrainFSMKeys.FocusTarget), Is.False);
                Assert.That(brain.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedPosition), Is.False);
                Assert.That(brain.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedTime), Is.False);
            });
        }

        // ---- the timers ----

        // 0x576160 stores zero under WatchElapsedTime (+0x70) on entry, 0x576060 adds its argument to the stored time with
        // a zero default and stores the sum (AdjustValue<float>), and 0x576170 removes the key on leaving.
        [Test]
        public void TheWatchTimerRestartsOnEntryAccumulatesAndIsRemovedOnLeaving()
        {
            WithBrain((owned, brain, enemy) =>
            {
                var transition = (EnemyBrainTransition_WatchToChaseTimeout)Build(EnemyBrainTransition_WatchToChaseTimeout.ConstructInstance);
                Assert.That(transition.GetElapsedSeconds(brain), Is.EqualTo(0f), "a missing timer reads zero");
                Assert.That(brain.Storage.HasValue(EnemyBrainFSMKeys.WatchElapsedTime), Is.True, "the read stores the sum");
                Assert.That(transition.GetElapsedSeconds(brain, 0.25f), Is.EqualTo(0.25f));
                Assert.That(transition.GetElapsedSeconds(brain, 0.5f), Is.EqualTo(0.75f));
                Assert.That(transition.GetElapsedSeconds(brain), Is.EqualTo(0.75f), "the adjustment defaults to zero");

                transition.OnEnter(brain, null);
                Assert.That(brain.Storage.GetValue(EnemyBrainFSMKeys.WatchElapsedTime, -1f, false), Is.EqualTo(0f), "entering restarts the timer");

                brain.Storage.SetValue(EnemyBrainFSMKeys.WatchElapsedTime, 4f);
                transition.OnLeave(brain, null);
                Assert.That(brain.Storage.HasValue(EnemyBrainFSMKeys.WatchElapsedTime), Is.False, "leaving removes the timer");
                Assert.That(() => transition.OnLeave(brain, null), Throws.Nothing, "removing a missing timer is harmless");
            });
        }

        private static string[] CompareInstructions(Type type)
        {
            MethodInfo update = type.GetMethod("DoUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            return OriginalILCallSequence.Instructions(update)
                .Select(instruction => instruction.Code)
                .Where(code => code == OpCodes.Clt || code == OpCodes.Clt_Un || code == OpCodes.Cgt || code == OpCodes.Cgt_Un || code == OpCodes.Ceq)
                .Select(code => code.Name)
                .ToArray();
        }

        // An emission self-check of the restored source, not a native-derived oracle: the equal, greater and NaN
        // truth tables of the timers are the scene tests below. A floating >= compiles to clt.un followed by a negation,
        // where > would be cgt and <= would be cgt.un and a negation.
        [Test]
        public void EmissionCheck_IdleAndWatchTimerComparesCompileToGreaterOrEqual()
        {
            Assert.That(CompareInstructions(typeof(EnemyBrainTransition_IdleToMove)), Is.EqualTo(new[] { "clt.un", "ceq" }));
            Assert.That(CompareInstructions(typeof(EnemyBrainTransition_WatchToChaseTimeout)), Is.EqualTo(new[] { "clt.un", "ceq" }));
        }

        // ---- scenes with a definition, a navigation and sensed targets ----

        private sealed class Unsubscribe : IDisposable
        {
            private readonly ISystem m_system;

            public Unsubscribe(ISystem system) => m_system = system;

            public void Dispose() => ProcessManager.UnsubscribeFromAllActions(m_system);
        }

        // A brain on an Enemy at the origin with a real EnemyDefinition and its own EnemyManager. The manager is a
        // plain instance whose action subscriptions are leased and removed, not a registered system. The Enemy joins it
        // through its own registration method, and the manager hands out the list that SenseTarget reads.
        private sealed class Scene
        {
            public Owned Owned;
            public Enemy Enemy;
            public EnemyBrain Brain;
            public EnemyManager Manager;
            public EnemyTraits Traits;

            public EnemyTargetActorProbe Target(Vector3 position)
            {
                var probe = Owned.Host(Name(), false).AddComponent<EnemyTargetActorProbe>();
                probe.SetWorldPosition(position);
                return probe;
            }

            public EnemyNavigationForwardProbe Forward()
            {
                var navigation = Owned.Host(Name(), false).AddComponent<EnemyNavigationForwardProbe>();
                navigation.RunSetUp();
                Set(Enemy, "m_navigation", navigation);
                return navigation;
            }

            // A home-radius navigation at the origin that constrains the enemy to the given distance from home.
            public EnemyNavigationHomeRadiusProbe Within(float distance)
            {
                var navigation = Owned.Host(Name(), false).AddComponent<EnemyNavigationHomeRadiusProbe>();
                navigation.RunSetUp();
                Set(navigation, "m_constrain", true);
                Set(navigation, "m_constrainDistance", distance);
                Set(Enemy, "m_navigation", navigation);
                return navigation;
            }

            // The brain senses the closest reachable of the manager's targets, as it does on each fixed update.
            public void Sense(params EnemyTargetActorProbe[] targets)
            {
                ((List<Actor>)Manager.GetTargetsForEnemies()).AddRange(targets);
                Brain.OnFixedUpdate(0.02f);
            }
        }

        private static void WithScene(Action<Scene> body, Action<EnemyTraits> configure = null) => Owned.Run(owned =>
        {
            owned.Lease(new VerificationFixtureOwnership.ActionPoolLease());
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            var manager = new EnemyManager();
            owned.Lease(new Unsubscribe(manager));
            Enemy enemy = NewEnemy(owned);
            var brain = new EnemyBrain();
            brain.Initialise(enemy, NewFsm());
            Call(enemy, "RegisterToEnemyManager", manager);
            // CreateInstance sends Awake, whose ActorDefinition.UpdateCachedValues (0x7A8060) calls
            // Settings.UpdateCachedValues on the unassigned Settings (+0x28) with no null test.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
            var definition = owned.Track(ScriptableObject.CreateInstance<EnemyDefinition>());
            definition.Traits = new EnemyTraits();
            configure?.Invoke(definition.Traits);
            definition.Traits.UpdateCachedValues();
            Set(enemy, "m_definition", definition);
            body(new Scene { Owned = owned, Enemy = enemy, Brain = brain, Manager = manager, Traits = definition.Traits });
        });

        private static void AssertNear(Vector3 actual, Vector3 expected, string why) =>
            Assert.That((actual - expected).magnitude, Is.LessThan(1e-4f), why + ": expected " + expected + " but was " + actual);

        private static float Stored(Scene scene, GraphStorageKey key) => scene.Brain.Storage.GetValue(key, -1f, false);

        // 0x574B85 (ucomiss xmm0 = the stored time, [rax + 0x30] = IdleSeconds; setae): true when the time is greater
        // than or equal to the idle seconds and both are ordered. A missing time reads zero (useDefault, 0x574A60).
        [Test]
        public void IdleToMoveIsTrueWhenTheStoredTimeReachesTheIdleSeconds()
        {
            WithScene(scene =>
            {
                IFSMTransition transition = Build(EnemyBrainTransition_IdleToMove.ConstructInstance);
                scene.Traits.IdleSeconds = 5f;
                Assert.That(transition.Update(scene.Brain, Context()), Is.False, "a missing time reads zero");
                Assert.That(scene.Brain.Storage.HasValue(EnemyBrainFSMKeys.IdleElapsedTime), Is.True, "the read stores the default");
                scene.Traits.IdleSeconds = 0f;
                Assert.That(transition.Update(scene.Brain, Context()), Is.True, "zero against zero idle seconds");

                foreach ((float stored, float idleSeconds, bool expected) in new[]
                {
                    (5f, 5f, true), (4.999f, 5f, false), (5.001f, 5f, true), (float.NaN, 5f, false), (5f, float.NaN, false),
                })
                {
                    scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.IdleElapsedTime, stored);
                    scene.Traits.IdleSeconds = idleSeconds;
                    Assert.That(transition.Update(scene.Brain, Context()), Is.EqualTo(expected), "stored " + stored + ", idle seconds " + idleSeconds);
                }
            });
        }

        // 0x576345 (ucomiss xmm0 = the sum, [rax + 0x40] = WatchToChaseTimeout; setae) after the adjusting read at
        // 0x576060 added the update's delta: true when the sum reaches the timeout, and the sum is stored either way.
        [Test]
        public void WatchToChaseIsTrueWhenTheAccumulatedTimeReachesTheTimeout()
        {
            WithScene(scene =>
            {
                scene.Traits.WatchToChaseTimeout = 1f;
                IFSMTransition transition = Build(EnemyBrainTransition_WatchToChaseTimeout.ConstructInstance);
                foreach ((float stored, bool expected, float sum) in new[]
                {
                    (0.5f, true, 1f), (0.25f, false, 0.75f), (0.75f, true, 1.25f), (float.NaN, false, float.NaN),
                })
                {
                    scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.WatchElapsedTime, stored);
                    Assert.That(transition.Update(scene.Brain, Context(0.5f)), Is.EqualTo(expected), "stored " + stored);
                    Assert.That(Stored(scene, EnemyBrainFSMKeys.WatchElapsedTime), Is.EqualTo(sum), "the sum after stored " + stored);
                }
                scene.Traits.WatchToChaseTimeout = float.NaN;
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.WatchElapsedTime, 2f);
                Assert.That(transition.Update(scene.Brain, Context(0.5f)), Is.False, "an unordered timeout");
            });
        }

        // 0x574F69 -> 0x575224: without the caching trait the transition is the negated reachability of the focus
        // target's live position, so a target outside the constraint is lost and one the navigation reaches is kept.
        [Test]
        public void TargetLostWithoutCachingNegatesTheReachabilityOfTheFocusTarget()
        {
            WithScene(scene =>
            {
                IFSMTransition transition = Build(EnemyBrainTransition_TargetLost.ConstructInstance);
                scene.Brain.Storage.SetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, scene.Target(new Vector3(100f, 0f, 0f)));
                scene.Within(5f);
                Assert.That(transition.Update(scene.Brain, Context()), Is.True, "beyond the distance constraint");
                scene.Forward();
                Assert.That(transition.Update(scene.Brain, Context()), Is.False, "an unconstrained navigation reaches it");
            });
        }

        // 0x575235 (r12b = 1 after the HasValue test of the cached position): with caching on and no sighting cached the
        // target is lost, and the cache timer is neither read nor stored.
        [Test]
        public void TargetLostWithCachingIsTrueWithoutACachedPosition()
        {
            WithScene(scene =>
            {
                scene.Forward();
                scene.Brain.Storage.SetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, scene.Target(Vector3.zero));
                Assert.That(Build(EnemyBrainTransition_TargetLost.ConstructInstance).Update(scene.Brain, Context(0.5f)), Is.True);
                Assert.That(scene.Brain.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedTime), Is.False);
            }, traits => { traits.CacheTargetLocationOnSight = true; traits.TimeoutForReachingCachedLocation = 1f; });
        }

        // 0x5750F2 (ucomiss xmm0 = the cached time after adding the delta, [rax + 0x3c] = TimeoutForReachingCachedLocation;
        // ja 0x575235): strictly greater is lost; otherwise (equal, less or unordered) the result is the negated
        // reachability of the cached position, not of the focus target.
        [Test]
        public void TargetLostWithCachingTimesOutStrictlyAfterTheCachedLocationTimeout()
        {
            WithScene(scene =>
            {
                scene.Forward();
                scene.Brain.Storage.SetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, scene.Target(Vector3.zero));
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedPosition, new Vector3(1f, 0f, 0f));
                IFSMTransition transition = Build(EnemyBrainTransition_TargetLost.ConstructInstance);
                foreach ((float stored, bool expected, float sum) in new[]
                {
                    (0.5f, false, 1f), (0.25f, false, 0.75f), (0.75f, true, 1.25f), (float.NaN, false, float.NaN),
                })
                {
                    scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedTime, stored);
                    Assert.That(transition.Update(scene.Brain, Context(0.5f)), Is.EqualTo(expected), "stored " + stored);
                    Assert.That(Stored(scene, EnemyBrainFSMKeys.TargetCachedTime), Is.EqualTo(sum), "the sum after stored " + stored);
                }
            }, traits => { traits.CacheTargetLocationOnSight = true; traits.TimeoutForReachingCachedLocation = 1f; });
        }

        [Test]
        public void TargetLostWithCachingReadsTheReachabilityOfTheCachedPositionNotTheFocusTarget()
        {
            WithScene(scene =>
            {
                scene.Within(5f);
                scene.Brain.Storage.SetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, scene.Target(new Vector3(100f, 0f, 0f)));
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedTime, 0.25f);
                IFSMTransition transition = Build(EnemyBrainTransition_TargetLost.ConstructInstance);
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedPosition, new Vector3(3f, 0f, 4f));
                Assert.That(transition.Update(scene.Brain, Context(0.5f)), Is.False, "a reachable cached position, a far focus target");
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedTime, 0.25f);
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedPosition, new Vector3(100f, 0f, 0f));
                Assert.That(transition.Update(scene.Brain, Context(0.5f)), Is.True, "an unreachable cached position");
            }, traits => { traits.CacheTargetLocationOnSight = true; traits.TimeoutForReachingCachedLocation = 1f; });
        }

        private static void WithSpottedTarget(Action<Scene, EnemyTargetActorProbe> body, Action<EnemyTraits> configure) =>
            WithScene(scene =>
            {
                EnemyTargetActorProbe target = scene.Target(new Vector3(3f, 0f, 4f));
                body(scene, target);
            }, configure);

        // 0x575B6A stores the focus target before the traits test (0x575BD6-0x575C56 then push the cached position to
        // the target plus the overshoot along the direction from the enemy, 0x575E2A zeroes the cache timer and 0x575E3C
        // answers true): the enemy at the origin and the target at (3,0,4), five away, with an overshoot of five.
        [Test]
        public void TargetSpottedWithCachingStoresTheOvershotPositionAndRestartsTheCacheTimer()
        {
            WithSpottedTarget((scene, target) =>
            {
                scene.Forward();
                scene.Sense(target);
                Assert.That(scene.Brain.Target, Is.SameAs(target));
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedTime, 9f);
                Assert.That(Build(EnemyBrainTransition_TargetSpotted.ConstructInstance).Update(scene.Brain, Context()), Is.True);
                Assert.That(scene.Brain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, null, false), Is.SameAs(target));
                AssertNear(scene.Brain.Storage.GetValue(EnemyBrainFSMKeys.TargetCachedPosition, Vector3.zero, false), new Vector3(6f, 0f, 8f), "the cached position");
                Assert.That(Stored(scene, EnemyBrainFSMKeys.TargetCachedTime), Is.EqualTo(0f));
            }, traits => { traits.CacheTargetLocationOnSight = true; traits.OvershootAmount = 5f; });
        }

        // 0x575C5B (ebx = 0): a cached position the enemy cannot reach is not a sighting, but the focus target stored
        // before it stays, and neither the position nor the timer is stored. The target itself, five from home, is
        // reachable under the distance of five, so the brain senses it.
        [Test]
        public void TargetSpottedWithCachingRejectsAnUnreachableOvershotPositionButKeepsTheFocusTarget()
        {
            WithSpottedTarget((scene, target) =>
            {
                scene.Within(5f);
                scene.Sense(target);
                Assert.That(scene.Brain.Target, Is.SameAs(target));
                Assert.That(Build(EnemyBrainTransition_TargetSpotted.ConstructInstance).Update(scene.Brain, Context()), Is.False);
                Assert.That(scene.Brain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, null, false), Is.SameAs(target));
                Assert.That(scene.Brain.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedPosition), Is.False);
                Assert.That(scene.Brain.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedTime), Is.False);
            }, traits => { traits.CacheTargetLocationOnSight = true; traits.OvershootAmount = 5f; });
        }

        [Test]
        public void TargetSpottedWithoutCachingStoresOnlyTheFocusTarget()
        {
            WithSpottedTarget((scene, target) =>
            {
                scene.Forward();
                scene.Sense(target);
                Assert.That(Build(EnemyBrainTransition_TargetSpotted.ConstructInstance).Update(scene.Brain, Context()), Is.True);
                Assert.That(scene.Brain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, null, false), Is.SameAs(target));
                Assert.That(scene.Brain.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedPosition), Is.False);
                Assert.That(scene.Brain.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedTime), Is.False);
            }, traits => { traits.CacheTargetLocationOnSight = false; });
        }

        // 0x575490: the negated CanSeeTarget of the stored focus target (not of the brain's sensed target). With a sight
        // distance of ten, a focus target twenty away is out of range although the sensed target is one away, and a
        // focus target five away is in range.
        [Test]
        public void TargetOutOfSightRangeNegatesCanSeeTargetForTheFocusTarget()
        {
            WithScene(scene =>
            {
                scene.Forward();
                scene.Sense(scene.Target(new Vector3(1f, 0f, 0f)));
                Assert.That(scene.Brain.Target, Is.Not.Null);
                IFSMTransition transition = Build(EnemyBrainTransition_TargetOutOfSightRange.ConstructInstance);
                scene.Brain.Storage.SetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, scene.Target(new Vector3(20f, 0f, 0f)));
                Assert.That(transition.Update(scene.Brain, Context()), Is.True, "twenty away");
                scene.Brain.Storage.SetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, scene.Target(new Vector3(5f, 0f, 0f)));
                Assert.That(transition.Update(scene.Brain, Context()), Is.False, "five away");
            }, traits => traits.SightDistance = 10f);
        }

        // ---- the fire projectile transition ----

        private static IFSMTransition BuildFireProjectileTriggered(ActorAbilityType type) =>
            Build(EnemyBrainTransition_FireProjectileTriggered.ConstructInstance, "{\"AbilityType\":" + (int)type + "}");

        // Reflection: Actor.InitialiseAbilities fills m_abilities from the authored definition list, which an unstarted
        // Enemy does not have.
        private static Dictionary<ActorAbilityType, ActorAbility> AbilitiesOf(Enemy enemy) =>
            Get<Dictionary<ActorAbilityType, ActorAbility>>(enemy, "m_abilities");

        // 0x574971..0x574998 take the brain's Enemy (+0x18) and call Actor.TryGetAbility<EnemyAbility_FireProjectile>
        // (0x7C7460) with the type the constructor stored at +0x1C; 0x57499D..0x5749AC return false when it finds none
        // (xor eax, eax at 0x5749AE) and otherwise the byte at +0x20 of the ability, its IsTriggered flag. So the
        // transition is false without an ability of the configured type and follows IsTriggered once there is one. The
        // flag is raised by a real update on a sensed target and cleared by leaving the ability.
        [Test]
        public void FireProjectileTriggeredIsFalseWithoutTheAbilityAndFollowsItsTriggeredFlag()
        {
            EnemyAbilityTestSupport.WithScene(scene =>
            {
                IFSMTransition transition = BuildFireProjectileTriggered(ActorAbilityType.Enemy_FireProjectile);
                Assert.That(transition.Update(scene.Brain, Context()), Is.False, "no ability");
                var ability = scene.Ability<EnemyAbility_FireProjectile, EnemyAbilityDefinition_FireProjectile>();
                AbilitiesOf(scene.Enemy)[ActorAbilityType.Enemy_FireProjectile] = ability;
                Assert.That(transition.Update(scene.Brain, Context()), Is.False, "ability not triggered");
                scene.TriggerFireProjectile(ability, new Vector3(0f, 0f, 10f));
                Assert.That(transition.Update(scene.Brain, Context()), Is.True, "ability triggered");
                ability.Leave();
                Assert.That(transition.Update(scene.Brain, Context()), Is.False, "ability cleared again");
            });
        }

        // The type read at 0x574987 is the one the constructor kept from the JSON (0x5748E1..0x574919), so a triggered
        // ability stored under another type does not count and each node looks up its own configured type.
        [Test]
        public void FireProjectileTriggeredLooksUpTheAbilityTypeItWasConfiguredWith()
        {
            EnemyAbilityTestSupport.WithScene(scene =>
            {
                var triggered = scene.Ability<EnemyAbility_FireProjectile, EnemyAbilityDefinition_FireProjectile>();
                scene.TriggerFireProjectile(triggered, new Vector3(0f, 0f, 10f));
                AbilitiesOf(scene.Enemy)[ActorAbilityType.Enemy_FireProjectile] = triggered;
                AbilitiesOf(scene.Enemy)[ActorAbilityType.Enemy_Dead] = scene.Ability<EnemyAbility_FireProjectile, EnemyAbilityDefinition_FireProjectile>();
                Assert.That(BuildFireProjectileTriggered(ActorAbilityType.Enemy_FireProjectile).Update(scene.Brain, Context()), Is.True);
                Assert.That(BuildFireProjectileTriggered(ActorAbilityType.Enemy_Dead).Update(scene.Brain, Context()), Is.False,
                    "the ability under the other type is not triggered");
                Assert.That(BuildFireProjectileTriggered(ActorAbilityType.Enemy_GroundAttack).Update(scene.Brain, Context()), Is.False,
                    "nothing is registered under that type");
            });
        }

        // TryGetAbility<EnemyAbility_FireProjectile> (0x7C7460) is generic over the ability class, so a triggered
        // ability of another class under the configured type is not found and the transition is false.
        [Test]
        public void FireProjectileTriggeredIgnoresATriggeredAbilityOfAnotherClass()
        {
            EnemyAbilityTestSupport.WithScene(scene =>
            {
                var dead = scene.Ability<EnemyAbility_Dead, EnemyAbilityDefinition_Dead>();
                scene.Flag("IsDead", true);
                dead.Enter();
                dead.Update(0.02f);
                Assert.That(dead.IsTriggered, Is.True, "the dead ability is triggered while the enemy is dead");
                AbilitiesOf(scene.Enemy)[ActorAbilityType.Enemy_FireProjectile] = dead;
                Assert.That(BuildFireProjectileTriggered(ActorAbilityType.Enemy_FireProjectile).Update(scene.Brain, Context()), Is.False);
            });
        }

        // DoUpdate (0x574930..0x5749B8) only reads: it makes no storage call, so the target position that
        // EnemyAbility_FireProjectile.SetTriggered publishes on the brain stays until its consumer removes it (the
        // ability's UpdateWhileTriggered waits on it, jne to ret at 0x56F834; EnemyBrainState_TurnTowardsPosition removes it
        // once the enemy faces it, 0x573C34). With a really triggered ability the transition is true on every update
        // and the brain still holds the position, which this transition does not consume.
        [Test]
        public void FireProjectileTriggeredLeavesThePublishedTargetPositionOnTheBrain()
        {
            EnemyAbilityTestSupport.WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_FireProjectile, EnemyAbilityDefinition_FireProjectile>();
                AbilitiesOf(scene.Enemy)[ActorAbilityType.Enemy_FireProjectile] = ability;
                IFSMTransition transition = BuildFireProjectileTriggered(ActorAbilityType.Enemy_FireProjectile);
                Assert.That(transition.Update(scene.Brain, Context()), Is.False, "before the trigger");

                scene.TriggerFireProjectile(ability, new Vector3(0f, 0f, 10f));
                Assert.That(scene.Brain.Storage.HasValue<Vector3>(EnemyBrainFSMKeys.FireProjectileTargetPosition), Is.True);
                Assert.That(transition.Update(scene.Brain, Context()), Is.True);
                Assert.That(transition.Update(scene.Brain, Context()), Is.True, "still true on the next update");
                Assert.That(scene.Brain.Storage.HasValue<Vector3>(EnemyBrainFSMKeys.FireProjectileTargetPosition), Is.True,
                    "the transition does not consume the published position");

                scene.ConsumeFireTargetPosition();
                Assert.That(scene.Brain.Storage.HasValue<Vector3>(EnemyBrainFSMKeys.FireProjectileTargetPosition), Is.False, "the turn state consumed it");
                Assert.That(transition.Update(scene.Brain, Context()), Is.True, "the flag, not the stored position, decides");
            });
        }

        // 0x5748E1..0x5748EE reads the JSON into AbilityJSONCtorArgs (JsonUtility.FromJson<>) before 0x574900 allocates the
        // node, as in the siblings, and 0x574919 runs the Transition_Ability constructor, which registers the node.
        [Test]
        public void FireProjectileTriggeredReadsItsJsonBeforeBuildingAndRegistersTheTransition()
        {
            FiniteStateMachine fsm = NewFsm();
            FSMIdentifier id = Id();
            IFSMTransition transition = EnemyBrainTransition_FireProjectileTriggered.ConstructInstance(fsm, id, "{}");
            Assert.That(transition.TransitionId, Is.EqualTo(id.Id));
            Assert.That(fsm.TransitionExists(id, out IFSMTransition registered), Is.True);
            Assert.That(registered, Is.SameAs(transition));

            FiniteStateMachine rejected = NewFsm();
            FSMIdentifier rejectedId = Id();
            Assert.That(() => EnemyBrainTransition_FireProjectileTriggered.ConstructInstance(rejected, rejectedId, "not json"),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(rejected.TransitionExists(rejectedId, out _), Is.False);
        }

        // DummyDll: public, derived from Transition_Ability (not EnemyBrainTransition), the Enemy/Brain menu without a
        // default name, the IL2CPP options in the order ArrayBoundsChecks, NullChecks, no fields, and the protected
        // constructor, public static factory and protected virtual hook. It registers with the abstract base's argument class.
        [Test]
        public void DeclarationShapeFireProjectileTriggeredKeepsItsBaseMenuAndMembers()
        {
            Type type = typeof(EnemyBrainTransition_FireProjectileTriggered);
            AssertNode(type, 2, 1);
            AssertMembers(type, 0x00100001, new (string, int)[0], new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) });
            Assert.That(type.BaseType, Is.EqualTo(typeof(Transition_Ability)));
            Assert.That(FSMClassFactory.Transitions.TryGetValue(type.Name, out JSONFactoryClass registered), Is.True);
            Assert.That(registered.ClassType, Is.EqualTo(type));
            Assert.That(registered.ConstructInstanceMethod.DeclaringType, Is.EqualTo(type));
            Assert.That(registered.JSONCtorArgsType.Name, Is.EqualTo("AbilityJSONCtorArgs"));
            Assert.That(registered.JSONCtorArgsType.DeclaringType, Is.EqualTo(typeof(Transition_Ability)));
        }

        // ---- construction ----

        // The factories at 0x574460 and its eight siblings call JsonUtility.FromJson<BaseJSONCtorArgs> (0x5744AB) before
        // allocating the node, so a malformed argument string faults and builds nothing, while an empty object is
        // accepted and carries no data. The node registers itself with its state machine through the base constructor.
        [Test]
        public void ConstructInstanceReadsItsJsonBeforeBuildingAndRegistersTheTransition()
        {
            foreach ((Type type, Factory factory) in ConcreteTransitions)
            {
                FiniteStateMachine fsm = NewFsm();
                FSMIdentifier id = Id();
                IFSMTransition transition = factory(fsm, id, "{}");
                Assert.That(transition, Is.InstanceOf(type));
                Assert.That(transition.TransitionId, Is.EqualTo(id.Id), type.Name);
                Assert.That(fsm.TransitionExists(id, out IFSMTransition registered), Is.True, type.Name);
                Assert.That(registered, Is.SameAs(transition), type.Name);

                FiniteStateMachine rejected = NewFsm();
                FSMIdentifier rejectedId = Id();
                Assert.That(() => factory(rejected, rejectedId, "not json"), Throws.InstanceOf<ArgumentException>(), type.Name);
                Assert.That(rejected.TransitionExists(rejectedId, out _), Is.False, type.Name + " registers nothing after a fault");
            }
        }

        // ---- declarations ----

        private static void AssertNode(Type type, params int[] il2CppOptions)
        {
            Assert.That(type.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Enemy/Brain/{0}"), type.Name);
            Assert.That(type.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false), Is.Null, type.Name);
            AssertIl2CppOptions(type, il2CppOptions);
        }

        // The brain transitions share one Enemy/Brain menu and have no default graph name; the order of the two IL2CPP
        // option attributes is the metadata order of each type.
        [Test]
        public void NodesCarryTheirOriginalGraphAuthoringAttributes()
        {
            AssertNode(typeof(EnemyBrainTransition), 2, 1);
            AssertNode(typeof(EnemyBrainTransition_AttackCycleComplete), 2, 1);
            AssertNode(typeof(EnemyBrainTransition_DestinationReached), 1, 2);
            AssertNode(typeof(EnemyBrainTransition_IdleToMove), 2, 1);
            AssertNode(typeof(EnemyBrainTransition_TargetLost), 2, 1);
            AssertNode(typeof(EnemyBrainTransition_TargetOutOfSightRange), 1, 2);
            AssertNode(typeof(EnemyBrainTransition_TargetReached), 1, 2);
            AssertNode(typeof(EnemyBrainTransition_TargetSpotted), 1, 2);
            AssertNode(typeof(EnemyBrainTransition_WatchToChaseTimeout), 2, 1);
            AssertNode(typeof(EnemyBrainTransition_WindUpCycleComplete), 2, 1);
        }

        // The factory finds each concrete node by its static ConstructInstance and takes the argument class nested in the
        // abstract base (it carries the FSM argument attribute); the abstract base itself is never registered.
        [Test]
        public void ClassFactoryRegistersTheNodesWithTheirArgumentTypes()
        {
            foreach ((Type node, Factory factory) in ConcreteTransitions)
            {
                Assert.That(FSMClassFactory.Transitions.TryGetValue(node.Name, out JSONFactoryClass registered), Is.True, node.Name);
                Assert.That(registered.ClassType, Is.EqualTo(node), node.Name);
                Assert.That(registered.ConstructInstanceMethod.DeclaringType, Is.EqualTo(node), node.Name);
                Assert.That(registered.JSONCtorArgsType.Name, Is.EqualTo("BaseJSONCtorArgs"), node.Name);
                Assert.That(registered.JSONCtorArgsType.DeclaringType, Is.EqualTo(typeof(EnemyBrainTransition)), node.Name);
            }
            Assert.That(FSMClassFactory.Transitions.ContainsKey(nameof(EnemyBrainTransition)), Is.False);
        }

        // Type, field and method flags straight from the metadata of the original assembly: the abstract base with its
        // protected constructor and its serialisable protected argument class, and the transitions with a protected
        // constructor, a public static factory and protected virtual overrides.
        [Test]
        public void DeclarationShapeTransitionsKeepTheirFlagsBasesAndArgumentClass()
        {
            const int constructor = 0x1884, factory = 0x0096, hook = 0x00c4;
            AssertMembers(typeof(EnemyBrainTransition), 0x00100081, new (string, int)[0], new[] { (".ctor", constructor) });
            Type arguments = typeof(EnemyBrainTransition).GetNestedType("BaseJSONCtorArgs", BindingFlags.NonPublic);
            AssertMembers(arguments, 0x00102004, new (string, int)[0], new[] { (".ctor", 0x1886) });
            Assert.That(arguments.GetCustomAttribute<FiniteStateMachineJsonCtorArgsAttribute>(false), Is.Not.Null);
            Assert.That(typeof(EnemyBrainTransition).BaseType, Is.EqualTo(typeof(FSMTransition)));

            var updateOnly = new[] { (".ctor", constructor), ("ConstructInstance", factory), ("DoUpdate", hook) };
            var enterAndUpdate = new[] { (".ctor", constructor), ("ConstructInstance", factory), ("DoOnEnter", hook), ("DoUpdate", hook) };
            foreach (Type type in new[]
            {
                typeof(EnemyBrainTransition_AttackCycleComplete), typeof(EnemyBrainTransition_DestinationReached),
                typeof(EnemyBrainTransition_IdleToMove), typeof(EnemyBrainTransition_TargetReached), typeof(EnemyBrainTransition_WindUpCycleComplete),
            })
            {
                AssertMembers(type, 0x00100001, new (string, int)[0], updateOnly);
                Assert.That(type.BaseType, Is.EqualTo(typeof(EnemyBrainTransition)), type.Name);
            }
            foreach (Type type in new[] { typeof(EnemyBrainTransition_TargetLost), typeof(EnemyBrainTransition_TargetOutOfSightRange), typeof(EnemyBrainTransition_TargetSpotted) })
            {
                AssertMembers(type, 0x00100001, new (string, int)[0], enterAndUpdate);
                Assert.That(type.BaseType, Is.EqualTo(typeof(EnemyBrainTransition)), type.Name);
            }
            AssertMembers(typeof(EnemyBrainTransition_WatchToChaseTimeout), 0x00100001, new (string, int)[0], new[]
            {
                (".ctor", constructor), ("ConstructInstance", factory), ("SetElapsedSeconds", 0x0081), ("GetElapsedSeconds", 0x0086),
                ("DoOnEnter", hook), ("DoOnLeave", hook), ("DoUpdate", hook),
            });
            Assert.That(typeof(EnemyBrainTransition_WatchToChaseTimeout).BaseType, Is.EqualTo(typeof(EnemyBrainTransition)));
            ParameterInfo adjust = typeof(EnemyBrainTransition_WatchToChaseTimeout).GetMethod("GetElapsedSeconds").GetParameters()[1];
            Assert.That((adjust.IsOptional, adjust.DefaultValue), Is.EqualTo((true, (object)0f)));
        }
    }
}
