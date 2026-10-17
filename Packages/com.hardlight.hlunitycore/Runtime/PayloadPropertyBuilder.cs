using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020001ea; bodies translated from the 1.10.1
    // x86-64 binary (0x1b07160..0x1b07ff0, shared generics 0x9859e0..0x986140,
    // iterators 0xb256c0 and 0xb25e80). Reflects over the parent's IPayload<>
    // properties and feeds them reader output once the parent allows it.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PayloadPropertyBuilder
    {
        public delegate bool IsAllowedToUpdateDelegate();

        private struct PropertyData
        {
            public PropertyInfo m_propertyInfo;
            public MethodInfo m_methodUpdateData;
            public MethodInfo m_methodCollectionAdd;
            public MethodInfo m_methodCollectionClear;

            // 0x06000c85: a collection entry needs both reflected methods.
            public bool IsCollectionType()
            {
                return m_methodCollectionAdd != null && m_methodCollectionClear != null;
            }
        }

        private static readonly Type s_payloadType = typeof(IPayload<>);

        private const string CollectionAddMethodId = "Add";
        private const string CollectionClearMethodId = "Clear";
        private const string PayloadGetIdentifierMethodId = "GetIdentifier";
        private const string PayloadUpdateDataMethodId = "UpdateData";

        private readonly Dictionary<string, PropertyData> m_propertyReferences = new Dictionary<string, PropertyData>();
        private readonly Dictionary<Type, Coroutine> m_pendingUpdates = new Dictionary<Type, Coroutine>();
        private readonly IsAllowedToUpdateDelegate m_isAllowedToUpdateDelegate;
        private readonly object m_parent;

        // 0x06000c74: the parent is stored before the delegate, then the
        // references are generated immediately.
        public PayloadPropertyBuilder(object parent, IsAllowedToUpdateDelegate isAllowedToUpdateDelegate)
        {
            m_parent = parent;
            m_isAllowedToUpdateDelegate = isAllowedToUpdateDelegate;
            GeneratePropertyReferences();
        }

        // 0x06000c75.
        ~PayloadPropertyBuilder()
        {
            CleanupPendingUpdates();
        }

        // 0x06000c76: a handle is recorded only when RunCoroutine returns one.
        public void UpdateProperties<TInput, TOutput>(IEnumerable<TInput> payloadData, IPayloadReader<TInput, TOutput> payloadReader, Action onCompleted, bool clearCollectionTypes = true)
        {
            Type payloadType = payloadReader.GetPayloadType();
            CleanupPendingUpdate(payloadType);
            Coroutine coroutine = CoroutineUtils.RunCoroutine(UpdatePropertiesWhenReady(payloadData, payloadReader, onCompleted, clearCollectionTypes));
            if (coroutine != null)
            {
                m_pendingUpdates[payloadType] = coroutine;
            }
        }

        // 0x06000c77.
        public void UpdateProperty<TInput, TOutput>(TInput payloadData, IPayloadReader<TInput, TOutput> payloadReader, Action onCompleted, bool clearIfPropertyIsCollection = true)
        {
            Type payloadType = payloadReader.GetPayloadType();
            CleanupPendingUpdate(payloadType);
            Coroutine coroutine = CoroutineUtils.RunCoroutine(UpdatePropertyWhenReady(payloadData, payloadReader, onCompleted, clearIfPropertyIsCollection));
            if (coroutine != null)
            {
                m_pendingUpdates[payloadType] = coroutine;
            }
        }

        // 0x06000c78; MoveNext 0x06000c88: the gate is polled again after each
        // null yield. Entries are always written without clearing collections.
        private IEnumerator UpdatePropertiesWhenReady<TInput, TOutput>(IEnumerable<TInput> payloadData, IPayloadReader<TInput, TOutput> payloadReader, Action onCompleted, bool clearCollectionTypes = true)
        {
            while (!m_isAllowedToUpdateDelegate())
            {
                yield return null;
            }

            Type payloadType = payloadReader.GetPayloadType();
            if (clearCollectionTypes)
            {
                ClearCollectionsOfType(payloadType);
            }

            foreach (TInput input in payloadData)
            {
                if (payloadReader.GetKeyValueFromData(input, out string identifier, out TOutput output))
                {
                    UpdateProperty(identifier, output);
                }
            }

            m_pendingUpdates.Remove(payloadType);
            onCompleted?.Invoke();
        }

        // 0x06000c79; MoveNext 0x06000c8e: the payload type is read again for
        // the pending removal, after the update.
        private IEnumerator UpdatePropertyWhenReady<TInput, TOutput>(TInput payloadData, IPayloadReader<TInput, TOutput> payloadReader, Action onCompleted, bool clearIfPropertyIsCollection = true)
        {
            while (!m_isAllowedToUpdateDelegate())
            {
                yield return null;
            }

            if (payloadReader.GetKeyValueFromData(payloadData, out string identifier, out TOutput output))
            {
                UpdateProperty(identifier, output, clearIfPropertyIsCollection);
            }

            m_pendingUpdates.Remove(payloadReader.GetPayloadType());
            onCompleted?.Invoke();
        }

        // 0x06000c7a: unknown identifiers are ignored. A collection receives a
        // fresh element, added before that element is given the value.
        private void UpdateProperty<T>(string identifier, T value, bool clearCollection = false)
        {
            if (m_propertyReferences.ContainsKey(identifier))
            {
                PropertyData propertyData = m_propertyReferences[identifier];
                bool isCollection = propertyData.IsCollectionType();
                object payload = propertyData.m_propertyInfo.GetValue(m_parent);
                if (isCollection)
                {
                    object collection = payload;
                    if (clearCollection)
                    {
                        propertyData.m_methodCollectionClear.Invoke(collection, null);
                    }
                    payload = Activator.CreateInstance(propertyData.m_propertyInfo.PropertyType.GenericTypeArguments[0]);
                    propertyData.m_methodCollectionAdd.Invoke(collection, new object[] { payload });
                }
                propertyData.m_methodUpdateData.Invoke(payload, new object[] { value });
            }
        }

        // 0x06000c7b: walks each collection element type up its base chain,
        // comparing open generic definitions, and clears on the first match.
        private void ClearCollectionsOfType(Type typeToClear)
        {
            foreach (KeyValuePair<string, PropertyData> propertyReference in m_propertyReferences)
            {
                // The shipped body re-reads the entry by key (get_Item 0x1b07bbe)
                // rather than using the pair's value.
                PropertyData propertyData = m_propertyReferences[propertyReference.Key];
                if (!propertyData.IsCollectionType())
                {
                    continue;
                }

                Type type = propertyData.m_propertyInfo.PropertyType.GenericTypeArguments[0];
                while (type != null)
                {
                    Type comparedType = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                    if (comparedType == typeToClear)
                    {
                        propertyData.m_methodCollectionClear.Invoke(propertyData.m_propertyInfo.GetValue(m_parent), null);
                        break;
                    }
                    type = type.BaseType;
                }
            }
        }

        // 0x06000c7c: collections are recognised by GetInterface("ICollection")
        // (simple-name match in any namespace, so ICollection`1 alone does not
        // qualify). Missing property values are created and assigned; a
        // collection's identifier comes from a throwaway element instance.
        private void GeneratePropertyReferences()
        {
            m_propertyReferences.Clear();
            PropertyInfo[] properties = m_parent.GetType().GetProperties();
            foreach (PropertyInfo propertyInfo in properties)
            {
                Type propertyType = propertyInfo.PropertyType;
                bool isCollection = propertyType.GetInterface("ICollection") != null;
                Type payloadType = isCollection ? propertyType.GenericTypeArguments[0] : propertyType;
                if (!DoesTypeInheritFromIPayload(payloadType))
                {
                    continue;
                }

                object payload = propertyInfo.GetValue(m_parent);
                if (payload == null)
                {
                    payload = Activator.CreateInstance(propertyType);
                    propertyInfo.SetValue(m_parent, payload);
                }

                MethodInfo methodCollectionAdd = null;
                MethodInfo methodCollectionClear = null;
                if (isCollection)
                {
                    methodCollectionAdd = propertyType.GetMethod(CollectionAddMethodId);
                    methodCollectionClear = propertyType.GetMethod(CollectionClearMethodId);
                    payload = Activator.CreateInstance(payloadType);
                }

                MethodInfo methodGetIdentifier = payloadType.GetMethod(PayloadGetIdentifierMethodId);
                MethodInfo methodUpdateData = payloadType.GetMethod(PayloadUpdateDataMethodId);
                string identifier = (string)methodGetIdentifier.Invoke(payload, null);
                if (!m_propertyReferences.ContainsKey(identifier))
                {
                    m_propertyReferences[identifier] = new PropertyData
                    {
                        m_propertyInfo = propertyInfo,
                        m_methodUpdateData = methodUpdateData,
                        m_methodCollectionAdd = methodCollectionAdd,
                        m_methodCollectionClear = methodCollectionClear
                    };
                }
            }
        }

        // 0x06000c7d: the type itself is never compared, only its interfaces.
        private bool DoesTypeInheritFromIPayload(Type type)
        {
            foreach (Type interfaceType in type.GetInterfaces())
            {
                Type comparedType = interfaceType.IsGenericType ? interfaceType.GetGenericTypeDefinition() : interfaceType;
                if (comparedType == s_payloadType)
                {
                    return true;
                }
            }
            return false;
        }

        // 0x06000c7e: stops a local copy; the dictionary entry is kept.
        private void CleanupPendingUpdate(Type payloadType)
        {
            if (m_pendingUpdates.ContainsKey(payloadType))
            {
                Coroutine coroutine = m_pendingUpdates[payloadType];
                CoroutineUtils.StopUtilCoroutine(ref coroutine);
            }
        }

        // 0x06000c7f.
        private void CleanupPendingUpdates()
        {
            foreach (KeyValuePair<Type, Coroutine> pendingUpdate in m_pendingUpdates)
            {
                Coroutine coroutine = pendingUpdate.Value;
                CoroutineUtils.StopUtilCoroutine(ref coroutine);
            }
            m_pendingUpdates.Clear();
        }
    }
}
