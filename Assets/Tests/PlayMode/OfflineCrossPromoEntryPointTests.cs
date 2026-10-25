#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Offline;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // D14 policy, using the live target relationships in the release's start-menu and
    // level-results prefabs: content button, separate More Games button, and InputEvent.
    // The legacy level-results content callback has a null target; its launcher parent hides it.
    public sealed class OfflineCrossPromoEntryPointTests
    {
        private static readonly GameInput PromptInput = (GameInput)(int.MinValue + 0x502);
        private static readonly GameInput OtherInput = (GameInput)(int.MinValue + 0x503);
        private static readonly string[] InputMaps =
        {
            "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis",
            "s_onAxisStart", "s_onAxisEnd", "s_onSwipe", "s_onScrollWheel",
        };

        [UnityTest]
        public IEnumerator ShippedEntryShapesStayHiddenAcrossAuthoredReactivationAndRestoreWithAdapterOff()
        {
            using (OfflineAdapters.Override(true))
            using (var owned = new Scene(liveContentCallback: false))
            {
                owned.StartOfflineSystem();
                owned.Menu.SetActive(true);
                owned.AssertHidden();
                Assert.That(Subscriptions(owned.Prompt), Is.Empty, "hidden input never subscribes");
                Assert.That(Subscriptions(owned.UnrelatedInput), Is.EqualTo(DownMaps), "unrelated input retains the original subscription");
                Assert.That(owned.UnrelatedButton.activeInHierarchy, Is.True);
                Assert.That(owned.NestedButton.activeInHierarchy, Is.True, "another container owns its own buttons");
                Assert.That(owned.InactiveOther.activeSelf, Is.False);
                owned.ReactivateEntryOwners();
                owned.AssertHidden();
                Assert.That(Subscriptions(owned.Prompt), Is.Empty);
                yield return null;
                owned.AssertHidden();
                Assert.That(Subscriptions(owned.Prompt), Is.Empty);
                owned.AssertOfflineResources();
                owned.Menu.SetActive(false);
                Assert.That(Subscriptions(owned.UnrelatedInput), Is.Empty);
                owned.Menu.SetActive(true);
                owned.AssertHidden();
                Assert.That(Subscriptions(owned.Prompt), Is.Empty);
                Assert.That(Subscriptions(owned.UnrelatedInput), Is.EqualTo(DownMaps));
                Assert.That(owned.UnrelatedButton.activeInHierarchy, Is.True);

                // Same public SetActive calls as the authored ShowMoreGamesButton and visibility binders.
                // The retained original OnEnable path becomes reachable again when the central hook is off.
                using (OfflineAdapters.Override(false))
                {
                    owned.ReactivateEntryOwners();
                    Assert.That(owned.Launcher.gameObject.activeInHierarchy, Is.True);
                    Assert.That(owned.MoreGames.activeInHierarchy, Is.True);
                    Assert.That(owned.InitiallyInactiveButton.activeInHierarchy, Is.True);
                    Assert.That(owned.Prompt.gameObject.activeInHierarchy, Is.True);
                    Assert.That(Subscriptions(owned.Prompt), Is.EqualTo(DownMaps));
                    owned.Prompt.gameObject.SetActive(false);
                    Assert.That(Subscriptions(owned.Prompt), Is.Empty, "original OnDisable removes all four callbacks");
                    // Do not advance Update with an empty original ad array: H-065's independent tests cover that guard.
                    owned.Menu.SetActive(false);
                }
            }
        }

        [UnityTest]
        public IEnumerator HiddenLauncherReleasesDeferredStartupAndIndependentPromptBeforeTheSystemExists()
        {
            using (OfflineAdapters.Override(true))
            using (var owned = new Scene())
            {
                Assert.That(owned.PromoReference.IsNull(), Is.True);
                // The independent controller owner can enable before the launcher's parent menu.
                owned.Prompt.transform.SetParent(null, false);
                owned.Prompt.gameObject.SetActive(true);
                Assert.That(owned.Prompt.gameObject.activeSelf, Is.False);
                Assert.That(Subscriptions(owned.Prompt), Is.Empty);
                owned.Menu.SetActive(true);
                owned.AssertHidden();
                Assert.That(Read(owned.Launcher, "m_crossPromo"), Is.Null);
                owned.StartOfflineSystem();
                Assert.That(Read(owned.Launcher, "m_crossPromo"), Is.Null,
                    "deactivation ran base OnDisable and removed its pending startup observer");
                owned.ReactivateEntryOwners();
                owned.AssertHidden();
                Assert.That(Read(owned.Launcher, "m_crossPromo"), Is.SameAs(owned.Promo));
                Assert.That(Subscriptions(owned.Prompt), Is.Empty);
                yield return null;
                owned.AssertHidden();
                owned.AssertOfflineResources();
            }
        }

        private static readonly string[] DownMaps = { "s_onButtonsDown", "s_onAxisStart", "s_onSwipe", "s_onScrollWheel" };
        private static IDictionary Map(string name) => (IDictionary)typeof(ControlMapping)
            .GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static GameInput KeyInput(object key) => (GameInput)key.GetType().GetField("GameInput").GetValue(key);
        private static bool Owns(object key) => KeyInput(key) == PromptInput || KeyInput(key) == OtherInput;
        private static string[] Subscriptions(InputEvent target) => InputMaps.Where(name => Map(name).Values.Cast<object>()
            .Cast<FastAction<float>>().Any(action => action.GetInvocationList().Any(callback => ReferenceEquals(callback.Target, target)))).ToArray();

        private static object Read(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(target);
            }
            throw new MissingFieldException(name);
        }

        private static void Reference(Object target, string field, Object value)
        {
            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty(field).objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Author exactly the serialized persistent-call shape, without writing private events or invoking callbacks.
        private static void AuthorInput(InputEvent input, GameInput key, Object target, string method)
        {
            using (var serialized = new SerializedObject(input))
            {
                serialized.FindProperty("m_input").intValue = (int)key;
                SerializedProperty calls = serialized.FindProperty("m_event.m_PersistentCalls.m_Calls");
                calls.arraySize = 1;
                SerializedProperty call = calls.GetArrayElementAtIndex(0);
                call.FindPropertyRelative("m_Target").objectReferenceValue = target;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
                call.FindPropertyRelative("m_MethodName").stringValue = method;
                call.FindPropertyRelative("m_Mode").intValue = (int)PersistentListenerMode.Void;
                call.FindPropertyRelative("m_CallState").intValue = (int)UnityEventCallState.RuntimeOnly;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private sealed class Scene : IDisposable
        {
            private readonly List<GameObject> objects = new List<GameObject>();
            private readonly List<LifecycleSystemRowLease> rows = new List<LifecycleSystemRowLease>();
            private LoggingHostTests.OwnedConfiguration configuration;
            private CrossPromoConfigurationAsset asset;
            public readonly GameObject Menu, MoreGames, InitiallyInactiveButton, UnrelatedButton, NestedButton, InactiveOther;
            public readonly CrossPromoLaunchButtonSDT Launcher;
            public readonly InputEvent Prompt, UnrelatedInput;
            public readonly SystemRef<CrossPromo> PromoReference;
            public CrossPromo Promo { get; private set; }
            private GameObject content;

            public Scene(bool liveContentCallback = true)
            {
                foreach (string name in InputMaps)
                    Assert.That(Map(name).Keys.Cast<object>().Any(Owns), Is.False, "fixture input already owned: " + name);
                try
                {
                    PromoReference = Lease<CrossPromo>();
                    Lease<UIManager>();
                    Lease<AudioManager>();
                    // PlayMode's logging bootstrap owns a live configuration row; borrow it through
                    // the existing asset-override lease, never replace its system or cached references.
                    configuration = new LoggingHostTests.OwnedConfiguration();
                    asset = ScriptableObject.CreateInstance<CrossPromoConfigurationAsset>();
                    JsonUtility.FromJsonOverwrite("{\"m_canUseLocalStorage\":false}", asset);
                    configuration.Add(asset);
                    Menu = Host("Lucid owned menu", null, false);
                    Menu.AddComponent<UIContainerSimple>();
                    GameObject launcherHost = Host("[Default] HLCrossPromo_LaunchButton", Menu, false);
                    Launcher = launcherHost.AddComponent<CrossPromoLaunchButtonSDT>();
                    content = Host("content", launcherHost, true);
                    Reference(Launcher, "m_content", content);
                    Reference(Launcher, "m_placeholderContent", Host("placeholder", content, false));
                    Reference(Launcher, "m_iconImage", Host("icon", content, true).AddComponent<Image>());
                    var contentButton = content.AddComponent<Button>();
                    if (liveContentCallback)
                        UnityEventTools.AddPersistentListener(contentButton.onClick, Launcher.Action_LaunchCrossPromo);
                    else
                    {
                        // The level-results legacy call names the base method but has fileID 0 in the release.
                        using (var serialized = new SerializedObject(contentButton))
                        {
                            SerializedProperty calls = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                            calls.arraySize = 1;
                            SerializedProperty call = calls.GetArrayElementAtIndex(0);
                            call.FindPropertyRelative("m_Target").objectReferenceValue = null;
                            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(CrossPromoLaunchButton).AssemblyQualifiedName;
                            call.FindPropertyRelative("m_MethodName").stringValue = nameof(CrossPromoLaunchButton.Action_LaunchCrossPromo);
                            call.FindPropertyRelative("m_Mode").intValue = (int)PersistentListenerMode.Void;
                            call.FindPropertyRelative("m_CallState").intValue = (int)UnityEventCallState.RuntimeOnly;
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                        }
                        Assert.That(contentButton.onClick.GetPersistentTarget(0), Is.Null);
                        Assert.That(contentButton.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(CrossPromoLaunchButton.Action_LaunchCrossPromo)));
                    }
                    MoreGames = Button("button_tertiary_more_games", Menu, true, true);
                    // The level-results launcher is nested beneath this button in the shipped prefab.
                    if (!liveContentCallback) launcherHost.transform.SetParent(MoreGames.transform, false);
                    InitiallyInactiveButton = Button("inactive owned cross-promo button", Menu, false, true);
                    UnrelatedButton = Button("unrelated button", Menu, true, false);
                    InactiveOther = Button("inactive unrelated button", Menu, false, false);
                    GameObject nested = Host("another container", Menu, true);
                    nested.AddComponent<UIContainerSimple>();
                    NestedButton = Button("nested foreign button", nested, true, true);
                    Prompt = Host("group_controller_more_games_prompt", Menu, false).AddComponent<InputEvent>();
                    AuthorInput(Prompt, PromptInput, Launcher, nameof(CrossPromoLaunchButtonSDT.Action_LaunchCrossPromo));
                    UnrelatedInput = Host("unrelated input", Menu, false).AddComponent<InputEvent>();
                    AuthorInput(UnrelatedInput, OtherInput, UnrelatedInput, nameof(InputEvent.Action_Suspend));
                    launcherHost.SetActive(true);
                    Prompt.gameObject.SetActive(true);
                    UnrelatedInput.gameObject.SetActive(true);
                }
                catch { Dispose(); throw; }
            }

            private SystemRef<T> Lease<T>() where T : class, ISystem
            {
                string name = ProcessManager.GetDefaultName<T>();
                var row = LifecycleSystemRowLease.AdmitAbsentOrIdle(LifecycleSystemRowLease.SharedRegistry, name);
                rows.Add(row);
                row.CaptureCreatedRow(ProcessManager.GetSystemRef(name));
                SystemRef<T> typed = ProcessManager.GetSystemRef<T>();
                row.CaptureTypedReference(typed);
                return typed;
            }

            private GameObject Host(string name, GameObject parent, bool active)
            {
                var host = new GameObject(name);
                objects.Add(host);
                host.SetActive(false);
                if (parent != null) host.transform.SetParent(parent.transform, false);
                host.SetActive(active);
                return host;
            }

            private GameObject Button(string name, GameObject parent, bool active, bool promo)
            {
                GameObject host = Host(name, parent, active);
                var button = host.AddComponent<Button>();
                if (promo) UnityEventTools.AddPersistentListener(button.onClick, Launcher.Action_LaunchCrossPromo);
                else UnityEventTools.AddBoolPersistentListener(button.onClick, host.SetActive, false);
                Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(promo ? (Object)Launcher : host));
                return host;
            }

            public void StartOfflineSystem()
            {
                var host = Host("Lucid owned offline startup", null, false).AddComponent<HLCrossPromo>();
                var start = (IEnumerator)typeof(HLCrossPromo).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, null);
                Assert.That(start.MoveNext(), Is.False, "H-057 finishes without a downloader coroutine");
                Promo = PromoReference.Get();
                AssertOfflineResources();
            }

            public void AssertOfflineResources()
            {
                Assert.That(Promo.IsReady(), Is.True);
                Assert.That(Promo.IsEnabled, Is.False);
                Assert.That(Read(Promo, "m_resourceManager").GetType().FullName,
                    Is.EqualTo("ProjectLucid.Offline.OfflineCrossPromoResourceManager"));
                Assert.That(Read(Promo, "m_parser"), Is.TypeOf<CrossPromoDataParser>());
                Assert.That(Read(Promo, "m_syncInProgress"), Is.False);
            }

            public void ReactivateEntryOwners()
            {
                Launcher.gameObject.SetActive(true);
                MoreGames.SetActive(true);
                InitiallyInactiveButton.SetActive(true);
                Prompt.gameObject.SetActive(true);
            }

            public void AssertHidden()
            {
                Assert.That(Launcher.gameObject.activeInHierarchy, Is.False, "the launcher is hidden, including beneath its level-results button owner");
                Assert.That(content.activeInHierarchy, Is.False);
                Assert.That(MoreGames.activeSelf, Is.False, "the separate selectable owner is hidden");
                Assert.That(InitiallyInactiveButton.activeSelf, Is.False);
                Assert.That(Prompt.gameObject.activeSelf, Is.False, "the independent controller owner is hidden");
            }

            public void Dispose()
            {
                if (rows.Count == 0) return;
                LifecycleSystemRowLease cleanup = rows[0];
                for (int i = 0; i < objects.Count; ++i)
                {
                    GameObject host = objects[i];
                    cleanup.RunCleanup("destroy-owned-object", () => { if (host != null) Object.DestroyImmediate(host); });
                }
                cleanup.RunCleanup("unregister-cross-promo", () => { if (Promo != null) ProcessManager.UnregisterSystem(Promo); });
                cleanup.RunCleanup("release-owned-configuration-override", () =>
                {
                    if (configuration != null) Assert.That(configuration.Release(), Is.Empty);
                });
                cleanup.RunCleanup("destroy-configuration", () => { if (asset != null) Object.DestroyImmediate(asset); });
                cleanup.RunCleanup("release-empty-owned-input-keys", () =>
                {
                    foreach (string name in InputMaps)
                    {
                        IDictionary map = Map(name);
                        foreach (object key in map.Keys.Cast<object>().Where(Owns).ToArray())
                        {
                            Assert.That(((FastAction<float>)map[key]).GetInvocationList(), Is.Empty, "real OnDisable must remove owned callbacks");
                            map.Remove(key); // Original Unsubscribe retains empty keys; only this fixture's admitted keys are removed.
                        }
                    }
                });
                for (int i = rows.Count - 1; i >= 0; --i) rows[i].Dispose();
                foreach (LifecycleSystemRowLease row in rows) Assert.That(row.CleanupFailures, Is.Empty);
            }
        }
    }
}
#endif
