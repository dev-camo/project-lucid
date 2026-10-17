using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour specification for Hardlight.OrientationAndNotchSpecificRectTransformModifier
    // (HLUnityUI.Runtime, TypeDefIndex 12015, MethodDefs 0x06000539..0x0600053e),
    // written from the shipped x86-64 bodies. Every case owns inactive GameObjects.
    // A case that needs a ScreenManager publishes its own MonoSingleton<ScreenManager>.Instance
    // and clears it again; every case restores the static notch evaluator it found.
    // Lifecycle methods are invoked directly.
    //
    // The base OrientationSpecificRectTransformModifier (0x0600053f..0x0600054d) supplies
    // InitialiseValues (Landscape picks m_landscapeSettings, anything else m_portraitSettings,
    // appended to m_desiredSetting) and SetAllValues (Width writes sizeDelta.x through
    // RectTransformModifier.set_Width 0x1b3a5d0, Height writes sizeDelta.y through
    // set_Height 0x1b3a4d0).
    [TestFixture]
    public sealed class OriginalOrientationAndNotchSpecificRectTransformModifierTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const float Tolerance = 1e-4f;
        const int Height = 0, Width = 1;
        static readonly Type Subject = typeof(OrientationAndNotchSpecificRectTransformModifier);
        static readonly Type Base = typeof(OrientationSpecificRectTransformModifier);
        static readonly Type SettingsType = Base.GetNestedType("OrientationSpecificRectSettings", BindingFlags.NonPublic);
        static readonly Type SettingTypeEnum = SettingsType.GetNestedType("SettingType", BindingFlags.Public | BindingFlags.NonPublic);
        static readonly FieldInfo EvaluatorField = Subject.GetField("IsNotchEnabled", BindingFlags.NonPublic | BindingFlags.Static);

        // ---- reflection helpers -------------------------------------------------

        static FieldInfo Field(string name) => Subject.GetField(name, Any) ?? Base.GetField(name, Any);
        static object Get(object target, string name) => Field(name).GetValue(target);
        static void Set(object target, string name, object value) => Field(name).SetValue(target, value);

        static void Call(object target, string name)
        {
            try { Subject.GetMethod(name, Any | BindingFlags.DeclaredOnly).Invoke(target, null); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static Array Settings(params (int type, float value)[] rows)
        {
            Array array = Array.CreateInstance(SettingsType, rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                object setting = Activator.CreateInstance(SettingsType);
                SettingsType.GetField("Setting").SetValue(setting, Enum.ToObject(SettingTypeEnum, rows[i].type));
                SettingsType.GetField("SettingValue").SetValue(setting, rows[i].value);
                array.SetValue(setting, i);
            }
            return array;
        }

        static IList DesiredList(OrientationSpecificRectTransformModifier modifier) => (IList)Get(modifier, "m_desiredSetting");

        static (int, float)[] Desired(OrientationSpecificRectTransformModifier modifier) =>
            DesiredList(modifier).Cast<object>().Select(entry => (
                Convert.ToInt32(SettingsType.GetField("Setting").GetValue(entry)),
                (float)SettingsType.GetField("SettingValue").GetValue(entry))).ToArray();

        // Five arrays with distinct values, so each entry names the array it came from.
        static readonly (int, float) LandscapeEntry = (Width, 1f);
        static readonly (int, float) PortraitEntry = (Width, 2f);
        static readonly (int, float) PortraitNotchEntry = (Height, 3f);
        static readonly (int, float) LandscapeLeftNotchEntry = (Height, 4f);
        static readonly (int, float) LandscapeRightNotchEntry = (Height, 5f);

        static void ConfigureAll(OrientationAndNotchSpecificRectTransformModifier modifier)
        {
            Set(modifier, "m_landscapeSettings", Settings(LandscapeEntry));
            Set(modifier, "m_portraitSettings", Settings(PortraitEntry));
            Set(modifier, "m_portraitNotchSettings", Settings(PortraitNotchEntry));
            Set(modifier, "m_landscapeLeftNotchSettings", Settings(LandscapeLeftNotchEntry));
            Set(modifier, "m_landscapeRightNotchSettings", Settings(LandscapeRightNotchEntry));
        }

        // ---- owned state --------------------------------------------------------

        sealed class Hosts : IDisposable
        {
            readonly List<GameObject> owned = new List<GameObject>();

            public OrientationAndNotchSpecificRectTransformModifier Modifier()
            {
                var owner = new GameObject("OrientationAndNotchSpecificRectTransformModifier fixture", typeof(RectTransform))
                    { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                owned.Add(owner);
                var modifier = owner.AddComponent<OrientationAndNotchSpecificRectTransformModifier>();
                Assert.That(owner.GetComponent<RectTransformModifier>(), Is.Not.Null, "RequireComponent prerequisite");
                modifier.Awake();
                ((RectTransform)owner.transform).sizeDelta = new Vector2(50f, 60f);
                return modifier;
            }

            public void Dispose()
            {
                foreach (GameObject owner in owned)
                    if (owner != null) Object.DestroyImmediate(owner);
            }
        }

        // Restores whatever evaluator the static field held before the case.
        sealed class NotchEvaluator : IDisposable
        {
            readonly object previous = EvaluatorField.GetValue(null);

            public NotchEvaluator(Func<bool> evaluator) => OrientationAndNotchSpecificRectTransformModifier.SetNotchEvaluator(evaluator);

            public void Dispose() => EvaluatorField.SetValue(null, previous);
        }

        sealed class ScreenFixture : IDisposable
        {
            static readonly MethodInfo SetInstance =
                typeof(MonoSingleton<ScreenManager>).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetSetMethod(true);
            static readonly MethodInfo SetOrientation =
                typeof(ScreenManager).GetProperty("Orientation").GetSetMethod(true);
            static readonly MethodInfo SetDeviceOrientation =
                typeof(ScreenManager).GetProperty("DeviceOrientation").GetSetMethod(true);

            readonly GameObject owner;
            public readonly ScreenManager Manager;

            public ScreenFixture(UIOrientation orientation, ScreenOrientation deviceOrientation)
            {
                Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "Fixture requires no foreign ScreenManager singleton");
                owner = new GameObject("Owned ScreenManager") { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                try
                {
                    Manager = owner.AddComponent<ScreenManager>();
                    SetOrientation.Invoke(Manager, new object[] { orientation });
                    SetDeviceOrientation.Invoke(Manager, new object[] { deviceOrientation });
                    SetInstance.Invoke(null, new object[] { Manager });
                }
                catch { Dispose(); throw; }
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

        // ---- declaration --------------------------------------------------------

        // dump.cs TypeDefIndex 12015: a non-sealed subclass of the orientation modifier
        // with one private static Func<bool> and three serialized arrays of the base's
        // protected settings type, in this order.
        [Test]
        public void DeclarationKeepsTheOriginalBaseFieldsAndAttributes()
        {
            Assert.That(Subject.BaseType, Is.EqualTo(Base));
            Assert.That(Subject.IsSealed, Is.False);
            var require = (RequireComponent)Subject.GetCustomAttributes(typeof(RequireComponent), false).Single();
            Assert.That(require.m_Type0, Is.EqualTo(typeof(RectTransformModifier)));

            Assert.That(EvaluatorField, Is.Not.Null);
            Assert.That(EvaluatorField.IsPrivate, Is.True);
            Assert.That(EvaluatorField.FieldType, Is.EqualTo(typeof(Func<bool>)));

            FieldInfo[] fields = Subject.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[] {
                "m_portraitNotchSettings", "m_landscapeLeftNotchSettings", "m_landscapeRightNotchSettings" }));
            foreach (FieldInfo field in fields)
            {
                Assert.That(field.FieldType, Is.EqualTo(SettingsType.MakeArrayType()), field.Name);
                Assert.That(field.IsPrivate, Is.True, field.Name);
                Assert.That(field.IsDefined(typeof(SerializeField), false), Is.True, field.Name);
            }

            MethodInfo setter = Subject.GetMethod("SetNotchEvaluator", BindingFlags.Public | BindingFlags.Static);
            Assert.That(setter.GetParameters().Single().Name, Is.EqualTo("notchEvaluator"));
            Assert.That(Subject.GetMethod("OnEnable", Any | BindingFlags.DeclaredOnly).IsPrivate, Is.True);
            Assert.That(Subject.GetMethod("OnDisable", Any | BindingFlags.DeclaredOnly).IsPrivate, Is.True);
        }

        // ---- SetNotchEvaluator 0x0600053b (0x1b644b0) ---------------------------

        // The body is a single static store (0x1b644e6), so null replaces a previous evaluator.
        [Test]
        public void SetNotchEvaluatorReplacesTheStaticEvaluator()
        {
            Func<bool> first = () => true;
            using (new NotchEvaluator(first))
            {
                Assert.That(EvaluatorField.GetValue(null), Is.SameAs(first));
                OrientationAndNotchSpecificRectTransformModifier.SetNotchEvaluator(null);
                Assert.That(EvaluatorField.GetValue(null), Is.Null);
            }
        }

        // ---- InitialiseValues 0x0600053c (0x1b64510) ----------------------------

        // A null evaluator (0x1b6456d) or one returning false (0x1b64581) returns after
        // the base call, so only the orientation array is appended.
        [Test]
        public void WithoutANotchOnlyTheOrientationSettingsAreAppended()
        {
            using (var hosts = new Hosts())
            using (new ScreenFixture(UIOrientation.Landscape, ScreenOrientation.LandscapeLeft))
            {
                using (new NotchEvaluator(null))
                {
                    var modifier = hosts.Modifier();
                    ConfigureAll(modifier);
                    Call(modifier, "InitialiseValues");
                    Assert.That(Desired(modifier), Is.EqualTo(new[] { LandscapeEntry }), "null evaluator");
                }

                int calls = 0;
                using (new NotchEvaluator(() => { calls++; return false; }))
                {
                    var modifier = hosts.Modifier();
                    ConfigureAll(modifier);
                    Call(modifier, "InitialiseValues");
                    Assert.That(Desired(modifier), Is.EqualTo(new[] { LandscapeEntry }), "evaluator reports no notch");
                    Assert.That(calls, Is.EqualTo(1));
                }
            }
        }

        // With a notch, Orientation (+0x50) and DeviceOrientation (+0x54) are read and
        // compared at 0x1b6461d..0x1b6464a: Landscape (2) with LandscapeLeft (3) appends
        // m_landscapeLeftNotchSettings (+0x50), Landscape with LandscapeRight (4) appends
        // m_landscapeRightNotchSettings (+0x58), and every other pair, including a
        // Landscape UI on a portrait device and a Portrait UI on a landscape device,
        // appends m_portraitNotchSettings (+0x48). The base array comes first.
        [Test]
        public void NotchArrayFollowsTheUIAndDeviceOrientationPair()
        {
            var cases = new (UIOrientation ui, ScreenOrientation device, (int, float) baseEntry, (int, float) notchEntry)[]
            {
                (UIOrientation.Landscape, ScreenOrientation.LandscapeLeft, LandscapeEntry, LandscapeLeftNotchEntry),
                (UIOrientation.Landscape, ScreenOrientation.LandscapeRight, LandscapeEntry, LandscapeRightNotchEntry),
                (UIOrientation.Landscape, ScreenOrientation.Portrait, LandscapeEntry, PortraitNotchEntry),
                (UIOrientation.Landscape, ScreenOrientation.PortraitUpsideDown, LandscapeEntry, PortraitNotchEntry),
                (UIOrientation.Landscape, ScreenOrientation.AutoRotation, LandscapeEntry, PortraitNotchEntry),
                (UIOrientation.Portrait, ScreenOrientation.LandscapeLeft, PortraitEntry, PortraitNotchEntry),
                (UIOrientation.Portrait, ScreenOrientation.LandscapeRight, PortraitEntry, PortraitNotchEntry),
                (UIOrientation.Uninitialised, ScreenOrientation.LandscapeRight, PortraitEntry, PortraitNotchEntry),
            };
            using (new NotchEvaluator(() => true))
            {
                foreach (var c in cases)
                {
                    using (var hosts = new Hosts())
                    using (new ScreenFixture(c.ui, c.device))
                    {
                        var modifier = hosts.Modifier();
                        ConfigureAll(modifier);
                        Call(modifier, "InitialiseValues");
                        Assert.That(Desired(modifier), Is.EqualTo(new[] { c.baseEntry, c.notchEntry }), c.ui + " UI on a " + c.device + " device");
                    }
                }
            }
        }

        // The base call at 0x1b64554 runs before the evaluator is read, so the evaluator
        // already sees the orientation entry in m_desiredSetting.
        [Test]
        public void TheEvaluatorRunsAfterTheOrientationSettingsAreAppended()
        {
            using (var hosts = new Hosts())
            using (new ScreenFixture(UIOrientation.Portrait, ScreenOrientation.Portrait))
            {
                var modifier = hosts.Modifier();
                ConfigureAll(modifier);
                var seen = new List<int>();
                using (new NotchEvaluator(() => { seen.Add(DesiredList(modifier).Count); return true; }))
                {
                    Call(modifier, "InitialiseValues");
                }
                Assert.That(seen, Is.EqualTo(new[] { 1 }));
                Assert.That(Desired(modifier), Is.EqualTo(new[] { PortraitEntry, PortraitNotchEntry }));
            }
        }

        // Orientation (+0x50, 0x1b645ce) and DeviceOrientation (+0x54, 0x1b64610) are read
        // only after the evaluator returns true (0x1b6457e), while the base call read the
        // orientation before it: an evaluator that rotates the screen splits the two picks.
        [Test]
        public void NotchOrientationIsReadAfterTheEvaluatorReturns()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(UIOrientation.Portrait, ScreenOrientation.Portrait))
            {
                var modifier = hosts.Modifier();
                ConfigureAll(modifier);
                MethodInfo setOrientation = typeof(ScreenManager).GetProperty("Orientation").GetSetMethod(true);
                MethodInfo setDeviceOrientation = typeof(ScreenManager).GetProperty("DeviceOrientation").GetSetMethod(true);
                using (new NotchEvaluator(() =>
                {
                    setOrientation.Invoke(screen.Manager, new object[] { UIOrientation.Landscape });
                    setDeviceOrientation.Invoke(screen.Manager, new object[] { ScreenOrientation.LandscapeLeft });
                    return true;
                }))
                {
                    Call(modifier, "InitialiseValues");
                }
                Assert.That(Desired(modifier), Is.EqualTo(new[] { PortraitEntry, LandscapeLeftNotchEntry }));
            }
        }

        // The selected array goes straight to List.AddRange (0x1b64663) with no guard:
        // an empty array adds nothing and a null one throws.
        [Test]
        public void AnEmptyNotchArrayAddsNothingAndANullOneThrows()
        {
            using (var hosts = new Hosts())
            using (new ScreenFixture(UIOrientation.Landscape, ScreenOrientation.LandscapeRight))
            using (new NotchEvaluator(() => true))
            {
                var modifier = hosts.Modifier();
                ConfigureAll(modifier);
                Set(modifier, "m_landscapeRightNotchSettings", Settings());
                Call(modifier, "InitialiseValues");
                Assert.That(Desired(modifier), Is.EqualTo(new[] { LandscapeEntry }));

                Set(modifier, "m_landscapeRightNotchSettings", null);
                Assert.Throws<ArgumentNullException>(() => Call(modifier, "InitialiseValues"));
            }
        }

        // ---- InitialiseAndSetValues 0x0600053d (0x1b64720) ----------------------

        // Slot-5 InitialiseValues then SetAllValues 0x06000545 (0x1b64742). The notch
        // entry follows the orientation entry, so with both writing Width the notch value
        // is the one left on the RectTransform; the list is never cleared, so a second
        // refresh appends both entries again.
        [Test]
        public void RefreshAppliesTheNotchEntryAfterTheOrientationEntry()
        {
            using (var hosts = new Hosts())
            using (new ScreenFixture(UIOrientation.Landscape, ScreenOrientation.LandscapeLeft))
            using (new NotchEvaluator(() => true))
            {
                var modifier = hosts.Modifier();
                ConfigureAll(modifier);
                Set(modifier, "m_landscapeSettings", Settings((Width, 70f)));
                Set(modifier, "m_landscapeLeftNotchSettings", Settings((Width, 90f)));
                Call(modifier, "InitialiseAndSetValues");
                var rect = (RectTransform)modifier.transform;
                Assert.That(rect.sizeDelta.x, Is.EqualTo(90f).Within(Tolerance));
                Assert.That(rect.sizeDelta.y, Is.EqualTo(60f).Within(Tolerance), "Height untouched");

                Call(modifier, "InitialiseAndSetValues");
                Assert.That(Desired(modifier), Is.EqualTo(new[] { (Width, 70f), (Width, 90f), (Width, 70f), (Width, 90f) }));
            }
        }

        // ---- OnEnable 0x06000539 (0x1b64260) ------------------------------------

        // When MonoSingleton<ScreenManager>.IsNull() (0x1b642b8) is false, OnEnable stores
        // OnOrientationChange (+0x58) + a slot-4 InitialiseAndSetValues delegate through
        // op_Addition (0x1b64350), then calls slot 4 directly (0x1b6437f). Unlike the base
        // OnEnable no coroutine is started: the inactive host would log
        // "Coroutine couldn't be started" if StartCoroutine ran.
        [Test]
        public void OnEnableSubscribesAndRefreshesImmediatelyWithoutACoroutine()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(UIOrientation.Landscape, ScreenOrientation.LandscapeRight))
            using (new NotchEvaluator(() => true))
            {
                var modifier = hosts.Modifier();
                ConfigureAll(modifier);
                Call(modifier, "OnEnable");
                Assert.That(Desired(modifier), Is.EqualTo(new[] { LandscapeEntry, LandscapeRightNotchEntry }), "refreshed within OnEnable");
                Vector2 size = ((RectTransform)modifier.transform).sizeDelta;
                Assert.That(size.x, Is.EqualTo(1f).Within(Tolerance), "landscape Width");
                Assert.That(size.y, Is.EqualTo(5f).Within(Tolerance), "landscape-right notch Height");

                List<Action> listeners = screen.Manager.OnOrientationChange.GetInvocationList();
                Assert.That(listeners.Count, Is.EqualTo(1));
                Assert.That(listeners[0].Target, Is.SameAs(modifier));
                Assert.That(listeners[0].Method.Name, Is.EqualTo("InitialiseAndSetValues"));

                FastAction.Invoke(screen.Manager.OnOrientationChange);
                Assert.That(DesiredList(modifier).Count, Is.EqualTo(4), "the subscription refreshes again");

                Call(modifier, "OnEnable");
                Assert.That(screen.Manager.OnOrientationChange.GetInvocationListCount(), Is.EqualTo(2), "plain +=, not AddUnique");
                LogAssert.NoUnexpectedReceived();
            }
        }

        // The refresh is outside the IsNull guard, so with no singleton the base
        // InitialiseValues reads Orientation from a null Instance and throws.
        [Test]
        public void OnEnableWithoutAScreenManagerThrowsFromTheRefresh()
        {
            Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "requires no foreign ScreenManager singleton");
            using (var hosts = new Hosts())
            using (new NotchEvaluator(null))
            {
                var modifier = hosts.Modifier();
                ConfigureAll(modifier);
                Assert.Throws<NullReferenceException>(() => Call(modifier, "OnEnable"));
                Assert.That(DesiredList(modifier).Count, Is.EqualTo(0));
            }
        }

        // IsNull uses Unity equality, so a destroyed singleton is not subscribed to, but
        // the refresh still reads the destroyed manager's managed orientation values.
        [Test]
        public void OnEnableWithADestroyedScreenManagerRefreshesWithoutSubscribing()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(UIOrientation.Portrait, ScreenOrientation.Portrait))
            using (new NotchEvaluator(() => true))
            {
                var modifier = hosts.Modifier();
                ConfigureAll(modifier);
                screen.DestroyHost();
                Call(modifier, "OnEnable");
                Assert.That(screen.Manager.OnOrientationChange, Is.Null);
                Assert.That(Desired(modifier), Is.EqualTo(new[] { PortraitEntry, PortraitNotchEntry }));
            }
        }

        // ---- OnDisable 0x0600053a (0x1b64390) -----------------------------------

        // Same IsNull guard (0x1b643e8); the stored op_Subtraction result (0x1b64485)
        // removes this component's listener and keeps a foreign one.
        [Test]
        public void OnDisableRemovesOnlyThisComponentsListener()
        {
            using (var hosts = new Hosts())
            using (var screen = new ScreenFixture(UIOrientation.Portrait, ScreenOrientation.Portrait))
            using (new NotchEvaluator(null))
            {
                Action foreign = () => { };
                screen.Manager.OnOrientationChange += foreign;
                var modifier = hosts.Modifier();
                ConfigureAll(modifier);
                Call(modifier, "OnEnable");
                Assert.That(screen.Manager.OnOrientationChange.GetInvocationListCount(), Is.EqualTo(2));
                Call(modifier, "OnDisable");
                Assert.That(screen.Manager.OnOrientationChange.GetInvocationList(), Is.EqualTo(new[] { foreign }));
            }
        }

        [Test]
        public void OnDisableWithoutALiveScreenManagerDoesNothing()
        {
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Modifier();
                Assert.DoesNotThrow(() => Call(modifier, "OnDisable"), "no singleton");
                using (var screen = new ScreenFixture(UIOrientation.Portrait, ScreenOrientation.Portrait))
                {
                    var listeners = new FastAction();
                    Action foreign = () => { };
                    listeners += foreign;
                    screen.Manager.OnOrientationChange = listeners;
                    screen.DestroyHost();
                    Call(modifier, "OnDisable");
                    Assert.That(screen.Manager.OnOrientationChange, Is.SameAs(listeners), "destroyed singleton untouched");
                    Assert.That(listeners.GetInvocationList(), Is.EqualTo(new[] { foreign }));
                }
            }
        }
    }
}
