using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The climbing, detector and half pipe ability definitions. Expected values come from the 1.10.1
    // x86-64 constructors and bodies cited per test; the constants are read from the binary's data
    // section, never from the implementation.
    public sealed class CharacterClimbDetectorHalfPipeDefinitionTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private T Create<T>() where T : ScriptableObject
        {
            var definition = ScriptableObject.CreateInstance<T>();
            m_owned.Add(definition);
            return definition;
        }

        private readonly System.Collections.Generic.List<Object> m_owned = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = m_owned.Count - 1; i >= 0; i--)
                if (m_owned[i] != null) Object.DestroyImmediate(m_owned[i]);
            m_owned.Clear();
        }

        // ---- CharacterAbilityDefinition_EnterClimbing (020004cb) ----

        // The constructors 0x542E30 (base), 0x542DD0, 0x542EE0 and 0x542F90 each store TrackMask = -1 (all layers,
        // LayerMask.op_Implicit(-1)), the two angle limits as one 8 byte load of the constant pair at VA 0x29a7710
        // (60 then 120), a half second re-attach cooldown (0x3f000000) and the ability base gravity multiplier 1.
        [Test]
        public void EnterClimbingConstructorsStoreTheNativeDefaults()
        {
            foreach (CharacterAbilityDefinition_EnterClimbing definition in new CharacterAbilityDefinition_EnterClimbing[]
            {
                Create<CharacterAbilityDefinition_ClimbingAir>(), Create<CharacterAbilityDefinition_ClimbingDirectionOverride>(),
                Create<CharacterAbilityDefinition_ClimbingGround>(),
            })
            {
                string name = definition.GetType().Name;
                Assert.That(definition.TrackMask.value, Is.EqualTo(-1), name);
                Assert.That(definition.MinVerticalAngleSurface, Is.EqualTo(60f), name);
                Assert.That(definition.MaxVerticalAngleSurface, Is.EqualTo(120f), name);
                Assert.That(definition.ReAttachCooldownSeconds, Is.EqualTo(0.5f), name);
                Assert.That(definition.GravityMultiplier, Is.EqualTo(1f), name);
            }
        }

        // 0x542DD0 stores 0x42340000 (45) at +0xDC and 0x542F90 stores 0x41200000 (10) at +0xDC.
        [Test]
        public void ClimbingLeavesStoreTheirOwnThresholds()
        {
            Assert.That(Create<CharacterAbilityDefinition_ClimbingAir>().MinAngleOfAttack, Is.EqualTo(45f));
            Assert.That(Create<CharacterAbilityDefinition_ClimbingGround>().VelocityThresholdGround, Is.EqualTo(10f));
        }

        // 0x542FF0 tail calls MathUtilities.WithinRangeWithTolerance(angle, +0xD0, +0xD4, tolerance) with the
        // tolerance loaded from VA 0x29a6dfc (1e-4). The inclusive interval is [60 - 1e-4, 120 + 1e-4]: 59.99995 and
        // 120.00005 sit inside the tolerance, 59.9998 and 120.0002 outside it. A tolerance of zero fails the first
        // pair, a larger one (1e-3) the second pair; NaN is outside; inverted limits are exchanged by the callee.
        [Test]
        public void SurfaceAngleIsValidInsideTheLimitsWidenedByTheNativeTolerance()
        {
            CharacterAbilityDefinition_EnterClimbing definition = Create<CharacterAbilityDefinition_ClimbingGround>();
            foreach (float inside in new[] { 60f, 90f, 120f, 59.99995f, 120.00005f })
                Assert.That(definition.SurfaceAngleValid(inside), Is.True, "angle " + inside);
            foreach (float outside in new[] { 0f, 59.9998f, 120.0002f, 180f, -90f, float.NaN })
                Assert.That(definition.SurfaceAngleValid(outside), Is.False, "angle " + outside);

            definition.MinVerticalAngleSurface = 120f;
            definition.MaxVerticalAngleSurface = 60f;
            Assert.That(definition.SurfaceAngleValid(90f), Is.True, "inverted limits are exchanged");
            Assert.That(definition.SurfaceAngleValid(30f), Is.False);
        }

        // The limits are read from the definition (+0xD0 minimum, +0xD4 maximum), not fixed constants.
        [Test]
        public void SurfaceAngleFollowsTheAuthoredLimits()
        {
            CharacterAbilityDefinition_EnterClimbing definition = Create<CharacterAbilityDefinition_ClimbingAir>();
            definition.MinVerticalAngleSurface = 10f;
            definition.MaxVerticalAngleSurface = 20f;
            Assert.That(definition.SurfaceAngleValid(15f), Is.True);
            Assert.That(definition.SurfaceAngleValid(90f), Is.False);
        }

        // ---- CharacterAbilityDefinition_Detector (0200050a) ----

        // 0x545E60 stores RefreshInterval = 1 (0x3f800000) at +0xCC; the proximity distance stays zero.
        [Test]
        public void DetectorRefreshesEverySecondByDefault()
        {
            foreach (CharacterAbilityDefinition_Detector definition in new CharacterAbilityDefinition_Detector[]
            {
                Create<CharacterAbilityDefinition_DetectorArchetype>(), Create<CharacterAbilityDefinition_DetectorLightspeedDash>(),
            })
            {
                Assert.That(definition.RefreshInterval, Is.EqualTo(1f), definition.GetType().Name);
                Assert.That(definition.ProximityDistance, Is.EqualTo(0f), definition.GetType().Name);
                Assert.That(definition.ProximityDistanceSqr, Is.EqualTo(0f), definition.GetType().Name);
            }
        }

        // 0x545E30 runs the ability base cache, then multiplies +0xD0 by itself into +0xD4 (mulss xmm0, xmm0).
        // The sign is lost, and the square is the single precision product.
        [Test]
        public void DetectorCachedValuesSquareTheProximityDistance()
        {
            CharacterAbilityDefinition_Detector definition = Create<CharacterAbilityDefinition_DetectorLightspeedDash>();
            foreach ((float distance, float square) in new[] { (3f, 9f), (-2f, 4f), (0.5f, 0.25f), (0f, 0f), (0.1f, 0.010000001f) })
            {
                definition.ProximityDistance = distance;
                definition.CalculateCachedValues();
                Assert.That(definition.ProximityDistanceSqr, Is.EqualTo(square), "distance " + distance);
            }
        }

        // The property is read only outside the class: the setter 0x545E20 is private.
        [Test]
        public void DetectorProximitySquareHasAPrivateSetter()
        {
            PropertyInfo property = typeof(CharacterAbilityDefinition_Detector).GetProperty("ProximityDistanceSqr");
            Assert.That(property.GetSetMethod(false), Is.Null);
            Assert.That(property.GetSetMethod(true).IsPrivate, Is.True);
        }

        // ---- CharacterAbilityDefinition_HalfPipe leaves (020004d0, 020004d3) ----

        // 0x543120 and 0x5431C0 store OffsetFromTracker = 0x3dcccccd (0.1f) at +0xD8.
        [Test]
        public void HalfPipeLeavesStoreTheTrackerOffset()
        {
            Assert.That(Create<CharacterAbilityDefinition_HalfPipeAir>().OffsetFromTracker, Is.EqualTo(0.1f));
            Assert.That(Create<CharacterAbilityDefinition_HalfPipeTransfer>().OffsetFromTracker, Is.EqualTo(0.1f));
        }

        // 0x5430E0: the ability base cache, then cosf(ExcludeAngle * 0.017453292) stored at +0x14 of the exclusion
        // object read from +0xF8. The conversion constant is the single precision VA 0x29a6c18.
        [Test]
        public void HalfPipeAirCachesTheCosineOfTheExclusionAngle()
        {
            CharacterAbilityDefinition_HalfPipeAir definition = Create<CharacterAbilityDefinition_HalfPipeAir>();
            definition.AirMotionExclude = new CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionExclude();
            foreach ((float angle, float cosine) in new[] { (0f, 1f), (60f, 0.5f), (90f, 0f), (180f, -1f), (45f, 0.70710678f) })
            {
                definition.AirMotionExclude.ExcludeAngle = angle;
                definition.CalculateCachedValues();
                Assert.That(definition.AirMotionExclude.ExcludeAngleCosine, Is.EqualTo(cosine).Within(1e-6f), "angle " + angle);
            }
        }

        // The exclusion object is dereferenced with no null test (NullChecks are off in the retail build), so a
        // definition without one faults, after the base cache has run.
        [Test]
        public void HalfPipeAirCachedValuesFaultWithoutAnExclusionObject()
        {
            CharacterAbilityDefinition_HalfPipeAir definition = Create<CharacterAbilityDefinition_HalfPipeAir>();
            definition.AirMotionExclude = null;
            Assert.Throws<NullReferenceException>(() => definition.CalculateCachedValues());
        }

        // ---- Declaration shape (not behaviour): the original script types, bases and asset menu entries ----

        [Test]
        public void DeclarationShapeConcreteDefinitionsNameTheirOwnAbilityScript()
        {
            Assert.That(Create<CharacterAbilityDefinition_ClimbingAir>().ScriptType, Is.EqualTo(typeof(CharacterAbility_ClimbingAir)));
            Assert.That(Create<CharacterAbilityDefinition_ClimbingDirectionOverride>().ScriptType, Is.EqualTo(typeof(CharacterAbility_ClimbingDirectionOverride)));
            Assert.That(Create<CharacterAbilityDefinition_ClimbingGround>().ScriptType, Is.EqualTo(typeof(CharacterAbility_ClimbingGround)));
            Assert.That(Create<CharacterAbilityDefinition_DetectorArchetype>().ScriptType, Is.EqualTo(typeof(CharacterAbility_DetectorArchetype)));
            Assert.That(Create<CharacterAbilityDefinition_DetectorLightspeedDash>().ScriptType, Is.EqualTo(typeof(CharacterAbility_DetectorLightspeedDash)));
            Assert.That(Create<CharacterAbilityDefinition_HalfPipeAir>().ScriptType, Is.EqualTo(typeof(CharacterAbility_HalfPipeAir)));
            Assert.That(Create<CharacterAbilityDefinition_HalfPipeTransfer>().ScriptType, Is.EqualTo(typeof(CharacterAbility_HalfPipeTransfer)));
        }

        [Test]
        public void DeclarationShapeDefinitionsKeepTheirOriginalBases()
        {
            Assert.That(typeof(CharacterAbilityDefinition_EnterClimbing).IsAbstract, Is.True);
            Assert.That(typeof(CharacterAbilityDefinition_EnterClimbing).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition)));
            Assert.That(typeof(CharacterAbilityDefinition_ClimbingAir).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_EnterClimbing)));
            Assert.That(typeof(CharacterAbilityDefinition_ClimbingDirectionOverride).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_EnterClimbing)));
            Assert.That(typeof(CharacterAbilityDefinition_ClimbingGround).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_EnterClimbing)));
            Assert.That(typeof(CharacterAbilityDefinition_Detector).IsAbstract, Is.True);
            Assert.That(typeof(CharacterAbilityDefinition_Detector).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition)));
            Assert.That(typeof(CharacterAbilityDefinition_DetectorArchetype).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_Detector)));
            Assert.That(typeof(CharacterAbilityDefinition_DetectorLightspeedDash).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_Detector)));
            Assert.That(typeof(CharacterAbilityDefinition_HalfPipeAir).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_HalfPipe)));
            Assert.That(typeof(CharacterAbilityDefinition_HalfPipeTransfer).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_HalfPipe)));
            Type motion = typeof(CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionOverride);
            Assert.That(motion.BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_HalfPipe.HalfPipeMotion)));
            Assert.That(motion.IsSerializable && motion.IsNestedPublic, Is.True);
            Type exclude = typeof(CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionExclude);
            Assert.That(exclude.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(exclude.IsSerializable && exclude.IsNestedPublic, Is.True);
        }

        // Dump.cs field order and metadata attribute order of the half pipe air definition and detector fields.
        [Test]
        public void DeclarationShapeFieldsKeepTheirOriginalOrder()
        {
            const BindingFlags declared = Members | BindingFlags.DeclaredOnly;
            Assert.That(typeof(CharacterAbilityDefinition_HalfPipeAir).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "Trajectory", "AirMotion", "AirMotionOverrides", "AirMotionExclude" }));
            Assert.That(typeof(CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionOverride).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "ActivationSpeed", "ActivationTime" }));
            Assert.That(typeof(CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionExclude).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "ExcludeAngle", "ExcludeAngleCosine" }));
            Assert.That(typeof(CharacterAbilityDefinition_EnterClimbing).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "TrackMask", "MinVerticalAngleSurface", "MaxVerticalAngleSurface", "ReAttachCooldownSeconds" }));
            Assert.That(typeof(CharacterAbilityDefinition_Detector).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "RefreshInterval", "ProximityDistance", "<ProximityDistanceSqr>k__BackingField" }));
        }

        [Test]
        public void DeclarationShapeConcreteDefinitionsAreCreatableFromTheAssetMenu()
        {
            foreach ((Type type, string menu) in new[]
            {
                (typeof(CharacterAbilityDefinition_ClimbingAir), "ClimbingAir"),
                (typeof(CharacterAbilityDefinition_ClimbingDirectionOverride), "ClimbingDirectionOverride"),
                (typeof(CharacterAbilityDefinition_ClimbingGround), "ClimbingGround"),
                (typeof(CharacterAbilityDefinition_DetectorArchetype), "DetectorArchetype"),
                (typeof(CharacterAbilityDefinition_DetectorLightspeedDash), "DetectorLightspeedDash"),
                (typeof(CharacterAbilityDefinition_HalfPipeAir), "HalfPipeAir"),
                (typeof(CharacterAbilityDefinition_HalfPipeTransfer), "HalfPipeTransfer"),
            })
            {
                CreateAssetMenuAttribute attribute = type.GetCustomAttribute<CreateAssetMenuAttribute>(false);
                Assert.That(attribute.fileName, Is.EqualTo(type.Name), type.Name);
                Assert.That(attribute.menuName, Is.EqualTo("HardlightProject/DefinitionData/AbilityDef/" + menu), type.Name);
                Assert.That(attribute.order, Is.EqualTo(1), type.Name);
            }
        }
    }
}
