using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The rolling and setting transitions and the ground running state. Expected values come from the 1.10.1 x86-64
    // bodies cited per test; each transition is built from its JSON through the node factory and updated against a
    // real Character on an owned inactive host.
    public sealed class CharacterRollingSettingTransitionTests
    {
        private static string Name() => "Lucid-moves07-" + Guid.NewGuid().ToString("N");

        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static IFSMTransition BuildTransition(string node, string json) =>
            FSMClassFactory.ConstructTransition(Machine(), node, new FSMIdentifier(Name()), json);

        private static bool Update(IFSMTransition transition, IGraphUser user) =>
            transition.Update(user, new FSMUpdateContext(0.02f, FSMUpdateType.FixedUpdate));

        private static string Flag(bool value) => value ? "true" : "false";

        private static string RollingJson(bool inverse, bool resetVelocity = false) =>
            "{\"AbilityType\":" + (int)ActorAbilityType.Character_MovementGroundRolling + ",\"Inverse\":" + Flag(inverse) +
            ",\"ResetVelocity\":" + Flag(resetVelocity) + "}";

        // A character with a rolling ability registered under its own type; the ability reports its trigger and exit
        // flags through their private setters, as its own update does.
        private static void WithRolling(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_Rolling> body) => Owned.Run(owned =>
        {
            owned.Lease(new VerificationFixtureOwnership.ActionPoolLease());
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            CharacterGravityVectorProbe character = NewCharacter(owned);
            var rolling = new CharacterAbility_Rolling();
            Set(rolling, "m_character", character);
            Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities")[ActorAbilityType.Character_MovementGroundRolling] = rolling;
            body(owned, character, rolling);
        });

        private static void SetRolling(CharacterAbility_Rolling rolling, bool triggered, bool exiting)
        {
            SetProperty(rolling, "IsRollingTriggered", triggered);
            SetProperty(rolling, "IsRollingExiting", exiting);
        }

        // ---- CharacterTransition_Rolling (020003fe) ----

        // 0x794040: the base transition first, then the rolling ability of the configured type (TryGetAbility); with
        // none the result is false, even when inverted. With it, IsRollingTriggered (+0xE8) compares with the inverse flag
        // (+0x20): test al / sete when inverted, setne otherwise, so the result is triggered != inverse. The exit flag
        // (+0xE9) plays no part.
        [Test]
        public void RollingFiresWhileTheRollingTriggerDiffersFromTheInverseFlag()
        {
            WithRolling((owned, character, rolling) =>
            {
                IFSMTransition plain = BuildTransition(nameof(CharacterTransition_Rolling), RollingJson(false));
                IFSMTransition inverted = BuildTransition(nameof(CharacterTransition_Rolling), RollingJson(true));
                foreach ((bool triggered, bool exiting) in new[] { (false, false), (true, false), (false, true), (true, true) })
                {
                    SetRolling(rolling, triggered, exiting);
                    Assert.That(Update(plain, character), Is.EqualTo(triggered), "plain, triggered " + triggered + ", exiting " + exiting);
                    Assert.That(Update(inverted, character), Is.EqualTo(!triggered), "inverted, triggered " + triggered + ", exiting " + exiting);
                }
            });
        }

        [Test]
        public void RollingIsFalseWithoutTheRollingAbilityEvenWhenInverted()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                owned.LeaseRow<App>();
                CharacterGravityVectorProbe character = NewCharacter(owned);
                Assert.That(Update(BuildTransition(nameof(CharacterTransition_Rolling), RollingJson(false)), character), Is.False);
                Assert.That(Update(BuildTransition(nameof(CharacterTransition_Rolling), RollingJson(true)), character), Is.False, "0x7940E2 skips the inverse test");
            });
        }

        [Test]
        public void RollingKeepsTheFlagsOfItsJson()
        {
            IFSMTransition transition = BuildTransition(nameof(CharacterTransition_Rolling), RollingJson(true, true));
            Assert.That(transition.GetType(), Is.EqualTo(typeof(CharacterTransition_Rolling)));
            Assert.That(Get<bool>(transition, "m_inverseCheck"), Is.True);
            Assert.That(Get<bool>(transition, "m_resetVelocity"), Is.True);
            Assert.That(Get<ActorAbilityType>(transition, "m_abilityType"), Is.EqualTo(ActorAbilityType.Character_MovementGroundRolling));
            IFSMTransition defaults = BuildTransition(nameof(CharacterTransition_Rolling), "{}");
            Assert.That((Get<bool>(defaults, "m_inverseCheck"), Get<bool>(defaults, "m_resetVelocity")), Is.EqualTo((false, false)), "omitted fields are false");
        }

        private static void Leave(IFSMTransition transition, IGraphUser user, FSMStateChangeAction action) => transition.OnLeave(user, action);

        // 0x794100: the base hook first; the velocity is cleared (vcall to SetWorldVelocity with Vector3.zero) only when
        // ResetVelocity (+0x21) is set and the action's TransitionThatFired (+0x28) is this very transition; a null
        // action (0x794146) and the flag clear (0x79413F) skip it.
        [Test]
        public void RollingClearsTheVelocityOnlyWhenItFiredAndIsAskedTo()
        {
            WithRolling((owned, character, rolling) =>
            {
                IFSMTransition resetting = BuildTransition(nameof(CharacterTransition_Rolling), RollingJson(false, true));
                IFSMTransition keeping = BuildTransition(nameof(CharacterTransition_Rolling), RollingJson(false, false));
                IFSMTransition other = BuildTransition(nameof(CharacterTransition_Rolling), RollingJson(false, true));
                Vector3 velocity = new Vector3(0f, 0f, 5f);

                character.SetWorldVelocity(velocity);
                Leave(resetting, character, null);
                Assert.That(character.WorldVelocity, Is.EqualTo(velocity), "no action");

                Leave(resetting, character, FSMStateChangeAction.Create(FSMActionReason.TransitionFired, transitionThatFired: other));
                Assert.That(character.WorldVelocity, Is.EqualTo(velocity), "another transition fired");

                Leave(keeping, character, FSMStateChangeAction.Create(FSMActionReason.TransitionFired, transitionThatFired: keeping));
                Assert.That(character.WorldVelocity, Is.EqualTo(velocity), "ResetVelocity is false");

                Leave(resetting, character, FSMStateChangeAction.Create(FSMActionReason.TransitionFired, transitionThatFired: resetting));
                Assert.That(character.WorldVelocity, Is.EqualTo(Vector3.zero), "this transition fired and resets");
            });
        }

        // ---- CharacterTransition_RollingToRunning (02000400) ----

        private static string ToRunningJson(bool inverse) =>
            "{\"AbilityType\":" + (int)ActorAbilityType.Character_MovementGroundRolling + ",\"Inverse\":" + Flag(inverse) + "}";

        // 0x7942C0: as above but on IsRollingExiting (+0xE9) alone, and the ability is read with GetAbility.
        [Test]
        public void RollingToRunningFiresWhileTheRollingExitDiffersFromTheInverseFlag()
        {
            WithRolling((owned, character, rolling) =>
            {
                IFSMTransition plain = BuildTransition(nameof(CharacterTransition_RollingToRunning), ToRunningJson(false));
                IFSMTransition inverted = BuildTransition(nameof(CharacterTransition_RollingToRunning), ToRunningJson(true));
                foreach ((bool triggered, bool exiting) in new[] { (false, false), (true, false), (false, true), (true, true) })
                {
                    SetRolling(rolling, triggered, exiting);
                    Assert.That(Update(plain, character), Is.EqualTo(exiting), "plain, triggered " + triggered + ", exiting " + exiting);
                    Assert.That(Update(inverted, character), Is.EqualTo(!exiting), "inverted, triggered " + triggered + ", exiting " + exiting);
                }
            });
        }

        // 0x794335 calls Actor.GetAbility and 0x79433E reads +0xE9 from the result with no null test, so a character
        // without the rolling ability faults here instead of answering false as the rolling transition does.
        [Test]
        public void RollingToRunningReadsAMissingAbilityWithoutATest()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                owned.LeaseRow<App>();
                CharacterGravityVectorProbe character = NewCharacter(owned);
                IFSMTransition transition = BuildTransition(nameof(CharacterTransition_RollingToRunning), ToRunningJson(false));
                Assert.Throws<NullReferenceException>(() => Update(transition, character));
            });
        }

        [Test]
        public void RollingToRunningKeepsTheFlagOfItsJson()
        {
            IFSMTransition transition = BuildTransition(nameof(CharacterTransition_RollingToRunning), ToRunningJson(true));
            Assert.That(transition.GetType(), Is.EqualTo(typeof(CharacterTransition_RollingToRunning)));
            Assert.That(Get<bool>(transition, "m_inverseCheck"), Is.True);
            Assert.That(Get<bool>(BuildTransition(nameof(CharacterTransition_RollingToRunning), "{}"), "m_inverseCheck"), Is.False);
        }

        // ---- CharacterTransition_Setting / CharacterTransition_SettingHomingAttack (02000402, 02000404) ----

        private static string SettingJson(ButtonMappingType mapping) => "{\"ButtonMapping\":" + (int)mapping + "}";

        // 0x794510 reads the saved homing attack mapping (+0x54), not the air stomp mapping (+0x58); the constructor
        // (0x794440) stores the JSON mapping (+0x1C).
        [Test]
        public void SettingHomingAttackReadsTheSavedHomingAttackMapping()
        {
            IFSMTransition transition = BuildTransition(nameof(CharacterTransition_SettingHomingAttack), SettingJson(ButtonMappingType.Primary));
            Assert.That(transition.GetType(), Is.EqualTo(typeof(CharacterTransition_SettingHomingAttack)));
            Assert.That(Get<ButtonMappingType>(transition, "m_buttonMapping"), Is.EqualTo(ButtonMappingType.Primary));
            var settings = new SaveDataSettings
            {
                HomingAttackMapping = ButtonMappingType.Primary,
                AirStompAttackMapping = ButtonMappingType.Secondary,
            };
            Assert.That(Call(transition, "GetButtonMapping", settings), Is.EqualTo(ButtonMappingType.Primary));
            settings.HomingAttackMapping = ButtonMappingType.Secondary;
            settings.AirStompAttackMapping = ButtonMappingType.Primary;
            Assert.That(Call(transition, "GetButtonMapping", settings), Is.EqualTo(ButtonMappingType.Secondary), "not the air stomp mapping");
        }

        [Test]
        public void SettingTransitionsKeepAnOmittedMappingAtNone()
        {
            IFSMTransition transition = BuildTransition(nameof(CharacterTransition_SettingHomingAttack), "{}");
            Assert.That(Get<ButtonMappingType>(transition, "m_buttonMapping"), Is.EqualTo(ButtonMappingType.None));
        }

        // 0x7943A0: the base transition, the character from the user, its saved settings, then the abstract mapping
        // getter (vcall slot 18) compared with the stored mapping. Reading the settings needs a started character's
        // save manager, so the order is read from the compiled IL.
        [Test]
        public void CallOrderSettingUpdateComparesTheMappingOfTheCharactersSavedSettings()
        {
            List<MethodBase> calls = Calls(typeof(CharacterTransition_Setting), "DoUpdate");
            int baseUpdate = IndexOf(calls, "FSMTransition", "DoUpdate");
            int character = IndexOf(calls, "IGraphUserExtensions", "GetAs", baseUpdate + 1);
            int settings = IndexOf(calls, "Character", "get_SaveDataSettings", character + 1);
            int mapping = IndexOf(calls, "CharacterTransition_Setting", "GetButtonMapping", settings + 1);
            Assert.That(mapping, Is.EqualTo(calls.Count - 1), "the mapping getter is the last call");
        }

        // ---- CharacterState_GroundRunning (0200036d) ----

        // 0x776BB0: the factory builds the state from the ability JSON and the base keeps the ability type.
        [Test]
        public void GroundRunningStateReadsItsAbilityTypeFromItsJson()
        {
            IFSMState state = FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_GroundRunning), new FSMIdentifier(Name()),
                "{\"AbilityType\":" + (int)ActorAbilityType.Character_MovementGroundRunning + "}");
            Assert.That(state.GetType(), Is.EqualTo(typeof(CharacterState_GroundRunning)));
            Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ActorAbilityType.Character_MovementGroundRunning));
        }

        // ---- Registration and graph authoring metadata (declaration shape, not behaviour) ----

        private static void AssertNode(Type node, string defaultName)
        {
            Assert.That(node.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo(defaultName), node.Name);
            Assert.That(node.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Character/{0}"), node.Name);
        }

        private static void AssertRegistered(IReadOnlyDictionary<string, JSONFactoryClass> lookup, Type node, string argsName, Type argsOwner)
        {
            Assert.That(lookup.TryGetValue(node.Name, out JSONFactoryClass factory), Is.True, node.Name);
            Assert.That(factory.ClassType, Is.EqualTo(node), node.Name);
            Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(node), node.Name);
            Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo(argsName), node.Name);
            Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(argsOwner), node.Name);
        }

        [Test]
        public void DeclarationShapeNodesCarryTheirOriginalGraphAuthoringAttributes()
        {
            AssertNode(typeof(CharacterTransition_Rolling), "ToRolling");
            AssertNode(typeof(CharacterTransition_RollingToRunning), "RollingToRunning");
            AssertNode(typeof(CharacterTransition_SettingHomingAttack), "SettingHomingAttack");
            AssertNode(typeof(CharacterState_GroundRunning), "GroundRunning");
            Assert.That(typeof(CharacterTransition_Setting).GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Character/{0}"));
            Assert.That(typeof(CharacterTransition_Setting).GetCustomAttribute<GraphNodeDefaultNameAttribute>(false), Is.Null, "the abstract base has no default name");
        }

        // The nested argument classes are found by the name JSONCtorArgs alone: the rolling pair carry no attribute in
        // the original, the setting base carries both [Serializable] and the marker.
        [Test]
        public void DeclarationShapeClassFactoryRegistersTheNodesWithTheirArgumentTypes()
        {
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_Rolling), "JSONCtorArgs", typeof(CharacterTransition_Rolling));
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_RollingToRunning), "JSONCtorArgs", typeof(CharacterTransition_RollingToRunning));
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_SettingHomingAttack), "JSONCtorArgs", typeof(CharacterTransition_Setting));
            AssertRegistered(FSMClassFactory.States, typeof(CharacterState_GroundRunning), "AbilityJSONCtorArgs", typeof(ActorState_Ability));
            Assert.That(FSMClassFactory.Transitions.ContainsKey(nameof(CharacterTransition_Setting)), Is.False, "the abstract base is not registered");

            const BindingFlags nonPublic = BindingFlags.NonPublic;
            Type rolling = typeof(CharacterTransition_Rolling).GetNestedType("JSONCtorArgs", nonPublic);
            Assert.That(rolling.GetCustomAttribute<FiniteStateMachineJsonCtorArgsAttribute>(false), Is.Null);
            Assert.That(rolling.IsNestedFamily, Is.True);
            Type setting = typeof(CharacterTransition_Setting).GetNestedType("JSONCtorArgs", nonPublic);
            Assert.That(setting.GetCustomAttribute<FiniteStateMachineJsonCtorArgsAttribute>(false), Is.Not.Null);
            Assert.That(setting.IsSerializable, Is.True);
        }
    }
}
