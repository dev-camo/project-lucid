using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original HLInput.Runtime 0x02000036, all six owner methods and four
    // private static readonly fields. Inline initializers preserve BeforeFieldInit.
    // Every mapping below comes from independently matched ARM/x86 native Adds,
    // original StringLiteral contexts, and exact original RVA field bytes.
    public static class InputUtilities
    {
        private static readonly Dictionary<GameInput, Dictionary<int, Dictionary<string, string>>>
            s_indexedGameInputMappedAxisLookup =
                new Dictionary<GameInput, Dictionary<int, Dictionary<string, string>>>(
                    Enum.GetValues(typeof(GameInput)).Length, HardlightEnumComparers.GameInputComparer);

        private static readonly Dictionary<KeyCode, KeyCode[]> s_indexedJoystickLookup =
            new Dictionary<KeyCode, KeyCode[]>(HardlightInputEnumComparers.KeyCodeComparer)
            {
                { (KeyCode)330, new KeyCode[] { (KeyCode)350, (KeyCode)370, (KeyCode)390, (KeyCode)410, (KeyCode)430, (KeyCode)450, (KeyCode)470, (KeyCode)490 } },
                { (KeyCode)331, new KeyCode[] { (KeyCode)351, (KeyCode)371, (KeyCode)391, (KeyCode)411, (KeyCode)431, (KeyCode)451, (KeyCode)471, (KeyCode)491 } },
                { (KeyCode)332, new KeyCode[] { (KeyCode)352, (KeyCode)372, (KeyCode)392, (KeyCode)412, (KeyCode)432, (KeyCode)452, (KeyCode)472, (KeyCode)492 } },
                { (KeyCode)333, new KeyCode[] { (KeyCode)353, (KeyCode)373, (KeyCode)393, (KeyCode)413, (KeyCode)433, (KeyCode)453, (KeyCode)473, (KeyCode)493 } },
                { (KeyCode)334, new KeyCode[] { (KeyCode)354, (KeyCode)374, (KeyCode)394, (KeyCode)414, (KeyCode)434, (KeyCode)454, (KeyCode)474, (KeyCode)494 } },
                { (KeyCode)335, new KeyCode[] { (KeyCode)355, (KeyCode)375, (KeyCode)395, (KeyCode)415, (KeyCode)435, (KeyCode)455, (KeyCode)475, (KeyCode)495 } },
                { (KeyCode)336, new KeyCode[] { (KeyCode)356, (KeyCode)376, (KeyCode)396, (KeyCode)416, (KeyCode)436, (KeyCode)456, (KeyCode)476, (KeyCode)496 } },
                { (KeyCode)337, new KeyCode[] { (KeyCode)357, (KeyCode)377, (KeyCode)397, (KeyCode)417, (KeyCode)437, (KeyCode)457, (KeyCode)477, (KeyCode)497 } },
                { (KeyCode)338, new KeyCode[] { (KeyCode)358, (KeyCode)378, (KeyCode)398, (KeyCode)418, (KeyCode)438, (KeyCode)458, (KeyCode)478, (KeyCode)498 } },
                { (KeyCode)339, new KeyCode[] { (KeyCode)359, (KeyCode)379, (KeyCode)399, (KeyCode)419, (KeyCode)439, (KeyCode)459, (KeyCode)479, (KeyCode)499 } },
                { (KeyCode)340, new KeyCode[] { (KeyCode)360, (KeyCode)380, (KeyCode)400, (KeyCode)420, (KeyCode)430, (KeyCode)460, (KeyCode)480, (KeyCode)500 } },
                { (KeyCode)341, new KeyCode[] { (KeyCode)361, (KeyCode)381, (KeyCode)401, (KeyCode)421, (KeyCode)431, (KeyCode)461, (KeyCode)481, (KeyCode)501 } },
                { (KeyCode)342, new KeyCode[] { (KeyCode)362, (KeyCode)382, (KeyCode)402, (KeyCode)422, (KeyCode)432, (KeyCode)462, (KeyCode)482, (KeyCode)502 } },
                { (KeyCode)343, new KeyCode[] { (KeyCode)363, (KeyCode)383, (KeyCode)403, (KeyCode)423, (KeyCode)433, (KeyCode)463, (KeyCode)483, (KeyCode)503 } },
                { (KeyCode)344, new KeyCode[] { (KeyCode)364, (KeyCode)384, (KeyCode)404, (KeyCode)424, (KeyCode)434, (KeyCode)464, (KeyCode)484, (KeyCode)504 } },
                { (KeyCode)345, new KeyCode[] { (KeyCode)365, (KeyCode)385, (KeyCode)405, (KeyCode)425, (KeyCode)435, (KeyCode)465, (KeyCode)485, (KeyCode)505 } },
                { (KeyCode)346, new KeyCode[] { (KeyCode)366, (KeyCode)386, (KeyCode)406, (KeyCode)426, (KeyCode)436, (KeyCode)466, (KeyCode)486, (KeyCode)506 } },
                { (KeyCode)347, new KeyCode[] { (KeyCode)367, (KeyCode)387, (KeyCode)407, (KeyCode)427, (KeyCode)437, (KeyCode)467, (KeyCode)487, (KeyCode)507 } },
                { (KeyCode)348, new KeyCode[] { (KeyCode)368, (KeyCode)388, (KeyCode)408, (KeyCode)428, (KeyCode)438, (KeyCode)468, (KeyCode)488, (KeyCode)508 } },
                { (KeyCode)349, new KeyCode[] { (KeyCode)369, (KeyCode)389, (KeyCode)409, (KeyCode)429, (KeyCode)439, (KeyCode)469, (KeyCode)489, (KeyCode)509 } },
            };

        private static readonly Dictionary<string, string[]> s_indexedAxisLookup =
            new Dictionary<string, string[]>
            {
                { "Axis 1", new string[] { "Joystick 1 Axis 1", "Joystick 2 Axis 1", "Joystick 3 Axis 1", "Joystick 4 Axis 1", "Joystick 5 Axis 1", "Joystick 6 Axis 1", "Joystick 7 Axis 1", "Joystick 8 Axis 1" } },
                { "Axis 2", new string[] { "Joystick 1 Axis 2", "Joystick 2 Axis 2", "Joystick 3 Axis 2", "Joystick 4 Axis 2", "Joystick 5 Axis 2", "Joystick 6 Axis 2", "Joystick 7 Axis 2", "Joystick 8 Axis 2" } },
                { "Axis 3", new string[] { "Joystick 1 Axis 3", "Joystick 2 Axis 3", "Joystick 3 Axis 3", "Joystick 4 Axis 3", "Joystick 5 Axis 3", "Joystick 6 Axis 3", "Joystick 7 Axis 3", "Joystick 8 Axis 3" } },
                { "Axis 4", new string[] { "Joystick 1 Axis 4", "Joystick 2 Axis 4", "Joystick 3 Axis 4", "Joystick 4 Axis 4", "Joystick 5 Axis 4", "Joystick 6 Axis 4", "Joystick 7 Axis 4", "Joystick 8 Axis 4" } },
                { "Axis 5", new string[] { "Joystick 1 Axis 5", "Joystick 2 Axis 5", "Joystick 3 Axis 5", "Joystick 4 Axis 5", "Joystick 5 Axis 5", "Joystick 6 Axis 5", "Joystick 7 Axis 5", "Joystick 8 Axis 5" } },
                { "Axis 6", new string[] { "Joystick 1 Axis 6", "Joystick 2 Axis 6", "Joystick 3 Axis 6", "Joystick 4 Axis 6", "Joystick 5 Axis 6", "Joystick 6 Axis 6", "Joystick 7 Axis 6", "Joystick 8 Axis 6" } },
                { "Axis 7", new string[] { "Joystick 1 Axis 7", "Joystick 2 Axis 7", "Joystick 3 Axis 7", "Joystick 4 Axis 7", "Joystick 5 Axis 7", "Joystick 6 Axis 7", "Joystick 7 Axis 7", "Joystick 8 Axis 7" } },
                { "Axis 8", new string[] { "Joystick 1 Axis 8", "Joystick 2 Axis 8", "Joystick 3 Axis 8", "Joystick 4 Axis 8", "Joystick 5 Axis 8", "Joystick 6 Axis 8", "Joystick 7 Axis 8", "Joystick 8 Axis 8" } },
                { "Axis 9", new string[] { "Joystick 1 Axis 9", "Joystick 2 Axis 9", "Joystick 3 Axis 9", "Joystick 4 Axis 9", "Joystick 5 Axis 9", "Joystick 6 Axis 9", "Joystick 7 Axis 9", "Joystick 8 Axis 9" } },
                { "Axis 10", new string[] { "Joystick 1 Axis 10", "Joystick 2 Axis 10", "Joystick 3 Axis 10", "Joystick 4 Axis 10", "Joystick 5 Axis 10", "Joystick 6 Axis 10", "Joystick 7 Axis 10", "Joystick 8 Axis 10" } },
                { "Axis 11", new string[] { "Joystick 1 Axis 11", "Joystick 2 Axis 11", "Joystick 3 Axis 11", "Joystick 4 Axis 11", "Joystick 5 Axis 11", "Joystick 6 Axis 11", "Joystick 7 Axis 11", "Joystick 8 Axis 11" } },
                { "Axis 12", new string[] { "Joystick 1 Axis 12", "Joystick 2 Axis 12", "Joystick 3 Axis 12", "Joystick 4 Axis 12", "Joystick 5 Axis 12", "Joystick 6 Axis 12", "Joystick 7 Axis 12", "Joystick 8 Axis 12" } },
                { "Axis 13", new string[] { "Joystick 1 Axis 13", "Joystick 2 Axis 13", "Joystick 3 Axis 13", "Joystick 4 Axis 13", "Joystick 5 Axis 13", "Joystick 6 Axis 13", "Joystick 7 Axis 13", "Joystick 8 Axis 13" } },
                { "Axis 14", new string[] { "Joystick 1 Axis 14", "Joystick 2 Axis 14", "Joystick 3 Axis 14", "Joystick 4 Axis 14", "Joystick 5 Axis 14", "Joystick 6 Axis 14", "Joystick 7 Axis 14", "Joystick 8 Axis 14" } },
                { "Axis 15", new string[] { "Joystick 1 Axis 15", "Joystick 2 Axis 15", "Joystick 3 Axis 15", "Joystick 4 Axis 15", "Joystick 5 Axis 15", "Joystick 6 Axis 15", "Joystick 7 Axis 15", "Joystick 8 Axis 15" } },
                { "Axis 16", new string[] { "Joystick 1 Axis 16", "Joystick 2 Axis 16", "Joystick 3 Axis 16", "Joystick 4 Axis 16", "Joystick 5 Axis 16", "Joystick 6 Axis 16", "Joystick 7 Axis 16", "Joystick 8 Axis 16" } },
                { "Axis 17", new string[] { "Joystick 1 Axis 17", "Joystick 2 Axis 17", "Joystick 3 Axis 17", "Joystick 4 Axis 17", "Joystick 5 Axis 17", "Joystick 6 Axis 17", "Joystick 7 Axis 17", "Joystick 8 Axis 17" } },
                { "Axis 18", new string[] { "Joystick 1 Axis 18", "Joystick 2 Axis 18", "Joystick 3 Axis 18", "Joystick 4 Axis 18", "Joystick 5 Axis 18", "Joystick 6 Axis 18", "Joystick 7 Axis 18", "Joystick 8 Axis 18" } },
                { "Axis 19", new string[] { "Joystick 1 Axis 19", "Joystick 2 Axis 19", "Joystick 3 Axis 19", "Joystick 4 Axis 19", "Joystick 5 Axis 19", "Joystick 6 Axis 19", "Joystick 7 Axis 19", "Joystick 8 Axis 19" } },
                { "Axis 20", new string[] { "Joystick 1 Axis 20", "Joystick 2 Axis 20", "Joystick 3 Axis 20", "Joystick 4 Axis 20", "Joystick 5 Axis 20", "Joystick 6 Axis 20", "Joystick 7 Axis 20", "Joystick 8 Axis 20" } },
                { "Axis 21", new string[] { "Joystick 1 Axis 21", "Joystick 2 Axis 21", "Joystick 3 Axis 21", "Joystick 4 Axis 21", "Joystick 5 Axis 21", "Joystick 6 Axis 21", "Joystick 7 Axis 21", "Joystick 8 Axis 21" } },
                { "Axis 22", new string[] { "Joystick 1 Axis 22", "Joystick 2 Axis 22", "Joystick 3 Axis 22", "Joystick 4 Axis 22", "Joystick 5 Axis 22", "Joystick 6 Axis 22", "Joystick 7 Axis 22", "Joystick 8 Axis 22" } },
                { "Axis 23", new string[] { "Joystick 1 Axis 23", "Joystick 2 Axis 23", "Joystick 3 Axis 23", "Joystick 4 Axis 23", "Joystick 5 Axis 23", "Joystick 6 Axis 23", "Joystick 7 Axis 23", "Joystick 8 Axis 23" } },
                { "Axis 24", new string[] { "Joystick 1 Axis 24", "Joystick 2 Axis 24", "Joystick 3 Axis 24", "Joystick 4 Axis 24", "Joystick 5 Axis 24", "Joystick 6 Axis 24", "Joystick 7 Axis 24", "Joystick 8 Axis 24" } },
                { "Axis 25", new string[] { "Joystick 1 Axis 25", "Joystick 2 Axis 25", "Joystick 3 Axis 25", "Joystick 4 Axis 25", "Joystick 5 Axis 25", "Joystick 6 Axis 25", "Joystick 7 Axis 25", "Joystick 8 Axis 25" } },
                { "Axis 26", new string[] { "Joystick 1 Axis 26", "Joystick 2 Axis 26", "Joystick 3 Axis 26", "Joystick 4 Axis 26", "Joystick 5 Axis 26", "Joystick 6 Axis 26", "Joystick 7 Axis 26", "Joystick 8 Axis 26" } },
                { "Axis 27", new string[] { "Joystick 1 Axis 27", "Joystick 2 Axis 27", "Joystick 3 Axis 27", "Joystick 4 Axis 27", "Joystick 5 Axis 27", "Joystick 6 Axis 27", "Joystick 7 Axis 27", "Joystick 8 Axis 27" } },
                { "Axis 28", new string[] { "Joystick 1 Axis 28", "Joystick 2 Axis 28", "Joystick 3 Axis 28", "Joystick 4 Axis 28", "Joystick 5 Axis 28", "Joystick 6 Axis 28", "Joystick 7 Axis 28", "Joystick 8 Axis 28" } },
            };

        // 0x06000136: this is twenty entries times eight, not the array length.
        private static readonly int s_joystickIndexesCount = unchecked(s_indexedJoystickLookup.Count * 8);

        // 0x06000131: a negative index returns before any lookup. Unknown keys
        // and indexes outside a present array retain the caller's original key.
        public static KeyCode GetJoystickMappedKeyCode(KeyCode keyCode, int joystickIndex)
        {
            if (joystickIndex < 0)
                return keyCode;
            if (s_indexedJoystickLookup.TryGetValue(keyCode, out KeyCode[] keys) && keys.Length > joystickIndex)
                return keys[joystickIndex];
            return keyCode;
        }

        public static string GetTrackingKeyGameInputMappedAxis(GameInput gameInput, int joystickIndex,
            string mappedAxis) // 0x06000132: the comparer argument is exactly null.
        {
            return GetTrackingKey(gameInput, joystickIndex, mappedAxis,
                s_indexedGameInputMappedAxisLookup, s_joystickIndexesCount, null);
        }

        // 0x06000133: intermediate maps are installed before later faults;
        // cached null values are returned unchanged. String concatenation has no
        // separators and preserves axis, joystick index, then GameInput order.
        public static string GetTrackingKey<TAxis>(GameInput gameInput, int joystickIndex, TAxis axis,
            Dictionary<GameInput, Dictionary<int, Dictionary<TAxis, string>>> indexedGameInputAxisMap,
            int axisMapCount, IEqualityComparer<TAxis> equalityComparer)
        {
            if (!indexedGameInputAxisMap.TryGetValue(gameInput, out Dictionary<int, Dictionary<TAxis, string>> joystickMap))
            {
                joystickMap = new Dictionary<int, Dictionary<TAxis, string>>(8);
                indexedGameInputAxisMap.Add(gameInput, joystickMap);
            }
            if (!joystickMap.TryGetValue(joystickIndex, out Dictionary<TAxis, string> axisMap))
            {
                axisMap = equalityComparer != null
                    ? new Dictionary<TAxis, string>(axisMapCount, equalityComparer)
                    : new Dictionary<TAxis, string>(axisMapCount);
                joystickMap.Add(joystickIndex, axisMap);
            }
            if (!axisMap.TryGetValue(axis, out string trackingKey))
            {
                trackingKey = string.Concat(axis.ToString(), joystickIndex.ToString(), gameInput.ToString());
                axisMap.Add(axis, trackingKey);
            }
            return trackingKey;
        }

        public static bool IsMouseKeyCode(KeyCode keyCode) // 0x06000134
        {
            return unchecked((uint)((int)keyCode - 323)) < 7;
        }

        public static string GetJoystickMappedAxis(string axis, int joystickIndex) // 0x06000135
        {
            if (joystickIndex < 0)
                return axis;
            if (s_indexedAxisLookup.TryGetValue(axis, out string[] axes) && axes.Length > joystickIndex)
                return axes[joystickIndex];
            return axis;
        }
    }
}
