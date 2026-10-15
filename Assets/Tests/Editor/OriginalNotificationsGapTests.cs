using System;
using System.Collections.Generic;
using System.IO;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // An owned inactive HLUnityCore (Awake never runs) is registered in an
    // absent default row only so HLOutput.LogError reaches an owned handler.
    public sealed class OriginalNotificationsGapTests
    {
        // HLNotificationsConfiguration.Validate 0x06000014, x86-64 0x1a4cc40:
        // template check (dataPath 0x1a4ccab, FileExists 0x1a4ccb8, Concat with
        // the template literal 0x1a4ccd1, LogError 0x1a4ccf8), then an
        // independent output check (0x1a4cd14..0x1a4cd76) with its own literal.
        // A combined check, an else-if, swapped literals or a reversed order
        // fails here. The field reread after FileExists has no observable hook.
        [Test]
        public void EachOverrideIsCheckedAndReportedTemplateFirst()
        {
            string coreName = ProcessManager.GetDefaultName<HLUnityCore>();
            Assert.That(HLUnityCore.IsNull(), Is.True, "A foreign Core singleton is outside this fixture.");
            var messages = new List<string>();
            GameObject host = null;
            HLNotificationsConfiguration config = null;
            using (var row = new VerificationFixtureOwnership.RegistryRow(coreName))
            {
                try
                {
                    host = new GameObject("Lucid owned inactive logging core");
                    host.SetActive(false);
                    HLUnityCore core = host.AddComponent<HLUnityCore>();
                    row.Register(core);
                    core.AddLogErrorHandler((message, code) => messages.Add(code + ":" + message));
                    config = ScriptableObject.CreateInstance<HLNotificationsConfiguration>();

                    // The default empty overrides resolve to the Assets directory, not a file.
                    config.Validate();
                    Assert.That(messages, Is.EqualTo(new[]
                    {
                        "0:Path to HLUnityCore Gradle template file not valid: ",
                        "0:Path to HLUnityCore Gradle file not valid: "
                    }));

                    string existing = "Tests/Editor/ProjectLucid.EditMode.asmdef";
                    string missing = "lucid-owned-missing-" + Guid.NewGuid().ToString("N") + ".gradle";
                    Assert.That(File.Exists(Path.Combine(Application.dataPath, existing)), Is.True);
                    using (var serialized = new SerializedObject(config))
                    {
                        serialized.FindProperty("m_gradleTemplateFileOverride").stringValue = existing;
                        serialized.FindProperty("m_gradleFileOverride").stringValue = missing;
                        Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                    }
                    messages.Clear();
                    config.Validate();
                    Assert.That(messages, Is.EqualTo(new[] { "0:Path to HLUnityCore Gradle file not valid: " + missing }));

                    using (var serialized = new SerializedObject(config))
                    {
                        serialized.FindProperty("m_gradleTemplateFileOverride").stringValue = missing;
                        serialized.FindProperty("m_gradleFileOverride").stringValue = existing;
                        Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                    }
                    messages.Clear();
                    config.Validate();
                    Assert.That(messages, Is.EqualTo(new[] { "0:Path to HLUnityCore Gradle template file not valid: " + missing }));
                }
                finally
                {
                    if (config != null) Object.DestroyImmediate(config);
                    if (host != null) Object.DestroyImmediate(host);
                }
            }
        }
    }
}
