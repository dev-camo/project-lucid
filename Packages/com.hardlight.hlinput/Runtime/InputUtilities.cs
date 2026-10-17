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
                { KeyCode.JoystickButton0, new KeyCode[] { KeyCode.Joystick1Button0, KeyCode.Joystick2Button0, KeyCode.Joystick3Button0, KeyCode.Joystick4Button0, KeyCode.Joystick5Button0, KeyCode.Joystick6Button0, KeyCode.Joystick7Button0, KeyCode.Joystick8Button0 } },
                { KeyCode.JoystickButton1, new KeyCode[] { KeyCode.Joystick1Button1, KeyCode.Joystick2Button1, KeyCode.Joystick3Button1, KeyCode.Joystick4Button1, KeyCode.Joystick5Button1, KeyCode.Joystick6Button1, KeyCode.Joystick7Button1, KeyCode.Joystick8Button1 } },
                { KeyCode.JoystickButton2, new KeyCode[] { KeyCode.Joystick1Button2, KeyCode.Joystick2Button2, KeyCode.Joystick3Button2, KeyCode.Joystick4Button2, KeyCode.Joystick5Button2, KeyCode.Joystick6Button2, KeyCode.Joystick7Button2, KeyCode.Joystick8Button2 } },
                { KeyCode.JoystickButton3, new KeyCode[] { KeyCode.Joystick1Button3, KeyCode.Joystick2Button3, KeyCode.Joystick3Button3, KeyCode.Joystick4Button3, KeyCode.Joystick5Button3, KeyCode.Joystick6Button3, KeyCode.Joystick7Button3, KeyCode.Joystick8Button3 } },
                { KeyCode.JoystickButton4, new KeyCode[] { KeyCode.Joystick1Button4, KeyCode.Joystick2Button4, KeyCode.Joystick3Button4, KeyCode.Joystick4Button4, KeyCode.Joystick5Button4, KeyCode.Joystick6Button4, KeyCode.Joystick7Button4, KeyCode.Joystick8Button4 } },
                { KeyCode.JoystickButton5, new KeyCode[] { KeyCode.Joystick1Button5, KeyCode.Joystick2Button5, KeyCode.Joystick3Button5, KeyCode.Joystick4Button5, KeyCode.Joystick5Button5, KeyCode.Joystick6Button5, KeyCode.Joystick7Button5, KeyCode.Joystick8Button5 } },
                { KeyCode.JoystickButton6, new KeyCode[] { KeyCode.Joystick1Button6, KeyCode.Joystick2Button6, KeyCode.Joystick3Button6, KeyCode.Joystick4Button6, KeyCode.Joystick5Button6, KeyCode.Joystick6Button6, KeyCode.Joystick7Button6, KeyCode.Joystick8Button6 } },
                { KeyCode.JoystickButton7, new KeyCode[] { KeyCode.Joystick1Button7, KeyCode.Joystick2Button7, KeyCode.Joystick3Button7, KeyCode.Joystick4Button7, KeyCode.Joystick5Button7, KeyCode.Joystick6Button7, KeyCode.Joystick7Button7, KeyCode.Joystick8Button7 } },
                { KeyCode.JoystickButton8, new KeyCode[] { KeyCode.Joystick1Button8, KeyCode.Joystick2Button8, KeyCode.Joystick3Button8, KeyCode.Joystick4Button8, KeyCode.Joystick5Button8, KeyCode.Joystick6Button8, KeyCode.Joystick7Button8, KeyCode.Joystick8Button8 } },
                { KeyCode.JoystickButton9, new KeyCode[] { KeyCode.Joystick1Button9, KeyCode.Joystick2Button9, KeyCode.Joystick3Button9, KeyCode.Joystick4Button9, KeyCode.Joystick5Button9, KeyCode.Joystick6Button9, KeyCode.Joystick7Button9, KeyCode.Joystick8Button9 } },
                // Original quirk: rows 10-19 map joystick 5 to Joystick5Button0-9, not 10-19.
                { KeyCode.JoystickButton10, new KeyCode[] { KeyCode.Joystick1Button10, KeyCode.Joystick2Button10, KeyCode.Joystick3Button10, KeyCode.Joystick4Button10, KeyCode.Joystick5Button0, KeyCode.Joystick6Button10, KeyCode.Joystick7Button10, KeyCode.Joystick8Button10 } },
                { KeyCode.JoystickButton11, new KeyCode[] { KeyCode.Joystick1Button11, KeyCode.Joystick2Button11, KeyCode.Joystick3Button11, KeyCode.Joystick4Button11, KeyCode.Joystick5Button1, KeyCode.Joystick6Button11, KeyCode.Joystick7Button11, KeyCode.Joystick8Button11 } },
                { KeyCode.JoystickButton12, new KeyCode[] { KeyCode.Joystick1Button12, KeyCode.Joystick2Button12, KeyCode.Joystick3Button12, KeyCode.Joystick4Button12, KeyCode.Joystick5Button2, KeyCode.Joystick6Button12, KeyCode.Joystick7Button12, KeyCode.Joystick8Button12 } },
                { KeyCode.JoystickButton13, new KeyCode[] { KeyCode.Joystick1Button13, KeyCode.Joystick2Button13, KeyCode.Joystick3Button13, KeyCode.Joystick4Button13, KeyCode.Joystick5Button3, KeyCode.Joystick6Button13, KeyCode.Joystick7Button13, KeyCode.Joystick8Button13 } },
                { KeyCode.JoystickButton14, new KeyCode[] { KeyCode.Joystick1Button14, KeyCode.Joystick2Button14, KeyCode.Joystick3Button14, KeyCode.Joystick4Button14, KeyCode.Joystick5Button4, KeyCode.Joystick6Button14, KeyCode.Joystick7Button14, KeyCode.Joystick8Button14 } },
                { KeyCode.JoystickButton15, new KeyCode[] { KeyCode.Joystick1Button15, KeyCode.Joystick2Button15, KeyCode.Joystick3Button15, KeyCode.Joystick4Button15, KeyCode.Joystick5Button5, KeyCode.Joystick6Button15, KeyCode.Joystick7Button15, KeyCode.Joystick8Button15 } },
                { KeyCode.JoystickButton16, new KeyCode[] { KeyCode.Joystick1Button16, KeyCode.Joystick2Button16, KeyCode.Joystick3Button16, KeyCode.Joystick4Button16, KeyCode.Joystick5Button6, KeyCode.Joystick6Button16, KeyCode.Joystick7Button16, KeyCode.Joystick8Button16 } },
                { KeyCode.JoystickButton17, new KeyCode[] { KeyCode.Joystick1Button17, KeyCode.Joystick2Button17, KeyCode.Joystick3Button17, KeyCode.Joystick4Button17, KeyCode.Joystick5Button7, KeyCode.Joystick6Button17, KeyCode.Joystick7Button17, KeyCode.Joystick8Button17 } },
                { KeyCode.JoystickButton18, new KeyCode[] { KeyCode.Joystick1Button18, KeyCode.Joystick2Button18, KeyCode.Joystick3Button18, KeyCode.Joystick4Button18, KeyCode.Joystick5Button8, KeyCode.Joystick6Button18, KeyCode.Joystick7Button18, KeyCode.Joystick8Button18 } },
                { KeyCode.JoystickButton19, new KeyCode[] { KeyCode.Joystick1Button19, KeyCode.Joystick2Button19, KeyCode.Joystick3Button19, KeyCode.Joystick4Button19, KeyCode.Joystick5Button9, KeyCode.Joystick6Button19, KeyCode.Joystick7Button19, KeyCode.Joystick8Button19 } },
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
