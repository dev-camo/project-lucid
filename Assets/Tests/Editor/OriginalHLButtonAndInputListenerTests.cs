using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectLucid.Tests
{
    // Bounded inactive managed observations. Active input, selection, sound,
    // haptics and coroutine paths require their own genuine provider tests.
    public sealed class OriginalHLButtonAndInputListenerTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void ButtonWholeDeclaredMembersKeepOriginalFlagsAndTypedSignatures()
        {
            Type type;
            type = typeof(HLButton);
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.IsGenericType, Is.False);
            Assert.That(type.Assembly, Is.SameAs(typeof(HLButton).Assembly));
            Fields(type, new string[] { "OnClickSfx", "OnDisabledClickSfx", "OnDownSfx", "OnUpSfx", "m_onClickSfxId", "m_onDisabledClickSfxId", "m_onDownSfxId", "m_onUpSfxId", "m_onClick", "m_onDisabledClick", "m_onOfflineClick", "m_onButtonDown", "m_onButtonUp", "OnDisabledSelected" }, new Type[] { typeof(global::Hardlight.FastAction<global::Hardlight.HLAudioTypes>), typeof(global::Hardlight.FastAction<global::Hardlight.HLAudioTypes>), typeof(global::Hardlight.FastAction<global::Hardlight.HLAudioTypes>), typeof(global::Hardlight.FastAction<global::Hardlight.HLAudioTypes>), typeof(global::Hardlight.HLAudioTypes), typeof(global::Hardlight.HLAudioTypes), typeof(global::Hardlight.HLAudioTypes), typeof(global::Hardlight.HLAudioTypes), typeof(global::UnityEngine.Events.UnityEvent), typeof(global::UnityEngine.Events.UnityEvent), typeof(global::UnityEngine.Events.UnityEvent), typeof(global::UnityEngine.Events.UnityEvent), typeof(global::UnityEngine.Events.UnityEvent), typeof(global::UnityEngine.Events.UnityEvent) }, new int[] { 22, 22, 22, 22, 1, 1, 1, 1, 4, 4, 4, 4, 4, 6 });
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(22));
            Assert.That(type.GetProperties(Declared).Length, Is.EqualTo(5));
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Method(type, "OnEnable", 196, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "OnDisable", 196, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "get_OnClick", 2182, typeof(global::UnityEngine.Events.UnityEvent), new Type[] {  }, new string[] {  });
            Method(type, "set_OnClick", 2182, typeof(void), new Type[] { typeof(global::UnityEngine.Events.UnityEvent) }, new string[] { "value" });
            Method(type, "get_OnDisabledClick", 2182, typeof(global::UnityEngine.Events.UnityEvent), new Type[] {  }, new string[] {  });
            Method(type, "set_OnDisabledClick", 2182, typeof(void), new Type[] { typeof(global::UnityEngine.Events.UnityEvent) }, new string[] { "value" });
            Method(type, "get_OnButtonDown", 2182, typeof(global::UnityEngine.Events.UnityEvent), new Type[] {  }, new string[] {  });
            Method(type, "set_OnButtonDown", 2182, typeof(void), new Type[] { typeof(global::UnityEngine.Events.UnityEvent) }, new string[] { "value" });
            Method(type, "get_OnButtonUp", 2182, typeof(global::UnityEngine.Events.UnityEvent), new Type[] {  }, new string[] {  });
            Method(type, "set_OnButtonUp", 2182, typeof(void), new Type[] { typeof(global::UnityEngine.Events.UnityEvent) }, new string[] { "value" });
            Method(type, "get_OnOfflineClick", 2182, typeof(global::UnityEngine.Events.UnityEvent), new Type[] {  }, new string[] {  });
            Method(type, "set_OnOfflineClick", 2182, typeof(void), new Type[] { typeof(global::UnityEngine.Events.UnityEvent) }, new string[] { "value" });
            Method(type, "Action_OnClick", 134, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "Action_OnDisabledClick", 134, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "Action_OnButtonDown", 134, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "Action_OnButtonUp", 134, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "Press", 198, typeof(bool), new Type[] {  }, new string[] {  });
            Method(type, "OnPointerDown", 198, typeof(void), new Type[] { typeof(global::UnityEngine.EventSystems.PointerEventData) }, new string[] { "eventData" });
            Method(type, "OnPointerUp", 198, typeof(void), new Type[] { typeof(global::UnityEngine.EventSystems.PointerEventData) }, new string[] { "eventData" });
            Method(type, "OnPointerEnter", 198, typeof(void), new Type[] { typeof(global::UnityEngine.EventSystems.PointerEventData) }, new string[] { "eventData" });
            Method(type, "OnPointerExit", 198, typeof(void), new Type[] { typeof(global::UnityEngine.EventSystems.PointerEventData) }, new string[] { "eventData" });
            Method(type, ".ctor", 6278, null, new Type[] {  }, new string[] {  });
            Assert.That(type.BaseType, Is.EqualTo(typeof(HLCurveAnimatedGraphic)));
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            foreach (string name in new[] { "OnClick", "OnDisabledClick", "OnButtonDown", "OnButtonUp", "OnOfflineClick" })
            {
                PropertyInfo property = type.GetProperty(name, Declared);
                Assert.That(property.PropertyType, Is.EqualTo(typeof(UnityEvent)));
                Assert.That(property.GetIndexParameters(), Is.Empty);
                Assert.That(property.GetGetMethod().IsVirtual, Is.False);
                Assert.That(property.GetSetMethod().IsVirtual, Is.False);
            }
        }

        [Test]
        public void ListenerWholeNaturalFamilyKeepsOriginalFlagsAndTypedSignatures()
        {
            Type type;
            type = typeof(global::Hardlight.HLSelectableInputListener.SelectedButtonInput);
            Assert.That((int)type.Attributes, Is.EqualTo(1056770));
            Assert.That(type.IsGenericType, Is.False);
            Assert.That(type.Assembly, Is.SameAs(typeof(HLButton).Assembly));
            Fields(type, new string[] { "m_triggerButton", "m_buttonToTrigger", "m_triggerEvent", "m_eventToTrigger", "m_inputToListenFor", "m_inputSupplierToListenFor", "m_stringID", "m_provider" }, new Type[] { typeof(global::System.Boolean), typeof(global::Hardlight.HLButton), typeof(global::System.Boolean), typeof(global::UnityEngine.Events.UnityEvent), typeof(global::Hardlight.GameInput), typeof(global::Hardlight.InputSupplier), typeof(global::Hardlight.Enums.Strings), typeof(global::Hardlight.HLSelectableInputListener.ISelectedButtonInputTextProvider) }, new int[] { 6, 6, 6, 6, 6, 6, 6, 1 });
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(3));
            Assert.That(type.GetProperties(Declared).Length, Is.EqualTo(1));
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Method(type, "get_Text", 2182, typeof(string), new Type[] {  }, new string[] {  });
            Method(type, "SetProvider", 134, typeof(void), new Type[] { typeof(global::Hardlight.HLSelectableInputListener.ISelectedButtonInputTextProvider) }, new string[] { "provider" });
            Method(type, ".ctor", 6278, null, new Type[] {  }, new string[] {  });
            type = typeof(global::Hardlight.HLSelectableInputListener.ISelectedButtonInputTextProvider);
            Assert.That((int)type.Attributes, Is.EqualTo(162));
            Assert.That(type.IsGenericType, Is.False);
            Assert.That(type.Assembly, Is.SameAs(typeof(HLButton).Assembly));
            Fields(type, new string[] {  }, new Type[] {  }, new int[] {  });
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(1));
            Assert.That(type.GetProperties(Declared).Length, Is.EqualTo(1));
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Method(type, "get_Text", 3526, typeof(string), new Type[] {  }, new string[] {  });
            // Shipping metadata (TypeDef 0x02000092) names this capture/callback
            // owner <>c__DisplayClass12_0; find it by contract, then pin the name.
            Type[] callbackOwners = typeof(HLSelectableInputListener).GetNestedTypes(Declared)
                .Where(candidate => candidate.DeclaringType == typeof(HLSelectableInputListener)
                    && candidate.GetCustomAttributesData().Any(attribute => attribute.AttributeType == typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))
                    && candidate.GetFields(Declared).OrderBy(field => field.MetadataToken).Select(field => field.Name)
                        .SequenceEqual(new[] { "gameInputIndex", "<>4__this" })
                    && candidate.GetMethod("<ButtonSelected>b__0", Declared, null, new[] { typeof(float) }, null) != null)
                .ToArray();
            Assert.That(callbackOwners.Length, Is.EqualTo(1), "exactly one natural capture/callback owner is required");
            Type currentCallbackOwner = callbackOwners.Single();
            Assert.That(currentCallbackOwner.Name, Is.EqualTo("<>c__DisplayClass12_0"));
            Assert.That(currentCallbackOwner.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(currentCallbackOwner.IsNestedPrivate, Is.True);
            Assert.That(currentCallbackOwner.GetNestedTypes(Declared), Is.Empty);
            var compilerAttributes = currentCallbackOwner.GetCustomAttributesData();
            Assert.That(compilerAttributes.Count, Is.EqualTo(1));
            Assert.That(compilerAttributes[0].AttributeType, Is.EqualTo(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            Assert.That(compilerAttributes[0].Constructor.GetParameters(), Is.Empty);
            Assert.That(compilerAttributes[0].ConstructorArguments, Is.Empty);
            Assert.That(compilerAttributes[0].NamedArguments, Is.Empty);
            type = currentCallbackOwner;
            Assert.That((int)type.Attributes, Is.EqualTo(1048835));
            Assert.That(type.IsGenericType, Is.False);
            Assert.That(type.Assembly, Is.SameAs(typeof(HLButton).Assembly));
            Fields(type, new string[] { "gameInputIndex", "<>4__this" }, new Type[] { typeof(global::System.Int32), typeof(global::Hardlight.HLSelectableInputListener) }, new int[] { 6, 6 });
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(2));
            Assert.That(type.GetProperties(Declared).Length, Is.EqualTo(0));
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Method(type, ".ctor", 6278, null, new Type[] {  }, new string[] {  });
            Method(type, "<ButtonSelected>b__0", 131, typeof(void), new Type[] { typeof(float) }, new string[] { "value" });
            type = typeof(global::Hardlight.HLSelectableInputListener);
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.IsGenericType, Is.False);
            Assert.That(type.Assembly, Is.SameAs(typeof(HLButton).Assembly));
            Fields(type, new string[] { "m_selectableToWatch", "m_buttonInputData", "m_hlSelectableController", "m_delegates", "m_inputBridge" }, new Type[] { typeof(global::Hardlight.HLSelectable), typeof(global::Hardlight.HLSelectableInputListener.SelectedButtonInput[]), typeof(global::Hardlight.SystemRef<global::Hardlight.HLSelectableController>), typeof(global::System.Collections.Generic.List<global::System.Action<global::System.Single>>), typeof(global::Hardlight.InputBridge) }, new int[] { 1, 1, 1, 1, 1 });
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(10));
            Assert.That(type.GetProperties(Declared).Length, Is.EqualTo(0));
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Method(type, "Awake", 129, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "OnEnable", 129, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "OnDisable", 129, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "Setup", 129, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "ClearUp", 129, typeof(void), new Type[] {  }, new string[] {  });
            Method(type, "ButtonSelected", 129, typeof(void), new Type[] { typeof(global::Hardlight.HLSelectable) }, new string[] { "selected" });
            Method(type, "ProcessTriggers", 129, typeof(void), new Type[] { typeof(float), typeof(global::Hardlight.InputSupplier) }, new string[] { "value", "inputSupplier" });
            Method(type, "ButtonDeselected", 129, typeof(void), new Type[] { typeof(global::Hardlight.HLSelectable) }, new string[] { "selected" });
            Method(type, "GetButtonInputData", 134, typeof(global::Hardlight.HLSelectableInputListener.SelectedButtonInput[]), new Type[] {  }, new string[] {  });
            Method(type, ".ctor", 6278, null, new Type[] {  }, new string[] {  });
            Assert.That(typeof(HLSelectableInputListener).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(typeof(HLSelectableInputListener).GetNestedTypes(Declared).Select(t => t.Name).OrderBy(n => n),
                Is.EqualTo(new[] { currentCallbackOwner.Name, "ISelectedButtonInputTextProvider", "SelectedButtonInput" }.OrderBy(name => name)));
            Assert.That(typeof(HLSelectableInputListener.SelectedButtonInput).IsSerializable, Is.True);
            Assert.That(typeof(HLSelectableInputListener.ISelectedButtonInputTextProvider).IsInterface, Is.True);
            Assert.That(typeof(HLSelectableInputListener.ISelectedButtonInputTextProvider).GetProperty("Text").GetGetMethod().IsAbstract, Is.True);
        }

        [Test]
        public void InactiveButtonConstructsFiveIndependentMutableEvents()
        {
            using (Owned owned = new Owned(false))
            {
                HLButton button = owned.Button;
                UnityEvent[] initial = Events(button);
                Assert.That(initial.All(e => !ReferenceEquals(e, null)), Is.True);
                for (int i = 0; i < initial.Length; ++i)
                    for (int j = i + 1; j < initial.Length; ++j)
                        Assert.That(ReferenceEquals(initial[i], initial[j]), Is.False);
                Assert.That(ReferenceEquals(button.OnDisabledSelected, null), Is.True);
                UnityEvent replacement = new UnityEvent();
                button.OnClick = replacement; button.OnDisabledClick = replacement;
                button.OnButtonDown = replacement; button.OnButtonUp = replacement;
                button.OnOfflineClick = replacement;
                Assert.That(Events(button).All(e => ReferenceEquals(e, replacement)), Is.True);
                button.OnClick = null; button.OnDisabledClick = null;
                button.OnButtonDown = null; button.OnButtonUp = null; button.OnOfflineClick = null;
                Assert.That(Events(button).All(e => ReferenceEquals(e, null)), Is.True);
                owned.AssertUnchanged();
            }
        }

        [Test]
        public void InactivePublicActionsReturnBeforeNullEventsOrSoundRoutes()
        {
            using (Owned owned = new Owned(false))
            {
                HLButton button = owned.Button;
                int calls = 0;
                UnityAction observer = () => ++calls;
                foreach (UnityEvent e in Events(button)) e.AddListener(observer);
                button.Action_OnClick(); button.Action_OnDisabledClick();
                button.Action_OnButtonDown(); button.Action_OnButtonUp();
                Assert.That(calls, Is.Zero);
                button.OnClick = null; button.OnDisabledClick = null;
                button.OnButtonDown = null; button.OnButtonUp = null; button.OnOfflineClick = null;
                Assert.DoesNotThrow(() => { button.Action_OnClick(); button.Action_OnDisabledClick();
                    button.Action_OnButtonDown(); button.Action_OnButtonUp(); });
                Assert.That(calls, Is.Zero);
                owned.AssertUnchanged();
            }
        }

        [Test]
        public void InactiveAnimationDisabledPressReturnsFalseWithoutDispatch()
        {
            using (Owned owned = new Owned(false))
            {
                HLButton button = owned.Button;
                Assert.That(button.IsActive(), Is.False);
                Assert.That(button.IsInteractable(), Is.True);
                // The genuine public property prevents the original inactive
                // disabled-animation branch from starting a coroutine.
                button.AnimationDisabled = true;
                int calls = 0;
                foreach (UnityEvent e in Events(button)) e.AddListener(() => ++calls);
                Assert.That(button.Press(), Is.False);
                Assert.That(button.AnimationDisabled, Is.True);
                Assert.That(calls, Is.Zero);
                button.OnClick = null; button.OnDisabledClick = null;
                Assert.That(button.Press(), Is.False);
                Assert.That(calls, Is.Zero);
                Assert.That(Read(typeof(HLCurveAnimatedGraphic), button, "m_clickAccepted"), Is.EqualTo(false));
                Assert.That(Read(typeof(HLCurveAnimatedGraphic), button, "m_animatingCoroutine"), Is.Null);
                owned.AssertUnchanged();
            }
        }

        [Test]
        public void InactiveListenerReturnsGenuineObservedArrayWithoutSetup()
        {
            using (Owned owned = new Owned(true))
            {
                HLSelectableInputListener listener = owned.Listener;
                object observed = Read(typeof(HLSelectableInputListener), listener, "m_buttonInputData");
                // Unity may choose null or empty for an unconfigured serialized
                // array. The public getter must preserve the actual identity.
                Assert.That(ReferenceEquals(listener.GetButtonInputData(), observed), Is.True);
                Assert.That(ReferenceEquals(listener.GetButtonInputData(), listener.GetButtonInputData()), Is.True);
                Assert.That(Read(typeof(HLSelectableInputListener), listener, "m_hlSelectableController"), Is.Null);
                Assert.That(Read(typeof(HLSelectableInputListener), listener, "m_inputBridge"), Is.Null);
                IList callbacks = (IList)Read(typeof(HLSelectableInputListener), listener, "m_delegates");
                Assert.That(callbacks, Is.Not.Null); Assert.That(callbacks.Count, Is.Zero);
                owned.AssertUnchanged();
            }
        }

        [Test]
        public void OrdinarySelectedButtonInputKeepsNullProviderAndZeroDefaults()
        {
            var data = new HLSelectableInputListener.SelectedButtonInput();
            Assert.That(data.m_triggerButton, Is.False); Assert.That(data.m_triggerEvent, Is.False);
            Assert.That(ReferenceEquals(data.m_buttonToTrigger, null), Is.True);
            Assert.That(ReferenceEquals(data.m_eventToTrigger, null), Is.True);
            Assert.That(ReferenceEquals(data.m_inputSupplierToListenFor, null), Is.True);
            Assert.That(Convert.ToInt32(data.m_inputToListenFor), Is.Zero);
            Assert.That(Convert.ToInt32(data.m_stringID), Is.Zero);
            Assert.That(data.Text, Is.Null);
            data.SetProvider(null); data.SetProvider(null);
            Assert.That(data.Text, Is.Null);
            Assert.That(Read(typeof(HLSelectableInputListener.SelectedButtonInput), data, "m_provider"), Is.Null);
        }

        // 0x0600047c x86-64 0x1b5f5c0: SetProvider stores only when the field at
        // +0x48 is null. 0x0600047b x86-64 0x1b5f510: get_Text returns null for a
        // null provider, else tail-calls the provider's interface Text on every read.
        // Mutations caught: dropping the first-write-wins guard (b replaces a), a
        // null argument clearing the stored provider, and Text ignoring or caching
        // the provider value.
        [Test]
        public void OrdinarySelectedButtonInputKeepsFirstProviderAndReadsItsTextEachTime()
        {
            var data = new HLSelectableInputListener.SelectedButtonInput();
            var first = new TextProvider { Value = "first provider text" };
            var later = new TextProvider { Value = "later provider text" };
            data.SetProvider(first);
            Assert.That(ReferenceEquals(Read(typeof(HLSelectableInputListener.SelectedButtonInput), data, "m_provider"), first), Is.True);
            Assert.That(data.Text, Is.SameAs(first.Value));
            data.SetProvider(later);
            data.SetProvider(null);
            Assert.That(ReferenceEquals(Read(typeof(HLSelectableInputListener.SelectedButtonInput), data, "m_provider"), first), Is.True);
            first.Value = "changed first provider text";
            Assert.That(data.Text, Is.SameAs(first.Value));
            Assert.That(first.Reads, Is.EqualTo(2));
            Assert.That(later.Reads, Is.Zero);
        }

        private sealed class TextProvider : HLSelectableInputListener.ISelectedButtonInputTextProvider
        {
            public string Value;
            public int Reads;
            public string Text { get { ++Reads; return Value; } }
        }

        // HLButton.Press 0x060005fc x86-64 0x1b6cfb0: a false base result on a
        // non-interactable button invokes m_onDisabledClick, then loads the static
        // OnDisabledClickSfx and m_onDisabledClickSfxId. Fails if the sound is
        // read or played before the event (the first action would then play).
        [Test]
        public void InactiveNonInteractablePressDispatchesEventBeforeRereadingDisabledSound()
        {
            using (Owned owned = new Owned(false))
            {
                HLButton button = owned.Button;
                FastAction<HLAudioTypes> priorClick = HLButton.OnClickSfx;
                FastAction<HLAudioTypes> priorDisabled = HLButton.OnDisabledClickSfx;
                var log = new List<string>();
                try
                {
                    DisabledWithoutAnimation(button);
                    using (var serialized = new SerializedObject(button))
                    {
                        serialized.Update();
                        serialized.FindProperty("m_onDisabledClickSfxId").intValue = (int)HLAudioTypes.None;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var click = new FastAction<HLAudioTypes>(); click += id => log.Add("click sound");
                    var first = new FastAction<HLAudioTypes>(); first += id => log.Add("first disabled sound");
                    var second = new FastAction<HLAudioTypes>(); second += id => log.Add("second disabled sound " + (int)id);
                    HLButton.OnClickSfx = click;
                    HLButton.OnDisabledClickSfx = first;
                    button.OnClick.AddListener(() => log.Add("click"));
                    button.OnDisabledClick.AddListener(() => { log.Add("disabled"); HLButton.OnDisabledClickSfx = second; });
                    Assert.That(button.Press(), Is.False);
                    Assert.That(log, Is.EqualTo(new[] { "disabled", "second disabled sound " + (int)HLAudioTypes.None }));
                    Assert.That(Read(typeof(HLCurveAnimatedGraphic), button, "m_animatingCoroutine"), Is.Null);
                }
                finally
                {
                    HLButton.OnClickSfx = priorClick;
                    HLButton.OnDisabledClickSfx = priorDisabled;
                }
                owned.AssertUnchanged();
                // The accepted branch needs an active button whose Awake/OnEnable publish
                // a VibrationManager row and ActiveSelectables entry; it is not entered here.
            }
        }

        // ButtonSelected 0x06000476 x86-64 0x1b5f000 appends one closure per entry
        // without clearing; closure 0x06000480 (0x1b5f5f0) rereads the entry's
        // supplier; ProcessTriggers 0x06000477 (0x1b5f3a0) calls virtual Press
        // before the entry's UnityEvent. Setup 0x06000474 (0x1b5eb20) adds ClearUp
        // to InputBridge.OnShutdown before SystemRef.IsNull; ClearUp 0x06000475
        // (0x1b5edd0) removes it and deselects before IsNull. Here the controller
        // reference was never published (no Awake), so both IsNull calls fault.
        [Test]
        public void InactiveListenerAppendsClosuresPressesBeforeEventsAndOrdersShutdownAroundFaults()
        {
            using (Owned owned = new Owned(true))
            {
                HLSelectableInputListener listener = owned.Listener;
                FastAction priorShutdown = InputBridge.OnShutdown;
                FastAction<HLAudioTypes> priorDisabled = HLButton.OnDisabledClickSfx;
                var objects = new List<UnityEngine.Object>();
                var log = new List<string>();
                try
                {
                    InputBridge.OnShutdown = null;
                    HLButton.OnDisabledClickSfx = null;
                    var buttonRoot = new GameObject("owned-original-listener-target", typeof(RectTransform));
                    objects.Add(buttonRoot);
                    buttonRoot.SetActive(false);
                    HLButton button = buttonRoot.AddComponent<HLButton>();
                    DisabledWithoutAnimation(button);
                    button.OnDisabledClick.AddListener(() => log.Add("press"));
                    var first = ScriptableObject.CreateInstance<HLInputSupplier>(); objects.Add(first);
                    var second = ScriptableObject.CreateInstance<HLInputSupplier>(); objects.Add(second);
                    using (var serialized = new SerializedObject(listener))
                    {
                        serialized.Update();
                        serialized.FindProperty("m_buttonInputData").arraySize = 3;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    HLSelectableInputListener.SelectedButtonInput[] data = listener.GetButtonInputData();
                    Assert.That(data.Length, Is.EqualTo(3));
                    Entry(data[0], first, button, "event0", log);
                    Entry(data[1], second, null, "event1", log);
                    Entry(data[2], first, null, "event2", log);
                    var delegates = (List<Action<float>>)Read(typeof(HLSelectableInputListener), listener, "m_delegates");

                    // A null watched selectable equals a null selection; repeats append.
                    Call(listener, "ButtonSelected", (HLSelectable)null);
                    Assert.That(delegates.Count, Is.EqualTo(3));
                    Call(listener, "ButtonSelected", (HLSelectable)null);
                    Assert.That(delegates.Count, Is.EqualTo(6));
                    Call(listener, "ButtonSelected", (HLSelectable)button);
                    Assert.That(delegates.Count, Is.EqualTo(6));

                    delegates[0](0.25f);
                    Assert.That(log, Is.EqualTo(new[] { "press", "event0", "event2" }));
                    log.Clear();
                    data[0].m_inputSupplierToListenFor = second;
                    delegates[3](0f);
                    Assert.That(log, Is.EqualTo(new[] { "press", "event0", "event1" }));
                    log.Clear();

                    // 0x1b5f600..0x1b5f61c reload m_buttonInputData, bounds-check the
                    // captured index and read the element on every call, so a replaced
                    // element and a replaced array are both seen.
                    var replacement = new HLSelectableInputListener.SelectedButtonInput();
                    Entry(replacement, first, null, "event0b", log);
                    data[0] = replacement;
                    delegates[0](0f);
                    Assert.That(log, Is.EqualTo(new[] { "event0b", "event2" }));
                    log.Clear();
                    FieldInfo dataField = typeof(HLSelectableInputListener).GetField("m_buttonInputData", Declared);
                    dataField.SetValue(listener, new[] { new HLSelectableInputListener.SelectedButtonInput() });
                    Assert.Throws<IndexOutOfRangeException>(() => delegates[2](0f));
                    Assert.That(log, Is.Empty);
                    dataField.SetValue(listener, data);

                    Assert.Throws<NullReferenceException>(() => Call(listener, "Setup"));
                    FastAction shutdown = InputBridge.OnShutdown;
                    Assert.That(shutdown, Is.Not.Null);
                    Assert.That(shutdown.GetInvocationListCount(), Is.EqualTo(1));
                    Assert.That(ReferenceEquals(shutdown.GetInvocationList()[0].Target, listener), Is.True);
                    Assert.That(shutdown.GetInvocationList()[0].Method.Name, Is.EqualTo("ClearUp"));

                    Assert.Throws<NullReferenceException>(() => Call(listener, "ClearUp"));
                    Assert.That(ReferenceEquals(InputBridge.OnShutdown, shutdown), Is.True);
                    Assert.That(shutdown.GetInvocationListCount(), Is.Zero);
                    Assert.That(delegates.Count, Is.Zero);
                    Assert.That(ReferenceEquals(Read(typeof(HLSelectableInputListener), listener, "m_delegates"), delegates), Is.True);
                }
                finally
                {
                    try
                    {
                        for (int i = objects.Count - 1; i >= 0; --i)
                            if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                    }
                    finally
                    {
                        InputBridge.OnShutdown = priorShutdown;
                        HLButton.OnDisabledClickSfx = priorDisabled;
                    }
                }
                owned.AssertUnchanged();
            }
        }

        // Serialized authoring only: no Interactable setter callbacks or coroutine.
        private static void DisabledWithoutAnimation(HLButton button)
        {
            using (var serialized = new SerializedObject(button))
            {
                serialized.Update();
                serialized.FindProperty("m_interactable").boolValue = false;
                serialized.FindProperty("m_animationDisabled").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.That(button.IsActive(), Is.False);
            Assert.That(button.IsInteractable(), Is.False);
            Assert.That(button.AnimationDisabled, Is.True);
        }

        private static void Entry(HLSelectableInputListener.SelectedButtonInput entry, InputSupplier supplier,
            HLButton button, string label, List<string> log)
        {
            entry.m_inputSupplierToListenFor = supplier;
            entry.m_triggerButton = button != null;
            entry.m_buttonToTrigger = button;
            entry.m_triggerEvent = true;
            entry.m_eventToTrigger = new UnityEvent();
            entry.m_eventToTrigger.AddListener(() => log.Add(label));
        }

        private static void Call(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(target, arguments); }
            catch (TargetInvocationException failure) { throw failure.InnerException; }
        }

        private static UnityEvent[] Events(HLButton button)
        { return new[] { button.OnClick, button.OnDisabledClick, button.OnOfflineClick, button.OnButtonDown, button.OnButtonUp }; }

        private static object Read(Type type, object target, string name)
        {
            FieldInfo field = type.GetField(name, Declared);
            Assert.That(field, Is.Not.Null, type.FullName + "." + name);
            return field.GetValue(target);
        }

        private static void Fields(Type type, string[] names, Type[] types, int[] flags)
        {
            FieldInfo[] actual = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(actual.Select(f => f.Name), Is.EqualTo(names));
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.That(actual[i].FieldType, Is.EqualTo(types[i]), names[i]);
                Assert.That((int)actual[i].Attributes, Is.EqualTo(flags[i]), names[i]);
                Assert.That(actual[i].IsLiteral, Is.False);
            }
        }

        private static void Method(Type type, string name, int flags, Type result, Type[] arguments, string[] parameterNames)
        {
            MethodBase method = name == ".ctor" ? (MethodBase)type.GetConstructor(Declared, null, arguments, null) :
                type.GetMethod(name, Declared, null, arguments, null);
            Assert.That(method, Is.Not.Null, type.FullName + "." + name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), name);
            Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo((MethodImplAttributes)0), name);
            Assert.That(method.IsGenericMethod, Is.False);
            if (method is MethodInfo info) Assert.That(info.ReturnType, Is.EqualTo(result), name);
            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(parameters.Select(p => p.Name), Is.EqualTo(parameterNames));
            Assert.That(parameters.Select(p => p.ParameterType), Is.EqualTo(arguments));
            foreach (ParameterInfo parameter in parameters)
            { Assert.That(parameter.Attributes, Is.EqualTo((ParameterAttributes)0)); Assert.That(parameter.HasDefaultValue, Is.False); }
        }

        // Read-only observations keep real foreign registry rows, reference
        // caches, action rows and selectable lists in place. No restoration,
        // typed system getter, callback invocation or registry reset occurs.
        private sealed class Observation
        {
            private readonly List<object> values = new List<object>();
            private readonly List<bool> byValue = new List<bool>();
            private void Add(object value)
            {
                values.Add(value);
                byValue.Add(value == null || value is string || value.GetType().IsValueType);
            }
            private void List(IList list)
            { Add(list); Add(list.Count); foreach (object value in list) Add(value); }
            private void Fast(object action)
            {
                Add(action); if (ReferenceEquals(action, null)) return;
                Type type = action.GetType().BaseType;
                bool active = (bool)Read(type, action, "m_invocationActive");
                IList added = (IList)Read(type, action, "m_addedList");
                IList removed = (IList)Read(type, action, "m_removedList");
                Assert.That(active, Is.False, "foreign FastAction dispatch is in progress");
                Assert.That(added.Count, Is.Zero); Assert.That(removed.Count, Is.Zero);
                Add(active); List((IList)Read(type, action, "m_invocationList")); List(added); List(removed);
            }
            private void Reference(object reference)
            {
                Add(reference); Type type = reference.GetType();
                while (type != typeof(object))
                {
                    foreach (FieldInfo field in type.GetFields(Declared).OrderBy(f => f.MetadataToken))
                    {
                        object value = field.GetValue(reference); Add(field.Name); Add(value);
                        if (field.Name == "m_actionOnSystemValid") Fast(value);
                    }
                    type = type.BaseType;
                }
            }
            public static Observation Capture()
            {
                var result = new Observation();
                bool dispatching = (bool)Read(typeof(ProcessManager), null, "s_systemActionInProgress");
                Assert.That(dispatching, Is.False, "foreign ProcessManager dispatch is in progress"); result.Add(dispatching);
                IList pending = (IList)Read(typeof(ProcessManager), null, "s_actionList");
                result.List(pending);
                IDictionary systems = (IDictionary)Read(typeof(ProcessManager), null, "s_systemDictionary");
                result.Add(systems); result.Add(systems.Count);
                foreach (DictionaryEntry entry in systems)
                {
                    result.Add(entry.Key); result.Add(entry.Value);
                    object reference = Read(entry.Value.GetType(), entry.Value, "SystemRef"); result.Reference(reference);
                    IDictionary cache = (IDictionary)Read(entry.Value.GetType(), entry.Value, "SystemRefDictionary");
                    result.Add(cache); result.Add(cache.Count);
                    foreach (DictionaryEntry typed in cache)
                    { result.Add(typed.Key); result.Reference(typed.Value); }
                }
                IDictionary actions = (IDictionary)Read(typeof(ProcessManager), null, "s_systemActionLookup");
                result.Add(actions); result.Add(actions.Count);
                foreach (DictionaryEntry entry in actions)
                {
                    result.Add(entry.Key); IDictionary row = (IDictionary)entry.Value; result.Add(row); result.Add(row.Count);
                    foreach (DictionaryEntry callback in row) { result.Add(callback.Key); result.Add(callback.Value); }
                }
                IDictionary scenes = (IDictionary)Read(typeof(ActiveSelectables), null, "s_selectables");
                result.Add(scenes); result.Add(scenes.Count);
                foreach (DictionaryEntry entry in scenes) { result.Add(entry.Key); result.List((IList)entry.Value); }
                result.List((IList)Read(typeof(ActiveSelectables), null, "s_allSelectables"));
                result.Fast(InputBridge.OnShutdown); result.Fast(InputBridge.OnUpdateLastInputType);
                result.Fast(HLSelectable.OnNavigateTowardsSfx); result.Fast(HLSelectable.InitShouldHighlight);
                result.Fast(Read(typeof(HLSelectable), null, "m_onShouldHighlightChanged"));
                result.Fast(HLButton.OnClickSfx); result.Fast(HLButton.OnDisabledClickSfx);
                result.Fast(HLButton.OnDownSfx); result.Fast(HLButton.OnUpSfx);
                result.Add(Read(typeof(HLCurveAnimatedGraphic), null, "s_globalInputOnCooldown"));
                result.Add(Read(typeof(HLCurveAnimatedGraphic), null, "m_cooldownCoroutine"));
                return result;
            }
            public void AssertSame()
            {
                Observation after = Capture(); Assert.That(after.values.Count, Is.EqualTo(values.Count));
                for (int i = 0; i < values.Count; ++i)
                {
                    Assert.That(after.byValue[i], Is.EqualTo(byValue[i]));
                    if (byValue[i]) Assert.That(after.values[i], Is.EqualTo(values[i]), "global value " + i);
                    else Assert.That(ReferenceEquals(after.values[i], values[i]), Is.True, "global identity " + i);
                }
            }
        }

        private sealed class Owned : IDisposable
        {
            private readonly Observation before;
            private readonly List<string> logs = new List<string>();
            private GameObject root;
            public HLButton Button { get; private set; }
            public HLSelectableInputListener Listener { get; private set; }
            private void Log(string message, string stackTrace, LogType type) { logs.Add(type + ": " + message); }
            public Owned(bool listener)
            {
                before = Observation.Capture();
                Application.logMessageReceived += Log;
                try
                {
                    root = new GameObject("owned-original-button-listener", typeof(RectTransform));
                    root.SetActive(false);
                    if (listener) Listener = root.AddComponent<HLSelectableInputListener>();
                    else Button = root.AddComponent<HLButton>();
                    Assert.That(root.activeSelf, Is.False); Assert.That(root.activeInHierarchy, Is.False);
                    if (!listener)
                    {
                        Assert.That(Button.IsActive(), Is.False);
                        Assert.That(ReferenceEquals(Button.TargetTransform, null), Is.True, "original Awake must not have run");
                        Assert.That(Read(typeof(HLCurveAnimatedGraphic), Button, "m_vibrationManager"), Is.Null);
                    }
                    else Assert.That(Read(typeof(HLSelectableInputListener), Listener, "m_hlSelectableController"), Is.Null);
                    AssertUnchanged();
                }
                catch { Dispose(); throw; }
            }
            public void AssertUnchanged() { before.AssertSame(); Assert.That(logs, Is.Empty); }
            public void Dispose()
            {
                try { if (!ReferenceEquals(root, null)) UnityEngine.Object.DestroyImmediate(root); }
                finally
                {
                    root = null;
                    try { AssertUnchanged(); }
                    finally { Application.logMessageReceived -= Log; }
                }
            }
        }
    }
}
