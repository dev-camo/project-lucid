using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared setup for the character ability fixtures. A Character is a real component on an owned, inactive
    // host (no Awake runs in Edit Mode) with a real FSM storage, collider settings, the genuine
    // CharacterCollider and a supplied gravity vector; an ability is built by its real constructor and given
    // the character and definition the way ActorAbility.Initialise would, which needs a started character.
    // The fixed pass is marked active so the character's velocity is the one stored on the Actor, as the
    // body-less Edit Mode character cannot read a rigid body.
    internal static class CharacterAbilityTestSupport
    {
        internal const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal const BindingFlags DeclaredMembers = Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Character's field initialiser reads the GameplayIslandManager row and CharacterAbility's creates the
        // App reference row, so both are admitted absent and released while still idle.
        internal static void WithAbility<TAbility, TDefinition>(Action<Owned, CharacterGravityVectorProbe, TAbility, TDefinition> body)
            where TAbility : CharacterAbility, new()
            where TDefinition : CharacterAbilityDefinition => WithAbility(_ => { }, body);

        // leaseAbilityRows leases the further rows the ability class's own field initialisers create; it runs
        // inside the cleanup scope before the ability is constructed.
        internal static void WithAbility<TAbility, TDefinition>(Action<Owned> leaseAbilityRows, Action<Owned, CharacterGravityVectorProbe, TAbility, TDefinition> body)
            where TAbility : CharacterAbility, new()
            where TDefinition : CharacterAbilityDefinition => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            leaseAbilityRows(owned);
            CharacterGravityVectorProbe character = NewCharacter(owned);
            var definition = owned.Track(ScriptableObject.CreateInstance<TDefinition>());
            var ability = new TAbility();
            Set(ability, "m_character", character);
            Set(ability, "m_definition", definition);
            body(owned, character, ability, definition);
        });

        // The GameplayIslandManager row must be leased by the caller.
        internal static CharacterGravityVectorProbe NewCharacter(Owned owned)
        {
            GameObject host = owned.Host("Lucid ability character", false);
            host.AddComponent<BoxCollider>();
            var character = host.AddComponent<CharacterGravityVectorProbe>();
            character.GravityVector = new Vector3(0f, -20f, 0f);
            SetActorProperty(character, "Storage", new FSMStorage(0));
            typeof(Character).GetMethod("InitialiseModifiers", Instance).Invoke(character, null);
            SetProperty(character, "Settings", new CharacterSettings { Collider = new CharacterSettings.ColliderSettings() });
            SetProperty(character, "Collider", new CharacterCollider(character, null, null));
            SetActorProperty(character, "FixedUpdateActive", true);
            SetActorProperty(character, "WorldRotation", Quaternion.identity);
            SetProperty(character, "SpinDashRoll", new CharacterSpinDashRoll(character));
            return character;
        }

        // Private setters are taken from the declaring type.
        internal static void SetProperty(object target, string property, object value)
        {
            Type owner = target.GetType().GetProperty(property, Instance).DeclaringType;
            owner.GetProperty(property, Instance | BindingFlags.DeclaredOnly).GetSetMethod(true).Invoke(target, new[] { value });
        }

        // The call targets of a declared method body in linear IL order.
        internal static List<MethodBase> Calls(Type owner, string name)
        {
            MethodBase method = owner.GetMethods(DeclaredMembers).Single(m => m.Name == name);
            return OriginalILCallSequence.Calls(method).Select(call => call.Target).ToList();
        }

        // The first call to the exact declaring type; a generic type definition is named without its arity suffix.
        internal static int IndexOf(List<MethodBase> calls, string typeName, string name, int from = 0)
        {
            int index = calls.FindIndex(from, m => (m.DeclaringType.Name == typeName || m.DeclaringType.Name.StartsWith(typeName + "`", StringComparison.Ordinal)) && m.Name == name);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), typeName + "." + name + " is called");
            return index;
        }

        internal static int Count(List<MethodBase> calls, string typeName, string name) =>
            calls.Count(m => (m.DeclaringType.Name == typeName || m.DeclaringType.Name.StartsWith(typeName + "`", StringComparison.Ordinal)) && m.Name == name);

        // A brain with no input of its own, for abilities whose update reads the queued actions.
        internal sealed class RailsBrain : CharacterBrain
        {
            public override void Close()
            {
            }

            // Queues the action without applying it, as a press between two brain updates does.
            public void Press(GameAction action) => SetState(action, true);
        }

        // Keeps the targeting interval from firing, so only the refresh request that the ability itself makes is seen.
        internal static void SuppressTargetingRefresh(object ability) => Set(ability, "m_nextTargetIntervalTime", 100f);

        internal static bool RefreshRequested(object ability) => Get<bool>(ability, "m_targetRequiresRefresh");

        // Serializable is a TypeDef flag, not a CustomAttribute row, but Mono's reflection reports it among the attributes.
        internal static string[] AttributeNames(Type type) => type.GetCustomAttributesData()
            .Where(data => data.AttributeType != typeof(SerializableAttribute))
            .Select(data => data.AttributeType.Name == "Il2CppSetOptionAttribute"
                ? "Il2CppSetOption:" + Convert.ToInt32(data.ConstructorArguments[0].Value)
                : data.AttributeType.Name.Replace("Attribute", "")).ToArray();

        internal static void WithInvariantCulture(Action body)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                body();
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }
    }
}
