using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.EdgeColliderGenerator and PolygonColliderGenerator, driven through a Linear SplineComputer in the XY plane:
    // a straight line from (0,0,0) to (10,0,0) has the samples (0,0) and (10,0), both looking along +x (forward (1,0,0)), so their
    // planar normal (-forward.y, forward.x) is (0,1); a point size scales that normal. Expected values are hand arithmetic from the
    // native bodies cited per test. Neither generator has ExecuteInEditMode, so the edit mode does not wake it and the tests call
    // Awake themselves. The generators are plain components on owned game objects and TearDown destroys everything the test made.
    public sealed class OriginalDreamteckColliderGeneratorTests
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
                    UnityEngine.Object.DestroyImmediate(owned);
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
            return NewComputer(1f, positions);
        }

        private SplineComputer NewComputer(float size, params Vector3[] positions)
        {
            SplineComputer computer = NewObject("line").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(positions.Select(p => new SplinePoint(p, p, Vector3.up, size, Color.white)).ToArray());
            computer.RebuildImmediate();
            return computer;
        }

        private EdgeColliderGenerator NewEdge(SplineComputer computer, float updateRate = 0f)
        {
            EdgeColliderGenerator edge = NewObject("edge").AddComponent<EdgeColliderGenerator>();
            Invoke(edge, "Awake");
            edge.updateRate = updateRate;
            edge.spline = computer;
            return edge;
        }

        private PolygonColliderGenerator NewPolygon(SplineComputer computer, float updateRate = 0f)
        {
            PolygonColliderGenerator polygon = NewObject("polygon").AddComponent<PolygonColliderGenerator>();
            Invoke(polygon, "Awake");
            polygon.updateRate = updateRate;
            polygon.spline = computer;
            return polygon;
        }

        private static T Read<T>(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return (T)field.GetValue(target);
                }
            }

            Assert.Fail("no field " + name);
            return default(T);
        }

        private static void Write(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
            }

            Assert.Fail("no field " + name);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == arguments.Length);
                if (method != null)
                {
                    return method.Invoke(target, arguments);
                }
            }

            Assert.Fail("no method " + name);
            return null;
        }

        // Constructor 0x49c7f0, Awake 0x49c2b0 (fetches the EdgeCollider2D that RequireComponent adds): an empty vertex array, an
        // update rate of 0.1, no offset.
        [Test]
        public void FreshEdgeGeneratorKeepsTheShippedDefaults()
        {
            EdgeColliderGenerator edge = NewObject("edge").AddComponent<EdgeColliderGenerator>();
            Assert.That(edge.updateRate, Is.EqualTo(0.1f));
            Assert.That(edge.offset, Is.EqualTo(0f));
            Assert.That(Read<Vector2[]>(edge, "vertices"), Is.Not.Null);
            Assert.That(Read<Vector2[]>(edge, "vertices").Length, Is.EqualTo(0));
            Assert.That(Read<float>(edge, "lastUpdateTime"), Is.EqualTo(0f));
            Assert.That(Read<bool>(edge, "updateCollider"), Is.False);
            EdgeCollider2D collider = edge.GetComponent<EdgeCollider2D>();
            Assert.That(collider != null, Is.True, "RequireComponent added the collider");
            Invoke(edge, "Awake");
            Assert.That(Read<EdgeCollider2D>(edge, "edgeCollider"), Is.SameAs(collider));
            Assert.That(edge is SplineUser, Is.True);
        }

        // Build 0x49c410 without an offset: one vertex per sample, the sample position's x and y. PostBuild 0x49c6b0 moves the
        // vertices into the local space of the object (here translated by (100,50)) and, with no update rate, gives them to the
        // collider at once; a changed vertex count reallocates the array.
        [Test]
        public void BuildTakesOneVertexPerSamplePositionInLocalSpace()
        {
            SplineComputer computer = NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f));
            EdgeColliderGenerator edge = NewEdge(computer);
            edge.RebuildImmediate();
            Near(Read<Vector2[]>(edge, "vertices"), new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, "vertices");
            Near(edge.GetComponent<EdgeCollider2D>().points, new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, "collider");

            edge.transform.position = new Vector3(100f, 50f, 0f);
            edge.RebuildImmediate();
            Near(edge.GetComponent<EdgeCollider2D>().points, new[] { new Vector2(-100f, -50f), new Vector2(-90f, -50f) }, "local space");

            SplineComputer longer = NewComputer(new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(15f, 0f, 0f));
            edge.spline = longer;
            edge.RebuildImmediate();
            Assert.That(Read<Vector2[]>(edge, "vertices").Length, Is.EqualTo(edge.sampleCount));
            Assert.That(edge.GetComponent<EdgeCollider2D>().points.Length, Is.EqualTo(edge.sampleCount));
            Assert.That(edge.sampleCount, Is.GreaterThan(2));
        }

        // Build 0x49c410 with an offset: each vertex moves along the planar normal (-forward.y, forward.x), normalised, scaled by
        // the sample size and the offset. A normal of length 1e-5 or less (a line along z) is zero, so nothing moves. A diagonal
        // line gets the normal (-1,1) / sqrt(2).
        [Test]
        public void OffsetMovesTheVerticesAlongTheScaledPlanarNormal()
        {
            EdgeColliderGenerator edge = NewEdge(NewComputer(3f, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)));
            edge.offset = 2f;
            edge.RebuildImmediate();
            Near(Read<Vector2[]>(edge, "vertices"), new[] { new Vector2(0f, 6f), new Vector2(10f, 6f) }, "size 3 offset 2");
            edge.offset = -1f;
            edge.RebuildImmediate();
            Near(Read<Vector2[]>(edge, "vertices"), new[] { new Vector2(0f, -3f), new Vector2(10f, -3f) }, "size 3 offset -1");

            EdgeColliderGenerator along = NewEdge(NewComputer(new Vector3(3f, 4f, 0f), new Vector3(3f, 4f, 10f)));
            along.offset = 5f;
            along.RebuildImmediate();
            Near(Read<Vector2[]>(along, "vertices"), new[] { new Vector2(3f, 4f), new Vector2(3f, 4f) }, "a line along z has no planar normal");

            EdgeColliderGenerator diagonal = NewEdge(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 10f, 0f)));
            diagonal.offset = 2f;
            diagonal.RebuildImmediate();
            float shift = 2f / Mathf.Sqrt(2f);
            Near(Read<Vector2[]>(diagonal, "vertices"), new[] { new Vector2(-shift, shift), new Vector2(10f - shift, 10f + shift) }, "diagonal");
        }

        // Editor branch. With an update rate the player PostBuild 0x49c6b0 only flags the collider and LateRun 0x49c350 gives it the
        // vertices later; the editor compile reads Application.isPlaying directly and applies the vertices at once while not
        // playing, leaving nothing for LateRun. The deferred shipped branch runs in play mode:
        // OriginalDreamteckColliderGeneratorPlayModeTests.UpdateRateDefersTheColliderToLateRun.
        [Test]
        public void UpdateRateDefersTheColliderToLateRun()
        {
            EdgeColliderGenerator edge = NewEdge(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)), 0.1f);
            EdgeCollider2D collider = edge.GetComponent<EdgeCollider2D>();
            Vector2[] untouched = collider.points;
            edge.RebuildImmediate();
            Assert.That(Read<bool>(edge, "updateCollider"), Is.False, "the editor compile applies at once while not playing");
            Near(collider.points, new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, "applied at once");

            collider.points = untouched;
            Invoke(edge, "LateRun");
            Near(collider.points, untouched, "nothing is pending for LateRun");
            Assert.That(Read<bool>(edge, "updateCollider"), Is.False);
        }

        // PostBuild 0x49c6b0 and LateRun 0x49c350 do nothing without a collider (the serialized field is empty).
        [Test]
        public void WithoutAColliderNothingIsLocalisedOrApplied()
        {
            EdgeColliderGenerator edge = NewEdge(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)), 0.1f);
            Write(edge, "edgeCollider", null);
            edge.transform.position = new Vector3(100f, 50f, 0f);
            edge.RebuildImmediate();
            Near(Read<Vector2[]>(edge, "vertices"), new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, "still world space");
            Assert.That(Read<bool>(edge, "updateCollider"), Is.False);
            Write(edge, "updateCollider", true);
            Assert.That(() => Invoke(edge, "LateRun"), Throws.Nothing);
            Assert.That(Read<bool>(edge, "updateCollider"), Is.True, "still waiting for a collider");
        }

        // OnEnable 0x49c320 calls the base, which rebuilds right away with buildOnEnable. Reset, OnDisable and OnDestroy only call
        // their base (empty), so the overrides must exist and leave the component alone.
        [Test]
        public void TheLifecycleOverridesCallTheBase()
        {
            EdgeColliderGenerator edge = NewEdge(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)));
            edge.buildOnEnable = true;
            Invoke(edge, "OnEnable");
            Assert.That(Read<Vector2[]>(edge, "vertices").Length, Is.EqualTo(edge.sampleCount), "the base OnEnable built");
            Assert.That(edge.sampleCount, Is.GreaterThan(0));
            Assert.That(() => { Invoke(edge, "Reset"); Invoke(edge, "OnDisable"); Invoke(edge, "OnDestroy"); }, Throws.Nothing);

            string[] declared = typeof(EdgeColliderGenerator)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.That(declared, Is.EqualTo(new[] { "Awake", "Build", "LateRun", "OnDestroy", "OnDisable", "OnEnable", "PostBuild", "Reset" }));
        }

        [Test]
        public void EdgeSerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(EdgeColliderGenerator).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "_offset", "edgeCollider", "vertices", "updateRate", "lastUpdateTime", "updateCollider" }));
            AddComponentMenu menu = (AddComponentMenu)typeof(EdgeColliderGenerator).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Edge Collider Generator"));
            RequireComponent require = (RequireComponent)typeof(EdgeColliderGenerator).GetCustomAttributes(typeof(RequireComponent), false).Single();
            Assert.That(require.m_Type0, Is.EqualTo(typeof(EdgeCollider2D)));
            foreach (string name in new[] { "_offset", "edgeCollider", "vertices" })
            {
                FieldInfo field = typeof(EdgeColliderGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            Assert.That(typeof(EdgeColliderGenerator).GetField("updateRate").GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            Assert.That(typeof(EdgeColliderGenerator).GetField("lastUpdateTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(0));
        }

        // Constructor 0x4aefb0: size 1, an empty vertex array, an update rate of 0.1, Path type, no offset.
        [Test]
        public void FreshPolygonGeneratorKeepsTheShippedDefaults()
        {
            PolygonColliderGenerator polygon = NewObject("polygon").AddComponent<PolygonColliderGenerator>();
            Assert.That(polygon.type, Is.EqualTo(PolygonColliderGenerator.Type.Path));
            Assert.That(polygon.size, Is.EqualTo(1f));
            Assert.That(polygon.offset, Is.EqualTo(0f));
            Assert.That(polygon.updateRate, Is.EqualTo(0.1f));
            Assert.That(Read<Vector2[]>(polygon, "vertices").Length, Is.EqualTo(0));
            PolygonCollider2D collider = polygon.GetComponent<PolygonCollider2D>();
            Assert.That(collider != null, Is.True, "RequireComponent added the collider");
            Invoke(polygon, "Awake");
            Assert.That(Read<PolygonCollider2D>(polygon, "polygonCollider"), Is.SameAs(collider));
        }

        // GeneratePath 0x4ae990: two vertices per sample. The first half runs along the +normal side (half the size away), the
        // second half comes back along the -normal side in reverse order (index 2n-1-i), both shifted by offset times the normal.
        // Size 2 and offset 0.5 on the sample size 1: the band is the rectangle y in [-0.5, 1.5].
        [Test]
        public void PathTypeBuildsAClosedBandAroundTheSamples()
        {
            PolygonColliderGenerator polygon = NewPolygon(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)));
            polygon.size = 2f;
            polygon.offset = 0.5f;
            polygon.RebuildImmediate();
            Near(Read<Vector2[]>(polygon, "vertices"),
                new[] { new Vector2(0f, 1.5f), new Vector2(10f, 1.5f), new Vector2(10f, -0.5f), new Vector2(0f, -0.5f) }, "band");
            PolygonCollider2D collider = polygon.GetComponent<PolygonCollider2D>();
            Assert.That(collider.pathCount, Is.EqualTo(1));
            Near(collider.GetPath(0), new[] { new Vector2(0f, 1.5f), new Vector2(10f, 1.5f), new Vector2(10f, -0.5f), new Vector2(0f, -0.5f) }, "collider path");

            polygon.size = 1f;
            polygon.offset = 0f;
            polygon.RebuildImmediate();
            Near(Read<Vector2[]>(polygon, "vertices"),
                new[] { new Vector2(0f, 0.5f), new Vector2(10f, 0.5f), new Vector2(10f, -0.5f), new Vector2(0f, -0.5f) }, "default band");

            PolygonColliderGenerator wide = NewPolygon(NewComputer(3f, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)));
            wide.size = 2f;
            wide.offset = 0.5f;
            wide.RebuildImmediate();
            Near(Read<Vector2[]>(wide, "vertices"),
                new[] { new Vector2(0f, 4.5f), new Vector2(10f, 4.5f), new Vector2(10f, -1.5f), new Vector2(0f, -1.5f) }, "sample size 3");

            PolygonColliderGenerator longer = NewPolygon(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f), new Vector3(10f, 0f, 0f)));
            longer.RebuildImmediate();
            int count = longer.sampleCount;
            Assert.That(Read<Vector2[]>(longer, "vertices").Length, Is.EqualTo(count * 2));
            Near(Read<Vector2[]>(longer, "vertices")[0], new Vector2(0f, 0.5f), "first sample, +normal side");
            Near(Read<Vector2[]>(longer, "vertices")[count * 2 - 1], new Vector2(0f, -0.5f), "first sample, -normal side comes last");
        }

        // GenerateShape 0x4aec30: one vertex per sample position, moved along the scaled planar normal by the offset when it is not
        // zero (the size field is not used). The type setter 0x4ae710 rebuilds for a changed value.
        [Test]
        public void ShapeTypeBuildsOneVertexPerSample()
        {
            PolygonColliderGenerator polygon = NewPolygon(NewComputer(3f, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)));
            polygon.type = PolygonColliderGenerator.Type.Shape;
            polygon.size = 99f;
            polygon.RebuildImmediate();
            Near(Read<Vector2[]>(polygon, "vertices"), new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, "shape");
            polygon.offset = 2f;
            polygon.RebuildImmediate();
            Near(Read<Vector2[]>(polygon, "vertices"), new[] { new Vector2(0f, 6f), new Vector2(10f, 6f) }, "shape with offset");
            Near(polygon.GetComponent<PolygonCollider2D>().GetPath(0), new[] { new Vector2(0f, 6f), new Vector2(10f, 6f) }, "collider path");

            PolygonColliderGenerator along = NewPolygon(NewComputer(new Vector3(3f, 4f, 0f), new Vector3(3f, 4f, 10f)));
            along.type = PolygonColliderGenerator.Type.Shape;
            along.offset = 5f;
            along.RebuildImmediate();
            Near(Read<Vector2[]>(along, "vertices"), new[] { new Vector2(3f, 4f), new Vector2(3f, 4f) }, "a line along z has no planar normal");
        }

        // Editor branch of PostBuild 0x4aee70 as the edge generator's: the vertices are moved into the local space and SetPath(0,
        // vertices) is applied at once while not playing, even with an update rate. The deferred shipped branch, LateRun 0x4ae880
        // and the missing collider run in play mode:
        // OriginalDreamteckColliderGeneratorPlayModeTests.PolygonUpdateRateAndLocalSpaceFollowTheEdgeGenerator.
        [Test]
        public void PolygonUpdateRateAndLocalSpaceFollowTheEdgeGenerator()
        {
            PolygonColliderGenerator polygon = NewPolygon(NewComputer(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)), 0.1f);
            polygon.type = PolygonColliderGenerator.Type.Shape;
            polygon.transform.position = new Vector3(100f, 50f, 0f);
            PolygonCollider2D collider = polygon.GetComponent<PolygonCollider2D>();
            Vector2[] untouched = collider.GetPath(0);
            polygon.RebuildImmediate();
            Assert.That(Read<bool>(polygon, "updateCollider"), Is.False, "the editor compile applies at once while not playing");
            Near(collider.GetPath(0), new[] { new Vector2(-100f, -50f), new Vector2(-90f, -50f) }, "applied at once");
            Near(Read<Vector2[]>(polygon, "vertices"), new[] { new Vector2(-100f, -50f), new Vector2(-90f, -50f) }, "local vertices");

            collider.SetPath(0, untouched);
            Invoke(polygon, "LateRun");
            Near(collider.GetPath(0), untouched, "nothing is pending for LateRun");
            Assert.That(Read<bool>(polygon, "updateCollider"), Is.False);
        }

        [Test]
        public void PolygonSerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(PolygonColliderGenerator).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "_type", "_size", "_offset", "polygonCollider", "vertices", "updateRate", "lastUpdateTime", "updateCollider" }));
            Assert.That(typeof(PolygonColliderGenerator).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "Type" }));
            Assert.That(Enum.GetNames(typeof(PolygonColliderGenerator.Type)), Is.EqualTo(new[] { "Path", "Shape" }));
            AddComponentMenu menu = (AddComponentMenu)typeof(PolygonColliderGenerator).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Polygon Collider Generator"));
            RequireComponent require = (RequireComponent)typeof(PolygonColliderGenerator).GetCustomAttributes(typeof(RequireComponent), false).Single();
            Assert.That(require.m_Type0, Is.EqualTo(typeof(PolygonCollider2D)));
            foreach (string name in new[] { "_type", "_size", "_offset", "polygonCollider", "vertices" })
            {
                FieldInfo field = typeof(PolygonColliderGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            Assert.That(typeof(PolygonColliderGenerator).GetField("updateRate").GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            string[] declared = typeof(PolygonColliderGenerator)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.That(declared, Is.EqualTo(new[]
            {
                "Awake", "Build", "GeneratePath", "GenerateShape", "LateRun", "OnDestroy", "OnDisable", "OnEnable", "PostBuild", "Reset"
            }));
        }
    }
}
