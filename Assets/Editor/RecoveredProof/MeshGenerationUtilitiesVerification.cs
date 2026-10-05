using System;
using System.Collections.Generic;
using System.Globalization;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    /// <summary>
    /// Execute with -executeMethod ProjectLucid.MeshGenerationUtilitiesVerification.Run.
    /// Expected geometry was transcribed from native stores and the verified metadata blob.
    /// This checks recovered source in Unity; it does not execute or QA the original game.
    /// </summary>
    public static class MeshGenerationUtilitiesVerification
    {
        public static void Run()
        {
            VerifyCase("offset square base", 2f, 4f, 3f, new[]
            {
                new Vector3(-1f, 2f, -2f), new Vector3(1f, 2f, -2f),
                new Vector3(1f, 4f, -2f), new Vector3(-1f, 4f, -2f),
                new Vector3(0f, 3f, 2f),
            }, new Vector3(0f, 3f, 0f), new Vector3(1f, 1f, 2f));

            VerifyCase("negative height offset", 4f, 6f, -2f, new[]
            {
                new Vector3(-2f, -4f, -3f), new Vector3(2f, -4f, -3f),
                new Vector3(2f, 0f, -3f), new Vector3(-2f, 0f, -3f),
                new Vector3(0f, -2f, 3f),
            }, new Vector3(0f, -2f, 0f), new Vector3(2f, 2f, 3f));

            VerifyCase("signed dimensions are preserved", -2f, -4f, 3f, new[]
            {
                new Vector3(1f, 4f, 2f), new Vector3(-1f, 4f, 2f),
                new Vector3(-1f, 2f, 2f), new Vector3(1f, 2f, 2f),
                new Vector3(0f, 3f, -2f),
            }, new Vector3(0f, 3f, 0f), new Vector3(1f, 1f, 2f));

            VerifyDegenerateCase();
            Debug.Log("Project Lucid source verification passed: MeshGenerationUtilities, 4 native-derived geometry cases. Original-game QA remains unverified.");
        }

        private static void VerifyCase(string label, float width, float length, float offset,
                                       Vector3[] expectedVertices, Vector3 expectedCenter, Vector3 expectedExtents)
        {
            Mesh mesh = MeshGenerationUtilities.CreateMeshPyramid(width, length, offset);
            try
            {
                Require(mesh != null, label + ": mesh is missing");
                Require(mesh.vertexCount == 5, label + ": vertex count differs");
                Require(mesh.triangles.Length == 18, label + ": triangle count differs");
                Require(mesh.uv.Length == 0, label + ": original utility does not generate UVs");
                Require(mesh.normals.Length == 5, label + ": normals were not recalculated");
                foreach (Vector3 normal in mesh.normals)
                    Require(Mathf.Abs(normal.magnitude - 1f) < 0.0001f, label + ": invalid normal");
                Require((mesh.bounds.center - expectedCenter).sqrMagnitude < 0.0000001f,
                        label + ": bounds center differs");
                Require((mesh.bounds.extents - expectedExtents).sqrMagnitude < 0.0000001f,
                        label + ": bounds extents differ");

                // Mesh.Optimize may reorder storage. Compare oriented geometric faces,
                // preserving winding while allowing triangle and vertex-array reorder.
                int[] expectedIndices = { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4, 2, 1, 0, 3, 2, 0 };
                List<string> actual = Faces(mesh.vertices, mesh.triangles);
                List<string> expected = Faces(expectedVertices, expectedIndices);
                Require(actual.Count == expected.Count, label + ": face count differs");
                for (int i = 0; i < expected.Count; i++)
                    Require(actual[i] == expected[i], label + ": face geometry or winding differs");
            }
            finally
            {
                if (mesh != null)
                    UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static void VerifyDegenerateCase()
        {
            // Native code has no validation/clamping: zero dimensions still create
            // the same five vertices and six triangles, rather than rejecting input.
            Mesh mesh = MeshGenerationUtilities.CreateMeshPyramid(0f, 0f, 2f);
            try
            {
                Require(mesh.vertexCount == 5 && mesh.triangles.Length == 18,
                        "zero dimensions unexpectedly changed topology");
                foreach (Vector3 vertex in mesh.vertices)
                    Require(vertex == new Vector3(0f, 2f, 0f), "zero-dimension vertex differs");
                Require(mesh.bounds.center == new Vector3(0f, 2f, 0f) && mesh.bounds.size == Vector3.zero,
                        "zero-dimension bounds differ");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static List<string> Faces(Vector3[] vertices, int[] indices)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                string a = Point(vertices[indices[i]]);
                string b = Point(vertices[indices[i + 1]]);
                string c = Point(vertices[indices[i + 2]]);
                string first = a + ";" + b + ";" + c;
                string second = b + ";" + c + ";" + a;
                string third = c + ";" + a + ";" + b;
                string smallest = String.CompareOrdinal(first, second) < 0 ? first : second;
                result.Add(String.CompareOrdinal(smallest, third) < 0 ? smallest : third);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static string Point(Vector3 point)
        {
            return Scalar(point.x) + "," + Scalar(point.y) + "," + Scalar(point.z);
        }

        private static string Scalar(float value)
        {
            return value == 0f ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("MeshGenerationUtilities source verification failed: " + message);
        }
    }
}
