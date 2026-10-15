using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Routes HLOutput.LogError to an owned, inactive original HLUnityCore so a
    // test can read the exact reported text. Awake never runs, so no native
    // bridge, Unity log callback or singleton Instance is installed; the
    // default registry row is acquired absent and removed on disposal.
    internal sealed class OwnedCoreErrorLog : IDisposable
    {
        public readonly List<string> Errors = new List<string>();
        private readonly VerificationFixtureOwnership.RegistryRow m_row;
        private GameObject m_host;

        public OwnedCoreErrorLog()
        {
            m_row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<HLUnityCore>(),
                HLUnityCore.Instance);
            try
            {
                m_host = new GameObject("Lucid owned error log host");
                m_host.SetActive(false);
                HLUnityCore core = m_host.AddComponent<HLUnityCore>();
                core.AddLogErrorHandler((message, code) => Errors.Add(message + ":" + code));
                m_row.Register(core);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            try { m_row.Dispose(); }
            finally
            {
                if (m_host != null) Object.DestroyImmediate(m_host);
                m_host = null;
            }
        }
    }
}
