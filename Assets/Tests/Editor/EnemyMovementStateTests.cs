using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.Enemies0102Support;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The enemy movement state (EnemyMovementState 02000656), its abstract transition base (02000659, with the
    // nested BaseJSONCtorArgs 0200065a) and EnemyMovementTransition_AttackWindUpComplete (0200065b) against the 1.10.1
    // x86-64 bodies at 0x577B20..0x577F90. The state is built through its own ConstructInstance and updated against a
    // real Enemy that holds a recording ability in its ability table.
    public sealed class EnemyMovementStateTests
    {
        private const ActorAbilityType AbilityType = ActorAbilityType.Enemy_GroundMovement;

        // Records the calls the state makes, in order, with their arguments.
        private sealed class RecordingAbility : EnemyAbility
        {
            internal readonly List<string> Calls = new List<string>();
            internal AbilityDefinition Definition;

            public override AbilityDefinition DefinitionBase => Definition;

            public override void UpdateMovement(float deltaTime) => Calls.Add("UpdateMovement " + deltaTime);

            public override void UpdateMovementAnimation(ActorAnimationDefinition animation) =>
                Calls.Add("UpdateMovementAnimation " + (animation == null ? "null" : animation.name));

            public override void UpdateVisualProxyMovement(float deltaTime) => Calls.Add("UpdateVisualProxyMovement " + deltaTime);
        }

        private static string AbilityJson() => "{\"AbilityType\":" + (int)AbilityType + "}";

        private static IFSMState NewState() =>
            EnemyMovementState.ConstructInstance(NewFsm(), new FSMIdentifier(Name()), AbilityJson());

        // The two dictionaries the effect states read from the actor's storage; a started Actor creates them.
        private static void PrimeEffectStorage(Enemy enemy)
        {
            enemy.Storage.SetValue(ActorFSMKeys.ActiveAnimationLookup, new Dictionary<string, ActorAnimationDefinition>());
            enemy.Storage.SetValue(ActorFSMKeys.FullscreenEffectDictionary, new Dictionary<int, List<FullscreenShaderManager.ParametersHandle>>());
        }

        private static ActorAnimationDefinition Animation(Scene scene, string name)
        {
            var animation = scene.Owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
            animation.name = name;
            return animation;
        }

        // ---- declarations ----

        // Flags, member order, Il2CppSetOption order (1 = NullChecks, 2 = ArrayBoundsChecks) and the graph menu from the
        // DummyDll tables. An emission self-check of the restored declarations, not a native behaviour oracle.
        [Test]
        public void EmissionCheck_StateAndTransitionsKeepTheirFlagsMembersAndMenus()
        {
            var none = new (string, int)[0];
            AssertMembers(typeof(EnemyMovementState), 0x00100001, none,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) });
            AssertIl2CppOptions(typeof(EnemyMovementState), 1, 2);
            Assert.That(typeof(EnemyMovementState).BaseType, Is.EqualTo(typeof(ActorState_Ability<EnemyAbility>)));
            Assert.That(Menu(typeof(EnemyMovementState)), Is.EqualTo("Enemy/Movement/{0}"));

            AssertMembers(typeof(EnemyMovementTransition), 0x00100081, none, new[] { (".ctor", 0x1884) });
            AssertIl2CppOptions(typeof(EnemyMovementTransition), 2, 1);
            Assert.That(typeof(EnemyMovementTransition).BaseType, Is.EqualTo(typeof(FSMTransition)));
            Assert.That(Menu(typeof(EnemyMovementTransition)), Is.EqualTo("Enemy/Brain/{0}"));
            Type args = typeof(EnemyMovementTransition).GetNestedType("BaseJSONCtorArgs", Declared);
            AssertMembers(args, 0x00102004, none, new[] { (".ctor", 0x1886) });
            AssertIl2CppOptions(args, 1, 2);
            Assert.That(args.GetCustomAttributes(typeof(FiniteStateMachineJsonCtorArgsAttribute), false), Has.Length.EqualTo(1));
            Assert.That(args.BaseType, Is.EqualTo(typeof(object)));

            AssertMembers(typeof(EnemyMovementTransition_AttackWindUpComplete), 0x00100001, none,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) });
            AssertIl2CppOptions(typeof(EnemyMovementTransition_AttackWindUpComplete), 1, 2);
            Assert.That(typeof(EnemyMovementTransition_AttackWindUpComplete).BaseType, Is.EqualTo(typeof(EnemyMovementTransition)));
            Assert.That(Menu(typeof(EnemyMovementTransition_AttackWindUpComplete)), Is.EqualTo("Enemy/Movement/{0}"));
        }

        private static string Menu(Type type) =>
            type.GetCustomAttributes(typeof(GraphNodeMenuFormatAttribute), false).Cast<GraphNodeMenuFormatAttribute>().Single().Format;

        // The class factory finds both concrete nodes by their static ConstructInstance. The state takes the ability
        // argument class of the ability state base; the transition takes the argument class nested in the abstract base,
        // which is itself never registered.
        [Test]
        public void ClassFactoryRegistersTheStateAndTheTransitionWithTheirArgumentTypes()
        {
            Assert.That(FSMClassFactory.States.TryGetValue(nameof(EnemyMovementState), out JSONFactoryClass state), Is.True);
            Assert.That(state.ClassType, Is.EqualTo(typeof(EnemyMovementState)));
            Assert.That(state.ConstructInstanceMethod.DeclaringType, Is.EqualTo(typeof(EnemyMovementState)));
            Assert.That(state.JSONCtorArgsType, Is.EqualTo(typeof(ActorState_Ability).GetNestedType("AbilityJSONCtorArgs", Declared)));

            Assert.That(FSMClassFactory.Transitions.TryGetValue(nameof(EnemyMovementTransition_AttackWindUpComplete), out JSONFactoryClass transition), Is.True);
            Assert.That(transition.ClassType, Is.EqualTo(typeof(EnemyMovementTransition_AttackWindUpComplete)));
            Assert.That(transition.JSONCtorArgsType, Is.EqualTo(typeof(EnemyMovementTransition).GetNestedType("BaseJSONCtorArgs", Declared)));
            Assert.That(FSMClassFactory.Transitions.ContainsKey(nameof(EnemyMovementTransition)), Is.False);
        }

        // 0x577B90 builds the state and the base constructor registers it in the machine it was given.
        [Test]
        public void TheStateIsBuiltFromItsAbilityArgumentAndRegisteredInItsMachine()
        {
            var fsm = NewFsm();
            var id = new FSMIdentifier(Name());
            IFSMState state = FSMClassFactory.ConstructState(fsm, nameof(EnemyMovementState), id, AbilityJson());
            Assert.That(state, Is.TypeOf<EnemyMovementState>());
            Assert.That(fsm.States[id.Id], Is.SameAs(state));
        }

        // ---- the state update ----

        // 0x577C50: after the effect base update the state fetches the enemy's ability for its ability type and calls
        // the movement update, then the visual proxy update (both with the frame time), then the movement animation
        // with the state's own animation definition (slot order 23, 25, 24; not the ability's definition animation).
        [Test]
        public void UpdateMovesTheEnemyThenItsProxyThenAnimatesWithTheStateAnimation()
        {
            WithScene(scene =>
            {
                PrimeEffectStorage(scene.Enemy);
                var ability = new RecordingAbility { Definition = scene.Owned.Track(ScriptableObject.CreateInstance<EnemyAbilityDefinition_Idle>()) };
                Get<Dictionary<ActorAbilityType, ActorAbility>>(scene.Enemy, "m_abilities")[AbilityType] = ability;
                IFSMState state = NewState();
                Set(state, "m_stateAnimationDefinition", Animation(scene, "state animation"));

                state.Update(scene.Enemy, Context(0.25f));
                Assert.That(ability.Calls, Is.EqualTo(new[] { "UpdateMovement 0.25", "UpdateVisualProxyMovement 0.25", "UpdateMovementAnimation state animation" }));

                ability.Definition.AnimationDefinition = Animation(scene, "ability animation");
                ability.Calls.Clear();
                state.Update(scene.Enemy, Context(0.5f));
                Assert.That(ability.Calls, Is.EqualTo(new[] { "UpdateMovement 0.5", "UpdateVisualProxyMovement 0.5", "UpdateMovementAnimation state animation" }),
                    "the movement animation still takes the state's own definition");
            });
        }

        // The effect base publishes the animation the state resolves (the ability's, else the state's) in the actor's
        // active animation lookup, which the movement update itself does not touch.
        [Test]
        public void UpdateStillRunsTheEffectBaseThatPublishesTheResolvedAnimation()
        {
            WithScene(scene =>
            {
                PrimeEffectStorage(scene.Enemy);
                var ability = new RecordingAbility { Definition = scene.Owned.Track(ScriptableObject.CreateInstance<EnemyAbilityDefinition_Idle>()) };
                Get<Dictionary<ActorAbilityType, ActorAbility>>(scene.Enemy, "m_abilities")[AbilityType] = ability;
                IFSMState state = NewState();
                ActorAnimationDefinition fallback = Animation(scene, "state animation");
                Set(state, "m_stateAnimationDefinition", fallback);

                state.Update(scene.Enemy, Context());
                Dictionary<string, ActorAnimationDefinition> lookup = scene.Enemy.GetActiveAnimationLookup();
                Assert.That(lookup.Values, Is.EqualTo(new[] { fallback }), "no ability animation: the state's is published");

                ActorAnimationDefinition own = Animation(scene, "ability animation");
                ability.Definition.AnimationDefinition = own;
                state.Update(scene.Enemy, Context());
                Assert.That(lookup.Values, Is.EqualTo(new[] { own }), "the ability's animation replaces it");
            });
        }

        // ---- the transition ----

        // 0x577F90: the transition is the enemy's wind-up-attack-complete flag; the JSON arguments carry nothing
        // (0x577F00 parses them into the empty argument class and the base constructor discards them).
        [Test]
        public void AttackWindUpCompleteReportsTheEnemyFlag()
        {
            WithScene(scene =>
            {
                IFSMTransition transition = EnemyMovementTransition_AttackWindUpComplete.ConstructInstance(NewFsm(), new FSMIdentifier(Name()), "{}");
                Assert.That(transition.Update(scene.Enemy, Context()), Is.False);
                scene.Flag("WindUpAttackComplete", true);
                Assert.That(transition.Update(scene.Enemy, Context()), Is.True);
                scene.Flag("WindUpAttackComplete", false);
                Assert.That(transition.Update(scene.Enemy, Context()), Is.False);
            });
        }

        [Test]
        public void AttackWindUpCompleteIsBuiltFromAnyArgumentsThroughTheFactory()
        {
            var fsm = NewFsm();
            var id = new FSMIdentifier(Name());
            IFSMTransition transition = FSMClassFactory.ConstructTransition(fsm, nameof(EnemyMovementTransition_AttackWindUpComplete), id, "{\"Unknown\":1}");
            Assert.That(transition, Is.TypeOf<EnemyMovementTransition_AttackWindUpComplete>());
        }
    }
}
