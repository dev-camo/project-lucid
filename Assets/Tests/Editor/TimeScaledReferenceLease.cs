using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original 06000ec7 stores a permanent registry reference. Only an observed
    // real descendant promotes a temporary lease, including enclosing scopes.
    internal sealed class TimeScaledReferenceLease : IDisposable
    {
        [ThreadStatic] private static List<TimeScaledReferenceLease> active;
        private static string Name => typeof(TimeManager).FullName;
        private static IDictionary Registry => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        private readonly VerificationFixtureOwnership.LifecycleRow row;
        private readonly object priorTyped;
        private object observed;
        private bool unqualified, disposed;

        internal TimeScaledReferenceLease()
        {
            row = new VerificationFixtureOwnership.LifecycleRow(Name);
            priorTyped = CurrentTyped();
            if (active == null) active = new List<TimeScaledReferenceLease>();
            active.Add(this);
        }

        internal static int ActiveCount => active?.Count ?? 0;

        private static object CurrentTyped()
        {
            object info = Registry[Name];
            if (info == null) return null;
            var cache = (IDictionary)info.GetType().GetField("SystemRefDictionary").GetValue(info);
            return cache[Name];
        }

        private static void Observe(TimeScaledComponent component, TimeScaledReferenceLease[] scopes)
        {
            if (component == null) throw new InvalidOperationException("Permanent reference observation requires a real component");
            object original = typeof(TimeScaledComponent).GetField("m_timeManagerRef", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (original == null || !ReferenceEquals(original, CurrentTyped()))
                throw new InvalidOperationException("Original readonly time-scaled reference is detached");
            foreach (TimeScaledReferenceLease scope in scopes)
            {
                scope.row.RetainTypedEntry(Name, original);
                scope.observed = original;
            }
        }

        internal static T Construct<T>(GameObject host, Func<T> construct) where T : Component
        {
            if (active == null || !typeof(TimeScaledComponent).IsAssignableFrom(typeof(T))) return construct();
            TimeScaledReferenceLease[] scopes = active.ToArray();
            TimeScaledComponent[] before = host.GetComponents<TimeScaledComponent>();
            T result = null;
            VerificationFixtureOwnership.RunThenCleanUp(() => result = construct(), () =>
            {
                try
                {
                    TimeScaledComponent[] added = host.GetComponents<TimeScaledComponent>()
                        .Where(value => !before.Any(prior => ReferenceEquals(prior, value))).ToArray();
                    if (added.Length == 1) Observe(added[0], scopes);
                    else
                    {
                        object current = CurrentTyped();
                        bool changed = false;
                        foreach (TimeScaledReferenceLease scope in scopes)
                            if (added.Length > 1 || (current != null && !ReferenceEquals(current, scope.priorTyped) && !ReferenceEquals(current, scope.observed)))
                            { scope.unqualified = true; changed = true; }
                        if (added.Length != 0 || changed)
                            throw new InvalidOperationException("Failed construction published an unqualified time-scaled reference; retain and report it");
                    }
                }
                catch
                {
                    foreach (TimeScaledReferenceLease scope in scopes) scope.unqualified = true;
                    throw;
                }
            });
            return result;
        }

        internal static T Add<T>(GameObject host) where T : Component => Construct(host, () => host.AddComponent<T>());

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (unqualified) throw new InvalidOperationException("Unqualified time-scaled reference retained after failed construction");
                row.Dispose();
            }
            finally
            {
                active.Remove(this);
                if (active.Count == 0) active = null;
            }
        }
    }
}
