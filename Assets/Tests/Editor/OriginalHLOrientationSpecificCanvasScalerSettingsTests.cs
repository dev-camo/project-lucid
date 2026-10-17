using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour specification for Hardlight.HLOrientationSpecificCanvasScalerSettings
    // (HLUnityUI.Runtime, TypeDefIndex 12014, MethodDefs 0x06000534..0x06000537) and its
    // nested OrientationSpecificCanvasScalerScreenMatchMode (TypeDefIndex 12013, 0x06000538),
    // written from the shipped x86-64 bodies. Every case owns inactive GameObjects; a case
    // that needs a ScreenManager publishes its own MonoSingleton<ScreenManager>.Instance and
    // clears it again. Lifecycle methods are invoked directly.
    //
    // Evaluate writes CanvasScaler m_ScreenMatchMode (+0x34) and m_MatchWidthOrHeight (+0x38)
    // directly (the trivial setters are inlined) and swaps through set_referenceResolution.
    [TestFixture]
    public sealed class OriginalHLOrientationSpecificCanvasScalerSettingsTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const float Tolerance = 1e-4f;
        static readonly Type Subject = typeof(HLOrientationSpecificCanvasScalerSettings);
        static readonly Type MatchMode = Subject.GetNestedType("OrientationSpecificCanvasScalerScreenMatchMode", BindingFlags.NonPublic);

        static void Set(object target, string name, object value) => Subject.GetField(name, Any).SetValue(target, value);

        static void Call(object target, string name)
        {
            try { Subject.GetMethod(name, Any).Invoke(target, null); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static object Settings(CanvasScaler.ScreenMatchMode mode, float match, bool flip)
        {
            object settings = Activator.CreateInstance(MatchMode);
            MatchMode.GetField("ScreenMatchModeToUse").SetValue(settings, mode);
            MatchMode.GetField("MatchWidthOrHeight").SetValue(settings, match);
            MatchMode.GetField("FlipReferenceResolution").SetValue(settings, flip);
            return settings;
        }

        sealed class Hosts : IDisposable
        {
            readonly List<GameObject> owned = new List<GameObject>();

            public GameObject Inactive(string name)
            {
                var owner = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                owned.Add(owner);
                return owner;
            }

            // Baseline scaler: MatchWidthOrHeight mode, match 0.25, reference 800 x 600.
            public CanvasScaler Scaler()
            {
                var scaler = Inactive("CanvasScaler fixture").AddComponent<CanvasScaler>();
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.25f;
                scaler.referenceResolution = new Vector2(800f, 600f);
                return scaler;
            }

            // Landscape: Shrink 0.75 without flip; portrait: Expand 0.5 with flip.
            public HLOrientationSpecificCanvasScalerSettings Component(CanvasScaler scaler)
            {
                var subject = Inactive("HLOrientationSpecificCanvasScalerSettings fixture").AddComponent<HLOrientationSpecificCanvasScalerSettings>();
                Set(subject, "m_canvasScaler", scaler);
                Set(subject, "m_landscapeSettings", Settings(CanvasScaler.ScreenMatchMode.Shrink, 0.75f, false));
                Set(subject, "m_portraitSettings", Settings(CanvasScaler.ScreenMatchMode.Expand, 0.5f, true));
                return subject;
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
            static readonly MethodInfo SetOrientation =
                typeof(ScreenManager).GetProperty("Orientation").GetSetMethod(true);

            readonly GameObject owner;
            public readonly ScreenManager Manager;

            public ScreenFixture(UIOrientation orientation)
            {
                Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "Fixture requires no foreign ScreenManager singleton");
                owner = new GameObject("Owned ScreenManager") { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                try
                {
                    Manager = owner.AddComponent<ScreenManager>();
                    Orientation = orientation;
                    SetInstance.Invoke(null, new object[] { Manager });
                }
                catch { Dispose(); throw; }
            }

            public UIOrientation Orientation
            {
                set => SetOrientation.Invoke(Manager, new object[] { value });
            }

            public void DestroyHost() => Object.DestroyImmediate(owner);

            public void Dispose()
            {
                try
                {
                    if (!ReferenceEquals(ScreenManager.Instance, null) && !ReferenceEquals(ScreenManager.Instance, Manager))
                        throw new InvalidOperationException("Fixture singleton was replaced");
                    SetInstance.Invoke(null, new object[] { null });
                }
                finally
                {
                    if (owner != null) Object.DestroyImmediate(owner);
                }
            }
        }

        static void AssertScaler(CanvasScaler scaler, CanvasScaler.ScreenMatchMode mode, float match, Vector2 resolution, string label)
        {
            Assert.That(scaler.screenMatchMode, Is.EqualTo(mode), label + " mode");
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(match).Within(Tolerance), label + " match");
            Assert.That(scaler.referenceResolution.x, Is.EqualTo(resolution.x).Within(Tolerance), label + " reference x");
            Assert.That(scaler.referenceResolution.y, Is.EqualTo(resolution.y).Within(Tolerance), label + " reference y");
        }

        // ---- declaration and .ctor 0x06000538 (0x1b64240) -----------------------

        // dump.cs TypeDefIndex 12013/12014: a private [Serializable] nested class with three
        // public fields, the match value ranged 0..1, held by three serialized fields.
        [Test]
        public void DeclarationKeepsTheOriginalFieldsAndNestedSettingsClass()
        {
            Assert.That(MatchMode, Is.Not.Null);
            Assert.That(MatchMode.IsNestedPrivate, Is.True);
            Assert.That(MatchMode.IsDefined(typeof(SerializableAttribute), false), Is.True);
            FieldInfo[] nested = MatchMode.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(nested.Select(f => f.Name), Is.EqualTo(new[] { "ScreenMatchModeToUse", "MatchWidthOrHeight", "FlipReferenceResolution" }));
            Assert.That(nested.Select(f => f.FieldType), Is.EqualTo(new[] { typeof(CanvasScaler.ScreenMatchMode), typeof(float), typeof(bool) }));
            Assert.That(nested.All(f => f.IsPublic), Is.True);
            var range = (UnityEngine.RangeAttribute)nested[1].GetCustomAttributes(typeof(UnityEngine.RangeAttribute), false).Single();
            Assert.That(range.min, Is.EqualTo(0f));
            Assert.That(range.max, Is.EqualTo(1f));

            FieldInfo[] fields = Subject.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[] { "m_canvasScaler", "m_landscapeSettings", "m_portraitSettings" }));
            Assert.That(fields.Select(f => f.FieldType), Is.EqualTo(new[] { typeof(CanvasScaler), MatchMode, MatchMode }));
            Assert.That(fields.All(f => f.IsPrivate && f.IsDefined(typeof(SerializeField), false)), Is.True);
        }

        // The constructor stores the packed qword 0x3f800000_00000001 at +0x10: mode
        // Expand (1), match 1.0, and leaves FlipReferenceResolution false.
        [Test]
        public void SettingsConstructorDefaultsToExpandWithAFullMatch()
        {
            object settings = Activator.CreateInstance(MatchMode);
            Assert.That(MatchMode.GetField("ScreenMatchModeToUse").GetValue(settings), Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            Assert.That(MatchMode.GetField("MatchWidthOrHeight").GetValue(settings), Is.EqualTo(1f));
            Assert.That(MatchMode.GetField("FlipReferenceResolution").GetValue(settings), Is.EqualTo(false));
        }

        // ---- Evaluate 0x06000536 (0x1b63f90) ------------------------------------

        // The Unity-null scaler check (0x1b63fe5) comes before ScreenManager.Instance is
        // read, so a missing or destroyed scaler returns even without a singleton.
        [Test]
        public void EvaluateWithoutAScalerReturnsBeforeReadingTheScreenManager()
        {
            Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "requires no foreign ScreenManager singleton");
            using (var hosts = new Hosts())
            {
                var unassigned = hosts.Component(null);
                Assert.DoesNotThrow(() => Call(unassigned, "Evaluate"), "no scaler");

                CanvasScaler destroyed = hosts.Scaler();
                var stale = hosts.Component(destroyed);
                Object.DestroyImmediate(destroyed);
                Assert.DoesNotThrow(() => Call(stale, "Evaluate"), "destroyed scaler");
            }
        }

        [Test]
        public void EvaluateWithAScalerButNoScreenManagerThrows()
        {
            Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "requires no foreign ScreenManager singleton");
            using (var hosts = new Hosts())
            {
                CanvasScaler scaler = hosts.Scaler();
                var subject = hosts.Component(scaler);
                Assert.Throws<NullReferenceException>(() => Call(subject, "Evaluate"));
                AssertScaler(scaler, CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0.25f, new Vector2(800f, 600f), "unchanged");
            }
        }

        // Orientation (+0x50) != Landscape selects m_portraitSettings (setne at 0x1b64067),
        // so Uninitialised behaves like Portrait.
        [Test]
        public void OnlyLandscapeUsesTheLandscapeSettings()
        {
            using (var hosts = new Hosts())
            {
                foreach (UIOrientation orientation in new[] { UIOrientation.Landscape, UIOrientation.Portrait, UIOrientation.Uninitialised })
                {
                    using (new ScreenFixture(orientation))
                    {
                        CanvasScaler scaler = hosts.Scaler();
                        var subject = hosts.Component(scaler);
                        Call(subject, "Evaluate");
                        if (orientation == UIOrientation.Landscape)
                            AssertScaler(scaler, CanvasScaler.ScreenMatchMode.Shrink, 0.75f, new Vector2(800f, 600f), orientation.ToString());
                        else
                            AssertScaler(scaler, CanvasScaler.ScreenMatchMode.Expand, 0.5f, new Vector2(600f, 800f), orientation.ToString());
                    }
                }
            }
        }

        // The flip reads the scaler's current reference resolution (+0x2c) and stores it
        // with x and y exchanged (shufps 0xe1, 0x1b64098), so it toggles on every flipped
        // evaluation rather than settling on a portrait shape.
        [Test]
        public void EachFlippedEvaluationSwapsTheCurrentReferenceResolutionAgain()
        {
            using (var hosts = new Hosts())
            using (new ScreenFixture(UIOrientation.Portrait))
            {
                CanvasScaler scaler = hosts.Scaler();
                var subject = hosts.Component(scaler);
                Call(subject, "Evaluate");
                AssertScaler(scaler, CanvasScaler.ScreenMatchMode.Expand, 0.5f, new Vector2(600f, 800f), "first");
                Call(subject, "Evaluate");
                AssertScaler(scaler, CanvasScaler.ScreenMatchMode.Expand, 0.5f, new Vector2(800f, 600f), "second");
            }
        }

        // ---- OnEnable 0x06000534 (0x1b63e20) / OnDisable 0x06000535 (0x1b640c0) --

        // IsNull (0x1b63e87) returns before anything else, so with no singleton or a
        // destroyed one the scaler is not evaluated and nothing is subscribed.
        [Test]
        public void OnEnableWithoutALiveScreenManagerDoesNothing()
        {
            Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "requires no foreign ScreenManager singleton");
            using (var hosts = new Hosts())
            {
                CanvasScaler scaler = hosts.Scaler();
                var subject = hosts.Component(scaler);
                Assert.DoesNotThrow(() => Call(subject, "OnEnable"));
                AssertScaler(scaler, CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0.25f, new Vector2(800f, 600f), "no singleton");

                using (var screen = new ScreenFixture(UIOrientation.Landscape))
                {
                    screen.DestroyHost();
                    Call(subject, "OnEnable");
                    Assert.That(screen.Manager.OnOrientationChange, Is.Null);
                    AssertScaler(scaler, CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0.25f, new Vector2(800f, 600f), "destroyed singleton");
                }
            }
        }

        // With a live singleton, OnOrientationChange (+0x58) gets Evaluate through
        // op_Addition (0x1b63f26) and Evaluate runs at once (0x1b63f4b); the listener
        // re-evaluates against the orientation current when the FastAction fires.
        [Test]
        public void OnEnableSubscribesEvaluateAndEvaluatesImmediately()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(UIOrientation.Landscape))
            {
                CanvasScaler scaler = hosts.Scaler();
                var subject = hosts.Component(scaler);
                Call(subject, "OnEnable");
                AssertScaler(scaler, CanvasScaler.ScreenMatchMode.Shrink, 0.75f, new Vector2(800f, 600f), "enabled in landscape");

                List<Action> listeners = screen.Manager.OnOrientationChange.GetInvocationList();
                Assert.That(listeners.Count, Is.EqualTo(1));
                Assert.That(listeners[0].Target, Is.SameAs(subject));
                Assert.That(listeners[0].Method.Name, Is.EqualTo("Evaluate"));

                screen.Orientation = UIOrientation.Portrait;
                FastAction.Invoke(screen.Manager.OnOrientationChange);
                AssertScaler(scaler, CanvasScaler.ScreenMatchMode.Expand, 0.5f, new Vector2(600f, 800f), "after rotating to portrait");
            }
        }

        // OnDisable guards with NotNull (0x1b64127) and stores the op_Subtraction result
        // (0x1b641bb), keeping foreign listeners.
        [Test]
        public void OnDisableRemovesOnlyThisComponentsListener()
        {
            using (var hosts = new Hosts())
            {
                var subject = hosts.Component(hosts.Scaler());
                Assert.DoesNotThrow(() => Call(subject, "OnDisable"), "no singleton");
                using (var screen = new ScreenFixture(UIOrientation.Landscape))
                {
                    Action foreign = () => { };
                    screen.Manager.OnOrientationChange += foreign;
                    Call(subject, "OnEnable");
                    Assert.That(screen.Manager.OnOrientationChange.GetInvocationListCount(), Is.EqualTo(2));
                    Call(subject, "OnDisable");
                    Assert.That(screen.Manager.OnOrientationChange.GetInvocationList(), Is.EqualTo(new[] { foreign }));
                }
            }
        }
    }
}
