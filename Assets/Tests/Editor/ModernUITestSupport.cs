using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Reflection and ownership helpers shared by the HLModernUI widget fixtures.
    // Every component lives on an inactive, fixture-owned GameObject, so Unity
    // runs none of its messages; the tests call the original methods directly.
    internal static class ModernUITestSupport
    {
        internal const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static FieldInfo Field(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        internal static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
        internal static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        // Calls the method declared on the given type, so a private base method is reached
        // even when a subclass declares one with the same name.
        internal static object Call(Type declaring, object target, string name, params object[] arguments)
        {
            MethodInfo method = declaring.GetMethod(name, Instance | BindingFlags.DeclaredOnly)
                ?? throw new MissingMethodException(declaring.FullName, name);
            try { return method.Invoke(target, arguments); }
            catch (TargetInvocationException failure) { ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); throw; }
        }

        internal static object Call(object target, string name, params object[] arguments) => Call(target.GetType(), target, name, arguments);
    }

    // Destroys every owned object, newest first, even after a failure.
    internal sealed class OwnedObjects : IDisposable
    {
        private readonly List<Object> m_objects = new List<Object>();

        public GameObject Inactive(string name)
        {
            var owner = new GameObject(name);
            owner.SetActive(false);
            m_objects.Add(owner);
            return owner;
        }

        public T Own<T>(T value) where T : Object
        {
            m_objects.Add(value);
            return value;
        }

        public void Dispose()
        {
            for (int i = m_objects.Count - 1; i >= 0; --i)
                if (m_objects[i] != null) Object.DestroyImmediate(m_objects[i]);
            m_objects.Clear();
        }
    }

    // ControlMapping's scalar callback tables are shared static dictionaries whose
    // keys are never removed by Unsubscribe (UnsubscribeInternal leaves the entry).
    // The fixture owns undefined GameInput values; Run executes the body, then removes
    // every key for them and requires each table to hold exactly its admitted keys and
    // actions. That report runs as a RunThenCleanUp step, so it never replaces the
    // body's own first failure.
    internal sealed class OwnedControlInputs
    {
        private static readonly string[] Maps =
        {
            "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis", "s_onAxisStart", "s_onAxisEnd", "s_onSwipe", "s_onScrollWheel",
        };

        private readonly GameInput[] m_owned;
        private readonly DictionaryEntry[][] m_before;
        private readonly GameInput[] m_exclusiveBefore;

        public static void Run(Action body, params GameInput[] owned)
        {
            var inputs = new OwnedControlInputs(owned);
            VerificationFixtureOwnership.RunThenCleanUp(body, inputs.Release);
        }

        private OwnedControlInputs(params GameInput[] owned)
        {
            m_owned = owned;
            foreach (GameInput input in owned)
                if (Enum.IsDefined(typeof(GameInput), input)) throw new InvalidOperationException("Owned input must be undefined: " + (int)input);
            m_before = Maps.Select(map => Entries(Map(map))).ToArray();
            for (int i = 0; i < Maps.Length; ++i)
                if (m_before[i].Any(row => m_owned.Contains(KeyInput(row.Key)))) throw new InvalidOperationException("Owned input already has a shared key: " + Maps[i]);
            m_exclusiveBefore = Exclusive().ToArray();
            if (m_exclusiveBefore.Any(m_owned.Contains)) throw new InvalidOperationException("Owned input is already exclusive");
        }

        public static IDictionary Map(string name) =>
            (IDictionary)typeof(ControlMapping).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        public static HashSet<GameInput> Exclusive() =>
            (HashSet<GameInput>)typeof(ControlMapping).GetField("s_exclusiveGameInputs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static DictionaryEntry[] Entries(IDictionary dictionary)
        {
            var rows = new List<DictionaryEntry>();
            foreach (DictionaryEntry row in dictionary) rows.Add(row);
            return rows.ToArray();
        }

        private static GameInput KeyInput(object key) => (GameInput)key.GetType().GetField("GameInput").GetValue(key);
        private static int KeyJoystick(object key) => (int)key.GetType().GetField("JoystickIndex").GetValue(key);

        // "Method@joystick" for each listener on an owned input in one table, in list order.
        public static string[] Listeners(string map, GameInput input)
        {
            var listeners = new List<string>();
            foreach (DictionaryEntry row in Map(map))
            {
                if (KeyInput(row.Key) != input) continue;
                foreach (Action<float> listener in ((FastAction<float>)row.Value).GetInvocationList())
                    listeners.Add(listener.Method.Name + "@" + KeyJoystick(row.Key));
            }
            return listeners.ToArray();
        }

        private void Release()
        {
            HashSet<GameInput> exclusive = Exclusive();
            foreach (GameInput input in m_owned) exclusive.Remove(input);
            var changed = new List<string>();
            for (int i = 0; i < Maps.Length; ++i)
            {
                IDictionary dictionary = Map(Maps[i]);
                foreach (object key in dictionary.Keys.Cast<object>().Where(key => m_owned.Contains(KeyInput(key))).ToArray()) dictionary.Remove(key);
                if (dictionary.Count != m_before[i].Length || m_before[i].Any(row => !dictionary.Contains(row.Key) || !ReferenceEquals(dictionary[row.Key], row.Value)))
                    changed.Add(Maps[i]);
            }
            if (!exclusive.SetEquals(m_exclusiveBefore)) changed.Add("s_exclusiveGameInputs");
            if (changed.Count != 0) throw new InvalidOperationException("Shared ControlMapping state changed: " + string.Join(", ", changed));
        }
    }

    // StringTable.GetString (0x1A355E0) reads MonoSingleton<StringTable>.Instance.m_strings.
    // The lease requires no live instance, installs an owned table on an inactive GameObject
    // (so Awake, Start and OnDestroy never run and nothing is registered) holding the given
    // entries, and puts back the null instance before destroying the owner.
    internal static class OwnedStringTable
    {
        private static readonly FieldInfo Singleton =
            typeof(MonoSingleton<StringTable>).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

        public static void Run(Dictionary<Strings, string> contents, Action body)
        {
            if (Singleton.GetValue(null) != null) throw new InvalidOperationException("Fixture requires no live StringTable instance");
            var owner = new GameObject("Lucid owned inactive string table");
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                owner.SetActive(false);
                StringTable table = owner.AddComponent<StringTable>();
                var strings = new Dictionary<int, StringTable.StringEntry>();
                foreach (KeyValuePair<Strings, string> entry in contents)
                    strings.Add((int)entry.Key, new StringTable.StringEntry("owned-" + (int)entry.Key, entry.Value, 0));
                ModernUITestSupport.Set(table, "m_strings", strings);
                Singleton.SetValue(null, table);
                body();
            }, () => Singleton.SetValue(null, null), () => Object.DestroyImmediate(owner));
        }
    }
}
