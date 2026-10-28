using System;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.ObjectLeaveChecker (TypeDef 0x02000a14, x86-64 0x6B29C0..0x6B2DA0) on owned
    // GameObjects. Start, InternalUpdate and HasObjectLeft are invoked directly. The Editor may
    // send OnValidate while the component is added, and TimeScaledComponent_SDT.OnValidate reads
    // its s_config lookup, which would otherwise register the SystemConfiguration row. Each test
    // therefore installs an owned, empty TimeCategoryLookup in that cache and restores the prior
    // value afterwards; with no entry the lookup assigns no time category.
    public sealed class OriginalObjectLeaveCheckerTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-leave-checker");
            public readonly ObjectLeaveChecker Checker;
            public readonly Transform Tracked;
            private readonly object m_priorConfig;

            public Fixture()
            {
                m_priorConfig = GetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal");
                try
                {
                    SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", Objects.Own(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                    Checker = TimeScaledReferenceLease.Add<ObjectLeaveChecker>(Objects.Create("checker"));
                    Tracked = Objects.Create("tracked").transform;
                }
                catch
                {
                    // The using statement never receives a fixture whose constructor throws.
                    Dispose();
                    throw;
                }
            }

            public bool HasObjectLeft() => (bool)Call(Checker, "HasObjectLeft");

            public float Timer => (float)Get(Checker, "m_leaveTimer");

            public void Update(float deltaTime) => Call(Checker, "InternalUpdate", deltaTime);

            public void Dispose()
            {
                try { Objects.Dispose(); }
                finally { SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", m_priorConfig); }
            }
        }

        [Test]
        public void DeclarationsAndConstructorDefaults()
        {
            Assert.That((int)typeof(ObjectLeaveChecker).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(ObjectLeaveChecker).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Assert.That(Fields(typeof(ObjectLeaveChecker)), Is.EqualTo(new[]
            {
                "m_leaveDistanceThreshold:Single:0001", "m_minimumTime:Single:0001", "m_boundsCollider:Collider:0001",
                "<WaitingForObjectToLeave>k__BackingField:Boolean:0001", "m_leaveDistanceThresholdSqr:Single:0001",
                "m_leaveTimer:Single:0001", "m_onLeaveCallback:Action:0001", "m_objectToTrack:Transform:0001",
                "m_usingColliderBoundsCheck:Boolean:0001", "m_colliderBoundsCache:Bounds:0001"
            }));
            Assert.That(Methods(typeof(ObjectLeaveChecker)), Is.EqualTo(new[]
            {
                "get_WaitingForObjectToLeave:0886", "set_WaitingForObjectToLeave:0881", "Start:0081", "Activate:0086",
                "InternalUpdate:00c4", "HasObjectLeft:0081", "OnDrawGizmosSelected:0081", "OnValidate:00c4",
                "UpdateCachedValues:0081", ".ctor:1886"
            }));
            var callback = typeof(ObjectLeaveChecker).GetMethod("Activate").GetParameters()[1];
            Assert.That(callback.IsOptional && callback.HasDefaultValue && callback.DefaultValue == null, Is.True);

            // .ctor 0x6B2DA0 writes 5 and 0.5 to +0x50/+0x54.
            using (var fixture = new Fixture())
            {
                Assert.That(Get(fixture.Checker, "m_leaveDistanceThreshold"), Is.EqualTo(5f));
                Assert.That(Get(fixture.Checker, "m_minimumTime"), Is.EqualTo(0.5f));
                Assert.That(fixture.Checker.WaitingForObjectToLeave, Is.False);
            }
        }

        // Start 0x6B29E0 squares the threshold, then records whether a bounds collider is set
        // and, only then, copies its bounds. OnValidate 0x6B2D70 runs the base, then squares.
        [Test]
        public void StartAndOnValidateCacheTheSquaredThresholdAndTheBounds()
        {
            using (var fixture = new Fixture())
            {
                Call(fixture.Checker, "Start");
                Assert.That(Get(fixture.Checker, "m_leaveDistanceThresholdSqr"), Is.EqualTo(25f));
                Assert.That(Get(fixture.Checker, "m_usingColliderBoundsCheck"), Is.False);
                Assert.That(Get(fixture.Checker, "m_colliderBoundsCache"), Is.EqualTo(default(Bounds)));

                var bounds = fixture.Objects.Create("bounds").AddComponent<BoxCollider>();
                Set(fixture.Checker, "m_boundsCollider", bounds);
                Set(fixture.Checker, "m_leaveDistanceThreshold", 3f);
                Call(fixture.Checker, "Start");
                Assert.That(Get(fixture.Checker, "m_leaveDistanceThresholdSqr"), Is.EqualTo(9f));
                Assert.That(Get(fixture.Checker, "m_usingColliderBoundsCheck"), Is.True);
                Assert.That(Get(fixture.Checker, "m_colliderBoundsCache"), Is.EqualTo(bounds.bounds));

                Set(fixture.Checker, "m_leaveDistanceThreshold", 4f);
                Call(fixture.Checker, "OnValidate");
                Assert.That(Get(fixture.Checker, "m_leaveDistanceThresholdSqr"), Is.EqualTo(16f));
            }
        }

        // Activate 0x6B2AA0 arms the checker, clears the timer and stores both arguments.
        [Test]
        public void ActivateArmsTheCheckerAndClearsTheTimer()
        {
            using (var fixture = new Fixture())
            {
                Set(fixture.Checker, "m_leaveTimer", 3f);
                Action callback = () => { };
                fixture.Checker.Activate(fixture.Tracked, callback);
                Assert.That(fixture.Checker.WaitingForObjectToLeave, Is.True);
                Assert.That(fixture.Timer, Is.EqualTo(0f));
                Assert.That(Get(fixture.Checker, "m_objectToTrack"), Is.SameAs(fixture.Tracked));
                Assert.That(Get(fixture.Checker, "m_onLeaveCallback"), Is.SameAs(callback));
                fixture.Checker.Activate(fixture.Tracked);
                Assert.That(Get(fixture.Checker, "m_onLeaveCallback"), Is.Null);
            }
        }

        // HasObjectLeft 0x6B2B60: a missing object has left; otherwise the squared distance from
        // this transform is compared with setae, so the threshold itself counts as left.
        [Test]
        public void HasObjectLeftCountsTheThresholdAndAMissingObjectAsLeft()
        {
            using (var fixture = new Fixture())
            {
                Call(fixture.Checker, "Start");
                Assert.That(fixture.HasObjectLeft(), Is.True, "nothing tracked");
                fixture.Checker.Activate(fixture.Tracked);
                fixture.Tracked.position = new Vector3(4.9f, 0f, 0f);
                Assert.That(fixture.HasObjectLeft(), Is.False, "inside");
                fixture.Tracked.position = new Vector3(0f, 0f, 5f);
                Assert.That(fixture.HasObjectLeft(), Is.True, "at the threshold");
            }
        }

        // HasObjectLeft 0x6B2C65 `ucomiss sqr, thrSqr; setae` and InternalUpdate 0x6B2B05
        // `ucomiss timer, min; jb`: an unordered compare reports not left and never fires, so the
        // timer keeps accumulating. Transform positions cannot be NaN, so the cached threshold
        // and the minimum time carry the NaN. `!(sqr < thrSqr)` or `!(timer < min)` would fire.
        [Test]
        public void UnorderedThresholdsNeitherLeaveNorFire()
        {
            using (var fixture = new Fixture())
            {
                Call(fixture.Checker, "Start");
                fixture.Checker.Activate(fixture.Tracked);
                fixture.Tracked.position = new Vector3(1f, 0f, 0f);
                Set(fixture.Checker, "m_leaveDistanceThresholdSqr", float.NaN);
                Assert.That(fixture.HasObjectLeft(), Is.False, "NaN squared threshold");

                Call(fixture.Checker, "Start");
                int calls = 0;
                fixture.Checker.Activate(fixture.Tracked, () => ++calls);
                fixture.Tracked.position = new Vector3(10f, 0f, 0f);
                Set(fixture.Checker, "m_minimumTime", float.NaN);
                fixture.Update(0.25f);
                fixture.Update(0.25f);
                Assert.That(calls, Is.EqualTo(0), "NaN minimum time");
                Assert.That(fixture.Checker.WaitingForObjectToLeave, Is.True);
                Assert.That(fixture.Timer, Is.EqualTo(0.5f));
            }
        }

        // With a bounds collider HasObjectLeft copies only bounds.center into the cache before
        // Bounds.SqrDistance, so the extents cached by Start stay in use.
        [Test]
        public void BoundsCheckRefreshesOnlyTheCachedCentre()
        {
            using (var fixture = new Fixture())
            {
                var bounds = fixture.Objects.Create("bounds").AddComponent<BoxCollider>();
                bounds.size = new Vector3(2f, 2f, 2f);
                Set(fixture.Checker, "m_boundsCollider", bounds);
                Call(fixture.Checker, "Start");
                fixture.Checker.Activate(fixture.Tracked);
                fixture.Tracked.position = new Vector3(7f, 0f, 0f);
                Set(fixture.Checker, "m_colliderBoundsCache", new Bounds(new Vector3(100f, 0f, 0f), new Vector3(20f, 20f, 20f)));
                Assert.That(fixture.HasObjectLeft(), Is.False, "inside the old extents around the new centre");
                var cache = (Bounds)Get(fixture.Checker, "m_colliderBoundsCache");
                Assert.That(cache.center, Is.EqualTo(bounds.bounds.center));
                Assert.That(cache.extents, Is.EqualTo(new Vector3(10f, 10f, 10f)));

                Set(fixture.Checker, "m_colliderBoundsCache", new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f)));
                Assert.That(fixture.HasObjectLeft(), Is.True, "six beyond the unit extents");
                fixture.Tracked.position = new Vector3(4f, 0f, 0f);
                Assert.That(fixture.HasObjectLeft(), Is.False, "three beyond the unit extents");
            }
        }

        // InternalUpdate 0x6B2AE0: idle or still inside clears the timer. Once the timer reaches
        // the minimum time (ordered >=) the checker disarms, clears the timer and invokes the
        // callback; the timer then advances by this delta from its re-read value.
        [Test]
        public void InternalUpdateFiresAtTheMinimumTimeAndKeepsTheFiringDelta()
        {
            using (var fixture = new Fixture())
            {
                Call(fixture.Checker, "Start");
                int calls = 0;
                Set(fixture.Checker, "m_leaveTimer", 3f);
                fixture.Update(0.25f);
                Assert.That(fixture.Timer, Is.EqualTo(0f), "idle");

                fixture.Checker.Activate(fixture.Tracked, () => ++calls);
                Set(fixture.Checker, "m_leaveTimer", 3f);
                fixture.Update(0.25f);
                Assert.That(fixture.Timer, Is.EqualTo(0f), "inside");

                fixture.Tracked.position = new Vector3(10f, 0f, 0f);
                fixture.Update(0.25f);
                fixture.Update(0.25f);
                Assert.That(fixture.Timer, Is.EqualTo(0.5f));
                Assert.That(calls, Is.EqualTo(0));
                fixture.Update(0.125f);
                Assert.That(calls, Is.EqualTo(1), "fires at exactly the minimum time");
                Assert.That(fixture.Checker.WaitingForObjectToLeave, Is.False);
                Assert.That(fixture.Timer, Is.EqualTo(0.125f));
                fixture.Update(0.25f);
                Assert.That(fixture.Timer, Is.EqualTo(0f), "disarmed");

                fixture.Checker.Activate(fixture.Tracked, () => Set(fixture.Checker, "m_leaveTimer", 10f));
                Set(fixture.Checker, "m_leaveTimer", 0.5f);
                fixture.Update(0.25f);
                Assert.That(fixture.Timer, Is.EqualTo(10.25f), "the callback's timer is re-read");

                fixture.Checker.Activate(fixture.Tracked);
                Set(fixture.Checker, "m_leaveTimer", 0.5f);
                fixture.Update(0.25f);
                Assert.That(fixture.Checker.WaitingForObjectToLeave, Is.False, "fires without a callback");
                Assert.That(fixture.Timer, Is.EqualTo(0.25f));
            }
        }
    }
}
