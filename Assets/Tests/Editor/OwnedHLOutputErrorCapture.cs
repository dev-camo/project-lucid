using System;
using System.Collections.Generic;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // HLOutput.LogError reaches a log only through the registered HLUnityCore
    // system; with no core it returns silently. This capture registers an owned,
    // never-activated core (Awake, native bridges and AppDomain hooks never run)
    // in an absent default row and records every routed error in order.
    internal sealed class OwnedHLOutputErrorCapture : IDisposable
    {
        private readonly VerificationFixtureOwnership.RegistryRow row;
        private readonly GameObject host;
        private readonly HLUnityCore core;
        private readonly Action<string, int> handler;
        public readonly List<string> Errors = new List<string>();

        public OwnedHLOutputErrorCapture()
        {
            // A live or registered foreign core would receive the fixture's errors.
            row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<HLUnityCore>(), HLUnityCore.Instance);
            try
            {
                host = new GameObject("Owned inactive HLOutput error capture");
                host.SetActive(false);
                core = host.AddComponent<HLUnityCore>();
                Assert.IsFalse(host.activeInHierarchy);
                handler = (message, code) => Errors.Add(message + "|" + code);
                core.AddLogErrorHandler(handler);
                row.Register(core);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            try { row.Dispose(); }
            finally
            {
                try { if (core != null && handler != null) core.RemoveLogErrorHandler(handler); }
                finally { if (host != null) Object.DestroyImmediate(host); }
            }
        }
    }
}
