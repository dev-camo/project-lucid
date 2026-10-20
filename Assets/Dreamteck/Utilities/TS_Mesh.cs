namespace Dreamteck
{
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    //Thread-safe mesh & bounds classes for working with threads.
    public class TS_Mesh
    {
        public int vertexCount
        {
            get { return vertices.Length; }
            set { }
        }
        public Vector3[] vertices = new Vector3[0];
        public Vector3[] normals = new Vector3[0];
        public Vector4[] tangents = new Vector4[0];
        public Color[] colors = new Color[0];
        public Vector2[] uv = new Vector2[0];
        public Vector2[] uv2 = new Vector2[0];
        public Vector2[] uv3 = new Vector2[0];
        public Vector2[] uv4 = new Vector2[0];
        public int[] triangles = new int[0];
        public List<int[]> subMeshes = new List<int[]>();
        public TS_Bounds bounds = new TS_Bounds(Vector3.zero, Vector3.zero);
        public UnityEngine.Rendering.IndexFormat indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;

        public volatile bool hasUpdate = false;

        private int[] _submeshTrisCount = new int[0];
        private int[] _submeshOffsets = new int[0];


        public TS_Mesh()
        {

        }

        public TS_Mesh(Mesh mesh)
        {
            CreateFromMesh(mesh);
        }

        public void Clear()
        {
            vertices = new Vector3[0];
            normals = new Vector3[0];
            tangents = new Vector4[0];
            colors = new Color[0];
            uv = new Vector2[0];
            uv2 = new Vector2[0];
            uv3 = new Vector2[0];
            uv4 = new Vector2[0];
            triangles = new int[0];
            subMeshes = new List<int[]>();
            bounds = new TS_Bounds(Vector3.zero, Vector3.zero);
        }

        public void CreateFromMesh(Mesh mesh)
        {
            vertices = mesh.vertices;
            normals = mesh.normals;
            tangents = mesh.tangents;
            colors = mesh.colors;
            uv = mesh.uv;
            uv2 = mesh.uv2;
            uv3 = mesh.uv3;
            uv4 = mesh.uv4;
            triangles = mesh.triangles;
            bounds = new TS_Bounds(mesh.bounds);
            indexFormat = mesh.indexFormat;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                subMeshes.Add(mesh.GetTriangles(i));
            }
        }

        /// <summary>
        /// Writes the combineMeshes array to the current TS_Mesh object and tries to not allocate memory
        /// </summary>
        /// <param name="combineMeshes"></param>
        public void Combine(List<TS_Mesh> combineMeshes)
        {
            int totalVertexCount = 0;
            int totalTrisCount = 0;
            int addedSubmeshCount = 0;

            for (int i = 0; i < combineMeshes.Count; i++)
            {
                totalVertexCount += combineMeshes[i].vertices.Length;
                totalTrisCount += combineMeshes[i].triangles.Length;
                if (combineMeshes[i].subMeshes.Count > addedSubmeshCount)
                {
                    addedSubmeshCount = combineMeshes[i].subMeshes.Count;
                }
            }

            if (_submeshTrisCount.Length != addedSubmeshCount)
            {
                _submeshTrisCount = new int[addedSubmeshCount];
            }
            else
            {
                for (int i = 0; i < _submeshTrisCount.Length; i++)
                {
                    _submeshTrisCount[i] = 0;
                }
            }


            for (int i = 0; i < combineMeshes.Count; i++)
            {
                for (int j = 0; j < combineMeshes[i].subMeshes.Count; j++)
                {
                    _submeshTrisCount[j] += combineMeshes[i].subMeshes[j].Length;
                }
            }

            if (vertices.Length != totalVertexCount) vertices = new Vector3[totalVertexCount];
            if (normals.Length != totalVertexCount) normals = new Vector3[totalVertexCount];
            if (uv.Length != totalVertexCount) uv = new Vector2[totalVertexCount];
            if (uv2.Length != totalVertexCount) uv2 = new Vector2[totalVertexCount];
            if (uv3.Length != totalVertexCount) uv3 = new Vector2[totalVertexCount];
            if (uv4.Length != totalVertexCount) uv4 = new Vector2[totalVertexCount];
            if (colors.Length != totalVertexCount) colors = new Color[totalVertexCount];
            if (tangents.Length != totalVertexCount) tangents = new Vector4[totalVertexCount];
            if (triangles.Length != totalTrisCount) triangles = new int[totalTrisCount];
            if (subMeshes.Count > addedSubmeshCount) subMeshes.Clear();

            int vertexOffset = 0;
            int trisOffset = 0;

            if(_submeshOffsets.Length != addedSubmeshCount)
            {
                _submeshOffsets = new int[addedSubmeshCount];
            } else
            {
                for (int i = 0; i < _submeshOffsets.Length; i++)
                {
                    _submeshOffsets[i] = 0;
                }
            }


            for (int i = 0; i < combineMeshes.Count; i++)
            {
                combineMeshes[i].vertices.CopyTo(vertices, vertexOffset);
                combineMeshes[i].normals.CopyTo(normals, vertexOffset);
                combineMeshes[i].uv.CopyTo(uv, vertexOffset);
                combineMeshes[i].uv2.CopyTo(uv2, vertexOffset);
                combineMeshes[i].uv3.CopyTo(uv3, vertexOffset);
                combineMeshes[i].uv4.CopyTo(uv4, vertexOffset);
                combineMeshes[i].colors.CopyTo(colors, vertexOffset);
                combineMeshes[i].tangents.CopyTo(tangents, vertexOffset);

                for (int t = 0; t < combineMeshes[i].triangles.Length; t++)
                {
                    int index = t + trisOffset;
                    triangles[index] = combineMeshes[i].triangles[t] + vertexOffset;
                }

                trisOffset += combineMeshes[i].triangles.Length;

                for (int j = 0; j < combineMeshes[i].subMeshes.Count; j++)
                {
                    if (j >= subMeshes.Count)
                    {
                        subMeshes.Add(new int[_submeshTrisCount[j]]);
                    }
                    else if (subMeshes[j].Length != _submeshTrisCount[j])
                    {
                        subMeshes[j] = new int[_submeshTrisCount[j]];
                    }
                    int[] submesh = combineMeshes[i].subMeshes[j];

                    for (int x = 0; x < submesh.Length; x++)
                    {
                        int index = _submeshOffsets[j] + x;
                        subMeshes[j][index] = submesh[x] + vertexOffset;
                    }
                    _submeshOffsets[j] += submesh.Length;
                }
                vertexOffset += combineMeshes[i].vertices.Length;
            }
        }

        /// <summary>
        /// Adds the provieded mesh list to the current mesh and allocates memory
        /// </summary>
        /// <param name="addedMeshes"></param>
        public void AddMeshes(List<TS_Mesh> addedMeshes)
        {
            int newVerts = 0;
            int newTris = 0;
            int submeshCount = 0;
            for (int i = 0; i < addedMeshes.Count; i++)
            {
                newVerts += addedMeshes[i].vertexCount;
                newTris += addedMeshes[i].triangles.Length;
                if (addedMeshes[i].subMeshes.Count > submeshCount)
                {
                    submeshCount = addedMeshes[i].subMeshes.Count;
                }
            }
            int[] submeshTrisCount = new int[submeshCount];
            int[] submeshOffsets = new int[submeshCount];
            for (int i = 0; i < addedMeshes.Count; i++)
            {
                for (int j = 0; j < addedMeshes[i].subMeshes.Count; j++)
                {
                    submeshTrisCount[j] += addedMeshes[i].subMeshes[j].Length;
                }
            }

            
            Vector3[] newVertices = new Vector3[vertices.Length + newVerts];
            Vector3[] newNormals = new Vector3[vertices.Length + newVerts];
            Vector2[] newUvs = new Vector2[vertices.Length + newVerts];
            Vector2[] newUvs2 = new Vector2[vertices.Length + newVerts];
            Vector2[] newUvs3 = new Vector2[vertices.Length + newVerts];
            Vector2[] newUvs4 = new Vector2[vertices.Length + newVerts];
            Color[] newColors = new Color[vertices.Length + newVerts];
            Vector4[] newTangents = new Vector4[tangents.Length + newVerts];
            int[] newTriangles = new int[triangles.Length + newTris];
            List<int[]> newSubmeshes = new List<int[]>();

            for (int i = 0; i < submeshTrisCount.Length; i++)
            {
                newSubmeshes.Add(new int[submeshTrisCount[i]]);
                if (i < subMeshes.Count)
                {
                    submeshTrisCount[i] = subMeshes[i].Length;
                }
                else
                {
                    submeshTrisCount[i] = 0;
                }
            }

            newVerts = vertexCount;
            newTris = triangles.Length;
            vertices.CopyTo(newVertices, 0);
            normals.CopyTo(newNormals, 0);
            uv.CopyTo(newUvs, 0);
            uv2.CopyTo(newUvs2, 0);
            uv3.CopyTo(newUvs3, 0);
            uv4.CopyTo(newUvs4, 0);
            colors.CopyTo(newColors, 0);
            tangents.CopyTo(newTangents, 0);
            triangles.CopyTo(newTriangles, 0);

            for (int i = 0; i < addedMeshes.Count; i++)
            {
                addedMeshes[i].vertices.CopyTo(newVertices, newVerts);
                addedMeshes[i].normals.CopyTo(newNormals, newVerts);
                addedMeshes[i].uv.CopyTo(newUvs, newVerts);
                addedMeshes[i].uv2.CopyTo(newUvs2, newVerts);
                addedMeshes[i].uv3.CopyTo(newUvs3, newVerts);
                addedMeshes[i].uv4.CopyTo(newUvs4, newVerts);
                addedMeshes[i].colors.CopyTo(newColors, newVerts);
                addedMeshes[i].tangents.CopyTo(newTangents, newVerts);

                for (int n = newTris; n < newTris + addedMeshes[i].triangles.Length; n++)
                {
                    newTriangles[n] = addedMeshes[i].triangles[n - newTris] + newVerts;
                }


                for (int n = 0; n < addedMeshes[i].subMeshes.Count; n++)
                {
                    for (int x = submeshTrisCount[n]; x < submeshTrisCount[n] + addedMeshes[i].subMeshes[n].Length; x++)
                    {
                        newSubmeshes[n][x] = addedMeshes[i].subMeshes[n][x - submeshTrisCount[n]] + newVerts;
                    }
                    submeshTrisCount[n] += addedMeshes[i].subMeshes[n].Length;
                }
                newTris += addedMeshes[i].triangles.Length;
                newVerts += addedMeshes[i].vertexCount;
            }

            vertices = newVertices;
            normals = newNormals;
            uv = newUvs;
            uv2 = newUvs2;
            uv3 = newUvs3;
            uv4 = newUvs4;
            colors = newColors;
            tangents = newTangents;
            triangles = newTriangles;
            subMeshes = newSubmeshes;
        }

        public static TS_Mesh Copy(TS_Mesh input)
        {
            TS_Mesh result = new TS_Mesh();
            result.vertices = new Vector3[input.vertices.Length];
            input.vertices.CopyTo(result.vertices, 0);
            result.normals = new Vector3[input.normals.Length];
            input.normals.CopyTo(result.normals, 0);
            result.uv = new Vector2[input.uv.Length];
            input.uv.CopyTo(result.uv, 0);
            result.uv2 = new Vector2[input.uv2.Length];
            input.uv2.CopyTo(result.uv2, 0);
            result.uv3 = new Vector2[input.uv3.Length];
            input.uv3.CopyTo(result.uv3, 0);
            result.uv4 = new Vector2[input.uv4.Length];
            input.uv4.CopyTo(result.uv4, 0);
            result.colors = new Color[input.colors.Length];
            input.colors.CopyTo(result.colors, 0);
            result.tangents = new Vector4[input.tangents.Length];
            input.tangents.CopyTo(result.tangents, 0);
            result.triangles = new int[input.triangles.Length];
            input.triangles.CopyTo(result.triangles, 0);
            result.subMeshes = new List<int[]>();
            for(int i = 0; i < input.subMeshes.Count; i++)
            {
                result.subMeshes.Add(new int[input.subMeshes[i].Length]);
                input.subMeshes[i].CopyTo(result.subMeshes[i], 0);
            }
            result.bounds = new TS_Bounds(input.bounds.center, input.bounds.size);
            result.indexFormat = input.indexFormat;
            return result;
        }

        public void Absorb(TS_Mesh input)
        {
            if (vertices.Length != input.vertexCount) vertices = new Vector3[input.vertexCount];
            if (normals.Length != input.normals.Length) normals = new Vector3[input.normals.Length];
            if (colors.Length != input.colors.Length) colors = new Color[input.colors.Length];
            if (uv.Length != input.uv.Length) uv = new Vector2[input.uv.Length];
            if (uv2.Length != input.uv2.Length) uv2 = new Vector2[input.uv2.Length];
            if (uv3.Length != input.uv3.Length) uv3 = new Vector2[input.uv3.Length];
            if (uv4.Length != input.uv4.Length) uv4 = new Vector2[input.uv4.Length];
            if (tangents.Length != input.tangents.Length) tangents = new Vector4[input.tangents.Length];
            if (triangles.Length != input.triangles.Length) triangles = new int[input.triangles.Length];

            input.vertices.CopyTo(vertices, 0);
            input.normals.CopyTo(normals, 0);
            input.colors.CopyTo(colors, 0);
            input.uv.CopyTo(uv, 0);
            input.uv2.CopyTo(uv2, 0);
            input.uv3.CopyTo(uv3, 0);
            input.uv4.CopyTo(uv4, 0);
            input.tangents.CopyTo(tangents, 0);
            input.triangles.CopyTo(triangles, 0);

            if (subMeshes.Count == input.subMeshes.Count)
            {
                for (int i = 0; i < subMeshes.Count; i++)
                {
                    if (input.subMeshes[i].Length != subMeshes[i].Length) subMeshes[i] = new int[input.subMeshes[i].Length];
                    input.subMeshes[i].CopyTo(subMeshes[i], 0);
                }
            }
            else
            {
                subMeshes = new List<int[]>();
                for (int i = 0; i < input.subMeshes.Count; i++)
                {
                    subMeshes.Add(new int[input.subMeshes[i].Length]);
                    input.subMeshes[i].CopyTo(subMeshes[i], 0);
                }
            }
            bounds = new TS_Bounds(input.bounds.center, input.bounds.size);
        }

        public void WriteMesh(ref Mesh input)
        {
            if (input == null) input = new Mesh();
            input.Clear();
            input.indexFormat = indexFormat;
            input.vertices = vertices;
            input.normals = normals;
            if (tangents.Length == vertices.Length) input.tangents = tangents;
            if (colors.Length == vertices.Length) input.colors = colors;
            if (uv.Length == vertices.Length) input.uv = uv;
            if (uv2.Length == vertices.Length) input.uv2 = uv2;
            if (uv3.Length == vertices.Length) input.uv3 = uv3;
            if (uv4.Length == vertices.Length) input.uv4 = uv4;
            input.triangles = triangles;
            if (subMeshes.Count > 0)
            {
                input.subMeshCount = subMeshes.Count;
                for (int i = 0; i < subMeshes.Count; i++)
                {
                    input.SetTriangles(subMeshes[i], i);
                }
            }
            input.RecalculateBounds();
            hasUpdate = false;
        }
    }
}
