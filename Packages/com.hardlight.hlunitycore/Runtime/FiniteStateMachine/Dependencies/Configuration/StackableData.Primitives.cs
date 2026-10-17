using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000025. All outer fields and methods live in
    // StackableData.cs; this file holds only the original nested types.
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
    }

}
