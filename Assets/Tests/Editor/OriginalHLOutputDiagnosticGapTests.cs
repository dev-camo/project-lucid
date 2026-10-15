using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using HLVersion = Hardlight.Utils.Version;

namespace ProjectLucid.Editor
{
    // Diagnostics routed through HLOutput.LogError (x86-64 0x1af8470), which reaches
    // ProcessManager.GetSystem<HLUnityCore>().LogError(message.ToString(), 0) only when a
    // core is registered. Each case registers an owned, inactive, constructor-built core on
    // a fresh registry row; any retained core row is set aside untouched and the whole
    // registry layout is restored afterwards. No Awake, native bridge or App boot runs.
    public sealed class OriginalHLOutputDiagnosticGapTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static List<string> CaptureErrors(Action action)
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

        // CollectionExtensions.AddRange (0x06000256; shared x86-64 0x8ebe00): a null range
        // tail-calls HLOutput.LogError("Provided range to add is null.") at 0x8ec13c..0x8ec158
        // and never touches the target. Fails if the diagnostic is dropped (silent return) or
        // replaced by an exception; the empty-range control must emit nothing.
        [Test]
        public void NullRangeReportsTheOriginalDiagnosticAndLeavesTheTarget()
        {
            var target = new List<string> { "kept" };
            List<string> errors = CaptureErrors(() => Hardlight.CollectionExtensions.AddRange<string>(target, null));
            Assert.That(errors, Is.EqualTo(new[] { "Provided range to add is null.:0" }));
            Assert.That(target, Is.EqualTo(new[] { "kept" }));

            errors = CaptureErrors(() => Hardlight.CollectionExtensions.AddRange<string>(target, new string[0]));
            Assert.That(errors, Is.Empty, "an empty range is not a diagnostic");
            Assert.That(target, Is.EqualTo(new[] { "kept" }));
            LogAssert.NoUnexpectedReceived();
        }

        // HLCrashReportConfigurationAsset.GetIncludedNativeFiles (0x06000038, x86-64 0x1a0e0e0)
        // passes both arrays straight to AddRange (calls at 0x1a0e156 and 0x1a0e165) with no
        // null guard, so a null included array reaches the AddRange diagnostic and the two
        // required files are still returned. Fails under a `?? Array.Empty` guard.
        [Test]
        public void NullIncludedFilesReachTheAddRangeDiagnosticAfterRequiredFiles()
        {
            HLCrashReportConfigurationAsset asset = null;
            try
            {
                asset = ScriptableObject.CreateInstance<HLCrashReportConfigurationAsset>();
                FieldInfo included = typeof(HLCrashReportConfigurationAsset).GetField("m_includedNativeFiles", Instance);
                included.SetValue(asset, null);
                IEnumerable<string> files = null;
                List<string> errors = CaptureErrors(() => files = asset.GetIncludedNativeFiles());
                Assert.That(errors, Is.EqualTo(new[] { "Provided range to add is null.:0" }));
                Assert.That(files.ToArray(), Is.EquivalentTo(new[] { "HLCrashReport.h", "HLCrashReport.mm" }));
                Assert.That(included.GetValue(asset), Is.Null, "the null array is not repaired");
            }
            finally
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Version(string) (0x0600125c, x86-64 0x1b37930) logs the validator error through
        // HLOutput.LogError (0x1b37a0c) and leaves the value unparsed; ParseVersionString
        // (0x0600126a, x86-64 0x1b37a20) logs again on every call for invalid serialized text
        // (branch 0x1b37aee -> 0x1b37c44) without marking it parsed. Literal fragments are
        // those of IsNumericIdentifierValid (x86-64 0x1b38480). Fails if either diagnostic
        // is dropped or the invalid parse is cached; the valid control emits nothing.
        [Test]
        public void InvalidVersionTextReportsEachOriginalDiagnosticThroughTheRegisteredCore()
        {
            HLVersion constructed = default;
            List<string> errors = CaptureErrors(() => constructed = new HLVersion("1..2"));
            Assert.That(errors, Is.EqualTo(new[]
            {
                "The numeric identifier in location 2 of version '1..2' split using '.' as separator is null or empty.:0",
            }));
            errors = CaptureErrors(() => Assert.That(constructed.ToString(), Is.EqualTo("0")));
            Assert.That(errors, Is.Empty, "the rejected text is not retained, so the default parses cleanly");

            HLVersion serialized = JsonUtility.FromJson<HLVersion>("{\"m_version\":\"1.x\"}");
            const string invalid = "The numeric identifier in location 2 of version '1.x' split using '.' as separator, which is 'x', is not an unsigned digit only number.:0";
            errors = CaptureErrors(() =>
            {
                Assert.That(serialized.ToString(), Is.EqualTo("1.x"));
                Assert.That(serialized.ToString(), Is.EqualTo("1.x"));
            });
            Assert.That(errors, Is.EqualTo(new[] { invalid, invalid }), "an invalid parse is never cached");

            errors = CaptureErrors(() => Assert.That(new HLVersion("1.2").ToString(), Is.EqualTo("1.2")));
            Assert.That(errors, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
