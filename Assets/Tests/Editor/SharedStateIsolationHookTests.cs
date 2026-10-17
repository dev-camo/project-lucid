using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Controls for the per-test isolation hook. The verdict also requires this
    // fixture's own clean lines in the produced editmode isolation report.
    public sealed class SharedStateIsolationHookTests
    {
        private sealed class Marker : ISystem { }

        private static IDictionary Registry =>
            (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // Fails if the Test Framework never ran the recorder's TestStarted for this test.
        [Test]
        public void HookCapturedThisTestBeforeItsBody()
        {
            Assert.AreEqual(TestContext.CurrentContext.Test.FullName, SharedStateIsolationRecorder.StartedTest);
        }

        // A row left behind is recorded as a leak that names the test and the
        // row; once the test removes it, the same start snapshot is clean.
        [Test]
        public void LeakRecordNamesTheTestAndEachChange()
        {
            string name = "ProjectLucid.IsolationHookSentinel." + Guid.NewGuid().ToString("N");
            VerificationFixtureOwnership.RequireAbsent(Registry, name);
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            ProcessManager.GetSystemRef<Marker>(name);
            object row = Registry[name];
            try
            {
                SharedStateIsolationRecorder.Record leak = SharedStateIsolationRecorder.Classify("Some.Test(1)", before, SharedStateSnapshot.Capture());
                Assert.AreEqual("leak", leak.status);
                Assert.AreEqual("Some.Test(1)", leak.test);
                CollectionAssert.Contains(leak.changes, "added system row '" + name + "'");
                var parsed = JsonUtility.FromJson<SharedStateIsolationRecorder.Record>(SharedStateIsolationRecorder.Serialize(leak));
                Assert.AreEqual(leak.test, parsed.test);
                Assert.AreEqual(leak.status, parsed.status);
                CollectionAssert.AreEqual(leak.changes, parsed.changes);
            }
            finally
            {
                VerificationFixtureOwnership.ReleaseEmptyRow(Registry, name, row);
            }
            SharedStateIsolationRecorder.Record clean = SharedStateIsolationRecorder.Classify("Some.Test(1)", before, SharedStateSnapshot.Capture());
            Assert.AreEqual("clean", clean.status);
            CollectionAssert.IsEmpty(clean.changes);
        }
    }
}
