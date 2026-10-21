using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The enemy brain transitions: the shared base and the nine transitions that do not need the ability of another
    // cluster. Expected values come from the 1.10.1 x86-64 bodies cited per test. Each transition is built through its
    // own ConstructInstance and updated against a real EnemyBrain with its own storage. An Enemy that has not started
    // holds no definition (Start reads it from the DataManager), so the bodies that read traits are checked here only
    // as far as they run without one; the traits-gated predicates are pinned by their compare instructions.
    public sealed class EnemyBrainTransitionTests
    {
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

        // 0x575EF0 ignores its argument: xorps xmm0 at 0x57603E precedes the tail-called SetValue<float>, and entry never
        // reads xmm0.
        [Test]
        public void SettingTheWatchTimerStoresZeroWhateverTheArgument()
        {
            WithBrain((owned, brain, enemy) =>
            {
                var transition = (EnemyBrainTransition_WatchToChaseTimeout)Build(EnemyBrainTransition_WatchToChaseTimeout.ConstructInstance);
                Call(transition, "SetElapsedSeconds", brain, 7.5f);
                Assert.That(brain.Storage.GetValue(EnemyBrainFSMKeys.WatchElapsedTime, -1f, false), Is.EqualTo(0f));
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

        // 0x574B85 (ucomiss xmm0, [rax + 0x30]; setae) and 0x576345 (ucomiss xmm0, [rax + 0x40]; setae): the stored time
        // against the idle seconds and against the watch timeout, true when the time is greater than or equal and
        // ordered, so a NaN time is not enough. The traits sit behind the enemy definition that Start reads, so the
        // operator is checked from the instructions. This is an emission self-check of the restored source, not a
        // native-derived oracle; the equal, greater and NaN truth table for both timers is owed until a definition can
        // be supplied without reflection. A floating >= compiles to clt.un followed by a negation, where
        // > would be cgt and <= would be cgt.un and a negation.
        [Test]
        public void EmissionCheck_IdleAndWatchTimerComparesCompileToGreaterOrEqual()
        {
            Assert.That(CompareInstructions(typeof(EnemyBrainTransition_IdleToMove)), Is.EqualTo(new[] { "clt.un", "ceq" }));
            Assert.That(CompareInstructions(typeof(EnemyBrainTransition_WatchToChaseTimeout)), Is.EqualTo(new[] { "clt.un", "ceq" }));
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
