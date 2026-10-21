using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The chaos aim and chaos dash definitions, the aim target data they hold and the camera override definition. The
    // expected values are read from the 1.10.1 x86-64 bodies cited per test (constants from the __const table at the
    // virtual addresses given). The trigonometric expectations are computed with System.Math on the authored angle, so
    // a missing Deg2Rad (or a stray one) moves them far outside the tolerance.
    public sealed class CharacterAirChaosDefinitionTests
    {
        private const float Tolerance = 1e-4f;
        private static readonly Vector3EqualityComparer Vec = new Vector3EqualityComparer(Tolerance);

        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        // The cone bounds fill the shared segment table on first use; it is put back as found.
        internal static void CacheKeepingTheConeTable(CharacterAbilityDefinition_AirChaosAim definition)
        {
            FieldInfo table = typeof(ConeVolume).GetField("s_coneSegmentAngles", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = table.GetValue(null);
            try
            {
                definition.CalculateCachedValues();
            }
            finally
            {
                table.SetValue(null, prior);
            }
        }

        private static float Degrees(Func<double, double> function, float degrees) => (float)function(degrees * Math.PI / 180.0);

        private static AnimationCurve CurveEndingAt(float time) => new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(time, 1f));

        // ---- CharacterAbilityDefinition_AirChaosAim constructor (06001ad1, 0x541880) ----

        // The scalar defaults are stored from the constants at VA 0x29a7680 (100, 50) and 0x29a7690 (180, 1, 0.5, 10),
        // the immediate 0x3dcccccd (0.1) at +0x1ac, and the masks come from LayerMask.op_Implicit(-1). LedgeAngleMin
        // (+0x198) is never written.
        [Test]
        public void AimDefinitionStartsWithTheNativeRaycastAndLedgeDefaults()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosAim>());
            Assert.That((definition.RaycastDistance, definition.GroundDistance, definition.LedgeAngleMin, definition.LedgeAngleMax),
                Is.EqualTo((100f, 50f, 0f, 180f)));
            Assert.That((definition.LedgeDistance, definition.LedgeOffset, definition.WorldDistance, definition.OffsetDistance),
                Is.EqualTo((1f, 0.5f, 10f, 0.1f)));
            Assert.That((definition.TrackMask.value, definition.CollisionMask.value), Is.EqualTo((-1, -1)));
        }

        // 0x5419ad stores 1, 30, 30, 1 at +0x1e4..+0x1f0 (VA 0x29a76a0), 0x5419bb stores 30, 30 at +0x1f4, and
        // the constants at VA 0x29a6c30 store 1 and 1 at +0x1fc/+0x200; +0x204 is the immediate 0x41200000 (10).
        [Test]
        public void AimDefinitionStartsWithTheNativeHomingDefaults()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosAim>());
            Assert.That(definition.HomingTargetProjectDistance, Is.EqualTo(1f));
            Assert.That(definition.HomingTargetProjectConeAngle, Is.EqualTo(new Vector2(30f, 30f)));
            Assert.That(definition.HomingRailProjectDistance, Is.EqualTo(1f));
            Assert.That(definition.HomingRailProjectConeAngle, Is.EqualTo(new Vector2(30f, 30f)));
            Assert.That((definition.HomingRailProjectDistanceStep, definition.AimLerpSmoothTime, definition.AimLerpMaxSpeed), Is.EqualTo((1f, 1f, 10f)));
            Assert.That(definition.HomingTargetScreenBorders, Is.EqualTo(Vector2.zero));
            Assert.That(definition.HomingTargetZoneOffset, Is.EqualTo(Vector3.zero));
        }

        // The scale curve is two keys (0, 1) and (1, 1) (0x541a1e..0x541a6e); each camera curve is three keys at
        // -180, 0 and 180 with values 1, 0, 1 (constants at VA 0x29a6e44 and 0x29a6e48 are -180 and 180).
        [Test]
        public void AimDefinitionStartsWithTheNativeCurves()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosAim>());
            AssertKeys(definition.AimLerpScaleAnim, (0f, 1f), (1f, 1f));
            AssertKeys(definition.CameraTargetHorizontalInputValue, (-180f, 1f), (0f, 0f), (180f, 1f));
            AssertKeys(definition.CameraTargetVerticalInputValue, (-180f, 1f), (0f, 0f), (180f, 1f));
        }

        private static void AssertKeys(AnimationCurve curve, params (float time, float value)[] expected)
        {
            Keyframe[] keys = curve.keys;
            Assert.That(keys.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That((keys[i].time, keys[i].value), Is.EqualTo(expected[i]), "key " + i);
        }

        // 0x5418e3 and 0x541970 build both dictionaries with the enum comparers; the prefab, the target list and
        // the other reference fields are never written.
        [Test]
        public void AimDefinitionBuildsItsDictionariesWithTheEnumComparers()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosAim>());
            Assert.That(definition.TargetTypes.Count, Is.EqualTo(0));
            Assert.That(definition.MappingHomingToAimTargetTypes.Count, Is.EqualTo(0));
            Assert.That(Get<Dictionary<AbilityAimTargetType, CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget>>(definition.TargetTypes, "m_dictionary").Comparer,
                Is.SameAs(HardlightProject.HardlightEnumComparers.AbilityAimTargetTypeComparer));
            Assert.That(Get<Dictionary<HomingTargetType, AbilityAimTargetType>>(definition.MappingHomingToAimTargetTypes, "m_dictionary").Comparer,
                Is.SameAs(HardlightProject.HardlightEnumComparers.HomingTargetTypeComparer));
            Assert.That(definition.HomingTargetDefinitions, Is.Null);
            Assert.That(definition.HomingAttackPrefab == null, Is.True);
        }

        [Test]
        public void AimAndDashDefinitionsNameTheirAbilityTypes()
        {
            Assert.That(Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosAim>()).ScriptType, Is.EqualTo(typeof(CharacterAbility_AirChaosAim)));
            Assert.That(Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosDash>()).ScriptType, Is.EqualTo(typeof(CharacterAbility_AirChaosDash)));
        }

        // ---- CharacterAbilityDefinition_AirChaosAim.CalculateCachedValues (06001ad0, 0x541360) ----

        private CharacterAbilityDefinition_AirChaosAim CachedAim(CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget target = null)
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosAim>());
            definition.HomingTargetProjectDistance = 10f;
            definition.HomingTargetProjectConeAngle = new Vector2(20f, 40f);
            definition.HomingRailProjectConeAngle = new Vector2(10f, 50f);
            definition.LedgeAngleMin = 30f;
            definition.LedgeAngleMax = 120f;
            if (target != null) definition.TargetTypes[AbilityAimTargetType.Wall] = target;
            CacheKeepingTheConeTable(definition);
            return definition;
        }

        // 0x541517 and 0x541529 take the cosine of the cone angles as authored (no Deg2Rad), unlike the tangents
        // (0x541446, 0x541461) and ledge cosines (0x54158c); the distance is squared at 0x54150e. With 20 and 40 as
        // radians the cosines are 0.408 and -0.667, whereas degrees would give 0.940 and 0.766.
        [Test]
        public void AimCachedConeCosinesUseTheRawAngleAndTheDistanceIsSquared()
        {
            var definition = CachedAim();
            Assert.That(definition.HomingTargetProjectDistanceSqr, Is.EqualTo(100f));
            Assert.That(definition.HomingTargetProjectConeCosAngle.x, Is.EqualTo((float)Math.Cos(20.0)).Within(Tolerance));
            Assert.That(definition.HomingTargetProjectConeCosAngle.y, Is.EqualTo((float)Math.Cos(40.0)).Within(Tolerance));
        }

        // 0x541469..0x541487 build the cone with a zero origin and rotation, the project distance and the
        // tangents of the angles in degrees; Step is left zero.
        [Test]
        public void AimCachedConeHasZeroOriginAndRotationAndDegreeTangents()
        {
            ConeVolume.ConeDescription cone = CachedAim().HomingTargetCone;
            Assert.That(cone.Origin, Is.EqualTo(Vector3.zero));
            Assert.That(cone.Rotation, Is.EqualTo(new Quaternion(0f, 0f, 0f, 0f)));
            Assert.That(cone.Distance, Is.EqualTo(10f));
            Assert.That(cone.Step, Is.EqualTo(0f));
            Assert.That(cone.Tangents.x, Is.EqualTo(Degrees(Math.Tan, 20f)).Within(Tolerance));
            Assert.That(cone.Tangents.y, Is.EqualTo(Degrees(Math.Tan, 40f)).Within(Tolerance));
        }

        // 0x5414d5..0x5414ef doubles the extents of the cone bounds: with a zero rotation the cone runs along +z
        // for the project distance and its end circle spans the distance times each tangent either side.
        [Test]
        public void AimCachedZoneSizeIsTheConeBoundsSize()
        {
            Vector3 expected = new Vector3(2f * 10f * Degrees(Math.Tan, 20f), 2f * 10f * Degrees(Math.Tan, 40f), 10f);
            Assert.That(CachedAim().HomingTargetZoneSize, Is.EqualTo(expected).Using(Vec));
        }

        // 0x54154e..0x5415bb: rail tangents from the rail angles, ledge cosines from the ledge angles (min first).
        [Test]
        public void AimCachedRailTangentsAndLedgeCosinesUseDegrees()
        {
            var definition = CachedAim();
            Assert.That(definition.HomingRailProjectConeTangents.x, Is.EqualTo(Degrees(Math.Tan, 10f)).Within(Tolerance));
            Assert.That(definition.HomingRailProjectConeTangents.y, Is.EqualTo(Degrees(Math.Tan, 50f)).Within(Tolerance));
            Assert.That(definition.LedgeCosAngleMin, Is.EqualTo(Degrees(Math.Cos, 30f)).Within(Tolerance));
            Assert.That(definition.LedgeCosAngleMax, Is.EqualTo(-0.5f).Within(Tolerance));
        }

        // 0x5415c3..0x541728 call the target caching for every dictionary value. The totals are the larger of the
        // position and rotation curve ends (position larger for arrow 1, rotation larger for arrow 0).
        [Test]
        public void AimCachedValuesCacheEveryAimTarget()
        {
            var target = new CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget
            {
                Arrow0PositionAnimation = CurveEndingAt(2f), Arrow0RotationAnimation = CurveEndingAt(3f),
                Arrow1PositionAnimation = CurveEndingAt(5f), Arrow1RotationAnimation = CurveEndingAt(4f),
            };
            CachedAim(target);
            Assert.That(Get<float>(target, "m_arrow0AnimTimeTotal"), Is.EqualTo(3f));
            Assert.That(Get<float>(target, "m_arrow1AnimTimeTotal"), Is.EqualTo(5f));
        }

        // ---- CharacterAbilityDefinition_AirChaosDash (06001ad7..06001adb, 0x542120..0x542230) ----

        // The constructor stores 0.1, 160, 0.5 and 100 from the table at VA 0x29a76b0 and the curve
        // AnimationCurve.Constant(0, 360, 20) (constants at VA 0x29a6e50 and 0x29a7508).
        [Test]
        public void DashDefinitionStartsWithTheNativeValues()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosDash>());
            Assert.That((definition.MinimumTime, definition.OffsetAngleToWorldUp, definition.OffsetDistance, definition.DashSpeed),
                Is.EqualTo((0.1f, 160f, 0.5f, 100f)));
            AssertKeys(definition.ExitSpeedBySlopeCurve, (0f, 20f), (360f, 20f));
            Assert.That(definition.ExitSpeedBySlopeCurve.Evaluate(123f), Is.EqualTo(20f));
        }

        // 0x54220f..0x542224: the cosine of OffsetAngleToWorldUp in degrees. The air base's (CharacterAbilityDefinition_Air) cached cosines are
        // computed first (0x5421b3..0x5421ff), so the default 45 degree gravity change cosine is present too.
        [Test]
        public void DashCachedValuesTakeTheCosineOfTheOffsetAngleInDegrees()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirChaosDash>());
            definition.OffsetAngleToWorldUp = 60f;
            definition.CalculateCachedValues();
            Assert.That(definition.OffsetCosineAngleToWorldUp, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(definition.DeactivateOnGravityCosineAngleChange, Is.EqualTo(Degrees(Math.Cos, 45f)).Within(Tolerance));
        }

        // ---- ChaosAimTarget (06001ad3..06001ad6, 0x541810..0x5420b0) ----

        [Test]
        public void AimTargetStartsWithIdentityRotationsAndBothArrowsEnabled()
        {
            var target = new CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget();
            Assert.That(target.TargetRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(target.ArrowsRotation, Is.EqualTo(Quaternion.identity));
            Assert.That((target.Arrow0Enabled, target.Arrow1Enabled), Is.EqualTo((true, true)));
            Assert.That((target.RequiresLerpToTarget, target.OrientateCharacterOnExit, target.RaycastGraceDistance), Is.EqualTo((false, false, 0f)));
            Assert.That((target.BeamTransformType, target.DiscTransformType, target.RingTransformType, target.ArrowsTransformType),
                Is.EqualTo((AbilityAimTransformType.None, AbilityAimTransformType.None, AbilityAimTransformType.None, AbilityAimTransformType.None)));
        }

        [Test]
        public void HomingTargetAimDefinitionStartsEmpty()
        {
            var definition = new CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition();
            Assert.That(definition.TargetType, Is.Null);
            Assert.That(definition.EnemyTypes, Is.Null);
            Assert.That(definition.HomingTypes, Is.Null);
            Assert.That(definition.OffsetLocal, Is.EqualTo(Vector3.zero));
        }

        private static CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget TimedTarget()
        {
            var target = new CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget
            {
                Arrow0PositionAnimation = CurveEndingAt(2f), Arrow0RotationAnimation = CurveEndingAt(3f),
                Arrow1PositionAnimation = CurveEndingAt(5f), Arrow1RotationAnimation = CurveEndingAt(4f),
            };
            target.CalculateCachedValues();
            return target;
        }

        // 0x541824..0x541869 store the larger curve end of each arrow at +0xa0 and +0xa4 (maxss).
        [Test]
        public void AimTargetCachesTheLongerCurveOfEachArrow()
        {
            var target = TimedTarget();
            Assert.That(Get<float>(target, "m_arrow0AnimTimeTotal"), Is.EqualTo(3f));
            Assert.That(Get<float>(target, "m_arrow1AnimTimeTotal"), Is.EqualTo(5f));
        }

        // 0x541d88 and 0x541d90 wrap with ucomiss/jb, so a clock equal to its total wraps to zero; the loop at
        // 0x541d90..0x541da3 subtracts repeatedly, so a clock several totals long ends inside one total. A NaN
        // clock never compares at or above the total and is left untouched. The two clocks are independent.
        [Test]
        public void AimTargetAnimationClocksWrapIndependentlyAtTheirTotals()
        {
            var target = TimedTarget();
            float arrow0 = 2.5f;
            float arrow1 = 4.5f;
            target.UpdateAnimation(ref arrow0, ref arrow1, 1f);
            Assert.That((arrow0, arrow1), Is.EqualTo((0.5f, 0.5f)));
            arrow0 = 2f;
            arrow1 = 1f;
            target.UpdateAnimation(ref arrow0, ref arrow1, 1f);
            Assert.That((arrow0, arrow1), Is.EqualTo((0f, 2f)), "equal to the total wraps to zero; the other clock only advances");
            arrow0 = 2.5f;
            arrow1 = 0f;
            target.UpdateAnimation(ref arrow0, ref arrow1, 7f);
            Assert.That((arrow0, arrow1), Is.EqualTo((0.5f, 2f)), "9.5 less three totals, and 7 less one total of 5");
        }

        [Test]
        public void AimTargetAnimationClockStaysNaNWithoutLooping()
        {
            var target = TimedTarget();
            float arrow0 = float.NaN;
            float arrow1 = 0f;
            target.UpdateAnimation(ref arrow0, ref arrow1, 1f);
            Assert.That(float.IsNaN(arrow0), Is.True);
            Assert.That(arrow1, Is.EqualTo(1f));
        }

        private CameraAbilitySlopeDefinition SlopeOverride(params Vector2[] ranges)
        {
            var definition = Own(ScriptableObject.CreateInstance<CameraAbilitySlopeDefinition>());
            definition.AngleRanges = new List<Vector2>(ranges);
            return definition;
        }

        // 0x541f64 skips a range when its minimum is above the angle (ja) and 0x541f73 when the angle is above its
        // maximum, so both ends are inclusive; the first matching range of the first matching override is returned.
        // A NaN angle fails both comparisons and so matches the very first range.
        [Test]
        public void AimTargetCameraOverrideIsTheFirstOverrideWithAnInclusiveRangeAroundTheAngle()
        {
            CameraAbilitySlopeDefinition first = SlopeOverride(new Vector2(10f, 20f));
            CameraAbilitySlopeDefinition second = SlopeOverride(new Vector2(30f, 40f), new Vector2(50f, 60f));
            var target = new CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget
            {
                CameraSlopeOverrides = new List<CameraAbilitySlopeDefinition> { first, second },
            };
            foreach (float angle in new[] { 10f, 15f, 20f, float.NaN })
                Assert.That(target.GetCameraOverride(angle), Is.SameAs(first), "angle " + angle);
            foreach (float angle in new[] { 30f, 40f, 55f, 60f })
                Assert.That(target.GetCameraOverride(angle), Is.SameAs(second), "angle " + angle);
            foreach (float angle in new[] { 9.99f, 20.01f, 25f, 29.99f, 45f, 60.01f })
                Assert.That(target.GetCameraOverride(angle) == null, Is.True, "angle " + angle);
        }

        [Test]
        public void AimTargetCameraOverrideIsNullForNoOverridesAndFailsForNoList()
        {
            var target = new CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget { CameraSlopeOverrides = new List<CameraAbilitySlopeDefinition>() };
            Assert.That(target.GetCameraOverride(10f) == null, Is.True);
            target.CameraSlopeOverrides = null;
            Assert.Throws<NullReferenceException>(() => target.GetCameraOverride(10f));
        }

        // ---- CameraAbilityOverrideDefinition (060019b4..060019b6, 0x7a8c50..0x7a8c70) ----

        [Test]
        public void CameraOverrideDefinitionAlignsToForwardByDefaultAndReadsItsFields()
        {
            var definition = Own(ScriptableObject.CreateInstance<CameraAbilityOverrideDefinition>());
            Assert.That(definition.AxisAlignment, Is.EqualTo(Vector3.forward));
            Assert.That(definition.ReverseByDirection, Is.False);
            Set(definition, "m_axisAlignment", new Vector3(1f, 2f, 3f));
            Set(definition, "m_reverseByDirection", true);
            Assert.That(definition.AxisAlignment, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(definition.ReverseByDirection, Is.True);
        }
    }
}
