using System;
using System.Collections.Generic;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped player branch of Dreamteck.Splines.ObjectBender, run in play mode: SplineUser.Rebuild only queues the build
    // (rebuild = getSamples = true) where the EditMode compile of the same text builds at once and leaves the flag false. The scene
    // is that of OriginalDreamteckObjectBenderTests: a straight spline along +X from (0,0,0) to (10,0,0) and the five vertex mesh
    // A(0,0,0) B(2,0,0) C(0,4,0) D(0,0,10) E(0,0,5). Objects and meshes are owned by the test and destroyed in TearDown.
    public sealed class OriginalDreamteckObjectBenderPlayModeTests
    {
        private const float Tolerance = 1e-4f;

        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly List<GameObject> _owned = new List<GameObject>();

        private readonly List<UnityEngine.Object> _ownedAssets = new List<UnityEngine.Object>();

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
            foreach (UnityEngine.Object asset in _ownedAssets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _ownedAssets.Clear();
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ", expected " + expected.ToString("F6") + ")");
        }

        private static T Read<T>(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, InstanceAny | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return (T)field.GetValue(target);
                }
            }

            Assert.Fail("no field " + name);
            return default(T);
        }

        // Linear spline from the origin to (10,0,0); both points carry the given size.
        private SplineComputer NewLine(float size = 1f, GameObject host = null)
        {
            SplineComputer computer = (host ?? NewObject("line")).AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            SplinePoint start = new SplinePoint(new Vector3(0f, 0f, 0f));
            SplinePoint end = new SplinePoint(new Vector3(10f, 0f, 0f));
            start.size = size;
            end.size = size;
            computer.SetPoints(new[] { start, end });
            computer.RebuildImmediate();
            return computer;
        }

        private Mesh NewMesh(Vector3 normal)
        {
            Mesh mesh = new Mesh();
            mesh.name = "bendSource";
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(0f, 4f, 0f),
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 5f)
            };
            mesh.normals = new[] { normal, normal, normal, normal, normal };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 };
            _ownedAssets.Add(mesh);
            return mesh;
        }

        // A bender on its own object with the five vertex mesh, attached to a fresh line and sampled, bend still off.
        private ObjectBender NewBender(out MeshFilter filter, out Mesh source, float lineSize = 1f)
        {
            GameObject root = NewObject("bender");
            filter = root.AddComponent<MeshFilter>();
            source = NewMesh(Vector3.forward);
            filter.sharedMesh = source;
            ObjectBender bender = root.AddComponent<ObjectBender>();
            bender.spline = NewLine(lineSize);
            bender.RebuildImmediate();
            return bender;
        }

        // Setters 0x4a0a10..0x4a10c0: every one is silent without a spline; with one, a changed value queues a rebuild (the user's
        // rebuild flag), the axis also re-reads the hierarchy, a custom vector counts as unchanged below squared distance 1e-10
        // but is still stored.
        [Test]
        public void SettersQueueARebuildOnlyWithASplineAndOnlyForAChange()
        {
            ObjectBender alone = NewObject("alone").AddComponent<ObjectBender>();
            alone.axis = ObjectBender.Axis.Y;
            alone.upMode = ObjectBender.NormalMode.Custom;
            alone.customNormal = Vector3.right;
            alone.forwardMode = ObjectBender.ForwardMode.Custom;
            alone.customForward = Vector3.left;
            Assert.That(Read<bool>(alone, "rebuild"), Is.False, "no spline, no rebuild");
            Assert.That(alone.bendProperties.Length, Is.EqualTo(0), "the axis setter needs a spline to re-read the hierarchy");
            Assert.That(alone.axis, Is.EqualTo(ObjectBender.Axis.Y));
            Assert.That(alone.upMode, Is.EqualTo(ObjectBender.NormalMode.Custom));
            Near(alone.customNormal, Vector3.right, "custom normal");
            Assert.That(alone.forwardMode, Is.EqualTo(ObjectBender.ForwardMode.Custom));
            Near(alone.customForward, Vector3.left, "custom forward");

            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            bender.RebuildImmediate();
            Assert.That(Read<bool>(bender, "rebuild"), Is.False);
            bender.axis = ObjectBender.Axis.Z;
            bender.upMode = ObjectBender.NormalMode.Auto;
            bender.forwardMode = ObjectBender.ForwardMode.Spline;
            bender.customNormal = Vector3.up;
            bender.customForward = Vector3.forward;
            Assert.That(Read<bool>(bender, "rebuild"), Is.False, "unchanged values queue nothing");
            Assert.That(bender.bendProperties.Length, Is.EqualTo(0));

            bender.axis = ObjectBender.Axis.X;
            Assert.That(Read<bool>(bender, "rebuild"), Is.True);
            Assert.That(bender.bendProperties.Length, Is.EqualTo(1), "the axis re-reads the hierarchy");
            bender.RebuildImmediate();
            bender.upMode = ObjectBender.NormalMode.Custom;
            Assert.That(Read<bool>(bender, "rebuild"), Is.True);
            bender.RebuildImmediate();
            bender.forwardMode = ObjectBender.ForwardMode.Custom;
            Assert.That(Read<bool>(bender, "rebuild"), Is.True);
            bender.RebuildImmediate();
            bender.customNormal = Vector3.back;
            Assert.That(Read<bool>(bender, "rebuild"), Is.True);
            bender.RebuildImmediate();
            bender.customForward = Vector3.down;
            Assert.That(Read<bool>(bender, "rebuild"), Is.True);

            bender.RebuildImmediate();
            Vector3 almost = Vector3.down + new Vector3(1e-6f, 0f, 0f);
            bender.customForward = almost;
            Assert.That(Read<bool>(bender, "rebuild"), Is.False, "within the Vector3 equality tolerance");
            Assert.That(bender.customForward.x, Is.EqualTo(almost.x), "still stored");
        }
    }
}
