using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineMesh.Channel.MeshDefinition with its Submesh and VertexGroup (x86-64 addresses in the 0x4bf6a0 /
    // 0x4c22c0 / 0x4c29a0 / 0x4c3750 / 0x4c4a50 .. 0x4c7300 ranges). The source meshes are the tetrahedron of SplineMeshFixture
    // (vertices (0,0,0) (1,0,0) (0,1,0) (0,0,1), normals up, triangles 0,2,1 0,1,3 1,2,3 0,3,2) and small meshes built per test;
    // expected values are hand arithmetic from the native bodies. TearDown destroys every mesh a test made.
    public sealed class OriginalDreamteckSplineMeshDefinitionTests
    {
        private static readonly int[] TetraTriangles = { 0, 2, 1, 0, 1, 3, 1, 2, 3, 0, 3, 2 };

        private readonly SplineMeshFixture _fixture = new SplineMeshFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        private SplineMesh.Channel.MeshDefinition NewDefinition()
        {
            return new SplineMesh.Channel.MeshDefinition(_fixture.Tetra());
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            SplineMeshFixture.Near(actual, expected, message);
        }

        private static void Near(Vector2 actual, Vector2 expected, string message)
        {
            SplineMeshFixture.Near(actual, expected, message);
        }

        // The field initialisers of the constructor 0x4c22c0 and a refresh without a mesh (Refresh 0x4c3750 empties every array and
        // answers true).
        [Test]
        public void DefinitionWithoutAMeshIsEmpty()
        {
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(null);
            Assert.That(definition.mesh == null, Is.True);
            Assert.That(definition.vertices, Is.Empty);
            Assert.That(definition.normals, Is.Empty);
            Assert.That(definition.tangents, Is.Empty);
            Assert.That(definition.colors, Is.Empty);
            Assert.That(definition.uv, Is.Empty);
            Assert.That(definition.uv2, Is.Empty);
            Assert.That(definition.uv3, Is.Empty);
            Assert.That(definition.uv4, Is.Empty);
            Assert.That(definition.triangles, Is.Empty);
            Assert.That(definition.subMeshes, Is.Empty);
            Assert.That(definition.vertexGroups, Is.Empty);
            Assert.That(definition.bounds.size, Is.EqualTo(Vector3.zero));
            Assert.That(definition.rotation, Is.EqualTo(Vector3.zero));
            Assert.That(definition.offset, Is.EqualTo(Vector3.zero));
            Assert.That(definition.scale, Is.EqualTo(Vector3.one));
            Assert.That(definition.uvScale, Is.EqualTo(Vector2.one));
            Assert.That(definition.uvOffset, Is.EqualTo(Vector2.zero));
            Assert.That(definition.uvRotation, Is.EqualTo(0f));
            Assert.That(definition.mirror, Is.EqualTo(SplineMesh.Channel.MeshDefinition.MirrorMethod.None));
            Assert.That(definition.spacing.front, Is.EqualTo(0f));
            Assert.That(definition.spacing.back, Is.EqualTo(0f));
            Assert.That(definition.vertexGroupingMargin, Is.EqualTo(0f));
            Assert.That(definition.removeInnerFaces, Is.False);
            Assert.That(definition.flipFaces, Is.False);
            Assert.That(definition.doubleSided, Is.False);

            definition.vertices = new[] { Vector3.one };
            Assert.That(definition.Refresh(), Is.True);
            Assert.That(definition.vertices, Is.Empty, "refreshing without a mesh empties the arrays again");
        }

        // Refresh 0x4c3750 reads the arrays of the mesh, fills white colours where the mesh has none, and groups the vertices by z.
        [Test]
        public void RefreshReadsTheMeshAndGroupsTheVertices()
        {
            Mesh tetra = _fixture.Tetra();
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(tetra);

            Assert.That(definition.mesh, Is.SameAs(tetra));
            Assert.That(definition.vertices.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Near(definition.vertices[i], tetra.vertices[i], "vertex " + i);
                Near(definition.normals[i], tetra.normals[i], "normal " + i);
                Near(definition.uv[i], tetra.uv[i], "uv " + i);
            }

            Assert.That(definition.triangles, Is.EqualTo(TetraTriangles));
            Assert.That(definition.tangents, Is.Empty);
            Assert.That(definition.colors, Is.EqualTo(new[] { Color.white, Color.white, Color.white, Color.white }), "the mesh has no colours");
            Assert.That(definition.subMeshes.Count, Is.EqualTo(1));
            Assert.That(definition.subMeshes[0].triangles, Is.EqualTo(TetraTriangles));

            Assert.That(definition.vertexGroups.Count, Is.EqualTo(2));
            Assert.That(definition.vertexGroups[0].value, Is.EqualTo(1f));
            Assert.That(definition.vertexGroups[0].percent, Is.EqualTo(1.0));
            Assert.That(definition.vertexGroups[0].ids, Is.EqualTo(new[] { 3 }));
            Assert.That(definition.vertexGroups[1].value, Is.EqualTo(0f));
            Assert.That(definition.vertexGroups[1].percent, Is.EqualTo(0.0));
            Assert.That(definition.vertexGroups[1].ids, Is.EqualTo(new[] { 0, 1, 2 }));

            Near(definition.bounds.min, Vector3.zero, "bounds min");
            Near(definition.bounds.max, Vector3.one, "bounds max");
            Near(definition.bounds.size, Vector3.one, "bounds size");
            Near(definition.bounds.center, new Vector3(0.5f, 0.5f, 0.5f), "bounds centre is min + extents");
        }

        // A mesh with colours keeps them; the submesh list follows the submesh count of the mesh: surplus submeshes are removed from the
        // front and missing ones appended.
        [Test]
        public void RefreshKeepsMeshColoursAndFollowsTheSubmeshCount()
        {
            Mesh tetra = _fixture.Tetra();
            tetra.colors = new[] { Color.red, Color.green, Color.blue, Color.black };
            tetra.subMeshCount = 2;
            tetra.SetTriangles(new[] { 0, 2, 1 }, 0);
            tetra.SetTriangles(new[] { 0, 1, 3, 1, 2, 3, 0, 3, 2 }, 1);
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(tetra);
            Assert.That(definition.colors, Is.EqualTo(new[] { Color.red, Color.green, Color.blue, Color.black }));
            Assert.That(definition.subMeshes.Count, Is.EqualTo(2));
            Assert.That(definition.subMeshes[0].triangles, Is.EqualTo(new[] { 0, 2, 1 }));
            Assert.That(definition.subMeshes[1].triangles, Is.EqualTo(new[] { 0, 1, 3, 1, 2, 3, 0, 3, 2 }));
            Assert.That(definition.triangles, Is.EqualTo(TetraTriangles), "the flat triangle list covers every submesh");

            SplineMesh.Channel.MeshDefinition.Submesh secondSubmesh = definition.subMeshes[1];
            definition.mesh = _fixture.Tetra();
            Assert.That(definition.subMeshes.Count, Is.EqualTo(1));
            Assert.That(definition.subMeshes[0], Is.SameAs(secondSubmesh), "the first entry is the one removed");
            Assert.That(definition.subMeshes[0].triangles, Is.EqualTo(TetraTriangles), "and the triangles are read again");

            SplineMesh.Channel.MeshDefinition.Submesh only = definition.subMeshes[0];
            definition.mesh = tetra;
            Assert.That(definition.subMeshes.Count, Is.EqualTo(2));
            Assert.That(definition.subMeshes[0], Is.SameAs(only), "missing submeshes are appended");
        }

        // Every setter skips a value equal to the stored one and otherwise refreshes the arrays from the mesh. The refresh is observed
        // through the triangle list, which a refresh replaces.
        [Test]
        public void SettersRefreshOnlyForChangedValues()
        {
            Mesh tetra = _fixture.Tetra();
            SplineMesh.Channel.MeshDefinition d = new SplineMesh.Channel.MeshDefinition(tetra);

            void Check(string name, Action equal, Action changed)
            {
                d.triangles = new[] { 7 };
                equal();
                Assert.That(d.triangles, Is.EqualTo(new[] { 7 }), name + ": an equal value does not refresh");
                changed();
                Assert.That(d.triangles, Is.Not.EqualTo(new[] { 7 }), name + ": a new value refreshes");
            }

            Check("mesh", () => d.mesh = tetra, () => d.mesh = _fixture.Tetra());
            Check("rotation", () => d.rotation = new Vector3(1e-6f, 0f, 0f), () => d.rotation = new Vector3(0f, 0f, 10f));
            Check("offset", () => d.offset = new Vector3(1e-6f, 0f, 0f), () => d.offset = new Vector3(1f, 0f, 0f));
            Check("scale", () => d.scale = new Vector3(1f + 1e-6f, 1f, 1f), () => d.scale = new Vector3(2f, 2f, 2f));
            Check("spacing", () => d.spacing = new SplineMesh.Channel.BoundsSpacing { front = 0f, back = 0f }, () => d.spacing = new SplineMesh.Channel.BoundsSpacing { front = 0f, back = 1f });
            Check("spacing front", () => d.spacing = new SplineMesh.Channel.BoundsSpacing { front = 0f, back = 1f }, () => d.spacing = new SplineMesh.Channel.BoundsSpacing { front = 2f, back = 1f });
            Check("uvScale", () => d.uvScale = new Vector2(1f + 1e-6f, 1f), () => d.uvScale = new Vector2(2f, 2f));
            Check("uvOffset", () => d.uvOffset = new Vector2(1e-6f, 0f), () => d.uvOffset = new Vector2(0.5f, 0f));
            Check("uvRotation", () => d.uvRotation = 0f, () => d.uvRotation = 15f);
            Check("vertexGroupingMargin", () => d.vertexGroupingMargin = 0f, () => d.vertexGroupingMargin = 0.5f);
            Check("mirror", () => d.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.None, () => d.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.X);
            Check("removeInnerFaces", () => d.removeInnerFaces = false, () => d.removeInnerFaces = true);
            Check("flipFaces", () => d.flipFaces = false, () => d.flipFaces = true);
            Check("doubleSided", () => d.doubleSided = false, () => d.doubleSided = true);

            Assert.That(d.offset, Is.EqualTo(new Vector3(1f, 0f, 0f)), "the stored value is the new one");
            Assert.That(d.scale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(d.spacing.front, Is.EqualTo(2f));
            Assert.That(d.spacing.back, Is.EqualTo(1f));
            Assert.That(d.mirror, Is.EqualTo(SplineMesh.Channel.MeshDefinition.MirrorMethod.X));
        }

        // CalculateBounds 0x4c5ca0 starts the box at the origin, so it always contains it, and widens z by the spacing (front above,
        // back below).
        [Test]
        public void BoundsContainTheOriginAndGrowByTheSpacing()
        {
            SplineMesh.Channel.MeshDefinition definition = NewDefinition();
            definition.offset = new Vector3(2f, 2f, 2f);
            Near(definition.bounds.min, Vector3.zero, "positive vertices: the box starts at the origin");
            Near(definition.bounds.max, new Vector3(3f, 3f, 3f), "max");
            Near(definition.bounds.size, new Vector3(3f, 3f, 3f), "size");
            Near(definition.bounds.center, new Vector3(1.5f, 1.5f, 1.5f), "centre");

            definition.spacing = new SplineMesh.Channel.BoundsSpacing { front = 2f, back = 1f };
            Near(definition.bounds.min, new Vector3(0f, 0f, -1f), "back spacing lowers min z");
            Near(definition.bounds.max, new Vector3(3f, 3f, 5f), "front spacing raises max z");
            Near(definition.bounds.size, new Vector3(3f, 3f, 6f), "size z");

            definition.spacing = new SplineMesh.Channel.BoundsSpacing();
            definition.offset = new Vector3(-2f, -2f, -2f);
            Near(definition.bounds.min, new Vector3(-2f, -2f, -2f), "negative vertices");
            Near(definition.bounds.max, Vector3.zero, "the origin caps the box from above");
        }

        // A box thinner than 0.002 on any axis warns with the mesh name.
        [Test]
        public void ThinBoundsLogAWarningWithTheMeshName()
        {
            Mesh flat = _fixture.Custom(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 });
            flat.name = "Flat";
            LogAssert.Expect(LogType.Warning, "The size of [Flat]'s bounds is too small! This could cause an issue if the [Auto Count] option is enabled!");
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(flat);
            Near(definition.bounds.size, new Vector3(1f, 1f, 0f), "flat along z");
        }

        // TransformVertices 0x4c5850: vertices go through T * R * S, normals through the inverse transpose (normalised), the uvs are
        // scaled, offset and then turned about z.
        [Test]
        public void TransformMovesRotatesAndScalesTheVerticesNormalsAndUvs()
        {
            SplineMesh.Channel.MeshDefinition definition = NewDefinition();
            definition.offset = new Vector3(1f, 2f, 3f);
            definition.rotation = new Vector3(0f, 0f, 90f);
            definition.scale = new Vector3(2f, 2f, 2f);
            definition.uvScale = new Vector2(2f, 3f);
            definition.uvOffset = new Vector2(0.5f, 0.25f);
            definition.uvRotation = 90f;

            Near(definition.vertices[0], new Vector3(1f, 2f, 3f), "origin");
            Near(definition.vertices[1], new Vector3(1f, 4f, 3f), "x axis scaled by 2 and turned onto y");
            Near(definition.vertices[2], new Vector3(-1f, 2f, 3f), "y axis scaled by 2 and turned onto -x");
            Near(definition.vertices[3], new Vector3(1f, 2f, 5f), "z axis scaled by 2");
            foreach (Vector3 normal in definition.normals)
            {
                Near(normal, new Vector3(-1f, 0f, 0f), "up scaled by 1/2, turned onto -x, normalised");
            }

            Near(definition.uv[0], new Vector2(-0.25f, 0.5f), "uv (0,0) -> (0.5,0.25) -> turned");
            Near(definition.uv[1], new Vector2(-0.25f, 2.5f), "uv (1,0) -> (2.5,0.25) -> turned");
            Near(definition.uv[2], new Vector2(-3.25f, 0.5f), "uv (0,1) -> (0.5,3.25) -> turned");
            Near(definition.uv[3], new Vector2(-1.75f, 1.5f), "uv (0.5,0.5) -> (1.5,1.75) -> turned");
            Near(definition.bounds.min, new Vector3(-1f, 0f, 0f), "bounds follow the transformed vertices");
            Near(definition.bounds.max, new Vector3(1f, 4f, 5f), "max");
        }

        // Mirror 0x4c4ec0 negates the axis on vertices and normals, turns every triangle (the first and last index are swapped) and
        // recalculates the tangents; without a mirror it leaves everything alone (including the tangents).
        [Test]
        public void MirrorNegatesTheAxisAndTurnsTheWinding()
        {
            SplineMesh.Channel.MeshDefinition definition = NewDefinition();
            Assert.That(definition.tangents, Is.Empty, "no mirror: no tangents are calculated");

            definition.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.X;
            Near(definition.vertices[1], new Vector3(-1f, 0f, 0f), "x mirrored");
            Near(definition.vertices[2], new Vector3(0f, 1f, 0f), "y kept");
            Near(definition.normals[0], new Vector3(0f, 1f, 0f), "normal x negated (zero stays zero)");
            Assert.That(definition.triangles, Is.EqualTo(new[] { 1, 2, 0, 3, 1, 0, 3, 2, 1, 2, 3, 0 }));
            Assert.That(definition.subMeshes[0].triangles, Is.EqualTo(new[] { 1, 2, 0, 3, 1, 0, 3, 2, 1, 2, 3, 0 }));
            Assert.That(definition.tangents.Length, Is.EqualTo(4));

            definition.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.Y;
            Near(definition.vertices[2], new Vector3(0f, -1f, 0f), "y mirrored");
            Near(definition.vertices[1], new Vector3(1f, 0f, 0f), "x kept: the arrays are read again from the mesh");
            Near(definition.normals[0], new Vector3(0f, -1f, 0f), "normal y negated");

            definition.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.Z;
            Near(definition.vertices[3], new Vector3(0f, 0f, -1f), "z mirrored");
            Near(definition.normals[0], new Vector3(0f, 1f, 0f), "normal z negated (zero stays zero)");
            Assert.That(definition.triangles, Is.EqualTo(new[] { 1, 2, 0, 3, 1, 0, 3, 2, 1, 2, 3, 0 }));
        }

        // CalculateTangents 0x4c6920 (Lengyel over the submesh triangles): after a mirror along x the single triangle (0,0,0) (1,0,0)
        // (0,1,0) with the uvs of its xy has the tangent (-1,0,0). The refresh multiplies the tangents by the normal matrix, which
        // drops the w to zero; calling the method itself leaves w = +1 (the bitangent turns along +y).
        [Test]
        public void MirrorTangentsFollowTheUvDirection()
        {
            Mesh triangle = _fixture.Custom(new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 });
            triangle.name = "Triangle";
            triangle.normals = new[] { new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, -1f) };
            triangle.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) };
            string warning = "The size of [Triangle]'s bounds is too small! This could cause an issue if the [Auto Count] option is enabled!";
            LogAssert.Expect(LogType.Warning, warning);
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(triangle);
            LogAssert.Expect(LogType.Warning, warning);
            definition.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.X;

            Assert.That(definition.triangles, Is.EqualTo(new[] { 2, 1, 0 }));
            Assert.That(definition.tangents.Length, Is.EqualTo(3));
            foreach (Vector4 tangent in definition.tangents)
            {
                Assert.That(tangent.x, Is.EqualTo(-1f).Within(1e-4f));
                Assert.That(tangent.y, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(tangent.z, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(tangent.w, Is.EqualTo(0f), "the matrix multiply of the refresh zeroes w");
            }

            DreamteckGeneratorFixture.Invoke(definition, "CalculateTangents");
            foreach (Vector4 tangent in definition.tangents)
            {
                Assert.That(tangent.x, Is.EqualTo(-1f).Within(1e-4f));
                Assert.That(tangent.y, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(tangent.z, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(tangent.w, Is.EqualTo(1f));
            }
        }

        // CalculateTangents without vertices only empties the tangents, even when triangles are present.
        [Test]
        public void CalculateTangentsOfAnEmptyDefinitionOnlyEmptiesTheTangents()
        {
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(null);
            definition.tangents = new[] { Vector4.one };
            definition.subMeshes.Add(new SplineMesh.Channel.MeshDefinition.Submesh(new[] { 0, 1, 2 }));
            Assert.That(() => DreamteckGeneratorFixture.Invoke(definition, "CalculateTangents"), Throws.Nothing);
            Assert.That(definition.tangents, Is.Empty);
        }

        // DoubleSided 0x4c5300 doubles the arrays through MeshUtility.MakeDoublesided (second half: negated normals, reversed winding);
        // FlipFaces 0x4c5690 reverses the winding and negates the normals in place. Double sided wins when both are set.
        [Test]
        public void DoubleSidedDoublesTheMeshAndFlipFacesReversesIt()
        {
            SplineMesh.Channel.MeshDefinition definition = NewDefinition();
            definition.doubleSided = true;
            Assert.That(definition.vertices.Length, Is.EqualTo(8));
            Assert.That(definition.normals.Length, Is.EqualTo(8));
            Assert.That(definition.uv.Length, Is.EqualTo(8));
            Assert.That(definition.colors.Length, Is.EqualTo(8));
            Near(definition.vertices[5], definition.vertices[1], "the second half repeats the vertices");
            Near(definition.normals[1], Vector3.up, "front normal");
            Near(definition.normals[5], Vector3.down, "back normal");
            Assert.That(definition.triangles.Length, Is.EqualTo(24));
            Assert.That(definition.triangles.Skip(12).Take(3).ToArray(), Is.EqualTo(new[] { 5, 6, 4 }), "triangle 0,2,1 reversed onto the back vertices");
            Assert.That(definition.subMeshes[0].triangles, Is.EqualTo(definition.triangles));

            definition.flipFaces = true;
            Assert.That(definition.vertices.Length, Is.EqualTo(8), "both set: double sided only");
            Assert.That(definition.triangles.Take(12).ToArray(), Is.EqualTo(TetraTriangles));

            definition.doubleSided = false;
            Assert.That(definition.vertices.Length, Is.EqualTo(4));
            Assert.That(definition.triangles, Is.EqualTo(new[] { 1, 2, 0, 3, 1, 0, 3, 2, 1, 2, 3, 0 }));
            Assert.That(definition.subMeshes[0].triangles, Is.EqualTo(new[] { 1, 2, 0, 3, 1, 0, 3, 2, 1, 2, 3, 0 }));
            foreach (Vector3 normal in definition.normals)
            {
                Near(normal, Vector3.down, "flipped normal");
            }
        }

        // RemoveInnerFaces 0x4c5dc0: a triangle whose three vertices all lie on the highest z or all on the lowest z is dropped from
        // the submesh. The highest z starts at zero, so a mesh entirely below zero keeps the triangles on its top.
        [Test]
        public void RemoveInnerFacesDropsTheCapsOnTheExtremeZ()
        {
            Mesh prism = _fixture.Custom(
                new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
                    new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(0f, 1f, 1f)
                },
                new[] { 0, 2, 1, 3, 4, 5, 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4, 2, 0, 3, 2, 3, 5 });
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(prism);
            Assert.That(definition.subMeshes[0].triangles.Length, Is.EqualTo(24));
            definition.removeInnerFaces = true;
            Assert.That(definition.subMeshes[0].triangles, Is.EqualTo(new[] { 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4, 2, 0, 3, 2, 3, 5 }), "only the six side triangles remain");
            Assert.That(definition.triangles.Length, Is.EqualTo(24), "the flat triangle list is not touched");

            definition.offset = new Vector3(0f, 0f, -3f);
            Assert.That(definition.subMeshes[0].triangles.Length, Is.EqualTo(21), "the bottom cap goes, the top cap at z -2 is not on the highest z (zero)");
            Assert.That(definition.subMeshes[0].triangles.Take(3).ToArray(), Is.EqualTo(new[] { 3, 4, 5 }));

            definition.removeInnerFaces = false;
            Assert.That(definition.subMeshes[0].triangles.Length, Is.EqualTo(24));
        }

        // GroupVertices 0x4c63f0 keeps the groups sorted by descending z; a vertex within the grouping margin of the group the search lands
        // on joins it, otherwise it starts a group before or after it.
        [Test]
        public void VerticesGroupByZWithinTheMargin()
        {
            Mesh stack = _fixture.Custom(
                new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0.4f), new Vector3(0f, 1f, 1f), new Vector3(1f, 1f, 1.05f) },
                new[] { 0, 1, 2, 1, 2, 3 });
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(stack);
            Assert.That(definition.vertexGroups.Select(g => g.value).ToArray(), Is.EqualTo(new[] { 1.05f, 1f, 0.4f, 0f }), "margin 0: one group per z");

            definition.vertexGroupingMargin = 0.1f;
            Assert.That(definition.vertexGroups.Select(g => g.value).ToArray(), Is.EqualTo(new[] { 1f, 0.4f, 0f }));
            Assert.That(definition.vertexGroups[0].ids, Is.EqualTo(new[] { 2, 3 }), "1.05 is within 0.1 of 1");
            Assert.That(definition.vertexGroups[1].ids, Is.EqualTo(new[] { 1 }));
            Assert.That(definition.vertexGroups[2].ids, Is.EqualTo(new[] { 0 }));
            Assert.That(definition.vertexGroups[0].percent, Is.EqualTo(1.0 / 1.05).Within(1e-5), "percent of the first vertex in the box 0..1.05");
            Assert.That(definition.vertexGroups[1].percent, Is.EqualTo(0.4 / 1.05).Within(1e-5));
            Assert.That(definition.vertexGroups[2].percent, Is.EqualTo(0.0));

            definition.vertexGroupingMargin = 0.4f;
            Assert.That(definition.vertexGroups.Select(g => g.value).ToArray(), Is.EqualTo(new[] { 1f, 0f }), "0.4 is exactly the margin away from 0 (approximately equal) and joins it");
            Assert.That(definition.vertexGroups[0].ids, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(definition.vertexGroups[1].ids, Is.EqualTo(new[] { 0, 1 }));
        }

        // FindInsertIndex 0x4c7020: binary search of the descending groups; an equal value answers its own index, otherwise the index where
        // the value would sit.
        [Test]
        public void FindInsertIndexSearchesTheDescendingGroups()
        {
            SplineMesh.Channel.MeshDefinition definition = new SplineMesh.Channel.MeshDefinition(null);
            Func<float, int> find = value => (int)DreamteckGeneratorFixture.Invoke(definition, "FindInsertIndex", Vector3.zero, value);
            Assert.That(find(0.5f), Is.EqualTo(0), "no groups");

            definition.vertexGroups.Add(new SplineMesh.Channel.MeshDefinition.VertexGroup(1f, 1.0, new[] { 0 }));
            definition.vertexGroups.Add(new SplineMesh.Channel.MeshDefinition.VertexGroup(0.4f, 0.4, new[] { 1 }));
            definition.vertexGroups.Add(new SplineMesh.Channel.MeshDefinition.VertexGroup(0f, 0.0, new[] { 2 }));
            Assert.That(find(1f), Is.EqualTo(0));
            Assert.That(find(0.4f), Is.EqualTo(1));
            Assert.That(find(0f), Is.EqualTo(2));
            Assert.That(find(2f), Is.EqualTo(0), "above the first");
            Assert.That(find(0.5f), Is.EqualTo(1), "between the first and the second");
            Assert.That(find(0.2f), Is.EqualTo(2), "between the second and the third");
            Assert.That(find(-1f), Is.EqualTo(3), "below the last");
        }

        // Write 0x4bf6a0 sizes the target arrays like its own (arrays that already fit are kept) and copies into them.
        [Test]
        public void WriteCopiesTheArraysAndKeepsFittingTargetArrays()
        {
            SplineMesh.Channel.MeshDefinition definition = NewDefinition();
            TS_Mesh target = new TS_Mesh();
            definition.Write(target);

            Assert.That(target.vertices, Is.EqualTo(definition.vertices));
            Assert.That(target.vertices, Is.Not.SameAs(definition.vertices));
            Assert.That(target.normals, Is.EqualTo(definition.normals));
            Assert.That(target.colors, Is.EqualTo(definition.colors));
            Assert.That(target.uv, Is.EqualTo(definition.uv));
            Assert.That(target.uv2, Is.Empty);
            Assert.That(target.uv3, Is.Empty);
            Assert.That(target.uv4, Is.Empty);
            Assert.That(target.tangents, Is.Empty);
            Assert.That(target.triangles, Is.EqualTo(TetraTriangles));
            Assert.That(target.subMeshes.Count, Is.EqualTo(1));
            Assert.That(target.subMeshes[0], Is.EqualTo(TetraTriangles));
            Assert.That(target.subMeshes[0], Is.Not.SameAs(definition.subMeshes[0].triangles));

            Vector3[] keptVertices = target.vertices;
            int[] keptTriangles = target.triangles;
            definition.vertices[0] = new Vector3(5f, 5f, 5f);
            definition.Write(target);
            Assert.That(target.vertices, Is.SameAs(keptVertices), "an array of the right length is reused");
            Assert.That(target.triangles, Is.SameAs(keptTriangles));
            Near(target.vertices[0], new Vector3(5f, 5f, 5f), "and rewritten");

            target.subMeshes = null;
            definition.Write(target);
            Assert.That(target.subMeshes, Is.Not.Null, "a missing submesh list is created");
            Assert.That(target.subMeshes.Count, Is.EqualTo(1));
        }

        // Write with a material id: the submeshes are cut or padded to id + 1 (surplus ones are removed from the front); the triangles go
        // into that submesh and the other submeshes are emptied. Without an id the submesh list follows the definition.
        [Test]
        public void WriteWithAMaterialIdFillsOneSubmesh()
        {
            SplineMesh.Channel.MeshDefinition definition = NewDefinition();
            TS_Mesh target = new TS_Mesh();
            definition.Write(target, 2);
            Assert.That(target.subMeshes.Count, Is.EqualTo(3));
            Assert.That(target.subMeshes[0], Is.Empty);
            Assert.That(target.subMeshes[1], Is.Empty);
            Assert.That(target.subMeshes[2], Is.EqualTo(TetraTriangles));
            Assert.That(target.triangles, Is.EqualTo(TetraTriangles), "the flat list is written as usual");

            // Count equals id + 1: nothing is removed, a filled submesh other than the target is emptied.
            target.subMeshes.RemoveAt(0);
            int[] dirty = { 1, 2, 3 };
            target.subMeshes[0] = dirty;
            definition.Write(target, 1);
            Assert.That(target.subMeshes.Count, Is.EqualTo(2));
            Assert.That(target.subMeshes[0], Is.Empty, "the other submesh is emptied");
            Assert.That(target.subMeshes[0], Is.Not.SameAs(dirty));
            Assert.That(target.subMeshes[1], Is.EqualTo(TetraTriangles));

            // Surplus submeshes go from the front: the last one (of the right length) is the one that stays.
            int[] last = new int[12];
            target.subMeshes = new List<int[]> { new int[1], new int[2], last };
            definition.Write(target, 0);
            Assert.That(target.subMeshes.Count, Is.EqualTo(1));
            Assert.That(target.subMeshes[0], Is.SameAs(last));
            Assert.That(target.subMeshes[0], Is.EqualTo(TetraTriangles));

            target.subMeshes[0] = new[] { 9 };
            definition.Write(target);
            Assert.That(target.subMeshes.Count, Is.EqualTo(1), "a negative id copies the submeshes of the definition");
            Assert.That(target.subMeshes[0], Is.EqualTo(TetraTriangles));

            definition.Write(target, 3);
            definition.Write(target);
            Assert.That(target.subMeshes.Count, Is.EqualTo(1), "surplus submeshes are removed");
            Assert.That(target.subMeshes[0], Is.EqualTo(TetraTriangles));
        }

        // Copy 0x4c29a0 builds the definition again from the mesh and then replaces the arrays by copies of the transformed ones; the
        // bounds are a TS_Bounds built from min and max as centre and size, and the spacing, the grouping margin and the inner face
        // removal are not copied.
        [Test]
        public void CopyKeepsTheArraysAndSomeSettings()
        {
            SplineMesh.Channel.MeshDefinition source = NewDefinition();
            source.offset = new Vector3(1f, 2f, 3f);
            source.scale = new Vector3(2f, 2f, 2f);
            source.rotation = new Vector3(0f, 90f, 0f);
            source.uvScale = new Vector2(2f, 3f);
            source.uvOffset = new Vector2(0.5f, 0.25f);
            source.uvRotation = 10f;
            source.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.Z;
            source.flipFaces = true;
            source.spacing = new SplineMesh.Channel.BoundsSpacing { front = 1f, back = 2f };
            source.vertexGroupingMargin = 0.25f;
            source.removeInnerFaces = true;

            SplineMesh.Channel.MeshDefinition copy = (SplineMesh.Channel.MeshDefinition)DreamteckGeneratorFixture.Invoke(source, "Copy");
            Assert.That(copy, Is.Not.SameAs(source));
            Assert.That(copy.mesh, Is.SameAs(source.mesh));
            Assert.That(copy.vertices, Is.EqualTo(source.vertices));
            Assert.That(copy.vertices, Is.Not.SameAs(source.vertices));
            Assert.That(copy.normals, Is.EqualTo(source.normals));
            Assert.That(copy.tangents, Is.EqualTo(source.tangents));
            Assert.That(copy.colors, Is.EqualTo(source.colors));
            Assert.That(copy.uv, Is.EqualTo(source.uv));
            Assert.That(copy.uv, Is.Not.SameAs(source.uv));
            Assert.That(copy.triangles, Is.EqualTo(source.triangles));
            Assert.That(copy.subMeshes.Count, Is.EqualTo(source.subMeshes.Count));
            Assert.That(copy.subMeshes[0].triangles, Is.EqualTo(source.subMeshes[0].triangles));
            Assert.That(copy.subMeshes[0], Is.Not.SameAs(source.subMeshes[0]));
            Assert.That(copy.subMeshes[0].triangles, Is.Not.SameAs(source.subMeshes[0].triangles));

            Assert.That(copy.mirror, Is.EqualTo(source.mirror));
            Assert.That(copy.offset, Is.EqualTo(source.offset));
            Assert.That(copy.rotation, Is.EqualTo(source.rotation));
            Assert.That(copy.scale, Is.EqualTo(source.scale));
            Assert.That(copy.uvOffset, Is.EqualTo(source.uvOffset));
            Assert.That(copy.uvScale, Is.EqualTo(source.uvScale));
            Assert.That(copy.uvRotation, Is.EqualTo(source.uvRotation));
            Assert.That(copy.flipFaces, Is.True);
            Assert.That(copy.doubleSided, Is.False);
            Assert.That(copy.spacing.front, Is.EqualTo(0f), "spacing is not copied");
            Assert.That(copy.spacing.back, Is.EqualTo(0f));
            Assert.That(copy.vertexGroupingMargin, Is.EqualTo(0f), "the grouping margin is not copied");
            Assert.That(copy.removeInnerFaces, Is.False, "the inner face removal is not copied");

            Near(copy.bounds.center, source.bounds.min, "the source min becomes the centre");
            Near(copy.bounds.size, source.bounds.max, "and the source max the size");
            Near(copy.bounds.extents, source.bounds.max * 0.5f, "extents are half the size");
            Assert.That(copy.vertexGroups.Select(g => g.value).ToArray(), Is.EqualTo(new[] { 1f, 0f }), "the vertex groups are those of the untransformed mesh");
        }

        // Submesh 0x4c7140/0x4c71a0 and VertexGroup 0x4c7240/0x4c72a0.
        [Test]
        public void SubmeshAndVertexGroupKeepTheirData()
        {
            Assert.That(new SplineMesh.Channel.MeshDefinition.Submesh().triangles, Is.Empty);
            int[] input = { 1, 2, 3 };
            SplineMesh.Channel.MeshDefinition.Submesh submesh = new SplineMesh.Channel.MeshDefinition.Submesh(input);
            Assert.That(submesh.triangles, Is.EqualTo(input));
            Assert.That(submesh.triangles, Is.Not.SameAs(input), "the input is copied");
            Assert.That(() => new SplineMesh.Channel.MeshDefinition.Submesh(null), Throws.TypeOf<NullReferenceException>());

            int[] ids = { 4 };
            SplineMesh.Channel.MeshDefinition.VertexGroup group = new SplineMesh.Channel.MeshDefinition.VertexGroup(2.5f, 0.75, ids);
            Assert.That(group.value, Is.EqualTo(2.5f));
            Assert.That(group.percent, Is.EqualTo(0.75));
            Assert.That(group.ids, Is.SameAs(ids), "the id array is kept as given");

            group.AddId(9);
            Assert.That(group.ids, Is.EqualTo(new[] { 4, 9 }));
            Assert.That(group.ids, Is.Not.SameAs(ids), "growing replaces the array");
            Assert.That(ids, Is.EqualTo(new[] { 4 }));
            group.AddId(7);
            Assert.That(group.ids, Is.EqualTo(new[] { 4, 9, 7 }));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type definition = typeof(SplineMesh.Channel.MeshDefinition);
            string[] fields =
            {
                "vertices", "normals", "tangents", "colors", "uv", "uv2", "uv3", "uv4", "triangles", "subMeshes", "bounds", "vertexGroups",
                "_mesh", "_rotation", "_offset", "_scale", "_uvScale", "_uvOffset", "_uvRotation", "_mirror", "_spacing",
                "_vertexGroupingMargin", "_removeInnerFaces", "_flipFaces", "_doubleSided"
            };
            Assert.That(definition.GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(fields));
            foreach (string name in fields)
            {
                FieldInfo field = definition.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
                Assert.That(field.IsPublic, Is.EqualTo(!name.StartsWith("_") || name == "_spacing"), name);
            }

            Assert.That(definition.IsSerializable, Is.True);
            Assert.That(definition.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EquivalentTo(new[] { "MirrorMethod", "Submesh", "VertexGroup" }));
            Assert.That(typeof(SplineMesh.Channel.MeshDefinition.Submesh).IsSerializable, Is.True);
            Assert.That(typeof(SplineMesh.Channel.MeshDefinition.Submesh).GetFields().Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "triangles" }));
            Assert.That(typeof(SplineMesh.Channel.MeshDefinition.VertexGroup).IsSerializable, Is.True);
            Assert.That(typeof(SplineMesh.Channel.MeshDefinition.VertexGroup).GetFields().Select(f => f.Name + ":" + f.FieldType.Name).ToArray(),
                Is.EqualTo(new[] { "value:Single", "percent:Double", "ids:Int32[]" }));

            Assert.That(definition.GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic).IsAssembly, Is.True, "internal");
            foreach (string name in new[] { "RemoveInnerFaces", "FlipFaces", "DoubleSided", "CalculateBounds", "Mirror", "TransformVertices", "GroupVertices", "FindInsertIndex", "CalculateTangents" })
            {
                Assert.That(definition.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).IsPrivate, Is.True, name);
            }

            ParameterInfo write = definition.GetMethod("Write").GetParameters()[1];
            Assert.That(write.HasDefaultValue && (int)write.DefaultValue == -1, Is.True, "forceMaterialId defaults to -1");
        }
    }
}
