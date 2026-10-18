using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.CatchReleasePoints (TypeDef 0x02000abf, x86-64 0x6E38C0..0x6E5500). The
    // characters are real Character components (a probe subclass supplies the gravity direction,
    // which the shipped override reads from a RigidbodyGravity that Edit Mode does not build) on
    // owned objects, with the Rigidbody their position setter writes. Character's field
    // initialiser takes the GameplayIslandManager reference, so every case leases that row. The
    // points sit at x = 0, 10 and 20; their rotations are yaws of 0, 90 and 270 degrees, so their
    // forward vectors are +z, +x and -x. The search helpers are inlined into EvaluatePosition and
    // EvaluateRotation in the native code; the expected picks below are worked out by hand from the
    // helper bodies (0x6E3EB0 closest by squared distance, 0x6E3FA0 best forward dot with the last
    // of equals winning, 0x6E40F0 step along the points).
    public sealed class OriginalCatchReleasePointsTests
    {
        private sealed class Scene : IDisposable
        {
            private readonly VerificationFixtureOwnership.LifecycleRow m_islandRow;
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-catch-points");
            public readonly CatchReleasePoints Definition = new CatchReleasePoints();
            public readonly CharacterGravityProbe Character;
            public readonly Transform[] Points;

            public Scene(float[] yaws = null, bool withPoints = true)
            {
                m_islandRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>());
                try
                {
                    GameObject host = Objects.Create("character");
                    Character = host.AddComponent<CharacterGravityProbe>();
                    Set(Character, "m_body", host.AddComponent<Rigidbody>());
                    yaws = yaws ?? new[] { 0f, 90f, 270f };
                    Points = new Transform[withPoints ? 3 : 0];
                    for (int i = 0; i < Points.Length; i++)
                        Points[i] = Objects.Create("point" + i, new Vector3(10f * i, 0f, 0f), Quaternion.Euler(0f, yaws[i], 0f)).transform;
                    Set(Definition, "m_points", Points);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            // The character stands at x on the world axis and looks along +x unless told otherwise.
            public void Place(float x, Quaternion? rotation = null)
            {
                Character.SetWorldPosition(new Vector3(x, 0f, 0f));
                Character.SetWorldRotation(rotation ?? Quaternion.Euler(0f, 90f, 0f));
            }

            public void Types(CatchReleasePositionType position, CatchReleaseRotationType rotation)
            {
                Set(Definition, "m_releasePositionType", position);
                Set(Definition, "m_releaseRotationType", rotation);
            }

            public void Dispose()
            {
                Objects.Dispose();
                m_islandRow.Dispose();
            }
        }

        private static void AreSameRotation(Quaternion expected, Quaternion actual, string label)
        {
            Assert.That(Quaternion.Angle(expected, actual), Is.LessThan(0.01f), label + ": expected " + expected.eulerAngles + " but was " + actual.eulerAngles);
        }

        // Metadata of the TypeDef: serialisable, six fields in order, two property getters then
        // the two Evaluate methods, the four private helpers and the constructor.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(CatchReleasePoints).Attributes, Is.EqualTo(0x00102001));
            Assert.That(Fields(typeof(CatchReleasePoints)), Is.EqualTo(new[]
            {
                "m_points:Transform[]:0001", "m_releasePositionType:CatchReleasePositionType:0001",
                "m_releaseRotationType:CatchReleaseRotationType:0001", "m_lookupFromAttachAngle:AnimationCurve:0001",
                "m_lookupFromInputAngle:AnimationCurve:0001", "m_inverseRotationsWhenReversed:Boolean:0001"
            }));
            Assert.That(Methods(typeof(CatchReleasePoints)), Is.EqualTo(new[]
            {
                "get_HasPoints:0886", "get_IsPositionFree:0886", "EvaluatePosition:0086", "EvaluateRotation:0086",
                "FindClosestPoint:0081", "FindClosestDotProduct:0081", "FindNextPoint:0081",
                "CalculateCharacterSteeringRotation:0081", "CalculateTransformSteeringDirection:0081", ".ctor:1886"
            }));
            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(typeof(CatchReleasePoints).GetField("m_points", field)), Is.EqualTo(
                "SerializeField() TooltipAttribute(Transform points considered for release.)"));
            Assert.That(AttributeRow(typeof(CatchReleasePoints).GetField("m_releasePositionType", field)), Is.EqualTo(
                "TooltipAttribute(Determines the release logic for position.) SerializeField()"));
            string attachRow = AttributeRow(typeof(CatchReleasePoints).GetField("m_lookupFromAttachAngle", field));
            Assert.That(attachRow, Does.StartWith("SerializeField() ShowIfAttribute(m_releaseRotationType,"));
            Assert.That(attachRow, Does.EndWith("TooltipAttribute(Release angle from plane determined by the attach angle to plane.))"));
            Assert.That(typeof(CatchReleasePoints).GetMethod("FindClosestPoint", field).GetParameters()[1].IsOut, Is.True);
            Assert.That(typeof(CatchReleasePoints).GetMethod("CalculateCharacterSteeringRotation", field).GetParameters()[2].ParameterType.IsByRef, Is.True);
        }

        // Constructor 0x6E53B0: Array.Empty<Transform>() into m_points (+0x10), the 64-bit store of
        // Free (0x8444D383) into both type fields (+0x18, +0x1C) and two AnimationCurve.Linear(0, 0,
        // 360, 360) (the 360 is the float at 0x29A6E50) into +0x20 and +0x28.
        [Test]
        public void ConstructorStoresTheNativeDefaults()
        {
            var definition = new CatchReleasePoints();
            Assert.That(Get(definition, "m_points"), Is.SameAs(Array.Empty<Transform>()));
            Assert.That((int)(CatchReleasePositionType)Get(definition, "m_releasePositionType"), Is.EqualTo(unchecked((int)0x8444D383)));
            Assert.That((int)(CatchReleaseRotationType)Get(definition, "m_releaseRotationType"), Is.EqualTo(unchecked((int)0x8444D383)));
            Assert.That(Get(definition, "m_inverseRotationsWhenReversed"), Is.False);
            var attachCurve = (AnimationCurve)Get(definition, "m_lookupFromAttachAngle");
            var inputCurve = (AnimationCurve)Get(definition, "m_lookupFromInputAngle");
            Assert.That(inputCurve, Is.Not.SameAs(attachCurve));
            foreach (AnimationCurve curve in new[] { attachCurve, inputCurve })
            {
                Assert.That((curve.length, curve[0].time, curve[0].value, curve[1].time, curve[1].value), Is.EqualTo((2, 0f, 0f, 360f, 360f)));
                Assert.That(curve.Evaluate(90f), Is.EqualTo(90f).Within(1e-3f));
            }
        }

        // HasPoints 0x6E38C0 is m_points.Length != 0. IsPositionFree 0x6E38E0 is true without points
        // and otherwise m_releasePositionType == Free (0x8444D383, the cmp at 0x6E38EE).
        [Test]
        public void PointsAndPositionTypeDecideHasPointsAndIsPositionFree()
        {
            using (var scene = new Scene(withPoints: false))
            {
                foreach (CatchReleasePositionType type in new[] { CatchReleasePositionType.Free, CatchReleasePositionType.Next })
                {
                    scene.Types(type, CatchReleaseRotationType.Free);
                    Assert.That((scene.Definition.HasPoints, scene.Definition.IsPositionFree), Is.EqualTo((false, true)), "no points, " + type);
                }
            }
            using (var scene = new Scene())
            {
                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.Free);
                Assert.That((scene.Definition.HasPoints, scene.Definition.IsPositionFree), Is.EqualTo((true, true)));
                foreach (CatchReleasePositionType type in new[]
                {
                    CatchReleasePositionType.Last, CatchReleasePositionType.Next, CatchReleasePositionType.QuantizeClosestAngle,
                    CatchReleasePositionType.QuantizeClosestPosition
                })
                {
                    scene.Types(type, CatchReleaseRotationType.Free);
                    Assert.That((scene.Definition.HasPoints, scene.Definition.IsPositionFree), Is.EqualTo((true, false)), "points, " + type);
                }
            }
        }

        // EvaluatePosition 0x6E3900: the character position without points or for Free; the closest
        // active point by squared distance for QuantizeClosestPosition (the 0x6E3EB0 search).
        [Test]
        public void FreeAndClosestPositionTypesPickThePositionOrTheNearestActivePoint()
        {
            using (var scene = new Scene())
            {
                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.Free);
                scene.Place(12f);
                AreClose(new Vector3(12f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "Free");

                scene.Types(CatchReleasePositionType.QuantizeClosestPosition, CatchReleaseRotationType.Free);
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "12 is nearest 10");
                scene.Place(16f);
                AreClose(new Vector3(20f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, true), "16 is nearer 20 than 10; reversed is ignored");
                scene.Place(12f);
                scene.Points[1].gameObject.SetActive(false);
                AreClose(new Vector3(20f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "the inactive point 10 is skipped");
            }
            using (var scene = new Scene(withPoints: false))
            {
                scene.Types(CatchReleasePositionType.QuantizeClosestPosition, CatchReleaseRotationType.Free);
                scene.Place(7f);
                AreClose(new Vector3(7f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "no points: the character position");
            }
        }

        // Last and Next both start from the closest point and compare the character with it along
        // its forward (0x6E40F0): behind (or level with, for Next) the closest point the closest point
        // stays; past it the choice steps one point back (Last) or on (Next), and a reversed catch steps
        // the other way. The index is clamped to the ends and an inactive target keeps the closest point.
        [Test]
        public void LastAndNextStepAlongThePointsFromTheClosestOne()
        {
            using (var scene = new Scene())
            {
                // Looking along +x at x = 12: past the closest point 10.
                scene.Place(12f);
                scene.Types(CatchReleasePositionType.Next, CatchReleaseRotationType.Free);
                AreClose(new Vector3(20f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "Next past 10");
                AreClose(new Vector3(0f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, true), "Next past 10, reversed");
                scene.Types(CatchReleasePositionType.Last, CatchReleaseRotationType.Free);
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "Last past 10 is 10");
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, true), "Last past 10 is 10, reversed");

                // At x = 8 the character has not reached the closest point 10.
                scene.Place(8f);
                scene.Types(CatchReleasePositionType.Next, CatchReleaseRotationType.Free);
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "Next before 10 is 10");
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, true), "Next before 10 is 10, reversed");
                scene.Types(CatchReleasePositionType.Last, CatchReleaseRotationType.Free);
                AreClose(new Vector3(0f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "Last before 10 is 0");
                AreClose(new Vector3(20f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, true), "Last before 10, reversed");

                // The ends clamp the index.
                scene.Place(22f);
                scene.Types(CatchReleasePositionType.Next, CatchReleaseRotationType.Free);
                AreClose(new Vector3(20f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "Next past the last point");
                scene.Place(-5f);
                scene.Types(CatchReleasePositionType.Last, CatchReleaseRotationType.Free);
                AreClose(new Vector3(0f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "Last before the first point");

                // An inactive point is never stepped onto.
                scene.Place(12f);
                scene.Types(CatchReleasePositionType.Next, CatchReleaseRotationType.Free);
                scene.Points[2].gameObject.SetActive(false);
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "Next onto an inactive point stays on 10");
            }
        }

        // QuantizeClosestAngle takes the point whose forward has the largest dot product with the
        // character forward; the last of equal dots wins (the ja at 0x6E4054 skips only a larger
        // best). A reversed catch with inverted rotations looks for the largest negated dot instead.
        [Test]
        public void QuantizeClosestAngleTakesTheBestAlignedPointAndTheLastOfEquals()
        {
            using (var scene = new Scene())
            {
                scene.Place(0f);
                scene.Types(CatchReleasePositionType.QuantizeClosestAngle, CatchReleaseRotationType.Free);
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "forward +x matches the point at 10");
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, true), "reversed without inversion");
                Set(scene.Definition, "m_inverseRotationsWhenReversed", true);
                AreClose(new Vector3(10f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "inversion needs a reversed catch");
                AreClose(new Vector3(20f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, true), "reversed with inversion picks the point facing -x");
            }
            using (var scene = new Scene(new[] { 0f, 90f, 90f }))
            {
                scene.Place(0f);
                scene.Types(CatchReleasePositionType.QuantizeClosestAngle, CatchReleaseRotationType.Free);
                AreClose(new Vector3(20f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "equal dots: the later point");
            }
        }

        // The default branch of the switch (0x6E3A2B) throws ArgumentOutOfRangeException with the
        // boxed value, but only once there are points; without points the position is returned first.
        [Test]
        public void AnUnsupportedPositionTypeThrowsOnlyWithPoints()
        {
            using (var scene = new Scene())
            {
                scene.Place(3f);
                scene.Types((CatchReleasePositionType)7, CatchReleaseRotationType.Free);
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => scene.Definition.EvaluatePosition(scene.Character, false));
                Assert.That(thrown.ParamName, Is.EqualTo("m_releasePositionType"));
                Assert.That(thrown.ActualValue, Is.EqualTo((CatchReleasePositionType)7));
                Assert.That(thrown.Message, Does.Contain("Is not supported in CatchReleasePoints"));
            }
            using (var scene = new Scene(withPoints: false))
            {
                scene.Place(3f);
                scene.Types((CatchReleasePositionType)7, CatchReleaseRotationType.Free);
                AreClose(new Vector3(3f, 0f, 0f), scene.Definition.EvaluatePosition(scene.Character, false), "no points");
            }
        }

        // EvaluateRotation 0x6E4280: the character rotation for Free and for any type without points;
        // otherwise the rotation of the point chosen exactly as for the position.
        [Test]
        public void RotationTypesReturnTheRotationOfTheChosenPoint()
        {
            using (var scene = new Scene())
            {
                Quaternion characterRotation = Quaternion.Euler(0f, 90f, 0f);
                scene.Place(12f, characterRotation);
                Quaternion yaw0 = Quaternion.identity, yaw90 = Quaternion.Euler(0f, 90f, 0f), yaw270 = Quaternion.Euler(0f, 270f, 0f);
                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.Free);
                AreSameRotation(characterRotation, scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null), "Free");

                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.QuantizeClosestPosition);
                AreSameRotation(yaw90, scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null), "closest point 10");
                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.Next);
                AreSameRotation(yaw270, scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null), "Next past 10 is the point at 20");
                AreSameRotation(yaw0, scene.Definition.EvaluateRotation(scene.Character, true, Vector3.forward, null), "Next past 10 reversed is the point at 0");
                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.Last);
                AreSameRotation(yaw90, scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null), "Last past 10 is 10");
                scene.Place(8f, characterRotation);
                AreSameRotation(yaw0, scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null), "Last before 10 is the point at 0");
                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.QuantizeClosestAngle);
                AreSameRotation(yaw90, scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null), "best aligned point");
            }
            using (var scene = new Scene(withPoints: false))
            {
                Quaternion characterRotation = Quaternion.Euler(0f, 30f, 0f);
                scene.Place(0f, characterRotation);
                foreach (CatchReleaseRotationType type in new[]
                {
                    CatchReleaseRotationType.QuantizeClosestPosition, CatchReleaseRotationType.Next, (CatchReleaseRotationType)7
                })
                {
                    scene.Types(CatchReleasePositionType.Free, type);
                    AreSameRotation(characterRotation, scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null), "no points, " + type);
                }
            }
        }

        // 0x6E4917: a reversed catch with m_inverseRotationsWhenReversed (+0x30) returns the point
        // rotation times AngleAxis(180, character.GravityNormalised) (the virtual call through vtable
        // offset 0x248). With a sideways gravity of +x the order matters: yaw 90 times a half turn
        // about x points along -x, whereas the half turn first would leave +x.
        [Test]
        public void AReversedInvertedCatchTurnsThePointRotationAHalfTurnAboutGravity()
        {
            using (var scene = new Scene())
            {
                scene.Character.Down = Vector3.right;
                scene.Place(10f);
                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.QuantizeClosestPosition);
                Quaternion plain = scene.Definition.EvaluateRotation(scene.Character, true, Vector3.forward, null);
                AreClose(Vector3.right, plain * Vector3.forward, "reversed without inversion keeps the point rotation");
                Set(scene.Definition, "m_inverseRotationsWhenReversed", true);
                AreClose(Vector3.right, scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null) * Vector3.forward,
                    "inversion needs a reversed catch");
                Quaternion flipped = scene.Definition.EvaluateRotation(scene.Character, true, Vector3.forward, null);
                AreClose(Vector3.left, flipped * Vector3.forward, "yaw 90 * half turn about +x");
            }
        }

        // An unsupported rotation type throws (0x6E4B65) only after the no-points exit.
        [Test]
        public void AnUnsupportedRotationTypeThrowsWithPoints()
        {
            using (var scene = new Scene())
            {
                scene.Place(3f);
                scene.Types(CatchReleasePositionType.Free, (CatchReleaseRotationType)7);
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null));
                Assert.That(thrown.ParamName, Is.EqualTo("m_releaseRotationType"));
                Assert.That(thrown.ActualValue, Is.EqualTo((CatchReleaseRotationType)7));
                Assert.That(thrown.Message, Does.Contain("Is not supported in CatchReleasePoints"));
            }
        }

        // CalculateCharacterSteeringRotation 0x6E4BF0 (reached by FreeLookup): with no steering input
        // (approximately zero squared magnitude) it returns false and leaves the rotation; otherwise
        // rotation = character.WorldRotation * AngleAxis(DeltaAngle(SignedAngle(cameraForward,
        // forward projected on the up plane, up), lookupFromInput(turn)), up) *
        // AngleAxis(lookupFromAttach(Angle(attachDirection, cameraForward)), -right), the product order
        // taken from the SSE bodies at 0x6E4F93..0x6E50CC. Both curves are the identity lookup.
        [Test]
        public void FreeLookupSteersTheCharacterRotationFromTheInputAndTheAttachAngle()
        {
            using (var scene = new Scene())
            {
                var tracking = new SteeringTrackingProbe(scene.Character);
                ((System.Collections.IDictionary)Get(scene.Character, "m_trackingTypes")).Add(tracking.Type, tracking);
                scene.Character.SetTrackingType(tracking.Type);
                Set(scene.Character, "m_cameraForwardOnCharacterPlane", Vector3.forward);
                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.FreeLookup);

                // No input: nothing is written and, with the points present, the closest point decides.
                scene.Place(10f, Quaternion.identity);
                tracking.InputForward = Vector3.zero;
                AreSameRotation(Quaternion.Euler(0f, 90f, 0f), scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null),
                    "no input: the closest point (yaw 90)");
                Assert.That(tracking.SteeringReads, Is.EqualTo(1));
                var untouched = Quaternion.Euler(10f, 20f, 30f);
                Assert.That(Call(scene.Definition, "CalculateCharacterSteeringRotation", scene.Character, Vector3.forward, untouched), Is.False);

                // A turn of 90 degrees with the attach direction along the camera forward (angle 0).
                tracking.InputForward = new Vector3(0f, 0f, 1f);
                tracking.InputTurn = 90f;
                AreSameRotation(Quaternion.Euler(0f, 90f, 0f), scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null),
                    "turn 90, attach angle 0");

                // An attach direction a quarter turn from the camera forward tilts the result up by 90.
                Quaternion tilted = scene.Definition.EvaluateRotation(scene.Character, false, Vector3.up, null);
                AreClose(Vector3.up, tilted * Vector3.forward, "tilt 90 about -right then yaw 90");
                AreClose(Vector3.left, tilted * Vector3.up, "the up vector follows the same two turns");

                // The character already turned 45 degrees from the camera forward: the delta is 45.
                scene.Place(10f, Quaternion.Euler(0f, 45f, 0f));
                AreSameRotation(Quaternion.Euler(0f, 90f, 0f), scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, null),
                    "world rotation 45 plus delta 45");
            }
        }

        // CalculateTransformSteeringDirection 0x6E5100 (reached by ClosestRotationLookup): without
        // input it returns the character up; otherwise LookRotation(cameraForward, attachTransform.up) *
        // AngleAxis(lookupFromInput(turn), up) * forward (product order from 0x6E5288..0x6E5341). The
        // point whose forward best matches that direction then supplies the rotation.
        [Test]
        public void ClosestRotationLookupPicksThePointFacingTheSteeredDirection()
        {
            using (var scene = new Scene())
            {
                var tracking = new SteeringTrackingProbe(scene.Character);
                ((System.Collections.IDictionary)Get(scene.Character, "m_trackingTypes")).Add(tracking.Type, tracking);
                scene.Character.SetTrackingType(tracking.Type);
                Set(scene.Character, "m_cameraForwardOnCharacterPlane", Vector3.forward);
                scene.Place(10f, Quaternion.identity);
                Transform attach = scene.Objects.Create("attach").transform;

                tracking.InputForward = Vector3.zero;
                AreClose(Vector3.up, (Vector3)Call(scene.Definition, "CalculateTransformSteeringDirection", scene.Character, attach), "no input: character up");

                tracking.InputForward = Vector3.forward;
                tracking.InputTurn = 90f;
                AreClose(Vector3.right, (Vector3)Call(scene.Definition, "CalculateTransformSteeringDirection", scene.Character, attach), "turn 90 about up");
                tracking.InputTurn = 270f;
                AreClose(Vector3.left, (Vector3)Call(scene.Definition, "CalculateTransformSteeringDirection", scene.Character, attach), "turn 270");

                scene.Types(CatchReleasePositionType.Free, CatchReleaseRotationType.ClosestRotationLookup);
                tracking.InputTurn = 90f;
                AreSameRotation(Quaternion.Euler(0f, 90f, 0f), scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, attach),
                    "the point facing +x");
                tracking.InputTurn = 270f;
                AreSameRotation(Quaternion.Euler(0f, 270f, 0f), scene.Definition.EvaluateRotation(scene.Character, false, Vector3.forward, attach),
                    "the point facing -x");
            }
        }
    }
}
