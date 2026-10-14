using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // A null cached row can still own observers and deferred work. This lease
    // admits only an absent key and removes only the fixture's cleared row.
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

        internal void CaptureCreatedRow(SystemRef reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
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

        private static object ReadRowField(object value, string fieldName) =>
            value.GetType().GetField(fieldName, InstanceFields).GetValue(value);

        private void RequireClearedReference(object reference)
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
