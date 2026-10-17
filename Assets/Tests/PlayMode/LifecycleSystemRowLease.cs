using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // A null cached row can still own observers and deferred work. This lease
    // admits an absent key and removes only the fixture's cleared row.
    // AdmitAbsentOrIdle also borrows a row whose every reference already holds
    // no system, observer or deferred work; that row and its prior typed
    // entries stay, and only entries added while borrowed are removed.
    internal sealed class LifecycleSystemRowLease : IDisposable
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private readonly IDictionary registry;
        private readonly string name;
        private readonly Dictionary<string, object> typedReferences = new Dictionary<string, object>();
        private readonly List<FailureRecord> cleanupFailures = new List<FailureRecord>();
        private readonly List<FailureRecord> diagnosticFailures = new List<FailureRecord>();
        private readonly Action<string> reportCleanupFailure;
        private object row;
        private SystemRef untypedReference;
        private IDictionary typedCache;
        private bool releaseAttempted;
        // Borrowed rows only: the row's typed entries at admission.
        private Dictionary<object, object> priorTypedEntries;

        internal sealed class FailureRecord
        {
            internal readonly string Step;
            internal readonly Exception Failure;
            internal FailureRecord(string step, Exception failure) { Step = step; Failure = failure; }
        }

        internal IReadOnlyList<FailureRecord> CleanupFailures => cleanupFailures;
        internal IReadOnlyList<FailureRecord> DiagnosticFailures => diagnosticFailures;
        internal Exception CleanupFailure => cleanupFailures.Count == 0 ? null : cleanupFailures[0].Failure;
        internal Exception CleanupDiagnosticFailure => diagnosticFailures.Count == 0 ? null : diagnosticFailures[0].Failure;

        internal static IDictionary SharedRegistry => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        internal LifecycleSystemRowLease(IDictionary registry, string name, Action<string> reportCleanupFailure = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (name == null) throw new ArgumentNullException(nameof(name));
            // Check the raw key before any GetSystemRef call or lifecycle action.
            if (registry.Contains(name))
                throw new InvalidOperationException("Lifecycle fixture requires an absent system row: " + name);
            this.registry = registry;
            this.name = name;
            this.reportCleanupFailure = reportCleanupFailure ?? (message => TestContext.Error.WriteLine(message));
        }

        private LifecycleSystemRowLease(IDictionary registry, string name, object idleRow, Action<string> reportCleanupFailure)
        {
            this.registry = registry;
            this.name = name;
            this.reportCleanupFailure = reportCleanupFailure ?? (message => TestContext.Error.WriteLine(message));
            row = idleRow;
            untypedReference = (SystemRef)ReadRowField(idleRow, "SystemRef");
            typedCache = (IDictionary)ReadRowField(idleRow, "SystemRefDictionary");
            priorTypedEntries = new Dictionary<object, object>();
            foreach (DictionaryEntry entry in typedCache) priorTypedEntries.Add(entry.Key, entry.Value);
        }

        // Admission reads fields only, so a rejected row's observers and
        // deferred work are neither invoked nor consumed.
        internal static LifecycleSystemRowLease AdmitAbsentOrIdle(IDictionary registry, string name, Action<string> reportCleanupFailure = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (!registry.Contains(name)) return new LifecycleSystemRowLease(registry, name, reportCleanupFailure);
            object existing = registry[name];
            if (existing == null)
                throw new InvalidOperationException("Lifecycle fixture requires an absent or idle system row: " + name);
            RequireClearedReference(ReadRowField(existing, "SystemRef"), name);
            foreach (DictionaryEntry entry in (IDictionary)ReadRowField(existing, "SystemRefDictionary"))
                RequireClearedReference(entry.Value, name);
            return new LifecycleSystemRowLease(registry, name, existing, reportCleanupFailure);
        }

        // Admits every named row before the fixture takes any reference. A
        // refusal releases the leases already taken, which hold no created
        // row yet, so the registry is left exactly as it was.
        internal static LifecycleSystemRowLease[] AdmitAllAbsentOrIdle(IDictionary registry, params string[] names)
        {
            var leases = new List<LifecycleSystemRowLease>();
            try
            {
                foreach (string name in names) leases.Add(AdmitAbsentOrIdle(registry, name));
            }
            catch
            {
                foreach (LifecycleSystemRowLease lease in leases) lease.Dispose();
                throw;
            }
            return leases.ToArray();
        }

        internal bool IsBorrowed => priorTypedEntries != null;

        internal void CaptureCreatedRow(SystemRef reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (IsBorrowed)
            {
                // The admitted row is already held; confirm GetSystemRef reused it.
                if (!ReferenceEquals(registry[name], row) || !ReferenceEquals(reference, untypedReference) ||
                    typedCache.Count != priorTypedEntries.Count)
                    throw new InvalidOperationException("Lifecycle fixture borrowed row changed: " + name);
                return;
            }
            if (row != null || !registry.Contains(name))
                throw new InvalidOperationException("Lifecycle fixture cannot capture its system row: " + name);
            object created = registry[name];
            if (!ReferenceEquals(ReadRowField(created, "SystemRef"), reference))
                throw new InvalidOperationException("Lifecycle fixture system reference changed: " + name);
            untypedReference = reference;
            typedCache = (IDictionary)ReadRowField(created, "SystemRefDictionary");
            if (typedCache.Count != 0)
                throw new InvalidOperationException("Lifecycle fixture found an unowned typed cache: " + name);
            row = created;
        }

        internal void CaptureTypedReference<T>(SystemRef<T> reference) where T : class, ISystem
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            string typeName = ProcessManager.GetDefaultName<T>();
            if (row == null || !ReferenceEquals(registry[name], row) ||
                typedReferences.ContainsKey(typeName) || !typedCache.Contains(typeName) ||
                !ReferenceEquals(typedCache[typeName], reference))
                throw new InvalidOperationException("Lifecycle fixture cannot capture its typed reference: " + name);
            typedReferences.Add(typeName, reference);
        }

        // Failure path only: capture a typed reference a partially completed
        // constructor already left in this fixture's own row, reading the cache
        // directly so no reference is created. A capture that already happened,
        // an uncaptured row or an absent entry is left as it is.
        internal void CaptureTypedReferenceIfCreated<T>() where T : class, ISystem
        {
            string typeName = ProcessManager.GetDefaultName<T>();
            if (row == null || typedReferences.ContainsKey(typeName) || !typedCache.Contains(typeName)) return;
            CaptureTypedReference(typedCache[typeName] as SystemRef<T>);
        }

        internal void RunCleanup(string step, Action action)
        {
            try { action(); }
            catch (Exception failure)
            {
                // Each owned finally step retains its failure without replacing
                // an earlier body failure or preventing the next cleanup step.
                cleanupFailures.Add(new FailureRecord(step, failure));
                try { reportCleanupFailure("Lifecycle fixture cleanup failed at " + step + ": " + failure); }
                catch (Exception diagnosticFailure) { diagnosticFailures.Add(new FailureRecord(step, diagnosticFailure)); }
            }
        }

        public void Dispose()
        {
            if (releaseAttempted) return;
            releaseAttempted = true;
            RunCleanup("remove-owned-system-row", RemoveClearedRow);
        }

        private void RemoveClearedRow()
        {
            if (row == null) return;
            if (IsBorrowed)
            {
                ReturnBorrowedRow();
                return;
            }
            if (!ReferenceEquals(registry[name], row) ||
                !ReferenceEquals(ReadRowField(row, "SystemRef"), untypedReference) ||
                !ReferenceEquals(ReadRowField(row, "SystemRefDictionary"), typedCache) ||
                typedCache.Count != typedReferences.Count)
                throw new InvalidOperationException("Lifecycle fixture row ownership changed: " + name);
            RequireClearedReference(untypedReference);
            foreach (KeyValuePair<string, object> reference in typedReferences)
            {
                if (!ReferenceEquals(typedCache[reference.Key], reference.Value))
                    throw new InvalidOperationException("Lifecycle fixture typed cache ownership changed: " + name);
                RequireClearedReference(reference.Value);
            }
            registry.Remove(name);
            row = null;
        }

        // The borrowed row stays registered. Every prior entry must be the
        // same idle reference, and the only other entries are the captured
        // ones this lease added; those are removed once cleared.
        private void ReturnBorrowedRow()
        {
            if (!ReferenceEquals(registry[name], row) ||
                !ReferenceEquals(ReadRowField(row, "SystemRef"), untypedReference) ||
                !ReferenceEquals(ReadRowField(row, "SystemRefDictionary"), typedCache))
                throw new InvalidOperationException("Lifecycle fixture row ownership changed: " + name);
            var added = new List<object>();
            foreach (KeyValuePair<string, object> reference in typedReferences)
                if (!priorTypedEntries.ContainsKey(reference.Key)) added.Add(reference.Key);
            if (typedCache.Count != priorTypedEntries.Count + added.Count)
                throw new InvalidOperationException("Lifecycle fixture row ownership changed: " + name);
            foreach (KeyValuePair<object, object> entry in priorTypedEntries)
                if (!typedCache.Contains(entry.Key) || !ReferenceEquals(typedCache[entry.Key], entry.Value))
                    throw new InvalidOperationException("Lifecycle fixture typed cache ownership changed: " + name);
            foreach (KeyValuePair<string, object> reference in typedReferences)
                if (!typedCache.Contains(reference.Key) || !ReferenceEquals(typedCache[reference.Key], reference.Value))
                    throw new InvalidOperationException("Lifecycle fixture typed cache ownership changed: " + name);
            RequireClearedReference(untypedReference);
            foreach (KeyValuePair<object, object> entry in priorTypedEntries) RequireClearedReference(entry.Value);
            foreach (object key in added) RequireClearedReference(typedCache[key]);
            foreach (object key in added) typedCache.Remove(key);
            row = null;
        }

        private static object ReadRowField(object value, string fieldName) =>
            value.GetType().GetField(fieldName, InstanceFields).GetValue(value);

        private void RequireClearedReference(object reference) => RequireClearedReference(reference, name);

        private static void RequireClearedReference(object reference, string name)
        {
            Type type = reference.GetType();
            while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>))
                type = type.BaseType;
            // Read ownership only; never clear events, deferred actions or values.
            foreach (string fieldName in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                if (type.GetField(fieldName, InstanceFields).GetValue(reference) != null)
                    throw new InvalidOperationException("Lifecycle fixture reference retains unowned state: " + name + "/" + fieldName);
        }
    }
}
