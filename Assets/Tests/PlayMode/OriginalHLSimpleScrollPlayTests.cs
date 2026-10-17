using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Bridge = ProjectLucid.Tests.OriginalSimpleScrollInputBridgeProbe;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The lerp iterator of Hardlight.HLSimpleScroll (<OnScrollWithLerp>d__16.MoveNext
    // 0x060005c2 @0x1B6ACD0), stepped by hand inside one play-mode frame so Time.deltaTime
    // is a fixed positive dt; m_lerpTime is set to 3.5 dt, so the loop body runs at timer
    // 0, dt, 2 dt and 3 dt. HLSimpleScroll stays on an inactive root, or is disabled
    // before its root is activated, so its OnEnable (Setup) never runs on its own; the
    // HLScrollRect root is activated with reset-on-enable cleared, and every root is
    // destroyed in TearDown. The Setup cases lease the UIRuntimeConfiguration system row
    // and restore the static InputBridge.OnShutdown.
    [TestFixture]
    public sealed class OriginalHLSimpleScrollPlayTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const float Tolerance = 1e-3f;

        readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedRoots()
        {
            foreach (GameObject root in roots)
                if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
        }

        static void Set(object target, System.Type type, string name, object value) => type.GetField(name, Instance).SetValue(target, value);
        static object Get(object target, System.Type type, string name) => type.GetField(name, Instance).GetValue(target);

        GameObject NewRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            roots.Add(root);
            root.SetActive(false);
            return root;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.hideFlags = HideFlags.HideAndDontSave;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        // A single-axis (vertical unless horizontal is set), unrestricted 100 x 100 viewport
        // over 100 x 300 content, an armed HLSimpleScroll with the given pixel step, and the
        // iterator for value.
        IEnumerator Lerp(int scrollPixels, float value, out HLSimpleScroll simple, out HLScrollRect scroll, bool horizontal = false)
        {
            GameObject scrollRoot = NewRoot("ProjectLucid.HLSimpleScroll.Play.ScrollRect");
            scroll = scrollRoot.AddComponent<HLScrollRect>();
            Set(scroll, typeof(HLScrollRect), "m_ResetVerticalPositionOnEnable", false);
            RectTransform viewport = NewRect("Viewport", scrollRoot.transform, new Vector2(100f, 100f));
            scroll.viewport = viewport;
            scroll.content = NewRect("Content", viewport, new Vector2(100f, 300f));
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Unrestricted;
            scrollRoot.SetActive(true);

            simple = NewRoot("ProjectLucid.HLSimpleScroll.Play.Owned").AddComponent<HLSimpleScroll>();
            Set(simple, typeof(HLSimpleScroll), "m_scrollRect", scroll);
            Set(simple, typeof(HLSimpleScroll), "m_scrollPixels", scrollPixels);
            Assert.That(Time.deltaTime, Is.GreaterThan(0f));
            Set(simple, typeof(HLSimpleScroll), "m_lerpTime", 3.5f * Time.deltaTime);
            simple.OnScrollUp((ScrollEventData)null);
            return (IEnumerator)typeof(HLSimpleScroll).GetMethod("OnScrollWithLerp", Instance).Invoke(simple, new object[] { value });
        }

        static float[] Run(IEnumerator lerp, HLScrollRect scroll, out bool finished, bool horizontal = false)
        {
            var heights = new List<float>();
            finished = false;
            for (int step = 0; step < 6; step++)
            {
                if (!lerp.MoveNext())
                {
                    finished = true;
                    break;
                }
                Assert.That(lerp.Current, Is.Null, "m_wait is never assigned");
                heights.Add(horizontal ? scroll.content.anchoredPosition.x : scroll.content.anchoredPosition.y);
            }
            return heights.ToArray();
        }

        // 0x1B6AE0B..0x1B6AE30: each step adds Lerp(0, 100, timer / (3.5 dt)) minus what was
        // already added, so the steps are 0, 100/3.5, 200/3.5 and 300/3.5; the loop ends
        // short of the target and disarms the scroll.
        [Test]
        public void LerpAddsTheGrowthOfTheLerpEachFrameAndStopsShort()
        {
            IEnumerator lerp = Lerp(200, 0.5f, out HLSimpleScroll simple, out HLScrollRect scroll);
            Assert.That(lerp.GetType().Name, Is.EqualTo("<OnScrollWithLerp>d__16"));
            float[] heights = Run(lerp, scroll, out bool finished);
            Assert.That(finished, Is.True);
            Assert.That(heights, Is.EqualTo(new[] { 0f, 100f / 3.5f, 200f / 3.5f, 300f / 3.5f }).Within(Tolerance));
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(300f / 3.5f).Within(Tolerance));
            Assert.That((bool)Get(simple, typeof(HLSimpleScroll), "m_isActiveScroll"), Is.False);
            Assert.That((float)Get(lerp, lerp.GetType(), "<timer>5__2"), Is.EqualTo(4f * Time.deltaTime).Within(1e-6f));
            Assert.That((Vector2)Get(lerp, lerp.GetType(), "<lerpTarget>5__3"), Is.EqualTo(new Vector2(0f, 100f)));
        }

        // 0x1B6AE46..0x1B6AEB6: a positive value subtracts a step with a negative component,
        // so a negative pixel step still moves the content up.
        [Test]
        public void PositiveValueSubtractsNegativeSteps()
        {
            IEnumerator lerp = Lerp(-200, 0.5f, out _, out HLScrollRect scroll);
            float[] heights = Run(lerp, scroll, out bool finished);
            Assert.That(finished, Is.True);
            Assert.That(heights, Is.EqualTo(new[] { 0f, 100f / 3.5f, 200f / 3.5f, 300f / 3.5f }).Within(Tolerance));
        }

        // The subtraction needs a positive value: a negative value adds its negative steps.
        [Test]
        public void NegativeValueAddsItsSteps()
        {
            IEnumerator lerp = Lerp(200, -0.5f, out _, out HLScrollRect scroll);
            float[] heights = Run(lerp, scroll, out bool finished);
            Assert.That(finished, Is.True);
            Assert.That(heights, Is.EqualTo(new[] { 0f, -100f / 3.5f, -200f / 3.5f, -300f / 3.5f }).Within(Tolerance));
        }

        // 0x1B6AD1F: an inactive rect ends the iterator at once and leaves the scroll armed.
        [Test]
        public void InactiveRectEndsTheLerpAndLeavesTheScrollArmed()
        {
            IEnumerator lerp = Lerp(200, 0.5f, out HLSimpleScroll simple, out HLScrollRect scroll);
            scroll.enabled = false;
            Assert.That(lerp.MoveNext(), Is.False);
            Assert.That(scroll.content.anchoredPosition.y, Is.Zero);
            Assert.That((bool)Get(simple, typeof(HLSimpleScroll), "m_isActiveScroll"), Is.True);
        }

        // 0x1B6AE79..0x1B6AE7D: the x component is tested first and a negative one jumps
        // straight to the subtraction, so a horizontal negative pixel step also moves the
        // content right.
        [Test]
        public void PositiveValueSubtractsANegativeHorizontalStep()
        {
            IEnumerator lerp = Lerp(-200, 0.5f, out _, out HLScrollRect scroll, horizontal: true);
            float[] offsets = Run(lerp, scroll, out bool finished, horizontal: true);
            Assert.That(finished, Is.True);
            Assert.That(offsets, Is.EqualTo(new[] { 0f, 100f / 3.5f, 200f / 3.5f, 300f / 3.5f }).Within(Tolerance));
            Assert.That(scroll.content.anchoredPosition.y, Is.Zero);
        }

        // 0x1B6A438..0x1B6A4D4 and 0x1B6A948..0x1B6A9EB: with m_lerpScroll set, each input
        // stops the running lerp, starts a new one and stores its handle in m_coroutine. The
        // first lerp step is zero, so the content only moves on later frames; once the last
        // handle is stopped by hand the content stays put, which needs both handlers to have
        // stopped their predecessor.
        [UnityTest]
        public IEnumerator LerpInputReplacesTheRunningLerp()
        {
            GameObject scrollRoot = new GameObject("ProjectLucid.HLSimpleScroll.Play.LerpScrollRect", typeof(RectTransform));
            roots.Add(scrollRoot);
            scrollRoot.SetActive(false);
            HLScrollRect scroll = scrollRoot.AddComponent<HLScrollRect>();
            Set(scroll, typeof(HLScrollRect), "m_ResetVerticalPositionOnEnable", false);
            RectTransform viewport = NewRect("Viewport", scrollRoot.transform, new Vector2(100f, 100f));
            scroll.viewport = viewport;
            scroll.content = NewRect("Content", viewport, new Vector2(100f, 300f));
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Unrestricted;
            scrollRoot.SetActive(true);

            GameObject owner = new GameObject("ProjectLucid.HLSimpleScroll.Play.LerpOwner");
            roots.Add(owner);
            owner.SetActive(false);
            HLSimpleScroll simple = owner.AddComponent<HLSimpleScroll>();
            // Disabled first, so activating the root starts coroutines without running Setup.
            simple.enabled = false;
            owner.SetActive(true);
            Set(simple, typeof(HLSimpleScroll), "m_scrollRect", scroll);
            Set(simple, typeof(HLSimpleScroll), "m_lerpScroll", true);
            Set(simple, typeof(HLSimpleScroll), "m_lerpTime", 1f);
            simple.OnScrollUp((ScrollEventData)null);

            simple.OnScrollUp(0.5f);
            var first = (Coroutine)Get(simple, typeof(HLSimpleScroll), "m_coroutine");
            Assert.That(first, Is.Not.Null);
            simple.OnScrollUp(0.5f);
            var second = (Coroutine)Get(simple, typeof(HLSimpleScroll), "m_coroutine");
            Assert.That(second, Is.Not.Null.And.Not.SameAs(first));
            simple.OnScrollDown(0.5f);
            var third = (Coroutine)Get(simple, typeof(HLSimpleScroll), "m_coroutine");
            Assert.That(third, Is.Not.Null.And.Not.SameAs(second));
            Assert.That(scroll.content.anchoredPosition.y, Is.Zero);
            Assert.That((bool)Get(simple, typeof(HLSimpleScroll), "m_isActiveScroll"), Is.True, "a running lerp leaves the scroll armed");

            simple.StopCoroutine(third);
            yield return null;
            yield return null;
            Assert.That(scroll.content.anchoredPosition.y, Is.Zero);
        }

        static List<HLInputSupplier> Suppliers(UIRuntimeConfiguration configuration, List<Object> owned)
        {
            var suppliers = new List<HLInputSupplier>();
            foreach (string name in new[] { "m_scrollUpInput", "m_scrollDownInput" })
            {
                HLInputSupplier supplier = ScriptableObject.CreateInstance<HLInputSupplier>();
                owned.Add(supplier);
                suppliers.Add(supplier);
                Set(configuration, typeof(UIRuntimeConfiguration), name, supplier);
            }
            return suppliers;
        }

        static void CallSetup(HLSimpleScroll simple)
        {
            try { typeof(HLSimpleScroll).GetMethod("Setup", Instance).Invoke(simple, null); }
            catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static void AssertShutdownBound(HLSimpleScroll simple)
        {
            List<Action> listeners = InputBridge.OnShutdown.GetInvocationList();
            Assert.That(listeners.Count, Is.EqualTo(1));
            Assert.That(listeners[0].Method.Name, Is.EqualTo("ClearUp"));
            Assert.That(listeners[0].Target, Is.SameAs(simple));
        }

        // Setup 0x060005b7 (0x1B69FA0) against a registered UIRuntimeConfiguration whose
        // root stays inactive (its Awake never runs): ClearUp joins InputBridge.OnShutdown,
        // the bridge is read from the configuration, and the up (+0x20) then down (+0x28)
        // inputs are registered on press through slot 19 with joystick index -1.
        [Test]
        public void SetupSubscribesShutdownAndRegistersBothInputsOnPress()
        {
            var lease = new LifecycleSystemRowLease(LifecycleSystemRowLease.SharedRegistry, ProcessManager.GetDefaultName<UIRuntimeConfiguration>());
            FastAction previous = InputBridge.OnShutdown;
            var owned = new List<Object>();
            UIRuntimeConfiguration configuration = null;
            using (lease)
            {
                try
                {
                    Bridge bridge = ScriptableObject.CreateInstance<Bridge>();
                    owned.Add(bridge);
                    configuration = NewRoot("ProjectLucid.HLSimpleScroll.Play.Configuration").AddComponent<UIRuntimeConfiguration>();
                    List<HLInputSupplier> suppliers = Suppliers(configuration, owned);
                    Set(configuration, typeof(UIRuntimeConfiguration), "m_inputBridge", bridge);
                    lease.CaptureCreatedRow(ProcessManager.RegisterSystem(configuration));
                    HLSimpleScroll simple = NewRoot("ProjectLucid.HLSimpleScroll.Play.Setup").AddComponent<HLSimpleScroll>();
                    InputBridge.OnShutdown = null;

                    CallSetup(simple);
                    AssertShutdownBound(simple);
                    Assert.That(Get(simple, typeof(HLSimpleScroll), "m_configuration"), Is.SameAs(configuration));
                    Assert.That(Get(simple, typeof(HLSimpleScroll), "m_inputBridge"), Is.SameAs(bridge));
                    Assert.That(bridge.Registrations.Select(r => r.Kind).ToArray(), Is.EqualTo(new[] { "down", "down" }));
                    Assert.That(bridge.Registrations.Select(r => r.Supplier).ToArray(), Is.EqualTo(suppliers.ToArray()));
                    Assert.That(bridge.Registrations.Select(r => r.Callback.Method.Name).ToArray(), Is.EqualTo(new[] { "OnScrollUp", "OnScrollDown" }));
                    Assert.That(bridge.Registrations.All(r => ReferenceEquals(r.Callback.Target, simple) && r.JoystickIndex == -1), Is.True);
                }
                finally
                {
                    InputBridge.OnShutdown = previous;
                    lease.RunCleanup("unregister-runtime-configuration", () => { if (!ReferenceEquals(configuration, null)) ProcessManager.UnregisterSystem(configuration); });
                    foreach (Object item in owned)
                        lease.RunCleanup("destroy-owned", () => { if (item != null) Object.DestroyImmediate(item); });
                }
            }
            Assert.IsEmpty(lease.CleanupFailures);
            Assert.IsEmpty(lease.DiagnosticFailures);
        }

        // 0x1B6A109: the configuration is compared with Unity equality after OnShutdown is
        // joined, so a registered but destroyed configuration (whose managed fields still
        // name a bridge) returns before the bridge is read or anything is registered.
        [Test]
        public void SetupWithADestroyedConfigurationOnlySubscribesShutdown()
        {
            var lease = new LifecycleSystemRowLease(LifecycleSystemRowLease.SharedRegistry, ProcessManager.GetDefaultName<UIRuntimeConfiguration>());
            FastAction previous = InputBridge.OnShutdown;
            var owned = new List<Object>();
            UIRuntimeConfiguration configuration = null;
            using (lease)
            {
                try
                {
                    Bridge bridge = ScriptableObject.CreateInstance<Bridge>();
                    owned.Add(bridge);
                    configuration = NewRoot("ProjectLucid.HLSimpleScroll.Play.DestroyedConfiguration").AddComponent<UIRuntimeConfiguration>();
                    Suppliers(configuration, owned);
                    Set(configuration, typeof(UIRuntimeConfiguration), "m_inputBridge", bridge);
                    lease.CaptureCreatedRow(ProcessManager.RegisterSystem(configuration));
                    Object.DestroyImmediate(configuration);
                    Assert.That(configuration == null && !ReferenceEquals(configuration, null), Is.True);
                    HLSimpleScroll simple = NewRoot("ProjectLucid.HLSimpleScroll.Play.SetupDestroyed").AddComponent<HLSimpleScroll>();
                    InputBridge.OnShutdown = null;

                    CallSetup(simple);
                    AssertShutdownBound(simple);
                    Assert.That(ReferenceEquals(Get(simple, typeof(HLSimpleScroll), "m_configuration"), configuration), Is.True);
                    Assert.That(Get(simple, typeof(HLSimpleScroll), "m_inputBridge"), Is.Null);
                    Assert.That(bridge.Registrations, Is.Empty);
                }
                finally
                {
                    InputBridge.OnShutdown = previous;
                    lease.RunCleanup("unregister-runtime-configuration", () => { if (!ReferenceEquals(configuration, null)) ProcessManager.UnregisterSystem(configuration); });
                    foreach (Object item in owned)
                        lease.RunCleanup("destroy-owned", () => { if (item != null) Object.DestroyImmediate(item); });
                }
            }
            Assert.IsEmpty(lease.CleanupFailures);
            Assert.IsEmpty(lease.DiagnosticFailures);
        }
    }
}
