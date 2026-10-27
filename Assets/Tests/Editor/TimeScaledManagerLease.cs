using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original06000ec7 retains the registry's typed reference permanently.
    // Only actual descendant construction can promote this temporary row lease.
    internal sealed class TimeScaledManagerLease : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static string Name => typeof(TimeManager).FullName;
        private readonly IDictionary registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private readonly TimeScaledReferenceLease temporary;
        private readonly object priorRow, priorUntyped, priorCache;
        private readonly DictionaryEntry[] priorTyped;
        private object acquiredRow, acquiredUntyped, acquiredCache, retainedReference, unqualifiedReference;
        private TimeManager manager;
        private bool registered, disposed;

        internal TimeScaledManagerLease()
        {
            temporary = new TimeScaledReferenceLease();
            priorRow = registry[Name];
            if (priorRow != null)
            {
                priorUntyped = Read(priorRow, "SystemRef"); priorCache = Read(priorRow, "SystemRefDictionary");
                priorTyped = Entries((IDictionary)priorCache);
            }
            else priorTyped = Array.Empty<DictionaryEntry>();
        }
        internal void AttachManager(TimeManager value)
        {
            Require(ReferenceEquals(manager, null) && value != null && !disposed, "one genuine manager attachment"); manager = value;
        }
        private static object Read(object value, string name)
        {
            for (Type t = value.GetType(); t != null; t = t.BaseType)
            { FieldInfo field = t.GetField(name, Fields | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(value); }
            throw new MissingFieldException(value.GetType().FullName, name);
        }
        private static DictionaryEntry[] Entries(IDictionary map)
        { var result = new List<DictionaryEntry>(); foreach (DictionaryEntry row in map) result.Add(row); return result.ToArray(); }
        private static IDictionary Typed(object row) => (IDictionary)Read(row, "SystemRefDictionary");
        private static SystemRef Reference(object row) => (SystemRef)Read(row, "SystemRef");
        private static void Require(bool condition, string detail)
        { if (!condition) throw new InvalidOperationException("Owned time-scaled manager: " + detail); }
        private static void RequireReference(object reference, TimeManager allowed)
        {
            foreach (string name in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
            {
                object value = Read(reference, name);
                bool system = name == "m_system" || name == "m_isystem";
                Require(ReferenceEquals(value, null) || (system && !ReferenceEquals(allowed, null) && ReferenceEquals(value, allowed)), "reference retains foreign state: " + name);
            }
        }
        private void RequireOwnedRow()
        {
            Require(acquiredRow != null && ReferenceEquals(registry[Name], acquiredRow) &&
                ReferenceEquals(Read(acquiredRow, "SystemRef"), acquiredUntyped) && ReferenceEquals(Typed(acquiredRow), acquiredCache),
                "registered row and reference-storage identities preserved");
            foreach (DictionaryEntry entry in priorTyped)
                Require(Typed(acquiredRow).Contains(entry.Key) && ReferenceEquals(Typed(acquiredRow)[entry.Key], entry.Value), "borrowed typed identity preserved before callback");
        }
        internal void RegisterManager(Action afterPublication = null)
        {
            Require(!disposed && manager != null, "live attached manager before registration");
            if (registered) { RequireOwnedRow(); Require(ReferenceEquals(Reference(acquiredRow).GetSafe(), manager), "manager identity preserved"); return; }
            // Recheck immediately before the public API; construction may have run callbacks.
            new VerificationFixtureOwnership.LifecycleRow(Name);
            Require(ReferenceEquals(registry[Name], priorRow), "admitted row identity preserved before registration");
            if (priorRow != null)
            {
                Require(ReferenceEquals(Reference(priorRow), priorUntyped) && ReferenceEquals(Typed(priorRow), priorCache),
                    "admitted reference-storage identities preserved before registration");
                DictionaryEntry[] current = Entries(Typed(priorRow));
                Require(current.Length == priorTyped.Length && current.Zip(priorTyped,
                    (a, b) => Equals(a.Key, b.Key) && ReferenceEquals(a.Value, b.Value)).All(equal => equal),
                    "admitted typed identities and order preserved before registration");
            }
            try { ProcessManager.RegisterSystem(manager, Name); afterPublication?.Invoke(); }
            finally
            {
                object row = registry[Name];
                if (row != null && ReferenceEquals(Reference(row).GetSafe(), manager))
                { acquiredRow = row; acquiredUntyped = Reference(row); acquiredCache = Typed(row); registered = true; }
            }
        }
        internal T Construct<T>(GameObject host, Func<T> construct) where T : TimeScaledComponent
        {
            RegisterManager();
            T[] before = host.GetComponents<T>(); T result = null;
            VerificationFixtureOwnership.RunThenCleanUp(() => result = TimeScaledReferenceLease.Construct(host, construct), () =>
            {
                RequireOwnedRow();
                T[] added = host.GetComponents<T>().Where(c => !before.Any(old => ReferenceEquals(old, c))).ToArray();
                if (added.Length == 1)
                {
                    // Observe after a real component exists, including a constructor
                    // path that published its component before throwing.
                    object original = typeof(TimeScaledComponent).GetField("m_timeManagerRef", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    Require(ReferenceEquals(original, Typed(acquiredRow)[Name]), "original readonly reference stays attached");
                    Require(retainedReference == null || ReferenceEquals(retainedReference, original), "permanent reference identity preserved");
                    retainedReference = original;
                }
                else
                {
                    object current = Typed(acquiredRow)[Name];
                    bool existed = priorTyped.Any(p => Equals(p.Key, Name) && ReferenceEquals(p.Value, current));
                    if (current != null && !existed && retainedReference == null) unqualifiedReference = current;
                    Require(added.Length == 0 && unqualifiedReference == null, "failed construction published an unqualified reference; retain and report it");
                }
            });
            return result;
        }
        internal void Unregister()
        {
            if (!registered) return;
            RequireOwnedRow();
            Require(ReferenceEquals(Reference(acquiredRow).GetSafe(), manager), "owned manager still registered before release");
            RequireReference(Reference(acquiredRow), manager);
            foreach (DictionaryEntry entry in Typed(acquiredRow)) RequireReference(entry.Value, manager);
            ProcessManager.UnregisterSystem(Name);
            Require(Reference(acquiredRow).IsNull(), "owned manager unregistered"); registered = false;
        }
        private void ReturnRow()
        {
            if (unqualifiedReference != null) throw new InvalidOperationException("Owned time-scaled manager: unqualified reference retained after failed construction");
            if (retainedReference == null) return;
            RequireOwnedRow();
            RequireReference(Reference(acquiredRow), null);
            IDictionary current = Typed(acquiredRow);
            foreach (DictionaryEntry entry in current) RequireReference(entry.Value, null);
            Require(ReferenceEquals(current[Name], retainedReference), "retained original reference remains in registry after unregister");
            if (priorRow != null)
                Require(ReferenceEquals(priorRow, acquiredRow) && ReferenceEquals(priorUntyped, Reference(acquiredRow)) && ReferenceEquals(priorCache, current), "borrowed row and storage identities preserved");
            foreach (DictionaryEntry entry in priorTyped)
                Require(current.Contains(entry.Key) && ReferenceEquals(current[entry.Key], entry.Value), "borrowed typed identity preserved");
            Require(current.Count == priorTyped.Length + (priorTyped.Any(e => Equals(e.Key, Name)) ? 0 : 1), "only original permanent typed entry added");
        }
        public void Dispose()
        {
            if (disposed) return;
            VerificationFixtureOwnership.RunThenCleanUp(() => { }, Unregister, ReturnRow, temporary.Dispose);
            disposed = true;
        }
    }
}
