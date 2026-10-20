using System.Collections.Generic;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineUtility.Merge (x86-64 0x4e4af0). The base spline is the Linear pair (0,0,0),(1,0,0) and the added
    // one the pair (5,0,0),(6,0,0). The added points are always taken backwards (last first). Every game object is owned by the
    // test and destroyed in TearDown; destroyAddedSpline stays false because Object.Destroy is not allowed in edit mode.
    public sealed class OriginalDreamteckSplineUtilityTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private SplineComputer NewPair(string name, float x)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            SplineComputer computer = owner.AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[] { new SplinePoint(new Vector3(x, 0f, 0f)), new SplinePoint(new Vector3(x + 1f, 0f, 0f)) });
            return computer;
        }

        private static void AssertX(SplineComputer computer, params float[] expected)
        {
            Assert.That(computer.pointCount, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(computer.GetPointPosition(i).x, Is.EqualTo(expected[i]).Within(Tolerance), "point " + i);
                Assert.That(computer.GetPointPosition(i).y, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(computer.GetPointPosition(i).z, Is.EqualTo(0f).Within(Tolerance));
            }
        }

        [Test]
        public void MergeSideValues()
        {
            Assert.That((int)SplineUtility.MergeSide.Start, Is.EqualTo(0));
            Assert.That((int)SplineUtility.MergeSide.End, Is.EqualTo(1));
        }

        // End: the base points, then the added points last first.
        [Test]
        public void MergeAtTheEndAppendsTheAddedPointsBackwards()
        {
            SplineComputer baseSpline = NewPair("base", 0f);
            SplineComputer added = NewPair("added", 5f);
            SplineUtility.Merge(baseSpline, added, SplineUtility.MergeSide.End);
            AssertX(baseSpline, 0f, 1f, 6f, 5f);
            AssertX(added, 5f, 6f);
            Assert.That(added != null, Is.True, "the added spline survives without destroyAddedSpline");
        }

        // End with mergeEndpoints drops the added spline's first point (the last one taken).
        [Test]
        public void MergeAtTheEndWithEndpointsDropsOnePoint()
        {
            SplineComputer baseSpline = NewPair("base", 0f);
            SplineComputer added = NewPair("added", 5f);
            SplineUtility.Merge(baseSpline, added, SplineUtility.MergeSide.End, true);
            AssertX(baseSpline, 0f, 1f, 6f);
        }

        // Start: the added points last first, then the base points.
        [Test]
        public void MergeAtTheStartPrependsTheAddedPointsBackwards()
        {
            SplineComputer baseSpline = NewPair("base", 0f);
            SplineComputer added = NewPair("added", 5f);
            SplineUtility.Merge(baseSpline, added, SplineUtility.MergeSide.Start);
            AssertX(baseSpline, 6f, 5f, 0f, 1f);
        }

        // Any other side value takes the third branch (as disassembled): the added points forwards, in front of the base ones.
        [Test]
        public void UnknownSideValuePlacesTheAddedPointsForwardsInFront()
        {
            SplineComputer baseSpline = NewPair("base", 0f);
            SplineComputer added = NewPair("added", 5f);
            SplineUtility.Merge(baseSpline, added, (SplineUtility.MergeSide)2);
            AssertX(baseSpline, 5f, 6f, 0f, 1f);
        }

        // The users of the added spline move to the base spline and their clip range is remapped into the share of the merged
        // spline that the added points occupy: (2 - 1) / (4 - 1) = 1/3, at the end 2/3..1 and at the start 0..1/3.
        [Test]
        public void MergeMovesTheUsersAndRemapsTheirClipRange()
        {
            foreach (SplineUtility.MergeSide side in new[] { SplineUtility.MergeSide.End, SplineUtility.MergeSide.Start })
            {
                SplineComputer baseSpline = NewPair("base", 0f);
                SplineComputer added = NewPair("added", 5f);
                GameObject owner = new GameObject("user");
                _owned.Add(owner);
                SplineUser user = owner.AddComponent<SplineUser>();
                user.spline = added;
                user.clipFrom = 0.5;
                SplineUtility.Merge(baseSpline, added, side);
                Assert.That(user.spline, Is.SameAs(baseSpline));
                Assert.That(added.subscriberCount, Is.EqualTo(0));
                Assert.That(baseSpline.IsSubscribed(user), Is.True);
                if (side == SplineUtility.MergeSide.End)
                {
                    Assert.That(user.clipFrom, Is.EqualTo(5.0 / 6.0).Within(1e-9));
                    Assert.That(user.clipTo, Is.EqualTo(1.0).Within(1e-9));
                }
                else
                {
                    Assert.That(user.clipFrom, Is.EqualTo(1.0 / 6.0).Within(1e-9));
                    Assert.That(user.clipTo, Is.EqualTo(1.0 / 3.0).Within(1e-9));
                }
            }
        }

        // The nodes of the added spline are disconnected and connected to the base spline. At the end the index is
        // nodeIndex - skip + basePointCount (here 1 - 0 + 2 = 3); at the start the node keeps its index, as shipped.
        [Test]
        public void MergeReconnectsTheNodesOfTheAddedSpline()
        {
            foreach (SplineUtility.MergeSide side in new[] { SplineUtility.MergeSide.End, SplineUtility.MergeSide.Start })
            {
                SplineComputer baseSpline = NewPair("base", 0f);
                SplineComputer added = NewPair("added", 5f);
                GameObject nodeOwner = new GameObject("node");
                _owned.Add(nodeOwner);
                nodeOwner.transform.position = new Vector3(6f, 0f, 0f);
                Node node = nodeOwner.AddComponent<Node>();
                added.ConnectNode(node, 1);
                Assert.That(added.GetNode(1), Is.SameAs(node));

                SplineUtility.Merge(baseSpline, added, side);
                Assert.That(added.GetNode(1), Is.Null);
                int expectedIndex = side == SplineUtility.MergeSide.End ? 3 : 1;
                Assert.That(baseSpline.GetNode(expectedIndex), Is.SameAs(node));
            }
        }
    }
}
