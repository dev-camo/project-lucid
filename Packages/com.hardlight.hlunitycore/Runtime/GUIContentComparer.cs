using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200026e, methods 0x06000f6b..0x06000f6d.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GUIContentComparer : IComparer<GUIContent>
    {
        public static readonly GUIContentComparer Comparer = new GUIContentComparer();
        private readonly StringComparison m_stringComparison;

        public GUIContentComparer(StringComparison stringComparison = StringComparison.Ordinal)
        {
            m_stringComparison = stringComparison;
        }

        // 0x06000f6c: a null content compares as a null text.
        public int Compare(GUIContent content1, GUIContent content2)
        {
            return string.Compare(content1?.text, content2?.text, m_stringComparison);
        }
    }
}
