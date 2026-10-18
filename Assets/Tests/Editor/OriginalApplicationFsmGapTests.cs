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
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned services for application FSM nodes. Every registry row a node
    // registers into is acquired absent and removed; App, AudioManager and
    // UIRuntimeConfiguration are inactive components whose Awake never runs.
    // Completed handles come from an owned ResourceManager, not Addressables.
    public sealed class OriginalApplicationFsmGapTests
    {
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
        // other call. Offline builds keep that statement under
        // PROJECT_LUCID_ORIGINAL_GAMECENTER and run the same callback with false
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
                Assert.That(closure.GetType().DeclaringType, Is.EqualTo(typeof(ApplicationStateLoadAssets)));
                Assert.That(closure.GetType().Name, Is.EqualTo("<>c__DisplayClass2_0"), "the original callback's closure");
                Assert.That(closure.GetType().GetField("loginSuccess").GetValue(closure), Is.EqualTo(false));
                Assert.That(user.Storage.GetCollection(), Is.Empty);
            }
        }

        // After authentication the offline DoOnEnter keeps the native order. It
        // invokes the cached original callback (<>9__2_0) with false, then calls
        // GetAs<App>, DataManager.LoadDefinitionData, GetSystem<SaveManager>,
        // InitialiseSaveData and RunCoroutine. It calls no Social member.
        [Test]
        public void LoadAssetsOfflineEntryInvokesTheCachedCallbackThenLoadsInTheOriginalOrder()
        {
            MethodInfo enter = typeof(ApplicationStateLoadAssets).GetMethod("DoOnEnter", Instance | BindingFlags.DeclaredOnly);
            Assert.That(enter, Is.Not.Null);
            List<Instruction> body = Read(enter);
            List<Instruction> calls = body.Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt).ToList();
            string[] expected =
            {
                typeof(Action<bool>).FullName + "::Invoke",
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
            Assert.That(body[body.IndexOf(calls[0]) - 1].Code, Is.EqualTo(OpCodes.Ldc_I4_0), "the callback reports a failed login");
        }

        // Source-preservation self-check, not behaviour evidence: EditMode and CI
        // compile only the offline branch. It pins the original-define statement
        // that 0x648C00 translates: Social.get_localUser, then the lazily cached
        // <>c.<>9__2_0 Action<bool>, then ILocalUser.Authenticate, before GetAs<App>.
        // Whitespace and line wrapping inside the branch are ignored. A build with
        // PROJECT_LUCID_ORIGINAL_GAMECENTER also checks that compiled order.
        [Test]
        public void LoadAssetsOriginalGameCenterSourceBranchIsUnchanged()
        {
            string path = Path.Combine(Application.dataPath, "Scripts", "Game", "Application", "FiniteStateMachine", "ApplicationStateLoadAssets.cs");
            string[] lines = File.ReadAllLines(path).Select(l => l.Trim()).ToArray();
            int open = Array.IndexOf(lines, "#if PROJECT_LUCID_ORIGINAL_GAMECENTER");
            Assert.That(open, Is.GreaterThanOrEqualTo(0));
            Assert.That(Array.IndexOf(lines, "#if PROJECT_LUCID_ORIGINAL_GAMECENTER", open + 1), Is.EqualTo(-1), "one original branch");
            int otherwise = Array.IndexOf(lines, "#else", open);
            Assert.That(otherwise, Is.GreaterThan(open));
            string branch = string.Concat(lines.Skip(open + 1).Take(otherwise - open - 1).Select(l => string.Concat(l.Where(c => !char.IsWhiteSpace(c)))));
            Assert.That(branch, Is.EqualTo(
                "Social.localUser.Authenticate(loginSuccess=>ProcessManager.GetSystemRef<GameCenter>().InvokeOnValid(gameCenter=>gameCenter.OnLoginComplete(loginSuccess)));"));
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
            MethodInfo enter = typeof(ApplicationStateLoadAssets).GetMethod("DoOnEnter", Instance | BindingFlags.DeclaredOnly);
            List<Instruction> body = Read(enter);
            List<Instruction> calls = body.Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt).ToList();
            string[] expected =
            {
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
            Assert.That(enter.Module.ResolveField(cache.Operand).Name, Is.EqualTo("<>9__2_0"));
            Assert.That(body.IndexOf(cache), Is.GreaterThan(body.IndexOf(calls[0])).And.LessThan(body.IndexOf(calls[1])));
#endif
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
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
            throw new InvalidOperationException("Owned offline case forbids original GameCenter.");
#endif
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
        // catches it. Stop only clears LocalProfilePresentation's binding, which
        // must already be unbound.
        [Test]
        public void LoadAssetsOfflineEntryPropagatesAStoppedLocalLifecycleBeforeDefinitionLoading()
        {
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
            throw new InvalidOperationException("Owned offline case forbids original GameCenter.");
#endif
            FieldInfo bound = typeof(ProjectLucid.Offline.LocalProfilePresentation).GetField("achievements", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(bound, Is.Not.Null);
            Assert.That(bound.GetValue(null), Is.Null, "foreign bound local achievements stay untouched");
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

            // An inactive manager whose Awake never runs, so its material is only the one
            // supplied here (null leaves the original fault in ApplyParameters reachable).
            public FullscreenShaderManager RegisterFullscreenShaderManager(Material material)
            {
                FullscreenShaderManager manager = Component<FullscreenShaderManager>();
                typeof(FullscreenShaderManager).GetField("m_fullscreenShaderMaterial", Instance).SetValue(manager, material);
                m_rows[ProcessManager.GetDefaultName<FullscreenShaderManager>()].Register(manager);
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

        private static List<Instruction> Read(MethodInfo method)
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

        private static string Describe(MethodInfo owner, Instruction call)
        {
            MethodBase target = owner.Module.ResolveMethod(call.Operand);
            string name = target.DeclaringType.FullName + "::" + target.Name;
            if (target.IsGenericMethod)
                name += "<" + string.Join(",", target.GetGenericArguments().Select(t => t.FullName)) + ">";
            return name;
        }
    }
}
