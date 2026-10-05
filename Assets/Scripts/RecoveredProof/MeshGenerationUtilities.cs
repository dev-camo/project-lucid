using UnityEngine;

namespace HardlightProject
{
    /// <summary>
    /// Original Game.Runtime utility reconstructed from both complete ARM64 method bodies.
    /// The pyramid points along Z; width sets both dimensions of its square XY base.
    /// Native-derived geometry; original method identities are retained below.
    /// </summary>
    public static class MeshGenerationUtilities
    {
        // Game.Runtime.dll:HardlightProject.MeshGenerationUtilities:0x060039b9
        // Native symbol: MeshGenerationUtilities_CreateMeshPyramid_m5BCD670F96BDFFCE790064D658A543C03C4B55CD
        // ARM64 0x68d4e0..0x68d5e4: halve width/length, write five Vector3s,
        // initialize the 18-index array, then tail-call CreateMesh.
        public static Mesh CreateMeshPyramid(float width, float length, float heightOffset)
        {
            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;

            Vector3[] vertices =
            {
                new Vector3(-halfWidth, heightOffset - halfWidth, -halfLength),
                new Vector3(halfWidth, heightOffset - halfWidth, -halfLength),
                new Vector3(halfWidth, halfWidth + heightOffset, -halfLength),
                new Vector3(-halfWidth, halfWidth + heightOffset, -halfLength),
                new Vector3(0f, heightOffset, halfLength),
            };

            // Original metadata field token 0x04002d8b, index 11658.
            // SHA256 of these 72 little-endian bytes matches its compiler-generated name:
            // 124DBEEBD32D0CE760E293BF46E477BAE35E7F666457E80A8B7F77AEDBC3221F.
            int[] indices =
            {
                0, 1, 4,
                1, 2, 4,
                2, 3, 4,
                3, 0, 4,
                2, 1, 0,
                3, 2, 0,
            };

            return CreateMesh(vertices, indices);
        }

        // Game.Runtime.dll:HardlightProject.MeshGenerationUtilities:0x060039ba
        // Native symbol: MeshGenerationUtilities_CreateMesh_m8C404486ADCB7250CA935B9A1FEE693F3F124912
        // ARM64 0x68d5e4..0x68d690 calls these Mesh methods in this exact order.
        private static Mesh CreateMesh(Vector3[] vertices, int[] indices)
        {
            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();
            return mesh;
        }
    }
}
