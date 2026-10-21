using System;
using System.Collections.Generic;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.ObjectBender and its BendProperty. Every scene uses a straight spline along +X from (0,0,0) to (10,0,0)
    // (forward (1,0,0), up (0,1,0), so right = Cross(up, forward) = (0,0,-1)) and the five vertex mesh
    // A(0,0,0) B(2,0,0) C(0,4,0) D(0,0,10) E(0,0,5). The bender's bounds always include the origin, so they come out as
    // min (0,0,0), max (2,4,10). Expected values are hand arithmetic from the native bodies cited per test (ObjectBender
    // 0x4a0a00..0x4a5930, BendProperty 0x4a5a60..0x4a5eb0). Objects and meshes are owned by the test and destroyed in TearDown.
    public sealed class OriginalDreamteckObjectBenderTests
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

        private static void Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, InstanceAny);
            Assert.That(method, Is.Not.Null, name);
            try
            {
                method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
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

        private SplineSample EvalResult(ObjectBender bender)
        {
            return Read<SplineSample>(bender, "evalResult");
        }

        // The three nested enums keep the shipped members (dump.cs).
        [Test]
        public void EnumsKeepTheShippedValues()
        {
            Assert.That(new[] { (int)ObjectBender.Axis.X, (int)ObjectBender.Axis.Y, (int)ObjectBender.Axis.Z }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(new[] { (int)ObjectBender.NormalMode.Spline, (int)ObjectBender.NormalMode.Auto, (int)ObjectBender.NormalMode.Custom }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(new[] { (int)ObjectBender.ForwardMode.Spline, (int)ObjectBender.ForwardMode.Custom }, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(Enum.GetNames(typeof(ObjectBender.Axis)), Is.EqualTo(new[] { "X", "Y", "Z" }));
        }

        // The constructor 0x4a5930: no properties, axis Z, normal mode Auto, forward mode Spline, custom up and forward, identity
        // bend rotation; bend off.
        [Test]
        public void FreshBenderKeepsTheShippedDefaults()
        {
            ObjectBender bender = NewObject("bender").AddComponent<ObjectBender>();
            Assert.That(bender.bend, Is.False);
            Assert.That(bender.bendProperties, Is.Not.Null);
            Assert.That(bender.bendProperties.Length, Is.EqualTo(0));
            Assert.That(bender.axis, Is.EqualTo(ObjectBender.Axis.Z));
            Assert.That(bender.upMode, Is.EqualTo(ObjectBender.NormalMode.Auto));
            Assert.That(bender.forwardMode, Is.EqualTo(ObjectBender.ForwardMode.Spline));
            Near(bender.customNormal, Vector3.up, "custom normal");
            Near(bender.customForward, Vector3.forward, "custom forward");
            Assert.That(Read<Quaternion>(bender, "bendRotation"), Is.EqualTo(Quaternion.identity));
        }

        // The BendProperty constructor 0x4a1b10 (field initialisers: enabled, rotation and scale on, mesh, spline and collider on,
        // collider update rate 0.2, empty arrays) captures the local position, scale and rotation and the parent's world
        // rotation (the object's own world rotation when it has no parent).
        [Test]
        public void BendPropertyCapturesTheLocalTransformAndTheParentRotation()
        {
            GameObject parent = NewObject("parent");
            parent.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            GameObject child = NewObject("child");
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(1f, 2f, 3f);
            child.transform.localScale = new Vector3(2f, 2f, 2f);
            child.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            ObjectBender.BendProperty property = new ObjectBender.BendProperty(child.transform);
            Assert.That(property.enabled, Is.True);
            Assert.That(property.applyRotation, Is.True);
            Assert.That(property.applyScale, Is.True);
            Assert.That(property.generateLightmapUVs, Is.False);
            Assert.That(property.bendMesh, Is.True);
            Assert.That(property.bendCollider, Is.True);
            Assert.That(property.bendSpline, Is.True);
            Assert.That(property.colliderUpdateRate, Is.EqualTo(0.2f));
            Assert.That(property.isParent, Is.False);
            Assert.That(property.isValid, Is.True);
            Assert.That(property.transform.transform, Is.SameAs(child.transform));
            Near(property.originalPosition, new Vector3(1f, 2f, 3f), "original position");
            Near(property.originalScale, new Vector3(2f, 2f, 2f), "original scale");
            Assert.That(Quaternion.Angle(property.originalRotation, Quaternion.Euler(0f, 0f, 45f)), Is.LessThan(1e-3f));
            Assert.That(Quaternion.Angle(property.parentRotation, parent.transform.rotation), Is.LessThan(1e-3f), "parent rotation");
            Assert.That(property.vertexPercents.Length + property.normals.Length + property.colliderVertexPercents.Length + property.colliderNormals.Length, Is.EqualTo(0));
            Assert.That(property.splinePointPercents.Length + property.primaryTangentPercents.Length + property.secondaryTangentPercents.Length, Is.EqualTo(0));
            Assert.That(property.filter == null && property.collider == null && property.splineComputer == null, Is.True);

            // a transform without a parent uses its own world rotation
            ObjectBender.BendProperty root = new ObjectBender.BendProperty(parent.transform, true);
            Assert.That(root.isParent, Is.True);
            Assert.That(Quaternion.Angle(root.parentRotation, parent.transform.rotation), Is.LessThan(1e-3f), "root rotation");
        }

        // 0x4a1b10: the renderer normals are rotated into world space and renormalised, the collider normals are only rotated.
        [Test]
        public void MeshNormalsAreRotatedAndNormalisedButColliderNormalsAreNot()
        {
            GameObject owner = NewObject("owner");
            owner.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Mesh renderMesh = NewMesh(new Vector3(0f, 0f, 2f));
            Mesh colliderMesh = NewMesh(new Vector3(0f, 0f, 2f));
            owner.AddComponent<MeshFilter>().sharedMesh = renderMesh;
            owner.AddComponent<MeshCollider>().sharedMesh = colliderMesh;

            ObjectBender.BendProperty property = new ObjectBender.BendProperty(owner.transform, true);
            Assert.That(property.normals.Length, Is.EqualTo(5));
            Assert.That(property.colliderNormals.Length, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Near(property.normals[i], new Vector3(1f, 0f, 0f), "renderer normal " + i);
                Near(property.colliderNormals[i], new Vector3(2f, 0f, 0f), "collider normal " + i);
            }
        }

        // 0x4a3bc0 / 0x4a3cd0: the edit meshes exist only while their flag is on and a mesh was captured, and a collider that
        // shares the renderer's mesh gets no copy of its own.
        [Test]
        public void EditMeshesFollowTheirFlagsAndTheSharedColliderMeshIsNotCopied()
        {
            GameObject owner = NewObject("owner");
            Mesh shared = NewMesh(Vector3.forward);
            owner.AddComponent<MeshFilter>().sharedMesh = shared;
            owner.AddComponent<MeshCollider>().sharedMesh = shared;
            ObjectBender.BendProperty property = new ObjectBender.BendProperty(owner.transform, true);
            Assert.That(property.editMesh, Is.Not.Null);
            Assert.That(property.editMesh.vertexCount, Is.EqualTo(5));
            Assert.That(property.editMesh, Is.SameAs(property.editMesh), "created once");
            Assert.That(property.editColliderMesh, Is.Null, "the collider mesh is the renderer mesh");
            property.bendMesh = false;
            Assert.That(property.editMesh, Is.Null);
            property.bendMesh = true;
            Assert.That(property.editMesh, Is.Not.Null, "recreated after the flag is back");

            GameObject other = NewObject("other");
            other.AddComponent<MeshFilter>().sharedMesh = NewMesh(Vector3.forward);
            other.AddComponent<MeshCollider>().sharedMesh = NewMesh(Vector3.forward);
            ObjectBender.BendProperty separate = new ObjectBender.BendProperty(other.transform, true);
            Assert.That(separate.editColliderMesh, Is.Not.Null);
            Assert.That(separate.editColliderMesh.vertexCount, Is.EqualTo(5));
            separate.bendCollider = false;
            Assert.That(separate.editColliderMesh, Is.Null);
        }

        // 0x4a2430 / 0x4a25c0 / 0x4a5b90: switching the mesh off restores the original mesh on the filter; switching it on again
        // re-reads the normals (rotated, not renormalised).
        [Test]
        public void SwitchingTheMeshFlagOffRestoresTheOriginalMeshAndOnRereadsTheNormals()
        {
            GameObject owner = NewObject("owner");
            owner.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Mesh original = NewMesh(new Vector3(0f, 0f, 2f));
            MeshFilter filter = owner.AddComponent<MeshFilter>();
            filter.sharedMesh = original;
            ObjectBender.BendProperty property = new ObjectBender.BendProperty(owner.transform, true);
            Mesh other = NewMesh(Vector3.forward);
            filter.sharedMesh = other;
            property.bendMesh = false;
            Assert.That(filter.sharedMesh, Is.SameAs(original));
            property.normals = new Vector3[0];
            property.bendMesh = true;
            Assert.That(property.normals.Length, Is.EqualTo(5));
            Near(property.normals[0], new Vector3(2f, 0f, 0f), "re-read normal");
        }

        // 0x4a1b10: a non-parent property finds the SplineComputer on its transform and snapshots its points; the
        // destination spline is sized from the snapshot and then given the computer's points (the sized array is replaced).
        [Test]
        public void SplineComputerOnTheTransformIsSnapshotAndTheParentFlagSkipsTheLookup()
        {
            GameObject owner = NewObject("owner");
            SplineComputer computer = NewLine(1f, owner);
            ObjectBender.BendProperty property = new ObjectBender.BendProperty(owner.transform);
            Assert.That(property.splineComputer, Is.SameAs(computer));
            Assert.That(property.originalSpline, Is.Not.Null);
            Assert.That(property.originalSpline.points.Length, Is.EqualTo(2));
            Near(property.originalSpline.points[1].position, new Vector3(10f, 0f, 0f), "snapshot point");
            Assert.That(property.destinationSpline, Is.Not.Null);
            Assert.That(property.destinationSpline.points.Length, Is.EqualTo(2));
            Assert.That(property.destinationSpline.points, Is.Not.SameAs(property.originalSpline.points));
            property.bendSpline = false;
            Assert.That(property.originalSpline, Is.Null, "no snapshot while the flag is off");

            ObjectBender.BendProperty parentFlag = new ObjectBender.BendProperty(owner.transform, true);
            Assert.That(parentFlag.splineComputer == null, Is.True);
            Assert.That(parentFlag.originalSpline, Is.Null);
            Assert.That(parentFlag.destinationSpline, Is.Null);
        }

        // GetObjects 0x4a1530 / GetTransformsRecursively 0x4a11d0: one property per transform, the object itself first and each
        // child's subtree before its next sibling; only the bender's own transform is the parent.
        [Test]
        public void UpdateReferencesCreatesOnePropertyPerTransformDepthFirst()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            GameObject first = NewObject("first");
            first.transform.SetParent(bender.transform, false);
            GameObject grandChild = NewObject("grandChild");
            grandChild.transform.SetParent(first.transform, false);
            GameObject second = NewObject("second");
            second.transform.SetParent(bender.transform, false);

            bender.UpdateReferences();
            Transform[] expected = { bender.transform, first.transform, grandChild.transform, second.transform };
            Assert.That(bender.bendProperties.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(bender.bendProperties[i].transform.transform, Is.SameAs(expected[i]), "property " + i);
                Assert.That(bender.bendProperties[i].isParent, Is.EqualTo(i == 0), "isParent " + i);
            }

            Assert.That(Read<bool>(bender, "_parentIsTheSpline"), Is.False);
        }

        // CreateProperty 0x4a16f0: a rebuilt property takes enabled, rotation, scale, the mesh and collider flags, the lightmap
        // flag and the collider update rate from the earlier property of the same transform (not the spline flag).
        [Test]
        public void UpdateReferencesCarriesOverTheSettingsOfTheEarlierProperty()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            GameObject child = NewObject("child");
            child.transform.SetParent(bender.transform, false);
            bender.UpdateReferences();
            ObjectBender.BendProperty earlier = bender.bendProperties[1];
            earlier.enabled = false;
            earlier.applyRotation = false;
            earlier.applyScale = false;
            earlier.bendMesh = false;
            earlier.bendCollider = false;
            earlier.bendSpline = false;
            earlier.generateLightmapUVs = true;
            earlier.colliderUpdateRate = 1.5f;

            bender.UpdateReferences();
            ObjectBender.BendProperty rebuilt = bender.bendProperties[1];
            Assert.That(rebuilt, Is.Not.SameAs(earlier));
            Assert.That(rebuilt.enabled, Is.False);
            Assert.That(rebuilt.applyRotation, Is.False);
            Assert.That(rebuilt.applyScale, Is.False);
            Assert.That(rebuilt.bendMesh, Is.False);
            Assert.That(rebuilt.bendCollider, Is.False);
            Assert.That(rebuilt.generateLightmapUVs, Is.True);
            Assert.That(rebuilt.colliderUpdateRate, Is.EqualTo(1.5f));
            Assert.That(rebuilt.bendSpline, Is.True, "the spline flag is not carried over");
        }

        // CalculateBounds 0x4a2710 / CalculatePropertyBounds 0x4a28e0: the bounds start at the origin and grow with the vertices
        // and the child origins in the bender's space; GetBounds (0x4a1a50) is a copy, centre = min + extents.
        [Test]
        public void BoundsStartAtTheOriginAndGrowWithVerticesAndChildOrigins()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            GameObject child = NewObject("child");
            child.transform.SetParent(bender.transform, false);
            child.transform.localPosition = new Vector3(-3f, 5f, 1f);
            bender.UpdateReferences();

            TS_Bounds bounds = bender.GetBounds();
            Near(bounds.min, new Vector3(-3f, 0f, 0f), "min");
            Near(bounds.max, new Vector3(2f, 5f, 10f), "max");
            Near(bounds.size, new Vector3(5f, 5f, 10f), "size");
            Near(bounds.extents, new Vector3(2.5f, 2.5f, 5f), "extents");
            Near(bounds.center, new Vector3(-0.5f, 2.5f, 5f), "centre");
            Assert.That(bounds, Is.Not.SameAs(Read<TS_Bounds>(bender, "bounds")), "a copy");
        }

        // The bender and the spline on one object: the bender's own property is skipped for bounds and bending
        // (_parentIsTheSpline), so nothing but the origin remains in the bounds.
        [Test]
        public void ABenderOnTheSplineObjectSkipsItsOwnProperty()
        {
            GameObject host = NewObject("host");
            host.AddComponent<MeshFilter>().sharedMesh = NewMesh(Vector3.forward);
            SplineComputer line = NewLine(1f, host);
            ObjectBender bender = host.AddComponent<ObjectBender>();
            bender.spline = line;
            bender.RebuildImmediate();
            bender.UpdateReferences();
            Assert.That(Read<bool>(bender, "_parentIsTheSpline"), Is.True);
            Assert.That(bender.bendProperties[0].isParent, Is.True);
            Near(bender.GetBounds().max, Vector3.zero, "bounds max");

            Vector3 before = bender.bendProperties[0].transform.position;
            bender.BendObject(bender.bendProperties[0]);
            Near(bender.bendProperties[0].transform.position, before, "not moved");
        }

        // CalculatePercents 0x4a3000: the position percent is the position inside the bounds (the bender's own transform
        // contributes zero); vertex percents follow the vertices. The collider array is compared with the renderer mesh's vertex count
        // but sized by the collider mesh, so a collider mesh of another size gets a fresh array on every call.
        [Test]
        public void PercentsFollowTheBoundsAndTheColliderArrayIsReallocatedWhenTheCountsDiffer()
        {
            GameObject root = NewObject("bender");
            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = NewMesh(Vector3.forward);
            Mesh colliderMesh = new Mesh();
            colliderMesh.name = "colliderSource";
            colliderMesh.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 5f) };
            colliderMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _ownedAssets.Add(colliderMesh);
            root.AddComponent<MeshCollider>().sharedMesh = colliderMesh;
            GameObject child = NewObject("child");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(2f, 0f, 5f);
            ObjectBender bender = root.AddComponent<ObjectBender>();
            bender.spline = NewLine(1f);
            bender.RebuildImmediate();
            bender.UpdateReferences();

            Near(bender.bendProperties[0].positionPercent, Vector3.zero, "root position percent");
            Near(bender.bendProperties[1].positionPercent, new Vector3(1f, 0f, 0.5f), "child position percent");
            Vector3[] percents = bender.bendProperties[0].vertexPercents;
            Assert.That(percents.Length, Is.EqualTo(5));
            Near(percents[0], new Vector3(0f, 0f, 0f), "A");
            Near(percents[1], new Vector3(1f, 0f, 0f), "B");
            Near(percents[2], new Vector3(0f, 1f, 0f), "C");
            Near(percents[3], new Vector3(0f, 0f, 1f), "D");
            Near(percents[4], new Vector3(0f, 0f, 0.5f), "E");
            Vector3[] colliderPercents = bender.bendProperties[0].colliderVertexPercents;
            Assert.That(colliderPercents.Length, Is.EqualTo(4));
            Near(colliderPercents[1], new Vector3(0.5f, 0f, 0f), "collider vertex (1,0,0) in bounds max (2,4,10)");
            Near(colliderPercents[2], new Vector3(0f, 0.5f, 0f), "collider vertex (0,2,0)");
            Near(colliderPercents[3], new Vector3(0f, 0f, 0.5f), "collider vertex (0,0,5)");

            bender.CalculatePercents(bender.bendProperties[0]);
            Assert.That(bender.bendProperties[0].colliderVertexPercents, Is.Not.SameAs(colliderPercents), "4 != 5 reallocates");
            Assert.That(bender.bendProperties[0].colliderVertexPercents.Length, Is.EqualTo(4));
            Assert.That(bender.bendProperties[0].vertexPercents, Is.SameAs(percents), "the renderer array keeps its size");
        }

        // GetevalResult 0x4a4420, axis Z: the sample at the z percent, moved along right by the lerp of the x bounds and along
        // up by the lerp of the y bounds, times the sample size. Percentage (0.25, 0.5, 0.75) in bounds (0,0,0)..(2,4,10): sample
        // (7.5,0,0), right (0,0,-1) * 0.5, up (0,1,0) * 2. The bend rotation is LookRotation(forward, up) (+90 about Y).
        [Test]
        public void GetevalResultOnTheZAxisOffsetsAlongRightAndUp()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            bender.UpdateReferences();
            Invoke(bender, "GetevalResult", new Vector3(0.25f, 0.5f, 0.75f));
            Near(EvalResult(bender).position, new Vector3(7.5f, 2f, -0.5f), "position");
            Near(Read<Quaternion>(bender, "bendRotation") * Vector3.forward, new Vector3(1f, 0f, 0f), "bend rotation");
            Near(Read<Matrix4x4>(bender, "normalMatrix").MultiplyVector(Vector3.forward), new Vector3(1f, 0f, 0f), "normal matrix");
        }

        // Axis X (0x4a46e2 block): the sample at the x percent; right is scaled by the z lerp from max to min (reversed), up by the
        // y lerp; the extra rotation Euler(0, -90, 0) cancels the +90 of the sample. Percentage (0.25, 0.5, 0.75): sample (2.5,0,0),
        // right * Lerp(10, 0, 0.75) = 2.5, up * 2.
        [Test]
        public void GetevalResultOnTheXAxisRunsTheZLerpBackwardsAndCancelsTheSampleTurn()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            bender.UpdateReferences();
            bender.axis = ObjectBender.Axis.X;
            Invoke(bender, "GetevalResult", new Vector3(0.25f, 0.5f, 0.75f));
            Near(EvalResult(bender).position, new Vector3(2.5f, 2f, -2.5f), "position");
            Near(Read<Quaternion>(bender, "bendRotation") * Vector3.forward, new Vector3(0f, 0f, 1f), "bend rotation");
        }

        // Axis Y (0x4a4811 block): the sample at the y percent; right by the x lerp, up by the z lerp; the extra rotation
        // Euler(90, 0, 0) turns forward down (0,-1,0) after the +90 about Y. Percentage (0.25, 0.5, 0.75): sample (5,0,0),
        // right * 0.5, up * 7.5.
        [Test]
        public void GetevalResultOnTheYAxisUsesTheXAndZLerps()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            bender.UpdateReferences();
            bender.axis = ObjectBender.Axis.Y;
            Invoke(bender, "GetevalResult", new Vector3(0.25f, 0.5f, 0.75f));
            Near(EvalResult(bender).position, new Vector3(5f, 7.5f, -0.5f), "position");
            Near(Read<Quaternion>(bender, "bendRotation") * Vector3.forward, new Vector3(0f, -1f, 0f), "bend rotation");
        }

        // Normal and forward modes: Custom copies the custom vector into the sample before the offsets, Auto derives up from
        // Cross(forward, right) of the spline sample. Axis Z, percentage (0.25, 0.5, 0.75).
        [Test]
        public void CustomNormalAndForwardModesReplaceTheSampleVectors()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            bender.UpdateReferences();
            bender.upMode = ObjectBender.NormalMode.Custom;
            bender.customNormal = new Vector3(0f, 0f, 1f);
            Invoke(bender, "GetevalResult", new Vector3(0.25f, 0.5f, 0.75f));
            Near(EvalResult(bender).up, new Vector3(0f, 0f, 1f), "custom up");
            Near(EvalResult(bender).position, new Vector3(7.5f, 0.5f, 2f), "custom normal position");

            bender.upMode = ObjectBender.NormalMode.Auto;
            bender.forwardMode = ObjectBender.ForwardMode.Custom;
            bender.customForward = new Vector3(0f, 0f, 1f);
            Invoke(bender, "GetevalResult", new Vector3(0.25f, 0.5f, 0.75f));
            Near(EvalResult(bender).forward, new Vector3(0f, 0f, 1f), "custom forward");
            Near(EvalResult(bender).up, new Vector3(0f, 1f, 0f), "up derived from the spline forward and right");
            Near(EvalResult(bender).position, new Vector3(8f, 2f, 0f), "custom forward position");
        }

        // BendObject 0x4a4bc0 on the Z axis: with this straight line the bent mesh is the original rotated into the spline's
        // frame, world (x,y,z) -> (z, y, -x), which the object's own transform (position at the start, rotation +90 about Y)
        // makes the local vertices stay unchanged. The normals (0,0,1) end up world (1,0,0).
        [Test]
        public void BendingTheMeshAlongTheLineRotatesItIntoTheSplinesFrame()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            bender.bend = true;
            Mesh bent = filter.sharedMesh;
            Assert.That(bent, Is.Not.SameAs(source));
            _ownedAssets.Add(bent);
            Transform root = bender.transform;
            Near(root.position, Vector3.zero, "root position");
            Assert.That(Quaternion.Angle(root.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1e-2f), "root rotation");
            Near(root.localScale, Vector3.one, "root scale");

            Vector3[] original = source.vertices;
            Vector3[] vertices = bent.vertices;
            Vector3[] normals = bent.normals;
            Assert.That(vertices.Length, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Vector3 expected = new Vector3(original[i].z, original[i].y, -original[i].x);
                Near(root.TransformPoint(vertices[i]), expected, "world vertex " + i);
                Near(root.TransformDirection(normals[i]), new Vector3(1f, 0f, 0f), "world normal " + i);
            }
        }

        // The sample size scales the offsets and the object: with both points size 2 the world position is (z, 2y, -2x) and the
        // root is scaled by 2.
        [Test]
        public void TheSampleSizeScalesTheOffsetsAndTheObject()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source, 2f);
            bender.bend = true;
            Mesh bent = filter.sharedMesh;
            _ownedAssets.Add(bent);
            Transform root = bender.transform;
            Near(root.localScale, new Vector3(2f, 2f, 2f), "root scale");
            Vector3[] original = source.vertices;
            Vector3[] vertices = bent.vertices;
            for (int i = 0; i < 5; i++)
            {
                Vector3 expected = new Vector3(original[i].z, 2f * original[i].y, -2f * original[i].x);
                Near(root.TransformPoint(vertices[i]), expected, "world vertex " + i);
            }
        }

        // BendMesh 0x4a5480 on the Y axis flips the third percentage (0x4a5547 cmp [this+0x1d0],1; 0x4a5550 1.0 - percent.z; the
        // last vertex again at 0x4a5690). The sample runs along y and the up offset along z, so for the bounds (0,0,0)..(2,4,10) the
        // vertices land at A(0,0,0) -> (0,10,0), B(2,0,0) -> (0,10,-2), C(0,4,0) -> (10,10,0), D(0,0,10) -> (0,0,0) and
        // E(0,0,5) -> (0,5,0). Without the flip A would stay at the origin and D would reach (0,10,0).
        [Test]
        public void BendingTheMeshAlongTheYAxisFlipsTheThirdPercentage()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            bender.axis = ObjectBender.Axis.Y;
            bender.bend = true;
            Mesh bent = filter.sharedMesh;
            Assert.That(bent, Is.Not.SameAs(source));
            _ownedAssets.Add(bent);
            Transform root = bender.transform;
            Vector3[] expected =
            {
                new Vector3(0f, 10f, 0f),
                new Vector3(0f, 10f, -2f),
                new Vector3(10f, 10f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 5f, 0f)
            };
            Vector3[] vertices = bent.vertices;
            Assert.That(vertices.Length, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Near(root.TransformPoint(vertices[i]), expected[i], "world vertex " + i);
            }
        }

        // BendObject 0x4a4bc0: a disabled property is skipped entirely, an enabled one has its snapshot position set
        // (applyRotation off keeps the original rotation).
        [Test]
        public void ADisabledPropertyIsNotBentAndApplyRotationOffKeepsTheOriginalRotation()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            GameObject child = NewObject("child");
            child.transform.SetParent(bender.transform, false);
            child.transform.localPosition = new Vector3(2f, 0f, 5f);
            bender.UpdateReferences();
            ObjectBender.BendProperty property = bender.bendProperties[1];
            property.enabled = false;
            Vector3 before = property.transform.position;
            bender.BendObject(property);
            Near(property.transform.position, before, "disabled");

            property.enabled = true;
            property.applyRotation = false;
            bender.BendObject(property);
            // child percent (1, 0, 0.5): sample (5,0,0) + right * Lerp(0,2,1) = (0,0,-2)
            Near(property.transform.position, new Vector3(5f, 0f, -2f), "enabled");
            Assert.That(Quaternion.Angle(property.transform.rotation, property.originalRotation), Is.LessThan(1e-3f));
        }

        // Revert 0x4a4060: switching bend off puts the transform and the mesh back.
        [Test]
        public void SwitchingBendOffRevertsTheTransformAndTheMesh()
        {
            MeshFilter filter;
            Mesh source;
            ObjectBender bender = NewBender(out filter, out source);
            bender.bend = true;
            Assert.That(filter.sharedMesh, Is.Not.SameAs(source));
            _ownedAssets.Add(filter.sharedMesh);
            Assert.That(Quaternion.Angle(bender.transform.rotation, Quaternion.identity), Is.GreaterThan(45f));

            bender.bend = false;
            Assert.That(filter.sharedMesh, Is.SameAs(source));
            Assert.That(Quaternion.Angle(bender.transform.localRotation, Quaternion.identity), Is.LessThan(1e-3f));
            Near(bender.transform.localPosition, Vector3.zero, "position");
            Near(bender.transform.localScale, Vector3.one, "scale");
        }

        // The spline part of BendObject: a child with a SplineComputer has its points bent into destinationSpline and pushed
        // to the computer, and reverting puts the snapshot back. Child points (0,0,0) and (0,0,10) in bounds (0,0,0)..(0,0,10)
        // end at (0,0,0) and (10,0,0).
        [Test]
        public void ASplineOnAChildIsBentAndRestored()
        {
            GameObject root = NewObject("bender");
            ObjectBender bender = root.AddComponent<ObjectBender>();
            bender.spline = NewLine(1f);
            bender.RebuildImmediate();
            GameObject child = NewObject("child");
            child.transform.SetParent(root.transform, false);
            SplineComputer childLine = child.AddComponent<SplineComputer>();
            childLine.type = Spline.Type.Linear;
            SplinePoint first = new SplinePoint(new Vector3(0f, 0f, 0f));
            first.normal = new Vector3(0f, 0f, 1f);
            SplinePoint second = new SplinePoint(new Vector3(0f, 0f, 10f));
            second.tangent = new Vector3(0f, 0f, 5f);
            second.normal = new Vector3(0f, 0f, 1f);
            childLine.SetPoints(new[] { first, second });
            childLine.RebuildImmediate();

            bender.bend = true;
            SplinePoint[] bent = childLine.GetPoints();
            Assert.That(bent.Length, Is.EqualTo(2));
            Near(bent[0].position, new Vector3(0f, 0f, 0f), "first point");
            Near(bent[1].position, new Vector3(10f, 0f, 0f), "second point");
            // The tangents have their own percentages (z 0.5 for the tangent at (0,0,5), where the position percent would give 10),
            // and the normal is turned by LookRotation(forward, up) on the Z axis (0x4a5350..0x4a538c): (0,0,1) -> (1,0,0).
            Near(bent[1].tangent, new Vector3(5f, 0f, 0f), "second point tangent");
            Near(bent[0].normal, new Vector3(1f, 0f, 0f), "first point normal");
            Near(bent[1].normal, new Vector3(1f, 0f, 0f), "second point normal");

            bender.bend = false;
            SplinePoint[] restored = childLine.GetPoints();
            Near(restored[0].position, new Vector3(0f, 0f, 0f), "restored first");
            Near(restored[1].position, new Vector3(0f, 0f, 10f), "restored second");
        }

        // Update 0x4a43e0 / ApplyCollider 0x4a5eb0: a collider that shares the renderer's mesh takes the bent renderer mesh.
        [Test]
        public void AColliderOnTheSharedMeshTakesTheBentRendererMeshOnUpdate()
        {
            GameObject root = NewObject("bender");
            MeshFilter filter = root.AddComponent<MeshFilter>();
            Mesh source = NewMesh(Vector3.forward);
            filter.sharedMesh = source;
            MeshCollider collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = source;
            ObjectBender bender = root.AddComponent<ObjectBender>();
            bender.spline = NewLine(1f);
            bender.RebuildImmediate();

            bender.bend = true;
            _ownedAssets.Add(filter.sharedMesh);
            bender.bendProperties[0].Update();
            Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh));
            Assert.That(collider.sharedMesh, Is.Not.SameAs(source));
        }

        // BendObject 0x4a4bc0 sets the world rotation to bendRotation * (Inverse(parentRotation) * originalRotation): the inner
        // product at 0x4a4c92..0x4a4d20, the outer at 0x4a4d24..0x4a4db4; CreateProperty measures a child as
        // Inverse(bender rotation) * child rotation (0x4a1963..0x4a1a27). bendRotation is LookRotation((1,0,0), up) = Euler(0,90,0).
        // Unrotated bender: a child at 45 degrees about Z ends at Euler(0,90,0) * Euler(0,0,45) (the reversed product differs).
        // Bender at 90 degrees about Z: the child's original is Inverse(90Z) * (90Z * 45Z) = 45Z and its parent rotation 90Z, so
        // it ends at Euler(0,90,0) * Inverse(90Z) * 45Z = Euler(0,90,0) * Euler(0,0,-45); keeping the local rotation would give +45.
        [Test]
        public void ABentChildComposesTheBendRotationWithItsOriginalRotation()
        {
            Quaternion bendRotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject plainRoot = NewObject("plainBender");
            ObjectBender plain = plainRoot.AddComponent<ObjectBender>();
            GameObject plainChild = NewObject("plainChild");
            plainChild.transform.SetParent(plainRoot.transform, false);
            plainChild.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            plain.spline = NewLine(1f);
            plain.RebuildImmediate();
            plain.bend = true;
            Assert.That(Quaternion.Angle(plainChild.transform.rotation, bendRotation * Quaternion.Euler(0f, 0f, 45f)), Is.LessThan(1e-2f), "unrotated bender");

            GameObject turnedRoot = NewObject("turnedBender");
            turnedRoot.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            GameObject turnedChild = NewObject("turnedChild");
            turnedChild.transform.SetParent(turnedRoot.transform, false);
            turnedChild.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            ObjectBender turned = turnedRoot.AddComponent<ObjectBender>();
            turned.spline = NewLine(1f);
            turned.RebuildImmediate();
            turned.bend = true;
            Assert.That(Quaternion.Angle(turnedChild.transform.rotation, bendRotation * Quaternion.Euler(0f, 0f, -45f)), Is.LessThan(1e-2f), "bender turned about Z");
        }

        // A collider with a mesh of its own: Apply 0x4a4363..0x4a438c queues the collider, Update calls ApplyCollider
        // (0x4a5eb0), which writes the bent collider vertices into destinationColliderMesh (named after the original at 0x4a602e).
        // That mesh is neither the original collider mesh nor the renderer's; the vertices go (x,y,z) -> (z,y,-x) as for the renderer.
        // Outside play mode the update delay stays 0, so Update writes at once.
        [Test]
        public void AColliderWithItsOwnMeshTakesABentCopyOfThatMesh()
        {
            GameObject root = NewObject("bender");
            MeshFilter filter = root.AddComponent<MeshFilter>();
            Mesh source = NewMesh(Vector3.forward);
            filter.sharedMesh = source;
            Vector3[] colliderVertices = { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 5f) };
            Mesh colliderSource = new Mesh();
            colliderSource.name = "colliderSource";
            colliderSource.vertices = colliderVertices;
            colliderSource.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            // MeshUtility.CalculateTangents (0x4fa4e0) indexes the uv array per triangle corner with bounds checks, so a collider mesh
            // without uvs throws IndexOutOfRangeException in the shipped build as well; the mesh carries uvs.
            colliderSource.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f) };
            colliderSource.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _ownedAssets.Add(colliderSource);
            MeshCollider collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = colliderSource;
            ObjectBender bender = root.AddComponent<ObjectBender>();
            bender.spline = NewLine(1f);
            bender.RebuildImmediate();

            bender.bend = true;
            _ownedAssets.Add(filter.sharedMesh);
            bender.bendProperties[0].Update();
            Mesh bentCollider = collider.sharedMesh;
            _ownedAssets.Add(bentCollider);
            Assert.That(bentCollider, Is.Not.SameAs(colliderSource));
            Assert.That(bentCollider, Is.Not.SameAs(filter.sharedMesh));
            Assert.That(bentCollider.name, Is.EqualTo("colliderSource"));
            Vector3[] vertices = bentCollider.vertices;
            Assert.That(vertices.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Vector3 expected = new Vector3(colliderVertices[i].z, colliderVertices[i].y, -colliderVertices[i].x);
                Near(bender.transform.TransformPoint(vertices[i]), expected, "world collider vertex " + i);
            }
        }
    }
}
