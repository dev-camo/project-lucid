using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Pure managed checks against genuine Unity/BCL types. Run is also callable
    // in the actual Editor; this helper creates no controller or engine substitute.
    public static class MathFoundationVerification
    {
        private static int checks;
        private static void Require(bool condition, string description)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(description);
        }
        private static void Near(float actual, float expected, string description, float tolerance = 0.00001f)
        {
            Require(!float.IsNaN(actual) && Math.Abs(actual - expected) <= tolerance, description);
        }
        private static void Vector(Vector3 actual, Vector3 expected, string description, float tolerance = 0.00001f)
        {
            Near(actual.x, expected.x, description + " x", tolerance);
            Near(actual.y, expected.y, description + " y", tolerance);
            Near(actual.z, expected.z, description + " z", tolerance);
        }
        private static void Vector(Vector2 actual, Vector2 expected, string description, float tolerance = 0.00001f)
        {
            Near(actual.x, expected.x, description + " x", tolerance);
            Near(actual.y, expected.y, description + " y", tolerance);
        }
        private static void Throws<T>(Action action, string description) where T : Exception
        {
            bool passed = false;
            try { action(); } catch (T) { passed = true; }
            Require(passed, description);
        }
        private static List<(OpCode opcode, object operand)> ReadIL(MethodBase method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null))
                .ToDictionary(c => unchecked((ushort)c.Value));
            byte[] bytes = method.GetMethodBody().GetILAsByteArray();
            var result = new List<(OpCode, object)>();
            for (int i = 0; i < bytes.Length;)
            {
                ushort value = bytes[i++];
                if (value == 0xfe) value = (ushort)(0xfe00 | bytes[i++]);
                OpCode code = codes[value];
                object operand = null;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineI: operand = (sbyte)bytes[i]; i++; break;
                    case OperandType.ShortInlineVar: operand = bytes[i]; i++; break;
                    case OperandType.ShortInlineBrTarget: operand = i + 1 + (sbyte)bytes[i]; i++; break;
                    case OperandType.InlineVar: operand = BitConverter.ToUInt16(bytes, i); i += 2; break;
                    case OperandType.InlineBrTarget: operand = i + 4 + BitConverter.ToInt32(bytes, i); i += 4; break;
                    case OperandType.InlineI: operand = BitConverter.ToInt32(bytes, i); i += 4; break;
                    case OperandType.InlineI8: operand = BitConverter.ToInt64(bytes, i); i += 8; break;
                    case OperandType.ShortInlineR: operand = BitConverter.ToSingle(bytes, i); i += 4; break;
                    case OperandType.InlineR: operand = BitConverter.ToDouble(bytes, i); i += 8; break;
                    case OperandType.InlineMethod: operand = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, i), method.DeclaringType.GetGenericArguments(), method.GetGenericArguments()); i += 4; break;
                    case OperandType.InlineField: operand = method.Module.ResolveField(BitConverter.ToInt32(bytes, i), method.DeclaringType.GetGenericArguments(), method.GetGenericArguments()); i += 4; break;
                    case OperandType.InlineType: operand = method.Module.ResolveType(BitConverter.ToInt32(bytes, i), method.DeclaringType.GetGenericArguments(), method.GetGenericArguments()); i += 4; break;
                    case OperandType.InlineTok: operand = method.Module.ResolveMember(BitConverter.ToInt32(bytes, i), method.DeclaringType.GetGenericArguments(), method.GetGenericArguments()); i += 4; break;
                    case OperandType.InlineString: operand = method.Module.ResolveString(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineSig: operand = method.Module.ResolveSignature(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(bytes, i); i += 4;
                        operand = Enumerable.Range(0, count).Select(n => i + 4 * count + BitConverter.ToInt32(bytes, i + 4 * n)).ToArray(); i += 4 * count;
                        break;
                    default: throw new InvalidOperationException("Unsupported genuine IL operand " + code.OperandType);
                }
                result.Add((code, operand));
            }
            return result;
        }
        private static string CallName(MethodBase method) => method.DeclaringType.FullName + "." + method.Name;

        // Test-only conversion operands expose call order and exceptions at the
        // genuine IConvertible boundary. They are never a maintained game service.
        private sealed class ConversionOperand : IConvertible
        {
            public static readonly List<string> Calls = new List<string>();
            private readonly string name;
            private readonly float value;
            private readonly bool fail;
            public ConversionOperand(string name, float value, bool fail = false)
            { this.name = name; this.value = value; this.fail = fail; }
            public TypeCode GetTypeCode() { return TypeCode.Object; }
            public float ToSingle(IFormatProvider provider)
            { Calls.Add(name); if (fail) throw new FormatException(name); return value; }
            public bool ToBoolean(IFormatProvider p) { throw new NotSupportedException(); }
            public byte ToByte(IFormatProvider p) { throw new NotSupportedException(); }
            public char ToChar(IFormatProvider p) { throw new NotSupportedException(); }
            public DateTime ToDateTime(IFormatProvider p) { throw new NotSupportedException(); }
            public decimal ToDecimal(IFormatProvider p) { throw new NotSupportedException(); }
            public double ToDouble(IFormatProvider p) { throw new NotSupportedException(); }
            public short ToInt16(IFormatProvider p) { throw new NotSupportedException(); }
            public int ToInt32(IFormatProvider p) { throw new NotSupportedException(); }
            public long ToInt64(IFormatProvider p) { throw new NotSupportedException(); }
            public sbyte ToSByte(IFormatProvider p) { throw new NotSupportedException(); }
            public string ToString(IFormatProvider p) { throw new NotSupportedException(); }
            public object ToType(Type t, IFormatProvider p) { throw new NotSupportedException(); }
            public ushort ToUInt16(IFormatProvider p) { throw new NotSupportedException(); }
            public uint ToUInt32(IFormatProvider p) { throw new NotSupportedException(); }
            public ulong ToUInt64(IFormatProvider p) { throw new NotSupportedException(); }
        }

        public static int RunManaged()
        {
            checks = 0;
            Type type = typeof(MathUtilities);
            Require(type.FullName == "Hardlight.MathUtilities" && type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original type/assembly identity");
            Require(type.IsPublic && type.IsSealed && type.IsAbstract && type.BaseType == typeof(object), "original static type flags");
            Require((type.Attributes & TypeAttributes.BeforeFieldInit) != 0 && type.TypeInitializer == null, "original BeforeFieldInit/constant-only schema");
            Require(type.GetCustomAttributesData().Count == 0, "original type has no custom attributes");
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Require(fields.Select(f => f.Name).SequenceEqual(new[] { "ZeroTolerance", "DegreesInCircle", "DegreesInSemiCircle", "RadiansInCircle", "RadiansInSemiCircle" }), "all ordered original fields");
            object[] constants = { 0.0001f, 360, 180, 6.2831855f, 3.1415927f };
            for (int i = 0; i < fields.Length; i++)
            {
                Require(fields[i].IsPublic && fields[i].IsStatic && fields[i].IsLiteral && !fields[i].IsInitOnly, "constant flags " + fields[i].Name);
                Require(Equals(fields[i].GetRawConstantValue(), constants[i]), "constant value " + fields[i].Name);
                Require(fields[i].GetCustomAttributesData().Count == 0, "constant attributes " + fields[i].Name);
            }
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Require(methods.Length == 24 && methods.All(m => m.IsPublic && m.IsStatic && m.IsHideBySig && !m.IsSpecialName && m.GetCustomAttributesData().Count == 0), "full original own24 methods and flags");
            string[] names = { "WithinTolerance", "WithinRangeWithTolerance" };
            foreach (string name in names)
                foreach (Type scalar in new[] { typeof(float), typeof(double) })
                {
                    MethodInfo method = methods.Single(m => m.Name == name && m.GetParameters()[0].ParameterType == scalar);
                    ParameterInfo p = method.GetParameters().Last();
                    Require(p.IsOptional && p.HasDefaultValue && Equals(p.DefaultValue, scalar == typeof(float) ? (object)0.0001f : (object)9.999999747378752E-05), "original exact optional tolerance " + name + scalar.Name);
                }
            MethodInfo lerp = methods.Single(m => m.Name == "Lerp");
            Type generic = lerp.GetGenericArguments().Single();
            Require(generic.Name == "T" && generic.GenericParameterAttributes == GenericParameterAttributes.None && generic.GetGenericParameterConstraints().SequenceEqual(new[] { typeof(IConvertible) }), "original IConvertible method parameter T");
            Require(lerp.ReturnType == generic && lerp.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { generic, generic, typeof(float) }), "original generic interpolation signature");
            var lerpIL = ReadIL(lerp.MakeGenericMethod(typeof(float)));
            var conversions = lerpIL.Where(x => x.operand is MethodBase).Select(x => (MethodBase)x.operand).ToArray();
            Require(conversions.Select(CallName).SequenceEqual(new[] { "System.Convert.ToSingle", "System.Convert.ToSingle", "UnityEngine.Mathf.Lerp", "System.Type.GetTypeFromHandle", "System.Convert.ChangeType" }), "original boxing/numeric conversion dependency order");
            Require(conversions.Take(2).All(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(object) })) && conversions.Last().GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(object), typeof(Type) }), "original exact genuine conversion overloads");
            Require(lerpIL.Take(6).Count(x => x.opcode == OpCodes.Box) == 2, "both original operands boxed before null testing");
            Require(!conversions.Any(m => m.DeclaringType.FullName.StartsWith("Hardlight.GenericMaths")), "no unrelated generic-operator dependency");
            foreach (string methodName in new[] { "RoundToInt", "FloorToInt", "Floor", "Ceil", "Sqrt", "Sin", "Cos" })
            {
                MethodInfo engine = typeof(Mathf).GetMethod(methodName, new[] { typeof(float) });
                Require(engine.GetMethodBody() != null && (engine.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) == 0, "genuine managed engine calculation " + methodName);
            }

            foreach (bool useDouble in new[] { false, true })
            {
                Func<double, double, double, bool> within = (v, t, e) => useDouble ? MathUtilities.WithinTolerance(v, t, e) : MathUtilities.WithinTolerance((float)v, (float)t, (float)e);
                Require(within(0, 0, 0.5), "strict tolerance interior " + useDouble);
                Require(!within(-0.5, 0, 0.5), "strict lower endpoint " + useDouble);
                Require(!within(0.5, 0, 0.5), "strict upper endpoint " + useDouble);
                Require(!within(0, 0, 0), "zero tolerance identity excluded " + useDouble);
                Require(!within(0, 0, -1), "negative tolerance excluded " + useDouble);
                Require(!within(double.NaN, 0, 1), "NaN value tolerance " + useDouble);
                Require(!within(0, double.NaN, 1), "NaN target tolerance " + useDouble);
                Require(!within(0, 0, double.NaN), "NaN tolerance " + useDouble);
                Require(!within(double.PositiveInfinity, double.PositiveInfinity, 1), "infinite equal tolerance excluded " + useDouble);
                Require(within(100000000, 100000000, 0.0001), "Single inputs use double interval " + useDouble);
                Func<double, double, double, bool> range = (v, a, b) => useDouble ? MathUtilities.WithinRange(v, a, b) : MathUtilities.WithinRange((float)v, (float)a, (float)b);
                foreach (double value in new[] { -1.0, 0.0, 1.0 })
                {
                    Require(range(value, -1, 1), "inclusive ordered range " + useDouble + value);
                    Require(range(value, 1, -1), "inclusive inverted range " + useDouble + value);
                }
                Require(!range(-2, -1, 1) && !range(2, -1, 1), "outside inclusive range " + useDouble);
                Require(range(3, 3, 3), "collapsed range includes its point " + useDouble);
                Require(!range(double.NaN, -1, 1) && !range(0, double.NaN, 1) && !range(0, -1, double.NaN), "NaN range inputs " + useDouble);
                Require(range(double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity), "infinite inclusive endpoint " + useDouble);
                Func<double, double, double, double, bool> tolerant = (v, a, b, e) => useDouble ? MathUtilities.WithinRangeWithTolerance(v, a, b, e) : MathUtilities.WithinRangeWithTolerance((float)v, (float)a, (float)b, (float)e);
                Require(tolerant(-2, -1, 1, 1) && tolerant(2, -1, 1, 1), "expanded tolerance endpoints " + useDouble);
                Require(tolerant(0, 1, -1, 0.5), "inverted tolerant interval " + useDouble);
                Require(tolerant(0, -1, 1, -1), "negative tolerance collapsed interval " + useDouble);
                Require(!tolerant(0.01, -1, 1, -1), "negative tolerance shrinks interval " + useDouble);
                Require(!tolerant(0, -1, 1, -2), "over-shrunk interval " + useDouble);
                Require(!tolerant(0, -1, 1, double.NaN), "NaN range tolerance " + useDouble);
            }
            Require(MathUtilities.WithinTolerance(0f, 0f) && MathUtilities.WithinTolerance(0.0, 0.0), "original default open tolerance");
            Require(MathUtilities.WithinRangeWithTolerance(0.00005f, 0f, 0f) && MathUtilities.WithinRangeWithTolerance(0.00005, 0.0, 0.0), "original default expanded interval");
            Vector(MathUtilities.GetClosestPointOnLine(new Vector3(1, 2, 3), Vector3.right, new Vector3(4, 5, 6)), new Vector3(4, 2, 3), "unit infinite line projection");
            Vector(MathUtilities.GetClosestPointOnLine(new Vector3(1, 2, 3), new Vector3(2, 0, 0), new Vector3(4, 5, 6)), new Vector3(13, 2, 3), "nonunit direction intentionally overshoots");
            Vector(MathUtilities.GetClosestPointOnLine(new Vector3(1, 2, 3), Vector3.zero, Vector3.one * 100), new Vector3(1, 2, 3), "zero direction returns origin");
            Vector(MathUtilities.GetClosestPointOnLine(Vector3.zero, Vector3.up, new Vector3(3, -5, 9)), new Vector3(0, -5, 0), "projection supports backward infinite line");
            float[] angles = { -1080, -720, -540, -360, -180, -179, 0, 179, 180, 360, 540, 720, 1080 };
            float[] reduced = { 0, 0, 180, 0, 180, -179, 0, 179, 180, 0, 180, 0, 0 };
            for (int i = 0; i < angles.Length; i++) Near(MathUtilities.ShortestAngle(angles[i]), reduced[i], "original repeated angle wrap " + angles[i]);
            Require(float.IsNaN(MathUtilities.ShortestAngle(float.NaN)), "NaN angle returns without looping");
            Require(BitConverter.ToInt32(BitConverter.GetBytes(MathUtilities.ShortestAngle(-0f)), 0) == unchecked((int)0x80000000), "angle retains negative zero");
            foreach (float value in new[] { -2f, 0f, 0.25f, 1f, 2f })
            {
                Near(MathUtilities.NormalisedToPercent(value), value * 100f, "percent conversion unrestricted " + value);
                Near(MathUtilities.PercentToNormalised(value * 100f), value, "inverse percent conversion " + value);
            }
            Require(float.IsPositiveInfinity(MathUtilities.NormalisedToPercent(float.PositiveInfinity)), "percent infinity unchanged");
            Require(float.IsNaN(MathUtilities.PercentToNormalised(float.NaN)), "inverse percent NaN unchanged");
            Near(MathUtilities.Lerp(2f, 10f, 0.25f), 4f, "generic single interpolation");
            Near(MathUtilities.Lerp(2f, 10f, -10f), 2f, "generic t lower clamp");
            Near(MathUtilities.Lerp(2f, 10f, 10f), 10f, "generic t upper clamp");
            Require(float.IsNaN(MathUtilities.Lerp(2f, 10f, float.NaN)), "generic NaN t propagates");
            Require(MathUtilities.Lerp(1, 4, 0.5f) == 2 && MathUtilities.Lerp(2, 5, 0.5f) == 4, "generic integer result converts nearest-even");
            Require(MathUtilities.Lerp((byte)10, (byte)20, 0.5f) == (byte)15, "generic byte conversion");
            Require(MathUtilities.Lerp(1m, 2m, 0.5f) == 1.5m, "generic decimal passes through Single");
            Require(MathUtilities.Lerp(16777217L, 16777221L, 0f) == 16777216L, "generic long loses precision through Single even at endpoint");
            Throws<ArgumentException>(() => lerp.MakeGenericMethod(typeof(object)), "original constraint rejects object instantiation");
            Require(MathUtilities.Lerp<string>(null, "bad", 0.5f) == null, "first null returns default before conversions");
            Require(MathUtilities.Lerp<string>("bad", null, 0.5f) == null, "second null returns default before conversions");
            Throws<ArgumentException>(() => lerp.MakeGenericMethod(typeof(int?)), "original constraint rejects nullable instantiation");
            Require(MathUtilities.Lerp(false, true, 0.25f), "generic Boolean numeric interpolation conversion");
            Throws<InvalidCastException>(() => MathUtilities.Lerp('a', 'b', 0.5f), "character operands reject Single conversion");
            Throws<FormatException>(() => MathUtilities.Lerp("bad", "4", 0.5f), "invalid first operand conversion");
            Throws<ArgumentException>(() => lerp.MakeGenericMethod(typeof(Vector3)), "original constraint rejects vector operator substitution");
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Require(MathUtilities.Lerp("1,5", "2,5", 0.5f) == "2", "generic input/output current culture");
                Require(MathUtilities.Lerp("1,5", "2,5", 0.25f) == "1,75", "generic fractional result current culture");
            }
            finally { CultureInfo.CurrentCulture = previous; }
            ConversionOperand.Calls.Clear();
            Throws<InvalidCastException>(() => MathUtilities.Lerp(new ConversionOperand("first", 2), new ConversionOperand("second", 4), 0.5f), "conversion target fails after both operand conversions");
            Require(ConversionOperand.Calls.SequenceEqual(new[] { "first", "second" }), "first operand then second conversion order");
            ConversionOperand.Calls.Clear();
            Throws<FormatException>(() => MathUtilities.Lerp(new ConversionOperand("first", 2, true), new ConversionOperand("second", 4), 0.5f), "first conversion error is propagated");
            Require(ConversionOperand.Calls.SequenceEqual(new[] { "first" }), "first conversion error stops second conversion");
            ConversionOperand.Calls.Clear();
            Require(MathUtilities.Lerp<ConversionOperand>(null, new ConversionOperand("second", 4), 0.5f) == null && ConversionOperand.Calls.Count == 0, "null prevents all operand conversions");
            Vector3 coefficients = MathUtilities.QuadraticFromPoints(0, 5, 1, 10, 2, 19);
            Vector(coefficients, new Vector3(2, 3, 5), "quadratic coefficient elimination");
            foreach (float x in new[] { -3f, -1f, 0f, 0.25f, 1f, 4f })
                Near(MathUtilities.QuadraticEvaluate(x, coefficients), 2f * x * x + 3f * x + 5f, "quadratic polynomial evaluation " + x);
            Vector(MathUtilities.QuadraticFromPoints(1, 10, 2, 19, 4, 49), new Vector3(2, 3, 5), "nonzero origin quadratic elimination");
            Vector3 symmetric = MathUtilities.QuadraticFromPoints(-1, 1, 1, 1, 2, 4);
            Require(float.IsNaN(symmetric.x) && symmetric.y == 0f && float.IsNaN(symmetric.z), "symmetric first abscissas retain native zero square-difference failure");
            Vector3 duplicate = MathUtilities.QuadraticFromPoints(1, 1, 1, 2, 2, 4);
            Require(float.IsNaN(duplicate.x) || float.IsInfinity(duplicate.x), "duplicate abscissa remains nonfinite");
            Near(MathUtilities.QuadraticEvaluate(1f, new Vector3(1e20f, -1e20f, 3f)), 3f, "quadratic operation order retains constant after cancellation");

            float[] numbers = { -7f, -5f, -3f, -1f, 1f, 3f, 5f, 7f };
            int[] rounded = { -8, -4, -4, 0, 0, 4, 4, 8 };
            int[] floored = { -8, -6, -4, -2, 0, 2, 4, 6 };
            for (int i = 0; i < numbers.Length; i++)
            {
                Require(MathUtilities.RoundToMultipleOf(numbers[i], 2) == rounded[i], "integer nearest-even multiple " + numbers[i]);
                Near(MathUtilities.RoundToMultipleOf(numbers[i], 2f), rounded[i], "floating nearest-even multiple " + numbers[i]);
                Require(MathUtilities.FloorToMultipleOf(numbers[i], 2) == floored[i], "integer floor multiple " + numbers[i]);
                Near(MathUtilities.FloorToMultipleOf(numbers[i], 2f), floored[i], "floating floor multiple " + numbers[i]);
            }
            Near(MathUtilities.RoundToMultipleOf(1.1f, 0.25f), 1f, "fractional multiple rounding");
            Near(MathUtilities.FloorToMultipleOf(1.1f, 0.25f), 1f, "fractional multiple floor");
            Require(MathUtilities.RoundToMultipleOf(3f, -2) == 4, "negative integer rounding multiple");
            Near(MathUtilities.RoundToMultipleOf(3f, -2f), 4f, "negative floating rounding multiple");
            Require(MathUtilities.FloorToMultipleOf(3f, -2) == 4, "negative integer floor multiple");
            Near(MathUtilities.FloorToMultipleOf(3f, -2f), 4f, "negative floating floor multiple");
            Require(MathUtilities.RoundToMultipleOf(2147483648f, 2) == int.MinValue, "unchecked integer rounded product overflow");
            Require(MathUtilities.FloorToMultipleOf(2147483648f, 2) == int.MinValue, "unchecked integer floored product overflow");
            Require(MathUtilities.RoundToMultipleOf(1f, 0) == 0 && MathUtilities.FloorToMultipleOf(1f, 0) == 0, "zero integer multiple retains unchecked zero product");
            Require(float.IsNaN(MathUtilities.RoundToMultipleOf(1f, 0f)) && float.IsNaN(MathUtilities.FloorToMultipleOf(1f, 0f)), "zero floating multiple produces NaN");
            Near(MathUtilities.RoundToMultipleOf(8388609f, 1f), 8388610f, "native Single half-step changes large integral quotient");
            Near(MathUtilities.RoundToMultipleOf(-8388609f, 1f), -8388610f, "native negative Single half-step changes large integral quotient");
            Require(MathUtilities.RoundToMultipleOf(8388609f, 1) == 8388609 && MathUtilities.RoundToMultipleOf(-8388609f, 1) == -8388609, "integer overload retains Double half-step arithmetic");
            Require(Mathf.Round(8388609f) == 8388609f && Mathf.Round(-8388609f) == -8388609f, "genuine managed Round differs from supplied native optimized floating overload");
            Require(float.IsPositiveInfinity(MathUtilities.RoundToMultipleOf(float.PositiveInfinity, 1f)), "floating round infinity category");
            Require(float.IsNaN(MathUtilities.RoundToMultipleOf(float.NaN, 1f)), "floating round NaN category");

            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(10, 0), 2, 1), new Vector2(2, 0), "positive x-axis radial intersection");
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(-10, 0), 2, 1), new Vector2(-2, 0), "negative x-axis radial intersection");
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(0, 10), 2, 1), new Vector2(-2, 0), "zero-x positive input preserves original horizontal root");
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(0, -10), 2, 1), new Vector2(-2, 0), "zero-x negative input preserves original horizontal root");
            Vector(MathUtilities.ClosestPointOnEllipse(Vector2.zero, 2, 1), new Vector2(2, 0), "zero point preserves positive root fallback");
            float diagonal = (float)Math.Sqrt(2);
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(10, 5), 2, 1), new Vector2(diagonal, diagonal * 0.5f), "first quadrant radial ellipse intersection");
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(-10, -5), 2, 1), new Vector2(-diagonal, -diagonal * 0.5f), "third quadrant radial ellipse intersection");
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(-10, 5), 2, 1), new Vector2(-diagonal, diagonal * 0.5f), "second quadrant radial ellipse intersection");
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(10, -5), 2, 1), new Vector2(diagonal, -diagonal * 0.5f), "fourth quadrant radial ellipse intersection");
            float interiorX = (float)(2 / Math.Sqrt(5));
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(-0.01f, 0.01f), 2, 1), new Vector2(interiorX, -interiorX), "interior point preserves opposite-quadrant fallback");
            Vector(MathUtilities.ClosestPointOnEllipse(new Vector2(10, 5), 2, -1), new Vector2(-diagonal, -diagonal * 0.5f), "negative radius preserves scaled-segment reversal");
            Vector2 degenerate = MathUtilities.ClosestPointOnEllipse(Vector2.one, 2, 0);
            Require(float.IsNaN(degenerate.x) && float.IsNaN(degenerate.y), "zero ry remains native NaN intersection");
            Vector2 nanPoint = MathUtilities.ClosestPointOnEllipse(new Vector2(float.NaN, 1), 2, 1);
            Require(float.IsNaN(nanPoint.x) && float.IsNaN(nanPoint.y), "NaN ellipse input has no replacement fallback");
            Vector(MathUtilities.CalculateTorusPosition(0, 0, 5, new Vector3(2, 3, 4)), new Vector3(0, 0, 9), "torus circle zero begins on z axis");
            Vector(MathUtilities.CalculateTorusPosition(90, 0, 5, new Vector3(2, 3, 4)), new Vector3(7, 0, 0), "torus circle quarter turn begins on x axis");
            Vector(MathUtilities.CalculateTorusPosition(90, 90, 5, new Vector3(2, 3, 4)), new Vector3(2, 3, 0), "torus tube quarter turn uses segment y radius");
            Vector(MathUtilities.CalculateTorusPosition(0, 180, 5, new Vector3(2, 3, 4)), new Vector3(0, 0, -1), "torus negative radial span preserved");
            Vector(MathUtilities.CalculateTorusPosition(0, 90, 20, new Vector3(2, 3, 4)), new Vector3(0, 3, 4), "torus height does not multiply tube radius");
            Vector(MathUtilities.CalculateTorusPosition(-90, -90, 5, new Vector3(2, 3, 4)), new Vector3(-2, -3, 0), "torus negative angles preserved");
            Vector(MathUtilities.CalculateTorusPosition(450, 0, 0, new Vector3(2, 3, 4)), new Vector3(2, 0, 0), "torus uses unrestricted angles");

            foreach (bool integral in new[] { false, true })
                foreach (int initial in new[] { -2, -1, 0, 1, 2 })
                {
                    int expected = initial < -1 ? -1 : initial > 1 ? 1 : initial;
                    bool changed;
                    int result;
                    if (integral) { int value = initial; changed = MathUtilities.TryClamp(ref value, -1, 1); result = value; }
                    else { float value = initial; changed = MathUtilities.TryClamp(ref value, -1f, 1f); result = (int)value; }
                    Require(result == expected && changed == (initial != expected), "ordered clamp mutation and result " + integral + initial);
                }
            float f = 0f; Require(MathUtilities.TryClamp(ref f, 2f, -2f) && f == 2f, "inverted float clamp below-min priority");
            f = 3f; Require(MathUtilities.TryClamp(ref f, 2f, -2f) && f == -2f, "inverted float clamp high branch");
            int n = 0; Require(MathUtilities.TryClamp(ref n, 2, -2) && n == 2, "inverted integer clamp below-min priority");
            n = 3; Require(MathUtilities.TryClamp(ref n, 2, -2) && n == -2, "inverted integer clamp high branch");
            f = float.NaN; Require(!MathUtilities.TryClamp(ref f, -1f, 1f) && float.IsNaN(f), "NaN clamp value unchanged");
            f = 0f; Require(!MathUtilities.TryClamp(ref f, float.NaN, float.NaN) && f == 0f, "NaN clamp bounds unchanged");
            f = float.PositiveInfinity; Require(MathUtilities.TryClamp(ref f, -1f, 1f) && f == 1f, "positive infinity clamps high");
            f = float.NegativeInfinity; Require(MathUtilities.TryClamp(ref f, -1f, 1f) && f == -1f, "negative infinity clamps low");
            f = -0f; Require(!MathUtilities.TryClamp(ref f, 0f, 0f) && BitConverter.ToInt32(BitConverter.GetBytes(f), 0) == unchecked((int)0x80000000), "unchanged clamp preserves negative zero");
            Plane p1 = new Plane(Vector3.right, -1), p2 = new Plane(Vector3.up, -2), p3 = new Plane(Vector3.forward, -3);
            Vector(MathUtilities.PlaneIntersect(p1, p2, p3), new Vector3(1, 2, 3), "axis plane intersection");
            Vector(MathUtilities.PlaneIntersect(p3, p1, p2), new Vector3(1, 2, 3), "cyclic plane order intersection");
            p1.normal = new Vector3(2, 0, 0); p1.distance = -2;
            p2.normal = new Vector3(0, 3, 0); p2.distance = -6;
            p3.normal = new Vector3(0, 0, 4); p3.distance = -12;
            Vector(MathUtilities.PlaneIntersect(p1, p2, p3), new Vector3(1, 2, 3), "stored nonunit planes are not renormalized");
            Plane diagonalPlane = new Plane(new Vector3(1, 1, 1), new Vector3(1, 2, 3));
            Vector(MathUtilities.PlaneIntersect(new Plane(Vector3.right, -1), new Plane(Vector3.up, -2), diagonalPlane), new Vector3(1, 2, 3), "nonorthogonal plane intersection");
            Vector3 parallel = MathUtilities.PlaneIntersect(new Plane(Vector3.right, -1), new Plane(Vector3.right, -2), new Plane(Vector3.up, -3));
            Require(float.IsNaN(parallel.x) && float.IsNaN(parallel.y) && float.IsNegativeInfinity(parallel.z), "parallel planes preserve direct divide-by-zero outcome");
            return checks;
        }

        public static int Run() { return RunManaged(); }
    }
}
