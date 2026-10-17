using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Tests.UISelectionShared;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests
{
    // Hardlight.UIClickable (TypeDef 0x0200006f, x86-64 0x1B510E0..0x1B54080) and Hardlight.UIButton
    // (0x0200006e, 0x1B510C0..0x1B521B0). Every case runs on an inactive owned component whose
    // UISelectable lifecycle never runs; the fixture authors the UISelectable event table (one
    // recording UnityEvent per UIEventType), an empty vibration reference and the target transform,
    // and restores both UIClickable statics, the EventSystem list and the ProcessManager rows it uses.
    // Probes steer the original virtual IsActive/IsInteractable/InteractionOverride calls.
    // Not covered here: Awake/OnEnable/OnDisable (they chain into the UISelectable lifecycle) and any
    // path that starts a coroutine on an active owner (StartCoroutine, CoroutineUtils.RunCoroutine).
    public sealed class OriginalUIClickableTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static readonly Type SelectionState = typeof(UISelectable).GetNestedType("SelectionState", BindingFlags.NonPublic);
        private static readonly Type AnimationState = typeof(UIClickable).GetNestedType("AnimationState", BindingFlags.NonPublic);

        // dump.cs TypeDefIndex 11940/11935..11939 and the DummyDll flag and attribute rows. The iterator
        // names d__34..d__37 fix the member order (18 fields, the enum, 5 property members, 10 methods).
        [Test]
        public void ClickableDeclarationsKeepOriginalFlagsFieldsAndIteratorNames()
        {
            Type type = typeof(UIClickable);
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(UISelectable)));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.GetInterfaces(), Is.SupersetOf(new[] { typeof(IPointerClickHandler), typeof(IEventSystemHandler), typeof(ISubmitHandler) }));
            SetOptions(type);
            Assert.That(type.GetCustomAttributesData().Count, Is.EqualTo(2), "no AddComponentMenu on UIClickable");
            Fields(type,
                new[] { "s_globalInputOnCooldown", "m_cooldownTimeInSeconds", "m_animationDisabled", "m_allowMultiTouchClickthrough",
                    "m_curveCollection", "m_cachedLocalPosition", "m_cachedLocalScale", "m_cachedLocalRotation", "m_hasCached",
                    "m_clickAccepted", "m_shouldResetInactiveAnim", "m_onCooldown", "m_hasStatusEffects", "m_buttonStatusEffects",
                    "m_currentAnimatingState", "m_animatingCoroutine", "m_cooldownCoroutine", "m_waitForUICacheLocalTransformCoroutine" },
                new[] { typeof(bool), typeof(float), typeof(bool), typeof(bool), typeof(HLCurveAnimatedGraphicCurveCollection),
                    typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool),
                    typeof(List<ButtonStatusEffects>), AnimationState, typeof(Coroutine), typeof(Coroutine), typeof(Coroutine) },
                new[] { 17, 4, 4, 4, 4, 1, 1, 1, 1, 4, 1, 1, 1, 33, 1, 1, 17, 1 });
            foreach (FieldInfo field in type.GetFields(Declared))
            {
                bool serialized = new[] { "m_cooldownTimeInSeconds", "m_animationDisabled", "m_allowMultiTouchClickthrough", "m_curveCollection" }.Contains(field.Name);
                AttributeRows(field, serialized ? new[] { typeof(SerializeField) } : new Type[0]);
            }

            Assert.That(type.GetProperties(Declared).Select(p => p.Name), Is.EquivalentTo(new[] { "CooldownTimeInSeconds", "AnimationDisabled" }));
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(22));
            Method(type, "get_CooldownTimeInSeconds", 2182, typeof(float), Type.EmptyTypes, new string[0]);
            Method(type, "get_AnimationDisabled", 2182, typeof(bool), Type.EmptyTypes, new string[0]);
            Method(type, "set_AnimationDisabled", 2182, typeof(void), new[] { typeof(bool) }, new[] { "value" });
            Method(type, "Awake", 196, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "OnEnable", 196, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "OnDisable", 196, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "CacheLocalTransform", 129, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "ResetTransformToCache", 129, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "OnPointerClick", 454, typeof(void), new[] { typeof(PointerEventData) }, new[] { "eventData" });
            Method(type, "OnSubmit", 454, typeof(void), new[] { typeof(BaseEventData) }, new[] { "eventData" });
            Method(type, "Press", 454, typeof(bool), Type.EmptyTypes, new string[0]);
            Method(type, "DoStateTransition", 196, typeof(void), new[] { SelectionState, typeof(bool) }, new[] { "state", "instant" });
            Method(type, "OnRectTransformDimensionsChange", 196, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "StopAnimationCoroutine", 132, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "CancelGlobalInputCooldown", 150, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "RegisterButtonStatusEffector", 134, typeof(void), new[] { typeof(ButtonStatusEffects) }, new[] { "effector" });
            Method(type, "TriggerButtonStatusAffectors", 132, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, ".ctor", 6278, null, Type.EmptyTypes, new string[0]);

            string[] iterators = { "AnimateOnPress", "AnimateOnRelease", "AnimateOnPressDisabled", "GlobalCooldownClicks" };
            string[] generated = { "<AnimateOnPress>d__34", "<AnimateOnRelease>d__35", "<AnimateOnPressDisabled>d__36", "<GlobalCooldownClicks>d__37" };
            for (int i = 0; i < iterators.Length; i++)
            {
                bool hasInstant = i < 3;
                MethodInfo method = (MethodInfo)Method(type, iterators[i], hasInstant ? 132 : 129, typeof(IEnumerator),
                    hasInstant ? new[] { typeof(bool) } : Type.EmptyTypes, hasInstant ? new[] { "instant" } : new string[0]);
                if (hasInstant)
                {
                    ParameterInfo instant = method.GetParameters().Single();
                    Assert.That(instant.Attributes, Is.EqualTo(ParameterAttributes.Optional | ParameterAttributes.HasDefault));
                    Assert.That(instant.DefaultValue, Is.EqualTo(false));
                }
                CustomAttributeData iterator = method.GetCustomAttributesData().Single();
                Assert.That(iterator.AttributeType, Is.EqualTo(typeof(IteratorStateMachineAttribute)));
                Type machine = (Type)iterator.ConstructorArguments.Single().Value;
                Assert.That(machine.Name, Is.EqualTo(generated[i]));
                Assert.That((int)machine.Attributes, Is.EqualTo(1048835));
                Assert.That(machine.GetCustomAttributesData().Select(a => a.AttributeType), Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
            }

            // Hoisted locals at +0x2C/+0x30(/+0x34) of the optimised compile (the project builds with
            // /optimize+); the per-frame x/y sample locals are not hoisted.
            IteratorFields(type, generated[0], new[] { "instant", "<endTime>5__2", "<timer>5__3" }, new[] { typeof(bool), typeof(float), typeof(float) });
            IteratorFields(type, generated[1], new[] { "instant", "<endTime>5__2", "<timer>5__3" }, new[] { typeof(bool), typeof(float), typeof(float) });
            IteratorFields(type, generated[2], new[] { "instant", "<endTime>5__2", "<timer>5__3", "<scaler>5__4" },
                new[] { typeof(bool), typeof(float), typeof(float), typeof(Vector2) });
            IteratorFields(type, generated[3], new string[0], new Type[0]);

            Assert.That((int)AnimationState.Attributes, Is.EqualTo(259));
            Assert.That(Enum.GetNames(AnimationState), Is.EqualTo(new[] { "NotAnimating", "AnimatingPress", "AnimatingRelease", "AnimatingDisabled" }));
            Assert.That(Enum.GetValues(AnimationState).Cast<object>().Select(Convert.ToInt32), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(type.GetNestedTypes(Declared).Length, Is.EqualTo(5));
        }

        // dump.cs TypeDefIndex 11934: no fields beyond the two pointer flags; the six UnityEvent
        // properties have no backing fields of their own.
        [Test]
        public void ButtonDeclarationsKeepOriginalFlagsFieldsAndAttributeRows()
        {
            Type type = typeof(UIButton);
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(UIClickable)));
            CustomAttributeData[] attributes = type.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Select(a => a.AttributeType),
                Is.EqualTo(new[] { typeof(AddComponentMenu), typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) }));
            Assert.That(attributes[0].ConstructorArguments.Select(a => a.Value), Is.EqualTo(new object[] { "Hardlight UI/UIButton", 30 }));
            SetOptions(type);
            Fields(type, new[] { "m_pointerExitShouldTriggerButtonUp", "m_wasPointerDownAndInside" }, new[] { typeof(bool), typeof(bool) }, new[] { 1, 1 });
            FieldInfo exitUp = type.GetField("m_pointerExitShouldTriggerButtonUp", Declared);
            AttributeRows(exitUp, typeof(TooltipAttribute), typeof(SerializeField));
            Assert.That(exitUp.GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("When button is pressed but pointer exits, should we trigger the button to be up or wait for pointer up."));
            AttributeRows(type.GetField("m_wasPointerDownAndInside", Declared));

            string[] events = { "OnClick", "OnDisabledClick", "OnButtonDown", "OnButtonUp", "OnButtonExit", "OnOfflineClick" };
            Assert.That(type.GetProperties(Declared).Select(p => p.Name), Is.EqualTo(events));
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(32));
            Method(type, "OnEnable", 196, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "OnDisable", 196, typeof(void), Type.EmptyTypes, new string[0]);
            foreach (string name in events)
            {
                Method(type, "get_" + name, 2182, typeof(UnityEvent), Type.EmptyTypes, new string[0]);
                Method(type, "set_" + name, 2182, typeof(void), new[] { typeof(UnityEvent) }, new[] { "value" });
            }
            foreach (string name in new[] { "Action_OnClick", "Action_OnDisabledClick", "Action_OnButtonDown", "Action_OnButtonUp", "Action_OnButtonExit" })
                Method(type, name, 134, typeof(void), Type.EmptyTypes, new string[0]);
            Method(type, "Press", 198, typeof(bool), Type.EmptyTypes, new string[0]);
            foreach (string name in new[] { "OnPointerDown", "OnPointerUp", "OnPointerEnter", "OnPointerExit" })
                Method(type, name, 198, typeof(void), new[] { typeof(PointerEventData) }, new[] { "eventData" });
            Method(type, "PublishUIMessage", 134, typeof(void), new[] { typeof(UIEventType), typeof(UIEvent) }, new[] { "type", "eventId" });
            foreach (string name in new[] { "OnClick", "OnDisabledClick", "OnOfflineClick", "OnButtonDown", "OnButtonUp", "OnButtonExit" })
                Method(type, "PublishUIMessage" + name, 134, typeof(void), new[] { typeof(UIEvent) }, new[] { "eventId" });
            Method(type, ".ctor", 6278, null, Type.EmptyTypes, new string[0]);
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
        }

        // .ctor 0x1B521B0 stores 0.15f (0x3E19999A) and a fresh list before the UISelectable base;
        // UIButton .ctor 0x1B52190 sets m_pointerExitShouldTriggerButtonUp first. 0x1B522E0..0x1B52300
        // are plain field accessors.
        [Test]
        public void ConstructorsSeedCooldownListAndExitFlagAndAccessorsUseTheirFields()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                Assert.That(button.CooldownTimeInSeconds, Is.EqualTo(0.15f));
                Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(button.CooldownTimeInSeconds), 0), Is.EqualTo(0x3E19999A));
                Assert.That(owned.ReadClickable("m_buttonStatusEffects"), Is.InstanceOf<List<ButtonStatusEffects>>().And.Empty);
                Assert.That(owned.ReadButton("m_pointerExitShouldTriggerButtonUp"), Is.EqualTo(true));
                Assert.That(owned.ReadButton("m_wasPointerDownAndInside"), Is.EqualTo(false));
                Assert.That(button.AnimationDisabled, Is.False);
                button.AnimationDisabled = true;
                Assert.That(owned.ReadClickable("m_animationDisabled"), Is.EqualTo(true));
                owned.WriteClickable("m_animationDisabled", false);
                Assert.That(button.AnimationDisabled, Is.False);
                owned.WriteClickable("m_cooldownTimeInSeconds", 2.5f);
                Assert.That(button.CooldownTimeInSeconds, Is.EqualTo(2.5f));
                owned.AssertUnchanged();
            }
        }

        // UIClickable.Press 0x1B51970 accepted branch (0x1B51AC0): the result is
        // m_currentAnimatingState != AnimatingDisabled. A positive cooldown on an owner that is not
        // active in the hierarchy (0x1B51AF4) starts no cooldown coroutine.
        [Test]
        public void AcceptedPressIsRefusedOnlyDuringTheDisabledShakeAndStartsNoCooldownWhenInactive()
        {
            using (var owned = new Owned<UIClickableProbe>())
            {
                UIClickableProbe clickable = owned.Component;
                clickable.ActiveResult = true;
                clickable.InteractableResult = true;
                owned.WriteClickable("m_cooldownTimeInSeconds", 5f);
                Assert.That(clickable.Press(), Is.True);
                Assert.That(owned.ReadClickable("m_clickAccepted"), Is.EqualTo(true));
                Assert.That(owned.ReadClickable("s_globalInputOnCooldown"), Is.EqualTo(false));
                Assert.That(owned.ReadClickable("m_cooldownCoroutine"), Is.Null);

                owned.WriteClickable("m_currentAnimatingState", Animation(3));
                Assert.That(clickable.Press(), Is.False);
                Assert.That(owned.ReadClickable("m_clickAccepted"), Is.EqualTo(false));
                Assert.That(owned.ReadClickable("m_shouldResetInactiveAnim"), Is.EqualTo(false), "the restart flag belongs to the rejected branch");
                Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(3)));

                owned.WriteClickable("m_currentAnimatingState", Animation(1));
                Assert.That(clickable.Press(), Is.True);
                Assert.That(owned.Events, Is.Empty, "UIClickable.Press raises no event itself");
                owned.AssertUnchanged();
            }
        }

        // Rejected branch 0x1B519E2: inactive, uninteractable or global cooldown each clear
        // m_clickAccepted; with the animation disabled no shake coroutine is started (0x1B51A34).
        [Test]
        public void RejectedPressClearsTheClickWithoutAnimatingWhenAnimationIsDisabled()
        {
            using (var owned = new Owned<UIClickableProbe>())
            {
                UIClickableProbe clickable = owned.Component;
                clickable.AnimationDisabled = true;
                foreach (int caseIndex in new[] { 0, 1, 2 })
                {
                    clickable.ActiveResult = caseIndex != 0;
                    clickable.InteractableResult = caseIndex != 1;
                    owned.WriteClickable("s_globalInputOnCooldown", caseIndex == 2);
                    owned.WriteClickable("m_clickAccepted", true);
                    owned.WriteClickable("m_currentAnimatingState", Animation(2));
                    Assert.That(clickable.Press(), Is.False, "case " + caseIndex);
                    Assert.That(owned.ReadClickable("m_clickAccepted"), Is.EqualTo(false), "case " + caseIndex);
                    Assert.That(owned.ReadClickable("m_animatingCoroutine"), Is.Null, "case " + caseIndex);
                    Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(2)), "no coroutine, so no stop either");
                    Assert.That(owned.ReadClickable("m_shouldResetInactiveAnim"), Is.EqualTo(false));
                }
                Assert.That(owned.ReadClickable("s_globalInputOnCooldown"), Is.EqualTo(true), "a rejected press leaves the flag alone");
                owned.AssertUnchanged();
            }
        }

        // UIButton.Press 0x1B51920: OnClick (8) when accepted, OnDisabledClick (9) only when the
        // refusal comes with IsInteractable() false; a cooldown or inactive refusal raises nothing.
        [Test]
        public void ButtonPressReportsClickOrDisabledClickByInteractability()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                button.AnimationDisabled = true;
                owned.WriteClickable("m_cooldownTimeInSeconds", 0f);

                button.ActiveResult = true;
                button.InteractableResult = true;
                Assert.That(button.Press(), Is.True);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnClick }));

                owned.WriteClickable("s_globalInputOnCooldown", true);
                Assert.That(button.Press(), Is.False);
                Assert.That(owned.TakeEvents(), Is.Empty);
                owned.WriteClickable("s_globalInputOnCooldown", false);

                button.ActiveResult = false;
                Assert.That(button.Press(), Is.False);
                Assert.That(owned.TakeEvents(), Is.Empty);

                button.InteractableResult = false;
                Assert.That(button.Press(), Is.False);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnDisabledClick }));

                button.ActiveResult = true;
                Assert.That(button.Press(), Is.False);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnDisabledClick }));

                // Accepted by interactability but refused by the disabled shake: neither event.
                button.InteractableResult = true;
                owned.WriteClickable("m_currentAnimatingState", Animation(3));
                Assert.That(button.Press(), Is.False);
                Assert.That(owned.TakeEvents(), Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // Action_OnClick 0x1B517B0 and Action_OnDisabledClick 0x1B517F0 both dispatch vtable slot 49
        // (Press) behind IsActive; the down/up/exit actions also need IsInteractable and raise 11/12/14.
        [Test]
        public void ActionsPressBehindActivityAndRaiseButtonEventsOnlyWhenInteractable()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                button.AnimationDisabled = true;
                owned.WriteClickable("m_cooldownTimeInSeconds", 0f);
                button.InteractableResult = true;

                button.ActiveResult = false;
                button.Action_OnClick();
                button.Action_OnDisabledClick();
                button.Action_OnButtonDown();
                button.Action_OnButtonUp();
                button.Action_OnButtonExit();
                Assert.That(owned.TakeEvents(), Is.Empty);

                button.ActiveResult = true;
                button.Action_OnClick();
                button.Action_OnDisabledClick();
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnClick, UIEventType.OnClick }));
                button.Action_OnButtonDown();
                button.Action_OnButtonUp();
                button.Action_OnButtonExit();
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonDown, UIEventType.OnButtonUp, UIEventType.OnButtonExit }));

                button.InteractableResult = false;
                button.Action_OnButtonDown();
                button.Action_OnButtonUp();
                button.Action_OnButtonExit();
                Assert.That(owned.TakeEvents(), Is.Empty);
                button.Action_OnDisabledClick();
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnDisabledClick }));
                owned.AssertUnchanged();
            }
        }

        // The six properties read GetEventInfo(type, false).m_onEvent (0x1B513B0 etc.) and assign through
        // SetEventHandler; event types 8, 9, 11, 12, 14, 10 in declaration order.
        [Test]
        public void EventPropertiesAreViewsOntoTheirEventTableEntries()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                var expected = new Dictionary<string, UIEventType>
                {
                    { "OnClick", UIEventType.OnClick }, { "OnDisabledClick", UIEventType.OnDisabledClick },
                    { "OnButtonDown", UIEventType.OnButtonDown }, { "OnButtonUp", UIEventType.OnButtonUp },
                    { "OnButtonExit", UIEventType.OnButtonExit }, { "OnOfflineClick", UIEventType.OnOfflineClick },
                };
                foreach (KeyValuePair<string, UIEventType> pair in expected)
                {
                    PropertyInfo property = typeof(UIButton).GetProperty(pair.Key);
                    Assert.That(property.GetValue(button), Is.SameAs(owned.Entry(pair.Value).m_onEvent), pair.Key);
                    var replacement = new UnityEvent();
                    property.SetValue(button, replacement);
                    Assert.That(owned.Entry(pair.Value).m_onEvent, Is.SameAs(replacement), pair.Key);
                    Assert.That(property.GetValue(button), Is.SameAs(replacement), pair.Key);
                }
                Assert.That(owned.EventTable.Count, Is.EqualTo(Enum.GetValues(typeof(UIEventType)).Length), "assignment replaced in place");

                // A missing entry reads as null and is not added by the read.
                owned.EventTable.RemoveAll(e => e.m_type == UIEventType.OnClick);
                int count = owned.EventTable.Count;
                Assert.That(button.OnClick, Is.Null);
                Assert.That(owned.EventTable.Count, Is.EqualTo(count));
                owned.AssertUnchanged();
            }
        }

        // OnPointerClick 0x1B52A50: without multi-touch clickthrough only pointer ids -1 and 0 press
        // ((id + 1) > 1 unsigned rejects, 0x1B52A6D). The press is observed as UIButton's disabled click.
        [Test]
        public void PointerClickPressesOnlyForMouseOrFirstTouchUnlessClickthroughIsAllowed()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                button.AnimationDisabled = true;
                foreach (int pointerId in new[] { -2, -1, 0, 1, 7 })
                {
                    button.OnPointerClick(Pointer(PointerEventData.InputButton.Left, pointerId));
                    Assert.That(owned.TakeEvents(), Is.EqualTo(pointerId == -1 || pointerId == 0 ? new[] { UIEventType.OnDisabledClick } : new UIEventType[0]),
                        "pointer " + pointerId);
                }
                owned.WriteClickable("m_allowMultiTouchClickthrough", true);
                foreach (int pointerId in new[] { -2, 1, 7 })
                {
                    button.OnPointerClick(Pointer(PointerEventData.InputButton.Left, pointerId));
                    Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnDisabledClick }), "pointer " + pointerId);
                }
                owned.AssertUnchanged();
            }
        }

        // OnSubmit 0x1B52BE0 presses unconditionally, then transitions only when active and interactable.
        [Test]
        public void SubmitAlwaysPresses()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                button.AnimationDisabled = true;
                button.ActiveResult = true;
                button.OnSubmit(new BaseEventData(null));
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnDisabledClick }));
                button.ActiveResult = false;
                button.OnSubmit(null);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnDisabledClick }));
                owned.AssertUnchanged();
            }
        }

        // UIButton.OnPointerDown 0x1B51B50 inlines the UISelectable handler: a left press sets IsPointerDown,
        // asks the bridge to select (only when interactable) and transitions; then, for any button,
        // a held pointer plus InteractionOverride() raises OnButtonDown (11).
        [Test]
        public void PointerDownRaisesButtonDownWhileHeldForAnyButton()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                UIClickableInputBridgeProbe bridge = owned.AttachBridge();

                PointerEventData right = Pointer(PointerEventData.InputButton.Right, 0);
                button.OnPointerDown(right);
                Assert.That(owned.IsPointerDown, Is.False);
                Assert.That(owned.TakeEvents(), Is.Empty);

                PointerEventData left = Pointer(PointerEventData.InputButton.Left, 0);
                button.InteractableResult = false;
                button.OnPointerDown(left);
                Assert.That(owned.IsPointerDown, Is.True);
                Assert.That(bridge.Calls, Is.Empty);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonDown }));

                button.OnPointerDown(right);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonDown }), "a second button while held");
                Assert.That(bridge.Calls, Is.Empty);

                button.InteractableResult = true;
                button.OnPointerDown(left);
                Assert.That(bridge.Calls, Is.EqualTo(new[] { "SetSelectedGameObjectWithMemory" }));
                Assert.That(bridge.SelectedObjects.Single(), Is.SameAs(button.gameObject));
                Assert.That(bridge.SelectedEventData.Single(), Is.SameAs(left));
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonDown }));

                button.InteractionOverrideResult = false;
                button.OnPointerDown(left);
                Assert.That(owned.TakeEvents(), Is.Empty);
                Assert.That(owned.IsPointerDown, Is.True);

                Assert.Throws<NullReferenceException>(() => button.OnPointerDown(null));
                owned.AssertUnchanged();
            }
        }

        // UIButton.OnPointerUp 0x1B51CB0: button-up when held and either inside or (exit does not trigger
        // up and the pointer left while held). The flag is cleared before the base, which only acts
        // on the left button; the decision was already taken, so any button reports the release.
        [Test]
        public void PointerUpDecidesButtonUpBeforeReleasingAndClearsTheExitMemory()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                PointerEventData left = Pointer(PointerEventData.InputButton.Left, 0);
                // down, inside, exitTriggersUp, wasDownAndInside -> button up
                var cases = new[]
                {
                    (false, true, false, true, false),
                    (true, true, true, false, true),
                    (true, false, true, true, false),
                    (true, false, false, true, true),
                    (true, false, false, false, false),
                };
                foreach ((bool down, bool inside, bool exitUp, bool wasInside, bool up) in cases)
                {
                    string label = down + "/" + inside + "/" + exitUp + "/" + wasInside;
                    owned.IsPointerDown = down;
                    owned.IsPointerInside = inside;
                    owned.WriteButton("m_pointerExitShouldTriggerButtonUp", exitUp);
                    owned.WriteButton("m_wasPointerDownAndInside", wasInside);
                    button.OnPointerUp(left);
                    Assert.That(owned.TakeEvents(), Is.EqualTo(up ? new[] { UIEventType.OnButtonUp } : new UIEventType[0]), label);
                    Assert.That(owned.IsPointerDown, Is.False, label);
                    Assert.That(owned.ReadButton("m_wasPointerDownAndInside"), Is.EqualTo(false), label);
                }

                owned.IsPointerDown = true;
                owned.IsPointerInside = true;
                button.OnPointerUp(Pointer(PointerEventData.InputButton.Right, 0));
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonUp }));
                Assert.That(owned.IsPointerDown, Is.True, "only the left release clears the hold");

                owned.WriteButton("m_wasPointerDownAndInside", true);
                Assert.Throws<NullReferenceException>(() => button.OnPointerUp(null));
                Assert.That(owned.ReadButton("m_wasPointerDownAndInside"), Is.EqualTo(false), "cleared before the base faults");
                Assert.That(owned.TakeEvents(), Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // UIButton.OnPointerExit 0x1B51F00: the held-inside decision and m_wasPointerDownAndInside
        // (stored at 0x1B51F3A) precede the UISelectable handler; then OnButtonExit (14) and, when exit
        // triggers up, OnButtonUp (12).
        [Test]
        public void PointerExitRemembersAHeldExitOrReleasesItAfterTheBase()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                UIClickableInputBridgeProbe bridge = owned.AttachBridge();
                PointerEventData left = Pointer(PointerEventData.InputButton.Left, 0);

                owned.IsPointerDown = true;
                owned.IsPointerInside = true;
                button.OnPointerExit(left);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonExit, UIEventType.OnButtonUp }));
                Assert.That(owned.ReadButton("m_wasPointerDownAndInside"), Is.EqualTo(false));
                Assert.That(owned.IsPointerInside, Is.True, "a hidden cursor leaves the inside flag alone");
                Assert.That(owned.IsPointerDown, Is.True, "exit never releases the hold");

                owned.WriteButton("m_pointerExitShouldTriggerButtonUp", false);
                button.OnPointerExit(left);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonExit }));
                Assert.That(owned.ReadButton("m_wasPointerDownAndInside"), Is.EqualTo(true));

                // A visible cursor lets the base clear the inside flag; a later exit is not held inside
                // and so forgets the remembered exit.
                bridge.CursorVisible = true;
                button.OnPointerExit(left);
                Assert.That(owned.IsPointerInside, Is.False);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonExit }));
                button.OnPointerExit(left);
                Assert.That(owned.TakeEvents(), Is.Empty);
                Assert.That(owned.ReadButton("m_wasPointerDownAndInside"), Is.EqualTo(false));

                owned.IsPointerInside = true;
                Write(typeof(UISelectable), button, "m_inputBridge", null);
                Assert.Throws<NullReferenceException>(() => button.OnPointerExit(left));
                Assert.That(owned.ReadButton("m_wasPointerDownAndInside"), Is.EqualTo(true), "stored before the base faults");
                Assert.That(owned.TakeEvents(), Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // UIButton.OnPointerEnter 0x1B51D70 reads EventSystem.current's selection (+0x40) and the
        // held-outside state before the inlined UISelectable handler (cursor-gated inside flag and the
        // navigation-toward event, 2), then raises OnButtonDown (11) for a selected re-entry.
        [Test]
        public void PointerEnterReportsButtonDownForASelectedHeldReentryWhateverTheCursor()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                UIClickableInputBridgeProbe bridge = owned.AttachBridge();
                EventSystem system = owned.InstallEventSystem();
                PointerEventData left = Pointer(PointerEventData.InputButton.Left, 0);

                owned.Select(system, button.gameObject);
                owned.IsPointerDown = true;
                button.OnPointerEnter(left);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnButtonDown }));
                Assert.That(owned.IsPointerInside, Is.False, "a hidden cursor does not mark the pointer inside");

                bridge.CursorVisible = true;
                owned.SetPointerEnterNavigation(true);
                button.OnPointerEnter(left);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnNavigateToward, UIEventType.OnButtonDown }));
                Assert.That(owned.IsPointerInside, Is.True);

                button.OnPointerEnter(left);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnNavigateToward }), "already inside");

                owned.IsPointerInside = false;
                owned.Select(system, owned.Target.gameObject);
                button.OnPointerEnter(left);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnNavigateToward }), "not the selected object");

                owned.IsPointerInside = false;
                owned.IsPointerDown = false;
                owned.Select(system, button.gameObject);
                button.OnPointerEnter(left);
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnNavigateToward }), "not held");
                owned.AssertUnchanged();
            }
        }

        // PublishUIMessage 0x1B52030 publishes eventId under UIMessage(type) on the auto-created
        // MessageExchangeBoundCallbackArg<UIMessage> (broadcast 3); the wrappers pass 8, 9, 10, 11, 12, 14.
        [Test]
        public void PublishedMessagesCarryTheEventUnderTheirUIMessageType()
        {
            using (var owned = new Owned<UIButtonProbe>())
            {
                UIButtonProbe button = owned.Component;
                UIEvent eventId = owned.Own(ScriptableObject.CreateInstance<UIEvent>());
                var received = new List<(UIEventType, UIEvent)>();
                MessageExchangeBoundCallbackArg<UIMessage> exchange = owned.UseExchange(received);

                button.PublishUIMessageOnClick(eventId);
                button.PublishUIMessageOnDisabledClick(eventId);
                button.PublishUIMessageOnOfflineClick(eventId);
                button.PublishUIMessageOnButtonDown(eventId);
                button.PublishUIMessageOnButtonUp(eventId);
                button.PublishUIMessageOnButtonExit(eventId);
                button.PublishUIMessage(UIEventType.OnRelease, eventId);
                Assert.That(received.Select(r => r.Item1), Is.EqualTo(new[] { UIEventType.OnClick, UIEventType.OnDisabledClick,
                    UIEventType.OnOfflineClick, UIEventType.OnButtonDown, UIEventType.OnButtonUp, UIEventType.OnButtonExit, UIEventType.OnRelease }));
                Assert.That(received.Select(r => r.Item2), Is.All.SameAs(eventId));
                Assert.That(ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIMessage>>(), Is.SameAs(exchange));
                Assert.That(owned.Events, Is.Empty, "publishing raises no selectable event");
                owned.AssertUnchanged();
            }
        }

        // DoStateTransition 0x1B52D60 (jump table 0x1B52F64), on an owner that is not active in the
        // hierarchy so no animation can start: Normal stops after a highlight unless releasing;
        // Highlighted stops nothing while inactive; Pressed stops unless shaking or cooling down;
        // Disabled stops only an accepted click, and never twice in a row.
        [Test]
        public void StateTransitionsStopAnimationsOnlyOnTheOriginalEdges()
        {
            using (var owned = new Owned<UIClickableProbe>())
            {
                // state, previous, animating, clickAccepted, onCooldown -> animating afterwards
                var cases = new[]
                {
                    (0, 1, 1, false, false, 0),
                    (0, 1, 2, false, false, 2),
                    (0, 2, 1, false, false, 1),
                    (1, 2, 1, false, false, 1),
                    (2, 0, 2, true, false, 0),
                    (2, 0, 3, true, false, 3),
                    (2, 0, 2, true, true, 2),
                    (3, 0, 2, true, false, 0),
                    (3, 0, 2, false, false, 2),
                    (3, 3, 2, true, false, 2),
                };
                foreach ((int state, int previous, int animating, bool accepted, bool cooldown, int after) in cases)
                {
                    string label = state + "<-" + previous + " anim " + animating + " accepted " + accepted + " cooldown " + cooldown;
                    Write(typeof(UISelectable), owned.Component, "m_previousSelectionState", Enum.ToObject(SelectionState, previous));
                    owned.WriteClickable("m_currentAnimatingState", Animation(animating));
                    owned.WriteClickable("m_clickAccepted", accepted);
                    owned.WriteClickable("m_onCooldown", cooldown);
                    Invoke(typeof(UIClickable), owned.Component, "DoStateTransition", Enum.ToObject(SelectionState, state), true);
                    Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(after)), label);
                    Assert.That(owned.ReadClickable("m_clickAccepted"), Is.EqualTo(accepted), label + ": cleared only when a press animation starts");
                    Assert.That(owned.ReadClickable("m_animatingCoroutine"), Is.Null, label);
                }
                owned.AssertUnchanged();
            }
        }

        // CacheLocalTransform 0x1B529E0 copies position, scale and Euler angles and marks the cache;
        // ResetTransformToCache 0x1B52900 and OnRectTransformDimensionsChange 0x1B531C0 are gated on
        // Application.isPlaying, which is false in edit mode.
        [Test]
        public void TransformCacheIsTakenDirectlyButRestoredAndRefreshedOnlyInPlayMode()
        {
            using (var owned = new Owned<UIClickableProbe>())
            {
                Transform target = owned.Target;
                Invoke(typeof(UIClickable), owned.Component, "OnRectTransformDimensionsChange");
                Assert.That(owned.ReadClickable("m_hasCached"), Is.EqualTo(false));

                target.localPosition = new Vector3(1f, 2f, 3f);
                target.localScale = new Vector3(4f, 5f, 6f);
                target.localEulerAngles = new Vector3(10f, 20f, 30f);
                Invoke(typeof(UIClickable), owned.Component, "CacheLocalTransform");
                Assert.That(owned.ReadClickable("m_hasCached"), Is.EqualTo(true));
                Assert.That(owned.ReadClickable("m_cachedLocalPosition"), Is.EqualTo(target.localPosition));
                Assert.That(owned.ReadClickable("m_cachedLocalScale"), Is.EqualTo(target.localScale));
                Assert.That(owned.ReadClickable("m_cachedLocalRotation"), Is.EqualTo(target.localEulerAngles));

                target.localPosition = new Vector3(-7f, 8f, 9f);
                target.localScale = Vector3.one;
                Invoke(typeof(UIClickable), owned.Component, "ResetTransformToCache");
                Assert.That(target.localPosition, Is.EqualTo(new Vector3(-7f, 8f, 9f)));
                Assert.That(target.localScale, Is.EqualTo(Vector3.one));
                owned.AssertUnchanged();
            }
        }

        // AnimateOnPress/AnimateOnRelease d__34/d__35 MoveNext (0x1B535B0, 0x1B53C90): state 1/2 and
        // event OnPress (5)/OnRelease (6); endTime is the larger curve end time (maxss), the timer starts
        // there when instant and at 0 otherwise, and one unscaled-delta step sets the scale to
        // (x, y, 1f) from that animation's own curves before the first check (a do-while).
        // The longer curve is X for the release and Y for the press, so both maxss operands matter.
        [Test]
        public void PressAndReleaseAnimationsStepOnceBeforeTheirFirstCheck(
            [Values(true, false)] bool release, [Values(true, false)] bool instant)
        {
            using (var owned = new Owned<UIClickableProbe>())
            {
                AnimationCurve x = new AnimationCurve(new Keyframe(0f, 2f), new Keyframe(release ? 600f : 0.25f, 2.5f));
                AnimationCurve y = new AnimationCurve(new Keyframe(0f, 3f), new Keyframe(release ? 0.25f : 600f, 3.5f));
                AnimationCurve other = new AnimationCurve(new Keyframe(0f, 9f), new Keyframe(900f, 9f));
                owned.UseCurves(release ? other : x, release ? other : y, release ? x : other, release ? y : other, other, other, Vector2.zero);

                var animation = (IEnumerator)Invoke(typeof(UIClickable), owned.Component, release ? "AnimateOnRelease" : "AnimateOnPress", instant);
                Assert.That(owned.Events, Is.Empty, "nothing runs before the first MoveNext");
                Assert.That(animation.MoveNext(), Is.True, "an instant animation still yields once");
                float delta = Time.unscaledDeltaTime;
                Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(release ? 2 : 1)));
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { release ? UIEventType.OnRelease : UIEventType.OnPress }));
                Assert.That(Read(animation.GetType(), animation, "<endTime>5__2"), Is.EqualTo(600f));
                float timer = (float)Read(animation.GetType(), animation, "<timer>5__3");
                Assert.That(timer, Is.EqualTo((instant ? 600f : 0f) + delta));
                Assert.That(owned.Target.localScale, Is.EqualTo(new Vector3(x.Evaluate(timer), y.Evaluate(timer), 1f)));
                Assert.That(animation.Current, Is.Null);

                // The check uses the timer of the previous step: an instant animation ends here.
                Assert.That(animation.MoveNext(), Is.EqualTo(timer < 600f));
                if (instant)
                {
                    Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(0)));
                    Assert.That(owned.ReadClickable("m_animatingCoroutine"), Is.Null);
                }
                else
                {
                    float next = (float)Read(animation.GetType(), animation, "<timer>5__3");
                    Assert.That(next, Is.EqualTo(timer + Time.unscaledDeltaTime));
                    Assert.That(owned.Target.localScale, Is.EqualTo(new Vector3(x.Evaluate(next), y.Evaluate(next), 1f)));
                    Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(release ? 2 : 1)));
                }
                Assert.That(owned.Events, Is.Empty, "the event is raised once per animation");
                owned.AssertUnchanged();
            }
        }

        // AnimateOnPressDisabled d__36 MoveNext 0x1B53900: state 3, OnDisabled (7), caches the target's
        // position, offsets it by (x * scaler.x, y * scaler.y, 0); after a yield a pending restart sets
        // the timer to 0 (even for an instant shake) before the comparison; the end restores the cache.
        [Test]
        public void DisabledShakeOffsetsTheCachedPositionAndRestartsAfterAYield()
        {
            using (var owned = new Owned<UIClickableProbe>())
            {
                AnimationCurve x = new AnimationCurve(new Keyframe(0f, 2f), new Keyframe(0.5f, 2f));
                AnimationCurve y = new AnimationCurve(new Keyframe(0f, -1f), new Keyframe(0.75f, -1f));
                AnimationCurve other = new AnimationCurve(new Keyframe(0f, 9f), new Keyframe(900f, 9f));
                owned.UseCurves(other, other, other, other, x, y, new Vector2(3f, 5f));
                owned.Target.localPosition = new Vector3(10f, 20f, 30f);

                var shake = (IEnumerator)Invoke(typeof(UIClickable), owned.Component, "AnimateOnPressDisabled", true);
                Assert.That(shake.MoveNext(), Is.True);
                Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(3)));
                Assert.That(owned.TakeEvents(), Is.EqualTo(new[] { UIEventType.OnDisabled }));
                Assert.That(owned.ReadClickable("m_cachedLocalPosition"), Is.EqualTo(new Vector3(10f, 20f, 30f)));
                Assert.That(owned.ReadClickable("m_hasCached"), Is.EqualTo(false), "only the position is cached");
                Assert.That(Read(shake.GetType(), shake, "<endTime>5__2"), Is.EqualTo(0.75f));
                Assert.That(Read(shake.GetType(), shake, "<scaler>5__4"), Is.EqualTo(new Vector2(3f, 5f)));
                float timer = (float)Read(shake.GetType(), shake, "<timer>5__3");
                Assert.That(owned.Target.localPosition,
                    Is.EqualTo(new Vector3(10f + x.Evaluate(timer) * 3f, 20f + y.Evaluate(timer) * 5f, 30f)));

                // The press that restarts the shake: the timer goes back to 0 before the end check, so
                // even this instant shake runs another step.
                owned.WriteClickable("m_shouldResetInactiveAnim", true);
                Assert.That(shake.MoveNext(), Is.True);
                Assert.That(owned.ReadClickable("m_shouldResetInactiveAnim"), Is.EqualTo(false));
                float restarted = (float)Read(shake.GetType(), shake, "<timer>5__3");
                Assert.That(restarted, Is.EqualTo(0f + Time.unscaledDeltaTime));

                // Without a restart the instant timer (>= end) ends it and restores the cached position.
                Write(shake.GetType(), shake, "<timer>5__3", 0.75f);
                owned.Target.localPosition = Vector3.zero;
                Assert.That(shake.MoveNext(), Is.False);
                Assert.That(owned.Target.localPosition, Is.EqualTo(new Vector3(10f, 20f, 30f)));
                Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(0)));
                Assert.That(owned.Events, Is.Empty);
                owned.AssertUnchanged();
            }
        }

        // GlobalCooldownClicks d__37 MoveNext 0x1B53F70 sets the static flag, yields
        // CoroutineUtils.WaitForRealSeconds(m_cooldownTimeInSeconds) and clears it on resumption;
        // CancelGlobalInputCooldown 0x1B53340 only clears the flag.
        [Test]
        public void GlobalCooldownSetsTheFlagAcrossOneRealTimeWait()
        {
            using (var owned = new Owned<UIClickableProbe>())
            {
                owned.WriteClickable("m_cooldownTimeInSeconds", 4.5f);
                var cooldown = (IEnumerator)Invoke(typeof(UIClickable), owned.Component, "GlobalCooldownClicks");
                Assert.That(owned.ReadClickable("s_globalInputOnCooldown"), Is.EqualTo(false));
                Assert.That(cooldown.MoveNext(), Is.True);
                Assert.That(owned.ReadClickable("s_globalInputOnCooldown"), Is.EqualTo(true));
                Assert.That(cooldown.Current, Is.InstanceOf<IEnumerator>());
                Assert.That(cooldown.Current.GetType().DeclaringType, Is.EqualTo(typeof(Hardlight.Utils.CoroutineUtils)));
                Assert.That(cooldown.Current.GetType().Name, Does.StartWith("<WaitForRealSeconds>d__"));
                Assert.That(Read(cooldown.Current.GetType(), cooldown.Current, "time"), Is.EqualTo(4.5f));

                UIClickable.CancelGlobalInputCooldown();
                Assert.That(owned.ReadClickable("s_globalInputOnCooldown"), Is.EqualTo(false));
                owned.WriteClickable("s_globalInputOnCooldown", true);
                Assert.That(cooldown.MoveNext(), Is.False);
                Assert.That(owned.ReadClickable("s_globalInputOnCooldown"), Is.EqualTo(false));
                Assert.That(cooldown.MoveNext(), Is.False);
                owned.AssertUnchanged();
            }
        }

        // StopAnimationCoroutine 0x1B52C40 resets the state even without a coroutine.
        // RegisterButtonStatusEffector 0x1B53380 adds once, sets m_hasStatusEffects and applies the
        // current interactability through TriggerButtonStatusAffectors 0x1B53460.
        [Test]
        public void StatusEffectorsRegisterOnceAndAreAppliedOnRegistration()
        {
            using (var owned = new Owned<UIClickableProbe>())
            {
                owned.WriteClickable("m_currentAnimatingState", Animation(2));
                Invoke(typeof(UIClickable), owned.Component, "StopAnimationCoroutine");
                Assert.That(owned.ReadClickable("m_currentAnimatingState"), Is.EqualTo(Animation(0)));

                ButtonStatusEffects effector = owned.AddStatusEffector();
                Invoke(typeof(UIClickable), owned.Component, "TriggerButtonStatusAffectors");
                Assert.That(Read(typeof(ButtonStatusEffects), effector, "m_isInitialised"), Is.EqualTo(false), "nothing registered yet");

                owned.Component.RegisterButtonStatusEffector(effector);
                owned.Component.RegisterButtonStatusEffector(effector);
                var effects = (List<ButtonStatusEffects>)owned.ReadClickable("m_buttonStatusEffects");
                Assert.That(effects, Is.EqualTo(new[] { effector }));
                Assert.That(owned.ReadClickable("m_hasStatusEffects"), Is.EqualTo(true));
                Assert.That(Read(typeof(ButtonStatusEffects), effector, "m_isInitialised"), Is.EqualTo(true));
                owned.AssertUnchanged();
            }
        }

        private static PointerEventData Pointer(PointerEventData.InputButton button, int pointerId) =>
            new PointerEventData(null) { button = button, pointerId = pointerId };

        private static object Animation(int value) => Enum.ToObject(AnimationState, value);

        // Owns one inactive clickable, its target child, the authored event table and every shared
        // static the cases touch. Disposal restores them even when a case fails.
        private sealed class Owned<T> : IDisposable where T : UIClickable
        {
            private static readonly FieldInfo EventSystems = typeof(EventSystem).GetField("m_EventSystems", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly FieldInfo Systems = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static);

            private readonly object priorCooldown;
            private readonly object priorCooldownCoroutine;
            private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
            private readonly List<string> logs = new List<string>();
            private readonly List<Action> restores = new List<Action>();
            private GameObject root;

            public T Component { get; }
            public Transform Target { get; }
            public List<UIEventType> Events { get; } = new List<UIEventType>();
            public List<UISelectable.UIEventInfo> EventTable { get; } = new List<UISelectable.UIEventInfo>();

            public Owned()
            {
                priorCooldown = Read(typeof(UIClickable), null, "s_globalInputOnCooldown");
                priorCooldownCoroutine = Read(typeof(UIClickable), null, "m_cooldownCoroutine");
                Application.logMessageReceived += Log;
                try
                {
                    Write(typeof(UIClickable), null, "s_globalInputOnCooldown", false);
                    root = new GameObject("owned-original-ui-clickable", typeof(RectTransform));
                    root.SetActive(false);
                    Component = root.AddComponent<T>();
                    var target = new GameObject("owned-original-ui-clickable-target");
                    target.transform.SetParent(root.transform, false);
                    Target = target.transform;
                    Write(typeof(UISelectable), Component, "m_targetTransform", Target);
                    foreach (UIEventType type in Enum.GetValues(typeof(UIEventType)))
                    {
                        var info = new UISelectable.UIEventInfo { m_type = type, m_onEvent = new UnityEvent() };
                        UIEventType raised = type;
                        info.m_onEvent.AddListener(() => Events.Add(raised));
                        EventTable.Add(info);
                    }
                    Write(typeof(UISelectable), Component, "m_events", EventTable);
                    FieldInfo vibration = typeof(UISelectable).GetField("m_vibrationManager", Declared);
                    vibration.SetValue(Component, Activator.CreateInstance(vibration.FieldType, "owned-original-ui-clickable-vibration", null));
                    Assert.That(root.activeInHierarchy, Is.False);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public bool IsPointerDown
            {
                get => (bool)typeof(UISelectable).GetProperty("IsPointerDown", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Component);
                set => typeof(UISelectable).GetProperty("IsPointerDown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Component, value);
            }

            public bool IsPointerInside
            {
                get => (bool)typeof(UISelectable).GetProperty("IsPointerInside", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Component);
                set => typeof(UISelectable).GetProperty("IsPointerInside", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Component, value);
            }

            public UISelectable.UIEventInfo Entry(UIEventType type) => EventTable.Single(e => e.m_type == type);

            public UIEventType[] TakeEvents()
            {
                UIEventType[] taken = Events.ToArray();
                Events.Clear();
                return taken;
            }

            public object ReadClickable(string name) => Read(typeof(UIClickable), Component, name);
            public void WriteClickable(string name, object value) => Write(typeof(UIClickable), Component, name, value);
            public object ReadButton(string name) => Read(typeof(UIButton), Component, name);
            public void WriteButton(string name, object value) => Write(typeof(UIButton), Component, name, value);

            public TObject Own<TObject>(TObject value) where TObject : UnityEngine.Object
            {
                objects.Add(value);
                return value;
            }

            public UIClickableInputBridgeProbe AttachBridge()
            {
                UIClickableInputBridgeProbe bridge = Own(ScriptableObject.CreateInstance<UIClickableInputBridgeProbe>());
                Write(typeof(UISelectable), Component, "m_inputBridge", bridge);
                return bridge;
            }

            public ButtonStatusEffects AddStatusEffector()
            {
                var holder = Own(new GameObject("owned-original-ui-clickable-status"));
                holder.SetActive(false);
                return holder.AddComponent<ButtonStatusEffects>();
            }

            public void UseCurves(AnimationCurve pressedX, AnimationCurve pressedY, AnimationCurve releasedX, AnimationCurve releasedY,
                AnimationCurve disabledX, AnimationCurve disabledY, Vector2 direction)
            {
                var collection = Own(ScriptableObject.CreateInstance<HLCurveAnimatedGraphicCurveCollection>());
                Write(typeof(HLCurveAnimatedGraphicCurveCollection), collection, "m_onPressedXCurve", Curve(pressedX));
                Write(typeof(HLCurveAnimatedGraphicCurveCollection), collection, "m_onPressedYCurve", Curve(pressedY));
                Write(typeof(HLCurveAnimatedGraphicCurveCollection), collection, "m_onReleasedXCurve", Curve(releasedX));
                Write(typeof(HLCurveAnimatedGraphicCurveCollection), collection, "m_onReleasedYCurve", Curve(releasedY));
                Write(typeof(HLCurveAnimatedGraphicCurveCollection), collection, "m_onDisabledXCurve", Curve(disabledX));
                Write(typeof(HLCurveAnimatedGraphicCurveCollection), collection, "m_onDisabledYCurve", Curve(disabledY));
                Write(typeof(HLCurveAnimatedGraphicCurveCollection), collection, "m_onDisabledAnimDirection", direction);
                WriteClickable("m_curveCollection", collection);
            }

            private HLCurve Curve(AnimationCurve curve)
            {
                HLCurve asset = Own(ScriptableObject.CreateInstance<HLCurve>());
                Write(typeof(HLCurve), asset, "m_curve", curve);
                return asset;
            }

            // Puts an owned EventSystem first in the static list behind EventSystem.current.
            public EventSystem InstallEventSystem()
            {
                var holder = Own(new GameObject("owned-original-ui-clickable-event-system"));
                holder.SetActive(false);
                EventSystem system = holder.AddComponent<EventSystem>();
                var list = (List<EventSystem>)EventSystems.GetValue(null);
                list.Insert(0, system);
                restores.Add(() => list.Remove(system));
                Assert.That(EventSystem.current, Is.SameAs(system));
                return system;
            }

            public void Select(EventSystem system, GameObject selected) => Write(typeof(EventSystem), system, "m_CurrentSelected", selected);

            public void SetPointerEnterNavigation(bool value)
            {
                FieldInfo navigation = typeof(UISelectable).GetField("m_navigation", Declared);
                object boxed = navigation.GetValue(Component);
                Write(typeof(UINavigation), boxed, "m_pointerEnterShouldTriggerNavigationEvent", value);
                navigation.SetValue(Component, boxed);
            }

            // Subscribes to every UIMessage type on the shared exchange, creating it only if absent;
            // the subscriptions, and an exchange row this fixture created, are removed on disposal.
            // A pre-existing exchange keeps the empty bound-subscriber list that each Both publish
            // creates for the owned UIEvent key (GetSubscriberList's TryGetOrNew).
            public MessageExchangeBoundCallbackArg<UIMessage> UseExchange(List<(UIEventType, UIEvent)> received)
            {
                string name = ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIMessage>>();
                var rows = (System.Collections.IDictionary)Systems.GetValue(null);
                bool hadRow = rows.Contains(name);
                bool hadSystem = ProcessManager.IsSystemValid(name);
                MessageExchangeBoundCallbackArg<UIMessage> exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIMessage>>();
                restores.Add(() =>
                {
                    if (!hadSystem) ProcessManager.UnregisterSystem(name);
                    if (!hadRow) rows.Remove(name);
                });
                foreach (UIEventType type in Enum.GetValues(typeof(UIEventType)))
                {
                    UIEventType subscribed = type;
                    var message = new UIMessage(type);
                    MessageCallback<UIEvent> callback = (in UIEvent value) => received.Add((subscribed, value));
                    exchange.SubscribeToMessage(in message, callback);
                    restores.Add(() => exchange.UnsubscribeFromMessage(in message, callback));
                }
                return exchange;
            }

            public void AssertUnchanged()
            {
                Assert.That(logs, Is.Empty);
            }

            public void Dispose()
            {
                try
                {
                    for (int i = restores.Count - 1; i >= 0; i--) restores[i]();
                    for (int i = objects.Count - 1; i >= 0; i--)
                        if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                    if (!ReferenceEquals(root, null)) UnityEngine.Object.DestroyImmediate(root);
                }
                finally
                {
                    restores.Clear();
                    objects.Clear();
                    root = null;
                    Write(typeof(UIClickable), null, "s_globalInputOnCooldown", priorCooldown);
                    Write(typeof(UIClickable), null, "m_cooldownCoroutine", priorCooldownCoroutine);
                    Application.logMessageReceived -= Log;
                }
            }

            private void Log(string message, string stackTrace, LogType type)
            {
                logs.Add(type + ": " + message);
            }
        }

        private static object Invoke(Type owner, object target, string name, params object[] arguments)
        {
            MethodInfo method = owner.GetMethods(Declared).Single(m => m.Name == name && m.GetParameters().Length == arguments.Length);
            try { return method.Invoke(target, arguments); }
            catch (TargetInvocationException failure) { throw failure.InnerException; }
        }

        private static object Read(Type owner, object target, string name)
        {
            FieldInfo field = owner.GetField(name, Declared);
            Assert.That(field, Is.Not.Null, owner.Name + "." + name);
            return field.GetValue(target);
        }

        private static void Write(Type owner, object target, string name, object value)
        {
            FieldInfo field = owner.GetField(name, Declared);
            Assert.That(field, Is.Not.Null, owner.Name + "." + name);
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

        private static void IteratorFields(Type owner, string name, string[] hoisted, Type[] types)
        {
            Type machine = owner.GetNestedType(name, BindingFlags.NonPublic);
            FieldInfo[] actual = machine.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            string[] expected = new[] { "<>1__state", "<>2__current", "<>4__this" }.Concat(hoisted).ToArray();
            Assert.That(actual.Select(f => f.Name), Is.EqualTo(expected), name);
            Assert.That(actual.Select(f => f.FieldType), Is.EqualTo(new[] { typeof(int), typeof(object), owner }.Concat(types)), name);
            Assert.That(actual.Select(f => (int)f.Attributes), Is.EqualTo(new[] { 1, 1, 6 }.Concat(hoisted.Select(h => h == "instant" ? 6 : 1))), name);
        }

        private static MethodBase Method(Type type, string name, int flags, Type result, Type[] arguments, string[] parameterNames)
        {
            MethodBase method = name == ".ctor" ? (MethodBase)type.GetConstructor(Declared, null, arguments, null) :
                type.GetMethod(name, Declared, null, arguments, null);
            Assert.That(method, Is.Not.Null, type.Name + "." + name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), name);
            if (method is MethodInfo info) Assert.That(info.ReturnType, Is.EqualTo(result), name);
            Assert.That(method.GetParameters().Select(p => p.Name), Is.EqualTo(parameterNames), name);
            return method;
        }
    }
}
