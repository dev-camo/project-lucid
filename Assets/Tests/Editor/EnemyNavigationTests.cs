using System;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldSurfaceShared;
using UnityEngine;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // The four enemy navigations: forward, home radius, static and return after chase, and spline. Expected values come
    // from the 1.10.1 x86-64 bodies cited per test. Each navigation is a real component on an owned inactive host; Unity
    // runs SetUp from Awake, which Edit Mode never reaches, and from the Editor OnValidate that AddComponent sends
    // (0x578060; CanValidate 0x6B1910 always answers true), so the probes also call the shipped SetUp directly. The spline
    // navigation is only checked without a spline. Its initialised paths (the start direction and squared follow
    // distance, the GetDestination walk with its end reversal and loop wrap, and the nearest-location line that
    // ConstrainPosition uses) need a spline with baked length and lookup data; an InertSpline in Edit Mode carries
    // none, and the tracker positions cannot be derived without a native run. That coverage is owed.
    public sealed class EnemyNavigationTests
    {
        private const float Tolerance = 1e-4f;

        private static readonly Type ConstrainType = typeof(EnemyNavigation).GetNestedType("ConstrainType", BindingFlags.NonPublic);

        private static T NewNavigation<T>(Owned owned, Vector3 position, Quaternion? rotation = null) where T : Component
        {
            GameObject host = owned.Host(Name(), false);
            host.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            return host.AddComponent<T>();
        }

        private static void Constrain(EnemyNavigation navigation, bool enabled, int type, float distance = 0f, Collider collider = null)
        {
            Set(navigation, "m_constrain", enabled);
            Set(navigation, "m_constrainType", Enum.ToObject(ConstrainType, type));
            Set(navigation, "m_constrainDistance", distance);
            Set(navigation, "m_constrainCollider", collider);
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string why) =>
            Assert.That((actual - expected).magnitude, Is.LessThan(Tolerance), why + ": expected " + expected + " but was " + actual);

        // ---- forward ----

        // 0x5783D0 stores 10 (0x41200000) in the look-ahead distance and 1 in the constrain type (the base's Distance);
        // 0x5782B0 marks the navigation initialised, which nothing else does. AddComponent already sent the Editor
        // OnValidate (0x578060), which reaches that SetUp because CanValidate (0x6B1910) is a bare "mov al, 1".
        [Test]
        public void ForwardStartsWithTenUnitsAheadAndInitialisesOnSetUp()
        {
            Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationForwardProbe>(owned, Vector3.zero);
                Assert.That(Get<float>(navigation, "m_lookAheadDistance"), Is.EqualTo(10f));
                Assert.That(Convert.ToInt32(Get<object>(navigation, "m_constrainType")), Is.EqualTo(1));
                Assert.That(navigation.IsInitialised, Is.True, "the Editor OnValidate ran SetUp");
                navigation.ClearInitialised();
                Assert.That(navigation.IsInitialised, Is.False);
                navigation.RunSetUp();
                Assert.That(navigation.IsInitialised, Is.True);
            });
        }

        // 0x5782E0: the transform position plus its forward scaled by the look-ahead distance (get_position first, then
        // get_forward, mulss/mulps by [rbx + 0x3C], addps/addss). A host turned a quarter about Y looks along +X.
        [Test]
        public void ForwardDestinationIsAHeadOfTheTransformAlongItsForward()
        {
            Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationForwardProbe>(owned, new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
                Set(navigation, "m_lookAheadDistance", 4f);
                AssertNear(navigation.GetDestination(), new Vector3(5f, 2f, 3f), "four ahead");
                Set(navigation, "m_lookAheadDistance", 0f);
                AssertNear(navigation.GetDestination(), new Vector3(1f, 2f, 3f), "nothing ahead");
                Set(navigation, "m_lookAheadDistance", -2f);
                AssertNear(navigation.GetDestination(), new Vector3(-1f, 2f, 3f), "a negative distance looks behind");
            });
        }

        // 0x578340 returns its argument and 0x578350 always answers true, even for a navigation that is constrained to
        // nothing; starting and interrupting (0x5782C0, 0x5782D0) are bare returns.
        [Test]
        public void ForwardNeverConstrainsAndHasNothingToStartOrInterrupt()
        {
            Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationForwardProbe>(owned, Vector3.zero);
                navigation.ClearInitialised();
                Constrain(navigation, true, 1, 0f);
                var far = new Vector3(100f, -50f, 7f);
                Assert.That(navigation.CanReachPosition(far), Is.True);
                Assert.That(navigation.ConstrainPosition(far), Is.EqualTo(far));
                Assert.That(() => { navigation.StartNavigation(); navigation.InterruptNavigation(); }, Throws.Nothing);
                Assert.That(navigation.IsInitialised, Is.False, "starting does not initialise");
            });
        }

        // ---- home radius and static ----

        // 0x578A70 stores 10 and 5 (0x41200000, 0x40A00000) in the radius and minimum distance and 1 in the constrain type.
        [Test]
        public void HomeRadiusStartsWithTenAndFive()
        {
            Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, Vector3.zero);
                Assert.That(Get<float>(navigation, "m_homeRadius"), Is.EqualTo(10f));
                Assert.That(navigation.m_minNewDestinationDistance, Is.EqualTo(5f));
                Assert.That(Convert.ToInt32(Get<object>(navigation, "m_constrainType")), Is.EqualTo(1));
            });
        }

        // 0x5783F0 takes the home from the transform, squares the radius and the minimum distance (mulss) and marks the
        // navigation initialised; 0x579A20 only takes the home, so the static navigation never reports initialised.
        [Test]
        public void SetUpTakesTheHomeFromTheTransform()
        {
            Owned.Run(owned =>
            {
                var home = new Vector3(3f, 0f, 4f);
                var radius = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, home);
                Set(radius, "m_homeRadius", 2f);
                radius.m_minNewDestinationDistance = 1.5f;
                radius.ClearInitialised();
                radius.RunSetUp();
                Assert.That(Get<Vector3>(radius, "m_home"), Is.EqualTo(home));
                Assert.That(Get<float>(radius, "m_homeRadiusSqr"), Is.EqualTo(4f));
                Assert.That(Get<float>(radius, "m_minNewDestinationDistanceSquared"), Is.EqualTo(2.25f));
                Assert.That(radius.IsInitialised, Is.True);

                var fixedHome = NewNavigation<EnemyNavigationStaticProbe>(owned, home);
                fixedHome.RunSetUp();
                Assert.That(Get<Vector3>(fixedHome, "m_home"), Is.EqualTo(home));
                Assert.That(fixedHome.GetDestination(), Is.EqualTo(home));
                Assert.That(fixedHome.IsInitialised, Is.False, "the static navigation never marks itself initialised");
            });
        }

        // 0x5787C0 raises the interrupted flag and nothing else; 0x579A60 and 0x579A50 are bare returns.
        [Test]
        public void InterruptingOnlyRaisesTheFlagOfTheHomeRadiusNavigation()
        {
            Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, Vector3.zero);
                Assert.That(Get<bool>(navigation, "m_wasInterrupted"), Is.False);
                navigation.InterruptNavigation();
                Assert.That(Get<bool>(navigation, "m_wasInterrupted"), Is.True);
                var fixedHome = NewNavigation<EnemyNavigationStaticProbe>(owned, Vector3.zero);
                Assert.That(() => { fixedHome.StartNavigation(); fixedHome.InterruptNavigation(); }, Throws.Nothing);
            });
        }

        // 0x578A20 (jbe at 0x578A4B, jb at 0x578A51): the minimum distance is reset to zero only when it is positive and
        // at least the radius; a smaller, zero, negative or unordered value stays, and so does everything when the radius
        // is unordered.
        [Test]
        public void ValidatingZeroesAMinimumDistanceThatReachesTheRadius()
        {
            Owned.Run(owned =>
            {
                foreach ((float radius, float minimum, float expected) in new[]
                {
                    (5f, 5f, 0f), (5f, 6f, 0f), (5f, 4.9f, 4.9f), (5f, 0f, 0f), (5f, -1f, -1f),
                    (5f, float.NaN, float.NaN), (float.NaN, 1f, 1f), (0f, 1f, 0f),
                })
                {
                    var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, Vector3.zero);
                    Set(navigation, "m_homeRadius", radius);
                    navigation.m_minNewDestinationDistance = minimum;
                    Call(navigation, "OnValidate");
                    Assert.That(navigation.m_minNewDestinationDistance, Is.EqualTo(expected), "radius " + radius + ", minimum " + minimum);
                }
            });
        }

        // 0x5788A0 and 0x579B40: unconstrained navigations reach everything; a distance constraint reaches the points
        // within its distance of home (ucomiss against the squared distance, so the boundary counts); an unknown constrain
        // type reaches nothing.
        [Test]
        public void TheDistanceConstraintReachesPointsWithinItsDistanceOfHome()
        {
            Owned.Run(owned =>
            {
                var home = new Vector3(1f, 2f, 3f);
                var radius = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, home);
                radius.RunSetUp();
                var fixedHome = NewNavigation<EnemyNavigationStaticProbe>(owned, home);
                fixedHome.RunSetUp();
                foreach (EnemyNavigation navigation in new EnemyNavigation[] { radius, fixedHome })
                {
                    string who = navigation.GetType().Name;
                    Constrain(navigation, false, 1, 0.5f);
                    Assert.That(navigation.CanReachPosition(new Vector3(500f, 0f, 0f)), Is.True, who + " unconstrained");
                    Constrain(navigation, true, 1, 2f);
                    Assert.That(navigation.CanReachPosition(home), Is.True, who + " at home");
                    Assert.That(navigation.CanReachPosition(home + new Vector3(0f, 0f, 2f)), Is.True, who + " exactly at the distance");
                    Assert.That(navigation.CanReachPosition(home + new Vector3(0f, 2.01f, 0f)), Is.False, who + " just beyond");
                    Assert.That(navigation.CanReachPosition(home + new Vector3(1.5f, 1.5f, 0f)), Is.False, who + " diagonally beyond");
                    Assert.That(navigation.CanReachPosition(home + new Vector3(1f, 1f, 1f)), Is.True, who + " diagonally within");
                    Constrain(navigation, true, 3, 2f);
                    Assert.That(navigation.CanReachPosition(home), Is.False, who + " with an unknown constrain type");
                    Constrain(navigation, true, 0, 2f);
                    Assert.That(navigation.CanReachPosition(home), Is.False, who + " with no constrain type");
                }
            });
        }

        // 0x5787E0 and 0x579A80: a reachable position is returned unchanged; a distance constraint pulls an unreachable
        // one back along the line from home to the point at exactly the distance; an unknown constrain type leaves it.
        [Test]
        public void TheDistanceConstraintPullsAnUnreachablePositionBackToTheDistance()
        {
            Owned.Run(owned =>
            {
                var home = new Vector3(1f, 2f, 3f);
                var radius = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, home);
                radius.RunSetUp();
                var fixedHome = NewNavigation<EnemyNavigationStaticProbe>(owned, home);
                fixedHome.RunSetUp();
                foreach (EnemyNavigation navigation in new EnemyNavigation[] { radius, fixedHome })
                {
                    string who = navigation.GetType().Name;
                    Constrain(navigation, true, 1, 2f);
                    var within = home + new Vector3(1f, 0f, 0f);
                    AssertNear(navigation.ConstrainPosition(within), within, who + " within");
                    AssertNear(navigation.ConstrainPosition(home + new Vector3(6f, 0f, 0f)), home + new Vector3(2f, 0f, 0f), who + " along X");
                    AssertNear(navigation.ConstrainPosition(home + new Vector3(0f, -9f, 0f)), home + new Vector3(0f, -2f, 0f), who + " along -Y");
                    AssertNear(navigation.ConstrainPosition(home + new Vector3(3f, 0f, 4f)), home + new Vector3(1.2f, 0f, 1.6f), who + " along a diagonal");
                    Constrain(navigation, true, 3, 2f);
                    var beyond = home + new Vector3(50f, 0f, 0f);
                    AssertNear(navigation.ConstrainPosition(beyond), beyond, who + " with an unknown constrain type");
                }
            });
        }

        // A volume constraint reaches the points the collider contains (Collider.ClosestPoint returns a contained point
        // unchanged, which the squared-distance equality at 0x578938 accepts) and pulls the others to its surface.
        [Test]
        public void TheVolumeConstraintFollowsTheColliderClosestPoint()
        {
            Owned.Run(owned =>
            {
                GameObject volume = owned.Host(Name(), true);
                var sphere = volume.AddComponent<SphereCollider>();
                sphere.radius = 1f;
                var radius = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, Vector3.zero);
                var fixedHome = NewNavigation<EnemyNavigationStaticProbe>(owned, Vector3.zero);
                foreach (EnemyNavigation navigation in new EnemyNavigation[] { radius, fixedHome })
                {
                    string who = navigation.GetType().Name;
                    Constrain(navigation, true, 2, 0f, sphere);
                    Assert.That(navigation.CanReachPosition(new Vector3(0.5f, 0f, 0f)), Is.True, who + " inside");
                    Assert.That(navigation.CanReachPosition(new Vector3(3f, 0f, 0f)), Is.False, who + " outside");
                    AssertNear(navigation.ConstrainPosition(new Vector3(0.5f, 0f, 0f)), new Vector3(0.5f, 0f, 0f), who + " inside");
                    AssertNear(navigation.ConstrainPosition(new Vector3(3f, 0f, 0f)), new Vector3(1f, 0f, 0f), who + " outside");
                }
            });
        }

        // 0x578440 draws x then z from Random.Range(-m_homeRadius, m_homeRadius) (calls at 0x5786D7 and 0x5786F1, the
        // radius re-read from [rbx + 0x3C] before each), turns (x, 0, z) by FromToRotation(Vector3.down, -transform.up)
        // (0x578503: the static down in xmm0:xmm1 as the from direction, the negated up as the to direction), adds the
        // home, and tries at most LoopLimit (20) times (0x5786AD..0x5787A2), keeping the last spot when none qualifies.
        // The tests take the raw engine stream from the same seed with the same Range bounds and write the expected
        // spot as an explicit component mapping of those draws; none of the navigation's geometry is repeated here.
        private static float[] Draws(int seed, float radius, int count)
        {
            Random.InitState(seed);
            var draws = new float[count];
            for (int i = 0; i < count; i++)
                draws[i] = Random.Range(-radius, radius);
            return draws;
        }

        private static Vector3 StartAndRead(EnemyNavigationHomeRadiusProbe navigation, int seed)
        {
            Random.InitState(seed);
            navigation.StartNavigation();
            return navigation.GetDestination();
        }

        private static void WithRandomState(Action body)
        {
            Random.State saved = Random.state;
            try { body(); }
            finally { Random.state = saved; }
        }

        // SetUp caches the squared radius (0x5783F0) but each attempt draws with the live m_homeRadius, so a radius of 10 at
        // SetUp followed by a radius of 1 accepts every spot (at most sqrt(2) from home) on the first attempt.
        private static (Vector3 actual, float x, float z) FirstSpot(Owned owned, Vector3 home, Quaternion hostRotation, int seed)
        {
            var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, home, hostRotation);
            Set(navigation, "m_homeRadius", 10f);
            navigation.m_minNewDestinationDistance = 0f;
            navigation.RunSetUp();
            Set(navigation, "m_homeRadius", 1f);
            float[] draws = Draws(seed, 1f, 2);
            Assert.That(Mathf.Abs(draws[0]) > 1e-3f && Mathf.Abs(draws[1]) > 1e-3f && Mathf.Abs(Mathf.Abs(draws[0]) - Mathf.Abs(draws[1])) > 1e-3f,
                Is.True, "seed " + seed + " draws two separable values");
            return (StartAndRead(navigation, seed), draws[0], draws[1]);
        }

        // An upright host: -up is down, FromToRotation(down, down) is the identity, so (x, 0, z) lands at
        // home + (x, 0, z) with x the first draw and z the second.
        [Test]
        public void StartingPlacesTheFirstSpotAtTheFirstDrawAlongXAndTheSecondAlongZ()
        {
            WithRandomState(() => Owned.Run(owned =>
            {
                (Vector3 actual, float x, float z) = FirstSpot(owned, new Vector3(2f, 1f, -3f), Quaternion.identity, 7);
                AssertNear(actual, new Vector3(2f + x, 1f, -3f + z), "x then z");
                // Engine behaviour, not the navigation's: UnityEngine.Random.Range(-1, 1) after InitState(7) yields these two
                // draws. The spot is then the home plus (first, 0, second) by the mapping above.
                Assert.That(x, Is.EqualTo(-0.0195802450f).Within(1e-6f), "engine first draw for seed 7");
                Assert.That(z, Is.EqualTo(0.350559354f).Within(1e-6f), "engine second draw for seed 7");
                AssertNear(actual, new Vector3(1.98041975f, 1f, -2.64944077f), "literal spot for seed 7");
            }));
        }

        // A host turned 90 degrees about X: up is (0, 0, 1), so -up is (0, 0, -1). The shortest turn from (0, -1, 0) to
        // (0, 0, -1) is +90 degrees about X (the cross product is +X), which takes y to z and z to -y:
        // (x, 0, z) -> (x, -z, 0).
        [Test]
        public void StartingPlacesTheSpotsOnTheEnemysOwnPlane()
        {
            WithRandomState(() => Owned.Run(owned =>
            {
                (Vector3 actual, float x, float z) = FirstSpot(owned, new Vector3(1f, 2f, 3f), Quaternion.Euler(90f, 0f, 0f), 11);
                AssertNear(actual, new Vector3(1f + x, 2f - z, 3f), "turned onto the tilted plane");
                // Engine behaviour: UnityEngine.Random.Range(-1, 1) after InitState(11) yields these two draws.
                Assert.That(x, Is.EqualTo(-0.708843470f).Within(1e-6f), "engine first draw for seed 11");
                Assert.That(z, Is.EqualTo(0.155140519f).Within(1e-6f), "engine second draw for seed 11");
                AssertNear(actual, new Vector3(0.291156530f, 1.84485948f, 3f), "literal spot for seed 11");
            }));
        }

        // Negative control for the two literals above: the plausible variants put the spot elsewhere. Reversed operands,
        // FromToRotation(-up, down) (the inverse turn), give (x, z, 0); drawing z first gives (z, 0, x) upright and
        // (z, -x, 0) tilted; no turn at all gives (x, 0, z) on the tilted host.
        [Test]
        public void TheSpotSeparatesTheRotationOperandOrderAndTheDrawOrder()
        {
            WithRandomState(() => Owned.Run(owned =>
            {
                (Vector3 upright, float ux, float uz) = FirstSpot(owned, new Vector3(2f, 1f, -3f), Quaternion.identity, 7);
                AssertFar(upright, new Vector3(2f + uz, 1f, -3f + ux), "upright, z drawn first");

                var home = new Vector3(1f, 2f, 3f);
                (Vector3 tilted, float x, float z) = FirstSpot(owned, home, Quaternion.Euler(90f, 0f, 0f), 11);
                AssertNear(tilted, new Vector3(1f + x, 2f - z, 3f), "pinned tilted spot");
                AssertFar(tilted, new Vector3(1f + x, 2f + z, 3f), "FromToRotation(-up, down)");
                AssertFar(tilted, new Vector3(1f + z, 2f - x, 3f), "tilted, z drawn first");
                AssertFar(tilted, new Vector3(1f + x, 2f, 3f + z), "no turn");
            }));
        }

        // The squared distance from home of the upright spot of attempt n: its x draw squared plus its z draw squared.
        private static float SquaredSpot(float[] draws, int n) => draws[2 * n] * draws[2 * n] + draws[2 * n + 1] * draws[2 * n + 1];

        private static void AssertFar(Vector3 actual, Vector3 variant, string why) =>
            Assert.That((actual - variant).magnitude, Is.GreaterThan(Tolerance), why + " would give " + variant + ", the same as " + actual);

        // 0x57875D (ucomiss m_homeRadiusSqr, d; jb rejects) and 0x57878B (ucomiss d, minSq; setae accepts): attempt n uses
        // draws 2n and 2n + 1, and the first spot with a squared distance from home in [36, 100] wins. The host stands
        // upright at home, so the squared distance of attempt n is draws[2n]^2 + draws[2n + 1]^2.
        [Test]
        public void StartingPicksTheFirstSpotWithinTheRadiusAndFarEnoughFromTheEnemy()
        {
            WithRandomState(() => Owned.Run(owned =>
            {
                var home = new Vector3(2f, 1f, -3f);
                var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, home);
                Set(navigation, "m_homeRadius", 10f);
                navigation.m_minNewDestinationDistance = 6f;
                navigation.RunSetUp();
                int rejectedFirst = 0;
                for (int seed = 1; seed <= 40; seed++)
                {
                    float[] d = Draws(seed, 10f, 40);
                    int n = 0;
                    while (n < 19 && !(SquaredSpot(d, n) >= 36f && SquaredSpot(d, n) <= 100f)) n++;
                    AssertNear(StartAndRead(navigation, seed), new Vector3(2f + d[2 * n], 1f, -3f + d[2 * n + 1]), "seed " + seed + ", attempt " + n);
                    if (n > 0) rejectedFirst++;
                }
                Assert.That(rejectedFirst, Is.GreaterThan(0), "some seed rejects a spot and takes a later one");
            }));
        }

        // The same comparisons at their boundaries: a spot exactly on the squared radius is inside and a spot exactly at
        // the squared minimum distance is far enough, while an unordered radius or minimum accepts nothing and leaves the
        // twentieth spot (draws 38 and 39). Home and enemy are the origin; the boundary is the squared length of the first
        // spot as the navigation returned it when everything is accepted, so the comparison sees the same bits.
        [Test]
        public void StartingAcceptsASpotExactlyOnTheRadiusAndExactlyAtTheMinimumDistance()
        {
            WithRandomState(() => Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, Vector3.zero);
                Set(navigation, "m_homeRadius", 10f);
                navigation.RunSetUp();
                foreach (int seed in new[] { 1, 2, 3, 4, 5 })
                {
                    float[] d = Draws(seed, 10f, 40);
                    var twentieth = new Vector3(d[38], 0f, d[39]);

                    Set(navigation, "m_homeRadiusSqr", 1e6f);
                    Set(navigation, "m_minNewDestinationDistanceSquared", 0f);
                    Vector3 first = StartAndRead(navigation, seed);
                    AssertNear(first, new Vector3(d[0], 0f, d[1]), "seed " + seed + " accepts the first spot");
                    Assert.That((first - twentieth).magnitude, Is.GreaterThan(Tolerance), "seed " + seed + " has a distinct twentieth spot");
                    float exact = first.sqrMagnitude;

                    Set(navigation, "m_homeRadiusSqr", exact);
                    AssertNear(StartAndRead(navigation, seed), first, "seed " + seed + " exactly on the radius");

                    Set(navigation, "m_homeRadiusSqr", 1e6f);
                    Set(navigation, "m_minNewDestinationDistanceSquared", exact);
                    AssertNear(StartAndRead(navigation, seed), first, "seed " + seed + " exactly at the minimum distance");

                    Set(navigation, "m_minNewDestinationDistanceSquared", float.NaN);
                    AssertNear(StartAndRead(navigation, seed), twentieth, "seed " + seed + " with an unordered minimum");

                    Set(navigation, "m_homeRadiusSqr", float.NaN);
                    Set(navigation, "m_minNewDestinationDistanceSquared", 0f);
                    AssertNear(StartAndRead(navigation, seed), twentieth, "seed " + seed + " with an unordered radius");
                }
            }));
        }

        // A radius of 4 keeps every spot within sqrt(32) of home, short of the minimum distance of 100, so all twenty
        // attempts fail and the last one, draws 38 and 39, stays.
        [Test]
        public void StartingKeepsTheTwentiethSpotWhenNoneQualifies()
        {
            WithRandomState(() => Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, Vector3.zero);
                Set(navigation, "m_homeRadius", 4f);
                navigation.m_minNewDestinationDistance = 100f;
                navigation.RunSetUp();
                foreach (int seed in new[] { 3, 17, 91 })
                {
                    float[] d = Draws(seed, 4f, 40);
                    AssertNear(StartAndRead(navigation, seed), new Vector3(d[38], 0f, d[39]), "seed " + seed);
                }
            }));
        }

        // The enemy position is projected onto the plane through home (Plane.ClosestPointOnPlane) before the distance
        // test. The host is lifted 50 units along its up axis, so unprojected every spot would be at least 50 away and
        // the first would always be taken; projected, the position is home itself and a first spot closer than the
        // minimum distance of 3 is rejected in favour of the first in [9, 25].
        [Test]
        public void StartingMeasuresTheMinimumDistanceFromTheEnemyProjectedOntoThePlane()
        {
            WithRandomState(() => Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, Vector3.zero);
                Set(navigation, "m_homeRadius", 5f);
                navigation.m_minNewDestinationDistance = 3f;
                navigation.RunSetUp();
                navigation.transform.position = new Vector3(0f, 50f, 0f);
                int checkedSeeds = 0;
                for (int seed = 1; seed <= 60; seed++)
                {
                    float[] d = Draws(seed, 5f, 40);
                    if (SquaredSpot(d, 0) >= 9f) continue;
                    checkedSeeds++;
                    int n = 1;
                    while (n < 19 && !(SquaredSpot(d, n) >= 9f && SquaredSpot(d, n) <= 25f)) n++;
                    Vector3 actual = StartAndRead(navigation, seed);
                    AssertNear(actual, new Vector3(d[2 * n], 0f, d[2 * n + 1]), "seed " + seed + ", attempt " + n);
                    Assert.That((actual - new Vector3(d[0], 0f, d[1])).magnitude, Is.GreaterThan(Tolerance), "seed " + seed + " does not keep the near first spot");
                }
                Assert.That(checkedSeeds, Is.GreaterThan(0), "some seed starts with a spot nearer than the minimum distance");
            }));
        }

        // 0x578474: the home moves to the position only when the reset flag is set and the navigation was interrupted, and
        // that clears the interrupted flag; otherwise the home and the flag stay.
        [Test]
        public void StartingMovesTheHomeToTheEnemyOnlyAfterAnInterruptWhenAsked()
        {
            WithRandomState(() => Owned.Run(owned =>
            {
                var origin = new Vector3(1f, 0f, 1f);
                var moved = new Vector3(8f, 0f, 2f);
                foreach ((bool reset, bool interrupt, bool homeMoves) in new[]
                {
                    (true, true, true), (true, false, false), (false, true, false), (false, false, false),
                })
                {
                    var navigation = NewNavigation<EnemyNavigationHomeRadiusProbe>(owned, origin);
                    Set(navigation, "m_resetHomeOnInterrupt", reset);
                    navigation.RunSetUp();
                    navigation.transform.position = moved;
                    if (interrupt) navigation.InterruptNavigation();
                    navigation.StartNavigation();
                    string why = "reset " + reset + ", interrupted " + interrupt;
                    Assert.That(Get<Vector3>(navigation, "m_home"), Is.EqualTo(homeMoves ? moved : origin), why);
                    Assert.That(Get<bool>(navigation, "m_wasInterrupted"), Is.EqualTo(interrupt && !homeMoves), why);
                }
            }));
        }

        // ---- spline ----

        // 0x579940 stores a follow distance of 3 and a step size of 1 (0x40400000, 0x3F800000) and the Distance constrain
        // type, and creates the two empty surface collections.
        [Test]
        public void SplineStartsWithAFollowDistanceOfThreeAndAStepOfOne()
        {
            Owned.Run(owned =>
            {
                var navigation = NewNavigation<EnemyNavigationSplineProbe>(owned, Vector3.zero);
                Assert.That(Get<float>(navigation, "m_followDistance"), Is.EqualTo(3f));
                Assert.That(Get<float>(navigation, "m_trackerStepSize"), Is.EqualTo(1f));
                Assert.That(Convert.ToInt32(Get<object>(navigation, "m_constrainType")), Is.EqualTo(1));
                Assert.That(Get<System.Collections.ICollection>(navigation, "m_trackableSurfaces").Count, Is.EqualTo(0));
                Assert.That(Get<System.Collections.ICollection>(navigation, "m_trackableSurfacesList").Count, Is.EqualTo(0));
                Assert.That(navigation.GetReversing(), Is.False);
            });
        }

        // 0x578B30 initialises only when a spline is assigned (op_Equality at 0x578BE4, jne 0x578E2A stores r15b = 0): with
        // none the navigation stays uninitialised, every query falls back to the transform and the unconstrained answers
        // (0x579369, 0x57943A, 0x579824, 0x578F42) even for a constrained navigation, and a reversing start is ignored.
        [Test]
        public void WithoutASplineTheSplineNavigationStaysUninitialisedAndFallsBackToTheTransform()
        {
            Owned.Run(owned =>
            {
                var position = new Vector3(4f, 5f, 6f);
                var navigation = NewNavigation<EnemyNavigationSplineProbe>(owned, position);
                Set(navigation, "m_startReversed", true);
                Constrain(navigation, true, 1, 0f);
                navigation.RunSetUp();
                Assert.That(navigation.IsInitialised, Is.False);
                Assert.That(navigation.GetReversing(), Is.False, "the start direction is only taken with a spline");
                Assert.That(navigation.GetDestination(), Is.EqualTo(position));
                Assert.That(navigation.CanReachPosition(new Vector3(99f, 0f, 0f)), Is.True);
                Assert.That(navigation.ConstrainPosition(new Vector3(99f, 0f, 0f)), Is.EqualTo(new Vector3(99f, 0f, 0f)));
                Assert.That(navigation.GetLocationWorldRotation(), Is.EqualTo(Quaternion.identity));
                Assert.That(() => { navigation.StartNavigation(); navigation.InterruptNavigation(); }, Throws.Nothing);
            });
        }

        // ---- declarations ----

        // The navigations carry the IL2CPP options in their metadata order and none of the graph authoring attributes.
        [Test]
        public void NavigationsCarryTheirOriginalIl2CppOptions()
        {
            AssertIl2CppOptions(typeof(EnemyNavigationForward), 2, 1);
            AssertIl2CppOptions(typeof(EnemyNavigationHomeRadius), 1, 2);
            AssertIl2CppOptions(typeof(EnemyNavigationSpline), 1, 2);
            AssertIl2CppOptions(typeof(EnemyNavigationStaticAndReturnAfterChase), 1, 2);
        }

        // Authored fields and their editor attributes in metadata order, so that the serialised layout of the original
        // scenes and prefabs still lines up.
        [Test]
        public void AuthoredFieldsKeepTheirEditorAttributes()
        {
            AssertAttributes(typeof(EnemyNavigationForward), "m_lookAheadDistance", typeof(SerializeField), typeof(MinAttribute), typeof(TooltipAttribute));
            AssertAttributes(typeof(EnemyNavigationHomeRadius), "m_homeRadius", typeof(TooltipAttribute), typeof(MinAttribute), typeof(SerializeField));
            AssertAttributes(typeof(EnemyNavigationHomeRadius), "m_minNewDestinationDistance", typeof(MinAttribute), typeof(TooltipAttribute));
            AssertAttributes(typeof(EnemyNavigationHomeRadius), "m_resetHomeOnInterrupt", typeof(TooltipAttribute), typeof(SerializeField));
            AssertAttributes(typeof(EnemyNavigationSpline), "m_spline", typeof(SerializeField));
            AssertAttributes(typeof(EnemyNavigationSpline), "m_startPosition", typeof(UnityEngine.RangeAttribute), typeof(SerializeField));
            AssertAttributes(typeof(EnemyNavigationSpline), "m_followDistance", typeof(MinAttribute), typeof(SerializeField));
            AssertAttributes(typeof(EnemyNavigationSpline), "m_trackerStepSize", typeof(SerializeField), typeof(MinAttribute));
            AssertAttributes(typeof(EnemyNavigationSpline), "m_startReversed", typeof(SerializeField));
            AssertAttributes(typeof(EnemyNavigationSpline), "m_loop", typeof(SerializeField));
            Assert.That(Tooltip(typeof(EnemyNavigationForward).GetField("m_lookAheadDistance", Declared)),
                Is.EqualTo("How far ahead to set the target. Make sure this is larger than the enemy's 'Move Within Distance'."));
            Assert.That(Tooltip(typeof(EnemyNavigationHomeRadius).GetField("m_homeRadius", Declared)),
                Is.EqualTo("Max distance to choose to move to from current home position."));
            Assert.That(Tooltip(typeof(EnemyNavigationHomeRadius).GetField("m_minNewDestinationDistance", Declared)),
                Is.EqualTo("Minimum distance to choose a new spot to move to."));
            Assert.That(Tooltip(typeof(EnemyNavigationHomeRadius).GetField("m_resetHomeOnInterrupt", Declared)),
                Is.EqualTo("If ticked, the \"home\" position is reset to the enemy's current location whenever navigation is interrupted."));
        }

        private static void AssertAttributes(Type owner, string field, params Type[] expected) =>
            Assert.That(AttributeTypes(owner.GetField(field, Declared)), Is.EqualTo(expected), owner.Name + "." + field);

        // Type, field and method flags straight from the metadata of the original assembly.
        [Test]
        public void DeclarationShapeNavigationsKeepTheirFieldsAndMethodFlags()
        {
            const int hook = 0x00c4, overrideMethod = 0x00c6, privateMethod = 0x0081, publicMethod = 0x0086, constructor = 0x1886, getter = 0x0881;
            AssertMembers(typeof(EnemyNavigationForward), 0x00100001, new[] { ("m_lookAheadDistance", 0x0001) }, new[]
            {
                ("SetUp", hook), ("StartNavigation", overrideMethod), ("InterruptNavigation", overrideMethod), ("GetDestination", overrideMethod),
                ("ConstrainPosition", overrideMethod), ("CanReachPosition", overrideMethod), ("OnDrawGizmos", privateMethod), (".ctor", constructor),
            });
            AssertMembers(typeof(EnemyNavigationHomeRadius), 0x00100001, new[]
            {
                ("m_homeRadius", 0x0001), ("m_minNewDestinationDistance", 0x0006), ("m_resetHomeOnInterrupt", 0x0001), ("m_home", 0x0001),
                ("m_homeRadiusSqr", 0x0001), ("m_destination", 0x0001), ("m_wasInterrupted", 0x0001), ("m_minNewDestinationDistanceSquared", 0x0001),
                ("LoopLimit", 0x8051),
            }, new[]
            {
                ("SetUp", hook), ("StartNavigation", overrideMethod), ("InterruptNavigation", overrideMethod), ("GetDestination", overrideMethod),
                ("ConstrainPosition", overrideMethod), ("CanReachPosition", overrideMethod), ("OnDrawGizmosSelected", publicMethod),
                ("OnValidate", privateMethod), (".ctor", constructor),
            });
            AssertMembers(typeof(EnemyNavigationStaticAndReturnAfterChase), 0x00100001, new[] { ("m_home", 0x0001) }, new[]
            {
                ("SetUp", hook), ("StartNavigation", overrideMethod), ("InterruptNavigation", overrideMethod), ("GetDestination", overrideMethod),
                ("ConstrainPosition", overrideMethod), ("CanReachPosition", overrideMethod), ("OnDrawGizmosSelected", publicMethod), (".ctor", constructor),
            });
            AssertMembers(typeof(EnemyNavigationSpline), 0x00100001, new[]
            {
                ("m_spline", 0x0001), ("m_startPosition", 0x0001), ("m_followDistance", 0x0001), ("m_trackerStepSize", 0x0001),
                ("m_startReversed", 0x0001), ("m_loop", 0x0001), ("m_reversing", 0x0001), ("m_followDistanceSqr", 0x0001), ("m_tracker2D", 0x0001),
                ("m_trackableSurfaces", 0x0021), ("m_trackableSurfacesList", 0x0021), ("m_nearestSplineLocationWorld", 0x0001),
            }, new[]
            {
                ("get_TrackerPosition", getter), ("get_TrackerForward", getter), ("get_TrackerAtEnd", getter), ("SetUp", hook),
                ("StartNavigation", overrideMethod), ("InterruptNavigation", overrideMethod), ("GetDestination", overrideMethod),
                ("ConstrainPosition", overrideMethod), ("CanReachPosition", overrideMethod), ("CalculateLine", privateMethod),
                ("GetLocationWorldRotation", publicMethod), ("GetReversing", publicMethod), ("OnDrawGizmos", privateMethod), (".ctor", constructor),
            });
            foreach (Type type in new[] { typeof(EnemyNavigationForward), typeof(EnemyNavigationHomeRadius), typeof(EnemyNavigationSpline), typeof(EnemyNavigationStaticAndReturnAfterChase) })
                Assert.That(type.BaseType, Is.EqualTo(typeof(EnemyNavigation)), type.Name);
        }
    }
}
