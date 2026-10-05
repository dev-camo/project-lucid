using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000025. Main control methods/instance fields
    // live in the coordinated partial; this file preserves original nested types.
    public partial class StackableData
    {
        public enum RetrievalOperation
        {
            Base = 0, Latest = 1, LogicalAnd = 2, LogicalOr = 3, Multiply = 4, Addition = 5
        }

        public enum OperationAction { Continue = 0, EarlyExit = 1 }

        public struct ResultCarrier<T>
        {
            // Original runtime-managed delegate 0x0600012b..0x0600012e.
            public delegate OperationAction Operation(StackableDataContainer<T> stackableDataContainer,
                ref ResultCarrier<T> result);

            public bool HasValue;
            // Original get/set 0x06000127/128; native specialized loads/stores of T.
            public T Value { get; private set; }

            // 0x06000129. Constructor sets presence before copying the supplied value.
            public ResultCarrier(T value)
            {
                HasValue = true;
                Value = value;
            }

            // 0x0600012a. Unlike default(ResultCarrier<T>), even null/default values
            // supplied through SetValue are present. Copy value before setting presence.
            public void SetValue(T value)
            {
                Value = value;
                HasValue = true;
            }
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class StackableCachedData
        {
            public RetrievalOperation RetrievalOperation;
            public bool IsDirty;

            // 0x0600012f; ARM64 0x1aa3090. IsDirty keeps CLR initialization false.
            public StackableCachedData(RetrievalOperation retrievalOperation)
            {
                RetrievalOperation = retrievalOperation;
            }
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class StackableCachedData<T> : StackableCachedData
        {
            public T Value;
            // 0x06000130; native specializations call base then copy T.
            public StackableCachedData(T value, RetrievalOperation retrievalOperation)
                : base(retrievalOperation) { Value = value; }
            // 0x06000131. A value write does not clear the dirty flag.
            public void Set(T value) { Value = value; }
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class StackableDataContainer
        {
            // 0x06000132; native creates dictionary then container then Add.
            public static Dictionary<int, StackableDataContainer> ToDictionary<T>(int id, T value)
            {
                var dictionary = new Dictionary<int, StackableDataContainer>();
                dictionary.Add(id, new StackableDataContainer<T>(value));
                return dictionary;
            }

            // 0x06000133; clear first, preserve matching typed container identity,
            // otherwise replace an absent/null/differently typed entry through indexer.
            public static Dictionary<int, StackableDataContainer> ToDictionary<T>(int id, T value,
                Dictionary<int, StackableDataContainer> dictionary, bool clearExisting = false)
            {
                if (clearExisting) dictionary.Clear();
                if (dictionary.TryGetValue(id, out var entry) && entry is StackableDataContainer<T> typed)
                    typed.Set(value);
                else
                    dictionary[id] = new StackableDataContainer<T>(value);
                return dictionary;
            }

            // 0x06000134; native uses two safe type checks and TryGetValue.
            public static StackableDataContainer<T> GetFromDictionary<T>(int id, object orderedDictionaryEntry)
            {
                if (orderedDictionaryEntry is Dictionary<int, StackableDataContainer> dictionary &&
                    dictionary.TryGetValue(id, out var entry)) return entry as StackableDataContainer<T>;
                return null;
            }

            // 0x06000135; ARM64 0x1aa30bc forwards only to System.Object's ctor.
            public StackableDataContainer() { }
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class StackableDataContainer<T> : StackableDataContainer
        {
            public T Value;
            // 0x06000136; native base ctor then value copy.
            public StackableDataContainer(T value) { Value = value; }
            // 0x06000137; native value copy, including reference write barrier.
            public void Set(T value) { Value = value; }
        }

        private static readonly Dictionary<Type, Delegate> LogicalAnds;
        private static readonly Dictionary<Type, Delegate> LogicalOrs;
        private static readonly Dictionary<Type, Delegate> Multiplies;
        private static readonly Dictionary<Type, Delegate> Additions;

        // Original add/remove 0x06000104/105, ARM64 0x1aa22c8/0x1aa237c.
        // The compiler supplies the original Delegate.Combine/Remove CAS loop and
        // the exact original private field name OnDataUpdated (0x0400005f).
        public event Action<int> OnDataUpdated;

        // 0x06000102; ARM64 0x1aa1c84. Original explicit static constructor:
        // allocate all four maps first, then register bool,int,float,long,Vector3.
        static StackableData()
        {
            LogicalAnds = new Dictionary<Type, Delegate>();
            LogicalOrs = new Dictionary<Type, Delegate>();
            Multiplies = new Dictionary<Type, Delegate>();
            Additions = new Dictionary<Type, Delegate>();
            RegisterTypeOperations<bool>(BoolLogicalAnd, BoolLogicalOr);
            RegisterTypeOperations<int>(multiply: IntMultiply, addition: IntAddition);
            RegisterTypeOperations<float>(multiply: FloatMultiply, addition: FloatAddition);
            RegisterTypeOperations<long>(multiply: LongMultiply, addition: LongAddition);
            RegisterTypeOperations<Vector3>(multiply: Vector3Multiply, addition: Vector3Addition);
        }

        // 0x0600011b; ARM64 0x1aa2a84. A present false value skips container access.
        private static OperationAction BoolLogicalAnd(StackableDataContainer<bool> stackableDataContainer,
            ref ResultCarrier<bool> result)
        {
            result.SetValue((!result.HasValue || result.Value) && stackableDataContainer.Value);
            return result.Value ? OperationAction.Continue : OperationAction.EarlyExit;
        }

        // 0x0600011c; ARM64 0x1aa2b1c. Original OR always returns Continue.
        private static OperationAction BoolLogicalOr(StackableDataContainer<bool> stackableDataContainer,
            ref ResultCarrier<bool> result)
        {
            result.SetValue((result.HasValue && result.Value) || stackableDataContainer.Value);
            return OperationAction.Continue;
        }

        // 0x0600011d; ARM64 0x1aa2bac. Int32 wrapping multiplication, stop at zero.
        private static OperationAction IntMultiply(StackableDataContainer<int> stackableDataContainer,
            ref ResultCarrier<int> result)
        {
            result.SetValue(result.HasValue ? unchecked(stackableDataContainer.Value * result.Value) : stackableDataContainer.Value);
            return result.Value == 0 ? OperationAction.EarlyExit : OperationAction.Continue;
        }

        // 0x0600011e; ARM64 0x1aa2c3c. Addition always continues.
        private static OperationAction IntAddition(StackableDataContainer<int> stackableDataContainer,
            ref ResultCarrier<int> result)
        {
            result.SetValue(result.HasValue ? unchecked(stackableDataContainer.Value + result.Value) : stackableDataContainer.Value);
            return OperationAction.Continue;
        }

        // 0x0600011f; ARM64 0x1aa2cc4. Single-precision multiply and exact zero.
        private static OperationAction FloatMultiply(StackableDataContainer<float> stackableDataContainer,
            ref ResultCarrier<float> result)
        {
            result.SetValue(result.HasValue ? result.Value * stackableDataContainer.Value : stackableDataContainer.Value);
            return result.Value == 0f ? OperationAction.EarlyExit : OperationAction.Continue;
        }

        // 0x06000120; ARM64 0x1aa2d54. Single-precision addition, no early exit.
        private static OperationAction FloatAddition(StackableDataContainer<float> stackableDataContainer,
            ref ResultCarrier<float> result)
        {
            result.SetValue(result.HasValue ? result.Value + stackableDataContainer.Value : stackableDataContainer.Value);
            return OperationAction.Continue;
        }

        // 0x06000121; ARM64 0x1aa2ddc. Preserve full Int64 wrap and zero test.
        private static OperationAction LongMultiply(StackableDataContainer<long> stackableDataContainer,
            ref ResultCarrier<long> result)
        {
            result.SetValue(result.HasValue ? unchecked(stackableDataContainer.Value * result.Value) : stackableDataContainer.Value);
            return result.Value == 0L ? OperationAction.EarlyExit : OperationAction.Continue;
        }

        // 0x06000122; ARM64 0x1aa2e6c.
        private static OperationAction LongAddition(StackableDataContainer<long> stackableDataContainer,
            ref ResultCarrier<long> result)
        {
            result.SetValue(result.HasValue ? unchecked(stackableDataContainer.Value + result.Value) : stackableDataContainer.Value);
            return OperationAction.Continue;
        }

        // 0x06000123; ARM64 0x1aa2ef4. Component-wise multiplication followed by
        // Unity Vector3.operator== zero (squared-distance threshold), not exact zero.
        private static OperationAction Vector3Multiply(StackableDataContainer<Vector3> stackableDataContainer,
            ref ResultCarrier<Vector3> result)
        {
            result.SetValue(result.HasValue ? Vector3.Scale(stackableDataContainer.Value, result.Value) : stackableDataContainer.Value);
            return result.Value == Vector3.zero ? OperationAction.EarlyExit : OperationAction.Continue;
        }

        // 0x06000124; ARM64 0x1aa2ff4.
        private static OperationAction Vector3Addition(StackableDataContainer<Vector3> stackableDataContainer,
            ref ResultCarrier<Vector3> result)
        {
            result.SetValue(result.HasValue ? result.Value + stackableDataContainer.Value : stackableDataContainer.Value);
            return OperationAction.Continue;
        }

        // 0x06000125; ARM64 0x1aa26c0. Mark an existing cache dirty before reading
        // and invoking the event once; subscribers may change/read that same cache.
        private void DataUpdated(int id)
        {
            if (m_stackableCache.TryGetValue(id, out var cached)) cached.IsDirty = true;
            OnDataUpdated?.Invoke(id);
        }

        // 0x06000126; ARM64 specialized instances e.g. bool0x9aeec4.
        // Null callbacks leave previous registrations intact; supplied callbacks
        // replace their own type entry in AND,OR,multiply,addition order.
        public static void RegisterTypeOperations<T>(ResultCarrier<T>.Operation logicalAnd = null,
            ResultCarrier<T>.Operation logicalOr = null, ResultCarrier<T>.Operation multiply = null,
            ResultCarrier<T>.Operation addition = null)
        {
            if (logicalAnd != null) LogicalAnds[typeof(T)] = logicalAnd;
            if (logicalOr != null) LogicalOrs[typeof(T)] = logicalOr;
            if (multiply != null) Multiplies[typeof(T)] = multiply;
            if (addition != null) Additions[typeof(T)] = addition;
        }
    }

}
