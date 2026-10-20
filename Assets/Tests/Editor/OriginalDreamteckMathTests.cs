using System;
using System.Reflection;
using Dreamteck;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Utilities math, array, curve and bounds helpers. Expected values are derived from the
    // x86-64 bodies (addresses in each comment) and from the arithmetic of the shipped formulas, not
    // read back from the restored source. Where a plausible textbook version would give a different
    // answer, the inputs are chosen to tell the two apart.
    public sealed class OriginalDreamteckMathTests
    {
        private const float Tolerance = 1e-5f;

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        // Original 0x4f9370..0x4f9610 tail-call the C runtime sin, cos, tan, System.Math.Pow,
        // System.Math.Log(double, double) and log10 with the arguments unchanged.
        [Test]
        public void TranscendentalWrappersReturnTheRuntimeMathValues()
        {
            Assert.That(DMath.Sin(1.0), Is.EqualTo(0.8414709848078965).Within(1e-15));
            Assert.That(DMath.Cos(1.0), Is.EqualTo(0.5403023058681398).Within(1e-15));
            Assert.That(DMath.Tan(1.0), Is.EqualTo(1.5574077246549023).Within(1e-14));
            Assert.That(DMath.Pow(2.0, 10.0), Is.EqualTo(1024.0).Within(1e-12));
            Assert.That(DMath.Pow(2.0, 0.5), Is.EqualTo(1.4142135623730951).Within(1e-15));
            Assert.That(DMath.Log(8.0, 2.0), Is.EqualTo(3.0).Within(1e-12));
            Assert.That(DMath.Log(100.0, 10.0), Is.EqualTo(2.0).Within(1e-12));
            Assert.That(DMath.Log10(1000.0), Is.EqualTo(3.0).Within(1e-12));
            Assert.That(DMath.Log10(0.001), Is.EqualTo(-3.0).Within(1e-12));
        }

        // Original Clamp01 0x4f9690 compares a > 1 then 0 > a and otherwise returns a, so NaN falls through
        // unchanged. Clamp 0x4f96c0 tests a > max before min > a, so with min > max the max wins above
        // the range and the min wins below it. A Math.Min(Math.Max(a, min), max) clamp answers 0 for
        // (-1, 3, 0) and a Math.Max(min, Math.Min(a, max)) clamp answers 3 for (5, 3, 0).
        [Test]
        public void ClampFamilyKeepsTheNativeComparisonOrder()
        {
            (double input, double expected)[] clamp01 =
            {
                (-1.0, 0.0), (0.0, 0.0), (0.25, 0.25), (1.0, 1.0), (2.0, 1.0), (double.PositiveInfinity, 1.0), (double.NegativeInfinity, 0.0),
            };
            foreach (var row in clamp01)
            {
                Assert.That(DMath.Clamp01(row.input), Is.EqualTo(row.expected), "Clamp01(" + row.input + ")");
            }

            Assert.That(double.IsNaN(DMath.Clamp01(double.NaN)), Is.True, "Clamp01 returns NaN unchanged");

            (double a, double min, double max, double expected)[] clamp =
            {
                (5.0, 0.0, 3.0, 3.0), (-5.0, 0.0, 3.0, 0.0), (1.5, 0.0, 3.0, 1.5), (1.0, 3.0, 0.0, 0.0), (-1.0, 3.0, 0.0, 3.0), (5.0, 3.0, 0.0, 0.0),
            };
            foreach (var row in clamp)
            {
                Assert.That(DMath.Clamp(row.a, row.min, row.max), Is.EqualTo(row.expected), "Clamp(" + row.a + ", " + row.min + ", " + row.max + ")");
            }

            Assert.That(double.IsNaN(DMath.Clamp(double.NaN, 0.0, 1.0)), Is.True, "Clamp returns NaN unchanged");
        }

        // Original Lerp 0x4f96e0 clamps t with the Clamp01 body; InverseLerp 0x4f9720 returns 0 when
        // a == b (not a division by zero) and otherwise clamps (t - a) / (b - a), which also holds when b < a.
        [Test]
        public void LerpAndInverseLerpClampTheirParameter()
        {
            Assert.That(DMath.Lerp(0.0, 10.0, 0.25), Is.EqualTo(2.5));
            Assert.That(DMath.Lerp(0.0, 10.0, 2.0), Is.EqualTo(10.0));
            Assert.That(DMath.Lerp(0.0, 10.0, -1.0), Is.EqualTo(0.0));
            Assert.That(DMath.Lerp(10.0, 0.0, 0.25), Is.EqualTo(7.5));
            // The native formula is a + (b - a) * t (subsd, mulsd, addsd at 0x4f9700..0x4f9708): 1 - 1e16 rounds to
            // -1e16 (ties to even at an ulp of 2), so the result is 0. The a * (1 - t) + b * t form gives 1.
            Assert.That(DMath.Lerp(1e16, 1.0, 1.0), Is.EqualTo(0.0));

            Assert.That(DMath.InverseLerp(2.0, 4.0, 3.0), Is.EqualTo(0.5));
            Assert.That(DMath.InverseLerp(2.0, 4.0, 10.0), Is.EqualTo(1.0));
            Assert.That(DMath.InverseLerp(2.0, 4.0, -5.0), Is.EqualTo(0.0));
            Assert.That(DMath.InverseLerp(3.0, 3.0, 5.0), Is.EqualTo(0.0));
            Assert.That(DMath.InverseLerp(4.0, 2.0, 3.0), Is.EqualTo(0.5));
            Assert.That(DMath.InverseLerp(4.0, 2.0, 1.0), Is.EqualTo(1.0));
        }

        // Original LerpVector3 0x4f97d0 and LerpVector3NonAlloc 0x4f9760 clamp t, subtract in float,
        // then widen: cvtps2pd, mulpd by t, addpd, cvtpd2ps. For a = (2.49566889, ., .), b = (-0.155678779, ., .)
        // and t = 0.9 the double pipeline gives 0.109456018 where an all-float pipeline gives 0.109456062.
        [Test]
        public void LerpVector3ClampsTAndWidensToDoubleForTheArithmetic()
        {
            Vector3 a = new Vector3(2.49566889f, -0.17841895f, -0.617192566f);
            Vector3 b = new Vector3(-0.155678779f, 2.01876879f, 2.16613269f);
            Vector3 mixed = DMath.LerpVector3(a, b, 0.9);
            Assert.That(mixed.x, Is.EqualTo(0.109456018f), "double-precision x");
            Assert.That(mixed.x, Is.Not.EqualTo(0.109456062f), "not the all-float result");
            Assert.That(mixed.y, Is.EqualTo(1.79904997f), "double-precision y (all-float gives 1.79904985)");
            Assert.That(mixed.z, Is.EqualTo(1.8878001f), "double-precision z (all-float gives 1.88780022)");

            // At t = 1 the double sum a + float(b - a) rounds back to b except for x, where the float difference
            // (an ulp of 2.4e-7) is coarser than an ulp of b: -0.155678749 instead of -0.155678779.
            Vector3 atOne = new Vector3(-0.155678749f, b.y, b.z);
            Assert.That(DMath.LerpVector3(a, b, 2.0), Is.EqualTo(atOne), "t is clamped to 1");
            Assert.That(DMath.LerpVector3(a, b, 1.0), Is.EqualTo(atOne), "the clamped t equals 1");
            Assert.That(DMath.LerpVector3(a, b, -3.0), Is.EqualTo(a), "t is clamped to 0");

            Vector3 target = new Vector3(100f, 100f, 100f);
            DMath.LerpVector3NonAlloc(a, b, 0.9, ref target);
            Assert.That(target.x, Is.EqualTo(0.109456018f), "NonAlloc matches LerpVector3");
            Assert.That(target, Is.EqualTo(mixed));
        }

        // Original Round 0x4f9890: modf splits the value; an exact +/-0.5 fraction returns the integer
        // part (adding +/-1 when it is odd, so halves go to the even neighbour); every other value
        // rounds to the nearest integer. IL2CPP inlined System.Math.Round(double) there, so only rows
        // that every runtime agrees on are asserted.
        [Test]
        public void RoundSendsHalvesToEvenAndOtherwiseFloorsAddedHalf()
        {
            (double input, double expected)[] rows =
            {
                (0.5, 0.0), (1.5, 2.0), (2.5, 2.0), (3.5, 4.0), (-0.5, 0.0), (-1.5, -2.0), (-2.5, -2.0), (-3.5, -4.0),
                (2.4, 2.0), (2.6, 3.0), (-2.4, -2.0), (-2.6, -3.0), (0.0, 0.0), (7.0, 7.0), (-7.0, -7.0),
            };
            foreach (var row in rows)
            {
                Assert.That(DMath.Round(row.input), Is.EqualTo(row.expected), "Round(" + row.input + ")");
                Assert.That(DMath.RoundInt(row.input), Is.EqualTo((int)row.expected), "RoundInt(" + row.input + ")");
            }
        }

        // Original Ceil 0x4f9a60 / CeilInt 0x4f9ae0 / Floor 0x4f9b40 / FloorInt 0x4f9bc0 are roundsd
        // (up / down) with the int forms followed by cvttsd2si.
        [Test]
        public void CeilAndFloorRoundTowardTheirDirection()
        {
            Assert.That(DMath.Ceil(1.2), Is.EqualTo(2.0));
            Assert.That(DMath.Ceil(-1.2), Is.EqualTo(-1.0));
            Assert.That(DMath.Floor(1.8), Is.EqualTo(1.0));
            Assert.That(DMath.Floor(-1.2), Is.EqualTo(-2.0));
            Assert.That(DMath.CeilInt(1.2), Is.EqualTo(2));
            Assert.That(DMath.CeilInt(-1.9), Is.EqualTo(-1));
            Assert.That(DMath.FloorInt(1.9), Is.EqualTo(1));
            Assert.That(DMath.FloorInt(-0.1), Is.EqualTo(-1));
        }

        // Original Move 0x4f9c20 is `target > current ? min(current + amount, target) : max(current - amount, target)`
        // with the tests in that order, so a negative amount moves away from the target and equal values
        // go through the downward branch. Abs 0x4f9c50 is max(-a, a).
        [Test]
        public void MoveNeverOvershootsAndAbsIsMagnitude()
        {
            (double current, double target, double amount, double expected)[] moves =
            {
                (0.0, 10.0, 3.0, 3.0), (9.0, 10.0, 3.0, 10.0), (10.0, 0.0, 3.0, 7.0), (2.0, 0.0, 3.0, 0.0), (5.0, 5.0, 1.0, 5.0),
                (5.0, 5.0, 0.0, 5.0), (0.0, 10.0, -3.0, -3.0), (10.0, 0.0, -3.0, 13.0),
            };
            foreach (var row in moves)
            {
                Assert.That(DMath.Move(row.current, row.target, row.amount), Is.EqualTo(row.expected), "Move(" + row.current + ", " + row.target + ", " + row.amount + ")");
            }

            Assert.That(DMath.Abs(-2.5), Is.EqualTo(2.5));
            Assert.That(DMath.Abs(3.0), Is.EqualTo(3.0));
            Assert.That(DMath.Abs(0.0), Is.EqualTo(0.0));
            // maxsd(-a, a) with both operands zero returns the second operand (0x4f9c60), so -0.0 stays negative;
            // System.Math.Abs would answer +0.0.
            Assert.That(double.IsNegative(DMath.Abs(-0.0)), Is.True, "Abs(-0.0) keeps the sign of zero");
        }

        // Original ArrayUtility 0x8d6140..0x8d7380: Add, Insert and RemoveAt build a new array and
        // assign it through the ref argument, so the old array is untouched; RemoveAt leaves an
        // empty array alone (same reference); Insert at Length appends.
        [Test]
        public void AddInsertAndRemoveAtReplaceTheArray()
        {
            int[] original = { 1, 2, 3 };
            int[] array = original;
            ArrayUtility.Add(ref array, 4);
            Assert.That(array, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(array, Is.Not.SameAs(original));
            Assert.That(original, Is.EqualTo(new[] { 1, 2, 3 }), "the source array is not modified");

            array = new[] { 1, 2, 3 };
            ArrayUtility.Insert(ref array, 0, 9);
            Assert.That(array, Is.EqualTo(new[] { 9, 1, 2, 3 }));
            array = new[] { 1, 2, 3 };
            ArrayUtility.Insert(ref array, 1, 9);
            Assert.That(array, Is.EqualTo(new[] { 1, 9, 2, 3 }));
            array = new[] { 1, 2, 3 };
            ArrayUtility.Insert(ref array, 3, 9);
            Assert.That(array, Is.EqualTo(new[] { 1, 2, 3, 9 }));

            array = new[] { 1, 2, 3 };
            int[] beforeFault = array;
            Assert.Throws<IndexOutOfRangeException>(() => ArrayUtility.Insert(ref array, 5, 9));
            Assert.That(array, Is.SameAs(beforeFault), "a fault leaves the reference unchanged");

            array = new[] { 1, 2, 3 };
            ArrayUtility.RemoveAt(ref array, 0);
            Assert.That(array, Is.EqualTo(new[] { 2, 3 }));
            array = new[] { 1, 2, 3 };
            ArrayUtility.RemoveAt(ref array, 1);
            Assert.That(array, Is.EqualTo(new[] { 1, 3 }));
            array = new[] { 1, 2, 3 };
            ArrayUtility.RemoveAt(ref array, 2);
            Assert.That(array, Is.EqualTo(new[] { 1, 2 }));

            int[] empty = new int[0];
            int[] emptyReference = empty;
            ArrayUtility.RemoveAt(ref empty, 0);
            Assert.That(empty, Is.SameAs(emptyReference), "an empty array is returned untouched");

            array = new[] { 1, 2, 3 };
            beforeFault = array;
            Assert.Throws<IndexOutOfRangeException>(() => ArrayUtility.RemoveAt(ref array, 7));
            Assert.That(array, Is.SameAs(beforeFault));
        }

        // Original Contains 0x8d63c0 / IndexOf 0x8d6730 compare with Equals; IndexOf returns 0 both
        // for a hit at index 0 and for a missing value (the not-found path xors the result register).
        [Test]
        public void ContainsAndIndexOfUseEqualsAndMissingValuesAnswerZero()
        {
            string[] names = { "a", "b", "c" };
            Assert.That(ArrayUtility.Contains(names, "b"), Is.True);
            Assert.That(ArrayUtility.Contains(names, "z"), Is.False);
            Assert.That(ArrayUtility.Contains(names, new string('b', 1)), Is.True, "value equality, not reference equality");
            Assert.That(ArrayUtility.IndexOf(names, "c"), Is.EqualTo(2));
            Assert.That(ArrayUtility.IndexOf(names, "a"), Is.EqualTo(0));
            Assert.That(ArrayUtility.IndexOf(names, "z"), Is.EqualTo(0));
            Assert.That(ArrayUtility.IndexOf(new string[0], "a"), Is.EqualTo(0));
            // IndexOf 0x8d6730..0x8d68ab has no landing pad, so the constrained Equals at 0x8d685c faults on a
            // null element (Array.IndexOf or a static Equals would answer 1).
            Assert.Throws<NullReferenceException>(() => ArrayUtility.IndexOf(new string[] { null, "a" }, "a"));
        }

        // Original Contains 0x8d63c0 wraps the loop body in a catch-all (landing pad 0x8d6547 jumps back to
        // the increment), so a null element is skipped rather than faulting on null.Equals.
        [Test]
        public void ContainsSkipsElementsWhoseEqualsFaults()
        {
            object match = "x";
            Assert.That(ArrayUtility.Contains(new object[] { null, match }, match), Is.True);
            Assert.That(ArrayUtility.Contains(new object[] { null, null }, match), Is.False);
            // null.Equals(null) faults and the fault is swallowed (0x8d651a, landing pad 0x8d6547), so the answer is
            // False; a static object.Equals(null, null) would answer True.
            Assert.That(ArrayUtility.Contains(new object[] { null }, null), Is.False);
            // The catch-all also covers an Equals override that throws; a static Equals would propagate it.
            Assert.That(ArrayUtility.Contains(new object[] { new ThrowingEquals(), match }, match), Is.True);
        }

        private sealed class ThrowingEquals
        {
            public override bool Equals(object obj)
            {
                throw new InvalidOperationException();
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }

        // Original SetLength 0x8d7600 allocates the new length first and copies min(newCount, old length);
        // ForEach 0x8d6620 calls the delegate in index order.
        [Test]
        public void SetLengthCopiesTheOverlapAndForEachWalksInOrder()
        {
            int[] array = { 1, 2 };
            ArrayUtility.SetLength(ref array, 4);
            Assert.That(array, Is.EqualTo(new[] { 1, 2, 0, 0 }));
            array = new[] { 1, 2, 3, 4 };
            ArrayUtility.SetLength(ref array, 2);
            Assert.That(array, Is.EqualTo(new[] { 1, 2 }));
            ArrayUtility.SetLength(ref array, 0);
            Assert.That(array, Is.Empty);

            int[] source = { 3, 1, 2 };
            System.Collections.Generic.List<int> seen = new System.Collections.Generic.List<int>();
            source.ForEach(item => seen.Add(item));
            Assert.That(seen, Is.EqualTo(new[] { 3, 1, 2 }));
            Assert.Throws<NullReferenceException>(() => source.ForEach(null));
        }

        // Original ShiftLeft 0x8d77d0 reads source[startIndex], moves every later element one place left,
        // then stores either that element (loop) or default(T) in the last slot. Original ShiftRight
        // 0x8d7a90 reads source[Length - 1] and then runs `for (i = startIndex + 1; i < Length; i++)
        // source[i] = source[i - 1]` in ascending order, so every slot after startIndex ends up equal
        // to the original source[startIndex]; only source[startIndex] receives the saved last element
        // (loop) or default(T). A true rotate right of {1, 2, 3, 4} would be {4, 1, 2, 3}.
        [Test]
        public void ShiftLeftRotatesAndShiftRightKeepsTheNativeAscendingSmear()
        {
            int[] left = { 1, 2, 3, 4 };
            left.ShiftLeft();
            Assert.That(left, Is.EqualTo(new[] { 2, 3, 4, 1 }));
            left = new[] { 1, 2, 3, 4 };
            left.ShiftLeft(0, false);
            Assert.That(left, Is.EqualTo(new[] { 2, 3, 4, 0 }));
            left = new[] { 1, 2, 3, 4 };
            left.ShiftLeft(1);
            Assert.That(left, Is.EqualTo(new[] { 1, 3, 4, 2 }));
            left = new[] { 1, 2, 3 };
            left.ShiftLeft(2, false);
            Assert.That(left, Is.EqualTo(new[] { 1, 2, 0 }), "starting at the last slot only clears it");
            Assert.Throws<IndexOutOfRangeException>(() => new int[0].ShiftLeft());

            int[] right = { 1, 2, 3, 4 };
            right.ShiftRight();
            Assert.That(right, Is.EqualTo(new[] { 4, 1, 1, 1 }));
            right = new[] { 1, 2, 3, 4 };
            right.ShiftRight(0, false);
            Assert.That(right, Is.EqualTo(new[] { 0, 1, 1, 1 }));
            right = new[] { 1, 2, 3, 4 };
            right.ShiftRight(1);
            Assert.That(right, Is.EqualTo(new[] { 1, 4, 2, 2 }));
            Assert.Throws<IndexOutOfRangeException>(() => new int[0].ShiftRight());
        }

        private sealed class FiveFoldKey : IComparable
        {
            public int Value;

            public int CompareTo(object other)
            {
                return Value.CompareTo(((FiveFoldKey)other).Value) * 5;
            }
        }

        // Original QuickSort 0x8d6b70 partitions on `CompareTo(pivot) == -1` and `== 1` (cmp [local], -1 / 1),
        // not `< 0` / `> 0`, so a key whose CompareTo answers -5 / 5 stops both scans at every element. The
        // expected arrays below are the native algorithm run by hand on {3, 1, 2} and {4, 3, 2, 1}.
        [Test]
        public void QuickSortSortsInPlaceAndComparesAgainstExactlyMinusOneAndOne()
        {
            int[] numbers = { 5, 3, 8, 1, 9, 2 };
            int[] sorted = numbers.QuickSort(value => value, 0, numbers.Length - 1);
            Assert.That(sorted, Is.SameAs(numbers), "the same array is returned");
            Assert.That(numbers, Is.EqualTo(new[] { 1, 2, 3, 5, 8, 9 }));

            string[] words = { "ccc", "a", "bb" };
            words.QuickSort(word => word.Length, 0, 2);
            Assert.That(words, Is.EqualTo(new[] { "a", "bb", "ccc" }));

            int[] odd = { 3, 1, 2 };
            odd.QuickSort(value => new FiveFoldKey { Value = value }, 0, 2);
            Assert.That(odd, Is.EqualTo(new[] { 2, 1, 3 }), "-5 and 5 are not -1 and 1, so the scans never advance");
            odd = new[] { 4, 3, 2, 1 };
            odd.QuickSort(value => new FiveFoldKey { Value = value }, 0, 3);
            Assert.That(odd, Is.EqualTo(new[] { 2, 1, 4, 3 }));

            int[] range = { 9, 8, 7, 6 };
            range.QuickSort(value => value, 1, 2);
            Assert.That(range, Is.EqualTo(new[] { 9, 7, 8, 6 }), "only the requested index range is sorted");
        }

        // Original DuplicateCurve 0x4f9c70 builds a new AnimationCurve, copies post- then pre-wrap mode and
        // adds every key with AddKey(Keyframe); DuplicateGradient 0x4f9db0 ignores its argument and
        // returns null (xor eax, eax; ret).
        [Test]
        public void DuplicateCurveCopiesKeysAndWrapModesAndGradientAnswersNull()
        {
            AnimationCurve source = new AnimationCurve(new Keyframe(0f, 1f, 0f, 2f), new Keyframe(1f, 3f, 4f, 0f), new Keyframe(2f, -1f, 1f, -1f));
            source.preWrapMode = WrapMode.Loop;
            source.postWrapMode = WrapMode.PingPong;
            AnimationCurve copy = DuplicateUtility.DuplicateCurve(source);
            Assert.That(copy, Is.Not.SameAs(source));
            Assert.That(copy.preWrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(copy.postWrapMode, Is.EqualTo(WrapMode.PingPong));
            Assert.That(copy.length, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(copy.keys[i].time, Is.EqualTo(source.keys[i].time), "time " + i);
                Assert.That(copy.keys[i].value, Is.EqualTo(source.keys[i].value), "value " + i);
                Assert.That(copy.keys[i].inTangent, Is.EqualTo(source.keys[i].inTangent), "inTangent " + i);
                Assert.That(copy.keys[i].outTangent, Is.EqualTo(source.keys[i].outTangent), "outTangent " + i);
            }

            source.AddKey(5f, 5f);
            Assert.That(copy.length, Is.EqualTo(3), "the copy does not share keys");
            Assert.Throws<NullReferenceException>(() => DuplicateUtility.DuplicateCurve(null));

            Assert.That(DuplicateUtility.DuplicateGradient(new Gradient()), Is.Null);
            Assert.That(DuplicateUtility.DuplicateGradient(null), Is.Null);
        }

        // Original ProjectOnLine 0x4f9dc0: a degenerate line (|to - from|^2 < Mathf.Epsilon) projects to
        // the zero offset, i.e. to fromPoint; a projection behind fromPoint (dot <= 0) returns fromPoint;
        // one past toPoint (squared offset greater than the squared line) returns toPoint.
        [Test]
        public void ProjectOnLineClampsToTheSegment()
        {
            Vector3 from = Vector3.zero;
            Vector3 to = new Vector3(10f, 0f, 0f);
            Near(LinearAlgebraUtility.ProjectOnLine(from, to, new Vector3(5f, 3f, 0f)), new Vector3(5f, 0f, 0f), "interior");
            Near(LinearAlgebraUtility.ProjectOnLine(from, to, new Vector3(-4f, 2f, 0f)), from, "behind from");
            Near(LinearAlgebraUtility.ProjectOnLine(from, to, new Vector3(14f, 1f, 0f)), to, "past to");
            Near(LinearAlgebraUtility.ProjectOnLine(from, to, new Vector3(0f, 5f, 0f)), from, "perpendicular at from (dot is 0)");
            Near(LinearAlgebraUtility.ProjectOnLine(from, to, new Vector3(10f, 5f, 0f)), to, "perpendicular at to");
            Near(LinearAlgebraUtility.ProjectOnLine(Vector3.zero, new Vector3(2f, 2f, 0f), new Vector3(2f, 0f, 0f)), new Vector3(1f, 1f, 0f), "diagonal");
            Near(LinearAlgebraUtility.ProjectOnLine(new Vector3(1f, 2f, 3f), new Vector3(1f, 2f, 3f), new Vector3(9f, 9f, 9f)), new Vector3(1f, 2f, 3f), "zero-length line");
        }

        // Original InverseLerp 0x4f9fb0 is dot(value - a, b - a) / |b - a|^2 with no clamp and no guard.
        [Test]
        public void VectorInverseLerpIsAnUnclampedDotRatio()
        {
            Vector3 a = Vector3.zero;
            Vector3 b = new Vector3(10f, 0f, 0f);
            Assert.That(LinearAlgebraUtility.InverseLerp(a, b, new Vector3(5f, 7f, 3f)), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(LinearAlgebraUtility.InverseLerp(a, b, new Vector3(20f, 0f, 0f)), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(LinearAlgebraUtility.InverseLerp(a, b, new Vector3(-10f, 0f, 0f)), Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(float.IsNaN(LinearAlgebraUtility.InverseLerp(a, a, a)), Is.True, "0 / 0");
            // The numerator is the dot with the same zero direction, so any value gives 0 / 0 rather than x / 0.
            Assert.That(float.IsNaN(LinearAlgebraUtility.InverseLerp(a, a, new Vector3(1f, 0f, 0f))), Is.True, "0 / 0 for any value");
        }

        // Original DistanceOnSphere 0x4fa000 does not use an arc length: coincident points (squared
        // distance below 0.00001f * 0.00001f, constant bits 0x2edbe6fe at 0x29a6e8c) answer 0, antipodal
        // points (squared sum below that) answer radius * PI, and anything else radius * sqrt(2) * sqrt(1 - dot).
        [Test]
        public void DistanceOnSphereUsesTheNativeThresholdsAndChordFormula()
        {
            Assert.That(LinearAlgebraUtility.DistanceOnSphere(Vector3.right, Vector3.right, 2f), Is.EqualTo(0f));
            Assert.That(LinearAlgebraUtility.DistanceOnSphere(Vector3.right, Vector3.left, 2f), Is.EqualTo(2f * Mathf.PI).Within(Tolerance));
            Assert.That(LinearAlgebraUtility.DistanceOnSphere(Vector3.right, Vector3.up, 2f), Is.EqualTo(2f * 1.41421354f).Within(Tolerance));
            Assert.That(LinearAlgebraUtility.DistanceOnSphere(Vector3.right, new Vector3(0f, -1f, 0f), 1f), Is.EqualTo(1.41421354f).Within(Tolerance));
            Assert.That(LinearAlgebraUtility.DistanceOnSphere(Vector3.right, new Vector3(0.5f, 0.8660254f, 0f), 1f), Is.EqualTo(1.41421354f * 0.70710678f).Within(Tolerance), "60 degrees apart");

            Assert.That(LinearAlgebraUtility.DistanceOnSphere(Vector3.zero, new Vector3(9e-6f, 0f, 0f), 1f), Is.EqualTo(0f), "squared distance 8.1e-11 is below 1e-10");
            Assert.That(LinearAlgebraUtility.DistanceOnSphere(Vector3.zero, new Vector3(1.1e-5f, 0f, 0f), 1f), Is.EqualTo(1.41421354f).Within(Tolerance), "squared distance 1.21e-10 is not");
        }

        // Original FlattenVector 0x4fa0c0 replaces one component; an undefined axis returns the input.
        [Test]
        public void FlattenVectorReplacesOneComponent()
        {
            Vector3 input = new Vector3(1f, 2f, 3f);
            Assert.That(LinearAlgebraUtility.FlattenVector(input, LinearAlgebraUtility.Axis.X), Is.EqualTo(new Vector3(0f, 2f, 3f)));
            Assert.That(LinearAlgebraUtility.FlattenVector(input, LinearAlgebraUtility.Axis.Y, 5f), Is.EqualTo(new Vector3(1f, 5f, 3f)));
            Assert.That(LinearAlgebraUtility.FlattenVector(input, LinearAlgebraUtility.Axis.Z, -2f), Is.EqualTo(new Vector3(1f, 2f, -2f)));
            Assert.That(LinearAlgebraUtility.FlattenVector(input, (LinearAlgebraUtility.Axis)7, 9f), Is.EqualTo(input));
            Assert.That((int)LinearAlgebraUtility.Axis.X, Is.EqualTo(0));
            Assert.That((int)LinearAlgebraUtility.Axis.Y, Is.EqualTo(1));
            Assert.That((int)LinearAlgebraUtility.Axis.Z, Is.EqualTo(2));
        }

        // Original TS_Bounds constructors 0x500270..0x5005b0 and CreateFromMinMax 0x500700: every field
        // starts as Vector3.zero; (Bounds) copies center and extents and derives max, min and size; (c, s)
        // takes size and halves it; (min, max, center) takes center as given instead of deriving it;
        // CreateFromMinMax sets center to min + extents.
        [Test]
        public void BoundsConstructorsDeriveTheirFieldsLikeTheNativeBodies()
        {
            TS_Bounds empty = new TS_Bounds();
            Assert.That(new[] { empty.center, empty.extents, empty.max, empty.min, empty.size }, Is.All.EqualTo(Vector3.zero));

            TS_Bounds fromBounds = new TS_Bounds(new Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 6f, 8f)));
            Assert.That(fromBounds.center, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(fromBounds.extents, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(fromBounds.max, Is.EqualTo(new Vector3(3f, 5f, 7f)));
            Assert.That(fromBounds.min, Is.EqualTo(new Vector3(-1f, -1f, -1f)));
            Assert.That(fromBounds.size, Is.EqualTo(new Vector3(4f, 6f, 8f)));

            TS_Bounds fromCenterSize = new TS_Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 6f, 8f));
            Assert.That(fromCenterSize.center, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(fromCenterSize.size, Is.EqualTo(new Vector3(4f, 6f, 8f)));
            Assert.That(fromCenterSize.extents, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(fromCenterSize.max, Is.EqualTo(new Vector3(3f, 5f, 7f)));
            Assert.That(fromCenterSize.min, Is.EqualTo(new Vector3(-1f, -1f, -1f)));

            TS_Bounds explicitCenter = new TS_Bounds(new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 4f), new Vector3(100f, 200f, 300f));
            Assert.That(explicitCenter.center, Is.EqualTo(new Vector3(100f, 200f, 300f)), "center is not derived from min and max");
            Assert.That(explicitCenter.size, Is.EqualTo(new Vector3(4f, 4f, 4f)));
            Assert.That(explicitCenter.extents, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(explicitCenter.min, Is.EqualTo(Vector3.zero));
            Assert.That(explicitCenter.max, Is.EqualTo(new Vector3(4f, 4f, 4f)));

            TS_Bounds minMax = new TS_Bounds();
            minMax.CreateFromMinMax(new Vector3(-2f, 0f, 2f), new Vector3(2f, 4f, 6f));
            Assert.That(minMax.size, Is.EqualTo(new Vector3(4f, 4f, 4f)));
            Assert.That(minMax.extents, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(minMax.center, Is.EqualTo(new Vector3(0f, 2f, 4f)));
            Assert.That(minMax.min, Is.EqualTo(new Vector3(-2f, 0f, 2f)));
            Assert.That(minMax.max, Is.EqualTo(new Vector3(2f, 4f, 6f)));
        }

        // Original Contains 0x500760 rejects `min.x > p.x` and `p.x > max.x` (then y, then z) and answers
        // true otherwise, so faces are inclusive and a NaN component never fails a comparison.
        [Test]
        public void BoundsContainsIsInclusiveAndAcceptsNaN()
        {
            TS_Bounds bounds = new TS_Bounds();
            bounds.CreateFromMinMax(new Vector3(-1f, -2f, -3f), new Vector3(1f, 2f, 3f));
            Assert.That(bounds.Contains(Vector3.zero), Is.True);
            Assert.That(bounds.Contains(new Vector3(-1f, -2f, -3f)), Is.True, "min corner");
            Assert.That(bounds.Contains(new Vector3(1f, 2f, 3f)), Is.True, "max corner");
            Assert.That(bounds.Contains(new Vector3(1.01f, 0f, 0f)), Is.False, "x above");
            Assert.That(bounds.Contains(new Vector3(-1.01f, 0f, 0f)), Is.False, "x below");
            Assert.That(bounds.Contains(new Vector3(0f, 2.01f, 0f)), Is.False, "y above");
            Assert.That(bounds.Contains(new Vector3(0f, -2.01f, 0f)), Is.False, "y below");
            Assert.That(bounds.Contains(new Vector3(0f, 0f, 3.01f)), Is.False, "z above");
            Assert.That(bounds.Contains(new Vector3(0f, 0f, -3.01f)), Is.False, "z below");
            Assert.That(bounds.Contains(new Vector3(float.NaN, float.NaN, float.NaN)), Is.True, "NaN fails neither comparison");
        }

        // Original TS_Bounds metadata: [Serializable] class with five public Vector3 fields in this order.
        [Test]
        public void BoundsKeepsTheSerializedFieldOrder()
        {
            Assert.That(typeof(TS_Bounds).IsSerializable, Is.True);
            string[] names = Array.ConvertAll(typeof(TS_Bounds).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly), field => field.Name);
            Assert.That(names, Is.EqualTo(new[] { "center", "extents", "max", "min", "size" }));
        }
    }
}
