using System;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplinePoint. Expected values are derived from the x86-64 bodies (addresses in each comment) and from the
    // arithmetic of the shipped formulas, not read back from the restored source. Where a plausible alternative would give a
    // different answer, the inputs are chosen to tell the two apart.
    public sealed class OriginalDreamteckSplinePointTests
    {
        private const float Tolerance = 1e-4f;

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private static SplinePoint BrokenPoint(Vector3 position, Vector3 tangent, Vector3 tangent2)
        {
            return new SplinePoint(position, tangent, tangent2, Vector3.up, 1f, Color.white);
        }

        private delegate void Setter(ref SplinePoint point, float value);

        // Original set_type 0x4deb20 compares the old and new type with setne and stores the result in isDirty (so an unchanged
        // type CLEARS the flag), stores the type, and only for SmoothMirrored re-mirrors tangent2 (0x4deb33), which sets isDirty.
        [Test]
        public void TypeSetterOverwritesTheDirtyFlagWithTheChangeAndOnlyMirrorsForSmoothMirrored()
        {
            SplinePoint point = BrokenPoint(Vector3.zero, new Vector3(-3f, 0f, 0f), new Vector3(0f, 5f, 0f));
            Assert.That(point.type, Is.EqualTo(SplinePoint.Type.Broken));
            Assert.That(point.isDirty, Is.False);

            point.isDirty = true;
            point.type = SplinePoint.Type.Broken;
            Assert.That(point.isDirty, Is.False, "an unchanged type overwrites a dirty flag with false");

            point.type = SplinePoint.Type.SmoothFree;
            Assert.That(point.isDirty, Is.True, "a changed type is dirty");
            Near(point.tangent2, new Vector3(0f, 5f, 0f), "switching to SmoothFree leaves tangent2 alone");

            point.isDirty = true;
            point.type = SplinePoint.Type.SmoothFree;
            Assert.That(point.isDirty, Is.False, "an unchanged SmoothFree overwrites the flag too");

            point.type = SplinePoint.Type.SmoothMirrored;
            Assert.That(point.isDirty, Is.True);
            Near(point.tangent2, new Vector3(3f, 0f, 0f), "tangent2 = position + (position - tangent)");

            point.isDirty = false;
            point.type = SplinePoint.Type.SmoothMirrored;
            Assert.That(point.isDirty, Is.True, "re-assigning SmoothMirrored still re-mirrors, which marks the point dirty");
        }

        // Original constructors 0x4e1700 (p), 0x4e17c0 (p, t), 0x4e1890 (pos, tan, nor, s, col): SmoothMirrored, tangent2 derived as
        // (position - tangent) + position by the SmoothMirrorTangent2 body (0x4e07e0), isDirty true, white / up / 1 defaults.
        [Test]
        public void MirroredConstructorsDeriveTangent2FromTheTangent()
        {
            Vector3 p = new Vector3(1f, 2f, 3f);

            SplinePoint single = new SplinePoint(p);
            Near(single.position, p, "position");
            Near(single.tangent, p, "tangent");
            Near(single.tangent2, p, "tangent2 mirrors a tangent that sits on the position");
            Assert.That(single.color, Is.EqualTo(Color.white));
            Near(single.normal, Vector3.up, "normal");
            Assert.That(single.size, Is.EqualTo(1f));
            Assert.That(single.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Assert.That(single.isDirty, Is.True);

            SplinePoint withTangent = new SplinePoint(p, new Vector3(3f, 2f, 1f));
            Near(withTangent.tangent, new Vector3(3f, 2f, 1f), "tangent");
            Near(withTangent.tangent2, new Vector3(-1f, 2f, 5f), "2 * position - tangent");
            Assert.That(withTangent.color, Is.EqualTo(Color.white));
            Near(withTangent.normal, Vector3.up, "normal");
            Assert.That(withTangent.size, Is.EqualTo(1f));
            Assert.That(withTangent.isDirty, Is.True);

            SplinePoint full = new SplinePoint(p, new Vector3(2f, 2f, 3f), new Vector3(0f, 0f, 1f), 0.5f, Color.red);
            Near(full.tangent2, new Vector3(0f, 2f, 3f), "2 * position - tangent");
            Near(full.normal, new Vector3(0f, 0f, 1f), "normal");
            Assert.That(full.size, Is.EqualTo(0.5f));
            Assert.That(full.color, Is.EqualTo(Color.red));
            Assert.That(full.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Assert.That(full.isDirty, Is.True);
        }

        // Original constructor 0x4e18f0 stores the given tangent2 and makes a clean Broken point.
        [Test]
        public void ExplicitTangent2ConstructorMakesACleanBrokenPoint()
        {
            SplinePoint point = new SplinePoint(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 9f), new Vector3(0f, 0f, -1f), 2f, Color.green);
            Near(point.position, new Vector3(1f, 2f, 3f), "position");
            Near(point.tangent, new Vector3(4f, 5f, 6f), "tangent");
            Near(point.tangent2, new Vector3(7f, 8f, 9f), "tangent2");
            Near(point.normal, new Vector3(0f, 0f, -1f), "normal");
            Assert.That(point.size, Is.EqualTo(2f));
            Assert.That(point.color, Is.EqualTo(Color.green));
            Assert.That(point.type, Is.EqualTo(SplinePoint.Type.Broken));
            Assert.That(point.isDirty, Is.False);
        }

        // Original copy constructor 0x4e1940 copies the fields, starts clean, and (0x4e1aa8..0x4e1ade) re-derives tangent2 for
        // SmoothMirrored (dirty) and SmoothFree (SmoothFreeTangent2) sources but not for Broken ones.
        [Test]
        public void CopyConstructorStartsCleanAndRederivesTangent2ForSmoothTypes()
        {
            SplinePoint broken = BrokenPoint(new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(9f, 9f, 9f));
            broken.SetPositionX(5f);
            Assert.That(broken.isDirty, Is.True);
            SplinePoint brokenCopy = new SplinePoint(broken);
            Assert.That(brokenCopy.isDirty, Is.False, "a copy never inherits the dirty flag");
            Near(brokenCopy.tangent2, new Vector3(9f, 9f, 9f), "Broken keeps tangent2");
            Near(brokenCopy.position, new Vector3(5f, 0f, 0f), "position");
            Assert.That(brokenCopy.type, Is.EqualTo(SplinePoint.Type.Broken));

            SplinePoint mirrored = new SplinePoint(new Vector3(1f, 2f, 3f), new Vector3(3f, 2f, 1f));
            mirrored.tangent2 = new Vector3(9f, 9f, 9f);
            SplinePoint mirroredCopy = new SplinePoint(mirrored);
            Near(mirroredCopy.tangent2, new Vector3(-1f, 2f, 5f), "SmoothMirrored re-mirrors an inconsistent tangent2");
            Assert.That(mirroredCopy.isDirty, Is.True);

            SplinePoint free = BrokenPoint(Vector3.zero, new Vector3(-3f, 0f, 0f), new Vector3(0f, 8f, 0f));
            free.type = SplinePoint.Type.SmoothFree;
            SplinePoint freeCopy = new SplinePoint(free);
            Near(freeCopy.tangent2, new Vector3(8f, 0f, 0f), "SmoothFree keeps tangent2's length (8) in the direction opposite to tangent");
            Assert.That(freeCopy.isDirty, Is.True);
        }

        // Original SetPosition 0x4e1200 subtracts the position change from both tangents and writes the position without touching
        // isDirty.
        [Test]
        public void SetPositionMovesBothTangentsAndLeavesTheDirtyFlag()
        {
            SplinePoint point = BrokenPoint(new Vector3(1f, 1f, 1f), new Vector3(2f, 1f, 1f), new Vector3(0f, 3f, 1f));
            point.SetPosition(new Vector3(4f, 5f, 6f));
            Near(point.position, new Vector3(4f, 5f, 6f), "position");
            Near(point.tangent, new Vector3(5f, 5f, 6f), "tangent moves with the position");
            Near(point.tangent2, new Vector3(3f, 7f, 6f), "tangent2 moves with the position");
            Assert.That(point.isDirty, Is.False);
        }

        // Original SetTangentPosition 0x4deb70 / SetTangent2Position 0x4debc0: store the tangent, then SmoothMirrored mirrors the
        // other tangent (marking the point dirty), SmoothFree keeps the other tangent's length along the opposite direction (also
        // dirty) and Broken touches nothing else.
        [Test]
        public void TangentSettersFollowThePointType()
        {
            SplinePoint mirrored = new SplinePoint(Vector3.zero, new Vector3(-1f, 0f, 0f));
            mirrored.isDirty = false;
            mirrored.SetTangentPosition(new Vector3(2f, 0f, 0f));
            Near(mirrored.tangent2, new Vector3(-2f, 0f, 0f), "SmoothMirrored mirrors tangent2");
            Assert.That(mirrored.isDirty, Is.True);
            mirrored.isDirty = false;
            mirrored.SetTangent2Position(new Vector3(0f, 4f, 0f));
            Near(mirrored.tangent, new Vector3(0f, -4f, 0f), "SmoothMirrored mirrors tangent");
            Assert.That(mirrored.isDirty, Is.True);

            SplinePoint free = BrokenPoint(Vector3.zero, new Vector3(-1f, 0f, 0f), new Vector3(0f, 5f, 0f));
            free.type = SplinePoint.Type.SmoothFree;
            free.isDirty = false;
            free.SetTangentPosition(new Vector3(0f, 0f, -2f));
            Near(free.tangent2, new Vector3(0f, 0f, 5f), "SmoothFree keeps |tangent2| = 5 and flips the direction");
            Assert.That(free.isDirty, Is.True);
            free.isDirty = false;
            free.SetTangent2Position(new Vector3(3f, 0f, 0f));
            Near(free.tangent, new Vector3(-2f, 0f, 0f), "SmoothFree keeps |tangent| = 2 and flips the direction");
            Assert.That(free.isDirty, Is.True);

            SplinePoint broken = BrokenPoint(Vector3.zero, new Vector3(-1f, 0f, 0f), new Vector3(0f, 5f, 0f));
            broken.SetTangentPosition(new Vector3(0f, 0f, -2f));
            broken.SetTangent2Position(new Vector3(3f, 0f, 0f));
            Near(broken.tangent, new Vector3(0f, 0f, -2f), "Broken keeps the stored tangent");
            Near(broken.tangent2, new Vector3(3f, 0f, 0f), "Broken keeps the stored tangent2");
            Assert.That(broken.isDirty, Is.False, "Broken tangent setters never mark the point dirty");
        }

        // Original SetPositionX..SetColorA 0x4e1c50..0x4e1f20: the flag is set only when the exact float value changes.
        [Test]
        public void ComponentSettersMarkTheSelectedFieldAndOnlyDirtyOnAChange()
        {
            (string name, Setter apply, Func<SplinePoint, float> read)[] rows =
            {
                ("SetPositionX", (ref SplinePoint p, float v) => p.SetPositionX(v), p => p.position.x),
                ("SetPositionY", (ref SplinePoint p, float v) => p.SetPositionY(v), p => p.position.y),
                ("SetPositionZ", (ref SplinePoint p, float v) => p.SetPositionZ(v), p => p.position.z),
                ("SetTangentX", (ref SplinePoint p, float v) => p.SetTangentX(v), p => p.tangent.x),
                ("SetTangentY", (ref SplinePoint p, float v) => p.SetTangentY(v), p => p.tangent.y),
                ("SetTangentZ", (ref SplinePoint p, float v) => p.SetTangentZ(v), p => p.tangent.z),
                ("SetTangent2X", (ref SplinePoint p, float v) => p.SetTangent2X(v), p => p.tangent2.x),
                ("SetTangent2Y", (ref SplinePoint p, float v) => p.SetTangent2Y(v), p => p.tangent2.y),
                ("SetTangent2Z", (ref SplinePoint p, float v) => p.SetTangent2Z(v), p => p.tangent2.z),
                ("SetNormalX", (ref SplinePoint p, float v) => p.SetNormalX(v), p => p.normal.x),
                ("SetNormalY", (ref SplinePoint p, float v) => p.SetNormalY(v), p => p.normal.y),
                ("SetNormalZ", (ref SplinePoint p, float v) => p.SetNormalZ(v), p => p.normal.z),
                ("SetColorR", (ref SplinePoint p, float v) => p.SetColorR(v), p => p.color.r),
                ("SetColorG", (ref SplinePoint p, float v) => p.SetColorG(v), p => p.color.g),
                ("SetColorB", (ref SplinePoint p, float v) => p.SetColorB(v), p => p.color.b),
                ("SetColorA", (ref SplinePoint p, float v) => p.SetColorA(v), p => p.color.a),
            };

            foreach (var row in rows)
            {
                SplinePoint point = new SplinePoint(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 9f), new Vector3(0.1f, 0.2f, 0.3f), 1f, new Color(0.1f, 0.2f, 0.3f, 0.4f));
                SplinePoint before = point;
                float unchanged = row.read(point);
                row.apply(ref point, unchanged);
                Assert.That(point.isDirty, Is.False, row.name + " with the same value stays clean");
                row.apply(ref point, unchanged + 0.5f);
                Assert.That(row.read(point), Is.EqualTo(unchanged + 0.5f), row.name + " stores the value");
                Assert.That(point.isDirty, Is.True, row.name + " with a new value is dirty");
                row.apply(ref point, unchanged);
                Assert.That(point.isDirty, Is.True, row.name + " never clears the flag");
                Assert.That(point == before, Is.True, row.name + " touched only its own component");
            }
        }

        // Original Flatten 0x4e1b00: FlattenVector on position, tangent and tangent2, then the normal becomes the world axis
        // (X right, Y up, Z forward); isDirty is not touched.
        [Test]
        public void FlattenReplacesOneComponentEverywhereAndAimsTheNormalAtTheAxis()
        {
            SplinePoint Fresh()
            {
                return BrokenPoint(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 9f));
            }

            SplinePoint x = Fresh();
            x.Flatten(LinearAlgebraUtility.Axis.X, 10f);
            Near(x.position, new Vector3(10f, 2f, 3f), "X position");
            Near(x.tangent, new Vector3(10f, 5f, 6f), "X tangent");
            Near(x.tangent2, new Vector3(10f, 8f, 9f), "X tangent2");
            Near(x.normal, Vector3.right, "X normal");

            SplinePoint y = Fresh();
            y.Flatten(LinearAlgebraUtility.Axis.Y);
            Near(y.position, new Vector3(1f, 0f, 3f), "Y position flattens to the default 0");
            Near(y.tangent, new Vector3(4f, 0f, 6f), "Y tangent");
            Near(y.tangent2, new Vector3(7f, 0f, 9f), "Y tangent2");
            Near(y.normal, Vector3.up, "Y normal");

            SplinePoint z = Fresh();
            z.Flatten(LinearAlgebraUtility.Axis.Z, -2f);
            Near(z.position, new Vector3(1f, 2f, -2f), "Z position");
            Near(z.tangent, new Vector3(4f, 5f, -2f), "Z tangent");
            Near(z.tangent2, new Vector3(7f, 8f, -2f), "Z tangent2");
            Near(z.normal, Vector3.forward, "Z normal");
            Assert.That(z.isDirty, Is.False, "Flatten does not mark the point dirty");
        }

        // Original Lerp 0x4e0820: the result starts as a copy of a, its type goes through the type setter (Broken if either is
        // Broken, else SmoothFree if either is SmoothFree, else SmoothMirrored), and the setter's dirty rule decides isDirty:
        // (a.type != result type), forced true for SmoothMirrored by the re-mirror.
        [Test]
        public void LerpPicksTheStrongestTypeAndDerivesTheDirtyFlagFromTheSetter()
        {
            const SplinePoint.Type m = SplinePoint.Type.SmoothMirrored;
            const SplinePoint.Type b = SplinePoint.Type.Broken;
            const SplinePoint.Type f = SplinePoint.Type.SmoothFree;
            (SplinePoint.Type a, SplinePoint.Type b, SplinePoint.Type result, bool dirty)[] truthTable =
            {
                (m, m, m, true),
                (m, b, b, true),
                (m, f, f, true),
                (b, m, b, false),
                (b, b, b, false),
                (b, f, b, false),
                (f, m, f, false),
                (f, b, b, true),
                (f, f, f, false),
            };

            foreach (var row in truthTable)
            {
                SplinePoint first = BrokenPoint(Vector3.zero, Vector3.left, Vector3.up);
                SplinePoint second = BrokenPoint(Vector3.right, Vector3.up, Vector3.forward);
                first.type = row.a;
                second.type = row.b;
                first.isDirty = false;
                SplinePoint result = SplinePoint.Lerp(first, second, 0.5f);
                Assert.That(result.type, Is.EqualTo(row.result), row.a + " + " + row.b + " type");
                Assert.That(result.isDirty, Is.EqualTo(row.dirty), row.a + " + " + row.b + " dirty");
            }
        }

        // Original Lerp 0x4e0820 and GetInterpolatedTangents 0x4e0a80: position, color, normal (Slerp) and size use clamped
        // interpolators; the tangents are the de Casteljau split of the cubic a.position, a.tangent2, b.tangent, b.position at the
        // UNCLAMPED t (incoming tangent from the first half, tangent2 from the second).
        [Test]
        public void LerpInterpolatesFieldsAndSplitsTheCubicForTheTangents()
        {
            SplinePoint a = new SplinePoint(Vector3.zero, new Vector3(-1f, 0f, 0f), new Vector3(0f, 3f, 0f), Vector3.up, 1f, Color.black);
            SplinePoint b = new SplinePoint(new Vector3(6f, 0f, 0f), new Vector3(6f, 3f, 0f), new Vector3(7f, 0f, 0f), Vector3.right, 3f, Color.white);

            SplinePoint half = SplinePoint.Lerp(a, b, 0.5f);
            Near(half.position, new Vector3(3f, 0f, 0f), "position");
            Assert.That(half.color, Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1f)));
            Assert.That(half.size, Is.EqualTo(2f));
            Near(half.normal, new Vector3(0.70710678f, 0.70710678f, 0f), "normal slerps (a lerp would give 0.5, 0.5)");
            Near(half.tangent, new Vector3(1.5f, 2.25f, 0f), "de Casteljau point at the end of the a-side half");
            Near(half.tangent2, new Vector3(4.5f, 2.25f, 0f), "de Casteljau point at the start of the b-side half");

            SplinePoint beyond = SplinePoint.Lerp(a, b, 2f);
            Near(beyond.position, new Vector3(6f, 0f, 0f), "position clamps t at 1");
            Assert.That(beyond.color, Is.EqualTo(Color.white));
            Assert.That(beyond.size, Is.EqualTo(3f));
            Near(beyond.normal, Vector3.right, "normal clamps t at 1");
            Near(beyond.tangent, new Vector3(24f, 0f, 0f), "tangents extrapolate with the unclamped t = 2");
            Near(beyond.tangent2, new Vector3(0f, -9f, 0f), "tangent2 extrapolates too");
        }

        // Original Equals / EqualsFast / ==, != 0x4e0b70..0x4e1060: Unity vector and color equality (squared difference below 1e-10,
        // so 7e-6 is equal and 1.5e-5 is not), exact size and the type; isDirty is ignored.
        [Test]
        public void EqualityUsesUnityToleranceExactSizeAndType()
        {
            SplinePoint a = new SplinePoint(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 9f), new Vector3(0f, 1f, 0f), 2f, new Color(0.25f, 0.5f, 0.75f, 1f));
            SplinePoint b = a;
            b.isDirty = !a.isDirty;
            Assert.That(a == b, Is.True);
            Assert.That(a != b, Is.False);
            Assert.That(a.Equals((object)b), Is.True);

            (string name, Func<SplinePoint, float, SplinePoint> shift)[] fields =
            {
                ("position", (p, d) => { p.position.x += d; return p; }),
                ("tangent", (p, d) => { p.tangent.y += d; return p; }),
                ("tangent2", (p, d) => { p.tangent2.z += d; return p; }),
                ("normal", (p, d) => { p.normal.y += d; return p; }),
                ("color", (p, d) => { p.color.g += d; return p; }),
            };
            foreach (var field in fields)
            {
                SplinePoint within = field.shift(a, 7e-6f);
                SplinePoint outside = field.shift(a, 1.5e-5f);
                Assert.That(a == within, Is.True, field.name + " within tolerance");
                Assert.That(a != within, Is.False, field.name + " within tolerance (!=)");
                Assert.That(a == outside, Is.False, field.name + " outside tolerance");
                Assert.That(a != outside, Is.True, field.name + " outside tolerance (!=)");
                Assert.That(a.Equals((object)outside), Is.False, field.name + " outside tolerance (Equals)");
            }

            SplinePoint sizeChanged = a;
            sizeChanged.size = 2f + 1e-6f;
            Assert.That(a == sizeChanged, Is.False, "size is compared exactly");

            SplinePoint typeChanged = a;
            typeChanged.type = SplinePoint.Type.SmoothFree;
            Assert.That(a == typeChanged, Is.False, "type is compared");

            Assert.That(a.Equals(null), Is.False);
            Assert.That(a.Equals("point"), Is.False);
            Assert.That(a.Equals((object)new Vector3(1f, 2f, 3f)), Is.False);
        }

        // Original GetHashCode 0x4e1f50: (((((type * 391 + position) * 23 + normal) * 23 + tangent) * 23 + tangent2) * 23 + color)
        // * 23 + size + 0x1aa55747 with Unity's Vector3 (x ^ y << 2 ^ z >> 2) and Color (r ^ g << 2 ^ b >> 2 ^ a >> 1) hashes over
        // the raw float bits. The literal below was computed from that formula outside the restored source.
        [Test]
        public void HashCodeCombinesTheFieldsWithTheNativeMultipliers()
        {
            SplinePoint point = new SplinePoint(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 9f), new Vector3(0f, 1f, 0f), 2f, new Color(0.25f, 0.5f, 0.75f, 1f));
            Assert.That(point.GetHashCode(), Is.EqualTo(1538900872));
            point.isDirty = true;
            Assert.That(point.GetHashCode(), Is.EqualTo(1538900872), "isDirty is not hashed");
            point.type = SplinePoint.Type.SmoothFree;
            Assert.That(point.GetHashCode(), Is.Not.EqualTo(1538900872), "the type is hashed");
        }

        // Original SplinePoint metadata: [Serializable] struct, eight fields in this order with isDirty [NonSerialized], the private
        // _type [SerializeField][HideInInspector] and FormerlySerializedAs names that keep the older serialized layouts readable.
        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndFormerNames()
        {
            Assert.That(typeof(SplinePoint).IsSerializable, Is.True);
            FieldInfo[] fields = typeof(SplinePoint).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(field => field.MetadataToken).ToArray();
            Assert.That(Array.ConvertAll(fields, field => field.Name), Is.EqualTo(new[] { "isDirty", "_type", "position", "color", "normal", "size", "tangent", "tangent2" }));
            Assert.That(fields[0].IsNotSerialized, Is.True);
            Assert.That(fields[1].IsPrivate, Is.True);
            Assert.That(fields[1].GetCustomAttribute<SerializeField>(), Is.Not.Null);

            string[] formerNames = { null, "type", "_position", "_color", "_normal", "_size", "_tangent", "_tangent2" };
            for (int i = 1; i < fields.Length; i++)
            {
                Assert.That(fields[i].GetCustomAttribute<HideInInspector>(), Is.Not.Null, fields[i].Name + " is hidden");
                Assert.That(fields[i].GetCustomAttribute<FormerlySerializedAsAttribute>().oldName, Is.EqualTo(formerNames[i]), fields[i].Name);
            }

            Assert.That(Enum.GetNames(typeof(SplinePoint.Type)), Is.EqualTo(new[] { "SmoothMirrored", "Broken", "SmoothFree" }));
            Assert.That((int)SplinePoint.Type.SmoothFree, Is.EqualTo(2));
        }
    }
}
