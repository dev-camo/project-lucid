using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Bridge = ProjectLucid.Tests.OriginalSimpleScrollInputBridgeProbe;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.HLSimpleScroll (HLUnityUI.Runtime 0x060005b5..5c5) against the
    // shipped x86-64 bodies; the time-driven lerp iterator is covered by
    // OriginalHLSimpleScrollPlayTests. Every case owns its HideAndDontSave roots and
    // destroys them in TearDown. HLSimpleScroll stays on an inactive root, so its
    // OnEnable (Setup, which reaches the global ProcessManager) never runs. OnScroll needs
    // HLScrollRect.IsActive(), so the scroll rect's root is activated after its
    // reset-on-enable flag is cleared; where ScrollRect's [ExecuteAlways] callbacks run,
    // that OnEnable queues a layout rebuild in the shared CanvasUpdateRegistry and the
    // TearDown destroy (OnDisable) marks the destroyed rect, which the registry drops on
    // its next update. The ClearUp case restores the static InputBridge.OnShutdown.
    [TestFixture]
    public sealed class OriginalHLSimpleScrollTests
    {
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const float Tolerance = 1e-3f;

        readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwned()
        {
            foreach (Object item in owned)
                if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
        }

        static object Get(object target, Type type, string name) => type.GetField(name, Instance).GetValue(target);
        static void Set(object target, Type type, string name, object value) => type.GetField(name, Instance).SetValue(target, value);

        static void Call(HLSimpleScroll scroll, string name)
        {
            try { typeof(HLSimpleScroll).GetMethod(name, Instance).Invoke(scroll, null); }
            catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        GameObject NewRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            owned.Add(root);
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

        // A 100 x 100 viewport over 100 x 300 content at (0, 0); the scroll rect is active
        // and enabled, unrestricted unless a case says otherwise.
        HLSimpleScroll Build(bool horizontal, bool vertical, out HLScrollRect scroll)
        {
            GameObject scrollRoot = NewRoot("ProjectLucid.HLSimpleScroll.ScrollRect");
            scroll = scrollRoot.AddComponent<HLScrollRect>();
            Set(scroll, typeof(HLScrollRect), "m_ResetVerticalPositionOnEnable", false);
            RectTransform viewport = NewRect("Viewport", scrollRoot.transform, new Vector2(100f, 100f));
            scroll.viewport = viewport;
            scroll.content = NewRect("Content", viewport, new Vector2(100f, 300f));
            scroll.horizontal = horizontal;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Unrestricted;
            scrollRoot.SetActive(true);
            Assert.That(scroll.IsActive(), Is.True);

            HLSimpleScroll simple = NewRoot("ProjectLucid.HLSimpleScroll.Owned").AddComponent<HLSimpleScroll>();
            Set(simple, typeof(HLSimpleScroll), "m_scrollRect", scroll);
            return simple;
        }

        static bool Armed(HLSimpleScroll simple) => (bool)Get(simple, typeof(HLSimpleScroll), "m_isActiveScroll");
        static void Arm(HLSimpleScroll simple) => simple.OnScrollUp((ScrollEventData)null);

        static void Near(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }

        // dump.cs TypeDefIndex 12036/12035 and DummyDll metadata: sealed, attribute and
        // interface order, field order/types/attributes, the constructor defaults 200 and
        // 0.3 (0x1B6ACA0), and the iterator <OnScrollWithLerp>d__16 with its original
        // hoisted names (offsets 0x10..0x38).
        [Test]
        public void DeclarationKeepsTheDumpedFieldsDefaultsAndIterator()
        {
            Type type = typeof(HLSimpleScroll);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("Hardlight.HLSimpleScroll"));
            Assert.That(type.IsSealed && type.BaseType == typeof(MonoBehaviour), Is.True);
            CustomAttributeData[] attributes = type.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Select(a => a.AttributeType).Distinct().Single(), Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(attributes.Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)).ToArray(), Is.EqualTo(new[] { 1, 2 }));
            CollectionAssert.IsSubsetOf(new[] { typeof(IScrollUpHandler), typeof(IEventSystemHandler), typeof(IScrollDownHandler) }, type.GetInterfaces());

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "m_scrollPixels", "m_scrollRect", "m_lerpScroll", "m_lerpTime", "m_wait", "m_coroutine",
                "m_isActiveScroll", "m_configuration", "m_inputBridge",
            }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                typeof(int), typeof(HLScrollRect), typeof(bool), typeof(float), typeof(WaitForEndOfFrame), typeof(Coroutine),
                typeof(bool), typeof(UIRuntimeConfiguration), typeof(InputBridge),
            }));
            Assert.That(fields.All(f => f.IsPrivate), Is.True);
            Assert.That(fields.Select(f => f.IsDefined(typeof(SerializeField))).ToArray(),
                Is.EqualTo(new[] { true, true, true, true, false, false, false, false, false }));

            foreach (string name in new[] { "OnScrollUp", "OnScrollDown" })
            {
                MethodInfo handler = type.GetMethod(name, new[] { typeof(ScrollEventData) });
                Assert.That(handler.IsPublic && handler.IsFinal && handler.IsVirtual, Is.True, name);
                Assert.That(type.GetMethod(name, new[] { typeof(float) }).IsVirtual, Is.False, name);
            }

            Type iterator = type.GetNestedTypes(Declared).Single();
            Assert.That(iterator.Name, Is.EqualTo("<OnScrollWithLerp>d__16"));
            FieldInfo[] hoisted = iterator.GetFields(Instance);
            Assert.That(hoisted.Take(7).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "<>1__state", "<>2__current", "<>4__this", "value", "<timer>5__2", "<lerpTarget>5__3", "<cumulativeLerpAmount>5__4",
            }));
            Assert.That(hoisted.Take(7).Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                typeof(int), typeof(object), type, typeof(float), typeof(float), typeof(Vector2), typeof(Vector2),
            }));

            HLSimpleScroll simple = NewRoot("ProjectLucid.HLSimpleScroll.Defaults").AddComponent<HLSimpleScroll>();
            Assert.That(Get(simple, type, "m_scrollPixels"), Is.EqualTo(200));
            Assert.That(Get(simple, type, "m_lerpTime"), Is.EqualTo(0.3f));
            Assert.That(Get(simple, type, "m_lerpScroll"), Is.EqualTo(false));
            Assert.That(Get(simple, type, "m_wait"), Is.Null);
            Assert.That(Armed(simple), Is.False);
        }

        // 0x060005bd/0x060005be arm the scroll; 0x060005b9 does nothing until then.
        [Test]
        public void ScrollEventsArmTheInputScroll()
        {
            HLSimpleScroll simple = Build(false, true, out HLScrollRect scroll);
            simple.OnScrollUp(0.5f);
            Near(scroll.content.anchoredPosition, Vector2.zero);

            simple.OnScrollDown((ScrollEventData)null);
            Assert.That(Armed(simple), Is.True);
            Set(simple, typeof(HLSimpleScroll), "m_isActiveScroll", false);
            Arm(simple);
            Assert.That(Armed(simple), Is.True);
        }

        // 0x060005bb: the value times 200 pixels moves the only enabled axis, then the
        // scroll is disarmed. Down passes its value unchanged, so it also moves up.
        [Test]
        public void InputScrollMovesTheSingleEnabledAxisByThePixelStep()
        {
            HLSimpleScroll simple = Build(false, true, out HLScrollRect scroll);
            Arm(simple);
            simple.OnScrollUp(0.5f);
            Near(scroll.content.anchoredPosition, new Vector2(0f, 100f));
            Assert.That(Armed(simple), Is.False);
            Arm(simple);
            simple.OnScrollDown(0.5f);
            Near(scroll.content.anchoredPosition, new Vector2(0f, 200f));
            Arm(simple);
            simple.OnScrollDown(-0.25f);
            Near(scroll.content.anchoredPosition, new Vector2(0f, 150f));

            HLSimpleScroll sideways = Build(true, false, out HLScrollRect horizontal);
            Arm(sideways);
            sideways.OnScrollUp(0.5f);
            Near(horizontal.content.anchoredPosition, new Vector2(100f, 0f));
        }

        // 0x1B6A438 / 0x1B6A948: with m_lerpScroll set both handlers start the lerp
        // iterator instead of calling OnScroll. On the inactive HLSimpleScroll root
        // StartCoroutine logs and returns null into m_coroutine, so nothing moves and the
        // scroll stays armed; the active-root path is in OriginalHLSimpleScrollPlayTests.
        [Test]
        public void LerpScrollStartsTheIteratorInsteadOfScrolling()
        {
            HLSimpleScroll simple = Build(false, true, out HLScrollRect scroll);
            Set(simple, typeof(HLSimpleScroll), "m_lerpScroll", true);
            Arm(simple);
            var inactive = new System.Text.RegularExpressions.Regex("Coroutine couldn't be started");

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, inactive);
            simple.OnScrollUp(0.5f);
            Near(scroll.content.anchoredPosition, Vector2.zero);
            Assert.That(Armed(simple), Is.True);
            Assert.That(Get(simple, typeof(HLSimpleScroll), "m_coroutine"), Is.Null);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, inactive);
            simple.OnScrollDown(0.5f);
            Near(scroll.content.anchoredPosition, Vector2.zero);
            Assert.That(Armed(simple), Is.True);
            Assert.That(Get(simple, typeof(HLSimpleScroll), "m_coroutine"), Is.Null);
        }

        // 0x1B6A62C..0x1B6A659: with both or neither axis enabled the step is zero, but the
        // scroll is still disarmed.
        [Test]
        public void InputScrollWithBothOrNeitherAxisDoesNotMoveButDisarms()
        {
            foreach (bool both in new[] { true, false })
            {
                HLSimpleScroll simple = Build(both, both, out HLScrollRect scroll);
                Arm(simple);
                simple.OnScrollUp(0.5f);
                Near(scroll.content.anchoredPosition, Vector2.zero);
                Assert.That(Armed(simple), Is.False, both ? "both" : "neither");
            }
        }

        // 0x1B6A3EA..0x1B6A42B is the inlined ContentNeedsScrolling, and 0x1B6A5E2 returns
        // before disarming when the rect is inactive: both leave the scroll armed.
        [Test]
        public void TooSmallContentOrAnInactiveRectLeavesTheScrollArmed()
        {
            HLSimpleScroll simple = Build(false, true, out HLScrollRect scroll);
            Set(scroll, typeof(HLScrollRect), "m_doNotScrollIfContentIsTooSmall", true);
            typeof(HLScrollRect).GetProperty("ContentHeightIsTooSmall").SetValue(scroll, true);
            Arm(simple);
            simple.OnScrollUp(0.5f);
            Near(scroll.content.anchoredPosition, Vector2.zero);
            Assert.That(Armed(simple), Is.True);

            typeof(HLScrollRect).GetProperty("ContentHeightIsTooSmall").SetValue(scroll, false);
            scroll.enabled = false;
            simple.OnScrollUp(0.5f);
            Near(scroll.content.anchoredPosition, Vector2.zero);
            Assert.That(Armed(simple), Is.True);
        }

        // 0x1B6A682/0x1B6A711: a clamped rect adds HLScrollRect's offset for the stale
        // m_ContentBounds (no UpdateBounds first), so a move below the top is undone and
        // the unchanged position is not written; a move that stays inside is kept. The view
        // side is viewRect.rect (0x1B6A794/0x1B6A79E), y in [-50, 50], not m_ViewBounds.
        [Test]
        public void ClampedInputScrollUsesTheCachedContentBounds()
        {
            HLSimpleScroll simple = Build(false, true, out HLScrollRect scroll);
            scroll.movementType = ScrollRect.MovementType.Clamped;
            FieldInfo bounds = typeof(ScrollRect).GetField("m_ContentBounds", BindingFlags.Instance | BindingFlags.NonPublic);
            bounds.SetValue(scroll, new Bounds(new Vector3(0f, -100f, 0f), new Vector3(100f, 300f, 0f)));
            Arm(simple);
            simple.OnScrollUp(-0.5f);
            Near(scroll.content.anchoredPosition, Vector2.zero);
            Assert.That(Armed(simple), Is.False);
            Assert.That(((Bounds)bounds.GetValue(scroll)).center.y, Is.EqualTo(-100f), "no UpdateBounds without a move");

            Arm(simple);
            simple.OnScrollUp(0.5f);
            Near(scroll.content.anchoredPosition, new Vector2(0f, 100f));
        }

        // 0x060005b8 (through OnDisable 0x060005b6): ClearUp leaves InputBridge.OnShutdown
        // and unregisters both press listeners with joystick index -1; with no bridge it only
        // leaves OnShutdown.
        [Test]
        public void ClearUpLeavesShutdownAndUnregistersBothInputs()
        {
            FastAction previous = InputBridge.OnShutdown;
            var suppliers = new List<HLInputSupplier>();
            Bridge bridge = ScriptableObject.CreateInstance<Bridge>();
            bridge.hideFlags = HideFlags.HideAndDontSave;
            owned.Add(bridge);
            try
            {
                HLSimpleScroll simple = NewRoot("ProjectLucid.HLSimpleScroll.ClearUp").AddComponent<HLSimpleScroll>();
                UIRuntimeConfiguration configuration = NewRoot("ProjectLucid.HLSimpleScroll.Configuration").AddComponent<UIRuntimeConfiguration>();
                foreach (string name in new[] { "m_scrollUpInput", "m_scrollDownInput" })
                {
                    HLInputSupplier supplier = ScriptableObject.CreateInstance<HLInputSupplier>();
                    supplier.hideFlags = HideFlags.HideAndDontSave;
                    owned.Add(supplier);
                    suppliers.Add(supplier);
                    Set(configuration, typeof(UIRuntimeConfiguration), name, supplier);
                }
                Set(simple, typeof(HLSimpleScroll), "m_configuration", configuration);
                Set(simple, typeof(HLSimpleScroll), "m_inputBridge", bridge);
                var clearUp = (Action)Delegate.CreateDelegate(typeof(Action), simple, typeof(HLSimpleScroll).GetMethod("ClearUp", Instance));
                InputBridge.OnShutdown = null;
                InputBridge.OnShutdown += clearUp;

                Call(simple, "OnDisable");
                Assert.That(InputBridge.OnShutdown.GetInvocationListCount(), Is.Zero);
                Assert.That(bridge.Registrations.Select(r => r.Kind).ToArray(), Is.EqualTo(new[] { "unregister", "unregister" }));
                Assert.That(bridge.Registrations.Select(r => r.Supplier).ToArray(), Is.EqualTo(suppliers.ToArray()));
                Assert.That(bridge.Registrations.Select(r => r.Callback.Method.Name).ToArray(), Is.EqualTo(new[] { "OnScrollUp", "OnScrollDown" }));
                Assert.That(bridge.Registrations.All(r => ReferenceEquals(r.Callback.Target, simple) && r.JoystickIndex == -1), Is.True);

                HLSimpleScroll lone = NewRoot("ProjectLucid.HLSimpleScroll.NoBridge").AddComponent<HLSimpleScroll>();
                InputBridge.OnShutdown += (Action)Delegate.CreateDelegate(typeof(Action), lone, typeof(HLSimpleScroll).GetMethod("ClearUp", Instance));
                Call(lone, "ClearUp");
                Assert.That(InputBridge.OnShutdown.GetInvocationListCount(), Is.Zero);
                Assert.That(bridge.Registrations.Count, Is.EqualTo(2));
            }
            finally
            {
                InputBridge.OnShutdown = previous;
            }
        }
    }
}
