using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using Dreamteck.Splines.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.IO.SplineParser, its transformation classes and its SplineDefinition. The nested types are internal, so
    // they are reached by reflection. Expected values are derived from the x86-64 bodies (addresses in each comment) and worked by hand.
    public sealed class OriginalDreamteckSplineParserTests
    {
        private const float Tolerance = 1e-4f;

        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ", expected " + expected.ToString("F6") + ")");
        }

        private static Type Nested(string name)
        {
            Type type = typeof(SplineParser).GetNestedType(name, BindingFlags.NonPublic);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object New(string nestedName, params object[] arguments)
        {
            return Activator.CreateInstance(Nested(nestedName), InstanceAny, null, arguments, null);
        }

        private static SplineParser NewParser()
        {
            return new SplineParser();
        }

        private static object Call(object target, string method, params object[] arguments)
        {
            MethodInfo info = target.GetType().GetMethod(method, InstanceAny);
            Assert.That(info, Is.Not.Null, method);
            try
            {
                return info.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }

        private static object Get(object target, string field)
        {
            FieldInfo info = target.GetType().GetField(field, InstanceAny);
            Assert.That(info, Is.Not.Null, field);
            return info.GetValue(target);
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, InstanceAny);
            Assert.That(info, Is.Not.Null, field);
            info.SetValue(target, value);
        }

        private static List<float> ParseFloats(string content)
        {
            return (List<float>)Call(NewParser(), "ParseFloatArray", content);
        }

        private static void AssertFloats(string content, params float[] expected)
        {
            List<float> actual = ParseFloats(content);
            Assert.That(actual.Count, Is.EqualTo(expected.Length), "'" + content + "' -> " + string.Join(" | ", actual.Select(v => v.ToString("R"))));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(1e-5f), "'" + content + "' value " + i);
            }
        }

        // Transformation.ResetMatrix 0x4ef150 sets the shared static matrix to identity; Push implementations multiply into it
        // (matrix = matrix * new) and Apply 0x4ef2c0 runs it over positions and tangents.
        private static SplinePoint[] Apply(object[] pushes, SplinePoint point)
        {
            Type transformation = Nested("Transformation");
            transformation.GetMethod("ResetMatrix", StaticAny).Invoke(null, null);
            foreach (object push in pushes)
            {
                transformation.GetMethod("Push", InstanceAny).Invoke(push, null);
            }

            SplinePoint[] points = { point };
            transformation.GetMethod("Apply", StaticAny).Invoke(null, new object[] { points });
            return points;
        }

        private static SplinePoint Probe(Vector3 position, Vector3 tangent, Vector3 tangent2)
        {
            return new SplinePoint(position, tangent, tangent2, new Vector3(0f, 0f, 1f), 7f, Color.red);
        }

        [Test]
        public void ParserKeepsItsDefaults()
        {
            SplineParser parser = NewParser();
            Assert.That(parser.name, Is.EqualTo(string.Empty), "fileName starts empty");
            Assert.That(Get(parser, "buffer"), Is.Null);
        }

        // IsWHiteSpace 0x4ef0c0: false at the first non-whitespace character, true for an empty string.
        [Test]
        public void IsWhiteSpaceIsTrueForEmptyAndBlankStringsOnly()
        {
            SplineParser parser = NewParser();
            Assert.That(parser.IsWHiteSpace(""), Is.True);
            Assert.That(parser.IsWHiteSpace(" \t\n "), Is.True);
            Assert.That(parser.IsWHiteSpace(" a"), Is.False);
            Assert.That(parser.IsWHiteSpace("a "), Is.False);
            Assert.That(parser.IsWHiteSpace("0"), Is.False);
            Assert.Throws<NullReferenceException>(() => parser.IsWHiteSpace(null));
        }

        // ParseFloatArray 0x4eebb0: a comma, a minus sign or whitespace ends the number being read (when it is not blank); a
        // minus then starts the next number. Whitespace never enters a number.
        [Test]
        public void ParseFloatArraySplitsOnCommasSpacesAndSigns()
        {
            AssertFloats("1,2,3", 1f, 2f, 3f);
            AssertFloats("1 2  3", 1f, 2f, 3f);
            AssertFloats("1\t2", 1f, 2f);
            AssertFloats("-1-2", -1f, -2f);
            AssertFloats("1-2", 1f, -2f);
            AssertFloats("10.5,-0.25", 10.5f, -0.25f);
            AssertFloats("");
            AssertFloats("   ");
            AssertFloats("7", 7f);

            // A comma after a separator is not blank-skipped: it becomes the current text, and the next separator parses "," as
            // a failed number (0).
            AssertFloats("1 , 2", 1f, 0f, 2f);
        }

        // A second decimal point ends the number and starts the next as "0." (the contains test in the branch condition), so
        // ".5.5" and "1.5.5" split in two.
        [Test]
        public void ParseFloatArrayStartsANewNumberAtASecondDecimalPoint()
        {
            AssertFloats(".5.5", 0.5f, 0.5f);
            AssertFloats("1.5.5", 1.5f, 0.5f);
            AssertFloats("1.5.5.5", 1.5f, 0.5f, 0.5f);
            AssertFloats("1.5 .5", 1.5f, 0.5f);
        }

        // The terminator test treats '-' as a separator everywhere, so an exponent sign splits "1e-3" into "1e" and "-3" ("1x-3"
        // stands in here: "1x" is not a number and parses as 0); an unsigned exponent stays in the number and parses with
        // NumberStyles.Any. A failed TryParse still adds a value (0).
        [Test]
        public void ParseFloatArrayCutsExponentsAtTheMinusSignAndAddsZeroForFailedParses()
        {
            AssertFloats("1x-3", 0f, -3f);
            AssertFloats("1e2", 100f);
            AssertFloats("5-", 5f, 0f);
            AssertFloats("a1", 0f);

            // The style is NumberStyles.Any (0x1ff, stored at 0x4ebfbb, passed at 0x4eee0c): parentheses mean negative. Float gives 0.
            AssertFloats("(2)", -2f);
        }

        // ParseVector2 0x4ee980: the first character (the path command) is dropped, numbers pair up with y negated, and an odd
        // trailing number is ignored.
        [Test]
        public void ParseVector2PairsNumbersAndNegatesY()
        {
            SplineParser parser = NewParser();
            Vector2[] pairs = (Vector2[])Call(parser, "ParseVector2", "M10,20 30,40");
            Assert.That(pairs, Is.EqualTo(new[] { new Vector2(10f, -20f), new Vector2(30f, -40f) }));
            Vector2[] odd = (Vector2[])Call(parser, "ParseVector2", "L1,2,3");
            Assert.That(odd, Is.EqualTo(new[] { new Vector2(1f, -2f) }));
            Vector2[] negative = (Vector2[])Call(parser, "ParseVector2", "m-1-2");
            Assert.That(negative, Is.EqualTo(new[] { new Vector2(-1f, 2f) }));
        }

        // 0x4eeb3c negates y with xorps (a sign flip), so a zero y becomes -0 rather than 0 - 0 = +0.
        [Test]
        public void ParseVector2NegatesAZeroYToNegativeZero()
        {
            Vector2[] pairs = (Vector2[])Call(NewParser(), "ParseVector2", "M5,0");
            Assert.That(1f / pairs[0].y, Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void ParseVector2WithFewerThanTwoNumbersLogsAndYieldsOneZero()
        {
            SplineParser parser = NewParser();
            LogAssert.Expect(LogType.Log, "Error in M5");
            Vector2[] single = (Vector2[])Call(parser, "ParseVector2", "M5");
            Assert.That(single, Is.EqualTo(new[] { Vector2.zero }));
            LogAssert.Expect(LogType.Log, "Error in M");
            Vector2[] none = (Vector2[])Call(parser, "ParseVector2", "M");
            Assert.That(none, Is.EqualTo(new[] { Vector2.zero }));
        }

        // ParseFloat 0x4eefc0: drops the first character; no numbers logs and returns a one element zero array.
        [Test]
        public void ParseFloatReturnsTheNumbersOrOneZeroWithAnError()
        {
            SplineParser parser = NewParser();
            Assert.That((float[])Call(parser, "ParseFloat", "H1 2 3"), Is.EqualTo(new[] { 1f, 2f, 3f }));
            LogAssert.Expect(LogType.Log, "Error in H");
            Assert.That((float[])Call(parser, "ParseFloat", "H"), Is.EqualTo(new[] { 0f }));
        }

        // Translate.Push 0x4ef530: translation (x, -y, 0), so SVG y points down. Apply moves position and both tangents (the
        // translation moves them like points) and keeps normal, size and color.
        [Test]
        public void TranslateNegatesYAndMovesPositionAndTangents()
        {
            SplinePoint[] result = Apply(new[] { New("Translate", new Vector2(3f, 4f)) }, Probe(new Vector3(1f, 1f, 1f), new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 5f)));
            Near(result[0].position, new Vector3(4f, -3f, 1f), "position");
            Near(result[0].tangent, new Vector3(5f, -4f, 0f), "tangent");
            Near(result[0].tangent2, new Vector3(3f, -4f, 5f), "tangent2");
            Near(result[0].normal, new Vector3(0f, 0f, 1f), "normal is kept");
            Assert.That(result[0].size, Is.EqualTo(7f));
            Assert.That(result[0].color, Is.EqualTo(Color.red));
            Assert.That(result[0].type, Is.EqualTo(SplinePoint.Type.Broken));
        }

        // Rotate.Push 0x4ef780: AngleAxis(angle, back) is a rotation about -z, so +90 degrees takes (x, y) to (y, -x).
        [Test]
        public void RotateTurnsAboutTheBackAxis()
        {
            SplinePoint[] result = Apply(new[] { New("Rotate", 90f) }, Probe(new Vector3(1f, 0f, 0f), new Vector3(0f, 2f, 5f), Vector3.zero));
            Near(result[0].position, new Vector3(0f, -1f, 0f), "position");
            Near(result[0].tangent, new Vector3(2f, 0f, 5f), "tangent");
        }

        // Scale.Push 0x4efa40 uses the 2D multiplier as a Vector3 with z = 0, so the z of every transformed point is flattened.
        [Test]
        public void ScaleMultipliesXyAndFlattensZ()
        {
            SplinePoint[] result = Apply(new[] { New("Scale", new Vector2(2f, 3f)) }, Probe(new Vector3(1f, 1f, 1f), new Vector3(0f, 1f, 4f), Vector3.zero));
            Near(result[0].position, new Vector3(2f, 3f, 0f), "position");
            Near(result[0].tangent, new Vector3(0f, 3f, 0f), "tangent");
        }

        // SkewX.Push 0x4efc50 puts tan(-amount * Deg2Rad) into matrix element 4 (row 0, column 1): x' = x + tan * y. SkewY.Push
        // 0x4efe50 uses element 1 (row 1, column 0): y' = y + tan * x. Both negate the angle.
        [Test]
        public void SkewsPutTheNegatedTangentInTheirMatrixElement()
        {
            SplinePoint[] x = Apply(new[] { New("SkewX", 45f) }, Probe(new Vector3(0f, 1f, 0f), new Vector3(2f, 3f, 0f), Vector3.zero));
            Near(x[0].position, new Vector3(-1f, 1f, 0f), "skewX position");
            Near(x[0].tangent, new Vector3(-1f, 3f, 0f), "skewX tangent");

            SplinePoint[] y = Apply(new[] { New("SkewY", 45f) }, Probe(new Vector3(1f, 0f, 0f), new Vector3(2f, 3f, 0f), Vector3.zero));
            Near(y[0].position, new Vector3(1f, -1f, 0f), "skewY position");
            Near(y[0].tangent, new Vector3(2f, 1f, 0f), "skewY tangent");

            SplinePoint[] none = Apply(new[] { New("SkewX", 0f) }, Probe(new Vector3(3f, 4f, 5f), Vector3.zero, Vector3.zero));
            Near(none[0].position, new Vector3(3f, 4f, 5f), "a zero skew is the identity");
        }

        // MatrixTransform ctor 0x4efff0: rows (a c 0 e), (b d 0 -f), (0 0 1 0), (0 0 0 1). With 1 2 3 4 5 6 the point (1, 1, 0)
        // maps to (1 + 3 + 5, 2 + 4 - 6, 0) = (9, 0, 0) and z is kept (unlike Scale).
        [Test]
        public void MatrixTransformMapsTheAffineRowsAndFlipsTheTranslationY()
        {
            object matrix = New("MatrixTransform", 1f, 2f, 3f, 4f, 5f, 6f);
            SplinePoint[] result = Apply(new[] { matrix }, Probe(new Vector3(1f, 1f, 0f), new Vector3(0f, 0f, 2f), Vector3.zero));
            Near(result[0].position, new Vector3(9f, 0f, 0f), "position");
            Near(result[0].tangent, new Vector3(5f, -6f, 2f), "tangent keeps z");
        }

        // Each Push multiplies the new matrix on the right (matrix = matrix * new), so the last push applies first: translate
        // (1, 0) then scale 2 maps (1, 0) to T(S(p)) = 3, while scale then translate gives S(T(p)) = 4.
        [Test]
        public void PushOrderMultipliesOnTheRightAndResetStartsOver()
        {
            Vector3 probe = new Vector3(1f, 0f, 0f);
            SplinePoint[] translateThenScale = Apply(new[] { New("Translate", new Vector2(1f, 0f)), New("Scale", new Vector2(2f, 2f)) }, Probe(probe, probe, probe));
            Near(translateThenScale[0].position, new Vector3(3f, 0f, 0f), "translate then scale");
            SplinePoint[] scaleThenTranslate = Apply(new[] { New("Scale", new Vector2(2f, 2f)), New("Translate", new Vector2(1f, 0f)) }, Probe(probe, probe, probe));
            Near(scaleThenTranslate[0].position, new Vector3(4f, 0f, 0f), "scale then translate");
            SplinePoint[] reset = Apply(new object[0], Probe(probe, probe, probe));
            Near(reset[0].position, probe, "an empty stack is the identity after ResetMatrix");
        }

        // The point is copied, changed and stored back, so the array element is replaced; an empty array is fine.
        [Test]
        public void ApplyWritesTheTransformedPointsBack()
        {
            Type transformation = Nested("Transformation");
            transformation.GetMethod("ResetMatrix", StaticAny).Invoke(null, null);
            transformation.GetMethod("Push", InstanceAny).Invoke(New("Translate", new Vector2(1f, 1f)), null);
            SplinePoint[] points = { Probe(Vector3.zero, Vector3.zero, Vector3.zero), Probe(Vector3.one, Vector3.one, Vector3.one) };
            transformation.GetMethod("Apply", StaticAny).Invoke(null, new object[] { points });
            Near(points[0].position, new Vector3(1f, -1f, 0f), "first");
            Near(points[1].position, new Vector3(2f, 0f, 1f), "second");
            transformation.GetMethod("Apply", StaticAny).Invoke(null, new object[] { new SplinePoint[0] });
        }

        private static object NewDefinition(string name = "shape", Spline.Type type = Spline.Type.Linear)
        {
            return New("SplineDefinition", name, type);
        }

        private static List<SplinePoint> PointsOf(object definition)
        {
            return (List<SplinePoint>)Get(definition, "points");
        }

        // SplineDefinition constructor 0x4ec650 and its field initialisers: Linear unless told otherwise, back normal, size 1,
        // white, everything else zero or empty.
        [Test]
        public void DefinitionStartsWithTheShippedPenState()
        {
            object definition = New("SplineDefinition", "path", Spline.Type.Bezier);
            Assert.That(Get(definition, "name"), Is.EqualTo("path"));
            Assert.That(Get(definition, "type"), Is.EqualTo(Spline.Type.Bezier));
            Assert.That(Get(definition, "closed"), Is.False);
            Assert.That(PointsOf(definition), Is.Empty);
            Assert.That(Get(definition, "position"), Is.EqualTo(Vector3.zero));
            Assert.That(Get(definition, "tangent"), Is.EqualTo(Vector3.zero));
            Assert.That(Get(definition, "tangent2"), Is.EqualTo(Vector3.zero));
            Assert.That(Get(definition, "normal"), Is.EqualTo(Vector3.back));
            Assert.That(Get(definition, "size"), Is.EqualTo(1f));
            Assert.That(Get(definition, "color"), Is.EqualTo(Color.white));
            PropertyInfo count = definition.GetType().GetProperty("pointCount", InstanceAny);
            Assert.That(count.GetValue(definition), Is.EqualTo(0));
        }

        // Constructor 0x4ebfd0 copies the type, closed = isClosed (the flag and at least three points) and a new list of the points.
        [Test]
        public void DefinitionFromASplineCopiesTypeClosedStateAndPoints()
        {
            Spline spline = new Spline(Spline.Type.Bezier);
            spline.points = new[] { new SplinePoint(Vector3.zero), new SplinePoint(Vector3.right), new SplinePoint(Vector3.up) };
            spline.Close();
            object closed = New("SplineDefinition", "copy", spline);
            Assert.That(Get(closed, "type"), Is.EqualTo(Spline.Type.Bezier));
            Assert.That(Get(closed, "closed"), Is.True);
            Assert.That(PointsOf(closed).Count, Is.EqualTo(3));
            Assert.That(closed.GetType().GetProperty("pointCount", InstanceAny).GetValue(closed), Is.EqualTo(3));
            spline.points[0].position = Vector3.one;
            Near(PointsOf(closed)[0].position, Vector3.zero, "the list is a copy of the array");

            Spline shortOne = new Spline(Spline.Type.Linear);
            shortOne.points = new[] { new SplinePoint(Vector3.zero), new SplinePoint(Vector3.right) };
            object open = New("SplineDefinition", "open", shortOne);
            Assert.That(Get(open, "closed"), Is.False);
            Assert.That(Get(open, "type"), Is.EqualTo(Spline.Type.Linear));
        }

        // GetLastPoint 0x4f01e0: an empty list answers an all-zero point (not a default-constructed pen point); SetLastPoint
        // 0x4f0290 does nothing on an empty list and replaces the last element otherwise.
        [Test]
        public void LastPointAccessorsHandleAnEmptyListAndReplaceTheLast()
        {
            object definition = NewDefinition();
            SplinePoint empty = (SplinePoint)Call(definition, "GetLastPoint");
            Assert.That(empty.color, Is.EqualTo(new Color(0f, 0f, 0f, 0f)), "an all-zero point, not white");
            Assert.That(empty.size, Is.EqualTo(0f));
            Assert.That(empty.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Call(definition, "SetLastPoint", new SplinePoint(Vector3.one));
            Assert.That(PointsOf(definition), Is.Empty);

            PointsOf(definition).Add(new SplinePoint(Vector3.zero));
            PointsOf(definition).Add(new SplinePoint(Vector3.right));
            Near(((SplinePoint)Call(definition, "GetLastPoint")).position, Vector3.right, "last");
            Call(definition, "SetLastPoint", new SplinePoint(Vector3.up));
            Assert.That(PointsOf(definition).Count, Is.EqualTo(2));
            Near(PointsOf(definition)[0].position, Vector3.zero, "first is untouched");
            Near(PointsOf(definition)[1].position, Vector3.up, "last is replaced");
        }

        private static void SetPen(object definition)
        {
            Set(definition, "position", new Vector3(1f, 2f, 3f));
            Set(definition, "tangent", new Vector3(2f, 2f, 3f));
            Set(definition, "tangent2", new Vector3(9f, 9f, 9f));
            Set(definition, "normal", new Vector3(0f, 1f, 0f));
            Set(definition, "size", 4f);
            Set(definition, "color", Color.green);
        }

        // CreateSmooth 0x4ed210 adds new SplinePoint(position, tangent, normal, size, color): SmoothMirrored with the tangent2
        // mirrored from the tangent (2 * position - tangent) and dirty. The pen's tangent2 field is not used.
        [Test]
        public void CreateSmoothAddsAMirroredDirtyPointFromThePenState()
        {
            object definition = NewDefinition();
            SetPen(definition);
            Call(definition, "CreateSmooth");
            SplinePoint point = PointsOf(definition).Single();
            Near(point.position, new Vector3(1f, 2f, 3f), "position");
            Near(point.tangent, new Vector3(2f, 2f, 3f), "tangent");
            Near(point.tangent2, new Vector3(0f, 2f, 3f), "tangent2 mirrors the tangent, not the pen's tangent2");
            Near(point.normal, new Vector3(0f, 1f, 0f), "normal");
            Assert.That(point.size, Is.EqualTo(4f));
            Assert.That(point.color, Is.EqualTo(Color.green));
            Assert.That(point.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Assert.That(point.isDirty, Is.True);
        }

        // CreateBroken 0x4f07b0: the same point, then Broken with tangent2 on the position; the type change leaves it dirty
        // (the six-argument constructor would have left it clean).
        [Test]
        public void CreateBrokenAddsADirtyBrokenPointWithTangent2OnThePosition()
        {
            object definition = NewDefinition();
            SetPen(definition);
            Call(definition, "CreateBroken");
            SplinePoint point = PointsOf(definition).Single();
            Near(point.position, new Vector3(1f, 2f, 3f), "position");
            Near(point.tangent, new Vector3(2f, 2f, 3f), "tangent");
            Near(point.tangent2, new Vector3(1f, 2f, 3f), "tangent2 sits on the position");
            Assert.That(point.type, Is.EqualTo(SplinePoint.Type.Broken));
            Assert.That(point.isDirty, Is.True);
            Assert.That(point.size, Is.EqualTo(4f));
            Assert.That(point.color, Is.EqualTo(Color.green));
        }

        // CreateLinear 0x4f0900 copies the position into the pen's tangent, then CreateSmooth: all three vectors end on the
        // position and the pen keeps the changed tangent.
        [Test]
        public void CreateLinearPutsTheTangentOnThePositionAndAddsASmoothPoint()
        {
            object definition = NewDefinition();
            SetPen(definition);
            Call(definition, "CreateLinear");
            SplinePoint point = PointsOf(definition).Single();
            Near(point.tangent, new Vector3(1f, 2f, 3f), "tangent");
            Near(point.tangent2, new Vector3(1f, 2f, 3f), "tangent2");
            Assert.That(point.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Assert.That(Get(definition, "tangent"), Is.EqualTo(new Vector3(1f, 2f, 3f)), "the pen tangent is overwritten");
        }

        // CreateClosingPoint 0x4f0300 adds new SplinePoint(points[0]): the copy constructor starts clean, keeps a Broken point as
        // is and re-derives (and dirties) tangent2 of a SmoothMirrored one. An empty list throws from points[0].
        [Test]
        public void CreateClosingPointAppendsACopyOfTheFirstPoint()
        {
            object definition = NewDefinition();
            Assert.Throws<ArgumentOutOfRangeException>(() => Call(definition, "CreateClosingPoint"));

            SplinePoint broken = new SplinePoint(new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(9f, 9f, 9f), Vector3.up, 3f, Color.blue);
            PointsOf(definition).Add(broken);
            Call(definition, "CreateClosingPoint");
            SplinePoint copy = PointsOf(definition)[1];
            Assert.That(PointsOf(definition).Count, Is.EqualTo(2));
            Near(copy.tangent2, new Vector3(9f, 9f, 9f), "Broken keeps tangent2");
            Assert.That(copy.isDirty, Is.False);
            Assert.That(copy.size, Is.EqualTo(3f));

            object smooth = NewDefinition();
            PointsOf(smooth).Add(new SplinePoint(new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f)));
            Call(smooth, "CreateClosingPoint");
            SplinePoint mirrored = PointsOf(smooth)[1];
            Near(mirrored.tangent2, new Vector3(0f, 0f, 0f), "tangent2 is derived from the tangent again");
            Assert.That(mirrored.isDirty, Is.True);
        }

        // CreateSpline 0x4ed6e0: a Spline of the definition's type with a copy of the points as an array; Close (inlined, error
        // included) for a closed definition.
        [Test]
        public void CreateSplineBuildsTheSplineAndClosesClosedDefinitions()
        {
            object definition = NewDefinition("shape", Spline.Type.Bezier);
            PointsOf(definition).Add(new SplinePoint(Vector3.zero));
            PointsOf(definition).Add(new SplinePoint(Vector3.right));
            PointsOf(definition).Add(new SplinePoint(Vector3.up));
            Set(definition, "closed", true);
            Spline spline = (Spline)Call(definition, "CreateSpline");
            Assert.That(spline.type, Is.EqualTo(Spline.Type.Bezier));
            Assert.That(spline.points.Length, Is.EqualTo(3));
            Assert.That(spline.isClosed, Is.True);

            PointsOf(definition).RemoveAt(2);
            LogAssert.Expect(LogType.Error, "Points need to be at least 3 to close the spline");
            Spline tooShort = (Spline)Call(definition, "CreateSpline");
            Assert.That(tooShort.isClosed, Is.False);

            Set(definition, "closed", false);
            Assert.That(((Spline)Call(definition, "CreateSpline")).isClosed, Is.False);
        }

        // CreateSplineComputer 0x4ed390: a game object named after the definition, positioned, rotated, with a SplineComputer of
        // the definition's type and the points in Local space; closed definitions close it. The first point is read for a closed
        // definition, so a closed empty one throws.
        [Test]
        public void CreateSplineComputerBuildsTheComputerFromTheDefinition()
        {
            object definition = NewDefinition("imported", Spline.Type.Linear);
            PointsOf(definition).Add(new SplinePoint(Vector3.zero));
            PointsOf(definition).Add(new SplinePoint(new Vector3(4f, 0f, 0f)));
            PointsOf(definition).Add(new SplinePoint(new Vector3(4f, 4f, 0f)));
            Set(definition, "closed", true);
            Quaternion rotation = Quaternion.Euler(0f, 45f, 0f);
            SplineComputer computer = (SplineComputer)Call(definition, "CreateSplineComputer", new Vector3(1f, 2f, 3f), rotation);
            _owned.Add(computer.gameObject);
            Assert.That(computer.name, Is.EqualTo("imported"));
            Near(computer.transform.position, new Vector3(1f, 2f, 3f), "position");
            Assert.That(Quaternion.Angle(computer.transform.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(computer.type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(computer.isClosed, Is.True);
            SplinePoint[] local = computer.GetPoints(SplineComputer.Space.Local);
            Assert.That(local.Length, Is.EqualTo(3));
            Near(local[1].position, new Vector3(4f, 0f, 0f), "local point 1");

            object emptyClosed = NewDefinition("empty", Spline.Type.Linear);
            Set(emptyClosed, "closed", true);
            // The game object is created before the throw; find it by a unique name so only our own object is destroyed.
            string uniqueName = "lucid-parser-closed-empty-" + Guid.NewGuid().ToString("N");
            Set(emptyClosed, "name", uniqueName);
            Assert.Throws<ArgumentOutOfRangeException>(() => Call(emptyClosed, "CreateSplineComputer", Vector3.zero, Quaternion.identity));
            GameObject leftover = GameObject.Find(uniqueName);
            if (leftover != null)
            {
                _owned.Add(leftover);
            }
        }

        // Transform 0x4f0920: ResetMatrix, every transformation's Push in list order, Apply to a copy of the points, then each
        // point is stored back into the list.
        [Test]
        public void TransformPushesEveryTransformationAndStoresThePointsBack()
        {
            object definition = NewDefinition();
            PointsOf(definition).Add(new SplinePoint(new Vector3(1f, 1f, 0f)));
            PointsOf(definition).Add(new SplinePoint(new Vector3(2f, 0f, 0f)));
            Type transformation = Nested("Transformation");
            object list = Activator.CreateInstance(typeof(List<>).MakeGenericType(transformation));
            list.GetType().GetMethod("Add").Invoke(list, new[] { New("Translate", new Vector2(1f, 0f)) });
            list.GetType().GetMethod("Add").Invoke(list, new[] { New("Scale", new Vector2(2f, 2f)) });
            Call(definition, "Transform", list);
            Near(PointsOf(definition)[0].position, new Vector3(3f, 2f, 0f), "T(S(1, 1)): scale to (2, 2), then translate by (1, -0)");
            Near(PointsOf(definition)[1].position, new Vector3(5f, 0f, 0f), "T(S(2, 0))");
            Assert.Throws<NullReferenceException>(() => Call(definition, "Transform", new object[] { null }));
        }

        // Shape of the parser family: nested type names and visibility, field order, the BeforeFieldInit static constructor of
        // Transformation and the protected matrix.
        [Test]
        public void ParserKeepsTheShippedNestedTypesAndFieldOrder()
        {
            string[] expectedNested = { "Transformation", "Translate", "Rotate", "Scale", "SkewX", "SkewY", "MatrixTransform", "SplineDefinition" };
            foreach (string name in expectedNested)
            {
                Type nested = Nested(name);
                Assert.That(nested.IsNestedAssembly, Is.True, name + " is internal");
            }

            foreach (string derived in new[] { "Translate", "Rotate", "Scale", "SkewX", "SkewY", "MatrixTransform" })
            {
                Assert.That(Nested(derived).BaseType, Is.EqualTo(Nested("Transformation")), derived);
            }

            BindingFlags declared = InstanceAny | BindingFlags.Static | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SplineParser).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "fileName", "culture", "style", "buffer" }));
            Assert.That(Nested("Transformation").GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "matrix" }));
            Assert.That(Nested("Transformation").GetField("matrix", declared).IsFamily, Is.True);
            Assert.That(Nested("Transformation").TypeInitializer, Is.Not.Null);
            Assert.That(Nested("Transformation").Attributes & TypeAttributes.BeforeFieldInit, Is.EqualTo(TypeAttributes.BeforeFieldInit));
            Assert.That(Nested("Translate").GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "offset" }));
            Assert.That(Nested("Rotate").GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "angle" }));
            Assert.That(Nested("Scale").GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "multiplier" }));
            Assert.That(Nested("SkewX").GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "amount" }));
            Assert.That(Nested("SkewY").GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "amount" }));
            Assert.That(Nested("MatrixTransform").GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "transformMatrix" }));
            Assert.That(Nested("SplineDefinition").GetFields(declared).Select(f => f.Name),
                Is.EqualTo(new[] { "name", "type", "points", "closed", "position", "tangent", "tangent2", "normal", "size", "color" }));
        }
    }
}
