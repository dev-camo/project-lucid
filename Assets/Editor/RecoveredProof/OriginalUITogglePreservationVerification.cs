using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace ProjectLucid.Verification
{
    // Real recovered components and owned Unity objects. Reflection configures
    // original serialized fields or invokes an original unexposed callback;
    // it does not provide a replacement runtime implementation.
    public static class OriginalUITogglePreservationVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private sealed class CallbackFault : Exception { }

        private sealed class OwnedObjects : IDisposable
        {
            private readonly List<GameObject> objects = new List<GameObject>();
            private readonly List<Object> assets = new List<Object>();
            public readonly List<UIToggle> Toggles = new List<UIToggle>();
            public readonly List<UIToggleGroupElement> Elements = new List<UIToggleGroupElement>();
            public readonly List<UIToggleGroup> Groups = new List<UIToggleGroup>();
            private readonly List<AnimationObjects> animations = new List<AnimationObjects>();

            public GameObject New(string name, Transform parent = null)
            {
                var value = new GameObject(name, typeof(RectTransform));
                value.SetActive(false);
                objects.Add(value);
                if (parent != null) value.transform.SetParent(parent, false);
                return value;
            }

            public UIToggleGroup Group(string name)
            {
                var value = New(name).AddComponent<UIToggleGroup>();
                Groups.Add(value);
                value.AllowSwitchOff = true;
                value.gameObject.SetActive(true);
                return value;
            }

            public ToggleObjects Toggle(string name, UIToggleGroup group = null, Transform parent = null)
            {
                GameObject go = New(name, parent);
                UIToggle toggle = go.AddComponent<UIToggle>();
                UIToggleGroupElement element = go.AddComponent<UIToggleGroupElement>();
                Toggles.Add(toggle); Elements.Add(element);
                Set(toggle, "m_groupElement", element);
                Set(element, "m_toggle", toggle);
                Set(element, "m_group", group);
                go.SetActive(true);
                if (group != null) group.RegisterToggle(element); // Safe if automatic OnEnable already registered it.
                return new ToggleObjects { Toggle = toggle, Element = element };
            }

            public AnimationObjects Animation(string name)
            {
                var toggle = Toggle(name);
                toggle.Toggle.gameObject.SetActive(false);
                Animation animation = toggle.Toggle.gameObject.AddComponent<Animation>();
                animation.playAutomatically = false;
                UITransitionAnimation transition = toggle.Toggle.gameObject.AddComponent<UITransitionAnimation>();
                var result = new AnimationObjects { Toggle = toggle.Toggle, Animation = animation, Transition = transition, Settings = new List<UITransitionAnimationSetting>() };
                foreach (UITransitionState state in new[] { UITransitionState.Normal, UITransitionState.Highlighted, UITransitionState.Pressed, UITransitionState.Selected, UITransitionState.Disabled })
                {
                    var clip = new AnimationClip { name = name + " " + state, legacy = true };
                    clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, 1f, 0f));
                    assets.Add(clip);
                    animation.AddClip(clip, clip.name);
                    var setting = new UITransitionAnimationSetting();
                    Set(setting, "m_state", state); Set(setting, "m_animationClip", clip);
                    result.Settings.Add(setting);
                }
                Set(transition, "m_animation", animation);
                Set(transition, "m_uiInteractable", result.Toggle);
                Set(transition, "m_settings", result.Settings);
                animations.Add(result);
                result.Toggle.gameObject.SetActive(true);
                Call(transition, "Awake"); // Explicit original callback; automatic scene scheduling is outside this fixture.
                return result;
            }

            public void Dispose()
            {
                // Fault cases intentionally retain broken owned state. Remove only
                // owned callbacks/memberships before original teardown can observe it.
                Exception cleanupError = null;
                Action<Action> cleanup = action => { try { action(); } catch (Exception error) { if (cleanupError == null) cleanupError = error; } };
                foreach (UIToggle value in Toggles)
                    cleanup(() => { if (value != null) { value.OnValueChanged?.RemoveAllListeners(); Set(value, "m_groupElement", null); Set(value, "m_transition", null); } });
                foreach (UIToggleGroupElement value in Elements)
                    cleanup(() => { if (value != null) Set(value, "m_group", null); });
                foreach (UIToggleGroup value in Groups)
                    cleanup(() => { if (value != null) { Members(value).Clear(); Set(value, "OnGroupChanged", (Action)(() => { })); } });
                foreach (AnimationObjects value in animations)
                    cleanup(() => { if (value.Transition != null && value.Animation != null) { Set(value.Transition, "m_animation", value.Animation); Set(value.Transition, "m_settings", value.Settings); } });
                for (int i = objects.Count - 1; i >= 0; --i)
                { GameObject value = objects[i]; cleanup(() => { if (value != null) Object.DestroyImmediate(value); }); }
                for (int i = assets.Count - 1; i >= 0; --i)
                { Object value = assets[i]; cleanup(() => { if (value != null) Object.DestroyImmediate(value); }); }
                if (cleanupError != null) throw new InvalidOperationException("Owned UI teardown failed after every cleanup was attempted.", cleanupError);
            }
        }

        private sealed class ToggleObjects { public UIToggle Toggle; public UIToggleGroupElement Element; }
        private sealed class AnimationObjects
        {
            public UIToggle Toggle; public Animation Animation; public UITransitionAnimation Transition;
            public List<UITransitionAnimationSetting> Settings;
        }

        private sealed class EventSystemRows : IDisposable
        {
            private readonly FieldInfo field = typeof(EventSystem).GetField("m_EventSystems", Own);
            private readonly IList rows;
            private readonly object[] previous;
            public readonly EventSystem System;

            public EventSystemRows(OwnedObjects owned)
            {
                if (field == null) throw new InvalidOperationException("Installed UGUI EventSystem registry is unavailable.");
                rows = (IList)field.GetValue(null);
                previous = new object[rows.Count]; rows.CopyTo(previous, 0);
                System = owned.New("Owned inactive EventSystem").AddComponent<EventSystem>();
                // This is explicit fixture registration of a real inactive system,
                // avoiding automatic UI Toolkit/Start side effects. No external
                // selection, registry object or row is replaced.
                try { rows.Add(System); EventSystem.current = System; }
                catch { Restore(); throw; }
            }

            public void Dispose()
            {
                bool identityChanged = !ReferenceEquals(rows, field.GetValue(null));
                Restore();
                if (identityChanged) throw new InvalidOperationException("EventSystem registry identity changed; original reference restored.");
                if (rows.Count != previous.Length) throw new InvalidOperationException("EventSystem registry count not restored.");
                for (int i = 0; i < previous.Length; ++i)
                    if (!ReferenceEquals(rows[i], previous[i])) throw new InvalidOperationException("EventSystem registry order not restored.");
            }

            private void Restore()
            {
                if (!ReferenceEquals(rows, field.GetValue(null))) field.SetValue(null, rows);
                rows.Clear(); foreach (object row in previous) rows.Add(row);
            }
        }

        public static int CanvasAndUnconditionalSetter()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            using (var events = new EventSystemRows(owned))
            {
                GameObject outer = owned.New("Outer canvas");
                GameObject inner = owned.New("Inner canvas", outer.transform);
                CanvasGroup outerGroup = outer.AddComponent<CanvasGroup>();
                CanvasGroup innerGroup = inner.AddComponent<CanvasGroup>();
                var child = owned.Toggle("Canvas child", null, inner.transform);
                outer.SetActive(true); inner.SetActive(true);
                var values = new List<bool>();
                var changed = new UnityEngine.Events.UnityEvent<bool>(); changed.AddListener(values.Add);
                Set(child.Toggle, "m_onInteractableValueChanged", changed);
                object cache = Get(child.Toggle, "m_canvasGroupCache");
                Require(child.Toggle.IsInteractable(), "constructor flags allow interaction", ref checks);
                outerGroup.interactable = false;
                Call(child.Toggle, "OnCanvasGroupChanged");
                Require(!child.Toggle.IsInteractable(), "enabled parent blocks", ref checks);
                innerGroup.ignoreParentGroups = true;
                Call(child.Toggle, "OnCanvasGroupChanged");
                Require(child.Toggle.IsInteractable(), "ignore parent stops traversal", ref checks);
                innerGroup.enabled = false;
                Call(child.Toggle, "OnCanvasGroupChanged");
                Require(child.Toggle.IsInteractable(), "disabled ignore flag still stops traversal", ref checks);
                innerGroup.ignoreParentGroups = false;
                Call(child.Toggle, "OnCanvasGroupChanged");
                Require(!child.Toggle.IsInteractable(), "disabled non-ignore group still visits parent", ref checks);
                outerGroup.enabled = false;
                Call(child.Toggle, "OnCanvasGroupChanged");
                Require(child.Toggle.IsInteractable(), "disabled blocking group is ignored", ref checks);
                Require(ReferenceEquals(cache, Get(child.Toggle, "m_canvasGroupCache")), "readonly component cache identity", ref checks);
                child.Toggle.Interactable = true; child.Toggle.Interactable = true;
                Require(values.Count == 2 && values[0] && values[1], "equal setter values still notify", ref checks);
                events.System.SetSelectedGameObject(child.Toggle.gameObject);
                Require(ReferenceEquals(events.System.currentSelectedGameObject, child.Toggle.gameObject), "owned selection established", ref checks);
                bool selectionWasCleared = false;
                changed.AddListener(value => { if (!value) selectionWasCleared = events.System.currentSelectedGameObject == null; });
                child.Toggle.Interactable = false;
                Require(selectionWasCleared, "selection cleared before bool callback", ref checks);
                Require(values.Count == 3 && !values[2], "disabled notification uses actual flags", ref checks);
                child.Toggle.Interactable = false;
                Require(values.Count == 4 && !values[3], "repeated disable still notifies", ref checks);
                Require(!child.Toggle.IsInteractable(), "stored false flag", ref checks);
                Set(child.Toggle, "m_onInteractableValueChanged", null);
                child.Toggle.Interactable = true;
                Require(child.Toggle.IsInteractable(), "null event permits mutation", ref checks);
                child.Toggle.transform.SetParent(outer.transform, false);
                outerGroup.enabled = true;
                Call(child.Toggle, "OnTransformParentChanged");
                Require(!child.Toggle.IsInteractable(), "original parent callback refreshes canvas flag", ref checks);
                Require(values.Count == 4, "canvas and null-event changes do not invent bool callbacks", ref checks);
            }
            return checks;
        }

        public static int TogglePointerAndFreshGroupCallbacks()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                UIToggleGroup group = owned.Group("Pointer group");
                var first = owned.Toggle("First", group); var second = owned.Toggle("Second", group);
                var order = new List<string>();
                group.OnGroupChanged += () => order.Add("group");
                first.Toggle.OnValueChanged.AddListener(value => order.Add("first:" + value));
                second.Toggle.OnValueChanged.AddListener(value => order.Add("second:" + value));
                first.Toggle.gameObject.SetActive(false); order.Clear();
                Throws<NullReferenceException>(() => first.Toggle.OnPointerClick(null), "event dereference precedes inactive guard", ref checks);
                first.Toggle.OnSubmit(null);
                Require(!first.Toggle.IsOn && order.Count == 0, "submit ignores null data but inactive returns", ref checks);
                first.Toggle.gameObject.SetActive(true); group.RegisterToggle(first.Element); order.Clear();
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Right };
                first.Toggle.OnPointerClick(pointer);
                Require(!first.Toggle.IsOn && order.Count == 0, "right click does not toggle", ref checks);
                pointer.button = PointerEventData.InputButton.Left;
                first.Toggle.OnPointerClick(pointer);
                Require(first.Toggle.IsOn && !second.Toggle.IsOn, "left click activates first", ref checks);
                Require(order.Count == 2 && order[0] == "group" && order[1] == "first:True", "group listener precedes public toggle listener", ref checks);
                order.Clear(); first.Toggle.IsOn = true;
                Require(order.Count == 0, "equal IsOn returns before all callbacks", ref checks);
                first.Toggle.SetIsOnWithoutNotify(false);
                Require(!first.Toggle.IsOn && order.Count == 0, "non-notify writes without callbacks", ref checks);
                group.AllowSwitchOff = false;
                first.Toggle.IsOn = true; order.Clear(); first.Toggle.IsOn = false;
                Require(first.Toggle.IsOn, "last active toggle is forced back on", ref checks);
                Require(order.Count == 2 && order[1] == "first:True", "forced-on event reports final fresh value", ref checks);
                first.Toggle.Interactable = false; order.Clear(); first.Toggle.OnSubmit(null);
                Require(first.Toggle.IsOn && order.Count == 0, "nonvirtual interaction flag prevents submit", ref checks);
                first.Toggle.Interactable = true;
                first.Toggle.Rebuild(UnityEngine.UI.CanvasUpdate.Layout); first.Toggle.LayoutComplete(); first.Toggle.GraphicUpdateComplete();
                Require(order.Count == 0 && first.Toggle.IsOn, "three original empty canvas callbacks", ref checks);
                Require(ReferenceEquals(((UnityEngine.UI.ICanvasElement)first.Toggle).transform, first.Toggle.transform), "explicit canvas getter uses real transform", ref checks);
                var oldTransition = owned.Animation("Old transition"); var newTransition = owned.Animation("New transition");
                group.AllowSwitchOff = true;
                Set(first.Toggle, "m_isOn", false); Set(second.Toggle, "m_isOn", true);
                Set(first.Toggle, "m_transition", oldTransition.Transition);
                second.Toggle.OnValueChanged.AddListener(value => { if (!value) Set(first.Toggle, "m_transition", newTransition.Transition); });
                order.Clear(); first.Toggle.IsOn = true;
                Require(first.Toggle.IsOn && !second.Toggle.IsOn, "group disables previous active element", ref checks);
                Require(State(Current(oldTransition.Transition)) == UITransitionState.Normal, "captured old transition remains untouched", ref checks);
                Require(State(Current(newTransition.Transition)) == UITransitionState.Selected, "fresh transition field read after group callback", ref checks);
                Require(order.Count == 4 && order[0] == "group" && order[1] == "second:False" && order[2] == "group" && order[3] == "first:True", "nested group callbacks precede initiating event", ref checks);
            }
            return checks;
        }

        public static int MembershipAndSignedSelectionFaults()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                UIToggleGroup group = owned.Group("Membership faults"); int changed = 0;
                group.OnGroupChanged += () => ++changed;
                Throws<NullReferenceException>(() => group.RegisterToggle(null), "null register faults after addition", ref checks);
                Require(group.ToggleCount == 1 && Members(group)[0] == null, "null prefix remains in live list", ref checks);
                Require(changed == 0, "null fault occurs before group callback", ref checks);
                group.RegisterToggle(null);
                Require(group.ToggleCount == 1, "duplicate null returns without another dereference", ref checks);
                Throws<ArgumentException>(() => group.NotifyToggleOn(null), "notification validates Unity-null before iteration", ref checks);
                Throws<NullReferenceException>(() => group.UnregisterToggle(null), "null unregister faults after removal", ref checks);
                Require(group.ToggleCount == 0 && changed == 0, "removal prefix survives fault", ref checks);
                var a = owned.Toggle("A", group); var b = owned.Toggle("B", group); var c = owned.Toggle("C", group);
                Set(a.Toggle, "m_isOn", false); Set(b.Toggle, "m_isOn", false); Set(c.Toggle, "m_isOn", false);
                group.SelectPrevious();
                Require(!a.Toggle.IsOn && b.Toggle.IsOn && !c.Toggle.IsOn, "no-active previous index is middle for three", ref checks);
                Throws<ArgumentOutOfRangeException>(() => Call(group, "SelectOffsetFromActiveToggle", typeof(int), -5), "signed remainder permits negative index", ref checks);
                Require(b.Toggle.IsOn && !a.Toggle.IsOn && !c.Toggle.IsOn, "selection index fault does not toggle", ref checks);
                Set(group, "m_cycle", false); Call(group, "SelectOffsetFromActiveToggle", typeof(int), -5);
                Require(a.Toggle.IsOn && !b.Toggle.IsOn && !c.Toggle.IsOn, "non-cycle clamp selects first", ref checks);
                var snapshot = group.ActiveToggles(); a.Toggle.SetIsOnWithoutNotify(false);
                Require(snapshot.Count == 1 && ReferenceEquals(snapshot[0], a.Element) && !snapshot[0].Toggle.IsOn, "active list is a stale membership snapshot", ref checks);
                Require(group.GetFirstActiveToggle() == null && group.GetFirstActiveToggleIndex() == -1, "empty active queries", ref checks);
                var dead = owned.Toggle("Destroyed unregistered"); UIToggleGroupElement handle = dead.Element;
                Object.DestroyImmediate(dead.Toggle.gameObject);
                Require(!ReferenceEquals(handle, null) && handle == null, "actual destroyed Unity handle", ref checks);
                Throws<ArgumentException>(() => group.NotifyToggleOn(handle), "destroyed notification rejects before list lookup", ref checks);
                var faultMember = owned.Toggle("Callback fault member");
                Action fault = () => { ++changed; throw new CallbackFault(); }; group.OnGroupChanged += fault;
                Throws<CallbackFault>(() => group.RegisterToggle(faultMember.Element), "registration callback faults after listener addition", ref checks);
                Require(group.ToggleCount == 4, "registration addition is retained", ref checks);
                Throws<CallbackFault>(() => faultMember.Toggle.OnValueChanged.Invoke(true), "original group listener was already installed", ref checks);
                Throws<CallbackFault>(() => group.UnregisterToggle(faultMember.Element), "unregister callback faults after listener removal", ref checks);
                int prior = changed; faultMember.Toggle.OnValueChanged.Invoke(false);
                Require(group.ToggleCount == 3 && changed == prior, "removed list entry and listener stay removed", ref checks);
                group.OnGroupChanged -= fault;
            }
            return checks;
        }

        public static int LiveMutationAndSwitchOffFaults()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                UIToggleGroup group = owned.Group("Live callback group");
                var a = owned.Toggle("Live A", group); var b = owned.Toggle("Live B", group); var c = owned.Toggle("Live C");
                int calls = 0;
                UnityEngine.Events.UnityAction<bool> mutate = value => { ++calls; group.RegisterToggle(c.Element); };
                a.Toggle.OnValueChanged.AddListener(mutate);
                Set(a.Toggle, "m_isOn", true); Set(b.Toggle, "m_isOn", true); group.AllowSwitchOff = false;
                Throws<InvalidOperationException>(() => group.SetAllTogglesOff(), "live list change faults next MoveNext", ref checks);
                Require(group.ToggleCount == 3 && calls == 1, "callback adds third element exactly once", ref checks);
                Require(!a.Toggle.IsOn && b.Toggle.IsOn && !c.Toggle.IsOn, "only first toggle mutated before enumerator fault", ref checks);
                Require(group.AllowSwitchOff, "fault does not restore previous false flag", ref checks);
                a.Toggle.OnValueChanged.RemoveListener(mutate); group.UnregisterToggle(c.Element);
                UnityEngine.Events.UnityAction<bool> fault = value => { ++calls; throw new CallbackFault(); };
                a.Toggle.OnValueChanged.AddListener(fault);
                Set(a.Toggle, "m_isOn", true); Set(b.Toggle, "m_isOn", true); group.AllowSwitchOff = false;
                Throws<CallbackFault>(() => group.SetAllTogglesOff(), "first callback fault stops later elements", ref checks);
                Require(!a.Toggle.IsOn && b.Toggle.IsOn && calls == 2, "callback fault retains first mutation", ref checks);
                Require(group.AllowSwitchOff, "callback fault retains true flag", ref checks);
                a.Toggle.OnValueChanged.RemoveListener(fault);
                Set(a.Toggle, "m_isOn", true); Set(b.Toggle, "m_isOn", true); group.AllowSwitchOff = false;
                group.SetAllTogglesOff(false);
                Require(!a.Toggle.IsOn && !b.Toggle.IsOn, "non-notify branch clears both", ref checks);
                Require(!group.AllowSwitchOff && calls == 2, "normal completion restores previous flag without callbacks", ref checks);
                UnityEngine.Events.UnityAction<bool> removeFirst = value => { if (value) group.UnregisterToggle(a.Element); };
                a.Toggle.OnValueChanged.AddListener(removeFirst);
                group.EnsureValidState();
                Require(a.Toggle.IsOn && !b.Toggle.IsOn, "first callback can remove newly enabled element", ref checks);
                Require(group.ToggleCount == 1 && ReferenceEquals(Members(group)[0], b.Element), "fresh index now identifies remaining element", ref checks);
                Require(!group.AnyTogglesOn(), "fresh notification does not invent an on write for remaining element", ref checks);
                a.Toggle.OnValueChanged.RemoveListener(removeFirst); group.RegisterToggle(a.Element);
                Set(a.Toggle, "m_isOn", true); Set(b.Toggle, "m_isOn", true);
                group.EnsureValidState();
                Require(b.Toggle.IsOn && !a.Toggle.IsOn, "first original active-list element wins", ref checks);
                Require(ReferenceEquals(group.GetFirstActiveToggle(), b.Element), "first active getter uses current snapshot", ref checks);
                var firstSnapshot = group.ActiveToggles(); var secondSnapshot = group.ActiveToggles();
                Require(!ReferenceEquals(firstSnapshot, secondSnapshot) && firstSnapshot.Count == 1, "separate active list snapshots", ref checks);
                Require(group.GetFirstActiveToggleIndex() == 0, "actual live-list first-active index", ref checks);
            }
            return checks;
        }

        public static int ElementLifecycleMembership()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                UIToggleGroup oldGroup = owned.Group("Old group"); UIToggleGroup newGroup = owned.Group("New group");
                var member = owned.Toggle("Lifecycle member", oldGroup); int oldCalls = 0, newCalls = 0;
                oldGroup.OnGroupChanged += () => ++oldCalls; newGroup.OnGroupChanged += () => ++newCalls;
                Require(ReferenceEquals(member.Element.Toggle, member.Toggle) && ReferenceEquals(member.Toggle.GroupElement, member.Element), "genuine serialized component pair", ref checks);
                Call(member.Element, "OnDisable");
                Require(oldGroup.ToggleCount == 0 && oldCalls == 1, "disable unregisters before base", ref checks);
                Require(ReferenceEquals(member.Element.Group, oldGroup), "disable retains serialized group", ref checks);
                Call(member.Element, "OnEnable");
                Require(oldGroup.ToggleCount == 1 && oldCalls == 2, "enable registers stored group", ref checks);
                member.Element.Group = newGroup;
                Require(oldGroup.ToggleCount == 0 && newGroup.ToggleCount == 1, "setter unregisters old then registers new", ref checks);
                Require(oldCalls == 3 && newCalls == 1 && ReferenceEquals(member.Element.Group, newGroup), "setter stores new group between real callbacks", ref checks);
                Call(member.Element, "OnDestroy");
                Require(newGroup.ToggleCount == 1, "original destroy validates without explicit unregister", ref checks);
                member.Toggle.gameObject.SetActive(false); newGroup.UnregisterToggle(member.Element); int before = newCalls;
                member.Element.Group = oldGroup;
                Require(ReferenceEquals(member.Element.Group, oldGroup) && oldGroup.ToggleCount == 0, "inactive setter stores without registration", ref checks);
                Require(newCalls == before, "already removed member does not invent callback", ref checks);
                Set(member.Toggle, "m_isOn", true); Call(member.Element, "OnEnable");
                Require(oldGroup.ToggleCount == 0, "explicit enable callback still observes real inactive flag", ref checks);
                member.Toggle.gameObject.SetActive(true); oldGroup.RegisterToggle(member.Element);
                Require(oldGroup.ToggleCount == 1, "active real member can register", ref checks);
                member.Element.Group = null;
                Require(oldGroup.ToggleCount == 0 && member.Element.Group == null, "null setter removes and stores null", ref checks);
            }
            return checks;
        }

        public static int AnimationQueueCallbackAndDisableFaults()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                AnimationObjects value = owned.Animation("Queue"); UITransitionAnimation transition = value.Transition;
                Require(State(Current(transition)) == UITransitionState.Normal && State(Fallback(transition)) == UITransitionState.Normal, "explicit original Awake establishes Normal records", ref checks);
                Require(!ReferenceEquals(Current(transition), Fallback(transition)), "default records are distinct", ref checks);
                Require((bool)Get(transition, "m_isAwake") && QueueCount(transition) == 0, "awake flag follows default and queue starts empty", ref checks);
                int calls = 0; AnimationClip clip = value.Settings[1].AnimationClip;
                transition.QueueCustomAnimation(clip, () => ++calls);
                object current = Current(transition), fallback = Fallback(transition);
                Require(!ReferenceEquals(current, fallback), "custom records remain distinct", ref checks);
                Require(ReferenceEquals(Property(current, "AnimationClip"), clip) && ReferenceEquals(Property(fallback, "AnimationClip"), clip), "both records retain exact clip", ref checks);
                Require(State(current) == UITransitionState.Normal && State(fallback) == UITransitionState.Normal, "custom record default state", ref checks);
                Action currentAction = (Action)Property(current, "OnComplete"), fallbackAction = (Action)Property(fallback, "OnComplete");
                Require(!ReferenceEquals(currentAction, fallbackAction) && ReferenceEquals(currentAction.Target, fallbackAction.Target), "two original callbacks share one genuine capture", ref checks);
                Require(QueueCount(transition) == 0 && value.Animation.IsPlaying(clip.name), "TryPlay dequeues and real Animation plays", ref checks);
                transition.Action_AnimationEnd(); transition.Action_AnimationEnd();
                Require(calls == 2, "empty queue does not suppress repeated current callback", ref checks);
                object before = Current(transition); transition.QueueTransitionToState((UITransitionState)999);
                Require(ReferenceEquals(before, Current(transition)) && QueueCount(transition) == 0, "missing setting returns before publication", ref checks);
                Set(transition, "m_animation", null);
                Throws<NullReferenceException>(() => transition.QueueCustomAnimation(clip, () => { ++calls; throw new CallbackFault(); }), "restore fault follows record publication and enqueue", ref checks);
                Require(QueueCount(transition) == 1 && !ReferenceEquals(Current(transition), Fallback(transition)), "pending original prefix survives restore fault", ref checks);
                Set(transition, "m_animation", value.Animation);
                Throws<CallbackFault>(() => transition.Action_AnimationEnd(), "current callback fault precedes next playback", ref checks);
                Require(calls == 3 && QueueCount(transition) == 1, "failed callback does not dequeue", ref checks);
                before = Current(transition); object oldFallback = Fallback(transition);
                Set(transition, "m_animation", null);
                Throws<NullReferenceException>(() => Call(transition, "OnDisable"), "disable Stop fault precedes queue Clear", ref checks);
                Require(QueueCount(transition) == 1, "disable fault retains queue", ref checks);
                Set(transition, "m_animation", value.Animation); Call(transition, "OnDisable");
                Require(QueueCount(transition) == 0 && !value.Animation.isPlaying, "real Stop and queue Clear complete", ref checks);
                Require(ReferenceEquals(before, Current(transition)) && ReferenceEquals(oldFallback, Fallback(transition)), "disable leaves current/fallback references", ref checks);
                transition.QueueCustomAnimation(clip); transition.Action_AnimationEnd();
                Require(QueueCount(transition) == 0 && calls == 3, "null custom callback is optional", ref checks);
                Set(transition, "m_settings", null); Set(transition, "m_isAwake", false); before = Current(transition);
                Throws<NullReferenceException>(() => Call(transition, "Awake"), "default lookup fault precedes awake flag", ref checks);
                Require(!(bool)Get(transition, "m_isAwake") && ReferenceEquals(before, Current(transition)), "failed Awake retains prior records and false flag", ref checks);
                Set(transition, "m_settings", value.Settings);
            }
            return checks;
        }

        public static int AnimationPointerAndSettingFaultPrefixes()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                AnimationObjects value = owned.Animation("Pointer animation"); UITransitionAnimation transition = value.Transition;
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                transition.OnPointerDown(pointer);
                Require(State(Current(transition)) == UITransitionState.Pressed, "left down forces Pressed", ref checks);
                Require(State(Fallback(transition)) == UITransitionState.Normal, "force preserves fallback", ref checks);
                transition.OnPointerUp(pointer);
                Require(State(Current(transition)) == UITransitionState.Normal, "up returns to captured fallback state", ref checks);
                transition.OnPointerEnter(pointer);
                Require(State(Current(transition)) == UITransitionState.Highlighted, "left enter forces Highlighted", ref checks);
                object savedFallback = Fallback(transition); transition.OnPointerExit(pointer);
                Require(ReferenceEquals(Current(transition), savedFallback), "exit forces exact captured fallback object", ref checks);
                pointer.button = PointerEventData.InputButton.Right; Set(transition, "m_currentAnimationElement", null);
                transition.OnPointerUp(pointer); transition.OnPointerExit(pointer);
                Require(Current(transition) == null, "right button avoids current-state dereference", ref checks);
                value.Toggle.gameObject.SetActive(false); pointer.button = PointerEventData.InputButton.Left;
                Throws<NullReferenceException>(() => transition.OnPointerDown(null), "event read precedes inactive checks", ref checks);
                Set(transition, "m_currentAnimationElement", Record(value.Settings[2])); Set(transition, "m_fallbackAnimationElement", null);
                Throws<NullReferenceException>(() => transition.OnPointerUp(pointer), "fallback state read precedes inactive validation", ref checks);
                object highlighted = Record(value.Settings[1]); Set(transition, "m_currentAnimationElement", highlighted);
                transition.OnPointerExit(pointer);
                Require(ReferenceEquals(Current(transition), highlighted) && Fallback(transition) == null, "inactive exit permits captured null fallback object", ref checks);
                Set(transition, "m_settings", null); transition.OnPointerEnter(pointer);
                Require(ReferenceEquals(Current(transition), highlighted), "inactive enter returns before settings lookup", ref checks);
                Set(transition, "m_settings", value.Settings); value.Toggle.gameObject.SetActive(true); Call(transition, "Awake");
                Set(transition, "m_settings", null); value.Toggle.Interactable = false;
                transition.OnPointerDown(pointer);
                Require(State(Current(transition)) == UITransitionState.Normal, "nonvirtual false interaction flag returns before lookup", ref checks);
                value.Toggle.Interactable = true;
                Throws<NullReferenceException>(() => transition.OnPointerDown(pointer), "active interaction performs genuine null-list lookup", ref checks);
                object before = Current(transition); Set(transition, "m_settings", new List<UITransitionAnimationSetting> { null, value.Settings[0] });
                Throws<NullReferenceException>(() => transition.QueueTransitionToState(UITransitionState.Normal), "null first entry faults before later match", ref checks);
                Require(ReferenceEquals(before, Current(transition)) && QueueCount(transition) == 0, "lookup fault leaves publication/queue untouched", ref checks);
                Set(transition, "m_settings", new List<UITransitionAnimationSetting> { value.Settings[0], null });
                transition.QueueTransitionToState(UITransitionState.Normal);
                Require(State(Current(transition)) == UITransitionState.Normal, "first match returns before later null entry", ref checks);
                var nullClip = new UITransitionAnimationSetting(); Set(nullClip, "m_state", UITransitionState.Selected);
                Set(transition, "m_settings", new List<UITransitionAnimationSetting> { nullClip });
                Throws<NullReferenceException>(() => transition.QueueTransitionToState(UITransitionState.Selected), "null clip faults in Restore after enqueue", ref checks);
                Require(QueueCount(transition) == 1 && State(Current(transition)) == UITransitionState.Selected && Property(Current(transition), "AnimationClip") == null, "null-clip publication and pending queue retained", ref checks);
                Set(transition, "m_settings", value.Settings);
            }
            return checks;
        }

        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            { FieldInfo field = type.GetField(name, Own); if (field != null) return field; }
            throw new MissingFieldException(name);
        }
        private static object Get(object value, string name) => Field(value.GetType(), name).GetValue(value);
        private static void Set(object value, string name, object fieldValue) => Field(value.GetType(), name).SetValue(value, fieldValue);
        private static IList Members(UIToggleGroup group) => (IList)Get(group, "m_toggles");
        private static object Current(UITransitionAnimation value) => Get(value, "m_currentAnimationElement");
        private static object Fallback(UITransitionAnimation value) => Get(value, "m_fallbackAnimationElement");
        private static int QueueCount(UITransitionAnimation value) => ((ICollection)Get(value, "m_animationsQueue")).Count;
        private static object Property(object value, string name) => value.GetType().GetProperty(name, Own).GetValue(value, null);
        private static UITransitionState State(object record) => (UITransitionState)Property(record, "State");
        private static object Record(UITransitionAnimationSetting setting)
        {
            Type type = typeof(UITransitionAnimation).GetNestedType("AnimationElement", BindingFlags.NonPublic);
            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { setting }, null);
        }
        private static void Call(object value, string name) => Invoke(value, name, Type.EmptyTypes, new object[0]);
        private static void Call(object value, string name, Type parameterType, object argument) => Invoke(value, name, new[] { parameterType }, new[] { argument });
        private static void Invoke(object value, string name, Type[] parameterTypes, object[] arguments)
        {
            for (Type type = value.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethod(name, Own, null, parameterTypes, null);
                if (method == null) continue;
                try { method.Invoke(value, arguments); return; }
                catch (TargetInvocationException error) { throw error.InnerException ?? error; }
            }
            throw new MissingMethodException(name);
        }
        private static void Require(bool condition, string label, ref int checks)
        { if (!condition) throw new InvalidOperationException(label); ++checks; }
        private static void Throws<T>(Action action, string label, ref int checks) where T : Exception
        {
            try { action(); }
            catch (T) { ++checks; return; }
            throw new InvalidOperationException(label + ": expected " + typeof(T).Name);
        }
    }
}
