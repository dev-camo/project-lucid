using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Behaviour of the original Assembly-CSharp-firstpass UIWidgetRetainLastSelected (1.10.1),
    // grounded in the x86-64 bodies cited per test. The firstpass type is reached by name.
    // Real components only: inactive owned GameObjects, the real UIManager and the concrete
    // UIContainerSimple. The deferred select on an active owner is the PlayMode
    // OriginalUIWidgetRetainLastSelectedPlayTests case.
    public sealed class OriginalUIWidgetRetainLastSelectedBehaviourTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static Type Widget => Type.GetType("Hardlight.UIWidgetRetainLastSelected, Assembly-CSharp-firstpass", true);

        private static FieldInfo Field(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);

        // Owns every object, ProcessManager row and typed cache entry it creates. Any existing
        // UIManager or UINavigationManager row is refused before the first lifecycle step: even a
        // null row can carry startup observers, deferred actions and typed caches.
        private sealed class Fixture : IDisposable
        {
            internal static readonly string[] GuardedRows = { ProcessManager.GetDefaultName<UIManager>(), ProcessManager.GetDefaultName<UINavigationManager>() };
            private readonly IDictionary registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            private readonly HashSet<object> priorRows = new HashSet<object>();
            private readonly Dictionary<object, HashSet<object>> priorTypedKeys = new Dictionary<object, HashSet<object>>();
            private readonly List<UObject> owned = new List<UObject>();
            private bool managerRegistered;

            public readonly GameObject Owner;
            public readonly UIContainerSimple Container;
            public readonly UIContainerIdentifier Identifier;
            public readonly UIContainerNavigation Navigation;
            public readonly UIWidgetNavigation First;
            public readonly UIWidgetNavigation Second;
            public readonly UIManager Manager;
            public readonly MonoBehaviour Retain;

            public Fixture(bool assignManager = true)
            {
                foreach (string name in GuardedRows)
                    if (registry.Contains(name))
                        throw new InvalidOperationException("Fixture requires an absent system row: " + name);
                foreach (DictionaryEntry row in registry)
                {
                    priorRows.Add(row.Key);
                    priorTypedKeys[row.Key] = new HashSet<object>(((IDictionary)Get(row.Value, "SystemRefDictionary")).Keys.Cast<object>());
                }

                try
                {
                    Manager = Own("Lucid owned UIManager 142").AddComponent<UIManager>();
                    Identifier = OwnAsset(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                    Set(Identifier, "m_guid", "lucid-retained-container-142");
                    Owner = Own("Lucid owned retained container 142");
                    Container = Owner.AddComponent<UIContainerSimple>();
                    Set(Container, "m_identifier", Identifier);
                    // The UIContainerNavigation field initializer creates the UINavigationManager row and typed reference.
                    Navigation = Owner.AddComponent<UIContainerNavigation>();
                    First = Own("Lucid owned widget 0").AddComponent<UIWidgetNavigation>();
                    Second = Own("Lucid owned widget 1").AddComponent<UIWidgetNavigation>();
                    SetWidgets(First, Second);
                    Retain = (MonoBehaviour)Owner.AddComponent(Widget);
                    Set(Retain, "m_uiContainer", Container);
                    Set(Retain, "m_uiContainerNavigation", Navigation);
                    // Awake's own resolution (0x426460) is covered by the Awake tests; elsewhere this
                    // stores the real UIManager the method bodies read from field 0x38.
                    if (assignManager) Set(Retain, "m_uiManager", Manager);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public GameObject Own(string name)
            {
                var owner = new GameObject(name);
                owner.SetActive(false);
                owned.Add(owner);
                return owner;
            }

            public T OwnAsset<T>(T asset) where T : UObject
            {
                owned.Add(asset);
                return asset;
            }

            public void SetWidgets(params UIWidgetNavigation[] widgets) => Set(Navigation, "m_navigableWidgets", widgets);

            public void RegisterManager()
            {
                ProcessManager.RegisterSystem(Manager);
                managerRegistered = true;
            }

            public void UnregisterManager()
            {
                ProcessManager.UnregisterSystem(Manager);
                managerRegistered = false;
            }

            public void Call(string method, params object[] arguments)
            {
                try { Widget.GetMethod(method, Instance | BindingFlags.DeclaredOnly).Invoke(Retain, arguments); }
                catch (TargetInvocationException failure) { ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); }
            }

            // Entries of the widget's OnWidgetSelected backing delegate bound to this component.
            public int Subscriptions(UIWidgetNavigation widget) =>
                ((Delegate)Get(widget, "OnWidgetSelected")).GetInvocationList().Count(entry => ReferenceEquals(entry.Target, Retain));

            public int ClosedSubscriptions() =>
                ((Delegate)Get(Manager, "OnContainerClosed")).GetInvocationList().Count(entry => ReferenceEquals(entry.Target, Retain));

            public int? Stored() => Manager.TryGetSelectedWidget(Container, out int index) ? index : (int?)null;

            public void Dispose()
            {
                var failures = new List<string>();
                if (managerRegistered) ProcessManager.UnregisterSystem(Manager);
                for (int i = owned.Count - 1; i >= 0; i--)
                    if (owned[i] != null) UObject.DestroyImmediate(owned[i]);
                // Remove only rows and typed entries this fixture created, and only once cleared.
                foreach (object key in registry.Keys.Cast<object>().ToArray())
                {
                    object row = registry[key];
                    IDictionary typed = (IDictionary)Get(row, "SystemRefDictionary");
                    if (!priorRows.Contains(key))
                    {
                        if (Cleared(Get(row, "SystemRef")) && typed.Values.Cast<object>().All(Cleared)) registry.Remove(key);
                        else failures.Add("created row retains a system: " + key);
                        continue;
                    }
                    foreach (object typedKey in typed.Keys.Cast<object>().Where(k => !priorTypedKeys[key].Contains(k)).ToArray())
                    {
                        if (Cleared(typed[typedKey])) typed.Remove(typedKey);
                        else failures.Add("created typed reference retains a system: " + key + "/" + typedKey);
                    }
                }
                if (failures.Count != 0) throw new InvalidOperationException(string.Join("; ", failures));
            }

            private static bool Cleared(object reference)
            {
                Type type = reference.GetType();
                while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
                foreach (string name in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                    if (type.GetField(name, Instance | BindingFlags.DeclaredOnly).GetValue(reference) != null) return false;
                return true;
            }
        }

        // Fixture ownership control: a dormant (null) row seeded with startup observers, a deferred
        // action and a typed cache is refused before any lifecycle step, and left exactly as seeded.
        // Catches: a guard that only refuses live rows, which would later replace the borrowed
        // row (running its callbacks) and revoke it on cleanup.
        [Test]
        public void FixtureRefusesAnExistingDormantRowWithoutTouchingIt()
        {
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            foreach (string name in Fixture.GuardedRows)
                Assert.That(registry.Contains(name), Is.False, "control starts without the row it seeds: " + name);
            foreach (string seeded in Fixture.GuardedRows)
            {
                int callbacks = 0;
                Action<ISystem> untypedStartup = _ => callbacks++;
                SystemRef untyped = ProcessManager.GetSystemRef(seeded);
                SeededReference typed = seeded == Fixture.GuardedRows[0]
                    ? SeededReference.Seed(ProcessManager.GetSystemRef<UIManager>(), () => callbacks++)
                    : SeededReference.Seed(ProcessManager.GetSystemRef<UINavigationManager>(), () => callbacks++);
                untyped.OnSystemStartup += untypedStartup;
                object row = registry[seeded];
                var cache = (IDictionary)Get(row, "SystemRefDictionary");
                var cacheBefore = cache.Keys.Cast<object>().ToDictionary(key => key, key => cache[key]);
                int rowCount = registry.Count;
                Fixture accepted = null;
                bool refused = false;
                try
                {
                    try { accepted = new Fixture(); }
                    catch (InvalidOperationException) { refused = true; }
                    finally { accepted?.Dispose(); }
                    Assert.That(refused, Is.True, "dormant borrowed row is refused: " + seeded);
                    Assert.That(callbacks, Is.Zero, "no foreign startup or deferred callback ran");
                    Assert.That(registry.Count, Is.EqualTo(rowCount));
                    Assert.That(registry[seeded], Is.SameAs(row));
                    Assert.That(Get(row, "SystemRef"), Is.SameAs(untyped));
                    Assert.That(untyped.IsNull() && typed.IsNull(), Is.True, "the row stays dormant");
                    Assert.That(cache.Keys.Cast<object>().ToDictionary(key => key, key => cache[key]), Is.EqualTo(cacheBefore), "typed cache unchanged");
                    Assert.That(cacheBefore.Values.Single(), Is.SameAs(typed.Reference));
                }
                finally
                {
                    untyped.OnSystemStartup -= untypedStartup;
                    typed.Release();
                    // The seeded row and its deferred action belong to this control alone.
                    registry.Remove(seeded);
                }
            }
        }

        // A typed reference seeded with a startup observer and a deferred InvokeOnValid action.
        private sealed class SeededReference
        {
            internal readonly object Reference;
            internal readonly Func<bool> IsNull;
            private readonly Action release;
            private SeededReference(object reference, Func<bool> isNull, Action release) { Reference = reference; IsNull = isNull; this.release = release; }
            internal void Release() => release();

            internal static SeededReference Seed<T>(SystemRef<T> reference, Action observed) where T : class, ISystem
            {
                Action<T> startup = _ => observed();
                reference.OnSystemStartup += startup;
                reference.InvokeOnValid(_ => observed());
                return new SeededReference(reference, reference.IsNull, () => reference.OnSystemStartup -= startup);
            }
        }

        // RetrieveLastSelected 06000007, x86-64 0x426570: TryGetSelectedWidget, then every widget
        // gets add_OnWidgetSelected; the AutoSelect write and StartCoroutine sit behind the
        // hasSelection branch (0x4265fb). Catches: writing AutoSelect without a stored index
        // (First would become true for the default out value 0), skipping the subscription,
        // or starting the selection coroutine (the inactive owner would log a Unity error).
        [Test]
        public void MissingSelectionSubscribesEachWidgetOnceAndLeavesAutoSelectUntouched()
        {
            using (var fixture = new Fixture())
            {
                fixture.First.AutoSelect = false;
                fixture.Second.AutoSelect = true;
                Assert.That(fixture.Stored(), Is.Null);
                fixture.Call("RetrieveLastSelected");
                Assert.That(fixture.Subscriptions(fixture.First), Is.EqualTo(1));
                Assert.That(fixture.Subscriptions(fixture.Second), Is.EqualTo(1));
                Assert.That(fixture.First.AutoSelect, Is.False);
                Assert.That(fixture.Second.AutoSelect, Is.True);
                Assert.That(fixture.Stored(), Is.Null);
            }
        }

        // 0x426570: with a stored index every widget is subscribed and gets
        // AutoSelect = (i == selectedIndex) (sete at 0x4266b2); no index matches 2 or -1, so no
        // coroutine starts on the inactive owner. Catches: writing only true values,
        // clamping the index, or dropping the subscription on the remembered branch.
        [Test]
        public void OutOfRangeSelectionClearsEveryAutoSelectAndStillSubscribes()
        {
            using (var fixture = new Fixture())
            {
                fixture.First.AutoSelect = true;
                fixture.Second.AutoSelect = true;
                fixture.Manager.SetSelectedWidget(fixture.Container, 2);
                fixture.Call("RetrieveLastSelected");
                Assert.That(fixture.First.AutoSelect, Is.False);
                Assert.That(fixture.Second.AutoSelect, Is.False);
                Assert.That(fixture.Subscriptions(fixture.First), Is.EqualTo(1));
                Assert.That(fixture.Subscriptions(fixture.Second), Is.EqualTo(1));

                fixture.First.AutoSelect = true;
                fixture.Second.AutoSelect = true;
                fixture.Manager.SetSelectedWidget(fixture.Container, -1);
                fixture.Call("RetrieveLastSelected");
                Assert.That(fixture.First.AutoSelect, Is.False);
                Assert.That(fixture.Second.AutoSelect, Is.False);
                Assert.That(fixture.Subscriptions(fixture.First), Is.EqualTo(2));
                Assert.That(fixture.Subscriptions(fixture.Second), Is.EqualTo(2));
            }
        }

        // 0x426570 adds a new handler on every call; OnDisable 06000008, 0x426900, removes one
        // handler per widget per call. Catches: de-duplicated subscription, OnDisable removing
        // every copy, or a remaining handler that no longer reaches OnSelected.
        [Test]
        public void RepeatedRetrievalSubscribesAgainAndOnDisableRemovesOnePerWidget()
        {
            using (var fixture = new Fixture())
            {
                fixture.Call("RetrieveLastSelected");
                fixture.Call("RetrieveLastSelected");
                Assert.That(fixture.Subscriptions(fixture.First), Is.EqualTo(2));
                Assert.That(fixture.Subscriptions(fixture.Second), Is.EqualTo(2));

                fixture.Call("OnDisable");
                Assert.That(fixture.Subscriptions(fixture.First), Is.EqualTo(1));
                Assert.That(fixture.Subscriptions(fixture.Second), Is.EqualTo(1));
                fixture.Second.OnSelect();
                Assert.That(fixture.Stored(), Is.EqualTo(1), "the remaining handler is the real OnSelected");

                fixture.Call("OnDisable");
                Assert.That(fixture.Subscriptions(fixture.First), Is.Zero);
                Assert.That(fixture.Subscriptions(fixture.Second), Is.Zero);
                fixture.First.OnSelect();
                Assert.That(fixture.Stored(), Is.EqualTo(1), "no handler is left to store index 0");
            }
        }

        // OnSelected 06000009, 0x4269b0: ReadOnlyListExtensions.FindIndex over the widgets, then
        // SetSelectedWidget only when the index is not -1 (0x426a73). Catches: last-match search,
        // storing -1 for a foreign widget, or storing without the guard.
        [Test]
        public void OnSelectedStoresFirstMatchingIndexAndIgnoresForeignWidgets()
        {
            using (var fixture = new Fixture())
            {
                UIWidgetNavigation foreign = fixture.Own("Lucid owned foreign widget").AddComponent<UIWidgetNavigation>();
                fixture.SetWidgets(fixture.Second, fixture.First, fixture.Second);
                fixture.Call("OnSelected", foreign);
                Assert.That(fixture.Stored(), Is.Null);
                fixture.Call("OnSelected", fixture.Second);
                Assert.That(fixture.Stored(), Is.EqualTo(0));
                fixture.Call("OnSelected", fixture.First);
                Assert.That(fixture.Stored(), Is.EqualTo(1));
                fixture.Call("OnSelected", foreign);
                Assert.That(fixture.Stored(), Is.EqualTo(1));
            }
        }

        // The <OnSelected>b__0 predicate (x86-64 0x426b80) is UnityEngine.Object.op_Equality, so a
        // null or destroyed argument matches the first destroyed entry, and only that.
        // Catches: reference or managed equality in the predicate (nothing would be stored).
        [Test]
        public void OnSelectedWithNullOrDestroyedWidgetMatchesTheFirstDestroyedEntry()
        {
            using (var fixture = new Fixture())
            {
                UIWidgetNavigation destroyed = fixture.Own("Lucid owned destroyed entry").AddComponent<UIWidgetNavigation>();
                UIWidgetNavigation destroyedArgument = fixture.Own("Lucid owned destroyed argument").AddComponent<UIWidgetNavigation>();
                UObject.DestroyImmediate(destroyed);
                UObject.DestroyImmediate(destroyedArgument);

                fixture.Call("OnSelected", (object)null);
                Assert.That(fixture.Stored(), Is.Null, "live entries are not Unity-null");

                fixture.SetWidgets(fixture.First, destroyed, fixture.Second, destroyed);
                fixture.Call("OnSelected", (object)null);
                Assert.That(fixture.Stored(), Is.EqualTo(1));

                fixture.SetWidgets(fixture.First, fixture.Second, destroyed);
                fixture.Call("OnSelected", destroyedArgument);
                Assert.That(fixture.Stored(), Is.EqualTo(2));
            }
        }

        // OnUIContainerClosed 06000006, 0x4268b0: UIManager.AreExclusivelyOpen(field 0x30) gates a
        // tail call to RetrieveLastSelected; the closed identifier is unused. The real UIManager
        // reads a real UIFree whose open container makes both branches observable.
        // Catches: an inverted or missing gate, or passing the closed identifier instead.
        [Test]
        public void ContainerClosedRetrievesOnlyWhenTheListedContainersAreExclusivelyOpen()
        {
            using (var fixture = new Fixture())
            {
                UIContainerIdentifier openIdentifier = fixture.OwnAsset(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                Set(openIdentifier, "m_guid", "lucid-open-container-142");
                GameObject template = fixture.Own("Lucid owned open container template");
                UIContainerSimple prefab = template.AddComponent<UIContainerSimple>();
                Set(prefab, "m_identifier", openIdentifier);
                Type freeType = typeof(UIContainerManager).Assembly.GetType("Hardlight.UIFree", true);
                var free = (UIContainerManager)Activator.CreateInstance(freeType, Instance, null, new object[] { template.transform }, null);
                UIContainer opened = free.OpenContainer(prefab);
                Assert.That(opened != null && opened.transform.parent == template.transform, "UIFree opened an owned clone");
                // UIManager.Awake fills this map; it is not run here because it also registers
                // global StackableData type operations bound to the instance.
                ((IDictionary)Get(fixture.Manager, "m_containerManagers")).Add(UIContainerBehaviour.Free, free);

                fixture.First.AutoSelect = true;
                fixture.Second.AutoSelect = true;
                fixture.Manager.SetSelectedWidget(fixture.Container, 5);
                Assert.That(fixture.Manager.AreExclusivelyOpen(Array.Empty<UIContainerIdentifier>()), Is.False);
                fixture.Call("OnUIContainerClosed", openIdentifier);
                Assert.That(fixture.Subscriptions(fixture.First), Is.Zero);
                Assert.That(fixture.First.AutoSelect && fixture.Second.AutoSelect, Is.True);

                Set(fixture.Retain, "m_selectWhenExclusivelyOpen", new[] { openIdentifier });
                Assert.That(fixture.Manager.AreExclusivelyOpen(new[] { openIdentifier }), Is.True);
                fixture.Call("OnUIContainerClosed", fixture.Identifier);
                Assert.That(fixture.Subscriptions(fixture.First), Is.EqualTo(1));
                Assert.That(fixture.Subscriptions(fixture.Second), Is.EqualTo(1));
                Assert.That(fixture.First.AutoSelect || fixture.Second.AutoSelect, Is.False);
            }
        }

        // Awake 06000004, 0x426460: GetSystem<UIManager> is stored to field 0x38, then
        // GetSystem<UINavigationManager> to 0x40, and only then add_OnContainerClosed and the
        // RetrieveLastSelected tail call. A missing navigation manager therefore throws after the
        // UIManager store and before any subscription. Catches: subscribing or retrieving before
        // the lookups, swapped lookups, or a swallowed lookup failure.
        [Test]
        public void AwakeLookupFailuresThrowBeforeAnySubscription()
        {
            Assert.That(HLUnityCore.IsNull(), Is.True, "without the logging host a null system throws");
            using (var fixture = new Fixture(assignManager: false))
            {
                var missingManager = Assert.Throws<NullReferenceException>(() => fixture.Call("Awake"));
                Assert.That(missingManager.Message, Is.EqualTo("System 'Hardlight.UIManager' is null"));
                Assert.That(Get(fixture.Retain, "m_uiManager"), Is.Null);

                fixture.RegisterManager();
                var missingNavigation = Assert.Throws<NullReferenceException>(() => fixture.Call("Awake"));
                Assert.That(missingNavigation.Message, Is.EqualTo("System 'Hardlight.UINavigationManager' is null"));
                Assert.That(Get(fixture.Retain, "m_uiManager"), Is.SameAs(fixture.Manager));
                Assert.That(Get(fixture.Retain, "m_uiNavigationManager"), Is.Null);
                Assert.That(fixture.ClosedSubscriptions(), Is.Zero);
                Assert.That(fixture.Subscriptions(fixture.First), Is.Zero);
                Assert.That(fixture.Subscriptions(fixture.Second), Is.Zero);
            }
        }

        // OnDestroy 06000005, 0x4267c0: remove_OnContainerClosed runs only when
        // GetSystemRef<UIManager>().IsValid(); the removal itself uses field 0x38. The subscription
        // is added through the public event with the component's own handler, as Awake does.
        // Catches: unconditional removal, a guard on the stored field instead of the reference,
        // or an inverted guard.
        [Test]
        public void OnDestroyUnsubscribesOnlyWhileTheUIManagerReferenceIsValid()
        {
            using (var fixture = new Fixture())
            {
                fixture.RegisterManager();
                var handler = (Action<UIContainerIdentifier>)Delegate.CreateDelegate(typeof(Action<UIContainerIdentifier>), fixture.Retain,
                    Widget.GetMethod("OnUIContainerClosed", Instance | BindingFlags.DeclaredOnly));
                fixture.Manager.OnContainerClosed += handler;
                Assert.That(fixture.ClosedSubscriptions(), Is.EqualTo(1));

                fixture.UnregisterManager();
                fixture.Call("OnDestroy");
                Assert.That(fixture.ClosedSubscriptions(), Is.EqualTo(1), "a revoked reference keeps the subscription");

                fixture.RegisterManager();
                fixture.Call("OnDestroy");
                Assert.That(fixture.ClosedSubscriptions(), Is.Zero);
            }
        }

        // Reset 06000003, 0x426410: GetComponent<UIContainer> stored to field 0x20.
        // Catches: an empty Reset or a lookup that misses the concrete subclass.
        [Test]
        public void ResetFindsTheConcreteContainer()
        {
            using (var fixture = new Fixture())
            {
                Set(fixture.Retain, "m_uiContainer", null);
                fixture.Call("Reset");
                Assert.That(Get(fixture.Retain, "m_uiContainer"), Is.SameAs(fixture.Container));
            }
        }
    }
}
