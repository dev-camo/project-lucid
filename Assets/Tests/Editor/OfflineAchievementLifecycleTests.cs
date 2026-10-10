using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Offline;
using UnityEngine;

namespace ProjectLucid.Tests.EditMode
{
    // Exercises local achievement and leaderboard lifecycles with genuine
    // Core objects, owned registrations and the existing local storage adapter.
    [TestFixture]
    public sealed class OfflineAchievementLifecycleTests
    {
        private static string Identifier => AchievementIdentifier.Anger_Management.GetString();
        private static string CompletionKey => "ProjectLucid.Local.default.Achievements." + Identifier;

        [Test]
        public void FailedCompletionDetachesOwnedEvaluatorAndPreservesThePrimaryError()
        {
            using (var world = new OwnedCoreWorld())
            {
                LocalAchievementManagerLease lease = null;
                int baselineCalls = 0;
                Action baseline = () => ++baselineCalls;
                Action evaluate = baseline;
                var primary = new InvalidOperationException("owned completion failure");
                Action observer = () => { };
                lease = world.Own(LocalAchievementManagerLease.Create(world.Store, new[] { Identifier },
                    () => true, () =>
                    {
                        lease.Manager.AddBehaviour(Identifier, 2, ref evaluate, () => 1);
                        throw primary;
                    }, () => LocalAchievementRuntime.DetachEvaluators(lease.Manager, ref evaluate)));
                lease.Manager.OnPlatformSyncComplete += observer;
                try
                {
                    Assert.That(Assert.Throws<InvalidOperationException>(() => lease.Start()), Is.SameAs(primary));
                    Assert.That(lease.IsReady, Is.False);
                    Assert.That(lease.Manager.Achievements.Count, Is.Zero);
                    Assert.That(ProcessManager.IsSystemNull<AchievementsManager>(), Is.True);
                    Assert.That(evaluate, Is.EqualTo(baseline));
                    evaluate();
                    Assert.That(baselineCalls, Is.EqualTo(1));
                    var handlers = (Action)typeof(AchievementsManager).GetField("OnPlatformSyncComplete",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lease.Manager);
                    Assert.That(Array.IndexOf(handlers.GetInvocationList(), observer), Is.GreaterThanOrEqualTo(0));
                }
                finally { lease.Manager.OnPlatformSyncComplete -= observer; }
            }
        }

        [Test]
        public void FailedCompletionRetainsBothPrimaryAndRollbackErrorsWhileRevokingReadiness()
        {
            using (var world = new OwnedCoreWorld())
            {
                var primary = new InvalidOperationException("owned primary");
                var cleanup = new ApplicationException("owned rollback");
                var lease = world.Own(LocalAchievementManagerLease.Create(world.Store, new[] { Identifier },
                    () => true, () => { throw primary; }, () => { throw cleanup; }));
                AggregateException error = Assert.Throws<AggregateException>(() => lease.Start());
                Assert.That(error.InnerExceptions, Is.EqualTo(new Exception[] { primary, cleanup }));
                Assert.That(lease.IsReady, Is.False);
                Assert.That(lease.Manager.Achievements.Count, Is.Zero);
                Assert.That(ProcessManager.IsSystemNull<AchievementsManager>(), Is.True);
            }
        }

        [Test]
        public void RealLocalCompletionSurvivesDisposalAndAStoreReloadWithoutAnOpenGameplaySlot()
        {
            using (var world = new OwnedCoreWorld())
            {
                LocalAchievementManagerLease first = null;
                Action evaluate = null;
                first = world.Own(LocalAchievementManagerLease.Create(world.Store, new[] { Identifier },
                    () => true, () => first.Manager.AddBehaviour(Identifier, 2, ref evaluate, () => 2),
                    () => LocalAchievementRuntime.DetachEvaluators(first.Manager, ref evaluate)));
                first.Start();
                evaluate();
                Assert.That(first.Manager.Achievements[Identifier].ReportedComplete, Is.True);
                Assert.That(world.Store.GetPropertyValueFromSave(LocalAchievementRuntime.SaveIdentifier, CompletionKey), Is.EqualTo("1"));
                first.Dispose();
                Assert.That(evaluate, Is.Null);
                world.Reload();
                var second = world.Own(LocalAchievementManagerLease.Create(world.Store, new[] { Identifier },
                    () => false, () => { }, () => { }));
                second.Start();
                Assert.That(second.Manager.Achievements[Identifier].ReportedComplete, Is.True);
                Assert.That(second.IsReady, Is.True);
                // This proves profile completion restoration only. Genuine
                // SaveManager slot switching and original game trackers are held.
            }
        }

        [Test]
        public void MalformedOwnedCompletionValueFailsBeforePublishingEntriesOrReadiness()
        {
            using (var world = new OwnedCoreWorld())
            {
                Assert.That(world.Store.TrySetPropertyValueOnSave(LocalAchievementRuntime.SaveIdentifier,
                    CompletionKey, "invalid"), Is.True);
                int completed = 0;
                var lease = world.Own(LocalAchievementManagerLease.Create(world.Store, new[] { Identifier },
                    () => true, () => ++completed, () => { }));
                Assert.Throws<FormatException>(() => lease.Start());
                Assert.That(completed, Is.Zero);
                Assert.That(lease.IsReady, Is.False);
                Assert.That(lease.Manager.Achievements.Count, Is.Zero);
                Assert.That(ProcessManager.IsSystemNull<AchievementsManager>(), Is.True);
            }
        }

        // These additional cases use a genuine independently named registry row.
        // The existing four preservation oracles and observation helpers are unchanged.
        [Test]
        public void OwnedDisposalPreservesASeparatelyNamedRegistrationOfTheSameManager()
        {
            string aliasName = "LucidAchievementOwnedAlias" + Guid.NewGuid().ToString("N");
            SystemRef alias = ProcessManager.GetSystemRef(aliasName, true);
            Assert.That(alias.IsNull(), Is.True);
            using (var world = new OwnedCoreWorld())
            {
                var lease = world.Own(LocalAchievementManagerLease.Create(world.Store, new[] { Identifier },
                    () => true, () => { }, () => { }));
                try
                {
                    ProcessManager.RegisterSystem(lease.Manager, aliasName);
                    Assert.That(alias.GetSafe(), Is.SameAs(lease.Manager));
                    lease.Start();
                    Assert.That(lease.IsReady, Is.True);
                    lease.Dispose();
                    Assert.That(ProcessManager.IsSystemNull<AchievementsManager>(), Is.True);
                    Assert.That(alias.GetSafe(), Is.SameAs(lease.Manager));
                    Assert.That(lease.IsReady, Is.False);
                    Assert.That(lease.Manager.Achievements.Count, Is.Zero);
                    lease.Dispose();
                    Assert.That(alias.GetSafe(), Is.SameAs(lease.Manager));
                }
                finally { ProcessManager.UnregisterSystem(aliasName); }
            }
        }

        [Test]
        public void OwnedCompletionFaultPreservesTheSameManagerAliasAndThePrimaryError()
        {
            string aliasName = "LucidAchievementFaultAlias" + Guid.NewGuid().ToString("N");
            SystemRef alias = ProcessManager.GetSystemRef(aliasName, true);
            Assert.That(alias.IsNull(), Is.True);
            using (var world = new OwnedCoreWorld())
            {
                var primary = new InvalidOperationException("owned alias completion fault");
                LocalAchievementManagerLease lease = null;
                lease = world.Own(LocalAchievementManagerLease.Create(world.Store, new[] { Identifier },
                    () => true, () =>
                    {
                        ProcessManager.RegisterSystem(lease.Manager, aliasName);
                        throw primary;
                    }, () => { }));
                try
                {
                    Assert.That(Assert.Throws<InvalidOperationException>(() => lease.Start()), Is.SameAs(primary));
                    Assert.That(ProcessManager.IsSystemNull<AchievementsManager>(), Is.True);
                    Assert.That(alias.GetSafe(), Is.SameAs(lease.Manager));
                    Assert.That(lease.IsReady, Is.False);
                    Assert.That(lease.Manager.Achievements.Count, Is.Zero);
                    lease.Dispose();
                    Assert.That(alias.GetSafe(), Is.SameAs(lease.Manager));
                }
                finally { ProcessManager.UnregisterSystem(aliasName); }
            }
        }

        [Test]
        public void GenuineLeaderboardStartupFaultPreservesItsSameObjectAliasAndPrimaryError()
        {
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
            throw new InvalidOperationException("Owned offline case forbids original GameCenter.");
#endif
            string aliasName = "LucidLeaderboardStartupAlias" + Guid.NewGuid().ToString("N");
            var observation = new OwnedLeaderboardLookupObservation(aliasName);
            string name = ProcessManager.GetDefaultName<LeaderboardManager>();
            SystemRef reference = ProcessManager.GetSystemRef(name, true);
            SystemRef alias = ProcessManager.GetSystemRef(aliasName, true);
            var primary = new InvalidOperationException("owned leaderboard startup fault");
            LeaderboardManager captured = null;
            Action<ISystem> startup = value =>
            {
                captured = value as LeaderboardManager;
                Assert.IsNotNull(captured, "Callback must receive the normally constructed real manager.");
                ProcessManager.RegisterSystem(captured, aliasName);
                throw primary;
            };
            reference.OnSystemStartup += startup;
            try
            {
                Assert.That(Assert.Throws<InvalidOperationException>(() => new LeaderboardManager()), Is.SameAs(primary));
                Assert.IsNotNull(captured);
                Assert.That(reference.GetSafe(), Is.Null);
                Assert.That(alias.GetSafe(), Is.SameAs(captured));
                Assert.That(alias.IsValid(), Is.True);
            }
            finally
            {
                reference.OnSystemStartup -= startup;
                if (captured != null)
                {
                    ProcessManager.UnsubscribeFromAllActions(captured);
                    if (ReferenceEquals(reference.GetSafe(), captured)) ProcessManager.UnregisterSystem(name);
                }
                ProcessManager.UnregisterSystem(aliasName);
                observation.AssertRestored();
            }
        }

        [Test]
        public void GenuineLeaderboardSubscribedShutdownPreservesItsSameObjectNamedAlias()
        {
#if PROJECT_LUCID_ORIGINAL_PROPERTY_STORAGE || PROJECT_LUCID_ORIGINAL_GAMECENTER
            throw new InvalidOperationException("Owned offline case forbids original storage/GameCenter.");
#endif
            RequireEmptyOwnedRecords();
            string aliasName = "LucidLeaderboardShutdownAlias" + Guid.NewGuid().ToString("N");
            var observation = new OwnedLeaderboardLookupObservation(aliasName);
            // Normal lookup preparation is fully observed by the outer snapshot.
            // The existing Core world then preserves these exact rows and caches.
            SystemRef reference = ProcessManager.GetSystemRef(ProcessManager.GetDefaultName<LeaderboardManager>(), true);
            SystemRef alias = ProcessManager.GetSystemRef(aliasName, true);
            ProcessManager.GetSystemRef<GameCenter>();
            ProcessManager.GetSystemRef<GameCenterLeaderboard>();
            ProcessManager.GetSystemRef<SaveManager>();
            ProcessManager.GetSystemRef<TimeTrialLeaderboardManager>();
            try
            {
                using (var world = new OwnedCoreWorld())
                {
                    GameObject host = null;
                    MissionDefinition mission = null;
                    GameplayLevelDefinition level = null;
                    LeaderboardManager manager = null;
                    bool recordInitialisationAttempted = false;
                    try
                    {
                        host = new GameObject("Lucid owned inactive leaderboard definitions");
                        host.SetActive(false);
                        DataManager data = host.AddComponent<DataManager>();
                        Assert.IsFalse(host.activeInHierarchy);
                        Assert.IsFalse(ReferenceEquals(ProcessManager.GetSystemRef(
                            ProcessManager.GetDefaultName<DataManager>(), false).GetSafe(), data),
                            "Inactive construction must not run original DataManager.Awake registration.");
                        mission = ScriptableObject.CreateInstance<MissionDefinition>();
                        level = ScriptableObject.CreateInstance<GameplayLevelDefinition>();
                        data.MissionDefinitionLevelLookup.Add(mission, level);
                        Assert.AreEqual(1, data.MissionDefinitionLevelLookup.Count);
                        Assert.AreEqual(LeaderboardIdentifier.None, mission.LeaderboardIdentifier);
                        Assert.IsTrue(world.Store.IsLoaded && world.Store.CanSave);
                        recordInitialisationAttempted = true;
                        InvokeActualRecordAdapter("Initialise", data, world.Store);
                        Assert.That(LocalPersonalRecords.IsReady, Is.True);
                        Assert.That(LocalPersonalRecords.Snapshot().Count, Is.Zero);
                        manager = new LeaderboardManager();
                        Assert.That(reference.GetSafe(), Is.SameAs(manager));
                        ProcessManager.RegisterSystem(manager, aliasName);
                        Assert.That(alias.GetSafe(), Is.SameAs(manager));
                        ProcessManager.ProcessSystemAction(manager, SystemAction.Shutdown);
                        Assert.That(reference.GetSafe(), Is.Null);
                        Assert.That(alias.GetSafe(), Is.SameAs(manager));
                        Assert.That(alias.IsValid(), Is.True);
                        ProcessManager.ProcessSystemAction(manager, SystemAction.Shutdown);
                        Assert.That(alias.GetSafe(), Is.SameAs(manager));
                    }
                    finally
                    {
                        if (manager != null)
                        {
                            ProcessManager.UnsubscribeFromAllActions(manager);
                            if (ReferenceEquals(reference.GetSafe(), manager))
                                ProcessManager.UnregisterSystem(ProcessManager.GetDefaultName<LeaderboardManager>());
                        }
                        ProcessManager.UnregisterSystem(aliasName);
                        if (recordInitialisationAttempted) InvokeActualRecordAdapter("Shutdown");
                        if (mission != null) UnityEngine.Object.DestroyImmediate(mission);
                        if (level != null) UnityEngine.Object.DestroyImmediate(level);
                        if (host != null) UnityEngine.Object.DestroyImmediate(host);
                    }
                }
            }
            finally
            {
                RequireEmptyOwnedRecords();
                observation.AssertRestored();
            }
        }

        // Exact internal adapter methods run normally; no private state is written.
        // This tests the record transport boundary, not full GameCenter bootstrap.
        private static void InvokeActualRecordAdapter(string name, params object[] arguments)
        {
            Type[] parameters = name == "Initialise"
                ? new[] { typeof(DataManager), typeof(HLPropertyStore) } : Type.EmptyTypes;
            Assert.IsTrue(name == "Initialise" || name == "Shutdown");
            MethodInfo method = typeof(LocalPersonalRecords).GetMethod(name,
                BindingFlags.Static | BindingFlags.NonPublic, null, parameters, null);
            Assert.IsNotNull(method);
            Assert.IsTrue(method.IsAssembly && method.IsStatic && method.ReturnType == typeof(void));
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException error)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        private static void RequireEmptyOwnedRecords()
        {
            Assert.That(LocalPersonalRecords.IsReady, Is.False, "Foreign local record state must remain untouched.");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var records = (IDictionary)typeof(LocalPersonalRecords).GetField("Records", flags).GetValue(null);
            var trials = (ICollection<LeaderboardIdentifier>)typeof(LocalPersonalRecords).GetField("TimeTrials", flags).GetValue(null);
            Assert.That(records.Count, Is.Zero);
            Assert.That(trials.Count, Is.Zero);
        }

            private sealed class OwnedLeaderboardLookupObservation
            {
                private readonly IDictionary systems = (IDictionary)ReadStatic("s_systemDictionary");
                private readonly IDictionary actions = (IDictionary)ReadStatic("s_systemActionLookup");
                private readonly IList pending = (IList)ReadStatic("s_actionList");
                private readonly List<object> priorPending = new List<object>();
                private readonly Dictionary<object, RegistryRow> priorSystems = new Dictionary<object, RegistryRow>();
                private readonly Dictionary<object, object> priorActionRows = new Dictionary<object, object>();
                private readonly Dictionary<object, Dictionary<object, object>> priorCallbacks = new Dictionary<object, Dictionary<object, object>>();
                private readonly string managerKey = ProcessManager.GetDefaultName<LeaderboardManager>();
                private readonly string aliasKey;
                private readonly Dictionary<string, Type> allowedCaches = new Dictionary<string, Type>
                {
                    { ProcessManager.GetDefaultName<GameCenter>(), typeof(GameCenter) },
                    { ProcessManager.GetDefaultName<GameCenterLeaderboard>(), typeof(GameCenterLeaderboard) },
                    { ProcessManager.GetDefaultName<SaveManager>(), typeof(SaveManager) },
                    { ProcessManager.GetDefaultName<TimeTrialLeaderboardManager>(), typeof(TimeTrialLeaderboardManager) }
                };
                private static object ReadStatic(string name) => typeof(ProcessManager)
                    .GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                private static object ReadField(object owner, string name)
                {
                    for (Type type = owner.GetType(); type != null; type = type.BaseType)
                    {
                        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (field != null) return field.GetValue(owner);
                    }
                    throw new InvalidOperationException("Missing genuine registry field " + name);
                }
                private sealed class ReferenceState
                {
                    private readonly object reference;
                    private readonly Dictionary<string, object> fields = new Dictionary<string, object>();
                    internal ReferenceState(object reference)
                    {
                        this.reference = reference;
                        foreach (string name in new[] { "m_systemName", "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                            fields.Add(name, ReadField(reference, name));
                    }
                    internal void RequireUnoccupiedWithoutCallbacks()
                    {
                        foreach (string name in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                            Assert.IsNull(fields[name], "The default manager row must be unoccupied with no foreign notification handlers.");
                    }
                    internal void AssertRestored()
                    {
                        foreach (var entry in fields) Assert.AreSame(entry.Value, ReadField(reference, entry.Key));
                    }
                }
                private sealed class RegistryRow
                {
                    internal readonly object Row;
                    internal readonly object Reference;
                    internal readonly IDictionary Cache;
                    internal readonly ReferenceState State;
                    internal readonly Dictionary<object, object> Cached = new Dictionary<object, object>();
                    internal readonly Dictionary<object, ReferenceState> CachedStates = new Dictionary<object, ReferenceState>();
                    internal RegistryRow(object row)
                    {
                        Row = row; Reference = ReadField(row, "SystemRef"); Cache = (IDictionary)ReadField(row, "SystemRefDictionary");
                        State = new ReferenceState(Reference);
                        foreach (DictionaryEntry entry in Cache)
                        { Cached.Add(entry.Key, entry.Value); CachedStates.Add(entry.Key, new ReferenceState(entry.Value)); }
                    }
                }
                internal OwnedLeaderboardLookupObservation(string aliasName)
                {
                    aliasKey = aliasName;
                    Assert.IsFalse((bool)ReadStatic("s_systemActionInProgress"));
                    foreach (object callback in pending) priorPending.Add(callback);
                    foreach (DictionaryEntry entry in systems) priorSystems.Add(entry.Key, new RegistryRow(entry.Value));
                    if (priorSystems.TryGetValue(managerKey, out RegistryRow manager))
                    {
                        manager.State.RequireUnoccupiedWithoutCallbacks();
                        foreach (ReferenceState state in manager.CachedStates.Values) state.RequireUnoccupiedWithoutCallbacks();
                    }
                    if (priorSystems.TryGetValue(aliasKey, out RegistryRow alias))
                    {
                        alias.State.RequireUnoccupiedWithoutCallbacks();
                        foreach (ReferenceState state in alias.CachedStates.Values) state.RequireUnoccupiedWithoutCallbacks();
                    }
                    foreach (DictionaryEntry row in actions)
                    {
                        priorActionRows.Add(row.Key, row.Value);
                        var callbacks = new Dictionary<object, object>();
                        foreach (DictionaryEntry callback in (IDictionary)row.Value) callbacks.Add(callback.Key, callback.Value);
                        priorCallbacks.Add(row.Key, callbacks);
                    }
                }
                private void AssertNewAllowedCache(object rowKey, object key, object reference, object untyped)
                {
                    Assert.IsTrue(allowedCaches.TryGetValue((string)rowKey, out Type type));
                    Assert.AreEqual(rowKey, key);
                    Assert.AreEqual(typeof(SystemRef<>).MakeGenericType(type), reference.GetType());
                    ISystem value = ((SystemRef)untyped).GetSafe();
                    object expected = type.IsInstanceOfType(value) ? value : null;
                    Assert.AreSame(expected, ReadField(reference, "m_system"));
                    Assert.AreSame(expected, ReadField(reference, "m_isystem"));
                    Assert.AreEqual(rowKey, ReadField(reference, "m_systemName"));
                    foreach (string name in new[] { "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                        Assert.IsNull(ReadField(reference, name));
                }
                internal void AssertRestored()
                {
                    Assert.IsFalse((bool)ReadStatic("s_systemActionInProgress"));
                    Assert.AreSame(systems, ReadStatic("s_systemDictionary"));
                    Assert.AreSame(actions, ReadStatic("s_systemActionLookup"));
                    Assert.AreSame(pending, ReadStatic("s_actionList"));
                    Assert.AreEqual(priorPending.Count, pending.Count);
                    for (int i = 0; i < priorPending.Count; i++) Assert.AreSame(priorPending[i], pending[i]);
                    foreach (var prior in priorSystems)
                    {
                        Assert.IsTrue(systems.Contains(prior.Key)); Assert.AreSame(prior.Value.Row, systems[prior.Key]);
                        Assert.AreSame(prior.Value.Reference, ReadField(prior.Value.Row, "SystemRef"));
                        Assert.AreSame(prior.Value.Cache, ReadField(prior.Value.Row, "SystemRefDictionary"));
                        prior.Value.State.AssertRestored();
                        foreach (var cached in prior.Value.Cached)
                        { Assert.IsTrue(prior.Value.Cache.Contains(cached.Key)); Assert.AreSame(cached.Value, prior.Value.Cache[cached.Key]); prior.Value.CachedStates[cached.Key].AssertRestored(); }
                        foreach (DictionaryEntry cached in prior.Value.Cache)
                            if (!prior.Value.Cached.ContainsKey(cached.Key))
                            { AssertNewAllowedCache(prior.Key, cached.Key, cached.Value, prior.Value.Reference); }
                    }
                    foreach (DictionaryEntry row in systems)
                        if (!priorSystems.ContainsKey(row.Key))
                        {
                            Assert.IsTrue((string)row.Key == managerKey || (string)row.Key == aliasKey || allowedCaches.ContainsKey((string)row.Key));
                            var observed = new RegistryRow(row.Value); observed.State.RequireUnoccupiedWithoutCallbacks();
                            foreach (DictionaryEntry cached in observed.Cache)
                            { AssertNewAllowedCache(row.Key, cached.Key, cached.Value, observed.Reference); }
                        }
                    foreach (var prior in priorCallbacks)
                    {
                        Assert.IsTrue(actions.Contains(prior.Key)); Assert.AreSame(priorActionRows[prior.Key], actions[prior.Key]);
                        var actual = (IDictionary)actions[prior.Key]; Assert.AreEqual(prior.Value.Count, actual.Count);
                        foreach (var callback in prior.Value) { Assert.IsTrue(actual.Contains(callback.Key)); Assert.AreSame(callback.Value, actual[callback.Key]); }
                    }
                    foreach (DictionaryEntry row in actions)
                        if (!priorCallbacks.ContainsKey(row.Key))
                        {
                            var action = (SystemAction)row.Key;
                            Assert.AreEqual(SystemAction.Shutdown, action);
                            Assert.AreEqual(0, ((IDictionary)row.Value).Count);
                        }
                }
            }


        private sealed class OwnedCoreWorld : IDisposable
        {
            private readonly string filePrefix = "LucidAchievementLifecycle" + Guid.NewGuid().ToString("N");
            private readonly FieldInfo singleton = typeof(HLPropertyStore).GetField("s_internalInstance", BindingFlags.Static | BindingFlags.NonPublic);
            private readonly object priorStore;
            private readonly List<LocalAchievementManagerLease> leases = new List<LocalAchievementManagerLease>();
            private readonly OwnedProcessObservation process;
            internal HLPropertyStore Store { get; private set; }
            internal OwnedCoreWorld()
            {
#if PROJECT_LUCID_ORIGINAL_PROPERTY_STORAGE || PROJECT_LUCID_ORIGINAL_GAMECENTER
                throw new InvalidOperationException("Owned offline cases forbid original storage/GameCenter research routes.");
#endif
                process = new OwnedProcessObservation();
                RequireEmptyLocalContexts();
                RequireNoPropertyHandlers();
                priorStore = singleton.GetValue(null);
                try { Reload(); }
                catch { singleton.SetValue(null, priorStore); DeleteOwnedFiles(); throw; }
            }
            internal LocalAchievementManagerLease Own(LocalAchievementManagerLease lease) { leases.Add(lease); return lease; }
            internal void Reload()
            {
                Store = new HLPropertyStore(string.Empty, 1, filePrefix);
                Store.LoadFromIdentifier(LocalAchievementRuntime.SaveIdentifier);
                Assert.That(Store.IsLoaded && Store.CanSave, Is.True);
            }
            private static void RequireEmptyLocalContexts()
            {
                var contexts = (IDictionary)typeof(LocalAchievementRuntime).GetField("Contexts", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(contexts.Count, Is.Zero, "Foreign local contexts must remain untouched.");
                var owners = (IDictionary)typeof(LocalAchievementRuntime).GetField("Owners", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(owners.Count, Is.Zero, "Foreign achievement owners must remain untouched.");
            }
            private static void RequireNoPropertyHandlers()
            {
                Assert.That(HLPropertyStore.IsThereAnySaveHandler || HLPropertyStore.IsThereAnyModifySaveHandler
                    || HLPropertyStore.IsThereAnyResolveConflictHandler || HLPropertyStore.IsThereAnySaveSuccessHandler
                    || HLPropertyStore.IsThereAnyAfterConflictsResolvedHandler || HLPropertyStore.IsThereAnyLoadHandler,
                    Is.False, "Do not dispatch original or foreign storage consumers.");
            }
            private void DeleteOwnedFiles()
            {
                string file = Path.Combine(Application.persistentDataPath, filePrefix + LocalAchievementRuntime.SaveIdentifier);
                if (File.Exists(file)) File.Delete(file);
                if (File.Exists(file + "-backup")) File.Delete(file + "-backup");
            }
            public void Dispose()
            {
                var errors = new List<Exception>();
                foreach (var lease in leases)
                    try { lease.Dispose(); } catch (Exception error) { errors.Add(error); }
                try { RequireEmptyLocalContexts(); process.AssertRestored(); }
                catch (Exception error) { errors.Add(error); }
                try { singleton.SetValue(null, priorStore); }
                catch (Exception error) { errors.Add(error); }
                try { DeleteOwnedFiles(); }
                catch (Exception error) { errors.Add(error); }
                if (errors.Count != 0) throw new AggregateException("Owned test cleanup failed.", errors);
            }

            // Read-only observation of the genuine registry/actions. Empty foreign
            // rows are retained, and no registry pruning or global reset is used.
            private sealed class OwnedProcessObservation
            {
                private readonly IDictionary systems = (IDictionary)ReadStatic("s_systemDictionary");
                private readonly IDictionary actions = (IDictionary)ReadStatic("s_systemActionLookup");
                private readonly IList pending = (IList)ReadStatic("s_actionList");
                private readonly List<object> priorPending = new List<object>();
                private readonly Dictionary<object, RegistryRow> priorSystems = new Dictionary<object, RegistryRow>();
                private readonly Dictionary<object, object> priorActionRows = new Dictionary<object, object>();
                private readonly Dictionary<object, Dictionary<object, object>> priorCallbacks = new Dictionary<object, Dictionary<object, object>>();
                private readonly string managerKey = ProcessManager.GetDefaultName<AchievementsManager>();
                private readonly string playerKey = ProcessManager.GetDefaultName<GameCenterLocalPlayer>();
                private static object ReadStatic(string name) => typeof(ProcessManager)
                    .GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                private static object ReadField(object owner, string name)
                {
                    for (Type type = owner.GetType(); type != null; type = type.BaseType)
                    {
                        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (field != null) return field.GetValue(owner);
                    }
                    throw new InvalidOperationException("Missing genuine registry field " + name);
                }
                private sealed class ReferenceState
                {
                    private readonly object reference;
                    private readonly Dictionary<string, object> fields = new Dictionary<string, object>();
                    internal ReferenceState(object reference)
                    {
                        this.reference = reference;
                        foreach (string name in new[] { "m_systemName", "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                            fields.Add(name, ReadField(reference, name));
                    }
                    internal void RequireUnoccupiedWithoutCallbacks()
                    {
                        foreach (string name in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                            Assert.IsNull(fields[name], "The default manager row must be unoccupied with no foreign notification handlers.");
                    }
                    internal void AssertRestored()
                    {
                        foreach (var entry in fields) Assert.AreSame(entry.Value, ReadField(reference, entry.Key));
                    }
                }
                private sealed class RegistryRow
                {
                    internal readonly object Row;
                    internal readonly object Reference;
                    internal readonly IDictionary Cache;
                    internal readonly ReferenceState State;
                    internal readonly Dictionary<object, object> Cached = new Dictionary<object, object>();
                    internal readonly Dictionary<object, ReferenceState> CachedStates = new Dictionary<object, ReferenceState>();
                    internal RegistryRow(object row)
                    {
                        Row = row; Reference = ReadField(row, "SystemRef"); Cache = (IDictionary)ReadField(row, "SystemRefDictionary");
                        State = new ReferenceState(Reference);
                        foreach (DictionaryEntry entry in Cache)
                        { Cached.Add(entry.Key, entry.Value); CachedStates.Add(entry.Key, new ReferenceState(entry.Value)); }
                    }
                }
                internal OwnedProcessObservation()
                {
                    Assert.IsFalse((bool)ReadStatic("s_systemActionInProgress"));
                    foreach (object callback in pending) priorPending.Add(callback);
                    foreach (DictionaryEntry entry in systems) priorSystems.Add(entry.Key, new RegistryRow(entry.Value));
                    if (priorSystems.TryGetValue(managerKey, out RegistryRow manager))
                    {
                        manager.State.RequireUnoccupiedWithoutCallbacks();
                        foreach (ReferenceState state in manager.CachedStates.Values) state.RequireUnoccupiedWithoutCallbacks();
                    }
                    foreach (DictionaryEntry row in actions)
                    {
                        priorActionRows.Add(row.Key, row.Value);
                        var callbacks = new Dictionary<object, object>();
                        foreach (DictionaryEntry callback in (IDictionary)row.Value) callbacks.Add(callback.Key, callback.Value);
                        priorCallbacks.Add(row.Key, callbacks);
                    }
                }
                private void AssertNewPlayerCache(IDictionary cache, object key, object reference, object untyped)
                {
                    Assert.AreEqual(playerKey, key);
                    Assert.IsInstanceOf<SystemRef<GameCenterLocalPlayer>>(reference);
                    Assert.AreSame(((SystemRef)untyped).GetSafe<GameCenterLocalPlayer>(), ReadField(reference, "m_system"));
                    Assert.AreSame(ReadField(reference, "m_system"), ReadField(reference, "m_isystem"));
                    Assert.AreEqual(playerKey, ReadField(reference, "m_systemName"));
                    foreach (string name in new[] { "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" }) Assert.IsNull(ReadField(reference, name));
                }
                internal void AssertRestored()
                {
                    Assert.IsFalse((bool)ReadStatic("s_systemActionInProgress"));
                    Assert.AreSame(systems, ReadStatic("s_systemDictionary"));
                    Assert.AreSame(actions, ReadStatic("s_systemActionLookup"));
                    Assert.AreSame(pending, ReadStatic("s_actionList"));
                    Assert.AreEqual(priorPending.Count, pending.Count);
                    for (int i = 0; i < priorPending.Count; i++) Assert.AreSame(priorPending[i], pending[i]);
                    foreach (var prior in priorSystems)
                    {
                        Assert.IsTrue(systems.Contains(prior.Key)); Assert.AreSame(prior.Value.Row, systems[prior.Key]);
                        Assert.AreSame(prior.Value.Reference, ReadField(prior.Value.Row, "SystemRef"));
                        Assert.AreSame(prior.Value.Cache, ReadField(prior.Value.Row, "SystemRefDictionary"));
                        prior.Value.State.AssertRestored();
                        foreach (var cached in prior.Value.Cached)
                        { Assert.IsTrue(prior.Value.Cache.Contains(cached.Key)); Assert.AreSame(cached.Value, prior.Value.Cache[cached.Key]); prior.Value.CachedStates[cached.Key].AssertRestored(); }
                        foreach (DictionaryEntry cached in prior.Value.Cache)
                            if (!prior.Value.Cached.ContainsKey(cached.Key))
                            { Assert.AreEqual(playerKey, prior.Key); AssertNewPlayerCache(prior.Value.Cache, cached.Key, cached.Value, prior.Value.Reference); }
                    }
                    foreach (DictionaryEntry row in systems)
                        if (!priorSystems.ContainsKey(row.Key))
                        {
                            Assert.IsTrue((string)row.Key == managerKey || (string)row.Key == playerKey);
                            var observed = new RegistryRow(row.Value); observed.State.RequireUnoccupiedWithoutCallbacks();
                            foreach (DictionaryEntry cached in observed.Cache)
                            { Assert.AreEqual(playerKey, row.Key); AssertNewPlayerCache(observed.Cache, cached.Key, cached.Value, observed.Reference); }
                        }
                    foreach (var prior in priorCallbacks)
                    {
                        Assert.IsTrue(actions.Contains(prior.Key)); Assert.AreSame(priorActionRows[prior.Key], actions[prior.Key]);
                        var actual = (IDictionary)actions[prior.Key]; Assert.AreEqual(prior.Value.Count, actual.Count);
                        foreach (var callback in prior.Value) { Assert.IsTrue(actual.Contains(callback.Key)); Assert.AreSame(callback.Value, actual[callback.Key]); }
                    }
                    foreach (DictionaryEntry row in actions)
                        if (!priorCallbacks.ContainsKey(row.Key))
                        {
                            var action = (SystemAction)row.Key;
                            Assert.IsTrue(action == SystemAction.AppInitialise || action == SystemAction.Initialise || action == SystemAction.Shutdown);
                            Assert.AreEqual(0, ((IDictionary)row.Value).Count);
                        }
                }
            }
        }
    }
}
