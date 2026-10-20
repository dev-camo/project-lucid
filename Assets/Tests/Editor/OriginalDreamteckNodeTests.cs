using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.Node and the node bookkeeping of SplineComputer (ConnectNode, DisconnectNode, GetNode, TransferNode,
    // ShiftNodes, GetNodes, GetJunctions, GetConnectedComputers). Nodes sit exactly on the computer points so that connecting
    // does not move anything unless a test moves the node. Native addresses are cited per test. Every game object is owned by
    // the test and destroyed in TearDown; nothing here is multithreaded.
    public sealed class OriginalDreamteckNodeTests
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

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private SplineComputer NewLine(string name, params Vector3[] positions)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            SplineComputer computer = owner.AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            SplinePoint[] points = new SplinePoint[positions.Length];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new SplinePoint(positions[i]);
            }

            computer.SetPoints(points);
            computer.RebuildImmediate();
            return computer;
        }

        private SplineComputer NewCorner(string name)
        {
            return NewLine(name, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f));
        }

        private Node NewNode(string name, Vector3 position)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            owner.transform.position = position;
            return owner.AddComponent<Node>();
        }

        // Defaults of the Node ctor and Awake 0x49e7c0: all three transform flags are on and there are no connections.
        [Test]
        public void FreshNodeKeepsTheShippedDefaults()
        {
            Node node = NewNode("fresh", Vector3.zero);
            Assert.That(node.type, Is.EqualTo(Node.Type.Smooth));
            Assert.That(node.transformNormals, Is.True);
            Assert.That(node.transformSize, Is.True);
            Assert.That(node.transformTangents, Is.True);
            Assert.That(node.GetConnections(), Is.Empty);
            Assert.That((int)Node.Type.Free, Is.EqualTo(1));
        }

        // ConnectNode 0x4b9320 / AddConnection 0x4a0310 / UpdateConnectedComputers 0x49eb80: the first connection pulls the
        // computer's point onto the node.
        [Test]
        public void ConnectingPullsThePointOntoTheNode()
        {
            SplineComputer computer = NewCorner("line");
            Node node = NewNode("node", new Vector3(0f, 0f, 3f));
            computer.ConnectNode(node, 0);
            Assert.That(computer.GetNode(0), Is.SameAs(node));
            Assert.That(computer.GetNode(1), Is.Null);
            Assert.That(node.GetConnections().Length, Is.EqualTo(1));
            Assert.That(node.GetConnections()[0].spline, Is.SameAs(computer));
            Assert.That(node.GetConnections()[0].pointIndex, Is.EqualTo(0));
            Assert.That(node.HasConnection(computer, 0), Is.True);
            Assert.That(node.HasConnection(computer, 1), Is.False);
            Near(computer.GetPointPosition(0), new Vector3(0f, 0f, 3f), "point 0 follows the node");
            Near(computer.GetPointPosition(1), new Vector3(10f, 0f, 0f), "other points stay");
        }

        // ConnectNode 0x4b9320: a node or point that already has a link logs an error; null and bad indices are rejected.
        [Test]
        public void ConnectNodeRejectsDuplicatesNullsAndBadIndices()
        {
            SplineComputer computer = NewCorner("line");
            Node first = NewNode("first", Vector3.zero);
            Node second = NewNode("second", Vector3.zero);
            computer.ConnectNode(first, 0);

            LogAssert.Expect(LogType.Error, "Node second is already connected to spline line at point 0");
            computer.ConnectNode(second, 0);
            Assert.That(second.GetConnections(), Is.Empty);

            LogAssert.Expect(LogType.Error, "Node first is already connected to spline line at point 0");
            computer.ConnectNode(first, 1);
            Assert.That(computer.GetNode(1), Is.Null);

            LogAssert.Expect(LogType.Error, "Missing Node");
            computer.ConnectNode(null, 1);
            computer.ConnectNode(second, 7);
            computer.ConnectNode(second, -1);
            Assert.That(second.GetConnections(), Is.Empty);
        }

        // DisconnectNode 0x4b98b0 removes the link on both sides; an unlinked index does nothing.
        [Test]
        public void DisconnectRemovesBothSides()
        {
            SplineComputer computer = NewCorner("line");
            Node node = NewNode("node", Vector3.zero);
            computer.ConnectNode(node, 0);
            computer.DisconnectNode(2);
            Assert.That(computer.GetNode(0), Is.SameAs(node));
            computer.DisconnectNode(0);
            Assert.That(computer.GetNode(0), Is.Null);
            Assert.That(node.GetConnections(), Is.Empty);
            Assert.That(node.HasConnection(computer, 0), Is.False);
        }

        // UpdateConnectedComputers 0x49eb80: a moved node drags the computer point along, except for the excluded computer.
        [Test]
        public void MovingTheNodeMovesTheConnectedPointUnlessExcluded()
        {
            SplineComputer computer = NewCorner("line");
            Node node = NewNode("node", Vector3.zero);
            computer.ConnectNode(node, 0);
            node.transform.position = new Vector3(0f, 0f, 5f);
            node.UpdateConnectedComputers(computer);
            Near(computer.GetPointPosition(0), Vector3.zero, "excluded");
            node.UpdateConnectedComputers();
            Near(computer.GetPointPosition(0), new Vector3(0f, 0f, 5f), "moved");
        }

        // OnDestroy 0x49fd40 -> ClearConnections 0x49fd50: destroying the node unlinks it from every computer.
        [Test]
        public void DestroyingTheNodeUnlinksTheComputer()
        {
            SplineComputer computer = NewCorner("line");
            GameObject owner = new GameObject("doomed");
            _owned.Add(owner);
            Node node = owner.AddComponent<Node>();
            computer.ConnectNode(node, 1);
            Assert.That(computer.GetNode(1), Is.SameAs(node));
            UnityEngine.Object.DestroyImmediate(owner);
            Assert.That(computer.GetNode(1), Is.Null);
            Assert.That(computer.GetNodes().Count, Is.EqualTo(0));
        }

        // SetPoint 0x49f3f0: a Smooth node copies the changed point to its other connections, a Free node does not.
        [Test]
        public void SmoothNodeMirrorsThePointAndFreeNodeDoesNot()
        {
            foreach (Node.Type type in new[] { Node.Type.Smooth, Node.Type.Free })
            {
                SplineComputer a = NewCorner("a");
                SplineComputer b = NewCorner("b");
                Node node = NewNode("shared", Vector3.zero);
                node.type = type;
                a.ConnectNode(node, 0);
                b.ConnectNode(node, 0);
                Assert.That(node.GetConnections().Length, Is.EqualTo(2));

                SplinePoint changed = new SplinePoint(Vector3.zero);
                changed.tangent = new Vector3(1f, 2f, 3f);
                changed.tangent2 = new Vector3(-1f, -2f, -3f);
                changed.size = 2f;
                node.SetPoint(0, changed, false);
                Vector3 mirrored = node.GetPoint(1, false).tangent;
                if (type == Node.Type.Smooth)
                {
                    Near(mirrored, new Vector3(1f, 2f, 3f), "smooth mirrors the tangent");
                    Assert.That(node.GetPoint(1, false).size, Is.EqualTo(2f).Within(1e-6f));
                }
                else
                {
                    Assert.That(Vector3.Distance(mirrored, new Vector3(1f, 2f, 3f)), Is.GreaterThan(Tolerance), "free keeps its own tangent");
                }
            }
        }

        // SetPoint 0x49f3f0, SmoothFree branch (cmp [r12+0x24],2 at 0x49f522, rebuild 0x49f9be..0x49fa82): each other connection
        // keeps its own tangent lengths along the changed point's direction. Both connections here have zero-length tangents,
        // so they stay zero; the plain copy of the Smooth branch would give (4,0,0).
        [Test]
        public void SmoothFreeNodeKeepsTheOtherTangentLengths()
        {
            SplineComputer a = NewCorner("a");
            SplineComputer b = NewCorner("b");
            Node node = NewNode("shared", Vector3.zero);
            node.type = Node.Type.Smooth;
            a.ConnectNode(node, 0);
            b.ConnectNode(node, 0);

            SplinePoint changed = new SplinePoint(Vector3.zero);
            changed.type = SplinePoint.Type.SmoothFree;
            changed.tangent = new Vector3(4f, 0f, 0f);
            changed.tangent2 = new Vector3(-1f, 0f, 0f);
            node.SetPoint(0, changed, false);
            SplinePoint other = node.GetPoint(1, false);
            Near(other.tangent, Vector3.zero, "tangent keeps its own length");
            Near(other.tangent2, Vector3.zero, "tangent2 keeps its own length");
            Assert.That(other.type, Is.EqualTo(SplinePoint.Type.SmoothFree));
        }

        // GetJunctions 0x4b8e30 / 0x4b90c0, GetConnectedComputers 0x4ba810 / 0x4ba320: a node shared by two computers.
        [Test]
        public void SharedNodeLinksTheComputers()
        {
            SplineComputer a = NewCorner("a");
            SplineComputer b = NewCorner("b");
            Node node = NewNode("shared", new Vector3(10f, 0f, 0f));
            a.ConnectNode(node, 1);
            b.ConnectNode(node, 0);

            List<Node.Connection> junction = a.GetJunctions(1);
            Assert.That(junction.Count, Is.EqualTo(1));
            Assert.That(junction[0].spline, Is.SameAs(b));
            Assert.That(junction[0].pointIndex, Is.EqualTo(0));
            Assert.That(a.GetJunctions(0), Is.Empty);
            Dictionary<int, List<Node.Connection>> range = a.GetJunctions(0.25, 0.75);
            Assert.That(range.Keys.ToArray(), Is.EqualTo(new[] { 1 }));

            Assert.That(a.GetConnectedComputers(), Is.EqualTo(new List<SplineComputer> { a, b }));
            Assert.That(b.GetConnectedComputers(), Is.EqualTo(new List<SplineComputer> { b, a }));

            List<SplineComputer> computers = new List<SplineComputer> { a };
            List<int> connectionIndices = new List<int> { 9 };
            List<int> connectedIndices = new List<int> { 9 };
            a.GetConnectedComputers(computers, connectionIndices, connectedIndices, 0.5, Spline.Direction.Forward, true);
            Assert.That(computers, Is.EqualTo(new List<SplineComputer> { b }));
            Assert.That(connectionIndices, Is.EqualTo(new List<int> { 1 }));
            Assert.That(connectedIndices, Is.EqualTo(new List<int> { 0 }));

            // percent 0.5 maps to point 1, where the node sits: backward keeps nodes at or before the index (jg skip at 0x4ba61c),
            // so a node exactly on it is found. A strict comparison would find nothing.
            a.GetConnectedComputers(computers, connectionIndices, connectedIndices, 0.5, Spline.Direction.Backward, true);
            Assert.That(computers, Is.EqualTo(new List<SplineComputer> { b }));
            Assert.That(connectionIndices, Is.EqualTo(new List<int> { 1 }));
            Assert.That(connectedIndices, Is.EqualTo(new List<int> { 0 }));

            // percent 0.25 maps to point 0: forward finds the node at or after it, backward only nodes at or before it.
            a.GetConnectedComputers(computers, connectionIndices, connectedIndices, 0.25, Spline.Direction.Forward, true);
            Assert.That(computers.Count, Is.EqualTo(1));
            a.GetConnectedComputers(computers, connectionIndices, connectedIndices, 0.25, Spline.Direction.Backward, true);
            Assert.That(computers, Is.Empty);
            computers.Add(a);
            a.GetConnectedComputers(computers, connectionIndices, connectedIndices, 0.5, Spline.Direction.Forward, false);
            Assert.That(computers, Is.Empty, "without includeEqual the lists are only cleared");
        }

        // GetConnectedComputers 0x4ba320, null guard at 0x4ba36d: a null connectedIndices allocates into connectionIndices
        // (0x4ba46e), so connectedIndices.Clear() (0x4ba4e6) throws. Null computers and null connectionIndices are created.
        [Test]
        public void GetConnectedComputersFaultsOnNullConnectedIndicesAndCreatesTheOtherLists()
        {
            SplineComputer a = NewCorner("a");
            Assert.Throws<NullReferenceException>(() =>
                a.GetConnectedComputers(new List<SplineComputer>(), new List<int>(), null, 0.5, Spline.Direction.Forward, true));
            Assert.DoesNotThrow(() =>
                a.GetConnectedComputers(null, null, new List<int>(), 0.5, Spline.Direction.Forward, true));

            // The guard writes the new list into the local connectionIndices (0x4ba46e..0x4ba48d), so the caller's own
            // connectionIndices is never cleared while computers is (0x4ba4a3). A version without the guard would clear it.
            List<SplineComputer> computers = new List<SplineComputer> { a };
            List<int> connectionIndices = new List<int> { 9 };
            Assert.Throws<NullReferenceException>(() =>
                a.GetConnectedComputers(computers, connectionIndices, null, 0.5, Spline.Direction.Forward, true));
            Assert.That(computers, Is.Empty);
            Assert.That(connectionIndices, Is.EqualTo(new List<int> { 9 }));
        }

        // GetNodes 0x4b9bc0: nodes strictly inside the point range, plus a node exactly on either end. An inverted range scans
        // the points between the two ends.
        [Test]
        public void GetNodesFollowsThePointRange()
        {
            SplineComputer computer = NewCorner("line");
            Node[] nodes =
            {
                NewNode("n0", new Vector3(0f, 0f, 0f)),
                NewNode("n1", new Vector3(10f, 0f, 0f)),
                NewNode("n2", new Vector3(10f, 0f, 10f))
            };
            for (int i = 0; i < nodes.Length; i++)
            {
                computer.ConnectNode(nodes[i], i);
            }

            Assert.That(computer.GetNodes().Keys.OrderBy(k => k).ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(computer.GetNodes(0.25, 0.75).Keys.ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(computer.GetNodes(0.0, 0.5).Keys.OrderBy(k => k).ToArray(), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(computer.GetNodes(0.75, 0.25).Keys.ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(computer.GetNodes(0.25, 0.75)[1], Is.SameAs(nodes[1]));
        }

        // TransferNode 0x4b9e70 and ShiftNodes 0x4ba1f0: the nodes of a range move from the highest index down, otherwise the
        // second node would find its target occupied. A node lands on the sample of its new point.
        [Test]
        public void ShiftNodesMovesFromTheTopAndTransferChecksTheTargets()
        {
            SplineComputer computer = NewLine("square", new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f));
            Node low = NewNode("low", new Vector3(0f, 0f, 0f));
            Node high = NewNode("high", new Vector3(10f, 0f, 0f));
            computer.ConnectNode(low, 0);
            computer.ConnectNode(high, 1);

            computer.ShiftNodes(0, 1, 1);
            Assert.That(computer.GetNode(0), Is.Null);
            Assert.That(computer.GetNode(1), Is.SameAs(low));
            Assert.That(computer.GetNode(2), Is.SameAs(high));
            Near(high.transform.position, new Vector3(10f, 0f, 10f), "high lands on the sample of point 2");
            Near(low.transform.position, new Vector3(10f, 0f, 0f), "low lands on the sample of point 1");
            Near(computer.GetPointPosition(2), new Vector3(10f, 0f, 10f), "point 2");

            LogAssert.Expect(LogType.Error, "Invalid new point index 9");
            computer.TransferNode(1, 9);
            LogAssert.Expect(LogType.Error, "Cannot move node to point 2. Point already connected to a node");
            computer.TransferNode(1, 2);
            LogAssert.Expect(LogType.Error, "No node connected to point 3");
            computer.TransferNode(3, 0);
            Assert.That(computer.GetNode(1), Is.SameAs(low));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndNestedTypes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(Node).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "type", "connections", "_transformSize", "_transformNormals", "_transformTangents", "_lastPosition", "_lastScale", "_lastRotation", "_trs"
            }));
            Assert.That(typeof(Node.Connection).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "invertTangents", "_pointIndex", "_computer", "point" }));
            Assert.That(typeof(Node).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(), Is.EquivalentTo(new[] { "Connection", "Type" }));
            Assert.That(Attribute.IsDefined(typeof(Node.Connection), typeof(SerializableAttribute)), Is.True);
        }
    }
}
