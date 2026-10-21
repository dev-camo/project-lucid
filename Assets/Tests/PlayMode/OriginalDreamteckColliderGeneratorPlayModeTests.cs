using System.Collections.Generic;
using System.Linq;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped player branches of Dreamteck.Splines.EdgeColliderGenerator and PolygonColliderGenerator, run in play mode where
    // Application.isPlaying is true and SplineUser caches it in Awake: Rebuild queues instead of building at once, and PostBuild
    // with an update rate only flags the collider (the edit mode twins in OriginalDreamteckColliderGeneratorTests cover the editor
    // branch). Awake runs by itself on AddComponent. The tests are synchronous, so no engine Update runs between the calls.
    public sealed class OriginalDreamteckColliderGeneratorPlayModeTests
    {
        private const float Tolerance = 1e-3f;

        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private static void Near(Vector2 actual, Vector2 expected, string message)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F5") + ")");
        }

        private static void Near(Vector2[] actual, Vector2[] expected, string message)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), message + " length");
            for (int i = 0; i < expected.Length; i++)
            {
                Near(actual[i], expected[i], message + "[" + i + "]");
            }
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        private SplineComputer NewComputer(params Vector3[] positions)
        {
            SplineComputer computer = NewObject("line").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(positions.Select(p => new SplinePoint(p, p, Vector3.up, 1f, Color.white)).ToArray());
            computer.RebuildImmediate();
            return computer;
        }

        private EdgeColliderGenerator NewEdge(SplineComputer computer, float updateRate = 0f)
        {
            EdgeColliderGenerator edge = NewObject("edge").AddComponent<EdgeColliderGenerator>();
            edge.updateRate = updateRate;
            edge.spline = computer;
            return edge;
        }

        private PolygonColliderGenerator NewPolygon(SplineComputer computer, float updateRate = 0f)
        {
            PolygonColliderGenerator polygon = NewObject("polygon").AddComponent<PolygonColliderGenerator>();
            polygon.updateRate = updateRate;
            polygon.spline = computer;
            return polygon;
        }

        private static T Read<T>(object target, string name)
        {
            return DreamteckGeneratorFixture.Field<T>(target, name);
        }

        private static void Write(object target, string name, object value)
        {
            DreamteckGeneratorFixture.SetField(target, name, value);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            return DreamteckGeneratorFixture.Invoke(target, name, arguments);
        }

        private static bool RebuildQueued(SplineUser user)
        {
            return Read<bool>(user, "rebuild");
        }

        // set_offset 0x49c270 rebuilds for a changed value only. Rebuild queues (the player text of SplineUser.Rebuild) because the
        // cached play state is true.
        [Test]
        public void OffsetSetterQueuesARebuildForAChangedValue()
        {
            EdgeColliderGenerator edge = NewEdge(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)));
            edge.RebuildImmediate();
            Assert.That(RebuildQueued(edge), Is.False);
            edge.offset = 0f;
            Assert.That(RebuildQueued(edge), Is.False, "unchanged");
            edge.offset = 1.5f;
            Assert.That(edge.offset, Is.EqualTo(1.5f));
            Assert.That(RebuildQueued(edge), Is.True);
        }

        // With an update rate PostBuild 0x49c6b0 only flags the collider and LateRun 0x49c350 gives it the vertices once
        // updateRate seconds have passed since the last update; the flag clears and the time is remembered. A pending update waits
        // when the last one is too recent.
        [Test]
        public void UpdateRateDefersTheColliderToLateRun()
        {
            EdgeColliderGenerator edge = NewEdge(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)), 0.1f);
            EdgeCollider2D collider = edge.GetComponent<EdgeCollider2D>();
            Vector2[] untouched = collider.points;
            edge.RebuildImmediate();
            Assert.That(Read<bool>(edge, "updateCollider"), Is.True);
            Near(collider.points, untouched, "the collider keeps its points");

            Write(edge, "lastUpdateTime", float.MaxValue);
            Invoke(edge, "LateRun");
            Assert.That(Read<bool>(edge, "updateCollider"), Is.True, "too recent");
            Near(collider.points, untouched, "still untouched");

            Write(edge, "lastUpdateTime", -1000000f);
            Invoke(edge, "LateRun");
            Assert.That(Read<bool>(edge, "updateCollider"), Is.False);
            Assert.That(Read<float>(edge, "lastUpdateTime"), Is.GreaterThan(-1000f), "the time was remembered");
            Near(collider.points, new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, "applied");

            Near(collider.points, new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, "stays");
            Assert.That(() => Invoke(edge, "LateRun"), Throws.Nothing, "nothing pending");
        }

        // The setters 0x4ae710/0x4ae750/0x4ae7a0 rebuild for a changed value only.
        [Test]
        public void PolygonSettersQueueARebuildForAChangedValue()
        {
            PolygonColliderGenerator polygon = NewPolygon(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)));
            polygon.RebuildImmediate();
            Assert.That(RebuildQueued(polygon), Is.False);
            polygon.type = PolygonColliderGenerator.Type.Path;
            polygon.size = 1f;
            polygon.offset = 0f;
            Assert.That(RebuildQueued(polygon), Is.False, "unchanged values");
            polygon.type = PolygonColliderGenerator.Type.Shape;
            Assert.That(polygon.type, Is.EqualTo(PolygonColliderGenerator.Type.Shape));
            Assert.That(RebuildQueued(polygon), Is.True);
            polygon.RebuildImmediate();
            polygon.size = 4f;
            Assert.That(polygon.size, Is.EqualTo(4f));
            Assert.That(RebuildQueued(polygon), Is.True);
            polygon.RebuildImmediate();
            polygon.offset = -2f;
            Assert.That(polygon.offset, Is.EqualTo(-2f));
            Assert.That(RebuildQueued(polygon), Is.True);
        }

        // PostBuild 0x4aee70 and LateRun 0x4ae880 as the edge generator's, with SetPath(0, vertices) for the collider and the vertices
        // moved into the local space first.
        [Test]
        public void PolygonUpdateRateAndLocalSpaceFollowTheEdgeGenerator()
        {
            PolygonColliderGenerator polygon = NewPolygon(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)), 0.1f);
            polygon.type = PolygonColliderGenerator.Type.Shape;
            polygon.transform.position = new Vector3(100f, 50f, 0f);
            PolygonCollider2D collider = polygon.GetComponent<PolygonCollider2D>();
            Vector2[] untouched = collider.GetPath(0);
            polygon.RebuildImmediate();
            Assert.That(Read<bool>(polygon, "updateCollider"), Is.True);
            Near(collider.GetPath(0), untouched, "the collider keeps its path");
            Near(Read<Vector2[]>(polygon, "vertices"), new[] { new Vector2(-100f, -50f), new Vector2(-90f, -50f) }, "local vertices");

            Write(polygon, "lastUpdateTime", float.MaxValue);
            Invoke(polygon, "LateRun");
            Assert.That(Read<bool>(polygon, "updateCollider"), Is.True, "too recent");
            Near(collider.GetPath(0), untouched, "still untouched");
            Write(polygon, "lastUpdateTime", -1000000f);
            Invoke(polygon, "LateRun");
            Assert.That(Read<bool>(polygon, "updateCollider"), Is.False);
            Near(collider.GetPath(0), new[] { new Vector2(-100f, -50f), new Vector2(-90f, -50f) }, "applied");

            polygon.updateRate = 0f;
            polygon.RebuildImmediate();
            Assert.That(Read<bool>(polygon, "updateCollider"), Is.False);
            Near(collider.GetPath(0), new[] { new Vector2(-100f, -50f), new Vector2(-90f, -50f) }, "applied at once");

            // A pending update reached through the real path (an update rate, then a build), then the collider reference
            // Awake fetched is gone: LateRun keeps the update pending.
            polygon.updateRate = 0.1f;
            polygon.RebuildImmediate();
            Assert.That(Read<bool>(polygon, "updateCollider"), Is.True, "pending again");
            Write(polygon, "polygonCollider", null);
            Assert.That(() => Invoke(polygon, "LateRun"), Throws.Nothing);
            Assert.That(Read<bool>(polygon, "updateCollider"), Is.True, "no collider to give the path to");
        }
    }
}
