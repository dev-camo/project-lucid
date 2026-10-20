using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dreamteck;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Utilities mesh, transform and TS_* snapshot helpers. Expected arrays are derived from
    // the x86-64 bodies cited per test (the loops are traced by hand and, for the generators, by an
    // independent script), and the quirks they show are asserted deliberately.
    public sealed class OriginalDreamteckMeshTests
    {
        private const float Tolerance = 1e-5f;
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private static void Near(Quaternion actual, Quaternion expected, string message)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.01f), message);
        }

        private static void NearAll(Vector3[] actual, Vector3[] expected, string message)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), message + " length");
            for (int i = 0; i < expected.Length; i++)
            {
                Near(actual[i], expected[i], message + "[" + i + "]");
            }
        }

        private static TS_Mesh Snapshot(Vector3[] vertices, int[] triangles, params int[][] submeshes)
        {
            TS_Mesh mesh = new TS_Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.subMeshes = new List<int[]>(submeshes);
            return mesh;
        }

        private static Mesh QuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f) };
            mesh.normals = new[] { new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            return mesh;
        }

        // Original TS_Mesh .ctor 0x5007d0 and get_vertexCount 0x4face0 / set_vertexCount 0x5007c0 (a bare ret).
        [Test]
        public void DefaultsAreEmptyArraysAndAZeroBounds()
        {
            TS_Mesh mesh = new TS_Mesh();
            Assert.That(mesh.vertices, Is.Empty);
            Assert.That(mesh.normals, Is.Empty);
            Assert.That(mesh.tangents, Is.Empty);
            Assert.That(mesh.colors, Is.Empty);
            Assert.That(mesh.uv, Is.Empty);
            Assert.That(mesh.uv2, Is.Empty);
            Assert.That(mesh.uv3, Is.Empty);
            Assert.That(mesh.uv4, Is.Empty);
            Assert.That(mesh.triangles, Is.Empty);
            Assert.That(mesh.subMeshes, Is.Not.Null.And.Empty);
            Assert.That(mesh.bounds, Is.Not.Null);
            Assert.That(new[] { mesh.bounds.center, mesh.bounds.extents, mesh.bounds.max, mesh.bounds.min, mesh.bounds.size }, Is.All.EqualTo(Vector3.zero));
            Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt16));
            Assert.That(mesh.hasUpdate, Is.False);
            Assert.That(typeof(TS_Mesh).GetField("_submeshTrisCount", Own).GetValue(mesh), Is.Empty);
            Assert.That(typeof(TS_Mesh).GetField("_submeshOffsets", Own).GetValue(mesh), Is.Empty);

            Assert.That(mesh.vertexCount, Is.EqualTo(0));
            mesh.vertices = new Vector3[3];
            Assert.That(mesh.vertexCount, Is.EqualTo(3), "the getter is vertices.Length");
            mesh.vertexCount = 9;
            Assert.That(mesh.vertexCount, Is.EqualTo(3), "the setter body is empty");
        }

        // Declaration-emission check, inferred from barrier evidence: the hasUpdate store at 0x5041ec is preceded by a
        // memory barrier (0x5041e7), the compiled shape of a volatile write, but DummyDll carries no volatile modreq,
        // so this pins the inference only.
        [Test]
        public void SnapshotHasUpdateIsEmittedVolatileFromBarrierEvidence()
        {
            Assert.That(typeof(TS_Mesh).GetField("hasUpdate").GetRequiredCustomModifiers(), Does.Contain(typeof(IsVolatile)));
        }

        // Original ctor(Mesh) 0x500b60 runs CreateFromMesh 0x500f10: every channel is read once, bounds come
        // from mesh.bounds, and each submesh's triangles are APPENDED to the existing list (it is not cleared).
        [Test]
        public void CreateFromMeshReadsEveryChannelAndAppendsSubmeshes()
        {
            Mesh source = new Mesh();
            try
            {
                source.indexFormat = IndexFormat.UInt32;
                source.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(2f, 4f, 0f), new Vector3(0f, 4f, 0f) };
                source.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                source.tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f) };
                source.colors = new[] { Color.red, Color.green, Color.blue, Color.white };
                source.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                source.uv2 = new[] { new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f) };
                source.uv3 = new[] { new Vector2(3f, 3f), new Vector2(4f, 4f), new Vector2(5f, 5f), new Vector2(6f, 6f) };
                source.uv4 = new[] { new Vector2(7f, 7f), new Vector2(8f, 8f), new Vector2(9f, 9f), new Vector2(1f, 1f) };
                source.subMeshCount = 2;
                source.SetTriangles(new[] { 0, 1, 2 }, 0);
                source.SetTriangles(new[] { 0, 2, 3 }, 1);

                TS_Mesh mesh = new TS_Mesh(source);
                Assert.That(mesh.vertices, Is.EqualTo(source.vertices));
                Assert.That(mesh.normals, Is.EqualTo(source.normals));
                Assert.That(mesh.tangents, Is.EqualTo(source.tangents));
                Assert.That(mesh.colors, Is.EqualTo(source.colors));
                Assert.That(mesh.uv, Is.EqualTo(source.uv));
                Assert.That(mesh.uv2, Is.EqualTo(source.uv2));
                Assert.That(mesh.uv3, Is.EqualTo(source.uv3));
                Assert.That(mesh.uv4, Is.EqualTo(source.uv4));
                Assert.That(mesh.triangles, Is.EqualTo(source.triangles));
                Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
                Assert.That(mesh.subMeshes.Count, Is.EqualTo(2));
                Assert.That(mesh.subMeshes[0], Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(mesh.subMeshes[1], Is.EqualTo(new[] { 0, 2, 3 }));
                Assert.That(mesh.bounds.center, Is.EqualTo(source.bounds.center));
                Assert.That(mesh.bounds.extents, Is.EqualTo(source.bounds.extents));
                Assert.That(mesh.bounds.size, Is.EqualTo(source.bounds.size));
                Assert.That(mesh.bounds.max, Is.EqualTo(source.bounds.center + source.bounds.extents));
                Assert.That(mesh.bounds.min, Is.EqualTo(source.bounds.center - source.bounds.extents));

                mesh.CreateFromMesh(source);
                Assert.That(mesh.subMeshes.Count, Is.EqualTo(4), "the second read appends two more submeshes");
                Assert.That(mesh.vertices, Is.EqualTo(source.vertices), "the other channels are replaced");
                Assert.Throws<NullReferenceException>(() => mesh.CreateFromMesh(null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        // Original Clear 0x5012a0 replaces the geometry arrays, the submesh list and the bounds with fresh
        // empty ones; indexFormat, hasUpdate and the submesh work arrays are not written.
        [Test]
        public void ClearResetsTheGeometryButNotTheFormatFlagOrWorkArrays()
        {
            TS_Mesh mesh = Snapshot(new[] { Vector3.one, Vector3.zero, Vector3.up }, new[] { 0, 1, 2 }, new[] { 0, 1, 2 });
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.hasUpdate = true;
            mesh.bounds = new TS_Bounds(Vector3.one, Vector3.one);
            int[] workArray = { 5 };
            typeof(TS_Mesh).GetField("_submeshTrisCount", Own).SetValue(mesh, workArray);
            List<int[]> oldSubMeshes = mesh.subMeshes;
            TS_Bounds oldBounds = mesh.bounds;

            mesh.Clear();
            Assert.That(mesh.vertices, Is.Empty);
            Assert.That(mesh.triangles, Is.Empty);
            Assert.That(mesh.subMeshes, Is.Empty);
            Assert.That(mesh.subMeshes, Is.Not.SameAs(oldSubMeshes));
            Assert.That(mesh.bounds, Is.Not.SameAs(oldBounds));
            Assert.That(mesh.bounds.center, Is.EqualTo(Vector3.zero));
            Assert.That(mesh.bounds.size, Is.EqualTo(Vector3.zero));
            Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            Assert.That(mesh.hasUpdate, Is.True);
            Assert.That(typeof(TS_Mesh).GetField("_submeshTrisCount", Own).GetValue(mesh), Is.SameAs(workArray));
        }

        // Original WriteMesh 0x503f10: allocate when the reference is null, Clear, set indexFormat, vertices
        // and normals unconditionally, then tangents/colors/uv..uv4 only when their length equals the vertex
        // count; triangles, submeshes (when any), RecalculateBounds, and finally hasUpdate = false.
        [Test]
        public void WriteMeshAllocatesAndSkipsChannelsThatDoNotMatchTheVertexCount()
        {
            TS_Mesh snapshot = new TS_Mesh();
            snapshot.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(0f, 4f, 0f) };
            snapshot.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            snapshot.tangents = new Vector4[2];
            snapshot.colors = new[] { Color.red, Color.green, Color.blue };
            snapshot.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            snapshot.uv2 = new Vector2[1];
            snapshot.uv3 = new[] { Vector2.one, Vector2.one, Vector2.one };
            snapshot.uv4 = new Vector2[0];
            snapshot.triangles = new[] { 0, 1, 2 };
            snapshot.subMeshes = new List<int[]> { new[] { 0, 1, 2 } };
            snapshot.indexFormat = IndexFormat.UInt32;
            snapshot.hasUpdate = true;

            Mesh mesh = null;
            try
            {
                snapshot.WriteMesh(ref mesh);
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.EqualTo(3));
                Assert.That(mesh.normals, Is.EqualTo(snapshot.normals));
                Assert.That(mesh.tangents, Is.Empty, "tangents length 2 != 3");
                Assert.That(mesh.colors, Is.EqualTo(snapshot.colors));
                Assert.That(mesh.uv, Is.EqualTo(snapshot.uv));
                Assert.That(mesh.uv2, Is.Empty, "uv2 length 1 != 3");
                Assert.That(mesh.uv3, Is.EqualTo(snapshot.uv3));
                Assert.That(mesh.uv4, Is.Empty, "uv4 length 0 != 3");
                Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
                Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                Assert.That(mesh.GetTriangles(0), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(mesh.bounds.size, Is.EqualTo(new Vector3(2f, 4f, 0f)));
                Assert.That(snapshot.hasUpdate, Is.False);

                Mesh same = mesh;
                snapshot.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f) };
                snapshot.hasUpdate = true;
                snapshot.WriteMesh(ref mesh);
                Assert.That(mesh, Is.SameAs(same), "an existing mesh is cleared and reused");
                Assert.That(mesh.bounds.size, Is.EqualTo(new Vector3(1f, 1f, 0f)));
                Assert.That(snapshot.hasUpdate, Is.False);
            }
            finally
            {
                if (mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        private static void ExpectBoundsRebuiltFromCenterAndSize(TS_Bounds bounds)
        {
            Assert.That(bounds.center, Is.EqualTo(new Vector3(100f, 200f, 300f)));
            Assert.That(bounds.size, Is.EqualTo(new Vector3(4f, 4f, 4f)));
            Assert.That(bounds.extents, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(bounds.min, Is.EqualTo(new Vector3(98f, 198f, 298f)), "a field-for-field copy would keep min (0, 0, 0)");
            Assert.That(bounds.max, Is.EqualTo(new Vector3(102f, 202f, 302f)));
        }

        // Original Copy 0x503020: a deep copy of every array and submesh, a new TS_Bounds(center, size), and
        // indexFormat; hasUpdate is not copied.
        [Test]
        public void CopyDeepCopiesTheArraysAndBoundsButNotHasUpdate()
        {
            TS_Mesh source = Snapshot(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 }, new[] { 0, 1, 2 });
            source.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            source.tangents = new[] { new Vector4(1f, 0f, 0f, 1f) };
            source.colors = new[] { Color.red };
            source.uv = new[] { Vector2.one };
            source.uv2 = new[] { Vector2.zero, Vector2.one };
            source.uv3 = new[] { Vector2.up };
            source.uv4 = new[] { Vector2.right };
            source.bounds = new TS_Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 6f, 8f));
            source.indexFormat = IndexFormat.UInt32;
            source.hasUpdate = true;

            TS_Mesh copy = TS_Mesh.Copy(source);
            Assert.That(copy, Is.Not.SameAs(source));
            Assert.That(copy.vertices, Is.EqualTo(source.vertices).And.Not.SameAs(source.vertices));
            Assert.That(copy.normals, Is.EqualTo(source.normals).And.Not.SameAs(source.normals));
            Assert.That(copy.tangents, Is.EqualTo(source.tangents).And.Not.SameAs(source.tangents));
            Assert.That(copy.colors, Is.EqualTo(source.colors).And.Not.SameAs(source.colors));
            Assert.That(copy.uv, Is.EqualTo(source.uv).And.Not.SameAs(source.uv));
            Assert.That(copy.uv2, Is.EqualTo(source.uv2).And.Not.SameAs(source.uv2));
            Assert.That(copy.uv3, Is.EqualTo(source.uv3).And.Not.SameAs(source.uv3));
            Assert.That(copy.uv4, Is.EqualTo(source.uv4).And.Not.SameAs(source.uv4));
            Assert.That(copy.triangles, Is.EqualTo(source.triangles).And.Not.SameAs(source.triangles));
            Assert.That(copy.subMeshes, Is.Not.SameAs(source.subMeshes));
            Assert.That(copy.subMeshes[0], Is.EqualTo(new[] { 0, 1, 2 }).And.Not.SameAs(source.subMeshes[0]));
            Assert.That(copy.bounds, Is.Not.SameAs(source.bounds));
            Assert.That(copy.bounds.center, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(copy.bounds.size, Is.EqualTo(new Vector3(4f, 6f, 8f)));
            Assert.That(copy.bounds.extents, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(copy.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            Assert.That(copy.hasUpdate, Is.False);
            Assert.Throws<NullReferenceException>(() => TS_Mesh.Copy(null));

            // Copy reads only bounds.center (+0x10) and bounds.size (+0x40) (0x503507, 0x50351a) and rebuilds
            // through the (center, size) constructor, so a center that disagrees with min/max is re-derived from.
            TS_Mesh skewed = new TS_Mesh();
            skewed.bounds = new TS_Bounds(Vector3.zero, new Vector3(4f, 4f, 4f), new Vector3(100f, 200f, 300f));
            ExpectBoundsRebuiltFromCenterAndSize(TS_Mesh.Copy(skewed).bounds);
        }

        // Original Absorb 0x5036a0 resizes only the arrays whose length differs from the input, copies the
        // input into them, reuses submesh arrays of equal length (rebuilding the list when the counts
        // differ) and replaces the bounds; indexFormat and hasUpdate are left alone.
        [Test]
        public void AbsorbReusesMatchingArraysAndReplacesBounds()
        {
            TS_Mesh input = Snapshot(new[] { Vector3.right, Vector3.up, Vector3.forward }, new[] { 2, 1, 0 }, new[] { 2, 1, 0 });
            input.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            input.bounds = new TS_Bounds(new Vector3(1f, 1f, 1f), new Vector3(2f, 2f, 2f));
            input.indexFormat = IndexFormat.UInt32;

            TS_Mesh target = Snapshot(new Vector3[3], new int[3], new int[3]);
            target.normals = new Vector3[5];
            target.hasUpdate = true;
            Vector3[] vertexArray = target.vertices;
            int[] triangleArray = target.triangles;
            int[] submeshArray = target.subMeshes[0];
            List<int[]> list = target.subMeshes;
            Vector3[] normalArray = target.normals;
            TS_Bounds oldBounds = target.bounds;

            target.Absorb(input);
            Assert.That(target.vertices, Is.SameAs(vertexArray), "equal length keeps the array");
            Assert.That(target.vertices, Is.EqualTo(input.vertices));
            Assert.That(target.triangles, Is.SameAs(triangleArray));
            Assert.That(target.triangles, Is.EqualTo(new[] { 2, 1, 0 }));
            Assert.That(target.normals, Is.Not.SameAs(normalArray), "length 5 != 3 reallocates");
            Assert.That(target.normals, Is.EqualTo(input.normals));
            Assert.That(target.subMeshes, Is.SameAs(list), "equal counts keep the list");
            Assert.That(target.subMeshes[0], Is.SameAs(submeshArray), "equal lengths keep the submesh array");
            Assert.That(target.subMeshes[0], Is.EqualTo(new[] { 2, 1, 0 }));
            Assert.That(target.bounds, Is.Not.SameAs(oldBounds));
            Assert.That(target.bounds.center, Is.EqualTo(new Vector3(1f, 1f, 1f)));
            Assert.That(target.bounds.size, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(target.indexFormat, Is.EqualTo(IndexFormat.UInt16), "indexFormat is not copied");
            Assert.That(target.hasUpdate, Is.True, "hasUpdate is not touched");

            input.subMeshes.Add(new[] { 0, 1, 2 });
            target.Absorb(input);
            Assert.That(target.subMeshes, Is.Not.SameAs(list), "differing counts build a new list");
            Assert.That(target.subMeshes.Count, Is.EqualTo(2));
            Assert.That(target.subMeshes[1], Is.EqualTo(new[] { 0, 1, 2 }));

            input.subMeshes[1] = new[] { 0, 1, 2, 1, 2, 0 };
            target.subMeshes[1] = new int[3];
            int[] firstSubmesh = target.subMeshes[0];
            target.Absorb(input);
            Assert.That(target.subMeshes[0], Is.SameAs(firstSubmesh));
            Assert.That(target.subMeshes[1], Is.EqualTo(new[] { 0, 1, 2, 1, 2, 0 }), "a different submesh length reallocates");

            // Absorb rebuilds the bounds from center and size alone, like Copy.
            input.bounds = new TS_Bounds(Vector3.zero, new Vector3(4f, 4f, 4f), new Vector3(100f, 200f, 300f));
            target.Absorb(input);
            ExpectBoundsRebuiltFromCenterAndSize(target.bounds);
        }

        // Original Combine 0x5015f0: totals the vertices/triangles and the submesh count, sizes EVERY channel
        // to the vertex total (even where no input has data), clears the submesh list when it is longer than
        // needed, then copies each mesh at a running vertex offset and rebases triangle indices by it.
        // Array and submesh instances of the right size are reused on the next call.
        [Test]
        public void CombineRebasesTrianglesAndSizesEveryChannelToTheVertexTotal()
        {
            TS_Mesh first = Snapshot(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 }, new[] { 0, 1, 2 });
            first.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            TS_Mesh second = Snapshot(new[] { Vector3.one, Vector3.left, Vector3.down }, new[] { 0, 2, 1, 1, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 1, 2 });
            second.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
            List<TS_Mesh> inputs = new List<TS_Mesh> { first, second };

            TS_Mesh combined = new TS_Mesh();
            combined.subMeshes.Add(new int[1]);
            combined.subMeshes.Add(new int[1]);
            combined.subMeshes.Add(new int[1]);
            combined.indexFormat = IndexFormat.UInt32;
            combined.Combine(inputs);

            Assert.That(combined.vertices, Is.EqualTo(new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one, Vector3.left, Vector3.down }));
            Assert.That(combined.normals, Is.EqualTo(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward }));
            Assert.That(combined.uv.Length, Is.EqualTo(6), "channels without data are still sized to the vertex total");
            Assert.That(combined.uv2.Length, Is.EqualTo(6));
            Assert.That(combined.uv3.Length, Is.EqualTo(6));
            Assert.That(combined.uv4.Length, Is.EqualTo(6));
            Assert.That(combined.colors.Length, Is.EqualTo(6));
            Assert.That(combined.tangents.Length, Is.EqualTo(6));
            Assert.That(combined.triangles, Is.EqualTo(new[] { 0, 1, 2, 3, 5, 4, 4, 4, 5 }));
            Assert.That(combined.subMeshes.Count, Is.EqualTo(2), "the three stale submeshes are cleared down to two");
            Assert.That(combined.subMeshes[0], Is.EqualTo(new[] { 0, 1, 2, 3, 5, 4 }));
            Assert.That(combined.subMeshes[1], Is.EqualTo(new[] { 4, 4, 5 }));
            Assert.That(combined.indexFormat, Is.EqualTo(IndexFormat.UInt32), "indexFormat is not touched");

            Vector3[] vertices = combined.vertices;
            int[] firstSubmesh = combined.subMeshes[0];
            combined.Combine(inputs);
            Assert.That(combined.vertices, Is.SameAs(vertices));
            Assert.That(combined.subMeshes[0], Is.SameAs(firstSubmesh));
            Assert.That(combined.triangles, Is.EqualTo(new[] { 0, 1, 2, 3, 5, 4, 4, 4, 5 }), "a repeat is idempotent");
            Assert.That(combined.subMeshes[1], Is.EqualTo(new[] { 4, 4, 5 }));
            Assert.Throws<NullReferenceException>(() => combined.Combine(null));
        }

        // Original AddMeshes 0x502220: new arrays are base + added (tangents are sized from the existing
        // tangents, every other channel from the existing vertex count), the base is copied first, each added
        // mesh follows at the running offset with triangle indices rebased, and submesh j of the added meshes
        // is concatenated. The base's own submesh triangles are not carried into the rebuilt list: the new
        // submesh arrays are sized for the added meshes only, so appending onto a non-empty base submesh
        // writes past the end of its array.
        [Test]
        public void AddMeshesAppendsAfterTheBaseAndDropsOrFaultsOnBaseSubmeshes()
        {
            TS_Mesh baseMesh = Snapshot(new[] { Vector3.zero, Vector3.right }, new[] { 0, 1, 0 });
            baseMesh.tangents = new Vector4[4];
            TS_Mesh addedOne = Snapshot(new[] { Vector3.up, Vector3.down }, new[] { 0, 1, 1 }, new[] { 0, 1, 1 });
            TS_Mesh addedTwo = Snapshot(new[] { Vector3.forward }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });
            List<TS_Mesh> added = new List<TS_Mesh> { addedOne, addedTwo };

            baseMesh.AddMeshes(added);
            Assert.That(baseMesh.vertices, Is.EqualTo(new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.down, Vector3.forward }));
            Assert.That(baseMesh.triangles, Is.EqualTo(new[] { 0, 1, 0, 2, 3, 3, 4, 4, 4 }));
            Assert.That(baseMesh.subMeshes.Count, Is.EqualTo(1));
            Assert.That(baseMesh.subMeshes[0], Is.EqualTo(new[] { 2, 3, 3, 4, 4, 4 }));
            Assert.That(baseMesh.normals.Length, Is.EqualTo(5), "vertex channels are sized base vertices + added vertices");
            Assert.That(baseMesh.uv.Length, Is.EqualTo(5));
            Assert.That(baseMesh.colors.Length, Is.EqualTo(5));
            Assert.That(baseMesh.tangents.Length, Is.EqualTo(7), "tangents are sized existing tangents (4) + added vertices (3), not vertices (2) + 3");

            TS_Mesh withSubmesh = Snapshot(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 }, new[] { 0, 1, 2 });
            withSubmesh.tangents = new Vector4[3];
            Vector3[] untouched = withSubmesh.vertices;
            Assert.Throws<IndexOutOfRangeException>(() => withSubmesh.AddMeshes(new List<TS_Mesh> { addedOne }));
            Assert.That(withSubmesh.vertices, Is.SameAs(untouched), "the fault happens before any field is replaced");
            Assert.Throws<NullReferenceException>(() => baseMesh.AddMeshes(null));
        }

        // Original FlipTriangles 0x4fe4c0 swaps the first and last index of each triple; FlipFaces 0x4fe520 does
        // that for every submesh array and the main triangle list and then negates every normal.
        [Test]
        public void FlipTrianglesAndFlipFacesSwapTheOuterIndices()
        {
            int[] triangles = { 0, 1, 2, 3, 4, 5 };
            MeshUtility.FlipTriangles(ref triangles);
            Assert.That(triangles, Is.EqualTo(new[] { 2, 1, 0, 5, 4, 3 }));
            int[] ragged = { 0, 1, 2, 3 };
            Assert.Throws<IndexOutOfRangeException>(() => MeshUtility.FlipTriangles(ref ragged), "the second triple reads index 5");

            TS_Mesh mesh = Snapshot(new Vector3[3], new[] { 0, 1, 2 }, new[] { 6, 7, 8 }, new[] { 9, 10, 11, 12, 13, 14 });
            mesh.normals = new[] { new Vector3(1f, 2f, 3f), new Vector3(0f, -1f, 0.5f), Vector3.zero };
            int[] firstSubmesh = mesh.subMeshes[0];
            MeshUtility.FlipFaces(mesh);
            Assert.That(mesh.triangles, Is.EqualTo(new[] { 2, 1, 0 }));
            Assert.That(mesh.subMeshes[0], Is.SameAs(firstSubmesh).And.EqualTo(new[] { 8, 7, 6 }), "submeshes are flipped in place");
            Assert.That(mesh.subMeshes[1], Is.EqualTo(new[] { 11, 10, 9, 14, 13, 12 }));
            Assert.That(mesh.normals, Is.EqualTo(new[] { new Vector3(-1f, -2f, -3f), new Vector3(0f, 1f, -0.5f), Vector3.zero }));
        }

        // Original TransformVertices 0x4fcc70 / TransformNormals 0x4fccf0 / TransformMesh(TS_Mesh) 0x4fc9d0 use
        // Matrix4x4.MultiplyPoint3x4 for positions and MultiplyVector for normals (no translation, no
        // renormalizing). The matrix scales by 2, turns 90 degrees about Y ((x, y, z) -> (z, y, -x)) and moves by (1, 2, 3).
        [Test]
        public void TransformHelpersApplyPointAndVectorMatrices()
        {
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, 2f, 2f));

            Vector3[] vertices = { new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f) };
            MeshUtility.TransformVertices(vertices, matrix);
            NearAll(vertices, new[] { new Vector3(1f, 2f, 1f), new Vector3(1f, 4f, 3f), new Vector3(3f, 2f, 3f) }, "vertices");

            Vector3[] normals = { new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f) };
            MeshUtility.TransformNormals(normals, matrix);
            NearAll(normals, new[] { new Vector3(0f, 0f, -2f), new Vector3(0f, 2f, 0f), new Vector3(2f, 0f, 0f) }, "normals are rotated and scaled, not normalized");

            TS_Mesh snapshot = Snapshot(new[] { new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f) }, new int[0]);
            snapshot.normals = new[] { new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f) };
            MeshUtility.TransformMesh(snapshot, matrix);
            NearAll(snapshot.vertices, new[] { new Vector3(1f, 2f, 1f), new Vector3(1f, 4f, 3f), new Vector3(3f, 2f, 3f) }, "snapshot vertices");
            NearAll(snapshot.normals, new[] { new Vector3(0f, 0f, -2f), new Vector3(0f, 2f, 0f), new Vector3(2f, 0f, 0f) }, "snapshot normals");
        }

        // Original TransformMesh(TS_Mesh) 0x4fc9d0 jumps to its epilogue (0x4fcaa4) when vertices (0x4fc9ea) or
        // normals (0x4fc9f7) is null, so the call returns without touching either array.
        [Test]
        public void TransformMeshOnASnapshotWithoutVerticesOrNormalsReturnsSilently()
        {
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.identity, Vector3.one);
            TS_Mesh noVertices = new TS_Mesh();
            noVertices.vertices = null;
            Assert.DoesNotThrow(() => MeshUtility.TransformMesh(noVertices, matrix));

            TS_Mesh noNormals = Snapshot(new[] { new Vector3(1f, 0f, 0f) }, new int[0]);
            noNormals.normals = null;
            Assert.DoesNotThrow(() => MeshUtility.TransformMesh(noNormals, matrix));
            Assert.That(noNormals.vertices[0], Is.EqualTo(new Vector3(1f, 0f, 0f)), "nothing is transformed");
        }

        // Original CalculateTangents 0x4fa4e0 reallocates its static tan1/tan2 only when the vertex count changes and
        // never clears them. One triangle (0,0,0) (1,0,0) (0,1,0) with uv (0,0) (1,0) (0,1) gives sdir (1,0,0) and
        // tdir (0,1,0), so w = +1. A second call with v flipped (uv (0,-1) for the last vertex) gives sdir (1,0,0) and
        // tdir (0,-1,0); a fresh sum would end with w = -1, but the kept sums leave tan2 = (0,0,0) and w = +1.
        [Test]
        public void CalculateTangentsAccumulatesIntoTheStaticScratchArrays()
        {
            FieldInfo tan1Field = typeof(MeshUtility).GetField("tan1", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo tan2Field = typeof(MeshUtility).GetField("tan2", BindingFlags.Static | BindingFlags.NonPublic);
            object savedTan1 = tan1Field.GetValue(null);
            object savedTan2 = tan2Field.GetValue(null);
            try
            {
                // Force reallocation so the result does not depend on which test ran first.
                tan1Field.SetValue(null, new Vector3[0]);
                tan2Field.SetValue(null, new Vector3[0]);
                TS_Mesh mesh = Snapshot(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 });
                mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
                mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) };

                MeshUtility.CalculateTangents(mesh);
                Assert.That(mesh.tangents, Is.EqualTo(new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) }), "first call");

                mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f) };
                MeshUtility.CalculateTangents(mesh);
                Assert.That(mesh.tangents, Is.EqualTo(new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) }), "second call keeps the sums");
            }
            finally
            {
                tan1Field.SetValue(null, savedTan1);
                tan2Field.SetValue(null, savedTan2);
            }
        }

        // Original TransformMesh(Mesh) 0x4fcac0 calls get_vertices (0x0600090d) for the array it names normals,
        // so after the call the mesh normals are MultiplyVector applied to the ORIGINAL VERTEX POSITIONS and
        // the stored normals are discarded; vertices become MultiplyPoint3x4 of themselves.
        [Test]
        public void TransformMeshOnAUnityMeshReplacesNormalsWithTransformedVertexVectors()
        {
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, 2f, 2f));
            Mesh mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f) };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
                MeshUtility.TransformMesh(mesh, matrix);
                NearAll(mesh.vertices, new[] { new Vector3(1f, 2f, 1f), new Vector3(1f, 4f, 3f), new Vector3(3f, 2f, 3f) }, "vertices");
                NearAll(mesh.normals, new[] { new Vector3(0f, 0f, -2f), new Vector3(0f, 2f, 0f), new Vector3(2f, 0f, 0f) }, "normals come from the vertex positions");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        // Original GeneratePlaneTriangles 0x4fa0f0/0x4fa1a0: x quads per row with x + 1 vertices per row and a
        // vertex skip at each row end; the sequences below were produced by an independent script from the
        // traced loop. The short overload forwards startTriangleIndex = 0, startVertex = 0 whatever it is given.
        [Test]
        public void GeneratePlaneTrianglesWalksRowsAndSkipsRowEnds()
        {
            Assert.That(MeshUtility.GeneratePlaneTriangles(2, 2, false), Is.EqualTo(new[] { 0, 1, 3, 1, 4, 3, 1, 2, 4, 2, 5, 4 }));
            Assert.That(MeshUtility.GeneratePlaneTriangles(2, 2, true), Is.EqualTo(new[] { 3, 1, 0, 3, 4, 1, 4, 2, 1, 4, 5, 2 }));
            Assert.That(MeshUtility.GeneratePlaneTriangles(2, 3, false), Is.EqualTo(new[] { 0, 1, 3, 1, 4, 3, 1, 2, 4, 2, 5, 4, 3, 4, 6, 4, 7, 6, 4, 5, 7, 5, 8, 7 }), "the second row starts at vertex 3, skipping vertex 2");
            Assert.That(MeshUtility.GeneratePlaneTriangles(2, 2, false, 6, 10), Is.EqualTo(MeshUtility.GeneratePlaneTriangles(2, 2, false)), "the start arguments are ignored");
            Assert.That(MeshUtility.GeneratePlaneTriangles(1, 1, false), Is.Empty);

            int[] triangles = { 7, 7, 7, 7, 7, 7 };
            int[] same = triangles;
            int[] result = MeshUtility.GeneratePlaneTriangles(ref triangles, 2, 2, false, 6, 10, true);
            Assert.That(result, Is.SameAs(triangles));
            Assert.That(triangles, Is.Not.SameAs(same), "reallocateArray with a different size allocates");
            Assert.That(triangles, Is.EqualTo(new[] { 7, 7, 7, 7, 7, 7, 10, 11, 13, 11, 14, 13, 11, 12, 14, 12, 15, 14 }), "startTriangleIndex 6 keeps the first six entries");

            int[] exact = new int[12];
            int[] exactReference = exact;
            MeshUtility.GeneratePlaneTriangles(ref exact, 2, 2, false, 0, 0, true);
            Assert.That(exact, Is.SameAs(exactReference), "a matching length is reused");
            Assert.That(exact, Is.EqualTo(new[] { 0, 1, 3, 1, 4, 3, 1, 2, 4, 2, 5, 4 }));

            int[] wrongSize = new int[5];
            MeshUtility.GeneratePlaneTriangles(ref wrongSize, 2, 2, false, 0, 0, true);
            Assert.That(wrongSize.Length, Is.EqualTo(12));

            int[] tooSmall = new int[5];
            Assert.Throws<IndexOutOfRangeException>(() => MeshUtility.GeneratePlaneTriangles(ref tooSmall, 2, 2, false));
        }

        private static readonly Vector2[] CounterClockwiseSquare = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };

        private static int[] Triangulated(Vector2[] points)
        {
            int[] output = new int[0];
            MeshUtility.Triangulate(points, ref output);
            return output;
        }

        // Original Triangulate 0x4fd9d0 (ear clipping): fewer than three points answer a new empty array; the
        // vertex ring is ascending for a positive Area and descending otherwise; each clipped ear is queued as
        // (V[w], V[v], V[u]) and the list is reversed at the end, except when the 2 * nv pass budget runs out,
        // which leaves the partial list unreversed. Sequences come from an independent script of the traced loop.
        [Test]
        public void TriangulateClipsEarsAndOnlyReversesWhenItFinishes()
        {
            Assert.That(Triangulated(CounterClockwiseSquare), Is.EqualTo(new[] { 1, 2, 3, 3, 0, 1 }));
            Assert.That(Triangulated(new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f) }), Is.EqualTo(new[] { 2, 1, 0, 0, 3, 2 }), "clockwise input");
            Assert.That(Triangulated(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) }), Is.EqualTo(new[] { 2, 0, 1 }));
            Assert.That(
                Triangulated(new[] { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 1f), new Vector2(1f, 1f), new Vector2(1f, 2f), new Vector2(0f, 2f) }),
                Is.EqualTo(new[] { 3, 5, 0, 0, 2, 3, 3, 4, 5, 0, 1, 2 }),
                "concave L");
            Assert.That(
                Triangulated(new[] { new Vector2(1f, 0f), new Vector2(1f, 2f), new Vector2(3f, 1f), new Vector2(0f, 2f) }),
                Is.EqualTo(new[] { 1, 0, 3 }),
                "one ear clips, the remaining ring is clockwise, and the partial list is returned in clip order");
            Assert.That(Triangulated(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f) }), Is.Empty, "collinear points never snip");

            int[] tooFew = new int[5];
            MeshUtility.Triangulate(new[] { Vector2.zero, Vector2.one }, ref tooFew);
            Assert.That(tooFew, Is.Empty);

            int[] reusable = new int[6];
            int[] reusableReference = reusable;
            MeshUtility.Triangulate(CounterClockwiseSquare, ref reusable);
            Assert.That(reusable, Is.SameAs(reusableReference), "an output of the right length is filled in place");
            Assert.That(reusable, Is.EqualTo(new[] { 1, 2, 3, 3, 0, 1 }));
            int[] nullOutput = null;
            Assert.Throws<NullReferenceException>(() => MeshUtility.Triangulate(CounterClockwiseSquare, ref nullOutput));
        }

        private static object InvokePrivate(string name, params object[] arguments)
        {
            return typeof(MeshUtility).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
        }

        // Original Area 0x4fe1b0 (half the shoelace sum over the first maxCount points, 0 for maxCount <= 0), Snip
        // 0x4fe230 (convex corner with cross >= Mathf.Epsilon and no other ring point inside) and InsideTriangle
        // 0x4ff4e0 (three edge cross products, each >= 0, so edges and corners count and orientation matters).
        [Test]
        public void AreaSnipAndInsideTriangleKeepTheirNativePredicates()
        {
            Assert.That((float)InvokePrivate("Area", CounterClockwiseSquare, 4), Is.EqualTo(1f));
            Assert.That((float)InvokePrivate("Area", new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f) }, 4), Is.EqualTo(-1f));
            Assert.That((float)InvokePrivate("Area", CounterClockwiseSquare, 3), Is.EqualTo(0.5f));
            Assert.That((float)InvokePrivate("Area", CounterClockwiseSquare, 0), Is.EqualTo(0f));

            Vector2 a = new Vector2(0f, 0f);
            Vector2 b = new Vector2(4f, 0f);
            Vector2 c = new Vector2(0f, 4f);
            (Vector2 point, bool expected)[] counterClockwise =
            {
                (new Vector2(1f, 1f), true), (new Vector2(5f, 5f), false), (new Vector2(2f, 0f), true), (new Vector2(0f, 0f), true), (new Vector2(-0.1f, 1f), false), (new Vector2(2f, 2f), true), (new Vector2(2.1f, 2.1f), false),
            };
            foreach (var row in counterClockwise)
            {
                Assert.That((bool)InvokePrivate("InsideTriangle", a, b, c, row.point), Is.EqualTo(row.expected), "counter-clockwise " + row.point);
            }

            Assert.That((bool)InvokePrivate("InsideTriangle", a, c, b, new Vector2(1f, 1f)), Is.False, "the clockwise winding rejects an interior point");

            int[] ring = { 0, 1, 2, 3 };
            Assert.That((bool)InvokePrivate("Snip", CounterClockwiseSquare, 0, 1, 2, 4, ring), Is.True);
            Assert.That((bool)InvokePrivate("Snip", CounterClockwiseSquare, 2, 1, 0, 4, ring), Is.False, "a clockwise corner has a negative cross product");
            Assert.That((bool)InvokePrivate("Snip", new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f), Vector2.one }, 0, 1, 2, 4, ring), Is.False, "collinear corner: Mathf.Epsilon > 0");
            Assert.That((bool)InvokePrivate("Snip", new[] { new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(0f, 4f), new Vector2(1f, 1f) }, 0, 1, 2, 4, ring), Is.False, "another ring point lies inside");
            Assert.That((bool)InvokePrivate("Snip", new[] { new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(0f, 4f), new Vector2(1f, 1f) }, 0, 1, 2, 3, ring), Is.True, "with n = 3 the extra point is never examined");

            // NaN truth table. InsideTriangle tests each cross with an ordered ucomiss/jae (0x4ff502) and cmpleps
            // (0x4ff543), so NaN is outside; a `!(x < 0)` form would call it inside. Snip rejects only on
            // ucomiss Epsilon, cross; jbe (0x4fe333), so a NaN cross is not rejected; a `!(cross >= Epsilon)` form would reject it.
            Assert.That((bool)InvokePrivate("InsideTriangle", a, b, c, new Vector2(float.NaN, float.NaN)), Is.False, "a NaN point is not inside");
            Assert.That((bool)InvokePrivate("Snip", new[] { new Vector2(0f, 0f), new Vector2(float.NaN, 0f), new Vector2(0f, 1f) }, 0, 1, 2, 3, new[] { 0, 1, 2 }), Is.True, "a NaN cross product is not rejected");
        }

        // Original MakeDoublesided(TS_Mesh) 0x4fb940: the arrays are doubled; the second half repeats the vertices,
        // negates the normals, repeats uv and colors, and reverses each triangle with indices raised by the
        // original vertex count; every submesh gets the same treatment. Colors are copied only where present.
        [Test]
        public void MakeDoublesidedAppendsBackFacesToASnapshot()
        {
            TS_Mesh mesh = Snapshot(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 }, new[] { 0, 1, 2 });
            mesh.normals = new[] { new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), new Vector3(0f, -2f, 0.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) };
            mesh.colors = new[] { Color.red, Color.green, Color.blue };

            MeshUtility.MakeDoublesided(mesh);
            Assert.That(mesh.vertices, Is.EqualTo(new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.zero, Vector3.right, Vector3.up }));
            Assert.That(mesh.normals, Is.EqualTo(new[] { new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), new Vector3(0f, -2f, 0.5f), new Vector3(0f, 0f, -1f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 2f, -0.5f) }));
            Assert.That(mesh.uv, Is.EqualTo(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) }));
            Assert.That(mesh.colors, Is.EqualTo(new[] { Color.red, Color.green, Color.blue, Color.red, Color.green, Color.blue }));
            Assert.That(mesh.triangles, Is.EqualTo(new[] { 0, 1, 2, 5, 4, 3 }));
            Assert.That(mesh.subMeshes[0], Is.EqualTo(new[] { 0, 1, 2, 5, 4, 3 }));

            TS_Mesh noColors = Snapshot(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 });
            noColors.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            noColors.uv = new Vector2[3];
            MeshUtility.MakeDoublesided(noColors);
            Assert.That(noColors.colors, Is.Empty, "an empty color array doubles to empty");
            Assert.That(noColors.subMeshes, Is.Empty);
        }

        [Test]
        public void MakeDoublesidedAppendsBackFacesToAUnityMesh()
        {
            Mesh mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
                mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) };
                mesh.triangles = new[] { 0, 1, 2 };
                MeshUtility.MakeDoublesided(mesh);
                Assert.That(mesh.vertexCount, Is.EqualTo(6));
                Assert.That(mesh.normals, Is.EqualTo(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward }));
                Assert.That(mesh.uv.Length, Is.EqualTo(6));
                Assert.That(mesh.uv[4], Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(mesh.GetTriangles(0), Is.EqualTo(new[] { 0, 1, 2, 5, 4, 3 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        // Original MakeDoublesidedHalf 0x4fc440 assumes the arrays are already twice as long and fills the
        // second half: vertices, tangents, uv..uv4 and colors repeated, normals negated, triangles and
        // submeshes reversed with indices raised by half the vertex count. Channels shorter than the index are skipped.
        [Test]
        public void MakeDoublesidedHalfFillsTheSecondHalfInPlace()
        {
            TS_Mesh mesh = Snapshot(new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.zero, Vector3.zero, Vector3.zero }, new[] { 0, 1, 2, 0, 0, 0 }, new[] { 0, 1, 2, 9, 9, 9 });
            mesh.normals = new[] { Vector3.back, new Vector3(1f, 2f, 3f), Vector3.up, Vector3.zero, Vector3.zero, Vector3.zero };
            mesh.tangents = new[] { new Vector4(1f, 2f, 3f, 4f), Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero };
            mesh.uv = new[] { new Vector2(1f, 2f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
            mesh.colors = new[] { Color.red, Color.green, Color.blue, Color.clear, Color.clear, Color.clear };

            MeshUtility.MakeDoublesidedHalf(mesh);
            Assert.That(mesh.vertices, Is.EqualTo(new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.zero, Vector3.right, Vector3.up }));
            Assert.That(mesh.normals[3], Is.EqualTo(Vector3.forward));
            Assert.That(mesh.normals[4], Is.EqualTo(new Vector3(-1f, -2f, -3f)));
            Assert.That(mesh.normals[5], Is.EqualTo(Vector3.down));
            Assert.That(mesh.tangents[3], Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)), "tangents are repeated, not negated");
            Assert.That(mesh.uv[3], Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(mesh.colors[3], Is.EqualTo(Color.red));
            Assert.That(mesh.colors[5], Is.EqualTo(Color.blue));
            Assert.That(mesh.uv2, Is.Empty);
            Assert.That(mesh.triangles, Is.EqualTo(new[] { 0, 1, 2, 5, 4, 3 }));
            Assert.That(mesh.subMeshes[0], Is.EqualTo(new[] { 0, 1, 2, 5, 4, 3 }));

            TS_Mesh shortChannel = Snapshot(new[] { Vector3.zero, Vector3.right }, new int[0]);
            shortChannel.uv3 = new[] { Vector2.one };
            Assert.Throws<IndexOutOfRangeException>(() => MeshUtility.MakeDoublesidedHalf(shortChannel), "a non-empty channel shorter than the doubled vertex array writes past its end");
        }

        // Original BreakMesh 0x4fe700 gives every triangle its own three vertices (triangles become
        // 0..n-1 in order), keeps per-corner normals or, with keepNormals = false, the normalized corner sum,
        // averages the three corner colors (a mesh with no color array is treated as all white), and copies uv
        // and tangents where the old arrays cover the corner.
        [Test]
        public void BreakMeshSplitsEveryTriangleAndAveragesColors()
        {
            Mesh mesh = QuadMesh();
            try
            {
                // BreakMesh reads get_tangents (0x4fe88b) and writes set_tangents (0x4ff454).
                Vector4 t0 = new Vector4(1f, 0f, 0f, 1f);
                Vector4 t1 = new Vector4(0f, 1f, 0f, 1f);
                Vector4 t2 = new Vector4(0f, 0f, 1f, -1f);
                Vector4 t3 = new Vector4(1f, 1f, 0f, -1f);
                mesh.tangents = new[] { t0, t1, t2, t3 };
                MeshUtility.BreakMesh(mesh);
                Assert.That(mesh.tangents, Is.EqualTo(new[] { t0, t1, t2, t0, t2, t3 }), "tangents follow the triangles {0,1,2, 0,2,3}");
                Assert.That(mesh.vertexCount, Is.EqualTo(6));
                NearAll(mesh.vertices, new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f) }, "vertices");
                NearAll(mesh.normals, new[] { new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f) }, "kept normals");
                Assert.That(mesh.uv, Is.EqualTo(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) }));
                Assert.That(mesh.colors, Is.All.EqualTo(Color.white), "no old colors means every corner is white");
                Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                Assert.That(mesh.GetTriangles(0), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }

            mesh = QuadMesh();
            try
            {
                mesh.colors = new[] { new Color(1f, 0f, 0f, 1f), new Color(0f, 1f, 0f, 1f), new Color(0f, 0f, 1f, 1f), new Color(1f, 1f, 1f, 1f) };
                MeshUtility.BreakMesh(mesh, false);
                float third = 1f / 3f;
                float twoThirds = 2f / 3f;
                NearAll(mesh.normals, new[]
                {
                    new Vector3(0.57735f, 0.57735f, 0.57735f), new Vector3(0.57735f, 0.57735f, 0.57735f), new Vector3(0.57735f, 0.57735f, 0.57735f),
                    new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 0f),
                }, "normalized corner sums");
                Color[] colors = mesh.colors;
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(colors[i].r, Is.EqualTo(third).Within(Tolerance), "triangle 0 red " + i);
                    Assert.That(colors[i].g, Is.EqualTo(third).Within(Tolerance));
                    Assert.That(colors[i].b, Is.EqualTo(third).Within(Tolerance));
                    Assert.That(colors[3 + i].r, Is.EqualTo(twoThirds).Within(Tolerance), "triangle 1 red " + i);
                    Assert.That(colors[3 + i].g, Is.EqualTo(third).Within(Tolerance));
                    Assert.That(colors[3 + i].b, Is.EqualTo(twoThirds).Within(Tolerance));
                    Assert.That(colors[3 + i].a, Is.EqualTo(1f).Within(Tolerance));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        // Original Copy(Mesh) 0x4fd7e0 copies name, vertices, normals, colors, uv..uv4, tangents, bone weights,
        // bind poses and triangles, sets the submesh count and then each submesh's triangles.
        [Test]
        public void CopyReproducesTheChannelsAndSubmeshesOfAUnityMesh()
        {
            Mesh source = QuadMesh();
            Mesh copy = null;
            try
            {
                source.name = "source mesh";
                source.colors = new[] { Color.red, Color.green, Color.blue, Color.white };
                source.uv2 = new[] { new Vector2(1f, 2f), new Vector2(3f, 4f), new Vector2(5f, 6f), new Vector2(7f, 8f) };
                source.tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) };
                source.subMeshCount = 2;
                source.SetTriangles(new[] { 0, 1, 2 }, 0);
                source.SetTriangles(new[] { 0, 2, 3 }, 1);

                copy = MeshUtility.Copy(source);
                Assert.That(copy, Is.Not.SameAs(source));
                Assert.That(copy.name, Is.EqualTo("source mesh"));
                Assert.That(copy.vertices, Is.EqualTo(source.vertices));
                Assert.That(copy.normals, Is.EqualTo(source.normals));
                Assert.That(copy.colors, Is.EqualTo(source.colors));
                Assert.That(copy.uv, Is.EqualTo(source.uv));
                Assert.That(copy.uv2, Is.EqualTo(source.uv2));
                Assert.That(copy.tangents, Is.EqualTo(source.tangents));
                Assert.That(copy.subMeshCount, Is.EqualTo(2));
                Assert.That(copy.GetTriangles(0), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(copy.GetTriangles(1), Is.EqualTo(new[] { 0, 2, 3 }));
                Assert.Throws<NullReferenceException>(() => MeshUtility.Copy(null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (copy != null)
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
            }
        }

        private static Shader AnyBuiltInShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Hidden/InternalErrorShader");
            }

            return shader;
        }

        // Original ToOBJString 0x4fcd70: "g <name>", then v/vn lines with the X component negated, vt, vt2 (for
        // uv2 AND uv3), vc, a blank line after each of the first five groups, then per submesh a blank line,
        // usemtl/usemap with the material name and reversed-winding face lines (1-based, "f c/c/c b/b/b a/a/a").
        // Commas from the current culture are replaced by dots on the way out; a null or destroyed mesh answers
        // "####Error####". The numbers are exact binary fractions so no runtime float formatting differs.
        [Test]
        public void ToOBJStringWritesTheNativeLayoutAndNormalizesTheDecimalSeparator()
        {
            Assert.That(MeshUtility.ToOBJString(null, null), Is.EqualTo("####Error####"));

            Mesh destroyed = new Mesh();
            UnityEngine.Object.DestroyImmediate(destroyed);
            Assert.That(MeshUtility.ToOBJString(destroyed, null), Is.EqualTo("####Error####"));

            Mesh mesh = new Mesh();
            Material material = new Material(AnyBuiltInShader());
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                mesh.name = "m";
                material.name = "mat";
                mesh.vertices = new[] { new Vector3(1f, 2f, 3f), new Vector3(2f, 0.5f, -4f), new Vector3(-1.5f, 0.25f, 2f) };
                mesh.normals = new[] { new Vector3(1f, 0f, 0f), new Vector3(0.5f, 0.25f, 1f), new Vector3(0.5f, 0.5f, 0.5f) };
                mesh.uv = new[] { new Vector2(0.5f, 0.25f), new Vector2(1f, 0.75f), new Vector2(0f, 1f) };
                mesh.uv2 = new[] { new Vector2(0.25f, 0.5f), new Vector2(0.75f, 1f), new Vector2(1f, 0f) };
                mesh.uv3 = new[] { new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 0f) };
                mesh.colors = new[] { new Color(1f, 0f, 1f, 1f), new Color(0f, 0f, 0f, 1f), new Color(1f, 1f, 1f, 0f) };
                mesh.triangles = new[] { 0, 1, 2 };

                CultureInfo commaCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                commaCulture.NumberFormat.NumberDecimalSeparator = ",";
                CultureInfo.CurrentCulture = commaCulture;
                string text = MeshUtility.ToOBJString(mesh, new[] { material });

                string expected =
                    "g m\n" +
                    "v -1 2 3\nv -2 0.5 -4\nv 1.5 0.25 2\n" +
                    "\n" +
                    "vn -1 0 0\nvn -0.5 0.25 1\nvn -0.5 0.5 0.5\n" +
                    "\n" +
                    "vt 0.5 0.25\nvt 1 0.75\nvt 0 1\n" +
                    "\n" +
                    "vt2 0.25 0.5\nvt2 0.75 1\nvt2 1 0\n" +
                    "\n" +
                    "vt2 1 0.5\nvt2 0.5 1\nvt2 0 0\n" +
                    "\n" +
                    "vc 1 0 1 1\nvc 0 0 0 1\nvc 1 1 1 0\n" +
                    "\nusemtl mat\nusemap mat\n" +
                    "f 3/3/3 2/2/2 1/1/1\n";
                Assert.That(text, Is.EqualTo(expected));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // Original TransformUtility 0x4ff600..0x4ff840: GetPosition is column 3, GetRotation is
        // LookRotation(column 2, column 1), GetScale is the length of columns 0..2 as Vector4s (so the
        // fourth row counts), SetPosition writes column 3 as (p, 1).
        [Test]
        public void MatrixAccessorsReadAndWriteTheTranslationRotationAndColumnLengths()
        {
            Quaternion rotation = Quaternion.Euler(10f, 20f, 30f);
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), rotation, new Vector3(2f, 3f, 4f));
            Assert.That(TransformUtility.GetPosition(matrix), Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Near(TransformUtility.GetScale(matrix), new Vector3(2f, 3f, 4f), "scale");
            Near(TransformUtility.GetRotation(matrix), rotation, "rotation");

            Matrix4x4 skewed = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(2f, 3f, 4f));
            skewed.m30 = 3f;
            Assert.That(TransformUtility.GetScale(skewed).x, Is.EqualTo(Mathf.Sqrt(13f)).Within(Tolerance), "column 0 length includes m30");
            Assert.That(TransformUtility.GetScale(skewed).y, Is.EqualTo(3f).Within(Tolerance));

            Matrix4x4 target = Matrix4x4.identity;
            target.m33 = 5f;
            Vector3 position = new Vector3(7f, 8f, 9f);
            TransformUtility.SetPosition(ref target, ref position);
            Assert.That(target.m03, Is.EqualTo(7f));
            Assert.That(target.m13, Is.EqualTo(8f));
            Assert.That(target.m23, Is.EqualTo(9f));
            Assert.That(target.m33, Is.EqualTo(1f), "w is written as 1");
        }

        private static GameObject MeshObject(string name, Transform parent, Vector3 localPosition, Mesh mesh)
        {
            GameObject owner = new GameObject(name);
            owner.transform.SetParent(parent, false);
            owner.transform.localPosition = localPosition;
            MeshFilter filter = owner.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            return owner;
        }

        private static Mesh CubeMesh(float halfExtent)
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-halfExtent, -halfExtent, -halfExtent), new Vector3(halfExtent, -halfExtent, -halfExtent),
                new Vector3(halfExtent, halfExtent, -halfExtent), new Vector3(-halfExtent, halfExtent, -halfExtent),
                new Vector3(-halfExtent, -halfExtent, halfExtent), new Vector3(halfExtent, -halfExtent, halfExtent),
                new Vector3(halfExtent, halfExtent, halfExtent), new Vector3(-halfExtent, halfExtent, halfExtent),
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 4, 4, 5, 6, 6, 7, 0 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // Original GetChildCount 0x4ff870 (every descendant, accumulated into the ref count) and IsParent
        // 0x5001b0 (walks the ancestors; a transform is not its own parent, and null is never an ancestor).
        [Test]
        public void HierarchyCountsAndAncestryFollowTheTransformTree()
        {
            GameObject root = new GameObject("root");
            try
            {
                Transform a = new GameObject("a").transform;
                a.SetParent(root.transform, false);
                Transform a1 = new GameObject("a1").transform;
                a1.SetParent(a, false);
                Transform b = new GameObject("b").transform;
                b.SetParent(root.transform, false);

                int count = 10;
                TransformUtility.GetChildCount(root.transform, ref count);
                Assert.That(count, Is.EqualTo(13), "adds the three descendants to the incoming value");
                count = 0;
                TransformUtility.GetChildCount(b, ref count);
                Assert.That(count, Is.EqualTo(0));

                Assert.That(TransformUtility.IsParent(a1, root.transform), Is.True);
                Assert.That(TransformUtility.IsParent(a1, a), Is.True);
                Assert.That(TransformUtility.IsParent(a, a1), Is.False);
                Assert.That(TransformUtility.IsParent(a, b), Is.False);
                Assert.That(TransformUtility.IsParent(a, a), Is.False, "a transform is not its own parent");
                Assert.That(TransformUtility.IsParent(root.transform, root.transform), Is.False);
                Assert.That(TransformUtility.IsParent(a1, null), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // Original MergeBoundsRecursively 0x4ffb50: for each child it recurses first (passing no name filter),
        // then encapsulates the child's mesh min/max corners, moved with TransformPoint and brought into
        // rootParent's space; a MeshFilter without a mesh logs "MESH FILTER <name> IS MISSING A MESH"; only the
        // first level honors nameToIgnore. The incoming bounds are expanded, so a zero bounds keeps the origin.
        [Test]
        public void MergeBoundsEncapsulatesChildMeshesAndFiltersNamesOnlyAtTheFirstLevel()
        {
            Mesh unit = CubeMesh(1f);
            Mesh small = CubeMesh(0.5f);
            GameObject root = new GameObject("root");
            try
            {
                GameObject a = MeshObject("a", root.transform, new Vector3(2f, 0f, 0f), unit);
                MeshObject("skip", a.transform, new Vector3(0f, 5f, 0f), small);
                MeshObject("b", root.transform, Vector3.zero, null);
                MeshObject("skip", root.transform, new Vector3(-20f, 0f, 0f), unit);

                LogAssert.Expect(LogType.Error, "MESH FILTER b IS MISSING A MESH");
                Bounds everything = new Bounds(Vector3.zero, Vector3.zero);
                root.transform.MergeBoundsRecursively(root.transform, ref everything);
                Assert.That(everything.min, Is.EqualTo(new Vector3(-21f, -1f, -1f)));
                Assert.That(everything.max, Is.EqualTo(new Vector3(3f, 5.5f, 1f)));

                LogAssert.Expect(LogType.Error, "MESH FILTER b IS MISSING A MESH");
                Bounds filtered = new Bounds(Vector3.zero, Vector3.zero);
                root.transform.MergeBoundsRecursively(root.transform, ref filtered, "skip");
                Assert.That(filtered.min, Is.EqualTo(new Vector3(0f, -1f, -1f)), "the first-level skip is left out");
                Assert.That(filtered.max, Is.EqualTo(new Vector3(3f, 5.5f, 1f)), "the nested object with the same name is still merged");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(unit);
                UnityEngine.Object.DestroyImmediate(small);
            }
        }

        // Original TS_Transform .ctor 0x504640 / Update 0x504700. The constructor takes the transform and runs
        // Update, which (with no set* flag raised) copies the live world and local values, the local scale and the
        // lossy scale into the snapshot. Field defaults: scale and lossy scale 1, everything else 0 (rotation is
        // (0, 0, 0, 0), not the identity). A null transform makes Update return at once.
        [Test]
        public void TransformSnapshotReadsTheLiveTransformAndDefaultsWithoutOne()
        {
            GameObject owner = new GameObject("snapshot");
            try
            {
                owner.transform.position = new Vector3(1f, 2f, 3f);
                owner.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                owner.transform.localScale = new Vector3(2f, 3f, 4f);
                TS_Transform snapshot = new TS_Transform(owner.transform);
                Assert.That(snapshot.transform, Is.SameAs(owner.transform));
                Assert.That(snapshot.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(snapshot.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Near(snapshot.rotation, Quaternion.Euler(0f, 90f, 0f), "rotation");
                Near(snapshot.localRotation, Quaternion.Euler(0f, 90f, 0f), "local rotation");
                Assert.That(snapshot.scale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Near(snapshot.lossyScale, new Vector3(2f, 3f, 4f), "lossy scale");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }

            TS_Transform detached = new TS_Transform(null);
            Assert.That(detached.transform, Is.Null);
            Assert.That(detached.position, Is.EqualTo(Vector3.zero));
            Assert.That(detached.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(detached.rotation, Is.EqualTo(new Quaternion(0f, 0f, 0f, 0f)));
            Assert.That(detached.localRotation, Is.EqualTo(new Quaternion(0f, 0f, 0f, 0f)));
            Assert.That(detached.scale, Is.EqualTo(Vector3.one));
            Assert.That(detached.lossyScale, Is.EqualTo(Vector3.one));
            Assert.DoesNotThrow(detached.Update);
            Assert.Throws<NullReferenceException>(() => detached.HasPositionChange());
        }

        // Original set_position 0x504260 / set_localPosition 0x504510 (and the rotation pair) raise their own
        // flag and clear the opposite one, and Update pushes only the raised side before clearing every flag.
        [Test]
        public void SettersChooseWhichSideUpdatePushesAndUpdateClearsTheFlags()
        {
            // The parent sits at 10 so the engine's world/local float round trip stays inside Tolerance (at 100 one ulp is 7.6e-6).
            GameObject parent = new GameObject("parent");
            GameObject owner = new GameObject("owner");
            try
            {
                parent.transform.position = new Vector3(10f, 0f, 0f);
                parent.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                owner.transform.SetParent(parent.transform, false);
                TS_Transform snapshot = new TS_Transform(owner.transform);

                snapshot.position = new Vector3(5f, 6f, 7f);
                snapshot.Update();
                Near(owner.transform.position, new Vector3(5f, 6f, 7f), "world position pushed");
                // After the push the native code goes straight to the scale test (0x5047ad -> 0x5047b2) without
                // reading the local position, so that side stays stale although the true local value is not zero.
                Assert.That(snapshot.localPosition, Is.EqualTo(Vector3.zero), "the local side is not refreshed after a world push");
                Assert.That(owner.transform.localPosition, Is.Not.EqualTo(Vector3.zero));

                owner.transform.position = new Vector3(9f, 9f, 9f);
                snapshot.Update();
                Near(snapshot.position, new Vector3(9f, 9f, 9f), "the flags were cleared, so Update refreshes instead of pushing");
                Near(owner.transform.position, new Vector3(9f, 9f, 9f), "and nothing is pushed back");

                snapshot.localPosition = new Vector3(2f, 0f, 0f);
                snapshot.Update();
                Near(owner.transform.localPosition, new Vector3(2f, 0f, 0f), "local position pushed");
                // Likewise 0x504818 -> 0x50481d: the world side stays at the earlier refresh.
                Near(snapshot.position, new Vector3(9f, 9f, 9f), "the world side is not refreshed after a local push");
                Near(owner.transform.position, new Vector3(10f, 0f, -2f), "the live world position moved");

                snapshot.position = new Vector3(1f, 1f, 1f);
                snapshot.localPosition = new Vector3(0f, 3f, 0f);
                snapshot.Update();
                Near(owner.transform.localPosition, new Vector3(0f, 3f, 0f), "the later local setter wins over the world setter");

                snapshot.localPosition = new Vector3(0f, 4f, 0f);
                snapshot.position = new Vector3(50f, 0f, 0f);
                snapshot.Update();
                Near(owner.transform.position, new Vector3(50f, 0f, 0f), "the later world setter wins over the local setter");

                snapshot.localRotation = Quaternion.identity;
                snapshot.Update();
                Near(owner.transform.rotation, parent.transform.rotation, "local identity follows the parent");
                snapshot.rotation = Quaternion.identity;
                snapshot.Update();
                Near(owner.transform.rotation, Quaternion.identity, "world rotation pushed");

                // The rotation pair behaves the same way (0x504a42 / 0x504ab2 jump to the flag clear at 0x504b72).
                snapshot.localRotation = Quaternion.Euler(0f, 45f, 0f);
                snapshot.Update();
                Near(owner.transform.rotation, Quaternion.Euler(0f, 135f, 0f), "local rotation pushed");
                Near(snapshot.rotation, Quaternion.identity, "the world rotation is not refreshed after a local push");
                snapshot.rotation = Quaternion.Euler(0f, 10f, 0f);
                snapshot.Update();
                Near(owner.transform.rotation, Quaternion.Euler(0f, 10f, 0f), "world rotation pushed again");
                Near(snapshot.localRotation, Quaternion.Euler(0f, 45f, 0f), "the local rotation is not refreshed after a world push");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        // Original set_lossyScale 0x504470 raises the SAME setScale flag as set_scale (0x504420 area); Update
        // then pushes the scale FIELDS (not the lossy ones) to localScale and re-reads lossyScale.
        [Test]
        public void LossyScaleSetterRaisesTheScaleFlagAndUpdatePushesTheLocalScaleFields()
        {
            GameObject owner = new GameObject("scale");
            try
            {
                owner.transform.localScale = new Vector3(4f, 4f, 4f);
                TS_Transform snapshot = new TS_Transform(owner.transform);
                snapshot.lossyScale = new Vector3(9f, 9f, 9f);
                owner.transform.localScale = new Vector3(7f, 7f, 7f);
                snapshot.Update();
                Assert.That(owner.transform.localScale, Is.EqualTo(new Vector3(4f, 4f, 4f)), "the stored local scale overwrites the external change");
                Near(snapshot.lossyScale, new Vector3(4f, 4f, 4f), "the lossy scale is re-read from the transform");

                snapshot.scale = new Vector3(2f, 3f, 4f);
                snapshot.Update();
                Assert.That(owner.transform.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Near(snapshot.lossyScale, new Vector3(2f, 3f, 4f), "lossy scale");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        // Original HasPositionChange 0x504bd0 / HasRotationChange 0x504c90 / HasScaleChange 0x504d80 compare the
        // stored components with the live ones for exact inequality (scale uses the lossy scale); HasChange 0x504b90
        // is their disjunction.
        [Test]
        public void ChangeDetectionComparesStoredValuesWithTheLiveTransform()
        {
            GameObject owner = new GameObject("changes");
            try
            {
                TS_Transform snapshot = new TS_Transform(owner.transform);
                Assert.That(snapshot.HasPositionChange(), Is.False);
                Assert.That(snapshot.HasRotationChange(), Is.False);
                Assert.That(snapshot.HasScaleChange(), Is.False);
                Assert.That(snapshot.HasChange(), Is.False);

                owner.transform.position = new Vector3(0f, 0f, 0.5f);
                Assert.That(snapshot.HasPositionChange(), Is.True, "z alone differs");
                Assert.That(snapshot.HasRotationChange(), Is.False);
                Assert.That(snapshot.HasScaleChange(), Is.False);
                Assert.That(snapshot.HasChange(), Is.True);
                snapshot.Update();
                Assert.That(snapshot.HasChange(), Is.False, "Update refreshes the stored values");

                owner.transform.rotation = Quaternion.Euler(0f, 0f, 30f);
                Assert.That(snapshot.HasRotationChange(), Is.True);
                Assert.That(snapshot.HasPositionChange(), Is.False);
                Assert.That(snapshot.HasChange(), Is.True);
                snapshot.Update();

                owner.transform.localScale = new Vector3(1f, 2f, 1f);
                Assert.That(snapshot.HasScaleChange(), Is.True);
                Assert.That(snapshot.HasChange(), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }

            // HasScaleChange reads lossyScaleX/Y/Z (+0x38..+0x40) against get_lossyScale (0x504dac..0x504e09), not
            // localScale: scaling only the parent changes the lossy scale while the owner's localScale stays put.
            GameObject parent = new GameObject("scaled parent");
            GameObject child = new GameObject("scaled child");
            try
            {
                child.transform.SetParent(parent.transform, false);
                TS_Transform snapshot = new TS_Transform(child.transform);
                Assert.That(snapshot.HasScaleChange(), Is.False);
                parent.transform.localScale = new Vector3(2f, 2f, 2f);
                Assert.That(child.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(snapshot.HasScaleChange(), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        // Original TransformPoint 0x504e40 = rotation * Scale(point, lossyScale) + position (scale, then rotate,
        // then translate); TransformDirection 0x504f50 = TransformPoint(direction) - position;
        // InverseTransformPoint 0x5050a0 = inverse(rotation) * (point - position) divided component-wise by the
        // lossy scale; InverseTransformDirection 0x5051b0 = inverse(rotation) * direction divided likewise.
        // A quarter turn about Y maps (x, y, z) to (z, y, -x), so scaling (1, 1, 1) by (2, 3, 4) first gives
        // (4, 3, -2); rotating first and scaling second would give (2, 3, -4).
        // The editor compile (EditMode) starts each of the four methods with `if (!isPlaying) return transform.X(...)`, where
        // isPlaying is the editor-only flag Update caches from Application.isPlaying; the shipped build compiles no such line.
        // DreamteckPlayState sets that cache to the value play mode gives it, so the shipped arithmetic above is what runs.
        [Test]
        public void PointAndDirectionTransformsScaleThenRotateThenTranslate()
        {
            TS_Transform snapshot = DreamteckPlayState.Cache(new TS_Transform(null));
            snapshot.position = new Vector3(1f, 0f, 0f);
            snapshot.rotation = Quaternion.Euler(0f, 90f, 0f);
            snapshot.lossyScale = new Vector3(2f, 3f, 4f);

            Near(snapshot.TransformPoint(Vector3.one), new Vector3(5f, 3f, -2f), "TransformPoint");
            Near(snapshot.TransformDirection(Vector3.one), new Vector3(4f, 3f, -2f), "TransformDirection");
            Near(snapshot.InverseTransformPoint(new Vector3(5f, 3f, -2f)), Vector3.one, "InverseTransformPoint");
            Near(snapshot.InverseTransformDirection(new Vector3(4f, 3f, -2f)), Vector3.one, "InverseTransformDirection");
        }

        // Original GetComponent<T> (shared generic 0x9d2160) forwards to the wrapped transform; SetTransform
        // 0x5046d0 assigns it, clears all five flags and runs Update, so a pending setter is dropped.
        [Test]
        public void GetComponentForwardsAndSetTransformDropsPendingSetters()
        {
            GameObject first = new GameObject("first");
            GameObject second = new GameObject("second");
            try
            {
                BoxCollider collider = first.AddComponent<BoxCollider>();
                first.transform.position = new Vector3(1f, 1f, 1f);
                second.transform.position = new Vector3(2f, 2f, 2f);
                TS_Transform snapshot = new TS_Transform(first.transform);
                Assert.That(snapshot.GetComponent<BoxCollider>(), Is.SameAs(collider));
                Assert.That(snapshot.GetComponent<MeshFilter>() == null, Is.True);
                Assert.Throws<NullReferenceException>(() => new TS_Transform(null).GetComponent<BoxCollider>());

                snapshot.position = new Vector3(9f, 9f, 9f);
                snapshot.SetTransform(second.transform);
                Assert.That(snapshot.transform, Is.SameAs(second.transform));
                Assert.That(snapshot.position, Is.EqualTo(new Vector3(2f, 2f, 2f)), "the pending world position is discarded");
                Assert.That(first.transform.position, Is.EqualTo(new Vector3(1f, 1f, 1f)), "and never pushed to the old transform");
                Assert.That(second.transform.position, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        // Original TS_Transform metadata: [Serializable]; five plain bool flags, then _transform and 20 float
        // fields in this order, each non-bool field with [SerializeField] and [HideInInspector]. The editor compile
        // (EditMode) declares one more field after lrotW, `private volatile bool isPlaying` under UNITY_EDITOR (the
        // editor-vs-DummyDll declaration diff lists exactly this addition for TS_Transform); the shipped type has none, and
        // the player-define compile reproduces the shipped list.
        [Test]
        public void TransformSnapshotKeepsItsSerializedFieldLayout()
        {
            Assert.That(typeof(TS_Transform).IsSerializable, Is.True);
            FieldInfo[] fields = typeof(TS_Transform).GetFields(Own);
            string[] expected =
            {
                "setPosition", "setRotation", "setScale", "setLocalPosition", "setLocalRotation", "_transform",
                "posX", "posY", "posZ", "scaleX", "scaleY", "scaleZ", "lossyScaleX", "lossyScaleY", "lossyScaleZ",
                "rotX", "rotY", "rotZ", "rotW", "lposX", "lposY", "lposZ", "lrotX", "lrotY", "lrotZ", "lrotW",
                "isPlaying", // editor-only, declared last in the original text
            };
            Assert.That(Array.ConvertAll(fields, field => field.Name), Is.EqualTo(expected));
            foreach (FieldInfo field in fields)
            {
                bool serialized = field.GetCustomAttribute<SerializeField>() != null && field.GetCustomAttribute<HideInInspector>() != null;
                Assert.That(serialized, Is.EqualTo(field.FieldType != typeof(bool)), field.Name + " attributes");
            }
        }

        // Declaration-emission check, inferred from barrier evidence (for example 0x504225 in get_position): every
        // float access is bracketed by memory barriers, the bools and the transform are not. DummyDll carries no
        // volatile modreq, so this pins the inference only. The one bool that is volatile is the editor-only isPlaying cache,
        // which the original text declares `private volatile bool isPlaying` under UNITY_EDITOR; the shipped type has no such
        // field, so every shipped bool (and the transform) stays non-volatile.
        [Test]
        public void TransformSnapshotFloatsAreEmittedVolatileFromBarrierEvidence()
        {
            foreach (FieldInfo field in typeof(TS_Transform).GetFields(Own))
            {
                bool volatileField = Array.IndexOf(field.GetRequiredCustomModifiers(), typeof(IsVolatile)) >= 0;
                bool expectedVolatile = field.FieldType == typeof(float) || field.Name == "isPlaying";
                Assert.That(volatileField, Is.EqualTo(expectedVolatile), field.Name + " volatility");
            }
        }
    }
}
