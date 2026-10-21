using System;
using System.Reflection;
using System.Threading;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // The original SplineThreading type initializer calls PrewarmThreads under UNITY_EDITOR, so any test that reaches the type
    // starts two pool threads that sleep until SplineThreading.Stop aborts them (Application.quitting, or ExitingPlayMode).
    // Left alone they are aborted inside the editor's teardown: at "Cleanup mono" of the EditMode process and right before the
    // domain reload that leaves play mode, where RunThread's catch still runs Debug.LogError and Debug.Log on the aborted thread
    // while the domain unloads. StopAndJoin ends them first through the original Stop and waits until each has finished, so no
    // pool thread is alive when the run's domain goes away. Run restarts a dead pool thread on demand, as it does after Stop.
    public static class DreamteckThreadCleanup
    {
        private const int JoinMilliseconds = 10000;

        public static void StopAndJoin()
        {
            FieldInfo threadsField = typeof(SplineThreading).GetField("threads", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(threadsField, Is.Not.Null, "SplineThreading.threads");
            bool previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                SplineThreading.Stop();
                foreach (object def in (Array)threadsField.GetValue(null))
                {
                    Thread thread = (Thread)def.GetType().GetField("thread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(def);
                    if (thread != null)
                    {
                        Assert.That(thread.Join(JoinMilliseconds), Is.True, "a SplineThreading pool thread ended");
                    }
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previous;
            }
        }
    }
}
