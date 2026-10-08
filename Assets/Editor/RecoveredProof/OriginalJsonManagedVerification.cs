using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Hardlight.JSON;
using Hardlight.Pooling;

namespace ProjectLucid.Verification
{
internal static class OriginalJsonManagedVerification
{
    private static int checks;
    private static void Check(bool condition, string detail)
    {
        ++checks;
        if (!condition) throw new Exception(detail);
    }
    private static void Equal(object expected, object actual, string detail)
    {
        Check(object.Equals(expected, actual), detail + ": " + actual);
    }
    private static void Throws<T>(Action action, string detail) where T : Exception
    {
        bool caught = false;
        try { action(); } catch (T) { caught = true; }
        Check(caught, detail);
    }
    private static void ThrowsIndexerFault(Action action)
    {
        bool caught = false;
        try { action(); }
        catch (TargetParameterCountException) { caught = true; }
        catch (ArgumentException) { caught = true; }
        // Installed Mono and NET use different reflection parameter faults.
        Check(caught, "unfiltered indexer getter faults before serialization");
    }
    private static object Call(string name, params object[] args)
    {
        MethodInfo method = typeof(JSONSerializer).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private delegate string ParseStringCall(char[] json, ref int index, ref bool success);

    public static int RunScalarConversions()
    {
        checks = 0;
        var number = new JSONLong { Value = 37 };
        var fraction = new JSONDouble { Value = 12.5 };
        var text = new JSONString { Value = "false" };
        var truth = new JSONBool { Value = true };
        float f; double d; int i; long l; bool b; string s;
        Check(JSONHelper.TryConvertToType(number, out f), "long to float succeeds"); Equal(37f, f, "long to float value");
        Check(JSONHelper.TryConvertToType(fraction, out d), "double succeeds"); Equal(12.5, d, "double value");
        Check(!JSONHelper.TryConvertToType(fraction, out i), "double is not integer"); Equal(0, i, "integer out initialized");
        Check(JSONHelper.TryConvertToType(number, out l), "long succeeds"); Equal(37L, l, "long value");
        Check(JSONHelper.TryConvertToType(text, out s), "string succeeds"); Equal("false", s, "string value");
        Check(JSONHelper.TryConvertToType(text, out b), "boolean string parsed"); Equal(false, b, "false string value");
        Check(JSONHelper.TryConvertToType(truth, out b), "bool succeeds"); Equal(true, b, "bool value");
        Check(!JSONHelper.TryConvertToType(null, out s), "null string incompatible"); Equal(null, s, "null string out");
        Check(!JSONHelper.TryConvertToType(text, out l), "string numeric rejected"); Equal(0L, l, "long out initialized");
        Equal(93, JSONHelper.ConvertToType(fraction, 93), "integer default retained");
        Equal(93f, JSONHelper.ConvertToType(text, 93f), "float default retained");
        Equal("fallback", JSONHelper.ConvertToType(number, "fallback"), "string default retained");
        Throws<FormatException>(() => JSONHelper.TryConvertToType(new JSONString { Value = "not-bool" }, out b), "bool parser faults propagate");
        Equal(150, JSONHelper.GetPoolSizeForType(typeof(JSONBool)), "bool pool size");
        Equal(100, JSONHelper.GetPoolSizeForType(typeof(JSONDouble)), "double pool size");
        Equal(200, JSONHelper.GetPoolSizeForType(typeof(JSONHashtable)), "table pool size");
        Equal(500, JSONHelper.GetPoolSizeForType(typeof(JSONLong)), "long pool size");
        Equal(1000, JSONHelper.GetPoolSizeForType(typeof(JSONString)), "string pool size");
        Equal(200, JSONHelper.GetPoolSizeForType(typeof(JSONArray)), "array pool size");
        Equal(100, JSONHelper.GetPoolSizeForType(typeof(object)), "fallback pool size");
        Equal(37, JSONHelper.GetBasicTypeFromJsonObject(number, typeof(int?)), "nullable unwrapped");
        Equal(Choice.Second, JSONHelper.GetBasicTypeFromJsonObject(new JSONString { Value = "Second" }, typeof(Choice)), "enum from string");
        Equal("true", JSONSerializer.Encode(truth), "bool exact encoding");
        Equal("37", JSONSerializer.Encode(number), "long exact encoding");
        Equal("12.5", JSONSerializer.Encode(fraction), "double exact encoding");
        return checks;
    }

    public static int RunParserAndCursor()
    {
        checks = 0;
        object[] args = { " true-tail".ToCharArray(), 0 };
        Equal(9, Call("LookAhead", args), "true prefix token"); Equal(0, args[1], "lookahead preserves cursor");
        Equal(9, Call("NextToken", args), "true token"); Equal(5, args[1], "prefix token consumes no delimiter");
        args = new object[] { "+1".ToCharArray(), 0 };
        Equal(0, Call("NextToken", args), "plus not initial numeric token"); Equal(0, args[1], "unknown token restores cursor");
        args = new object[] { "-5.4e+2tail".ToCharArray(), 0 };
        Equal(6, Call("GetLastIndexOfNumber", args), "number span includes exponent plus"); Equal(0, args[1], "number span does not consume");
        args = new object[] { "\"a\\qb\\/c\"".ToCharArray(), 0, true };
        Equal("ab/c", Call("ParseString", args), "unknown escape dropped and slash preserved"); Equal(true, args[2], "ordinary string success"); Equal(9, args[1], "string final cursor");
        args = new object[] { "\"\\u0041\"".ToCharArray(), 0, true };
        Equal("A", Call("ParseString", args), "unicode code point"); Equal(true, args[2], "unicode success");
        args = new object[] { "\"\\uZZZZ\"".ToCharArray(), 0, true };
        Equal("", Call("ParseString", args), "invalid hex empty string"); Equal(false, args[2], "invalid hex failure"); Equal(3, args[1], "invalid hex leaves digits unconsumed");
        args = new object[] { "\"\\uD800\"".ToCharArray(), 0, true };
        ParseStringCall parseString = JSONSerializer.ParseString;
        int faultIndex = 0; bool faultSuccess = true;
        Throws<ArgumentOutOfRangeException>(() => parseString((char[])args[0], ref faultIndex, ref faultSuccess), "surrogate code point throws"); Equal(true, faultSuccess, "hex success before surrogate fault"); Equal(3, faultIndex, "surrogate fault before digit advance");
        args = new object[] { "\"\\u12".ToCharArray(), 0, true };
        Equal(null, Call("ParseString", args), "short unicode null"); Equal(false, args[2], "short unicode failure");
        bool success = false;
        IJsonObject obj = JSONSerializer.Decode("truegarbage", ref success);
        Check(obj is JSONBool, "prefix literal accepted"); Equal(true, success, "trailing content not checked"); obj.Release();
        obj = JSONSerializer.Decode("1e-2", ref success);
        Check(obj is JSONString, "fractional exponent without decimal falls back to string"); Equal("1e-2", ((JSONString)obj).Value, "failed integer original lexeme"); Equal(true, success, "numeric fallback remains success"); obj.Release();
        obj = JSONSerializer.Decode("1.5", ref success);
        Check(obj is JSONDouble, "decimal selects double"); Equal(1.5, ((JSONDouble)obj).Value, "parsed double"); obj.Release();
        obj = JSONSerializer.Decode("[1,,null,]", ref success);
        Check(obj is JSONArray, "permissive array parsed"); Equal(2, ((JSONArray)obj).Count, "extra comma skipped"); Equal(null, ((JSONArray)obj)[1], "null retained");
        // Original array Release faults on null children after returning itself.
        Throws<NullReferenceException>(() => obj.Release(), "array null release fault preserved");
        obj = JSONSerializer.Decode("{\"key\" 7}", ref success);
        Equal(null, obj, "missing colon null result"); Equal(true, success, "missing colon does not mark failure");
        Throws<ArgumentException>(() => JSONSerializer.Decode("{\"a\":1,\"a\":2}"), "duplicate key fault preserved");
        return checks;
    }

    public static int RunSerializationOrder()
    {
        checks = 0;
        Equal("null", JSONSerializer.Encode(null), "null literal");
        Equal("\"a\\n\\t\\b\\f\\r\\\"\\\\/\\u00e9\\ud83d\\ude00\"", JSONSerializer.Encode("a\n\t\b\f\r\"\\/é😀"), "escaped UTF16 exact");
        Equal("[1, null, 2]", JSONSerializer.Encode(new object[] { 1, null, 2 }, options: JSONSerializer.EncodeOptions.SkipNull), "skip null does not filter array");
        Equal("NaN", JSONSerializer.Encode(double.NaN), "nonfinite double retained");
        Equal("Infinity", JSONSerializer.Encode(float.PositiveInfinity), "nonfinite float retained");
        DateTime time = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        Equal(time.ToString("o"), JSONSerializer.Encode(time), "date unquoted roundtrip string");
        Equal("\"Second\"", JSONSerializer.Encode(Choice.Second), "enum name quoted");
        var table = new Hashtable(); table.Add("one", 1);
        Equal("{\"one\":1}", JSONSerializer.Encode(table), "hashtable string keys");
        var dictionary = new Dictionary<int, string>(); dictionary.Add(2, "two");
        Equal("{\"TokenDictionaryKey\":\"TokenDictionaryValue\", 2:\"two\"}", JSONSerializer.Encode(dictionary), "dictionary marker and native key");
        var named = new Fields();
        Equal("{\"Value\":7, \"Empty\":null, \"Read\":9}", JSONSerializer.Encode(named), "field before property and NonSerialized omitted");
        Equal("{\"Value\":7, \"Read\":9}", JSONSerializer.Encode(named, options: JSONSerializer.EncodeOptions.SkipNull), "skip null fields");
        Equal("{\"Read\":9}", JSONSerializer.Encode(named, BindingFlags.NonPublic | BindingFlags.Instance), "properties ignore supplied field flags");
        ThrowsIndexerFault(() => JSONSerializer.Encode(new WithIndexer()));
        Throws<TargetInvocationException>(() => JSONSerializer.Encode(new ThrowingProperty()), "getter fault before serialization propagates");
        var builder = new StringBuilder();
        var failing = new ObserveJson(false);
        Equal(false, Call("SerializeValue", failing, builder, BindingFlags.Public | BindingFlags.Instance, JSONSerializer.EncodeOptions.None), "custom failure result");
        Equal("partial", builder.ToString(), "custom partial output retained"); Equal(1, failing.Encoded, "custom callback once");
        var sequence = new DisposeSequence(); builder.Clear();
        Equal(true, Call("SerializeArray", sequence, builder, BindingFlags.Public | BindingFlags.Instance, JSONSerializer.EncodeOptions.None), "array serialization success");
        Equal("[1]", builder.ToString(), "array output"); Equal("Current,Dispose", string.Join(",", sequence.Calls), "enumeration reads then disposes");
        sequence = new DisposeSequence { FailDispose = true }; builder.Clear();
        Throws<InvalidOperationException>(() => Call("SerializeArray", sequence, builder, BindingFlags.Public | BindingFlags.Instance, JSONSerializer.EncodeOptions.None), "array dispose fault propagates");
        Equal("[1", builder.ToString(), "array closing delimiter follows disposal");
        return checks;
    }

    public static int RunContainerOwnership()
    {
        checks = 0;
        JSONArray array = JSONArray.Create(new int[0]);
        var child = new ObserveJson(true);
        child.OnRelease = () => Equal(0, ObjectPool<JSONArray>.UsedObjectCount, "array returned before child release");
        array.Add(child); array.Release(); Equal(1, child.Released, "child released once");
        array = JSONArray.Create(new int[0]); Equal(0, array.Count, "reused array reset");
        int i; Throws<ArgumentOutOfRangeException>(() => array.TryGetIntFromIndex(-1, out i), "negative index throws");
        Check(!array.TryGetIntFromIndex(0, out i), "upper bound incompatible"); Equal(0, i, "upper bound out init"); array.Release();
        JSONHashtable table = JSONHashtable.Create(); var keys = table.Keys;
        table.Add("first", new JSONLong { Value = 1 }); Equal(1, keys.Count, "live table keys");
        IJsonObject prior = table.Get("first"); table.Add("first", 9L);
        Check(object.ReferenceEquals(prior, table.Get("first")), "typed Add reuses compatible original value"); Equal(9L, table.GetLong("first"), "typed Add updates value");
        table.Add("null", (IJsonObject)null); Throws<ArgumentException>(() => table.Add("null", 3L), "existing null typed Add duplicates");
        table.Release(); Equal(0, keys.Count, "table release clears live keys");
        JSONDictionary dict = JSONDictionary.Create(); var key = new ObserveJson(true); var value = new ObserveJson(true);
        dict.Add(key, value); var order = new List<string>(); key.OnRelease = () => order.Add("key"); value.OnRelease = () => order.Add("value");
        dict.Release(); Equal("key,value", string.Join(",", order), "dictionary key before value release");
        dict = JSONDictionary.Create(); key = new ObserveJson(true); dict.Add(key, key); dict.Release(); Equal(2, key.Released, "aliased key value released twice");
        return checks;
    }

    public enum Choice { First = 1, Second = 2 }
    public class Fields { public int Value = 7; public object Empty; [NonSerialized] public int Hidden = 99; public int Read { get { return 9; } } }
    public class WithIndexer { public int this[int index] { get { return index; } } }
    public class ThrowingProperty { public int Value { get { throw new InvalidOperationException(); } } }
    // Observation-only fixture input. It supplies no recovered provider API.
    private class ObserveJson : IJsonObject
    {
        private readonly bool result;
        public int Encoded, Released;
        public Action OnRelease;
        public ObserveJson(bool result) { this.result = result; }
        public IJsonObject Clone() { return this; }
        public bool Encode(StringBuilder builder) { ++Encoded; builder.Append("partial"); return result; }
        public void Release() { ++Released; if (OnRelease != null) OnRelease(); }
    }
    private class DisposeSequence : IEnumerable
    {
        public readonly List<string> Calls = new List<string>();
        public bool FailDispose;
        public IEnumerator GetEnumerator() { return new Enumerator(this); }
        private class Enumerator : IEnumerator, IDisposable
        {
            private readonly DisposeSequence owner; private int state;
            public Enumerator(DisposeSequence owner) { this.owner = owner; }
            public bool MoveNext() { return state++ == 0; }
            public object Current { get { owner.Calls.Add("Current"); return 1; } }
            public void Reset() { throw new NotSupportedException(); }
            public void Dispose() { owner.Calls.Add("Dispose"); if (owner.FailDispose) throw new InvalidOperationException(); }
        }
    }
}

}
