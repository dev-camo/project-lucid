using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class Vector3FoundationVerification
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
        private static bool Same(Vector3 a, Vector3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }
        private static string CanonicalType(Type type)
        {
            return type.IsGenericType ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(CanonicalType)) + ">" : type.FullName;
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
            Type type = typeof(Vector3Extensions);
            Require(type.Assembly.GetName().Name == "HLUnityCore.Runtime" && type.FullName == "Hardlight.Vector3Extensions", "original vector assembly/type identity");
            Require(type.IsPublic && type.IsAbstract && type.IsSealed && type.BaseType == typeof(object), "original vector type flags");
            Require(type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Length == 0 && type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length == 0, "full original fieldless vector schema");
            Require(type.GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(new[] { typeof(ExtensionAttribute) }), "original class extension attribute");
            var methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).OrderBy(m => m.MetadataToken).ToArray();
            string[] names = { "xy", "xz", "yz", "yx", "zx", "zy", "XZ", "Divide", "SlerpClamped", "LerpUnclamped", "Min", "Max", "WithinTolerance", "ClampToZero", "ClampToInt", "SmoothDampIndependentAxis", "SmoothDampAngleIndependentAxis", "SmoothDampAngleIndependentAxis", "ExtractTranslationFromMatrix", "ExtractScale", "ExtractScaleNegativeX", "MoveTowards", "Abs", "SignedVectorAngle", "SignOfAngle", "SignedVectorAngleOnPlane", "ProjectPointOnLine", "TryParse", "ToDebugString", "PointOnCircleWithFixedY", "PointOnCircleWithFixedYLHS", "IsValid" };
            string[] parameters = { "aVector", "aVector", "aVector", "aVector", "aVector", "aVector", "aVector", "a,b", "fromFn,toFn,t", "from,to,t", "a,b", "a,b", "a,b,tolerance", "aVector,zeroTolerance", "aVector,zeroTolerance", "current,target,currentVelocity,smoothTime", "current,target,currentVelocity,smoothTime", "current,target,currentVelocity,smoothTime,deltaTime", "matrix", "matrix", "matrix", "from,to,amount", "vec", "referenceVector,otherVector,normal", "referenceVector,otherVector,normal", "referenceVector,otherVector,normal", "vA,vB,vPoint,lineLength", "sVector,result", "v,numDecimalPlaces", "centre,angleInRadians,distance,fixedY", "centre,angleInRadians,distance,fixedY", "aVector" };
            string[] returnTypes = { "UnityEngine.Vector2", "UnityEngine.Vector2", "UnityEngine.Vector2", "UnityEngine.Vector2", "UnityEngine.Vector2", "UnityEngine.Vector2", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "System.Boolean", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "System.Single", "System.Single", "System.Single", "System.Boolean", "System.Boolean", "System.String", "UnityEngine.Vector3", "UnityEngine.Vector3", "System.Boolean" };
            string[] parameterTypes = { "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3", "System.Func`1<UnityEngine.Vector3>,System.Func`1<UnityEngine.Vector3>,System.Single", "UnityEngine.Vector3,UnityEngine.Vector3,System.Single", "UnityEngine.Vector3,UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3,System.Single", "UnityEngine.Vector3,System.Single", "UnityEngine.Vector3,System.Single", "UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3&,UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3&,UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3&,UnityEngine.Vector3,System.Single", "UnityEngine.Matrix4x4&", "UnityEngine.Matrix4x4&", "UnityEngine.Matrix4x4&", "UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3", "UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3", "UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3&,System.Single", "System.String,UnityEngine.Vector3&", "UnityEngine.Vector3,System.Int32", "UnityEngine.Vector3,System.Single,System.Single,System.Single", "UnityEngine.Vector3,System.Single,System.Single,System.Single", "UnityEngine.Vector3" };
            int[] regular = { 8, 18, 19, 20, 21, 23, 24, 25, 26, 27 };
            Require(methods.Length == 32 && methods.Select(m => m.Name).SequenceEqual(names), "complete ordered original vector API");
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];
                Require(m.IsPublic && m.IsStatic && !m.IsGenericMethod && m.GetMethodBody() != null && (m.Attributes & MethodAttributes.HideBySig) != 0, names[i] + " original concrete method flags");
                Require(m.GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(regular.Contains(i) ? Type.EmptyTypes : new[] { typeof(ExtensionAttribute) }), names[i] + " exact original method attributes");
                Require(string.Join(",", m.GetParameters().Select(p => p.Name)) == parameters[i], names[i] + " exact original parameter names/order");
                Require(CanonicalType(m.ReturnType) == returnTypes[i] && string.Join(",", m.GetParameters().Select(p => CanonicalType(p.ParameterType))) == parameterTypes[i], names[i] + " exact original return/parameter type identities");
                Require(m.GetParameters().All(p => !p.IsOut), names[i] + " original ref rather than out contracts");
            }
            foreach (int index in new[] { 12, 13, 14 })
                Require(Equals(methods[index].GetParameters().Last().DefaultValue, 0.0001f), "original tolerance default " + index);
            Require(Equals(methods[26].GetParameters().Last().DefaultValue, 0f) && Equals(methods[28].GetParameters().Last().DefaultValue, 2), "original projection and formatting defaults");
            foreach (int index in new[] { 15, 16, 17 })
                Require(methods[index].GetParameters()[2].ParameterType == typeof(Vector3).MakeByRefType(), "original velocity ref " + index);
            foreach (int index in new[] { 18, 19, 20 })
                Require(methods[index].GetParameters()[0].ParameterType == typeof(Matrix4x4).MakeByRefType(), "original matrix ref " + index);
            Require(methods[26].GetParameters()[2].ParameterType == typeof(Vector3).MakeByRefType() && methods[27].GetParameters()[1].ParameterType == typeof(Vector3).MakeByRefType(), "original projection and parse refs");

            type = typeof(MathUtilities);
            Require(type.FullName == "Hardlight.MathUtilities" && type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original math assembly/type identity");
            Require(type.IsPublic && type.IsAbstract && type.IsSealed && type.BaseType == typeof(object) && type.GetCustomAttributesData().Count == 0, "original math type flags/attributes");
            var fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).OrderBy(f => f.MetadataToken).ToArray();
            string[] fieldNames = { "ZeroTolerance", "DegreesInCircle", "DegreesInSemiCircle", "RadiansInCircle", "RadiansInSemiCircle" };
            object[] values = { 0.0001f, 360, 180, 6.2831855f, 3.1415927f };
            Require(fields.Length == 5 && fields.Select(f => f.Name).SequenceEqual(fieldNames), "all five original math own fields");
            for (int i = 0; i < fields.Length; i++)
            {
                Require(fields[i].IsPublic && fields[i].IsStatic && fields[i].IsLiteral && !fields[i].IsInitOnly && fields[i].GetCustomAttributesData().Count == 0, fieldNames[i] + " original constant flags/attributes");
                Require(fields[i].FieldType == values[i].GetType() && Equals(fields[i].GetRawConstantValue(), values[i]), fieldNames[i] + " exact type/value");
            }
            methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).OrderBy(m => m.MetadataToken).ToArray();
            Require(methods.Length == 24, "complete original math inventory; full signatures and bodies checked separately");
            methods = methods.Where(m => m.Name == "ClampToInt" || m.Name == "ClampToZero").ToArray();
            foreach (var m in methods)
            {
                Require(m.IsPublic && m.IsStatic && m.ReturnType == typeof(float) && m.GetMethodBody() != null && m.GetCustomAttributesData().Count == 0, m.Name + " original math method metadata");
                Require(m.GetParameters().Select(p => p.Name).SequenceEqual(new[] { "value", "zeroTolerance" }) && m.GetParameters().All(p => p.ParameterType == typeof(float)) && Equals(m.GetParameters()[1].DefaultValue, 0.0001f), m.Name + " original math parameter/default metadata");
            }
            MethodInfo conversion = typeof(OpString).GetMethod("op_Implicit", BindingFlags.Static | BindingFlags.Public);
            Require(conversion != null && conversion.IsSpecialName && conversion.IsHideBySig && conversion.ReturnType == typeof(string) && conversion.GetCustomAttributesData().Count == 0, "original implicit conversion flags/return/attributes");
            Require(conversion.GetParameters().Length == 1 && conversion.GetParameters()[0].ParameterType == typeof(OpString) && conversion.GetParameters()[0].Name == "t", "original implicit conversion parameter");
            byte[] conversionIL = conversion.GetMethodBody().GetILAsByteArray();
            Require(conversionIL.Length == 7 && conversionIL[0] == 0x02 && conversionIL[1] == 0x6f && conversionIL[6] == 0x2a, "conversion preserves genuine virtual dispatch without a null fallback");
            MethodBase virtualTarget = conversion.Module.ResolveMethod(BitConverter.ToInt32(conversionIL, 2));
            Require(virtualTarget.Name == "ToString" && virtualTarget.IsVirtual && virtualTarget.DeclaringType == typeof(object) && typeof(OpString).GetMethod("ToString").GetBaseDefinition() == virtualTarget, "conversion targets original Object.ToString virtual slot overridden by OpString");
            OpString builder = OpString.Create(64) + "native conversion";
            string converted = builder;
            Require(converted == "native conversion" && converted == builder.ToString(), "implicit conversion reads genuine builder through ToString");
            OpString missing = null;
            Throws<NullReferenceException>(() => { string ignored = missing; }, "null implicit conversion throws at original virtual call boundary");

            Vector3 a = new Vector3(2, -3, 7);
            Vector2[] swizzles = { a.xy(), a.xz(), a.yz(), a.yx(), a.zx(), a.zy() };
            Vector2[] expected = { new Vector2(2, -3), new Vector2(2, 7), new Vector2(-3, 7), new Vector2(-3, 2), new Vector2(7, 2), new Vector2(7, -3) };
            for (int i = 0; i < 6; i++) Require(swizzles[i] == expected[i], "ordered swizzle " + names[i]);
            Require(Same(a.XZ(), new Vector3(2, 0, 7)), "XZ clears only Y");
            Require(Same(new Vector3(12, -9, 5).Divide(new Vector3(3, -3, 2)), new Vector3(4, 3, 2.5f)), "independent coordinate division");
            a = new Vector3(1, 0, -1).Divide(Vector3.zero);
            Require(float.IsPositiveInfinity(a.x) && float.IsNaN(a.y) && float.IsNegativeInfinity(a.z), "division preserves nonfinite results");
            Require(Same(new Vector3(1, 2, 3).LerpUnclamped(new Vector3(3, 4, 5), 2), new Vector3(5, 6, 7)), "lerp extrapolates above one");
            Require(Same(new Vector3(1, 2, 3).LerpUnclamped(new Vector3(3, 4, 5), -1), new Vector3(-1, 0, 1)), "lerp extrapolates below zero");
            Require(Same(Vector3Extensions.Min(new Vector3(-2, 8, 1), new Vector3(3, -9, 5)), new Vector3(-2, -9, 1)), "component minima");
            Require(Same(Vector3Extensions.Max(new Vector3(-2, 8, 1), new Vector3(3, -9, 5)), new Vector3(3, 8, 5)), "component maxima");
            a = Vector3Extensions.Min(new Vector3(float.NaN, 3, float.NaN), new Vector3(2, float.NaN, float.NaN));
            Require(a.x == 2 && float.IsNaN(a.y) && float.IsNaN(a.z), "minimum unordered components choose second operand");
            a = Vector3Extensions.Max(new Vector3(float.NaN, 3, float.NaN), new Vector3(2, float.NaN, float.NaN));
            Require(a.x == 2 && float.IsNaN(a.y) && float.IsNaN(a.z), "maximum unordered components choose second operand");
            Require(!new Vector3(2, 3, 6).WithinTolerance(Vector3.zero, 7) && new Vector3(2, 3, 6).WithinTolerance(Vector3.zero, 7.001f), "strict Euclidean three-axis radius");
            Require(new Vector3(2, 3, 6).WithinTolerance(Vector3.zero, -7.001f), "negative tolerance is squared");
            Require(!Vector3.zero.WithinTolerance(Vector3.zero, 0) && Vector3.zero.WithinTolerance(Vector3.zero), "zero versus positive default tolerance");
            Require(!new Vector3(float.NaN, 0, 0).WithinTolerance(Vector3.zero, 10) && !Vector3.zero.WithinTolerance(Vector3.zero, float.NaN), "unordered tolerance/distance rejected");
            Require(!new Vector3(float.PositiveInfinity, 0, 0).WithinTolerance(Vector3.zero, float.PositiveInfinity), "infinite strict radius boundary");

            Require(MathUtilities.ClampToInt(2.001f, 0.01f) == 2 && MathUtilities.ClampToInt(2.01f, 0.001f) == 2.01f, "integer snapping inside and outside strict window");
            Require(MathUtilities.ClampToInt(2.25f, 0.25f) == 2.25f && MathUtilities.ClampToInt(1.75f, 0.25f) == 1.75f, "both exact window boundaries remain unsnapped");
            Require(MathUtilities.ClampToInt(-2.001f, 0.01f) == -2.001f && MathUtilities.ClampToInt(-2f, 0.01f) == -2f, "negative half-add truncation does not select nearest negative integer");
            Require(MathUtilities.ClampToInt(-1.2f, 1.3f) == 0 && MathUtilities.ClampToInt(1.2f, 1.3f) == 1, "wide window exposes asymmetric signed integer candidate");
            Require(Bits(MathUtilities.ClampToInt(-0f, 0)) == unchecked((int)0x80000000), "zero window retains negative zero");
            Require(MathUtilities.ClampToInt(2.001f, -0.1f) == 2.001f && float.IsNaN(MathUtilities.ClampToInt(float.NaN)), "negative window and unordered input are unsnapped");
            Require(MathUtilities.ClampToZero(0.001f, 0.01f) == 0 && MathUtilities.ClampToZero(-0.001f, 0.01f) == 0, "zero snapping both signs");
            Require(MathUtilities.ClampToZero(0.01f, 0.01f) == 0.01f && MathUtilities.ClampToZero(-0.01f, 0.01f) == -0.01f, "strict zero-window boundaries");
            Require(Bits(MathUtilities.ClampToZero(-0f, 0)) == unchecked((int)0x80000000) && Bits(MathUtilities.ClampToZero(-0f)) == 0, "zero window preserves sign, positive default clears it");
            Require(MathUtilities.ClampToZero(0.001f, -1) == 0.001f && float.IsNaN(MathUtilities.ClampToZero(float.NaN)), "negative/NaN zero clamp behavior");
            Require(Same(new Vector3(0.001f, -0.001f, 0.01f).ClampToZero(0.01f), new Vector3(0, 0, 0.01f)), "vector zero clamp calls original scalar policy on all axes");
            Require(Same(new Vector3(2.001f, -2.001f, 0.001f).ClampToInt(0.01f), new Vector3(2, -2.001f, 0)), "vector integer clamp preserves signed scalar asymmetry");

            Vector3 velocity = new Vector3(2, -3, 4), before = velocity;
            Vector3 current = new Vector3(350, 12, 90), target = new Vector3(10, 12, 180), smooth = new Vector3(0.2f, 0.3f, 0.4f);
            Require(Same(current.SmoothDampAngleIndependentAxis(target, ref velocity, smooth, 0), current) && Same(velocity, before), "zero delta time returns before any velocity work");
            Require(Same(current.SmoothDampAngleIndependentAxis(target, ref velocity, smooth, -0f), current) && Same(velocity, before), "negative zero delta time also returns immediately");
            Require(Same(current.SmoothDampAngleIndependentAxis(current, ref velocity, smooth, 0.016f), current) && Same(velocity, before), "matching axes retain nonzero velocities");
            Vector3 damped = current.SmoothDampAngleIndependentAxis(target, ref velocity, smooth, 0.016f);
            Require(damped.x > 350 && damped.x < 360 && damped.z > 90 && damped.z < 180, "angles choose short wrap and independent forward progression");
            Require(damped.y == 12 && velocity.y == -3 && velocity.x > 2 && velocity.z > 4, "equal axis retains velocity while two other axes update");
            velocity = default;
            damped = Vector3.zero.SmoothDampAngleIndependentAxis(new Vector3(360, 0, 0), ref velocity, smooth, 0.016f);
            Require(damped.x == 0 && velocity.x == 0, "angular equivalence follows real damping rather than exact-equality shortcut");
            velocity = before;
            damped = current.SmoothDampAngleIndependentAxis(target, ref velocity, smooth, -0.016f);
            Require(damped.x != current.x && damped.z != current.z && velocity.y == before.y, "negative delta time is passed through without a new guard");

            Matrix4x4 matrix = default;
            matrix.m00 = 3; matrix.m10 = 4; matrix.m30 = 12;
            matrix.m11 = 5; matrix.m31 = 12;
            matrix.m22 = 8; matrix.m32 = 15;
            matrix.m03 = -7; matrix.m13 = 9; matrix.m23 = 11; matrix.m33 = -42;
            Matrix4x4 untouched = matrix;
            Require(Same(Vector3Extensions.ExtractTranslationFromMatrix(ref matrix), new Vector3(-7, 9, 11)), "translation reads column three without normalization");
            Require(Same(Vector3Extensions.ExtractScale(ref matrix), new Vector3(13, 13, 17)), "scale includes nonaffine fourth row in all three columns");
            Require(Same(Vector3Extensions.ExtractScaleNegativeX(ref matrix), new Vector3(13, 13, 17)), "positive three-column handedness retains scale sign");
            Require(matrix == untouched, "all extraction methods preserve matrix ref");
            matrix.m00 = -3; matrix.m10 = -4;
            Require(Same(Vector3Extensions.ExtractScaleNegativeX(ref matrix), new Vector3(-13, 13, 17)), "negative handedness flips X only, retaining 4D magnitudes");
            matrix.m22 = 0; matrix.m32 = 0;
            Require(Same(Vector3Extensions.ExtractScaleNegativeX(ref matrix), new Vector3(13, 13, 0)), "singular zero handedness does not flip X");
            Require(Same(Vector3Extensions.MoveTowards(new Vector3(0, 3, -2), new Vector3(5, 1, 3), new Vector3(2, 9, 1)), new Vector3(2, 1, -1)), "per-axis move amounts snap only the reached coordinate");
            Require(Same(Vector3Extensions.MoveTowards(Vector3.zero, new Vector3(1, -1, 0), new Vector3(-2, -3, -4)), new Vector3(-2, 3, -4)), "negative move amounts preserve movement away from target");
            a = new Vector3(-0f, -3, float.NegativeInfinity).Abs();
            Require(Bits(a.x) == 0 && a.y == 3 && float.IsPositiveInfinity(a.z), "absolute values preserve nonfinite policy and clear zero sign");

            Require(Near(Vector3Extensions.SignedVectorAngle(Vector3.right, Vector3.up, Vector3.forward), -90), "original signed angle handedness");
            Require(Near(Vector3Extensions.SignedVectorAngle(Vector3.up, Vector3.right, Vector3.forward), 90), "reverse orientation reverses sign");
            Require(Near(Vector3Extensions.SignedVectorAngle(Vector3.right, Vector3.left, Vector3.zero), 180), "zero sign dot chooses positive half turn");
            Require(Vector3Extensions.SignOfAngle(Vector3.right, Vector3.up, Vector3.forward) == -1 && Vector3Extensions.SignOfAngle(Vector3.right, Vector3.right, Vector3.forward) == 1, "negative and exactly zero sign branches");
            Require(Vector3Extensions.SignedVectorAngle(Vector3.zero, Vector3.up, Vector3.forward) == 0, "zero vector follows genuine engine Angle threshold");
            Require(Vector3Extensions.SignOfAngle(new Vector3(float.NaN, 0, 0), Vector3.up, Vector3.forward) == -1, "unordered sign follows original Mathf.Sign negative branch");
            Require(Near(Vector3Extensions.SignedVectorAngleOnPlane(new Vector3(1, 1, 0), new Vector3(0, 1, 1), Vector3.up), 90), "unit plane projection removes height before angle");
            Require(Near(Vector3Extensions.SignedVectorAngleOnPlane(new Vector3(1, 1, 0), new Vector3(0, 1, 1), new Vector3(0, 2, 0)), 25.841932f), "nonunit plane normal remains unnormalized");

            Vector3 point = new Vector3(5, 9, -3);
            Require(Vector3Extensions.ProjectPointOnLine(Vector3.zero, new Vector3(10, 0, 0), ref point) && Same(point, new Vector3(5, 0, 0)), "interior projection discards perpendicular offset");
            point = new Vector3(0, 8, 7);
            Require(Vector3Extensions.ProjectPointOnLine(Vector3.zero, new Vector3(10, 0, 0), ref point) && Same(point, Vector3.zero), "inclusive start endpoint");
            point = new Vector3(10, 8, 7);
            Require(Vector3Extensions.ProjectPointOnLine(Vector3.zero, new Vector3(10, 0, 0), ref point) && Same(point, new Vector3(10, 0, 0)), "inclusive final endpoint");
            foreach (float x in new[] { -1f, 11f })
            {
                point = new Vector3(x, 8, 7); before = point;
                Require(!Vector3Extensions.ProjectPointOnLine(Vector3.zero, new Vector3(10, 0, 0), ref point) && Same(point, before), "outside segment rejection preserves point " + x);
            }
            point = new Vector3(10, 1, 0);
            Require(Vector3Extensions.ProjectPointOnLine(Vector3.zero, new Vector3(10, 0, 0), ref point, 20) && Same(point, new Vector3(2.5f, 0, 0)), "supplied length is not recomputed or direction normalized");
            point = new Vector3(1, 2, 3); before = point;
            Require(!Vector3Extensions.ProjectPointOnLine(Vector3.zero, new Vector3(10, 0, 0), ref point, -10) && Same(point, before), "negative length retains native rejection");
            point = new Vector3(1, 2, 3);
            Require(Vector3Extensions.ProjectPointOnLine(Vector3.zero, Vector3.zero, ref point) && float.IsNaN(point.x) && float.IsNaN(point.y) && float.IsNaN(point.z), "degenerate line unordered comparisons pass and commit NaN");
            point = new Vector3(float.NaN, 2, 3);
            Require(Vector3Extensions.ProjectPointOnLine(Vector3.zero, new Vector3(10, 0, 0), ref point) && float.IsNaN(point.x) && float.IsNaN(point.y) && float.IsNaN(point.z), "unordered point commits original NaN projection");

            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                a = new Vector3(7, 8, 9);
                Require(Vector3Extensions.TryParse("(1.5, -2.25, 3e1)", ref a) && Same(a, new Vector3(1.5f, -2.25f, 30)), "three current-culture parsed values commit together");
                foreach (string text in new[] { "", "1,2,3", "(1,2)", "(1,2,3,4)", " (1,2,3)", "(1,2,3) ", "(bad,2,3)", "(1,bad,3)", "(1,2,bad)", "()" })
                {
                    before = a;
                    Require(!Vector3Extensions.TryParse(text, ref a) && Same(a, before), "invalid parse preserves ref " + text);
                }
                before = a;
                Throws<NullReferenceException>(() => Vector3Extensions.TryParse(null, ref a), "null string preserves original failure boundary");
                Require(Same(a, before), "null parse leaves ref intact");
                Require(Vector3Extensions.TryParse("(NaN,Infinity,-Infinity)", ref a) && float.IsNaN(a.x) && float.IsPositiveInfinity(a.y) && float.IsNegativeInfinity(a.z), "parsing does not introduce finite checks");
                Require(new Vector3(1.5f, -2.25f, 30).ToDebugString() == "(1.50, -2.25, 30.00)", "original debug formatting defaults");
                Require(new Vector3(1.125f, 2, 3).ToDebugString(3) == "(1.125, 2.000, 3.000)", "explicit debug precision");
                OpString held = OpString.i + "previous";
                Require(new Vector3(1, 2, 3).ToDebugString(0) == "(1, 2, 3)" && held.ToString() == "({0:F0}, {1:F0}, {2:F0})", "debug clears and reuses genuine shared OpString");
                Require(Vector3.zero.ToDebugString(-1) == "(F-1, F-1, F-1)", "negative precision preserves original custom numeric format");
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Require(new Vector3(1.5f, -2.25f, 30).ToDebugString() == "(1,50, -2,25, 30,00)", "debug retains culture-specific decimals");
                a = default;
                Require(Vector3Extensions.TryParse("(1, 2, 3)", ref a) && Same(a, new Vector3(1, 2, 3)) && !Vector3Extensions.TryParse("(1.5, 2, 3)", ref a), "parse retains culture-specific decimal syntax");
            }
            finally { CultureInfo.CurrentCulture = original; }

            Vector3 centre = new Vector3(10, float.NaN, -4);
            Require(Same(centre.PointOnCircleWithFixedY(0, 3, 7), new Vector3(13, 7, -4)), "normal circle zero angle uses cosine on X and fixed Y");
            Require(Same(centre.PointOnCircleWithFixedYLHS(0, 3, 7), new Vector3(10, 7, -1)), "LHS zero angle uses cosine on Z");
            a = centre.PointOnCircleWithFixedY(Mathf.PI / 2, -3, 7);
            Require(Near(a.x, 10) && a.y == 7 && Near(a.z, -7), "negative circle radius preserved");
            a = centre.PointOnCircleWithFixedYLHS(Mathf.PI / 2, 3, 7);
            Require(Near(a.x, 13) && a.y == 7 && Near(a.z, -4), "LHS quarter turn swaps sine and cosine");
            Require(Vector3.zero.IsValid() && new Vector3(float.MaxValue, float.Epsilon, -0f).IsValid(), "finite components do not require normalization");
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                for (int i = 0; i < 3; i++)
                {
                    a = Vector3.zero; a[i] = value;
                    Require(!a.IsValid(), "invalid coordinate " + i);
                }
            return checks;
        }

        // Actual engine-only branches: no fake Time or Vector3.Slerp implementation.
        public static int Run()
        {
            RunManaged();
            string order = "";
            Func<Vector3> from = () => { order += "F"; return Vector3.right; };
            Func<Vector3> to = () => { order += "T"; return Vector3.up; };
            Require(Same(Vector3Extensions.SlerpClamped(from, null, 0), Vector3.right) && order == "F", "zero endpoint invokes only from");
            order = "";
            Require(Same(Vector3Extensions.SlerpClamped(null, to, 1), Vector3.up) && order == "T", "one endpoint invokes only to");
            order = "";
            Require(Same(Vector3Extensions.SlerpClamped(from, null, float.NegativeInfinity), Vector3.right) && order == "F", "negative infinity clamps from");
            order = "";
            Require(Same(Vector3Extensions.SlerpClamped(null, to, float.PositiveInfinity), Vector3.up) && order == "T", "positive infinity clamps to");
            order = "";
            Vector3 middle = Vector3Extensions.SlerpClamped(from, to, 0.5f);
            Require(order == "FT" && Near(middle.x, 0.7071068f) && Near(middle.y, 0.7071068f) && Near(middle.z, 0), "actual spherical middle with ordered callbacks");
            order = "";
            Vector3Extensions.SlerpClamped(from, to, float.NaN);
            Require(order == "FT", "unordered fraction invokes both endpoints in order");
            order = "";
            Throws<InvalidOperationException>(() => Vector3Extensions.SlerpClamped(() => { order += "F"; throw new InvalidOperationException(); }, to, 0.5f), "from callback exception propagated");
            Require(order == "F", "from failure does not evaluate to");
            order = "";
            Throws<InvalidOperationException>(() => Vector3Extensions.SlerpClamped(from, () => { order += "T"; throw new InvalidOperationException(); }, 0.5f), "to callback exception propagated");
            Require(order == "FT", "to failure follows from callback");
            order = "";
            Throws<NullReferenceException>(() => Vector3Extensions.SlerpClamped(from, null, 0.5f), "middle null to failure retained");
            Require(order == "F", "middle null to evaluates from first");

            Vector3 current = new Vector3(0, 12, 350), target = new Vector3(8, 12, 10), smooth = new Vector3(0.2f, 0.3f, 0.4f);
            Vector3 velocity = new Vector3(2, -3, 4), before = velocity;
            Require(Same(current.SmoothDampIndependentAxis(current, ref velocity, smooth), current) && Same(velocity, before), "actual Time-based equal axes retain all velocities");
            Require(Same(current.SmoothDampAngleIndependentAxis(current, ref velocity, smooth), current) && Same(velocity, before), "actual angle equal axes retain all velocities");
            float dt = Time.deltaTime;
            Vector3 expectedVelocity = before;
            Vector3 expected = new Vector3(Mathf.SmoothDamp(current.x, target.x, ref expectedVelocity.x, smooth.x, float.PositiveInfinity, dt), target.y, Mathf.SmoothDamp(current.z, target.z, ref expectedVelocity.z, smooth.z, float.PositiveInfinity, dt));
            Vector3 damped = current.SmoothDampIndependentAxis(target, ref velocity, smooth);
            Require(Same(damped, expected) && Same(velocity, expectedVelocity), "actual scalar engine damping for changed X/Z");
            Require(damped.y == 12 && velocity.y == -3, "actual equal Y axis retains velocity");
            velocity = before; expectedVelocity = before;
            expected = new Vector3(Mathf.SmoothDampAngle(current.x, target.x, ref expectedVelocity.x, smooth.x, float.PositiveInfinity, dt), target.y, Mathf.SmoothDampAngle(current.z, target.z, ref expectedVelocity.z, smooth.z, float.PositiveInfinity, dt));
            damped = current.SmoothDampAngleIndependentAxis(target, ref velocity, smooth);
            Require(Same(damped, expected) && Same(velocity, expectedVelocity), "actual angle engine damping for changed X/Z");
            Require(damped.y == 12 && velocity.y == -3, "actual angle equal Y axis retains velocity");
            Debug.Log("PASS original vector3 foundation checks=" + checks);
            return checks;
        }
    }
}
