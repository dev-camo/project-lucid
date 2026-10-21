using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The rails proximity (air, ground) and rails targeting ability definitions, 1.10.1 x86-64. The constructors inline
    // every base initialiser, so the packed immediates there are authoritative: 0x29a77b0 (80, 1) for the proximity air
    // pair, 0x29a77c0 (1, 50) with the packed pair 0x41f00000 0x41f00000 for the targeting leaf, and the rails homing
    // defaults 0x29a77a0 (180, 1), the targeting pair 0x29a7720 (0.1, 0.1), 3.0f at +0xE4 and 1.0f at +0x50 common to all
    // three. The cached values are read at 0x544C50 (air) and 0x544E60 (targeting) from the cosf and tanf calls.
    public sealed class CharacterRailsHomingDefinitionTests
    {
        private const float Tolerance = 1e-6f;

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        // The rails homing and targeting defaults that every leaf definition constructor writes.
        private static void AssertInheritedDefaults(CharacterAbilityDefinition_RailsHoming definition, string name)
        {
            Assert.That(Bits(definition.HomingRotationalVelocity), Is.EqualTo(0x43340000), name + " HomingRotationalVelocity");
            Assert.That(Bits(definition.HomingDistance), Is.EqualTo(0x3f800000), name + " HomingDistance");
            Assert.That(Bits(definition.TargetingIntervalSeconds), Is.EqualTo(0x3dcccccd), name + " TargetingIntervalSeconds");
            Assert.That(Bits(definition.CooldownTimeSeconds), Is.EqualTo(0x3dcccccd), name + " CooldownTimeSeconds");
            Assert.That(Bits(definition.TimeoutSeconds), Is.EqualTo(0x40400000), name + " TimeoutSeconds");
            Assert.That(Bits(definition.GravityMultiplier), Is.EqualTo(0x3f800000), name + " GravityMultiplier");
        }

        // 0x544CF0 stores the packed pair 0x29a77b0 (80, 1) over the two fields at +0x10C and +0x110; the cached cosine
        // at +0x114 is only written by CalculateCachedValues.
        [Test]
        public void RailsProximityAirDefinitionStartsWithTheNativeDefaults()
        {
            Owned.Run(owned =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsProximityAir>());
                Assert.That(Bits(definition.SurfaceNormalAngleTolerance), Is.EqualTo(0x42a00000));
                Assert.That(Bits(definition.RailTrackerAttachDistance), Is.EqualTo(0x3f800000));
                Assert.That(definition.SurfaceNormalAngleCosineTolerance, Is.EqualTo(0f));
                AssertInheritedDefaults(definition, nameof(CharacterAbilityDefinition_RailsProximityAir));
            });
        }

        // 0x544DA0 writes only the inherited rails homing defaults.
        [Test]
        public void RailsProximityGroundDefinitionWritesOnlyTheInheritedDefaults()
        {
            Owned.Run(owned =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsProximityGround>());
                AssertInheritedDefaults(definition, nameof(CharacterAbilityDefinition_RailsProximityGround));
                Assert.That(definition.HomingDistanceSqr, Is.EqualTo(0f), "cached values are only set by CalculateCachedValues");
            });
        }

        // 0x544F30: 0x41f0000041f00000 over the cone angle pair at +0x10C, 0x29a77c0 (1, 50) over the step and velocity at
        // +0x114, nothing for the camera flag at +0x11C and the cached tangents at +0x120.
        [Test]
        public void RailsTargetingDefinitionStartsWithTheNativeDefaults()
        {
            Owned.Run(owned =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsTargeting>());
                Assert.That((Bits(definition.HomingProjectConeAngle.x), Bits(definition.HomingProjectConeAngle.y)), Is.EqualTo((0x41f00000, 0x41f00000)));
                Assert.That(Bits(definition.HomingProjectDistanceStep), Is.EqualTo(0x3f800000));
                Assert.That(Bits(definition.HomingVelocity), Is.EqualTo(0x42480000));
                Assert.That(definition.ProjectFromCameraWhenNoDirectionInput, Is.False);
                Assert.That(definition.HomingProjectConeTangents, Is.EqualTo(Vector2.zero));
                AssertInheritedDefaults(definition, nameof(CharacterAbilityDefinition_RailsTargeting));
            });
        }

        // 0x544C70..0x544CDF: cosf of Deg2Rad (0x29a6c18) times the tolerance, stored at +0x114.
        [Test]
        public void RailsProximityAirCachesTheCosineOfTheSurfaceNormalTolerance()
        {
            Owned.Run(owned =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsProximityAir>());
                foreach ((float angle, float cosine) in new[] { (0f, 1f), (60f, 0.5f), (80f, 0.17364818f), (90f, 0f), (180f, -1f) })
                {
                    definition.SurfaceNormalAngleTolerance = angle;
                    definition.CalculateCachedValues();
                    Assert.That(definition.SurfaceNormalAngleCosineTolerance, Is.EqualTo(cosine).Within(Tolerance), "angle " + angle);
                }
            });
        }

        // The air leaf inlines the whole targeting and rails homing refresh (0x544C5B..0x544CC6): the discard cosine of the
        // input angle, the pitch cosine of the complement of the maximum pitch (0x29a7354 is 90), the squared inner radius
        // and the squared homing distance.
        [Test]
        public void RailsLeafDefinitionsRefreshTheInheritedCachesWhenCached()
        {
            Owned.Run(owned =>
            {
                foreach (CharacterAbilityDefinition_RailsHoming definition in new CharacterAbilityDefinition_RailsHoming[]
                {
                    owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsProximityAir>()),
                    owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsProximityGround>()),
                    owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsTargeting>()),
                })
                {
                    definition.InputDiscardTargetAngle = 60f;
                    definition.TargetMaxPitchAngle = 30f;
                    definition.TargetInnerRadius = 4f;
                    definition.HomingDistance = 3f;
                    definition.CalculateCachedValues();
                    string name = definition.GetType().Name;
                    Assert.That(definition.InputDiscardTargetCosine, Is.EqualTo(0.5f).Within(Tolerance), name + " discard cosine");
                    Assert.That(definition.TargetMaxPitchCosine, Is.EqualTo(0.5f).Within(Tolerance), name + " pitch cosine of the complement");
                    Assert.That(definition.TargetInnerRadiusSqr, Is.EqualTo(16f), name);
                    Assert.That(definition.HomingDistanceSqr, Is.EqualTo(9f), name);
                }
            });
        }

        // 0x544EDD..0x544F15: tanf of Deg2Rad times each angle, the x angle first, stored as the x and y of the pair at
        // +0x120.
        [Test]
        public void RailsTargetingCachesTheTangentOfEachConeAngleOnItsOwnAxis()
        {
            Owned.Run(owned =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailsTargeting>());
                foreach ((float x, float y, float tangentX, float tangentY) in new[]
                {
                    (30f, 30f, 0.57735026f, 0.57735026f), (45f, 60f, 1f, 1.7320508f), (0f, 10f, 0f, 0.17632698f), (60f, 45f, 1.7320508f, 1f),
                })
                {
                    definition.HomingProjectConeAngle = new Vector2(x, y);
                    definition.CalculateCachedValues();
                    Assert.That(definition.HomingProjectConeTangents.x, Is.EqualTo(tangentX).Within(Tolerance), "x of " + x + ", " + y);
                    Assert.That(definition.HomingProjectConeTangents.y, Is.EqualTo(tangentY).Within(Tolerance), "y of " + x + ", " + y);
                }
            });
        }

        // 0x544BD0, 0x544D40 and 0x544DE0 load the runtime type of their ability.
        [Test]
        public void DefinitionsNameTheirAbilityScripts()
        {
            Owned.Run(owned =>
            {
                foreach ((Type definition, Type ability) in new[]
                {
                    (typeof(CharacterAbilityDefinition_RailsProximityAir), typeof(CharacterAbility_RailsProximityAir)),
                    (typeof(CharacterAbilityDefinition_RailsProximityGround), typeof(CharacterAbility_RailsProximityGround)),
                    (typeof(CharacterAbilityDefinition_RailsTargeting), typeof(CharacterAbility_RailsTargeting)),
                })
                {
                    var instance = (AbilityDefinition)owned.Track(ScriptableObject.CreateInstance(definition));
                    Assert.That(instance.ScriptType, Is.EqualTo(ability), definition.Name);
                }
            });
        }

        private static (float min, float max) Range(FieldInfo field)
        {
            UnityEngine.RangeAttribute range = field.GetCustomAttributes(typeof(UnityEngine.RangeAttribute), false).Cast<UnityEngine.RangeAttribute>().Single();
            return (range.min, range.max);
        }

        private static float Min(FieldInfo field) => field.GetCustomAttributes(typeof(MinAttribute), false).Cast<MinAttribute>().Single().min;

        // The authored attributes of the DummyDll field rows, in metadata order.
        [Test]
        public void AuthoredFieldsKeepTheirRangesMinimaAndTooltips()
        {
            FieldInfo tolerance = typeof(CharacterAbilityDefinition_RailsProximityAir).GetField("SurfaceNormalAngleTolerance");
            FieldInfo attach = typeof(CharacterAbilityDefinition_RailsProximityAir).GetField("RailTrackerAttachDistance");
            Assert.That(Range(tolerance), Is.EqualTo((0f, 360f)));
            Assert.That(Tooltip(tolerance), Is.EqualTo("Angle of the rail surface normal to character velocity for proximity attachment."));
            Assert.That(AttributeTypes(tolerance), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(UnityEngine.RangeAttribute) }));
            Assert.That(Min(attach), Is.EqualTo(0f));
            Assert.That(Tooltip(attach), Is.EqualTo("Rail attach distance when tracking, e.g. from a jump."));
            Assert.That(AttributeTypes(attach), Is.EqualTo(new[] { typeof(MinAttribute), typeof(TooltipAttribute) }));

            FieldInfo cone = typeof(CharacterAbilityDefinition_RailsTargeting).GetField("HomingProjectConeAngle");
            FieldInfo step = typeof(CharacterAbilityDefinition_RailsTargeting).GetField("HomingProjectDistanceStep");
            FieldInfo velocity = typeof(CharacterAbilityDefinition_RailsTargeting).GetField("HomingVelocity");
            FieldInfo camera = typeof(CharacterAbilityDefinition_RailsTargeting).GetField("ProjectFromCameraWhenNoDirectionInput");
            Assert.That(Tooltip(cone), Is.EqualTo("The cone ellipse angles."));
            Assert.That(Min(cone), Is.EqualTo(0f));
            Assert.That(AttributeTypes(cone), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(MinAttribute) }));
            Assert.That(Tooltip(step), Is.EqualTo("The distance to step when projecting through the world to find a target."));
            Assert.That(Min(step), Is.EqualTo(0.1f));
            Assert.That(Tooltip(velocity), Is.EqualTo("Velocity when homing to a target."));
            Assert.That(Min(velocity), Is.EqualTo(0f));
            Assert.That(Tooltip(camera), Is.EqualTo("If disabled, collider will always project from character position."));
            Assert.That(AttributeTypes(camera), Is.EqualTo(new[] { typeof(TooltipAttribute) }));
        }

        private static void AssertAssetMenu(Type type, string name)
        {
            CustomAttributeData menu = type.GetCustomAttributesData().Single(data => data.AttributeType == typeof(CreateAssetMenuAttribute));
            Assert.That(menu.NamedArguments.Single(argument => argument.MemberName == "fileName").TypedValue.Value, Is.EqualTo("CharacterAbilityDefinition_" + name));
            Assert.That(menu.NamedArguments.Single(argument => argument.MemberName == "menuName").TypedValue.Value, Is.EqualTo("HardlightProject/DefinitionData/AbilityDef/" + name));
            Assert.That(menu.NamedArguments.Single(argument => argument.MemberName == "order").TypedValue.Value, Is.EqualTo(1));
        }

        // The declaration rows of the original DummyDll tables (type flags, attribute order, fields and methods with their
        // flags in metadata order). This is declaration shape, not behaviour.
        [Test]
        public void DefinitionsKeepTheOriginalDeclarationShape()
        {
            AssertMembers(typeof(CharacterAbilityDefinition_RailsProximityAir), 0x00100001,
                new[] { ("SurfaceNormalAngleTolerance", 0x0006), ("RailTrackerAttachDistance", 0x0006), ("<SurfaceNormalAngleCosineTolerance>k__BackingField", 0x0001) },
                new[] { ("get_ScriptType", 0x08c6), ("get_SurfaceNormalAngleCosineTolerance", 0x0886), ("set_SurfaceNormalAngleCosineTolerance", 0x0881), ("CalculateCachedValues", 0x00c6), (".ctor", 0x1886) });
            Assert.That(typeof(CharacterAbilityDefinition_RailsProximityAir).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_RailsHoming)));
            Assert.That(AttributeNames(typeof(CharacterAbilityDefinition_RailsProximityAir)), Is.EqualTo(new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "CreateAssetMenu" }));
            AssertAssetMenu(typeof(CharacterAbilityDefinition_RailsProximityAir), "RailsProximityAir");

            AssertMembers(typeof(CharacterAbilityDefinition_RailsProximityGround), 0x00100001,
                new (string, int)[0],
                new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
            Assert.That(typeof(CharacterAbilityDefinition_RailsProximityGround).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_RailsHoming)));
            Assert.That(AttributeNames(typeof(CharacterAbilityDefinition_RailsProximityGround)), Is.EqualTo(new[] { "Il2CppSetOption:2", "CreateAssetMenu", "Il2CppSetOption:1" }));
            AssertAssetMenu(typeof(CharacterAbilityDefinition_RailsProximityGround), "RailsProximityGround");

            AssertMembers(typeof(CharacterAbilityDefinition_RailsTargeting), 0x00100001,
                new[] { ("HomingProjectConeAngle", 0x0006), ("HomingProjectDistanceStep", 0x0006), ("HomingVelocity", 0x0006), ("ProjectFromCameraWhenNoDirectionInput", 0x0006), ("<HomingProjectConeTangents>k__BackingField", 0x0001) },
                new[] { ("get_ScriptType", 0x08c6), ("get_HomingProjectConeTangents", 0x0886), ("set_HomingProjectConeTangents", 0x0881), ("CalculateCachedValues", 0x00c6), (".ctor", 0x1886) });
            Assert.That(typeof(CharacterAbilityDefinition_RailsTargeting).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_RailsHoming)));
            Assert.That(AttributeNames(typeof(CharacterAbilityDefinition_RailsTargeting)), Is.EqualTo(new[] { "Il2CppSetOption:1", "CreateAssetMenu", "Il2CppSetOption:2" }));
            AssertAssetMenu(typeof(CharacterAbilityDefinition_RailsTargeting), "RailsTargeting");
        }
    }
}
