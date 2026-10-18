using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ProjectLucid.Tests
{
    // Behaviour of the HLModernUI.Runtime widgets UIWidgetButtonDebounce (02000040),
    // UIWidgetToggle (0200004a), UIWidgetCheckbox (02000043), UIWidgetCollapsable
    // (02000045), UIWidgetButtonLocalised (02000042), UIWidgetNavigationHotButton
    // (0200001b), ConditionalUIEventBroadcaster (0200000e) and UIAnimatedScreenTransition
    // (0200000b), from the 1.10.1 x86-64 bodies cited per test. Components live on
    // owned GameObjects that are inactive unless a test says otherwise; the
    // navigation-manager lifecycle is not exercised here.
    [TestFixture]
    public sealed class OriginalModernUIWidgetTests
    {
        private static readonly GameInput OwnedInput = (GameInput)(int.MinValue + 0x4D33);

        private static UIWidgetToggle Toggle(OwnedObjects owned, out UIToggle toggle)
        {
            GameObject owner = owned.Inactive("Lucid owned widget toggle");
            var widget = owner.AddComponent<UIWidgetToggle>();
            toggle = owner.GetComponent<UIToggle>();
            ModernUITestSupport.Set(widget, "m_toggle", toggle);
            return widget;
        }

        // The exchange row must be absent; the fixture registers its own exchange there, so
        // publishing never reaches a shared one. The exchange constructor subscribes to
        // Shutdown, whose action row unregistering keeps, so that row is leased too.
        private static void WithOwnedExchange(Action<MessageExchangeBoundCallbackArg<UIModernMessage>> body)
        {
            string name = ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Shutdown);
            VerificationFixtureOwnership.RegistryRow row = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                row = new VerificationFixtureOwnership.RegistryRow(name);
                var exchange = new MessageExchangeBoundCallbackArg<UIModernMessage>();
                row.Register(exchange);
                body(exchange);
            }, () => row?.Dispose(), actionRows.Dispose);
        }

        // Subscribes to every UIModernEventType and records (type, event) per publish.
        private static List<(UIModernEventType, UIModernEvent)> Record(MessageExchangeBoundCallbackArg<UIModernMessage> exchange, List<Action> unsubscribe)
        {
            var received = new List<(UIModernEventType, UIModernEvent)>();
            foreach (UIModernEventType type in Enum.GetValues(typeof(UIModernEventType)))
            {
                UIModernEventType subscribed = type;
                var message = new UIModernMessage(type);
                MessageCallback<UIModernEvent> callback = (in UIModernEvent value) => received.Add((subscribed, value));
                exchange.SubscribeToMessage(in message, callback);
                unsubscribe.Add(() => exchange.UnsubscribeFromMessage(in message, callback));
            }
            return received;
        }

        // ---- UIWidgetButtonDebounce ----

        // .ctor 0x1A49440 writes 0.5f (0x3f000000) to m_timeoutInSeconds; OnEnable 0x1A491F0
        // allocates a new WaitForSeconds from it on every call, zero included.
        [Test]
        public void DebounceDelayIsRebuiltFromTheTimeoutOnEachEnable()
        {
            using (var owned = new OwnedObjects())
            {
                var debounce = owned.Inactive("Lucid owned debounce").AddComponent<UIWidgetButtonDebounce>();
                Assert.That(ModernUITestSupport.Get(debounce, "m_timeoutInSeconds"), Is.EqualTo(0.5f));
                Assert.That(ModernUITestSupport.Get(debounce, "m_debounceDelay"), Is.Null);
                ModernUITestSupport.Call(debounce, "OnEnable");
                object first = ModernUITestSupport.Get(debounce, "m_debounceDelay");
                Assert.That(ModernUITestSupport.Get(first, "m_Seconds"), Is.EqualTo(0.5f));
                ModernUITestSupport.Set(debounce, "m_timeoutInSeconds", 0f);
                ModernUITestSupport.Call(debounce, "OnEnable");
                object second = ModernUITestSupport.Get(debounce, "m_debounceDelay");
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(ModernUITestSupport.Get(second, "m_Seconds"), Is.EqualTo(0f), "a zero timeout still builds a delay");
            }
        }

        // <DebounceRoutine>d__7.MoveNext 0x1A49470: state 0 caches the button's interactable
        // field (+0xd8) into m_cachedButtonState, disables the button and yields the cached
        // m_debounceDelay; state 1 restores the cached value and nulls m_debounceRoutine.
        [Test]
        public void DebounceRoutineDisablesTheButtonUntilTheDelayThenRestoresIt()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject owner = owned.Inactive("Lucid owned debounce routine");
                var button = owner.AddComponent<Button>();
                var debounce = owner.AddComponent<UIWidgetButtonDebounce>();
                ModernUITestSupport.Set(debounce, "m_uiButton", button);
                ModernUITestSupport.Call(debounce, "OnEnable");
                object delay = ModernUITestSupport.Get(debounce, "m_debounceDelay");
                foreach (bool interactable in new[] { true, false })
                {
                    button.interactable = interactable;
                    var routine = (IEnumerator)ModernUITestSupport.Call(debounce, "DebounceRoutine");
                    Assert.That(routine.MoveNext(), Is.True);
                    Assert.That(routine.Current, Is.SameAs(delay), "yields the cached delay");
                    Assert.That(button.interactable, Is.False, "disabled while debouncing");
                    Assert.That(ModernUITestSupport.Get(debounce, "m_cachedButtonState"), Is.EqualTo(interactable));
                    Assert.That(routine.MoveNext(), Is.False);
                    Assert.That(button.interactable, Is.EqualTo(interactable), "restored to the cached state");
                    Assert.That(ModernUITestSupport.Get(debounce, "m_debounceRoutine"), Is.Null);
                }
            }
        }

        // OnPointerClick 0x1A49260 checks m_debounceRoutine, then gameObject.activeInHierarchy,
        // and only then reads the button: an inactive owner returns before the button field is
        // dereferenced and starts nothing. ClearActiveDebounce 0x1A493C0 (inlined in OnDisable
        // 0x1A49400) touches the button only for a live handle.
        [Test]
        public void DebounceIgnoresClicksOnAnInactiveOwnerAndDisableWithoutAHandle()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject owner = owned.Inactive("Lucid owned debounce click");
                var button = owner.AddComponent<Button>();
                var debounce = owner.AddComponent<UIWidgetButtonDebounce>();
                Assert.DoesNotThrow(() => debounce.OnPointerClick(null), "the null button is never read");
                ModernUITestSupport.Set(debounce, "m_uiButton", button);
                debounce.OnPointerClick(null);
                Assert.That(button.interactable, Is.True);
                Assert.That(ModernUITestSupport.Get(debounce, "m_debounceRoutine"), Is.Null);

                ModernUITestSupport.Set(debounce, "m_cachedButtonState", false);
                ModernUITestSupport.Call(debounce, "OnDisable");
                ModernUITestSupport.Call(debounce, "ClearActiveDebounce");
                Assert.That(button.interactable, Is.True, "no handle, so the cached state is not applied");
            }
        }

        // ClearActiveDebounce 0x1A493C0 stops a live handle and restores m_cachedButtonState
        // (+0x2c) without storing to m_debounceRoutine (+0x30), so the stopped handle keeps
        // OnPointerClick 0x1A49260 from starting another debounce. The debounce owner is
        // active only around each click (as OriginalInOutTestSupport.StartedCoroutine does)
        // and the Button sits on its own inactive owner, so Selectable.OnEnable never runs.
        [Test]
        public void DebounceDisableRestoresTheButtonAndKeepsTheStoppedHandle()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject owner = owned.Inactive("Lucid owned debounce disable");
                var debounce = owner.AddComponent<UIWidgetButtonDebounce>();
                var button = owned.Inactive("Lucid owned debounce button").AddComponent<Button>();
                ModernUITestSupport.Set(debounce, "m_uiButton", button);
                ModernUITestSupport.Call(debounce, "OnEnable");
                Action click = () =>
                {
                    owner.SetActive(true);
                    try { debounce.OnPointerClick(null); }
                    finally { owner.SetActive(false); }
                };

                click();
                object handle = ModernUITestSupport.Get(debounce, "m_debounceRoutine");
                Assert.That(handle, Is.Not.Null, "the click started a debounce");
                Assert.That(button.interactable, Is.False, "disabled by the routine's first step");
                Assert.That(ModernUITestSupport.Get(debounce, "m_cachedButtonState"), Is.True);

                ModernUITestSupport.Call(debounce, "OnDisable");
                Assert.That(button.interactable, Is.True, "restored to the cached state");
                Assert.That(ModernUITestSupport.Get(debounce, "m_debounceRoutine"), Is.SameAs(handle), "the stopped handle is kept");

                click();
                Assert.That(ModernUITestSupport.Get(debounce, "m_debounceRoutine"), Is.SameAs(handle), "a later click starts nothing");
                Assert.That(button.interactable, Is.True);
            }
        }

        // ---- UIWidgetToggle ----

        // .ctor 0x1A49F10 allocates Label before the MonoBehaviour constructor; Setup 0x1A49CC0
        // casts through GetAs<UIWidgetToggleParameters>, then writes Label (+0x10) and the
        // callback (+0x18); Action_OnValueChanged 0x1A49D30 forwards to a non-null callback.
        [Test]
        public void ToggleSetupStoresTheLabelAndCallback()
        {
            using (var owned = new OwnedObjects())
            {
                UIWidgetToggle widget = Toggle(owned, out _);
                Assert.That(widget.Label, Is.Not.Null);
                Assert.DoesNotThrow(() => widget.Action_OnValueChanged(true), "no callback yet");
                var calls = new List<bool>();
                widget.Setup(new UIWidgetToggleParameters("Lucid label", calls.Add));
                Assert.That(widget.Label.Value, Is.EqualTo("Lucid label"));
                widget.Action_OnValueChanged(true);
                widget.Action_OnValueChanged(false);
                Assert.That(calls, Is.EqualTo(new[] { true, false }));

                // GetAs is a cast: other parameters throw before either field is written.
                Assert.Throws<InvalidCastException>(() => widget.Setup(new UIWidgetCheckboxParameters(_ => { })));
                Assert.That(widget.Label.Value, Is.EqualTo("Lucid label"));
                widget.Action_OnValueChanged(true);
                Assert.That(calls, Is.EqualTo(new[] { true, false, true }));
            }
        }

        // SetSelected 0x1A49E00: returns only when m_mustBeInteractableToSelect (+0x28), the
        // toggle's inlined nonvirtual IsInteractable (+0x30 && +0x20) is false and selected;
        // otherwise UIToggle.Set(selected, notify: true). Truth table over the three inputs,
        // read back through the toggle's notifications. Mutations caught: blocking deselection,
        // ignoring the flag, or testing the canvas-group-free m_interactable alone.
        [Test]
        public void SetSelectedBlocksOnlySelectingANonInteractableToggle()
        {
            using (var owned = new OwnedObjects())
            {
                UIWidgetToggle widget = Toggle(owned, out UIToggle toggle);
                var notified = new List<bool>();
                toggle.OnValueChanged.AddListener(notified.Add);
                foreach (bool mustBeInteractable in new[] { false, true })
                foreach (bool interactable in new[] { false, true })
                foreach (bool groupsAllow in new[] { false, true })
                foreach (bool selected in new[] { false, true })
                {
                    string label = "must " + mustBeInteractable + ", interactable " + interactable + ", groups " + groupsAllow + ", selected " + selected;
                    ModernUITestSupport.Set(widget, "m_mustBeInteractableToSelect", mustBeInteractable);
                    ModernUITestSupport.Set(toggle, "m_interactable", interactable);
                    ModernUITestSupport.Set(toggle, "m_canvasGroupAllowInteraction", groupsAllow);
                    ModernUITestSupport.Set(toggle, "m_isOn", !selected);
                    notified.Clear();
                    widget.SetSelected(selected);
                    bool blocked = mustBeInteractable && !(interactable && groupsAllow) && selected;
                    Assert.That(toggle.IsOn, Is.EqualTo(blocked ? !selected : selected), label);
                    Assert.That(notified, Is.EqualTo(blocked ? new bool[0] : new[] { selected }), label);
                    Assert.That(widget.IsSelected(), Is.EqualTo(toggle.IsOn), "IsSelected reads IsOn (+0x40)");
                }
            }
        }

        // SetSelectedWithoutNotify 0x1A49E50 is Set(selected, false) with no interactable guard;
        // ToggleIsOn 0x1A49EA0 is Set(!isOn, true); SetInteractable 0x1A49E80 calls the virtual
        // Interactable setter (slot 18).
        [Test]
        public void ToggleForwardsTheRemainingCallsToTheUIToggle()
        {
            using (var owned = new OwnedObjects())
            {
                UIWidgetToggle widget = Toggle(owned, out UIToggle toggle);
                var notified = new List<bool>();
                toggle.OnValueChanged.AddListener(notified.Add);
                ModernUITestSupport.Set(widget, "m_mustBeInteractableToSelect", true);
                toggle.Interactable = false;
                widget.SetSelectedWithoutNotify(true);
                Assert.That(toggle.IsOn, Is.True, "no interactable guard");
                Assert.That(notified, Is.Empty, "no notification");

                widget.ToggleIsOn();
                widget.ToggleIsOn();
                Assert.That(notified, Is.EqualTo(new[] { false, true }));

                widget.SetInteractable(true);
                Assert.That(ModernUITestSupport.Get(toggle, "m_interactable"), Is.True);
                widget.SetInteractable(false);
                Assert.That(ModernUITestSupport.Get(toggle, "m_interactable"), Is.False);
            }
        }

        // SetToggleGroup 0x1A49E60 reaches UIToggleGroupElement.SetToggleGroup(group, true)
        // through the toggle's group element (+0x48); WidgetNavigation 0x1A49CB0 reads +0x30;
        // Reset 0x1A49EC0 stores GetComponent<UIToggle>().
        [Test]
        public void ToggleGroupNavigationAndResetUseTheirOwnFields()
        {
            using (var owned = new OwnedObjects())
            {
                UIWidgetToggle widget = Toggle(owned, out UIToggle toggle);
                var element = toggle.gameObject.AddComponent<UIToggleGroupElement>();
                ModernUITestSupport.Set(toggle, "m_groupElement", element);
                ModernUITestSupport.Set(element, "m_toggle", toggle);
                var group = owned.Inactive("Lucid owned toggle group").AddComponent<UIToggleGroup>();
                widget.SetToggleGroup(group);
                Assert.That(element.Group, Is.SameAs(group));
                widget.SetToggleGroup(null);
                Assert.That(element.Group, Is.Null);

                Assert.That(widget.WidgetNavigation, Is.Null);
                var navigation = owned.Inactive("Lucid owned toggle navigation").AddComponent<UIWidgetNavigation>();
                ModernUITestSupport.Set(widget, "m_uiWidgetNavigation", navigation);
                Assert.That(widget.WidgetNavigation, Is.SameAs(navigation));

                ModernUITestSupport.Set(widget, "m_toggle", null);
                ModernUITestSupport.Call(widget, "Reset");
                Assert.That(ModernUITestSupport.Get(widget, "m_toggle"), Is.SameAs(toggle));
            }
        }

        // Action_PublishUIMessage 0x1A49D60 publishes eventId under UIModernMessage(8, OnClick)
        // on the auto-created exchange with broadcast 3 (Both).
        [Test]
        public void TogglePublishesTheEventUnderOnClick()
        {
            WithOwnedExchange(exchange =>
            {
                using (var owned = new OwnedObjects())
                {
                    UIWidgetToggle widget = Toggle(owned, out _);
                    UIModernEvent eventId = owned.Own(ScriptableObject.CreateInstance<UIModernEvent>());
                    var unsubscribe = new List<Action>();
                    try
                    {
                        List<(UIModernEventType, UIModernEvent)> received = Record(exchange, unsubscribe);
                        widget.Action_PublishUIMessage(eventId);
                        Assert.That(received.Select(r => r.Item1), Is.EqualTo(new[] { UIModernEventType.OnClick }));
                        Assert.That(received[0].Item2, Is.SameAs(eventId));
                    }
                    finally { foreach (Action step in unsubscribe) step(); }
                }
            });
        }

        // ---- UIWidgetCheckbox and UIWidgetButtonLocalised ----

        // Setup 0x1A49610 stores Callback (+0x10) only for a UIWidgetCheckboxParameters;
        // anything else, null included, calls HLOutput.LogError with "Could not set up
        // UIWidgetCheckbox with given data." and keeps the old callback. Action_Toggle
        // 0x1A496D0 stores the value before invoking the callback.
        [Test]
        public void CheckboxSetupAcceptsOnlyCheckboxParameters()
        {
            using (var owned = new OwnedObjects())
            {
                var checkbox = owned.Inactive("Lucid owned checkbox").AddComponent<UIWidgetCheckbox>();
                Assert.That(checkbox.GetComponent<Toggle>(), Is.Not.Null, "RequireComponent(Toggle)");
                var seen = new List<(bool argument, object stored)>();
                checkbox.Setup(new UIWidgetCheckboxParameters(value => seen.Add((value, ModernUITestSupport.Get(checkbox, "m_isOn")))));
                checkbox.Action_Toggle(true);
                checkbox.Action_Toggle(false);
                Assert.That(seen, Is.EqualTo(new[] { (true, (object)true), (false, (object)false) }));

                using (var capture = new OwnedHLOutputErrorCapture())
                {
                    checkbox.Setup(null);
                    Assert.That(capture.Errors, Is.EqualTo(new[] { "Could not set up UIWidgetCheckbox with given data.|0" }), "null parameters");
                    checkbox.Setup(new UIWidgetToggleParameters("Lucid label", _ => { }));
                    Assert.That(capture.Errors, Is.EqualTo(new[]
                    {
                        "Could not set up UIWidgetCheckbox with given data.|0",
                        "Could not set up UIWidgetCheckbox with given data.|0",
                    }), "wrong parameter type");
                }
                checkbox.Action_Toggle(true);
                Assert.That(seen.Count, Is.EqualTo(3), "the accepted callback is kept");
                Assert.That(ModernUITestSupport.Get(checkbox, "m_isOn"), Is.True);
            }
        }

        private const Strings OwnedOpenString = Strings.MISSION_TIMER_FORMAT_DESCRIPTION;
        private const Strings OwnedCloseString = Strings.NUMBER_SEPARATOR;

        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { OwnedOpenString, "Lucid owned open" },
            { OwnedCloseString, "Lucid owned close" },
        };

        // SetLabel 0x1A49590 passes its identifier (esi) to StringTable.GetString (0x1A355E0)
        // and writes the result through the virtual Label setter; NONE returns string.Empty
        // even though the owned table is installed.
        [Test]
        public void LocalisedButtonSetsItsLabelFromTheStringTable()
        {
            using (var owned = new OwnedObjects())
            {
                var button = owned.Inactive("Lucid owned localised button").AddComponent<UIWidgetButtonLocalised>();
                OwnedStringTable.Run(OwnedStrings, () =>
                {
                    button.Label.Value = "stale";
                    button.SetLabel(OwnedOpenString);
                    Assert.That(button.Label.Value, Is.EqualTo("Lucid owned open"));
                    button.SetLabel(OwnedCloseString);
                    Assert.That(button.Label.Value, Is.EqualTo("Lucid owned close"));
                    button.SetLabel(Strings.NONE);
                    Assert.That(button.Label.Value, Is.SameAs(string.Empty));
                });
            }
        }

        // ---- UIWidgetCollapsable ----

        // Start 0x1A49740 stores sizeDelta.y; Action_OnClick 0x1A49770 flips m_isOpen and labels
        // the button with the string at 0x30 + 4 * the pre-toggle m_isOpen, so m_openString
        // (+0x30) once now open and m_closeString (+0x34) once now closed; it then keeps
        // sizeDelta.x and sets y to the stored height when open, else 0.
        [Test]
        public void CollapsableTogglesTheContentHeight()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject owner = owned.Inactive("Lucid owned collapsable");
                var collapsable = owner.AddComponent<UIWidgetCollapsable>();
                var content = owned.Inactive("Lucid owned collapsable content").AddComponent<RectTransform>();
                content.sizeDelta = new Vector2(40f, 120f);
                var button = owned.Inactive("Lucid owned collapsable button").AddComponent<UIWidgetButtonLocalised>();
                ModernUITestSupport.Set(collapsable, "m_collapsableContent", content);
                ModernUITestSupport.Set(collapsable, "m_collapsableButton", button);
                ModernUITestSupport.Set(collapsable, "m_openString", OwnedOpenString);
                ModernUITestSupport.Set(collapsable, "m_closeString", OwnedCloseString);

                ModernUITestSupport.Call(collapsable, "Start");
                Assert.That(ModernUITestSupport.Get(collapsable, "m_originalHeight"), Is.EqualTo(120f));
                content.sizeDelta = new Vector2(55f, 9f);
                button.Label.Value = "stale";

                OwnedStringTable.Run(OwnedStrings, () =>
                {
                    collapsable.Action_OnClick();
                    Assert.That(ModernUITestSupport.Get(collapsable, "m_isOpen"), Is.True);
                    Assert.That(content.sizeDelta, Is.EqualTo(new Vector2(55f, 120f)), "opening restores the stored height");
                    Assert.That(button.Label.Value, Is.EqualTo("Lucid owned open"), "now open shows m_openString");

                    collapsable.Action_OnClick();
                    Assert.That(ModernUITestSupport.Get(collapsable, "m_isOpen"), Is.False);
                    Assert.That(content.sizeDelta, Is.EqualTo(new Vector2(55f, 0f)), "closing collapses to zero");
                    Assert.That(button.Label.Value, Is.EqualTo("Lucid owned close"), "now closed shows m_closeString");
                });
            }
        }

        // ---- UIWidgetNavigationHotButton ----

        // The .ctor 0x1A3D6E0 writes m_input = 0xb6fa1284 (GameInput.None) before calling
        // ProcessManager.GetSystemRef<UINavigationManager>(null, true), which creates or reuses
        // the manager row and its typed reference; the fixture leases that row.
        private static void WithHotButton(Action<OwnedObjects, UIWidgetNavigationHotButton> body)
        {
            var row = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<UINavigationManager>());
            var owned = new OwnedObjects();
            VerificationFixtureOwnership.RunThenCleanUp(
                () => body(owned, owned.Inactive("Lucid owned hot button").AddComponent<UIWidgetNavigationHotButton>()),
                owned.Dispose, row.Dispose);
        }

        [Test]
        public void HotButtonConstructorDefaultsToNoInputAndTheSharedManagerReference()
        {
            WithHotButton((owned, hotButton) =>
            {
                Assert.That(ModernUITestSupport.Get(hotButton, "m_input"), Is.EqualTo(GameInput.None));
                Assert.That((int)GameInput.None, Is.EqualTo(unchecked((int)0xb6fa1284)));
                Assert.That(ModernUITestSupport.Get(hotButton, "m_navigationManagerRef"),
                    Is.SameAs(ProcessManager.GetSystemRef<UINavigationManager>()));
                Assert.That(ModernUITestSupport.Get(hotButton, "m_exclusive"), Is.False);
            });
        }

        // TriggerButton 0x1A3D580 calls the button's virtual Selectable.IsInteractable (slot 24)
        // and, only when true, runs the inlined OnButtonPress(true): Selected = true, the
        // callback, then onClick (+0x100). OnValidate 0x1A3D680 is empty; Reset 0x1A3D690
        // stores GetComponent<UIWidgetButton>().
        [Test]
        public void HotButtonPressesOnlyAnInteractableButton()
        {
            WithHotButton((owned, hotButton) =>
            {
                GameObject owner = hotButton.gameObject;
                var uiButton = owner.AddComponent<Button>();
                var widget = owner.AddComponent<UIWidgetButton>();
                ModernUITestSupport.Set(widget, "m_uiButton", uiButton);
                var trace = new List<string>();
                widget.Setup(new UIWidgetButtonParameters("Lucid hot", () => trace.Add("callback " + widget.Selected.Value)));
                uiButton.onClick.AddListener(() => trace.Add("click"));
                ModernUITestSupport.Set(hotButton, "m_button", null);
                ModernUITestSupport.Call(hotButton, "Reset");
                Assert.That(ModernUITestSupport.Get(hotButton, "m_button"), Is.SameAs(widget));
                ModernUITestSupport.Call(hotButton, "OnValidate");

                uiButton.interactable = false;
                ModernUITestSupport.Call(hotButton, "TriggerButton", 1f);
                Assert.That(trace, Is.Empty);
                Assert.That(widget.Selected.Value, Is.False);

                uiButton.interactable = true;
                ModernUITestSupport.Call(hotButton, "TriggerButton", 0f);
                Assert.That(trace, Is.EqualTo(new[] { "callback True", "click" }), "selected, then callback, then onClick");
            });
        }

        // OnNavigationActiveChanged 0x1A3D300 returns for a missing glyph (Unity equality) and
        // otherwise sets the glyph's GameObject active state to the value.
        [Test]
        public void HotButtonShowsTheGlyphWhileNavigationIsActive()
        {
            WithHotButton((owned, hotButton) =>
            {
                Assert.DoesNotThrow(() => ModernUITestSupport.Call(hotButton, "OnNavigationActiveChanged", true));
                GameObject glyph = owned.Inactive("Lucid owned glyph");
                ModernUITestSupport.Set(hotButton, "m_rootGlyph", glyph.transform);
                ModernUITestSupport.Call(hotButton, "OnNavigationActiveChanged", true);
                Assert.That(glyph.activeSelf, Is.True);
                ModernUITestSupport.Call(hotButton, "OnNavigationActiveChanged", false);
                Assert.That(glyph.activeSelf, Is.False);
            });
        }

        // OnEnable 0x1A3D080 subscribes TriggerButton with trigger Down (edx 1) and joystick -1,
        // calls AddExclusiveInput only when m_exclusive (+0x38) is set, and finally passes
        // OnNavigationManagerValid to m_navigationManagerRef.InvokeOnValid (+0x40). The
        // fixture swaps in its own empty reference, so that deferred call lands there and the
        // shared manager row never holds it. Mutations caught: the Held trigger, an
        // unconditional AddExclusiveInput, or a skipped InvokeOnValid.
        [Test]
        public void HotButtonEnableSubscribesDownAndAddsTheExclusiveInputOnlyWhenExclusive()
        {
            WithHotButton((owned, hotButton) =>
            {
                OwnedControlInputs.Run(() =>
                {
                    ModernUITestSupport.Set(hotButton, "m_input", OwnedInput);
                    foreach (bool exclusive in new[] { false, true })
                    {
                        var reference = new SystemRef<UINavigationManager>("Lucid owned navigation manager", null);
                        ModernUITestSupport.Set(hotButton, "m_navigationManagerRef", reference);
                        ModernUITestSupport.Set(hotButton, "m_exclusive", exclusive);
                        ModernUITestSupport.Call(hotButton, "OnEnable");
                        string label = "exclusive " + exclusive;
                        Assert.That(OwnedControlInputs.Listeners("s_onButtonsDown", OwnedInput), Is.EqualTo(new[] { "TriggerButton@-1" }), label);
                        Assert.That(OwnedControlInputs.Listeners("s_onButtonsHeld", OwnedInput), Is.Empty, label);
                        Assert.That(OwnedControlInputs.Exclusive().Contains(OwnedInput), Is.EqualTo(exclusive), label);
                        var deferred = (FastAction<UINavigationManager>)ModernUITestSupport.Get(reference, "m_actionOnSystemValid");
                        Assert.That(deferred.GetInvocationList().Select(d => d.Method.Name + "@" + ReferenceEquals(d.Target, hotButton)),
                            Is.EqualTo(new[] { "OnNavigationManagerValid@True" }), label);

                        ControlMapping.Unsubscribe(OwnedInput, TriggerButton(hotButton));
                        ControlMapping.RemoveExclusiveInput(OwnedInput);
                    }
                }, OwnedInput);
            });
        }

        private static Action<float> TriggerButton(UIWidgetNavigationHotButton hotButton) =>
            (Action<float>)typeof(UIWidgetNavigationHotButton).GetMethod("TriggerButton", ModernUITestSupport.Instance)
                .CreateDelegate(typeof(Action<float>), hotButton);

        // OnDisable 0x1A3D390 unsubscribes TriggerButton with joystick -1 (0x1A3D453, edx -1),
        // calls RemoveExclusiveInput whatever m_exclusive holds, and touches the manager only
        // when its reference is valid (the leased row is idle, so it is not).
        [Test]
        public void HotButtonDisableAlwaysRemovesTheExclusiveInput()
        {
            WithHotButton((owned, hotButton) =>
            {
                OwnedControlInputs.Run(() =>
                {
                    ModernUITestSupport.Set(hotButton, "m_input", OwnedInput);
                    ModernUITestSupport.Set(hotButton, "m_exclusive", false);
                    ControlMapping.Subscribe(OwnedInput, TriggerButton(hotButton), InputTrigger.Down);
                    ControlMapping.AddExclusiveInput(OwnedInput);
                    Assert.That(OwnedControlInputs.Listeners("s_onButtonsDown", OwnedInput), Is.EqualTo(new[] { "TriggerButton@-1" }), "precondition");
                    ModernUITestSupport.Call(hotButton, "OnDisable");
                    Assert.That(OwnedControlInputs.Exclusive().Contains(OwnedInput), Is.False);
                    Assert.That(OwnedControlInputs.Listeners("s_onButtonsDown", OwnedInput), Is.Empty);
                }, OwnedInput);
            });
        }

        // ---- ConditionalUIEventBroadcaster ----

        // Action_ConditionalEvent 0x1A3B270 picks m_eventPositive (+0x20) for true and
        // m_eventNegative (+0x28) for false, returns when ScriptableObjectWithGuid == null holds
        // (a destroyed event too), and otherwise publishes it under OnClick (8), broadcast 3.
        [Test]
        public void BroadcasterPublishesTheEventMatchingTheToggleState()
        {
            WithOwnedExchange(exchange =>
            {
                using (var owned = new OwnedObjects())
                {
                    var broadcaster = owned.Inactive("Lucid owned broadcaster").AddComponent<ConditionalUIEventBroadcaster>();
                    UIModernEvent positive = owned.Own(ScriptableObject.CreateInstance<UIModernEvent>());
                    UIModernEvent negative = owned.Own(ScriptableObject.CreateInstance<UIModernEvent>());
                    var unsubscribe = new List<Action>();
                    try
                    {
                        List<(UIModernEventType, UIModernEvent)> received = Record(exchange, unsubscribe);
                        broadcaster.Action_ConditionalEvent(true);
                        broadcaster.Action_ConditionalEvent(false);
                        Assert.That(received, Is.Empty, "no events assigned");

                        ModernUITestSupport.Set(broadcaster, "m_eventPositive", positive);
                        ModernUITestSupport.Set(broadcaster, "m_eventNegative", negative);
                        broadcaster.Action_ConditionalEvent(true);
                        broadcaster.Action_ConditionalEvent(false);
                        // By reference: fresh events share an empty GUID, so their Equals cannot tell them apart.
                        Assert.That(received.Select(r => r.Item1), Is.EqualTo(new[] { UIModernEventType.OnClick, UIModernEventType.OnClick }));
                        Assert.That(received[0].Item2, Is.SameAs(positive), "true publishes the positive event");
                        Assert.That(received[1].Item2, Is.SameAs(negative), "false publishes the negative event");

                        received.Clear();
                        UnityEngine.Object.DestroyImmediate(positive);
                        broadcaster.Action_ConditionalEvent(true);
                        Assert.That(received, Is.Empty, "a destroyed event compares equal to null");
                    }
                    finally { foreach (Action step in unsubscribe) step(); }
                }
            });
        }

        // The null check precedes GetSystemAutoCreate, so a missing event never creates the
        // exchange row (RegistryRow requires it absent and Dispose finds nothing to remove).
        [Test]
        public void BroadcasterWithoutAnEventNeverCreatesTheExchange()
        {
            string name = ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            using (var owned = new OwnedObjects())
            using (new VerificationFixtureOwnership.RegistryRow(name))
            {
                var broadcaster = owned.Inactive("Lucid owned idle broadcaster").AddComponent<ConditionalUIEventBroadcaster>();
                broadcaster.Action_ConditionalEvent(true);
                broadcaster.Action_ConditionalEvent(false);
                Assert.That(ProcessManager.IsSystemNull(name), Is.True);
                var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                Assert.That(registry.Contains(name), Is.False);
            }
        }

        // ---- UIAnimatedScreenTransition ----

        // .ctor 0x1A3ABB0 writes 1f to both speeds. StartTransition 0x1A3A900 plays the in clip
        // (the inlined PlayAnimation) before storing either callback or raising m_onStartEvent,
        // so a missing Animation faults with nothing stored.
        [Test]
        public void TransitionPlaysBeforeStoringItsCallbacks()
        {
            using (var owned = new OwnedObjects())
            {
                var transition = owned.Inactive("Lucid owned transition").AddComponent<UIAnimatedScreenTransition>();
                Assert.That(ModernUITestSupport.Get(transition, "m_transitionInSpeed"), Is.EqualTo(1f));
                Assert.That(ModernUITestSupport.Get(transition, "m_transitionOutSpeed"), Is.EqualTo(1f));
                int started = 0;
                var onStart = new UnityEvent();
                onStart.AddListener(() => started++);
                ModernUITestSupport.Set(transition, "m_onStartEvent", onStart);
                ModernUITestSupport.Set(transition, "m_animation", null);
                Assert.Throws<NullReferenceException>(() => transition.StartTransition(() => { }, () => { }));
                Assert.That(ModernUITestSupport.Get(transition, "m_midpointCallback"), Is.Null);
                Assert.That(ModernUITestSupport.Get(transition, "m_completionCallback"), Is.Null);
                Assert.That(started, Is.Zero);
            }
        }

        // StartTransition 0x1A3A900 sets the in clip (+0x28) and its state's speed from +0x30,
        // plays, stores both callbacks and only then raises m_onStartEvent (+0x48). The
        // ContinueTransition lambda <ContinueTransition>b__13_0 0x1A3ABD0 does the same with the
        // out clip (+0x38) and speed (+0x40). The owner is active so the legacy Animation builds
        // its states as it does in a scene; Unity sends no messages to it in EditMode.
        [Test]
        public void TransitionPlaysTheInClipThenTheLambdaPlaysTheOutClip()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject owner = owned.Inactive("Lucid owned transition clips");
                var animation = owner.AddComponent<Animation>();
                var transition = owner.AddComponent<UIAnimatedScreenTransition>();
                AnimationClip inClip = owned.Own(new AnimationClip { name = "Lucid in", legacy = true });
                AnimationClip outClip = owned.Own(new AnimationClip { name = "Lucid out", legacy = true });
                owner.SetActive(true);
                animation.AddClip(inClip, inClip.name);
                animation.AddClip(outClip, outClip.name);
                ModernUITestSupport.Set(transition, "m_animation", animation);
                ModernUITestSupport.Set(transition, "m_transitionInClip", inClip);
                ModernUITestSupport.Set(transition, "m_transitionInSpeed", 2f);
                ModernUITestSupport.Set(transition, "m_transitionOutClip", outClip);
                ModernUITestSupport.Set(transition, "m_transitionOutSpeed", 3f);
                var started = new List<bool>();
                var onStart = new UnityEvent();
                onStart.AddListener(() => started.Add(ModernUITestSupport.Get(transition, "m_completionCallback") != null));
                ModernUITestSupport.Set(transition, "m_onStartEvent", onStart);

                Action midpoint = () => { };
                Action completion = () => { };
                transition.StartTransition(midpoint, completion);
                Assert.That(animation.clip, Is.SameAs(inClip));
                Assert.That(animation["Lucid in"].speed, Is.EqualTo(2f));
                Assert.That(animation["Lucid out"].speed, Is.EqualTo(1f), "the out state is untouched");
                Assert.That(ModernUITestSupport.Get(transition, "m_midpointCallback"), Is.SameAs(midpoint));
                Assert.That(ModernUITestSupport.Get(transition, "m_completionCallback"), Is.SameAs(completion));
                Assert.That(started, Is.EqualTo(new[] { true }), "raised once, after both callbacks are stored");

                ModernUITestSupport.Call(transition, "<ContinueTransition>b__13_0");
                Assert.That(animation.clip, Is.SameAs(outClip));
                Assert.That(animation["Lucid out"].speed, Is.EqualTo(3f));
                Assert.That(animation["Lucid in"].speed, Is.EqualTo(2f), "the in state is untouched");
                Assert.That(started.Count, Is.EqualTo(1), "the lambda raises nothing");
            }
        }

        // Action_OnMidpointReached 0x1A3AAA0 sets the flag (read back by HasReachedMidpoint
        // 0x1A3A8F0), then invokes the midpoint callback and then m_onMidpointReachedEvent;
        // Action_OnCompletion 0x1A3AAE0 invokes only the completion callback. Null delegates and
        // events are skipped, and nothing clears the flag.
        [Test]
        public void TransitionReportsTheMidpointAndCompletionInOrder()
        {
            using (var owned = new OwnedObjects())
            {
                var transition = owned.Inactive("Lucid owned transition events").AddComponent<UIAnimatedScreenTransition>();
                Assert.That(transition.HasReachedMidpoint, Is.False);
                Assert.DoesNotThrow(transition.Action_OnMidpointReached, "null callback and event");
                Assert.DoesNotThrow(transition.Action_OnCompletion, "null completion");
                Assert.That(transition.HasReachedMidpoint, Is.True);

                var trace = new List<string>();
                ModernUITestSupport.Set(transition, "m_hasReachedMidpoint", false);
                ModernUITestSupport.Set(transition, "m_midpointCallback", (Action)(() => trace.Add("callback " + transition.HasReachedMidpoint)));
                ModernUITestSupport.Set(transition, "m_completionCallback", (Action)(() => trace.Add("complete")));
                var midpoint = new UnityEvent();
                midpoint.AddListener(() => trace.Add("event"));
                ModernUITestSupport.Set(transition, "m_onMidpointReachedEvent", midpoint);
                transition.Action_OnMidpointReached();
                transition.Action_OnCompletion();
                Assert.That(trace, Is.EqualTo(new[] { "callback True", "event", "complete" }));
                Assert.That(((IUIScreenTransition)transition).HasReachedMidpoint, Is.True);
            }
        }

        // Reset 0x1A3AB20 assigns GetComponent<Animation>() only while m_animation == null.
        [Test]
        public void TransitionResetKeepsAnAssignedAnimation()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject owner = owned.Inactive("Lucid owned transition reset");
                var local = owner.AddComponent<Animation>();
                var transition = owner.AddComponent<UIAnimatedScreenTransition>();
                var other = owned.Inactive("Lucid owned other animation").AddComponent<Animation>();
                ModernUITestSupport.Set(transition, "m_animation", other);
                ModernUITestSupport.Call(transition, "Reset");
                Assert.That(ModernUITestSupport.Get(transition, "m_animation"), Is.SameAs(other));
                ModernUITestSupport.Set(transition, "m_animation", null);
                ModernUITestSupport.Call(transition, "Reset");
                Assert.That(ModernUITestSupport.Get(transition, "m_animation"), Is.SameAs(local));
            }
        }
    }
}
