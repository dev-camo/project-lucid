using System.Collections;
using System.Reflection;
using Hardlight.Analytics;
using Hardlight.JSON;
using Hardlight.Pooling;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // The offline provider reports IsSupported false, so Create never reaches
    // Spawn; the original private Spawn body is entered directly for an owned
    // event type whose pool exists only for this test and is reset afterwards.
    public sealed class OriginalAnalyticsEventGapTests
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public sealed class OwnedPooledEvent : AnalyticsEvent<OwnedPooledEvent>
        {
            public int Initialisations;
            public int UsedWhenInitialised = -1;

            protected override void Initialise()
            {
                ++Initialisations;
                UsedWhenInitialised = ObjectPool<OwnedPooledEvent>.UsedObjectCount;
            }

            protected override void FillData(JSONHashtable data) { }
        }

        // AnalyticsEvent<T>.Spawn 0x06000015, shared x86-64 0xf1b700:
        // ObjectPool.IsInitialised 0xf1b75f, InitialisePool with the literal 5
        // 0xf1b88d/0xf1b892 only when absent, ObjectPool.Spawn 0xf1b7c2, then
        // the virtual Initialise (slot +0x1b8) 0xf1b7d7 on the spawned event.
        // A different capacity, re-initialising the pool, initialising before
        // the spawn or skipping Initialise fails here.
        [Test]
        public void SpawnBuildsAFiveEventPoolOnceAndInitialisesEachSpawnedEvent()
        {
            MethodInfo spawn = typeof(AnalyticsEvent<OwnedPooledEvent>).GetMethod("Spawn", AnyStatic);
            FieldInfo poolField = typeof(ObjectPool<OwnedPooledEvent>).GetField("s_objectPool", AnyStatic);
            Assert.That(ObjectPool<OwnedPooledEvent>.IsInitialised(), Is.False, "The owned event type starts without a pool.");
            try
            {
                var first = (OwnedPooledEvent)spawn.Invoke(null, null);
                object pool = poolField.GetValue(null);
                Assert.That(pool, Is.Not.Null);
                Assert.That(FreeCount(pool), Is.EqualTo(4), "Five pooled events, one spawned.");
                Assert.That(ObjectPool<OwnedPooledEvent>.UsedObjectCount, Is.EqualTo(1));
                Assert.That(first.Initialisations, Is.EqualTo(1));
                Assert.That(first.UsedWhenInitialised, Is.EqualTo(1), "Initialise follows the pool spawn.");

                var second = (OwnedPooledEvent)spawn.Invoke(null, null);
                Assert.That(poolField.GetValue(null), Is.SameAs(pool), "An existing pool is kept.");
                Assert.That(ReferenceEquals(first, second), Is.False);
                Assert.That(FreeCount(pool), Is.EqualTo(3));
                Assert.That(second.Initialisations, Is.EqualTo(1));
                Assert.That(second.UsedWhenInitialised, Is.EqualTo(2));
                Assert.That(first.Initialisations, Is.EqualTo(1));
            }
            finally
            {
                poolField.SetValue(null, null);
            }
        }

        private static int FreeCount(object pool) =>
            ((ICollection)typeof(ObjectPool<OwnedPooledEvent>).GetField("m_freeObjects", AnyInstance).GetValue(pool)).Count;
    }
}
