using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hardlight;

namespace ProjectLucid.Verification
{
// Fixed behavioral oracles use fresh BCL clients for every case.
public sealed class OriginalEnumerablePreservationVerification
{
    int checks, failures;
    readonly List<string> failedChecks = new List<string>();
    void Check(string name, bool pass)
    {
        ++checks;
        if (!pass) ++failures;
        if (!pass) failedChecks.Add(name);
    }
    void Events(string name, List<string> log, string expected) => Check(name, string.Join("|", log) == expected);
    void Throws(string name, Action action, Type type, Exception identity = null)
    {
        Exception observed = null;
        try { action(); } catch (Exception error) { observed = error; }
        Check(name, observed != null && observed.GetType() == type && (identity == null || ReferenceEquals(identity, observed)));
    }
    sealed class Marker : Exception { public Marker(string message) : base(message) { } }
    sealed class Trace<T> : IEnumerable<T>
    {
        public readonly List<string> Log;
        public readonly string Name;
        public readonly T[] Items;
        public Exception GetFault, MoveFault, CurrentFault, DisposeFault;
        public int MoveFaultAt, CurrentFaultAt = -1;
        public bool NullEnumerator;
        public E Last;
        public Trace(List<string> log, string name, params T[] items) { Log = log; Name = name; Items = items; }
        public IEnumerator<T> GetEnumerator()
        {
            Log.Add(Name + ".get");
            if (GetFault != null) throw GetFault;
            if (NullEnumerator) return null;
            return Last = new E(this);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public sealed class E : IEnumerator<T>
        {
            readonly Trace<T> owner;
            int index = -1;
            public int Moves, Disposes;
            public E(Trace<T> owner) { this.owner = owner; }
            public T Current
            {
                get
                {
                    owner.Log.Add(owner.Name + ".current" + index);
                    if (owner.CurrentFault != null && index == owner.CurrentFaultAt) throw owner.CurrentFault;
                    return owner.Items[index];
                }
            }
            object IEnumerator.Current => Current;
            public bool MoveNext()
            {
                ++Moves; owner.Log.Add(owner.Name + ".move" + Moves);
                if (owner.MoveFault != null && Moves == owner.MoveFaultAt) throw owner.MoveFault;
                return ++index < owner.Items.Length;
            }
            public void Dispose()
            {
                ++Disposes; owner.Log.Add(owner.Name + ".dispose");
                if (owner.DisposeFault != null) throw owner.DisposeFault;
            }
            public void Reset() { throw new NotSupportedException(); }
        }
    }
    sealed class EqualitySpy : IEquatable<EqualitySpy>
    {
        readonly List<string> log; readonly string name; readonly bool result;
        public EqualitySpy(List<string> log, string name, bool result) { this.log = log; this.name = name; this.result = result; }
        public override bool Equals(object other) { log.Add(name + ".ObjectEquals"); return result; }
        public bool Equals(EqualitySpy other) { log.Add(name + ".TypedEquals"); return false; }
        public override int GetHashCode() => 0;
    }
    sealed class TextSpy
    {
        readonly List<string> log; readonly string text; readonly Exception fault;
        public TextSpy(List<string> log, string text, Exception fault = null) { this.log = log; this.text = text; this.fault = fault; }
        public override string ToString() { log.Add("text"); if (fault != null) throw fault; return text; }
    }
    sealed class HashSpy
    {
        readonly int hash; public HashSpy(int hash) { this.hash = hash; }
        public override int GetHashCode() => hash;
    }
    void Equality()
    {
        Check("equals both-null reference identity", IEnumerableExtensions.EqualsDeep(null, null));
        var log = new List<string>(); var same = new Trace<object>(log, "s"); same.GetFault = new Marker("unused");
        Check("equals same collection bypasses acquisition", same.EqualsDeep(same)); Events("same no acquisition", log, "");
        Check("equals rhs-null false", !same.EqualsDeep(null)); Events("rhs-null no acquisition", log, "");
        Throws("equals lhs-null asymmetric fault", () => IEnumerableExtensions.EqualsDeep(null, same), typeof(NullReferenceException));
        var l = new Trace<object>(log, "l"); var r = new Trace<object>(log, "r");
        Check("equals empty true", l.EqualsDeep(r)); Events("equals empty both move and reverse dispose", log, "l.get|r.get|l.move1|r.move1|r.dispose|l.dispose");
        log.Clear(); l = new Trace<object>(log, "l"); r = new Trace<object>(log, "r", 7);
        Check("equals unequal length false", !l.EqualsDeep(r)); Check("equals both MoveNext despite lhs false", r.Last.Moves == 1);
        Events("equals no Current after unequal MoveNext", log, "l.get|r.get|l.move1|r.move1|r.dispose|l.dispose");
        log.Clear(); l = new Trace<object>(log, "l", 7, 8); r = new Trace<object>(log, "r", 7);
        Check("equals longer lhs false", !l.EqualsDeep(r)); Check("equals left extra movement retained", l.Last.Moves == 2 && r.Last.Moves == 2);
        Check("equals nested arrays true", new object[] { new int[] { 1, 2 }, null }.EqualsDeep(new object[] { new int[] { 1, 2 }, null }));
        Check("equals nested arrays difference false", !new object[] { new int[] { 1, 2 } }.EqualsDeep(new object[] { new int[] { 1, 3 } }));
        Check("equals enumerable vs scalar false", !new object[] { new int[] { 1 } }.EqualsDeep(new object[] { 1 }));
        log.Clear(); var left = new EqualitySpy(log, "left", true); var right = new EqualitySpy(log, "right", false);
        Check("equals calls left Object.Equals", new object[] { left }.EqualsDeep(new object[] { right })); Events("equals receiver direction", log, "left.ObjectEquals");
        log.Clear(); Check("equals identical current bypass", new object[] { left }.EqualsDeep(new object[] { left })); Events("identical current no callback", log, "");
        log.Clear(); l = new Trace<object>(log, "l", 1); r = new Trace<object>(log, "r", 1); var fault = new Marker("get-r"); r.GetFault = fault;
        Throws("equals rhs acquisition exact fault", () => l.EqualsDeep(r), typeof(Marker), fault); Events("equals rhs acquisition leaves lhs undisposed", log, "l.get|r.get"); Check("equals leaked acquired lhs prefix", l.Last.Disposes == 0);
        log.Clear(); l = new Trace<object>(log, "l") { NullEnumerator = true }; r = new Trace<object>(log, "r", 1);
        Throws("equals null enumerator fault", () => l.EqualsDeep(r), typeof(NullReferenceException)); Events("equals null left disposes right", log, "l.get|r.get|r.dispose");
        log.Clear(); l = new Trace<object>(log, "l", 1); r = new Trace<object>(log, "r", 1); fault = new Marker("current-l"); l.CurrentFault = fault; l.CurrentFaultAt = 0;
        Throws("equals Current exact fault", () => l.EqualsDeep(r), typeof(Marker), fault); Events("equals Current fault prefix and cleanup", log, "l.get|r.get|l.move1|r.move1|l.current0|r.dispose|l.dispose");
        log.Clear(); l = new Trace<object>(log, "l", 1); r = new Trace<object>(log, "r", 1); fault = new Marker("move-r"); r.MoveFault = fault; r.MoveFaultAt = 1;
        Throws("equals right Move exact fault", () => l.EqualsDeep(r), typeof(Marker), fault); Events("equals right Move fault cleanup", log, "l.get|r.get|l.move1|r.move1|r.dispose|l.dispose");
        log.Clear(); l = new Trace<object>(log, "l"); r = new Trace<object>(log, "r"); fault = new Marker("dispose-r"); r.DisposeFault = fault;
        Throws("equals inner disposal exact fault", () => l.EqualsDeep(r), typeof(Marker), fault); Events("equals outer disposal after inner fault", log, "l.get|r.get|l.move1|r.move1|r.dispose|l.dispose");
        log.Clear(); l = new Trace<object>(log, "l"); r = new Trace<object>(log, "r"); fault = new Marker("dispose-l"); l.DisposeFault = fault; r.DisposeFault = new Marker("dispose-r");
        Throws("equals outer disposal replaces inner fault", () => l.EqualsDeep(r), typeof(Marker), fault);
    }
    void Hashing()
    {
        Check("hash empty fixed541", new object[0].GetHashCodeDeep() == 541);
        Check("hash null item fixed214777", new object[] { null }.GetHashCodeDeep() == 214777);
        Check("hash zero fixed214776", new object[] { new HashSpy(0) }.GetHashCodeDeep() == 214776);
        Check("hash ordered values fixed85267257", new int[] { 1, 2 }.GetHashCodeDeep() == 85267257);
        Check("hash reversed fixed85266870", new int[] { 2, 1 }.GetHashCodeDeep() == 85266870);
        Check("hash unchecked overflow fixed", new object[] { new HashSpy(int.MaxValue) }.GetHashCodeDeep() == -2147268871);
        Check("hash nested recursively matches", new object[] { new int[] { 1, 2 } }.GetHashCodeDeep() == new object[] { new HashSpy(85267257) }.GetHashCodeDeep());
        Throws("hash null collection faults", () => IEnumerableExtensions.GetHashCodeDeep(null), typeof(NullReferenceException));
        var log = new List<string>(); var c = new Trace<object>(log, "h", 1, 2);
        Check("hash trace fixed", c.GetHashCodeDeep() == 85267257); Events("hash iteration disposal order", log, "h.get|h.move1|h.current0|h.move2|h.current1|h.move3|h.dispose");
        log.Clear(); var f = new Marker("hash-current"); c = new Trace<object>(log, "h", 1) { CurrentFault = f, CurrentFaultAt = 0 };
        Throws("hash Current fault identity", () => c.GetHashCodeDeep(), typeof(Marker), f); Events("hash fault disposal", log, "h.get|h.move1|h.current0|h.dispose");
    }
    void Joining()
    {
        Check("join defaults null blank", new object[] { "a", null, 3 }.Join() == "a,,3");
        Check("join explicit separator substitute", new object[] { "a", null, "z" }.Join("|", "nil") == "a|nil|z");
        Check("join null separator", new object[] { "a", "z" }.Join(null) == "az");
        var log = new List<string>(); var sb = new StringBuilder("seed"); var c = new Trace<object>(log, "j");
        Check("join empty returns empty", c.Join("|", "nil", sb) == ""); Check("join empty retains builder seed", sb.ToString() == "seed"); Events("join empty disposal", log, "j.get|j.move1|j.dispose");
        log.Clear(); c = new Trace<object>(log, "j", "a", "b");
        Check("join reused builder result", c.Join("|", "nil", sb) == "a|b"); Check("join reused builder clears seed", sb.ToString() == "a|b"); Events("join current and separator order", log, "j.get|j.move1|j.current0|j.move2|j.current1|j.move3|j.dispose");
        log.Clear(); sb.Clear().Append("seed"); var f = new Marker("get"); c = new Trace<object>(log, "j") { GetFault = f };
        Throws("join acquisition fault identity", () => c.Join("|", "nil", sb), typeof(Marker), f); Check("join acquisition preserves builder", sb.ToString() == "seed"); Events("join acquisition no disposal", log, "j.get");
        log.Clear(); f = new Marker("first-move"); c = new Trace<object>(log, "j", "a") { MoveFault = f, MoveFaultAt = 1 };
        Throws("join first Move fault identity", () => c.Join("|", "nil", sb), typeof(Marker), f); Check("join first Move preserves builder", sb.ToString() == "seed"); Events("join first Move disposal", log, "j.get|j.move1|j.dispose");
        log.Clear(); f = new Marker("current"); c = new Trace<object>(log, "j", "a") { CurrentFault = f, CurrentFaultAt = 0 };
        Throws("join Current fault identity", () => c.Join("|", "nil", sb), typeof(Marker), f); Check("join Current fault cleared builder", sb.Length == 0); Events("join Current fault disposal", log, "j.get|j.move1|j.current0|j.dispose");
        log.Clear(); f = new Marker("text"); c = new Trace<object>(log, "j", "a", new TextSpy(log, "unused", f));
        Throws("join ToString fault identity", () => c.Join("|", "nil", sb), typeof(Marker), f); Check("join ToString fault partial separator", sb.ToString() == "a|"); Events("join ToString fault prefix", log, "j.get|j.move1|j.current0|j.move2|j.current1|text|j.dispose");
        log.Clear(); f = new Marker("second-move"); c = new Trace<object>(log, "j", "a") { MoveFault = f, MoveFaultAt = 2 };
        Throws("join second Move fault identity", () => c.Join("|", "nil", sb), typeof(Marker), f); Check("join second Move prefix before separator", sb.ToString() == "a");
        log.Clear(); Check("join nonnull ToString null no substitute", new object[] { new TextSpy(log, null) }.Join("|", "nil") == ""); Events("join ToString once", log, "text");
        log.Clear(); f = new Marker("dispose"); c = new Trace<object>(log, "j", "a") { DisposeFault = f };
        Throws("join Dispose replaces result", () => c.Join("|", "nil", sb), typeof(Marker), f); Check("join result builder set before Dispose fault", sb.ToString() == "a");
    }
    void Counting()
    {
        Check("count item int two", new int[] { 1, 2, 1 }.CountOccurrences(1) == 2);
        Check("count predicate two", new int[] { 1, 2, 3 }.CountOccurrences(v => v > 1) == 2);
        Check("count null item empty returns0", new string[0].CountOccurrences((string)null) == 0);
        Throws("count null captured item nonempty faults", () => new string[] { null }.CountOccurrences((string)null), typeof(NullReferenceException));
        Check("count null predicate empty returns0", new int[0].CountOccurrences((Func<int, bool>)null) == 0);
        var log = new List<string>(); var c = new Trace<int>(log, "c", 1);
        Throws("count null predicate nonempty fault", () => c.CountOccurrences((Func<int, bool>)null), typeof(NullReferenceException)); Events("count Current before null callback and Dispose", log, "c.get|c.move1|c.current0|c.dispose");
        Throws("count null collection faults", () => IEnumerableExtensions.CountOccurrences<int>(null, v => true), typeof(NullReferenceException));
        log.Clear(); var item = new EqualitySpy(log, "item", true); var element = new EqualitySpy(log, "element", false);
        Check("count captured Object.Equals distinct from IEquatable", new EqualitySpy[] { element }.CountOccurrences(item) == 1); Events("count captured receiver callback", log, "item.ObjectEquals");
        log.Clear(); var f = new Marker("predicate"); c = new Trace<int>(log, "c", 1, 2, 3); int callbacks = 0;
        Throws("count predicate exact fault", () => c.CountOccurrences(v => { log.Add("callback" + v); if (++callbacks == 2) throw f; return true; }), typeof(Marker), f); Check("count callback fault exact prefix", callbacks == 2); Events("count callback fault cleanup", log, "c.get|c.move1|c.current0|callback1|c.move2|c.current1|callback2|c.dispose");
        log.Clear(); c = new Trace<int>(log, "c", 1, 2); int nested = 0;
        Check("count callback reentry result", c.CountOccurrences(v => { nested += new int[] { v, v }.CountOccurrences(v); return true; }) == 2); Check("count callback reentry independent captures", nested == 4);
        log.Clear(); c = new Trace<int>(log, "c", 1, 2, 3);
        Check("count live array mutation", c.CountOccurrences(v => { if (v == 1) c.Items[1] = 9; return v == 9; }) == 1);
    }
    void Zipping()
    {
        var log = new List<string>(); var l = new Trace<int>(log, "l", 1, 2); var r = new Trace<string>(log, "r", "a"); var z = l.Zip(r); var e = z.GetEnumerator();
        Events("zip construction and GetEnumerator deferred", log, ""); Check("zip initial generic Current default", e.Current.Item1 == 0 && e.Current.Item2 == null);
        Throws("zip Reset unsupported", () => ((IEnumerator)e).Reset(), typeof(NotSupportedException)); Events("zip Reset no source acquisition", log, "");
        Check("zip first MoveNext true", e.MoveNext()); Check("zip first tuple", e.Current.Item1 == 1 && e.Current.Item2 == "a"); Check("zip nongeneric Current boxes tuple", ((ValueTuple<int, string>)((IEnumerator)e).Current).Item1 == 1);
        Events("zip first acquisition/move/current order", log, "l.get|r.get|l.move1|r.move1|l.current0|r.current0");
        Check("zip shorter right stops", !e.MoveNext()); Events("zip completion reverse disposal", log, "l.get|r.get|l.move1|r.move1|l.current0|r.current0|l.move2|r.move2|r.dispose|l.dispose");
        Check("zip Current retains last tuple after end", e.Current.Item1 == 1 && e.Current.Item2 == "a"); e.Dispose(); Check("zip repeated Dispose does not dispose again", l.Last.Disposes == 1 && r.Last.Disposes == 1);
        Check("zip repeated MoveNext stays false", !e.MoveNext());
        log.Clear(); l = new Trace<int>(log, "l"); r = new Trace<string>(log, "r", "a"); e = l.Zip(r).GetEnumerator(); Check("zip empty lhs stops", !e.MoveNext()); Check("zip right MoveNext short-circuited", r.Last.Moves == 0); Events("zip empty lhs still acquires right", log, "l.get|r.get|l.move1|r.dispose|l.dispose");
        log.Clear(); l = new Trace<int>(log, "l", 1); r = new Trace<string>(log, "r", "a"); e = l.Zip(r).GetEnumerator(); e.Dispose(); Events("zip pre-start Dispose no acquisition", log, ""); Check("zip pre-start Dispose retains startable state", e.MoveNext()); e.Dispose(); Events("zip abandonment cleanup", log, "l.get|r.get|l.move1|r.move1|l.current0|r.current0|r.dispose|l.dispose");
        log.Clear(); l = new Trace<int>(log, "l", 1); r = new Trace<string>(log, "r", "a"); var f = new Marker("get-r"); r.GetFault = f; e = l.Zip(r).GetEnumerator();
        Throws("zip rhs acquisition exact fault", () => e.MoveNext(), typeof(Marker), f); Events("zip rhs acquisition disposes lhs", log, "l.get|r.get|l.dispose"); Check("zip fault iterator stopped", !e.MoveNext());
        log.Clear(); l = new Trace<int>(log, "l", 1); r = new Trace<string>(log, "r", "a"); f = new Marker("current-r"); r.CurrentFault = f; r.CurrentFaultAt = 0; e = l.Zip(r).GetEnumerator();
        Throws("zip rhs Current exact fault", () => e.MoveNext(), typeof(Marker), f); Events("zip Current fault reverse cleanup", log, "l.get|r.get|l.move1|r.move1|l.current0|r.current0|r.dispose|l.dispose");
        log.Clear(); l = new Trace<int>(log, "l", 1); r = new Trace<string>(log, "r", "a"); f = new Marker("dispose-r"); r.DisposeFault = f; e = l.Zip(r).GetEnumerator(); e.MoveNext();
        Throws("zip inner Dispose exact fault", () => e.Dispose(), typeof(Marker), f); Check("zip outer Dispose after inner fault", l.Last.Disposes == 1); Check("zip Dispose fault stopped state", !e.MoveNext());
        log.Clear(); l = new Trace<int>(log, "l", 1); r = new Trace<string>(log, "r", "a"); f = new Marker("dispose-l"); l.DisposeFault = f; r.DisposeFault = new Marker("dispose-r"); e = l.Zip(r).GetEnumerator(); e.MoveNext();
        Throws("zip outer Dispose replaces inner fault", () => e.Dispose(), typeof(Marker), f);
        log.Clear(); r = new Trace<string>(log, "r", "a"); e = IEnumerableExtensions.Zip<int, string>(null, r).GetEnumerator(); Events("zip null lhs deferred", log, ""); Throws("zip null lhs Move fault", () => e.MoveNext(), typeof(NullReferenceException)); Events("zip null lhs prevents rhs acquisition", log, "");
        log.Clear(); l = new Trace<int>(log, "l", 1); e = l.Zip((IEnumerable<string>)null).GetEnumerator(); Throws("zip null rhs Move fault", () => e.MoveNext(), typeof(NullReferenceException)); Events("zip null rhs cleanup lhs", log, "l.get|l.dispose");
    }
    static int Verify(Action<OriginalEnumerablePreservationVerification> action, int expectedChecks)
    {
        var owned = new OriginalEnumerablePreservationVerification();
        action(owned);
        if (owned.checks != expectedChecks)
            throw new InvalidOperationException("Enumerable check count " + owned.checks + " expected " + expectedChecks);
        if (owned.failures != 0)
            throw new InvalidOperationException("Enumerable preservation checks failed: " + string.Join("; ", owned.failedChecks));
        return owned.checks;
    }

    public static int VerifyDeepEquality() => Verify(owned => owned.Equality(), 32);
    public static int VerifyDeepHash() => Verify(owned => owned.Hashing(), 12);
    public static int VerifyJoin() => Verify(owned => owned.Joining(), 27);
    public static int VerifyCount() => Verify(owned => owned.Counting(), 16);
    public static int VerifyZip() => Verify(owned => owned.Zipping(), 33);
}
}
