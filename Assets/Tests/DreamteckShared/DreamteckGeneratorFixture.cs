using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Shared setup of the Dreamteck mesh generator tests (MeshGenerator, PathGenerator, SplineRenderer): spline computers that
    // hold a straight Linear line, generators on owned game objects and reflection access to the protected state the native
    // bodies read. Dispose destroys every game object and mesh the fixture made.
    public sealed class DreamteckGeneratorFixture : System.IDisposable
    {
        public const float Tolerance = 1e-4f;

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo UvsField = typeof(MeshGenerator).GetField("__uvs", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly List<GameObject> _owned = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Vector2 _uvsBaseline;
        private bool _uvsCaptured;

        // The static MeshGenerator.__uvs scratch value as found when this test first acquired an object. A new component does not
        // rerun the type initializer, so a test that reads the default asserts against this baseline and Dispose puts it back.
        public Vector2 UvsBaseline
        {
            get
            {
                CaptureUvs();
                return _uvsBaseline;
            }
        }

        // Registers a mesh the fixture must destroy even when its component has already been destroyed or has replaced it.
        public Mesh Track(Mesh mesh)
        {
            if (mesh != null && !_meshes.Contains(mesh))
            {
                _meshes.Add(mesh);
            }

            return mesh;
        }

        private void CaptureUvs()
        {
            if (!_uvsCaptured)
            {
                _uvsBaseline = (Vector2)UvsField.GetValue(null);
                _uvsCaptured = true;
            }
        }

        public void Dispose()
        {
            try
            {
                foreach (Mesh tracked in _meshes)
                {
                    if (tracked != null)
                    {
                        Object.DestroyImmediate(tracked);
                    }
                }

                _meshes.Clear();

                foreach (GameObject owned in _owned)
                {
                    if (owned == null)
                    {
                        continue;
                    }

                    foreach (MeshGenerator generator in owned.GetComponents<MeshGenerator>())
                    {
                        Mesh mesh = MeshOf(generator);
                        if (mesh != null)
                        {
                            Object.DestroyImmediate(mesh);
                        }
                    }

                    Object.DestroyImmediate(owned);
                }

                _owned.Clear();
            }
            finally
            {
                // The shared scratch value is handed back even when a destroy above throws.
                if (_uvsCaptured)
                {
                    UvsField.SetValue(null, _uvsBaseline);
                    _uvsCaptured = false;
                }
            }
        }

        public GameObject NewObject(string name)
        {
            CaptureUvs();
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        // A Linear spline through the points: one cached sample per point.
        public SplineComputer NewComputer(params SplinePoint[] points)
        {
            SplineComputer computer = NewObject("computer").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(points);
            computer.RebuildImmediate();
            return computer;
        }

        // (0,0,0) -> (10,0,0): two samples, forward (1,0,0), up (0,1,0), size 1, white.
        public SplineComputer NewLine()
        {
            return NewComputer(new SplinePoint(Vector3.zero), new SplinePoint(new Vector3(10f, 0f, 0f)));
        }

        // A generator exactly as the engine creates it, with the filter and the renderer a plain MeshGenerator does not require.
        public T NewBare<T>() where T : MeshGenerator
        {
            GameObject owner = NewObject("generator");
            owner.AddComponent<MeshFilter>();
            owner.AddComponent<MeshRenderer>();

            T generator = owner.AddComponent<T>();
            Track(MeshOf(generator));
            return generator;
        }

        // A generator that follows the computer and has built once. Tangents are off: the native tangent scratch arrays are
        // static and not the subject of these tests.
        public T NewGenerator<T>(SplineComputer computer) where T : MeshGenerator
        {
            T generator = NewBare<T>();
            generator.calculateTangents = false;
            generator.spline = computer;
            generator.RebuildImmediate();
            return generator;
        }

        public static TS_Mesh TsMeshOf(MeshGenerator generator)
        {
            return (TS_Mesh)typeof(MeshGenerator).GetProperty("_tsMesh", AnyInstance).GetValue(generator, null);
        }

        public static Mesh MeshOf(MeshGenerator generator)
        {
            return (Mesh)typeof(MeshGenerator).GetField("_mesh", AnyInstance).GetValue(generator);
        }

        public static T Field<T>(object target, string name)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return (T)field.GetValue(target);
                }
            }

            Assert.Fail("no field " + name);
            return default(T);
        }

        public static void SetField(object target, string name, object value)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
            }

            Assert.Fail("no field " + name);
        }

        public static object Invoke(object target, string name, params object[] arguments)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type
                    .GetMethods(AnyInstance | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == arguments.Length);
                if (method != null)
                {
                    return method.Invoke(target, arguments);
                }
            }

            Assert.Fail("no method " + name);
            return null;
        }

        // The runner of the queued rebuild and the late run (SplineUser.Update).
        public static void Update(SplineUser user)
        {
            Invoke(user, "Update");
        }

        public static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F5") + ", expected " + expected.ToString("F5") + ")");
        }

        public static void Near(Vector2 actual, Vector2 expected, string message)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F5") + ", expected " + expected.ToString("F5") + ")");
        }

        public static void Near(Color actual, Color expected, string message)
        {
            Assert.That(Mathf.Abs(actual.r - expected.r) + Mathf.Abs(actual.g - expected.g) + Mathf.Abs(actual.b - expected.b) + Mathf.Abs(actual.a - expected.a),
                Is.LessThan(Tolerance), message + " (actual " + actual + ", expected " + expected + ")");
        }
    }
}
