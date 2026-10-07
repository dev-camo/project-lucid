using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class OriginalArrayExtensionsVerification
    {
        private enum NumericState : byte { None = 0, Ready = 2 }

        private sealed class Checks
        {
            internal int Count;
            internal void Require(bool condition, string message)
            {
                ++Count;
                if (!condition) throw new InvalidOperationException(message);
            }
            internal void Throws<T>(Action action, string message) where T : Exception
            {
                ++Count;
                try { action(); }
                catch (T) { return; }
                throw new InvalidOperationException(message);
            }
        }

        public static int RunOriginalDeclarations()
        {
            var checks = new Checks();
            Type type = typeof(ArrayExtensions);
            checks.Require(type.FullName == "Hardlight.ArrayExtensions", "Original owner identity.");
            checks.Require(type.IsPublic && type.IsAbstract && type.IsSealed, "Original static owner flags.");
            checks.Require(type.BaseType == typeof(object), "Original object base.");
            checks.Require(type.IsDefined(typeof(ExtensionAttribute), false), "Original owner extension attribute.");
            checks.Require(type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length == 0, "Original fieldless owner.");
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            checks.Require(methods.Length == 11, "Complete eleven ordinary methods.");
            checks.Require(methods.Count(m => m.Name == "Convert") == 3, "Three original Convert overloads.");
            checks.Require(methods.Count(m => m.Name == "TryConvert") == 3, "Three original TryConvert overloads.");
            foreach (MethodInfo method in methods)
            {
                checks.Require(method.IsGenericMethodDefinition, "Each ordinary method remains generic.");
                checks.Require(method.IsDefined(typeof(ExtensionAttribute), false), "Each original method remains an extension.");
                checks.Require(method.GetParameters()[0].ParameterType.IsArray, "Original first array receiver.");
                checks.Require(method.GetMethodBody() != null, "Actual emitted ordinary body.");
            }
            MethodInfo valid = methods.Single(m => m.Name == "IsArrayValid");
            ParameterInfo flag = valid.GetParameters()[1];
            checks.Require(flag.Name == "checkUnityObjects" && flag.IsOptional && flag.HasDefaultValue && Equals(flag.DefaultValue, false), "Original false optional default.");
            checks.Require(valid.GetGenericArguments()[0].GenericParameterAttributes == GenericParameterAttributes.ReferenceTypeConstraint, "Original IsArrayValid class constraint.");
            foreach (MethodInfo method in methods.Where(m => m.Name == "TryConvert"))
            {
                checks.Require(method.GetParameters()[1].IsOut && method.GetParameters()[1].ParameterType.IsByRef, "Original out array parameter.");
                checks.Require(method.GetMethodBody().ExceptionHandlingClauses.Count == 1, "One actual catch handler.");
                checks.Require(method.GetMethodBody().ExceptionHandlingClauses[0].CatchType == typeof(object), "Original bare object catch.");
            }
            foreach (MethodInfo method in methods.Where(m => (m.Name == "Convert" && m.GetParameters().Length == 1) || (m.Name == "TryConvert" && m.GetParameters().Length == 2)))
            {
                Type target = method.GetGenericArguments()[1];
                checks.Require(target.GenericParameterAttributes == GenericParameterAttributes.None, "Implicit conversion has no guessed reference/value constraint.");
                checks.Require(target.GetGenericParameterConstraints().SequenceEqual(new[] { typeof(IConvertible) }), "Original target IConvertible constraint.");
            }
            return checks.Count;
        }

        public static int RunOriginalArrayValidityAndContains()
        {
            var checks = new Checks();
            object[] absent = null;
            checks.Require(!absent.IsArrayValid(), "Null array is invalid.");
            checks.Require(!absent.IsArrayValid(true), "Opt-in null array is invalid.");
            checks.Require(new object[0].IsArrayValid(), "Empty array is valid.");
            checks.Require(new object[0].IsArrayValid(true), "Opt-in empty array is valid.");
            object first = new object(), second = new object();
            var array = new[] { first, second };
            checks.Require(array.IsArrayValid(), "Non-null CLR objects are valid.");
            checks.Require(array.IsArrayValid(true), "Unity checking retains ordinary CLR objects.");
            checks.Require(!new[] { first, null, second }.IsArrayValid(), "A middle CLR null invalidates.");
            checks.Require(!new[] { first, null, second }.IsArrayValid(true), "Unity opt-in retains CLR null invalidation.");
            checks.Require(new[] { "", "value" }.IsArrayValid(true), "Empty string remains a non-null reference.");
            checks.Require(!new[] { "value", null }.IsArrayValid(), "String null remains invalid.");
            checks.Require(array.Contains(first) && array.Contains(second), "Contains retains actual reference elements.");
            checks.Require(!array.Contains(new object()), "Contains retains object identity equality.");
            checks.Require(new[] { "a", "b" }.Contains(new string(new[] { 'b' })), "Contains uses original default value equality.");
            checks.Require(new object[] { null }.Contains(null), "Contains includes a null element.");
            checks.Require(!new int[0].Contains(0), "Empty Contains is false.");
            checks.Throws<ArgumentNullException>(() => absent.Contains(first), "Contains retains BCL null-array failure.");
            checks.Require(ReferenceEquals(array[0], first) && ReferenceEquals(array[1], second), "Validation and membership do not mutate input.");
            return checks.Count;
        }

        public static int RunOriginalDelegateConversions()
        {
            var checks = new Checks();
            int[] source = { 7, -4, 9 };
            var visits = new List<int>();
            string[] converted = source.Convert<int, string>(value => { visits.Add(value); return "v" + value; });
            checks.Require(visits.SequenceEqual(source), "Callbacks run in original array order.");
            checks.Require(converted.SequenceEqual(new[] { "v7", "v-4", "v9" }), "Each converted element is retained.");
            checks.Require(source.SequenceEqual(new[] { 7, -4, 9 }), "Delegate conversion retains source values.");
            Type observed = null;
            visits.Clear();
            string[] withType = source.Convert<int, string>((value, type) => { checks.Require(type == typeof(string), "Actual target type reaches every callback."); if (observed != null) checks.Require(ReferenceEquals(observed, type), "Same captured Type reaches subsequent callbacks."); observed = type; visits.Add(value); return value.ToString(CultureInfo.InvariantCulture); });
            checks.Require(visits.SequenceEqual(source), "Typed callbacks retain array order.");
            checks.Require(withType.SequenceEqual(new[] { "7", "-4", "9" }), "Typed callback results are retained.");
            checks.Require(new int[0].Convert<int, string>((Func<int, string>)null).Length == 0, "Empty array does not call null one-argument delegate.");
            checks.Require(new int[0].Convert<int, string>((Func<int, Type, string>)null).Length == 0, "Empty array does not call null typed delegate.");
            checks.Throws<NullReferenceException>(() => source.Convert<int, string>((Func<int, string>)null), "Non-empty one-argument null delegate faults.");
            checks.Throws<NullReferenceException>(() => source.Convert<int, string>((Func<int, Type, string>)null), "Non-empty typed null delegate faults.");
            int[] absent = null;
            int calls = 0;
            checks.Throws<NullReferenceException>(() => absent.Convert<int, string>(value => { ++calls; return "value"; }), "Null source faults before delegate.");
            checks.Throws<NullReferenceException>(() => absent.Convert<int, string>((value, type) => { ++calls; return "value"; }), "Typed null source faults before delegate.");
            checks.Require(calls == 0, "No callbacks occur for null source.");
            visits.Clear();
            checks.Throws<InvalidOperationException>(() => source.Convert<int, string>(value => { visits.Add(value); if (value == -4) throw new InvalidOperationException(); return "done"; }), "Callback fault propagates directly.");
            checks.Require(visits.SequenceEqual(new[] { 7, -4 }), "Failure retains callback prefix and prevents later callbacks.");
            object entry = new object();
            object[] identitySource = { entry };
            object[] identityResult = identitySource.Convert<object, object>(value => value);
            checks.Require(!ReferenceEquals(identitySource, identityResult) && ReferenceEquals(entry, identityResult[0]), "A fresh output array retains returned reference identities.");
            checks.Require(new[] { 1 }.Convert<int, object>(value => null)[0] == null, "Delegate-produced null is retained.");
            return checks.Count;
        }

        public static int RunOriginalImplicitConversions()
        {
            var checks = new Checks();
            checks.Require(new[] { 1, -2, 3 }.Convert<int, long>().SequenceEqual(new long[] { 1, -2, 3 }), "Numeric ChangeType conversion.");
            checks.Require(new[] { "12", "-3" }.Convert<string, int>().SequenceEqual(new[] { 12, -3 }), "String ChangeType conversion.");
            NumericState[] values = new[] { (byte)0, (byte)2, (byte)255 }.Convert<byte, NumericState>();
            checks.Require(values[0] == NumericState.None && values[1] == NumericState.Ready, "Known numeric enum values.");
            checks.Require((byte)values[2] == 255 && !Enum.IsDefined(typeof(NumericState), values[2]), "Unnamed numeric enum value is retained.");
            checks.Throws<ArgumentException>(() => new[] { "Ready" }.Convert<string, NumericState>(), "Original Enum.ToObject rejects strings.");
            checks.Throws<FormatException>(() => new[] { "bad" }.Convert<string, int>(), "Numeric conversion retains format failure.");
            checks.Throws<OverflowException>(() => new[] { "256" }.Convert<string, byte>(), "Numeric conversion retains overflow.");
            checks.Throws<InvalidCastException>(() => new object[] { null }.Convert<object, int>(), "ChangeType null to value type faults.");
            checks.Require(new object[] { null }.Convert<object, string>()[0] == null, "ChangeType null to reference type remains null.");
            checks.Require(new int[0].Convert<int, NumericState>().Length == 0, "Empty enum conversion succeeds.");
            checks.Require(new int[0].Convert<int, int>().Length == 0, "Empty value conversion succeeds.");
            int[] absent = null;
            checks.Throws<NullReferenceException>(() => absent.Convert<int, NumericState>(), "Enum conversion retains null-source failure.");
            checks.Throws<NullReferenceException>(() => absent.Convert<int, int>(), "Non-enum conversion retains null-source failure.");
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                checks.Require(new[] { "1,5" }.Convert<string, decimal>()[0] == 1.5m, "ChangeType uses current culture.");
                checks.Require(new[] { 1.5m }.Convert<decimal, string>()[0] == "1,5", "Current culture also formats strings.");
            }
            finally { CultureInfo.CurrentCulture = original; }
            checks.Require(ReferenceEquals(CultureInfo.CurrentCulture, original), "Fixture restores exact current culture.");
            return checks.Count;
        }

        public static int RunOriginalTryConversionFailures()
        {
            var checks = new Checks();
            int[] source = { 1, 2, 3 };
            string[] output = { "old" };
            checks.Require(source.TryConvert(out output, value => "v" + value), "One-argument TryConvert success.");
            checks.Require(output.SequenceEqual(new[] { "v1", "v2", "v3" }), "One-argument successful output.");
            checks.Require(source.TryConvert(out output, (value, type) => type.Name + value), "Typed TryConvert success.");
            checks.Require(output.SequenceEqual(new[] { "String1", "String2", "String3" }), "Typed successful output.");
            int[] numbers;
            checks.Require(new[] { "4", "5" }.TryConvert<string, int>(out numbers) && numbers.SequenceEqual(new[] { 4, 5 }), "Implicit TryConvert success.");
            var visits = new List<int>();
            checks.Require(!source.TryConvert(out output, value => { visits.Add(value); if (value == 2) throw new InvalidOperationException(); return "v"; }), "One-argument callback failure returns false.");
            checks.Require(output == null && visits.SequenceEqual(new[] { 1, 2 }), "One-argument failure clears output and retains callback prefix.");
            output = new[] { "old" }; visits.Clear();
            checks.Require(!source.TryConvert(out output, (value, type) => { visits.Add(value); if (value == 2) throw new ArgumentException(); return "v"; }), "Typed callback failure returns false.");
            checks.Require(output == null && visits.SequenceEqual(new[] { 1, 2 }), "Typed failure clears output and retains callback prefix.");
            checks.Require(!new[] { "bad" }.TryConvert<string, int>(out numbers) && numbers == null, "Implicit TryConvert clears malformed numeric output.");
            NumericState[] states;
            checks.Require(!new[] { "Ready" }.TryConvert<string, NumericState>(out states) && states == null, "Implicit TryConvert catches enum string rejection.");
            int[] absent = null;
            checks.Require(!absent.TryConvert(out output, value => "v") && output == null, "One-argument TryConvert catches null source.");
            checks.Require(!absent.TryConvert(out output, (value, type) => "v") && output == null, "Typed TryConvert catches null source.");
            checks.Require(!absent.TryConvert<int, int>(out numbers) && numbers == null, "Implicit TryConvert catches null source.");
            checks.Require(!source.TryConvert(out output, (Func<int, string>)null) && output == null, "One-argument TryConvert catches null callback.");
            checks.Require(!source.TryConvert(out output, (Func<int, Type, string>)null) && output == null, "Typed TryConvert catches null callback.");
            checks.Require(new int[0].TryConvert(out output, (Func<int, string>)null) && output.Length == 0, "Empty TryConvert succeeds with null callback.");
            checks.Require(new int[0].TryConvert(out output, (Func<int, Type, string>)null) && output.Length == 0, "Empty typed TryConvert succeeds with null callback.");
            checks.Require(new int[0].TryConvert<int, int>(out numbers) && numbers.Length == 0, "Empty implicit TryConvert succeeds.");
            return checks.Count;
        }

        public static int RunOriginalSwapBoundaries()
        {
            var checks = new Checks();
            int[] values = { 1, 2, 3 };
            values.SwapElements(0, 2);
            checks.Require(values.SequenceEqual(new[] { 3, 2, 1 }), "SwapElements changes actual endpoints.");
            values.SwapFirst(1);
            checks.Require(values.SequenceEqual(new[] { 2, 3, 1 }), "SwapFirst uses literal first endpoint.");
            values.SwapLast(0);
            checks.Require(values.SequenceEqual(new[] { 1, 3, 2 }), "SwapLast uses actual final endpoint.");
            values.SwapElements(-9, -9);
            checks.Require(values.SequenceEqual(new[] { 1, 3, 2 }), "Equal invalid indices return before array access.");
            checks.Throws<IndexOutOfRangeException>(() => values.SwapElements(0, 3), "Invalid second index faults.");
            checks.Require(values.SequenceEqual(new[] { 1, 3, 2 }), "Invalid second read performs no write.");
            checks.Throws<IndexOutOfRangeException>(() => values.SwapElements(-1, 0), "Invalid first index faults.");
            checks.Require(values.SequenceEqual(new[] { 1, 3, 2 }), "Invalid first read performs no write.");
            int[] absent = null;
            absent.SwapElements(int.MinValue, int.MinValue);
            checks.Require(absent == null, "Null array with equal indices returns.");
            absent.SwapFirst(0);
            checks.Require(absent == null, "SwapFirst zero equal endpoints return on null array.");
            checks.Throws<NullReferenceException>(() => absent.SwapElements(0, 1), "Null array with different indices faults.");
            checks.Throws<NullReferenceException>(() => absent.SwapFirst(1), "SwapFirst different null endpoints fault.");
            checks.Throws<NullReferenceException>(() => absent.SwapLast(-1), "SwapLast always reads null array length first.");
            var empty = new int[0];
            empty.SwapFirst(0);
            empty.SwapLast(-1);
            checks.Require(empty.Length == 0, "Empty equal-endpoint first and last swaps return.");
            checks.Throws<IndexOutOfRangeException>(() => empty.SwapFirst(1), "Empty first different endpoints fault.");
            checks.Throws<IndexOutOfRangeException>(() => empty.SwapLast(0), "Empty last different endpoints fault.");
            object first = new object(), second = new object();
            object[] references = { first, second };
            references.SwapElements(0, 1);
            checks.Require(ReferenceEquals(references[0], second) && ReferenceEquals(references[1], first), "Reference swap preserves exact identities.");
            DateTime a = new DateTime(1234), b = new DateTime(5678);
            DateTime[] structures = { a, b };
            structures.SwapElements(0, 1);
            checks.Require(structures[0] == b && structures[1] == a, "Value-type swap preserves complete elements.");
            return checks.Count;
        }

        // This group requires genuine Unity object construction/destruction and
        // remains unexecuted in the private managed-host proof.
        public static int RunOriginalUnityObjectValidity()
        {
            var checks = new Checks();
            var root = new GameObject("OriginalArrayValidityFixture");
            UnityEngine.Object preserved = root;
            try
            {
                checks.Require(new[] { preserved }.IsArrayValid(), "Live Unity object passes CLR null check.");
                checks.Require(new[] { preserved }.IsArrayValid(true), "Live Unity object passes Unity equality.");
                UnityEngine.Object.DestroyImmediate(root);
                checks.Require(!ReferenceEquals(preserved, null), "Destroyed object retains CLR wrapper.");
                checks.Require(preserved == null, "Destroyed object has genuine Unity null equality.");
                checks.Require(new[] { preserved }.IsArrayValid(), "Default validity retains destroyed CLR wrapper.");
                checks.Require(!new[] { preserved }.IsArrayValid(true), "Opt-in validity rejects destroyed Unity wrapper.");
                checks.Require(new object[] { preserved }.IsArrayValid(), "Object-typed default validity retains wrapper.");
                checks.Require(!new object[] { preserved }.IsArrayValid(true), "Object-typed opt-in validity detects Unity wrapper.");
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
            return checks.Count;
        }
    }
}
