using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;

namespace ProjectLucid
{
    // Maintainer fixtures must not replace retained registry references or
    // execute their observers while borrowing an apparently empty row.
    public static class VerificationFixtureOwnership
    {
        private static readonly string[] OwnershipFields = { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" };

        // Reads, never clears, the system value, both observer events and the
        // deferred InvokeOnValid work on the untyped reference and on every
        // typed entry of a SystemInfo row; the first field holding state is reported.
        private static void RequireIdleRow(object row, string failurePrefix)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            RequireIdleReference(row.GetType().GetField("SystemRef", fields).GetValue(row), failurePrefix);
            foreach (DictionaryEntry entry in (IDictionary)row.GetType().GetField("SystemRefDictionary", fields).GetValue(row))
                RequireIdleReference(entry.Value, failurePrefix);
        }

        private static void RequireIdleReference(object reference, string failurePrefix) =>
            RequireIdleReference(reference, null, failurePrefix);

        // As above, except that the system value may also be liveSystem: a typed
        // entry cached on a live row carries that row's system from construction.
        private static void RequireIdleReference(object reference, ISystem liveSystem, string failurePrefix)
        {
            Type type = reference.GetType();
            while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
            foreach (string field in OwnershipFields)
            {
                object value = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference);
                bool systemField = field == "m_system" || field == "m_isystem";
                if (value != null && !(systemField && liveSystem != null && ReferenceEquals(value, liveSystem)))
                    throw new InvalidOperationException(failurePrefix + field);
            }
        }

        public static void RequireAbsent(IDictionary registry, string name, params object[] sharedState)
        {
            if (registry.Contains(name))
                throw new InvalidOperationException("Fixture requires an absent registry row: " + name);
            foreach (object value in sharedState)
                if (!ReferenceEquals(value, null))
                    throw new InvalidOperationException("Fixture requires unowned static state: " + name);
        }

        // Admits a row that the original lifecycle registers into, reading
        // fields only so a refused row's observers and deferred work are neither
        // run nor consumed. Same admission and release model as the PlayMode
        // LifecycleSystemRowLease, which this Editor assembly cannot reference.
        // An absent row becomes the proof's own and is removed once cleared; an
        // existing row must be idle on its untyped and every typed reference,
        // and stays, losing only the cleared typed entries added while
        // borrowed. With borrowLiveSystem, a row whose system is already
        // registered is borrowed as it is, because the proof then never
        // registers into it. Its system and prior typed entries are never
        // touched; typed entries added while borrowed are removed once they
        // hold no observer or deferred work and no system but the row's.
        //
        // The proofs never hand their references to the row, so a created row
        // or typed entry is not captured as the PlayMode lease does. Each proof
        // is synchronous and holds the key from admission to release, so a row
        // or typed entry first found at release was created while it ran; it is
        // removed only when its untyped and every typed reference are cleared.
        public sealed class LifecycleRow : IDisposable
        {
            private readonly IDictionary registry;
            private readonly string name;
            private readonly object borrowedRow;
            private readonly object borrowedReference;
            private readonly IDictionary borrowedCache;
            private readonly Dictionary<object, object> priorTypedEntries;
            private bool released;

            public bool Existed { get; }
            public bool BorrowsLiveSystem { get; }

            public LifecycleRow(string name, bool borrowLiveSystem = false)
                : this((IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null), name, borrowLiveSystem) { }

            public LifecycleRow(IDictionary registry, string name, bool borrowLiveSystem = false)
            {
                this.registry = registry;
                this.name = name;
                if (!registry.Contains(name)) return;
                object row = registry[name];
                if (row == null) throw new InvalidOperationException("Fixture requires an absent or idle registry row: " + name);
                if (borrowLiveSystem && Reference(row).IsValid()) BorrowsLiveSystem = true;
                else RequireIdle(row, name);
                Existed = true;
                borrowedRow = row;
                borrowedReference = Reference(row);
                borrowedCache = TypedCache(row);
                priorTypedEntries = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in borrowedCache) priorTypedEntries.Add(entry.Key, entry.Value);
            }

            private static SystemRef Reference(object row) => (SystemRef)Read(row, "SystemRef");

            private static IDictionary TypedCache(object row) => (IDictionary)Read(row, "SystemRefDictionary");

            private static object Read(object value, string field) =>
                value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value);

            private static void RequireIdle(object row, string name) =>
                RequireIdleRow(row, "Fixture registry row retains unowned state: " + name + "/");

            // Call after the proof has unregistered its systems. A row that
            // still holds state is left in place and reported.
            public void Dispose()
            {
                if (released) return;
                released = true;
                if (Existed)
                {
                    ReturnBorrowedRow();
                    return;
                }
                if (!registry.Contains(name)) return;
                RequireIdle(registry[name], name);
                registry.Remove(name);
            }

            // The borrowed row and its prior typed entries stay as admitted;
            // typed entries added while borrowed are removed once cleared. A live
            // row keeps its system, and its added entries are checked against the
            // system it holds at release.
            private void ReturnBorrowedRow()
            {
                if (!ReferenceEquals(registry[name], borrowedRow) ||
                    !ReferenceEquals(Reference(borrowedRow), borrowedReference) ||
                    !ReferenceEquals(TypedCache(borrowedRow), borrowedCache))
                    throw new InvalidOperationException("Fixture borrowed registry row was replaced: " + name);
                foreach (KeyValuePair<object, object> entry in priorTypedEntries)
                    if (!borrowedCache.Contains(entry.Key) || !ReferenceEquals(borrowedCache[entry.Key], entry.Value))
                        throw new InvalidOperationException("Fixture borrowed typed cache changed: " + name);
                if (!BorrowsLiveSystem) RequireIdle(borrowedRow, name);
                var added = new List<object>();
                foreach (DictionaryEntry entry in borrowedCache)
                    if (!priorTypedEntries.ContainsKey(entry.Key)) added.Add(entry.Key);
                if (BorrowsLiveSystem)
                {
                    ISystem liveSystem = Reference(borrowedRow).GetSafe();
                    foreach (object key in added)
                        RequireIdleReference(borrowedCache[key], liveSystem, "Fixture registry row retains unowned state: " + name + "/");
                }
                foreach (object key in added) borrowedCache.Remove(key);
            }
        }

        // Removes a row the fixture's own construction created, only while every reference in
        // it is still empty; a row that gained a system or observer is foreign state and stays.
        // The untyped reference is checked like every typed entry: GetSystemRef(name)
        // hands it out directly, and a later registration under a removed row's
        // name builds a new row that never reaches its observers or deferred work.
        public static void ReleaseEmptyRow(IDictionary registry, string name, object row)
        {
            RequireIdleRow(row, "Fixture row reference retains state: " + name + ".");
            registry.Remove(name);
        }

        public sealed class RegistryRow : IDisposable
        {
            private readonly IDictionary registry;
            private readonly string name;
            private readonly List<ISystem> systems = new List<ISystem>();
            private object ownedRow;

            public RegistryRow(string name, params object[] sharedState)
            {
                registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                RequireAbsent(registry, name, sharedState);
                this.name = name;
            }

            private SystemRef Reference(object row) => (SystemRef)row.GetType().GetField("SystemRef").GetValue(row);

            private void RequireOwnedRow()
            {
                if (!ReferenceEquals(registry[name], ownedRow))
                    throw new InvalidOperationException("Fixture registry row was replaced: " + name);
                ISystem current = Reference(ownedRow).GetSafe();
                if (current != null && !systems.Any(system => ReferenceEquals(system, current)))
                    throw new InvalidOperationException("Fixture registry row has a foreign owner: " + name);
            }

            public SystemRef Register(ISystem system, bool canReplace = false)
            {
                if (ownedRow == null) RequireAbsent(registry, name);
                else RequireOwnedRow();
                // A replacement callback can throw after publishing this owned
                // system. Keep that partial acquisition eligible for cleanup.
                systems.Add(system);
                SystemRef reference = ProcessManager.RegisterSystem(system, name, canReplace);
                if (ownedRow == null) ownedRow = registry[name];
                return reference;
            }

            public void RegisterUsing(ISystem system, Action registration)
            {
                if (ownedRow != null) throw new InvalidOperationException("Fixture already acquired this row: " + name);
                RequireAbsent(registry, name);
                try
                {
                    registration();
                }
                finally
                {
                    // A lifecycle callback can throw after publishing this owned
                    // system in the newly admitted row; keep it eligible for cleanup.
                    object row = registry[name];
                    if (row != null && ReferenceEquals(Reference(row).GetSafe(), system))
                    {
                        systems.Add(system);
                        ownedRow = row;
                    }
                }
                if (ownedRow == null)
                    throw new InvalidOperationException("Fixture registration did not acquire its expected system: " + name);
            }

            // A system a constructor registered into this row while the fixture held it
            // (the row was acquired absent, so whatever it now holds is the fixture's).
            public void AdoptCurrent()
            {
                if (ownedRow == null || !ReferenceEquals(registry[name], ownedRow))
                    throw new InvalidOperationException("Fixture does not hold this row: " + name);
                ISystem current = Reference(ownedRow).GetSafe();
                if (current != null && !systems.Any(system => ReferenceEquals(system, current)))
                    systems.Add(current);
            }

            public void Dispose()
            {
                if (ownedRow == null) return;
                RequireOwnedRow();
                ProcessManager.UnregisterSystem(name);
                RequireOwnedRow();
                if (!Reference(ownedRow).IsNull())
                    throw new InvalidOperationException("Fixture unregister retained a system: " + name);
                registry.Remove(name);
                ownedRow = null;
            }
        }

        // SubscribeToAction (x86-64 0x1B11E20) creates a missing outer action
        // row through TryGetOrNew, and neither UnsubscribeFromAction (0x1B13970)
        // nor UnsubscribeFromAllActions (0x1B13A30) removes one. A fixture that
        // constructs a subscribing service therefore owns only the named rows it
        // found absent. Admission reads fields only and runs no callback.
        //
        // Release after the fixture has unsubscribed. A row that existed keeps its
        // dictionary and exactly its admitted subscriber/callback pairs. A row the
        // fixture created is removed only while empty; one that still has a
        // subscriber stays and is reported. Release it as the last
        // RunThenCleanUp step, so that report never replaces the fixture's own
        // first failure.
        public sealed class ActionRows : IDisposable
        {
            private readonly Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> lookup = SharedActionLookup;
            private readonly SystemAction[] actions;
            private readonly Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> priorRows =
                new Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>();
            private readonly Dictionary<SystemAction, List<KeyValuePair<ISystem, Action<object>>>> priorPairs =
                new Dictionary<SystemAction, List<KeyValuePair<ISystem, Action<object>>>>();
            private bool released;

            public ActionRows(params SystemAction[] actions)
            {
                this.actions = (SystemAction[])actions.Clone();
                foreach (SystemAction action in this.actions)
                {
                    if (!lookup.TryGetValue(action, out Dictionary<ISystem, Action<object>> row)) continue;
                    priorRows[action] = row;
                    priorPairs[action] = new List<KeyValuePair<ISystem, Action<object>>>(row);
                }
            }

            internal static Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> SharedActionLookup =>
                (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)typeof(ProcessManager)
                    .GetField("s_systemActionLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            public void Dispose()
            {
                if (released) return;
                released = true;
                if (!ReferenceEquals(SharedActionLookup, lookup))
                    throw new InvalidOperationException("Fixture action lookup was replaced");
                var retained = new List<string>();
                foreach (SystemAction action in actions)
                {
                    lookup.TryGetValue(action, out Dictionary<ISystem, Action<object>> row);
                    if (priorRows.TryGetValue(action, out Dictionary<ISystem, Action<object>> prior))
                    {
                        if (!ReferenceEquals(row, prior)) retained.Add(action + " row replaced");
                        else if (!SamePairs(row, priorPairs[action])) retained.Add(action + " subscribers changed");
                    }
                    else if (row != null)
                    {
                        if (row.Count == 0) lookup.Remove(action);
                        else retained.Add(action + " row gained subscribers");
                    }
                }
                if (retained.Count != 0)
                    throw new InvalidOperationException("Fixture action rows retain unowned state: " + string.Join(", ", retained));
            }

            private static bool SamePairs(Dictionary<ISystem, Action<object>> row, List<KeyValuePair<ISystem, Action<object>>> pairs)
            {
                if (row.Count != pairs.Count) return false;
                foreach (KeyValuePair<ISystem, Action<object>> pair in pairs)
                    if (!row.TryGetValue(pair.Key, out Action<object> callback) || !ReferenceEquals(callback, pair.Value)) return false;
                return true;
            }
        }

        // Runs body, then every cleanup step in order, each one even after an
        // earlier failure. The first failure, the body's or else the first
        // step's, propagates unchanged with its stack trace. A step that fails
        // after it is logged as a warning, so a release report such as
        // ActionRows' never replaces the failure that caused it. State such a
        // step could not return stays in place.
        public static void RunThenCleanUp(Action body, params Action[] cleanup)
        {
            ExceptionDispatchInfo first = null;
            try
            {
                body();
            }
            catch (Exception failure)
            {
                first = ExceptionDispatchInfo.Capture(failure);
            }
            foreach (Action step in cleanup)
            {
                try
                {
                    step();
                }
                catch (Exception failure)
                {
                    if (first == null) first = ExceptionDispatchInfo.Capture(failure);
                    else
                    {
                        // A diagnostic that fails must not skip later steps or replace the first failure.
                        try { UnityEngine.Debug.LogWarning("Fixture cleanup failed after an earlier failure: " + failure); }
                        catch (Exception) { }
                    }
                }
            }
            first?.Throw();
        }

        // Mutable static dictionaries whose original lookups cache a miss through
        // the indexer, such as the HardlightEnumExtensions EnumToString tables.
        // Dispose puts back the admitted dictionary object, removes keys added
        // since, and reassigns every admitted value by reference. It never throws
        // for changed contents, so a fixture's own failure stays the result.
        public sealed class StaticDictionaryLease : IDisposable
        {
            private readonly FieldInfo[] fields;
            private readonly IDictionary[] dictionaries;
            private readonly List<DictionaryEntry>[] entries;
            private bool released;

            public StaticDictionaryLease(Type owner, params string[] fieldNames)
                : this(Array.ConvertAll(fieldNames, name => owner.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(owner.FullName, name))) { }

            public StaticDictionaryLease(params FieldInfo[] fields)
            {
                this.fields = (FieldInfo[])fields.Clone();
                dictionaries = new IDictionary[fields.Length];
                entries = new List<DictionaryEntry>[fields.Length];
                for (int i = 0; i < fields.Length; ++i)
                {
                    dictionaries[i] = (IDictionary)fields[i].GetValue(null);
                    entries[i] = new List<DictionaryEntry>();
                    foreach (DictionaryEntry entry in dictionaries[i]) entries[i].Add(entry);
                }
            }

            public void Dispose()
            {
                if (released) return;
                released = true;
                for (int i = 0; i < fields.Length; ++i)
                {
                    fields[i].SetValue(null, dictionaries[i]);
                    var admitted = new HashSet<object>();
                    foreach (DictionaryEntry entry in entries[i]) admitted.Add(entry.Key);
                    var added = new List<object>();
                    foreach (object key in dictionaries[i].Keys) if (!admitted.Contains(key)) added.Add(key);
                    foreach (object key in added) dictionaries[i].Remove(key);
                    foreach (DictionaryEntry entry in entries[i]) dictionaries[i][entry.Key] = entry.Value;
                }
            }
        }

        // ObjectPool<FSMStateChangeAction> is one static pool shared with runtime
        // states. Admission records the actions already leased; Dispose despawns
        // only actions leased while the fixture ran, newest first, and leaves
        // every admitted lease, with its parent graph, in place. The fixture's own
        // Destroy calls are unchanged: on success nothing is left for Dispose.
        // Never DespawnEntirePool here, which would also free borrowed actions.
        public sealed class ActionPoolLease : IDisposable
        {
            private readonly HashSet<FSMStateChangeAction> borrowed;
            private bool released;

            public ActionPoolLease() { borrowed = new HashSet<FSMStateChangeAction>(UsedActions()); }

            // FSMStateChangeAction keeps object equality, so the set compares by reference.
            internal static List<FSMStateChangeAction> UsedActions()
            {
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
                object pool = typeof(Hardlight.Pooling.ObjectPool<FSMStateChangeAction>).GetField("s_objectPool", fields).GetValue(null);
                if (pool == null) return new List<FSMStateChangeAction>();
                return new List<FSMStateChangeAction>((List<FSMStateChangeAction>)pool.GetType().GetField("m_usedObjects", fields).GetValue(pool));
            }

            public void Dispose()
            {
                if (released) return;
                released = true;
                List<FSMStateChangeAction> used = UsedActions();
                for (int i = used.Count - 1; i >= 0; --i)
                    if (!borrowed.Contains(used[i])) Hardlight.Pooling.ObjectPool<FSMStateChangeAction>.Despawn(used[i]);
            }
        }

        // Test-only fault point for the EditMode cleanup controls: a control sets
        // it to throw at one named site inside a fixture's leased region. Null
        // outside those controls, so the fixtures run unchanged.
        internal static Action<string> FaultInjection;

        internal static void FaultPoint(string site) => FaultInjection?.Invoke(site);
    }
}
