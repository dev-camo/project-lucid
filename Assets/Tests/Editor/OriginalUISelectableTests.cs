using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Tests.UISelectionShared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.UISelectable (HLUnityUI.Runtime 0x0600038e..d2)
    // against the shipped x86-64 bodies, driven through RecordingUISelectable.
    // The probe keeps Unity's lifecycle callbacks off, so objects can be
    // activated without ProcessManager registrations; OnEnable and OnDisable run
    // only where a case calls them. Each case owns its objects, bridge and
    // configuration; UISelectableStaticState saves and clears the static tables,
    // highlight actions and OnSfx for the case. SetUp first asserts that no
    // UIRuntimeConfiguration or UISelectableController system is registered.
    // Edit mode makes no EventSystem current; cases that need one call its
    // OnEnable, and TearDown calls its OnDisable.
    public sealed class OriginalUISelectableTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Declared = Any | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // SelectionState literals (dump.cs TypeDefIndex 11948).
        const int Normal = 0, Highlighted = 1, Pressed = 2, Disabled = 3;

        UISelectableStaticState m_state;
        readonly List<Object> m_owned = new List<Object>();
        EventSystem m_eventSystem;

        static List<UISelectable> AllSelectables => UISelectableStaticState.AllSelectables;
        static Dictionary<string, List<UISelectable>> SceneSelectables => UISelectableStaticState.SceneSelectables;
        static FastAction<bool> HighlightChanged => UISelectableStaticState.HighlightChanged;

        [SetUp]
        public void TakeStaticState()
        {
            Assert.That(ProcessManager.IsSystemNull<UIRuntimeConfiguration>(), Is.True, "fixture needs no UIRuntimeConfiguration system");
            Assert.That(ProcessManager.IsSystemValid<UISelectableController>(), Is.False, "fixture needs no UISelectableController system");
            m_state = new UISelectableStaticState();
        }

        [TearDown]
        public void RestoreStaticState()
        {
            if (m_eventSystem != null)
            {
                typeof(EventSystem).GetMethod("OnDisable", Any).Invoke(m_eventSystem, null);
                m_eventSystem = null;
            }
            for (int i = m_owned.Count - 1; i >= 0; i--)
            {
                if (m_owned[i] != null) Object.DestroyImmediate(m_owned[i]);
            }
            m_owned.Clear();
            m_state?.Dispose();
            m_state = null;
        }

        T Own<T>(T obj) where T : Object
        {
            m_owned.Add(obj);
            return obj;
        }

        GameObject NewObject(string name, bool active, params Type[] components)
        {
            var go = Own(new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(active);
            return go;
        }

        RecordingUISelectable NewSelectable(string name = "Selectable", bool active = false, Vector3 position = default)
        {
            GameObject go = NewObject(name, active);
            go.transform.position = position;
            return go.AddComponent<RecordingUISelectable>();
        }

        // A selectable that passes UISelectableController.IsValidSelectableForNavigation
        // while no controller system is registered.
        RecordingUISelectable NewNavigable(string name, Vector3 position)
        {
            RecordingUISelectable selectable = NewSelectable(name, true, position);
            selectable.Navigation = UINavigation.DefaultNavigation;
            selectable.Calls.Clear();
            return selectable;
        }

        UIRuntimeConfiguration NewConfiguration(RecordingUISelectable owner, bool highlightSelection)
        {
            var configuration = NewObject("Configuration", false).AddComponent<UIRuntimeConfiguration>();
            typeof(UIRuntimeConfiguration).GetField("m_shouldHighlightSelection", Any).SetValue(configuration, highlightSelection);
            Set(owner, "m_uiRuntimeConfiguration", configuration);
            return configuration;
        }

        RecordingSelectionInputBridge NewBridge(RecordingUISelectable owner)
        {
            var bridge = Own(ScriptableObject.CreateInstance<RecordingSelectionInputBridge>());
            Set(owner, "m_inputBridge", bridge);
            return bridge;
        }

        EventSystem MakeCurrentEventSystem()
        {
            Assert.That(EventSystem.current, Is.Null, "fixture needs no current EventSystem");
            m_eventSystem = NewObject("EventSystem", false).AddComponent<EventSystem>();
            typeof(EventSystem).GetMethod("OnEnable", Any).Invoke(m_eventSystem, null);
            Assert.That(EventSystem.current, Is.SameAs(m_eventSystem));
            return m_eventSystem;
        }

        // InvokeEvent needs the entry list and the haptics reference Awake would set.
        static void PrepareEvents(RecordingUISelectable selectable, params UISelectable.UIEventInfo[] entries)
        {
            Set(selectable, "m_events", entries.ToList());
            Set(selectable, "m_vibrationManager", new SystemRef<VibrationManager>("VibrationManager", null));
        }

        static UISelectable.UIEventInfo Entry(UIEventType type, RecordingUISelectable recorder, string label, int sfx = 0)
        {
            var onEvent = new UnityEvent();
            onEvent.AddListener(() => recorder.Calls.Add(label));
            return new UISelectable.UIEventInfo { m_type = type, m_sfxId = (HLAudioTypes)sfx, m_onEvent = onEvent };
        }

        static void SetNavigationFlag(RecordingUISelectable selectable, UINavigation.NavMode mode, string flag)
        {
            object navigation = new UINavigation { Mode = mode };
            if (flag != null) typeof(UINavigation).GetField(flag, Any).SetValue(navigation, true);
            selectable.Navigation = (UINavigation)navigation;
            selectable.Calls.Clear();
        }

        static object Get(object target, string field) => typeof(UISelectable).GetField(field, Any).GetValue(target);
        static void Set(object target, string field, object value) => typeof(UISelectable).GetField(field, Any).SetValue(target, value);

        static object Call(object target, string method, params object[] args)
        {
            try { return typeof(UISelectable).GetMethod(method, Any).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static object State(int value) => Enum.ToObject(typeof(UISelectable).GetNestedType("SelectionState", BindingFlags.NonPublic), value);

        [Test]
        public void DeclarationKeepsOriginalAttributesFieldOrderAndGeneratedIterator()
        {
            Type type = typeof(UISelectable);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("Hardlight.UISelectable"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(SelectableBase)));
            Assert.That(type.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] {
                typeof(AddComponentMenu), typeof(ExecuteInEditMode), typeof(SelectionBase), typeof(DisallowMultipleComponent) }));
            AddComponentMenu menu = type.GetCustomAttribute<AddComponentMenu>();
            Assert.That(menu.componentMenu, Is.EqualTo("Hardlight UI/UISelectable"));
            Assert.That(menu.componentOrder, Is.EqualTo(70));

            // dump.cs TypeDefIndex 11950: instance offsets 0x20..0xE9 and statics 0x0..0x20.
            Assert.That(type.GetFields(Declared).Select(f => f.Name).ToArray(), Is.EqualTo(new[] {
                "OnSfx", "m_events", "m_targetTransform", "m_selectedAndHighlightObject", "m_highlightParentTransform",
                "m_customisations", "m_navigation", "m_interactable", "m_neverSelectable", "m_autoSelectOnEnable",
                "m_nonInteractiveMaterial", "m_graphicsToMaterialSwapWithInteractability", "m_inputListener",
                "m_onShouldHighlightChanged", "m_highlightEnabled", "m_canvasGroupCache", "m_groupsAllowInteraction",
                "m_currentSelectionState", "m_previousSelectionState", "m_graphicAndMaterialPairs", "m_sceneName",
                "m_selectableController", "m_inputBridge", "m_uiRuntimeConfiguration", "m_vibrationManager",
                "InitShouldHighlight", "s_selectables", "s_allSelectables",
                "<IsPointerInside>k__BackingField", "<IsPointerDown>k__BackingField" }));
            FieldInfo interactable = type.GetField("m_interactable", Any);
            Assert.That(interactable.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(type.GetField("m_neverSelectable", Any).GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));

            CollectionAssert.AreEquivalent(new[] { "UIEventInfo", "GraphicAndMaterialPair", "SelectionState", "<WaitForRootObjects>d__74" },
                type.GetNestedTypes(Declared).Select(t => t.Name));
            Type iterator = type.GetNestedType("<WaitForRootObjects>d__74", BindingFlags.NonPublic);
            Assert.That(iterator.IsDefined(typeof(CompilerGeneratedAttribute), false), Is.True);
            Assert.That(iterator.GetFields(Declared).Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "<>1__state", "<>2__current", "<>4__this" }));
            Assert.That(type.GetNestedType("GraphicAndMaterialPair", BindingFlags.NonPublic).IsValueType, Is.True);
            Assert.That(Enum.GetNames(type.GetNestedType("SelectionState", BindingFlags.NonPublic)), Is.EqualTo(new[] { "Normal", "Highlighted", "Pressed", "Disabled" }));

            Type info = typeof(UISelectable.UIEventInfo);
            Assert.That(info.IsSerializable && info.IsNestedPublic, Is.True);
            Assert.That(info.GetFields(Declared).Select(f => f.Name + ":" + string.Join(",", f.GetCustomAttributesData().Select(a => a.AttributeType.Name))).ToArray(),
                Is.EqualTo(new[] { "m_type:SerializeField", "m_sfxId:HashEnumAttribute,SerializeField", "m_haptic:SerializeField,HashEnumAttribute", "m_onEvent:SerializeField" }));

            ParameterInfo createIfMissing = type.GetMethod("GetEventInfo", Any).GetParameters()[1];
            Assert.That(createIfMissing.HasDefaultValue && Equals(createIfMissing.DefaultValue, false), Is.True);
            Assert.That(type.GetMethod("IsPressed", Any, null, new[] { typeof(BaseEventData) }, null).IsDefined(typeof(ObsoleteAttribute), false), Is.True);
        }

        [Test]
        public void ConstructionEnablesInteractionHighlightingAndGroupsButNoNavigation()
        {
            RecordingUISelectable selectable = NewSelectable();
            // 0x1B53542..0x1B53587.
            Assert.That(selectable.Interactable, Is.True);
            Assert.That(Get(selectable, "m_highlightEnabled"), Is.True);
            Assert.That(Get(selectable, "m_groupsAllowInteraction"), Is.True);
            Assert.That(Get(selectable, "m_canvasGroupCache"), Is.Not.Null);
            Assert.That(selectable.Navigation.Mode, Is.EqualTo(UINavigation.NavMode.None));
            Assert.That(selectable.IsInteractable(), Is.True);
            Assert.That(UISelectable.OnSfx, Is.Not.Null);
        }

        [Test]
        public void AddKeepsTheGlobalListUniqueButAppendsToTheSceneList()
        {
            RecordingUISelectable first = NewSelectable("First"), second = NewSelectable("Second");
            UISelectable.Add(first, "Scene A");
            UISelectable.Add(first, "Scene A");
            UISelectable.Add(second, "Scene B");

            // AddUnique for the global list, List.Add for the scene list (0x1B549F9 / 0x1B54B21).
            Assert.That(AllSelectables, Is.EqualTo(new UISelectable[] { first, second }));
            Assert.That(UISelectable.GetSelectables("Scene A"), Is.EqualTo(new UISelectable[] { first, first }));
            Assert.That(UISelectable.GetSelectables("Scene B"), Is.EqualTo(new UISelectable[] { second }));
        }

        [Test]
        public void BlankSceneNamesUseOnlyTheGlobalList()
        {
            RecordingUISelectable selectable = NewSelectable();
            UISelectable.Add(selectable, " ");
            UISelectable.Add(selectable, null);
            Assert.That(SceneSelectables, Is.Empty);
            Assert.That(UISelectable.GetSelectables("\t"), Is.SameAs(AllSelectables));
            Assert.That(UISelectable.GetSelectables(null), Is.EqualTo(new UISelectable[] { selectable }));

            UISelectable.Remove(selectable, "  ");
            Assert.That(AllSelectables, Is.Empty);
        }

        [Test]
        public void GetSelectablesRegistersAnEmptyListForAnUnknownScene()
        {
            IReadOnlyCollection<UISelectable> unseen = UISelectable.GetSelectables("Unseen scene");
            Assert.That(unseen, Is.Empty);
            Assert.That(SceneSelectables.ContainsKey("Unseen scene"), Is.True);
            Assert.That(UISelectable.GetSelectables("Unseen scene"), Is.SameAs(unseen));
        }

        [Test]
        public void RemoveTakesOneSceneEntryAndTheGlobalEntry()
        {
            RecordingUISelectable selectable = NewSelectable();
            UISelectable.Add(selectable, "Scene A");
            UISelectable.Add(selectable, "Scene A");
            UISelectable.Remove(selectable, "Scene A");
            Assert.That(AllSelectables, Is.Empty);
            Assert.That(UISelectable.GetSelectables("Scene A"), Is.EqualTo(new UISelectable[] { selectable }));

            // An unknown scene name is not added by Remove.
            UISelectable.Remove(selectable, "Scene B");
            Assert.That(SceneSelectables.ContainsKey("Scene B"), Is.False);
        }

        [Test]
        public void InteractableSetterSkipsUnchangedValuesAndRespectsTheOverride()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.Interactable = true;
            Assert.That(selectable.Calls, Is.Empty);

            selectable.AllowInteractionChange = false;
            selectable.Interactable = false;
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "InteractionOverride" }));
            Assert.That(selectable.Interactable, Is.True);
        }

        [Test]
        public void InteractableChangeInvokesTheMatchingEventThenReevaluates()
        {
            RecordingUISelectable selectable = NewSelectable();
            PrepareEvents(selectable,
                Entry(UIEventType.OnInteractable, selectable, "interactable event"),
                Entry(UIEventType.OnUninteractable, selectable, "uninteractable event"));

            selectable.Interactable = false;
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "InteractionOverride", "OnInteractableChanged", "uninteractable event", "UpdateHighlight" }));
            Assert.That(selectable.IsInteractable(), Is.False);

            selectable.Calls.Clear();
            selectable.Interactable = true;
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "InteractionOverride", "OnInteractableChanged", "interactable event", "UpdateHighlight" }));
        }

        [Test]
        public void ActiveNonInteractableSelectableBecomesDisabled()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = true;
            Set(selectable, "m_interactable", false);
            selectable.SetStates(Highlighted, Normal);

            Call(selectable, "OnDidApplyAnimationProperties");
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Disabled, Highlighted }));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight", "DoStateTransition 3 False" }));
        }

        [Test]
        public void DisabledSelectableReturnsToNormalWhenInactiveOrInteractable()
        {
            // 0x1B55758: the Disabled-to-Normal branch also runs while inactive,
            // even when the selectable is still not interactable.
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = false;
            Set(selectable, "m_interactable", false);
            selectable.SetStates(Disabled, Normal);
            Call(selectable, "OnDidApplyAnimationProperties");
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Normal, Disabled }));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight", "DoStateTransition 0 False" }));

            selectable.Calls.Clear();
            selectable.ActiveOverride = true;
            Set(selectable, "m_interactable", true);
            selectable.SetStates(Disabled, Disabled);
            Call(selectable, "OnDidApplyAnimationProperties");
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Normal, Disabled }));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight", "DoStateTransition 0 False" }));
        }

        [Test]
        public void DifferingStoredStatesTransitionWithoutAChange()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = true;
            selectable.SetStates(Pressed, Normal);
            Call(selectable, "OnDidApplyAnimationProperties");
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Pressed, Normal }));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight", "DoStateTransition 2 False" }));

            selectable.Calls.Clear();
            selectable.SetStates(Pressed, Pressed);
            Call(selectable, "OnDidApplyAnimationProperties");
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight" }));
        }

        [Test]
        public void OnEnableEvaluatesInstantlyAndRegistersAndOnDisableUndoesIt()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = true;
            Set(selectable, "m_interactable", false);
            selectable.SetStates(Pressed, Pressed);
            FastAction<bool> initArgument = null;
            Action<bool> initListener = null;
            FastAction<FastAction<bool>, Action<bool>> init = null;
            init += (action, listener) => { initArgument = action; initListener = listener; };
            UISelectable.InitShouldHighlight = init;

            selectable.CallOnEnable();
            // Both states reset to Normal, then the instant evaluation disables.
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Disabled, Normal }));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight", "DoStateTransition 3 True" }));
            Assert.That(HighlightChanged.GetInvocationListCount(), Is.EqualTo(1));
            Assert.That(initArgument, Is.SameAs(HighlightChanged));
            Assert.That(initListener.Target, Is.SameAs(selectable));
            Assert.That(initListener.Method.Name, Is.EqualTo("ShouldHighlight"));
            Assert.That(selectable.SceneName, Is.EqualTo(selectable.gameObject.scene.name));
            Assert.That(AllSelectables, Is.EqualTo(new UISelectable[] { selectable }));

            selectable.Calls.Clear();
            FastAction<bool>.Invoke(HighlightChanged, false);
            Assert.That(Get(selectable, "m_highlightEnabled"), Is.False);
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight" }));

            selectable.Calls.Clear();
            selectable.PointerInside = true;
            selectable.PointerDown = true;
            selectable.CallOnDisable();
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "Deselect" }));
            Assert.That(selectable.PointerInside || selectable.PointerDown, Is.False);
            Assert.That(HighlightChanged.GetInvocationListCount(), Is.EqualTo(0));
            Assert.That(AllSelectables, Is.Empty);
        }

        [Test]
        public void NonPointerEventsUsePointerInsideWithoutAConfiguration()
        {
            RecordingUISelectable selectable = NewSelectable();
            var eventData = new BaseEventData(null);
            selectable.PointerInside = true;
            selectable.OnSelect(eventData);
            Assert.That(selectable.Calls, Is.Empty, "inactive selectables do not evaluate");

            selectable.ActiveOverride = true;
            selectable.OnSelect(eventData);
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Highlighted, Normal }));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight", "DoStateTransition 1 False" }));

            selectable.PointerInside = false;
            selectable.OnSelect(eventData);
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Normal, Highlighted }));
        }

        [Test]
        public void PointerHighlightNeedsNavigationValidityWhenSelectionIsHighlighted()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = true;
            selectable.PointerInside = true;
            var pointer = new PointerEventData(null);
            UIRuntimeConfiguration configuration = NewConfiguration(selectable, true);

            // The object is inactive with no navigation mode, so it is not a valid
            // navigation target and the pointer highlight is dropped (0x1B56629).
            selectable.OnSelect(pointer);
            Assert.That(selectable.CurrentState, Is.EqualTo(Normal));

            typeof(UIRuntimeConfiguration).GetField("m_shouldHighlightSelection", Any).SetValue(configuration, false);
            selectable.OnSelect(pointer);
            Assert.That(selectable.CurrentState, Is.EqualTo(Highlighted));
        }

        [Test]
        public void PointerEventsRequireTheConfiguration()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = true;
            selectable.PointerInside = true;
            // 0x1B565F1 reads the configuration without a null check.
            Assert.Throws<NullReferenceException>(() => selectable.OnSelect(new PointerEventData(null)));
        }

        [Test]
        public void PointerDownHonoursOnlyTheLeftButton()
        {
            RecordingUISelectable selectable = NewSelectable();
            RecordingSelectionInputBridge bridge = NewBridge(selectable);
            selectable.ActiveOverride = true;
            selectable.PointerInside = true;
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Right };

            selectable.OnPointerDown(pointer);
            Assert.That(selectable.PointerDown, Is.False);
            Assert.That(bridge.Calls.Concat(selectable.Calls), Is.Empty);

            pointer.button = PointerEventData.InputButton.Left;
            selectable.OnPointerDown(pointer);
            Assert.That(selectable.PointerDown, Is.True);
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "SetSelectedGameObjectWithMemory" }));
            Assert.That(bridge.GameObjects.Single(), Is.SameAs(selectable.gameObject));
            Assert.That(bridge.EventData.Single(), Is.SameAs(pointer));
            Assert.That(selectable.CurrentState, Is.EqualTo(Pressed));

            pointer.button = PointerEventData.InputButton.Middle;
            selectable.OnPointerUp(pointer);
            Assert.That(selectable.PointerDown, Is.True);
        }

        [Test]
        public void PointerDownOnANonInteractableSelectableSkipsTheBridgeAndDisables()
        {
            RecordingUISelectable selectable = NewSelectable();
            RecordingSelectionInputBridge bridge = NewBridge(selectable);
            selectable.ActiveOverride = true;
            selectable.PointerInside = true;
            Set(selectable, "m_interactable", false);

            selectable.OnPointerDown(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Assert.That(bridge.Calls, Is.Empty);
            // UpdateSelectionState stores Pressed first; the evaluation then disables.
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Disabled, Pressed }));
        }

        [Test]
        public void PointerEnterAndExitNeedAVisibleCursorAndCanRaiseNavigationEvents()
        {
            RecordingUISelectable selectable = NewSelectable();
            RecordingSelectionInputBridge bridge = NewBridge(selectable);
            NewConfiguration(selectable, false);
            PrepareEvents(selectable,
                Entry(UIEventType.OnNavigateToward, selectable, "navigate toward event"),
                Entry(UIEventType.OnNavigateAway, selectable, "navigate away event"));
            selectable.ActiveOverride = true;
            var pointer = new PointerEventData(null);

            selectable.OnPointerEnter(pointer);
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "GetHardwareCursorVisibility" }));
            Assert.That(selectable.PointerInside, Is.False);
            Assert.That(selectable.Calls, Is.Empty);

            bridge.CursorVisible = true;
            selectable.OnPointerEnter(pointer);
            Assert.That(selectable.PointerInside, Is.True);
            Assert.That(selectable.CurrentState, Is.EqualTo(Highlighted));
            Assert.That(selectable.Calls, Does.Not.Contain("navigate toward event"));

            SetNavigationFlag(selectable, UINavigation.NavMode.Automatic, "m_pointerEnterShouldTriggerNavigationEvent");
            selectable.OnPointerEnter(pointer);
            Assert.That(selectable.Calls.Last(), Is.EqualTo("navigate toward event"));
            selectable.OnPointerExit(pointer);
            Assert.That(selectable.PointerInside, Is.False);
            Assert.That(selectable.Calls, Does.Not.Contain("navigate away event"));

            SetNavigationFlag(selectable, UINavigation.NavMode.Automatic, "m_pointerExitShouldTriggerNavigationEvent");
            selectable.OnPointerExit(pointer);
            Assert.That(selectable.Calls.Last(), Is.EqualTo("navigate away event"));
            Assert.That(selectable.Calls, Does.Not.Contain("navigate toward event"));
        }

        [Test]
        public void InvokeEventRunsTheEntryEventBeforeTheTypeSound()
        {
            RecordingUISelectable selectable = NewSelectable();
            PrepareEvents(selectable, Entry(UIEventType.OnPress, selectable, "press event", 42));
            FastAction<HLAudioTypes> sfx = null;
            sfx += id => selectable.Calls.Add("sfx " + (int)id);
            UISelectable.OnSfx[UIEventType.OnPress] = sfx;

            selectable.CallInvokeEvent(UIEventType.OnPress);
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "press event", "sfx 42" }));

            selectable.Calls.Clear();
            selectable.CallInvokeEvent(UIEventType.OnRelease);
            Assert.That(selectable.Calls, Is.Empty);
        }

        [Test]
        public void MissingEventEntryStillPlaysTheTypeSoundWithIdZero()
        {
            // GetEventInfo hands back a detached entry whose sound id is zero.
            RecordingUISelectable selectable = NewSelectable();
            PrepareEvents(selectable);
            FastAction<HLAudioTypes> sfx = null;
            sfx += id => selectable.Calls.Add("sfx " + (int)id);
            UISelectable.OnSfx[UIEventType.OnClick] = sfx;

            selectable.CallInvokeEvent(UIEventType.OnClick);
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "sfx 0" }));
            Assert.That((List<UISelectable.UIEventInfo>)Get(selectable, "m_events"), Is.Empty);
        }

        [Test]
        public void GetEventInfoStoresAMissingEntryOnlyWhenAsked()
        {
            RecordingUISelectable selectable = NewSelectable();
            UISelectable.UIEventInfo press = Entry(UIEventType.OnPress, selectable, "press");
            PrepareEvents(selectable, press);
            var events = (List<UISelectable.UIEventInfo>)Get(selectable, "m_events");

            Assert.That(selectable.CallGetEventInfo(UIEventType.OnPress, false), Is.SameAs(press));
            UISelectable.UIEventInfo detached = selectable.CallGetEventInfo(UIEventType.OnClick, false);
            Assert.That(detached.m_type, Is.EqualTo(UIEventType.OnClick));
            Assert.That(detached.m_onEvent, Is.Null);
            Assert.That(selectable.CallGetEventInfo(UIEventType.OnClick, false), Is.Not.SameAs(detached));
            Assert.That(events.Count, Is.EqualTo(1));

            UISelectable.UIEventInfo stored = selectable.CallGetEventInfo(UIEventType.OnClick, true);
            Assert.That(events, Is.EqualTo(new[] { press, stored }));
            Assert.That(stored.m_onEvent, Is.Null);
            Assert.That(selectable.CallGetEventInfo(UIEventType.OnClick, false), Is.SameAs(stored));
        }

        [Test]
        public void SetEventHandlerReplacesTheHandlerOfTheFirstMatchingEntry()
        {
            RecordingUISelectable selectable = NewSelectable();
            UISelectable.UIEventInfo first = Entry(UIEventType.OnPress, selectable, "first");
            UISelectable.UIEventInfo duplicate = Entry(UIEventType.OnPress, selectable, "duplicate");
            PrepareEvents(selectable, first, duplicate);
            UnityEvent duplicateHandler = duplicate.m_onEvent;
            var replacement = new UnityEvent();

            Assert.That(selectable.CallSetEventHandler(UIEventType.OnPress, replacement), Is.SameAs(first));
            Assert.That(first.m_onEvent, Is.SameAs(replacement));
            Assert.That(duplicate.m_onEvent, Is.SameAs(duplicateHandler));

            UISelectable.UIEventInfo added = selectable.CallSetEventHandler(UIEventType.OnButtonUp, null);
            Assert.That((List<UISelectable.UIEventInfo>)Get(selectable, "m_events"), Is.EqualTo(new[] { first, duplicate, added }));
            Assert.That(added.m_type, Is.EqualTo(UIEventType.OnButtonUp));
            Assert.That(added.m_onEvent, Is.Null);
        }

        [Test]
        public void SetNavigationActionListensOnTheNavigateTowardEntry()
        {
            RecordingUISelectable selectable = NewSelectable();
            PrepareEvents(selectable);
            selectable.SetNavigationAction(() => selectable.Calls.Add("first action"));
            var entry = ((List<UISelectable.UIEventInfo>)Get(selectable, "m_events")).Single();
            Assert.That(entry.m_type, Is.EqualTo(UIEventType.OnNavigateToward));
            UnityEvent created = entry.m_onEvent;
            Assert.That(created, Is.Not.Null);

            selectable.SetNavigationAction(() => selectable.Calls.Add("second action"));
            Assert.That(entry.m_onEvent, Is.SameAs(created));
            selectable.CallInvokeEvent(UIEventType.OnNavigateToward);
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "first action", "second action" }));
        }

        [Test]
        public void ExplicitNavigationReturnsEachDirectionTargetUnchecked()
        {
            RecordingUISelectable selectable = NewSelectable();
            UIRuntimeConfiguration configuration = NewConfiguration(selectable, false);
            RecordingUISelectable up = NewSelectable("Up"), down = NewSelectable("Down"), left = NewSelectable("Left"), right = NewSelectable("Right");
            selectable.Navigation = new UINavigation { Mode = UINavigation.NavMode.Explicit, SelectOnUp = up, SelectOnDown = down, SelectOnLeft = left, SelectOnRight = right };

            // The targets are inactive and so not valid navigation targets.
            Assert.That(new[] { selectable.FindSelectableOnUp(), selectable.FindSelectableOnDown(), selectable.FindSelectableOnLeft(), selectable.FindSelectableOnRight() },
                Is.EqualTo(new UISelectable[] { up, down, left, right }));

            // With highlighted selection and the fallback flag, an invalid target
            // falls back to a search, which finds nothing here.
            typeof(UIRuntimeConfiguration).GetField("m_shouldHighlightSelection", Any).SetValue(configuration, true);
            Assert.That(selectable.FindSelectableOnLeft(), Is.SameAs(left));
            object navigation = selectable.Navigation;
            typeof(UINavigation).GetField("m_autoNavigateIfExplicitSelectionIsNotValid", Any).SetValue(navigation, true);
            selectable.Navigation = (UINavigation)navigation;
            Assert.That(selectable.FindSelectableOnLeft(), Is.Null);

            // The explicit branch reads the configuration without a null check (0x1B55DFC).
            Set(selectable, "m_uiRuntimeConfiguration", null);
            Assert.Throws<NullReferenceException>(() => selectable.FindSelectableOnLeft());
        }

        [Test]
        public void AutomaticNavigationSearchesOnlyTheModeAxes()
        {
            RecordingUISelectable origin = NewNavigable("Origin", Vector3.zero);
            RecordingUISelectable left = NewNavigable("Left", new Vector3(-5f, 0f, 0f));
            RecordingUISelectable up = NewNavigable("Up", new Vector3(0f, 5f, 0f));
            UISelectable.Add(left, null);
            UISelectable.Add(up, null);

            SetNavigationFlag(origin, UINavigation.NavMode.Horizontal, null);
            Assert.That(origin.FindSelectableOnLeft(), Is.SameAs(left));
            Assert.That(origin.FindSelectableOnUp(), Is.Null);

            SetNavigationFlag(origin, UINavigation.NavMode.Vertical, null);
            Assert.That(origin.FindSelectableOnLeft(), Is.Null);
            Assert.That(origin.FindSelectableOnUp(), Is.SameAs(up));
            // MoveDirection.None takes the default branch: up on the vertical axis.
            Assert.That(origin.FindSelectable_Internal(MoveDirection.None), Is.SameAs(up));

            SetNavigationFlag(origin, UINavigation.NavMode.Automatic, null);
            Assert.That(origin.FindSelectableOnRight(), Is.Null);
            Assert.That(origin.FindSelectableOnLeft(), Is.SameAs(left));

            // A configuration without highlighted selection disables the search.
            NewConfiguration(origin, false);
            Assert.That(origin.FindSelectableOnLeft(), Is.Null);
        }

        [Test]
        public void MoveNavigatesOnlyAfterFindingATarget()
        {
            RecordingUISelectable origin = NewNavigable("Origin", Vector3.zero);
            RecordingUISelectable left = NewNavigable("Left", new Vector3(-5f, 0f, 0f));
            UISelectable.Add(left, null);
            left.Calls.Clear();
            var axis = new AxisEventData(null) { moveDir = MoveDirection.Left };
            Assert.DoesNotThrow(() => origin.OnMove(axis));
            axis.moveDir = MoveDirection.Right;
            Assert.DoesNotThrow(() => origin.OnMove(axis));
            // Navigate has no effect beyond its IsActive check in this build.
            Assert.That(origin.Calls.Concat(left.Calls), Is.Empty);
            Assert.That(AllSelectables, Is.EqualTo(new UISelectable[] { left }));
        }

        [Test]
        public void GetPointOnRectEdgeScalesByTheLargerComponent()
        {
            Assert.That(UISelectable.GetPointOnRectEdge(null, Vector2.one), Is.EqualTo(Vector3.zero));
            var rect = (RectTransform)NewObject("Rect", false, typeof(RectTransform)).transform;
            rect.sizeDelta = new Vector2(200f, 100f);

            // Unit-length normalisation would give (89.4, 22.4) for the first case.
            Assert.That(UISelectable.GetPointOnRectEdge(rect, new Vector2(1f, 0.5f)), Is.EqualTo(new Vector3(100f, 25f, 0f)));
            Assert.That(UISelectable.GetPointOnRectEdge(rect, new Vector2(4f, -2f)), Is.EqualTo(new Vector3(100f, -25f, 0f)));
            Assert.That(UISelectable.GetPointOnRectEdge(rect, new Vector2(-0.5f, 0.25f)), Is.EqualTo(new Vector3(-100f, 25f, 0f)));
            Assert.That(UISelectable.GetPointOnRectEdge(rect, Vector2.zero), Is.EqualTo(Vector3.zero));

            rect.pivot = Vector2.zero;
            Assert.That(UISelectable.GetPointOnRectEdge(rect, new Vector2(1f, 0.5f)), Is.EqualTo(new Vector3(200f, 75f, 0f)));
        }

        [Test]
        public void MaterialSwapCachesStartMaterialsAndSwapsOnDisabled()
        {
            RecordingUISelectable selectable = NewSelectable();
            Material first = Own(new Material(Graphic.defaultGraphicMaterial)), second = Own(new Material(Graphic.defaultGraphicMaterial));
            Material disabled = Own(new Material(Graphic.defaultGraphicMaterial));
            Image firstImage = NewObject("First image", false, typeof(RectTransform)).AddComponent<Image>();
            Image secondImage = NewObject("Second image", false, typeof(RectTransform)).AddComponent<Image>();
            firstImage.material = first;
            secondImage.material = second;

            Set(selectable, "m_graphicsToMaterialSwapWithInteractability", new Graphic[0]);
            Call(selectable, "CacheGraphicAndMaterialPairs");
            Assert.That(Get(selectable, "m_graphicAndMaterialPairs"), Is.Null);
            Assert.DoesNotThrow(() => Call(selectable, "DoStateTransition", State(Disabled), false));

            Set(selectable, "m_graphicsToMaterialSwapWithInteractability", new Graphic[] { firstImage, secondImage });
            Set(selectable, "m_nonInteractiveMaterial", disabled);
            Call(selectable, "CacheGraphicAndMaterialPairs");
            Call(selectable, "DoStateTransition", State(Disabled), false);
            Assert.That(new[] { firstImage.material, secondImage.material }, Is.EqualTo(new[] { disabled, disabled }));

            // Every other state restores the cached start materials.
            Call(selectable, "DoStateTransition", State(Pressed), true);
            Assert.That(new[] { firstImage.material, secondImage.material }, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void CanvasGroupSearchStopsAtIgnoreParentGroups()
        {
            GameObject grandparent = NewObject("Grandparent", false);
            CanvasGroup blocking = grandparent.AddComponent<CanvasGroup>();
            blocking.interactable = false;
            GameObject parent = NewObject("Parent", true);
            parent.transform.SetParent(grandparent.transform, false);
            CanvasGroup inner = parent.AddComponent<CanvasGroup>();
            RecordingUISelectable selectable = NewSelectable();
            selectable.transform.SetParent(parent.transform, false);

            inner.ignoreParentGroups = true;
            Call(selectable, "OnCanvasGroupChanged");
            Assert.That(selectable.IsInteractable(), Is.True);
            Assert.That(selectable.Calls, Is.Empty, "no change, no re-evaluation");

            inner.ignoreParentGroups = false;
            Call(selectable, "OnCanvasGroupChanged");
            Assert.That(selectable.IsInteractable(), Is.False);
            Assert.That(selectable.Interactable, Is.True);
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight" }));
        }

        [Test]
        public void TransformSettersReevaluateOnlyOnChangeAndHighlightParentFallsBack()
        {
            RecordingUISelectable selectable = NewSelectable();
            Transform other = NewObject("Other", false).transform;
            Assert.That(selectable.HighlightParentTransform, Is.SameAs(selectable.transform));
            Assert.That(selectable.TargetTransform, Is.Null, "Awake has not run");

            selectable.HighlightParentTransform = other;
            selectable.TargetTransform = other;
            Assert.That(selectable.HighlightParentTransform, Is.SameAs(other));
            Assert.That(selectable.TargetTransform, Is.SameAs(other));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight", "UpdateHighlight" }));

            selectable.HighlightParentTransform = other;
            selectable.TargetTransform = other;
            Assert.That(selectable.Calls.Count, Is.EqualTo(2));
        }

        [Test]
        public void IsSelectedAsksTheControllerWhenSelectionIsHighlighted()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = true;
            NewConfiguration(selectable, true);
            // Inactive and never enabled, so HighlightThis returns at once.
            var controller = NewObject("Controller", false).AddComponent<UISelectableController>();
            FieldInfo currentlySelected = typeof(UISelectableController).GetField("m_currentlySelected", Any);
            currentlySelected.SetValue(controller, selectable);
            Set(selectable, "m_selectableController", controller);

            // The initial state comes from IsSelected (0x1B525E9), whose controller
            // route compares the controller's current selection (0x1B55056).
            selectable.CallOnEnable();
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Highlighted, Normal }));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight", "DoStateTransition 1 True" }));

            // Neither pressed nor highlighted: the fallback still asks IsSelected.
            selectable.Calls.Clear();
            selectable.OnSelect(new BaseEventData(null));
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Highlighted, Highlighted }));
            Assert.That(selectable.Calls, Is.EqualTo(new[] { "UpdateHighlight" }));

            currentlySelected.SetValue(controller, null);
            selectable.OnSelect(new BaseEventData(null));
            Assert.That(new[] { selectable.CurrentState, selectable.PreviousState }, Is.EqualTo(new[] { Normal, Highlighted }));
        }

        [Test]
        public void IsSelectedAsksTheBridgeOnlyWithACurrentEventSystem()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = true;
            NewConfiguration(selectable, false);
            RecordingSelectionInputBridge bridge = NewBridge(selectable);
            bridge.ModuleExists = bridge.ObjectSelected = true;
            var eventData = new BaseEventData(null);

            selectable.OnSelect(eventData);
            Assert.That(selectable.CurrentState, Is.EqualTo(Normal));
            Assert.That(bridge.Calls, Is.Empty);

            // 0x1B55081..0x1B55147: EventSystem.current, InputModuleExists, then
            // IsObjectSelected with this object.
            MakeCurrentEventSystem();
            selectable.OnSelect(eventData);
            Assert.That(selectable.CurrentState, Is.EqualTo(Highlighted));
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "InputModuleExists", "IsObjectSelected" }));
            Assert.That(bridge.GameObjects, Is.EqualTo(new[] { selectable.gameObject }));

            // Without an input module the object is not asked; UpdateHighlight
            // asks again because the state is no longer Highlighted.
            bridge.Calls.Clear();
            bridge.ModuleExists = false;
            selectable.OnSelect(eventData);
            Assert.That(selectable.CurrentState, Is.EqualTo(Normal));
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "InputModuleExists", "InputModuleExists" }));
        }

        [Test]
        public void PointerHighlightComparesThePressedObject()
        {
            RecordingUISelectable selectable = NewSelectable();
            selectable.ActiveOverride = true;
            NewConfiguration(selectable, false);
            GameObject other = NewObject("Other", false);
            var pointer = new PointerEventData(null);

            int Evaluate(bool down, bool inside, GameObject pressed)
            {
                selectable.PointerDown = down;
                selectable.PointerInside = inside;
                pointer.pointerPress = pressed;
                selectable.OnSelect(pointer);
                return selectable.CurrentState;
            }

            // Pressed here and dragged outside (first term, 0x1B564DB..0x1B56539).
            Assert.That(Evaluate(true, false, selectable.gameObject), Is.EqualTo(Highlighted));
            Assert.That(Evaluate(false, true, selectable.gameObject), Is.EqualTo(Highlighted));
            Assert.That(Evaluate(false, true, null), Is.EqualTo(Highlighted));
            // Inside, but another object holds the press.
            Assert.That(Evaluate(false, true, other), Is.EqualTo(Normal));
            Assert.That(Evaluate(true, false, other), Is.EqualTo(Normal));
            Assert.That(Evaluate(true, false, null), Is.EqualTo(Normal));
        }

        [Test]
        public void HighlightObjectFollowsTheHighlightWithoutSelectionHighlighting()
        {
            RecordingUISelectable selectable = NewSelectable();
            GameObject highlight = NewObject("Highlight", false);
            Set(selectable, "m_selectedAndHighlightObject", highlight);

            // show = m_highlightEnabled && (Highlighted || IsSelected()), 0x1B5621A..0x1B562CB.
            selectable.SetStates(Highlighted, Highlighted);
            selectable.CallShouldHighlight(true);
            Assert.That(highlight.activeSelf, Is.True);
            selectable.CallShouldHighlight(false);
            Assert.That(highlight.activeSelf, Is.False);
            selectable.SetStates(Normal, Normal);
            selectable.CallShouldHighlight(true);
            Assert.That(highlight.activeSelf, Is.False);

            // Selection highlighting only goes through the controller.
            NewConfiguration(selectable, true);
            highlight.SetActive(true);
            selectable.CallShouldHighlight(false);
            Assert.That(highlight.activeSelf, Is.True);
        }

        [Test]
        public void SelectNeedsACurrentEventSystemButDeselectDoesNot()
        {
            RecordingUISelectable selectable = NewSelectable();
            RecordingSelectionInputBridge bridge = NewBridge(selectable);
            // Select dereferences EventSystem.current unchecked (0x1B56B04..0x1B56BCA).
            Assert.Throws<NullReferenceException>(() => selectable.Select());
            selectable.Deselect();
            Assert.That(bridge.Calls, Is.Empty);

            MakeCurrentEventSystem();
            selectable.Select();
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "SetSelectedGameObjectWithMemory" }));
            Assert.That(bridge.GameObjects, Is.EqualTo(new[] { selectable.gameObject }));
            Assert.That(bridge.EventData, Is.EqualTo(new BaseEventData[] { null }));

            bridge.Calls.Clear();
            bridge.GameObjects.Clear();
            selectable.Deselect();
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "DeselectObjectIfCurrentlySelected" }));
            Assert.That(bridge.GameObjects, Is.EqualTo(new[] { selectable.gameObject }));

            Set(selectable, "m_inputBridge", null);
            Assert.DoesNotThrow(() => selectable.Select());
            Assert.DoesNotThrow(() => selectable.Deselect());
        }

        [Test]
        public void NavigationSearchesUseTheRotatedDirection()
        {
            RecordingUISelectable origin = NewNavigable("Origin", Vector3.zero);
            RecordingUISelectable left = NewNavigable("Left candidate", new Vector3(-5f, 0f, 0f));
            RecordingUISelectable right = NewNavigable("Right candidate", new Vector3(5f, 0f, 0f));
            UISelectable.Add(left, null);
            UISelectable.Add(right, null);
            object navigation = new UINavigation { Mode = UINavigation.NavMode.Explicit, SelectOnLeft = NewSelectable("Inactive target") };
            typeof(UINavigation).GetField("m_autoNavigateIfExplicitSelectionIsNotValid", Any).SetValue(navigation, true);
            origin.Navigation = (UINavigation)navigation;
            NewConfiguration(origin, true);

            // The invalid explicit target falls back to a search (0x1B55F6D..0x1B55F91).
            Assert.That(origin.FindSelectableOnLeft(), Is.SameAs(left));
            // Turned half way round, left is world right.
            origin.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            Assert.That(origin.FindSelectableOnLeft(), Is.SameAs(right));

            // The automatic search rotates the same way (0x1B55EA8..0x1B55ED0).
            SetNavigationFlag(origin, UINavigation.NavMode.Automatic, null);
            Set(origin, "m_uiRuntimeConfiguration", null);
            Assert.That(origin.FindSelectableOnLeft(), Is.SameAs(right));
            origin.transform.rotation = Quaternion.identity;
            Assert.That(origin.FindSelectableOnLeft(), Is.SameAs(left));
        }
    }
}
