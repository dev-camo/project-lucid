using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a03. The dump shows only fully shared instantiations;
    // the parameters and the struct, IConvertible constraint come from the metadata.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnumValuePair<TEnum, TValue> where TEnum : struct, IConvertible
    {
        [SerializeField] private TEnum m_key;
        [SerializeField] private TValue m_value;

        // 0x06003976/0x06003977
        public TEnum Key => m_key;
        public TValue Value => m_value;
    }
}
