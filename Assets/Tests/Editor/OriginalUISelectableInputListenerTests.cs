using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using NUnit.Framework;
using ProjectLucid.Tests.UISelectionShared;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectLucid.Tests
{
    // Hardlight.UISelectableInputListener (HLUnityUI.Runtime, TypeDef 0x02000082), x86-64 bodies
    // 0x1b59c60..0x1b5a870. Every case runs on an inactive owned listener, so Awake never queries
    // ProcessManager; the shipped static InputBridge.OnShutdown is restored after each case.
    // Not covered here: Awake (needs a registered UISelectableController and UIRuntimeConfiguration),
    // the controller OnSelect/OnDeselect subscription and inlined IsCurrentSelection path of Setup
    // (0x1b59e68..0x1b59fe3, needs an owned UISelectableController).
    public sealed class OriginalUISelectableInputListenerTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Flags, member order and attribute rows from dump.cs TypeDefIndex 11954..11957 and the
        // custom-attribute rows they print. <>c__DisplayClass12_0 is the shipped capture class name;
        // it requires both nested types to be declared before ButtonSelected (their exact source
        // position is inferred).
        [Test]
        public void DeclarationsKeepOriginalFlagsSignaturesAndAttributeRows()
        {
            Type type = typeof(UISelectableInputListener);
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            SetOptions(type);
            Fields(type, new[] { "m_selectableToWatch", "m_buttonInputData", "m_selectableController", "m_delegates", "m_inputBridge" },
                new[] { typeof(UISelectable), typeof(UISelectableInputListener.SelectedButtonInput[]), typeof(SystemRef<UISelectableController>),
                    typeof(List<Action<float>>), typeof(InputBridge) },
                new[] { 1, 1, 1, 1, 1 });
            AttributeRows(type.GetField("m_selectableToWatch", Declared), typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type.GetField("m_buttonInputData", Declared), typeof(SerializeField), typeof(TooltipAttribute));
            Assert.That(type.GetField("m_selectableToWatch", Declared).GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("The selectable to watch, can be the same as the button to be triggered by needs to be defined separately"));
            Assert.That(type.GetField("m_buttonInputData", Declared).GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("Buttons to trigger, events to fire and inputs to listen to are all defined here"));
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(10));
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Method(type, "Awake", 129, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "OnEnable", 129, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "OnDisable", 129, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "Setup", 129, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "ClearUp", 129, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "ButtonSelected", 129, typeof(void), new[] { typeof(UISelectable) }, new[] { "selected" });
            Method(type, "ProcessTriggers", 129, typeof(void), new[] { typeof(float), typeof(InputSupplier) }, new[] { "value", "inputSupplier" });
            Method(type, "ButtonDeselected", 129, typeof(void), new[] { typeof(UISelectable) }, new[] { "selected" });
            Method(type, "GetButtonInputData", 134, typeof(UISelectableInputListener.SelectedButtonInput[]), Type.EmptyTypes, new string[0]);
            Method(type, ".ctor", 6278, null, Type.EmptyTypes, new string[0]);
            Assert.That(type.GetNestedTypes(Declared).Select(t => t.Name).OrderBy(n => n),
                Is.EqualTo(new[] { "<>c__DisplayClass12_0", "ISelectedButtonInputTextProvider", "SelectedButtonInput" }));

            type = typeof(UISelectableInputListener.SelectedButtonInput);
            Assert.That((int)type.Attributes, Is.EqualTo(1056770));
            Assert.That(type.IsSerializable, Is.True);
            SetOptions(type);
            Fields(type, new[] { "m_triggerButton", "m_buttonToTrigger", "m_triggerEvent", "m_eventToTrigger", "m_inputToListenFor",
                    "m_inputSupplierToListenFor", "m_stringID", "m_provider" },
                new[] { typeof(bool), typeof(UIButton), typeof(bool), typeof(UnityEvent), typeof(GameInput), typeof(InputSupplier),
                    typeof(Strings), typeof(UISelectableInputListener.ISelectedButtonInputTextProvider) },
                new[] { 6, 6, 6, 6, 6, 6, 6, 1 });
            AttributeRows(type.GetField("m_buttonToTrigger"), typeof(TooltipAttribute));
            AttributeRows(type.GetField("m_eventToTrigger"), typeof(TooltipAttribute));
            AttributeRows(type.GetField("m_inputToListenFor"), typeof(TooltipAttribute));
            AttributeRows(type.GetField("m_inputSupplierToListenFor"));
            AttributeRows(type.GetField("m_stringID"), typeof(TooltipAttribute), typeof(HashEnumAttribute));
            CustomAttributeData hashEnum = type.GetField("m_stringID").GetCustomAttributesData()[1];
            Assert.That(hashEnum.ConstructorArguments.Single().Value, Is.EqualTo(typeof(Strings)));
            Assert.That(type.GetField("m_stringID").GetCustomAttribute<TooltipAttribute>().tooltip, Is.EqualTo("String to be shown in the overlay."));
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(3));
            Method(type, "get_Text", 2182, typeof(string), Type.EmptyTypes, new string[0]);
            Method(type, "SetProvider", 134, typeof(void), new[] { typeof(UISelectableInputListener.ISelectedButtonInputTextProvider) }, new[] { "provider" });
            Method(type, ".ctor", 6278, null, Type.EmptyTypes, new string[0]);

            type = typeof(UISelectableInputListener.ISelectedButtonInputTextProvider);
            // Unity 2022.3's compiler emits no BeforeFieldInit here: the same 162 check on
            // HLSelectableInputListener.ISelectedButtonInputTextProvider passed natively (Editor660 run, 2026-10-11).
            Assert.That((int)type.Attributes, Is.EqualTo(162));
            Assert.That(type.GetMethods(Declared).Length, Is.EqualTo(1));
            Method(type, "get_Text", 3526, typeof(string), Type.EmptyTypes, new string[0]);

            type = typeof(UISelectableInputListener).GetNestedType("<>c__DisplayClass12_0", Declared);
            Assert.That(type, Is.Not.Null);
            Assert.That((int)type.Attributes, Is.EqualTo(1048835));
            Assert.That(type.GetCustomAttributesData().Select(a => a.AttributeType),
                Is.EqualTo(new[] { typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute) }));
            Fields(type, new[] { "gameInputIndex", "<>4__this" }, new[] { typeof(int), typeof(UISelectableInputListener) }, new[] { 6, 6 });
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(2));
            Method(type, "<ButtonSelected>b__0", 131, typeof(void), new[] { typeof(float) }, new[] { "value" });
            Method(type, ".ctor", 6278, null, Type.EmptyTypes, new string[0]);
        }

        // 0x06000410 returns the field; 0x06000411 allocates one m_delegates list before MonoBehaviour's ctor.
        [Test]
        public void InactiveListenerReturnsSerializedArrayAndStartsWithEmptyDelegateList()
        {
            using (var owned = new Owned())
            {
                UISelectableInputListener listener = owned.Listener;
                Assert.That(ReferenceEquals(listener.GetButtonInputData(), Read(listener, "m_buttonInputData")), Is.True);
                Assert.That(Read(listener, "m_selectableController"), Is.Null, "Awake must not have run");
                Assert.That(Read(listener, "m_inputBridge"), Is.Null);
                Assert.That(owned.Delegates, Is.Not.Null);
                Assert.That(owned.Delegates, Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // 0x06000412 x86-64 0x1b5a750: null provider gives null without consulting m_stringID.
        [Test]
        public void SelectedButtonInputDefaultsGiveNullTextWhateverTheStringId()
        {
            var data = new UISelectableInputListener.SelectedButtonInput();
            Assert.That(data.m_triggerButton, Is.False);
            Assert.That(data.m_triggerEvent, Is.False);
            Assert.That(ReferenceEquals(data.m_buttonToTrigger, null), Is.True);
            Assert.That(data.m_eventToTrigger, Is.Null);
            Assert.That(ReferenceEquals(data.m_inputSupplierToListenFor, null), Is.True);
            Assert.That(Convert.ToInt32(data.m_inputToListenFor), Is.Zero);
            Assert.That(Convert.ToInt32(data.m_stringID), Is.Zero);
            Assert.That(data.Text, Is.Null);
            data.m_stringID = Strings.MISSING;
            Assert.That(data.Text, Is.Null);
            data.SetProvider(null);
            Assert.That(data.Text, Is.Null);
        }

        // 0x06000413 x86-64 0x1b5a800 stores only while +0x48 is null; 0x06000412 calls the provider's
        // interface Text on every read. Catches a replacing SetProvider, a null clearing the provider,
        // and a cached Text.
        [Test]
        public void SelectedButtonInputKeepsFirstProviderAndReadsItsTextEachTime()
        {
            var data = new UISelectableInputListener.SelectedButtonInput();
            var first = new TextProvider { Value = "first" };
            var later = new TextProvider { Value = "later" };
            data.SetProvider(first);
            Assert.That(data.Text, Is.SameAs(first.Value));
            data.SetProvider(later);
            data.SetProvider(null);
            first.Value = "first changed";
            Assert.That(data.Text, Is.SameAs(first.Value));
            Assert.That(first.Reads, Is.EqualTo(2));
            Assert.That(later.Reads, Is.Zero);
        }

        // 0x0600040d x86-64 0x1b5a240: one closure is appended per entry without clearing the list,
        // and RegisterInputListenerOnDown (slot 19, ecx = -1) receives m_delegates[i], not the closure
        // just appended. So the second selection re-registers the first three closures.
        [Test]
        public void ButtonSelectedAppendsClosuresAndRegistersTheDelegateAtTheEntryIndex()
        {
            using (var owned = new Owned())
            {
                UISelectableInputListener.SelectedButtonInput[] data = owned.Configure(3);
                UISelectableInputListenerBridgeProbe bridge = owned.AttachBridge();

                // A null watched selectable equals a null selection.
                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                Assert.That(owned.Delegates.Count, Is.EqualTo(3));
                Assert.That(bridge.Calls.Select(c => c.Name), Is.EqualTo(Enumerable.Repeat("RegisterOnDown", 3)));
                Assert.That(bridge.Calls.Select(c => c.Supplier), Is.EqualTo(data.Select(d => d.m_inputSupplierToListenFor)));
                Assert.That(bridge.Calls.Select(c => c.JoystickIndex), Is.EqualTo(new[] { -1, -1, -1 }));
                for (int i = 0; i < 3; i++)
                    Assert.That(ReferenceEquals(bridge.Calls[i].Callback, owned.Delegates[i]), Is.True, "registration " + i);
                Assert.That(owned.Delegates.Distinct().Count(), Is.EqualTo(3));

                bridge.Calls.Clear();
                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                Assert.That(owned.Delegates.Count, Is.EqualTo(6));
                Assert.That(owned.Delegates.Distinct().Count(), Is.EqualTo(6));
                Assert.That(bridge.Calls.Count, Is.EqualTo(3));
                for (int i = 0; i < 3; i++)
                    Assert.That(ReferenceEquals(bridge.Calls[i].Callback, owned.Delegates[i]), Is.True, "re-registration " + i);

                // Without a bridge the closures are still appended.
                Write(owned.Listener, "m_inputBridge", null);
                bridge.Calls.Clear();
                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                Assert.That(owned.Delegates.Count, Is.EqualTo(9));
                Assert.That(bridge.Calls, Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // Closure 0x06000417 (0x1b5a830) rereads m_buttonInputData[index].m_inputSupplierToListenFor on
        // each call; ProcessTriggers 0x0600040e (0x1b5a5e0) ignores value and fires, in entry order,
        // every entry whose supplier matches and whose m_triggerEvent is set.
        [Test]
        public void ClosuresRereadTheEntrySupplierAndFireEveryMatchingEntryInOrder()
        {
            using (var owned = new Owned())
            {
                var log = new List<string>();
                UISelectableInputListener.SelectedButtonInput[] data = owned.Configure(4);
                for (int i = 0; i < data.Length; i++)
                {
                    string label = "event" + i;
                    data[i].m_triggerEvent = true;
                    data[i].m_eventToTrigger = new UnityEvent();
                    data[i].m_eventToTrigger.AddListener(() => log.Add(label));
                }
                InputSupplier first = data[0].m_inputSupplierToListenFor;
                InputSupplier second = data[1].m_inputSupplierToListenFor;
                data[2].m_inputSupplierToListenFor = first;
                data[3].m_inputSupplierToListenFor = first;
                data[3].m_triggerEvent = false;
                data[3].m_eventToTrigger = null;

                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                owned.Delegates[0](0.75f);
                Assert.That(log, Is.EqualTo(new[] { "event0", "event2" }));

                log.Clear();
                owned.Delegates[1](-3f);
                Assert.That(log, Is.EqualTo(new[] { "event1" }));

                log.Clear();
                data[0].m_inputSupplierToListenFor = second;
                owned.Delegates[0](0f);
                Assert.That(log, Is.EqualTo(new[] { "event0", "event1" }));

                log.Clear();
                Call(owned.Listener, "ProcessTriggers", 1f, (InputSupplier)null);
                Assert.That(log, Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // ProcessTriggers 0x1b5a671..0x1b5a6a0: a matching entry with m_triggerButton dispatches the
        // button's virtual Press (slot 49) before its own event; m_buttonToTrigger is read only then.
        [Test]
        public void TriggeredButtonsArePressedBeforeTheirEntryEvent()
        {
            using (var owned = new Owned())
            {
                var log = new List<string>();
                UISelectableInputListener.SelectedButtonInput[] data = owned.Configure(3);
                InputSupplier supplier = data[0].m_inputSupplierToListenFor;
                data[1].m_inputSupplierToListenFor = supplier;
                data[0].m_triggerButton = true;
                data[0].m_buttonToTrigger = owned.AddInactiveButton(() => log.Add("press0"));
                data[0].m_triggerEvent = true;
                data[0].m_eventToTrigger = new UnityEvent();
                data[0].m_eventToTrigger.AddListener(() => log.Add("event0"));
                data[1].m_triggerButton = true;
                data[1].m_buttonToTrigger = owned.AddInactiveButton(() => log.Add("press1"));
                data[2].m_triggerButton = true;
                data[2].m_buttonToTrigger = owned.AddInactiveButton(() => log.Add("press2"));

                Call(owned.Listener, "ProcessTriggers", 0f, supplier);
                Assert.That(log, Is.EqualTo(new[] { "press0", "event0", "press1" }));

                log.Clear();
                data[0].m_triggerButton = false;
                data[0].m_buttonToTrigger = null;
                Call(owned.Listener, "ProcessTriggers", 0f, supplier);
                Assert.That(log, Is.EqualTo(new[] { "event0", "press1" }));
                owned.AssertUnchanged();
            }
        }

        // 0x0600040f x86-64 0x1b5a460: unregisters (slot 22, ecx = -1) only while
        // i < m_buttonInputData.Length && i < m_delegates.Count, then clears the whole list in place.
        [Test]
        public void ButtonDeselectedUnregistersOnlyTheFirstEntryCountThenClearsInPlace()
        {
            using (var owned = new Owned())
            {
                UISelectableInputListener.SelectedButtonInput[] data = owned.Configure(3);
                UISelectableInputListenerBridgeProbe bridge = owned.AttachBridge();
                List<Action<float>> delegates = owned.Delegates;
                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                Action<float>[] appended = delegates.ToArray();
                Assert.That(appended.Length, Is.EqualTo(6));

                bridge.Calls.Clear();
                Call(owned.Listener, "ButtonDeselected", (UISelectable)null);
                Assert.That(bridge.Calls.Select(c => c.Name), Is.EqualTo(Enumerable.Repeat("Unregister", 3)));
                Assert.That(bridge.Calls.Select(c => c.Supplier), Is.EqualTo(data.Select(d => d.m_inputSupplierToListenFor)));
                Assert.That(bridge.Calls.Select(c => c.JoystickIndex), Is.EqualTo(new[] { -1, -1, -1 }));
                for (int i = 0; i < 3; i++)
                    Assert.That(ReferenceEquals(bridge.Calls[i].Callback, appended[i]), Is.True, "unregistration " + i);
                Assert.That(delegates, Is.Empty);
                Assert.That(ReferenceEquals(owned.Delegates, delegates), Is.True);

                // An empty list bounds the loop before the bridge is touched.
                bridge.Calls.Clear();
                Call(owned.Listener, "ButtonDeselected", (UISelectable)null);
                Assert.That(bridge.Calls, Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // Both callbacks start with UnityEngine.Object.op_Inequality(m_selectableToWatch, selected)
        // and return before touching the list or the bridge.
        [Test]
        public void SelectAndDeselectIgnoreAnotherSelectable()
        {
            using (var owned = new Owned())
            {
                owned.Configure(2);
                UISelectableInputListenerBridgeProbe bridge = owned.AttachBridge();
                UISelectable other = owned.AddInactiveSelectable();

                Call(owned.Listener, "ButtonSelected", other);
                Assert.That(owned.Delegates, Is.Empty);
                Assert.That(bridge.Calls, Is.Empty);

                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                bridge.Calls.Clear();
                Call(owned.Listener, "ButtonDeselected", other);
                Assert.That(owned.Delegates.Count, Is.EqualTo(2));
                Assert.That(bridge.Calls, Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // A watched selectable matches only itself: a null selection no longer matches once
        // m_selectableToWatch is set, so this catches a reference or null-tolerant comparison.
        [Test]
        public void SelectAndDeselectMatchOnlyTheWatchedSelectable()
        {
            using (var owned = new Owned())
            {
                UISelectableInputListener.SelectedButtonInput[] data = owned.Configure(2);
                UISelectableInputListenerBridgeProbe bridge = owned.AttachBridge();
                UISelectable watched = owned.AddInactiveSelectable();
                Write(owned.Listener, "m_selectableToWatch", watched);

                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                Assert.That(owned.Delegates, Is.Empty);
                Assert.That(bridge.Calls, Is.Empty);

                Call(owned.Listener, "ButtonSelected", watched);
                Assert.That(owned.Delegates.Count, Is.EqualTo(2));
                Assert.That(bridge.Calls.Select(c => c.Name), Is.EqualTo(Enumerable.Repeat("RegisterOnDown", 2)));
                Assert.That(bridge.Calls.Select(c => c.Supplier), Is.EqualTo(data.Select(d => d.m_inputSupplierToListenFor)));

                bridge.Calls.Clear();
                Call(owned.Listener, "ButtonDeselected", (UISelectable)null);
                Assert.That(owned.Delegates.Count, Is.EqualTo(2));
                Assert.That(bridge.Calls, Is.Empty);

                Action<float>[] appended = owned.Delegates.ToArray();
                Call(owned.Listener, "ButtonDeselected", watched);
                Assert.That(bridge.Calls.Select(c => c.Name), Is.EqualTo(Enumerable.Repeat("Unregister", 2)));
                Assert.That(bridge.Calls.Select(c => c.Callback), Is.EqualTo(appended));
                Assert.That(owned.Delegates, Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // OnEnable 0x06000409 tail-calls Setup 0x0600040b, which adds ClearUp to InputBridge.OnShutdown
        // before SystemRef.IsNull; OnDisable 0x0600040a tail-calls ClearUp 0x0600040c, which removes the
        // hook and deselects m_selectableToWatch before IsNull. The reference names an unregistered
        // controller (the shape Awake's GetSystemRef gives when none is registered), so IsNull is true
        // and both return without touching a controller.
        [Test]
        public void EnableAndDisableOrderTheShutdownHookAndDeselectAroundAnUnregisteredController()
        {
            using (var owned = new Owned())
            {
                owned.Configure(2);
                UISelectableInputListenerBridgeProbe bridge = owned.AttachBridge();
                Write(owned.Listener, "m_selectableController",
                    new SystemRef<UISelectableController>("owned-original-ui-selectable-controller", null));
                InputBridge.OnShutdown = null;

                Call(owned.Listener, "OnEnable");
                FastAction shutdown = InputBridge.OnShutdown;
                Assert.That(shutdown, Is.Not.Null);
                Assert.That(shutdown.GetInvocationListCount(), Is.EqualTo(1));
                Assert.That(ReferenceEquals(shutdown.GetInvocationList()[0].Target, owned.Listener), Is.True);
                Assert.That(shutdown.GetInvocationList()[0].Method.Name, Is.EqualTo("ClearUp"));
                Assert.That(owned.Delegates, Is.Empty, "no controller means no initial selection");
                Assert.That(bridge.Calls, Is.Empty);

                Call(owned.Listener, "ButtonSelected", (UISelectable)null);
                Action<float>[] appended = owned.Delegates.ToArray();
                bridge.Calls.Clear();
                Call(owned.Listener, "OnDisable");
                Assert.That(ReferenceEquals(InputBridge.OnShutdown, shutdown), Is.True);
                Assert.That(shutdown.GetInvocationListCount(), Is.Zero);
                Assert.That(bridge.Calls.Select(c => c.Callback), Is.EqualTo(appended));
                Assert.That(owned.Delegates, Is.Empty);
                owned.AssertUnchanged();
            }
        }

        private sealed class TextProvider : UISelectableInputListener.ISelectedButtonInputTextProvider
        {
            public string Value;
            public int Reads;
            public string Text { get { ++Reads; return Value; } }
        }

        // Owns an inactive listener, its suppliers, bridge and any extra objects, captures the shipped
        // static InputBridge.OnShutdown and restores it. AssertUnchanged, called at the end of each
        // case body, fails on any log message or a changed shipped hook; Dispose never throws, so it
        // cannot hide the first failure of a case.
        private sealed class Owned : IDisposable
        {
            private readonly FastAction priorShutdown;
            private readonly int priorShutdownCount;
            private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
            private readonly List<string> logs = new List<string>();
            private GameObject root;

            public UISelectableInputListener Listener { get; }
            public List<Action<float>> Delegates => (List<Action<float>>)Read(Listener, "m_delegates");

            public Owned()
            {
                priorShutdown = InputBridge.OnShutdown;
                priorShutdownCount = priorShutdown == null ? -1 : priorShutdown.GetInvocationListCount();
                Application.logMessageReceived += Log;
                try
                {
                    root = new GameObject("owned-original-ui-selectable-input-listener", typeof(RectTransform));
                    root.SetActive(false);
                    Listener = root.AddComponent<UISelectableInputListener>();
                    Assert.That(root.activeInHierarchy, Is.False);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            // Serialized authoring of n entries, each given its own supplier.
            public UISelectableInputListener.SelectedButtonInput[] Configure(int count)
            {
                using (var serialized = new SerializedObject(Listener))
                {
                    serialized.Update();
                    serialized.FindProperty("m_buttonInputData").arraySize = count;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                UISelectableInputListener.SelectedButtonInput[] data = Listener.GetButtonInputData();
                Assert.That(data.Length, Is.EqualTo(count));
                for (int i = 0; i < count; i++)
                {
                    var supplier = ScriptableObject.CreateInstance<HLInputSupplier>();
                    objects.Add(supplier);
                    data[i].m_inputSupplierToListenFor = supplier;
                }
                return data;
            }

            public UISelectableInputListenerBridgeProbe AttachBridge()
            {
                var bridge = ScriptableObject.CreateInstance<UISelectableInputListenerBridgeProbe>();
                objects.Add(bridge);
                Write(Listener, "m_inputBridge", bridge);
                return bridge;
            }

            // An inactive probe button whose own Press refuses quietly (inactive, interactable,
            // no animation), so only the dispatch is observed.
            public UIButton AddInactiveButton(Action pressed)
            {
                var holder = new GameObject("owned-original-ui-selectable-button", typeof(RectTransform));
                objects.Add(holder);
                holder.SetActive(false);
                UIButtonProbe button = holder.AddComponent<UIButtonProbe>();
                button.InteractableResult = true;
                button.AnimationDisabled = true;
                button.Pressed = pressed;
                return button;
            }

            public UISelectable AddInactiveSelectable()
            {
                var other = new GameObject("owned-original-ui-selectable-other", typeof(RectTransform));
                objects.Add(other);
                other.SetActive(false);
                return other.AddComponent<UISelectable>();
            }

            public void AssertUnchanged()
            {
                Assert.That(priorShutdown == null ? -1 : priorShutdown.GetInvocationListCount(), Is.EqualTo(priorShutdownCount));
                Assert.That(logs, Is.Empty);
            }

            public void Dispose()
            {
                try
                {
                    for (int i = objects.Count - 1; i >= 0; i--)
                        if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                    if (!ReferenceEquals(root, null)) UnityEngine.Object.DestroyImmediate(root);
                }
                finally
                {
                    objects.Clear();
                    root = null;
                    // A case that replaces the static gets the shipped value back here.
                    InputBridge.OnShutdown = priorShutdown;
                    Application.logMessageReceived -= Log;
                }
            }

            private void Log(string message, string stackTrace, LogType type)
            {
                logs.Add(type + ": " + message);
            }
        }

        private static void Call(object target, string name, params object[] arguments)
        {
            MethodInfo method = typeof(UISelectableInputListener).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(target, arguments); }
            catch (TargetInvocationException failure) { throw failure.InnerException; }
        }

        private static object Read(object target, string name)
        {
            FieldInfo field = typeof(UISelectableInputListener).GetField(name, Declared);
            Assert.That(field, Is.Not.Null, name);
            return field.GetValue(target);
        }

        private static void Write(object target, string name, object value)
        {
            FieldInfo field = typeof(UISelectableInputListener).GetField(name, Declared);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void SetOptions(Type type)
        {
            CustomAttributeData[] options = type.GetCustomAttributesData()
                .Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute)).ToArray();
            Assert.That(options.Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)), Is.EqualTo(new[] { 1, 2 }), type.Name);
            Assert.That(options.Select(a => a.ConstructorArguments[1].Value), Is.EqualTo(new object[] { false, false }), type.Name);
        }

        private static void AttributeRows(FieldInfo field, params Type[] expected)
        {
            Assert.That(field, Is.Not.Null);
            Assert.That(field.GetCustomAttributesData().Select(a => a.AttributeType), Is.EqualTo(expected), field.Name);
        }

        private static void Fields(Type type, string[] names, Type[] types, int[] flags)
        {
            FieldInfo[] actual = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(actual.Select(f => f.Name), Is.EqualTo(names), type.Name);
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i].FieldType, Is.EqualTo(types[i]), names[i]);
                Assert.That((int)actual[i].Attributes, Is.EqualTo(flags[i]), names[i]);
            }
        }

        private static void Method(Type type, string name, int flags, Type result, Type[] arguments, string[] parameterNames)
        {
            MethodBase method = name == ".ctor" ? (MethodBase)type.GetConstructor(Declared, null, arguments, null) :
                type.GetMethod(name, Declared, null, arguments, null);
            Assert.That(method, Is.Not.Null, type.Name + "." + name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), name);
            if (method is MethodInfo info) Assert.That(info.ReturnType, Is.EqualTo(result), name);
            Assert.That(method.GetParameters().Select(p => p.Name), Is.EqualTo(parameterNames), name);
        }
    }
}
