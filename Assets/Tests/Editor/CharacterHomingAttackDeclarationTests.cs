using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The declaration shape of the homing attack, running, rolling and setting types: type flags, base, implemented
    // interfaces, attribute order, and every declared field and method with its flags in metadata order. The expected
    // rows are the original DummyDll ECMA-335 tables (this is declaration shape, not behaviour). The compiler-generated
    // lambda methods and auto-property backing fields appear in the rows because they are part of the original shape.
    public sealed class CharacterHomingAttackDeclarationTests
    {
        private static Type Nested(Type owner, string name) => owner.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);

        // Serializable is TypeDef flag 0x2000 (checked with the type flags), not a CustomAttribute row, but
        // Mono's reflection reports it among the attributes.
        private static string[] AttributeNames(Type type) => type.GetCustomAttributesData()
            .Where(data => data.AttributeType != typeof(SerializableAttribute))
            .Select(data => data.AttributeType.Name == "Il2CppSetOptionAttribute"
                ? "Il2CppSetOption:" + Convert.ToInt32(data.ConstructorArguments[0].Value)
                : data.AttributeType.Name.Replace("Attribute", "")).ToArray();

        private static void AssertShape(Type type, int typeFlags, Type baseType, string[] interfaces, string[] attributes,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            Type actualBase = type.BaseType.IsGenericType ? type.BaseType.GetGenericTypeDefinition() : type.BaseType;
            Assert.That(actualBase, Is.EqualTo(baseType), type.FullName + " base");
            string[] declared = type.GetInterfaces().Select(i => i.Name.Split('`')[0]).ToArray();
            Assert.That(declared, Is.SupersetOf(interfaces), type.FullName + " interfaces");
            Assert.That(AttributeNames(type), Is.EqualTo(attributes), type.FullName + " attributes");
        }

        [Test]
        public void DefinitionsKeepTheOriginalShape()
        {
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttackBase), 0x00100081, typeof(CharacterAbilityDefinition_Targeting),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("HomingMask", 0x0006), ("ProjectFromCameraWhenNoDirectionInput", 0x0006), ("ProjectFromCharacterForward", 0x0006), ("TargetZoneSize", 0x0006), ("TargetZoneSizeFromCamera", 0x0006), ("CharacterTargetZoneOffset", 0x0006), ("CameraTargetZoneOffset", 0x0006), ("HomingAttackPrefab", 0x0006) },
                new[] { ("get_TargetDistance", 0x0886), (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttack), 0x00100081, typeof(CharacterAbilityDefinition_HomingAttackBase),
                new string[0],
                new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" },
                new[] { ("TargetTypes", 0x0006), ("HomingVelocityMax", 0x0006), ("HomingAdditionalSpeed", 0x0006), ("HomingVelocityFromTime", 0x0006), ("HomingVelocityTime", 0x0006), ("ComboBufferTime", 0x0006), ("ComboTimeSettings", 0x0006), ("DeactivationDistance", 0x0006) },
                new[] { ("HasTargetType", 0x0086), (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttackAir), 0x00100081, typeof(CharacterAbilityDefinition_HomingAttack),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new (string, int)[0],
                new[] { (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttackAirLightspeedDash), 0x00100001, typeof(CharacterAbilityDefinition_HomingAttackAir),
                new string[0],
                new[] { "CreateAssetMenu", "Il2CppSetOption:2", "Il2CppSetOption:1" },
                new (string, int)[0],
                new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttackAirOneShot), 0x00100001, typeof(CharacterAbilityDefinition_HomingAttackAir),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "CreateAssetMenu" },
                new[] { ("TriggerTimeBefore", 0x0006), ("TriggerTimeAfter", 0x0006) },
                new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttackAirTarget), 0x00100001, typeof(CharacterAbilityDefinition_HomingAttackAir),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "CreateAssetMenu" },
                new (string, int)[0],
                new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttackGround), 0x00100081, typeof(CharacterAbilityDefinition_HomingAttack),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("ActivationTime", 0x0006), ("ForwardMotion", 0x0006), ("BoostDefinition", 0x0006) },
                new[] { ("TryGetBoostDefinition", 0x00c6), (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttackGroundRolling), 0x00100001, typeof(CharacterAbilityDefinition_HomingAttackGround),
                new string[0],
                new[] { "Il2CppSetOption:2", "CreateAssetMenu", "Il2CppSetOption:1" },
                new (string, int)[0],
                new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbilityDefinition_HomingAttackGroundRunning), 0x00100001, typeof(CharacterAbilityDefinition_HomingAttackGround),
                new string[0],
                new[] { "CreateAssetMenu", "Il2CppSetOption:2", "Il2CppSetOption:1" },
                new (string, int)[0],
                new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbilityDefinition_RailsHoming), 0x00100081, typeof(CharacterAbilityDefinition_Targeting),
                new string[0],
                new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" },
                new[] { ("HomingRotationalVelocity", 0x0006), ("HomingDistance", 0x0006), ("HomingAdditionalSpeed", 0x0006), ("<HomingDistanceSqr>k__BackingField", 0x0001) },
                new[] { ("get_HomingDistanceSqr", 0x0886), ("set_HomingDistanceSqr", 0x0881), ("CalculateCachedValues", 0x00c6), (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbilityDefinition_Running), 0x00100001, typeof(CharacterAbilityDefinition_MovementGround),
                new string[0],
                new[] { "Il2CppSetOption:1", "CreateAssetMenu", "Il2CppSetOption:2" },
                new (string, int)[0],
                new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
        }

        [Test]
        public void AbilitiesKeepTheOriginalShape()
        {
            AssertShape(typeof(CharacterAbility_TargetingHoming<>), 0x00100081, typeof(CharacterAbility_Targeting<>),
                new[] { "IHomingAbility", "IAbility", "IAbilityTrigger" },
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("m_cameraManagerRef", 0x0021), ("<IsTriggered>k__BackingField", 0x0001), ("<AdditionalSpeedCurve>k__BackingField", 0x0021) },
                new[] { ("get_IsTriggered", 0x09e6), ("set_IsTriggered", 0x09e6), ("get_ProjectFromCameraWhenNoDirectionInput", 0x09c4), ("get_OverridesTargeting", 0x09c6), ("ShouldProjectFromCamera", 0x01e6), ("get_TargetingDefinition", 0x09e6), ("get_TargetingIntervalSeconds", 0x08c4), ("get_CooldownTimeSeconds", 0x08c4), ("get_UseTimeScaling", 0x08c4), ("get_AdditionalSpeedCurve", 0x09c4), ("TrySetTargetImmediately", 0x0084), ("DoOnEnter", 0x00c4), ("DoOnLeave", 0x00c4), ("Reset", 0x01c6), ("GetAdditionalSpeed", 0x01c6), ("get_CanActivate", 0x09e6), (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbility_HomingAttackBase<>), 0x00100081, typeof(CharacterAbility_TargetingHoming<>),
                new[] { "IHomingAttack" },
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("m_homingAttack", 0x0001), ("m_rigidbody", 0x0001), ("m_targetZoneSizeAdjustment", 0x0001), ("m_homingAttackObjectEnable", 0x0001), ("m_homingAttackObjectDisable", 0x0001) },
                new[] { ("get_TargetZoneSize", 0x09e6), ("get_TargetZoneOffset", 0x09e6), ("get_ProjectFromCameraWhenNoDirectionInput", 0x08c4), ("Initialise", 0x00c6), ("Close", 0x00c6), ("DoOnEnter", 0x00c4), ("DoOnLeave", 0x00c4), ("AddTarget", 0x01c6), ("RemoveTarget", 0x01c6), ("OnHomingTargetHit", 0x01c6), ("IsHomingTargetValid", 0x01c6), ("OnTriggerEnter", 0x01e6), ("OnTriggerExit", 0x01e6), ("DoOnUpdate", 0x00c4), ("HandleAttack", 0x05c4), ("EnableHomingTarget", 0x0084), ("ActivateHomingAttackObject", 0x0081), ("UpdateHomingTargetLocation", 0x01c4), ("SetHomingTargetTransform", 0x0084), ("ValidateTargetType", 0x05c4), (".ctor", 0x1884), ("<Initialise>b__11_0", 0x0081), ("<Initialise>b__11_1", 0x0081) });
            AssertShape(typeof(CharacterAbility_HomingAttack<>), 0x00100081, typeof(CharacterAbility_HomingAttackBase<>),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("m_comboCount", 0x0001), ("m_comboTimer", 0x0001), ("m_comboTimeOverride", 0x0001) },
                new[] { ("get_AdditionalSpeedCurve", 0x08c4), ("SetActive", 0x01c6), ("UpdateTarget", 0x01c6), ("ClearTarget", 0x0081), ("OnHomingTargetHit", 0x00c6), ("RemoveTarget", 0x00c6), ("DoOnLeave", 0x00c4), ("HomingAttackActivated", 0x01c4), ("HandleAttack", 0x00c4), ("AddComboHit", 0x0081), ("UpdateComboHits", 0x0081), ("ClearComboHits", 0x0081), ("ValidateTargetType", 0x00c4), ("GetDebugExtraInfo", 0x00c4), (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbility_HomingAttackAir<>), 0x00100081, typeof(CharacterAbility_HomingAttack<>),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new (string, int)[0],
                new[] { (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbility_HomingAttackAirLightspeedDash), 0x00100001, typeof(CharacterAbility_HomingAttackAir<>),
                new string[0],
                new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" },
                new (string, int)[0],
                new[] { ("DoOnEnter", 0x00c4), ("HomingAttackActivated", 0x00c4), ("HandleAttack", 0x00c4), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbility_HomingAttackAirOneShot), 0x00100001, typeof(CharacterAbility_HomingAttackAir<>),
                new[] { "IAbilityOneShot" },
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new (string, int)[0],
                new[] { ("AddTarget", 0x00c6), ("RemoveTarget", 0x00c6), ("ActionOnCharacterHit", 0x0081), ("UpdateHomingTargetLocation", 0x00c4), ("HomingAttackActivated", 0x00c4), ("get_OverridesTargeting", 0x08c6), ("HandleAttack", 0x00c4), ("CanTriggerBefore", 0x01e6), ("CanTriggerAfter", 0x01e6), ("SetActive", 0x00c6), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbility_HomingAttackAirTarget), 0x00100001, typeof(CharacterAbility_HomingAttackAir<>),
                new string[0],
                new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" },
                new (string, int)[0],
                new[] { ("HomingAttackActivated", 0x00c4), ("HandleAttack", 0x00c4), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbility_HomingAttackGround), 0x00100081, typeof(CharacterAbility_HomingAttack<>),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new (string, int)[0],
                new[] { ("DoOnUpdate", 0x00c4), ("ValidateTargetType", 0x00c4), ("UpdateForwardSpeed", 0x0086), (".ctor", 0x1884) });
            AssertShape(typeof(CharacterAbility_HomingAttackGroundRolling), 0x00100001, typeof(CharacterAbility_HomingAttackGround),
                new string[0],
                new[] { "Il2CppSetOption:2", "Il2CppSetOption:1" },
                new (string, int)[0],
                new[] { ("HomingAttackActivated", 0x00c4), ("HandleAttack", 0x00c4), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbility_HomingAttackGroundRunning), 0x00100001, typeof(CharacterAbility_HomingAttackGround),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new (string, int)[0],
                new[] { ("HomingAttackActivated", 0x00c4), ("HandleAttack", 0x00c4), ("ValidateTargetType", 0x00c4), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbility_Running), 0x00100001, typeof(CharacterAbility_MovementGround<>),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new (string, int)[0],
                new[] { (".ctor", 0x1886) });
        }

        [Test]
        public void NodesKeepTheOriginalShape()
        {
            AssertShape(typeof(CharacterState_GroundRunning), 0x00100001, typeof(CharacterState_Ground<,>),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "GraphNodeDefaultName", "GraphNodeMenuFormat" },
                new (string, int)[0],
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096) });
            AssertShape(typeof(CharacterTransition_Rolling), 0x00100001, typeof(Transition_Ability),
                new string[0],
                new[] { "GraphNodeMenuFormat", "GraphNodeDefaultName", "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("m_inverseCheck", 0x0021), ("m_resetVelocity", 0x0021) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4) });
            AssertShape(Nested(typeof(CharacterTransition_Rolling), "JSONCtorArgs"), 0x00100004, Nested(typeof(Transition_Ability), "AbilityJSONCtorArgs"),
                new string[0],
                new string[0],
                new[] { ("Inverse", 0x0006), ("ResetVelocity", 0x0006) },
                new[] { (".ctor", 0x1886) });
            AssertShape(typeof(CharacterTransition_RollingToRunning), 0x00100001, typeof(Transition_Ability),
                new string[0],
                new[] { "GraphNodeDefaultName", "Il2CppSetOption:2", "Il2CppSetOption:1", "GraphNodeMenuFormat" },
                new[] { ("m_inverseCheck", 0x0001) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) });
            AssertShape(Nested(typeof(CharacterTransition_RollingToRunning), "JSONCtorArgs"), 0x00100004, Nested(typeof(Transition_Ability), "AbilityJSONCtorArgs"),
                new string[0],
                new string[0],
                new[] { ("Inverse", 0x0006) },
                new[] { (".ctor", 0x1886) });
            AssertShape(typeof(CharacterTransition_Setting), 0x00100081, typeof(FSMTransition),
                new string[0],
                new[] { "Il2CppSetOption:2", "Il2CppSetOption:1", "GraphNodeMenuFormat" },
                new[] { ("m_buttonMapping", 0x0021) },
                new[] { (".ctor", 0x1884), ("DoUpdate", 0x00c4), ("GetButtonMapping", 0x05c4) });
            AssertShape(Nested(typeof(CharacterTransition_Setting), "JSONCtorArgs"), 0x00102004, typeof(object),
                new string[0],
                new[] { "Il2CppSetOption:2", "FiniteStateMachineJsonCtorArgs", "Il2CppSetOption:1" },
                new[] { ("ButtonMapping", 0x0006) },
                new[] { (".ctor", 0x1886) });
            AssertShape(typeof(CharacterTransition_SettingHomingAttack), 0x00100001, typeof(CharacterTransition_Setting),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "GraphNodeDefaultName", "GraphNodeMenuFormat" },
                new (string, int)[0],
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("GetButtonMapping", 0x00c4) });
        }
    }
}
