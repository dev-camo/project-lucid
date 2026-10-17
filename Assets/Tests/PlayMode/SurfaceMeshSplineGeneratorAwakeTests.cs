using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // SurfaceMeshSplineGenerator.Awake 0x06003d61 (x86-64 0x6d9a90): after base.Awake, the null
    // filter return, GetVertices, InvokeOnValid(RegisterMesh) and the startup and shutdown
    // subscriptions (add_OnSystemShutdown call at 0x6d9c20), the body loads the receiver's class
    // and tail-jumps through class+0x328 with its MethodInfo at +0x330 (0x6d9c25..0x6d9c47). The
    // same binary calibrates the vtable stride: InternalUpdate 0x6da744 calls +0x338 for
    // ApplyTrackerToMesh (slot 32) and GenerateMesh 0x6d6a0d calls +0x348 for UpdateSamplePoints
    // (slot 33), so +0x328 is slot 31, InitialiseSplineTrackers; Reset (slot 26) would be +0x2d8.
    // Ownership: the pool manager row is admitted while absent or idle, a genuine
    // InstancedObjectPoolManager is registered into it so InvokeOnValid runs instead of queuing,
    // and the row is captured before the generator's field initializer adds its typed reference.
    // Every cleanup step runs through the lease; the first body failure stays primary.
    public sealed class SurfaceMeshSplineGeneratorAwakeTests
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const float Tolerance = .01f;

        private static FieldInfo Field(Type owner, string name) =>
            owner.GetField(name, Own) ?? throw new MissingFieldException(owner.FullName, name);

        private static object Read(object owner, Type declaring, string name) => Field(declaring, name).GetValue(owner);

        private static Mesh TemplateMesh(string name, int vertexCount)
        {
            var vertices = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++) vertices[i] = new Vector3(i, 1f, 0f);
            return new Mesh { name = name, vertices = vertices };
        }

        // True when the event backing field holds a delegate bound to this generator's named method.
        private static bool Subscribed(object reference, string eventName, object target, string method)
        {
            Type type = reference.GetType();
            while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
            var handlers = (Delegate)type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference);
            if (handlers == null) return false;
            foreach (Delegate handler in handlers.GetInvocationList())
                if (ReferenceEquals(handler.Target, target) && handler.Method.Name == method) return true;
            return false;
        }

        private sealed class Owned
        {
            internal readonly List<Object> Objects = new List<Object>();
            internal Mesh Template;
            internal MeshFilter Filter;
        }

        // Builds a genuine pool manager and a generator of type T on inactive objects, sets the
        // authored fields, activates the generator (Unity's Awake then OnEnable), runs body, and
        // always releases owned state. configure runs before activation.
        private static void RunOwned<T>(int templateVertices, Action<T, Owned> configure, Action<T, Owned> body)
            where T : SurfaceMeshSplineGenerator
        {
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            string row = ProcessManager.GetDefaultName<InstancedObjectPoolManager>();
            bool rowWasPresent = registry.Contains(row);
            LifecycleSystemRowLease lease = LifecycleSystemRowLease.AdmitAbsentOrIdle(registry, row);
            var owned = new Owned();
            GameObject poolObject = null, generatorObject = null;
            T generator = null;
            Exception bodyFailure = null;
            try
            {
                poolObject = new GameObject("Lucid owned surface mesh pool manager");
                poolObject.SetActive(false);
                poolObject.AddComponent<InstancedObjectPoolManager>();
                poolObject.SetActive(true);
                lease.CaptureRowIfCreated();

                owned.Template = TemplateMesh("Lucid owned surface mesh template", templateVertices);
                owned.Objects.Add(owned.Template);
                generatorObject = new GameObject("Lucid owned surface mesh generator");
                generatorObject.SetActive(false);
                // RequireComponent adds the MeshFilter; the field initializer takes the typed pool reference.
                generator = generatorObject.AddComponent<T>();
                lease.CaptureTypedReferenceIfCreated<InstancedObjectPoolManager>();
                owned.Filter = generatorObject.GetComponent<MeshFilter>();
                owned.Filter.sharedMesh = owned.Template;
                Field(typeof(SurfaceMeshSplineGenerator), "m_meshFilter").SetValue(generator, owned.Filter);
                // An assigned category skips the SystemConfiguration lookup in Awake, and the authored
                // activator flag skips the first enable, so no Initialise is queued on the TimeManager row.
                var category = ScriptableObject.CreateInstance<TimeCategoryObject>();
                owned.Objects.Add(category);
                generator.TimeCategoryObject = category;
                Field(typeof(TimeScaledComponent), "m_enabledByActivator").SetValue(generator, true);
                configure(generator, owned);
                generatorObject.SetActive(true);
                body(generator, owned);
            }
            catch (Exception failure)
            {
                bodyFailure = failure;
            }
            finally
            {
                // A pooled clone acquired by a dirty update is returned on destroy and then owned here.
                lease.RunCleanup("capture-pooled-mesh", () =>
                {
                    if (owned.Filter == null) return;
                    Mesh current = owned.Filter.sharedMesh;
                    if (current != null && current != owned.Template && !owned.Objects.Contains(current)) owned.Objects.Add(current);
                });
                // The component goes first so its OnDisable/OnDestroy still see the MeshFilter and pool.
                T ownedGenerator = generator;
                lease.RunCleanup("destroy-generator", () => { if (ownedGenerator != null) Object.DestroyImmediate(ownedGenerator); });
                GameObject ownedGeneratorObject = generatorObject;
                lease.RunCleanup("destroy-generator-object", () => { if (ownedGeneratorObject != null) Object.DestroyImmediate(ownedGeneratorObject); });
                GameObject ownedPoolObject = poolObject;
                lease.RunCleanup("destroy-pool-manager", () => { if (ownedPoolObject != null) Object.DestroyImmediate(ownedPoolObject); });
                lease.RunCleanup("capture-typed-reference", () => lease.CaptureTypedReferenceIfCreated<InstancedObjectPoolManager>());
                for (int i = owned.Objects.Count - 1; i >= 0; i--)
                {
                    Object ownedObject = owned.Objects[i];
                    lease.RunCleanup("destroy-owned-object", () => { if (ownedObject != null) Object.DestroyImmediate(ownedObject); });
                }
                lease.Dispose();
            }
            if (bodyFailure != null)
            {
                foreach (LifecycleSystemRowLease.FailureRecord record in lease.CleanupFailures)
                    try { TestContext.Error.WriteLine("Surface mesh cleanup also failed at " + record.Step + ": " + record.Failure); }
                    catch (Exception) { }
                ExceptionDispatchInfo.Capture(bodyFailure).Throw();
            }
            Assert.That(lease.CleanupFailures, Is.Empty);
            Assert.That(registry.Contains(row), Is.EqualTo(rowWasPresent), "the pool manager row is left as it was found");
        }

        // Catches a Reset (slot 26) tail, a missing tracker dispatch, a second dispatch, and a
        // dispatch moved ahead of the vertex read or either pool subscription.
        [Test]
        public void AwakeDispatchesInitialiseSplineTrackersOnceAfterTheShutdownSubscriptionAndNeverReset()
        {
            int verticesAtDispatch = -1;
            bool startupAtDispatch = false, shutdownAtDispatch = false;
            RunOwned<SurfaceMeshSplineGeneratorAwakeProbe>(4, (probe, owned) =>
            {
                Assert.That(probe.Calls, Is.Empty, "nothing is dispatched between AddComponent and activation");
                probe.AtInitialiseSplineTrackers = () =>
                {
                    object reference = Read(probe, typeof(SurfaceMeshSplineGenerator), "m_poolManagerRef");
                    verticesAtDispatch = ((List<Vector3>)Read(probe, typeof(SurfaceMeshSplineGenerator), "m_meshVertices")).Count;
                    startupAtDispatch = Subscribed(reference, "OnSystemStartup", probe, "RegisterMesh");
                    shutdownAtDispatch = Subscribed(reference, "OnSystemShutdown", probe, "UnregisterMesh");
                };
            }, (probe, owned) =>
            {
                Assert.That(probe.Calls, Is.EqualTo(new[] { "InitialiseSplineTrackers" }),
                    "Awake dispatches slot 31 exactly once and never the Reset message");
                Assert.That(verticesAtDispatch, Is.EqualTo(4), "the vertex read precedes the dispatch");
                Assert.That(startupAtDispatch, Is.True, "the startup subscription precedes the dispatch");
                Assert.That(shutdownAtDispatch, Is.True, "the shutdown subscription precedes the dispatch");
                // Control: a Reset dispatch is recorded, so its absence above is not vacuous.
                probe.InvokeReset();
                Assert.That(probe.Calls, Is.EqualTo(new[] { "InitialiseSplineTrackers", "Reset" }));
            });
        }

        // SurfaceMeshPipeGenerator.InitialiseSplineTrackers 0x06003d4a is the only AddSpline caller
        // and the only writer of the profile lists. A straight two-knot spline from (0,0,0) to
        // (0,0,2) with a mesh step longer than the spline gives exactly two profile samples, at its
        // ends, whatever rounding the length has. Without the Awake dispatch the tracker list stays
        // empty, OnEnable subscribes to no spline, and the first dirty ApplyTrackerToMesh would
        // reach GetTracker(0) with no tracker.
        [Test]
        public void PipeAwakeBuildsTrackerAndProfileFromTheSuppliedSplineBeforeTheFirstDirtyUpdate()
        {
            EditableSpline editable = null;
            RunOwned<SurfaceMeshPipeGenerator>(16, (pipe, owned) =>
            {
                var splineObject = new GameObject("Lucid owned pipe spline");
                owned.Objects.Add(splineObject);
                editable = splineObject.AddComponent<EditableSpline>();
                foreach (float z in new[] { 0f, 2f })
                {
                    var knotObject = new GameObject("Lucid owned pipe spline knot");
                    knotObject.transform.SetParent(splineObject.transform, false);
                    knotObject.transform.localPosition = new Vector3(0f, 0f, z);
                    editable.Knots.Add(knotObject.AddComponent<SplineKnot>());
                }
                editable.Initialise();
                editable.KnotsUpdated();
                Assert.That(editable.Length, Is.EqualTo(2f).Within(Tolerance), "the authored spline is two units long");
                var facade = splineObject.AddComponent<Spline>();
                Field(typeof(Spline), "m_editableSpline").SetValue(facade, editable);
                Assert.That(facade.GetSpline(), Is.SameAs(editable));
                Field(typeof(SurfaceMeshPipeGenerator), "m_spline").SetValue(pipe, facade);
                Field(typeof(SurfaceMeshSplineGenerator), "m_meshStepDistance").SetValue(pipe, 10f);
                Field(typeof(SurfaceMeshSplineGenerator), "m_updateAtRuntime").SetValue(pipe, true);
            }, (pipe, owned) =>
            {
                Type generator = typeof(SurfaceMeshSplineGenerator), pipeType = typeof(SurfaceMeshPipeGenerator);
                var trackers = (List<Tracker2D>)Read(pipe, generator, "m_trackers");
                var splines = (List<ISpline>)Read(pipe, generator, "m_splines");
                Assert.That(trackers, Has.Count.EqualTo(1), "Awake added the supplied spline's tracker");
                Assert.That(splines, Has.Count.EqualTo(1));
                Assert.That(splines[0], Is.SameAs(editable));
                Assert.That(trackers[0].Location.m_surface, Is.SameAs(editable), "the tracker is on the supplied spline");

                var positions = (List<Vector3>)Read(pipe, pipeType, "m_splinePositions");
                var rotations = (List<Quaternion>)Read(pipe, pipeType, "m_splineRotations");
                var offsets = (List<Vector2>)Read(pipe, pipeType, "m_pipeOffsets");
                Assert.That(positions, Has.Count.EqualTo(2), "one profile sample at each end");
                Assert.That(Vector3.Distance(positions[0], Vector3.zero), Is.LessThan(Tolerance), "first sample at the start knot");
                Assert.That(Vector3.Distance(positions[1], new Vector3(0f, 0f, 2f)), Is.LessThan(Tolerance), "last sample at the end knot");
                Assert.That(rotations, Has.Count.EqualTo(2));
                foreach (Quaternion rotation in rotations)
                    Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.LessThan(.5f), "a straight +z spline faces forward");
                // Eight default segments of radius .2 around the profile.
                Assert.That(offsets, Has.Count.EqualTo(8));
                Assert.That(Vector2.Distance(offsets[0], new Vector2(.2f, 0f)), Is.LessThan(Tolerance));
                Assert.That(Vector2.Distance(offsets[2], new Vector2(0f, .2f)), Is.LessThan(Tolerance));

                // First dirty update: OnEnable subscribed to the tracked spline, so a knot update marks
                // the pipe dirty and the next update acquires a pooled mesh and rings it around the spline.
                FieldInfo dirty = Field(generator, "m_isDirty");
                Assert.That(dirty.GetValue(pipe), Is.False);
                editable.KnotsUpdated();
                Assert.That(dirty.GetValue(pipe), Is.True, "OnEnable subscribed to the spline Awake tracked");
                pipe.OnUpdate(0f);
                Assert.That(dirty.GetValue(pipe), Is.False);
                Mesh pooled = owned.Filter.sharedMesh;
                Assert.That(pooled, Is.Not.Null);
                Assert.That(pooled, Is.Not.SameAs(owned.Template), "the dirty update acquired the pooled mesh");
                owned.Objects.Add(pooled);
                Vector3[] vertices = pooled.vertices;
                Assert.That(vertices, Has.Length.EqualTo(16));
                Assert.That(Vector3.Distance(vertices[0], new Vector3(.2f, 0f, 0f)), Is.LessThan(Tolerance), "first ring at the start");
                Assert.That(Vector3.Distance(vertices[8], new Vector3(.2f, 0f, 2f)), Is.LessThan(Tolerance), "second ring at the end");
            });
        }
    }
}
