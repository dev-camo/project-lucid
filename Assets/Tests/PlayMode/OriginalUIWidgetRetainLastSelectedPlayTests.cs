using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Successful Awake and the deferred remembered selection of the original
    // Assembly-CSharp-firstpass UIWidgetRetainLastSelected (1.10.1) on an active owner, through a
    // real registered UIManager and a real constructed UINavigationManager. The firstpass type and
    // HLInputModule are reached by name because this test assembly references neither.
    public sealed class OriginalUIWidgetRetainLastSelectedPlayTests
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
        private static bool HasObservers(object reference)
        {
            Type type = reference.GetType();
            while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
            return new[] { "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" }
                .Any(name => type.GetField(name, Instance | BindingFlags.DeclaredOnly).GetValue(reference) != null);
        }

        private static int Bound(Delegate source, object target) => source == null ? 0 : source.GetInvocationList().Count(entry => ReferenceEquals(entry.Target, target));

        private static GameObject Owned(List<UObject> owned, string name, bool active = false)
        {
            var owner = new GameObject(name);
            owner.SetActive(active);
            owned.Add(owner);
            return owner;
        }

        // Awake 06000004 (x86-64 0x426460) resolves UIManager into field 0x38 and
        // UINavigationManager into 0x40, subscribes OnContainerClosed once, then tail-calls
        // RetrieveLastSelected 06000007 (0x426570). For the stored index that body sets
        // AutoSelect and starts WaitForUI with <RetrieveLastSelected>b__0 (0x426be0), which calls
        // UINavigationManager.Select with the captured widget. OnDisable 06000008 (0x426900) and
        // OnDestroy 06000005 (0x4267c0) then release both subscriptions.
        // Catches: missing or duplicated Awake subscription, selecting immediately instead of
        // deferring, selecting the wrong index or re-reading the stored index at selection time,
        // never selecting, and lifecycle methods that leave either subscription behind.
        [UnityTest]
        public IEnumerator SuccessfulAwakeDefersRealNavigationSelectOfTheRememberedWidget()
        {
            Assert.That(EventSystem.current == null, "Fixture refuses a borrowed EventSystem");
            var registry = LifecycleSystemRowLease.SharedRegistry;
            var managerLease = new LifecycleSystemRowLease(registry, ProcessManager.GetDefaultName<UIManager>());
            var navigationLease = new LifecycleSystemRowLease(registry, ProcessManager.GetDefaultName<UINavigationManager>());
            var runtimeLease = new LifecycleSystemRowLease(registry, ProcessManager.GetDefaultName<UIRuntimeConfiguration>());
            // SystemConfiguration: an absent row is leased and removed; a live one is borrowed for one
            // override; a dormant one is reused only without observers and returned to dormant.
            string configurationName = ProcessManager.GetDefaultName<SystemConfiguration>();
            LifecycleSystemRowLease configurationLease = null;
            object dormantConfigurationRow = null;
            object[] dormantConfigurationKeys = null;
            if (!registry.Contains(configurationName))
                configurationLease = new LifecycleSystemRowLease(registry, configurationName);
            else if (!((SystemRef)Get(registry[configurationName], "SystemRef")).IsValid())
            {
                dormantConfigurationRow = registry[configurationName];
                var dormantCache = (IDictionary)Get(dormantConfigurationRow, "SystemRefDictionary");
                Assert.That(HasObservers(Get(dormantConfigurationRow, "SystemRef")) || dormantCache.Values.Cast<object>().Any(HasObservers), Is.False,
                    "Fixture refuses a dormant configuration row with observers");
                dormantConfigurationKeys = dormantCache.Keys.Cast<object>().ToArray();
            }
            using (configurationLease)
            using (runtimeLease)
            using (navigationLease)
            using (managerLease)
            {
                var owned = new List<UObject>();
                var actions = (IDictionary)typeof(ProcessManager).GetField("s_systemActionLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                bool hadShutdownRow = actions.Contains(SystemAction.Shutdown);
                FastAction<UIInputType> priorInputListeners = InputBridge.OnUpdateLastInputType;
                bool priorConfiguration = !ProcessManager.IsSystemNull<SystemConfiguration>();
                SystemConfiguration createdConfiguration = null;
                StackableDataHandle configurationHandle = null;
                UIManager uiManager = null;
                UIRuntimeConfiguration runtimeConfiguration = null;
                UINavigationManager navigationManager = null;
                GameObject retainOwner = null, firstOwner = null, secondOwner = null;
                try
                {
                    // Navigation configuration with no selection inputs and an owned highlight prefab.
                    GameObject highlightTemplate = Owned(owned, "Lucid owned highlight template");
                    UIHighlight highlightPrefab = highlightTemplate.AddComponent<UIHighlight>();
                    var highlightDefinition = ScriptableObject.CreateInstance<UIHighlightDefinition>();
                    owned.Add(highlightDefinition);
                    Set(highlightDefinition, "m_prefab", highlightPrefab);
                    var navigationConfiguration = ScriptableObject.CreateInstance<UINavigationConfiguration>();
                    owned.Add(navigationConfiguration);
                    Set(navigationConfiguration, "m_defaultHighlightPrefab", highlightDefinition);
                    configurationHandle = SystemConfiguration.AddConfig<SystemConfigurationAsset>(navigationConfiguration);
                    if (!priorConfiguration) createdConfiguration = ProcessManager.GetSystem<SystemConfiguration>();
                    configurationLease?.CaptureCreatedRow(ProcessManager.GetSystemRef(configurationName));

                    // Inactive HLInputModule data: the current input type requires selection.
                    // Its Awake/OnEnable are not run; only the bridge-facing members are used.
                    Type moduleType = Type.GetType("Hardlight.HLInputModule, HLInput.Runtime", true);
                    var module = (BaseInputModule)Owned(owned, "Lucid owned input module data").AddComponent(moduleType);
                    var bridge = ScriptableObject.CreateInstance<HLInputBridge>();
                    owned.Add(bridge);
                    FieldInfo enforced = Field(moduleType, "m_inputTypesThatEnforceSelectionControl");
                    Array selectionTypes = Array.CreateInstance(enforced.FieldType.GetElementType(), 1);
                    selectionTypes.SetValue(Enum.ToObject(enforced.FieldType.GetElementType(), (int)bridge.GetLastInputType()), 0);
                    enforced.SetValue(module, selectionTypes);

                    // UIRuntimeConfiguration.Awake registers, then SetInputModule, then
                    // RegisterForInputUpdateEvents, whose static subscriptions its OnDestroy never
                    // removes. Only the first two steps are performed, through the same calls.
                    runtimeConfiguration = Owned(owned, "Lucid owned UI runtime configuration").AddComponent<UIRuntimeConfiguration>();
                    Set(runtimeConfiguration, "m_inputBridge", bridge);
                    Set(runtimeConfiguration, "m_inputModule", module);
                    runtimeLease.CaptureCreatedRow(ProcessManager.RegisterSystem(runtimeConfiguration));
                    bridge.SetInputModule(module);

                    // UIManager.Awake also registers global StackableData type operations bound to the
                    // instance, so the real component is registered directly.
                    uiManager = Owned(owned, "Lucid owned UIManager").AddComponent<UIManager>();
                    managerLease.CaptureCreatedRow(ProcessManager.RegisterSystem(uiManager));
                    managerLease.CaptureTypedReference(ProcessManager.GetSystemRef<UIManager>());

                    navigationManager = new UINavigationManager();
                    Assert.That(navigationManager.IsActive, Is.True, "fixture input type requires selection");
                    navigationLease.CaptureCreatedRow(ProcessManager.RegisterSystem(navigationManager));
                    navigationLease.CaptureTypedReference(ProcessManager.GetSystemRef<UINavigationManager>());

                    var eventSystem = Owned(owned, "Lucid owned event system", true).AddComponent<EventSystem>();
                    Assert.That(EventSystem.current, Is.SameAs(eventSystem));

                    // Widgets are enabled before the owner so their own OnEnable registration
                    // (AutoSelect false) cannot schedule a selection; the owner's coroutine is the only one.
                    firstOwner = Owned(owned, "Lucid owned widget 0");
                    secondOwner = Owned(owned, "Lucid owned widget 1");
                    UIWidgetNavigation first = firstOwner.AddComponent<UIWidgetNavigation>();
                    UIWidgetNavigation second = secondOwner.AddComponent<UIWidgetNavigation>();
                    firstOwner.SetActive(true);
                    secondOwner.SetActive(true);
                    // The container navigation stays inactive, so its own first-widget selection never runs.
                    UIContainerNavigation navigation = Owned(owned, "Lucid owned container navigation").AddComponent<UIContainerNavigation>();
                    Set(navigation, "m_navigableWidgets", new[] { first, second });

                    var identifier = ScriptableObject.CreateInstance<UIContainerIdentifier>();
                    owned.Add(identifier);
                    Set(identifier, "m_guid", "lucid-retained-container-142-play");
                    retainOwner = Owned(owned, "Lucid owned retained container");
                    UIContainerSimple container = retainOwner.AddComponent<UIContainerSimple>();
                    Set(container, "m_identifier", identifier);
                    var retain = (MonoBehaviour)retainOwner.AddComponent(Widget);
                    Set(retain, "m_uiContainer", container);
                    Set(retain, "m_uiContainerNavigation", navigation);
                    uiManager.SetSelectedWidget(container, 1);

                    int selectedFrame = -1;
                    Action<UIWidgetNavigation> observer = widget => { if (selectedFrame < 0) selectedFrame = Time.frameCount; };
                    second.OnWidgetSelected += observer;
                    first.OnWidgetSelected += observer;

                    int awakeFrame = Time.frameCount;
                    retainOwner.SetActive(true);
                    Assert.That(Get(retain, "m_uiManager"), Is.SameAs(uiManager));
                    Assert.That(Get(retain, "m_uiNavigationManager"), Is.SameAs(navigationManager));
                    Assert.That(Bound((Delegate)Get(uiManager, "OnContainerClosed"), retain), Is.EqualTo(1));
                    Assert.That(Bound((Delegate)Get(first, "OnWidgetSelected"), retain), Is.EqualTo(1));
                    Assert.That(Bound((Delegate)Get(second, "OnWidgetSelected"), retain), Is.EqualTo(1));
                    Assert.That(first.AutoSelect, Is.False);
                    Assert.That(second.AutoSelect, Is.True);
                    Assert.That(navigationManager.GetCurrentlySelected(), Is.Null, "selection is deferred");

                    // The closure captured the widget, so a later stored index does not redirect it.
                    uiManager.SetSelectedWidget(container, 0);
                    yield return null;
                    Assert.That(navigationManager.GetCurrentlySelected(), Is.Null, "first frame remains suspended");
                    for (int i = 0; i < 4 && selectedFrame < 0; i++) yield return null;
                    Assert.That(selectedFrame - awakeFrame, Is.EqualTo(2), "WaitForUI two-frame dependency");
                    Assert.That(navigationManager.GetCurrentlySelected(), Is.SameAs(second));
                    Assert.That(second.IsSelected && !first.IsSelected, Is.True);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(secondOwner));
                    Assert.That(second.GetComponentsInChildren<UIHighlight>(true).Length, Is.EqualTo(1), "Select reached the highlighter");
                    Assert.That(uiManager.TryGetSelectedWidget(container, out int stored) ? stored : -2, Is.EqualTo(1),
                        "the real selection event reached OnSelected");

                    retainOwner.SetActive(false);
                    Assert.That(Bound((Delegate)Get(first, "OnWidgetSelected"), retain), Is.Zero);
                    Assert.That(Bound((Delegate)Get(second, "OnWidgetSelected"), retain), Is.Zero);
                    Assert.That(Bound((Delegate)Get(uiManager, "OnContainerClosed"), retain), Is.EqualTo(1));
                    UObject.Destroy(retainOwner);
                    yield return null;
                    Assert.That(retain == null, Is.True);
                    Assert.That(Bound((Delegate)Get(uiManager, "OnContainerClosed"), retain), Is.Zero);
                    first.OnWidgetSelected -= observer;
                    second.OnWidgetSelected -= observer;
                }
                finally
                {
                    // Widgets deregister (and deselect) while the navigation manager and event system live.
                    managerLease.RunCleanup("destroy-retained-owner", () => { if (retainOwner != null) UObject.DestroyImmediate(retainOwner); });
                    managerLease.RunCleanup("disable-widgets", () =>
                    {
                        if (firstOwner != null) firstOwner.SetActive(false);
                        if (secondOwner != null) secondOwner.SetActive(false);
                    });
                    navigationLease.RunCleanup("shutdown-navigation", () =>
                    {
                        if (navigationManager == null) return;
                        // The real Shutdown action removes the static input-type listener.
                        ProcessManager.ProcessSystemAction(navigationManager, SystemAction.Shutdown);
                        ProcessManager.UnregisterSystem(navigationManager);
                    });
                    navigationLease.RunCleanup("restore-shared-statics", () =>
                    {
                        if (!hadShutdownRow && actions.Contains(SystemAction.Shutdown) && ((IDictionary)actions[SystemAction.Shutdown]).Count == 0)
                            actions.Remove(SystemAction.Shutdown);
                        if (priorInputListeners == null && InputBridge.OnUpdateLastInputType != null && InputBridge.OnUpdateLastInputType.GetInvocationListCount() == 0)
                            InputBridge.OnUpdateLastInputType = null;
                    });
                    managerLease.RunCleanup("unregister-ui-manager", () => { if (uiManager != null) ProcessManager.UnregisterSystem(uiManager); });
                    runtimeLease.RunCleanup("unregister-runtime-configuration", () => { if (runtimeConfiguration != null) ProcessManager.UnregisterSystem(runtimeConfiguration); });
                    runtimeLease.RunCleanup("restore-configuration", () =>
                    {
                        if (configurationHandle != null) ProcessManager.GetSystem<SystemConfiguration>().StackableData.RemoveOverrides(configurationHandle);
                        if (createdConfiguration != null) ProcessManager.UnregisterSystem(createdConfiguration);
                        if (dormantConfigurationRow == null) return;
                        object row = registry[configurationName];
                        var cache = (IDictionary)Get(row, "SystemRefDictionary");
                        if (!ReferenceEquals(row, dormantConfigurationRow) || ((SystemRef)Get(row, "SystemRef")).IsValid() ||
                            !cache.Keys.Cast<object>().SequenceEqual(dormantConfigurationKeys))
                            throw new InvalidOperationException("Borrowed dormant configuration row was not returned unchanged");
                    });
                    for (int i = owned.Count - 1; i >= 0; i--)
                    {
                        UObject item = owned[i];
                        runtimeLease.RunCleanup("destroy-owned", () => { if (item != null) UObject.DestroyImmediate(item); });
                    }
                }
            }
            foreach (var lease in new[] { managerLease, navigationLease, runtimeLease, configurationLease }.Where(lease => lease != null))
            {
                Assert.IsEmpty(lease.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
                Assert.IsEmpty(lease.DiagnosticFailures, "Fixture cleanup reporting failed; diagnostic failures remain retained.");
            }
        }
    }
}
