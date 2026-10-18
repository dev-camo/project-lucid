using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
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
    // The fixture owns undefined GameInput values; Dispose removes every key for them
    // and then requires each table to hold exactly its admitted keys and actions.
    internal sealed class OwnedControlInputs : IDisposable
    {
        private static readonly string[] Maps =
        {
            "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis", "s_onAxisStart", "s_onAxisEnd", "s_onSwipe", "s_onScrollWheel",
        };

        private readonly GameInput[] m_owned;
        private readonly DictionaryEntry[][] m_before;
        private readonly GameInput[] m_exclusiveBefore;

        public OwnedControlInputs(params GameInput[] owned)
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

        public void Dispose()
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

    // Behaviour of Hardlight.InputControlledSlider (HLModernUI.Runtime 02000037,
    // 06000180..0600018f) and Hardlight.InputControlledSliderSeeker (02000039,
    // 06000190..06000195), from the 1.10.1 x86-64 bodies cited per test.
    [TestFixture]
    public sealed class OriginalModernUIInputSliderTests
    {
        private static readonly GameInput OwnedPositive = (GameInput)(int.MinValue + 0x4D31);
        private static readonly GameInput OwnedNegative = (GameInput)(int.MinValue + 0x4D32);

        private static T MakeSlider<T>(OwnedObjects owned, out Slider slider) where T : InputControlledSlider
        {
            GameObject owner = owned.Inactive("Lucid owned input slider");
            slider = owner.AddComponent<Slider>();
            slider.minValue = -10f;
            slider.maxValue = 10f;
            slider.value = 0f;
            var component = owner.AddComponent<T>();
            ModernUITestSupport.Set(component, "m_slider", slider);
            ModernUITestSupport.Set(component, "m_inputPositive", OwnedPositive);
            ModernUITestSupport.Set(component, "m_inputNegative", OwnedNegative);
            return component;
        }

        // .ctor 0x1A483F0 writes the 64-bit pair 0x1_3F800000 at +0x34: m_maxInputValue 1f
        // and m_inputTriggerType 1 (Down); m_minInputValue stays 0. The sealed seeker's
        // .ctor 0x1A48800 inlines the same write.
        [Test]
        public void ConstructorsSeedTheMaximumAndTheDownTrigger()
        {
            using (var owned = new OwnedObjects())
            {
                foreach (InputControlledSlider component in new InputControlledSlider[]
                {
                    owned.Inactive("Lucid owned slider defaults").AddComponent<InputControlledSlider>(),
                    owned.Inactive("Lucid owned seeker defaults").AddComponent<InputControlledSliderSeeker>(),
                })
                {
                    Assert.That(ModernUITestSupport.Get(component, "m_minInputValue"), Is.EqualTo(0f));
                    Assert.That(ModernUITestSupport.Get(component, "m_maxInputValue"), Is.EqualTo(1f));
                    Assert.That(ModernUITestSupport.Get(component, "m_inputTriggerType"), Is.EqualTo(InputTrigger.Down));
                    Assert.That(ModernUITestSupport.Get(component, "m_isSelected"), Is.False);
                }
            }
        }

        // TriggerInputEvent 0x1A48290: |v| clamped to [min, max] (minss with max, then the
        // cmpltss min select), negated unless 0 <= v (cmpnless), added to the slider's
        // virtual value (slots 46/47). OnPositiveInput 0x1A481F0 inlines it for |v| (negated
        // only for NaN); OnNegativeInput 0x1A48330 inlines it for -|v|, negated when v != 0,
        // so a zero negative input moves the slider up by the minimum. min 0.25, max 0.75.
        // Mutations caught: an unclamped or unsigned delta, Mathf.Sign(0) == 0, a negative
        // input that negates zero, or a positive input that keeps the sign.
        [Test]
        public void InputEventsMoveTheSliderByTheSignedClampedMagnitude()
        {
            using (var owned = new OwnedObjects())
            {
                InputControlledSlider component = MakeSlider<InputControlledSlider>(owned, out Slider slider);
                ModernUITestSupport.Set(component, "m_minInputValue", 0.25f);
                ModernUITestSupport.Set(component, "m_maxInputValue", 0.75f);
                ModernUITestSupport.Set(component, "m_isSelected", true);
                var cases = new (string method, float input, float delta)[]
                {
                    ("TriggerInputEvent", 0.5f, 0.5f), ("TriggerInputEvent", -0.5f, -0.5f),
                    ("TriggerInputEvent", 2f, 0.75f), ("TriggerInputEvent", -2f, -0.75f),
                    ("TriggerInputEvent", 0.125f, 0.25f), ("TriggerInputEvent", -0.125f, -0.25f),
                    ("TriggerInputEvent", 0f, 0.25f), ("TriggerInputEvent", -0f, 0.25f),
                    ("OnPositiveInput", -0.5f, 0.5f), ("OnPositiveInput", 2f, 0.75f), ("OnPositiveInput", 0f, 0.25f),
                    ("OnNegativeInput", 0.5f, -0.5f), ("OnNegativeInput", -2f, -0.75f), ("OnNegativeInput", 0.125f, -0.25f),
                    ("OnNegativeInput", 0f, 0.25f), ("OnNegativeInput", -0f, 0.25f),
                };
                foreach (var (method, input, delta) in cases)
                {
                    slider.value = 1f;
                    ModernUITestSupport.Call(typeof(InputControlledSlider), component, method, input);
                    Assert.That(slider.value, Is.EqualTo(1f + delta), method + "(" + input.ToString("R") + ")");
                }

                // Every entry point returns before reading the slider while unselected.
                ModernUITestSupport.Set(component, "m_isSelected", false);
                foreach (string method in new[] { "TriggerInputEvent", "OnPositiveInput", "OnNegativeInput" })
                {
                    slider.value = 1f;
                    ModernUITestSupport.Call(typeof(InputControlledSlider), component, method, 0.5f);
                    Assert.That(slider.value, Is.EqualTo(1f), method + " while unselected");
                }
            }
        }

        // OnDeselect 0x1A481D0 and ClearSelected 0x1A480A0 clear m_isSelected and pass the
        // handle to SafeStopCoroutine, which leaves a null handle untouched.
        [Test]
        public void DeselectAndClearSelectedDropTheSelection()
        {
            using (var owned = new OwnedObjects())
            {
                InputControlledSlider component = MakeSlider<InputControlledSlider>(owned, out _);
                ModernUITestSupport.Set(component, "m_isSelected", true);
                component.OnDeselect(null);
                Assert.That(ModernUITestSupport.Get(component, "m_isSelected"), Is.False, "OnDeselect");
                ModernUITestSupport.Set(component, "m_isSelected", true);
                ModernUITestSupport.Call(typeof(InputControlledSlider), component, "ClearSelected");
                Assert.That(ModernUITestSupport.Get(component, "m_isSelected"), Is.False, "ClearSelected");
                Assert.That(ModernUITestSupport.Get(component, "m_delayedSelected"), Is.Null);
            }
        }

        // <DelaySelected>d__16.MoveNext 0x1A48420: state 0 yields a new WaitForEndOfFrame and
        // leaves the selection alone; state 1 sets m_isSelected and nulls m_delayedSelected
        // (+0x3c/+0x40) and finishes. Reset 0x1A484F0 throws NotSupportedException.
        [Test]
        public void DelaySelectedSelectsOnlyAfterTheEndOfFrame()
        {
            using (var owned = new OwnedObjects())
            {
                InputControlledSlider component = MakeSlider<InputControlledSlider>(owned, out _);
                var routine = (IEnumerator)ModernUITestSupport.Call(typeof(InputControlledSlider), component, "DelaySelected");
                Assert.That(ModernUITestSupport.Get(component, "m_isSelected"), Is.False, "creating the iterator runs nothing");
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.InstanceOf<WaitForEndOfFrame>());
                Assert.That(ModernUITestSupport.Get(component, "m_isSelected"), Is.False, "not selected before the frame ends");
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(ModernUITestSupport.Get(component, "m_isSelected"), Is.True);
                Assert.That(ModernUITestSupport.Get(component, "m_delayedSelected"), Is.Null);
                Assert.That(routine.MoveNext(), Is.False, "finished");
                Assert.Throws<NotSupportedException>(routine.Reset);
            }
        }

        // OnEnable 0x1A47EA0 subscribes OnPositiveInput then OnNegativeInput with the
        // serialized trigger (+0x38) and joystick -1; OnDisable 0x1A47FA0 unsubscribes both
        // with joystick -1 and clears the selection. Held routes to the held and axis tables,
        // and every trigger also reaches the swipe and scroll-wheel tables.
        // Mutations caught: a hard-coded Down trigger, a swapped input, a missing removal or
        // an OnDisable that keeps the selection.
        [Test]
        public void EnableSubscribesBothInputsWithTheSerializedTriggerAndDisableRemovesThem()
        {
            using (var owned = new OwnedObjects())
            using (new OwnedControlInputs(OwnedPositive, OwnedNegative))
            {
                InputControlledSlider component = MakeSlider<InputControlledSlider>(owned, out _);
                ModernUITestSupport.Set(component, "m_inputTriggerType", InputTrigger.Held);
                ModernUITestSupport.Call(typeof(InputControlledSlider), component, "OnEnable");
                foreach (string map in new[] { "s_onButtonsHeld", "s_onAxis", "s_onSwipe", "s_onScrollWheel" })
                {
                    Assert.That(OwnedControlInputs.Listeners(map, OwnedPositive), Is.EqualTo(new[] { "OnPositiveInput@-1" }), map);
                    Assert.That(OwnedControlInputs.Listeners(map, OwnedNegative), Is.EqualTo(new[] { "OnNegativeInput@-1" }), map);
                }
                foreach (string map in new[] { "s_onButtonsDown", "s_onAxisStart", "s_onButtonsUp", "s_onAxisEnd" })
                    Assert.That(OwnedControlInputs.Listeners(map, OwnedPositive).Concat(OwnedControlInputs.Listeners(map, OwnedNegative)), Is.Empty, map);

                ModernUITestSupport.Set(component, "m_isSelected", true);
                ModernUITestSupport.Call(typeof(InputControlledSlider), component, "OnDisable");
                foreach (string map in new[] { "s_onButtonsHeld", "s_onAxis", "s_onSwipe", "s_onScrollWheel" })
                    Assert.That(OwnedControlInputs.Listeners(map, OwnedPositive).Concat(OwnedControlInputs.Listeners(map, OwnedNegative)), Is.Empty, map);
                Assert.That(ModernUITestSupport.Get(component, "m_isSelected"), Is.False);
            }
        }

        // Seeker OnEnable 0x1A48540 calls the base OnEnable first, then subscribes positive
        // Down (1), positive Up (2), negative Down, negative Up. The base OnDisable 0x1A47FA0
        // is private and only removes the two base callbacks, so the seeker's four remain.
        [Test]
        public void SeekerSubscribesDownAndUpAfterTheBaseAndKeepsThemOnDisable()
        {
            using (var owned = new OwnedObjects())
            using (new OwnedControlInputs(OwnedPositive, OwnedNegative))
            {
                InputControlledSliderSeeker seeker = MakeSlider<InputControlledSliderSeeker>(owned, out _);
                ModernUITestSupport.Call(typeof(InputControlledSliderSeeker), seeker, "OnEnable");
                Assert.That(OwnedControlInputs.Listeners("s_onButtonsDown", OwnedPositive), Is.EqualTo(new[] { "OnPositiveInput@-1", "OnPositiveInputDown@-1" }));
                Assert.That(OwnedControlInputs.Listeners("s_onButtonsUp", OwnedPositive), Is.EqualTo(new[] { "OnPositiveInputUp@-1" }));
                Assert.That(OwnedControlInputs.Listeners("s_onButtonsDown", OwnedNegative), Is.EqualTo(new[] { "OnNegativeInput@-1", "OnNegativeInputDown@-1" }));
                Assert.That(OwnedControlInputs.Listeners("s_onButtonsUp", OwnedNegative), Is.EqualTo(new[] { "OnNegativeInputUp@-1" }));
                Assert.That(OwnedControlInputs.Listeners("s_onSwipe", OwnedPositive),
                    Is.EqualTo(new[] { "OnPositiveInput@-1", "OnPositiveInputDown@-1", "OnPositiveInputUp@-1" }), "base first, then Down, then Up");

                ModernUITestSupport.Call(typeof(InputControlledSlider), seeker, "OnDisable");
                Assert.That(OwnedControlInputs.Listeners("s_onButtonsDown", OwnedPositive), Is.EqualTo(new[] { "OnPositiveInputDown@-1" }));
                Assert.That(OwnedControlInputs.Listeners("s_onButtonsUp", OwnedNegative), Is.EqualTo(new[] { "OnNegativeInputUp@-1" }));
                Assert.That(OwnedControlInputs.Listeners("s_onSwipe", OwnedNegative), Is.EqualTo(new[] { "OnNegativeInputDown@-1", "OnNegativeInputUp@-1" }));
            }
        }

        // OnPositiveInputDown/OnNegativeInputDown (0x1A486E0/0x1A48770) invoke m_onInputDown
        // (+0x48) only while selected; the Up handlers (0x1A48700/0x1A48790) pass the slider's
        // current value, not their argument, to m_onInputUp (+0x50). Null events are skipped.
        [Test]
        public void SeekerHandlersRaiseTheirEventsOnlyWhileSelected()
        {
            using (var owned = new OwnedObjects())
            {
                InputControlledSliderSeeker seeker = MakeSlider<InputControlledSliderSeeker>(owned, out Slider slider);
                var trace = new List<string>();
                var down = new UnityEvent();
                down.AddListener(() => trace.Add("down"));
                var up = new UnityEvent<float>();
                up.AddListener(value => trace.Add("up " + value.ToString("R")));
                ModernUITestSupport.Set(seeker, "m_onInputDown", down);
                ModernUITestSupport.Set(seeker, "m_onInputUp", up);
                slider.value = 3f;
                string[] handlers = { "OnPositiveInputDown", "OnPositiveInputUp", "OnNegativeInputDown", "OnNegativeInputUp" };

                foreach (string handler in handlers) ModernUITestSupport.Call(typeof(InputControlledSliderSeeker), seeker, handler, 7f);
                Assert.That(trace, Is.Empty, "unselected");

                ModernUITestSupport.Set(seeker, "m_isSelected", true);
                foreach (string handler in handlers) ModernUITestSupport.Call(typeof(InputControlledSliderSeeker), seeker, handler, 7f);
                Assert.That(trace, Is.EqualTo(new[] { "down", "up 3", "down", "up 3" }));

                ModernUITestSupport.Set(seeker, "m_onInputDown", null);
                ModernUITestSupport.Set(seeker, "m_onInputUp", null);
                foreach (string handler in handlers)
                    Assert.DoesNotThrow(() => ModernUITestSupport.Call(typeof(InputControlledSliderSeeker), seeker, handler, 7f), handler + " with a null event");
            }
        }
    }
}
