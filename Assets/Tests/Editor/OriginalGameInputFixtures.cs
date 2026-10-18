using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Declaration checks shared by the Game.Runtime input fixtures. Expected flags,
    // field order and attribute rows come from the DummyDll metadata of the 1.10.1 build.
    internal static class GameInputDeclarations
    {
        internal const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Option values in custom-attribute row order (1 NullChecks, 2 ArrayBoundsChecks), each false.
        internal static void SetOptions(Type type, params int[] order)
        {
            CustomAttributeData[] options = type.GetCustomAttributesData()
                .Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute)).ToArray();
            Assert.That(options.Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)), Is.EqualTo(order), type.Name);
            Assert.That(options.Select(a => a.ConstructorArguments[1].Value), Is.All.EqualTo(false), type.Name);
        }

        internal static void Fields(Type type, params (string name, Type type, int flags)[] expected)
        {
            FieldInfo[] actual = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(actual.Select(f => f.Name), Is.EqualTo(expected.Select(e => e.name)), type.Name);
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i].FieldType, Is.EqualTo(expected[i].type), expected[i].name);
                Assert.That((int)actual[i].Attributes, Is.EqualTo(expected[i].flags), expected[i].name);
            }
        }

        // Every declared method and constructor in metadata order, with its flags.
        internal static void Methods(Type type, params (string name, int flags)[] expected)
        {
            MethodBase[] actual = type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared))
                .OrderBy(m => m.MetadataToken).ToArray();
            Assert.That(actual.Select(m => m.Name), Is.EqualTo(expected.Select(e => e.name)), type.Name);
            for (int i = 0; i < actual.Length; i++)
                Assert.That((int)actual[i].Attributes, Is.EqualTo(expected[i].flags), type.Name + "." + expected[i].name);
        }

        internal static void AttributeRows(Type type, string field, params Type[] expected)
        {
            FieldInfo info = type.GetField(field, Declared);
            Assert.That(info, Is.Not.Null, field);
            Assert.That(info.GetCustomAttributesData().Select(a => a.AttributeType), Is.EqualTo(expected), field);
        }

        internal static string Tooltip(Type type, string field) => type.GetField(field, Declared).GetCustomAttribute<TooltipAttribute>().tooltip;

        internal static Type Nested(Type type, string name)
        {
            Type nested = type.GetNestedType(name, Declared);
            Assert.That(nested, Is.Not.Null, type.Name + "+" + name);
            return nested;
        }
    }

    // Records IInputButton callbacks as "kind:joystick:input:value:inputType".
    internal sealed class ButtonCallbackLog
    {
        public readonly List<string> Calls = new List<string>();

        public static string Row(string kind, int joystick, GameInput input, float value, InputType inputType) =>
            kind + ":" + joystick + ":" + (int)input + ":" + value.ToString("R", CultureInfo.InvariantCulture) + ":" + (int)inputType;

        public void Held(int joystick, GameInput input, float value, InputType inputType) => Calls.Add(Row("held", joystick, input, value, inputType));
        public void Down(int joystick, GameInput input, float value, InputType inputType) => Calls.Add(Row("down", joystick, input, value, inputType));
        public void Up(int joystick, GameInput input, float value, InputType inputType) => Calls.Add(Row("up", joystick, input, value, inputType));

        public void Attach(IInputButton button)
        {
            button.ButtonHandlers += Held;
            button.ButtonDownHandlers += Down;
            button.ButtonUpHandlers += Up;
        }
    }

    // A fixture's leases and owned objects, released newest first after the body; the
    // body's own failure, else the first release failure, is the result.
    internal sealed class GameInputScope
    {
        private static readonly FieldInfo CoroutineHost =
            typeof(Hardlight.Utils.CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly List<Action> m_release = new List<Action>();
        private readonly Dictionary<string, VerificationFixtureOwnership.LifecycleRow> m_rows =
            new Dictionary<string, VerificationFixtureOwnership.LifecycleRow>();
        private readonly HashSet<ISystem> m_registered = new HashSet<ISystem>();

        public static void Run(Action<GameInputScope> body)
        {
            var scope = new GameInputScope();
            VerificationFixtureOwnership.RunThenCleanUp(() => body(scope), scope.Release);
        }

        private void Release()
        {
            Exception first = null;
            for (int i = m_release.Count - 1; i >= 0; --i)
            {
                try { m_release[i](); }
                catch (Exception failure) { first = first ?? failure; }
            }
            m_release.Clear();
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }

        public void Defer(Action release) => m_release.Add(release);

        // An absent or idle registry row for T, admitted once per scope; rows the proof
        // creates are removed once idle.
        public VerificationFixtureOwnership.LifecycleRow Row<T>() where T : class, ISystem
        {
            string name = ProcessManager.GetDefaultName<T>();
            if (m_rows.TryGetValue(name, out VerificationFixtureOwnership.LifecycleRow admitted)) return admitted;
            var row = new VerificationFixtureOwnership.LifecycleRow(name);
            m_rows.Add(name, row);
            Defer(row.Dispose);
            return row;
        }

        public GameObject Inactive(string name, params Type[] components)
        {
            var owner = new GameObject(name);
            Defer(() => Object.DestroyImmediate(owner));
            owner.SetActive(false);
            foreach (Type component in components) owner.AddComponent(component);
            return owner;
        }

        public T Own<T>(T value) where T : Object
        {
            Defer(() => { if (value != null) Object.DestroyImmediate(value); });
            return value;
        }

        // Registers an owned system under its default name, only into a row this scope admitted
        // that still holds no system; it is unregistered before the row is released.
        public void Register(ISystem system)
        {
            string name = ProcessManager.GetDefaultName(system);
            if (!m_rows.ContainsKey(name))
                throw new InvalidOperationException("Fixture registers only into a row it admitted: " + name);
            if (!ProcessManager.IsSystemNull(name))
                throw new InvalidOperationException("Fixture row already holds a system: " + name);
            ProcessManager.RegisterSystem(system);
            m_registered.Add(system);
            Defer(() => ProcessManager.UnregisterSystem(system));
        }

        // Unregisters a system this scope registered, ahead of its deferred release, which then
        // finds nothing to remove; Register can put it back into the same row.
        public void Unregister(ISystem system)
        {
            if (!m_registered.Contains(system))
                throw new InvalidOperationException("Fixture unregisters only a system it registered: " + ProcessManager.GetDefaultName(system));
            ProcessManager.UnregisterSystem(system);
        }

        // An InputSystem on an inactive owner (its Awake never runs) holding an owned mapping,
        // registered in the scope's InputSystem row.
        public ControlMapping RegisterInputSystem()
        {
            Row<InputSystem>();
            InputSystem system = Inactive("Lucid owned input system").AddComponent<InputSystem>();
            ControlMapping mapping = Own(ScriptableObject.CreateInstance<ControlMapping>());
            ModernUITestSupport.Set(system, "m_controlMapping", mapping);
            Register(system);
            return mapping;
        }

        // CoroutineUtils.OnNextFrame (0x06001175) dereferences the static host without a guard.
        // The host is leased as absent for the rest of the scope: CoroutineUtils.Awake publishes
        // s_instance and nothing clears it, so an earlier host would otherwise decide the result.
        // Release restores the prior value, and reports a host published while the scope ran.
        public void WithoutCoroutineHost()
        {
            object prior = CoroutineHost.GetValue(null);
            CoroutineHost.SetValue(null, null);
            Defer(() =>
            {
                object current = CoroutineHost.GetValue(null);
                CoroutineHost.SetValue(null, prior);
                if (current != null) throw new InvalidOperationException("A CoroutineUtils host was published during the fixture");
            });
        }
    }

    // ControlMapping's scalar callback tables for inputs that may be defined, such as
    // SkipTheSplash: admission requires that no table holds a key for them, and release
    // removes every key added for them and checks the other keys are untouched.
    internal sealed class ControlTableLease
    {
        private readonly GameInput[] m_owned;
        private readonly int[] m_counts;

        public static void Run(Action body, params GameInput[] owned)
        {
            var lease = new ControlTableLease(owned);
            VerificationFixtureOwnership.RunThenCleanUp(body, lease.Release);
        }

        private ControlTableLease(GameInput[] owned)
        {
            m_owned = owned;
            m_counts = new int[ControlTables.All.Length];
            for (int i = 0; i < ControlTables.All.Length; ++i)
            {
                IDictionary map = OwnedControlInputs.Map(ControlTables.All[i]);
                if (map.Keys.Cast<object>().Any(key => m_owned.Contains(ControlTables.KeyInput(key))))
                    throw new InvalidOperationException("Fixture input already has a shared key: " + ControlTables.All[i]);
                m_counts[i] = map.Count;
            }
        }

        private void Release()
        {
            var changed = new List<string>();
            for (int i = 0; i < ControlTables.All.Length; ++i)
            {
                IDictionary map = OwnedControlInputs.Map(ControlTables.All[i]);
                foreach (object key in map.Keys.Cast<object>().Where(key => m_owned.Contains(ControlTables.KeyInput(key))).ToArray()) map.Remove(key);
                if (map.Count != m_counts[i]) changed.Add(ControlTables.All[i]);
            }
            if (changed.Count != 0) throw new InvalidOperationException("Shared ControlMapping state changed: " + string.Join(", ", changed));
        }
    }

    // Live callbacks per ControlMapping table for one input, as "DeclaringType.Method@joystick".
    internal static class ControlTables
    {
        internal static readonly string[] All =
        {
            "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis", "s_onAxisStart", "s_onAxisEnd", "s_onSwipe", "s_onScrollWheel",
        };

        // The input a table key is for.
        internal static GameInput KeyInput(object key) => (GameInput)key.GetType().GetField("GameInput").GetValue(key);

        internal static Dictionary<string, string[]> Subscriptions(GameInput input, object target)
        {
            var result = new Dictionary<string, string[]>();
            foreach (string map in All)
            {
                var listeners = new List<string>();
                foreach (DictionaryEntry row in OwnedControlInputs.Map(map))
                {
                    if (KeyInput(row.Key) != input) continue;
                    int joystick = (int)row.Key.GetType().GetField("JoystickIndex").GetValue(row.Key);
                    foreach (Action<float> listener in ((FastAction<float>)row.Value).GetInvocationList())
                        if (ReferenceEquals(listener.Target, target))
                            listeners.Add(listener.Method.DeclaringType.Name + "." + listener.Method.Name + "@" + joystick);
                }
                if (listeners.Count != 0) result.Add(map, listeners.ToArray());
            }
            return result;
        }

        // Tables Subscribe (0x06000088) fills for each trigger, swipe and scroll wheel always last.
        internal static Dictionary<string, string[]> Expected(InputTrigger trigger, string listener)
        {
            string[] maps = trigger == InputTrigger.Held ? new[] { "s_onButtonsHeld", "s_onAxis" }
                : trigger == InputTrigger.Down ? new[] { "s_onButtonsDown", "s_onAxisStart" }
                : new[] { "s_onButtonsUp", "s_onAxisEnd" };
            return maps.Concat(new[] { "s_onSwipe", "s_onScrollWheel" }).ToDictionary(map => map, map => new[] { listener + "@-1" });
        }
    }
}
