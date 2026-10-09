using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Hardlight;
using Hardlight.Utils;

namespace ProjectLucid.RecoveredProof
{
    // Managed cases use owned builders and collection clients. Existing cached
    // references and culture are restored even when a verification fails.
    public static class OriginalCoreHelperPreservationVerification
    {
        static void Check(ref int n, bool ok, string label)
        { if (!ok) throw new InvalidOperationException(label); ++n; }
        static Exception Fault(Action action)
        { try { action(); } catch (Exception e) { return e; } return null; }
        static OpString Text(string value) => OpString.Create(64) + value;
        static FieldInfo Field(string name) => typeof(OpString).GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        static StringBuilder Builder(OpString value) => (StringBuilder)Field("sb").GetValue(value);
        static string Items(IList<int> list) => string.Join(",", list);

        public static int OpStringCreationMutationAndOverloads()
        {
            int n = 0;
            var value = OpString.Create(32);
            Check(ref n, value.Capacity == 32 && value.Length == 0, "explicit capacity and empty length");
            Check(ref n, OpString.small.Capacity == 64, "small capacity");
            Check(ref n, OpString.medium.Capacity == 256, "medium capacity");
            Check(ref n, OpString.large.Capacity == 1024, "large capacity");
            Check(ref n, !ReferenceEquals(OpString.small, OpString.small), "small is newly owned");
            Check(ref n, !ReferenceEquals(OpString.medium, OpString.medium), "medium is newly owned");
            Check(ref n, !ReferenceEquals(OpString.large, OpString.large), "large is newly owned");
            Check(ref n, Fault(() => OpString.Create(-1)) is ArgumentOutOfRangeException, "negative capacity faults");
            Check(ref n, ReferenceEquals(value + "ab", value), "string append identity");
            Check(ref n, ReferenceEquals(value + 'c', value), "character append identity");
            Check(ref n, ReferenceEquals(value + new[] { 'd', 'e' }, value), "array append identity");
            Check(ref n, ReferenceEquals(value + new StringBuilder("f"), value), "builder append identity");
            Check(ref n, value.ToString() == "abcdef", "append order");
            Check(ref n, ReferenceEquals(value.Replace("cd", "X"), value), "replace identity");
            Check(ref n, value.ToString() == "abXef", "replace content");
            Check(ref n, ReferenceEquals(value.Remove(1, 2), value), "remove identity");
            Check(ref n, value.ToString() == "aef", "remove content");
            value.Length = 1;
            Check(ref n, value.ToString() == "a", "length truncation");
            value.Length = 3;
            Check(ref n, value.ToString() == "a\0\0", "length extension uses null characters");
            value.Capacity = 70;
            Check(ref n, value.Capacity == 70 && value.Length == 3, "capacity keeps content length");
            var old = Builder(value); value.Clear();
            Check(ref n, !ReferenceEquals(Builder(value), old), "clear replaces builder");
            Check(ref n, value.Length == 0 && old.ToString() == "a\0\0", "clear leaves old alias intact");
            Check(ref n, ReferenceEquals(value + (string)null, value) && value.Length == 0, "null string append");
            Check(ref n, ReferenceEquals(value + (char[])null, value) && value.Length == 0, "null character array append");
            Check(ref n, ReferenceEquals(value + (StringBuilder)null, value) && value.Length == 0, "null builder append");
            Check(ref n, Fault(() => value.Remove(0, 1)) is ArgumentOutOfRangeException && value.Length == 0, "remove failure prefix");
            Check(ref n, Fault(() => value.Replace("", "x")) is ArgumentException && value.Length == 0, "replace failure prefix");
            Check(ref n, Fault(() => { string unused = (OpString)null; }) is NullReferenceException, "implicit null faults");
            Check(ref n, Fault(() => { var unused = (OpString)null + "x"; }) is NullReferenceException, "null receiver append faults");
            return n;
        }

        public static int OpStringCultureFormattingAndOriginalTrim()
        {
            int n = 0; var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var v = OpString.Create(128);
                Check(ref n, ReferenceEquals(v + true, v), "boolean append identity");
                Check(ref n, ReferenceEquals(v + (short)-12, v), "short append identity");
                Check(ref n, ReferenceEquals(v + (byte)255, v), "byte append identity");
                Check(ref n, ReferenceEquals(v + 1.5f, v), "float append identity");
                Check(ref n, v.ToString() == "True-122551,5", "BCL scalar overload and current culture");
                Check(ref n, ReferenceEquals(v + 0, v), "zero int identity");
                Check(ref n, ReferenceEquals(v + 0u, v), "zero uint identity");
                Check(ref n, v.ToString() == "True-122551,500", "zero ASCII digits");
                Check(ref n, (string)(Text("") + -12345) == "-12345", "negative integer digits");
                Check(ref n, (string)(Text("") + int.MaxValue) == "2147483647", "bounded signed maximum digits");
                Check(ref n, (string)(Text("") + uint.MaxValue) == "4294967295", "bounded unsigned maximum digits");
                Check(ref n, (string)(Text("") + 1000000000) == "1000000000", "decimal power boundary");
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                var lower = Text("Iİ-x"); var upper = Text("iı-X");
                Check(ref n, ReferenceEquals(lower.ToLower(), lower), "lower preserves identity");
                Check(ref n, lower.ToString() == "ıi-x", "lower uses current culture");
                Check(ref n, ReferenceEquals(upper.ToUpper(), upper), "upper preserves identity");
                Check(ref n, upper.ToString() == "İI-X", "upper uses current culture");
                Check(ref n, Text("").ToLower().Length == 0 && Text("").ToUpper().Length == 0, "empty case conversion");
                var trimmed = Text("  abc  ");
                Check(ref n, ReferenceEquals(trimmed.Trim(), trimmed), "trim identity");
                Check(ref n, trimmed.ToString() == "ab", "original end before start and stopping-character removal");
                Check(ref n, Text("abc\t ").TrimEnd().ToString() == "ab", "end removes nonspace stopping character");
                Check(ref n, Text("abc").TrimEnd().ToString() == "abc", "end unchanged without trailing whitespace");
                Check(ref n, Text("x ").TrimEnd().ToString() == "", "one character and suffix removed");
                Check(ref n, Text(" \t\n").TrimEnd().ToString() == " \t\n", "all-space end remains unchanged");
                Check(ref n, Text(" \t\n").TrimStart().ToString() == " \t\n", "all-space start remains unchanged");
                Check(ref n, Text(" \t\n").Trim().ToString() == " \t\n", "all-space trim remains unchanged");
                Check(ref n, Text("  abc").TrimStart().ToString() == "abc", "start removes prefix only");
                Check(ref n, Text("abc  ").TrimStart().ToString() == "abc  ", "start retains suffix");
                Check(ref n, Text("").Trim().Length == 0, "empty trim");
                Check(ref n, Text(" a ").Trim().ToString() == " ", "trim keeps remaining all-space prefix");
            }
            finally { CultureInfo.CurrentCulture = oldCulture; }
            Check(ref n, ReferenceEquals(CultureInfo.CurrentCulture, oldCulture), "exact culture reference restored");
            return n;
        }

        sealed class CacheScope : IDisposable
        {
            readonly FieldInfo instance = Field("instance"), thread = Field("singletonThread");
            readonly object oldInstance, oldThread; public readonly OpString Owned;
            public CacheScope()
            {
                RuntimeHelpers.RunClassConstructor(typeof(OpString).TypeHandle);
                oldInstance = instance.GetValue(null); oldThread = thread.GetValue(null);
                Owned = OpString.Create(1024); instance.SetValue(null, Owned); thread.SetValue(null, null);
            }
            public void Dispose() { instance.SetValue(null, oldInstance); thread.SetValue(null, oldThread); }
        }

        public static int OpStringThreadCacheAndExceptionalRestoration()
        {
            int n = 0;
            RuntimeHelpers.RunClassConstructor(typeof(OpString).TypeHandle);
            var oldInstance = Field("instance").GetValue(null); var oldThread = Field("singletonThread").GetValue(null);
            var oldBuilder = Builder((OpString)oldInstance); string oldText = oldBuilder.ToString();
            using (var scope = new CacheScope())
            {
                var first = OpString.i; var builder = Builder(first);
                Check(ref n, ReferenceEquals(first, scope.Owned), "first accessor owned cache");
                Check(ref n, ReferenceEquals(Field("singletonThread").GetValue(null), Thread.CurrentThread), "first thread ownership");
                var alias = first + "before";
                Check(ref n, ReferenceEquals(alias, first) && alias.ToString() == "before", "live alias content");
                Check(ref n, ReferenceEquals(OpString.i, first), "same owner cache reused");
                Check(ref n, ReferenceEquals(Builder(first), builder), "accessor preserves builder identity");
                Check(ref n, alias.Length == 0, "existing cache alias observes clearing");
                first = first + "owner";
                OpString otherA = null, otherB = null; Exception failure = null;
                var worker = new Thread(() => { try { otherA = OpString.i + "worker"; otherB = OpString.i; } catch (Exception e) { failure = e; } });
                worker.Start(); try { Check(ref n, worker.Join(10000), "owned thread completed"); } finally { worker.Join(); }
                Check(ref n, failure == null, "other-thread calls succeed");
                Check(ref n, !ReferenceEquals(otherA, first) && !ReferenceEquals(otherB, first), "other thread never aliases cache");
                Check(ref n, !ReferenceEquals(otherA, otherB), "each other-thread access is fresh");
                Check(ref n, otherA.ToString() == "worker" && otherB.Length == 0, "fresh small does not clear earlier other instance");
                Check(ref n, otherA.Capacity == 64 && otherB.Capacity == 64, "other-thread small capacity");
                Check(ref n, first.ToString() == "owner", "other-thread leaves owner builder intact");
                Check(ref n, ReferenceEquals(Field("singletonThread").GetValue(null), Thread.CurrentThread), "other-thread preserves owner");
                first.Clear();
                Check(ref n, !ReferenceEquals(Builder(first), builder), "explicit clear replaces owned cache builder");
                Check(ref n, builder.ToString() == "owner", "clear preserves prior builder alias");
            }
            Check(ref n, ReferenceEquals(Field("instance").GetValue(null), oldInstance), "cache instance reference restored");
            Check(ref n, ReferenceEquals(Field("singletonThread").GetValue(null), oldThread), "cache thread reference restored");
            Check(ref n, ReferenceEquals(Builder((OpString)oldInstance), oldBuilder) && oldBuilder.ToString() == oldText, "preexisting builder untouched");
            var expected = new InvalidOperationException("owned fault");
            Check(ref n, ReferenceEquals(Fault(() => { using (var scope = new CacheScope()) { var v = OpString.i + "fault"; throw expected; } }), expected), "exception not replaced during cleanup");
            Check(ref n, ReferenceEquals(Field("instance").GetValue(null), oldInstance) && ReferenceEquals(Field("singletonThread").GetValue(null), oldThread), "exception restores exact old cache references");
            Check(ref n, oldBuilder.ToString() == oldText, "exception leaves preexisting builder content");
            return n;
        }

        public static int ListSearchLiveAliasAndOutFaults()
        {
            int n = 0; var list = new List<string> { "first", "match", "last" }; string result = "prior"; var calls = new List<string>();
            bool found = Hardlight.ListExtensions.TryFind(list, value => { calls.Add(value); if (value != "match") return false; list[1] = "replacement"; return true; }, out result);
            Check(ref n, found, "search succeeds");
            Check(ref n, result == "replacement", "search performs live second index read");
            Check(ref n, string.Join(",", calls) == "first,match", "search stops on match");
            Check(ref n, list[1] == "replacement", "callback mutation retained");
            found = Hardlight.ListExtensions.TryFind(list, value => false, out result);
            Check(ref n, !found && result == null, "missing reference writes default");
            int number = 9; found = Hardlight.ListExtensions.TryFind(new List<int> { 3, 4 }, value => false, out number);
            Check(ref n, !found && number == 0, "missing value writes default");
            found = Hardlight.ListExtensions.TryFind(new List<int> { 3, 4 }, value => value == 4, out number);
            Check(ref n, found && number == 4, "value match forwarded");
            var expected = new InvalidOperationException("predicate"); result = "prior"; calls.Clear();
            var error = Fault(() => Hardlight.ListExtensions.TryFind(list, value => { calls.Add(value); throw expected; }, out result));
            Check(ref n, ReferenceEquals(error, expected), "predicate exact fault");
            Check(ref n, result == "prior", "predicate failure leaves out untouched");
            Check(ref n, string.Join(",", calls) == "first", "predicate fault prefix");
            var one = new List<string> { "only" }; result = "prior";
            error = Fault(() => Hardlight.ListExtensions.TryFind(one, value => { one.Clear(); return true; }, out result));
            Check(ref n, error is ArgumentOutOfRangeException, "matched index is reread after removal");
            Check(ref n, result == "prior" && one.Count == 0, "index fault leaves out and retains mutation");
            Check(ref n, !Hardlight.ListExtensions.TryFind(new List<string>(), null, out result) && result == null, "empty does not call null predicate");
            result = "prior";
            Check(ref n, Fault(() => Hardlight.ListExtensions.TryFind((List<string>)null, value => true, out result)) is NullReferenceException, "null list search fault");
            Check(ref n, result == "prior", "null search preserves out");
            return n;
        }

        sealed class ObservedList : IList<int>
        {
            public readonly List<int> Data; public readonly List<string> Trace;
            public Action<int> BeforeAdd, BeforeContains; public Action<int,int> BeforeSet; public Action<int> BeforeGet;
            public ObservedList(List<string> trace, params int[] values) { Trace = trace; Data = new List<int>(values); }
            public int Count { get { Trace.Add("count"); return Data.Count; } }
            public bool IsReadOnly => false;
            public int this[int i] { get { Trace.Add("get:" + i); BeforeGet?.Invoke(i); return Data[i]; } set { Trace.Add("set:" + i + ":" + value); BeforeSet?.Invoke(i,value); Data[i] = value; } }
            public bool Contains(int v) { Trace.Add("contains:" + v); BeforeContains?.Invoke(v); return Data.Contains(v); }
            public void Add(int v) { Trace.Add("add:" + v); BeforeAdd?.Invoke(v); Data.Add(v); }
            public int IndexOf(int v) => Data.IndexOf(v); public void Insert(int i,int v) => Data.Insert(i,v);
            public bool Remove(int v) => Data.Remove(v); public void RemoveAt(int i) => Data.RemoveAt(i);
            public void Clear() => Data.Clear(); public void CopyTo(int[] a,int i) => Data.CopyTo(a,i);
            public IEnumerator<int> GetEnumerator() => Data.GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        sealed class ObservedRange : IReadOnlyCollection<int>
        {
            readonly int[] data; readonly List<string> trace; public int FaultMove = -1; public Exception Error;
            public ObservedRange(List<string> trace, params int[] data) { this.trace = trace; this.data = data; }
            public int Count => data.Length;
            public IEnumerator<int> GetEnumerator() { trace.Add("enumerator"); return new Iter(this); }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            sealed class Iter : IEnumerator<int>
            {
                readonly ObservedRange owner; int index = -1; public Iter(ObservedRange owner) { this.owner = owner; }
                public bool MoveNext() { ++index; owner.trace.Add("move:"+index); if (index == owner.FaultMove) throw owner.Error; return index < owner.data.Length; }
                public int Current { get { owner.trace.Add("current:"+index); return owner.data[index]; } }
                object IEnumerator.Current => Current; public void Dispose() { owner.trace.Add("dispose"); } public void Reset() => throw new NotSupportedException();
            }
        }

        public static int ListUniqueRangeCallbacksAndDisposal()
        {
            int n = 0; var trace = new List<string>(); var list = new ObservedList(trace, 1);
            Check(ref n, !Hardlight.ListExtensions.AddUnique(list, 1), "duplicate false");
            Check(ref n, string.Join(",", trace) == "contains:1", "duplicate no add"); trace.Clear();
            Check(ref n, Hardlight.ListExtensions.AddUnique(list, 2), "new true");
            Check(ref n, string.Join(",", trace) == "contains:2,add:2", "contains before add");
            Check(ref n, Items(list.Data) == "1,2", "new appended"); trace.Clear();
            var range = new ObservedRange(trace, 2, 3, 4);
            Check(ref n, Hardlight.ListExtensions.AddUniqueFromRange(list, range), "range true");
            Check(ref n, Items(list.Data) == "1,2,3,4", "all later entries evaluated after first success");
            Check(ref n, string.Join(",", trace) == "enumerator,move:0,current:0,contains:2,move:1,current:1,contains:3,add:3,move:2,current:2,contains:4,add:4,move:3,dispose", "range order and disposal");
            Check(ref n, !Hardlight.ListExtensions.AddUniqueFromRange(list.Data, list.Data), "self range alias does not mutate");
            Check(ref n, Items(list.Data) == "1,2,3,4", "self alias content intact"); trace.Clear();
            var expected = new InvalidOperationException("enumeration"); range = new ObservedRange(trace, 5, 6) { FaultMove = 1, Error = expected };
            Check(ref n, ReferenceEquals(Fault(() => Hardlight.ListExtensions.AddUniqueFromRange(list, range)), expected), "enumeration exact fault");
            Check(ref n, Items(list.Data) == "1,2,3,4,5", "earlier addition survives enumeration fault");
            Check(ref n, string.Join(",", trace) == "enumerator,move:0,current:0,contains:5,add:5,move:1,dispose", "fault disposal prefix"); trace.Clear();
            list.BeforeContains = value => { throw expected; };
            Check(ref n, ReferenceEquals(Fault(() => Hardlight.ListExtensions.AddUnique(list, 7)), expected), "contains exact fault");
            Check(ref n, string.Join(",", trace) == "contains:7" && !list.Data.Contains(7), "contains failure skips add");
            list.BeforeContains = null; list.BeforeAdd = value => { throw expected; }; trace.Clear();
            Check(ref n, ReferenceEquals(Fault(() => Hardlight.ListExtensions.AddUniqueFromRange(list, new ObservedRange(trace, 7, 8))), expected), "add exact fault");
            Check(ref n, string.Join(",", trace) == "enumerator,move:0,current:0,contains:7,add:7,dispose", "add fault disposes before next entry");
            Check(ref n, !list.Data.Contains(7) && !list.Data.Contains(8), "add failure prefix content");
            Check(ref n, Fault(() => Hardlight.ListExtensions.AddUnique((IList<int>)null, 1)) is NullReferenceException, "null unique list");
            Check(ref n, !Hardlight.ListExtensions.AddUniqueFromRange((IList<int>)null, Array.Empty<int>()), "empty range does not touch null list");
            return n;
        }

        sealed class ObservedRandom : System.Random
        {
            readonly List<string> trace; readonly int[] answers; int call; public Action<int> BeforeNext;
            public ObservedRandom(List<string> trace, params int[] answers) { this.trace = trace; this.answers = answers; }
            public override int Next(int minValue, int maxValue) { trace.Add("next:"+minValue+":"+maxValue); BeforeNext?.Invoke(call); return answers[call++]; }
        }

        public static int SeededShuffleSnapshotCallbacksAndPartialWrites()
        {
            int n = 0; var trace = new List<string>(); var list = new ObservedList(trace, 10, 20, 30, 40); var random = new ObservedRandom(trace, 2, 3, 2);
            Hardlight.ListExtensions.Shuffle(list, random);
            Check(ref n, Items(list.Data) == "30,40,10,20", "explicit permutation");
            Check(ref n, string.Join(",", trace) == "count,next:0:4,get:2,get:0,set:0:30,set:2:10,next:1:4,get:3,get:1,set:1:40,set:3:20,next:2:4,get:2,get:2,set:2:10,set:2:10", "all bounds reads and writes");
            trace.Clear(); var empty = new ObservedList(trace); Hardlight.ListExtensions.Shuffle(empty, null);
            Check(ref n, string.Join(",", trace) == "count", "empty avoids null random"); trace.Clear();
            var one = new ObservedList(trace, 7); Hardlight.ListExtensions.Shuffle(one, null);
            Check(ref n, string.Join(",", trace) == "count" && Items(one.Data) == "7", "single avoids null random"); trace.Clear();
            list = new ObservedList(trace, 10, 20); var expected = new InvalidOperationException("shuffle");
            Check(ref n, Fault(() => Hardlight.ListExtensions.Shuffle(list, null)) is NullReferenceException, "nonempty null random");
            Check(ref n, string.Join(",", trace) == "count" && Items(list.Data) == "10,20", "null random before reads"); trace.Clear();
            random = new ObservedRandom(trace, 1) { BeforeNext = call => { throw expected; } };
            Check(ref n, ReferenceEquals(Fault(() => Hardlight.ListExtensions.Shuffle(list, random)), expected), "random exact fault");
            Check(ref n, string.Join(",", trace) == "count,next:0:2", "random before element reads"); trace.Clear();
            random = new ObservedRandom(trace, 1); list.BeforeGet = i => { if (i == 0) throw expected; };
            Check(ref n, ReferenceEquals(Fault(() => Hardlight.ListExtensions.Shuffle(list, random)), expected), "second read exact fault");
            Check(ref n, string.Join(",", trace) == "count,next:0:2,get:1,get:0", "both reads before writes");
            Check(ref n, Items(list.Data) == "10,20", "read fault no mutation"); trace.Clear(); list.BeforeGet = null;
            list.BeforeSet = (i,v) => { if (i == 1) throw expected; }; random = new ObservedRandom(trace, 1);
            Check(ref n, ReferenceEquals(Fault(() => Hardlight.ListExtensions.Shuffle(list, random)), expected), "second write exact fault");
            Check(ref n, Items(list.Data) == "20,20", "first write survives second-write fault");
            Check(ref n, string.Join(",", trace) == "count,next:0:2,get:1,get:0,set:0:20,set:1:10", "partial-write prefix"); trace.Clear();
            var growing = new ObservedList(trace, 1, 2, 3); random = new ObservedRandom(trace, 2, 1) { BeforeNext = call => { if (call == 0) growing.Data.Add(99); } };
            Hardlight.ListExtensions.Shuffle(growing, random);
            Check(ref n, Items(growing.Data) == "3,2,1,99", "random callback mutation retained");
            Check(ref n, string.Join(",", trace) == "count,next:0:3,get:2,get:0,set:0:3,set:2:1,next:1:3,get:1,get:1,set:1:2,set:1:2", "count remains original snapshot");
            return n;
        }

        public static int ResourcePathStringsAndOutFaultPrefixes()
        {
            int n = 0; string path, name;
            Check(ref n, ResourceUtils.GetResourceLeafName("a/b/c") == "c", "leaf");
            Check(ref n, ResourceUtils.GetResourceLeafName("a//") == "", "trailing slash leaf");
            Check(ref n, ResourceUtils.GetResourceLeafName("/c") == "c", "leading slash leaf");
            Check(ref n, ResourceUtils.GetResourceLeafName("") == "", "empty leaf");
            Check(ref n, Fault(() => ResourceUtils.GetResourceLeafName(null)) is NullReferenceException, "null leaf");
            ResourceUtils.SplitPathAndName("a/b/c", out path, out name);
            Check(ref n, path == "a/b" && name == "c", "last slash split");
            ResourceUtils.SplitPathAndName("/c", out path, out name);
            Check(ref n, path == "" && name == "/c", "index-zero slash is not split");
            ResourceUtils.SplitPathAndName("a/", out path, out name);
            Check(ref n, path == "a" && name == "", "trailing split");
            var literal = new string(new[] { 'n', 'a', 'm', 'e' }); ResourceUtils.SplitPathAndName(literal, out path, out name);
            Check(ref n, path == "" && ReferenceEquals(name, literal), "unsplit exact string alias");
            path = "prior path"; name = "prior name";
            Check(ref n, Fault(() => ResourceUtils.SplitPathAndName(null, out path, out name)) is NullReferenceException, "null split");
            Check(ref n, path == "prior path" && name == "prior name", "split failure before either out");
            Check(ref n, ReferenceEquals(ResourceUtils.GetPathDifference("", literal), literal), "empty base exact destination alias");
            Check(ref n, ResourceUtils.GetPathDifference("", null) == null, "empty base bypasses null destination");
            Check(ref n, ResourceUtils.GetPathDifference("a/b/c", "a/d/e") == "../d/e", "one remaining base slash");
            Check(ref n, ResourceUtils.GetPathDifference("a/b/c/d", "a/d/e") == "../../d/e", "two remaining base slashes");
            Check(ref n, ResourceUtils.GetPathDifference("a/b", "A/B") == "", "invariant equal path");
            Check(ref n, ReferenceEquals(ResourceUtils.GetPathDifference("z", literal), literal), "first-character difference alias");
            Check(ref n, ResourceUtils.GetPathDifference("a/b", "a/") == "./", "same directory marker");
            Check(ref n, ResourceUtils.GetPathDifference("a", "ab") == "ab", "no segment-boundary normalization");
            Check(ref n, ResourceUtils.GetPathDifference("abc", "ab") == "ab", "short destination no slash");
            Check(ref n, Fault(() => ResourceUtils.GetPathDifference(null, "a")) is NullReferenceException, "null base");
            Check(ref n, Fault(() => ResourceUtils.GetPathDifference("a", null)) is NullReferenceException, "null nonempty destination");
            var oldCulture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Check(ref n, ResourceUtils.GetPathDifference("I/x", "i/y") == "y", "path comparison invariant under Turkish culture"); }
            finally { CultureInfo.CurrentCulture = oldCulture; }
            Check(ref n, ReferenceEquals(CultureInfo.CurrentCulture, oldCulture), "path culture exact restoration");
            Check(ref n, new ResourceUtils().GetType() == typeof(ResourceUtils), "genuine public ordinary constructor");
            return n;
        }

        public static int ResourceCombineCacheAliasesAndRestoration()
        {
            int n = 0; RuntimeHelpers.RunClassConstructor(typeof(OpString).TypeHandle);
            var oldInstance = Field("instance").GetValue(null); var oldThread = Field("singletonThread").GetValue(null);
            var oldBuilder = Builder((OpString)oldInstance); string oldText = oldBuilder.ToString();
            var a = new string(new[] { 'a' }); var b = new string(new[] { 'b' });
            using (var scope = new CacheScope())
            {
                var cache = OpString.i + "untouched";
                Check(ref n, ReferenceEquals(ResourceUtils.CombinePaths(a, ""), a), "empty second exact first alias");
                Check(ref n, ReferenceEquals(ResourceUtils.CombinePaths(a, null), a), "null second exact first alias");
                Check(ref n, ReferenceEquals(ResourceUtils.CombinePaths("", b), b), "empty first exact second alias");
                Check(ref n, ReferenceEquals(ResourceUtils.CombinePaths(null, b), b), "null first exact second alias");
                Check(ref n, ResourceUtils.CombinePaths(null, null) == null, "both null");
                Check(ref n, cache.ToString() == "untouched", "early returns do not access cache");
                Check(ref n, ResourceUtils.CombinePaths(a, b) == "a/b", "insert one slash");
                Check(ref n, cache.ToString() == "a/b", "existing cache alias observes combination");
                Check(ref n, ResourceUtils.CombinePaths("a/", "b") == "a/b", "existing slash retained");
                Check(ref n, ResourceUtils.CombinePaths("a/", "/b") == "a//b", "doubled slash retained");
                Check(ref n, ResourceUtils.CombinePaths("a", "/b") == "a//b", "leading second slash retained");
                Check(ref n, cache.ToString() == "a//b", "last combination live cache content");
            }
            Check(ref n, ReferenceEquals(Field("instance").GetValue(null), oldInstance) && ReferenceEquals(Field("singletonThread").GetValue(null), oldThread), "combine restores exact cache references");
            Check(ref n, ReferenceEquals(Builder((OpString)oldInstance), oldBuilder) && oldBuilder.ToString() == oldText, "combine never mutates old builder");
            return n;
        }
    }
}
