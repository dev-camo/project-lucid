using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // ApplicationTransitionFontsReady.DoUpdate 0x0600084d, x86-64 0x663500, against
    // an owned inactive App in an owned registry row. App.Awake never runs, so the
    // only construction effects are its field initialisers' two reference rows.
    public sealed class OriginalFontReadinessUpdateTests
    {
        // GetSystemRef<App> 0x663583; Storage.GetValue<bool>(FontsFallbackPreloadReady
        // static +0x350, false, true) 0x66365e; App re-read 0x663671; GetValue<bool>
        // (FontsChangeLanguageReady +0x360, false, true) 0x66371e; `and al, r14b`
        // 0x663721 with no branch between the reads. The user argument is unused.
        // Catches && short-circuiting (second key never stored) and storeDefault false.
        [Test]
        public void UpdateReadsAndStoresBothKeysEagerlyFromTheRegisteredApp()
        {
            IDictionary registry = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            string[] fieldRows = { ProcessManager.GetDefaultName<SaveManager>(), ProcessManager.GetDefaultName<UIScreenTransitionManager>() };
            // App's field initialisers auto-register these rows. An earlier lookup
            // may have left one unoccupied; cleanup removes only rows created here.
            bool[] existed = new bool[fieldRows.Length];
            for (int i = 0; i < fieldRows.Length; i++)
            {
                existed[i] = registry.Contains(fieldRows[i]);
                if (existed[i]) Assert.IsTrue(ProcessManager.IsSystemNull(fieldRows[i]), "Occupied row " + fieldRows[i]);
            }
            GameObject host = null;
            try
            {
                using (var appRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<App>()))
                {
                    host = new GameObject("Owned inactive font-readiness App");
                    host.SetActive(false);
                    App app = host.AddComponent<App>();
                    Assert.IsFalse(host.activeInHierarchy);
                    appRow.Register(app);
                    IGraphStorage storage = app.Storage;
                    var machine = new FiniteStateMachine("Lucid-font-readiness-update-" + Guid.NewGuid().ToString("N"),
                        skipAddToManager: true);
                    IFSMTransition transition = ApplicationTransitionFontsReady.ConstructInstance(machine,
                        new FSMIdentifier(110010104), null);
                    var context = new FSMUpdateContext(0f, FSMUpdateType.Update);

                    Assert.IsFalse(storage.HasValue<bool>(AppFSMKeys.FontsFallbackPreloadReady));
                    Assert.IsFalse(storage.HasValue<bool>(AppFSMKeys.FontsChangeLanguageReady));
                    Assert.IsFalse(transition.Update(null, context));
                    Assert.IsTrue(storage.TryGetValue(AppFSMKeys.FontsFallbackPreloadReady, out bool preload));
                    Assert.IsFalse(preload);
                    Assert.IsTrue(storage.TryGetValue(AppFSMKeys.FontsChangeLanguageReady, out bool change),
                        "The second key is read and its default stored even though the first is false");
                    Assert.IsFalse(change);

                    storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, true);
                    Assert.IsFalse(transition.Update(null, context));
                    storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, false);
                    storage.SetValue(AppFSMKeys.FontsFallbackPreloadReady, true);
                    Assert.IsFalse(transition.Update(null, context));
                    storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, true);
                    Assert.IsTrue(transition.Update(null, context));
                    Assert.AreSame(storage, app.Storage);
                }
            }
            finally
            {
                try { if (host != null) UnityEngine.Object.DestroyImmediate(host); }
                finally
                {
                    // Remove only the field-initialiser rows created above, after
                    // checking they never held a system.
                    for (int i = 0; i < fieldRows.Length; i++)
                    {
                        if (!registry.Contains(fieldRows[i])) continue;
                        Assert.IsTrue(ProcessManager.IsSystemNull(fieldRows[i]), fieldRows[i]);
                        if (!existed[i]) registry.Remove(fieldRows[i]);
                    }
                }
            }
        }
    }
}
