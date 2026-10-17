using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // HLInputDebugMenu private menu callbacks on an owned inactive menu (Awake is
    // empty and never runs). The App is an owned inactive component in an owned
    // registry row, so its Awake and OnDestroy never run.
    public sealed class OriginalHLInputDebugMenuBehaviourTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // GetAbilitySteeringAssistDesc 0x06002007, x86-64 0x55efc0: one
        // GetValue<bool>(InputAbilitySteeringAssistDisabled, false, storeDefault: true)
        // (0x55f0f3..0x55f0fe), then cmov selection (0x55f101..0x55f123): enabled
        // labels are '<< On >>' when not disabled, else the other initialised literal
        // 'On'; disabled labels are 'Off' when not disabled, else '<< Off >>'.
        // SetAbilitySteeringAssist 0x06002008, x86-64 0x55f140, stores the inverse
        // (`xor bl, 1` at 0x55f22e). Catches swapped label pairs, storing the
        // argument itself, or a read that does not store the missing default.
        [Test]
        public void SteeringAssistLabelsFollowTheStoredDisabledFlagAndTheSetterStoresItsInverse()
        {
            WithOwnedApp((app, owned) =>
            {
                HLInputDebugMenu menu = Inactive(owned, "Owned inactive input debug menu").AddComponent<HLInputDebugMenu>();
                Func<bool, string> describe = value => (string)Method("GetAbilitySteeringAssistDesc").Invoke(menu, new object[] { value });
                Action<bool> set = value => Method("SetAbilitySteeringAssist").Invoke(menu, new object[] { value });
                IGraphStorage storage = app.Storage;
                GraphStorageKey key = AppFSMKeys.InputAbilitySteeringAssistDisabled;

                Assert.IsFalse(storage.HasValue<bool>(key));
                Assert.AreEqual("<< On >>", describe(true));
                Assert.IsTrue(storage.TryGetValue(key, out bool stored), "The missing flag is stored as its default.");
                Assert.IsFalse(stored);
                Assert.AreEqual("Off", describe(false));

                set(false);
                Assert.IsTrue(storage.TryGetValue(key, out stored));
                Assert.IsTrue(stored, "Disabling assist stores disabled = true.");
                Assert.AreEqual("On", describe(true));
                Assert.AreEqual("<< Off >>", describe(false));

                set(true);
                Assert.IsTrue(storage.TryGetValue(key, out stored));
                Assert.IsFalse(stored);
                Assert.AreEqual("<< On >>", describe(true));
                Assert.AreEqual("Off", describe(false));
            });
        }

        // ToggleGlyphs 0x0600200d, x86-64 0x55f280: hide-and-return only when the
        // parent is activeSelf (0x55f3c1) AND activeInHierarchy (0x55f3d0) AND the
        // last shown type equals the request (0x55f3d9); that path is a lone
        // SetActive(false) (0x55f3f5). Otherwise it stores the type (0x55f3fa),
        // activates the parent, hides every child (0x55f411) and builds the sorted
        // list before the first pool Instantiate, which faults here because no
        // prefab is authored. Catches hiding children on the early path, dropping
        // the hierarchy or type test, or not recording the requested type.
        [Test]
        public void SameVisibleTypeHidesOnlyTheParentWhileOtherRequestsReshowAndClearChildren()
        {
            // The menu's field initialiser creates an empty App row; no App exists.
            IDictionary registry = Registry();
            string appRow = ProcessManager.GetDefaultName<App>();
            Assert.IsFalse(registry.Contains(appRow), "Fixture requires an absent App row.");
            var owned = new List<Object>();
            try
            {
                HLInputDebugMenuProbe probe = Probe(owned);
                FieldInfo last = typeof(HLInputDebugMenu).GetField("m_lastShownInputType", Instance);

                probe.Child.SetActive(true);
                probe.ToggleExpectingMissingPrefab(InputType.Keyboard);
                Assert.AreEqual(InputType.Keyboard, (InputType)last.GetValue(probe.Menu));
                Assert.IsTrue(probe.Parent.activeSelf);
                Assert.IsFalse(probe.Child.activeSelf, "A showing request hides the children first.");

                probe.Child.SetActive(true);
                probe.Toggle(InputType.Keyboard);
                Assert.IsFalse(probe.Parent.activeSelf, "The same visible type hides the parent.");
                Assert.IsTrue(probe.Child.activeSelf, "Only the parent is hidden.");
                Assert.AreEqual(InputType.Keyboard, (InputType)last.GetValue(probe.Menu));

                // A hidden parent with the same type is shown again.
                probe.ToggleExpectingMissingPrefab(InputType.Keyboard);
                Assert.IsTrue(probe.Parent.activeSelf);
                Assert.IsFalse(probe.Child.activeSelf);

                // activeSelf alone is not enough: an inactive ancestor keeps the
                // same-type request on the showing path.
                probe.Child.SetActive(true);
                probe.Ancestor.SetActive(false);
                Assert.IsTrue(probe.Parent.activeSelf && !probe.Parent.activeInHierarchy);
                probe.ToggleExpectingMissingPrefab(InputType.Keyboard);
                Assert.IsTrue(probe.Parent.activeSelf);
                Assert.IsFalse(probe.Child.activeSelf);
                probe.Ancestor.SetActive(true);

                // A different type with a visible parent is also a showing request.
                probe.Child.SetActive(true);
                probe.ToggleExpectingMissingPrefab(InputType.Touch);
                Assert.AreEqual(InputType.Touch, (InputType)last.GetValue(probe.Menu));
                Assert.IsTrue(probe.Parent.activeSelf);
                Assert.IsFalse(probe.Child.activeSelf);
            }
            finally
            {
                try
                {
                    for (int i = owned.Count - 1; i >= 0; --i)
                        if (owned[i] != null) Object.DestroyImmediate(owned[i]);
                }
                finally
                {
                    if (registry.Contains(appRow))
                    {
                        Assert.IsTrue(ProcessManager.IsSystemNull(appRow), "The App row never held a system.");
                        registry.Remove(appRow);
                    }
                }
            }
        }

        // <>c.<ToggleGlyphs>b__17_0 0x06002016, x86-64 0x560300: Enum.ToString on
        // both values (0x56034e, 0x56036e) and String.CompareOrdinal (0x56037b).
        // Down (-711622032) sorts before Left (-1290930909) by name although its
        // value is larger. Catches sorting by enum value or reversing the order.
        [Test]
        public void GlyphComparatorOrdersInputsByOrdinalNameNotValue()
        {
            // Discover the natural lambda cache by role, as the contract test does.
            const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly;
            Type cache = typeof(HLInputDebugMenu).GetNestedTypes(declared).Single(t =>
                t.GetFields(declared).Any(f => f.IsStatic && f.FieldType == typeof(Comparison<GameInput>)));
            object instance = cache.GetFields(declared).Single(f => f.IsStatic && f.FieldType == cache).GetValue(null);
            MethodInfo comparator = cache.GetMethods(declared).Single(m => m.Name.StartsWith("<ToggleGlyphs>b__", StringComparison.Ordinal));
            Func<GameInput, GameInput, int> compare = (x, y) => (int)comparator.Invoke(instance, new object[] { x, y });

            Assert.Less(compare(GameInput.Down, GameInput.Left), 0);
            Assert.Greater(compare(GameInput.Left, GameInput.Down), 0);
            Assert.Greater((int)GameInput.Down, (int)GameInput.Left);
            Assert.AreEqual(0, compare(GameInput.UISubmit, GameInput.UISubmit));

            var all = Enum.GetValues(typeof(GameInput)).Cast<GameInput>().ToList();
            all.Sort((x, y) => compare(x, y));
            CollectionAssert.AreEqual(all.Select(v => v.ToString()).OrderBy(n => n, StringComparer.Ordinal).ToList(),
                all.Select(v => v.ToString()).ToList());
        }

        private static IDictionary Registry() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        private static MethodInfo Method(string name)
        {
            MethodInfo method = typeof(HLInputDebugMenu).GetMethod(name, Instance);
            Assert.IsNotNull(method, name);
            return method;
        }

        private static GameObject Inactive(List<Object> owned, string name)
        {
            var value = new GameObject(name);
            value.SetActive(false);
            owned.Add(value);
            return value;
        }

        private static HLInputDebugMenuProbe Probe(List<Object> owned)
        {
            var ancestor = new GameObject("Owned input debug ancestor");
            owned.Add(ancestor);
            var parent = new GameObject("Owned input debug parent");
            parent.transform.SetParent(ancestor.transform, false);
            var child = new GameObject("Owned input debug child");
            child.transform.SetParent(parent.transform, false);
            HLInputDebugMenu menu = Inactive(owned, "Owned inactive glyph debug menu").AddComponent<HLInputDebugMenu>();
            typeof(HLInputDebugMenu).GetField("m_debugParent", Instance).SetValue(menu, parent);
            return new HLInputDebugMenuProbe(menu, ancestor, parent, child);
        }

        private sealed class HLInputDebugMenuProbe
        {
            public readonly HLInputDebugMenu Menu;
            public readonly GameObject Ancestor, Parent, Child;
            private readonly MethodInfo toggle = Method("ToggleGlyphs");

            public HLInputDebugMenuProbe(HLInputDebugMenu menu, GameObject ancestor, GameObject parent, GameObject child)
            { Menu = menu; Ancestor = ancestor; Parent = parent; Child = child; }

            public void Toggle(InputType type) => toggle.Invoke(Menu, new object[] { type });

            // The showing path reaches Object.Instantiate(null prefab) and faults
            // before any EventSystem or glyph lookup.
            public void ToggleExpectingMissingPrefab(InputType type)
            {
                var failure = Assert.Throws<TargetInvocationException>(() => Toggle(type));
                Assert.IsInstanceOf<ArgumentException>(failure.InnerException);
                Assert.AreEqual(1, Parent.transform.childCount, "No pool item was created.");
            }
        }

        // An owned inactive App in an absent default row. Its field initialisers
        // auto-register the SaveManager and UIScreenTransitionManager rows; rows
        // created here are removed after checking they never held a system, and
        // a borrowed row loses only the typed references GetSystemRef<T> cached.
        private static void WithOwnedApp(Action<App, List<Object>> body)
        {
            IDictionary registry = Registry();
            string[] fieldRows = { ProcessManager.GetDefaultName<SaveManager>(), ProcessManager.GetDefaultName<UIScreenTransitionManager>() };
            var borrowed = new BorrowedSystemRowTypedCache[fieldRows.Length];
            for (int i = 0; i < fieldRows.Length; i++)
            {
                if (registry.Contains(fieldRows[i])) Assert.IsTrue(ProcessManager.IsSystemNull(fieldRows[i]), "Occupied row " + fieldRows[i]);
                borrowed[i] = BorrowedSystemRowTypedCache.TryBorrow(fieldRows[i]);
            }
            var owned = new List<Object>();
            try
            {
                using (var appRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<App>()))
                {
                    App app = Inactive(owned, "Owned inactive debug-menu App").AddComponent<App>();
                    appRow.Register(app);
                    try { body(app, owned); }
                    finally
                    {
                        for (int i = owned.Count - 1; i >= 0; --i)
                            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < fieldRows.Length; i++)
                {
                    if (borrowed[i] != null) { borrowed[i].Dispose(); continue; }
                    if (!registry.Contains(fieldRows[i])) continue;
                    Assert.IsTrue(ProcessManager.IsSystemNull(fieldRows[i]), fieldRows[i]);
                    registry.Remove(fieldRows[i]);
                }
            }
        }
    }
}
