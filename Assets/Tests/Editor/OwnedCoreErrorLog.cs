using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Routes HLOutput.LogError (IsSystemNull<HLUnityCore>, then GetSystem) to an
    // HLUnityCore so a test can read the exact reported text. The default
    // registry row is admitted through LifecycleRow and never required absent.
    // An absent or idle row receives an owned, inactive original core: Awake
    // never runs, so no native bridge, Unity log callback or singleton Instance
    // is installed. Disposal unregisters that core and returns the row as it was
    // admitted, removing a row the core's registration created. A row holding a
    // live core is borrowed as it is: no system is registered into it, and only
    // this log's handler is added to that core for the scope, so the core's own
    // handlers still receive each error as they would outside the test.
    internal sealed class OwnedCoreErrorLog : IDisposable
    {
        public readonly List<string> Errors = new List<string>();
        private readonly VerificationFixtureOwnership.LifecycleRow m_row;
        private readonly Action<string, int> m_handler;
        private HLUnityCore m_core;
        private bool m_registered;
        private GameObject m_host;
        private bool m_released;

        public OwnedCoreErrorLog()
        {
            m_handler = (message, code) => Errors.Add(message + ":" + code);
            string name = ProcessManager.GetDefaultName<HLUnityCore>();
            m_row = new VerificationFixtureOwnership.LifecycleRow(name, borrowLiveSystem: true);
            try
            {
                if (m_row.BorrowsLiveSystem)
                {
                    // GetSystemRef returns the existing untyped reference without creating anything.
                    m_core = ProcessManager.GetSystemRef(name).GetSafe() as HLUnityCore
                        ?? throw new InvalidOperationException("Fixture core row holds a foreign system: " + name);
                    m_core.AddLogErrorHandler(m_handler);
                    return;
                }
                if (!ReferenceEquals(HLUnityCore.Instance, null))
                    throw new InvalidOperationException("Fixture requires unowned static state: " + name);
                m_host = new GameObject("Lucid owned error log host");
                m_host.SetActive(false);
                HLUnityCore core = m_host.AddComponent<HLUnityCore>();
                core.AddLogErrorHandler(m_handler);
                m_core = core;
                // Set first: a registration that throws after publishing the core is still released.
                m_registered = true;
                ProcessManager.RegisterSystem(core, name);
            }
            catch
            {
                // A release report never replaces the admission or registration failure.
                try { Dispose(); }
                catch (Exception cleanup) { Debug.LogWarning("Owned core error log release also failed: " + cleanup); }
                throw;
            }
        }

        // The core leaves the row before the row is released; the host goes last.
        public void Dispose()
        {
            if (m_released) return;
            m_released = true;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                if (m_registered) ProcessManager.UnregisterSystem(m_core);
                else m_core?.RemoveLogErrorHandler(m_handler);
            }, m_row.Dispose, () =>
            {
                if (m_host != null) Object.DestroyImmediate(m_host);
                m_host = null;
            });
        }
    }

    // Control: plants an idle default HLUnityCore row, with one idle typed
    // reference, in place of whatever row was there, and checks that an
    // OwnedCoreErrorLog scope returns it with its row, untyped and typed
    // references unchanged and empty. Dispose puts the prior row and the
    // registry layout back.
    internal sealed class DormantCoreRowControl : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly IDictionary m_rows;
        private readonly string m_name = ProcessManager.GetDefaultName<HLUnityCore>();
        private readonly object[] m_layout;
        private readonly bool m_hadRow;
        private readonly object m_priorRow;
        private readonly object m_row;
        private readonly SystemRef m_reference;
        private readonly SystemRef<HLUnityCore> m_typed;
        private readonly List<DictionaryEntry> m_typedEntries = new List<DictionaryEntry>();

        public DormantCoreRowControl()
        {
            m_rows = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            m_layout = FSMManagerVerification.CaptureLayout(m_rows);
            m_hadRow = m_rows.Contains(m_name);
            m_priorRow = m_hadRow ? m_rows[m_name] : null;
            m_rows.Remove(m_name);
            // Each call creates or reads a null-system reference with no observer or deferred work.
            m_reference = ProcessManager.GetSystemRef(m_name);
            m_typed = ProcessManager.GetSystemRef<HLUnityCore>(m_name);
            m_row = m_rows[m_name];
            foreach (DictionaryEntry entry in TypedCache()) m_typedEntries.Add(entry);
            Assert.That(m_typedEntries.Count, Is.EqualTo(1), "the control plants one typed reference");
        }

        private IDictionary TypedCache() => (IDictionary)m_row.GetType().GetField("SystemRefDictionary", Fields).GetValue(m_row);

        public void AssertUntouched()
        {
            Assert.That(m_rows[m_name], Is.SameAs(m_row), "the dormant core row stays in place");
            Assert.That(m_row.GetType().GetField("SystemRef", Fields).GetValue(m_row), Is.SameAs(m_reference), "its untyped reference is kept");
            var after = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in TypedCache()) after.Add(entry);
            Assert.That(after.Count, Is.EqualTo(m_typedEntries.Count), "it keeps exactly its typed entries");
            for (int i = 0; i < after.Count; i++)
            {
                Assert.That(after[i].Key, Is.SameAs(m_typedEntries[i].Key), "typed entry key " + i);
                Assert.That(after[i].Value, Is.SameAs(m_typedEntries[i].Value), "typed entry reference " + i);
            }
            Assert.That(m_reference.IsNull(), Is.True, "the untyped reference is empty again");
            Assert.That(m_typed.IsNull(), Is.True, "the typed reference is empty again");
            Assert.That(ReferenceEquals(HLUnityCore.Instance, null), Is.True, "no singleton Instance was installed");
        }

        public void Dispose()
        {
            m_rows.Remove(m_name);
            if (m_hadRow) m_rows[m_name] = m_priorRow;
            FSMManagerVerification.RestoreLayout(m_rows, m_layout);
        }
    }
}
