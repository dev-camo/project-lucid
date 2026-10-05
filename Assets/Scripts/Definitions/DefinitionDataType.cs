using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class DefinitionDataType<TKey, TData> : DefinitionDataType where TData : ScriptableObject
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public new class DefinitionElement<T> : DefinitionElement, ISerializationCallbackReceiver
        {
            [SerializeField, HideInInspector]
            public string Name;
            [SerializeField]
            public TData Data;

            // Game.Runtime 0x06001c49: native tailcall to GenerateName.
            public void OnBeforeSerialize() => GenerateName();
            // Game.Runtime 0x06001c4a: original Unity null/destroyed comparison and exact native literal.
            private void GenerateName() => Name = Data == null ? "NONE" : Data.name;
            // Game.Runtime 0x06001c4b: genuine player RET body.
            public void OnAfterDeserialize() { }
            // Game.Runtime 0x06001c4c: genuine element base, store Data, then GenerateName.
            public DefinitionElement(TData value) { Data = value; GenerateName(); }
        }

        [SerializeField]
        public DefinitionElement<TData>[] m_elements;

        // Game.Runtime 0x06001c45: comparer callback precedes dictionary creation and array access;
        // each element key is generated before reading its Data again for dictionary insertion.
        public Dictionary<TKey, TData> GetData()
        {
            var result = new Dictionary<TKey, TData>(GetKeyComparer());
            foreach (var element in m_elements)
                result.Add(GetElementKey(element.Data), element.Data);
            return result;
        }

        // Game.Runtime 0x06001c46/0x06001c47: original protected abstract contracts; no invented implementations.
        protected abstract TKey GetElementKey(TData data);
        protected abstract IEqualityComparer<TKey> GetKeyComparer();
        // Game.Runtime 0x06001c48: original base-only constructor, leaves m_elements null.
        protected DefinitionDataType() { }
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class DefinitionDataType : ScriptableObject
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class DefinitionElement
        {
            // Game.Runtime 0x06001c4e: System.Object base only.
            public DefinitionElement() { }
        }
        // Game.Runtime 0x06001c4d: ScriptableObject base only.
        protected DefinitionDataType() { }
    }
}
