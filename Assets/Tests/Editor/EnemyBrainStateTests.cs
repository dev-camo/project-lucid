using System;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The enemy brain states (Attack 02000643, CueTargetSpottedReaction 0200063d, Idle 0200063e, Move 0200063f,
    // MoveToDestination 02000640, MoveToTarget 02000641, ResetHome 02000642, TurnTowardsPosition 02000644 with its
    // JSONCtorArgs 02000645, WindUp_Attack 02000646) against the 1.10.1 x86-64 bodies at 0x571860..0x573D60.
    // A brain is the real EnemyBrain user with a real FSM storage; the enemy is a real component on an owned inactive
    // host (Edit Mode sends no Awake) with an authored definition, a navigation probe and a position and rotation set
    // through the Actor setters. States are built through the FSM class factory, as graph loading builds them.
    // Not covered: the same states driven by a started Enemy (Enemy.Initialise needs the data manager's definitions and
    // an FSM asset), and the Debug.DrawLine call at the end of SetMovementImpulseForDestination (0x572413), which has
    // no observable result.
    public sealed class EnemyBrainStateTests
    {
        private static readonly string[] ConcreteStates =
        {
            nameof(EnemyBrainState_Attack), nameof(EnemyBrainState_CueTargetSpottedReaction), nameof(EnemyBrainState_Idle),
            nameof(EnemyBrainState_MoveToDestination), nameof(EnemyBrainState_MoveToTarget), nameof(EnemyBrainState_ResetHome),
            nameof(EnemyBrainState_TurnTowardsPosition), nameof(EnemyBrainState_WindUp_Attack),
        };

        private static string Name() => "Lucid-enemies-01-" + Guid.NewGuid().ToString("N");

        private static IFSMState NewState(string node, string json = "{}") =>
            FSMClassFactory.ConstructState(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private sealed class Scene
        {
            public Owned Owned;
            public EnemyBrain Brain;
            public Enemy Enemy;
            public FSMStorage Storage;
            public Enemies01NavigationProbe Navigation;

            public bool Has(GraphStorageKey key) => Storage.GetCollection().ContainsKey(key);

            public T Peek<T>(GraphStorageKey key) => Storage.GetValue(key, default(T), false);

            // Another real Actor, used as the brain's focus target.
            public Enemy NewTarget(Vector3 position)
            {
                var target = Owned.Host("Lucid enemy target", false).AddComponent<Enemy>();
                SetActorProperty(target, "WorldPosition", position);
                return target;
            }

            public void Enter(IFSMState state) => state.OnEnter(Brain, null);
            public void Update(IFSMState state, float deltaTime = 0.25f) => state.Update(Brain, new FSMUpdateContext(deltaTime));
            public void Leave(IFSMState state) => state.OnLeave(Brain, null);
        }

        // The enemy constructor reads the character, enemy and gameplay island rows, so those are leased and released
        // idle. The traits are authored here: turn on the spot beyond 3 degrees, move forward below 45 degrees, and
        // count the destination as reached within 2 units.
        private static void WithScene(Action<Scene> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<CharacterManager>();
            owned.LeaseRow<EnemyManager>();
            owned.LeaseRow<GameplayIslandManager>();
            var scene = new Scene { Owned = owned };
            var definition = owned.Track(ScriptableObject.CreateInstance<Enemies01EnemyDefinitionProbe>());
            definition.Traits = new EnemyTraits { FaceAngleTolerance = 3f, MaxAngleForwardMovement = 45f, MoveWithinDistanceTolerance = 2f };
            definition.Traits.UpdateCachedValues();
            GameObject host = owned.Host("Lucid enemy", false);
            scene.Navigation = host.AddComponent<Enemies01NavigationProbe>();
            scene.Enemy = host.AddComponent<Enemy>();
            Set(scene.Enemy, "m_definition", definition);
            Set(scene.Enemy, "m_navigation", scene.Navigation);
            SetActorProperty(scene.Enemy, "WorldPosition", Vector3.zero);
            SetActorProperty(scene.Enemy, "UpDirection", Vector3.up);
            SetActorProperty(scene.Enemy, "ForwardDirection", Vector3.forward);
            scene.Storage = new FSMStorage(0);
            scene.Brain = new EnemyBrain();
            SetProperty(scene.Brain, "Storage", scene.Storage);
            SetProperty(scene.Brain, "Enemy", scene.Enemy);
            SetProperty(scene.Enemy, "Brain", scene.Brain);
            body(scene);
        });

        // ---- Construction and declarations ----

        // Each ConstructInstance (0x571870, 0x571A80, 0x572440, 0x572960, 0x5732D0, 0x573A20, 0x573D60, 0x5733F0) builds
        // the state and the base constructor registers it in the machine it was given. Move is abstract and has no
        // ConstructInstance, so the factory never lists it.
        [Test]
        public void ConcreteStatesAreBuiltFromTheirNodeNamesAndRegisteredInTheirMachine()
        {
            foreach (string node in ConcreteStates)
            {
                var fsm = new FiniteStateMachine(Name(), skipAddToManager: true);
                var id = new FSMIdentifier(Name());
                string json = node == nameof(EnemyBrainState_TurnTowardsPosition) ? "{\"PositionFSMVariableName\":\"Spot\"}" : "{}";
                IFSMState state = FSMClassFactory.ConstructState(fsm, node, id, json);
                Assert.That(state.GetType().Name, Is.EqualTo(node));
                Assert.That(fsm.States[id.Id], Is.SameAs(state), node);
                Assert.That(FSMClassFactory.States.ContainsKey(node), Is.True, node);
            }
            Assert.That(FSMClassFactory.States.ContainsKey(nameof(EnemyBrainState_Move)), Is.False);
            Assert.That(typeof(EnemyBrainState_Move).IsAbstract, Is.True);
        }

        // Metadata: every state is in the "Enemy/Brain" menu and none has a default node name.
        [Test]
        public void StatesAreListedUnderTheEnemyBrainMenu()
        {
            foreach (Type type in ConcreteStates.Select(name => typeof(EnemyBrainState_Attack).Assembly.GetType("HardlightProject." + name)).Append(typeof(EnemyBrainState_Move)))
            {
                Assert.That(type.GetCustomAttributes(typeof(GraphNodeMenuFormatAttribute), false).Cast<GraphNodeMenuFormatAttribute>().Single().Format, Is.EqualTo("Enemy/Brain/{0}"), type.Name);
                Assert.That(type.GetCustomAttributes(typeof(GraphNodeDefaultNameAttribute), false), Is.Empty, type.Name);
            }
        }

        // Flags, member order and Il2CppSetOption order from the DummyDll tables (1 = NullChecks, 2 = ArrayBoundsChecks).
        [Test]
        public void StatesKeepTheirFlagsFieldsAndMethods()
        {
            var noFields = new (string, int)[0];
            var ctorAndFactory = new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096) };
            var enterUpdateLeave = new[] { ("DoOnEnter", 0x00c4), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4) };

            AssertMembers(typeof(EnemyBrainState_Attack), 0x00100001, noFields, ctorAndFactory.Concat(enterUpdateLeave).ToArray());
            AssertIl2CppOptions(typeof(EnemyBrainState_Attack), 2, 1);
            AssertMembers(typeof(EnemyBrainState_CueTargetSpottedReaction), 0x00100001, noFields, ctorAndFactory.Concat(new[] { ("DoOnEnter", 0x00c4) }).ToArray());
            AssertIl2CppOptions(typeof(EnemyBrainState_CueTargetSpottedReaction), 2, 1);
            AssertMembers(typeof(EnemyBrainState_Idle), 0x00100001, noFields, ctorAndFactory.Concat(enterUpdateLeave).ToArray());
            AssertIl2CppOptions(typeof(EnemyBrainState_Idle), 1, 2);
            AssertMembers(typeof(EnemyBrainState_Move), 0x00100081, noFields, new[] { (".ctor", 0x1884), ("SetMovementImpulseForDestination", 0x0096) });
            AssertIl2CppOptions(typeof(EnemyBrainState_Move), 2, 1);
            AssertMembers(typeof(EnemyBrainState_MoveToDestination), 0x00100001, noFields, ctorAndFactory.Concat(enterUpdateLeave).ToArray());
            AssertIl2CppOptions(typeof(EnemyBrainState_MoveToDestination), 2, 1);
            AssertMembers(typeof(EnemyBrainState_MoveToTarget), 0x00100001, noFields, ctorAndFactory.Concat(enterUpdateLeave).ToArray());
            AssertIl2CppOptions(typeof(EnemyBrainState_MoveToTarget), 2, 1);
            AssertMembers(typeof(EnemyBrainState_ResetHome), 0x00100001, noFields, ctorAndFactory.Concat(new[] { ("DoOnEnter", 0x00c4) }).ToArray());
            AssertIl2CppOptions(typeof(EnemyBrainState_ResetHome), 1, 2);
            AssertMembers(typeof(EnemyBrainState_WindUp_Attack), 0x00100001, noFields, ctorAndFactory.Concat(enterUpdateLeave).ToArray());
            AssertIl2CppOptions(typeof(EnemyBrainState_WindUp_Attack), 1, 2);

            AssertMembers(typeof(EnemyBrainState_TurnTowardsPosition), 0x00100001,
                new[] { ("m_positionFSMVariable", 0x0021), ("m_removeWhenComplete", 0x0021) },
                ctorAndFactory.Concat(new[] { ("DoOnEnter", 0x00c4), ("DoUpdate", 0x00c4) }).ToArray());
            AssertIl2CppOptions(typeof(EnemyBrainState_TurnTowardsPosition), 2, 1);
            Type args = typeof(EnemyBrainState_TurnTowardsPosition).GetNestedType("JSONCtorArgs", Declared);
            AssertMembers(args, 0x00102004, new[] { ("PositionFSMVariableName", 0x0006), ("RemoveWhenComplete", 0x0006) }, new[] { (".ctor", 0x1886) });
            AssertIl2CppOptions(args, 1, 2);
            Assert.That(args.BaseType, Is.EqualTo(typeof(ActorState).GetNestedType("BaseJSONCtorArgs", Declared)));
            Assert.That(typeof(EnemyBrainState_Move).GetMethod("SetMovementImpulseForDestination").GetParameters().Last().DefaultValue, Is.EqualTo(false));
        }

        // 0x5739D0: the storage key is built from the argument name with node and graph ids 0, and the flag is copied.
        [Test]
        public void TurnTowardsPositionTakesItsStorageKeyAndFlagFromTheJsonArguments()
        {
            IFSMState state = NewState(nameof(EnemyBrainState_TurnTowardsPosition), "{\"PositionFSMVariableName\":\"Spot\",\"RemoveWhenComplete\":true}");
            Assert.That(Get<GraphStorageKey>(state, "m_positionFSMVariable"), Is.EqualTo(new GraphStorageKey("Spot", 0, 0)));
            Assert.That(Get<bool>(state, "m_removeWhenComplete"), Is.True);
            state = NewState(nameof(EnemyBrainState_TurnTowardsPosition), "{\"PositionFSMVariableName\":\"Other\"}");
            Assert.That(Get<GraphStorageKey>(state, "m_positionFSMVariable"), Is.EqualTo(new GraphStorageKey("Other", 0, 0)));
            Assert.That(Get<bool>(state, "m_removeWhenComplete"), Is.False);
        }

        // ---- Attack and wind-up ----

        // 0x573480: the focus target is read (and a missing one stored as null); only when it exists is the attack
        // target flag raised. The base hook runs first and changes nothing.
        [Test]
        public void AttackEnterRaisesTheAttackFlagOnlyWithAFocusTarget()
        {
            WithScene(scene =>
            {
                IFSMState state = NewState(nameof(EnemyBrainState_Attack));
                scene.Enter(state);
                Assert.That(scene.Has(EnemyBrainFSMKeys.AttackTarget), Is.False);
                scene.Storage.SetValue(EnemyBrainFSMKeys.FocusTarget, scene.NewTarget(Vector3.zero));
                scene.Enter(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.AttackTarget), Is.True);
            });
        }

        // 0x573820: the attack flag is cleared and the enemy's wind-up-complete flag (stored on its brain) is cleared.
        [Test]
        public void AttackLeaveClearsTheAttackFlagAndTheWindUpCompleteFlag()
        {
            WithScene(scene =>
            {
                IFSMState state = NewState(nameof(EnemyBrainState_Attack));
                scene.Storage.SetValue(EnemyBrainFSMKeys.AttackTarget, true);
                scene.Storage.SetValue(EnemyBrainFSMKeys.AttackWindUpComplete, true);
                scene.Leave(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.AttackTarget), Is.False);
                Assert.That(scene.Has(EnemyBrainFSMKeys.AttackTarget), Is.True);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.AttackWindUpComplete), Is.False);
                Assert.That(scene.Has(EnemyBrainFSMKeys.AttackWindUpComplete), Is.True);
            });
        }

        // 0x573780 and 0x574200 set the movement impulse to Vector2.zero on every update.
        [Test]
        public void AttackAndWindUpUpdatesStopTheMovement()
        {
            WithScene(scene =>
            {
                foreach (string node in new[] { nameof(EnemyBrainState_Attack), nameof(EnemyBrainState_WindUp_Attack) })
                {
                    scene.Storage.SetValue(EnemyBrainFSMKeys.MovementImpulse, new Vector2(1f, -1f));
                    scene.Update(NewState(node));
                    Assert.That(scene.Brain.GetMovement(), Is.EqualTo(Vector2.zero), node);
                }
            });
        }

        // 0x573DF0: a missing focus target returns before either flag is written; with one, the wind-up starts and the
        // completed flag is cleared (seeded true to show the write).
        [Test]
        public void WindUpEnterStartsTheWindUpOnlyWithAFocusTarget()
        {
            WithScene(scene =>
            {
                IFSMState state = NewState(nameof(EnemyBrainState_WindUp_Attack));
                scene.Storage.SetValue(EnemyBrainFSMKeys.AttackWindUpComplete, true);
                scene.Enter(state);
                Assert.That(scene.Has(EnemyBrainFSMKeys.StartAttackWindUp), Is.False);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.AttackWindUpComplete), Is.True);
                scene.Storage.SetValue(EnemyBrainFSMKeys.FocusTarget, scene.NewTarget(Vector3.zero));
                scene.Enter(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.StartAttackWindUp), Is.True);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.AttackWindUpComplete), Is.False);
            });
        }

        // 0x5742A0 clears only the start flag.
        [Test]
        public void WindUpLeaveClearsOnlyTheStartFlag()
        {
            WithScene(scene =>
            {
                scene.Storage.SetValue(EnemyBrainFSMKeys.StartAttackWindUp, true);
                scene.Storage.SetValue(EnemyBrainFSMKeys.AttackWindUpComplete, true);
                scene.Leave(NewState(nameof(EnemyBrainState_WindUp_Attack)));
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.StartAttackWindUp), Is.False);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.AttackWindUpComplete), Is.True);
            });
        }

        // 0x571900 raises the animation trigger and has no base call, target check or other write.
        [Test]
        public void CueTargetSpottedReactionRaisesTheAnimationTrigger()
        {
            WithScene(scene =>
            {
                scene.Enter(NewState(nameof(EnemyBrainState_CueTargetSpottedReaction)));
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger), Is.True);
                Assert.That(scene.Storage.GetCollection().Count, Is.EqualTo(1));
            });
        }

        // ---- Idle ----

        // 0x571B10: the target lock is cleared, the impulse is zeroed and the idle timer restarts at zero (all seeded
        // with other values to show the writes).
        [Test]
        public void IdleEnterUnlocksTheTargetStopsTheMovementAndRestartsTheTimer()
        {
            WithScene(scene =>
            {
                scene.Storage.SetValue(EnemyBrainFSMKeys.TargetLocked, true);
                scene.Storage.SetValue(EnemyBrainFSMKeys.MovementImpulse, new Vector2(0.5f, 1f));
                scene.Storage.SetValue(EnemyBrainFSMKeys.IdleElapsedTime, 7f);
                scene.Enter(NewState(nameof(EnemyBrainState_Idle)));
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.TargetLocked), Is.False);
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(Vector2.zero));
                Assert.That(scene.Peek<float>(EnemyBrainFSMKeys.IdleElapsedTime), Is.EqualTo(0f));
            });
        }

        // 0x571F00 adds the update context's delta to the idle timer (AdjustValue with a zero default), 0x572010 removes it.
        [Test]
        public void IdleUpdateAddsTheFrameTimeAndLeaveRemovesTheTimer()
        {
            WithScene(scene =>
            {
                IFSMState state = NewState(nameof(EnemyBrainState_Idle));
                scene.Update(state, 0.25f);
                Assert.That(scene.Peek<float>(EnemyBrainFSMKeys.IdleElapsedTime), Is.EqualTo(0.25f));
                scene.Update(state, 0.5f);
                Assert.That(scene.Peek<float>(EnemyBrainFSMKeys.IdleElapsedTime), Is.EqualTo(0.75f));
                scene.Leave(state);
                Assert.That(scene.Has(EnemyBrainFSMKeys.IdleElapsedTime), Is.False);
            });
        }

        // ---- Navigation states ----

        // 0x5724D0 starts the navigation only when it is initialised (the flag byte at +0x38).
        [Test]
        public void MoveToDestinationEnterStartsOnlyAnInitialisedNavigation()
        {
            WithScene(scene =>
            {
                IFSMState state = NewState(nameof(EnemyBrainState_MoveToDestination));
                scene.Enter(state);
                Assert.That(scene.Navigation.Calls, Is.Empty);
                scene.Navigation.SetInitialised(true);
                scene.Enter(state);
                Assert.That(scene.Navigation.Calls, Is.EqualTo(new[] { "StartNavigation" }));
            });
        }

        // 0x572570: with navigation the destination is steered towards and the reached flag is the steering result;
        // without it the destination counts as reached and the impulse is left alone.
        [Test]
        public void MoveToDestinationUpdateStoresWhetherTheDestinationWasReached()
        {
            WithScene(scene =>
            {
                IFSMState state = NewState(nameof(EnemyBrainState_MoveToDestination));
                scene.Storage.SetValue(EnemyBrainFSMKeys.MovementImpulse, new Vector2(0.5f, 0.5f));
                scene.Update(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.MoveToDestinationReached), Is.True);
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(new Vector2(0.5f, 0.5f)));

                scene.Navigation.SetInitialised(true);
                scene.Navigation.Destination = new Vector3(0f, 0f, 10f);
                scene.Update(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.MoveToDestinationReached), Is.False);
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(new Vector2(0f, 1f)));

                scene.Navigation.Destination = new Vector3(0f, 0f, 1f);
                scene.Update(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.MoveToDestinationReached), Is.True);
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(Vector2.zero));
            });
        }

        // 0x5727C0 removes the reached flag.
        [Test]
        public void MoveToDestinationLeaveRemovesTheReachedFlag()
        {
            WithScene(scene =>
            {
                scene.Storage.SetValue(EnemyBrainFSMKeys.MoveToDestinationReached, true);
                scene.Leave(NewState(nameof(EnemyBrainState_MoveToDestination)));
                Assert.That(scene.Has(EnemyBrainFSMKeys.MoveToDestinationReached), Is.False);
            });
        }

        // 0x573360 interrupts the navigation, initialised or not.
        [Test]
        public void ResetHomeEnterInterruptsTheNavigation()
        {
            WithScene(scene =>
            {
                scene.Enter(NewState(nameof(EnemyBrainState_ResetHome)));
                Assert.That(scene.Navigation.Calls, Is.EqualTo(new[] { "InterruptNavigation" }));
            });
        }

        // ---- Move to target ----

        // 0x5729F0 locks the target on entry with or without a focus target (the null comparison has no branch body).
        [Test]
        public void MoveToTargetEnterLocksTheTargetWithOrWithoutAFocusTarget()
        {
            WithScene(scene =>
            {
                IFSMState state = NewState(nameof(EnemyBrainState_MoveToTarget));
                scene.Enter(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.TargetLocked), Is.True);
                scene.Storage.SetValue(EnemyBrainFSMKeys.TargetLocked, false);
                scene.Storage.SetValue(EnemyBrainFSMKeys.FocusTarget, scene.NewTarget(Vector3.zero));
                scene.Enter(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.TargetLocked), Is.True);
            });
        }

        // 0x572C10: the focus target's live position is steered towards; a stored cached position replaces it.
        [Test]
        public void MoveToTargetUpdateSteersTowardsTheTargetUnlessAPositionIsCached()
        {
            WithScene(scene =>
            {
                IFSMState state = NewState(nameof(EnemyBrainState_MoveToTarget));
                scene.Storage.SetValue(EnemyBrainFSMKeys.FocusTarget, scene.NewTarget(new Vector3(0f, 0f, 10f)));
                scene.Update(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.TargetReached), Is.False);
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(new Vector2(0f, 1f)));

                scene.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedPosition, new Vector3(10f, 0f, 0f));
                scene.Update(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.TargetReached), Is.False);
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(new Vector2(1f, 0f)));

                scene.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedPosition, new Vector3(0f, 0f, 1f));
                scene.Update(state);
                Assert.That(scene.Peek<bool>(EnemyBrainFSMKeys.TargetReached), Is.True);
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(Vector2.zero));
            });
        }

        // 0x573020 removes the reached flag and the cached position.
        [Test]
        public void MoveToTargetLeaveRemovesTheReachedFlagAndTheCachedPosition()
        {
            WithScene(scene =>
            {
                scene.Storage.SetValue(EnemyBrainFSMKeys.TargetReached, true);
                scene.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedPosition, Vector3.one);
                scene.Leave(NewState(nameof(EnemyBrainState_MoveToTarget)));
                Assert.That(scene.Has(EnemyBrainFSMKeys.TargetReached), Is.False);
                Assert.That(scene.Has(EnemyBrainFSMKeys.TargetCachedPosition), Is.False);
            });
        }

        // ---- Turn towards position ----

        private static IFSMState NewTurnState(bool removeWhenComplete) => NewState(nameof(EnemyBrainState_TurnTowardsPosition),
            "{\"PositionFSMVariableName\":\"Spot\",\"RemoveWhenComplete\":" + (removeWhenComplete ? "true" : "false") + "}");

        private static readonly GraphStorageKey SpotKey = new GraphStorageKey("Spot", 0, 0);

        // 0x573B00: with no stored position nothing happens. A position straight ahead is faced (0x573C27 passes
        // ignoreForwardMovement = true, so no forward impulse), and the stored position is then removed only when
        // RemoveWhenComplete is set. A position to the side is turned towards, not faced, and is kept.
        [Test]
        public void TurnTowardsPositionTurnsInPlaceAndRemovesTheSpotOnceFaced()
        {
            WithScene(scene =>
            {
                IFSMState removing = NewTurnState(true);
                scene.Update(removing);
                Assert.That(scene.Storage.GetCollection().Count, Is.EqualTo(0));

                scene.Storage.SetValue(SpotKey, new Vector3(10f, 0f, 0f));
                scene.Update(removing);
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(scene.Has(SpotKey), Is.True, "not yet facing");

                scene.Storage.SetValue(SpotKey, new Vector3(0f, 0f, 10f));
                scene.Update(NewTurnState(false));
                Assert.That(scene.Peek<Vector2>(EnemyBrainFSMKeys.MovementImpulse), Is.EqualTo(Vector2.zero), "no forward movement while turning");
                Assert.That(scene.Has(SpotKey), Is.True, "RemoveWhenComplete is off");
                scene.Update(removing);
                Assert.That(scene.Has(SpotKey), Is.False);
            });
        }

        // ---- SetMovementImpulseForDestination ----

        private static bool Steer(Scene scene, Vector3 destination, bool ignoreForwardMovement = false) =>
            EnemyBrainState_Move.SetMovementImpulseForDestination(scene.Brain, destination, ignoreForwardMovement);

        // Native 0x5721A0 truth table for an enemy at the origin facing +Z with +Y up, tolerance 2 (squared 4), face
        // angle 3 and forward limit 45. Each row: destination, the impulse and result without and then with
        // ignoreForwardMovement. The result is "distance within tolerance" normally (setae on 0x5723F2) and "facing"
        // when forward movement is ignored (setbe on 0x5723DF), chosen by the final cmovne (0x57241A); the rows tell
        // the two apart (near-side is within tolerance but not facing; far-ahead is facing but not within tolerance).
        [Test]
        public void SteeringChoosesTurnForwardAndTheResultFromDistanceAndAngle()
        {
            (Vector3 destination, Vector2 impulse, bool reached, Vector2 ignoredImpulse, bool facing)[] rows =
            {
                (new Vector3(0f, 0f, 10f), new Vector2(0f, 1f), false, new Vector2(0f, 0f), true),
                (new Vector3(0f, 0f, 1f), new Vector2(0f, 0f), true, new Vector2(0f, 0f), true),
                (new Vector3(10f, 0f, 0f), new Vector2(1f, 0f), false, new Vector2(1f, 0f), false),
                (new Vector3(-10f, 0f, 0f), new Vector2(-1f, 0f), false, new Vector2(-1f, 0f), false),
                (new Vector3(2f, 0f, 0f), new Vector2(1f, 0f), true, new Vector2(1f, 0f), false),
            };
            WithScene(scene =>
            {
                foreach (var row in rows)
                {
                    Assert.That(Steer(scene, row.destination), Is.EqualTo(row.reached), row.destination.ToString());
                    Assert.That(scene.Brain.GetMovement(), Is.EqualTo(row.impulse), row.destination.ToString());
                    Assert.That(Steer(scene, row.destination, true), Is.EqualTo(row.facing), row.destination + " ignoring forward movement");
                    Assert.That(scene.Brain.GetMovement(), Is.EqualTo(row.ignoredImpulse), row.destination + " ignoring forward movement");
                }
            });
        }

        // The distance test is inclusive (cmpleps against the squared tolerance, 0x572357..0x57235A): exactly 2 units
        // is within tolerance and moves no further, just beyond moves forward.
        [Test]
        public void SteeringTreatsTheToleranceDistanceItselfAsReached()
        {
            WithScene(scene =>
            {
                Assert.That(Steer(scene, new Vector3(0f, 0f, 2f)), Is.True);
                Assert.That(scene.Brain.GetMovement(), Is.EqualTo(Vector2.zero));
                Assert.That(Steer(scene, new Vector3(0f, 0f, 2.01f)), Is.False);
                Assert.That(scene.Brain.GetMovement(), Is.EqualTo(new Vector2(0f, 1f)));
            });
        }

        // Turning needs more than the face angle (3 degrees) either way; forward movement needs less than the forward
        // limit (45 degrees): 2 degrees only moves, 4 degrees turns and moves, 44 turns and moves, 46 only turns.
        [Test]
        public void SteeringTurnsBeyondTheFaceAngleAndMovesBelowTheForwardLimit()
        {
            WithScene(scene =>
            {
                foreach ((float degrees, Vector2 impulse) in new[]
                {
                    (2f, new Vector2(0f, 1f)), (-2f, new Vector2(0f, 1f)), (4f, new Vector2(1f, 1f)), (-4f, new Vector2(-1f, 1f)),
                    (44f, new Vector2(1f, 1f)), (-44f, new Vector2(-1f, 1f)), (46f, new Vector2(1f, 0f)), (-46f, new Vector2(-1f, 0f)),
                })
                {
                    double radians = degrees * Math.PI / 180.0;
                    Steer(scene, new Vector3((float)(10.0 * Math.Sin(radians)), 0f, (float)(10.0 * Math.Cos(radians))));
                    Assert.That(scene.Brain.GetMovement(), Is.EqualTo(impulse), degrees + " degrees");
                }
            });
        }

        // The offset is flattened onto the plane of the enemy's up direction before the distance and angle are taken
        // (the packed ProjectOnPlane at 0x572255..0x5722BD), for the default up direction and for a sideways one.
        [Test]
        public void SteeringIgnoresTheOffsetAlongTheUpDirection()
        {
            WithScene(scene =>
            {
                Assert.That(Steer(scene, new Vector3(0f, 5f, 10f)), Is.False);
                Assert.That(scene.Brain.GetMovement(), Is.EqualTo(new Vector2(0f, 1f)), "a height offset does not turn the enemy");
                Assert.That(Steer(scene, new Vector3(0f, 5f, 0f)), Is.True, "a purely vertical offset is no distance");
                Assert.That(scene.Brain.GetMovement(), Is.EqualTo(Vector2.zero));

                SetActorProperty(scene.Enemy, "UpDirection", Vector3.right);
                Assert.That(Steer(scene, new Vector3(5f, 0f, 10f)), Is.False);
                Assert.That(scene.Brain.GetMovement(), Is.EqualTo(new Vector2(0f, 1f)), "the sideways up direction removes the x offset");
            });
        }

        // The position is measured from the enemy, not from the origin.
        [Test]
        public void SteeringMeasuresFromTheEnemyPosition()
        {
            WithScene(scene =>
            {
                SetActorProperty(scene.Enemy, "WorldPosition", new Vector3(100f, 0f, 100f));
                Assert.That(Steer(scene, new Vector3(100f, 0f, 101f)), Is.True);
                Assert.That(scene.Brain.GetMovement(), Is.EqualTo(Vector2.zero));
                Assert.That(Steer(scene, new Vector3(100f, 0f, 110f)), Is.False);
                Assert.That(scene.Brain.GetMovement(), Is.EqualTo(new Vector2(0f, 1f)));
            });
        }
    }
}
