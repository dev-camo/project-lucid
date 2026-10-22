using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.CatchReleaseDefinition_Attach (TypeDef 0x020004a7, x86-64 0x540680..0x5404D0),
    // CatchReleaseDefinition_Catch (0x020004a8, 0x5402D0, 0x5404D0) with its nested Events (0x020004a9,
    // 0x540700..0x540800) and CatchPoint (0x020004aa, 0x5406A0), and CatchReleaseDefinition_Release
    // (0x020004ab, 0x5402E0, 0x540600). All three are plain serialisable classes built by their
    // real constructors.
    public sealed class OriginalCatchReleaseDefinitionTests
    {
        // Metadata of the three TypeDefs (flags, fields in order, methods in order) and the
        // authored attribute order of the fields whose Min and Tooltip attributes swap places.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(CatchReleaseDefinition_Attach).Attributes, Is.EqualTo(0x00102001));
            Assert.That(Fields(typeof(CatchReleaseDefinition_Attach)), Is.EqualTo(new[]
            {
                "InputRequired:Boolean:0006", "InputRequiredIsContinuous:Boolean:0006", "InputGameAction:GameAction:0006",
                "IsDynamic:Boolean:0006", "OrientateFromVelocity:Boolean:0006", "UseSpeedMagnitude:Boolean:0006",
                "SpeedMin:Single:0006", "AlignmentDirection:Vector3:0006", "AlignmentAngleTolerance:Single:0006",
                "SpeedNormalised:Single:0006", "SpeedCurve:AnimationCurve:0006", "SnapToAxis:SnapToAxis:0006",
                "SnapToAttachPoint:Boolean:0006", "OffsetToTopOfCharacter:Boolean:0006",
                "<AlignmentCosAngleTolerance>k__BackingField:Single:0001"
            }));
            Assert.That(Methods(typeof(CatchReleaseDefinition_Attach)), Is.EqualTo(new[]
            {
                "get_AlignmentCosAngleTolerance:0886", "set_AlignmentCosAngleTolerance:0881", "CalculateCachedValues:0086", ".ctor:1886"
            }));
            Assert.That(AttributeRow(typeof(CatchReleaseDefinition_Attach).GetField("AlignmentAngleTolerance")), Is.EqualTo(
                "MinAttribute(0) TooltipAttribute(Angle velocity must be within tolerance of to activate an attach. NB: Zero means no alignment check.)"));
            Assert.That(AttributeRow(typeof(CatchReleaseDefinition_Attach).GetField("SpeedNormalised")), Is.EqualTo(
                "MinAttribute(0) TooltipAttribute(Speed that attach point will travel at normalised multiplier.)"));
            Assert.That(AttributeRow(typeof(CatchReleaseDefinition_Attach).GetField("SpeedCurve")), Is.EqualTo(
                "TooltipAttribute(X axis = velocity magnitude \nY axis = speed while attached.)"));

            Assert.That((int)typeof(CatchReleaseDefinition_Catch).Attributes, Is.EqualTo(0x00102001));
            Assert.That(Fields(typeof(CatchReleaseDefinition_Catch)), Is.EqualTo(new[]
            {
                "RemainTime:Single:0006", "ReverseOnDirection:Boolean:0006", "PointForward:CatchPoint:0006",
                "PointReverse:CatchPoint:0006", "Connector:CatchConnector:0006"
            }));
            Assert.That(Methods(typeof(CatchReleaseDefinition_Catch)), Is.EqualTo(new[] { "CalculateCachedValues:0086", ".ctor:1886" }));
            Assert.That(AttributeRow(typeof(CatchReleaseDefinition_Catch).GetField("RemainTime")), Is.EqualTo(
                "MinAttribute(0) TooltipAttribute(Minimum time to remain in toy before a transition exit can be triggered.)"));

            Assert.That((int)typeof(CatchReleaseDefinition_Catch.Events).Attributes, Is.EqualTo(0x00102002));
            Assert.That(Fields(typeof(CatchReleaseDefinition_Catch.Events)), Is.EqualTo(new[]
            {
                "m_onEnter:UnityEvent`1:0006", "m_onUpdateConstraints:UnityEvent`1:0006", "m_onExit:UnityEvent`1:0006", "m_onReset:UnityEvent:0006"
            }));
            Assert.That(Methods(typeof(CatchReleaseDefinition_Catch.Events)), Is.EqualTo(new[]
                { "OnEnter:0086", "OnUpdateConstraints:0086", "OnExit:0086", "OnReset:0086", ".ctor:1886" }));
            Assert.That((int)typeof(CatchReleaseDefinition_Catch.CatchPoint).Attributes, Is.EqualTo(0x00102002));
            Assert.That(Fields(typeof(CatchReleaseDefinition_Catch.CatchPoint)), Is.EqualTo(new[]
            {
                "PositionOffset:Vector3:0006", "AngleOffset:Vector3:0006", "IgnoreRotation:Boolean:0006", "Events:Events:0006"
            }));
            Assert.That(Methods(typeof(CatchReleaseDefinition_Catch.CatchPoint)), Is.EqualTo(new[] { ".ctor:1886" }));

            Assert.That((int)typeof(CatchReleaseDefinition_Release).Attributes, Is.EqualTo(0x00102001));
            Assert.That(Fields(typeof(CatchReleaseDefinition_Release)), Is.EqualTo(new[]
            {
                "InputType:CatchInputReleaseType:0006", "TransformPoints:CatchReleasePoints:0006", "VelocityPoints:CatchReleasePoints:0006",
                "UseWorldSpeed:Boolean:0006", "SpeedCurve:AnimationCurve:0006", "MultiplierCurve:AnimationCurve:0006",
                "ControlsDisabledTime:Single:0006", "VelocityLockTime:Single:0006", "ReturnSpeed:Single:0006", "LeaveChecker:ObjectLeaveChecker:0006"
            }));
            Assert.That(Methods(typeof(CatchReleaseDefinition_Release)), Is.EqualTo(new[] { "CalculateCachedValues:0086", ".ctor:1886" }));
            Assert.That(AttributeRow(typeof(CatchReleaseDefinition_Release).GetField("TransformPoints")), Is.EqualTo(
                "TooltipAttribute(Points to snap transform to on catch release.) FormerlySerializedAsAttribute(Points)"));
            Assert.That(AttributeRow(typeof(CatchReleaseDefinition_Release).GetField("ReturnSpeed")), Is.EqualTo(
                "MinAttribute(0) TooltipAttribute(Speed that attach point will travel after release and returning back to start.)"));
        }

        // Constructor 0x540470 stores true into InputRequiredIsContinuous (+0x11), the GameAction
        // 0x36A31B5C (+0x14), true into OrientateFromVelocity (+0x19), AnimationCurve.Linear(0, 0,
        // 100, 100) into SpeedCurve (+0x38; the 100 is the float at 0x29A70CC) and true into
        // OffsetToTopOfCharacter (+0x44); everything else keeps its zero.
        [Test]
        public void AttachConstructorStoresTheNativeDefaults()
        {
            var attach = new CatchReleaseDefinition_Attach();
            Assert.That(attach.InputRequired, Is.False);
            Assert.That(attach.InputRequiredIsContinuous, Is.True);
            Assert.That((int)attach.InputGameAction, Is.EqualTo(0x36A31B5C));
            Assert.That(attach.IsDynamic, Is.False);
            Assert.That(attach.OrientateFromVelocity, Is.True);
            Assert.That(attach.UseSpeedMagnitude, Is.False);
            Assert.That(attach.SpeedMin, Is.Zero);
            Assert.That(attach.AlignmentDirection, Is.EqualTo(Vector3.zero));
            Assert.That(attach.AlignmentAngleTolerance, Is.Zero);
            Assert.That(attach.SpeedNormalised, Is.Zero);
            Assert.That(attach.SnapToAxis.SnapToX || attach.SnapToAxis.SnapToY || attach.SnapToAxis.SnapToZ, Is.False);
            Assert.That(attach.SnapToAttachPoint, Is.False);
            Assert.That(attach.OffsetToTopOfCharacter, Is.True);
            Assert.That(attach.AlignmentCosAngleTolerance, Is.Zero, "only CalculateCachedValues writes it");
            Assert.That(attach.SpeedCurve.length, Is.EqualTo(2));
            Assert.That((attach.SpeedCurve[0].time, attach.SpeedCurve[0].value), Is.EqualTo((0f, 0f)));
            Assert.That((attach.SpeedCurve[1].time, attach.SpeedCurve[1].value), Is.EqualTo((100f, 100f)));
            Assert.That(attach.SpeedCurve.Evaluate(50f), Is.EqualTo(50f).Within(1e-4f));
            Assert.That(new CatchReleaseDefinition_Attach().SpeedCurve, Is.Not.SameAs(attach.SpeedCurve), "one curve per instance");
        }

        // CalculateCachedValues 0x5402A0 multiplies AlignmentAngleTolerance (+0x2C) by the float at
        // 0x29A6C18 (0.017453292, degrees to radians), calls cosf and stores the result in the
        // backing field (+0x48). Cosines of 0, 60, 90 and 180 degrees; a cosine taken of the
        // unconverted tolerance (cos of 60 radians is -0.952) or of the half angle (0.866) does
        // not give 0.5.
        [TestCase(0f, 1f)]
        [TestCase(60f, 0.5f)]
        [TestCase(90f, 0f)]
        [TestCase(180f, -1f)]
        public void CachedAlignmentCosineIsTheCosineOfTheToleranceInDegrees(float degrees, float expected)
        {
            var attach = new CatchReleaseDefinition_Attach { AlignmentAngleTolerance = degrees };
            attach.CalculateCachedValues();
            Assert.That(attach.AlignmentCosAngleTolerance, Is.EqualTo(expected).Within(1e-6f));
        }

        // Constructor 0x540600: InputType 0xE2BF0AE1 (ToAir) at +0x10, SpeedCurve (+0x30)
        // Linear(0, 0, 100, 100), MultiplierCurve (+0x38) Linear(0, 1, 10, 1) with the float
        // 0x29A6E2C (10) and 0x29A6BE0 (1), ReturnSpeed 0x41200000 (10) at +0x48. The TransformPoints,
        // VelocityPoints and LeaveChecker references stay null. CalculateCachedValues 0x5402E0 is
        // a bare return.
        [Test]
        public void ReleaseConstructorStoresTheNativeDefaultsAndCachingChangesNothing()
        {
            var release = new CatchReleaseDefinition_Release();
            Assert.That((int)release.InputType, Is.EqualTo(unchecked((int)0xE2BF0AE1)));
            Assert.That(release.InputType, Is.EqualTo(CatchInputReleaseType.ToAir));
            Assert.That(release.TransformPoints, Is.Null);
            Assert.That(release.VelocityPoints, Is.Null);
            Assert.That(release.UseWorldSpeed, Is.False);
            Assert.That(release.ControlsDisabledTime, Is.Zero);
            Assert.That(release.VelocityLockTime, Is.Zero);
            Assert.That(release.ReturnSpeed, Is.EqualTo(10f));
            Assert.That(release.LeaveChecker, Is.Null);
            Assert.That((release.SpeedCurve.length, release.SpeedCurve[1].time, release.SpeedCurve[1].value), Is.EqualTo((2, 100f, 100f)));
            Assert.That(release.SpeedCurve.Evaluate(25f), Is.EqualTo(25f).Within(1e-4f));
            Assert.That((release.MultiplierCurve.length, release.MultiplierCurve[0].time, release.MultiplierCurve[0].value), Is.EqualTo((2, 0f, 1f)));
            Assert.That((release.MultiplierCurve[1].time, release.MultiplierCurve[1].value), Is.EqualTo((10f, 1f)));
            Assert.That(release.MultiplierCurve.Evaluate(4f), Is.EqualTo(1f).Within(1e-4f), "flat at 1 over the 10 seconds");
            Assert.That(release.MultiplierCurve, Is.Not.SameAs(release.SpeedCurve));

            release.ReturnSpeed = 3f;
            release.CalculateCachedValues();
            Assert.That(release.ReturnSpeed, Is.EqualTo(3f));
        }

        // Constructor 0x5404D0 builds a CatchPoint for +0x18 and another for +0x20 (each runs the
        // CatchPoint constructor 0x5406A0, which builds its Events); every Events builds three
        // UnityEvent<float> and a UnityEvent (0x540800). Nothing is shared.
        [Test]
        public void CatchBuildsSeparateCatchPointsAndEvents()
        {
            var catchDefinition = new CatchReleaseDefinition_Catch();
            Assert.That(catchDefinition.RemainTime, Is.Zero);
            Assert.That(catchDefinition.ReverseOnDirection, Is.False);
            Assert.That(catchDefinition.Connector, Is.Null);
            CatchReleaseDefinition_Catch.CatchPoint forward = catchDefinition.PointForward;
            CatchReleaseDefinition_Catch.CatchPoint reverse = catchDefinition.PointReverse;
            Assert.That(forward, Is.Not.Null);
            Assert.That(reverse, Is.Not.Null.And.Not.SameAs(forward));
            Assert.That(forward.Events, Is.Not.Null);
            Assert.That(reverse.Events, Is.Not.Null.And.Not.SameAs(forward.Events));
            Assert.That(forward.PositionOffset, Is.EqualTo(Vector3.zero));
            Assert.That(forward.AngleOffset, Is.EqualTo(Vector3.zero));
            Assert.That(forward.IgnoreRotation, Is.False);
            var events = new List<object>();
            foreach (CatchReleaseDefinition_Catch.Events set in new[] { forward.Events, reverse.Events })
                events.AddRange(new object[] { set.m_onEnter, set.m_onUpdateConstraints, set.m_onExit, set.m_onReset });
            Assert.That(events, Has.None.Null);
            Assert.That(events, Is.Unique, "eight separate UnityEvent objects");
            Assert.DoesNotThrow(catchDefinition.CalculateCachedValues, "0x5402D0 is a bare return");
        }

        // OnEnter 0x540700, OnUpdateConstraints 0x540750 and OnExit 0x5407A0 each invoke their
        // own UnityEvent<float> (+0x10, +0x18, +0x20) with the argument; OnReset 0x5407F0 invokes the
        // plain UnityEvent (+0x28).
        [Test]
        public void EventsRaiseTheirOwnEventWithTheirArgument()
        {
            var events = new CatchReleaseDefinition_Catch.Events();
            var log = new List<string>();
            events.m_onEnter.AddListener(value => log.Add("enter " + value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            events.m_onUpdateConstraints.AddListener(value => log.Add("constraints " + value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            events.m_onExit.AddListener(value => log.Add("exit " + value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            events.m_onReset.AddListener(() => log.Add("reset"));

            events.OnEnter(3.5f);
            Assert.That(log, Is.EqualTo(new[] { "enter 3.5" }));
            events.OnUpdateConstraints(1.25f);
            events.OnExit(-2f);
            events.OnReset();
            Assert.That(log, Is.EqualTo(new[] { "enter 3.5", "constraints 1.25", "exit -2", "reset" }));
        }
    }
}
