using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;

namespace ProjectLucid.Verification
{
    // These cases use owned enum values and arrays with the real BCL; no Unity objects are needed.
    internal static class OriginalEnumPreservationVerification
    {
        private enum Signed : int { Negative = -3, Zero = 0, Seven = 7 }
        private enum Aliased : int { Negative = -2, Zero = 0, Two = 2, Duplicate = 2, Nine = 9 }
        private enum SmallByte : byte { Zero = 0, High = 255 }
        private enum SmallSigned : sbyte { Negative = -128, Zero = 0, Three = 3 }
        private enum ShortSigned : short { Zero = 0, High = 32767 }
        private enum ShortUnsigned : ushort { Zero = 0, High = 65535 }
        private enum WideSigned : long { Negative = -1, Zero = 0, High = 2147483647L }
        private enum LargeUnsigned : uint { Zero = 0, AboveInt32 = 2147483648U }
        private enum HugeUnsigned : ulong { Zero = 0, High = ulong.MaxValue }
        [Flags] private enum Access : int { None = 0, Read = 1, Write = 2 }

        private sealed class Checks
        {
            internal int Count;

            internal void Require(bool condition, string label)
            {
                ++Count;
                if (!condition) throw new InvalidOperationException(label);
            }

            internal void Throws<TException>(Action action, string label) where TException : Exception
            {
                ++Count;
                try { action(); }
                catch (TException) { return; }
                catch (Exception e) { throw new InvalidOperationException(label + ": unexpected " + e.GetType().FullName, e); }
                throw new InvalidOperationException(label + ": no exception");
            }

            internal void Array<T>(T[] actual, T[] expected, string label)
            {
                Require(actual != null && actual.Length == expected.Length, label + " length");
                for (int i = 0; i < expected.Length; ++i)
                    Require(EqualityComparer<T>.Default.Equals(actual[i], expected[i]), label + " element " + i);
            }
        }

        internal static int ParseRetainsCaseSensitiveNamesAndNumericValues()
        {
            var c = new Checks();
            c.Require(EnumUtilities.Parse<Signed>("Seven") == Signed.Seven, "named parse");
            c.Require(EnumUtilities.Parse<Signed>("Negative") == Signed.Negative, "negative name");
            c.Require(EnumUtilities.Parse<Signed>("7") == Signed.Seven, "named numeric value");
            c.Require(EnumUtilities.Parse<Signed>("-3") == Signed.Negative, "signed numeric value");
            c.Require((int)EnumUtilities.Parse<Signed>("123") == 123, "unnamed numeric value is accepted");
            c.Require(EnumUtilities.Parse<Signed>("  Seven  ") == Signed.Seven, "BCL surrounding whitespace");
            c.Require(EnumUtilities.Parse<Aliased>("Duplicate") == Aliased.Two, "alias name");
            c.Require(EnumUtilities.Parse<SmallByte>("255") == SmallByte.High, "byte numeric endpoint");
            c.Require(EnumUtilities.Parse<ShortSigned>("32767") == ShortSigned.High, "short numeric endpoint");
            c.Require(EnumUtilities.Parse<HugeUnsigned>("18446744073709551615") == HugeUnsigned.High, "ulong numeric endpoint");
            c.Require((int)EnumUtilities.Parse<Access>("Read, Write") == 3, "BCL flags list");
            c.Throws<ArgumentException>(() => EnumUtilities.Parse<Signed>("seven"), "case-sensitive parse fault");
            c.Throws<ArgumentException>(() => EnumUtilities.Parse<Signed>("Missing"), "unknown name fault");
            c.Throws<ArgumentException>(() => EnumUtilities.Parse<Signed>(""), "empty parse fault");
            c.Throws<ArgumentNullException>(() => EnumUtilities.Parse<Signed>(null), "null parse fault");
            c.Throws<ArgumentException>(() => EnumUtilities.Parse<int>("7"), "unconstrained non-enum parse fault");
            return c.Count;
        }

        internal static int SafeParseRetainsFallbackAcrossParseAndTypeFaults()
        {
            var c = new Checks();
            c.Require(EnumUtilities.SafeParse("Seven", Signed.Negative) == Signed.Seven, "successful fallback parse");
            c.Require(EnumUtilities.SafeParse("7", Signed.Negative) == Signed.Seven, "successful numeric fallback parse");
            c.Require(EnumUtilities.SafeParse("Missing", Signed.Negative) == Signed.Negative, "unknown fallback");
            c.Require(EnumUtilities.SafeParse("seven", Signed.Negative) == Signed.Negative, "case-sensitive fallback");
            c.Require(EnumUtilities.SafeParse("", Signed.Seven) == Signed.Seven, "empty fallback");
            c.Require((int)EnumUtilities.SafeParse(null, (Signed)111) == 111, "null retains unnamed fallback");
            c.Require((int)EnumUtilities.SafeParse("111", Signed.Seven) == 111, "unnamed number succeeds");
            c.Require((int)EnumUtilities.SafeParse("Read, Write", Access.None) == 3, "flags fallback parse");
            c.Require(EnumUtilities.SafeParse("7", 42) == 42, "invalid enum type fallback");
            string fallback = new string(new[] { 'o', 'w', 'n', 'e', 'd' });
            c.Require(ReferenceEquals(EnumUtilities.SafeParse<string>("Zero", fallback), fallback), "reference fallback identity");
            c.Require(EnumUtilities.SafeParse("999", SmallByte.High) == SmallByte.High, "byte overflow fallback");
            c.Require(EnumUtilities.SafeParse("18446744073709551616", HugeUnsigned.High) == HugeUnsigned.High, "ulong overflow fallback");
            return c.Count;
        }

        internal static int NamesAndValuesRetainAliasesAndIndependentArrays()
        {
            var c = new Checks();
            Aliased[] values = EnumUtilities.GetValues<Aliased>();
            var expected = new[] { Aliased.Zero, Aliased.Two, Aliased.Two, Aliased.Nine, Aliased.Negative };
            c.Array(values, expected, "unsigned-value ordering retains duplicates");
            c.Require(!ReferenceEquals(values, EnumUtilities.GetValues<Aliased>()), "value arrays are independent");
            string[] names = EnumUtilities.GetNames<Aliased>();
            c.Require(names.Length == 5, "alias name count");
            c.Require(names[0] == "Zero", "zero name order");
            c.Require(names[3] == "Nine", "positive name order");
            c.Require(names[4] == "Negative", "negative uses unsigned ordering");
            c.Require(names[1] == "Two" || names[2] == "Two", "first alias name retained");
            c.Require(names[1] == "Duplicate" || names[2] == "Duplicate", "second alias name retained");
            c.Require(names[1] != names[2], "both alias names remain distinct");
            names[0] = "Changed";
            string[] nextNames = EnumUtilities.GetNames<Aliased>();
            c.Require(nextNames[0] == "Zero", "name mutation stays in owned array");
            c.Require(!ReferenceEquals(names, nextNames), "name arrays are independent");
            values[0] = Aliased.Negative;
            c.Array(EnumUtilities.GetValues<Aliased>(), expected, "value mutation stays in owned array");
            c.Array(EnumUtilities.GetNames<Signed>(), new[] { "Zero", "Seven", "Negative" }, "signed names");
            c.Array(EnumUtilities.GetValues<Signed>(), new[] { Signed.Zero, Signed.Seven, Signed.Negative }, "signed values");
            return c.Count;
        }

        internal static int IntegerValuesRetainSignedConversionAndOverflow()
        {
            var c = new Checks();
            c.Array(EnumUtilities.GetValuesAsInt<Signed>(), new[] { 0, 7, -3 }, "int values");
            c.Array(EnumUtilities.GetValuesAsInt<Aliased>(), new[] { 0, 2, 2, 9, -2 }, "integer aliases");
            c.Array(EnumUtilities.GetValuesAsInt<SmallByte>(), new[] { 0, 255 }, "byte conversion");
            c.Array(EnumUtilities.GetValuesAsInt<SmallSigned>(), new[] { 0, 3, -128 }, "sbyte conversion");
            c.Array(EnumUtilities.GetValuesAsInt<ShortUnsigned>(), new[] { 0, 65535 }, "ushort conversion");
            c.Array(EnumUtilities.GetValuesAsInt<WideSigned>(), new[] { 0, int.MaxValue, -1 }, "long in-range conversion");
            c.Throws<OverflowException>(() => EnumUtilities.GetValuesAsInt<LargeUnsigned>(), "uint conversion overflow propagates");
            c.Throws<OverflowException>(() => EnumUtilities.GetValuesAsInt<HugeUnsigned>(), "ulong conversion overflow propagates");
            c.Throws<ArgumentException>(() => EnumUtilities.GetValuesAsInt<int>(), "IConvertible alone does not make a type an enum");
            int[] changed = EnumUtilities.GetValuesAsInt<Aliased>();
            int[] other = EnumUtilities.GetValuesAsInt<Aliased>();
            c.Require(!ReferenceEquals(changed, other), "converted arrays are independent");
            changed[0] = 99;
            c.Array(EnumUtilities.GetValuesAsInt<Aliased>(), new[] { 0, 2, 2, 9, -2 }, "converted mutation stays in owned array");
            return c.Count;
        }

        internal static int MembershipRequiresDefinedEnumValues()
        {
            var c = new Checks();
            c.Require(EnumUtilities.IsDefined(Signed.Seven), "defined positive value");
            c.Require(EnumUtilities.IsDefined(Signed.Negative), "defined negative value");
            c.Require(!EnumUtilities.IsDefined((Signed)123), "undefined value");
            c.Require(EnumUtilities.IsDefined(Aliased.Duplicate), "defined alias value");
            c.Require(EnumUtilities.IsDefined(Access.None), "defined zero flag");
            c.Require(!EnumUtilities.IsDefined(Access.Read | Access.Write), "unnamed flags combination is not defined");
            c.Require(!EnumUtilities.IsDefined((Access)8), "unknown flag bit");
            c.Throws<ArgumentException>(() => EnumUtilities.IsDefined(7), "non-enum membership fault");
            c.Throws<ArgumentException>(() => EnumUtilities.IsDefined("Seven"), "reference non-enum membership fault");
            return c.Count;
        }

        internal static int ConversionRetainsOutValueAndFaultPrefixes()
        {
            var c = new Checks();
            Signed result = Signed.Negative;
            c.Require(EnumUtilities.TryConvertEnum(Signed.Seven, out result), "defined conversion succeeds");
            c.Require(result == Signed.Seven, "defined converted value");
            c.Require(EnumUtilities.TryConvertEnum(Signed.Negative, out result), "negative conversion succeeds");
            c.Require(result == Signed.Negative, "negative converted value");
            c.Require(!EnumUtilities.TryConvertEnum((Signed)123, out result), "undefined conversion returns false");
            c.Require(result == Signed.Zero, "false conversion clears output");
            result = Signed.Negative;
            c.Throws<NullReferenceException>(() => EnumUtilities.TryConvertEnum<Signed>(null, out result), "null cast faults before assignment");
            c.Require(result == Signed.Negative, "null cast retains prior output");
            c.Throws<InvalidCastException>(() => EnumUtilities.TryConvertEnum<Signed>(SmallByte.High, out result), "different underlying type faults before assignment");
            c.Require(result == Signed.Negative, "invalid cast retains prior output");
            Enum input = Signed.Seven;
            Enum baseResult = Signed.Negative;
            c.Throws<ArgumentException>(() => EnumUtilities.TryConvertEnum<Enum>(input, out baseResult), "base Enum membership faults after assignment");
            c.Require(ReferenceEquals(baseResult, input), "membership fault retains exact assigned input reference");
            c.Require(EnumUtilities.TryConvertEnum(Signed.Zero, out result), "defined zero succeeds");
            c.Require(result == Signed.Zero, "defined zero output");
            Aliased alias;
            c.Require(EnumUtilities.TryConvertEnum(Aliased.Duplicate, out alias), "alias conversion succeeds");
            c.Require(alias == Aliased.Two, "alias retains underlying value");
            return c.Count;
        }

        internal static int OriginalConstraintsRemainObservable()
        {
            var c = new Checks();
            Type type = typeof(EnumUtilities);
            c.Require(new EnumUtilities() != null, "original ordinary constructor");
            c.Require(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0, "original fieldless owner");
            foreach (string name in new[] { "Parse", "SafeParse", "GetNames", "GetValues", "IsDefined" })
                c.Require(type.GetMethod(name).GetGenericArguments()[0].GetGenericParameterConstraints().Length == 0, name + " remains unconstrained");
            foreach (string name in new[] { "Parse", "SafeParse", "GetNames", "GetValues", "GetValuesAsInt", "IsDefined", "TryConvertEnum" })
                c.Require(type.GetMethod(name).GetGenericArguments()[0].GenericParameterAttributes == GenericParameterAttributes.None, name + " has no extra struct/reference flags");
            Type[] integerConstraints = type.GetMethod("GetValuesAsInt").GetGenericArguments()[0].GetGenericParameterConstraints();
            c.Require(integerConstraints.Length == 1, "single integer constraint");
            c.Require(integerConstraints[0] == typeof(IConvertible), "authentic convertible constraint");
            Type[] conversionConstraints = type.GetMethod("TryConvertEnum").GetGenericArguments()[0].GetGenericParameterConstraints();
            c.Require(conversionConstraints.Length == 1, "single conversion constraint");
            c.Require(conversionConstraints[0] == typeof(Enum), "authentic Enum constraint");
            ParameterInfo output = type.GetMethod("TryConvertEnum").GetParameters()[1];
            c.Require(output.ParameterType.IsByRef, "original byref output");
            c.Require(output.IsOut, "original out flag");
            c.Require(!output.IsOptional, "no optional output added");
            ParameterInfo parseInput = type.GetMethod("Parse").GetParameters()[0];
            c.Require(!parseInput.IsOptional, "no optional parse input added");
            c.Require(parseInput.Name == "enumName", "original parse parameter name");
            c.Require(type.GetConstructor(Type.EmptyTypes) != null, "original public parameterless constructor");
            c.Require(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 7, "complete seven static APIs");
            return c.Count;
        }
    }
}
