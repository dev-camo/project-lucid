using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Exercise the reconstructed callback boundary with real game classes.
    // No device provider, platform service or shipping native binary runs here.
    public static class InputControlMappingVerification
    {
        private static void Require(bool condition, string label, ref int checks)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++;
        }

        private static void Fault<T>(Action action, string label, ref int checks) where T : Exception
        {
            try { action(); }
            catch (T) { checks++; return; }
            throw new InvalidOperationException(label);
        }

        private static FieldInfo Static(string name) => typeof(ControlMapping).GetField(
            name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing original ControlMapping field " + name);

        // The original dictionaries are shared by all maps. Restore their old
        // entries and the same collection objects even if an assertion fails.
        private sealed class Scope : IDisposable
        {
            internal readonly ControlMapping Map;
            private readonly List<IDictionary> dictionaries = new List<IDictionary>();
            private readonly List<List<DictionaryEntry>> entries = new List<List<DictionaryEntry>>();
            private readonly List<GameObject> eventHandlers;
            private readonly List<GameObject> oldEventHandlers;
            private readonly HashSet<GameInput> exclusive;
            private readonly List<GameInput> oldExclusive;
            private readonly object oldType, oldJoystick, oldTypeEvent, oldJoystickEvent;

            internal Scope()
            {
                oldType = Static("s_lastInputType").GetValue(null);
                oldJoystick = Static("s_lastJoystickIndex").GetValue(null);
                oldTypeEvent = Static("OnUpdateLastInputType").GetValue(null);
                oldJoystickEvent = Static("OnUpdateLastJoystickIndex").GetValue(null);
                foreach (string name in new[] {
                    "s_onPointer", "s_onScrollWheel", "s_onTap", "s_onTouch", "s_onTouchRelease",
                    "s_onMultiTouch", "s_onMultiTouchRelease", "s_onVectorisedGameInput",
                    "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis",
                    "s_onAxisStart", "s_onAxisEnd", "s_onSwipe" })
                {
                    IDictionary dictionary = (IDictionary)Static(name).GetValue(null);
                    var saved = new List<DictionaryEntry>();
                    foreach (DictionaryEntry entry in dictionary) saved.Add(entry);
                    dictionaries.Add(dictionary);
                    entries.Add(saved);
                }
                eventHandlers = (List<GameObject>)Static("s_eventHandlers").GetValue(null);
                oldEventHandlers = new List<GameObject>(eventHandlers);
                exclusive = (HashSet<GameInput>)Static("s_exclusiveGameInputs").GetValue(null);
                oldExclusive = new List<GameInput>(exclusive);
                Map = ScriptableObject.CreateInstance<ControlMapping>();
                try
                {
                    foreach (IDictionary dictionary in dictionaries) dictionary.Clear();
                    eventHandlers.Clear();
                    exclusive.Clear();
                    Static("s_lastInputType").SetValue(null, InputType.Unsupported);
                    Static("s_lastJoystickIndex").SetValue(null, -1);
                    Static("OnUpdateLastInputType").SetValue(null, (Action<InputType>)(_ => { }));
                    Static("OnUpdateLastJoystickIndex").SetValue(null, (Action<int>)(_ => { }));
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                try
                {
                    for (int i = 0; i < dictionaries.Count; i++)
                    {
                        dictionaries[i].Clear();
                        foreach (DictionaryEntry entry in entries[i]) dictionaries[i].Add(entry.Key, entry.Value);
                    }
                    eventHandlers.Clear();
                    eventHandlers.AddRange(oldEventHandlers);
                    exclusive.Clear();
                    exclusive.UnionWith(oldExclusive);
                    Static("s_lastInputType").SetValue(null, oldType);
                    Static("s_lastJoystickIndex").SetValue(null, oldJoystick);
                    Static("OnUpdateLastInputType").SetValue(null, oldTypeEvent);
                    Static("OnUpdateLastJoystickIndex").SetValue(null, oldJoystickEvent);
                }
                finally { UnityEngine.Object.DestroyImmediate(Map); }
            }
        }

        private static ButtonHandler.OnDown Down(InputButton button) =>
            (ButtonHandler.OnDown)typeof(BaseInputButton<KeyCode, ButtonBinding>).GetField(
                "ButtonDownHandlers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);

        private static void BlockMouse(ControlMapping map)
        {
            // Use the real private serialized record and its real constructor.
            Type recordType = typeof(ControlMap).GetNestedType("PlatformsWithBlockedInputTypes", BindingFlags.NonPublic);
            object record = Activator.CreateInstance(recordType);
            recordType.GetField("Platform").SetValue(record, Application.platform);
            recordType.GetField("InputTypes").SetValue(record, new List<InputType> { InputType.Mouse });
            Array records = Array.CreateInstance(recordType, 1);
            records.SetValue(record, 0);
            typeof(ControlMap).GetField("m_blockedInputTypes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(map, records);
        }

        public static int VerifyCallbackRouting()
        {
            int checks = 0;
            using (var scope = new Scope())
            {
                ControlMapping map = scope.Map;
                var button = new InputButton();
                var trace = new List<string>();
                Action<float> fallback = value => trace.Add("fallback:" + value.ToString(CultureInfo.InvariantCulture));
                Action<float> specific = value => trace.Add("specific:" + value.ToString(CultureInfo.InvariantCulture));
                Action<InputType> typeChanged = type => trace.Add("type:" + ((int)type).ToString(CultureInfo.InvariantCulture));
                Action<int> joystickChanged = index => trace.Add("joystick:" + index.ToString(CultureInfo.InvariantCulture));
                ControlMapping.OnUpdateLastInputType += typeChanged;
                ControlMapping.OnUpdateLastJoystickIndex += joystickChanged;
                ControlMapping.Subscribe(GameInput.UISubmit, fallback);
                ControlMapping.Subscribe(GameInput.UISubmit, fallback);
                ControlMapping.Subscribe(GameInput.UISubmit, specific, InputTrigger.Down, 2);
                map.Initialise(button);
                ButtonHandler.OnDown down = Down(button);
                Require(down != null && down.GetInvocationList().Length == 2,
                    "original initialization installs both direct and registered callbacks", ref checks);
                down(2, GameInput.UISubmit, 1f, InputType.Keyboard);
                Require(trace.Count == 3 && trace[0] == "type:-1162288346" && trace[1] == "joystick:2"
                    && trace[2] == "specific:1", "last-type then joystick then exact-index callback; duplicate frame suppressed", ref checks);
                down(3, GameInput.UISubmit, 2f, InputType.Mouse);
                Require(trace.Count == 3 && ControlMapping.LastInputType == InputType.Keyboard,
                    "same-frame input returns before updating shared device state", ref checks);
                map.ClearFrameInputs();
                down(3, GameInput.UISubmit, 2f, InputType.Keyboard);
                Require(trace.Count == 5 && trace[3] == "joystick:3" && trace[4] == "fallback:2",
                    "missing exact-index key falls back and AddUnique avoids duplicate subscribers", ref checks);

                ControlMapping.Unsubscribe(GameInput.UISubmit, specific, 2);
                map.ClearFrameInputs();
                int before = trace.Count;
                down(2, GameInput.UISubmit, 3f, InputType.Keyboard);
                Require(trace.Count == before + 1 && trace[before] == "joystick:2",
                    "retained empty exact-index entry prevents fallback after unsubscribe", ref checks);

                map.ClearFrameInputs();
                ControlMapping.AddExclusiveInput(GameInput.UIBack);
                before = trace.Count;
                down(-1, GameInput.UISubmit, 4f, InputType.Keyboard);
                Require(trace.Count == before, "exclusive other input blocks callback", ref checks);
                ControlMapping.ClearExclusiveInputs(null);
                down(-1, GameInput.UISubmit, 4f, InputType.Keyboard);
                Require(trace.Count == before + 2 && trace[before] == "joystick:-1" && trace[before + 1] == "fallback:4",
                    "exclusive filter did not consume frame; clear ignores its argument", ref checks);

                BlockMouse(map);
                map.ClearFrameInputs();
                before = trace.Count;
                down(-1, GameInput.UISubmit, 5f, InputType.Mouse);
                down(-1, GameInput.UISubmit, 6f, InputType.Keyboard);
                Require(trace.Count == before && ControlMapping.LastInputType == InputType.Keyboard,
                    "blocked device consumes frame before returning and does not change last type", ref checks);
                map.ClearFrameInputs();
                down(-1, GameInput.UISubmit, 6f, InputType.Keyboard);
                Require(trace.Count == before + 1 && trace[before] == "fallback:6", "next frame accepts unblocked input", ref checks);

                map.ClearFrameInputs();
                before = trace.Count;
                down(-1, GameInput.UISubmit, 7f, InputType.Unsupported);
                Require(trace.Count == before + 1 && trace[before] == "type:1208867107"
                    && ControlMapping.LastInputType == InputType.Unsupported,
                    "unsupported device updates last type before suppressing callback", ref checks);

                map.Shutdown(button);
                Require(Down(button).GetInvocationList().Length == 1, "direct shutdown leaves provider registration intact", ref checks);
                map.UnregisterButtonProvider(button);
                Require(Down(button) == null, "provider unregister removes the remaining original callback", ref checks);
            }
            return checks;
        }

        public static int VerifyDisableScopes()
        {
            int checks = 0;
            using (var scope = new Scope())
            {
                ControlMapping map = scope.Map;
                var trace = new List<string>();
                Action<GameInput, bool> disabled = (input, value) => trace.Add("disabled:" + value);
                Action<float> released = value => trace.Add("released:" + value.ToString(CultureInfo.InvariantCulture));
                Require(!map.IsGameInputDisabled(GameInput.UISubmit), "input starts enabled before stack initialization", ref checks);
                Fault<NullReferenceException>(() => map.AddGameInputDisabled(GameInput.UISubmit), "add-before-initialization retains original fault", ref checks);
                map.SubscribeInputDisabled(GameInput.UISubmit, disabled);
                Require(trace.Count == 1 && trace[0] == "disabled:False", "immediate disabled callback uses absent-data false", ref checks);
                map.InitialiseStackableData();
                trace.Clear();
                map.SubscribeInputDisabled(GameInput.UISubmit, disabled, false);
                ControlMapping.Subscribe(GameInput.UISubmit, released, InputTrigger.Up);
                StackableDataHandle first = map.AddGameInputDisabled(GameInput.UISubmit);
                Require(map.IsGameInputDisabled(GameInput.UISubmit) && trace.Count == 3
                    && trace[0] == "released:0" && trace[1] == "released:0" && trace[2] == "disabled:True",
                    "button-up and axis-end zeros precede disabled notification; initialization cleared prior subscription", ref checks);
                trace.Clear();
                StackableDataHandle second = map.AddGameInputDisabled(GameInput.UISubmit);
                Require(map.IsGameInputDisabled(GameInput.UISubmit) && trace.Count == 3,
                    "second logical-or disable remains disabled and repeats original notifications", ref checks);
                trace.Clear();
                map.RemoveGameInputDisabled(first);
                Require(map.IsGameInputDisabled(GameInput.UISubmit) && trace.Count == 3 && trace[2] == "disabled:True",
                    "removing one disable scope cannot re-enable the other", ref checks);
                trace.Clear();
                map.RemoveGameInputDisabled(second);
                Require(!map.IsGameInputDisabled(GameInput.UISubmit) && trace.Count == 1 && trace[0] == "disabled:False",
                    "last scope removal restores base value without release zeros", ref checks);

                trace.Clear();
                map.SubscribeInputDisabled(GameInput.UISubmit, disabled, false);
                StackableDataHandle third = map.AddGameInputDisabled(GameInput.UISubmit);
                Require(trace.Count == 4 && trace[2] == "disabled:True" && trace[3] == "disabled:True",
                    "disabled subscriptions retain duplicates", ref checks);
                map.UnsubscribeInputDisabled(GameInput.UISubmit, disabled);
                trace.Clear();
                map.RemoveGameInputDisabled(third);
                Require(trace.Count == 1 && trace[0] == "disabled:False", "unsubscribe removes one delegate occurrence", ref checks);
                trace.Clear();
                map.ShutdownStackableData();
                Require(trace.Count == 1 && trace[0] == "disabled:False" && !map.IsGameInputDisabled(GameInput.UISubmit),
                    "shutdown deconstructs callback entries then clears original data", ref checks);
                map.RemoveGameInputDisabled(third);
                Require(!map.IsGameInputDisabled(GameInput.UISubmit), "remove after shutdown uses original null guard", ref checks);

                map.InitialiseStackableData();
                Fault<NullReferenceException>(() => map.SubscribeInputDisabled(GameInput.UIBack, null),
                    "immediate null subscriber faults after storing its entry", ref checks);
                Fault<NullReferenceException>(() => map.ShutdownStackableData(),
                    "stored null callback retains shutdown fault", ref checks);
                FieldInfo data = typeof(ControlMap).GetField("m_gameInputsDisabled", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(data.GetValue(map) != null, "faulted shutdown preserves data before its final clear", ref checks);
            }
            return checks;
        }
    }
}
