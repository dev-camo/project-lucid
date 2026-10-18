using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Genuine CharacterManager instances for Edit Mode fixtures. The class has one constructor, which
    // subscribes Shutdown, takes the App, defers into the SaveManager and preloads the character
    // state machines, so this fixture owns what that reaches: a real SaveManager holding an open save,
    // a real App and DataManager on inactive hosts (Awake never runs), the rows the constructor's
    // field initialisers cache references from, and an inactive CoroutineUtils host that refuses
    // the two coroutines the constructor starts (RequestSave and LoadStates), each logging the engine
    // error that is expected here. Nothing is registered under the CharacterManager row; a case
    // that needs one registers the manager itself. Create it before the case's own leases and
    // registrations and dispose it last: its rows are admitted absent or idle and released idle.
    internal sealed class OwnedCharacterManagers : IDisposable
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly FieldInfo CoroutineHost = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Release steps, newest first.
        private readonly Stack<Action> m_release = new Stack<Action>();
        private bool m_disposed;

        // A failed build releases what it took; the build's failure stays the one reported.
        public OwnedCharacterManagers()
        {
            bool built = false;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                Build();
                built = true;
            }, () => { if (!built) Dispose(); });
        }

        private void Build()
        {
            // SaveManager subscribes Initialise and Shutdown; CharacterManager subscribes Shutdown.
            Own(new VerificationFixtureOwnership.ActionRows(SystemAction.Initialise, SystemAction.Shutdown));
            // Rows the CharacterManager and App field initialisers cache typed references from.
            LeaseRow<MissionManager>();
            LeaseRow<DreamPowerStoreManager>();
            LeaseRow<LevelManager>();
            LeaseRow<AddressableManager>();
            LeaseRow<UIScreenTransitionManager>();

            object priorHost = CoroutineHost.GetValue(null);
            var host = new GameObject("Lucid character manager coroutine host");
            m_release.Push(() => Object.DestroyImmediate(host));
            host.SetActive(false);
            m_release.Push(() => CoroutineHost.SetValue(null, priorHost));
            CoroutineHost.SetValue(null, host.AddComponent<CoroutineUtils>());

            // The constructor's deferred callback reads the open save.
            var save = new SaveManager();
            // Its constructor subscribes now, before its row is admitted.
            m_release.Push(() => ProcessManager.UnsubscribeFromAllActions(save));
            var game = new SaveDataGame();
            game.Initialise();
            typeof(SaveManager).GetField("m_currentSave", Instance).SetValue(save, game);
            Register(save);
            Register(AddComponent<DataManager>("data manager"));
            Register(AddComponent<App>("app"));
        }

        // A manager built by its real constructor. The refused RequestSave and LoadStates coroutines
        // each log one engine error, expected here.
        public CharacterManager NewManager()
        {
            LogAssert.Expect(LogType.Error, RefusedCoroutine);
            LogAssert.Expect(LogType.Error, RefusedCoroutine);
            var manager = new CharacterManager(() => { });
            m_release.Push(() => ProcessManager.UnsubscribeFromAllActions(manager));
            return manager;
        }

        private void Own(IDisposable lease) => m_release.Push(lease.Dispose);

        private void LeaseRow<T>() where T : class, ISystem =>
            Own(new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<T>()));

        private T AddComponent<T>(string name) where T : Component
        {
            var host = new GameObject("Lucid character manager " + name);
            m_release.Push(() => Object.DestroyImmediate(host));
            host.SetActive(false);
            return host.AddComponent<T>();
        }

        // Registers into a row admitted absent or idle, and unregisters before the row is released.
        private void Register<T>(T system) where T : class, ISystem
        {
            string name = ProcessManager.GetDefaultName<T>();
            Own(new VerificationFixtureOwnership.LifecycleRow(name));
            ProcessManager.RegisterSystem(system, name);
            m_release.Push(() => ProcessManager.UnregisterSystem(name));
        }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            VerificationFixtureOwnership.RunThenCleanUp(() => { }, m_release.ToArray());
        }
    }
}
