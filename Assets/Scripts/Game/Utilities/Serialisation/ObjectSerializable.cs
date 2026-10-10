using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game02000a24: all four declarations, the original class constraint,
    // and its sole serialized Unity object field. Ordinary class values become null.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ObjectSerializable<T> where T : class
    {
        [SerializeField] private UnityEngine.Object m_value;
        public ObjectSerializable() { }
        public ObjectSerializable(T value) { m_value = value as UnityEngine.Object; }
        public T Value
        {
            get { return m_value as T; }
            set { m_value = value as UnityEngine.Object; }
        }
    }
}
