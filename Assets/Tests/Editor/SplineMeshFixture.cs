using System.Collections.Generic;
using Dreamteck;
using Dreamteck.Splines;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Shared setup of the SplineMesh tests: the generator fixture of the other Dreamteck mesh users plus the source meshes and
    // spline computers the channels read. Dispose destroys everything the fixture made.
    //
    // The source mesh is a tetrahedron on the origin: vertices (0,0,0) (1,0,0) (0,1,0) (0,0,1), every normal (0,1,0), the uvs
    // (0,0) (1,0) (0,1) (0.5,0.5). Its bounds are (0,0,0)..(1,1,1), so the vertices group by z into the group {3} at z 1 (percent
    // 1) followed by the group {0,1,2} at z 0 (percent 0).
    //
    // On the Linear line (0,0,0) -> (10,0,0) every sample has forward (1,0,0), up (0,1,0) and right Cross(up, forward) =
    // (0,0,-1), so the rotation of a sample maps the local x axis to (0,0,-1), y to (0,1,0) and z to (1,0,0).
    internal sealed class SplineMeshFixture : System.IDisposable
    {
        public const float Tolerance = DreamteckGeneratorFixture.Tolerance;

        public readonly DreamteckGeneratorFixture Generators = new DreamteckGeneratorFixture();

        private readonly List<Mesh> _meshes = new List<Mesh>();

        public void Dispose()
        {
            Generators.Dispose();
            foreach (Mesh mesh in _meshes)
            {
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }
            }

            _meshes.Clear();
        }

        public Mesh Tetra()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Tetra";
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 0, 3, 2 };
            _meshes.Add(mesh);
            return mesh;
        }

        // A mesh from vertices and triangles only; the normals point up.
        public Mesh Custom(Vector3[] vertices, int[] triangles)
        {
            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.normals = normals;
            mesh.triangles = triangles;
            _meshes.Add(mesh);
            return mesh;
        }

        // The line (0,0,0) -> (10,0,0) whose points carry the size and the colour.
        public SplineComputer SizedLine(float size, Color color)
        {
            SplinePoint start = new SplinePoint(Vector3.zero);
            start.size = size;
            start.color = color;
            SplinePoint end = new SplinePoint(new Vector3(10f, 0f, 0f));
            end.size = size;
            end.color = color;
            return Generators.NewComputer(start, end);
        }

        // A generator that follows the computer and has built once, without the default channel the engine's Reset adds. It caches
        // the play-mode state (DreamteckPlayState): the editor compile of SplineUser.Rebuild otherwise builds at once, which would
        // run a build from every channel setter and break the queued-build counts of the shipped Rebuild path.
        public SplineMesh NewSplineMesh(SplineComputer computer)
        {
            SplineMesh generator = Generators.NewGenerator<SplineMesh>(computer);
            DreamteckPlayState.Cache(generator);
            while (generator.GetChannelCount() > 0)
            {
                generator.RemoveChannel(0);
            }

            return generator;
        }

        public SplineMesh NewSplineMesh()
        {
            return NewSplineMesh(Generators.NewLine());
        }

        // Adds the tetrahedron as a channel of the given type and copy count.
        public SplineMesh.Channel AddTetra(SplineMesh generator, SplineMesh.Channel.Type type, int count)
        {
            SplineMesh.Channel channel = generator.AddChannel(Tetra(), "tetra");
            channel.type = type;
            channel.count = count;
            return channel;
        }

        public static TS_Mesh Ts(SplineMesh generator)
        {
            return DreamteckGeneratorFixture.TsMeshOf(generator);
        }

        public static void Near(Vector3 actual, Vector3 expected, string message)
        {
            DreamteckGeneratorFixture.Near(actual, expected, message);
        }

        public static void Near(Vector2 actual, Vector2 expected, string message)
        {
            DreamteckGeneratorFixture.Near(actual, expected, message);
        }

        public static void Near(Color actual, Color expected, string message)
        {
            DreamteckGeneratorFixture.Near(actual, expected, message);
        }

        // Counts the builds the generator runs when it is updated.
        public sealed class BuildCounter
        {
            private readonly SplineMesh _generator;

            public int Builds { get; private set; }

            public BuildCounter(SplineMesh generator)
            {
                _generator = generator;
                _generator.onPostBuild += OnBuilt;
                Update();
            }

            private void OnBuilt()
            {
                Builds++;
            }

            // Runs the queued rebuild, if any, and answers how many builds that ran.
            public int Update()
            {
                int before = Builds;
                DreamteckGeneratorFixture.Update(_generator);
                return Builds - before;
            }
        }
    }
}
