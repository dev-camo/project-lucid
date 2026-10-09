using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ProjectLucid.Verification
{
    // Controlled checks for original input configuration, direct trigger callbacks,
    // callback fault ordering, inactive providers and glyph setter order.
    // Automatic GameInputTrigger lifecycle scheduling requires a separate PlayMode gate.
    public static class InputRemainingOriginalBehaviorVerification
    {
        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }
        private static FieldInfo Field(Type owner, string name)
        {
            for (Type t = owner; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new InvalidOperationException("Missing genuine field " + owner.FullName + "." + name);
        }
        private static object Get(object owner, string name) => Field(owner.GetType(), name).GetValue(owner);
        private static void Set(object owner, string name, object value) => Field(owner.GetType(), name).SetValue(owner, value);
        private static void Cleanup(Action action, List<Exception> failures)
        {
            try { action(); } catch (Exception failure) { failures.Add(failure); }
        }
        private static void NullFault(Action action, string label)
        {
            try { action(); } catch (NullReferenceException) { return; }
            throw new InvalidOperationException(label);
        }
        private static void ExactFault(Action action, Exception expected, string label)
        {
            try { action(); }
            catch (Exception actual)
            {
                if (!ReferenceEquals(actual, expected)) throw new InvalidOperationException(label, actual);
                return;
            }
            throw new InvalidOperationException(label);
        }
        private sealed class Owned : IDisposable
        {
            private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
            private readonly List<Action> beforeDestroy = new List<Action>();
            internal T Scriptable<T>() where T : ScriptableObject
            {
                T value = ScriptableObject.CreateInstance<T>(); objects.Add(value); return value;
            }
            internal GameObject InactiveGameObject(string name, params Type[] components)
            {
                GameObject value = new GameObject(name, components); objects.Add(value);
                value.SetActive(false); return value;
            }
            internal Texture2D Texture()
            {
                Texture2D value = new Texture2D(2, 2); objects.Add(value); return value;
            }
            internal void BeforeDestroy(Action action) { beforeDestroy.Add(action); }
            public void Dispose()
            {
                var failures = new List<Exception>();
                for (int i = beforeDestroy.Count - 1; i >= 0; --i) Cleanup(beforeDestroy[i], failures);
                for (int i = objects.Count - 1; i >= 0; --i)
                {
                    UnityEngine.Object value = objects[i];
                    Cleanup(() => UnityEngine.Object.DestroyImmediate(value), failures);
                }
                beforeDestroy.Clear(); objects.Clear();
                if (failures.Count != 0) throw new AggregateException("Owned input object cleanup failed", failures);
            }
        }
        // This lease OUTLIVES the inner Owned scope: direct original OnDisable
        // cleanup must complete while isolated dictionaries are still installed.
        // Live genuine button providers are removed before any globals restore.
        private sealed class MappingScope : IDisposable
        {
            internal readonly ControlMapping Map;
            private readonly List<ModifierTriggerInputOnCriteria> ownedProviders = new List<ModifierTriggerInputOnCriteria>();
            private readonly List<IDictionary> dictionaries = new List<IDictionary>();
            private readonly List<List<DictionaryEntry>> entries = new List<List<DictionaryEntry>>();
            private readonly List<GameObject> handlers, oldHandlers;
            private readonly HashSet<GameInput> exclusive;
            private readonly List<GameInput> oldExclusive;
            private readonly object oldType, oldJoystick, oldTypeEvent, oldJoystickEvent;
            private static FieldInfo Static(string name) => Field(typeof(ControlMapping), name);
            internal MappingScope()
            {
                oldType = Static("s_lastInputType").GetValue(null); oldJoystick = Static("s_lastJoystickIndex").GetValue(null);
                oldTypeEvent = Static("OnUpdateLastInputType").GetValue(null); oldJoystickEvent = Static("OnUpdateLastJoystickIndex").GetValue(null);
                foreach (string name in new[] { "s_onPointer", "s_onScrollWheel", "s_onTap", "s_onTouch", "s_onTouchRelease", "s_onMultiTouch", "s_onMultiTouchRelease", "s_onVectorisedGameInput", "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis", "s_onAxisStart", "s_onAxisEnd", "s_onSwipe" })
                {
                    IDictionary value = (IDictionary)Static(name).GetValue(null);
                    var saved = new List<DictionaryEntry>(); foreach (DictionaryEntry entry in value) saved.Add(entry);
                    dictionaries.Add(value); entries.Add(saved);
                }
                handlers = (List<GameObject>)Static("s_eventHandlers").GetValue(null); oldHandlers = new List<GameObject>(handlers);
                exclusive = (HashSet<GameInput>)Static("s_exclusiveGameInputs").GetValue(null); oldExclusive = new List<GameInput>(exclusive);
                Map = ScriptableObject.CreateInstance<ControlMapping>();
                try
                {
                    foreach (IDictionary dictionary in dictionaries) dictionary.Clear();
                    handlers.Clear(); exclusive.Clear();
                    Static("s_lastInputType").SetValue(null, InputType.Unsupported); Static("s_lastJoystickIndex").SetValue(null, -1);
                    Static("OnUpdateLastInputType").SetValue(null, (Action<InputType>)(_ => { }));
                    Static("OnUpdateLastJoystickIndex").SetValue(null, (Action<int>)(_ => { }));
                }
                catch { Dispose(); throw; }
            }
            internal ModifierTriggerInputOnCriteria Button()
            {
                var button = ScriptableObject.CreateInstance<ModifierTriggerInputOnCriteria>(); ownedProviders.Add(button);
                Set(button, "m_controlMapping", Map); Set(button, "m_gameInputToTrigger", GameInput.UISubmit);
                Set(button, "m_criteriaRule", Enum.ToObject(Field(button.GetType(), "m_criteriaRule").FieldType, 0));
                Set(button, "m_criteriaValue", 0.5f); Set(button, "m_joystickIndex", 7);
                Set(button, "m_valueToTriggerWith", 0.75f); Set(button, "m_inputType", InputType.Keyboard);
                return button;
            }
            public void Dispose()
            {
                var failures = new List<Exception>();
                try
                {
                    Cleanup(() =>
                    {
                        var providers = (List<IInputButton>)Get(Map, "m_buttonProviders");
                        IInputButton[] savedProviders = providers.ToArray();
                        for (int i = savedProviders.Length - 1; i >= 0; --i)
                        {
                            IInputButton value = savedProviders[i]; Cleanup(() => Map.UnregisterButtonProvider(value), failures);
                        }
                    }, failures);
                    for (int i = 0; i < dictionaries.Count; ++i)
                    {
                        IDictionary value = dictionaries[i]; List<DictionaryEntry> old = entries[i];
                        Cleanup(() => { value.Clear(); foreach (DictionaryEntry entry in old) value.Add(entry.Key, entry.Value); }, failures);
                    }
                    Cleanup(() => { handlers.Clear(); handlers.AddRange(oldHandlers); }, failures);
                    Cleanup(() => { exclusive.Clear(); exclusive.UnionWith(oldExclusive); }, failures);
                    Cleanup(() => Static("s_lastInputType").SetValue(null, oldType), failures);
                    Cleanup(() => Static("s_lastJoystickIndex").SetValue(null, oldJoystick), failures);
                    Cleanup(() => Static("OnUpdateLastInputType").SetValue(null, oldTypeEvent), failures);
                    Cleanup(() => Static("OnUpdateLastJoystickIndex").SetValue(null, oldJoystickEvent), failures);
                }
                finally
                {
                    for (int i = ownedProviders.Count - 1; i >= 0; --i)
                    {
                        ModifierTriggerInputOnCriteria value = ownedProviders[i]; Cleanup(() => UnityEngine.Object.DestroyImmediate(value), failures);
                    }
                    ownedProviders.Clear(); Cleanup(() => UnityEngine.Object.DestroyImmediate(Map), failures);
                }
                if (failures.Count != 0) throw new AggregateException("ControlMapping restoration failed", failures);
            }
        }
        public static void VerifyConfigurationAndInactiveProviders()
        {
            using (var owned = new Owned())
            {
                var configuration = owned.Scriptable<InputMonitorConfiguration>();
                string[] fields = { "m_configurationFolder", "m_activeInputBindingsFolder", "m_inputBindingsFolder", "m_inputMonitorsFolder", "m_autoGeneratedCodeFolderPath", "m_autoGeneratedNamespace", "m_autoGeneratedInputEnumComparerClassName", "m_pluginLocationOverride" };
                Func<string>[] getters = { () => configuration.ConfigurationFolderPath, () => configuration.ActiveInputBindingsFolder, () => configuration.InputBindingsFolder, () => configuration.InputMonitorsFolder, () => configuration.AutoGeneratedCodeFolderPath, () => configuration.AutoGeneratedNamespace, () => configuration.AutoGeneratedInputEnumComparerClassName, () => configuration.PluginLocationOverride };
                string[] defaults = { "Assets/Dash Assets/Configuration/", "Assets/Dash Assets/Configuration/ActiveInputBindings/", "Assets/Dash Assets/Configuration/InputBindings/", "Assets/Dash Assets/Configuration/InputMonitors/", "Plugins/AutoGenerated", "Hardlight", "HardlightEnumComparers", "" };
                string json = JsonUtility.ToJson(configuration);
                for (int i = 0; i < fields.Length; ++i)
                {
                    FieldInfo field = Field(configuration.GetType(), fields[i]);
                    Require(field.FieldType == typeof(string) && field.IsPrivate && field.IsDefined(typeof(SerializeField), false), "genuine private serialized string " + fields[i]);
                    Require(getters[i]() == defaults[i] && (string)field.GetValue(configuration) == defaults[i], "original configuration default/getter association " + fields[i]);
                    Require(json.Contains("\"" + fields[i] + "\":"), "actual JsonUtility emits original field name " + fields[i]);
                }
                var former = (FormerlySerializedAsAttribute)Attribute.GetCustomAttribute(Field(configuration.GetType(), "m_pluginLocationOverride"), typeof(FormerlySerializedAsAttribute));
                Require(former != null && former.oldName == "m_pluginLocation", "original plugin-location legacy field annotation");
                JsonUtility.FromJsonOverwrite("{\"m_configurationFolder\":\"owned-0\",\"m_activeInputBindingsFolder\":\"owned-1\",\"m_inputBindingsFolder\":\"owned-2\",\"m_inputMonitorsFolder\":\"owned-3\",\"m_autoGeneratedCodeFolderPath\":\"owned-4\",\"m_autoGeneratedNamespace\":\"owned-5\",\"m_autoGeneratedInputEnumComparerClassName\":\"owned-6\",\"m_pluginLocationOverride\":\"owned-7\"}", configuration);
                string[] replacements = { "owned-0", "owned-1", "owned-2", "owned-3", "owned-4", "owned-5", "owned-6", "owned-7" };
                for (int i = 0; i < fields.Length; ++i)
                    Require(getters[i]() == replacements[i] && (string)Get(configuration, fields[i]) == replacements[i], "actual overwrite reaches original field/getter " + fields[i]);
                Set(configuration, "m_pluginLocationOverride", null);
                Require(configuration.PluginLocationOverride == null, "plugin override getter retains null rather than inventing fallback");
                Require(InputMonitorConfiguration.DefaultFileName == "InputMonitorConfiguration", "original configuration filename literal");
                var pathObject = owned.InactiveGameObject("owned original HLInput path constants");
                var paths = pathObject.AddComponent<HLInputPathConstants>();
                Require(paths != null && typeof(HLInputPathConstants).BaseType == typeof(MonoBehaviour) && paths.gameObject == pathObject, "original path constants are a real instantiable MonoBehaviour");
                Require(HLInputPathConstants.ModuleMenuBase == "Hardlight/HLInput/" && HLInputPathConstants.ControllerProviders == "ControllerProviders/" && HLInputPathConstants.GameInputGlyphMaps == "GameInputGlyphMaps/" && HLInputPathConstants.Modifiers == "Modifiers/", "all original path constants");
                var osx = owned.Scriptable<StandaloneOSXControllerNameProviderScriptableObject>();
                var unity = owned.Scriptable<UnityControllerNameProviderScriptableObject>();
                Require(!osx.IsActiveProvider() && !unity.IsActiveProvider(), "both genuine Scriptable providers retain original inactive result");
                // Never call either factory, GetControllerNames, Initialise or
                // polling routine. This assertion cannot activate a plugin.
            }
        }
        // Ordinary EditMode does not establish automatic lifecycle scheduling for
        // this non-ExecuteInEditMode component. Invoke its genuine private callback
        // on the owned inactive component; do not synthesize a subscription.
        private static void InvokeOriginalTriggerCallback(GameInputTrigger trigger, string name)
        {
            Require(name == "OnEnable" || name == "OnDisable", "Only original trigger lifecycle callbacks may be invoked.");
            MethodInfo callback = typeof(GameInputTrigger).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Require(callback != null && callback.DeclaringType == typeof(GameInputTrigger) &&
                (ushort)callback.Attributes == 129 && callback.ReturnType == typeof(void) &&
                callback.GetParameters().Length == 0 && !callback.IsGenericMethod,
                "Original private trigger callback declaration changed: " + name);
            // Setup/cleanup invocation failures fail the fixture; no refusal or
            // expected runtime fault is inferred from reflection exceptions here.
            callback.Invoke(trigger, null);
        }
        private static GameInputTrigger TriggerObject(Owned owned, GameInputTrigger.InputEvent inputEvent, out GameObject gameObject)
        {
            gameObject = owned.InactiveGameObject("owned original GameInputTrigger");
            GameInputTrigger trigger = gameObject.AddComponent<GameInputTrigger>();
            Set(trigger, "m_gameInput", GameInput.UISubmit); Set(trigger, "m_intputTrigger", InputTrigger.Down);
            Set(trigger, "OnGameInput", inputEvent);
            owned.BeforeDestroy(() => InvokeOriginalTriggerCallback(trigger, "OnDisable"));
            return trigger;
        }
        public static void VerifyGameInputTriggerCallbacks()
        {
            using (var scope = new MappingScope())
            using (var owned = new Owned())
            {
                var inputEvent = new GameInputTrigger.InputEvent(); var trace = new List<string>();
                UnityAction<float> first = value => trace.Add("event-first:" + (value == 0.75f));
                UnityAction<float> second = value => trace.Add("event-second");
                inputEvent.AddListener(first); inputEvent.AddListener(second);
                owned.BeforeDestroy(() => inputEvent.RemoveListener(first)); owned.BeforeDestroy(() => inputEvent.RemoveListener(second));
                GameObject gameObject; GameInputTrigger trigger = TriggerObject(owned, inputEvent, out gameObject);
                var button = scope.Button();
                ControlMapping.Subscribe(GameInput.UISubmit, value => trace.Add("before"), InputTrigger.Down);
                // Controlled original callback; fields/event are already set,
                // and the owned component remains inactive throughout this test.
                InvokeOriginalTriggerCallback(trigger, "OnEnable");
                ControlMapping.Subscribe(GameInput.UISubmit, value => trace.Add("after"), InputTrigger.Down);
                button.Modify(1f, 0f);
                Require(trace.Count == 4 && trace[0] == "before" && trace[1] == "event-first:True" && trace[2] == "event-second" && trace[3] == "after", "genuine subscription and UnityEvent listener ordering/value");
                scope.Map.ClearFrameInputs(); trace.Clear(); InvokeOriginalTriggerCallback(trigger, "OnDisable"); button.Modify(1f, 0f);
                Require(trace.Count == 2 && trace[0] == "before" && trace[1] == "after", "controlled original OnDisable removes original subscription");
                scope.Map.ClearFrameInputs(); trace.Clear(); InvokeOriginalTriggerCallback(trigger, "OnEnable"); button.Modify(1f, 0f);
                Require(trace.Count == 4 && trace[0] == "before" && trace[1] == "after" && trace[2] == "event-first:True" && trace[3] == "event-second", "second controlled OnEnable appends one original callback without duplicates");
                inputEvent.RemoveListener(second); scope.Map.ClearFrameInputs(); trace.Clear(); button.Modify(1f, 0f);
                Require(trace.Count == 3 && trace[2] == "event-first:True", "genuine UnityEvent listener removal takes effect");
                inputEvent.RemoveListener(first); scope.Map.ClearFrameInputs(); trace.Clear(); button.Modify(1f, 0f);
                Require(trace.Count == 2 && trace[0] == "before" && trace[1] == "after", "empty genuine InputEvent does not invent a null-event fault");
            }
        }
        public static void VerifyGameInputTriggerFaults()
        {
            // Separate isolated scopes: genuine FastAction remains invocation-
            // active after a thrown callback. Never re-invoke either faulted route.
            using (var scope = new MappingScope())
            using (var owned = new Owned())
            {
                GameObject gameObject; GameInputTrigger trigger = TriggerObject(owned, null, out gameObject);
                Require(Get(trigger, "OnGameInput") == null && trigger.gameObject == gameObject, "null-event setup retains genuine owned component and null field");
                var button = scope.Button(); var trace = new List<string>();
                ControlMapping.Subscribe(GameInput.UISubmit, value => trace.Add("before"), InputTrigger.Down);
                InvokeOriginalTriggerCallback(trigger, "OnEnable");
                ControlMapping.Subscribe(GameInput.UISubmit, value => trace.Add("after"), InputTrigger.Down);
                NullFault(() => button.Modify(1f, 0f), "original null InputEvent retains direct null fault");
                Require(trace.Count == 1 && trace[0] == "before" && !(bool)Get(button, "m_inputIsDown"), "null event stops later route callback before genuine Down state store");
            }
            using (var scope = new MappingScope())
            using (var owned = new Owned())
            {
                var inputEvent = new GameInputTrigger.InputEvent(); var trace = new List<string>();
                var failure = new InvalidOperationException("owned UnityEvent callback failure");
                UnityAction<float> first = value => trace.Add("event-first");
                UnityAction<float> failing = value => { trace.Add("event-failing"); throw failure; };
                UnityAction<float> last = value => trace.Add("event-last");
                inputEvent.AddListener(first); inputEvent.AddListener(failing); inputEvent.AddListener(last);
                owned.BeforeDestroy(() => inputEvent.RemoveListener(first)); owned.BeforeDestroy(() => inputEvent.RemoveListener(failing)); owned.BeforeDestroy(() => inputEvent.RemoveListener(last));
                GameObject gameObject; GameInputTrigger trigger = TriggerObject(owned, inputEvent, out gameObject);
                var button = scope.Button();
                ControlMapping.Subscribe(GameInput.UISubmit, value => trace.Add("before"), InputTrigger.Down);
                InvokeOriginalTriggerCallback(trigger, "OnEnable");
                ControlMapping.Subscribe(GameInput.UISubmit, value => trace.Add("after"), InputTrigger.Down);
                ExactFault(() => button.Modify(1f, 0f), failure, "original UnityEvent callback exception identity is preserved");
                Require(trace.Count == 3 && trace[0] == "before" && trace[1] == "event-first" && trace[2] == "event-failing" && !(bool)Get(button, "m_inputIsDown"), "throwing UnityEvent callback aborts remaining event/map callbacks and Down store");
            }
        }
        public static void VerifyGlyphDisplaySetterOrderAndFaults()
        {
            using (var owned = new Owned())
            {
                // Text/RawImage require distinct Graphic owners. Owned textures
                // precede GameObjects so reverse cleanup destroys graphics first.
                Texture2D oldTexture = owned.Texture(), newTexture = owned.Texture();
                GameObject textObject = owned.InactiveGameObject("owned glyph Text", typeof(RectTransform), typeof(CanvasRenderer));
                GameObject imageObject = owned.InactiveGameObject("owned glyph RawImage", typeof(RectTransform), typeof(CanvasRenderer));
                Text text = textObject.AddComponent<Text>(); RawImage image = imageObject.AddComponent<RawImage>();
                var displayObject = owned.InactiveGameObject("owned original glyph debug display");
                var display = displayObject.AddComponent<GlyphMappingDebugDisplay>(); Set(display, "text", text); Set(display, "rawImage", image);
                textObject.SetActive(true); imageObject.SetActive(true); text.text = "before"; image.texture = oldTexture;
                var trace = new List<string>();
                UnityAction textProbe = () => trace.Add(image.texture == oldTexture ? "text:old-texture" : "text:new-texture");
                UnityAction imageProbe = () => trace.Add("image:" + text.text);
                text.RegisterDirtyVerticesCallback(textProbe); owned.BeforeDestroy(() => text.UnregisterDirtyVerticesCallback(textProbe));
                image.RegisterDirtyVerticesCallback(imageProbe); owned.BeforeDestroy(() => image.UnregisterDirtyVerticesCallback(imageProbe));
                display.SetupDisplay("first", newTexture);
                Require(text.text == "first" && image.texture == newTexture && trace.Count == 2 && trace[0] == "text:old-texture" && trace[1] == "image:first", "real Text setter/callback completes before RawImage setter/callback");
                trace.Clear(); display.SetupDisplay("first", newTexture);
                Require(trace.Count == 0, "genuine unchanged UI setters do not invent callbacks");
                image.texture = oldTexture; trace.Clear(); Set(display, "rawImage", null);
                NullFault(() => display.SetupDisplay("before-null-image", newTexture), "original missing RawImage faults after Text setter");
                Require(text.text == "before-null-image" && image.texture == oldTexture && trace.Count == 1 && trace[0] == "text:old-texture", "first real setter effect survives missing second field");
                Set(display, "rawImage", image); Set(display, "text", null); trace.Clear();
                NullFault(() => display.SetupDisplay("unused", newTexture), "original missing Text faults before RawImage setter");
                Require(image.texture == oldTexture && trace.Count == 0, "missing first field leaves second setter untouched");
                Set(display, "text", text); var failure = new InvalidOperationException("owned UI dirty callback failure");
                UnityAction textFailure = () => { throw failure; };
                text.RegisterDirtyVerticesCallback(textFailure); owned.BeforeDestroy(() => text.UnregisterDirtyVerticesCallback(textFailure));
                trace.Clear();
                ExactFault(() => display.SetupDisplay("text-callback-failed", newTexture), failure, "real Text dirty callback exception propagates");
                Require(text.text == "text-callback-failed" && image.texture == oldTexture && trace.Count == 1, "stored text survives callback exception while texture setter never runs");
                text.UnregisterDirtyVerticesCallback(textFailure);
                UnityAction imageFailure = () => { throw failure; };
                image.RegisterDirtyVerticesCallback(imageFailure); owned.BeforeDestroy(() => image.UnregisterDirtyVerticesCallback(imageFailure));
                trace.Clear();
                ExactFault(() => display.SetupDisplay("image-callback-failed", newTexture), failure, "real RawImage dirty callback exception propagates");
                Require(text.text == "image-callback-failed" && image.texture == newTexture && trace.Count == 2 && trace[1] == "image:image-callback-failed", "both setter values persist before RawImage dirty callback fault");
                image.UnregisterDirtyVerticesCallback(imageFailure); trace.Clear(); display.SetupDisplay(null, null);
                Require(text.text == string.Empty && image.texture == null, "genuine UI providers handle null values without a fabricated component");
            }
        }
    }
}
