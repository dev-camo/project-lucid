using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The homing attack definitions, the running definition and the abstract rails homing definition. Expected values
    // come from the 1.10.1 x86-64 constructors and bodies cited per test: the constructors inline every base
    // initialiser (HomingAttack 0x543230 and the leaf constructors write the same ten fields), so the immediates there
    // are the authoritative defaults.
    public sealed class CharacterHomingAttackDefinitionTests
    {
        private readonly List<Object> m_owned = new List<Object>();

        private T Create<T>() where T : ScriptableObject
        {
            var definition = ScriptableObject.CreateInstance<T>();
            m_owned.Add(definition);
            return definition;
        }

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = m_owned.Count - 1; i >= 0; i--)
                if (m_owned[i] != null) Object.DestroyImmediate(m_owned[i]);
            m_owned.Clear();
        }

        private static readonly (Type Definition, Type Ability, string Menu)[] ConcreteHomingDefinitions =
        {
            (typeof(CharacterAbilityDefinition_HomingAttackAirLightspeedDash), typeof(CharacterAbility_HomingAttackAirLightspeedDash), "HomingAttackAirLightspeedDash"),
            (typeof(CharacterAbilityDefinition_HomingAttackAirOneShot), typeof(CharacterAbility_HomingAttackAirOneShot), "HomingAttackAirOneShot"),
            (typeof(CharacterAbilityDefinition_HomingAttackAirTarget), typeof(CharacterAbility_HomingAttackAirTarget), "HomingAttackAirTarget"),
            (typeof(CharacterAbilityDefinition_HomingAttackGroundRolling), typeof(CharacterAbility_HomingAttackGroundRolling), "HomingAttackGroundRolling"),
            (typeof(CharacterAbilityDefinition_HomingAttackGroundRunning), typeof(CharacterAbility_HomingAttackGroundRunning), "HomingAttackGroundRunning"),
        };

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        private T CreateOfType<T>(Type type) where T : ScriptableObject
        {
            var definition = (T)ScriptableObject.CreateInstance(type);
            m_owned.Add(definition);
            return definition;
        }

        // 0x543230 (HomingAttack), 0x5432A0 (HomingAttackBase) and the targeting constructor, all inlined into every
        // leaf (0x5433D0, 0x5434A0, 0x543570, 0x543710, 0x5437E0): HomingVelocityMax 0x42a00000 (+0x140) and
        // ComboBufferTime 0x3e4ccccd (+0x158); HomingMask from LayerMask.op_Implicit(-1) (+0xF8) and the camera
        // projection flag 1 (+0xFC); TargetingIntervalSeconds and CooldownTimeSeconds from the 8-byte constant
        // 0x3dcccccd 0x3dcccccd (+0xCC), TimeoutSeconds 0x40400000 (+0xE4) and GravityMultiplier 0x3f800000 (+0x50).
        [Test]
        public void ConcreteHomingDefinitionsStartWithTheNativeInitialisers()
        {
            foreach ((Type type, Type _, string _) in ConcreteHomingDefinitions)
            {
                var definition = CreateOfType<CharacterAbilityDefinition_HomingAttack>(type);
                Assert.That(Bits(definition.HomingVelocityMax), Is.EqualTo(0x42a00000), type.Name + " HomingVelocityMax");
                Assert.That(Bits(definition.ComboBufferTime), Is.EqualTo(0x3e4ccccd), type.Name + " ComboBufferTime");
                Assert.That(definition.HomingMask.value, Is.EqualTo(-1), type.Name + " HomingMask");
                Assert.That(definition.ProjectFromCameraWhenNoDirectionInput, Is.True, type.Name);
                Assert.That(Bits(definition.TargetingIntervalSeconds), Is.EqualTo(0x3dcccccd), type.Name + " TargetingIntervalSeconds");
                Assert.That(Bits(definition.CooldownTimeSeconds), Is.EqualTo(0x3dcccccd), type.Name + " CooldownTimeSeconds");
                Assert.That(Bits(definition.TimeoutSeconds), Is.EqualTo(0x40400000), type.Name + " TimeoutSeconds");
                Assert.That(Bits(definition.GravityMultiplier), Is.EqualTo(0x3f800000), type.Name + " GravityMultiplier");
            }
        }

        // The constructors do not write these flags, times and zone boxes: they keep the zero of a fresh object.
        [Test]
        public void ConcreteHomingDefinitionsLeaveTheFlagsTimesAndZoneBoxesAtZero()
        {
            foreach ((Type type, Type _, string _) in ConcreteHomingDefinitions)
            {
                var definition = CreateOfType<CharacterAbilityDefinition_HomingAttack>(type);
                Assert.That(definition.ProjectFromCharacterForward, Is.False, type.Name);
                Assert.That(definition.HomingVelocityFromTime, Is.False, type.Name);
                Assert.That((definition.HomingVelocityTime, definition.DeactivationDistance), Is.EqualTo((0f, 0f)), type.Name);
                Assert.That((definition.TargetZoneSize, definition.TargetZoneSizeFromCamera), Is.EqualTo((Vector3.zero, Vector3.zero)), type.Name);
                Assert.That((definition.CharacterTargetZoneOffset, definition.CameraTargetZoneOffset), Is.EqualTo((Vector3.zero, Vector3.zero)), type.Name);
            }
            var oneShot = Create<CharacterAbilityDefinition_HomingAttackAirOneShot>();
            Assert.That((oneShot.TriggerTimeBefore, oneShot.TriggerTimeAfter), Is.EqualTo((0f, 0f)));
            var ground = Create<CharacterAbilityDefinition_HomingAttackGroundRolling>();
            Assert.That(ground.ActivationTime, Is.EqualTo(0f));
        }

        // 0x5435E0 reads the float at +0x108, the third component of TargetZoneSize (+0x100).
        [Test]
        public void TargetDistanceIsTheDepthOfTheDetectionBox()
        {
            var definition = Create<CharacterAbilityDefinition_HomingAttackAirLightspeedDash>();
            definition.TargetZoneSize = new Vector3(1f, 2f, 3f);
            Assert.That(definition.TargetDistance, Is.EqualTo(3f));
            definition.TargetZoneSize = new Vector3(3f, 2f, 1f);
            Assert.That(definition.TargetDistance, Is.EqualTo(1f), "not the width");
            definition.TargetZoneSizeFromCamera = new Vector3(9f, 9f, 9f);
            Assert.That(definition.TargetDistance, Is.EqualTo(1f), "not the camera box");
        }

        // 0x5431E0: Array.IndexOf over the authored list, then setns: any index from zero up is a hit.
        [Test]
        public void HasTargetTypeSearchesTheAuthoredList()
        {
            var definition = Create<CharacterAbilityDefinition_HomingAttackAirLightspeedDash>();
            definition.TargetTypes = new[] { HomingTargetType.Enemy, HomingTargetType.Ring };
            Assert.That(definition.HasTargetType(HomingTargetType.Enemy), Is.True, "the first element is index zero");
            Assert.That(definition.HasTargetType(HomingTargetType.Ring), Is.True);
            Assert.That(definition.HasTargetType(HomingTargetType.Rail), Is.False);
            definition.TargetTypes = new HomingTargetType[0];
            Assert.That(definition.HasTargetType(HomingTargetType.Enemy), Is.False, "an empty list");
        }

        [Test]
        public void HasTargetTypeFaultsOnAnUnassignedList()
        {
            var definition = Create<CharacterAbilityDefinition_HomingAttackAirLightspeedDash>();
            definition.TargetTypes = null;
            Assert.Throws<ArgumentNullException>(() => definition.HasTargetType(HomingTargetType.Enemy));
        }

        // 0x543370, 0x543440, 0x543510, 0x5436B0, 0x5437E0 and 0x5451A0 load the Il2CppType of the ability script.
        [Test]
        public void DefinitionsNameTheirOwnAbilityScript()
        {
            foreach ((Type type, Type ability, string _) in ConcreteHomingDefinitions)
                Assert.That(CreateOfType<AbilityDefinition>(type).ScriptType, Is.EqualTo(ability), type.Name);
            Assert.That(Create<CharacterAbilityDefinition_Running>().ScriptType, Is.EqualTo(typeof(CharacterAbility_Running)));
        }

        [Test]
        public void DefinitionsAreCreatableFromTheAssetMenu()
        {
            var all = ConcreteHomingDefinitions.Select(d => (d.Definition, d.Menu)).Append((typeof(CharacterAbilityDefinition_Running), "Running"));
            foreach ((Type type, string menu) in all)
            {
                CreateAssetMenuAttribute attribute = type.GetCustomAttribute<CreateAssetMenuAttribute>(false);
                Assert.That(attribute.fileName, Is.EqualTo(type.Name), type.Name);
                Assert.That(attribute.menuName, Is.EqualTo("HardlightProject/DefinitionData/AbilityDef/" + menu), type.Name);
                Assert.That(attribute.order, Is.EqualTo(1), type.Name);
            }
        }

        // 0x543620 hands out the authored boost definition and reports true, even while it is unassigned; the base
        // virtual (checked on the running definition, which does not override it) reports false.
        [Test]
        public void GroundHomingDefinitionsAlwaysReportTheirBoostDefinition()
        {
            foreach (Type type in new[] { typeof(CharacterAbilityDefinition_HomingAttackGroundRolling), typeof(CharacterAbilityDefinition_HomingAttackGroundRunning) })
            {
                var definition = CreateOfType<CharacterAbilityDefinition_HomingAttackGround>(type);
                definition.BoostDefinition = null;
                Assert.That(definition.TryGetBoostDefinition(out CharacterStamina.Definition reported), Is.True, type.Name);
                Assert.That(reported, Is.Null);
                definition.BoostDefinition = new CharacterStamina.Definition();
                Assert.That(definition.TryGetBoostDefinition(out reported), Is.True, type.Name);
                Assert.That(reported, Is.SameAs(definition.BoostDefinition));
            }
            Assert.That(Create<CharacterAbilityDefinition_Running>().TryGetBoostDefinition(out CharacterStamina.Definition none), Is.False);
            Assert.That(none, Is.Null);
        }

        // 0x544B90: the 8-byte constant 0x43340000 0x3f800000 initialises HomingRotationalVelocity (+0xF8) to 180 and
        // HomingDistance (+0xFC) to 1; the targeting values (0x29a7720 constant, 3 and 1) are inlined as well.
        [Test]
        public void RailsHomingDefinitionStartsAtOneEightyDegreesAndOneUnit()
        {
            var definition = Create<RailsHomingDefinitionProbe>();
            Assert.That(Bits(definition.HomingRotationalVelocity), Is.EqualTo(0x43340000));
            Assert.That(Bits(definition.HomingDistance), Is.EqualTo(0x3f800000));
            Assert.That(Bits(definition.TargetingIntervalSeconds), Is.EqualTo(0x3dcccccd));
            Assert.That(Bits(definition.CooldownTimeSeconds), Is.EqualTo(0x3dcccccd));
            Assert.That(Bits(definition.TimeoutSeconds), Is.EqualTo(0x40400000));
            Assert.That(Bits(definition.GravityMultiplier), Is.EqualTo(0x3f800000));
        }

        // 0x544A90: the abstract definition's own refresh squares the distance (+0xFC into +0x108) after the inlined
        // targeting refresh, whose cosines use the 0.0174533 degree factor and (90 - pitch).
        [Test]
        public void RailsHomingCalculateCachedValuesSquaresTheDistanceAndRefreshesTheTargetingCache()
        {
            var definition = Create<RailsHomingDefinitionProbe>();
            definition.InputDiscardTargetAngle = 60f;
            definition.TargetMaxPitchAngle = 30f;
            definition.TargetInnerRadius = 2f;
            definition.HomingDistance = 3f;
            definition.CalculateCachedValues();
            Assert.That(definition.HomingDistanceSqr, Is.EqualTo(9f));
            Assert.That(definition.TargetInnerRadiusSqr, Is.EqualTo(4f));
            Assert.That(definition.InputDiscardTargetCosine, Is.EqualTo(0.5f).Within(1e-5f), "cos(60 degrees)");
            Assert.That(definition.TargetMaxPitchCosine, Is.EqualTo(0.5f).Within(1e-5f), "cos(90 - 30 degrees), not cos(30 degrees)");
        }

        // The attribute order of each authored field is the original metadata order.
        [Test]
        public void FieldsKeepTheirAuthoringAttributesInOrder()
        {
            Type[] Attributes(Type owner, string name) => AttributeTypes(Field(owner, name));
            Assert.That(Attributes(typeof(CharacterAbilityDefinition_HomingAttackBase), "TargetZoneSizeFromCamera"),
                Is.EqualTo(new[] { typeof(ShowIfAttribute), typeof(TooltipAttribute) }));
            Assert.That(Attributes(typeof(CharacterAbilityDefinition_HomingAttackBase), "CameraTargetZoneOffset"),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(ShowIfAttribute) }));
            Assert.That(Attributes(typeof(CharacterAbilityDefinition_HomingAttack), "HomingVelocityMax"),
                Is.EqualTo(new[] { typeof(MinAttribute), typeof(TooltipAttribute) }));
            Assert.That(Attributes(typeof(CharacterAbilityDefinition_RailsHoming), "HomingRotationalVelocity"),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(UnityEngine.RangeAttribute) }));
            Assert.That(Attributes(typeof(CharacterAbilityDefinition_RailsHoming), "HomingDistance"),
                Is.EqualTo(new[] { typeof(MinAttribute), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(Field(typeof(CharacterAbilityDefinition_HomingAttack), "DeactivationDistance")), Is.EqualTo("Distance from target to deactivate homing."));
            Assert.That(Tooltip(Field(typeof(CharacterAbilityDefinition_HomingAttackAirOneShot), "TriggerTimeAfter")),
                Is.EqualTo("Period of time to allow a queued activation after being enabled."));
            UnityEngine.RangeAttribute range = Field(typeof(CharacterAbilityDefinition_RailsHoming), "HomingRotationalVelocity").GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.That((range.min, range.max), Is.EqualTo((0f, 360f)));
        }
    }
}
