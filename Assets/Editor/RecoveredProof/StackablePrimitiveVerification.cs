using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Native-derived checks; run before a fixture adds custom operation registrations.
    public static class StackablePrimitiveVerification
    {
        private static int count;
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Static;
        private class BaseValue { }
        private sealed class DerivedValue : BaseValue { }
        private sealed class DerivedContainer : StackableData.StackableDataContainer<BaseValue>
        { public DerivedContainer(BaseValue v) : base(v) { } }
        private static void Check(bool value, string label)
        { if (!value) throw new Exception(label); count++; }
        private static StackableData.ResultCarrier<T>.Operation Op<T>(string method) =>
            (StackableData.ResultCarrier<T>.Operation)Delegate.CreateDelegate(
                typeof(StackableData.ResultCarrier<T>.Operation), typeof(StackableData).GetMethod(method, Priv));
        private static int Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
        private static void Numeric<T>(string name, T initial, bool present, T incoming, T expected,
            StackableData.OperationAction action)
        {
            var result = present ? new StackableData.ResultCarrier<T>(initial) : default(StackableData.ResultCarrier<T>);
            var a = Op<T>(name)(new StackableData.StackableDataContainer<T>(incoming), ref result);
            Check(a == action, name + " action");
            Check(result.HasValue, name + " presence");
            if (typeof(T) == typeof(float))
            {
                var value = (float)(object)result.Value; var wanted = (float)(object)expected;
                Check(float.IsNaN(wanted) ? float.IsNaN(value) : Bits(value) == Bits(wanted), name + " exact float");
            }
            else Check(EqualityComparer<T>.Default.Equals(result.Value, expected), name + " value");
        }
        public static void Run()
        {
            Debug.Log("Original StackableData primitive verified checks=" + RunManaged());
        }
        public static int RunManaged()
        {
            count = 0;
            RuntimeHelpers.RunClassConstructor(typeof(StackableData).TypeHandle);
            CarrierAndContainerChecks(); ArithmeticChecks(); RegistryChecks(); CallbackChecks();
            return count;
        }
        private static void CarrierAndContainerChecks()
        {
            var zero = default(StackableData.ResultCarrier<string>);
            Check(!zero.HasValue && zero.Value == null, "default carrier absent");
            var present = new StackableData.ResultCarrier<string>(null);
            Check(present.HasValue && present.Value == null, "null constructor present");
            zero.SetValue(null); Check(zero.HasValue && zero.Value == null, "null SetValue present");
            var integer = new StackableData.ResultCarrier<int>(0);
            Check(integer.HasValue && integer.Value == 0, "zero constructor present");
            integer.SetValue(int.MinValue); Check(integer.HasValue && integer.Value == int.MinValue, "value copy");
            var d = StackableData.StackableDataContainer.ToDictionary(7, "a"); var original = d[7];
            Check(d.Count == 1 && ((StackableData.StackableDataContainer<string>)original).Value == "a", "new dictionary");
            Check(ReferenceEquals(d, StackableData.StackableDataContainer.ToDictionary(7, "b", d)), "same dictionary");
            Check(ReferenceEquals(original, d[7]) && ((StackableData.StackableDataContainer<string>)original).Value == "b", "matching identity");
            d[8] = new StackableData.StackableDataContainer<int>(99);
            StackableData.StackableDataContainer.ToDictionary(7, 42, d);
            Check(!ReferenceEquals(original, d[7]) && d.Count == 2, "different type replaces only key");
            var beforeClear = d[7]; StackableData.StackableDataContainer.ToDictionary(7, 43, d, true);
            Check(d.Count == 1 && !ReferenceEquals(beforeClear, d[7]), "clear forces new container");
            d[7] = null; StackableData.StackableDataContainer.ToDictionary(7, 44, d);
            Check(((StackableData.StackableDataContainer<int>)d[7]).Value == 44, "null entry replaced");
            Check(StackableData.StackableDataContainer.GetFromDictionary<int>(7, d) == d[7], "typed dictionary lookup");
            Check(StackableData.StackableDataContainer.GetFromDictionary<long>(7, d) == null, "wrong type absent");
            Check(StackableData.StackableDataContainer.GetFromDictionary<int>(8, d) == null, "missing key absent");
            Check(StackableData.StackableDataContainer.GetFromDictionary<int>(7, null) == null, "null dictionary absent");
            Check(StackableData.StackableDataContainer.GetFromDictionary<int>(7, new object()) == null, "other object absent");
            d[7] = null; Check(StackableData.StackableDataContainer.GetFromDictionary<int>(7, d) == null, "null entry absent");
            d[7] = new StackableData.StackableDataContainer<DerivedValue>(new DerivedValue());
            Check(StackableData.StackableDataContainer.GetFromDictionary<BaseValue>(7, d) == null, "generic class invariant");
            var derivedContainer = new DerivedContainer(new DerivedValue()); d[7] = derivedContainer;
            Check(StackableData.StackableDataContainer.GetFromDictionary<BaseValue>(7, d) == derivedContainer, "actual subclass accepted");
            StackableData.StackableDataContainer.ToDictionary<BaseValue>(7, null, d);
            Check(ReferenceEquals(d[7], derivedContainer) && derivedContainer.Value == null, "subclass identity retained");
            bool threw = false;
            try { StackableData.StackableDataContainer.ToDictionary(7, 1, null); } catch (NullReferenceException) { threw = true; }
            Check(threw, "null supplied dictionary throws");
            var cachedType = typeof(StackableData).GetNestedType("StackableCachedData`1", BindingFlags.NonPublic).MakeGenericType(typeof(long));
            var cache = Activator.CreateInstance(cachedType, new object[] { long.MaxValue, (StackableData.RetrievalOperation)99 });
            var dirty = cachedType.GetField("IsDirty"); var value = cachedType.GetField("Value");
            Check(!(bool)dirty.GetValue(cache) && (long)value.GetValue(cache) == long.MaxValue, "cache initial false/preserves long");
            Check((int)(StackableData.RetrievalOperation)cachedType.GetField("RetrievalOperation").GetValue(cache) == 99, "invalid retrieval accepted");
            dirty.SetValue(cache, true); cachedType.GetMethod("Set").Invoke(cache, new object[] { long.MinValue });
            Check((bool)dirty.GetValue(cache) && (long)value.GetValue(cache) == long.MinValue, "cache Set retains dirty");
            Check(typeof(StackableData).GetField("OnDataUpdated", BindingFlags.NonPublic | BindingFlags.Instance) != null, "original event field name");
            Check(typeof(StackableData).GetField("m_OnDataUpdated", BindingFlags.NonPublic | BindingFlags.Instance) == null, "no stub field rename");
            Check((typeof(StackableData).Attributes & TypeAttributes.BeforeFieldInit) == 0, "original explicit cctor timing");
        }
        private static void ArithmeticChecks()
        {
            var and = Op<bool>("BoolLogicalAnd"); var or = Op<bool>("BoolLogicalOr");
            for (int present = 0; present < 2; present++) for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++)
            {
                var value = new StackableData.ResultCarrier<bool>(a != 0); value.HasValue = present != 0;
                var action = and(new StackableData.StackableDataContainer<bool>(b != 0), ref value);
                var expected = (present == 0 || a != 0) && b != 0;
                Check(value.HasValue && value.Value == expected, "AND value");
                Check(action == (expected ? StackableData.OperationAction.Continue : StackableData.OperationAction.EarlyExit), "AND stop");
                value = new StackableData.ResultCarrier<bool>(a != 0); value.HasValue = present != 0;
                action = or(new StackableData.StackableDataContainer<bool>(b != 0), ref value);
                Check(value.HasValue && value.Value == ((present != 0 && a != 0) || b != 0), "OR value");
                Check(action == StackableData.OperationAction.Continue, "OR always continues");
            }
            var falsity = new StackableData.ResultCarrier<bool>(false);
            Check(and(null, ref falsity) == StackableData.OperationAction.EarlyExit && !falsity.Value, "AND skips null after false");
            var truth = new StackableData.ResultCarrier<bool>(true);
            Check(or(null, ref truth) == StackableData.OperationAction.Continue && truth.Value, "OR skips null after true");
            Numeric("IntMultiply", int.MaxValue, true, 2, -2, StackableData.OperationAction.Continue);
            Numeric("IntMultiply", int.MinValue, true, 2, 0, StackableData.OperationAction.EarlyExit);
            Numeric("IntAddition", int.MaxValue, true, 1, int.MinValue, StackableData.OperationAction.Continue);
            Numeric("IntAddition", int.MinValue, true, -1, int.MaxValue, StackableData.OperationAction.Continue);
            Numeric("LongMultiply", long.MaxValue, true, 2L, -2L, StackableData.OperationAction.Continue);
            Numeric("LongMultiply", long.MinValue, true, 2L, 0L, StackableData.OperationAction.EarlyExit);
            Numeric("LongAddition", long.MaxValue, true, 1L, long.MinValue, StackableData.OperationAction.Continue);
            Numeric("LongAddition", long.MinValue, true, -1L, long.MaxValue, StackableData.OperationAction.Continue);
            Numeric("LongAddition", 0L, true, 0x123456789abcdefL, 0x123456789abcdefL, StackableData.OperationAction.Continue);
            Numeric("FloatMultiply", 1f, true, -0f, -0f, StackableData.OperationAction.EarlyExit);
            Numeric("FloatMultiply", float.PositiveInfinity, true, 0f, float.NaN, StackableData.OperationAction.Continue);
            Numeric("FloatMultiply", float.Epsilon, true, .5f, 0f, StackableData.OperationAction.EarlyExit);
            Numeric("FloatMultiply", float.NaN, true, 1f, float.NaN, StackableData.OperationAction.Continue);
            Numeric("FloatAddition", 16777216f, true, 1f, 16777216f, StackableData.OperationAction.Continue);
            Numeric("FloatAddition", float.PositiveInfinity, true, float.NegativeInfinity, float.NaN, StackableData.OperationAction.Continue);
            Numeric("IntMultiply", 99, false, 0, 0, StackableData.OperationAction.EarlyExit);
            Numeric("LongMultiply", 99L, false, long.MaxValue, long.MaxValue, StackableData.OperationAction.Continue);
            Numeric("FloatAddition", 99f, false, -0f, -0f, StackableData.OperationAction.Continue);
            var multiply = Op<Vector3>("Vector3Multiply"); var add = Op<Vector3>("Vector3Addition");
            var r = new StackableData.ResultCarrier<Vector3>(new Vector3(2, 3, 4));
            Check(multiply(new StackableData.StackableDataContainer<Vector3>(new Vector3(5, 6, 7)), ref r) == StackableData.OperationAction.Continue, "vector multiply continue");
            Check(r.HasValue && r.Value.Equals(new Vector3(10, 18, 28)), "vector component product");
            Check(add(new StackableData.StackableDataContainer<Vector3>(new Vector3(1, -2, 3)), ref r) == StackableData.OperationAction.Continue && r.Value.Equals(new Vector3(11, 16, 31)), "vector addition");
            foreach (float magnitude in new[] { 0f, 1e-6f, 1e-5f, 1e-4f, float.NaN, float.PositiveInfinity })
            {
                r = default(StackableData.ResultCarrier<Vector3>);
                var action = multiply(new StackableData.StackableDataContainer<Vector3>(new Vector3(magnitude, 0, 0)), ref r);
                var expected = magnitude * magnitude < BitConverter.ToSingle(BitConverter.GetBytes(0x2edbe6fe), 0);
                Check(action == (expected ? StackableData.OperationAction.EarlyExit : StackableData.OperationAction.Continue), "vector native squared threshold");
                Check(r.HasValue && (float.IsNaN(magnitude) ? float.IsNaN(r.Value.x) : Bits(r.Value.x) == Bits(magnitude)), "vector first-copy");
            }
        }
        private static void RegistryChecks()
        {
            string[] names = { "LogicalAnds", "LogicalOrs", "Multiplies", "Additions" };
            string[] methods = { "BoolLogicalAnd", "BoolLogicalOr", "IntMultiply", "IntAddition" };
            var maps = new Dictionary<Type, Delegate>[4];
            for (int i = 0; i < 4; i++) maps[i] = (Dictionary<Type, Delegate>)typeof(StackableData).GetField(names[i], Priv).GetValue(null);
            Check(maps[0].Count == 1 && maps[1].Count == 1 && maps[2].Count == 4 && maps[3].Count == 4, "exact cctor registration counts");
            Check(maps[0][typeof(bool)].Method.Name == methods[0] && maps[1][typeof(bool)].Method.Name == methods[1], "bool registry targets");
            Type[] arithmetic = { typeof(int), typeof(float), typeof(long), typeof(Vector3) };
            string[] prefixes = { "Int", "Float", "Long", "Vector3" };
            for (int i = 0; i < 4; i++)
            {
                Check(maps[2][arithmetic[i]].Method.Name == prefixes[i] + "Multiply", "multiply target");
                Check(maps[3][arithmetic[i]].Method.Name == prefixes[i] + "Addition", "addition target");
            }
            var prior = maps[2][typeof(int)];
            StackableData.RegisterTypeOperations<int>(); Check(ReferenceEquals(prior, maps[2][typeof(int)]), "null registration retains existing");
            StackableData.ResultCarrier<int>.Operation custom = delegate(StackableData.StackableDataContainer<int> c, ref StackableData.ResultCarrier<int> r) { r.SetValue(71); return StackableData.OperationAction.EarlyExit; };
            StackableData.RegisterTypeOperations<int>(multiply: custom);
            Check(ReferenceEquals(custom, maps[2][typeof(int)]) && maps[3][typeof(int)].Method.Name == "IntAddition", "only supplied registry replaced");
            var carrier = default(StackableData.ResultCarrier<int>); var registered = (StackableData.ResultCarrier<int>.Operation)maps[2][typeof(int)];
            Check(registered(null, ref carrier) == StackableData.OperationAction.EarlyExit && carrier.Value == 71, "custom registered behavior");
            maps[2][typeof(int)] = prior;
        }
        private static void CallbackChecks()
        {
            var obj = (StackableData)FormatterServices.GetUninitializedObject(typeof(StackableData));
            var cacheType = typeof(StackableData).GetNestedType("StackableCachedData`1", BindingFlags.NonPublic).MakeGenericType(typeof(int));
            var cache = Activator.CreateInstance(cacheType, new object[] { 8, StackableData.RetrievalOperation.Latest });
            var field = typeof(StackableData).GetField("m_stackableCache", BindingFlags.Instance | BindingFlags.NonPublic);
            var dictionary = (IDictionary)Activator.CreateInstance(field.FieldType); dictionary.Add(7, cache); field.SetValue(obj, dictionary);
            var call = typeof(StackableData).GetMethod("DataUpdated", BindingFlags.Instance | BindingFlags.NonPublic);
            var dirty = cacheType.GetField("IsDirty"); var calls = new List<string>();
            Action<int> second = id => calls.Add("second:" + id); Action<int> first = null;
            first = id => { Check((bool)dirty.GetValue(cache), "dirty precedes callback"); calls.Add("first:" + id); obj.OnDataUpdated -= second; };
            obj.OnDataUpdated += first; obj.OnDataUpdated += second;
            call.Invoke(obj, new object[] { 7 });
            Check(string.Join(",", calls) == "first:7,second:7", "event invocation snapshot/order");
            calls.Clear(); dirty.SetValue(cache, false); call.Invoke(obj, new object[] { 7 });
            Check(string.Join(",", calls) == "first:7", "removed callback absent next update");
            obj.OnDataUpdated -= first; calls.Clear(); obj.OnDataUpdated += second;
            call.Invoke(obj, new object[] { 88 }); Check(calls.Count == 1 && calls[0] == "second:88", "uncached id still invokes event");
            obj.OnDataUpdated -= second; call.Invoke(obj, new object[] { 88 }); Check(calls.Count == 1, "no subscriber no-op");
        }
    }
}
