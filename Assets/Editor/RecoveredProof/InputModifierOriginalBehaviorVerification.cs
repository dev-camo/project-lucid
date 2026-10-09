using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Controlled checks for the reconstructed original input modifiers.
    // All modifiers/maps are genuine owned ScriptableObjects. No device,
    // plugin, fake provider, replacement clock or uninitialized bypass runs.
    public static class InputModifierOriginalBehaviorVerification
    {
        private static void Require(bool condition, string label, ref int checks)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++;
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) <= 0.00001f;
        private static bool Same(Vector3 a, Vector3 b) => Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z);
        private static FieldInfo Field(Type owner, string name)
        {
            for (Type t = owner; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            throw new InvalidOperationException("Missing genuine field " + owner.FullName + "." + name);
        }
        private static object Get(object owner, string name) => Field(owner.GetType(), name).GetValue(owner);
        private static T Get<T>(object owner, string name) => (T)Get(owner, name);
        private static void Set(object owner, string name, object value) => Field(owner.GetType(), name).SetValue(owner, value);
        private static IDictionary States(object owner) => (IDictionary)Get(owner, "m_gameInputStates");
        private static void NullFault(Action action, string label, ref int checks)
        {
            try { action(); }
            catch (NullReferenceException) { checks++; return; }
            throw new InvalidOperationException(label);
        }
        private static void ExactFault(Action action, Exception expected, string label, ref int checks)
        {
            try { action(); }
            catch (Exception actual)
            {
                if (!ReferenceEquals(actual, expected)) throw new InvalidOperationException(label, actual);
                checks++;
                return;
            }
            throw new InvalidOperationException(label);
        }
        private static void Cleanup(Action action, List<Exception> failures)
        {
            try { action(); } catch (Exception failure) { failures.Add(failure); }
        }
        private sealed class Owned : IDisposable
        {
            private readonly List<ScriptableObject> objects = new List<ScriptableObject>();
            internal T Create<T>() where T : ScriptableObject
            {
                T result = ScriptableObject.CreateInstance<T>();
                objects.Add(result);
                return result;
            }
            public void Dispose()
            {
                var failures = new List<Exception>();
                for (int i = objects.Count - 1; i >= 0; --i)
                {
                    ScriptableObject value = objects[i];
                    Cleanup(() => UnityEngine.Object.DestroyImmediate(value), failures);
                }
                objects.Clear();
                if (failures.Count != 0) throw new AggregateException("Owned modifier cleanup failed", failures);
            }
        }
        // Preserve collection identities/old entries and actual event delegates.
        // Old FastAction entries remain untouched because the real dictionaries
        // are emptied before new subscriptions. No allocation-internal parity.
        private sealed class MappingScope : IDisposable
        {
            internal readonly ControlMapping Map;
            private readonly List<IDictionary> dictionaries = new List<IDictionary>();
            private readonly List<List<DictionaryEntry>> entries = new List<List<DictionaryEntry>>();
            private readonly List<GameObject> handlers;
            private readonly List<GameObject> oldHandlers;
            private readonly HashSet<GameInput> exclusive;
            private readonly List<GameInput> oldExclusive;
            private readonly object oldType, oldJoystick, oldTypeEvent, oldJoystickEvent;
            private static FieldInfo Static(string name) => Field(typeof(ControlMapping), name);
            internal MappingScope()
            {
                oldType = Static("s_lastInputType").GetValue(null);
                oldJoystick = Static("s_lastJoystickIndex").GetValue(null);
                oldTypeEvent = Static("OnUpdateLastInputType").GetValue(null);
                oldJoystickEvent = Static("OnUpdateLastJoystickIndex").GetValue(null);
                foreach (string name in new[] { "s_onPointer", "s_onScrollWheel", "s_onTap", "s_onTouch", "s_onTouchRelease", "s_onMultiTouch", "s_onMultiTouchRelease", "s_onVectorisedGameInput", "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis", "s_onAxisStart", "s_onAxisEnd", "s_onSwipe" })
                {
                    IDictionary value = (IDictionary)Static(name).GetValue(null);
                    var saved = new List<DictionaryEntry>();
                    foreach (DictionaryEntry item in value) saved.Add(item);
                    dictionaries.Add(value); entries.Add(saved);
                }
                handlers = (List<GameObject>)Static("s_eventHandlers").GetValue(null);
                oldHandlers = new List<GameObject>(handlers);
                exclusive = (HashSet<GameInput>)Static("s_exclusiveGameInputs").GetValue(null);
                oldExclusive = new List<GameInput>(exclusive);
                Map = ScriptableObject.CreateInstance<ControlMapping>();
                try
                {
                    foreach (IDictionary value in dictionaries) value.Clear();
                    handlers.Clear(); exclusive.Clear();
                    Static("s_lastInputType").SetValue(null, InputType.Unsupported);
                    Static("s_lastJoystickIndex").SetValue(null, -1);
                    Static("OnUpdateLastInputType").SetValue(null, (Action<InputType>)(_ => { }));
                    Static("OnUpdateLastJoystickIndex").SetValue(null, (Action<int>)(_ => { }));
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                var failures = new List<Exception>();
                try
                {
                    // Call real removal on every owned-map provider, even if a
                    // preceding removal/restore fails. No external provider ran.
                    Cleanup(() =>
                    {
                        var providers = (List<IInputButton>)Get(Map, "m_buttonProviders");
                        IInputButton[] savedProviders = providers.ToArray();
                        for (int i = savedProviders.Length - 1; i >= 0; --i)
                        {
                            IInputButton value = savedProviders[i];
                            Cleanup(() => Map.UnregisterButtonProvider(value), failures);
                        }
                    }, failures);
                    for (int i = 0; i < dictionaries.Count; ++i)
                    {
                        IDictionary value = dictionaries[i]; List<DictionaryEntry> old = entries[i];
                        Cleanup(() => { value.Clear(); foreach (DictionaryEntry e in old) value.Add(e.Key, e.Value); }, failures);
                    }
                    Cleanup(() => { handlers.Clear(); handlers.AddRange(oldHandlers); }, failures);
                    Cleanup(() => { exclusive.Clear(); exclusive.UnionWith(oldExclusive); }, failures);
                    Cleanup(() => Static("s_lastInputType").SetValue(null, oldType), failures);
                    Cleanup(() => Static("s_lastJoystickIndex").SetValue(null, oldJoystick), failures);
                    Cleanup(() => Static("OnUpdateLastInputType").SetValue(null, oldTypeEvent), failures);
                    Cleanup(() => Static("OnUpdateLastJoystickIndex").SetValue(null, oldJoystickEvent), failures);
                }
                finally { Cleanup(() => UnityEngine.Object.DestroyImmediate(Map), failures); }
                if (failures.Count != 0) throw new AggregateException("ControlMapping restoration failed", failures);
            }
        }
        public static int VerifyDeadZones()
        {
            int checks = 0;
            using (var owned = new Owned())
            {
                var dead = owned.Create<ModifierDeadZone>();
                Require(dead.Modify(0.49f, 0f) == 0f, "closed scalar gate blocks below open threshold", ref checks);
                Require(dead.Modify(-0.5f, 0f) == -0.5f, "magnitude reaches scalar open threshold", ref checks);
                Require(dead.Modify(0.45f, 0f) == 0.45f, "open gate retains hysteresis band", ref checks);
                Require(dead.Modify(0.4f, 0f) == 0f && !Get<bool>(dead, "m_gateOpen"), "close equality closes gate", ref checks);
                dead.Reset();
                Require(!Get<bool>(dead, "m_gateOpen") && dead.Modify(new Vector3(0f, 0f, 0.5f), 0f).z == 0.5f, "base vector gate includes Z", ref checks);
                dead.Reset();
                Require(!Get<bool>(dead, "m_gateOpen"), "deadzone reset closes shared scalar/vector gate", ref checks);
                Set(dead, "m_invertDeadZone", true); Set(dead, "m_openGateValue", 0.25f); Set(dead, "m_closeGateValue", 0.5f);
                Require(dead.Modify(-0.25f, 0f) == -0.25f && Get<bool>(dead, "m_gateOpen"), "inverted open equality preserves sign", ref checks);
                Require(dead.Modify(0.4f, 0f) == 0.4f, "inverted open gate keeps interior band", ref checks);
                Require(dead.Modify(0.5f, 0f) == 0f, "inverted close equality closes", ref checks);

                var radial = owned.Create<ModifierRadialDeadZone>();
                Require(Same(radial.Modify(new Vector3(0f, 0f, 9f), 0f), Vector3.zero), "radial gate ignores Z for radius", ref checks);
                Require(Get<bool>(radial, "m_cachedValues") && Get<float>(radial, "m_radiusOpenGateSquared") == 0.25f, "first radial call caches squared open threshold", ref checks);
                Set(radial, "m_openGateValue", 0.75f); Set(radial, "m_closeGateValue", 0.25f);
                Require(Same(radial.Modify(new Vector3(0.5f, 0f, 7f), 0f), new Vector3(0.5f, 0f, 7f)), "live field edit does not replace existing radial cache", ref checks);
                Require(Same(radial.Modify(new Vector3(0.45f, 0f, 7f), 0f), new Vector3(0.45f, 0f, 7f)), "cached radial hysteresis retains original vector/Z", ref checks);
                float oldClose = Get<float>(radial, "m_radiusCloseGateSquared"); radial.Reset();
                Require(!Get<bool>(radial, "m_gateOpen") && !Get<bool>(radial, "m_cachedValues") && Get<float>(radial, "m_radiusCloseGateSquared") == oldClose, "radial reset invalidates cache without clearing squared values", ref checks);
                Require(Same(radial.Modify(new Vector3(0.5f, 0f, 7f), 0f), Vector3.zero) && Get<float>(radial, "m_radiusOpenGateSquared") == 0.5625f, "next radial call recomputes edited threshold", ref checks);
                Require(radial.Modify(new Vector3(0.75f, 0f, 7f), 0f).z == 7f, "recomputed radial open equality retains Z", ref checks);
            }
            return checks;
        }
        public static int VerifyCooldownAndDebounce()
        {
            int checks = 0;
            using (var owned = new Owned())
            {
                var scalar = owned.Create<ModifierCoolDown>(); Set(scalar, "m_cooldownTime", 0.5f);
                Require(scalar.Modify(1f, 0.5f) == 1f && Get<float>(scalar, "m_timer") == 0f, "scalar cooldown activation returns before timer advance", ref checks);
                Require(scalar.Modify(2f, 0.25f) == 0f && Get<float>(scalar, "m_timer") == 0.25f, "active scalar cooldown suppresses input", ref checks);
                scalar.Reset();
                Require(Get<bool>(scalar, "m_stateChange") && Get<float>(scalar, "m_timer") == 0.25f, "cooldown inherits genuine empty Reset", ref checks);
                Require(scalar.Modify(2f, 0.25f) == 0f && !Get<bool>(scalar, "m_stateChange"), "scalar completion frame remains zero", ref checks);
                Require(scalar.Modify(2f, 0.25f) == 2f && Get<float>(scalar, "m_timer") == 0f, "following scalar activation passes again", ref checks);
                var vector = owned.Create<ModifierCoolDown>(); Set(vector, "m_cooldownTime", 0.5f);
                Require(Same(vector.Modify(Vector3.right, 0.25f), Vector3.right) && Get<float>(vector, "m_timer") == 0.25f, "vector cooldown activation falls through and advances timer", ref checks);
                Require(Same(vector.Modify(Vector3.right, 0.25f), Vector3.zero) && !Get<bool>(vector, "m_stateChange"), "vector completion frame alone returns zero", ref checks);
                var per = owned.Create<ModifierCoolDown>(); Set(per, "m_cooldownTime", 0.5f);
                Require(per.Modify(0f, 0f, GameInput.UISubmit) == 0f && States(per).Count == 1, "zero per-input cooldown still allocates real state", ref checks);
                Require(per.Modify(1f, 0.5f, GameInput.UISubmit) == 1f && Get<float>(States(per)[GameInput.UISubmit], "Timer") == 0f, "per-input scalar activation also returns before advance", ref checks);
                Require(per.Modify(1f, 0f, GameInput.UIBack) == 1f && States(per).Count == 2, "second genuine input has independent cooldown state", ref checks);

                var debounce = owned.Create<ModifierDebounce>(); Set(debounce, "m_debounceTime", 0.5f);
                Require(debounce.Modify(1f, 0.25f) == 1f && Get<float>(debounce, "m_timer") == 0.25f, "debounce activation advances scalar timer immediately", ref checks);
                Require(debounce.Modify(1f, 0.25f, GameInput.UISubmit) == 1f, "per-input debounce activation advances its own state", ref checks);
                object oldState = States(debounce)[GameInput.UISubmit]; IDictionary oldDictionary = States(debounce);
                debounce.Reset();
                Require(!Get<bool>(debounce, "m_stateChange") && Get<float>(debounce, "m_timer") == 0f, "debounce Reset clears scalar state/timer", ref checks);
                Require(ReferenceEquals(States(debounce), oldDictionary) && ReferenceEquals(States(debounce)[GameInput.UISubmit], oldState) && Get<bool>(oldState, "StateChange") && Get<float>(oldState, "Timer") == 0.25f, "debounce Reset retains dictionary identity, entry and per-input timer", ref checks);
                Require(debounce.Modify(1f, 0.25f, GameInput.UISubmit) == 0f && !Get<bool>(oldState, "StateChange"), "retained per-input timer completes immediately after Reset", ref checks);
                Require(debounce.Modify(1f, 0f, GameInput.UIBack) == 1f && States(debounce).Count == 2, "other input remains a separate real state", ref checks);
                debounce.Reset();
                Require(Same(debounce.Modify(Vector3.right, 0.5f), Vector3.zero), "vector debounce completes on activation frame with full delta", ref checks);
                var nan = owned.Create<ModifierCoolDown>();
                Require(float.IsNaN(nan.Modify(float.NaN, 0.25f)) && !Get<bool>(nan, "m_stateChange"), "ordered scalar nonzero gate does not activate for NaN", ref checks);
            }
            return checks;
        }
        public static int VerifyCurvesAndScalarLeaves()
        {
            int checks = 0;
            using (var owned = new Owned())
            {
                var curve = owned.Create<global::ModifierCurve>();
                NullFault(() => curve.Modify(1f, 0f), "original missing curve retains scalar null fault", ref checks);
                Set(curve, "m_curve", AnimationCurve.Constant(0f, 1f, -2f));
                Require(curve.Modify(0.5f, 0f) == -2f && curve.Modify(-0.5f, 0f) == 2f, "scalar curve applies original input sign after negative evaluation", ref checks);
                Require(curve.Modify(0f, 0f) == 2f, "scalar zero takes the negative-result branch", ref checks);
                Require(Same(curve.Modify(new Vector3(0.5f, 0f, 7f), 0f), new Vector3(2f, 0f, 7f)), "vector curve corrects X sign and preserves Z", ref checks);
                Require(Same(curve.Modify(new Vector3(-0.5f, 0f, -7f), 0f), new Vector3(-2f, 0f, -7f)), "negative vector input keeps original sign", ref checks);
                Require(Same(curve.Modify(new Vector3(0f, 0.5f, 3f), 0f), new Vector3(0f, 2f, 3f)), "vector curve uses Y and keeps original Z", ref checks);
                Require(Same(curve.Modify(new Vector3(2f, 0f, 3f), 0f), new Vector3(4f, 0f, 3f)), "curve evaluation radius is capped without normalizing original XY", ref checks);
                Require(Same(curve.Modify(new Vector3(0f, 0f, 20f), 0f), Vector3.zero), "near-zero XY path discards even nonzero Z", ref checks);
                var angle = owned.Create<ModifierAngleCurveNorthSouth>(); Set(angle, "m_curve", AnimationCurve.Constant(-180f, 180f, 0f));
                Require(angle.Modify(-3f, 0f) == -3f, "angle modifier scalar is identity", ref checks);
                Require(Same(angle.Modify(new Vector3(0f, 2f, 9f), 0f), new Vector3(0f, 4f, 9f)), "angle curve uses squared XY radius and preserves Z", ref checks);
                var greater = owned.Create<ModifierClampGreaterThan>(); Set(greater, "m_minimum", -0.5f);
                Require(greater.Modify(-1f, 0f) == -0.5f && greater.Modify(1f, 0f) == 1f && greater.Modify(float.NaN, 0f) == -0.5f, "greater clamp retains ordered NaN fallback", ref checks);
                var less = owned.Create<ModifierClampLessThan>(); Set(less, "m_maximum", 0.5f);
                Require(less.Modify(1f, 0f) == 0.5f && less.Modify(-1f, 0f) == -1f && less.Modify(float.NaN, 0f) == 0.5f, "less clamp retains ordered NaN fallback", ref checks);
                var multiplier = owned.Create<ModifierMultiplier>(); Set(multiplier, "m_multiplier", -2f);
                Require(multiplier.Modify(3f, 0f) == -6f && Same(multiplier.Modify(Vector3.right, 0f), Vector3.right), "multiplier scalar and genuine inherited vector identity", ref checks);
                var delta = owned.Create<ModifierSetDelta>(); Set(delta, "m_newDelta", 2f);
                Require(delta.Modify(0.25f, 0f) == 2f && delta.Modify(-0.25f, 0f) == -0.25f && delta.Modify(0f, 0f) == 0f, "set delta changes only same-sign nonzero scalar", ref checks);
                Set(delta, "m_newDelta", -2f);
                Require(delta.Modify(-0.25f, 0f) == -2f && delta.Modify(0.25f, 0f) == 0.25f, "negative delta has corresponding sign gate", ref checks);
                var ignore = owned.Create<ModifierIgnoreReturn>();
                Require(ignore.Modify(0.75f, 0f) == 0.75f && ignore.Modify(0.5f, 0f) == 0.75f, "ignore return retains preceding positive peak", ref checks);
                Require(ignore.Modify(-0.5f, 0f) == -0.5f && ignore.Modify(-0.25f, 0f) == -0.5f, "ignore return retains preceding negative peak", ref checks);
                ignore.Reset();
                Require(Get<float>(ignore, "m_valuePrevious") == 0f && ignore.Modify(0.25f, 0f) == 0.25f, "ignore-return Reset clears prior peak", ref checks);
            }
            return checks;
        }
        public static int VerifyRepeat()
        {
            int checks = 0;
            using (var owned = new Owned())
            {
                var repeat = owned.Create<ModifierRepeat>();
                Require(Get(repeat, "m_state") == null, "genuine Repeat construction does not initialize scalar state", ref checks);
                Require(repeat.Modify(0f, 1f) == 0f && repeat.Modify(0.00005f, 1f) == 0.00005f, "near-zero scalar exits before null-state dereference", ref checks);
                NullFault(() => repeat.Modify(1f, 0.25f), "nonzero scalar retains original null-state fault", ref checks);
                Require(repeat.Modify(0f, 0f, GameInput.UISubmit) == 0f && States(repeat).Count == 1, "per-input near-zero allocates genuine Repeat.State", ref checks);
                var state = (ModifierRepeat.State)States(repeat)[GameInput.UISubmit];
                Require(state.Timer == 0f && state.LastUpdateTime == 0f && state.LastResetTime == 0f, "near-zero per-input leaves new state fields untouched", ref checks);
                // Force branches through real serialized/state data; Time itself
                // is never replaced or set. All Time assertions are brackets.
                Set(repeat, "m_resetAfterInactivitySec", float.PositiveInfinity);
                Set(repeat, "m_cooldownOverTimeSec", AnimationCurve.Constant(0f, 1f, 0.5f));
                float before = Time.time;
                Require(repeat.Modify(1f, 0.25f, GameInput.UISubmit) == 0f && state.Timer == 0.25f, "per-input Repeat accumulates below cooldown", ref checks);
                float after = Time.time;
                Require(before <= state.LastUpdateTime && state.LastUpdateTime <= after, "Repeat update uses actual Unity Time within surrounding reads", ref checks);
                float savedUpdate = state.LastUpdateTime;
                Require(repeat.Modify(0.00005f, 8f, GameInput.UISubmit) == 0.00005f && state.Timer == 0.25f && state.LastUpdateTime == savedUpdate, "near-zero path bypasses timer and Time stores", ref checks);
                Require(repeat.Modify(1f, 0.25f, GameInput.UISubmit) == 1f && state.Timer == 0f, "Repeat completion resets timer and passes input", ref checks);
                Require(repeat.Modify(0f, 0f, GameInput.UIBack) == 0f && States(repeat).Count == 2 && !ReferenceEquals(States(repeat)[GameInput.UIBack], state), "second input obtains a separate genuine Repeat.State", ref checks);
                Set(repeat, "m_resetAfterInactivitySec", 0.05f); state.LastUpdateTime = float.NegativeInfinity; state.Timer = 0.25f;
                before = Time.time;
                Require(repeat.Modify(1f, 0.25f, GameInput.UISubmit) == 1f && state.Timer == 0f, "certain inactivity branch calls genuine State.Reset and passes input", ref checks);
                after = Time.time;
                Require(before <= state.LastUpdateTime && state.LastUpdateTime <= after && before <= state.LastResetTime && state.LastResetTime <= after, "distinct update/reset Time reads are bounded without imposing bit identity", ref checks);
                savedUpdate = state.LastUpdateTime; before = Time.time; state.Timer = 2f; state.Reset(); after = Time.time;
                Require(state.Timer == 0f && state.LastUpdateTime == savedUpdate && before <= state.LastResetTime && state.LastResetTime <= after, "State.Reset retains LastUpdateTime and reads real Time for reset", ref checks);
                IDictionary dictionary = States(repeat); repeat.Reset();
                Require(ReferenceEquals(States(repeat), dictionary) && States(repeat).Count == 2 && Get(repeat, "m_state") == null, "Repeat inherits empty reset without repairing scalar state or clearing inputs", ref checks);
            }
            return checks;
        }
        private static ModifierTriggerInputOnCriteria Trigger(Owned owned, ControlMapping map, bool held = false)
        {
            var trigger = owned.Create<ModifierTriggerInputOnCriteria>();
            Set(trigger, "m_controlMapping", map); Set(trigger, "m_gameInputToTrigger", GameInput.UISubmit);
            Set(trigger, "m_criteriaRule", Enum.ToObject(Field(trigger.GetType(), "m_criteriaRule").FieldType, 0));
            Set(trigger, "m_criteriaValue", 0.5f); Set(trigger, "m_heldButton", held);
            Set(trigger, "m_joystickIndex", 7); Set(trigger, "m_valueToTriggerWith", 0.75f); Set(trigger, "m_inputType", InputType.Keyboard);
            return trigger;
        }
        public static int VerifyTriggerOrderAndReset()
        {
            int checks = 0;
            using (var owned = new Owned())
            using (var scope = new MappingScope())
            {
                var trigger = Trigger(owned, scope.Map); var trace = new List<string>();
                trigger.ButtonDownHandlers += (joystick, input, value, type) => trace.Add(Get<bool>(trigger, "m_inputIsDown") ? "direct:down" : "direct:up");
                ControlMapping.OnUpdateLastInputType += type => trace.Add("type");
                ControlMapping.OnUpdateLastJoystickIndex += index => trace.Add("joystick");
                ControlMapping.Subscribe(GameInput.UISubmit, value => trace.Add(Get<bool>(trigger, "m_inputIsDown") ? "mapped:down" : "mapped:up"), InputTrigger.Down, 7);
                Require(trigger.Modify(1f, 0f) == 1f && Get<bool>(trigger, "m_providerRegistered"), "Trigger registers its actual provider before criteria callback", ref checks);
                Require(trace.Count == 4 && trace[0] == "direct:up" && trace[1] == "type" && trace[2] == "joystick" && trace[3] == "mapped:up", "existing typed event precedes real map type/joystick/routed callback; all see prior state", ref checks);
                Require(Get<bool>(trigger, "m_inputIsDown") && ((List<IInputButton>)Get(scope.Map, "m_buttonProviders")).Count == 1, "down state stored after callbacks and one provider registered", ref checks);
                trace.Clear(); trigger.Modify(1f, 0f);
                Require(trace.Count == 1 && trace[0] == "direct:down", "Down is not edge-gated while map suppresses already handled input", ref checks);
                scope.Map.ClearFrameInputs(); trace.Clear(); trigger.Modify(1f, 0f);
                Require(trace.Count == 2 && trace[0] == "direct:down" && trace[1] == "mapped:down", "cleared real map frame routes another Down without duplicate registration", ref checks);
                bool upSawDown = false;
                trigger.ButtonUpHandlers += (joystick, input, value, type) => upSawDown = Get<bool>(trigger, "m_inputIsDown");
                scope.Map.ClearFrameInputs(); trigger.Reset();
                Require(upSawDown && !Get<bool>(trigger, "m_inputIsDown"), "Reset invokes real up delegate before clearing down state", ref checks);
                Require(Get<bool>(trigger, "m_providerRegistered") && ((List<IInputButton>)Get(scope.Map, "m_buttonProviders")).Count == 1, "Reset leaves registration flag and genuine provider list intact", ref checks);
            }
            return checks;
        }
        public static int VerifyTriggerFaultsAndHeld()
        {
            int checks = 0;
            using (var owned = new Owned())
            using (var scope = new MappingScope())
            {
                var trigger = Trigger(owned, scope.Map);
                Require(trigger.Modify(0f, 0f) == 0f && Get<bool>(trigger, "m_providerRegistered"), "nonmatching criteria still registers provider", ref checks);
                var failure = new InvalidOperationException("owned route callback failure");
                Action<float> routedFailure = value => { throw failure; };
                ControlMapping.Subscribe(GameInput.UISubmit, routedFailure, InputTrigger.Down, 7);
                ExactFault(() => trigger.Modify(1f, 0f), failure, "real mapped callback failure is preserved", ref checks);
                Require(!Get<bool>(trigger, "m_inputIsDown"), "mapped Down callback fault precedes state store", ref checks);
                // Genuine FastAction leaves its invocation-active flag set when
                // a callback throws. Removal may be queued; this fixture never
                // invokes that failed Down route again, and scope cleanup drops
                // only owned entries before restoring the old exact objects.
                ControlMapping.Unsubscribe(GameInput.UISubmit, routedFailure, 7); scope.Map.ClearFrameInputs();
                scope.Map.UnregisterButtonProvider(trigger);
                Require(Get(trigger, "ButtonDownHandlers") == null && Get<bool>(trigger, "m_providerRegistered"), "real provider removal clears map delegate but original registration flag persists", ref checks);
                NullFault(() => trigger.Modify(1f, 0f), "missing typed Down delegate faults without silent repair", ref checks);
                Require(!Get<bool>(trigger, "m_inputIsDown") && ((List<IInputButton>)Get(scope.Map, "m_buttonProviders")).Count == 0, "null fault neither stores down nor reregisters", ref checks);
                ButtonHandler.OnDown down = (joystick, input, value, type) => { };
                trigger.ButtonDownHandlers += down; trigger.Modify(1f, 0f);
                NullFault(() => trigger.Modify(0f, 0f), "missing Up delegate retains null fault", ref checks);
                Require(Get<bool>(trigger, "m_inputIsDown"), "Up null fault retains down state", ref checks);
                ButtonHandler.OnUp failingUp = (joystick, input, value, type) => { throw failure; };
                trigger.ButtonUpHandlers += failingUp;
                ExactFault(() => trigger.Reset(), failure, "Reset preserves the same typed Up callback exception", ref checks);
                Require(Get<bool>(trigger, "m_inputIsDown"), "faulted Reset retains down state", ref checks);
                trigger.ButtonUpHandlers -= failingUp; bool oldDown = false;
                trigger.ButtonUpHandlers += (joystick, input, value, type) => oldDown = Get<bool>(trigger, "m_inputIsDown");
                trigger.Reset();
                Require(oldDown && !Get<bool>(trigger, "m_inputIsDown"), "successful Up then clears state", ref checks);

                var held = Trigger(owned, scope.Map, true); int heldCalls = 0;
                held.ButtonHandlers += (joystick, input, value, type) => { if (Get<bool>(held, "m_inputIsDown")) throw new InvalidOperationException("Held stored Down state"); heldCalls++; };
                scope.Map.ClearFrameInputs(); held.Modify(1f, 0f); held.Modify(1f, 0f); held.Modify(0f, 0f); held.Reset();
                Require(heldCalls == 2 && !Get<bool>(held, "m_inputIsDown"), "Held repeats genuine typed callback without Down/Up state", ref checks);
                Require(((List<IInputButton>)Get(scope.Map, "m_buttonProviders")).Count == 1 && Get<bool>(held, "m_providerRegistered"), "held provider also registers exactly once", ref checks);
                var unmapped = owned.Create<ModifierTriggerInputOnCriteria>();
                NullFault(() => unmapped.Modify(0f, 0f), "missing real ControlMapping faults before criteria", ref checks);
                Require(!Get<bool>(unmapped, "m_providerRegistered"), "failed registration does not store registration success", ref checks);
            }
            return checks;
        }
    }
}
