using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class MovementFoundationVerification
    {
        private static int checks;
        private static void Require(bool condition, string description)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(description);
        }
        private static bool Near(float value, float expected, float tolerance = 0.0001f)
        {
            return Math.Abs(value - expected) <= tolerance;
        }
        private static bool Same(Quaternion a, Quaternion b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        }
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static void Throws<T>(Action action, string description) where T : Exception
        {
            bool passed = false;
            try { action(); }
            catch (T) { passed = true; }
            Require(passed, description);
        }

        public static int RunManaged()
        {
            checks = 0;
            var types = new[] { typeof(NumericExtensions), typeof(QuaternionExtensions), typeof(Vector2Extensions), typeof(Vector2IntExtensions) };
            var names = new[] { "CompareTo", "IsValid", "ExtractRotationFromMatrix", "SignedAngle", "SmoothDamp", "TryParse", "ToDebugString", "IsValid", "CeilToInt", "RoundToInt", "FloorToInt", "ToXZ", "Divide", "Min", "Max", "FlipX", "FlipY", "Abs", "IsValid", "WithinTolerance", "MaxComponent" };
            int offset = 0;
            foreach (var type in types)
            {
                Require(type.Assembly.GetName().Name == "HLUnityCore.Runtime", type.Name + " original assembly identity");
                Require(type.IsPublic && type.IsAbstract && type.IsSealed && type.BaseType == typeof(object), type.Name + " original static type flags");
                Require(type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Length == 0 && type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length == 0, type.Name + " original fieldless/no-constructor graph");
                Require(type.GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(new[] { typeof(ExtensionAttribute) }), type.Name + " complete original class attributes");
                var methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).OrderBy(m => m.MetadataToken).ToArray();
                int expected = type == typeof(NumericExtensions) ? 2 : type == typeof(QuaternionExtensions) ? 6 : type == typeof(Vector2Extensions) ? 12 : 1;
                Require(methods.Length == expected && methods.Select(m => m.Name).SequenceEqual(names.Skip(offset).Take(expected)), type.Name + " complete original ordered API");
                foreach (var method in methods)
                {
                    Require(method.IsPublic && method.IsStatic && !method.IsGenericMethod && method.GetMethodBody() != null, method.Name + " concrete original method flags");
                    bool extension = type != typeof(QuaternionExtensions) || method.Name == "ToDebugString" || method.Name == "IsValid";
                    Require(method.GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(extension ? new[] { typeof(ExtensionAttribute) } : Type.EmptyTypes), type.Name + "." + method.Name + " exact original extension metadata");
                }
                offset += expected;
            }
            Require(offset == 21, "twenty-one genuine foundation methods, separate signed formatter not recounted here");
            var debug = typeof(QuaternionExtensions).GetMethod("ToDebugString");
            Require(debug.GetParameters()[1].Name == "numDecimalPlaces" && Equals(debug.GetParameters()[1].DefaultValue, 2), "original decimal default and name");
            var tolerance = typeof(Vector2Extensions).GetMethod("WithinTolerance");
            Require(tolerance.GetParameters()[2].Name == "tolerance" && Equals(tolerance.GetParameters()[2].DefaultValue, 0.0001f), "original tolerance default and name");
            var parseRef = typeof(QuaternionExtensions).GetMethod("TryParse").GetParameters()[1];
            Require(parseRef.ParameterType == typeof(Quaternion).MakeByRefType() && !parseRef.IsOut && !parseRef.IsOptional, "original parse ref contract");
            var matrixRef = typeof(QuaternionExtensions).GetMethod("ExtractRotationFromMatrix").GetParameters()[0];
            Require(matrixRef.ParameterType == typeof(Matrix4x4).MakeByRefType() && !matrixRef.IsOut && !matrixRef.IsOptional, "original matrix ref contract");

            Require(new Vector2(-1.999f, 1.999f).FloorToInt() == new Vector2Int(-1, 1), "misnamed floor preserves negative truncation");
            Require(new Vector2(-0.9f, 0.9f).FloorToInt() == Vector2Int.zero, "truncation near zero");
            Require(new Vector2(-1.999f, 1.001f).CeilToInt() == new Vector2Int(-1, 2), "ceiling separately rounds signed coordinates");
            Require(new Vector2(2.5f, 3.5f).RoundToInt() == new Vector2Int(2, 4), "ties round to even");
            Require(new Vector2(-2.5f, -3.5f).RoundToInt() == new Vector2Int(-2, -4), "negative ties round to even");
            Require(new Vector2Int(int.MinValue, int.MaxValue).MaxComponent() == int.MaxValue, "signed integer maximum extremes");
            Require(new Vector2Int(-3, -7).MaxComponent() == -3 && new Vector2Int(8, 8).MaxComponent() == 8, "signed maximum and tie");
            var lifted = new Vector2(-2, 5).ToXZ();
            Require(lifted.x == -2 && lifted.y == 0 && lifted.z == 5, "XZ lift retains component order");
            Require(new Vector2(12, -9).Divide(new Vector2(3, -3)) == new Vector2(4, 3), "component division");
            var divided = new Vector2(1, 0).Divide(Vector2.zero);
            Require(float.IsPositiveInfinity(divided.x) && float.IsNaN(divided.y), "division preserves infinity and indeterminate zero");
            Require(Vector2Extensions.Min(new Vector2(-1, 7), new Vector2(3, -9)) == new Vector2(-1, -9), "component minimum");
            Require(Vector2Extensions.Max(new Vector2(-1, 7), new Vector2(3, -9)) == new Vector2(3, 7), "component maximum");
            Require(Vector2Extensions.Min(new Vector2(float.NaN, 7), new Vector2(3, float.NaN)).x == 3 && float.IsNaN(Vector2Extensions.Min(new Vector2(float.NaN, 7), new Vector2(3, float.NaN)).y), "minimum chooses second operand for unordered values");
            Require(Vector2Extensions.Max(new Vector2(float.NaN, 7), new Vector2(3, float.NaN)).x == 3 && float.IsNaN(Vector2Extensions.Max(new Vector2(float.NaN, 7), new Vector2(3, float.NaN)).y), "maximum chooses second operand for unordered values");
            Require(new Vector2(-2, 7).FlipX() == new Vector2(2, 7), "flip X changes only first component");
            Require(new Vector2(-2, 7).FlipY() == new Vector2(-2, -7), "flip Y changes only second component");
            Require(Bits(Vector2.zero.FlipX().x) == unchecked((int)0x80000000) && Bits(Vector2.zero.FlipY().y) == unchecked((int)0x80000000), "flips retain negative zero");
            var absolute = new Vector2(-0f, -3).Abs();
            Require(Bits(absolute.x) == 0 && absolute.y == 3, "absolute clears zero sign");
            Require(new Vector2(3, 4).WithinTolerance(Vector2.zero, 5.001f), "inside Euclidean radius");
            Require(!new Vector2(3, 4).WithinTolerance(Vector2.zero, 5f), "strict Euclidean boundary");
            Require(new Vector2(3, 4).WithinTolerance(Vector2.zero, -5.001f), "negative radius squares identically");
            Require(!Vector2.zero.WithinTolerance(Vector2.zero, 0f), "zero radius rejects even identical vectors");
            Require(Vector2.zero.WithinTolerance(Vector2.zero), "positive default accepts identity");
            Require(!new Vector2(float.NaN, 0).WithinTolerance(Vector2.zero, 1f), "unordered distance rejected");
            Require(new Vector2(100, 100).WithinTolerance(Vector2.zero, float.PositiveInfinity), "infinite radius accepts finite distance");
            Require(!new Vector2(float.PositiveInfinity, 0).WithinTolerance(Vector2.zero, float.PositiveInfinity), "infinite distance strict boundary");
            Require(!new Vector2(1, 0).WithinTolerance(Vector2.zero, float.NaN), "NaN tolerance rejected");

            float[] finite = { 0f, -0f, float.Epsilon, -float.Epsilon, float.MaxValue, float.MinValue, 123.5f };
            foreach (float value in finite) Require(value.IsValid(), "finite Single pattern " + Bits(value));
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Require(!value.IsValid(), "exponent-all-ones Single rejected");
                Require(!new Vector2(value, 0).IsValid() && !new Vector2(0, value).IsValid(), "invalid vector component rejected in either coordinate");
                for (int i = 0; i < 4; i++)
                {
                    Quaternion q = Quaternion.identity; q[i] = value;
                    Require(!q.IsValid(), "invalid quaternion component " + i);
                }
            }
            Require(Vector2.zero.IsValid() && new Vector2(float.MaxValue, float.Epsilon).IsValid(), "finite vectors accepted");
            Require(new Quaternion(0, 0, 0, 0).IsValid() && new Quaternion(float.MaxValue, 1, 2, 3).IsValid(), "validity does not require normalization or nonzero length");
            Require(NumericExtensions.CompareTo(3, 4, 1) == 0, "inclusive numeric tolerance");
            Require(NumericExtensions.CompareTo(3, 4, 0.5f) == -1 && NumericExtensions.CompareTo(4, 3, 0.5f) == 1, "numeric signed ordering outside tolerance");
            Require(NumericExtensions.CompareTo(3, 3, -1) == -1, "negative tolerance makes equal values take original negative branch");
            Require(NumericExtensions.CompareTo(float.NaN, 3, 1) == -1 && NumericExtensions.CompareTo(3, float.NaN, 1) == -1, "unordered values take original negative branch");
            Require(NumericExtensions.CompareTo(3, 3, float.NaN) == -1 && NumericExtensions.CompareTo(float.PositiveInfinity, float.PositiveInfinity, 1) == -1, "unordered tolerance and infinity subtraction retain original ordering");

            Quaternion velocity = default;
            Quaternion damped = QuaternionExtensions.SmoothDamp(new Quaternion(0, 0, 0, 2), new Quaternion(0, 0, 0, 2), ref velocity, 0.2f, 20, 0.016f);
            Require(Same(damped, new Quaternion(0, 0, 0, 2)) && Same(velocity, default), "stationary nonunit quaternion remains nonunit");
            velocity = default;
            damped = QuaternionExtensions.SmoothDamp(new Quaternion(0, 0, 0, 2), new Quaternion(0, 0, 0, -2), ref velocity, 0.2f, 20, 0.016f);
            Require(damped.w == 2 && Same(velocity, default), "negative dot chooses antipodal target sign");
            velocity = default;
            damped = QuaternionExtensions.SmoothDamp(Quaternion.identity, default, ref velocity, 0.2f, 20, 0.016f);
            Require(damped.w > 0 && damped.w < 1 && Near(velocity.w, 0, 0.00001f), "decay remains unnormalized with parallel velocity removed");
            velocity = default;
            damped = QuaternionExtensions.SmoothDamp(default, default, ref velocity, 0.2f, 20, 0.016f);
            Require(Same(damped, default) && float.IsNaN(velocity.x) && float.IsNaN(velocity.y) && float.IsNaN(velocity.z) && float.IsNaN(velocity.w), "zero result retains unguarded NaN velocity projection");
            velocity = new Quaternion(2, -1, 3, 0.25f);
            damped = QuaternionExtensions.SmoothDamp(new Quaternion(1, 2, 3, 4), new Quaternion(2, 3, 4, 5), ref velocity, 0.2f, 20, 0.016f);
            Require(Near(Quaternion.Dot(velocity, damped), 0, 0.00005f), "velocity projected tangent to unnormalized output");
            Require(Quaternion.Dot(damped, damped) > 20f, "output length is intentionally not forced to one");
            velocity = default;
            damped = QuaternionExtensions.SmoothDamp(new Quaternion(1, 2, 3, 4), new Quaternion(5, 6, 7, 8), ref velocity, 0.2f, 0, 0.016f);
            Require(Same(damped, new Quaternion(1, 2, 3, 4)) && Same(velocity, default), "zero maximum speed holds every nonunit component");
            velocity = new Quaternion(4, 3, 2, 1);
            damped = QuaternionExtensions.SmoothDamp(new Quaternion(1, 2, 3, 4), new Quaternion(5, 6, 7, 8), ref velocity, 0.2f, 20, 0);
            Require(Same(damped, new Quaternion(1, 2, 3, 4)) && Near(velocity.x, 10f / 3f) && Near(velocity.y, 5f / 3f) && Near(velocity.z, 0) && Near(velocity.w, -5f / 3f), "zero elapsed time still projects the initial velocity");

            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var q = new Quaternion(7, 8, 9, 10);
                Require(QuaternionExtensions.TryParse("(1.5, -2.25, 3e1, 4)", ref q) && Same(q, new Quaternion(1.5f, -2.25f, 30, 4)), "complete current-culture parse");
                string[] invalid = { "", "1,2,3,4", "(1,2,3)", "(1,2,3,4,5)", " (1,2,3,4)", "(1,2,3,4) ", "(no,2,3,4)", "(1,no,3,4)", "(1,2,no,4)", "(1,2,3,no)", "()" };
                foreach (string text in invalid)
                {
                    Quaternion before = q;
                    Require(!QuaternionExtensions.TryParse(text, ref q) && Same(before, q), "failed parse preserves ref " + text);
                }
                Quaternion beforeNull = q;
                Throws<NullReferenceException>(() => QuaternionExtensions.TryParse(null, ref q), "null parse retains null-reference boundary");
                Require(Same(beforeNull, q), "null parse preserves ref");
                Require(QuaternionExtensions.TryParse("(NaN,Infinity,-Infinity,0)", ref q) && float.IsNaN(q.x) && float.IsPositiveInfinity(q.y) && float.IsNegativeInfinity(q.z), "parse does not add finite-value validation");
                Require(new Quaternion(1.5f, -2.25f, 30, 4).ToDebugString() == "(1.50, -2.25, 30.00, 4.00)", "debug default decimals and comma spacing");
                Require(new Quaternion(1.125f, 2, 3, 4).ToDebugString(3) == "(1.125, 2.000, 3.000, 4.000)", "debug explicit decimals");
                OpString held = OpString.i + "previous content";
                Require(new Quaternion(1, 2, 3, 4).ToDebugString(0) == "(1, 2, 3, 4)" && held.ToString() == "({0:F0}, {1:F0}, {2:F0}, {3:F0})", "debug uses genuine reused OpString and clears prior content");
                Require(Quaternion.identity.ToDebugString(-1) == "(F-1, F-1, F-1, F-1)", "negative decimal count becomes the original custom numeric format");
                foreach (int value in new[] { 0, 1, -1, 10, -10, 987654321, -987654321, int.MaxValue })
                {
                    OpString builder = OpString.Create(64);
                    Require(ReferenceEquals(builder + value, builder) && builder.ToString() == value.ToString(CultureInfo.InvariantCulture), "genuine signed formatter " + value);
                }
                OpString minimum = OpString.Create(64);
                string minimumText = (minimum + int.MinValue).ToString();
                Require(minimumText == "-" || minimumText == "-0", "signed formatter preserves native/backend minimum-value cast boundary");
                OpString none = null;
                Throws<NullReferenceException>(() => { var ignored = none + 0; }, "zero formatter original null receiver boundary");
                Throws<NullReferenceException>(() => { var ignored = none + 1; }, "positive formatter original null receiver boundary");
                Throws<NullReferenceException>(() => { var ignored = none + int.MinValue; }, "minimum formatter original null receiver boundary");
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                q = default;
                Require(QuaternionExtensions.TryParse("(1, 2, 3, 4)", ref q) && Same(q, new Quaternion(1, 2, 3, 4)), "French culture integer components");
                Require(!QuaternionExtensions.TryParse("(1.5, 2, 3, 4)", ref q), "parse retains current-culture decimal syntax");
                Require(new Quaternion(1.5f, -2.25f, 30, 4).ToDebugString() == "(1,50, -2,25, 30,00, 4,00)", "debug retains current-culture decimal comma");
            }
            finally { CultureInfo.CurrentCulture = original; }
            return checks;
        }

        // Actual Unity-only quaternion conversion and LookRotation branches.
        // Pure managed checks above never invoke this method or native icalls.
        public static int Run()
        {
            RunManaged();
            Matrix4x4 zero = default;
            Require(Same(QuaternionExtensions.ExtractRotationFromMatrix(ref zero), Quaternion.identity), "zero forward matrix uses pure identity branch");
            zero.m02 = 0.000001f;
            Require(Same(QuaternionExtensions.ExtractRotationFromMatrix(ref zero), Quaternion.identity), "near-zero forward follows Unity equality threshold");
            zero.m02 = 0;
            Require(zero.m01 == 0 && zero.m12 == 0, "matrix ref is not modified");
            Quaternion yaw = Quaternion.Euler(0, 90, 0);
            Require(Near(QuaternionExtensions.SignedAngle(Quaternion.identity, yaw, Vector3.up), 90), "actual positive yaw axis");
            Require(Near(QuaternionExtensions.SignedAngle(Quaternion.identity, yaw, Vector3.down), -90), "actual negative yaw axis");
            Require(Near(QuaternionExtensions.SignedAngle(Quaternion.identity, yaw, Vector3.zero), 90), "actual zero comparison axis keeps angle");
            Require(Near(QuaternionExtensions.SignedAngle(Quaternion.identity, Quaternion.Euler(0, 270, 0), Vector3.up), -90), "actual wrap beyond half circle");
            Require(Near(QuaternionExtensions.SignedAngle(Quaternion.Euler(0, 45, 0), Quaternion.Euler(0, 135, 0), Vector3.up), 90), "actual offset relative rotations");
            Require(Near(QuaternionExtensions.SignedAngle(Quaternion.Euler(0, 45, 0), Quaternion.identity, Vector3.up), -45), "actual reverse relative rotations");
            Require(Near(QuaternionExtensions.SignedAngle(yaw, yaw, Vector3.up), 0), "actual matching rotations");
            Quaternion pitch = Quaternion.AngleAxis(90, Vector3.right);
            Require(Near(QuaternionExtensions.SignedAngle(pitch, yaw, Vector3.forward), 120), "noncommuting rotations preserve B times inverse A order");
            Require(Near(QuaternionExtensions.SignedAngle(yaw, pitch, Vector3.forward), -120), "reversing noncommuting rotations reverses the axis sign");
            Quaternion intended = Quaternion.Euler(20, 40, 15);
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(6, -3, 8), intended, new Vector3(2, 3, 4));
            Matrix4x4 before = matrix;
            Quaternion extracted = QuaternionExtensions.ExtractRotationFromMatrix(ref matrix);
            Require(Quaternion.Angle(extracted, intended) < 0.001f, "actual matrix column rotation ignores translation and nonuniform positive scale");
            Require(matrix == before, "actual extraction leaves full matrix intact");
            Debug.Log("PASS original movement foundation checks=" + checks);
            return checks;
        }
    }
}
