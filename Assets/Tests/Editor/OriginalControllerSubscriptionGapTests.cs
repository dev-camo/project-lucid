using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of InputSubscription.ApplyModifiers (Game.Runtime 060023da,
    // x86-64 0x58b1c0) and InputActionSubscription.Subscribe/Unsubscribe
    // (060023be/bf, x86-64 0x58a760/0x58a920), which ControllerActionTests
    // observe only through the callbacks or an emission snapshot.
    [TestFixture]
    public sealed class OriginalControllerSubscriptionGapTests
    {
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        // Undefined enum value owned by this fixture; no shared key may use it.
        static readonly GameInput Owned = (GameInput)(int.MinValue + 0x4C5A);
        static readonly string[] Maps =
        {
            "s_onPointer", "s_onScrollWheel", "s_onTap", "s_onTouch", "s_onTouchRelease", "s_onMultiTouch", "s_onMultiTouchRelease",
            "s_onVectorisedGameInput", "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis", "s_onAxisStart", "s_onAxisEnd", "s_onSwipe",
        };

        // Test-only subclasses use the genuine protected constructors.
        sealed class Probe : InputSubscription
        {
            public Probe(GameControlsBinding binding, ValidatorFunc validator) : base(binding, validator) { }
            public bool Apply(ref float value) => ApplyModifiers(ref value);
            public override void Subscribe() { }
            public override void Unsubscribe() { }
        }
        sealed class Recorder : InputActionSubscription
        {
            public readonly List<string> Trace = new List<string>();
            public Recorder(GameControlsBinding binding) : base(binding, null) { }
            protected override void OnDown(float value) => Trace.Add("down");
            protected override void OnHeld(float value) => Trace.Add("held");
            protected override void OnUp(float value) => Trace.Add("up");
            protected override void OnDisabled() => Trace.Add("disabled");
        }

        // Native 0x58b1c0 passes the float literal 0.0001 to the float
        // WithinTolerance overload (0x1b04c70), which widens all three operands
        // and uses two strict comparisons, so the exact float bound is outside.
        // Mutations caught: a wider tolerance (0.001/0.01), an inclusive bound,
        // a one-sided check, or calling the validator before the rejection.
        [Test]
        public void ApplyModifiersRejectsOnlyValuesStrictlyInsideTheFloatTolerance()
        {
            int calls = 0; bool result = true;
            var probe = new Probe(new GameControlsBinding(Owned), () => { calls++; return result; });
            foreach (float inside in new[] { 0f, 0.00009f, -0.00009f, 0.0000999f })
            {
                float value = inside;
                Assert.IsFalse(probe.Apply(ref value), "rejected " + inside.ToString("R"));
                Assert.AreEqual(inside, value, "no modifier changes the value");
            }
            Assert.AreEqual(0, calls, "rejection returns before the validator");
            float[] outside = { 0.0001f, -0.0001f, 0.00011f, -0.00011f };
            foreach (float accepted in outside) { float value = accepted; Assert.IsTrue(probe.Apply(ref value), "accepted " + accepted.ToString("R")); }
            Assert.AreEqual(outside.Length, calls, "each accepted value calls the validator exactly once");
            result = false; float rejected = 1f;
            Assert.IsFalse(probe.Apply(ref rejected), "the validator result is returned");
            Assert.AreEqual(outside.Length + 1, calls);
            float unvalidated = 0.00011f;
            Assert.IsTrue(new Probe(new GameControlsBinding(Owned), null).Apply(ref unvalidated), "a null validator accepts (0x58b223)");
        }

        // Native 0x58b1c0 runs GameControlsBinding.ApplyModifiers (0x5478b0)
        // on the referenced value before reading it back for the tolerance.
        // Mutations caught: testing the raw input before the modifiers, not
        // writing the modified value back, or iterating the list in reverse.
        [Test]
        public void ApplyModifiersRunsTheAuthoredListInOrderBeforeTheTolerance()
        {
            var created = new List<Object>();
            try
            {
                ModifierClampGreaterThan clamp = Authored<ModifierClampGreaterThan>(created, "m_minimum", 0.5f);
                ModifierMultiplier zero = Authored<ModifierMultiplier>(created, "m_multiplier", 0f);
                int calls = 0; var binding = new GameControlsBinding(Owned); var probe = new Probe(binding, () => { calls++; return true; });

                Modifiers(binding, clamp); float value = 0f;
                Assert.IsTrue(probe.Apply(ref value), "a raw zero raised by the clamp is accepted");
                Assert.AreEqual(0.5f, value, "the modified value is written back"); Assert.AreEqual(1, calls);

                Modifiers(binding, clamp, zero); value = 1f;
                Assert.IsFalse(probe.Apply(ref value), "clamp then multiply by zero is rejected");
                Assert.AreEqual(0f, value); Assert.AreEqual(1, calls, "rejected before the validator");

                Modifiers(binding, zero, clamp); value = 1f;
                Assert.IsTrue(probe.Apply(ref value), "multiply by zero then clamp is accepted");
                Assert.AreEqual(0.5f, value); Assert.AreEqual(2, calls);
            }
            finally { foreach (Object value in created) Object.DestroyImmediate(value); }
        }

        // Native 0x58a760 calls ControlMapping.Subscribe (0x1a1ae50) with
        // trigger 0 (Held) and then 2 (Up), both with joystick -1, and returns
        // when the cached InputSystem reference is empty; 0x58a920 removes both.
        // Held and Up share the swipe and scroll-wheel lists, so their order is
        // visible there. Mutations caught: Up before Held, the Down trigger, a
        // joystick other than -1, an unguarded lookup, or removing only one.
        [Test]
        public void SubscribeRegistersHeldThenUpAndUnsubscribeRemovesBoth()
        {
            var subscription = new Recorder(new GameControlsBinding(Owned));
            Assert.IsNull(CachedReference().GetSafe(), "fixture requires no registered InputSystem");
            var statics = new StaticCallbackCustody();
            Finish(() =>
            {
                subscription.Subscribe();
                RequireRegistered(subscription);
                subscription.Unsubscribe();
                RequireUnregistered();
                CollectionAssert.IsEmpty(subscription.Trace, "registration dispatches nothing");
            }, statics.Dispose);
            LogAssert.NoUnexpectedReceived();
        }

        // Native 0x58a760 reads the system's mapping without a null check and
        // tail-calls SubscribeInputDisabled (0x1a1bff0) with callImmediately
        // false only after both static registrations; 0x58a920 mirrors it with
        // UnsubscribeInputDisabled. Mutations caught: looking the system up
        // before the static registrations, invoking the callback immediately,
        // skipping the input-disabled removal, or subscribing without a system.
        [Test]
        public void InputDisabledSubscriptionFollowsTheStaticOnesOnlyForARegisteredSystem()
        {
            var subscription = new Recorder(new GameControlsBinding(Owned));
            SystemRef<InputSystem> reference = CachedReference();
            GameObject owner = null; InputSystem system = null; ControlMapping mapping = null; bool registered = false;
            var row = new InputSystemRowCustody(reference); StaticCallbackCustody statics = null;
            Finish(() =>
            {
                statics = new StaticCallbackCustody();
                owner = new GameObject("Owned original controller subscription system"); owner.SetActive(false);
                system = owner.AddComponent<InputSystem>(); Assert.IsNull(system.ControlMapping, "original serialized default");
                InvokePrivate(system, "Awake"); registered = true;
                Assert.AreSame(system, reference.GetSafe(), "Awake publishes the owned system through the cached reference");

                // A present system with no mapping faults only after both static registrations.
                Assert.Throws<NullReferenceException>(subscription.Subscribe);
                RequireRegistered(subscription);
                Assert.Throws<NullReferenceException>(subscription.Unsubscribe);
                RequireUnregistered();

                mapping = ScriptableObject.CreateInstance<ControlMapping>();
                typeof(InputSystem).GetField("m_controlMapping", Declared).SetValue(system, mapping);
                mapping.InitialiseStackableData();
                subscription.Subscribe();
                RequireRegistered(subscription);
                Delegate disabled = DisabledCallback(mapping);
                Assert.IsNotNull(disabled, "input-disabled callback registered");
                Assert.AreEqual(1, disabled.GetInvocationList().Length);
                Assert.AreSame(subscription, disabled.Target); Assert.AreEqual("OnInputDisabled", disabled.Method.Name);
                CollectionAssert.IsEmpty(subscription.Trace, "callImmediately is false");
                subscription.Unsubscribe();
                RequireUnregistered(); Assert.IsNull(DisabledCallback(mapping), "the input-disabled callback and its key are removed");

                // The registered callback is the real OnInputDisabled.
                subscription.Subscribe();
                mapping.ShutdownStackableData();
                CollectionAssert.AreEqual(new[] { "disabled" }, subscription.Trace, "shutdown notifies the subscribed callback once");
                subscription.Unsubscribe(); subscription.Trace.Clear();

                // Once the system is unregistered the same mapping receives nothing.
                InvokePrivate(system, "OnDestroy"); registered = false;
                Assert.IsNull(reference.GetSafe());
                mapping.InitialiseStackableData();
                subscription.Subscribe();
                RequireRegistered(subscription);
                Assert.IsNull(DisabledCallback(mapping), "no input-disabled subscription without a registered system");
                subscription.Unsubscribe(); RequireUnregistered();
                mapping.ShutdownStackableData();
                CollectionAssert.IsEmpty(subscription.Trace);
            },
            () => { if (registered) InvokePrivate(system, "OnDestroy"); },
            () => { if (mapping != null) mapping.ShutdownStackableData(); },
            () => { if (owner != null) Object.DestroyImmediate(owner); },
            () => { if (mapping != null) Object.DestroyImmediate(mapping); },
            () => { if (statics != null) statics.Dispose(); },
            row.RequireReleased);
            LogAssert.NoUnexpectedReceived();
        }

        static T Authored<T>(List<Object> created, string field, float value) where T : InputModifier
        {
            T modifier = ScriptableObject.CreateInstance<T>(); created.Add(modifier);
            FieldInfo info = typeof(T).GetField(field, Declared); Assert.IsNotNull(info, field); info.SetValue(modifier, value); return modifier;
        }
        static void Modifiers(GameControlsBinding binding, params InputModifier[] modifiers) =>
            typeof(GameControlsBinding).GetField("m_modifiers", Declared).SetValue(binding, new List<InputModifier>(modifiers));
        static void InvokePrivate(object target, string name)
        {
            try { target.GetType().GetMethod(name, Declared).Invoke(target, null); }
            catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); }
        }
        static void Finish(Action body, params Action[] cleanup)
        {
            var failures = new List<Exception>(); try { body(); } catch (Exception e) { failures.Add(e); }
            foreach (Action action in cleanup) try { action(); } catch (Exception e) { failures.Add(e); }
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }
        static SystemRef<InputSystem> CachedReference() =>
            (SystemRef<InputSystem>)typeof(InputActionSubscription).GetField("m_inputSystemRef", Declared).GetValue(null);
        static Delegate DisabledCallback(ControlMap mapping)
        {
            var callbacks = (IDictionary)typeof(ControlMap).GetField("m_gameInputsDisabledCallbacks", Declared).GetValue(mapping);
            return callbacks.Contains(Owned) ? (Delegate)callbacks[Owned] : null;
        }

        static IDictionary Map(string name)
        {
            FieldInfo field = typeof(ControlMapping).GetField(name, Declared);
            Assert.IsNotNull(field, name); return (IDictionary)field.GetValue(null);
        }
        // IDictionary enumeration yields DictionaryEntry; LINQ over the
        // non-generic IEnumerable would yield boxed KeyValuePair values.
        static DictionaryEntry[] Entries(IDictionary dictionary) { var rows = new List<DictionaryEntry>(); foreach (DictionaryEntry row in dictionary) rows.Add(row); return rows.ToArray(); }
        static GameInput KeyInput(object key) => (GameInput)key.GetType().GetField("GameInput").GetValue(key);
        static int KeyJoystick(object key) => (int)key.GetType().GetField("JoystickIndex").GetValue(key);
        // Owned keys only; the genuine FastAction list is read, never invoked.
        static Dictionary<int, Delegate[]> OwnedRows(string map)
        {
            var rows = new Dictionary<int, Delegate[]>(); IDictionary dictionary = Map(map);
            foreach (DictionaryEntry row in dictionary)
            {
                if (KeyInput(row.Key) != Owned) continue;
                var list = (IList)row.Value.GetType().GetMethod("GetInvocationList", BindingFlags.Instance | BindingFlags.Public).Invoke(row.Value, null);
                rows.Add(KeyJoystick(row.Key), list.Cast<Delegate>().ToArray());
            }
            return rows;
        }
        static void RequireRegistered(Recorder subscription)
        {
            const string held = "OnCallbackHeld", up = "OnCallbackUp";
            var expected = new Dictionary<string, string[]>
            {
                { "s_onButtonsHeld", new[] { held } }, { "s_onAxis", new[] { held } },
                { "s_onButtonsUp", new[] { up } }, { "s_onAxisEnd", new[] { up } },
                { "s_onSwipe", new[] { held, up } }, { "s_onScrollWheel", new[] { held, up } },
            };
            foreach (string map in Maps)
            {
                Dictionary<int, Delegate[]> rows = OwnedRows(map);
                if (!expected.ContainsKey(map)) { Assert.IsFalse(rows.Values.Any(x => x.Length > 0), map + " has no owned callback"); continue; }
                Assert.IsTrue(rows.Keys.All(x => x == -1), map + " uses only joystick -1");
                Assert.IsTrue(rows.ContainsKey(-1), map + " registered");
                Assert.IsTrue(rows[-1].All(x => ReferenceEquals(x.Target, subscription)), map + " callbacks target the subscription");
                CollectionAssert.AreEqual(expected[map], rows[-1].Select(x => x.Method.Name).ToArray(), map + " ordered callbacks");
            }
        }
        static void RequireUnregistered()
        {
            foreach (string map in Maps) Assert.IsFalse(OwnedRows(map).Values.Any(x => x.Length > 0), map + " retains no owned callback");
        }

        // The static dictionaries are shared. Require no key for the owned
        // input at entry, then remove only owned keys and verify the rest.
        sealed class StaticCallbackCustody : IDisposable
        {
            readonly DictionaryEntry[][] before;
            public StaticCallbackCustody()
            {
                before = Maps.Select(map => Entries(Map(map))).ToArray();
                for (int i = 0; i < Maps.Length; i++) if (before[i].Any(row => KeyInput(row.Key) == Owned)) throw new InvalidOperationException("Owned controller input already has a shared key: " + Maps[i]);
                if (Enum.IsDefined(typeof(GameInput), Owned)) throw new InvalidOperationException("Owned controller input must be undefined");
            }
            public void Dispose()
            {
                for (int i = 0; i < Maps.Length; i++)
                {
                    IDictionary dictionary = Map(Maps[i]);
                    foreach (object key in dictionary.Keys.Cast<object>().Where(key => KeyInput(key) == Owned).ToArray()) dictionary.Remove(key);
                    if (dictionary.Count != before[i].Length || before[i].Any(row => !dictionary.Contains(row.Key) || !ReferenceEquals(dictionary[row.Key], row.Value)))
                        throw new InvalidOperationException("Shared controller callback map changed: " + Maps[i]);
                }
            }
        }

        // Borrow the live InputSystem row without writing its references or
        // events: refuse unless every cached reference is empty and has no
        // observer, so registering the owned system runs no foreign callback.
        sealed class InputSystemRowCustody
        {
            readonly IDictionary registry; readonly string name; readonly object row; readonly object[] references;
            public InputSystemRowCustody(SystemRef<InputSystem> cached)
            {
                registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", Declared).GetValue(null);
                name = typeof(InputSystem).ToString(); row = registry[name];
                if (row == null) throw new InvalidOperationException("Cached InputSystem reference has no registry row");
                var cache = (IDictionary)row.GetType().GetField("SystemRefDictionary", Declared).GetValue(row);
                references = new[] { row.GetType().GetField("SystemRef", Declared).GetValue(row) }.Concat(cache.Values.Cast<object>()).ToArray();
                if (!references.Contains(cached)) throw new InvalidOperationException("Cached InputSystem reference is not in its row");
                RequireReleased();
            }
            public void RequireReleased()
            {
                if (!ReferenceEquals(registry[name], row)) throw new InvalidOperationException("InputSystem registry row replaced");
                foreach (object reference in references)
                {
                    Type owner = reference.GetType(); while (!owner.IsGenericType || owner.GetGenericTypeDefinition() != typeof(SystemRef<>)) owner = owner.BaseType;
                    foreach (string field in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                        if (owner.GetField(field, Declared).GetValue(reference) != null) throw new InvalidOperationException("InputSystem reference is borrowed: " + field);
                }
            }
        }
    }
}
