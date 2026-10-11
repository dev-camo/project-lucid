using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests.ScreenShared
{
    // Bounded rebuilt managed obligations. Original native/platform parity and
    // positive polling are separate; no original body is invoked through reflection.
    public static class ScreenManagerVerification
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static bool leaseBusy;
        private static object suiteScreenInfo;
        private static SystemRef suiteScreenRef;

        private static FieldInfo Field(Type type, string name)
        {
            for (Type at = type; at != null; at = at.BaseType)
            {
                FieldInfo field = at.GetField(name, Own);
                if (field != null) return field;
            }
            throw new AssertionException("Missing genuine field " + type + ":" + name);
        }
        private static object Read(object instance, string name) => Field(instance.GetType(), name).GetValue(instance);
        private static object Static(Type type, string name) => Field(type, name).GetValue(null);

        public static void CompleteDeclaredManagerAndEnumShapes()
        {
            Type type = typeof(ScreenManager);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoSingleton<ScreenManager>)));
            Assert.That(type.GetMethods(Own).Length, Is.EqualTo(25));
            Assert.That(type.GetConstructors(Own).Single().GetParameters(), Is.Empty);
            Assert.That((int)type.GetConstructors(Own).Single().Attributes, Is.EqualTo(6278));
            Assert.That(type.GetProperties(Own).Select(p => p.Name).OrderBy(x => x),
                Is.EqualTo(new[] { "DeviceOrientation", "Orientation" }));
            EventInfo evt = type.GetEvents(Own).Single();
            Assert.That(evt.Name, Is.EqualTo("OnSafeAreaChange"));
            Assert.That(evt.EventHandlerType, Is.EqualTo(typeof(Action<bool>)));
            Method(type, "get_Orientation", 2182, typeof(UIOrientation));
            Method(type, "set_Orientation", 2177, typeof(void), typeof(UIOrientation));
            Method(type, "get_DeviceOrientation", 2182, typeof(ScreenOrientation));
            Method(type, "set_DeviceOrientation", 2177, typeof(void), typeof(ScreenOrientation));
            Method(type, "add_OnSafeAreaChange", 2182, typeof(void), typeof(Action<bool>));
            Method(type, "remove_OnSafeAreaChange", 2182, typeof(void), typeof(Action<bool>));
            Method(type, "Awake", 196, typeof(void));
            Method(type, "GetScreenOrientation", 129, typeof(ScreenOrientation), typeof(bool));
            Method(type, "OnDestroy", 196, typeof(void));
            Method(type, "Initialise", 129, typeof(void), typeof(object));
            Method(type, "Shutdown", 129, typeof(void), typeof(object));
            Method(type, "GetUIOrientationFromScreenSize", 129, typeof(UIOrientation));
            Method(type, "GetScreenOrientationFromScreenSize", 129, typeof(ScreenOrientation));
            Method(type, "OnPropertyStoreSave", 129, typeof(void), typeof(HLPropertyList));
            Method(type, "OnPropertyStoreLoad", 129, typeof(void), typeof(HLPropertyList), typeof(bool));
            Method(type, "ChangeOrientation", 134, typeof(void), typeof(UIOrientation), typeof(Action));
            Method(type, "ForceOrientation", 129, typeof(void));
            Method(type, "ToggleOrientationLock", 134, typeof(void), typeof(bool));
            Method(type, "SetOrientationLock", 134, typeof(void), typeof(bool));
            Method(type, "ControlOrientationLock", 129, typeof(void), typeof(bool));
            Method(type, "IsOrientationLocked", 134, typeof(bool));
            Method(type, "ConvertScreenToUIOrientation", 129, typeof(UIOrientation));
            Method(type, "SetScreenToAutoRotate", 129, typeof(IEnumerator));
            Method(type, "PollScreenRotate", 129, typeof(IEnumerator));
            Method(type, "ScreenHasSafeArea", 134, typeof(bool));
            foreach (string name in new[] { "GetScreenOrientation", "Initialise", "Shutdown" })
            {
                ParameterInfo parameter = type.GetMethod(name, Own).GetParameters().Single();
                Assert.That(parameter.IsOptional && parameter.HasDefaultValue, Is.True);
                Assert.That(parameter.DefaultValue, Is.EqualTo(name == "GetScreenOrientation" ? (object)false : null));
            }
            ParameterInfo optional = type.GetMethod("ChangeOrientation", Own).GetParameters()[1];
            Assert.That(optional.IsOptional && optional.HasDefaultValue, Is.True);
            Assert.That(optional.DefaultValue, Is.Null);
            string[] fieldNames = { "DesiredOrientationsSaveKey", "AutoRotateSaveKey", "AutoRotationCheckTime",
                "m_reapplyAutoRotation", "m_wait", "m_userLockedAutoRotate", "m_initialised",
                "m_lastScreenOrientation", "m_lastScreenOrientationUpdated", "m_timedWait", "m_pollScreenRotate",
                "<Orientation>k__BackingField", "<DeviceOrientation>k__BackingField", "OnOrientationChange",
                "OnSafeAreaChange", "m_editorOrientation" };
            Assert.That(type.GetFields(Own).Select(f => f.Name).OrderBy(x => x),
                Is.EqualTo(fieldNames.OrderBy(x => x)));
            Assert.That(Field(type, "DesiredOrientationsSaveKey").GetRawConstantValue(), Is.EqualTo("desired_screen_orientation"));
            Assert.That(Field(type, "AutoRotateSaveKey").GetRawConstantValue(), Is.EqualTo("user_locked_auto_rotate"));
            Assert.That(Field(type, "AutoRotationCheckTime").GetRawConstantValue(), Is.EqualTo(0.25f));
            Assert.That(Field(type, "m_wait").FieldType, Is.EqualTo(typeof(WaitForEndOfFrame)));
            Assert.That(Field(type, "m_wait").IsInitOnly, Is.True);
            Assert.That(Field(type, "m_timedWait").FieldType, Is.EqualTo(typeof(WaitForSeconds)));
            Assert.That(Field(type, "m_timedWait").IsInitOnly, Is.True);
            Assert.That(Field(type, "OnOrientationChange").FieldType, Is.EqualTo(typeof(FastAction)));
            Assert.That(Field(type, "m_editorOrientation").GetCustomAttributesData().Single().AttributeType,
                Is.EqualTo(typeof(SerializeField)));
            Assert.That(Enum.GetUnderlyingType(typeof(UIOrientation)), Is.EqualTo(typeof(int)));
            Assert.That(Enum.GetNames(typeof(UIOrientation)), Is.EqualTo(new[] { "Uninitialised", "Portrait", "Landscape" }));
            Assert.That(Enum.GetValues(typeof(UIOrientation)).Cast<UIOrientation>().Select(v => (int)v),
                Is.EqualTo(new[] { 0, 1, 2 }));
        }

        private static void Method(Type type, string name, int flags, Type result, params Type[] parameters)
        {
            MethodInfo method = type.GetMethods(Own).Single(m => m.Name == name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), name);
            Assert.That(method.ReturnType, Is.EqualTo(result), name);
            Assert.That(method.GetParameters().Select(p => p.ParameterType), Is.EqualTo(parameters), name);
            Assert.That(method.IsGenericMethod, Is.False, name);
        }

        public static void TwoNaturalIteratorsHaveTwelveManagedRoles()
        {
            var seen = new HashSet<Type>();
            foreach (string name in new[] { "SetScreenToAutoRotate", "PollScreenRotate" })
            {
                MethodInfo method = typeof(ScreenManager).GetMethod(name, Own);
                CustomAttributeData attribute = method.GetCustomAttributesData().Single(a => a.AttributeType == typeof(IteratorStateMachineAttribute));
                Type iterator = (Type)attribute.ConstructorArguments.Single().Value;
                Assert.That(seen.Add(iterator), Is.True);
                Assert.That(iterator.DeclaringType, Is.EqualTo(typeof(ScreenManager)));
                Assert.That(iterator.BaseType, Is.EqualTo(typeof(object)));
                Assert.That(iterator.GetConstructors(Own).Single().GetParameters().Single().ParameterType, Is.EqualTo(typeof(int)));
                Assert.That(iterator.GetMethods(Own).Length, Is.EqualTo(5));
                Assert.That(iterator.GetMethods(Own).Count(m => m.Name == "MoveNext" && m.ReturnType == typeof(bool)), Is.EqualTo(1));
                Assert.That(iterator.GetMethods(Own).Count(m => m.Name.EndsWith(".Dispose", StringComparison.Ordinal) && m.ReturnType == typeof(void)), Is.EqualTo(1));
                Assert.That(iterator.GetMethods(Own).Count(m => m.Name.EndsWith(".Reset", StringComparison.Ordinal) && m.ReturnType == typeof(void)), Is.EqualTo(1));
                Assert.That(iterator.GetMethods(Own).Count(m => m.Name.EndsWith(".get_Current", StringComparison.Ordinal) && m.ReturnType == typeof(object)), Is.EqualTo(2));
                Assert.That(iterator.GetFields(Own).Select(f => f.Name).OrderBy(x => x),
                    Is.EqualTo(new[] { "<>1__state", "<>2__current", "<>4__this" }.OrderBy(x => x)));
                Assert.That(Field(iterator, "<>1__state").FieldType, Is.EqualTo(typeof(int)));
                Assert.That(Field(iterator, "<>2__current").FieldType, Is.EqualTo(typeof(object)));
                Assert.That(Field(iterator, "<>4__this").FieldType, Is.EqualTo(typeof(ScreenManager)));
            }
            // Names/ordinals and current metadata tokens are actual managed roles,
            // not normalized equivalents of original d__40/d__41 native identities.
        }

        public static void InactiveAttachmentRetainsOriginalConstructorDefaults()
        {
            Inactive(manager =>
            {
                Assert.That(manager.Orientation, Is.EqualTo(UIOrientation.Uninitialised));
                Assert.That((int)manager.DeviceOrientation, Is.EqualTo(0));
                Assert.That(manager.IsOrientationLocked(), Is.False);
                Assert.That(Read(manager, "m_initialised"), Is.EqualTo(false));
                Assert.That(Read(manager, "m_lastScreenOrientationUpdated"), Is.EqualTo(false));
                Assert.That(Read(manager, "m_reapplyAutoRotation"), Is.Null);
                Assert.That(Read(manager, "m_pollScreenRotate"), Is.Null);
                Assert.That(Read(manager, "m_wait"), Is.TypeOf<WaitForEndOfFrame>());
                Assert.That(Read(manager, "m_timedWait"), Is.TypeOf<WaitForSeconds>());
                Assert.That(manager.OnOrientationChange, Is.Null);
            });
        }
        public static void InactiveNoChangeSuppressesCompletion()
        {
            Inactive(manager =>
            {
                int completions = 0;
                Action complete = () => ++completions;
                manager.ChangeOrientation(UIOrientation.Uninitialised, complete);
                manager.ChangeOrientation(manager.Orientation, complete);
                Assert.That(completions, Is.EqualTo(0));
                Assert.That(manager.Orientation, Is.EqualTo(UIOrientation.Uninitialised));
                Assert.That(Read(manager, "m_reapplyAutoRotation"), Is.Null);
            });
        }
        public static void InactivePublicSubscriptionsRetainOwnedMembership()
        {
            Inactive(manager =>
            {
                int calls = 0;
                Action<bool> safe = value => ++calls;
                Action orientation = () => ++calls;
                manager.OnSafeAreaChange += safe;
                try
                {
                    Assert.That(((Delegate)Read(manager, "OnSafeAreaChange")).GetInvocationList(), Is.EqualTo(new Delegate[] { safe }));
                    manager.OnOrientationChange += orientation;
                    try { Assert.That(manager.OnOrientationChange.GetInvocationList(), Is.EqualTo(new[] { orientation })); }
                    finally { manager.OnOrientationChange -= orientation; }
                    Assert.That(manager.OnOrientationChange.GetInvocationList(), Is.Empty);
                }
                finally { manager.OnSafeAreaChange -= safe; }
                Assert.That(Read(manager, "OnSafeAreaChange"), Is.Null);
                Assert.That(calls, Is.EqualTo(0));
                // No selected original method dispatches OnSafeAreaChange.
            });
        }
        public static void InactiveLockAndTogglePreserveOriginalGate()
        {
            Inactive(manager =>
            {
                manager.SetOrientationLock(true);
                Assert.That(manager.IsOrientationLocked(), Is.True);
                Assert.That(Screen.autorotateToLandscapeLeft || Screen.autorotateToLandscapeRight, Is.False);
                Assert.That(Screen.autorotateToPortrait && Screen.autorotateToPortraitUpsideDown, Is.True);
                manager.ToggleOrientationLock(false);
                Assert.That(manager.IsOrientationLocked(), Is.True);
                Assert.That(Screen.autorotateToLandscapeLeft || Screen.autorotateToLandscapeRight, Is.False);
                manager.SetOrientationLock(false);
                Assert.That(manager.IsOrientationLocked(), Is.False);
                Assert.That(Screen.autorotateToLandscapeLeft && Screen.autorotateToLandscapeRight &&
                    Screen.autorotateToPortrait && Screen.autorotateToPortraitUpsideDown, Is.True);
            });
        }

        private static void Inactive(Action<ScreenManager> test)
        {
            using (var lease = new WorldLease())
            {
                GameObject host = new GameObject("Owned original ScreenManager inactive");
                host.SetActive(false);
                try
                {
                    ScreenManager manager = host.AddComponent<ScreenManager>();
                    Assert.That(Read(manager, "m_initialised"), Is.EqualTo(false), "Inactive AddComponent lifecycle prerequisite");
                    Assert.That(ScreenManager.Instance, Is.Null);
                    test(manager);
                    LogAssert.NoUnexpectedReceived();
                }
                finally { Object.DestroyImmediate(host); }
                lease.VerifyNoRegistration();
            }
        }

        public static void GenuinePlayLifecycleSavesOwnedPropertiesAndTearsDown()
        {
            Assert.That(Application.isPlaying, Is.True, "Real PlayMode host required");
#if PROJECT_LUCID_ORIGINAL_PROPERTY_STORAGE
            Assert.Fail("This controlled fixture requires the genuine reviewed default local storage selection.");
#else
            using (var lease = new WorldLease())
            {
                string file = "lucid-owned-screen-" + Guid.NewGuid().ToString("N");
                string primary = Path.Combine(Application.persistentDataPath, file);
                string backup = primary + "-backup";
                Assert.That(File.Exists(primary) || File.Exists(backup), Is.False);
                HLPropertyStore store = null;
                GameObject host = null;
                HLPropertyStore.SaveHandler observer = null;
                bool ownsFiles = false;
                try
                {
                    store = new HLPropertyStore("owned-screen-fixture", 1, file);
                    Assert.That(Read(store, "m_propertyFileStorage"), Is.TypeOf<ProjectLucid.Offline.LocalPropertySave>());
                    ownsFiles = true;
                    HLPropertyStore.Load();
                    Assert.That(store.IsLoaded && store.CanSave, Is.True);
                    store.AddProperty("desired_screen_orientation", 99);
                    store.AddProperty("user_locked_auto_rotate", true);
                    host = new GameObject("Owned original ScreenManager active");
                    host.SetActive(false);
                    ScreenManager manager = host.AddComponent<ScreenManager>();
                    host.SetActive(true);
                    lease.Adopt(manager);
                    UIOrientation fromDimensions = Screen.width > Screen.height ? UIOrientation.Landscape : UIOrientation.Portrait;
                    Assert.That(manager.Orientation, Is.EqualTo(fromDimensions));
                    Assert.That(manager.IsOrientationLocked(), Is.False, "Original load ignores saved lock and orientation keys");
                    Assert.That(Read(manager, "m_initialised"), Is.EqualTo(true));
                    Assert.That(Read(manager, "m_pollScreenRotate"), Is.Not.Null);
                    Assert.That(Read(manager, "m_reapplyAutoRotation"), Is.Not.Null);
                    int saves = 0;
                    observer = properties =>
                    {
                        ++saves;
                        Assert.That(properties.AsInt("desired_screen_orientation"), Is.EqualTo((int)manager.Orientation));
                        Assert.That(properties.AsBool("user_locked_auto_rotate"), Is.EqualTo(manager.IsOrientationLocked()));
                    };
                    HLPropertyStore.AddSaveHandler(observer);
                    Assert.That(HLPropertyStore.SaveImmediate(), Is.True);
                    Assert.That(saves, Is.EqualTo(1));
                    manager.SetOrientationLock(true);
                    Assert.That(HLPropertyStore.SaveImmediate(), Is.True);
                    Assert.That(saves, Is.EqualTo(2));
                    manager.ProcessSystemAction(SystemAction.Shutdown);
                    Assert.That(Read(manager, "m_initialised"), Is.EqualTo(false));
                    Assert.That(HLPropertyStore.IsThereAnyLoadHandler, Is.False);
                    manager.ProcessSystemAction(SystemAction.Initialise);
                    Assert.That(Read(manager, "m_initialised"), Is.EqualTo(true));
                    Assert.That(manager.Orientation, Is.EqualTo(fromDimensions));
                    Assert.That(manager.IsOrientationLocked(), Is.True);
                    Assert.That(HLPropertyStore.SaveImmediate(), Is.True);
                    Assert.That(saves, Is.EqualTo(3));
                    Object.DestroyImmediate(host);
                    host = null;
                    Assert.That(ScreenManager.Instance, Is.Null);
                    Assert.That(HLPropertyStore.IsThereAnyLoadHandler, Is.False);
                    Assert.That(((Delegate)Static(typeof(HLPropertyStore), "SaveHandlers")).GetInvocationList(),
                        Is.EqualTo(new Delegate[] { observer }));
                    lease.VerifyOwnedTeardown();
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    try { if (host != null) Object.DestroyImmediate(host); }
                    finally
                    {
                        try { if (observer != null) HLPropertyStore.RemoveSaveHandler(observer); }
                        finally
                        {
                            try { if (store != null && ownsFiles) store.WipeSaveFile(); }
                            finally { if (store != null) store.Shutdown(); }
                        }
                    }
                }
                Assert.That(File.Exists(primary) || File.Exists(backup), Is.False);
                lease.VerifyOwnedTeardown();
            }
#endif
            // Coroutines are genuinely started by Awake and stopped by OnDestroy,
            // but this synchronous case does not advance polling or assert timing.
        }

        private sealed class ScreenState
        {
            private readonly ScreenOrientation orientation = Screen.orientation;
            private readonly bool left = Screen.autorotateToLandscapeLeft;
            private readonly bool right = Screen.autorotateToLandscapeRight;
            private readonly bool portrait = Screen.autorotateToPortrait;
            private readonly bool upside = Screen.autorotateToPortraitUpsideDown;
            public void Restore()
            {
                try { Screen.orientation = orientation; }
                finally
                {
                    try { Screen.autorotateToLandscapeLeft = left; }
                    finally
                    {
                        try { Screen.autorotateToLandscapeRight = right; }
                        finally
                        {
                            try { Screen.autorotateToPortrait = portrait; }
                            finally { Screen.autorotateToPortraitUpsideDown = upside; }
                        }
                    }
                }
                Assert.That(Screen.autorotateToLandscapeLeft, Is.EqualTo(left));
                Assert.That(Screen.autorotateToLandscapeRight, Is.EqualTo(right));
                Assert.That(Screen.autorotateToPortrait, Is.EqualTo(portrait));
                Assert.That(Screen.autorotateToPortraitUpsideDown, Is.EqualTo(upside));
                Assert.That(Screen.orientation, Is.EqualTo(orientation), "Owned lease must restore the observed global orientation");
                // A platform that cannot restore the observed state fails this
                // ownership prerequisite; no positive platform parity is inferred.
            }
        }

        private sealed class WorldLease : IDisposable
        {
            private readonly IDictionary systems;
            private readonly IDictionary actions;
            private readonly object actionList;
            private readonly Dictionary<object, object> priorSystems = new Dictionary<object, object>();
            private readonly Dictionary<object, object> priorActionRows = new Dictionary<object, object>();
            private readonly Dictionary<object, Dictionary<object, object>> priorActions = new Dictionary<object, Dictionary<object, object>>();
            private readonly List<GraphObservation> foreignRefs = new List<GraphObservation>();
            private readonly GraphObservation listObservation;
            private readonly ScreenState screen;
            private ScreenManager owned;
            private bool disposed;
            private readonly string screenName = ProcessManager.GetDefaultName<ScreenManager>();
            private static readonly string[] StoreEvents = { "SaveHandlers", "ModifySaveHandlers", "ResolveConflictHandlers",
                "SaveSuccessHandlers", "AfterConflictsResolvedHandlers", "LoadHandlers" };
            public WorldLease()
            {
                Assert.That(leaseBusy, Is.False, "Concurrent suite lease is unavailable");
                Assert.That(ScreenManager.Instance, Is.Null, "Foreign singleton cannot be replaced");
                Assert.That(Static(typeof(HLPropertyStore), "s_internalInstance"), Is.Null, "Foreign store cannot be replaced");
                Assert.That(ProcessManager.IsSystemNull<HLPropertyStore>(), Is.True, "Foreign named store would receive SaveDelayed");
                foreach (string name in StoreEvents) Assert.That(Static(typeof(HLPropertyStore), name), Is.Null, "Foreign store callback " + name);
                Assert.That(Static(typeof(ProcessManager), "s_systemActionInProgress"), Is.EqualTo(false));
                systems = (IDictionary)Static(typeof(ProcessManager), "s_systemDictionary");
                actions = (IDictionary)Static(typeof(ProcessManager), "s_systemActionLookup");
                actionList = Static(typeof(ProcessManager), "s_actionList");
                // The completed global dispatcher retains its snapshot list.
                // It is observed exactly, not treated as a pending queue or reset;
                // this fixture only calls the targeted per-system action route.
                if (systems.Contains(screenName))
                {
                    Assert.That(ReferenceEquals(systems[screenName], suiteScreenInfo), Is.True, "Only suite-proven inert row can be reused");
                    Assert.That(ReferenceEquals(Read(suiteScreenInfo, "SystemRef"), suiteScreenRef), Is.True);
                    Assert.That(suiteScreenRef.IsNull(), Is.True);
                    Assert.That(((IDictionary)Read(suiteScreenInfo, "SystemRefDictionary")).Count, Is.EqualTo(0));
                    Assert.That(Read(suiteScreenRef, "OnSystemStartup"), Is.Null);
                    Assert.That(Read(suiteScreenRef, "OnSystemShutdown"), Is.Null);
                    Assert.That(Read(suiteScreenRef, "m_actionOnSystemValid"), Is.Null);
                }
                foreach (DictionaryEntry entry in systems)
                {
                    priorSystems.Add(entry.Key, entry.Value);
                    if (!Equals(entry.Key, screenName)) foreignRefs.Add(new GraphObservation(entry.Value));
                }
                foreach (DictionaryEntry row in actions)
                {
                    priorActionRows.Add(row.Key, row.Value);
                    priorActions.Add(row.Key, Copy((IDictionary)row.Value));
                }
                listObservation = new GraphObservation(actionList);
                screen = new ScreenState();
                leaseBusy = true;
            }
            private static Dictionary<object, object> Copy(IDictionary source)
            {
                var result = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in source) result.Add(entry.Key, entry.Value);
                return result;
            }
            public void Adopt(ScreenManager manager)
            {
                // Record actual owned identities before later assertions, so failed
                // behavior still has targeted teardown and sequential provenance.
                owned = manager;
                Assert.That(ReferenceEquals(ScreenManager.Instance, manager), Is.True);
                Assert.That(systems.Contains(screenName), Is.True);
                object info = systems[screenName];
                if (suiteScreenInfo != null) Assert.That(ReferenceEquals(suiteScreenInfo, info), Is.True);
                suiteScreenInfo = info;
                suiteScreenRef = (SystemRef)Read(info, "SystemRef");
                Assert.That(ReferenceEquals(suiteScreenRef.GetSafe(), manager), Is.True);
                Assert.That(((IDictionary)Read(info, "SystemRefDictionary")).Count, Is.EqualTo(0));
            }
            public void VerifyNoRegistration() { Assert.That(owned, Is.Null); Verify(false); }
            public void VerifyOwnedTeardown() { Verify(true); }
            private void Verify(bool allowOwnedRow)
            {
                Assert.That(ReferenceEquals(Static(typeof(ProcessManager), "s_systemDictionary"), systems), Is.True);
                Assert.That(ReferenceEquals(Static(typeof(ProcessManager), "s_systemActionLookup"), actions), Is.True);
                foreach (var row in priorSystems) Assert.That(ReferenceEquals(systems[row.Key], row.Value), Is.True);
                foreach (DictionaryEntry row in systems)
                    if (!priorSystems.ContainsKey(row.Key)) Assert.That(allowOwnedRow && Equals(row.Key, screenName) && ReferenceEquals(row.Value, suiteScreenInfo), Is.True);
                Assert.That(systems.Count, Is.EqualTo(priorSystems.Count + (allowOwnedRow && !priorSystems.ContainsKey(screenName) ? 1 : 0)));
                foreach (GraphObservation graph in foreignRefs) graph.Verify();
                foreach (DictionaryEntry row in actions)
                {
                    if (!priorActionRows.ContainsKey(row.Key))
                    {
                        Assert.That(allowOwnedRow && (Equals(row.Key, SystemAction.Initialise) || Equals(row.Key, SystemAction.Shutdown)), Is.True);
                        Assert.That(((IDictionary)row.Value).Count, Is.EqualTo(0));
                    }
                    else
                    {
                        Assert.That(ReferenceEquals(row.Value, priorActionRows[row.Key]), Is.True);
                        IDictionary dictionary = (IDictionary)row.Value;
                        var old = priorActions[row.Key];
                        Assert.That(dictionary.Count, Is.EqualTo(old.Count));
                        foreach (var entry in old) Assert.That(ReferenceEquals(dictionary[entry.Key], entry.Value), Is.True);
                    }
                }
                foreach (var row in priorActionRows) Assert.That(actions.Contains(row.Key), Is.True);
                Assert.That(Static(typeof(ProcessManager), "s_systemActionInProgress"), Is.EqualTo(false));
                Assert.That(ReferenceEquals(Static(typeof(ProcessManager), "s_actionList"), actionList), Is.True);
                listObservation.Verify();
                Assert.That(ScreenManager.Instance, Is.Null);
                if (allowOwnedRow)
                {
                    Assert.That(suiteScreenRef.IsNull(), Is.True);
                    Assert.That(((IDictionary)Read(suiteScreenInfo, "SystemRefDictionary")).Count, Is.EqualTo(0));
                    Assert.That(Read(suiteScreenRef, "OnSystemStartup"), Is.Null);
                    Assert.That(Read(suiteScreenRef, "OnSystemShutdown"), Is.Null);
                    Assert.That(Read(suiteScreenRef, "m_actionOnSystemValid"), Is.Null);
                }
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    Assert.That(Static(typeof(HLPropertyStore), "s_internalInstance"), Is.Null);
                    foreach (string name in StoreEvents) Assert.That(Static(typeof(HLPropertyStore), name), Is.Null);
                    Verify(owned != null || suiteScreenInfo != null && systems.Contains(screenName));
                }
                finally
                {
                    try { screen.Restore(); }
                    finally { leaseBusy = false; }
                }
            }
        }

        // Read-only graph of genuine registry SystemInfo/SystemRef/FastAction
        // fields, lists and dictionaries. Live ISystem targets are identity leaves;
        // no service object is traversed or invoked and no field is assigned.
        private sealed class GraphObservation
        {
            private readonly object root;
            private readonly List<Action> checks = new List<Action>();
            private readonly HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
            public GraphObservation(object root) { this.root = root; Capture(root); }
            private void Capture(object value)
            {
                if (value == null || value is string || value.GetType().IsValueType || value is Delegate || value is Type || value is Object || value is ISystem) return;
                if (!seen.Add(value)) return;
                if (value is IDictionary dictionary)
                {
                    DictionaryEntry[] entries = Entries(dictionary);
                    checks.Add(() =>
                    {
                        DictionaryEntry[] now = Entries(dictionary);
                        Assert.That(now.Length, Is.EqualTo(entries.Length));
                        for (int i = 0; i < entries.Length; ++i)
                        {
                            Assert.That(Equals(now[i].Key, entries[i].Key), Is.True);
                            Assert.That(ReferenceEquals(now[i].Value, entries[i].Value), Is.True);
                        }
                    });
                    foreach (DictionaryEntry entry in entries) Capture(entry.Value);
                    return;
                }
                if (value is IList list)
                {
                    object[] entries = list.Cast<object>().ToArray();
                    checks.Add(() =>
                    {
                        Assert.That(list.Count, Is.EqualTo(entries.Length));
                        for (int i = 0; i < entries.Length; ++i) Assert.That(ReferenceEquals(list[i], entries[i]), Is.True);
                    });
                    foreach (object entry in entries) Capture(entry);
                    return;
                }
                Assert.That(value.GetType().Assembly, Is.EqualTo(typeof(ProcessManager).Assembly), "Unexpected registry observation type");
                for (Type at = value.GetType(); at != null && at != typeof(object); at = at.BaseType)
                    foreach (FieldInfo field in at.GetFields(Own).Where(f => !f.IsStatic))
                    {
                        object saved = field.GetValue(value);
                        checks.Add(() =>
                        {
                            object now = field.GetValue(value);
                            Assert.That(field.FieldType.IsValueType ? Equals(now, saved) : ReferenceEquals(now, saved), Is.True, field.Name);
                        });
                        Capture(saved);
                    }
            }
            private static DictionaryEntry[] Entries(IDictionary dictionary)
            {
                var entries = new List<DictionaryEntry>();
                foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
                return entries.ToArray();
            }
            public void Verify() { foreach (Action check in checks) check(); }
        }
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
