using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The chaos aim and chaos dash FSM nodes (TypeDefs 0x02000352..0x02000354): the shared generic base
    // CharacterState_AirChaos`2 (0xE6B2E0..0xE6B840) and the two concrete nodes (0x774DE0..0x774F80). The
    // character is a real component on an owned inactive host and the abilities are built by their real
    // constructors. Not run here: DoOnEnter and DoOnLeave (the air ability base state they call first needs a started
    // character with an orientation, velocity storage and the save data settings) and DoUpdate with an ability
    // present (the ability's time scale update needs a live time manager and its activation the audio filter).
    public sealed class CharacterAirChaosStateTests : OwnedGraphNamesFixture
    {
        public CharacterAirChaosStateTests() : base("Lucid-air-chaos-state-") { }

        private static readonly Type AimBase = typeof(CharacterState_AirChaos<CharacterAbility_AirChaosAim, CharacterAbilityDefinition_AirChaosAim>);

        private sealed class AimProbeState : CharacterState_AirChaosAim
        {
            public AimProbeState(FiniteStateMachine fsm, FSMIdentifier stateId)
                : base(fsm, stateId, new AbilityJSONCtorArgs { AbilityType = ActorAbilityType.Character_AirChaosAim })
            {
            }
        }

        private static string Name() => "Lucid-air-chaos-state-" + Guid.NewGuid().ToString("N");

        private static AimProbeState NewState() => new AimProbeState(new FiniteStateMachine(Name(), skipAddToManager: true), new FSMIdentifier(Name()));

        private static void WithCharacter(Action<Owned, Character> body) =>
            WithAbility<CharacterAbility_AirChaosAim, CharacterAbilityDefinition_AirChaosAim>(owned =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<CinemachineCameraManager>();
            }, (owned, character, ability, definition) => body(owned, character));

        private static Dictionary<ActorAbilityType, ActorAbility> Abilities(Character character) =>
            Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities");

        private static bool TryGetAbility(object state, Character character, out CharacterAbility_AirChaosAim ability)
        {
            var arguments = new object[] { character, null };
            bool found = (bool)AimBase.GetMethod("TryGetAbility", DeclaredMembers).Invoke(state, arguments);
            ability = (CharacterAbility_AirChaosAim)arguments[1];
            return found;
        }

        // 0xE6B88b..0xE6B999: the ability registered under the state's ability type is cast to the closed ability type
        // (IsInst), and the result is whether that cast produced an object. An air chaos dash ability registered under
        // the aim type is not an aim ability, so it is refused; with nothing registered the lookup fails too.
        [Test]
        public void TryGetAbilityAcceptsOnlyAnAbilityOfTheClosedType()
        {
            WithCharacter((owned, character) =>
            {
                AimProbeState state = NewState();
                Assert.That(TryGetAbility(state, character, out CharacterAbility_AirChaosAim missing), Is.False);
                Assert.That(missing, Is.Null);
                Abilities(character)[ActorAbilityType.Character_AirChaosAim] = new CharacterAbility_AirChaosDash();
                Assert.That(TryGetAbility(state, character, out CharacterAbility_AirChaosAim mismatched), Is.False);
                Assert.That(mismatched, Is.Null);
                var aim = new CharacterAbility_AirChaosAim();
                Abilities(character)[ActorAbilityType.Character_AirChaosAim] = aim;
                Assert.That(TryGetAbility(state, character, out CharacterAbility_AirChaosAim found), Is.True);
                Assert.That(found, Is.SameAs(aim));
            });
        }

        // 0xE6B5c7..0xE6B60b: when the ability cannot be used the update skips the air ability update and instead sets
        // the character's world velocity to zero (virtual slot 37 with Vector3.zero).
        [Test]
        public void UpdateStopsTheCharacterWhenTheStateHasNoAbility()
        {
            WithCharacter((owned, character) =>
            {
                AimProbeState state = NewState();
                character.SetWorldVelocity(new Vector3(0f, 0f, 5f));
                Call(state, "DoUpdate", character, new FSMUpdateContext(0.02f, FSMUpdateType.FixedUpdate));
                Assert.That(character.WorldVelocity, Is.EqualTo(Vector3.zero));
            });
        }

        // ---- ConstructInstance (0600151e @0x774E50 and 06001520 @0x774F80) ----

        // The factory parses the JSON arguments and builds the node of its own type; the parsed ability type is the one
        // the node looks its ability up by.
        [Test]
        public void ConstructInstanceBuildsTheNodeOfEachConcreteTypeFromItsJsonAbilityType()
        {
            var fsm = new FiniteStateMachine(Name(), skipAddToManager: true);
            IFSMState aim = CharacterState_AirChaosAim.ConstructInstance(fsm, new FSMIdentifier(Name()), "{\"AbilityType\":-821166510}");
            IFSMState dash = CharacterState_AirChaosDash.ConstructInstance(fsm, new FSMIdentifier(Name()), "{\"AbilityType\":270625489}");
            Assert.That(aim.GetType(), Is.EqualTo(typeof(CharacterState_AirChaosAim)));
            Assert.That(dash.GetType(), Is.EqualTo(typeof(CharacterState_AirChaosDash)));
            PropertyInfo abilityType = typeof(ActorState_Ability).GetProperty("m_abilityType", Instance);
            Assert.That(abilityType.GetValue(aim), Is.EqualTo(ActorAbilityType.Character_AirChaosAim));
            Assert.That(abilityType.GetValue(dash), Is.EqualTo(ActorAbilityType.Character_AirChaosDash));
        }
    }
}
