using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour specification for Hardlight.OrientationSpecificRectTransformModifier
    // (HLUnityUI.Runtime, TypeDefIndex 12019, MethodDefs 0x0600053f..0x0600054d),
    // written from the shipped x86-64 bodies. Every case owns inactive GameObjects,
    // and any ScreenManager it needs owns both the "Hardlight.ScreenManager"
    // ProcessManager row and the MonoSingleton<ScreenManager>.Instance value,
    // restoring both before returning. Lifecycle methods are invoked directly;
    // the inactive host makes StartCoroutine log instead of scheduling anything.
    //
    // RectTransformModifier (HLUnityUI.Runtime 0x0600002b..0x06000062) is the
    // collaborator every setting is routed to. Its setter mappings, cited where
    // used, decide the RectTransform state these cases compare.
    [TestFixture]
    public sealed class OriginalOrientationSpecificRectTransformModifierTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const float Tolerance = 1e-4f;
        static readonly Type Subject = typeof(OrientationSpecificRectTransformModifier);
        static readonly Type SettingsType = Subject.GetNestedType("OrientationSpecificRectSettings", BindingFlags.NonPublic);
        static readonly Type SettingTypeEnum = SettingsType.GetNestedType("SettingType", BindingFlags.Public | BindingFlags.NonPublic);
        static readonly Regex InactiveCoroutine = new Regex("Coroutine couldn't be started");

        const int Height = 0, Width = 1, PosX = 2, PosY = 3, XAnchorMin = 4, XAnchorMax = 5, YAnchorMin = 6,
            YAnchorMax = 7, LeftOffset = 8, RightOffset = 9, BotOffset = 10, TopOffset = 11, PivotX = 12, PivotY = 13;

        // ---- reflection helpers -------------------------------------------------

        static object Get(object target, string name) => Subject.GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => Subject.GetField(name, Any).SetValue(target, value);

        static object Call(object target, string name)
        {
            try { return Subject.GetMethod(name, Any).Invoke(target, null); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static object Setting(int type, float value)
        {
            object setting = Activator.CreateInstance(SettingsType);
            SettingsType.GetField("Setting").SetValue(setting, Enum.ToObject(SettingTypeEnum, type));
            SettingsType.GetField("SettingValue").SetValue(setting, value);
            return setting;
        }

        static Array Settings(params (int type, float value)[] rows)
        {
            Array array = Array.CreateInstance(SettingsType, rows.Length);
            for (int i = 0; i < rows.Length; i++) array.SetValue(Setting(rows[i].type, rows[i].value), i);
            return array;
        }

        static IList DesiredList(OrientationSpecificRectTransformModifier modifier) => (IList)Get(modifier, "m_desiredSetting");

        static void Desire(OrientationSpecificRectTransformModifier modifier, params (int type, float value)[] rows)
        {
            IList list = DesiredList(modifier);
            foreach ((int type, float value) row in rows) list.Add(Setting(row.type, row.value));
        }

        static (int, float)[] Desired(OrientationSpecificRectTransformModifier modifier) =>
            DesiredList(modifier).Cast<object>().Select(entry => (
                Convert.ToInt32(SettingsType.GetField("Setting").GetValue(entry)),
                (float)SettingsType.GetField("SettingValue").GetValue(entry))).ToArray();

        static void Configure(OrientationSpecificRectTransformModifier modifier, (int, float)[] landscape, (int, float)[] portrait)
        {
            Set(modifier, "m_landscapeSettings", Settings(landscape));
            Set(modifier, "m_portraitSettings", Settings(portrait));
        }

        // ---- owned hosts --------------------------------------------------------

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

            // [RequireComponent(typeof(RectTransformModifier))] brings the sibling modifier.
            public T Modifier<T>(string name = "OrientationSpecificRectTransformModifier fixture") where T : OrientationSpecificRectTransformModifier
            {
                GameObject owner = Inactive(name);
                T modifier = owner.AddComponent<T>();
                Assert.That(owner.GetComponent<RectTransformModifier>(), Is.Not.Null, "RequireComponent prerequisite");
                Assert.That((Object)Get(modifier, "m_rectTransformModifier") == null, Is.True, "Inactive AddComponent runs no Awake");
                return modifier;
            }

            public T Awake<T>(string name = "OrientationSpecificRectTransformModifier fixture") where T : OrientationSpecificRectTransformModifier
            {
                T modifier = Modifier<T>(name);
                modifier.Awake();
                return modifier;
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
            static readonly MethodInfo SetOrientationMethod =
                typeof(ScreenManager).GetProperty("Orientation", BindingFlags.Public | BindingFlags.Instance).GetSetMethod(true);

            readonly VerificationFixtureOwnership.RegistryRow row;
            readonly GameObject owner;
            public readonly ScreenManager Manager;
            bool published;

            public ScreenFixture(UIOrientation orientation, bool register = true, bool publish = true)
            {
                Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "Fixture requires no foreign ScreenManager singleton");
                row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<ScreenManager>(), ScreenManager.Instance);
                owner = new GameObject("Owned ScreenManager") { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                try
                {
                    Manager = owner.AddComponent<ScreenManager>();
                    Orientation = orientation;
                    if (register) Register();
                    if (publish) Publish();
                }
                catch { Dispose(); throw; }
            }

            public UIOrientation Orientation
            {
                get => Manager.Orientation;
                set => SetOrientationMethod.Invoke(Manager, new object[] { value });
            }

            public void Register() => row.Register(Manager);

            public void Publish()
            {
                SetInstance.Invoke(null, new object[] { Manager });
                published = true;
            }

            public void DestroyHost() => Object.DestroyImmediate(owner);

            public void Dispose()
            {
                try
                {
                    if (published)
                    {
                        if (!ReferenceEquals(ScreenManager.Instance, Manager))
                            throw new InvalidOperationException("Fixture singleton was replaced");
                        SetInstance.Invoke(null, new object[] { null });
                    }
                }
                finally
                {
                    try { row.Dispose(); }
                    finally { if (owner != null) Object.DestroyImmediate(owner); }
                }
            }
        }

        // ---- RectTransform state ------------------------------------------------

        static void Baseline(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.1f, 0.2f);
            rect.anchorMax = new Vector2(0.6f, 0.7f);
            rect.pivot = new Vector2(0.3f, 0.4f);
            rect.sizeDelta = new Vector2(50f, 60f);
            rect.anchoredPosition = new Vector2(5f, 6f);
        }

        static Vector2[] State(RectTransform rect) =>
            new[] { rect.anchorMin, rect.anchorMax, rect.pivot, rect.sizeDelta, rect.anchoredPosition };

        static void AssertState(RectTransform expected, RectTransform actual, string label)
        {
            string[] names = { "anchorMin", "anchorMax", "pivot", "sizeDelta", "anchoredPosition" };
            Vector2[] want = State(expected), got = State(actual);
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(got[i].x, Is.EqualTo(want[i].x).Within(Tolerance), label + " " + names[i] + ".x");
                Assert.That(got[i].y, Is.EqualTo(want[i].y).Within(Tolerance), label + " " + names[i] + ".y");
            }
        }

        // What each RectTransformModifier setter writes (x86-64 bodies):
        // set_Height 0x1b3a4d0 sizeDelta.y, set_Width 0x1b3a5d0 sizeDelta.x,
        // set_XOffset 0x1b3a6d0 anchoredPosition.x, set_YOffset 0x1b3a7e0 anchoredPosition.y,
        // set_XAnchorMin 0x1b3a8e0 anchorMin.x, set_XAnchorMax 0x1b3a9e0 anchorMax.x,
        // set_YAnchorMin 0x1b3aaf0 anchorMin.y, set_YAnchorMax 0x1b3ac00 anchorMax.y,
        // set_LeftOffset 0x1b3ad00 offsetMin.x, set_RightOffset 0x1b3ae30 offsetMax.x = -value
        // (xorps with the 0x80000000 mask at 0x29a6cc0), set_BotOffset 0x1b3af80 offsetMin.y,
        // set_TopOffset 0x1b3b0d0 offsetMax.y = -value, set_PivotX 0x1b3b210 pivot.x,
        // set_PivotY 0x1b3b340 pivot.y.
        static void ApplyExpected(RectTransform rect, int type, float value)
        {
            switch (type)
            {
                case Height: rect.sizeDelta = new Vector2(rect.sizeDelta.x, value); break;
                case Width: rect.sizeDelta = new Vector2(value, rect.sizeDelta.y); break;
                case PosX: rect.anchoredPosition = new Vector2(value, rect.anchoredPosition.y); break;
                case PosY: rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, value); break;
                case XAnchorMin: rect.anchorMin = new Vector2(value, rect.anchorMin.y); break;
                case XAnchorMax: rect.anchorMax = new Vector2(value, rect.anchorMax.y); break;
                case YAnchorMin: rect.anchorMin = new Vector2(rect.anchorMin.x, value); break;
                case YAnchorMax: rect.anchorMax = new Vector2(rect.anchorMax.x, value); break;
                case LeftOffset: rect.offsetMin = new Vector2(value, rect.offsetMin.y); break;
                case RightOffset: rect.offsetMax = new Vector2(-value, rect.offsetMax.y); break;
                case BotOffset: rect.offsetMin = new Vector2(rect.offsetMin.x, value); break;
                case TopOffset: rect.offsetMax = new Vector2(rect.offsetMax.x, -value); break;
                case PivotX: rect.pivot = new Vector2(value, rect.pivot.y); break;
                case PivotY: rect.pivot = new Vector2(rect.pivot.x, value); break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        static RectTransform Twin(Hosts hosts, params (int type, float value)[] applied)
        {
            var rect = (RectTransform)hosts.Inactive("Expected RectTransform twin").transform;
            Baseline(rect);
            foreach ((int type, float value) step in applied) ApplyExpected(rect, step.type, step.value);
            return rect;
        }

        static RectTransform Rect(OrientationSpecificRectTransformModifier modifier) => (RectTransform)modifier.transform;

        // ---- declarations the behaviour depends on ------------------------------

        // dump.cs TypeDefIndex 12016: serialized Setting values are these literals,
        // and the SetAllValues jump table (0x1b64a64) is indexed by them directly.
        // <WaitForRequiredSystems>d__9 (TypeDefIndex 12018) fixes WaitForRequiredSystems
        // as member ordinal 9 (tenth declared member, counting the nested settings type
        // and the five fields).
        [Test]
        public void SettingTypeLiteralsAndIteratorNameMatchTheOriginal()
        {
            Assert.That(SettingsType, Is.Not.Null);
            Assert.That(SettingsType.IsNestedFamily, Is.True, "protected nested class");
            Assert.That(SettingsType.GetCustomAttributes(typeof(SerializableAttribute), false), Is.Not.Empty);
            Assert.That(Enum.GetNames(SettingTypeEnum), Is.EqualTo(new[] {
                "Height", "Width", "PosX", "PosY", "XAnchorMin", "XAnchorMax", "YAnchorMin", "YAnchorMax",
                "LeftOffset", "RightOffset", "BotOffset", "TopOffset", "PivotX", "PivotY" }));
            Assert.That(Enum.GetValues(SettingTypeEnum).Cast<object>().Select(v => Convert.ToInt32(v)), Is.EqualTo(Enumerable.Range(0, 14)));
            var iterator = (IteratorStateMachineAttribute)Subject.GetMethod("WaitForRequiredSystems", Any)
                .GetCustomAttribute(typeof(IteratorStateMachineAttribute));
            Assert.That(iterator.StateMachineType.Name, Is.EqualTo("<WaitForRequiredSystems>d__9"));
        }

        // ---- .ctor 0x06000546 (0x1b64b20) ---------------------------------------

        // The constructor allocates a fresh List<OrientationSpecificRectSettings>
        // into m_desiredSetting (+0x38, 0x1b64b7a) before MonoBehaviour..ctor; it
        // touches no other field, so m_UIOrientation (+0x40) stays Uninitialised.
        [Test]
        public void ConstructorCreatesAnEmptyDesiredListPerComponent()
        {
            using (var hosts = new Hosts())
            {
                var first = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                var second = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                IList list = DesiredList(first);
                Assert.That(list, Is.Not.Null);
                Assert.That(list.GetType(), Is.EqualTo(typeof(List<>).MakeGenericType(SettingsType)));
                Assert.That(list.Count, Is.EqualTo(0));
                Assert.That(DesiredList(second), Is.Not.SameAs(list), "each instance owns its list");
                Assert.That(Get(first, "m_UIOrientation"), Is.EqualTo(UIOrientation.Uninitialised));
            }
        }

        // ---- Awake 0x0600053f (0x1b64ba0) ---------------------------------------

        // Awake compares m_rectTransformModifier with Unity's op_Equality against null
        // (0x1b64bf5) and only then stores GetComponent<RectTransformModifier>()
        // (0x1b64c0f). A live inspector reference is kept; a destroyed one counts as
        // null and is replaced. A CLR null check would keep the destroyed reference;
        // an unconditional GetComponent would overwrite the live foreign one.
        [Test]
        public void AwakeFetchesTheSiblingModifierOnlyWhenTheFieldIsUnityNull()
        {
            using (var hosts = new Hosts())
            {
                var unassigned = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                unassigned.Awake();
                Assert.That(Get(unassigned, "m_rectTransformModifier"), Is.SameAs(unassigned.GetComponent<RectTransformModifier>()));

                RectTransformModifier foreign = hosts.Inactive("Foreign modifier").AddComponent<RectTransformModifier>();
                var assigned = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                Set(assigned, "m_rectTransformModifier", foreign);
                assigned.Awake();
                Assert.That(Get(assigned, "m_rectTransformModifier"), Is.SameAs(foreign));

                RectTransformModifier destroyed = hosts.Inactive("Destroyed modifier").AddComponent<RectTransformModifier>();
                var stale = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                Set(stale, "m_rectTransformModifier", destroyed);
                Object.DestroyImmediate(destroyed);
                stale.Awake();
                Assert.That(Get(stale, "m_rectTransformModifier"), Is.SameAs(stale.GetComponent<RectTransformModifier>()));
            }
        }

        // ---- SetAllValues 0x06000545 (0x1b64750) --------------------------------

        // Each entry's Setting indexes the 14-entry jump table at 0x1b64a64:
        // 0 set_Height (0x1b6483e), 1 set_Width (0x1b64893), 2 set_XOffset (0x1b648bb),
        // 3 set_YOffset (0x1b648e3), 4 set_XAnchorMin (0x1b6490b), 5 set_XAnchorMax
        // (0x1b64933), 6 set_YAnchorMin (0x1b6495b), 7 set_YAnchorMax (0x1b64983),
        // 8 set_LeftOffset (0x1b649ab), 9 set_RightOffset (0x1b649d3), 10 set_BotOffset
        // (0x1b649fb), 11 set_TopOffset (0x1b64a23), 12 set_PivotX (0x1b64a4b),
        // 13 set_PivotY (0x1b647e0), each passing SettingValue (+0x14). Every type
        // is applied alone to a fresh baseline and compared with a twin that received
        // the same RectTransform write, so any swapped, missing or sign-flipped route
        // changes at least one compared component.
        [Test]
        public void SetAllValuesRoutesEverySettingTypeToItsModifierSetter()
        {
            float[] values = { 21f, 22f, 23f, 24f, 0.15f, 0.55f, 0.25f, 0.65f, 26f, 27f, 28f, 29f, 0.35f, 0.45f };
            for (int type = 0; type < values.Length; type++)
            {
                using (var hosts = new Hosts())
                {
                    var modifier = hosts.Awake<OrientationSpecificRectTransformModifier>();
                    Baseline(Rect(modifier));
                    Desire(modifier, (type, values[type]));
                    Call(modifier, "SetAllValues");
                    AssertState(Twin(hosts, (type, values[type])), Rect(modifier), Enum.ToObject(SettingTypeEnum, type).ToString());
                    Assert.That(DesiredList(modifier).Count, Is.EqualTo(1), "SetAllValues does not consume entries");
                }
            }
        }

        // The loop runs index 0 upward (0x1b647af, inc at 0x1b647f0) re-reading the
        // list count each pass. Applying LeftOffset then Width differs from the
        // reverse order (Width then LeftOffset leaves offsetMin.x at 3), and two
        // Width entries leave the later value.
        [Test]
        public void SetAllValuesAppliesEntriesInListOrder()
        {
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Awake<OrientationSpecificRectTransformModifier>();
                Baseline(Rect(modifier));
                Desire(modifier, (LeftOffset, 3f), (Width, 40f), (PosY, 9f), (PosY, -12f));
                Call(modifier, "SetAllValues");
                AssertState(Twin(hosts, (LeftOffset, 3f), (Width, 40f), (PosY, 9f), (PosY, -12f)), Rect(modifier), "forward order");
                Assert.That(Rect(modifier).sizeDelta.x, Is.EqualTo(40f).Within(Tolerance));
                Assert.That(Rect(modifier).anchoredPosition.y, Is.EqualTo(-12f).Within(Tolerance));
            }
        }

        // Values above 13 and negative values both fail the unsigned bound check
        // (cmp rax, 0xd; ja at 0x1b64814) and reach HLOutput.LogError("Enum extended,
        // add condition to script") (0x1b6486b), which jumps back to the loop
        // increment (0x1b64870): later entries still apply and nothing is thrown.
        // An owned HLUnityCore receives one report per unknown entry; HLOutput.LogError
        // forwards error code 0.
        [Test]
        public void SetAllValuesSkipsUnknownSettingTypesAndContinues()
        {
            using (var log = new OwnedCoreErrorLog())
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Awake<OrientationSpecificRectTransformModifier>();
                Baseline(Rect(modifier));
                Desire(modifier, (14, 77f), (Width, 33f), (-1, 88f), (Height, 44f));
                Assert.DoesNotThrow(() => Call(modifier, "SetAllValues"));
                CollectionAssert.AreEqual(new[]
                {
                    "Enum extended, add condition to script:0",
                    "Enum extended, add condition to script:0",
                }, log.Errors, "one report per unknown entry (14, then -1)");
                AssertState(Twin(hosts, (Width, 33f), (Height, 44f)), Rect(modifier), "unknown entries skipped");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // The count test (0x1b647a5) precedes any read of m_rectTransformModifier,
        // so an empty list never dereferences a missing modifier.
        [Test]
        public void SetAllValuesWithNoEntriesNeverReadsTheModifier()
        {
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                Assert.DoesNotThrow(() => Call(modifier, "SetAllValues"));
            }
        }

        // ---- InitialiseValues 0x06000544 (0x1b64670) -----------------------------

        // Reads ScreenManager.Instance.Orientation (+0x50), stores it in m_UIOrientation
        // (+0x40, 0x1b646e9), then AddRanges field [this + 0x28 + 8 * (orientation != 2)]
        // (setne at 0x1b646f1, load at 0x1b64702): only Landscape (2) selects
        // m_landscapeSettings; Portrait, Uninitialised and out-of-range values all
        // select m_portraitSettings. Entries keep their array order and the same list
        // instance receives them.
        [Test]
        public void InitialiseValuesSelectsLandscapeOnlyForTheLandscapeOrientation()
        {
            (int, float)[] landscape = { (Width, 1f), (Height, 2f) };
            (int, float)[] portrait = { (PosX, 3f), (PivotY, 0.5f), (PosY, 4f) };
            foreach (UIOrientation orientation in new[] { UIOrientation.Landscape, UIOrientation.Portrait, UIOrientation.Uninitialised, (UIOrientation)3 })
            {
                using (var screen = new ScreenFixture(orientation))
                using (var hosts = new Hosts())
                {
                    var modifier = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                    Configure(modifier, landscape, portrait);
                    IList list = DesiredList(modifier);
                    Call(modifier, "InitialiseValues");
                    Assert.That(DesiredList(modifier), Is.SameAs(list), orientation + " keeps the list instance");
                    Assert.That(Desired(modifier), Is.EqualTo(orientation == UIOrientation.Landscape ? landscape : portrait), orientation.ToString());
                    Assert.That(Get(modifier, "m_UIOrientation"), Is.EqualTo(orientation));
                }
            }
        }

        // AddRange (0x1b6470d) is the only list operation: nothing clears the list,
        // so a second call after an orientation change appends the other array
        // behind the first one.
        [Test]
        public void InitialiseValuesAppendsWithoutClearing()
        {
            using (var screen = new ScreenFixture(UIOrientation.Landscape))
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                Configure(modifier, new[] { (Width, 1f) }, new[] { (Height, 2f) });
                Call(modifier, "InitialiseValues");
                Call(modifier, "InitialiseValues");
                screen.Orientation = UIOrientation.Portrait;
                Call(modifier, "InitialiseValues");
                Assert.That(Desired(modifier), Is.EqualTo(new[] { (Width, 1f), (Width, 1f), (Height, 2f) }));
                Assert.That(Get(modifier, "m_UIOrientation"), Is.EqualTo(UIOrientation.Portrait));
            }
        }

        // InitialiseValues has no NotNull guard: it reads Instance straight from the
        // MonoSingleton<ScreenManager> statics (0x1b646a1..0x1b646e6). With no
        // singleton the read faults before m_UIOrientation or the list is written.
        [Test]
        public void InitialiseValuesWithoutTheSingletonFaultsBeforeWriting()
        {
            Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "Fixture requires no ScreenManager singleton");
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                Configure(modifier, new[] { (Width, 1f) }, new[] { (Height, 2f) });
                Set(modifier, "m_UIOrientation", UIOrientation.Landscape);
                Assert.Throws<NullReferenceException>(() => Call(modifier, "InitialiseValues"));
                Assert.That(Get(modifier, "m_UIOrientation"), Is.EqualTo(UIOrientation.Landscape));
                Assert.That(DesiredList(modifier).Count, Is.EqualTo(0));
            }
        }

        // ---- InitialiseAndSetValues 0x06000543 (0x1b64f60) -----------------------

        // Virtual call through slot 5 (InitialiseValues, call [rax + 0x188] at
        // 0x1b64f73) followed by a tail jump to SetAllValues (0x1b64f82). The probe's
        // InitialiseValues override adds a Width entry instead of reading the
        // screen; it is applied in the same call, so SetAllValues runs after it.
        [Test]
        public void InitialiseAndSetValuesInitialisesVirtuallyThenApplies()
        {
            using (var hosts = new Hosts())
            {
                var probe = hosts.Awake<OrientationSpecificRectTransformModifierProbe>();
                Baseline(Rect(probe));
                probe.InjectedWidth = 41f;
                Call(probe, "InitialiseAndSetValues");
                Assert.That(probe.Calls, Is.EqualTo(new[] { "InitialiseAndSetValues", "InitialiseValues" }));
                AssertState(Twin(hosts, (Width, 41f)), Rect(probe), "injected width applied");
            }
        }

        // Because InitialiseValues never clears, an orientation change re-applies the
        // stale landscape entries before the portrait ones: a landscape-only PosX is
        // written again after it was moved, and the later portrait Width wins.
        [Test]
        public void InitialiseAndSetValuesReappliesStaleEntriesAfterAnOrientationChange()
        {
            using (var screen = new ScreenFixture(UIOrientation.Landscape))
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Awake<OrientationSpecificRectTransformModifier>();
                Baseline(Rect(modifier));
                Configure(modifier, new[] { (Width, 100f), (PosX, 7f) }, new[] { (Width, 200f) });
                Call(modifier, "InitialiseAndSetValues");
                AssertState(Twin(hosts, (Width, 100f), (PosX, 7f)), Rect(modifier), "landscape");

                Rect(modifier).anchoredPosition = new Vector2(0f, Rect(modifier).anchoredPosition.y);
                screen.Orientation = UIOrientation.Portrait;
                Call(modifier, "InitialiseAndSetValues");
                Assert.That(Rect(modifier).anchoredPosition.x, Is.EqualTo(7f).Within(Tolerance), "stale landscape PosX re-applied");
                Assert.That(Rect(modifier).sizeDelta.x, Is.EqualTo(200f).Within(Tolerance), "portrait Width applied last");
                Assert.That(Desired(modifier), Is.EqualTo(new[] { (Width, 100f), (PosX, 7f), (Width, 200f) }));
                Assert.That(Get(modifier, "m_UIOrientation"), Is.EqualTo(UIOrientation.Portrait));
            }
        }

        // ---- OnEnable 0x06000540 (0x1b64c30) / OnDisable 0x06000541 (0x1b64e20) --

        // When MonoSingleton<ScreenManager>.NotNull() (0x1b64c88) holds, OnEnable
        // builds Action(this, vtable slot 4 InitialiseAndSetValues; [rax + 0x180] at
        // 0x1b64d00) and stores Instance.OnOrientationChange (+0x58) + action via
        // FastActionBase.op_Addition (0x1b64d24), not AddUnique, so enabling twice
        // subscribes twice. It then starts WaitForRequiredSystems (0x1b64d97) on
        // both paths; the inactive host makes StartCoroutine log once per call
        // (which iterator is started: OnEnableStartsTheWaitForRequiredSystemsIterator).
        // Invoking the FastAction dispatches to the probe's override.
        [Test]
        public void OnEnableSubscribesTheVirtualRefreshAndStartsTheCoroutine()
        {
            using (var screen = new ScreenFixture(UIOrientation.Landscape))
            using (var hosts = new Hosts())
            {
                var probe = hosts.Awake<OrientationSpecificRectTransformModifierProbe>();
                Baseline(Rect(probe));
                Configure(probe, new[] { (Width, 30f) }, new[] { (Width, 60f) });
                Assert.That(screen.Manager.OnOrientationChange, Is.Null);

                LogAssert.Expect(LogType.Error, InactiveCoroutine);
                Call(probe, "OnEnable");
                List<Action> listeners = screen.Manager.OnOrientationChange.GetInvocationList();
                Assert.That(listeners.Count, Is.EqualTo(1));
                Assert.That(listeners[0].Target, Is.SameAs(probe));
                Assert.That(listeners[0].Method.Name, Is.EqualTo("InitialiseAndSetValues"), "a method-group delegate, not a lambda");
                Assert.That(probe.Calls, Is.Empty, "OnEnable itself does not refresh");

                FastAction.Invoke(screen.Manager.OnOrientationChange);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "InitialiseAndSetValues", "InitialiseValues" }));
                Assert.That(Rect(probe).sizeDelta.x, Is.EqualTo(30f).Within(Tolerance));

                LogAssert.Expect(LogType.Error, InactiveCoroutine);
                Call(probe, "OnEnable");
                Assert.That(screen.Manager.OnOrientationChange.GetInvocationListCount(), Is.EqualTo(2));
                LogAssert.NoUnexpectedReceived();
            }
        }

        // NotNull uses Unity equality, so neither a missing nor a destroyed singleton
        // is subscribed to; the coroutine is still started (0x1b64c8f jumps to the
        // StartCoroutine block at 0x1b64d37).
        [Test]
        public void OnEnableWithoutALiveScreenManagerOnlyStartsTheCoroutine()
        {
            Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "Fixture requires no ScreenManager singleton");
            using (var hosts = new Hosts())
            {
                var probe = hosts.Awake<OrientationSpecificRectTransformModifierProbe>();
                LogAssert.Expect(LogType.Error, InactiveCoroutine);
                Assert.DoesNotThrow(() => Call(probe, "OnEnable"));

                using (var screen = new ScreenFixture(UIOrientation.Landscape, register: false))
                {
                    screen.DestroyHost();
                    Assert.That(ScreenManager.Instance == null && !ReferenceEquals(ScreenManager.Instance, null), Is.True,
                        "destroyed singleton prerequisite");
                    LogAssert.Expect(LogType.Error, InactiveCoroutine);
                    Call(probe, "OnEnable");
                    Assert.That(screen.Manager.OnOrientationChange, Is.Null, "a destroyed singleton is not subscribed");
                }
                Assert.That(probe.Calls, Is.Empty);
                LogAssert.NoUnexpectedReceived();
            }
        }

        // The coroutine cases above only see Unity's generic inactive-host error, which
        // does not name the iterator. 0x1b64d53..0x1b64d97 allocates
        // <WaitForRequiredSystems>d__9 (WaitForRequiredSystems inlined: state 0, this)
        // and tail-jumps StartCoroutine(IEnumerator) 0x06000f41, so OnEnable's last two
        // calls must be exactly that iterator method and that overload.
        [Test]
        public void OnEnableStartsTheWaitForRequiredSystemsIterator()
        {
            MethodInfo onEnable = Subject.GetMethod("OnEnable", Any);
            MethodInfo wait = Subject.GetMethod("WaitForRequiredSystems", Any);
            MethodInfo start = typeof(MonoBehaviour).GetMethod("StartCoroutine", new[] { typeof(IEnumerator) });
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(onEnable);
            Assert.That(calls.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(calls[calls.Count - 2].Target, Is.EqualTo(wait), "the started iterator");
            Assert.That(calls[calls.Count - 1].Target, Is.EqualTo(start), "StartCoroutine(IEnumerator)");
            Assert.That(calls.Count(call => call.Target == (MethodBase)wait), Is.EqualTo(1), "one iterator per OnEnable");
            var iterator = (IteratorStateMachineAttribute)wait.GetCustomAttribute(typeof(IteratorStateMachineAttribute));
            Assert.That(iterator.StateMachineType.Name, Is.EqualTo("<WaitForRequiredSystems>d__9"));
        }

        // OnDisable mirrors the guard (0x1b64e78) and stores OnOrientationChange -
        // Action(this, slot 4) via op_Subtraction (0x1b64f10); it starts nothing.
        // Only its own listener goes; an unrelated listener survives. Without a
        // subscription the null FastAction stays null (op_Subtraction returns null),
        // and without a singleton OnDisable does nothing.
        [Test]
        public void OnDisableRemovesOnlyItsOwnSubscription()
        {
            using (var hosts = new Hosts())
            {
                var probe = hosts.Awake<OrientationSpecificRectTransformModifierProbe>();
                Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "Fixture requires no ScreenManager singleton");
                Assert.DoesNotThrow(() => Call(probe, "OnDisable"));

                using (var screen = new ScreenFixture(UIOrientation.Portrait))
                {
                    Call(probe, "OnDisable");
                    Assert.That(screen.Manager.OnOrientationChange, Is.Null, "removing from no FastAction leaves null");

                    int unrelated = 0;
                    Action other = () => ++unrelated;
                    screen.Manager.OnOrientationChange += other;
                    LogAssert.Expect(LogType.Error, InactiveCoroutine);
                    Call(probe, "OnEnable");
                    FastAction before = screen.Manager.OnOrientationChange;
                    Call(probe, "OnDisable");
                    Assert.That(screen.Manager.OnOrientationChange, Is.SameAs(before));
                    Assert.That(screen.Manager.OnOrientationChange.GetInvocationList(), Is.EqualTo(new[] { other }));
                    FastAction.Invoke(screen.Manager.OnOrientationChange);
                    Assert.That(unrelated, Is.EqualTo(1));
                    Assert.That(probe.Calls, Is.Empty, "the disabled modifier is no longer refreshed");
                }
                LogAssert.NoUnexpectedReceived();
            }
        }

        // The m_UIOrientation store (0x1b646e9) precedes the tail-jump to
        // List.AddRange (0x1b6470d), which receives the selected array with no null
        // test: a null portrait array throws ArgumentNullException after the field
        // already holds the new orientation, and the list stays empty.
        [Test]
        public void InitialiseValuesStoresTheOrientationBeforeAddingANullArray()
        {
            using (var screen = new ScreenFixture(UIOrientation.Portrait))
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                Set(modifier, "m_landscapeSettings", Settings((Width, 1f)));
                Set(modifier, "m_portraitSettings", null);
                Set(modifier, "m_UIOrientation", UIOrientation.Landscape);
                Assert.Throws<ArgumentNullException>(() => Call(modifier, "InitialiseValues"));
                Assert.That(Get(modifier, "m_UIOrientation"), Is.EqualTo(UIOrientation.Portrait));
                Assert.That(DesiredList(modifier).Count, Is.EqualTo(0));
            }
        }

        // ---- <WaitForRequiredSystems>d__9 0x06000548..0x0600054d ----------------

        static IEnumerator Iterator(OrientationSpecificRectTransformModifier modifier) =>
            (IEnumerator)Call(modifier, "WaitForRequiredSystems");

        // MoveNext (0x1b64fb0) gates on ProcessManager.IsSystemNull<ScreenManager>()
        // (0x1b65017), the registry row, not the singleton: with Instance published
        // but no row it yields null and stays in state 1. Once the row exists it calls
        // InitialiseAndSetValues through slot 4 (0x1b65053) and finishes; later
        // MoveNext calls (state -1, jae at 0x1b64fe6) return false without calling it.
        [Test]
        public void WaitForRequiredSystemsPollsTheRegistryNotTheSingleton()
        {
            using (var screen = new ScreenFixture(UIOrientation.Landscape, register: false))
            using (var hosts = new Hosts())
            {
                var probe = hosts.Awake<OrientationSpecificRectTransformModifierProbe>();
                Configure(probe, new[] { (Width, 15f) }, new[] { (Width, 25f) });
                IEnumerator iterator = Iterator(probe);
                Assert.That(probe.Calls, Is.Empty, "creating the iterator runs nothing");
                for (int step = 0; step < 2; step++)
                {
                    Assert.That(iterator.MoveNext(), Is.True, "yields while the registry row is absent");
                    Assert.That(iterator.Current, Is.Null);
                }
                Assert.That(probe.Calls, Is.Empty);

                screen.Register();
                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "InitialiseAndSetValues", "InitialiseValues" }));
                Assert.That(Rect(probe).sizeDelta.x, Is.EqualTo(15f).Within(Tolerance));
                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(probe.Calls.Count, Is.EqualTo(2), "a finished iterator does not refresh again");
            }
        }

        // The check runs before any yield: with the row already registered the first
        // MoveNext refreshes and returns false.
        [Test]
        public void WaitForRequiredSystemsRefreshesImmediatelyWhenRegistered()
        {
            using (var screen = new ScreenFixture(UIOrientation.Portrait))
            using (var hosts = new Hosts())
            {
                var probe = hosts.Awake<OrientationSpecificRectTransformModifierProbe>();
                Configure(probe, new[] { (Width, 15f) }, new[] { (Width, 25f) });
                IEnumerator iterator = Iterator(probe);
                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "InitialiseAndSetValues", "InitialiseValues" }));
                Assert.That(Rect(probe).sizeDelta.x, Is.EqualTo(25f).Within(Tolerance));
            }
        }

        // The registry row alone releases the wait: with the row registered and no
        // published singleton the first MoveNext still calls slot 4. A probe that
        // injects its entries completes the refresh; the base InitialiseValues then
        // faults on the unguarded Instance read (0x1b646a1..0x1b646e6).
        [Test]
        public void WaitForRequiredSystemsDoesNotWaitForTheSingleton()
        {
            using (var screen = new ScreenFixture(UIOrientation.Landscape, register: true, publish: false))
            using (var hosts = new Hosts())
            {
                Assert.That(ReferenceEquals(ScreenManager.Instance, null), Is.True, "singleton stays unpublished");
                var probe = hosts.Awake<OrientationSpecificRectTransformModifierProbe>();
                Baseline(Rect(probe));
                probe.InjectedWidth = 12f;
                IEnumerator iterator = Iterator(probe);
                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "InitialiseAndSetValues", "InitialiseValues" }));
                Assert.That(Rect(probe).sizeDelta.x, Is.EqualTo(12f).Within(Tolerance));

                var modifier = hosts.Awake<OrientationSpecificRectTransformModifier>();
                Configure(modifier, new[] { (Width, 15f) }, new[] { (Width, 25f) });
                IEnumerator unguarded = Iterator(modifier);
                Assert.Throws<NullReferenceException>(() => unguarded.MoveNext());
                Assert.That(DesiredList(modifier).Count, Is.EqualTo(0));
            }
        }

        // IEnumerator.Reset (0x1b65080) throws NotSupportedException.
        [Test]
        public void WaitForRequiredSystemsIteratorRejectsReset()
        {
            using (var hosts = new Hosts())
            {
                var modifier = hosts.Modifier<OrientationSpecificRectTransformModifier>();
                IEnumerator iterator = Iterator(modifier);
                Assert.Throws<NotSupportedException>(() => iterator.Reset());
            }
        }
    }
}
