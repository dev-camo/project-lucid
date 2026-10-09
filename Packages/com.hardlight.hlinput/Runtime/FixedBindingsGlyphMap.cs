using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // HLInput.Runtime 0x02000080: complete local one-constructor provider;
    // the original BaseGlyphMap provider is a genuine dependency, never a shell.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/GameInputGlyphMaps/Create FixedBindingsGlyphMap")]
    public class FixedBindingsGlyphMap : BaseGlyphMap<FixedBindingsGlyphMap.FixedBindingsData>
    {
        // 0x02000081: complete original serializable ten-boolean value record;
        // it has no locally declared native methods or extra behavioral contract.
        [Serializable]
        public struct FixedBindingsData
        {
            public bool BindToPointer;
            public bool BindToScrollWheel;
            public bool BindToTap;
            public bool BindToDoubleTap;
            public bool BindToTouch;
            public bool BindToTouchRelease;
            public bool BindToMultiTouch;
            public bool BindToMultiTouchRelease;
            public bool BindToHold;
            public bool BindToHoldPressure;
        }

        [SerializeField]
        [Tooltip("Populate with GameInputBindings to then dynamically fill used FixedBinding values.")]
        private List<GameInputBinding> m_populateDataFromBindings = new List<GameInputBinding>();

        // 0x06000262: list initialization precedes the original generic base ctor.
        public FixedBindingsGlyphMap() { }
    }
}
