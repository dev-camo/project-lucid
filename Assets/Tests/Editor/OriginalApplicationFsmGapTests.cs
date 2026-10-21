using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms;
using CameraType = HardlightProject.CameraType;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned services for application FSM nodes. Every registry row a node
    // registers into is acquired absent and removed; App, AudioManager and
    // UIRuntimeConfiguration are inactive components whose Awake never runs.
    // Completed handles come from an owned ResourceManager, not Addressables.
    public sealed class OriginalApplicationFsmGapTests : OwnedGraphNamesFixture
    {
        public OriginalApplicationFsmGapTests() : base("Lucid-app-fsm-gap-") { }

        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FSMUpdateContext s_context = new FSMUpdateContext(0.25f, FSMUpdateType.Manual);

        private static string Name() => "Lucid-app-fsm-gap-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        private static FSMUser User() => new FSMUser(new FSMStorage(0));

        private static Dictionary<string, AsyncOperationHandle> Levels(IGraphStorage storage, string key, AsyncOperationHandle handle)
        {
            var levels = new Dictionary<string, AsyncOperationHandle> { { key, handle } };
            storage.SetValue(AppFSMKeys.LoadedLevels, levels);
            return levels;
        }

        // ApplicationTransitionIsSceneLoaded.DoUpdate 0x06000876, x86-64
        // 0x664f80. TryGetStorage 0x664ffc runs and can return false before the
        // IsAddressable byte (+0x31) is read 0x665009, so even the SceneManager
        // branch (GetSceneByName 0x6650d0) is gated by an absent App. The
        // addressable branch needs the dictionary 0x665058, the key 0x665077,
        // IsValid 0x665088 and IsDone 0x665097. Skipping TryGetStorage for the
        // SceneManager branch, or accepting an invalid handle, fails here.
        [Test]
        public void SceneLoadedSelectsStorageBeforeEitherSceneSource()
        {
            using (var owned = new OwnedServices())
            {
                owned.Row<App>();
                Scene runner = SceneManager.GetActiveScene();
                string priorName = runner.name;
                string sceneName = Name();
                try
                {
                    runner.name = sceneName;
                    Scene byName = SceneManager.GetSceneByName(sceneName);
                    Assert.That(byName.IsValid() && byName.isLoaded, Is.True, "the renamed runner scene is found by name");

                    FiniteStateMachine machine = Machine();
                    IFSMTransition userScene = SceneLoaded(machine, sceneName, false, false);
                    IFSMTransition appScene = SceneLoaded(machine, sceneName, true, false);
                    FSMUser user = User();
                    Assert.That(userScene.Update(user, s_context), Is.True);
                    Assert.That(appScene.Update(user, s_context), Is.False, "absent App fails before the SceneManager query");
                    Assert.That(SceneLoaded(machine, Name(), false, false).Update(user, s_context), Is.False);

                    IFSMTransition userAddressable = SceneLoaded(machine, "lucid-addressable", false, true);
                    IFSMTransition appAddressable = SceneLoaded(machine, "lucid-addressable", true, true);
                    Assert.That(userAddressable.Update(user, s_context), Is.False, "no LoadedLevels entry");
                    Dictionary<string, AsyncOperationHandle> levels = Levels(user.Storage, "lucid-other", owned.Completed());
                    Assert.That(userAddressable.Update(user, s_context), Is.False, "no scene key");
                    levels["lucid-addressable"] = default;
                    Assert.That(userAddressable.Update(user, s_context), Is.False, "invalid handle");
                    levels["lucid-addressable"] = owned.Completed();
                    Assert.That(userAddressable.Update(user, s_context), Is.True);
                    Assert.That(appAddressable.Update(user, s_context), Is.False, "user storage is not read for App storage");
                }
                finally { runner.name = priorName; }
            }
        }

        // ApplicationTransitionIsAppReady.DoUpdate 0x06000862, x86-64 0x664210.
        // After the s_common lookup 0x66438a the handle's IsValid 0x66439f (and
        // IsDone 0x6643ae only when valid) are combined without a branch around
        // GetSystemRef<App> 0x6643e2, then SystemRef.IsValid 0x6643f7 and Get
        // 0x66440d. A short-circuit && would skip the App reference lookup for
        // an invalid handle; the typed reference it caches in the owned row
        // shows the lookup happened.
        [Test]
        public void AppReadyResolvesTheAppReferenceEvenForAnInvalidCommonHandle()
        {
            using (var owned = new OwnedServices())
            {
                string appName = ProcessManager.GetDefaultName<App>();
                owned.Row<App>();
                IFSMTransition ready = ApplicationTransitionIsAppReady.ConstructInstance(Machine(), Name(), string.Empty);
                FSMUser user = User();
                Dictionary<string, AsyncOperationHandle> levels = Levels(user.Storage, "s_common", default);
                Assert.That(owned.TypedReferences(appName).Contains(appName), Is.False);
                Assert.That(ready.Update(user, s_context), Is.False);
                Assert.That(owned.TypedReferences(appName).Contains(appName), Is.True,
                    "the App reference is resolved although the common scene handle is invalid");

                App app = owned.RegisterApp();
                SetReady(app, true);
                Assert.That(ready.Update(user, s_context), Is.False, "a ready App does not satisfy an invalid scene");
                levels["s_common"] = owned.Completed();
                Assert.That(ready.Update(user, s_context), Is.True);
                SetReady(app, false);
                Assert.That(ready.Update(user, s_context), Is.False);
            }
        }

        // ApplicationTransitionIsGuiReady.DoUpdate 0x0600086d, x86-64 0x664960,
        // and TryGetStorage 0x0600086e, x86-64 0x664b30. The UseAppStorage byte
        // (+0x30) gates the App IsNull test 0x664b8c and selects App.Storage
        // 0x664bb4 or the user's storage. DoUpdate then reads s_gui 0x664ad7,
        // IsValid 0x664ae9, IsDone 0x664afb and only last the UI configuration
        // reference 0x664b12. Consulting the App with UseAppStorage false, or
        // reading the user's storage when App storage is selected, fails here.
        [Test]
        public void GuiReadySelectsStorageByFlagAndChecksUiConfigurationLast()
        {
            using (var owned = new OwnedServices())
            {
                owned.Row<App>();
                owned.Row<UIRuntimeConfiguration>();
                FiniteStateMachine machine = Machine();
                IFSMTransition userGui = ApplicationTransitionIsGuiReady.ConstructInstance(machine, Name(), "{\"UseAppStorage\":false}");
                IFSMTransition appGui = ApplicationTransitionIsGuiReady.ConstructInstance(machine, Name(), "{\"UseAppStorage\":true}");
                FSMUser user = User();
                Levels(user.Storage, "s_gui", owned.Completed());
                Assert.That(userGui.Update(user, s_context), Is.False, "no registered UI configuration");
                owned.RegisterUiConfiguration();
                Assert.That(userGui.Update(user, s_context), Is.True, "user storage is used while the App is absent");
                Assert.That(appGui.Update(user, s_context), Is.False, "absent App returns before any storage read");

                App app = owned.RegisterApp();
                Dictionary<string, AsyncOperationHandle> appLevels = Levels(app.Storage, "s_gui", default);
                FSMUser emptyUser = User();
                Assert.That(appGui.Update(emptyUser, s_context), Is.False, "invalid App-stored handle");
                appLevels["s_gui"] = owned.Completed();
                Assert.That(appGui.Update(emptyUser, s_context), Is.True);
                Assert.That(emptyUser.Storage.GetCollection(), Is.Empty, "the caller's storage is not read or defaulted");
            }
        }

        // ApplicationStatePlayAudio.DoOnEnter 0x060006fb, x86-64 0x64f060.
        // HasSource 0x64f0d8 is tested first; only a missing source with clip 0
        // (+0x24) returns 0x64f0e5. Replay false (+0x2d) calls TryGetAudioSourceData
        // 0x64f0fa and returns only when IsPlayingClip 0x64f10c is true. One-shot
        // (+0x2c) calls PlayOneShotAtSource 0x64f12f with pan 0 and volume 1.0.
        // Suppressing every clip on a missing source, suppressing clip 0 on an
        // existing source, or returning early for a present but silent source
        // with Replay false fails here. The owned manager has no clip handler,
        // so a reached clip load stops with the rebuilt managed fault.
        [Test]
        public void PlayAudioSuppressesOnlyZeroClipForAMissingSource()
        {
            using (var log = new OwnedCoreErrorLog())
            using (var owned = new OwnedServices())
            {
                AudioManager audio = owned.RegisterAudioManager();
                FiniteStateMachine machine = Machine();
                FSMUser user = User();

                PlayAudio(machine, "None", "Background", true, true).OnEnter(user, null);
                Assert.That(log.Errors, Is.Empty, "missing source and clip 0 return first");

                Assert.Throws<NullReferenceException>(() =>
                    PlayAudio(machine, "AmbientBossCrabOcean", "Background", true, true).OnEnter(user, null));
                Assert.That(log.Errors.Count, Is.EqualTo(1));
                StringAssert.StartsWith("Cannot load clip, the source ", log.Errors[0]);
                StringAssert.EndsWith(" does not exist.:0", log.Errors[0]);

                HLAudioSourceData source = owned.AddSource(audio, HLAudioSourceIdentifier.Background);
                log.Errors.Clear();
                PlayAudio(machine, "None", "Background", true, true).OnEnter(user, null);
                Assert.That(log.Errors.Count, Is.EqualTo(1), "an existing source still reaches the zero-clip diagnostic");
                StringAssert.StartsWith("Will not play the given audio clip at source ", log.Errors[0]);
                StringAssert.EndsWith(", no clip was specified.:0", log.Errors[0]);

                log.Errors.Clear();
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.False);
                Assert.Throws<NullReferenceException>(() =>
                    PlayAudio(machine, "AmbientBossCrabOcean", "Background", true, false).OnEnter(user, null));
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.True, "a silent source does not stop a non-replay play");
                Assert.That(log.Errors, Is.Empty);
            }
        }

        // ApplicationStateUnloadScene.DoOnEnter 0x06000774, x86-64 0x655b00:
        // base DoOnEnter 0x655b8c, Storage and GetValue 0x655cbe, TryGetValue
        // 0x655cdd, AddressableManager.UnloadSceneAndClearResources 0x655d01,
        // Dictionary.Remove 0x655d17, then Storage again and SetValue 0x655e39.
        // The present-key body needs the process-wide Addressables instance, so
        // this compares the rebuilt IL call order instead of entering it.
        // Republishing before Remove, or a cached scene name, fails here.
        [Test]
        public void UnloadScenePresentKeyUnloadsRemovesThenRepublishes()
        {
            MethodInfo enter = typeof(ApplicationStateUnloadScene).GetMethod("DoOnEnter", Instance | BindingFlags.DeclaredOnly);
            Assert.That(enter, Is.Not.Null);
            List<Instruction> body = Read(enter);
            List<Instruction> calls = body.Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt).ToList();
            Type levels = typeof(Dictionary<string, AsyncOperationHandle>);
            string[] expected =
            {
                typeof(FSMState).FullName + "::DoOnEnter",
                typeof(IGraphUser).FullName + "::get_Storage",
                typeof(IGraphStorage).FullName + "::GetValue<" + levels.FullName + ">",
                levels.FullName + "::TryGetValue",
                typeof(AddressableManager).FullName + "::UnloadSceneAndClearResources",
                levels.FullName + "::Remove",
                typeof(IGraphUser).FullName + "::get_Storage",
                typeof(IGraphStorage).FullName + "::SetValue<" + levels.FullName + ">",
            };
            Assert.That(calls.Select(c => Describe(enter, c)).ToArray(), Is.EqualTo(expected));

            Instruction remove = calls[5];
            Instruction argument = body[body.IndexOf(remove) - 1];
            Assert.That(argument.Code, Is.EqualTo(OpCodes.Ldfld));
            FieldInfo read = enter.Module.ResolveField(argument.Operand);
            Assert.That(read.DeclaringType, Is.EqualTo(typeof(ApplicationStateUnloadScene)));
            Assert.That(read.Name, Is.EqualTo("m_sceneName"), "Remove rereads the scene field");
            int unload = body.IndexOf(calls[4]);
            Assert.That(body.Skip(unload).Take(body.IndexOf(remove) - unload).Count(i => i.Code == OpCodes.Ldfld), Is.EqualTo(1));

            // One conditional skips the whole present-key block; nothing gates
            // the republish once the unload has been requested.
            List<Instruction> conditionals = body.Where(IsConditional).ToList();
            Assert.That(conditionals.Count, Is.EqualTo(1));
            Assert.That(body.IndexOf(conditionals[0]), Is.LessThan(unload));
            Assert.That(conditionals[0].Target, Is.GreaterThan(calls[7].Offset));
            Assert.That(enter.GetMethodBody().ExceptionHandlingClauses, Is.Empty);
        }

        // ApplicationStateLoadAssets.DoOnEnter 0x06000695, x86-64 0x648C00, reads
        // Social.localUser and passes it the cached <>c.<DoOnEnter>b__2_0 before any
        // other call. Offline builds keep that statement behind the OfflineAdapters.Active
        // hook (H-017) and report a failed login through the same GameCenter route
        // (#237), as GameCenter.NativeLoginRequest does. The owned Social.Active
        // throws when it is reached. The absent owned GameCenter row defers the
        // notification on its typed reference, and entry then faults at the App
        // cast that comes before LoadDefinitionData. Calling platform
        // authentication, dropping the notification or passing true fails here.
        [Test]
        public void LoadAssetsOfflineNotifiesGameCenterWithoutPlatformAuthentication()
        {
            using (var owned = new OwnedServices())
            using (var social = new OwnedSocialPlatform())
            {
                owned.Row<GameCenter>();
                // Negative control: a direct static-facade call reaches the owned provider.
                Assert.Throws<SocialPlatformReached>(() => Social.localUser.Authenticate(_ => { }));
                Assert.That((social.LocalUserReads, social.Authentications), Is.EqualTo((1, 1)));
                social.Reset();

                FiniteStateMachine machine = Machine();
                FSMUser user = User();
                IFSMState state = ApplicationStateLoadAssets.ConstructInstance(machine, Name(), null);
                Assert.Throws<InvalidCastException>(() => state.OnEnter(user, null));
                Assert.That((social.LocalUserReads, social.Authentications), Is.EqualTo((0, 0)), "no platform authentication");

                SystemRef<GameCenter> reference = ProcessManager.GetSystemRef<GameCenter>(null, false);
                Assert.That(reference.IsNull(), Is.True);
                var pending = (FastAction<GameCenter>)typeof(SystemRef<GameCenter>)
                    .GetField("m_actionOnSystemValid", Instance).GetValue(reference);
                Assert.That(pending, Is.Not.Null, "the GameCenter notification is deferred on the typed reference");
                List<Action<GameCenter>> listeners = pending.GetInvocationList();
                Assert.That(listeners.Count, Is.EqualTo(1), "exactly one notification");
                object closure = listeners[0].Target;
                Type closureOwner = closure.GetType().DeclaringType;
                Assert.That(closureOwner, Is.Not.Null, "the notification closure is nested in the adapter");
                Assert.That(closureOwner.FullName, Is.EqualTo("ProjectLucid.Offline.LocalGameCenterLifecycle"), "the adapter's login report, not the original callback");
                Assert.That(closure.GetType().GetField("loginSuccess").GetValue(closure), Is.EqualTo(false));
                Assert.That(user.Storage.GetCollection(), Is.Empty);
            }
        }

        // The offline DoOnEnter reads the central switch, reports a failed login through
        // LocalGameCenterLifecycle.ReportLogin(false) (H-017), and then, behind the
        // untaken original branch, still holds the original Social.localUser.Authenticate
        // statement with its cached <>9__2_0 callback. After the hook it runs the native
        // order: GetAs<App>, DataManager.LoadDefinitionData, GetSystem<SaveManager>,
        // InitialiseSaveData and RunCoroutine. The offline branch calls no Social member.
        [Test]
        public void LoadAssetsOfflineEntryReportsLoginThenLoadsInTheOriginalOrder()
        {
            MethodInfo enter = typeof(ApplicationStateLoadAssets).GetMethod("DoOnEnter", Instance | BindingFlags.DeclaredOnly);
            Assert.That(enter, Is.Not.Null);
            List<Instruction> body = Read(enter);
            List<Instruction> calls = body.Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt).ToList();
            string[] expected =
            {
                "ProjectLucid.Offline.OfflineAdapters::get_Active",
                "ProjectLucid.Offline.LocalGameCenterLifecycle::ReportLogin",
                typeof(Social).FullName + "::get_localUser",
                typeof(ILocalUser).FullName + "::Authenticate",
                typeof(IGraphUserExtensions).FullName + "::GetAs<" + typeof(App).FullName + ">",
                typeof(App).FullName + "::get_DataManager",
                typeof(DataManager).FullName + "::LoadDefinitionData",
                typeof(ProcessManager).FullName + "::GetSystem<" + typeof(SaveManager).FullName + ">",
                typeof(ApplicationStateLoadAssets).FullName + "::InitialiseSaveData",
                typeof(Hardlight.Utils.CoroutineUtils).FullName + "::RunCoroutine",
            };
            Assert.That(calls.Select(c => Describe(enter, c)).ToArray(), Is.EqualTo(expected));

            Instruction cache = body.First(i => i.Code == OpCodes.Ldsfld);
            Assert.That(enter.Module.ResolveField(cache.Operand).Name, Is.EqualTo("<>9__2_0"), "the original cached delegate");
            Assert.That(body.IndexOf(cache), Is.GreaterThan(body.IndexOf(calls[1])), "the offline report precedes the original branch");
            Assert.That(body[body.IndexOf(calls[1]) - 1].Code, Is.EqualTo(OpCodes.Ldc_I4_0), "the offline report is a failed login");
            Assert.That(body.IndexOf(calls[0]), Is.LessThan(body.IndexOf(calls[1])), "the switch is read first");
        }

        // Source-preservation self-check, not behaviour evidence. The original statement that
        // 0x648C00 translates (Social.get_localUser, then the lazily cached <>c.<>9__2_0
        // Action<bool>, then ILocalUser.Authenticate, before GetAs<App>) stays compiled in the
        // else branch of the offline hook H-017. Whitespace and line wrapping are ignored. The
        // compiled order of the original statement is pinned by the IL test above.
        [Test]
        public void LoadAssetsOriginalGameCenterSourceBranchIsUnchanged()
        {
            string path = Path.Combine(Application.dataPath, "Scripts", "Game", "Application", "FiniteStateMachine", "ApplicationStateLoadAssets.cs");
            string[] lines = File.ReadAllLines(path).Select(l => l.Trim()).ToArray();
            int hook = Array.IndexOf(lines, "if (ProjectLucid.Offline.OfflineAdapters.Active)");
            Assert.That(hook, Is.GreaterThanOrEqualTo(0));
            Assert.That(Array.IndexOf(lines, "if (ProjectLucid.Offline.OfflineAdapters.Active)", hook + 1), Is.EqualTo(-1), "one hook");
            Assert.That(lines[hook + 1], Is.EqualTo("ProjectLucid.Offline.LocalGameCenterLifecycle.ReportLogin(false);"));
            Assert.That(lines[hook + 2], Is.EqualTo("else"));
            string original = string.Concat(lines[hook + 3].Where(c => !char.IsWhiteSpace(c)));
            Assert.That(original, Is.EqualTo(
                "Social.localUser.Authenticate(loginSuccess=>ProcessManager.GetSystemRef<GameCenter>().InvokeOnValid(gameCenter=>gameCenter.OnLoginComplete(loginSuccess)));"));
        }

        // Prepared Application_BootFlow.json (FSMApplication_BootFlow, LoadAssets,
        // a LoadingGroup member) and Application.json (FSMApplication,
        // FastLoadAssets) name ApplicationStateLoadAssets. The sha256 values are
        // those of the 2026-10-05 reference export's StreamingAssets copies. Each
        // authored entry is constructed through the genuine FSMClassFactory and
        // entered under the owned throwing Social.Active: neither reads
        // Social.localUser, and each reaches the App cast that precedes
        // LoadDefinitionData. Graph traversal, definition loading, save
        // initialisation and local readiness are not exercised here.
        [Test]
        public void LoadAssetsAuthoredBootAndFastLoadStatesEnterWithoutPlatformAuthentication()
        {
            Assert.That(ProjectLucid.Offline.OfflineAdapters.Active, Is.True, "Owned offline case requires the offline adapters.");
            var authored = new[]
            {
                ("Application_BootFlow.json", "a45f7f51f44e1ce3d4dfe415bdf849856e30ec7c7442ac856493d544b7d92879", "FSMApplication_BootFlow", "LoadAssets"),
                ("Application.json", "5471978370b5743cc3f2ed7bf32f219026d3ef3abe0db811a550da0b497eac2f", "FSMApplication", "FastLoadAssets"),
            };
            foreach ((string file, string sha256, string machineName, string stateName) in authored)
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "FSM", "Application", file));
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    Assert.That(string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2"))), Is.EqualTo(sha256), file);
                var graphs = JsonUtility.FromJson<FSMClassFactory.JSONMultipleFSMs>(System.Text.Encoding.UTF8.GetString(bytes));
                FSMClassFactory.JSONFiniteStateMachineClass graph = graphs.FSMs.Single(m => m.Name == machineName);
                FSMClassFactory.JSONStateClass[] loads = graph.States.Where(st => st.Class == nameof(ApplicationStateLoadAssets)).ToArray();
                Assert.That(loads.Select(st => st.Name), Is.EqualTo(new[] { stateName }), file);
                Assert.That(loads[0].CtorArgs, Is.Empty);
                if (stateName == "LoadAssets")
                {
                    FSMClassFactory.JSONStateClass group = graph.States.Single(st => st.Name == "LoadingGroup");
                    Assert.That(group.Class, Is.EqualTo(nameof(FSMStateGroup)));
                    Assert.That(JsonUtility.FromJson<GroupArgs>(group.CtorArgs).States, Does.Contain(stateName));
                }

                using (var owned = new OwnedServices())
                using (var social = new OwnedSocialPlatform())
                {
                    owned.Row<GameCenter>();
                    // A fresh state name keeps the authored names out of the global lookup.
                    IFSMState state = FSMClassFactory.ConstructState(Machine(), loads[0].Class, new FSMIdentifier(Name()), loads[0].CtorArgs);
                    Assert.That(state, Is.TypeOf<ApplicationStateLoadAssets>(), file);
                    FSMUser user = User();
                    Assert.Throws<InvalidCastException>(() => state.OnEnter(user, null), file);
                    Assert.That((social.LocalUserReads, social.Authentications), Is.EqualTo((0, 0)), file);
                }
            }
        }

        // With a registered offline GameCenter, the synchronous callback reaches
        // OnLoginComplete(false) and LocalGameCenterLifecycle.Begin during entry.
        // Control: a running lifecycle leaves the login pending (no App) and entry
        // reaches the App cast before LoadDefinitionData. After Stop, the state a
        // failed provider callback also leaves, Begin throws and that fault escapes
        // DoOnEnter before the cast, so definitions and save initialisation do not
        // start. The fault propagates as other OnLoginComplete faults do; nothing
        // catches it.
        [Test]
        public void LoadAssetsOfflineEntryPropagatesAStoppedLocalLifecycleBeforeDefinitionLoading()
        {
            Assert.That(ProjectLucid.Offline.OfflineAdapters.Active, Is.True, "Owned offline case requires the offline adapters.");
            Assert.That(ProcessManager.IsSystemNull<App>(), Is.True, "no foreign live App for the pending login to reach");
            // GameCenter (Initialise, AppShutdown, Update) and the GameCenterLocalPlayer
            // it constructs (AppInitialise, AppShutdown) subscribe to these actions,
            // creating any missing row; own them before the constructor runs.
            var actionRows = new VerificationFixtureOwnership.ActionRows(
                SystemAction.Initialise, SystemAction.AppInitialise, SystemAction.AppShutdown, SystemAction.Update);
            GameCenter gameCenter = null;
            GameCenterLocalPlayer localPlayer = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                using (var owned = new OwnedServices())
                using (var social = new OwnedSocialPlatform())
                {
                    owned.ReadOnlyRow<App>();
                    owned.Row<GameCenter>();
                    owned.Row<GameCenterLocalPlayer>();
                    try
                    {
                        gameCenter = new GameCenter();
                        Assert.That(ProcessManager.GetSystemSafe<GameCenter>(null, false), Is.SameAs(gameCenter));
                        object lifecycle = typeof(GameCenter).GetField("m_offlineLifecycle", Instance).GetValue(gameCenter);
                        FieldInfo pending = lifecycle.GetType().GetField("complete", Instance);

                        IFSMState running = ApplicationStateLoadAssets.ConstructInstance(Machine(), Name(), null);
                        Assert.Throws<InvalidCastException>(() => running.OnEnter(User(), null));
                        Assert.That(pending.GetValue(lifecycle), Is.Not.Null, "the local login is pending, not published");
                        Assert.That(gameCenter.SystemIsReady, Is.False);

                        lifecycle.GetType().GetMethod("Stop", Instance).Invoke(lifecycle, null);
                        IFSMState stopped = ApplicationStateLoadAssets.ConstructInstance(Machine(), Name(), null);
                        var fault = Assert.Throws<InvalidOperationException>(() => stopped.OnEnter(User(), null));
                        Assert.That(fault.Message, Is.EqualTo("Local account lifecycle is stopped."));
                        Assert.That(gameCenter.SystemIsReady, Is.False);
                        Assert.That((social.LocalUserReads, social.Authentications), Is.EqualTo((0, 0)), "no platform authentication");
                    }
                    finally
                    {
                        // GameCenter registers itself and its local player into the owned rows.
                        localPlayer = ProcessManager.GetSystemSafe<GameCenterLocalPlayer>(null, false);
                        owned.AdoptCurrent<GameCenterLocalPlayer>();
                        owned.AdoptCurrent<GameCenter>();
                    }
                }
            },
            () =>
            {
                // Safety net if row disposal throws before unregistering either system.
                if (localPlayer != null) ProcessManager.UnsubscribeFromAllActions(localPlayer);
                if (gameCenter != null) ProcessManager.UnsubscribeFromAllActions(gameCenter);
            },
            actionRows.Dispose);
        }

        // Issue 415: ApplicationStateOpenGameCenterPage.DoOnEnter calls these two GameCenter methods.
        // The original bodies call Social.ShowAchievementsUI / Social.ShowLeaderboardUI; on the shipped
        // macOS player GameCenterPlatform's versions (0x06000066 / 0x06000067) run VerifyAuthentication
        // (0x06000065), which logs "Must authenticate first" and shows nothing without a logged-in
        // account. Offline (hooks H-026, H-027) the local profile is never logged in, so the hooks take
        // that path: the same log, no platform call, no exception, no input change. Control: with the
        // adapters overridden off, the retained original bodies reach the owned social platform.
        [Test]
        public void GameCenterPageRequestsTakeTheOriginalNotLoggedInPathOffline()
        {
            Assert.That(ProjectLucid.Offline.OfflineAdapters.Active, Is.True, "Owned offline case requires the offline adapters.");
            var actionRows = new VerificationFixtureOwnership.ActionRows(
                SystemAction.Initialise, SystemAction.AppInitialise, SystemAction.AppShutdown, SystemAction.Update);
            GameCenter gameCenter = null;
            GameCenterLocalPlayer localPlayer = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                using (var owned = new OwnedServices())
                using (var social = new OwnedSocialPlatform())
                {
                    owned.ReadOnlyRow<App>();
                    owned.Row<GameCenter>();
                    owned.Row<GameCenterLocalPlayer>();
                    try
                    {
                        gameCenter = new GameCenter();
                        var exclusive = new HashSet<GameInput>(OwnedControlInputs.Exclusive());
                        UnityEngine.TestTools.LogAssert.Expect(LogType.Log, "Must authenticate first");
                        gameCenter.ShowAchievementPage();
                        UnityEngine.TestTools.LogAssert.Expect(LogType.Log, "Must authenticate first");
                        gameCenter.ShowLeaderboardPage();
                        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                        Assert.That(OwnedControlInputs.Exclusive().SetEquals(exclusive), Is.True, "no input change");
                        Assert.That((social.LocalUserReads, social.Authentications), Is.EqualTo((0, 0)), "no platform query");
                        using (ProjectLucid.Offline.OfflineAdapters.Override(false))
                        {
                            Assert.Throws<SocialPlatformReached>(() => gameCenter.ShowAchievementPage(), "original achievements body");
                            Assert.Throws<SocialPlatformReached>(() => gameCenter.ShowLeaderboardPage(), "original leaderboard body");
                        }
                    }
                    finally
                    {
                        localPlayer = ProcessManager.GetSystemSafe<GameCenterLocalPlayer>(null, false);
                        owned.AdoptCurrent<GameCenterLocalPlayer>();
                        owned.AdoptCurrent<GameCenter>();
                    }
                }
            },
            () =>
            {
                if (localPlayer != null) ProcessManager.UnsubscribeFromAllActions(localPlayer);
                if (gameCenter != null) ProcessManager.UnsubscribeFromAllActions(gameCenter);
            },
            actionRows.Dispose);
        }

        [Serializable]
        private sealed class GroupArgs
        {
            public List<string> States;
        }

        private sealed class SocialPlatformReached : Exception { }

        // Owns Social.Active through the raw ActivePlatform._active field, so a
        // platform that was never selected is restored as unselected.
        private sealed class OwnedSocialPlatform : ISocialPlatform, ILocalUser, IDisposable
        {
            private static readonly FieldInfo s_active = typeof(Social).Assembly
                .GetType("UnityEngine.SocialPlatforms.ActivePlatform", true)
                .GetField("_active", BindingFlags.Static | BindingFlags.NonPublic);
            private readonly object m_prior;
            public int LocalUserReads { get; private set; }
            public int Authentications { get; private set; }

            public OwnedSocialPlatform()
            {
                Assert.That(s_active, Is.Not.Null, "ActivePlatform._active");
                m_prior = s_active.GetValue(null);
                Social.Active = this;
            }

            public void Reset() { LocalUserReads = 0; Authentications = 0; }
            public void Dispose() => s_active.SetValue(null, m_prior);

            private Exception Authenticated() { Authentications++; return new SocialPlatformReached(); }

            public ILocalUser localUser { get { LocalUserReads++; return this; } }
            public void Authenticate(Action<bool> callback) => throw Authenticated();
            public void Authenticate(Action<bool, string> callback) => throw Authenticated();
            void ISocialPlatform.Authenticate(ILocalUser user, Action<bool> callback) => throw Authenticated();
            void ISocialPlatform.Authenticate(ILocalUser user, Action<bool, string> callback) => throw Authenticated();
            public void LoadFriends(Action<bool> callback) => throw new SocialPlatformReached();
            public IUserProfile[] friends => throw new SocialPlatformReached();
            public bool authenticated => throw new SocialPlatformReached();
            public bool underage => throw new SocialPlatformReached();
            public string userName => throw new SocialPlatformReached();
            public string id => throw new SocialPlatformReached();
            public string gameId => throw new SocialPlatformReached();
            public bool isFriend => throw new SocialPlatformReached();
            public UserState state => throw new SocialPlatformReached();
            public Texture2D image => throw new SocialPlatformReached();
            public void LoadUsers(string[] userIDs, Action<IUserProfile[]> callback) => throw new SocialPlatformReached();
            public void ReportProgress(string achievementID, double progress, Action<bool> callback) => throw new SocialPlatformReached();
            public void LoadAchievementDescriptions(Action<IAchievementDescription[]> callback) => throw new SocialPlatformReached();
            public void LoadAchievements(Action<IAchievement[]> callback) => throw new SocialPlatformReached();
            public IAchievement CreateAchievement() => throw new SocialPlatformReached();
            public void ReportScore(long score, string board, Action<bool> callback) => throw new SocialPlatformReached();
            public void LoadScores(string leaderboardID, Action<IScore[]> callback) => throw new SocialPlatformReached();
            public ILeaderboard CreateLeaderboard() => throw new SocialPlatformReached();
            public void ShowAchievementsUI() => throw new SocialPlatformReached();
            public void ShowLeaderboardUI() => throw new SocialPlatformReached();
            void ISocialPlatform.LoadFriends(ILocalUser user, Action<bool> callback) => throw new SocialPlatformReached();
            void ISocialPlatform.LoadScores(ILeaderboard board, Action<bool> callback) => throw new SocialPlatformReached();
            bool ISocialPlatform.GetLoading(ILeaderboard board) => throw new SocialPlatformReached();
        }

        private static IFSMTransition SceneLoaded(FiniteStateMachine machine, string scene, bool useApp, bool addressable) =>
            ApplicationTransitionIsSceneLoaded.ConstructInstance(machine, Name(),
                "{\"SceneName\":\"" + scene + "\",\"UseAppStorage\":" + (useApp ? "true" : "false") +
                ",\"IsAddressable\":" + (addressable ? "true" : "false") + "}");

        // FSMState_PublishStateMessage.DoOnEnter 0x0600232c, x86-64 0x584670: after
        // FSMState.DoOnEnter it resolves ProcessManager.GetSystem<App> (0x98ea70) and
        // App.Storage (+0x20), never the entering user's storage, and tail-calls
        // ListExtensions.AddUnique<object> (0x96ffa0). Publishing into the user's list, or
        // adding without the uniqueness check, fails here.
        [Test]
        public void PublishStateMessageAddsToAppStorageOnceAndLeavesTheUsersList()
        {
            using (var owned = new OwnedServices())
            {
                owned.Row<App>();
                App app = owned.RegisterApp();
                ApplicationStateEvent stateEvent = ScriptableObject.CreateInstance<ApplicationStateEvent>();
                try
                {
                    stateEvent.name = Name();
                    var appEvents = new List<ApplicationStateEvent>();
                    app.Storage.SetValue(AppFSMKeys.StateEvents, appEvents);
                    FSMUser user = User();
                    var userEvents = new List<ApplicationStateEvent>();
                    user.Storage.SetValue(AppFSMKeys.StateEvents, userEvents);
                    IFSMState publish = FSMState_PublishStateMessage.ConstructInstance(Machine(), Name(), StateMessageJson(stateEvent));
                    publish.OnEnter(user, null);
                    publish.OnEnter(user, null);
                    Assert.That(app.Storage.GetValueOnly<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents), Is.SameAs(appEvents));
                    Assert.That(appEvents.Count, Is.EqualTo(1), "entering twice adds the event once");
                    Assert.That(appEvents[0], Is.SameAs(stateEvent), "the factory resolved the named event");
                    Assert.That(userEvents, Is.Empty, "the user's own list is not the target");
                }
                finally { Object.DestroyImmediate(stateEvent); }
            }
        }

        // FSMState_RemoveStateMessage.DoOnEnter 0x06002330, x86-64 0x584920: the same
        // App.Storage lookup, then a tail call to List<object>.Remove (0x15f2540) whose
        // result is unused. Removing from the user's list, or faulting when the event is
        // already absent, fails here.
        [Test]
        public void RemoveStateMessageRemovesFromAppStorageAndAcceptsAnAbsentEvent()
        {
            using (var owned = new OwnedServices())
            {
                owned.Row<App>();
                App app = owned.RegisterApp();
                ApplicationStateEvent stateEvent = ScriptableObject.CreateInstance<ApplicationStateEvent>();
                try
                {
                    stateEvent.name = Name();
                    var appEvents = new List<ApplicationStateEvent> { stateEvent };
                    app.Storage.SetValue(AppFSMKeys.StateEvents, appEvents);
                    FSMUser user = User();
                    var userEvents = new List<ApplicationStateEvent> { stateEvent };
                    user.Storage.SetValue(AppFSMKeys.StateEvents, userEvents);
                    IFSMState remove = FSMState_RemoveStateMessage.ConstructInstance(Machine(), Name(), StateMessageJson(stateEvent));
                    remove.OnEnter(user, null);
                    Assert.That(appEvents, Is.Empty);
                    Assert.That(userEvents.Count, Is.EqualTo(1), "the user's own list is not the target");
                    Assert.DoesNotThrow(() => remove.OnEnter(user, null), "an absent event is a no-op");
                    Assert.That(appEvents, Is.Empty);
                }
                finally { Object.DestroyImmediate(stateEvent); }
            }
        }

        // FSMTransitionStateMessage.DoUpdate 0x06002372, x86-64 0x587980: the base
        // DoUpdate result is discarded and the transition returns App.Storage's
        // List<object>.Remove (0x15f2540) result, so it fires once per published event and
        // consumes it. An event only in the user's list never fires it.
        [Test]
        public void StateMessageTransitionFiresOncePerPublishedAppEvent()
        {
            using (var owned = new OwnedServices())
            {
                owned.Row<App>();
                App app = owned.RegisterApp();
                ApplicationStateEvent stateEvent = ScriptableObject.CreateInstance<ApplicationStateEvent>();
                try
                {
                    stateEvent.name = Name();
                    var appEvents = new List<ApplicationStateEvent>();
                    app.Storage.SetValue(AppFSMKeys.StateEvents, appEvents);
                    FSMUser user = User();
                    var userEvents = new List<ApplicationStateEvent> { stateEvent };
                    user.Storage.SetValue(AppFSMKeys.StateEvents, userEvents);
                    FiniteStateMachine machine = Machine();
                    IFSMTransition consume = FSMTransitionStateMessage.ConstructInstance(machine, Name(), StateMessageJson(stateEvent));
                    IFSMState publish = FSMState_PublishStateMessage.ConstructInstance(machine, Name(), StateMessageJson(stateEvent));
                    Assert.That(consume.Update(user, s_context), Is.False, "the user's list alone does not fire it");
                    publish.OnEnter(user, null);
                    Assert.That(consume.Update(user, s_context), Is.True);
                    Assert.That(appEvents, Is.Empty, "firing consumes the event");
                    Assert.That(consume.Update(user, s_context), Is.False, "one publication fires once");
                    publish.OnEnter(user, null);
                    Assert.That(consume.Update(user, s_context), Is.True);
                    Assert.That(userEvents.Count, Is.EqualTo(1), "the user's list is never consumed");
                }
                finally { Object.DestroyImmediate(stateEvent); }
            }
        }

        // ApplicationStateLoadingInitialiseSystems.ConstructInstance 0x060007c7, x86-64
        // 0x65a9a0, resolves EventId with ApplicationStateEvent.FindByName (0x53b620)
        // before allocating. PublishMessage 0x060007c9 (0x65ad80), which the
        // CharacterManager's initialised callback runs, reads the entering user's
        // IGraphStorage.GetValue<List<ApplicationStateEvent>>(StateEvents, null, true)
        // and tail-calls ListExtensions.AddUnique<object> (0x96ffa0). Publishing into the
        // App's list, or adding without the uniqueness check, fails here.
        [Test]
        public void LoadingInitialiseSystemsPublishesIntoTheUsersStorageOnce()
        {
            using (var owned = new OwnedServices())
            {
                owned.Row<App>();
                App app = owned.RegisterApp();
                ApplicationStateEvent stateEvent = ScriptableObject.CreateInstance<ApplicationStateEvent>();
                try
                {
                    stateEvent.name = Name();
                    var appEvents = new List<ApplicationStateEvent>();
                    app.Storage.SetValue(AppFSMKeys.StateEvents, appEvents);
                    FSMUser user = User();
                    var userEvents = new List<ApplicationStateEvent>();
                    user.Storage.SetValue(AppFSMKeys.StateEvents, userEvents);
                    IFSMState state = ApplicationStateLoadingInitialiseSystems.ConstructInstance(Machine(), Name(), StateMessageJson(stateEvent));
                    Assert.That(state, Is.TypeOf<ApplicationStateLoadingInitialiseSystems>());
                    MethodInfo publish = typeof(ApplicationStateLoadingInitialiseSystems).GetMethod("PublishMessage", Instance);
                    publish.Invoke(state, new object[] { user });
                    publish.Invoke(state, new object[] { user });
                    Assert.That(userEvents, Is.EqualTo(new[] { stateEvent }), "the resolved event is published once into the user's list");
                    Assert.That(appEvents, Is.Empty, "the App's list is not the target");
                }
                finally { Object.DestroyImmediate(stateEvent); }
            }
        }

        // ApplicationStateLoadingInitialiseSystems.DoOnEnter 0x060007c8, x86-64 0x65aa40.
        // Entering constructs every level system, including a CharacterManager that
        // preloads the character FSMs through App.DataManager, so this compares the
        // rebuilt IL with the native order: FSMState.DoOnEnter (0x1ac3530),
        // GetSystem<LevelManager> (0x98ea70) and ManagedSystems (+0x70), the placement
        // manager added by instance (0x95c080), UnregisterSystem<ProcMeshPlacementUpdater>
        // (0x98fd70) before RegisterSystem (0x1b11080) of its new updater, the seven
        // parameterless AddManagedSystem<T> (0x95bf80) in native order, the
        // CharacterManager added by instance with a callback to PublishMessage, then
        // SystemRef<SaveManager>.TryGet (0xc07280) and the GameIsPlaying store (+0x39).
        // Reordering the systems, or a callback that does not publish, fails here.
        [Test]
        public void LoadingInitialiseSystemsAddsTheLevelSystemsInNativeOrder()
        {
            Type state = typeof(ApplicationStateLoadingInitialiseSystems);
            MethodInfo enter = state.GetMethod("DoOnEnter", Instance | BindingFlags.DeclaredOnly);
            Assert.That(enter, Is.Not.Null);
            List<Instruction> body = Read(enter);
            List<Instruction> calls = body.Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt || i.Code == OpCodes.Newobj).ToList();
            Type closure = state.GetNestedType("<>c__DisplayClass5_0", BindingFlags.NonPublic);
            Assert.That(closure, Is.Not.Null, "the original closure name");
            string add = typeof(LevelManagerSystems).FullName + "::AddManagedSystem<";
            string[] expected =
            {
                closure.FullName + "::.ctor",
                typeof(FSMState).FullName + "::DoOnEnter",
                typeof(ProcessManager).FullName + "::GetSystem<" + typeof(LevelManager).FullName + ">",
                typeof(LevelManager).FullName + "::get_ManagedSystems",
                typeof(ProceduralMesh_PlacementManager).FullName + "::.ctor",
                add + typeof(ProceduralMesh_PlacementManager).FullName + ">",
                typeof(ProcessManager).FullName + "::UnregisterSystem<" + typeof(ProcMeshPlacementUpdater).FullName + ">",
                typeof(ProcMeshPlacementUpdater).FullName + "::.ctor",
                typeof(ProcessManager).FullName + "::RegisterSystem",
                add + typeof(SimplifiedBodyManager).FullName + ">",
                add + typeof(CollectableManager).FullName + ">",
                add + typeof(EnemyManager).FullName + ">",
                add + typeof(MessageManager).FullName + ">",
                add + typeof(GameplayIslandManager).FullName + ">",
                add + typeof(EntityActivationManager).FullName + ">",
                add + typeof(LevelRespawnPoints).FullName + ">",
                typeof(Action).FullName + "::.ctor",
                typeof(CharacterManager).FullName + "::.ctor",
                add + typeof(CharacterManager).FullName + ">",
                typeof(SystemRef<SaveManager>).FullName + "::TryGet",
                typeof(SaveManager).FullName + "::set_GameIsPlaying",
            };
            Assert.That(calls.Select(c => Describe(enter, c)).ToArray(), Is.EqualTo(expected));

            // The placement manager and the CharacterManager use the instance overload;
            // the other seven use the parameterless one that keeps a valid system.
            for (int i = 0; i < calls.Count; i++)
            {
                if (!expected[i].StartsWith(add)) continue;
                Type first = enter.Module.ResolveMethod(calls[i].Operand).GetParameters()[0].ParameterType;
                bool byInstance = i == 5 || i == 18;
                Assert.That(first == typeof(bool), Is.Not.EqualTo(byInstance), expected[i]);
            }

            // The CharacterManager's callback is the closure method, which publishes.
            Instruction callback = body[body.IndexOf(calls[16]) - 1];
            Assert.That(callback.Code, Is.EqualTo(OpCodes.Ldftn));
            MethodBase lambda = enter.Module.ResolveMethod(callback.Operand);
            Assert.That(lambda.DeclaringType, Is.EqualTo(closure));
            Assert.That(lambda.Name, Is.EqualTo("<DoOnEnter>b__0"));
            List<Instruction> lambdaCalls = Read((MethodInfo)lambda).Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt).ToList();
            Assert.That(lambdaCalls.Select(c => Describe((MethodInfo)lambda, c)).ToArray(),
                Is.EqualTo(new[] { state.FullName + "::PublishMessage" }));
        }

        private static string StateMessageJson(ApplicationStateEvent stateEvent) =>
            "{\"EventId\":\"" + stateEvent.name + "\"}";

        private static IFSMState PlayAudio(FiniteStateMachine machine, string clip, string source, bool oneShot, bool replay) =>
            ApplicationStatePlayAudio.ConstructInstance(machine, Name(),
                "{\"Clip\":\"" + clip + "\",\"Source\":\"" + source + "\",\"OneShot\":" + (oneShot ? "true" : "false") +
                ",\"Replay\":" + (replay ? "true" : "false") + "}");

        // FSMState_Error.DoOnEnter 0x06002328, x86-64 0x5843b0: after FSMState.DoOnEnter
        // it formats literal "{0} - Error in FSM {1} for user {2}: {3}" with String.Format
        // (0x1bf5130) over the virtual ToString (vtable slot 3, the state's name), FSM,
        // the user and the stored message, then routes it to HLOutput.LogError (0x1af8470).
        // Formatting the FSMId (slot 4) or dropping a field fails here.
        [Test]
        public void ErrorStateLogsItsNameMachineUserAndMessageOnce()
        {
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                FiniteStateMachine machine = Machine();
                string stateName = Name();
                IFSMState error = FSMState_Error.ConstructInstance(machine, stateName, "{\"ErrorMessage\":\"lucid failure\"}");
                Assert.That(error, Is.TypeOf<FSMState_Error>());
                FSMUser user = User();
                Assert.That(capture.Errors, Is.Empty);
                error.OnEnter(user, null);
                Assert.That(capture.Errors.Count, Is.EqualTo(1));
                string expected = stateName + " - Error in FSM " + machine + " for user " + user + ": lucid failure";
                Assert.That(capture.Errors[0], Does.StartWith(expected + "|"), "the state name, then machine, user and message");
                Assert.That(error.ToString(), Is.EqualTo(stateName));
            }
        }

        // FSMTransitionCheckCutscenePlaying 0x0600236b..6e, x86-64 0x5874d0..0x5877f0.
        // The CutsceneManager reference is a field initializer; ConstructInstance parses
        // Compare case-insensitively; DoUpdate returns false while CutsceneIsLoading
        // (0x79f640), otherwise IsCutscenePlaying (0x79f5e0), its negation, or true per
        // Comparison. An inverted comparison or a missing loading gate fails a row.
        [Test]
        public void CheckCutscenePlayingFollowsLoadingThenComparisonTruthTable()
        {
            // The CutsceneManager constructor subscribes these actions; own them first.
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Initialise, SystemAction.Shutdown);
            CutsceneManager manager = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                using (var owned = new OwnedServices())
                {
                    owned.ReadOnlyRow<FullscreenShaderManager>();
                    owned.ReadOnlyRow<VisualQualityManager_SDT>();
                    owned.ReadOnlyRow<CinemachineCameraManager>();
                    owned.Row<CutsceneManager>();
                    var host = new GameObject("Lucid owned cutscene");
                    try
                    {
                        host.SetActive(false);
                        Cutscene cutscene = host.AddComponent<ComicCutscene>();
                        manager = new CutsceneManager();
                        FiniteStateMachine machine = Machine();
                        IFSMTransition whenPlaying = CheckCutscene(machine, "TrueCausesTransition");
                        IFSMTransition whenStopped = CheckCutscene(machine, "falsecausestransition");
                        IFSMTransition either = CheckCutscene(machine, "EitherCausesTransition");
                        // Enum.Parse accepts the numeric string "3"; no Comparison case matches it
                        // (DoUpdate 0x587840..0x58787d falls through to false).
                        IFSMTransition outOfRange = CheckCutscene(machine, "3");
                        FSMUser user = User();
                        bool[] Row() => new[] { whenPlaying.Update(user, s_context), whenStopped.Update(user, s_context), either.Update(user, s_context), outOfRange.Update(user, s_context) };

                        Assert.That(Row(), Is.EqualTo(new[] { false, true, true, false }), "idle");
                        SetCutscene(manager, "m_currentCutscene", cutscene);
                        Assert.That(Row(), Is.EqualTo(new[] { true, false, true, false }), "playing");
                        SetCutscene(manager, "m_cutsceneIsLoading", true);
                        Assert.That(Row(), Is.EqualTo(new[] { true, false, true, false }), "loading without play-on-load is not CutsceneIsLoading");
                        SetCutscene(manager, "m_cutscenePlayOnLoad", true);
                        Assert.That(manager.CutsceneIsLoading, Is.True);
                        Assert.That(Row(), Is.EqualTo(new[] { false, false, false, false }), "loading gates every comparison");
                        SetCutscene(manager, "m_currentCutscene", null);
                        Assert.That(Row(), Is.EqualTo(new[] { false, false, false, false }), "loading while idle");
                    }
                    finally
                    {
                        Object.DestroyImmediate(host);
                        // The constructor registered the manager into the owned row.
                        owned.AdoptCurrent<CutsceneManager>();
                    }
                }
            },
            () =>
            {
                // Safety net if row disposal throws before unsubscribing the manager.
                if (manager != null) ProcessManager.UnsubscribeFromAllActions(manager);
            },
            actionRows.Dispose);
        }

        // ConstructInstance calls Enum.Parse(typeof(Comparison), Compare, true) with no
        // handler and no default (0x5876cd..0x58772b), so an unknown name throws before
        // anything is allocated or registered.
        [Test]
        public void CheckCutscenePlayingRejectsAnUnknownComparisonName()
        {
            FiniteStateMachine machine = Machine();
            Assert.Throws<ArgumentException>(() => CheckCutscene(machine, "NeitherCausesTransition"));
        }

        private static IFSMTransition CheckCutscene(FiniteStateMachine machine, string compare) =>
            FSMTransitionCheckCutscenePlaying.ConstructInstance(machine, Name(), "{\"Compare\":\"" + compare + "\"}");

        private static void SetCutscene(CutsceneManager manager, string field, object value) =>
            typeof(CutsceneManager).GetField(field, Instance).SetValue(manager, value);

        // FSMState_StopFullscreenShader.InvokeTriggerStart 0x06002334, x86-64 0x584b90:
        // ProcessManager.GetSystem<FullscreenShaderManager> (0x98ea70) then
        // StopAllEffectOverrides (0x6878c0) run before the user's IGraphStorage
        // RemoveValue<ParametersHandle>(FSMStorageKeys.ShaderOverride) tail call. With no
        // material, StopAllEffectOverrides faults in ApplyParameters, so a handle that
        // survives the fault shows the removal comes second.
        [Test]
        public void StopFullscreenShaderStopsOverridesBeforeDroppingTheUsersHandle()
        {
            using (var owned = new OwnedServices())
            {
                owned.Row<FullscreenShaderManager>();
                owned.RegisterFullscreenShaderManager(null);
                FSMUser user = User();
                user.Storage.SetValue(FSMStorageKeys.ShaderOverride, default(FullscreenShaderManager.ParametersHandle));
                IFSMState stop = FSMState_StopFullscreenShader.ConstructInstance(Machine(), Name(), "{\"ActiveEventType\":0}");
                Assert.Throws<NullReferenceException>(() => stop.OnEnter(user, null));
                Assert.That(user.Storage.HasValue<FullscreenShaderManager.ParametersHandle>(FSMStorageKeys.ShaderOverride), Is.True,
                    "the handle is removed only after every override has stopped");
            }
        }

        // FSMState_Trigger dispatch (0x06002336/37) with the restored InvokeTriggerStart:
        // EnterStart stops on entry, LeaveStart only on leave. Stopping clears the pending
        // delayed stops and removes the user's ShaderOverride handle.
        [Test]
        public void StopFullscreenShaderRunsOnItsTriggerEventAndClearsPendingStops()
        {
            using (var owned = new OwnedServices())
            {
                owned.Row<FullscreenShaderManager>();
                FullscreenShaderManager manager = owned.RegisterFullscreenShaderManager(owned.OwnedMaterial());
                var delayed = (List<FullscreenShaderManager.ParametersHandle>)typeof(FullscreenShaderManager)
                    .GetField("m_delayedStops", Instance).GetValue(manager);
                FiniteStateMachine machine = Machine();
                foreach (int eventType in new[] { 0, 1 })
                {
                    FSMUser user = User();
                    user.Storage.SetValue(FSMStorageKeys.ShaderOverride, default(FullscreenShaderManager.ParametersHandle));
                    delayed.Add(default(FullscreenShaderManager.ParametersHandle));
                    IFSMState stop = FSMState_StopFullscreenShader.ConstructInstance(machine, Name(), "{\"ActiveEventType\":" + eventType + "}");
                    stop.OnEnter(user, null);
                    bool stoppedOnEnter = eventType == 0;
                    Assert.That(delayed.Count, Is.EqualTo(stoppedOnEnter ? 0 : 1), "event type " + eventType + " on enter");
                    Assert.That(user.Storage.HasValue<FullscreenShaderManager.ParametersHandle>(FSMStorageKeys.ShaderOverride), Is.Not.EqualTo(stoppedOnEnter));
                    stop.OnLeave(user, null);
                    Assert.That(delayed, Is.Empty, "event type " + eventType + " after leave");
                    Assert.That(user.Storage.HasValue<FullscreenShaderManager.ParametersHandle>(FSMStorageKeys.ShaderOverride), Is.False);
                }
            }
        }

        // FSMState_TriggerCharacterCamera 0x0600233f..42, x86-64 0x585490..0x585650. The
        // CinemachineCameraManager reference is a field initializer; InvokeTriggerStart
        // requests the override type and InvokeTriggerEnd requests CameraType.None
        // through RequestCharacterCameraChange (0x714e40), each only when
        // SystemRef.IsNull (0xc071d0) is false. Ending with the override type, or
        // dropping the IsNull guard, fails here.
        [Test]
        public void TriggerCharacterCameraTakesTheCameraOnEnterAndReleasesItOnLeave()
        {
            string json = "{\"ActiveEventType\":2,\"CameraTypeOverride\":" + (int)CameraType.AttachedToPoint + "}";
            using (var owned = new OwnedServices())
            {
                CinemachineCameraManager manager = owned.RegisterCameraManager();
                FieldInfo handle = typeof(CinemachineCameraManager).GetField("m_playerCharacterHandle", Instance);
                IFSMState trigger = FSMState_TriggerCharacterCamera.ConstructInstance(Machine(), Name(), json);
                FSMUser user = User();
                trigger.OnEnter(user, null);
                Assert.That(handle.GetValue(manager), Is.Not.Null, "entering takes the character camera handle");
                Assert.That(ActiveCamera(manager), Is.EqualTo(CameraType.AttachedToPoint));
                trigger.OnLeave(user, null);
                Assert.That(handle.GetValue(manager), Is.Null, "leaving requests no camera type");
                Assert.That(ActiveCamera(manager), Is.EqualTo(CameraType.None));
            }
            using (var owned = new OwnedServices())
            {
                owned.Row<CinemachineCameraManager>();
                IFSMState trigger = FSMState_TriggerCharacterCamera.ConstructInstance(Machine(), Name(), json);
                FSMUser user = User();
                Assert.DoesNotThrow(() => trigger.OnEnter(user, null), "an empty row is skipped on entry");
                Assert.DoesNotThrow(() => trigger.OnLeave(user, null), "and on leave");
            }
        }

        // FSMState_TriggerCamera.InvokeTriggerStart 0x0600233d, x86-64 0x584f10: after the
        // SystemRef IsNull guard, TryGetValue<StackableDataHandle>(FSMStorageKeys.CameraOverride)
        // (0x951d20) selects the path. A stored handle is reapplied with the new type
        // (0x71a540) and stored, or for None released (0x7155c0) and removed; without one,
        // None does nothing and any other type takes a new handle (0x715420) that is
        // stored. Taking a handle for None, or replacing a stored handle, fails here.
        [Test]
        public void TriggerCameraReusesOrReleasesTheUsersOverrideHandle()
        {
            using (var owned = new OwnedServices())
            {
                CinemachineCameraManager manager = owned.RegisterCameraManager();
                FiniteStateMachine machine = Machine();
                IFSMState none = TriggerCamera(machine, CameraType.None);
                IFSMState attached = TriggerCamera(machine, CameraType.AttachedToPoint);
                IFSMState climbing = TriggerCamera(machine, CameraType.Climbing);
                FSMUser user = User();

                none.OnEnter(user, null);
                Assert.That(user.Storage.HasValue<StackableDataHandle>(FSMStorageKeys.CameraOverride), Is.False, "None takes no handle");
                Assert.That(ActiveCamera(manager), Is.EqualTo(CameraType.None));

                attached.OnEnter(user, null);
                StackableDataHandle handle = user.Storage.GetValueOnly<StackableDataHandle>(FSMStorageKeys.CameraOverride);
                Assert.That(handle, Is.Not.Null);
                Assert.That(ActiveCamera(manager), Is.EqualTo(CameraType.AttachedToPoint));

                climbing.OnEnter(user, null);
                Assert.That(user.Storage.GetValueOnly<StackableDataHandle>(FSMStorageKeys.CameraOverride), Is.SameAs(handle), "the stored handle is reapplied");
                Assert.That(ActiveCamera(manager), Is.EqualTo(CameraType.Climbing));

                none.OnEnter(user, null);
                Assert.That(user.Storage.HasValue<StackableDataHandle>(FSMStorageKeys.CameraOverride), Is.False, "None releases the stored handle");
                Assert.That(ActiveCamera(manager), Is.EqualTo(CameraType.None));
            }
        }

        // FSMState_TriggerCutscene 0x06002344..46, x86-64 0x5856e0..0x5857f0.
        // ConstructInstance parses CutsceneTriggerType with EnumUtilities.Parse (0x907ac0)
        // and copies PreloadOnly. InvokeTriggerStart resolves GetSystem<CutsceneManager>
        // first; a CutsceneDefinition queued under AppFSMKeys.CutsceneQueued (HasValue
        // 0x9514c0) is read, passed to LoadCutscene (0x7a07d0) with play = !PreloadOnly,
        // and removed. Preloading the cutscene already loading without play returns
        // early, so the queued definition is consumed and nothing is promoted. Leaving the
        // queue in place, or playing a preload, fails here.
        [Test]
        public void TriggerCutsceneConsumesAQueuedCutsceneWithoutPlayingAPreload()
        {
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Initialise, SystemAction.Shutdown);
            CutsceneManager manager = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                using (var owned = new OwnedServices())
                {
                    owned.ReadOnlyRow<FullscreenShaderManager>();
                    owned.ReadOnlyRow<VisualQualityManager_SDT>();
                    owned.ReadOnlyRow<CinemachineCameraManager>();
                    owned.Row<CutsceneManager>();
                    CutsceneDefinition definition = ScriptableObject.CreateInstance<CutsceneDefinition>();
                    try
                    {
                        manager = new CutsceneManager();
                        SetCutscene(manager, "m_cutsceneIsLoading", true);
                        SetCutscene(manager, "m_loadingCutscene", definition);
                        IFSMState preload = FSMState_TriggerCutscene.ConstructInstance(Machine(), Name(),
                            "{\"ActiveEventType\":0,\"CutsceneTriggerType\":\"EndCredits\",\"PreloadOnly\":true}");
                        Assert.That(typeof(FSMState_TriggerCutscene).GetField("m_cutsceneTriggerType", Instance).GetValue(preload), Is.EqualTo(CutsceneTriggerType.EndCredits));
                        Assert.That(typeof(FSMState_TriggerCutscene).GetField("m_preloadOnly", Instance).GetValue(preload), Is.True);
                        FSMUser user = User();
                        user.Storage.SetValue(AppFSMKeys.CutsceneQueued, definition);
                        preload.OnEnter(user, null);
                        Assert.That(user.Storage.HasValue<CutsceneDefinition>(AppFSMKeys.CutsceneQueued), Is.False, "the queued cutscene is consumed");
                        Assert.That(typeof(CutsceneManager).GetField("m_cutscenePlayOnLoad", Instance).GetValue(manager), Is.False,
                            "a preload does not promote the loading cutscene to play");
                    }
                    finally
                    {
                        Object.DestroyImmediate(definition);
                        owned.AdoptCurrent<CutsceneManager>();
                    }
                }
            },
            () =>
            {
                if (manager != null) ProcessManager.UnsubscribeFromAllActions(manager);
            },
            actionRows.Dispose);
        }

        // FSMState_TriggerCutscene.InvokeTriggerStart 0x06002346, x86-64 0x5857f0. Without a
        // queued cutscene it tail-calls PreloadAnyValidCutscene (0x79c2e0) when
        // PreloadOnly, otherwise PlayAnyValidCutscene (0x79c250), both with a null
        // callback. Those need the App's cutscene definitions, so this compares the IL
        // call order and which branch each call sits on.
        [Test]
        public void TriggerCutsceneChoosesPreloadOrPlayByPreloadOnly()
        {
            MethodInfo start = typeof(FSMState_TriggerCutscene).GetMethod("InvokeTriggerStart", Instance | BindingFlags.DeclaredOnly);
            List<Instruction> body = Read(start);
            List<Instruction> calls = body.Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt).ToList();
            string storage = typeof(IGraphStorage).FullName;
            string definition = typeof(CutsceneDefinition).FullName;
            string[] expected =
            {
                typeof(ProcessManager).FullName + "::GetSystem<" + typeof(CutsceneManager).FullName + ">",
                typeof(IGraphUser).FullName + "::get_Storage",
                typeof(IGraphStorageExtensions).FullName + "::HasValue<" + definition + ">",
                typeof(IGraphUser).FullName + "::get_Storage",
                storage + "::GetValue<" + definition + ">",
                typeof(CutsceneManager).FullName + "::LoadCutscene",
                typeof(IGraphUser).FullName + "::get_Storage",
                storage + "::RemoveValue<" + definition + ">",
                typeof(CutsceneManager).FullName + "::PreloadAnyValidCutscene",
                typeof(CutsceneManager).FullName + "::PlayAnyValidCutscene",
            };
            Assert.That(calls.Select(c => Describe(start, c)).ToArray(), Is.EqualTo(expected));

            // The first branch after the queued group reads m_preloadOnly, and a false value
            // jumps past the preload to the Play call.
            int remove = body.IndexOf(calls[7]);
            Instruction branch = body.Skip(remove).First(IsConditional);
            Assert.That(body.Skip(remove).Take(body.IndexOf(branch) - remove)
                .Any(i => i.Code == OpCodes.Ldfld && start.Module.ResolveField(i.Operand).Name == "m_preloadOnly"), Is.True);
            int target = body.FindIndex(i => i.Offset == branch.Target);
            int preload = body.IndexOf(calls[8]);
            int play = body.IndexOf(calls[9]);
            bool falseJumps = branch.Code == OpCodes.Brfalse || branch.Code == OpCodes.Brfalse_S;
            Assert.That(falseJumps, Is.True, "a false PreloadOnly jumps");
            Assert.That(target > preload && target <= play, Is.True, "the jump skips the preload to the play call");
        }

        // FSMState_TriggerScreenControls.InvokeTriggerStart 0x06002350, x86-64 0x586340:
        // the user is cast with GetAs<Character> (0x952720), GetSystemSafe<MessageManager>
        // (0x98f7b0) can be null and skips the publish, and only then is an Action over
        // OnRequestComplete (0x06002351, an empty body) built for
        // ComponentMessagesWithCompletion.PublishMessage<ScreenControlsRequestMessage>
        // (0x7ba400) with timeout -1. Its subscribers are keyed by a live Character, so
        // this compares the IL call order and the arguments.
        [Test]
        public void TriggerScreenControlsPublishesItsGroupFromTheCharacterWhenMessagesExist()
        {
            MethodInfo start = typeof(FSMState_TriggerScreenControls).GetMethod("InvokeTriggerStart", Instance | BindingFlags.DeclaredOnly);
            List<Instruction> body = Read(start);
            List<Instruction> calls = body.Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt || i.Code == OpCodes.Newobj).ToList();
            Type exchange = typeof(MessageExchangeWithCompletion<Component>);
            string[] expected =
            {
                typeof(IGraphUserExtensions).FullName + "::GetAs<" + typeof(Character).FullName + ">",
                typeof(ProcessManager).FullName + "::GetSystemSafe<" + typeof(MessageManager).FullName + ">",
                typeof(Action).FullName + "::.ctor",
                exchange.FullName + "::PublishMessage<" + typeof(ScreenControlsRequestMessage).FullName + ">",
            };
            Assert.That(calls.Select(c => Describe(start, c)).ToArray(), Is.EqualTo(expected));
            // The override field is read only after GetSystemSafe returns (+0x28 load at 0x5863ee
            // follows the call at 0x5863e9), so the message is built after the lookup.
            Instruction group = body.Single(i => i.Code == OpCodes.Ldfld && start.Module.ResolveField(i.Operand).Name == "m_screenControlsGroupOverride");
            Assert.That(group.Offset, Is.GreaterThan(calls[1].Offset), "the group is read after GetSystemSafe");

            Instruction callback = body[body.IndexOf(calls[2]) - 1];
            Assert.That(callback.Code, Is.EqualTo(OpCodes.Ldftn));
            Assert.That(start.Module.ResolveMethod(callback.Operand).Name, Is.EqualTo("OnRequestComplete"));
            Assert.That(body[body.IndexOf(calls[3]) - 1].Code, Is.EqualTo(OpCodes.Ldc_I4_M1), "the timeout is -1");
            Instruction guard = body.Take(body.IndexOf(calls[2])).Last(IsConditional);
            Assert.That(guard.Target, Is.GreaterThan(calls[3].Offset), "a missing MessageManager skips the publish");
            MethodInfo complete = typeof(FSMState_TriggerScreenControls).GetMethod("OnRequestComplete", Instance);
            List<Instruction> empty = Read(complete);
            Assert.That(empty.Last().Code, Is.EqualTo(OpCodes.Ret));
            Assert.That(empty.Take(empty.Count - 1).All(i => i.Code == OpCodes.Nop), Is.True, "the completion callback is empty");
        }

        private static IFSMState TriggerCamera(FiniteStateMachine machine, CameraType cameraType) =>
            FSMState_TriggerCamera.ConstructInstance(machine, Name(),
                "{\"ActiveEventType\":0,\"CameraTypeOverride\":" + (int)cameraType + "}");

        private static CameraType ActiveCamera(CinemachineCameraManager manager) =>
            manager.GetCameraSettingOverride<CameraType>(CameraSettingType.ActiveCamera);

        private static void SetReady(App app, bool ready) =>
            typeof(App).GetField("<IsReady>k__BackingField", Instance).SetValue(app, ready);

        private sealed class OwnedServices : IDisposable
        {
            private readonly IDictionary m_registry = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            private readonly Dictionary<string, VerificationFixtureOwnership.RegistryRow> m_rows =
                new Dictionary<string, VerificationFixtureOwnership.RegistryRow>();
            private readonly List<IDisposable> m_order = new List<IDisposable>();
            private readonly List<GameObject> m_objects = new List<GameObject>();
            private readonly List<Object> m_assets = new List<Object>();
            private readonly List<AsyncOperationHandle> m_handles = new List<AsyncOperationHandle>();
            private ResourceManager m_resources;

            // A row a node or component registers into must be acquired absent.
            public void Row<T>() where T : class, ISystem
            {
                string name = ProcessManager.GetDefaultName<T>();
                var row = new VerificationFixtureOwnership.RegistryRow(name);
                m_rows.Add(name, row);
                m_order.Add(row);
                row.Register(null);
            }

            public void AdoptCurrent<T>() where T : class, ISystem => m_rows[ProcessManager.GetDefaultName<T>()].AdoptCurrent();

            // A row the case only reads (App's field initializers read SaveManager
            // and UIScreenTransitionManager), where GetSystemRef<T> still caches a
            // typed reference. An absent row is acquired owned; a borrowed row has
            // those added entries removed.
            public void ReadOnlyRow<T>() where T : class, ISystem
            {
                BorrowedSystemRowTypedCache borrowed = BorrowedSystemRowTypedCache.TryBorrow(ProcessManager.GetDefaultName<T>());
                if (borrowed == null) Row<T>();
                else m_order.Add(borrowed);
            }

            public IDictionary TypedReferences(string name)
            {
                object info = m_registry[name];
                return (IDictionary)info.GetType().GetField("SystemRefDictionary").GetValue(info);
            }

            private T Component<T>() where T : Component
            {
                var host = new GameObject("Lucid owned " + typeof(T).Name);
                m_objects.Add(host);
                host.SetActive(false);
                return host.AddComponent<T>();
            }

            public App RegisterApp()
            {
                ReadOnlyRow<SaveManager>();
                ReadOnlyRow<UIScreenTransitionManager>();
                App app = Component<App>();
                m_rows[ProcessManager.GetDefaultName<App>()].Register(app);
                return app;
            }

            // AddComponent sends the editor Reset and OnValidate, which read the TimeScaledComponent_SDT
            // s_config lookup; with no cached lookup that registers a SystemConfiguration row (GetConfig).
            // An owned empty lookup stands in while the component is added, and the prior cache is put back.
            private T AddTimeScaled<T>() where T : Component
            {
                FieldInfo config = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
                object prior = config.GetValue(null);
                var lookup = ScriptableObject.CreateInstance<TimeCategoryLookup>();
                config.SetValue(null, lookup);
                try { return Component<T>(); }
                finally
                {
                    config.SetValue(null, prior);
                    Object.DestroyImmediate(lookup);
                }
            }

            // An inactive manager whose Awake never runs, so its material is only the one
            // supplied here (null leaves the original fault in ApplyParameters reachable).
            public FullscreenShaderManager RegisterFullscreenShaderManager(Material material)
            {
                FullscreenShaderManager manager = AddTimeScaled<FullscreenShaderManager>();
                typeof(FullscreenShaderManager).GetField("m_fullscreenShaderMaterial", Instance).SetValue(manager, material);
                m_rows[ProcessManager.GetDefaultName<FullscreenShaderManager>()].Register(manager);
                return manager;
            }

            // An inactive camera manager whose Awake never runs. Its camera list stays the
            // empty one supplied here, so UpdateCameras only rebuilds the override stack.
            public CinemachineCameraManager RegisterCameraManager()
            {
                ReadOnlyRow<SaveManager>();
                ReadOnlyRow<CameraStackManager>();
                Row<CinemachineCameraManager>();
                CinemachineCameraManager manager = AddTimeScaled<CinemachineCameraManager>();
                FieldInfo cameras = typeof(CinemachineCameraManager).GetField("m_virtualCamerasByType", Instance);
                cameras.SetValue(manager, Activator.CreateInstance(cameras.FieldType));
                m_rows[ProcessManager.GetDefaultName<CinemachineCameraManager>()].Register(manager);
                return manager;
            }

            public Material OwnedMaterial()
            {
                var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
                m_assets.Add(material);
                return material;
            }

            public void RegisterUiConfiguration() =>
                m_rows[ProcessManager.GetDefaultName<UIRuntimeConfiguration>()].Register(Component<UIRuntimeConfiguration>());

            public AudioManager RegisterAudioManager()
            {
                Row<AudioManager>();
                AudioManager audio = Component<AudioManager>();
                HLAudioSourceHandler handler = audio.gameObject.AddComponent<HLAudioSourceHandler>();
                typeof(AudioManager).GetField("m_audioSourceHandler", Instance).SetValue(audio, handler);
                handler.Initialise(audio.transform);
                m_rows[ProcessManager.GetDefaultName<AudioManager>()].Register(audio);
                return audio;
            }

            public HLAudioSourceData AddSource(AudioManager audio, HLAudioSourceIdentifier identifier)
            {
                AudioSource source = Component<AudioSource>();
                var data = new HLAudioSourceData(identifier, source, false, HLAudioSourceTransition.None, default);
                audio.AddSource(data);
                Assert.That(audio.HasSource(identifier), Is.True);
                return data;
            }

            public AsyncOperationHandle Completed()
            {
                if (m_resources == null)
                {
                    m_resources = new ResourceManager();
                    typeof(ResourceManager).GetField("CallbackHooksEnabled", Instance).SetValue(m_resources, false);
                }
                AsyncOperationHandle handle = m_resources.CreateCompletedOperation<GameObject>(null, null);
                m_handles.Add(handle);
                Assert.That(handle.IsValid() && handle.IsDone, Is.True);
                return handle;
            }

            public void Dispose()
            {
                var failures = new List<Exception>();
                for (int i = m_order.Count - 1; i >= 0; i--)
                {
                    try { m_order[i].Dispose(); }
                    catch (Exception failure) { failures.Add(failure); }
                }
                foreach (GameObject host in m_objects)
                    if (host != null) Object.DestroyImmediate(host);
                foreach (Object asset in m_assets)
                    if (asset != null) Object.DestroyImmediate(asset);
                foreach (AsyncOperationHandle handle in m_handles)
                    if (handle.IsValid()) m_resources.Release(handle);
                m_resources?.Dispose();
                if (failures.Count != 0) throw new AggregateException("Owned FSM service cleanup failed", failures);
            }
        }

        // ApplicationStatePostLevelLoadReset 0x060007b5..b9, x86-64 0x659ea0..0x65a330. The
        // four system references are field initializers taken in the order LevelManager,
        // DataManager, ProceduralMesh_PlacementManager, ProcMeshPlacementUpdater (0x98f010
        // each) before FSMState's constructor. DoOnEnter 0x65a040 never calls
        // FSMState.DoOnEnter: it reads the current level (+0xc0), runs ReplaceMeshPools
        // 0x65a130 and the inlined ReplaceCameraManager, then invokes OnLevelRestart (+0x90)
        // without a null test. The level and its scene need the live level systems, so
        // this compares the IL with the native order. Calling the base, or swapping the
        // pool before the placements move, fails here.
        [Test]
        public void PostLevelLoadResetRebuildsThePoolsThenSwapsTheCameraAndRestarts()
        {
            Type state = typeof(ApplicationStatePostLevelLoadReset);
            string refs = typeof(ProcessManager).FullName + "::GetSystemRef<";
            ConstructorInfo ctor = state.GetConstructor(Instance, null, new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier) }, null);
            Assert.That(Calls(ctor), Is.EqualTo(new[]
            {
                refs + typeof(LevelManager).FullName + ">",
                refs + typeof(DataManager).FullName + ">",
                refs + typeof(ProceduralMesh_PlacementManager).FullName + ">",
                refs + typeof(ProcMeshPlacementUpdater).FullName + ">",
                typeof(FSMState).FullName + "::.ctor",
            }));

            MethodInfo enter = state.GetMethod("DoOnEnter", Instance | BindingFlags.DeclaredOnly);
            Assert.That(Calls(enter), Is.EqualTo(new[]
            {
                typeof(SystemRef<LevelManager>).FullName + "::Get",
                typeof(LevelManager).FullName + "::GetCurrentLevelUnsafe",
                state.FullName + "::ReplaceMeshPools",
                state.FullName + "::ReplaceCameraManager",
                typeof(Action).FullName + "::Invoke",
            }));

            string systems = typeof(LevelManagerSystems).FullName;
            string setups = typeof(Dictionary<LevelSetupTypes, LevelSetupDefinition>).FullName + "::get_Item";
            Assert.That(Calls(state.GetMethod("ReplaceMeshPools", Instance)), Is.EqualTo(new[]
            {
                typeof(SystemRef<ProceduralMesh_PlacementManager>).FullName + "::Get",
                typeof(ProceduralMesh_PlacementManager).FullName + "::get_Placements",
                typeof(LevelManager).FullName + "::get_ManagedSystems",
                systems + "::RemoveManagedSystem<" + typeof(ProceduralMesh_PlacementManager).FullName + ">",
                typeof(ProceduralMesh_PlacementManager).FullName + "::.ctor",
                typeof(LevelManager).FullName + "::get_ManagedSystems",
                systems + "::AddManagedSystem<" + typeof(ProceduralMesh_PlacementManager).FullName + ">",
                typeof(ProceduralMesh_PlacementManager).FullName + "::RegisterInstance",
                typeof(SystemRef<ProcMeshPlacementUpdater>).FullName + "::Get",
                typeof(ProcessManager).FullName + "::ProcessSystemAction",
                typeof(ProcessManager).FullName + "::UnregisterSystem<" + typeof(ProcMeshPlacementUpdater).FullName + ">",
                typeof(ProcMeshPlacementUpdater).FullName + "::.ctor",
                typeof(ProcessManager).FullName + "::RegisterSystem",
                typeof(SystemRef<DataManager>).FullName + "::Get",
                typeof(LevelManagerLevel).FullName + "::get_LevelDefinition",
                typeof(GameplayLevelDefinition).FullName + "::get_LevelSetupType",
                setups,
                typeof(LevelManager).FullName + "::get_ManagedSystems",
                typeof(LevelSetupDefinition).FullName + "::get_PrefabPoolManager",
                systems + "::ReplacePrefab<" + typeof(PrefabPoolManager).FullName + ">",
                typeof(ProceduralMesh_PlacementManager).FullName + "::InitialiseSetup",
            }));

            Assert.That(Calls(state.GetMethod("ReplaceCameraManager", Instance)), Is.EqualTo(new[]
            {
                typeof(SystemRef<DataManager>).FullName + "::Get",
                typeof(LevelManagerLevel).FullName + "::get_LevelDefinition",
                typeof(GameplayLevelDefinition).FullName + "::get_LevelSetupType",
                setups,
                typeof(LevelManager).FullName + "::get_ManagedSystems",
                typeof(LevelSetupDefinition).FullName + "::get_CinemachineCameraManager",
                systems + "::ReplacePrefab<" + typeof(CinemachineCameraManager).FullName + ">",
            }));
        }

        // ApplicationStateInitialiseHLCrashReport.DoOnEnter 0x0600068f, x86-64 0x6488d0:
        // after FSMState.DoOnEnter (0x1ac3530), GetSystem<HLCrashReport> (0x98ea70) is
        // resolved before a new HLCrashReportSettings (0x79b730) is asked
        // GetServiceEnabled (0x79b6f0); only an enabled service is configured with those
        // settings (0x1a0c480) and started (0x1a0ca00). Starting the native bridge is
        // process-wide, so this compares the IL. HLCrashReportSettings.GetServiceEnabled
        // 0x0600185a reads CoreGameConfiguration's SendCrashReports byte (+0x31).
        [Test]
        public void InitialiseHLCrashReportStartsOnlyWhenCrashReportsAreEnabled()
        {
            Type state = typeof(ApplicationStateInitialiseHLCrashReport);
            MethodInfo enter = state.GetMethod("DoOnEnter", Instance | BindingFlags.DeclaredOnly);
            Assert.That(Calls(enter), Is.EqualTo(new[]
            {
                typeof(FSMState).FullName + "::DoOnEnter",
                typeof(ProcessManager).FullName + "::GetSystem<" + typeof(HLCrashReport).FullName + ">",
                typeof(HLCrashReportSettings).FullName + "::.ctor",
                typeof(HLCrashReportSettings).FullName + "::GetServiceEnabled",
                typeof(HLCrashReport).FullName + "::Configure",
                typeof(HLCrashReport).FullName + "::StartService",
            }));
            List<Instruction> body = Read(enter);
            Instruction guard = body.First(IsConditional);
            int configure = body.FindIndex(i => (i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt) && Describe(enter, i).EndsWith("::Configure"));
            int start = body.FindIndex(i => (i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt) && Describe(enter, i).EndsWith("::StartService"));
            Assert.That(body.IndexOf(guard), Is.LessThan(configure));
            Assert.That(guard.Code == OpCodes.Brfalse || guard.Code == OpCodes.Brfalse_S, Is.True, "a false GetServiceEnabled jumps");
            Assert.That(guard.Target, Is.GreaterThan(body[start].Offset), "a disabled service skips both calls");

            MethodInfo enabled = typeof(HLCrashReportSettings).GetMethod("GetServiceEnabled");
            Assert.That(Calls(enabled), Is.EqualTo(new[]
            {
                typeof(SystemConfiguration).FullName + "::GetConfig<" + typeof(CoreGameConfiguration).FullName + ">",
                typeof(CoreGameConfiguration).FullName + "::get_SendCrashReports",
            }));
            Assert.That(typeof(ICrashReportSettings).IsAssignableFrom(typeof(HLCrashReportSettings)), Is.True);
        }

        private sealed class Instruction
        {
            public int Offset;
            public OpCode Code;
            public int Operand;
            public int Target = -1;
        }

        private static readonly Dictionary<short, OpCode> s_opcodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);

        private static bool IsConditional(Instruction instruction) =>
            instruction.Code.FlowControl == FlowControl.Cond_Branch;

        private static List<Instruction> Read(MethodBase method)
        {
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            var result = new List<Instruction>();
            for (int position = 0; position < il.Length;)
            {
                var instruction = new Instruction { Offset = position };
                short value = il[position++];
                if (value == 0xfe) value = (short)(0xfe00 | il[position++]);
                instruction.Code = s_opcodes[value];
                switch (instruction.Code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget:
                        instruction.Target = position + 1 + (sbyte)il[position]; position += 1; break;
                    case OperandType.InlineBrTarget:
                        instruction.Target = position + 4 + BitConverter.ToInt32(il, position); position += 4; break;
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: position += 1; break;
                    case OperandType.InlineVar: position += 2; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: position += 8; break;
                    case OperandType.InlineSwitch: position += 4 + 4 * BitConverter.ToInt32(il, position); break;
                    default: instruction.Operand = BitConverter.ToInt32(il, position); position += 4; break;
                }
                result.Add(instruction);
            }
            return result;
        }

        // Every call, callvirt and newobj target of a rebuilt body, in IL order.
        private static string[] Calls(MethodBase method) =>
            Read(method).Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt || i.Code == OpCodes.Newobj)
                .Select(i => Describe(method, i)).ToArray();

        private static string Describe(MethodBase owner, Instruction call)
        {
            MethodBase target = owner.Module.ResolveMethod(call.Operand);
            string name = target.DeclaringType.FullName + "::" + target.Name;
            if (target.IsGenericMethod)
                name += "<" + string.Join(",", target.GetGenericArguments().Select(t => t.FullName)) + ">";
            return name;
        }
    }
}
