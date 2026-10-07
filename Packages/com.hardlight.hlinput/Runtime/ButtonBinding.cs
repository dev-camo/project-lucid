using System;
using System.Collections.Generic;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ButtonBinding : BaseBinding, IKeyProvider<KeyCode>
    {
        [HashEnum(typeof(KeyCode)), SerializeField] private KeyCode m_keyCode;
        // HLInput.Runtime 06000045: return the exact signed enum bits without normalization.
        public KeyCode Key { get { return m_keyCode; } }
        // 06000046: genuine BaseBinding list initialization precedes the key store.
        public ButtonBinding(KeyCode keyCode, List<InputModifier> modifiers) : base(modifiers) { m_keyCode = keyCode; }
    }
}
