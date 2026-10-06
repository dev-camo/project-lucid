using System;
using UnityEngine;

namespace Hardlight.Utils
{
    // Original HLUnityCore.Runtime 0x020002ae. Index operations deliberately retain
    // Int32 shift wrapping; IsAllSet is exact equality, whereas IsSet tests containment.
    [Serializable]
    public struct BitFlags32
    {
        [SerializeField] private int m_flags;

        public static int AllSelectedNonFlags<T>()
        {
            if (!typeof(T).IsEnum) throw new ArgumentException("T must be an enumerated type");
            int selected = 0;
            foreach (T value in Enum.GetValues(typeof(T)))
                selected |= 1 << Convert.ToInt32(Enum.Parse(typeof(T), value.ToString()) as Enum);
            return selected;
        }

        public static int AllSelected<T>()
        {
            if (!typeof(T).IsEnum) throw new ArgumentException("T must be an enumerated type");
            int selected = 0;
            foreach (T value in Enum.GetValues(typeof(T)))
                selected |= Convert.ToInt32(Enum.Parse(typeof(T), value.ToString()) as Enum);
            return selected;
        }

        public static int SelectFlag<T>(T val) { return 1 << Convert.ToInt32(val); }
        public static implicit operator BitFlags32(int val) { return new BitFlags32 { m_flags = val }; }
        public bool IsSet(int mask) { return (m_flags & mask) == mask; }
        public bool IsAnySet(int mask) { return (m_flags & mask) != 0; }
        public bool IsAnySet() { return m_flags != 0; }
        public bool IsAllSet(int flags) { return m_flags == flags; }
        public void Set(int mask, bool val) { m_flags = val ? m_flags | mask : m_flags & ~mask; }
        public bool IsIndexSet(int index) { return (m_flags & (1 << index)) != 0; }
        public void SetIndex(int index, bool val) { Set(1 << index, val); }
        public void Clear() { m_flags = 0; }
        public int ToInt() { return m_flags; }
    }
}
