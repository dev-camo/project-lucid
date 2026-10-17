// Original HLUnityUI.Runtime Hardlight.HLRecyclingScrollRectElement`1 020000ea,
// reconstructed from the fully shared x86-64 bodies (0x1393150..0x1393270) and metadata.
// No original type derives from it; the exported script has no asset references.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class HLRecyclingScrollRectElement<T> : MonoBehaviour
    {
        [SerializeField]
        private RectTransform m_rectTransform;
        // Set once per closed type by the static constructor (0x06000683).
        public static Type DataType = typeof(T);

        public RectTransform RectTransform => m_rectTransform;

        // Called with the entry's data whenever the element is (re)assigned to an entry.
        public abstract void Initialise(T data);

        // Called on every element each time PositionChanged runs with data (scroll value
        // changes and RefreshView).
        public virtual void OnScroll()
        {
        }
    }
}
