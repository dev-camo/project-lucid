using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // The data model tests create native ProfilerRecorder handles. These cases run that fixture and
    // check its teardown releases exactly the recorders it created: a recorder created outside the
    // fixture on the same marker must keep recording.
    public sealed class OriginalHLProfilerDataModelCleanupTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static ProfilerRecorder Recorder(HLProfilerData data) =>
            (ProfilerRecorder)typeof(HLProfilerData).GetField("m_recorder", Instance).GetValue(data);

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Instance).GetValue(target);

        // Runs the tracked-marker case and returns its data in creation order: the stopped marker A,
        // which only the fixture still references, then the manager's remaining B and C.
        private static List<HLProfilerData> RunTrackedMarkers(OriginalHLProfilerDataModelTests fixture)
        {
            fixture.TrackedMarkersShareAGroupAndAppearInSnapshots();
            var data = new List<HLProfilerData>(Field<List<HLProfilerData>>(fixture, "m_data"));
            Assert.That(data.Count, Is.EqualTo(1), "the stopped marker stays owned by the fixture");
            foreach (GameObject owner in Field<List<GameObject>>(fixture, "m_objects"))
                foreach (HLProfilerData item in ((IDictionary)Field<object>(owner.GetComponent<HLProfilerDataManager>(), "m_customDataList")).Values)
                    data.Add(item);
            Assert.That(data.Count, Is.EqualTo(3));
            foreach (HLProfilerData item in data) Assert.That(Recorder(item).Valid, Is.True, "recorder is live before teardown");
            return data;
        }

        private static HLProfilerData StartForeign()
        {
            var foreign = new HLProfilerData(new ProfilerMarker("Lucid.HLProfilerTest.A"), "Foreign", 60, HLProfilerData.SampleType.Average, true);
            foreign.Start();
            Assert.That(Recorder(foreign).Valid && Recorder(foreign).IsRunning, Is.True);
            return foreign;
        }

        [Test]
        public void TeardownDisposesEveryOwnedRecorderAndKeepsAForeignOneRecording()
        {
            var fixture = new OriginalHLProfilerDataModelTests();
            HLProfilerData foreign = StartForeign();
            var data = new List<HLProfilerData>();
            try
            {
                data.AddRange(RunTrackedMarkers(fixture));
                fixture.TearDown();
                foreach (HLProfilerData item in data) Assert.That(Recorder(item).Valid, Is.False, item.StatName + " recorder released");
                Assert.That(Recorder(foreign).Valid && Recorder(foreign).IsRunning && foreign.IsActive, Is.True, "foreign recorder untouched");
            }
            finally
            {
                fixture.TearDown();
                foreach (HLProfilerData item in data) if (Recorder(item).Valid) item.Shutdown();
                foreign.Shutdown();
            }
        }

        // Negative control: without the fixture's own reference, nothing releases the stopped marker,
        // so the check above would report the leak.
        [Test]
        public void LosingTheStoppedMarkerOwnershipLeavesItsRecorderLive()
        {
            var fixture = new OriginalHLProfilerDataModelTests();
            var data = new List<HLProfilerData>();
            try
            {
                data.AddRange(RunTrackedMarkers(fixture));
                Field<List<HLProfilerData>>(fixture, "m_data").Clear();
                fixture.TearDown();
                Assert.That(Recorder(data[0]).Valid, Is.True, "stopped marker leaks without fixture ownership");
                Assert.That(Recorder(data[1]).Valid || Recorder(data[2]).Valid, Is.False, "tracked markers still released");
            }
            finally
            {
                fixture.TearDown();
                foreach (HLProfilerData item in data) if (Recorder(item).Valid) item.Shutdown();
            }
        }
    }
}
