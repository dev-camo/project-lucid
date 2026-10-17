using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(ProjectLucid.Tests.SharedStateIsolationRecorder))]

namespace ProjectLucid.Tests
{
    // Compares SharedStateSnapshot from the start to the finish of every test
    // and appends one JSON line per test to the file named by
    // LUCID_ISOLATION_REPORT; tools/lucidlib/verification.py fails the run on
    // any leak, unchecked test or missing report.
    //
    // Hook, as read in Test Framework 1.1.33: TestRunCallbackListener collects
    // [assembly: TestRunCallback] types from every loaded assembly that
    // references UnityEngine.TestRunner. EditModeLauncher and PlaymodeLauncher
    // both add that listener, and UnityWorkItem.Execute and WorkItemComplete
    // raise TestStarted and TestFinished around the whole test command, so the
    // comparison spans SetUp, UnitySetUp, the body, TearDown and UnityTearDown.
    // EditModeLauncher adds the listener after RunStarted has been raised, so
    // nothing here depends on RunStarted. A listener exception is rethrown into
    // the run, so every step below catches and records instead.
    //
    // Suites are not compared, so a OneTimeSetUp or OneTimeTearDown leak is not
    // attributed to a test. Capture reads static fields, which runs a type's
    // static constructor if nothing has touched the type yet.
    public sealed class SharedStateIsolationRecorder : ITestRunCallback
    {
        public const string ReportVariable = "LUCID_ISOLATION_REPORT";
        public const string NonceVariable = "LUCID_ISOLATION_NONCE";

        [Serializable]
        public sealed class Record
        {
            public string nonce;
            public string test;
            // clean, leak, or unchecked when a snapshot was missing or could not be read.
            public string status;
            public string[] changes;
        }

        private readonly Dictionary<string, SharedStateSnapshot> m_started = new Dictionary<string, SharedStateSnapshot>();
        private readonly Dictionary<string, string> m_startFailures = new Dictionary<string, string>();

        // The full name of the test whose start snapshot was last taken and which
        // has not finished; the hook controls require it to be their own name.
        public static string StartedTest { get; private set; }

        public void RunStarted(ITest testsToRun) { }

        public void RunFinished(ITestResult testResults) { }

        public void TestStarted(ITest test)
        {
            if (test.IsSuite) return;
            m_started.Remove(test.Id);
            m_startFailures.Remove(test.Id);
            try
            {
                m_started[test.Id] = SharedStateSnapshot.Capture();
            }
            catch (Exception exception)
            {
                m_startFailures[test.Id] = "start snapshot failed: " + exception.GetType().FullName + ": " + exception.Message;
            }
            StartedTest = test.FullName;
        }

        public void TestFinished(ITestResult result)
        {
            ITest test = result.Test;
            if (test.IsSuite) return;
            StartedTest = null;
            Record record = Compare(test.FullName, test.Id);
            m_started.Remove(test.Id);
            m_startFailures.Remove(test.Id);
            Append(record);
        }

        private Record Compare(string test, string id)
        {
            if (m_startFailures.TryGetValue(id, out string failure))
                return Make(test, "unchecked", failure);
            if (!m_started.TryGetValue(id, out SharedStateSnapshot before))
                return Make(test, "unchecked", "no start snapshot: the hook did not see this test start");
            try
            {
                return Classify(test, before, SharedStateSnapshot.Capture());
            }
            catch (Exception exception)
            {
                return Make(test, "unchecked", "finish snapshot failed: " + exception.GetType().FullName + ": " + exception.Message);
            }
        }

        // The record for one test from its start and finish snapshots.
        public static Record Classify(string test, SharedStateSnapshot before, SharedStateSnapshot after)
        {
            List<string> changes = after.Differences(before);
            return Make(test, changes.Count == 0 ? "clean" : "leak", changes.ToArray());
        }

        private static Record Make(string test, string status, params string[] changes) => new Record
        {
            nonce = Environment.GetEnvironmentVariable(NonceVariable) ?? "",
            test = test,
            status = status,
            changes = changes,
        };

        public static string Serialize(Record record) => JsonUtility.ToJson(record);

        // Without LUCID_ISOLATION_REPORT (an interactive run) nothing is written;
        // lucid.py sets it for every verdict run and fails when the file is absent.
        private static void Append(Record record)
        {
            string path = Environment.GetEnvironmentVariable(ReportVariable);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                File.AppendAllText(path, Serialize(record) + "\n", new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                // The verdict reports the missing line for this test as unchecked.
                Debug.LogWarning("Isolation report write failed for " + record.test + ": " + exception.Message);
            }
        }
    }
}
