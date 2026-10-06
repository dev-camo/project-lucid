using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Complete original HLInput.Runtime Int32 enum; literals add no body credit.
    public enum InputType
    {
        DefaultController = 897719803,
        Keyboard = -1162288346,
        MfiController = -594121215,
        Mouse = -1482781790,
        NpadController = -1000566503,
        PS4Controller = -1901056579,
        PS5Controller = 1329525373,
        Remote = 1437681611,
        Touch = -31649603,
        Unknown = 1838982690,
        Unsupported = 1208867107,
        XboneController = -1984142849,
    }

    // Complete original HLInput.Runtime Int32 enum; literals add no body credit.
    public enum SwipeScreenZone
    {
        None = 0,
        Custom = -92066078,
        Left = -1290930909,
        Right = -1134475173,
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputTypeEqualityComparer : IEqualityComparer<InputType>
    {
        // Original HLInput.Runtime 0x06000128..12a: signed Int32 bits,
        // equality and raw hash; natural Object base-only constructor.
        public bool Equals(InputType a, InputType b) { return a == b; }
        public int GetHashCode(InputType a) { return (int)a; }
    }

    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class KeyCodeEqualityComparer : IEqualityComparer<KeyCode>
    {
        // Original HLInput.Runtime 0x0600012e..130: signed Int32 bits,
        // equality and raw hash; natural Object base-only constructor.
        public bool Equals(KeyCode a, KeyCode b) { return a == b; }
        public int GetHashCode(KeyCode a) { return (int)a; }
    }

    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SwipeScreenZoneEqualityComparer : IEqualityComparer<SwipeScreenZone>
    {
        // Original HLInput.Runtime 0x06000277..279: signed Int32 bits,
        // equality and raw hash; natural Object base-only constructor.
        public bool Equals(SwipeScreenZone a, SwipeScreenZone b) { return a == b; }
        public int GetHashCode(SwipeScreenZone a) { return (int)a; }
    }

    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HardlightInputEnumComparers
    {
        // HLInput.Runtime 0x0600012b: readonly initializers preserve the
        // original BeforeFieldInit flag and allocation/store ordering.
        public static readonly InputTypeEqualityComparer InputTypeComparer = new InputTypeEqualityComparer();
        public static readonly KeyCodeEqualityComparer KeyCodeComparer = new KeyCodeEqualityComparer();
        public static readonly SwipeScreenZoneEqualityComparer SwipeScreenZoneComparer = new SwipeScreenZoneEqualityComparer();
    }
}
