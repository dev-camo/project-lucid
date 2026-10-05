using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace ProjectLucid
{
    public static class ReadOnlyListVerification
    {
        private static int checks;
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks++;
        }
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); }
            catch (T) { checks++; return; }
            throw new InvalidOperationException(label);
        }
        private sealed class Observed<T> : IReadOnlyList<T>
        {
            public readonly List<T> Items = new List<T>();
            public readonly List<string> Events = new List<string>();
            public Func<int> CountRead;
            public Func<int, T> ItemRead;
            public Func<IEnumerator<T>> Enumerator;
            public Observed(params T[] items) { Items.AddRange(items); }
            public int Count { get { Events.Add("count"); return CountRead == null ? Items.Count : CountRead(); } }
            public T this[int index] { get { Events.Add("index:" + index); return ItemRead == null ? Items[index] : ItemRead(index); } }
            public IEnumerator<T> GetEnumerator() { Events.Add("enumerator"); return Enumerator == null ? Items.GetEnumerator() : Enumerator(); }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public string Trace => string.Join(",", Events);
        }
        private sealed class EqualProbe : IEquatable<EqualProbe>
        {
            public int Id;
            public Func<EqualProbe, bool> Equality;
            public static int OperatorCalls;
            public bool Equals(EqualProbe other) => Equality == null ? Id == other.Id : Equality(other);
            public override bool Equals(object other) => other is EqualProbe value && Equals(value);
            public override int GetHashCode() => Id;
            public static bool operator ==(EqualProbe left, EqualProbe right) { OperatorCalls++; return true; }
            public static bool operator !=(EqualProbe left, EqualProbe right) { OperatorCalls++; return false; }
        }
        private struct EqualValue : IEquatable<EqualValue>
        {
            public int Id;
            public static string Trace;
            public bool Equals(EqualValue other) { Trace += Id + ":" + other.Id + ";"; return Id == other.Id; }
        }
        private sealed class Comparable : IComparable<Comparable>
        {
            public int Value;
            public Action<Comparable> Compared;
            public Func<Comparable, int> Comparison;
            public int CompareTo(Comparable other) { Compared?.Invoke(other); return Comparison == null ? Value.CompareTo(other.Value) : Comparison(other); }
        }
        private sealed class Validatable : IValidatable
        {
            public Action Action;
            public void Validate() => Action();
        }
        private struct ValidatableValue : IValidatable
        {
            public int Value;
            public static int Total;
            public void Validate() { Total += Value; Value++; }
        }
        private sealed class ValidationEnumerator : IEnumerator<Validatable>
        {
            private readonly List<string> events;
            private readonly Validatable value;
            private int position;
            public bool ThrowMove;
            public bool ThrowCurrent;
            public bool ThrowDispose;
            public ValidationEnumerator(List<string> events, Validatable value) { this.events = events; this.value = value; }
            public bool MoveNext() { events.Add("move"); if (ThrowMove) throw new FormatException(); return position++ == 0; }
            public Validatable Current { get { events.Add("current"); if (ThrowCurrent) throw new FormatException(); return value; } }
            object IEnumerator.Current => Current;
            public void Dispose() { events.Add("dispose"); if (ThrowDispose) throw new ApplicationException(); }
            public void Reset() => throw new NotSupportedException();
        }

        public static void Run() => Debug.Log("Project Lucid ReadOnlyList checks=" + RunManaged());

        public static int RunManaged()
        {
            checks = 0;
            IReadOnlyList<int> none = null;
            var empty = new Observed<int>();
            var list = new Observed<int>(2, 4, 6);
            Check(ReadOnlyListExtensions.FindIndex(empty, null) == -1, "empty find index bypasses predicate");
            Check(ReadOnlyListExtensions.Find(empty, null) == 0, "empty find default");
            Check(!ReadOnlyListExtensions.Exists(empty, null), "empty exists false");
            Check(ReadOnlyListExtensions.FindIndex(list, x => x == 4) == 1, "first predicate index");
            Check(ReadOnlyListExtensions.Find(list, x => x > 2) == 4, "first predicate item");
            Check(ReadOnlyListExtensions.Exists(list, x => x == 6), "predicate exists");
            Check(ReadOnlyListExtensions.FindIndex(list, x => false) == -1, "missing predicate index");
            Check(ReadOnlyListExtensions.Find(list, x => false) == 0, "missing predicate item default");
            Check(!ReadOnlyListExtensions.Exists(list, x => false), "missing predicate exists");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.FindIndex(none, null), "null find index");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.Find(none, null), "null find");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.Exists(none, null), "null exists");
            foreach (int method in new[] { 0, 1, 2 })
            {
                var live = new Observed<int>(1);
                Func<int, bool> predicate = x => { live.Events.Add("predicate:" + x); if (x == 1) live.Items.Add(2); return x == 2; };
                int found = method == 0 ? ReadOnlyListExtensions.FindIndex(live, predicate) : method == 1 ? ReadOnlyListExtensions.Find(live, predicate) : ReadOnlyListExtensions.Exists(live, predicate) ? 2 : 0;
                Check(found == (method == 0 ? 1 : 2), "predicate growth " + method);
                Check(live.Trace == "count,index:0,predicate:1,count,index:1,predicate:2", "predicate live Count/order " + method);
                live = new Observed<int>(1, 2);
                predicate = x => { live.Items.Clear(); return false; };
                if (method == 0) Check(ReadOnlyListExtensions.FindIndex(live, predicate) == -1, "find index shrink");
                if (method == 1) Check(ReadOnlyListExtensions.Find(live, predicate) == 0, "find shrink");
                if (method == 2) Check(!ReadOnlyListExtensions.Exists(live, predicate), "exists shrink");
                var invalid = new Observed<int>(1) { ItemRead = i => throw new FormatException() };
                if (method == 0) Throws<FormatException>(() => ReadOnlyListExtensions.FindIndex(invalid, null), "find index source precedes null callback");
                if (method == 1) Throws<FormatException>(() => ReadOnlyListExtensions.Find(invalid, null), "find source precedes null callback");
                if (method == 2) Throws<FormatException>(() => ReadOnlyListExtensions.Exists(invalid, null), "exists source precedes null callback");
            }
            var captured = new Observed<int>(7);
            Check(ReadOnlyListExtensions.Find(captured, x => { captured.Items[0] = 9; return true; }) == 7, "find captures item before callback");

            Check(!ReadOnlyListExtensions.Contains(empty, 1), "empty contains");
            Check(ReadOnlyListExtensions.Contains(list, 4), "integer contains");
            Check(!ReadOnlyListExtensions.Contains(list, 5), "integer missing");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.Contains(none, 0), "contains null list");
            var equalityEvents = new List<string>();
            var query = new EqualProbe { Id = 5, Equality = x => throw new ApplicationException() };
            var candidate = new EqualProbe { Id = 5, Equality = x => { equalityEvents.Add("equals"); return ReferenceEquals(x, query); } };
            var equalityList = new Observed<EqualProbe>(candidate);
            Check(ReadOnlyListExtensions.Contains(equalityList, query), "default comparer uses candidate as receiver");
            Check(equalityEvents.Count == 1 && equalityList.Trace == "count,index:0", "contains callback after source, short circuit");
            EqualProbe.OperatorCalls = 0;
            candidate.Equality = x => throw new ApplicationException();
            Check(!ReadOnlyListExtensions.Contains(equalityList, null), "null query bypasses equality");
            equalityList.Items.Add(null);
            Check(ReadOnlyListExtensions.Contains(equalityList, null), "raw null match");
            Check(EqualProbe.OperatorCalls == 0, "null matching bypasses overloaded operator");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.Contains((IReadOnlyList<EqualProbe>)null, null), "null query on null list");
            var grows = new Observed<EqualProbe>();
            var next = new EqualProbe { Id = 5, Equality = x => true };
            grows.Items.Add(new EqualProbe { Equality = x => { grows.Items.Add(next); return false; } });
            Check(ReadOnlyListExtensions.Contains(grows, query), "comparer mutation grows list");
            Check(grows.Trace == "count,index:0,count,index:1", "contains live Count");
            grows = new Observed<EqualProbe>();
            grows.Items.Add(new EqualProbe { Equality = x => { grows.Items.Clear(); return false; } });
            Check(!ReadOnlyListExtensions.Contains(grows, query), "comparer mutation shrinks list");
            EqualValue.Trace = "";
            Check(ReadOnlyListExtensions.Contains<EqualValue>(new[] { new EqualValue { Id = 1 }, new EqualValue { Id = 2 } }, new EqualValue { Id = 2 }), "struct comparer equality");
            Check(EqualValue.Trace == "1:2;2:2;", "struct comparator operand order");
            Check(ReadOnlyListExtensions.Contains<int?>(new int?[] { 1, null }, null), "nullable raw null");
            Check(!ReadOnlyListExtensions.Contains<int?>(new int?[] { 1, null }, 2), "nullable nonnull default comparer");
            Check(ReadOnlyListExtensions.Contains<float>(new[] { float.NaN }, float.NaN), "float default comparer handles NaN");
            Check(ReadOnlyListExtensions.Contains<DayOfWeek>(new[] { DayOfWeek.Monday }, DayOfWeek.Monday), "enum comparer specialization");

            var conversion = new Observed<int>(1);
            var converted = ReadOnlyListExtensions.ConvertAll(conversion, x => { conversion.Events.Add("convert:" + x); if (x == 1) conversion.Items.Add(2); return x.ToString(); });
            Check(converted is List<string> && converted.Count == 2 && converted[1] == "2", "conversion returns mutable List with live input");
            Check(conversion.Trace == "count,count,index:0,convert:1,count,index:1,convert:2,count", "conversion capacity then live Count");
            Check(ReadOnlyListExtensions.ConvertAll<int, string>(empty, null).Count == 0, "empty conversion bypasses null callback");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.ConvertAll<int, string>(none, null), "null conversion list");
            Throws<FormatException>(() => ReadOnlyListExtensions.ConvertAll<int, string>(new Observed<int>(1) { ItemRead = x => throw new FormatException() }, null), "conversion source before null delegate");
            Throws<ArgumentOutOfRangeException>(() => ReadOnlyListExtensions.ConvertAll(new Observed<int> { CountRead = () => -1 }, (Converter<int, string>)null), "capacity error before callback");

            int[] sorted = { 10, 20, 30, 40 };
            foreach (int value in new[] { 0, 10, 15, 20, 25, 30, 35, 40, 50 })
                Check(ReadOnlyListExtensions.BinarySearch<int>(sorted, value) == Array.BinarySearch(sorted, value), "binary index/insertion " + value);
            Check(ReadOnlyListExtensions.BinarySearch(empty, 2) == -1, "empty search");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.BinarySearch(none, 1), "null search");
            var comparable = new Comparable { Value = 2 };
            var compared = new Observed<Comparable>(new Comparable { Value = 1 }, new Comparable { Value = 3 });
            comparable.Compared = x => { compared.Events.Add("compare:" + x.Value); };
            Check(ReadOnlyListExtensions.BinarySearch(compared, comparable) == ~1, "value owns comparison");
            Check(compared.Trace == "count,index:0,compare:1,index:1,compare:3", "binary initial Count only");
            Check(ReadOnlyListExtensions.BinarySearch<Comparable>(new Comparable[0], null) == -1, "empty null comparable");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.BinarySearch<Comparable>(new[] { comparable }, null), "nonempty null comparable");
            compared = new Observed<Comparable>(new Comparable { Value = 1 }, new Comparable { Value = 3 });
            comparable.Compared = x => compared.Items.Clear();
            Throws<ArgumentOutOfRangeException>(() => ReadOnlyListExtensions.BinarySearch(compared, comparable), "binary mutation retains initial bound");
            Check(compared.Trace == "count,index:0,index:1", "binary mutation does not reread Count");
            int comparisons = 0;
            var overflowing = new Observed<Comparable> { CountRead = () => int.MaxValue, ItemRead = i => new Comparable() };
            var extremeQuery = new Comparable { Comparison = x => comparisons++ == 0 ? 1 : -1 };
            Check(ReadOnlyListExtensions.BinarySearch(overflowing, extremeQuery) == 536870912, "native wrapped midpoint termination result");
            Check(overflowing.Trace == "count,index:1073741823,index:-536870913", "binary midpoint uses wrapped sum, truncating division");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 10, (Comparison<int>)null) == 10, "exact search bypasses closest callback");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 0, (Comparison<int>)null) == 10, "lower endpoint bypasses closest callback");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 50, (Comparison<int>)null) == 40, "upper endpoint bypasses closest callback");
            Throws<ArgumentOutOfRangeException>(() => ReadOnlyListExtensions.BinarySearchClosest(empty, 1), "empty closest indexes first");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 15, (Comparison<int>)null), "interior null closest delegate");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 11) == 20, "default sign comparison ties upward despite numeric distance");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 19, false) == 10, "default sign comparison ties downward");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 11, (a, b) => a - b) == 10, "custom smaller lower signed distance");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 19, (a, b) => a - b) == 20, "custom smaller upper signed distance");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 15, (a, b) => a - b) == 20, "custom tie upward");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 15, (a, b) => a - b, false) == 10, "custom tie downward");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(sorted, 15, (a, b) => a == 15 ? -2 : -1) == 10, "signed comparison no absolute distance");
            var closest = new Observed<int>(10, 20);
            int calls = 0;
            Check(ReadOnlyListExtensions.BinarySearchClosest(closest, 15, (a, b) => { closest.Events.Add("closest:" + a + ":" + b); closest.Items.Clear(); return calls++ == 0 ? 3 : 8; }) == 10, "neighbors captured before callback mutation");
            Check(closest.Trace == "count,index:0,index:1,count,index:0,index:1,closest:15:10,closest:20:15", "closest lookup/callback operand order");

            var endpoints = new Observed<int>(3, 8);
            Check(ReadOnlyListExtensions.First(endpoints) == 3 && endpoints.Trace == "index:0", "first direct index");
            endpoints.Events.Clear();
            Check(ReadOnlyListExtensions.Last(endpoints) == 8 && endpoints.Trace == "count,index:1", "last Count then index");
            Throws<ArgumentOutOfRangeException>(() => ReadOnlyListExtensions.First(empty), "empty first source exception");
            Throws<ArgumentOutOfRangeException>(() => ReadOnlyListExtensions.Last(empty), "empty last source exception");
            Check(ReadOnlyListExtensions.FirstOrDefault(empty) == 0, "empty first default");
            Check(ReadOnlyListExtensions.LastOrDefault(empty) == 0, "empty last default");
            Check(ReadOnlyListExtensions.ElementAtOrDefault(none, -1) == 0, "negative null element default");
            Check(!ReadOnlyListExtensions.TryGetIndex(none, -1, out int result) && result == 0, "negative null try index");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.TryGetIndex(none, 0, out result), "nonnull index null list");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.FirstOrDefault(none), "null first default");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.LastOrDefault(none), "null last default");
            result = 99;
            Throws<FormatException>(() => ReadOnlyListExtensions.TryGetIndex(new Observed<int>(1) { CountRead = () => throw new FormatException() }, 0, out result), "count exception");
            Check(result == 99, "count failure preserves out storage");
            Throws<FormatException>(() => ReadOnlyListExtensions.TryGetIndex(new Observed<int>(1) { ItemRead = x => throw new FormatException() }, 0, out result), "index exception");
            Check(result == 99, "index failure preserves out storage");
            Check(!ReadOnlyListExtensions.TryGetIndex(endpoints, 2, out result) && result == 0, "past end defaults");
            endpoints.Events.Clear();
            Check(ReadOnlyListExtensions.TryGetLast(endpoints, out result) && result == 8, "last try success");
            Check(endpoints.Trace == "count,count,index:1", "last try double Count");
            int countReads = 0;
            var shrinking = new Observed<int>(1, 2) { CountRead = () => countReads++ == 0 ? 2 : 1 };
            Check(!ReadOnlyListExtensions.TryGetLast(shrinking, out result) && result == 0, "last try evaluates changed bound");

            var copying = new Observed<int>(1, 2);
            int[] destination = { -1, -1, -1, -1 };
            ReadOnlyListExtensions.CopyTo(copying, destination, 1);
            Check(string.Join(",", destination) == "-1,1,2,-1", "copy offset");
            Check(copying.Trace == "count,index:0,count,index:1,count", "copy live Count/source order");
            destination = new[] { -1 };
            Throws<IndexOutOfRangeException>(() => ReadOnlyListExtensions.CopyTo(copying, destination), "copy short destination");
            Check(destination[0] == 1, "copy partial writes survive");
            ReadOnlyListExtensions.CopyTo(empty, (int[])null, -99);
            Check(true, "empty copy ignores destination and start");
            Throws<FormatException>(() => ReadOnlyListExtensions.CopyTo(new Observed<int>(1) { ItemRead = i => throw new FormatException() }, (int[])null), "source read precedes null destination");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.CopyTo(copying, (int[])null), "nonnull source null destination");
            Throws<IndexOutOfRangeException>(() => ReadOnlyListExtensions.CopyTo(copying, new int[2], -1), "negative destination index");
            Throws<FormatException>(() => ReadOnlyListExtensions.CopyTo(new Observed<int>(1) { ItemRead = i => throw new FormatException() }, new int[0]), "source read precedes destination bounds");
            copying = new Observed<int>(1);
            copying.ItemRead = i => { if (i == 0) copying.Items.Add(2); return copying.Items[i]; };
            destination = new int[2];
            ReadOnlyListExtensions.CopyTo(copying, destination);
            Check(destination[1] == 2, "copy index mutation grows source");

            foreach (int failure in new[] { 0, 1, 2, 3 })
            {
                var events = new List<string>();
                var value = new Validatable { Action = () => { events.Add("validate"); if (failure == 1) throw new FormatException(); } };
                var iterator = new ValidationEnumerator(events, value) { ThrowMove = failure == 2, ThrowCurrent = failure == 3 };
                var validatables = new Observed<Validatable> { Enumerator = () => iterator };
                if (failure == 0) { ReadOnlyListExtensions.Validate(validatables); Check(events.Count == 5, "validation completion"); }
                else Throws<FormatException>(() => ReadOnlyListExtensions.Validate(validatables), "validation failure " + failure);
                Check(events[events.Count - 1] == "dispose", "enumerator finally disposal " + failure);
                Check(validatables.Trace == "enumerator", "validation uses enumeration without Count/index " + failure);
            }
            var nullEvents = new List<string>();
            var nullIterator = new ValidationEnumerator(nullEvents, null);
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.Validate(new Observed<Validatable> { Enumerator = () => nullIterator }), "null validatable not skipped");
            Check(nullEvents[nullEvents.Count - 1] == "dispose", "null validatable still disposes");
            Throws<NullReferenceException>(() => ReadOnlyListExtensions.Validate((IReadOnlyList<Validatable>)null), "null validation list");
            var disposeEvents = new List<string>();
            var disposing = new ValidationEnumerator(disposeEvents, new Validatable { Action = () => { } }) { ThrowDispose = true };
            Throws<ApplicationException>(() => ReadOnlyListExtensions.Validate(new Observed<Validatable> { Enumerator = () => disposing }), "disposal error propagates");
            var values = new[] { new ValidatableValue { Value = 4 } };
            ValidatableValue.Total = 0;
            ReadOnlyListExtensions.Validate<ValidatableValue>(values);
            Check(ValidatableValue.Total == 4 && values[0].Value == 4, "struct validated by local copy");
            var mutating = new Observed<Validatable>();
            mutating.Items.Add(new Validatable { Action = () => mutating.Items.Add(null) });
            Throws<InvalidOperationException>(() => ReadOnlyListExtensions.Validate(mutating), "validation preserves ordinary enumerator mutation error");
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
            Check(typeof(ReadOnlyListExtensions).GetMethods(flags).Length == 18, "complete original root method surface");
            Check(typeof(ReadOnlyListExtensions).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly).Length == 0, "original root fieldless");
            Check(typeof(IValidatable).IsInterface && typeof(IValidatable).GetMethods().Length == 1 && typeof(IValidatable).GetMethod("Validate").IsAbstract, "complete original validation interface");
            VerifyCompiledCallbackIdentity();
            return checks;
        }

        private static void VerifyCompiledCallbackIdentity()
        {
            // The default comparison is the original cached compiler callback.
            // Check the matching Editor's output, rather than assuming another
            // compiler preserves this nested type and method ordinal.
            var declared = System.Reflection.BindingFlags.DeclaredOnly;
            var all = declared | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var root = typeof(ReadOnlyListExtensions);
            Check(root.Assembly.GetName().Name == "HLUnityCore.Runtime", "original assembly identity");
            Check(root.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false), "original extension class attribute");
            Check((root.Attributes & System.Reflection.TypeAttributes.BeforeFieldInit) != 0, "original root BeforeFieldInit");
            var nested = root.GetNestedType("<>c__6`1", System.Reflection.BindingFlags.NonPublic);
            Check(nested != null && root.GetNestedTypes(all).Length == 1, "original single generic callback type and ordinal");
            Check(nested.IsNestedPrivate && nested.IsSealed && nested.IsSerializable, "original private sealed serializable callback");
            Check(nested.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false), "original generated callback attribute");
            Check((nested.Attributes & System.Reflection.TypeAttributes.BeforeFieldInit) != 0, "original callback BeforeFieldInit");
            var parameters = nested.GetGenericArguments();
            Check(parameters.Length == 1 && parameters[0].Name == "T", "original callback generic parameter");
            var closed = nested.MakeGenericType(typeof(int));
            var singleton = closed.GetField("<>9", all | System.Reflection.BindingFlags.Static);
            var comparison = closed.GetField("<>9__6_0", all | System.Reflection.BindingFlags.Static);
            Check(closed.GetFields(all | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance).Length == 2,
                "original complete callback field surface");
            Check(singleton != null && singleton.IsPublic && singleton.IsStatic && singleton.IsInitOnly && singleton.FieldType == closed,
                "original readonly singleton identity");
            Check(comparison != null && comparison.IsPublic && comparison.IsStatic && !comparison.IsInitOnly && comparison.FieldType == typeof(Comparison<int>),
                "original mutable comparison cache identity");
            var callback = closed.GetMethod("<BinarySearchClosest>b__6_0", all | System.Reflection.BindingFlags.Instance);
            Check(callback != null && callback.IsAssembly && callback.ReturnType == typeof(int) && callback.GetParameters().Length == 2,
                "original generated comparison method surface");
            Check(closed.GetMethods(all | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static).Length == 1 &&
                closed.GetConstructors(all | System.Reflection.BindingFlags.Instance).Length == 1 && closed.TypeInitializer != null,
                "original generated constructor and initializer surface");
            var cache = (Comparison<int>)comparison.GetValue(null);
            Check(cache != null && ReferenceEquals(cache.Target, singleton.GetValue(null)) && cache.Method == callback,
                "default closest uses original singleton callback");
            Check(cache(15, 10) == 1 && cache(20, 15) == 1, "cached callback retains signed CompareTo behavior");
            Check(ReadOnlyListExtensions.BinarySearchClosest<int>(new[] { 10, 20 }, 11) == 20 &&
                ReferenceEquals(cache, comparison.GetValue(null)), "subsequent closest call reuses original cache");
        }
    }
}
