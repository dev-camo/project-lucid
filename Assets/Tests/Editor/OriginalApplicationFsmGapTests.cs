using System;
using System.Collections;
using System.Collections.Generic;
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

        private static IFSMTransition SceneLoaded(FiniteStateMachine machine, string scene, bool useApp, bool addressable) =>
            ApplicationTransitionIsSceneLoaded.ConstructInstance(machine, Name(),
                "{\"SceneName\":\"" + scene + "\",\"UseAppStorage\":" + (useApp ? "true" : "false") +
                ",\"IsAddressable\":" + (addressable ? "true" : "false") + "}");

        private static IFSMState PlayAudio(FiniteStateMachine machine, string clip, string source, bool oneShot, bool replay) =>
            ApplicationStatePlayAudio.ConstructInstance(machine, Name(),
                "{\"Clip\":\"" + clip + "\",\"Source\":\"" + source + "\",\"OneShot\":" + (oneShot ? "true" : "false") +
                ",\"Replay\":" + (replay ? "true" : "false") + "}");

        private static void SetReady(App app, bool ready) =>
            typeof(App).GetField("<IsReady>k__BackingField", Instance).SetValue(app, ready);

        private sealed class OwnedServices : IDisposable
        {
            private readonly IDictionary m_registry = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            private readonly Dictionary<string, VerificationFixtureOwnership.RegistryRow> m_rows =
                new Dictionary<string, VerificationFixtureOwnership.RegistryRow>();
            private readonly List<VerificationFixtureOwnership.RegistryRow> m_order = new List<VerificationFixtureOwnership.RegistryRow>();
            private readonly List<GameObject> m_objects = new List<GameObject>();
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

            // App's own initializers only read or create these rows, so a row
            // left by earlier work is read as it is.
            private void ReadOnlyRow<T>() where T : class, ISystem
            {
                if (!m_registry.Contains(ProcessManager.GetDefaultName<T>())) Row<T>();
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
