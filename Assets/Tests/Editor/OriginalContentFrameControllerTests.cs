using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour specification for Hardlight.ContentFrameController (HLUnityUI.Runtime
    // 0x0600054e..0x0600055b) and ContentFrameControllerOverride (0x06000022..0x0600002a),
    // written from the shipped x86-64 bodies. Every case owns inactive GameObjects, so
    // lifecycle methods run only when a case invokes them. Any ScreenManager a case needs
    // is an inactive component published as MonoSingleton<ScreenManager>.Instance and
    // unpublished before returning.
    //
    // The editor's Screen.safeArea is not under test control, so the dodged landscape and
    // portrait insets are not exercised here; every Apply case below is chosen so that
    // the native branch structure gives the initial anchors whether or not the editor
    // reports a safe area.
    [TestFixture]
    public sealed class OriginalContentFrameControllerTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const float Tolerance = 1e-5f;
        static readonly Type Subject = typeof(ContentFrameController);

        static object Get(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);

        static object Call(object target, string name, params object[] args)
        {
            try { return Subject.GetMethod(name, Any).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        sealed class Handler : IContentFrameControllerOverrideHandler
        {
            public bool Active;
            public int Queries;
            public event Action<bool> OnApply;

            public bool IsOverrideActive()
            {
                Queries++;
                return Active;
            }

            public int Subscribers => OnApply?.GetInvocationList().Length ?? 0;
            public void Raise(bool force) => OnApply?.Invoke(force);
        }

        static ContentFrameControllerOverride Override(Handler handler, bool side = false, bool landscapeBottom = false,
            bool portraitTop = false, bool portraitBottom = false)
        {
            var frameOverride = new ContentFrameControllerOverride();
            Set(frameOverride, "m_overrideLandscapeSideMargin", side);
            Set(frameOverride, "m_overrideLandscapeBottomMargin", landscapeBottom);
            Set(frameOverride, "m_overridePortraitTopMargin", portraitTop);
            Set(frameOverride, "m_overridePortraitBottomMargin", portraitBottom);
            if (handler != null) frameOverride.SetContentFrameControllerOverrideHandler(handler);
            return frameOverride;
        }

        sealed class Hosts : IDisposable
        {
            readonly List<GameObject> owned = new List<GameObject>();

            public GameObject Inactive(string name)
            {
                var owner = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                owned.Add(owner);
                return owner;
            }

            public ContentFrameController Controller(bool withRoot = true)
            {
                ContentFrameController controller = Inactive("ContentFrameController fixture").AddComponent<ContentFrameController>();
                if (withRoot)
                {
                    var root = (RectTransform)Inactive("Content root").transform;
                    root.anchorMin = new Vector2(0.1f, 0.2f);
                    root.anchorMax = new Vector2(0.7f, 0.9f);
                    Set(controller, "m_contentRoot", root);
                }
                return controller;
            }

            public void Dispose()
            {
                foreach (GameObject owner in owned)
                    if (owner != null) Object.DestroyImmediate(owner);
            }
        }

        sealed class ScreenFixture : IDisposable
        {
            static readonly MethodInfo SetInstance =
                typeof(MonoSingleton<ScreenManager>).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetSetMethod(true);
            static readonly MethodInfo SetDeviceOrientation =
                typeof(ScreenManager).GetProperty("DeviceOrientation", BindingFlags.Public | BindingFlags.Instance).GetSetMethod(true);
            static readonly FieldInfo SafeAreaChange = typeof(ScreenManager).GetField("OnSafeAreaChange", BindingFlags.Instance | BindingFlags.NonPublic);

            readonly GameObject owner;
            public readonly ScreenManager Manager;

            public ScreenFixture(ScreenOrientation orientation)
            {
                Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "Fixture requires no foreign ScreenManager singleton");
                owner = new GameObject("Owned ScreenManager") { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                try
                {
                    Manager = owner.AddComponent<ScreenManager>();
                    SetDeviceOrientation.Invoke(Manager, new object[] { orientation });
                    SetInstance.Invoke(null, new object[] { Manager });
                }
                catch { Object.DestroyImmediate(owner); throw; }
            }

            public Action<bool> SafeAreaListeners => (Action<bool>)SafeAreaChange.GetValue(Manager);
            public int SafeAreaCount => SafeAreaListeners?.GetInvocationList().Length ?? 0;
            public int OrientationCount => Manager.OnOrientationChange?.GetInvocationListCount() ?? 0;

            public void Dispose()
            {
                try
                {
                    if (!ReferenceEquals(ScreenManager.Instance, Manager))
                        throw new InvalidOperationException("Fixture singleton was replaced");
                    SetInstance.Invoke(null, new object[] { null });
                }
                finally { Object.DestroyImmediate(owner); }
            }
        }

        static RectTransform Root(ContentFrameController controller) => (RectTransform)Get(controller, "m_contentRoot");
        static bool ShouldLerp(ContentFrameController controller) => (bool)Get(controller, "m_shouldLerp");
        static Vector2 Vector(ContentFrameController controller, string name) => (Vector2)Get(controller, name);

        static void AssertVector(Vector2 expected, Vector2 actual, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), label + ".x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), label + ".y");
        }

        static void AssertRatio(float expected, float actual, string label)
        {
            if (float.IsNaN(expected) || float.IsInfinity(expected)) Assert.That(actual, Is.EqualTo(expected), label);
            else Assert.That(actual, Is.EqualTo(expected).Within(Tolerance), label);
        }

        // ---- declarations -------------------------------------------------------

        [Test]
        public void DeclarationsKeepTheOriginalConstantsAndFieldOrder()
        {
            float Constant(string name) => (float)Subject.GetField(name, Any).GetRawConstantValue();
            Assert.That(Constant("NotchUnits"), Is.EqualTo(30f));
            Assert.That(Constant("IPhoneXSizeUnits"), Is.EqualTo(812f));
            // 0x29a8530 holds 0x3eb33333, the factor both landscape ratios use.
            Assert.That(Constant("SideRatioShrink"), Is.EqualTo(0.35f));
            Assert.That(Constant("NoRatio"), Is.EqualTo(0f));

            string[] dumped =
            {
                "NotchUnits", "IPhoneXSizeUnits", "m_contentRoot", "m_landscapeNotchDodge", "m_portraitNotchDodge",
                "m_lerpTransitionTime", "m_initialAnchorMin", "m_initialAnchorMax", "m_currentAnchorMin", "m_currentAnchorMax",
                "m_shouldLerp", "m_lerpProgress", "m_targetAnchorMin", "m_targetAnchorMax", "m_portraitTopMarginRatio",
                "m_portraitBottomMarginRatio", "m_landscapeSideMarginRatio", "m_landscapeBottomMarginRatio",
                "m_contentFrameControllerOverride", "m_isInitialised", "SideRatioShrink", "NoRatio"
            };
            string[] declared = Subject.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray();
            Assert.That(declared, Is.EqualTo(dumped));
        }

        [Test]
        public void ConstructorSeedsTheLerpTimeAndLeavesTheFrameUninitialised()
        {
            using (var hosts = new Hosts())
            {
                ContentFrameController controller = hosts.Controller(withRoot: false);
                // 0x1b65e69 stores 0x3e99999a.
                Assert.That((float)Get(controller, "m_lerpTransitionTime"), Is.EqualTo(0.3f));
                foreach (string name in new[] { "m_initialAnchorMin", "m_initialAnchorMax", "m_currentAnchorMin",
                             "m_currentAnchorMax", "m_targetAnchorMin", "m_targetAnchorMax" })
                    Assert.That(Vector(controller, name), Is.EqualTo(Vector2.zero), name);
                Assert.That(ShouldLerp(controller), Is.False);
                Assert.That((bool)Get(controller, "m_isInitialised"), Is.False);
                Assert.That(Get(controller, "m_contentFrameControllerOverride"), Is.Null);
            }
        }

        // ---- EnsureInitialisation (0x1b65ae0) ---------------------------------------

        [Test]
        public void EnsureInitialisationMeasuresTheSafeAreaOnceAgainstTheResolutionHeight()
        {
            using (var hosts = new Hosts())
            {
                ContentFrameController controller = hosts.Controller();
                Call(controller, "EnsureInitialisation");

                // The native body reads safeArea.y, then height - safeArea.yMax, then
                // max(safeArea.x, width - safeArea.xMax), and divides each by
                // currentResolution.height; both landscape ratios are scaled by 0.35.
                Rect safeArea = Screen.safeArea;
                float top = safeArea.y;
                float bottom = Screen.height - safeArea.yMax;
                float side = Mathf.Max(safeArea.x, Screen.width - safeArea.xMax);
                float height = Screen.currentResolution.height;
                AssertRatio(top / height, (float)Get(controller, "m_portraitTopMarginRatio"), "portrait top");
                AssertRatio(bottom / height, (float)Get(controller, "m_portraitBottomMarginRatio"), "portrait bottom");
                AssertRatio(side * 0.35f / height, (float)Get(controller, "m_landscapeSideMarginRatio"), "landscape side");
                AssertRatio(bottom * 0.35f / height, (float)Get(controller, "m_landscapeBottomMarginRatio"), "landscape bottom");
                AssertVector(new Vector2(0.1f, 0.2f), Vector(controller, "m_initialAnchorMin"), "initial min");
                AssertVector(new Vector2(0.7f, 0.9f), Vector(controller, "m_initialAnchorMax"), "initial max");

                // A second call is a no-op: neither the ratios nor the initial anchors are re-read.
                Set(controller, "m_portraitTopMarginRatio", 123f);
                Root(controller).anchorMin = new Vector2(0.3f, 0.3f);
                Call(controller, "EnsureInitialisation");
                Assert.That((float)Get(controller, "m_portraitTopMarginRatio"), Is.EqualTo(123f));
                AssertVector(new Vector2(0.1f, 0.2f), Vector(controller, "m_initialAnchorMin"), "initial min after second call");
            }
        }

        [Test]
        public void ApplyWithoutAContentRootInitialisesButDoesNotLerp()
        {
            using (var hosts = new Hosts())
            {
                ContentFrameController controller = hosts.Controller(withRoot: false);
                // Returns at 0x1b653c9, before the ScreenManager singleton is read.
                Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "No ScreenManager is needed on this path");
                Call(controller, "Apply", false);
                Assert.That((bool)Get(controller, "m_isInitialised"), Is.True);
                Assert.That(ShouldLerp(controller), Is.False);
                Assert.That(Vector(controller, "m_initialAnchorMin"), Is.EqualTo(Vector2.zero));
            }
        }

        // ---- Apply (0x1b65350) and StartLerp (0x1b65d70) ----------------------------

        // Orientation values: Portrait 1, LandscapeLeft 3, LandscapeRight 4, AutoRotation 5
        // (the native compares DeviceOrientation against 3/4 and 1/2).
        [TestCase(false, false, 3)]
        [TestCase(false, false, 1)]
        [TestCase(true, false, 1)]
        [TestCase(false, true, 4)]
        [TestCase(true, true, 5)]
        public void ApplyWithoutADodgedOrientationLerpsBackToTheInitialAnchors(bool landscapeDodge, bool portraitDodge, int orientation)
        {
            using (var hosts = new Hosts())
            using (new ScreenFixture((ScreenOrientation)orientation))
            {
                ContentFrameController controller = hosts.Controller();
                Set(controller, "m_landscapeNotchDodge", landscapeDodge);
                Set(controller, "m_portraitNotchDodge", portraitDodge);
                Call(controller, "EnsureInitialisation");
                Root(controller).anchorMin = new Vector2(0.05f, 0.15f);
                Root(controller).anchorMax = new Vector2(0.95f, 0.85f);
                Set(controller, "m_lerpProgress", 5f);

                Call(controller, "Apply", false);

                AssertVector(new Vector2(0.1f, 0.2f), Vector(controller, "m_targetAnchorMin"), "target min");
                AssertVector(new Vector2(0.7f, 0.9f), Vector(controller, "m_targetAnchorMax"), "target max");
                AssertVector(new Vector2(0.05f, 0.15f), Vector(controller, "m_currentAnchorMin"), "current min");
                AssertVector(new Vector2(0.95f, 0.85f), Vector(controller, "m_currentAnchorMax"), "current max");
                Assert.That(ShouldLerp(controller), Is.True);
                Assert.That((float)Get(controller, "m_lerpProgress"), Is.EqualTo(0f));
                // Apply only arms the lerp; the anchors move in Update.
                AssertVector(new Vector2(0.05f, 0.15f), Root(controller).anchorMin, "live anchorMin");
            }
        }

        // ---- Update (0x1b65db0) -----------------------------------------------------

        static ContentFrameController Lerping(Hosts hosts, float progress, float time)
        {
            ContentFrameController controller = hosts.Controller();
            Root(controller).anchorMin = new Vector2(0.9f, 0.9f);
            Root(controller).anchorMax = new Vector2(0.9f, 0.9f);
            Set(controller, "m_shouldLerp", true);
            Set(controller, "m_lerpProgress", progress);
            Set(controller, "m_lerpTransitionTime", time);
            Set(controller, "m_currentAnchorMin", new Vector2(0f, 0f));
            Set(controller, "m_currentAnchorMax", new Vector2(1f, 1f));
            Set(controller, "m_targetAnchorMin", new Vector2(0.2f, 0.4f));
            Set(controller, "m_targetAnchorMax", new Vector2(0.6f, 0.8f));
            return controller;
        }

        [Test]
        public void UpdateAdvancesTheProgressAndLerpsFromTheStoredCurrentAnchors()
        {
            using (var hosts = new Hosts())
            {
                ContentFrameController controller = Lerping(hosts, 0.1f, 0.3f);
                float progress = 0.1f + Time.deltaTime;
                float t = Mathf.Clamp01(progress / 0.3f);
                Call(controller, "Update");
                Assert.That((float)Get(controller, "m_lerpProgress"), Is.EqualTo(progress).Within(Tolerance));
                // The lerp starts from m_currentAnchor*, not from the live anchors (0.9, 0.9).
                AssertVector(new Vector2(0.2f * t, 0.4f * t), Root(controller).anchorMin, "anchorMin");
                AssertVector(new Vector2(1f - 0.4f * t, 1f - 0.2f * t), Root(controller).anchorMax, "anchorMax");
                Assert.That(ShouldLerp(controller), Is.True, "The flag is cleared only by a later Update");
            }
        }

        [Test]
        public void UpdateClampsTheFractionToTheUnitRange()
        {
            float dt = Time.deltaTime;
            Assert.That(dt, Is.LessThan(1f), "Precondition: the editor delta time keeps both fractions outside [0, 1]");
            using (var hosts = new Hosts())
            {
                // (-2 + dt) / -1 > 1 clamps to 1 (minss with 1.0 at 0x29a6be0).
                ContentFrameController above = Lerping(hosts, -2f, -1f);
                Call(above, "Update");
                AssertVector(new Vector2(0.2f, 0.4f), Root(above).anchorMin, "upper clamp anchorMin");
                AssertVector(new Vector2(0.6f, 0.8f), Root(above).anchorMax, "upper clamp anchorMax");

                // (-5 + dt) / 1 < 0 clamps to 0 (cmpltss/andnps).
                ContentFrameController below = Lerping(hosts, -5f, 1f);
                Call(below, "Update");
                AssertVector(Vector2.zero, Root(below).anchorMin, "lower clamp anchorMin");
                AssertVector(Vector2.one, Root(below).anchorMax, "lower clamp anchorMax");
            }
        }

        [Test]
        public void UpdateAtTheTransitionTimeOnlyClearsTheFlag()
        {
            using (var hosts = new Hosts())
            {
                ContentFrameController finished = Lerping(hosts, 0.3f, 0.3f);
                Call(finished, "Update");
                Assert.That(ShouldLerp(finished), Is.False);
                Assert.That((float)Get(finished, "m_lerpProgress"), Is.EqualTo(0.3f));
                AssertVector(new Vector2(0.9f, 0.9f), Root(finished).anchorMin, "anchorMin untouched");

                ContentFrameController idle = Lerping(hosts, 0.1f, 0.3f);
                Set(idle, "m_shouldLerp", false);
                Call(idle, "Update");
                Assert.That((float)Get(idle, "m_lerpProgress"), Is.EqualTo(0.1f));
                AssertVector(new Vector2(0.9f, 0.9f), Root(idle).anchorMin, "idle anchorMin untouched");
            }
        }

        // ---- lifecycle and subscriptions (0x1b650d0, 0x1b65110, 0x1b65850) ----------

        [Test]
        public void OnEnableWithoutAnOverrideSubscribesBothCallbacksAndApplies()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(ScreenOrientation.Portrait))
            {
                ContentFrameController controller = hosts.Controller();
                Call(controller, "OnEnable");
                Assert.That(ShouldLerp(controller), Is.True, "OnEnable applies when no override is active");
                Assert.That(screen.OrientationCount, Is.EqualTo(1));
                Assert.That(screen.SafeAreaCount, Is.EqualTo(1));
                Assert.That(screen.SafeAreaListeners.GetInvocationList()[0].Target, Is.SameAs(controller));

                Set(controller, "m_shouldLerp", false);
                FastAction.Invoke(screen.Manager.OnOrientationChange);
                Assert.That(ShouldLerp(controller), Is.True, "The orientation callback applies");

                Set(controller, "m_shouldLerp", false);
                screen.SafeAreaListeners(true);
                Assert.That(ShouldLerp(controller), Is.True, "The safe-area callback applies");

                Call(controller, "OnDisable");
                Assert.That(screen.OrientationCount, Is.EqualTo(0));
                Assert.That(screen.SafeAreaCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void OnEnableWithAnActiveOverrideRegistersApplyWithTheHandlerInstead()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(ScreenOrientation.Portrait))
            {
                var handler = new Handler { Active = true };
                ContentFrameController controller = hosts.Controller();
                Set(controller, "m_contentFrameControllerOverride", Override(handler));

                Call(controller, "OnEnable");
                Assert.That((bool)Get(controller, "m_isInitialised"), Is.False, "OnEnable does not apply while the override is active");
                Assert.That(handler.Subscribers, Is.EqualTo(1));
                Assert.That(screen.OrientationCount, Is.EqualTo(0), "The orientation callback is replaced by the handler");
                Assert.That(screen.SafeAreaCount, Is.EqualTo(1), "The safe-area callback is registered either way");

                handler.Raise(true);
                Assert.That(ShouldLerp(controller), Is.True, "The handler drives Apply");

                Call(controller, "OnDisable");
                Assert.That(handler.Subscribers, Is.EqualTo(0));
                Assert.That(screen.SafeAreaCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void OnEnableWithAnInactiveOverrideKeepsTheOrientationCallback()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(ScreenOrientation.Portrait))
            {
                var handler = new Handler { Active = false };
                ContentFrameController controller = hosts.Controller();
                Set(controller, "m_contentFrameControllerOverride", Override(handler));

                Call(controller, "OnEnable");
                Assert.That(handler.Subscribers, Is.EqualTo(0));
                Assert.That(screen.OrientationCount, Is.EqualTo(1));
                Assert.That(ShouldLerp(controller), Is.True);
            }
        }

        [Test]
        public void OnEnableWithoutAScreenManagerOnlyRegistersWithTheHandler()
        {
            using (var hosts = new Hosts())
            {
                Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "Precondition: no ScreenManager singleton");
                var handler = new Handler { Active = true };
                ContentFrameController controller = hosts.Controller(withRoot: false);
                Set(controller, "m_contentFrameControllerOverride", Override(handler));
                Call(controller, "OnEnable");
                Assert.That(handler.Subscribers, Is.EqualTo(1));
                Call(controller, "OnDisable");
                Assert.That(handler.Subscribers, Is.EqualTo(0));

                // Without an override, a missing singleton skips both subscriptions and
                // Apply returns at the missing content root.
                ContentFrameController plain = hosts.Controller(withRoot: false);
                Call(plain, "OnEnable");
                Assert.That((bool)Get(plain, "m_isInitialised"), Is.True);
            }
        }

        [Test]
        public void SetOverrideMovesTheSubscriptionsOnlyForTheFirstOverride()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(ScreenOrientation.Portrait))
            {
                ContentFrameController controller = hosts.Controller();
                Call(controller, "OnEnable");
                Assert.That(screen.OrientationCount, Is.EqualTo(1));

                var first = new Handler { Active = true };
                ContentFrameControllerOverride firstOverride = Override(first);
                controller.SetOverride(firstOverride);
                Assert.That(Get(controller, "m_contentFrameControllerOverride"), Is.SameAs(firstOverride));
                Assert.That(screen.OrientationCount, Is.EqualTo(0), "Unregistered before the override was stored");
                Assert.That(first.Subscribers, Is.EqualTo(1));
                Assert.That(screen.SafeAreaCount, Is.EqualTo(1), "Removed and added again, not doubled");

                var second = new Handler { Active = true };
                controller.SetOverride(Override(second));
                Assert.That(Get(controller, "m_contentFrameControllerOverride"), Is.SameAs(firstOverride));
                Assert.That(second.Subscribers, Is.EqualTo(0));
                Assert.That(first.Subscribers, Is.EqualTo(1));

                Call(controller, "OnDisable");
            }
        }

        [Test]
        public void SetNotchEvaluatorNeverCallsTheEvaluator()
        {
            int calls = 0;
            ContentFrameController.SetNotchEvaluator(() => { calls++; return true; });
            ContentFrameController.SetNotchEvaluator(null);
            Assert.That(calls, Is.EqualTo(0));
        }

        // ---- ContentFrameControllerOverride ---------------------------------------

        [Test]
        public void OverrideWithoutAHandlerReportsNothingAndIgnoresRegistrations()
        {
            ContentFrameControllerOverride frameOverride = Override(null, true, true, true, true);
            Assert.That(frameOverride.IsOverrideActive, Is.False);
            Assert.That(frameOverride.IsLandscapeSideMarginOverridden, Is.False);
            Assert.That(frameOverride.IsLandscapeBottomMarginOverridden, Is.False);
            Assert.That(frameOverride.IsPortraitTopMarginOverridden, Is.False);
            Assert.That(frameOverride.IsPortraitBottomMarginOverridden, Is.False);
            Action<bool> listener = force => { };
            Assert.DoesNotThrow(() => frameOverride.RegisterToHandler(listener));
            Assert.DoesNotThrow(() => frameOverride.UnregisterToHandler(listener));
        }

        [Test]
        public void OverrideMarginsRequireTheirOwnFlagAndAnActiveHandler()
        {
            var handler = new Handler { Active = true };
            ContentFrameControllerOverride side = Override(handler, side: true);
            Assert.That(side.IsLandscapeSideMarginOverridden, Is.True);
            Assert.That(side.IsLandscapeBottomMarginOverridden, Is.False);
            Assert.That(side.IsPortraitTopMarginOverridden, Is.False);
            Assert.That(side.IsPortraitBottomMarginOverridden, Is.False);

            Assert.That(Override(handler, landscapeBottom: true).IsLandscapeBottomMarginOverridden, Is.True);
            Assert.That(Override(handler, portraitTop: true).IsPortraitTopMarginOverridden, Is.True);
            Assert.That(Override(handler, portraitBottom: true).IsPortraitBottomMarginOverridden, Is.True);
            Assert.That(Override(handler, portraitBottom: true).IsPortraitTopMarginOverridden, Is.False);

            // The flag is tested first (0x1b3a046): a clear flag never queries the handler.
            ContentFrameControllerOverride none = Override(handler);
            handler.Queries = 0;
            Assert.That(none.IsLandscapeSideMarginOverridden, Is.False);
            Assert.That(none.IsPortraitBottomMarginOverridden, Is.False);
            Assert.That(handler.Queries, Is.EqualTo(0));
            Assert.That(none.IsOverrideActive, Is.True);
            Assert.That(handler.Queries, Is.EqualTo(1));

            handler.Active = false;
            ContentFrameControllerOverride all = Override(handler, true, true, true, true);
            Assert.That(all.IsOverrideActive, Is.False);
            Assert.That(all.IsLandscapeSideMarginOverridden, Is.False);
            Assert.That(all.IsPortraitTopMarginOverridden, Is.False);
        }

        [Test]
        public void OverrideKeepsTheFirstHandlerAndForwardsRegistrations()
        {
            var first = new Handler { Active = false };
            var second = new Handler { Active = true };
            ContentFrameControllerOverride frameOverride = Override(first);
            frameOverride.SetContentFrameControllerOverrideHandler(second);
            Assert.That(frameOverride.IsOverrideActive, Is.False, "The second handler is ignored");
            Assert.That(second.Queries, Is.EqualTo(0));

            int calls = 0;
            Action<bool> listener = force => calls++;
            frameOverride.RegisterToHandler(listener);
            Assert.That(first.Subscribers, Is.EqualTo(1));
            Assert.That(second.Subscribers, Is.EqualTo(0));
            first.Raise(true);
            Assert.That(calls, Is.EqualTo(1));
            frameOverride.UnregisterToHandler(listener);
            Assert.That(first.Subscribers, Is.EqualTo(0));
        }
    }
}
