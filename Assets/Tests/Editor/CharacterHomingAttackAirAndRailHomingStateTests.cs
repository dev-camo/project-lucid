using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
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
    // The air homing attack states (TypeDefs 0x02000373..0x02000378), the ground homing attack states (0x02000379,
    // 0x0200037a) and the rail homing states (0x02000389..0x0200038c), x86-64 bodies 0xE73850..0xE76210 (shared generic)
    // and 0x7797E0..0x77DF20. Construction from JSON, the debug text and the declarations run against real objects. The
    // enter, update and leave bodies reach the camera, form, stamina and tracker graphs of a started Character (the base
    // CharacterState_Ability enter and update), so they are checked as call sequences read from the compiled IL; those
    // witnesses are emission-only checks of the reconstruction's own IL, separate from the behaviour cases. Not covered by
    // any test, because they need a started Character: the arithmetic of the three update bodies (the step clamp with
    // 0xE73BC8 jb / 0xE75AD1 jbe, the 180 degree turn for a zero rotational speed 0xE75BE3, the ground steering
    // conditions 0x779E98 ColliderRadiusSqr > sqrMagnitude / 0x779EA5 height > ColliderRadius), the air enter force trigger
    // polarity (0xE739C4 xor r14b,1), the rail homing timeout (0xE7592C jae, equality cancels), the rail homing update
    // without an active target (0xE756B2 je 0xE757B7, which stores the cancelled flag) and the enter and leave effects.
    // The rail homing update starts with the CharacterState_Ability update (call at 0xE75688), so the no target path
    // needs the same started Character.
    public sealed class CharacterHomingAttackAirAndRailHomingStateTests
    {
        private static string Name() => "Lucid-homing-attack-state-" + Guid.NewGuid().ToString("N");

        private static IFSMState NewState(string node, string json) =>
            FSMClassFactory.ConstructState(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private static string AbilityJson(ActorAbilityType type, string extra = "") => "{\"AbilityType\":" + (int)type + extra + "}";

        private static Dictionary<ActorAbilityType, ActorAbility> Abilities(Character character) =>
            (Dictionary<ActorAbilityType, ActorAbility>)Get<object>(character, "m_abilities");

        private static readonly (Type State, ActorAbilityType Ability, string Node)[] AirStates =
        {
            (typeof(CharacterState_HomingAttackAirOneShot), ActorAbilityType.Character_HomingAttackAirOneShot, "HomingAttackAirOneShot"),
            (typeof(CharacterState_HomingAttackAirTarget), ActorAbilityType.Character_HomingAttackAirTarget, "HomingAttackAirTarget"),
        };

        private static readonly (Type State, ActorAbilityType Ability, string Node)[] LeafStates =
        {
            (typeof(CharacterState_HomingAttackGroundRolling), ActorAbilityType.Character_HomingAttackGroundRolling, "HomingAttackGroundRolling"),
            (typeof(CharacterState_HomingAttackGroundRunning), ActorAbilityType.Character_HomingAttackGroundRunning, "HomingAttackGroundRunning"),
            (typeof(CharacterState_RailHomingProximityAir), ActorAbilityType.Character_RailsProximityAir, "RailHomingProximityAir"),
            (typeof(CharacterState_RailHomingProximityGround), ActorAbilityType.Character_RailsProximityGround, "RailHomingProximityGround"),
            (typeof(CharacterState_RailHomingTargeting), ActorAbilityType.Character_RailsTargeting, "RailHomingTargeting"),
        };

        // ---- Construction ----

        // 0x7797E0 and 0x779950 take the air arguments and 0xE73850 stores the ignored ability list of them (+0x20 of the
        // arguments into +0x40 of the state); the factory builds each leaf through its ConstructInstance. The ability type of
        // the arguments is deliberately different from the state's own, to show it comes from the JSON.
        [Test]
        public void AirStatesReadTheirAbilityTypeAndIgnoredAbilityListFromJson()
        {
            foreach ((Type type, ActorAbilityType _, string _) in AirStates)
            {
                IFSMState state = NewState(type.Name, AbilityJson(ActorAbilityType.Character_Jump, ",\"IgnoreForceTriggerAbilityTypes\":[" + (int)ActorAbilityType.Character_Idle + "," + (int)ActorAbilityType.Character_InHub + "]"));
                Assert.That(state.GetType(), Is.EqualTo(type));
                Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ActorAbilityType.Character_Jump), type.Name);
                Assert.That(Get<List<ActorAbilityType>>(state, "m_ignoreForceTriggerAbilityTypes"),
                    Is.EqualTo(new List<ActorAbilityType> { ActorAbilityType.Character_Idle, ActorAbilityType.Character_InHub }), type.Name);
            }
        }

        // 0x779AC0, 0x77A140 and 0x77DC50..0x77DEB0 pass the plain arguments on to their bases.
        [Test]
        public void GroundAndRailLeafStatesReadTheirAbilityTypeFromJson()
        {
            foreach ((Type type, ActorAbilityType _, string _) in LeafStates)
            {
                IFSMState state = NewState(type.Name, AbilityJson(ActorAbilityType.Character_LedgeMount));
                Assert.That(state.GetType(), Is.EqualTo(type));
                Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ActorAbilityType.Character_LedgeMount), type.Name);
            }
        }

        // ---- Debug text ----

        // The rows the ability constructors read, a real homing pool and the ability registered under its type, the way the
        // state's GetAbility finds it.
        private static void WithAbilityInCharacter<TAbility, TDefinition>(ActorAbilityType type, Action<Owned, CharacterGravityVectorProbe, TAbility, TDefinition> body)
            where TAbility : CharacterAbility, new()
            where TDefinition : CharacterAbilityDefinition => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            owned.LeaseRow<MessageManager>();
            owned.LeaseRow<CinemachineCameraManager>();
            CharacterGravityVectorProbe character = NewCharacter(owned);
            SetProperty(character, "HomingPool", new CharacterHomingPool(character, new Dictionary<HomingTargetType, TargetingTypePriorityDefinition>()));
            var definition = owned.Track(ScriptableObject.CreateInstance<TDefinition>());
            var ability = new TAbility();
            Set(ability, "m_character", character);
            Set(ability, "m_definition", definition);
            Abilities(character)[type] = ability;
            body(owned, character, ability, definition);
        });

        private static void ActivateTarget(Owned owned, Character character, IHomingAbility ability, float speed, float rotationalSpeed)
        {
            GameObject target = owned.Host("Lucid homing target");
            character.HomingPool.SetTargetImmediately(ability, new CharacterTargetObjectData { Target = target, WorldPosition = new Vector3(3f, 4f, 0f), WorldRotation = Quaternion.identity },
                Vector3.zero, speed, rotationalSpeed);
        }

        // 0xE73D00..0xE73FDA: without a live target the debug text ends with the error line (the op_Equality of the target at
        // 0xE73E29 also catches a missing record); with one it names the target and gives the speed and the distance to it with
        // two decimals, in the order Target, Speed (followed by a comma and a space) and Distance.
        [Test]
        public void TheAirStateDebugTextNamesTheTargetItsSpeedAndTheDistance()
        {
            WithInvariantCulture(() => WithAbilityInCharacter<CharacterAbility_HomingAttackAirTarget, CharacterAbilityDefinition_HomingAttackAirTarget>(
                ActorAbilityType.Character_HomingAttackAirTarget, (owned, character, ability, definition) =>
            {
                IFSMState state = NewState(nameof(CharacterState_HomingAttackAirTarget), AbilityJson(ActorAbilityType.Character_HomingAttackAirTarget));
                var none = new StringBuilder();
                Call(state, "DebugInfo", character, none);
                Assert.That(none.ToString(), Does.EndWith("ERROR: No targeted object found." + Environment.NewLine));

                ActivateTarget(owned, character, ability, 12.5f, 0f);
                var text = new StringBuilder();
                Call(state, "DebugInfo", character, text);
                Assert.That(text.ToString(), Does.EndWith("Target = Lucid homing target" + Environment.NewLine + "Speed = 12.50, Distance = 5.00" + Environment.NewLine));
            }));
        }

        // 0xE75E30..0xE76142: the rail homing text is one line each for the target name, the speed, the distance and the
        // rotational speed, with two decimals; without a live target only the error line follows the base text.
        [Test]
        public void TheRailHomingStateDebugTextNamesTheTargetSpeedDistanceAndRotationalSpeed()
        {
            WithInvariantCulture(() => WithAbilityInCharacter<CharacterAbility_RailsTargeting, CharacterAbilityDefinition_RailsTargeting>(
                ActorAbilityType.Character_RailsTargeting, (owned, character, ability, definition) =>
            {
                IFSMState state = NewState(nameof(CharacterState_RailHomingTargeting), AbilityJson(ActorAbilityType.Character_RailsTargeting));
                var none = new StringBuilder();
                Call(state, "DebugInfo", character, none);
                Assert.That(none.ToString(), Does.EndWith("ERROR: No targeted object found." + Environment.NewLine));

                ActivateTarget(owned, character, ability, 12.5f, 2.5f);
                var text = new StringBuilder();
                Call(state, "DebugInfo", character, text);
                string newLine = Environment.NewLine;
                Assert.That(text.ToString(), Does.EndWith("Target = Lucid homing target" + newLine + "Speed = 12.50" + newLine + "Distance = 5.00" + newLine + "RotationalSpeed = 2.50" + newLine));
            }));
        }

        // ---- Call sequences (emission-only witnesses of the reconstruction's own IL) ----

        private static int Position(List<MethodBase> calls, string name, int from = 0)
        {
            int index = calls.FindIndex(from, m => m.Name == name);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), name + " is called after position " + from);
            return index;
        }

        private static void AssertInOrder(List<MethodBase> calls, params string[] names)
        {
            int at = -1;
            foreach (string name in names) at = Position(calls, name, at + 1);
        }

        // 0xE738A0: base enter, the ability, the ignored types are tested one by one with HasAbilityType, then SetActive.
        // 0xE73C50 leaves through the base and then SetActive(false, false).
        [Test]
        public void CallOrderTheAirStateEnterAndLeaveFollowTheNativeOrder()
        {
            AssertInOrder(Calls(typeof(CharacterState_HomingAttackAir<,>), "DoOnEnter"), "DoOnEnter", "GetAbility", "GetEnumerator", "HasAbilityType", "SetActive");
            AssertInOrder(Calls(typeof(CharacterState_HomingAttackAir<,>), "DoOnLeave"), "DoOnLeave", "GetAbility", "SetActive");
        }

        // 0xE73A70: base update, the ability, the remain timer, the target update, the additional speed, then the velocity
        // before the rotation (SetWorldVelocity at 0xE73C03, SetWorldRotation at 0xE73C33).
        [Test]
        public void CallOrderTheAirStateUpdateFollowsTheNativeOrder()
        {
            AssertInOrder(Calls(typeof(CharacterState_HomingAttackAir<,>), "DoUpdate"),
                "DoUpdate", "GetAbility", "UpdateRemainTimer", "UpdateTarget", "GetAdditionalSpeed", "SetWorldVelocity", "get_WorldUp", "LookRotation", "SetWorldRotation");
        }

        // 0xE75610: base update, the active target test, the bounds, the definition, the elapsed time, the blocking raycast,
        // the additional speed, the velocity, the turn; the cancel path of the same body stores the cancelled flag and clears
        // the target. 0xE75400 and 0xE75C60 are the enter and leave around the rail state's own.
        [Test]
        public void CallOrderTheRailHomingStateBodiesFollowTheNativeOrder()
        {
            AssertInOrder(Calls(typeof(CharacterState_RailHoming<,>), "DoUpdate"),
                "DoUpdate", "IsTargetActive", "get_Bounds", "GetRailsHomingDefinition", "AdjustValue", "Raycast", "GetAbility", "GetAdditionalSpeed", "SetWorldVelocity", "RotateTowards", "SetWorldRotation");
            AssertInOrder(Calls(typeof(CharacterState_RailHoming<,>), "DoOnEnter"), "DoOnEnter", "SetValue", "SetValue", "IsTargetActive");
            AssertInOrder(Calls(typeof(CharacterState_RailHoming<,>), "DoOnLeave"),
                "DoOnLeave", "RemoveValue", "IsTargetActive", "SetWorldPosition", "SetWorldVelocity", "OnTeleport", "ClearTarget");
            AssertInOrder(Calls(typeof(CharacterState_HomingAttackGroundRolling), "DoUpdate"),
                "DoUpdate", "OrientateToGround", "StickToCollider", "UpdateTarget", "SignedAngle", "AngleAxis", "UpdateForwardSpeed", "SetLocalVelocity");
            AssertInOrder(Calls(typeof(CharacterState_HomingAttackGroundRunning), "DoUpdate"),
                "DoUpdate", "OrientateToGround", "StickToCollider", "UpdateTarget", "SignedAngle", "AngleAxis", "UpdateForwardSpeed", "SetLocalVelocity");
            foreach (Type ground in new[] { typeof(CharacterState_HomingAttackGroundRolling), typeof(CharacterState_HomingAttackGroundRunning) })
            {
                AssertInOrder(Calls(ground, "DoOnEnter"), "DoOnEnter", "GetAbility", "Activate", "SetActive");
                AssertInOrder(Calls(ground, "DoOnLeave"), "DoOnLeave", "GetAbility", "SetActive");
            }
        }

        // ---- Declarations ----

        private static Type Nested(Type owner, string name) => owner.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);

        private static void AssertGraphNode(Type type, string name)
        {
            Assert.That(type.GetCustomAttributes(typeof(GraphNodeDefaultNameAttribute), false).Cast<GraphNodeDefaultNameAttribute>().Single().DefaultName, Is.EqualTo(name), type.Name);
            Assert.That(type.GetCustomAttributes(typeof(GraphNodeMenuFormatAttribute), false).Cast<GraphNodeMenuFormatAttribute>().Single().Format, Is.EqualTo("Character/{0}"), type.Name);
        }

        // The declaration rows of the original DummyDll tables; this is declaration shape, not behaviour.
        [Test]
        public void AirStatesKeepTheOriginalDeclarationShape()
        {
            Type generic = typeof(CharacterState_HomingAttackAir<,>);
            AssertMembers(generic, 0x00100081,
                new[] { ("m_ignoreForceTriggerAbilityTypes", 0x0021) },
                new[] { (".ctor", 0x1884), ("DoOnEnter", 0x00c4), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4), ("DebugInfo", 0x00c6) });
            Assert.That(generic.BaseType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterState_Ability<>)));
            Assert.That(AttributeNames(generic), Is.EqualTo(new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" }));
            Type[] constraints = generic.GetGenericArguments().Select(argument => argument.GetGenericParameterConstraints().Single()).ToArray();
            Assert.That(constraints[0].GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_HomingAttackAir<>)));
            Assert.That(constraints[1], Is.EqualTo(typeof(CharacterAbilityDefinition_HomingAttackAir)));

            Type args = Nested(generic, "HomingAttackJSONCtorArgs");
            AssertMembers(args, 0x00102004, new[] { ("IgnoreForceTriggerAbilityTypes", 0x0006) }, new[] { (".ctor", 0x1886) });
            Assert.That(AttributeNames(args), Is.EqualTo(new[] { "FiniteStateMachineJsonCtorArgs", "Il2CppSetOption:1", "Il2CppSetOption:2" }));
            Assert.That(args.IsSerializable, Is.True);
            Assert.That(args.BaseType, Is.EqualTo(typeof(ActorState_Ability).GetNestedType("AbilityJSONCtorArgs", BindingFlags.NonPublic)));

            foreach ((Type type, Type ability, Type definition, string nested, string node, string[] attributes, string[] argumentAttributes) in new[]
            {
                (typeof(CharacterState_HomingAttackAirOneShot), typeof(CharacterAbility_HomingAttackAirOneShot), typeof(CharacterAbilityDefinition_HomingAttackAirOneShot), "HomingAttackOneShotJSONCtorArgs", "HomingAttackAirOneShot",
                    new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "GraphNodeDefaultName", "GraphNodeMenuFormat" }, new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "FiniteStateMachineJsonCtorArgs" }),
                (typeof(CharacterState_HomingAttackAirTarget), typeof(CharacterAbility_HomingAttackAirTarget), typeof(CharacterAbilityDefinition_HomingAttackAirTarget), "HomingAttackTargetJSONCtorArgs", "HomingAttackAirTarget",
                    new[] { "GraphNodeMenuFormat", "GraphNodeDefaultName", "Il2CppSetOption:2", "Il2CppSetOption:1" }, new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "FiniteStateMachineJsonCtorArgs" }),
            })
            {
                AssertMembers(type, 0x00100001, new (string, int)[0], new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096) });
                Assert.That(type.BaseType, Is.EqualTo(generic.MakeGenericType(ability, definition)), type.Name);
                Assert.That(AttributeNames(type), Is.EqualTo(attributes), type.Name);
                AssertGraphNode(type, node);
                Type leafArgs = Nested(type, nested);
                AssertMembers(leafArgs, 0x00102004, new (string, int)[0], new[] { (".ctor", 0x1886) });
                Assert.That(AttributeNames(leafArgs), Is.EqualTo(argumentAttributes), nested);
                Assert.That(leafArgs.IsSerializable, Is.True, nested);
                Assert.That(leafArgs.BaseType, Is.EqualTo(args.MakeGenericType(ability, definition)), nested);
            }
        }

        [Test]
        public void GroundStatesKeepTheOriginalDeclarationShape()
        {
            foreach ((Type type, Type ability, string node, string[] attributes) in new[]
            {
                (typeof(CharacterState_HomingAttackGroundRolling), typeof(CharacterAbility_HomingAttackGroundRolling), "HomingAttackGroundRolling",
                    new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "GraphNodeDefaultName", "GraphNodeMenuFormat" }),
                (typeof(CharacterState_HomingAttackGroundRunning), typeof(CharacterAbility_HomingAttackGroundRunning), "HomingAttackGroundRunning",
                    new[] { "GraphNodeDefaultName", "GraphNodeMenuFormat", "Il2CppSetOption:1", "Il2CppSetOption:2" }),
            })
            {
                AssertMembers(type, 0x00100001, new (string, int)[0],
                    new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00c4), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4) });
                Assert.That(type.BaseType, Is.EqualTo(typeof(CharacterState_Ability<>).MakeGenericType(ability)), type.Name);
                Assert.That(AttributeNames(type), Is.EqualTo(attributes), type.Name);
                AssertGraphNode(type, node);
            }
        }

        [Test]
        public void RailHomingStatesKeepTheOriginalDeclarationShape()
        {
            Type generic = typeof(CharacterState_RailHoming<,>);
            AssertMembers(generic, 0x00100081, new (string, int)[0],
                new[] { (".ctor", 0x1884), ("DoOnEnter", 0x00c4), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4), ("DebugInfo", 0x00c6), ("GetRailsHomingDefinition", 0x0081) });
            Assert.That(generic.BaseType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterState_Rail<>)));
            Assert.That(AttributeNames(generic), Is.EqualTo(new[] { "Il2CppSetOption:2", "Il2CppSetOption:1", "GraphNodeMenuFormat" }));
            Assert.That(generic.GetCustomAttributes(typeof(GraphNodeDefaultNameAttribute), false), Is.Empty);
            Type[] constraints = generic.GetGenericArguments().Select(argument => argument.GetGenericParameterConstraints().Single()).ToArray();
            Assert.That(constraints[0].GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_TargetingHoming<>)));
            Assert.That(constraints[1], Is.EqualTo(typeof(CharacterAbilityDefinition_Targeting)));

            foreach ((Type type, Type ability, Type definition, string node, string[] attributes) in new[]
            {
                (typeof(CharacterState_RailHomingProximityAir), typeof(CharacterAbility_RailsProximityAir), typeof(CharacterAbilityDefinition_RailsProximityAir), "RailHomingProximityAir",
                    new[] { "GraphNodeDefaultName", "Il2CppSetOption:1", "GraphNodeMenuFormat", "Il2CppSetOption:2" }),
                (typeof(CharacterState_RailHomingProximityGround), typeof(CharacterAbility_RailsProximityGround), typeof(CharacterAbilityDefinition_RailsProximityGround), "RailHomingProximityGround",
                    new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "GraphNodeDefaultName", "GraphNodeMenuFormat" }),
                (typeof(CharacterState_RailHomingTargeting), typeof(CharacterAbility_RailsTargeting), typeof(CharacterAbilityDefinition_RailsTargeting), "RailHomingTargeting",
                    new[] { "GraphNodeMenuFormat", "Il2CppSetOption:1", "Il2CppSetOption:2", "GraphNodeDefaultName" }),
            })
            {
                AssertMembers(type, 0x00100001, new (string, int)[0], new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096) });
                Assert.That(type.BaseType, Is.EqualTo(generic.MakeGenericType(ability, definition)), type.Name);
                Assert.That(AttributeNames(type), Is.EqualTo(attributes), type.Name);
                AssertGraphNode(type, node);
            }
        }
    }
}
