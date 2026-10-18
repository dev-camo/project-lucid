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
    // The diagnostics HLOutput.LogError (x86-64 0x1af8470) sends to a registered HLUnityCore,
    // captured through an owned, inactive, constructor-built core on a fresh registry row. Any
    // retained core row is set aside untouched and the whole registry layout is restored
    // afterwards. No Awake, native bridge or App boot runs.
    internal static class HLOutputDiagnosticCapture
    {
        internal static List<string> CaptureErrors(Action action)
        {
            IDictionary rows = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string name = ProcessManager.GetDefaultName<HLUnityCore>();
            var errors = new List<string>();
            GameObject owner = null;
            try
            {
                owner = new GameObject("Owned inactive HLOutput diagnostic host");
                owner.SetActive(false);
                HLUnityCore core = owner.AddComponent<HLUnityCore>();
                Assert.That(owner.activeInHierarchy, Is.False);
                rows.Remove(name);
                ProcessManager.RegisterSystem(core, name);
                core.AddLogErrorHandler((message, code) => errors.Add(message + ":" + code));
                action();
                ProcessManager.UnregisterSystem(name);
            }
            finally
            {
                try
                {
                    FSMManagerVerification.RestoreLayout(rows, layout);
                }
                finally
                {
                    if (owner != null) Object.DestroyImmediate(owner);
                }
            }
            return errors;
        }
    }
}
