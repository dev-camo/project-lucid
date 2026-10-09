// Project Lucid controlled verification of the original input lookup utilities.
using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    public static class InputUtilitiesVerification
    {
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        public static void VerifyOriginalMappings()
        {
            // These expected rows describe the original native initializer, including
            // its fifth-controller entries. Do not replace it with a uniform formula.
            for (int button = 0; button != 20; ++button)
            {
                var original = (KeyCode)(330 + button);
                for (int joystick = 0; joystick != 8; ++joystick)
                {
                    int expected = 350 + joystick * 20 + button;
                    if (joystick == 4 && button >= 10) expected -= 10;
                    Check(InputUtilities.GetJoystickMappedKeyCode(original, joystick) == (KeyCode)expected,
                        "Original joystick table row " + button + ", column " + joystick);
                }
                Check(InputUtilities.GetJoystickMappedKeyCode(original, -1) == original,
                    "Negative joystick indexes retain the input key.");
                Check(InputUtilities.GetJoystickMappedKeyCode(original, 8) == original,
                    "Indexes beyond an existing joystick row retain the input key.");
            }
            foreach (int code in new[] { int.MinValue, -1, 0, 323, 329, 350, 509, int.MaxValue })
                Check(InputUtilities.GetJoystickMappedKeyCode((KeyCode)code, 0) == (KeyCode)code,
                    "Unknown joystick keys are returned unchanged.");

            for (int axis = 1; axis <= 28; ++axis)
            {
                string original = "Axis " + axis;
                for (int joystick = 0; joystick != 8; ++joystick)
                    Check(InputUtilities.GetJoystickMappedAxis(original, joystick) ==
                        "Joystick " + (joystick + 1) + " Axis " + axis,
                        "Original axis table row " + axis + ", column " + joystick);
                Check(ReferenceEquals(InputUtilities.GetJoystickMappedAxis(original, -1), original),
                    "Negative indexes retain the exact original string.");
                Check(ReferenceEquals(InputUtilities.GetJoystickMappedAxis(original, 8), original),
                    "Out-of-range indexes retain the exact original string.");
            }
            string unknown = new string("Axis 29".ToCharArray());
            Check(ReferenceEquals(InputUtilities.GetJoystickMappedAxis(unknown, 0), unknown),
                "Unknown axes retain the caller's string object.");
            Check(InputUtilities.GetJoystickMappedAxis(null, -1) == null,
                "A negative index returns null before dictionary lookup.");
            Throws<ArgumentNullException>(() => InputUtilities.GetJoystickMappedAxis(null, 0),
                "A nonnegative null axis reaches the real dictionary key check.");

            for (int code = 320; code <= 332; ++code)
                Check(InputUtilities.IsMouseKeyCode((KeyCode)code) == (code >= 323 && code <= 329),
                    "Original unsigned mouse-key interval at " + code);
            Check(!InputUtilities.IsMouseKeyCode((KeyCode)int.MinValue) &&
                !InputUtilities.IsMouseKeyCode((KeyCode)int.MaxValue), "Mouse interval must not wrap.");

            Type owner = typeof(InputUtilities);
            Check(owner.IsAbstract && owner.IsSealed && owner.TypeInitializer != null,
                "InputUtilities must remain a static owner with its initializer.");
            Check((owner.Attributes & TypeAttributes.BeforeFieldInit) != 0,
                "Inline initializer semantics must retain BeforeFieldInit.");
            FieldInfo[] fields = owner.GetFields(StaticPrivate);
            Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            string[] names = { "s_indexedGameInputMappedAxisLookup", "s_indexedJoystickLookup",
                "s_indexedAxisLookup", "s_joystickIndexesCount" };
            Check(fields.Length == names.Length, "The original four static fields must be complete.");
            for (int i = 0; i != fields.Length; ++i)
                Check(fields[i].Name == names[i] && fields[i].IsPrivate && fields[i].IsStatic && fields[i].IsInitOnly,
                    "Original readonly field identity/order at " + i);
            Check((int)fields[3].GetValue(null) == 160,
                "The original cached count is twenty joystick rows times eight.");
            var tracking = (Dictionary<GameInput, Dictionary<int, Dictionary<string, string>>>)fields[0].GetValue(null);
            var keys = (Dictionary<KeyCode, KeyCode[]>)fields[1].GetValue(null);
            var axes = (Dictionary<string, string[]>)fields[2].GetValue(null);
            Check(ReferenceEquals(tracking.Comparer, HardlightEnumComparers.GameInputComparer),
                "The genuine GameInput comparer comes from HLAutoGenerated.");
            Check(ReferenceEquals(keys.Comparer, HardlightInputEnumComparers.KeyCodeComparer),
                "The genuine KeyCode comparer comes from HLInput.Runtime.");
            Check(keys.Count == 20 && axes.Count == 28, "Original initializer tables must be complete.");
        }

        public static void VerifyTrackingCacheAndFailures()
        {
            var game = (GameInput)123456789;
            var maps = NewMap<string>();
            string first = InputUtilities.GetTrackingKey(game, -12, "Horizontal", maps, 3, StringComparer.OrdinalIgnoreCase);
            Check(first == "Horizontal-12123456789", "Tracking strings have original order and no separators.");
            Check(maps.Count == 1 && maps[game].Count == 1 && maps[game][-12].Count == 1,
                "The original nested cache is populated at each level.");
            Check(ReferenceEquals(maps[game][-12].Comparer, StringComparer.OrdinalIgnoreCase),
                "A supplied axis comparer is used by the original inner dictionary.");
            Check(ReferenceEquals(first, InputUtilities.GetTrackingKey(game, -12, "HORIZONTAL", maps, -1, null)),
                "An existing axis cache ignores later capacity/comparer and returns the stored string object.");
            maps[game][-12]["null value"] = null;
            Check(InputUtilities.GetTrackingKey(game, -12, "NULL VALUE", maps, 0, null) == null,
                "A cached null value remains null without regeneration.");

            var failedCapacity = NewMap<string>();
            Throws<ArgumentOutOfRangeException>(() => InputUtilities.GetTrackingKey(game, 1, "axis", failedCapacity, -1, null),
                "Negative capacity reaches the genuine inner dictionary constructor.");
            Check(failedCapacity.Count == 1 && failedCapacity[game].Count == 0,
                "The joystick map is installed before the inner dictionary constructor fails.");
            var failedKey = NewMap<string>();
            Throws<ArgumentNullException>(() => InputUtilities.GetTrackingKey(game, 7, (string)null, failedKey, 2, null),
                "Null axis reaches the genuine inner dictionary lookup.");
            Check(failedKey.Count == 1 && failedKey[game].Count == 1 && failedKey[game][7].Count == 0,
                "Both outer cache levels survive a subsequent null-key fault.");
            Throws<NullReferenceException>(() => InputUtilities.GetTrackingKey(game, 0, "axis", (Dictionary<GameInput, Dictionary<int, Dictionary<string, string>>>)null, 1, null),
                "A null caller cache retains its managed receiver fault.");
            var nullJoystickMap = NewMap<string>(); nullJoystickMap.Add(game, null);
            Throws<NullReferenceException>(() => InputUtilities.GetTrackingKey(game, 0, "axis", nullJoystickMap, 1, null),
                "A stored null joystick map is not silently replaced.");
            Check(nullJoystickMap.Count == 1 && nullJoystickMap[game] == null, "Stored null joystick map remains intact.");
            var nullAxisMap = NewMap<string>();
            nullAxisMap.Add(game, new Dictionary<int, Dictionary<string, string>> { { 0, null } });
            Throws<NullReferenceException>(() => InputUtilities.GetTrackingKey(game, 0, "axis", nullAxisMap, 1, null),
                "A stored null axis map is not silently replaced.");
            Check(nullAxisMap[game][0] == null, "Stored null axis map remains intact.");

            var reentrant = NewMap<AxisProbe>();
            var axis = new AxisProbe();
            int callbackInvocations = 0;
            axis.OnString = () =>
            {
                ++callbackInvocations;
                Check(axis.StringCalls == 1 && reentrant[game][2].Count == 0, "Cache levels are visible during the first axis ToString callback.");
                // Real Dictionary duplicate-key diagnostics may stringify the key again.
                // Keep that BCL behavior separate from the original callback schedule.
                axis.OnString = null;
                reentrant[game][2].Add(axis, "inserted by callback");
            };
            Throws<ArgumentException>(() => InputUtilities.GetTrackingKey(game, 2, axis, reentrant, 1, null),
                "Reentrant insertion retains the original later Add collision.");
            Check(callbackInvocations == 1 && reentrant[game][2][axis] == "inserted by callback",
                "A failed outer Add retains the callback's value and one mutation callback.");
            int callsAfterFailure = axis.StringCalls;
            axis.OnString = () => { throw new InvalidOperationException("Cached entries must not stringify again."); };
            Check(InputUtilities.GetTrackingKey(game, 2, axis, reentrant, -1, null) == "inserted by callback" && axis.StringCalls == callsAfterFailure,
                "Cached custom keys return without another ToString callback.");

            var throwing = NewMap<AxisProbe>();
            var badAxis = new AxisProbe { OnString = () => { throw new AxisFailure(); } };
            Throws<AxisFailure>(() => InputUtilities.GetTrackingKey(game, 3, badAxis, throwing, 0, null),
                "Axis ToString exceptions propagate after cache publication.");
            Check(badAxis.StringCalls == 1 && throwing[game][3].Count == 0,
                "A ToString fault retains both empty cache levels and does not publish a string.");

            FieldInfo cacheField = typeof(InputUtilities).GetField("s_indexedGameInputMappedAxisLookup", StaticPrivate);
            var originalCache = (Dictionary<GameInput, Dictionary<int, Dictionary<string, string>>>)cacheField.GetValue(null);
            var snapshot = new Dictionary<GameInput, Dictionary<int, Dictionary<string, string>>>(originalCache, originalCache.Comparer);
            try
            {
                originalCache.Clear();
                string key = InputUtilities.GetTrackingKeyGameInputMappedAxis(game, 5, "mapped");
                Check(key == "mapped5123456789" && ReferenceEquals(key,
                    InputUtilities.GetTrackingKeyGameInputMappedAxis(game, 5, "mapped")), "Original convenience method uses the shared cache.");
                originalCache[game][5]["mapped"] = null;
                Check(InputUtilities.GetTrackingKeyGameInputMappedAxis(game, 5, "mapped") == null,
                    "The convenience method also retains cached nulls.");
                Check(!originalCache[game][5].ContainsKey("MAPPED"), "The original null comparer selects case-sensitive string equality.");
            }
            finally
            {
                originalCache.Clear();
                foreach (var pair in snapshot) originalCache.Add(pair.Key, pair.Value);
            }
            Check(originalCache.Count == snapshot.Count, "The fixture restores the genuine shared tracking cache.");
            foreach (var pair in snapshot)
                Check(ReferenceEquals(originalCache[pair.Key], pair.Value), "Prior cache entries retain their original nested objects.");
        }

        private static Dictionary<GameInput, Dictionary<int, Dictionary<T, string>>> NewMap<T>()
        {
            return new Dictionary<GameInput, Dictionary<int, Dictionary<T, string>>>(HardlightEnumComparers.GameInputComparer);
        }
        private sealed class AxisFailure : Exception { }
        private sealed class AxisProbe
        {
            internal Action OnString;
            internal int StringCalls;
            public override string ToString() { ++StringCalls; OnString?.Invoke(); return "probe"; }
        }
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(message);
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
