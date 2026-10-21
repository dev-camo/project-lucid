using System;
using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // OnScreenTargetingButton (02000979) against its 1.10.1 x86-64 bodies (0x692160..0x6924DA). The target's homing
    // abilities are probes that answer a targeting definition of the case's ability type and count each read.
    public sealed class OnScreenTargetingButtonTests
    {
        private sealed class TargetingDefinitionProbe : CharacterAbilityDefinition_Targeting
        {
            public override Type ScriptType => typeof(ActorAbility);
        }

        private sealed class HomingAbilityProbe : ActorAbility, IHomingAbility
        {
            public CharacterAbilityDefinition_Targeting Definition;
            public int DefinitionReads;
            public override AbilityDefinition DefinitionBase => null;
            protected override void DoOnEnter() { }
            protected override void DoOnLeave() { }
            public void Reset() { }
            public bool CanActivate => false;
            public bool IsTriggered { get; set; }
            public bool OverridesTargeting => false;
            public bool ShouldProjectFromCamera() => false;
            public float GetAdditionalSpeed(float time) => 0f;
            public CharacterAbilityDefinition_Targeting TargetingDefinition
            {
                get
                {
                    DefinitionReads++;
                    return Definition;
                }
            }
        }

        private const ActorAbilityType Listed = ActorAbilityType.Character_AirStompAttack;
        private const ActorAbilityType AlsoListed = ActorAbilityType.Character_SpinDashCharge;
        private const ActorAbilityType NotListed = ActorAbilityType.Boss_AttackArea;

        private static void WithButton(Action<GameInputScope, OnScreenTargetingButton> body) => GameInputScope.Run(scope =>
        {
            var group = scope.Own(ScriptableObject.CreateInstance<CharacterAbilityTypeGroup>());
            foreach (ActorAbilityType type in new[] { Listed, AlsoListed })
            {
                var definition = new CharacterAbilityTypeGroup.AbilityTypeDefinition();
                ModernUITestSupport.Set(definition, "m_type", type); // serialized in the original
                group.Abilities.Add(definition);
            }
            var button = scope.Inactive("Lucid owned targeting button").AddComponent<OnScreenTargetingButton>();
            ModernUITestSupport.Set(button, "m_validAbilityTypeGroup", group); // serialized in the original
            body(scope, button);
        });

        private static HomingAbilityProbe Ability(GameInputScope scope, ActorAbilityType type)
        {
            var definition = scope.Own(ScriptableObject.CreateInstance<TargetingDefinitionProbe>());
            definition.AbilityType = type;
            return new HomingAbilityProbe { Definition = definition };
        }

        private static HomingTargetUpdateMessage Message(bool isValid, params HomingAbilityProbe[] abilities) => new HomingTargetUpdateMessage
        {
            IsValid = isValid,
            TargetValue = new CharacterHomingPool.PooledTarget { Abilities = new List<IHomingAbility>(abilities) },
        };

        // OnEnable 0x692160 sets IsTargetingActive to false (Bindable.Value, vtable slot 9).
        [Test]
        public void EnablingTurnsTargetingOff()
        {
            WithButton((scope, button) =>
            {
                Assert.That(button.IsTargetingActive.Value, Is.False);
                button.IsTargetingActive.Value = true;
                ModernUITestSupport.Call(button, "OnEnable");
                Assert.That(button.IsTargetingActive.Value, Is.False);
            });
        }

        // SetTargetingActive 0x692180: an invalid message turns targeting off without reading any ability
        // (the first byte of the message is tested before the list is touched). A valid one turns it on when the
        // targeting definition of any ability has an ability type of the valid group, checking every ability: the
        // result is OR-ed (0x69237D) with no early exit, so each ability is read exactly once.
        [Test]
        public void TargetingIsActiveWhenAnyAbilityOfAValidTargetIsInTheValidGroup()
        {
            WithButton((scope, button) =>
            {
                foreach ((bool valid, ActorAbilityType[] types, bool expected) in new[]
                {
                    (false, new[] { Listed }, false),
                    (true, new ActorAbilityType[0], false),
                    (true, new[] { NotListed }, false),
                    (true, new[] { Listed }, true),
                    (true, new[] { AlsoListed }, true),
                    (true, new[] { NotListed, Listed }, true),
                    (true, new[] { Listed, NotListed }, true),
                    (true, new[] { NotListed, NotListed, Listed }, true),
                    (false, new[] { Listed, AlsoListed }, false),
                })
                {
                    var abilities = new List<HomingAbilityProbe>();
                    foreach (ActorAbilityType type in types) abilities.Add(Ability(scope, type));
                    button.IsTargetingActive.Value = !expected;

                    button.SetTargetingActive(Message(valid, abilities.ToArray()));

                    string row = "valid " + valid + ", " + string.Join("/", types);
                    Assert.That(button.IsTargetingActive.Value, Is.EqualTo(expected), row);
                    Assert.That(abilities.ConvertAll(ability => ability.DefinitionReads), Is.EqualTo(abilities.ConvertAll(ability => valid ? 1 : 0)), row);
                }
            });
        }

        // The constructor 0x692440 allocates the Bindable once, defaulting to false.
        [Test]
        public void EachButtonHasItsOwnTargetingBindable()
        {
            WithButton((scope, button) =>
            {
                var other = scope.Inactive("Lucid owned second targeting button").AddComponent<OnScreenTargetingButton>();
                Assert.That(button.IsTargetingActive, Is.Not.Null);
                Assert.That(button.IsTargetingActive, Is.Not.SameAs(other.IsTargetingActive));
                Assert.That(other.IsTargetingActive.Value, Is.False);
            });
        }
    }
}
