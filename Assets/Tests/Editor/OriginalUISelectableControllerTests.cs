using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Tests.UISelectionShared;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.UISelectableController (HLUnityUI.Runtime
    // 0x060003db..0x06000407) against the shipped x86-64 bodies. Each case owns
    // a controller on an inactive object, a RecordingSelectionInputBridge in its
    // m_inputBridge field and RecordingUISelectable candidates whose lifecycle
    // callbacks stay off. Edit mode never calls the controller's Start, so no
    // system is registered; SetUp asserts that and UISelectableStaticState
    // saves and clears the static selectable tables for the case. Exclusive
    // scenes registered by a case are removed in TearDown.
    public sealed class OriginalUISelectableControllerTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Declared = Any | BindingFlags.Static | BindingFlags.DeclaredOnly;
        const string ExclusiveScene = "UISelectableController exclusive fixture scene";

        UISelectableStaticState m_state;
        readonly List<Object> m_owned = new List<Object>();
        List<string> m_savedExclusiveScenes;

        static List<string> ExclusiveScenes =>
            (List<string>)typeof(UISelectableController).GetField("s_exclusiveScenes", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        [SetUp]
        public void TakeStaticState()
        {
            Assert.That(ProcessManager.IsSystemValid<UISelectableController>(), Is.False, "fixture needs no UISelectableController system");
            Assert.That(ProcessManager.IsSystemNull<UIRuntimeConfiguration>(), Is.True, "fixture needs no UIRuntimeConfiguration system");
            m_savedExclusiveScenes = ExclusiveScenes.ToList();
            m_state = new UISelectableStaticState();
        }

        [TearDown]
        public void RestoreStaticState()
        {
            for (int i = m_owned.Count - 1; i >= 0; i--)
            {
                if (m_owned[i] != null) Object.DestroyImmediate(m_owned[i]);
            }
            m_owned.Clear();
            m_state?.Dispose();
            m_state = null;
            if (m_savedExclusiveScenes != null)
            {
                ExclusiveScenes.Clear();
                ExclusiveScenes.AddRange(m_savedExclusiveScenes);
            }
        }

        T Own<T>(T obj) where T : Object
        {
            m_owned.Add(obj);
            return obj;
        }

        (UISelectableController controller, RecordingSelectionInputBridge bridge) NewController()
        {
            var go = Own(new GameObject("UISelectableController fixture") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            var controller = go.AddComponent<UISelectableController>();
            var bridge = Own(ScriptableObject.CreateInstance<RecordingSelectionInputBridge>());
            Set(controller, "m_inputBridge", bridge);
            return (controller, bridge);
        }

        RecordingUISelectable NewSelectable(string name, bool active, Vector3 position = default)
        {
            var go = Own(new GameObject(name) { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(active);
            go.transform.position = position;
            return go.AddComponent<RecordingUISelectable>();
        }

        // Valid for navigation while no controller system is registered.
        RecordingUISelectable NewNavigable(string name, Vector3 position, string scene = null)
        {
            RecordingUISelectable selectable = NewSelectable(name, true, position);
            selectable.Navigation = UINavigation.DefaultNavigation;
            UISelectable.Add(selectable, scene);
            return selectable;
        }

        static object Get(object target, string field) => typeof(UISelectableController).GetField(field, Any).GetValue(target);
        static void Set(object target, string field, object value) => typeof(UISelectableController).GetField(field, Any).SetValue(target, value);

        static object Call(object target, string method, params object[] args)
        {
            try { return typeof(UISelectableController).GetMethod(method, Any).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        [Test]
        public void DeclarationKeepsOriginalFieldsAndGeneratedNames()
        {
            Type type = typeof(UISelectableController);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("Hardlight.UISelectableController"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(SelectableControllerBase)));
            CustomAttributeData[] options = type.GetCustomAttributesData().ToArray();
            Assert.That(options.Select(a => a.AttributeType).Distinct().Single(), Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(options.Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)).ToArray(), Is.EqualTo(new[] { 1, 2 }));

            // dump.cs TypeDefIndex 11953.
            Assert.That(type.GetFields(Declared).Select(f => f.Name).ToArray(), Is.EqualTo(new[] {
                "MaxTrackedSubmittedSelectables", "m_buttonHighlightObject", "m_buttonHighlightTransitionLayer",
                "m_buttonHighlightTransitionTime", "m_sameSceneButtonWeighting", "m_movementEasingType", "m_selectableOriginPoint",
                "m_forceInstantSelection", "m_coroutine", "m_isButtonHighlightActive", "m_currentlySelected", "m_cachedButtonHighlight",
                "m_inputBridge", "s_exclusiveScenes", "m_systemActive", "m_systemEnabled", "m_cachedLastInputType",
                "m_recentlySubmittedSelectables", "m_overrideList", "m_lastKnownSelected", "m_setInstantTransition",
                "m_recoveryScreenOrigin", "OnSelect", "OnDeselect", "BlockFindSelectable", "UseOverrideList" }));
            Assert.That(type.GetField("MaxTrackedSubmittedSelectables", Declared).GetRawConstantValue(), Is.EqualTo(5));
            Assert.That(type.GetField("m_movementEasingType", Any).GetCustomAttributesData().Select(a => a.AttributeType).ToArray(),
                Is.EqualTo(new[] { typeof(HashEnumAttribute), typeof(SerializeField) }));

            ParameterInfo destroyed = type.GetMethod("DeactivateSystem", Declared).GetParameters().Single();
            Assert.That(destroyed.HasDefaultValue, Is.False);
            Assert.That(type.GetMethod("SelectThis", Declared).GetParameters()[1].HasDefaultValue, Is.True);
            Assert.That(type.GetMethod("DisableSystem", Declared).GetParameters().Single().HasDefaultValue, Is.True);

            Type lambdas = type.GetNestedType("<>c", BindingFlags.NonPublic);
            Assert.That(lambdas.IsDefined(typeof(CompilerGeneratedAttribute), false), Is.True);
            CollectionAssert.IsSupersetOf(lambdas.GetMethods(Declared).Select(m => m.Name),
                new[] { "<TryGetCachedSelectable>b__53_0", "<AppReloading>b__54_0", "<.ctor>b__61_0", "<.ctor>b__61_1" });

            Type iterator = type.GetNestedType("<MoveToNewParent>d__50", BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null);
            // Debug compilations may hoist further locals after these; the
            // original eleven keep their order and numbering.
            string[] original = { "<>1__state", "<>2__current", "<>4__this", "selectable", "<timer>5__2", "<selectedIndicatorTransform>5__3",
                "<cachedRect>5__4", "<cachedScale>5__5", "<cachedRot>5__6", "<cachedOffsetMin>5__7", "<cachedOffsetMax>5__8", "<cachedAnchoredPosition3D>5__9" };
            Assert.That(iterator.GetFields(Declared).Select(f => f.Name).Where(original.Contains).ToArray(), Is.EqualTo(original));
        }

        [Test]
        public void ConstructionSeedsTransitionTimeWeightingAndUnsupportedInput()
        {
            var (controller, _) = NewController();
            // 0x1B58DF6..0x1B58E03 and the two cached lambdas.
            Assert.That(controller.ButtonTransitionTime, Is.EqualTo(0.2f));
            Assert.That(controller.GetButtonTransitionTime(), Is.EqualTo(0.2f));
            Assert.That(controller.SameSceneButtonWeighting, Is.EqualTo(3f));
            Assert.That(Get(controller, "m_cachedLastInputType"), Is.EqualTo(UIInputType.Unsupported));
            Assert.That(controller.OverrideList, Is.Empty);
            Assert.That(controller.OverrideList, Is.Not.SameAs(Get(controller, "m_recentlySubmittedSelectables")));
            Assert.That(controller.BlockFindSelectable(), Is.False);
            Assert.That(controller.UseOverrideList(), Is.False);
            Assert.That(controller.ShouldBlockFindSelectable(), Is.False);

            controller.BlockFindSelectable = () => true;
            Assert.That(controller.ShouldBlockFindSelectable(), Is.True);
        }

        [Test]
        public void TrackRecentlySubmittedKeepsTheFiveNewestAndCopiesTheCurrentSelection()
        {
            var (controller, _) = NewController();
            RecordingUISelectable current = NewSelectable("Current", false);
            Set(controller, "m_currentlySelected", current);
            RecordingUISelectable[] submitted = Enumerable.Range(0, 6).Select(i => NewSelectable("Submitted " + i, false)).ToArray();

            foreach (RecordingUISelectable selectable in submitted) controller.TrackRecentlySubmittedSelectable(selectable);
            var recent = (List<UISelectable>)Get(controller, "m_recentlySubmittedSelectables");
            Assert.That(recent, Is.EqualTo(submitted.Skip(1).ToArray()));
            Assert.That(Get(controller, "m_lastKnownSelected"), Is.SameAs(current));

            controller.TrackRecentlySubmittedSelectable(submitted[2]);
            Assert.That(recent, Is.EqualTo(new UISelectable[] { submitted[1], submitted[3], submitted[4], submitted[5], submitted[2] }));
        }

        [Test]
        public void ExclusiveScenesAreRegisteredOnceAndRemovedOnDemand()
        {
            var (controller, _) = NewController();
            controller.RegisterExclusiveScene(ExclusiveScene);
            controller.RegisterExclusiveScene(ExclusiveScene);
            Assert.That(ExclusiveScenes.Count(s => s == ExclusiveScene), Is.EqualTo(1));
            controller.DeregisterExclusiveScene(ExclusiveScene);
            Assert.That(ExclusiveScenes, Does.Not.Contain(ExclusiveScene));
            Assert.DoesNotThrow(() => controller.DeregisterExclusiveScene(ExclusiveScene));
        }

        [Test]
        public void ActivationForASelectionInputEnablesAndHidesTheCursor()
        {
            var (controller, bridge) = NewController();
            bridge.LastInputType = bridge.SelectionInputType = UIInputType.Keyboard;

            controller.ActivateSystem();
            // With no highlighter prefab nothing is spawned, but the flags are still
            // set (0x1B575CE); the downward search finds nothing.
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "GetLastInputType", "DoesInputTypeRequireSelection Keyboard", "SetHardwareCursorVisibility False" }));
            Assert.That(new[] { "m_systemActive", "m_systemEnabled", "m_isButtonHighlightActive", "m_setInstantTransition" }.Select(f => (bool)Get(controller, f)), Is.All.True);
            Assert.That(controller.CurrentlySelected, Is.Null);
        }

        [Test]
        public void EnablingSelectsTheBestCandidateBelowTheRecoveryOrigin()
        {
            var (controller, bridge) = NewController();
            bridge.LastInputType = bridge.SelectionInputType = UIInputType.Keyboard;
            RecordingUISelectable below = NewNavigable("Below", new Vector3(0f, -3f, 0f));
            NewNavigable("Above", new Vector3(0f, 3f, 0f));

            controller.ActivateSystem();
            Assert.That(controller.CurrentlySelected, Is.SameAs(below));
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "GetLastInputType", "DoesInputTypeRequireSelection Keyboard", "SetSelectedGameObjectWithMemory", "SetHardwareCursorVisibility False" }));
            Assert.That(bridge.GameObjects.Single(), Is.SameAs(below.gameObject));
            Assert.That(bridge.EventData.Single(), Is.Null);
        }

        [Test]
        public void ActivationWithoutASelectionInputStaysDisabled()
        {
            var (controller, bridge) = NewController();
            bridge.LastInputType = UIInputType.Touch;
            bridge.SelectionInputType = UIInputType.Keyboard;
            controller.ActivateSystem();
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "GetLastInputType", "DoesInputTypeRequireSelection Touch" }));
            Assert.That(Get(controller, "m_systemActive"), Is.True);
            Assert.That(Get(controller, "m_systemEnabled"), Is.False);
        }

        [Test]
        public void InputTypeChangesAreIgnoredWhileInactiveOrRepeated()
        {
            var (controller, bridge) = NewController();
            bridge.SelectionInputType = UIInputType.Keyboard;
            Call(controller, "OnUpdateLastInputType", UIInputType.Keyboard);
            Assert.That(bridge.Calls, Is.Empty);
            Assert.That(Get(controller, "m_cachedLastInputType"), Is.EqualTo(UIInputType.Unsupported));

            Set(controller, "m_systemActive", true);
            Call(controller, "OnUpdateLastInputType", UIInputType.Unsupported);
            Assert.That(bridge.Calls, Is.Empty);

            Call(controller, "OnUpdateLastInputType", UIInputType.Touch);
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "DoesInputTypeRequireSelection Touch" }));
            Assert.That(Get(controller, "m_cachedLastInputType"), Is.EqualTo(UIInputType.Touch));
            Assert.That(Get(controller, "m_systemEnabled"), Is.False);
        }

        [Test]
        public void InputTypeChangesToggleTheEnabledSystem()
        {
            var (controller, bridge) = NewController();
            bridge.SelectionInputType = UIInputType.Keyboard;
            Set(controller, "m_systemActive", true);

            Call(controller, "OnUpdateLastInputType", UIInputType.Keyboard);
            Assert.That(Get(controller, "m_systemEnabled"), Is.True);
            Assert.That(bridge.Calls.Last(), Is.EqualTo("SetHardwareCursorVisibility False"));

            bridge.Calls.Clear();
            Call(controller, "OnUpdateLastInputType", UIInputType.Touch);
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "DoesInputTypeRequireSelection Touch", "SetHardwareCursorVisibility True" }));
            Assert.That(Get(controller, "m_systemEnabled"), Is.False);
        }

        [Test]
        public void DeactivateSystemOnlyActsWhileActive()
        {
            var (controller, bridge) = NewController();
            controller.DeactivateSystem(false);
            Assert.That(bridge.Calls, Is.Empty);

            Set(controller, "m_systemActive", true);
            Set(controller, "m_systemEnabled", true);
            controller.DeactivateSystem(true);
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "SetHardwareCursorVisibility True" }));
            Assert.That(Get(controller, "m_systemActive"), Is.False);
            Assert.That(Get(controller, "m_systemEnabled"), Is.False);
        }

        [Test]
        public void DisabledSystemRecordsAnySelectionWithoutValidation()
        {
            var (controller, bridge) = NewController();
            RecordingUISelectable inactive = NewSelectable("Inactive", false);
            controller.SelectThis(inactive);
            Assert.That(controller.CurrentlySelected, Is.SameAs(inactive));
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "SetSelectedGameObjectWithMemory" }));
            Assert.That(bridge.GameObjects.Single(), Is.SameAs(inactive.gameObject));
            Assert.That(controller.IsCurrentSelection(inactive), Is.True);
        }

        [Test]
        public void EnabledSelectionDeselectsThePreviousAndAnnouncesTheNew()
        {
            var (controller, bridge) = NewController();
            Set(controller, "m_systemEnabled", true);
            RecordingUISelectable previous = NewSelectable("Previous", false);
            RecordingUISelectable next = NewNavigable("Next", Vector3.zero);
            Set(controller, "m_currentlySelected", previous);
            var events = new List<string>();
            controller.OnSelect += s => events.Add("select " + s.name);
            controller.OnDeselect += s => events.Add("deselect " + s.name);

            controller.SelectThis(next);
            Assert.That(events, Is.EqualTo(new[] { "deselect Previous", "select Next" }));
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "DeselectObjectIfCurrentlySelected", "SetSelectedGameObjectWithMemory" }));
            Assert.That(bridge.GameObjects, Is.EqualTo(new[] { previous.gameObject, next.gameObject }));
            Assert.That(controller.CurrentlySelected, Is.SameAs(next));
            Assert.That(Get(controller, "m_lastKnownSelected"), Is.SameAs(previous));
            Assert.That(Get(controller, "m_setInstantTransition"), Is.False);

            // Reselecting goes to HighlightThis, which needs an active highlighter.
            events.Clear();
            controller.SelectThis(next, true);
            Assert.That(events, Is.Empty);
            Assert.That(Get(controller, "m_setInstantTransition"), Is.False);

            Set(controller, "m_forceInstantSelection", true);
            controller.SelectThis(NewNavigable("Forced", Vector3.zero));
            Assert.That(Get(controller, "m_setInstantTransition"), Is.True);
        }

        [Test]
        public void EnabledSelectionIgnoresAnInvalidSelectable()
        {
            var (controller, bridge) = NewController();
            Set(controller, "m_systemEnabled", true);
            controller.SelectThis(NewSelectable("Inactive", false));
            Assert.That(controller.CurrentlySelected, Is.Null);
            Assert.That(bridge.Calls, Is.Empty);
        }

        [Test]
        public void DeselectWithNothingSelectedOnlyClearsTheLastKnown()
        {
            var (controller, bridge) = NewController();
            Set(controller, "m_lastKnownSelected", NewSelectable("Last", false));
            controller.OnDeselect += _ => Assert.Fail("nothing was selected");
            controller.DeselectCurrentSelection();
            Assert.That(Get(controller, "m_lastKnownSelected"), Is.Null);
            Assert.That(bridge.Calls, Is.Empty);
        }

        [Test]
        public void IsValidSelectableForNavigationRejectsEachFailingCondition()
        {
            Assert.That(UISelectableController.IsValidSelectableForNavigation(null), Is.False);
            Assert.That(UISelectableController.IsValidSelectableForNavigation(NewNavigable("Valid", Vector3.zero)), Is.True);

            RecordingUISelectable noMode = NewNavigable("No mode", Vector3.zero);
            noMode.Navigation = default(UINavigation);
            RecordingUISelectable inactive = NewSelectable("Inactive", false);
            inactive.Navigation = UINavigation.DefaultNavigation;
            RecordingUISelectable never = NewNavigable("Never", Vector3.zero);
            typeof(UISelectable).GetField("m_neverSelectable", Any).SetValue(never, true);
            RecordingUISelectable disabled = NewNavigable("Disabled component", Vector3.zero);
            disabled.enabled = false;
            RecordingUISelectable uninteractable = NewNavigable("Not interactable", Vector3.zero);
            typeof(UISelectable).GetField("m_interactable", Any).SetValue(uninteractable, false);
            RecordingUISelectable hiddenParent = NewNavigable("Hidden parent", Vector3.zero);
            var parent = Own(new GameObject("Inactive parent") { hideFlags = HideFlags.HideAndDontSave });
            parent.SetActive(false);
            hiddenParent.transform.SetParent(parent.transform, false);

            foreach (RecordingUISelectable selectable in new[] { noMode, inactive, never, disabled, uninteractable, hiddenParent })
                Assert.That(UISelectableController.IsValidSelectableForNavigation(selectable), Is.False, selectable.name);
        }

        [Test]
        public void FindSelectablePrefersAlignmentOverDistance()
        {
            // Scores are dot / sqrMagnitude: (0, 3) scores 1/3, the nearer (2, 1)
            // only 1/5 (0x1B57CAF).
            RecordingUISelectable aligned = NewNavigable("Aligned", new Vector3(0f, 3f, 0f));
            NewNavigable("Nearer", new Vector3(2f, 1f, 0f));
            Assert.That(UISelectableController.FindSelectable(Vector3.up, Vector3.zero, null, null), Is.SameAs(aligned));

            RecordingUISelectable close = NewNavigable("Close", new Vector3(0f, 1f, 0f));
            Assert.That(UISelectableController.FindSelectable(Vector3.up, Vector3.zero, null, null), Is.SameAs(close));
        }

        [Test]
        public void FindSelectableSkipsTheCallerAndCandidatesNotAhead()
        {
            RecordingUISelectable caller = NewNavigable("Caller", new Vector3(0f, 1f, 0f));
            NewNavigable("Behind", new Vector3(0f, -1f, 0f));
            NewNavigable("Beside", new Vector3(4f, 0f, 0f));
            Assert.That(UISelectableController.FindSelectable(Vector3.up, Vector3.zero, caller, null), Is.Null);

            RecordingUISelectable ahead = NewNavigable("Ahead", new Vector3(0f, 6f, 0f));
            Assert.That(UISelectableController.FindSelectable(Vector3.up, Vector3.zero, caller, null), Is.SameAs(ahead));
        }

        [Test]
        public void ExclusiveSceneLimitsTheSearchToItsOwnList()
        {
            var (controller, _) = NewController();
            RecordingUISelectable global = NewNavigable("Global", new Vector3(0f, 1f, 0f));
            RecordingUISelectable scoped = NewNavigable("Scoped", new Vector3(0f, 5f, 0f), ExclusiveScene);

            Assert.That(UISelectableController.FindSelectable(Vector3.up, Vector3.zero, null, ExclusiveScene), Is.SameAs(global), "not yet exclusive");
            controller.RegisterExclusiveScene(ExclusiveScene);
            Assert.That(UISelectableController.FindSelectable(Vector3.up, Vector3.zero, null, ExclusiveScene), Is.SameAs(scoped));
            Assert.That(UISelectableController.FindSelectable(Vector3.up, Vector3.zero, null, "Another scene"), Is.SameAs(global));
            Assert.That(UISelectableController.FindSelectable(Vector3.up, Vector3.zero, null, null), Is.SameAs(global));
        }

        [Test]
        public void FindSelectableFromATransformStartsAtItsRectEdge()
        {
            // From the right edge (100, 0) "Far" (150, 0) scores 1/50 against
            // "Offset" (101, 10) at 1/101; from the centre "Offset" would win.
            var origin = (RectTransform)Own(new GameObject("Origin", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave }).transform;
            origin.sizeDelta = new Vector2(200f, 100f);
            RecordingUISelectable far = NewNavigable("Far", new Vector3(150f, 0f, 0f));
            RecordingUISelectable offset = NewNavigable("Offset", new Vector3(101f, 10f, 0f));
            Assert.That(UISelectableController.FindSelectable(Vector3.right, Vector3.zero, null, null), Is.SameAs(offset));
            Assert.That(UISelectableController.FindSelectable(Vector3.right, origin, null, null), Is.SameAs(far));

            // Rotated a quarter turn, the world-right edge is the local bottom
            // edge at (50, 0): "Offset" scores 51/2701 against 100/10000.
            origin.rotation = Quaternion.Euler(0f, 0f, 90f);
            Assert.That(UISelectableController.FindSelectable(Vector3.right, origin, null, null), Is.SameAs(offset));
        }

        [Test]
        public void TryGetCachedSelectablePrefersLastKnownThenNewestValidSubmission()
        {
            var (controller, _) = NewController();
            RecordingUISelectable last = NewNavigable("Last", Vector3.zero);
            Set(controller, "m_lastKnownSelected", last);
            Assert.That(controller.TryGetCachedSelectable(), Is.SameAs(last));

            last.gameObject.SetActive(false);
            RecordingUISelectable older = NewNavigable("Older", Vector3.zero);
            RecordingUISelectable newer = NewNavigable("Newer", Vector3.zero);
            RecordingUISelectable destroyed = NewNavigable("Destroyed", Vector3.zero);
            RecordingUISelectable newestInvalid = NewSelectable("Newest invalid", false);
            var recent = (List<UISelectable>)Get(controller, "m_recentlySubmittedSelectables");
            recent.AddRange(new UISelectable[] { older, newer, destroyed, newestInvalid });
            Object.DestroyImmediate(destroyed.gameObject);

            Assert.That(controller.TryGetCachedSelectable(), Is.SameAs(newer));
            Assert.That(recent, Is.EqualTo(new UISelectable[] { older, newer, newestInvalid }));
        }

        [Test]
        public void TryGetCachedSelectableFallsBackToADownwardSearchFromTheRecoveryOrigin()
        {
            var (controller, _) = NewController();
            RecordingUISelectable upper = NewNavigable("Upper", new Vector3(0f, 8f, 0f));
            RecordingUISelectable lower = NewNavigable("Lower", new Vector3(0f, -2f, 0f));
            Assert.That(controller.TryGetCachedSelectable(), Is.SameAs(lower));

            // Start would put the origin at the top of the screen; from (0, 10)
            // the nearer candidate below is "Upper".
            Set(controller, "m_recoveryScreenOrigin", new Vector3(0f, 10f, 0f));
            Assert.That(controller.TryGetCachedSelectable(), Is.SameAs(upper));
        }

        [Test]
        public void AppReloadingResetsTheBlockDelegateAndDisables()
        {
            var (controller, bridge) = NewController();
            Func<bool> blocking = () => true;
            controller.BlockFindSelectable = blocking;
            Set(controller, "m_systemEnabled", true);

            controller.AppReloading();
            Assert.That(controller.BlockFindSelectable, Is.Not.SameAs(blocking));
            Assert.That(controller.BlockFindSelectable(), Is.False);
            Assert.That(Get(controller, "m_systemEnabled"), Is.False);
            Assert.That(bridge.Calls, Is.EqualTo(new[] { "SetHardwareCursorVisibility True" }));
        }
    }
}
