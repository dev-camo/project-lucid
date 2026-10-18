using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Original HLSplines.Runtime placement types 0200002a..0200003a on owned
    // GameObjects, assets and in-memory surfaces. Edit Mode runs no automatic
    // Start/OnDestroy for the placement components, so lifecycle bodies are
    // invoked explicitly; every ProcessManager row they reach is leased or
    // owned by the proof. Storage bodies (the splines-storage types) are never
    // reached: every case keeps mesh type and storage lists empty or null.
    public sealed class OriginalProceduralMeshPlacementTests
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string HostName = "Owned placement host";
        private const string RatioMessage = "' has no mesh types with a valid ratio. Set at least one to have a value greater than zero";

        // PerformanceSetup..ctor 0x06000111 (x86-64 0x1A60E70): stores Medium (0),
        // the pair {40f, 1f} read from 0x29A8190, 5f (0x40A00000), true,
        // 1f (0x3F800000) and false. Reset 0x06000101 (0x1A60D80) appends the
        // same inlined default. Fails if any default or the append changes.
        [Test]
        public void PerformanceSetupConstructorAndResetStoreTheNativeDefaults()
        {
            AssertDefaultProfile(new ProceduralMesh_PlacementSetup.PerformanceSetup());
            using (var owned = new OwnedSetup())
            {
                List<ProceduralMesh_PlacementSetup.PerformanceSetup> profiles = owned.Profiles;
                Assert.That(profiles, Is.Empty);
                Assert.That(profiles.Capacity, Is.EqualTo(1), "The constructor 0x1A612C0 allocates List(1).");
                Invoke(owned.Setup, "Reset");
                Invoke(owned.Setup, "Reset");
                Assert.That(profiles.Count, Is.EqualTo(2));
                Assert.That(profiles[0], Is.Not.SameAs(profiles[1]), "Each Reset appends a fresh profile.");
                AssertDefaultProfile(profiles[0]);
                AssertDefaultProfile(profiles[1]);
            }
        }

        // CompareTo 0x06000112 (0x1A613B0): casts the argument, then compares
        // only the boxed quality levels with Enum.CompareTo. A null argument
        // takes the null-reference exit 0x1A61467; another type the cast
        // exit 0x1A6145F. Fails if any other field orders profiles.
        [Test]
        public void PerformanceSetupComparesQualityLevelsOnly()
        {
            var low = Profile(PerformanceProfile.QualityLevel.Low, 10f);
            var high = Profile(PerformanceProfile.QualityLevel.High, 1f);
            var medium = Profile(PerformanceProfile.QualityLevel.Medium, 99f);
            var otherMedium = Profile(PerformanceProfile.QualityLevel.Medium, -5f);
            Assert.That(Math.Sign(low.CompareTo(high)), Is.EqualTo(-1));
            Assert.That(Math.Sign(high.CompareTo(low)), Is.EqualTo(1));
            Assert.That(medium.CompareTo(otherMedium), Is.Zero, "Spawn distance never orders profiles.");
            var sorted = new List<ProceduralMesh_PlacementSetup.PerformanceSetup> { high, medium, low };
            sorted.Sort();
            Assert.That(sorted, Is.EqualTo(new[] { low, medium, high }));
            Assert.Throws<NullReferenceException>(() => low.CompareTo(null));
            Assert.Throws<InvalidCastException>(() => low.CompareTo("High"));
        }

        // Awake 0x06000104 runs AttemptScalablePerformanceSetup 0x06000105
        // (0x1A60EC0): fetch the typed ScalablePerformance reference once and
        // add the startup and shutdown handlers. A second attempt returns at
        // the non-null test 0x1A60F4A. Fails if the reference is not taken, a
        // handler is missing, or a repeated attempt subscribes again.
        [Test]
        public void AwakeObservesTheScalablePerformanceReferenceOnce()
        {
            using (var owned = new OwnedSetup())
            {
                SystemRef<ScalablePerformance> reference = owned.Reference;
                Assert.That(reference, Is.Not.Null, "CreateInstance runs Awake for a ScriptableObject.");
                Assert.That(reference.SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<ScalablePerformance>()));
                Assert.That(reference.IsValid(), Is.False, "The fixture admits only an unowned row.");
                Assert.That(owned.Handlers("OnSystemStartup"), Is.EqualTo(new[] { owned.Startup }));
                Assert.That(owned.Handlers("OnSystemShutdown"), Is.EqualTo(new[] { owned.Shutdown }));
                Invoke(owned.Setup, "AttemptScalablePerformanceSetup");
                Assert.That(Get(owned.Setup, "m_scalablePerformanceRef"), Is.SameAs(reference));
                Assert.That(owned.Handlers("OnSystemStartup"), Is.EqualTo(new[] { owned.Startup }));
                Assert.That(owned.Handlers("OnSystemShutdown"), Is.EqualTo(new[] { owned.Shutdown }));
            }
        }

        // GetSetup 0x06000102 (0x1A59B40): with no profiles a new default is
        // cached (0x1A59D40) but never listed; with profiles the lowest of a
        // sorted GetRange copy (0x1A59C41..0x1A59C6A) is selected, and the
        // ScalablePerformance raise is skipped while the reference is invalid
        // (0x1A59C99). Fails if the default is not cached, is listed, the
        // highest or first profile is chosen, or the authored order is sorted.
        [Test]
        public void GetSetupCachesTheDefaultOrTheLowestProfileOfASortedCopy()
        {
            using (var owned = new OwnedSetup())
            {
                ProceduralMesh_PlacementSetup.PerformanceSetup fallback = owned.Setup.GetSetup();
                AssertDefaultProfile(fallback);
                Assert.That(owned.Setup.GetSetup(), Is.SameAs(fallback));
                Assert.That(owned.Profiles, Is.Empty);
            }

            using (var owned = new OwnedSetup())
            {
                var high = Profile(PerformanceProfile.QualityLevel.High, 1f);
                var veryLow = Profile(PerformanceProfile.QualityLevel.VeryLow, 2f);
                var medium = Profile(PerformanceProfile.QualityLevel.Medium, 3f);
                owned.Profiles.AddRange(new[] { high, veryLow, medium });
                Assert.That(owned.Setup.GetSetup(), Is.SameAs(veryLow));
                Assert.That(owned.Profiles, Is.EqualTo(new[] { high, veryLow, medium }), "Only the copy is sorted.");
                owned.Profiles.Clear();
                Assert.That(owned.Setup.GetSetup(), Is.SameAs(veryLow), "The selection stays cached.");
            }
        }

        // OnPerformanceProfileUpdated 0x06000103 (0x1A61060): clears the cached
        // profile, then invokes OnUpdated with the setup. Fails if the cache
        // survives, the event is skipped or repeated, or another sender is passed.
        [Test]
        public void ProfileUpdatesDropTheSelectionAndRaiseOnUpdatedOnce()
        {
            using (var owned = new OwnedSetup())
            {
                ProceduralMesh_PlacementSetup.PerformanceSetup first = owned.Setup.GetSetup();
                var senders = new List<ProceduralMesh_PlacementSetup>();
                Action<ProceduralMesh_PlacementSetup> record = senders.Add;
                owned.Setup.OnUpdated += record;
                try
                {
                    Invoke(owned.Setup, "OnPerformanceProfileUpdated", (PerformanceProfile)null);
                }
                finally
                {
                    owned.Setup.OnUpdated -= record;
                }
                Assert.That(senders, Is.EqualTo(new[] { owned.Setup }));
                Assert.That(Get(owned.Setup, "m_selectedProfile"), Is.Null);
                ProceduralMesh_PlacementSetup.PerformanceSetup second = owned.Setup.GetSetup();
                Assert.That(second, Is.Not.SameAs(first), "A fresh default replaces the dropped one.");
                AssertDefaultProfile(second);
            }
        }

        // AddErrorMessage 0x0600009d (0x1A5A470): a null list becomes
        // List(1) (0x1A5A4DA) before the add; an existing list is returned
        // itself. Fails if a list is always allocated or the message is lost.
        [Test]
        public void AddErrorMessageAllocatesOnlyForANullList()
        {
            var created = (List<string>)InvokeStatic(typeof(ProceduralMesh_Placement), "AddErrorMessage", null, "first");
            Assert.That(created, Is.EqualTo(new[] { "first" }));
            Assert.That(created.Capacity, Is.EqualTo(1));
            var appended = (List<string>)InvokeStatic(typeof(ProceduralMesh_Placement), "AddErrorMessage", created, "second");
            Assert.That(appended, Is.SameAs(created));
            Assert.That(created, Is.EqualTo(new[] { "first", "second" }));
        }

        // GetPath 0x0600009f (0x1A5A560): a root is "/" + name; a child is its
        // parent's path + "/" + name. Fails if the root slash, an ancestor or
        // the order is lost.
        [Test]
        public void GetPathJoinsEveryAncestorFromTheRoot()
        {
            var root = new GameObject("Root");
            try
            {
                var middle = new GameObject("Middle");
                middle.transform.SetParent(root.transform, false);
                var leaf = new GameObject("Leaf");
                leaf.transform.SetParent(middle.transform, false);
                Assert.That(InvokeStatic(typeof(ProceduralMesh_Placement), "GetPath", root.transform), Is.EqualTo("/Root"));
                Assert.That(InvokeStatic(typeof(ProceduralMesh_Placement), "GetPath", leaf.transform), Is.EqualTo("/Root/Middle/Leaf"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // TransformBounds 0x060000a1 (0x1A5A630): the centre goes through
        // TransformPoint; each extent axis goes through TransformVector and the
        // new extents are the sums of absolute components (mask 0x29A6C20).
        // Hand values: Y rotation by 90 degrees and scale (2, 3, 4) map local
        // extents (1, 2, 3) to (12, 6, 2). Fails for a naive scale-only
        // result (2, 6, 12) or a dropped centre transform.
        [Test]
        public void TransformBoundsSumsTheAbsoluteTransformedExtentAxes()
        {
            var host = new GameObject("Bounds host");
            try
            {
                host.transform.position = new Vector3(10f, 0f, 0f);
                host.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                host.transform.localScale = new Vector3(2f, 3f, 4f);
                var local = new Bounds { center = new Vector3(1f, 0f, 0f), extents = new Vector3(1f, 2f, 3f) };
                Bounds world = ProceduralMesh_Placement.TransformBounds(host.transform, local);
                AssertVector(world.center, new Vector3(10f, 0f, -2f));
                AssertVector(world.extents, new Vector3(12f, 6f, 2f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // TestAgainstCameraFrustum 0x060000a2 (0x1A5A720): returns true when
        // UseCameraFrustum (config +0x2B) is off; otherwise tests the shared
        // frustum planes against the transformed bounds. Fails if the flag is
        // ignored or the local, untransformed bounds are tested.
        [Test]
        public void FrustumTestHonoursTheConfigFlagAndUsesTransformedBounds()
        {
            FieldInfo planesField = typeof(ProceduralMesh_Placement).GetField("m_cameraPlanes", Own);
            object priorPlanes = planesField.GetValue(null);
            var host = new GameObject(HostName);
            var config = ScriptableObject.CreateInstance<ProceduralMesh_SystemConfig>();
            try
            {
                var placement = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                SetProperty(placement, typeof(ProceduralMesh_Placement), "SystemConfig", config);
                planesField.SetValue(null, new[] { new Plane(Vector3.right, 0f) });
                var target = new GameObject("Frustum target");
                target.transform.SetParent(host.transform, false);
                var local = new Bounds(new Vector3(-5f, 0f, 0f), Vector3.one);

                Assert.That(placement.TestAgainstCameraFrustum(target.transform, local), Is.True, "Frustum culling is off by default.");
                Set(config, "m_useCameraFrustum", true);
                Assert.That(placement.TestAgainstCameraFrustum(target.transform, local), Is.False);
                target.transform.position = new Vector3(10f, 0f, 0f);
                Assert.That(placement.TestAgainstCameraFrustum(target.transform, local), Is.True, "The transformed bounds are tested.");
            }
            finally
            {
                planesField.SetValue(null, priorPlanes);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(config);
            }
        }

        // Constructors: knot surfaces store m_endKnot = -1 (0x1A5AD19) and leave
        // m_startKnot 0; every list is List(1); RibbonGrid stores strides
        // {1, 1} (0x29A6C30) and bias {-1, 1} (0x29A81A0); SplineGrid stores
        // strideZ 1 (0x1A60691). Fails if a default or capacity differs.
        [Test]
        public void ConstructorsStoreTheNativeFieldDefaults()
        {
            var host = new GameObject(HostName);
            try
            {
                var splineGrid = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                Assert.That(Get(splineGrid, "m_startKnot"), Is.Zero);
                Assert.That(Get(splineGrid, "m_endKnot"), Is.EqualTo(-1));
                Assert.That(Get(splineGrid, "m_strideZ"), Is.EqualTo(1f));
                Assert.That(Get(splineGrid, "m_scaleStride"), Is.False);
                Assert.That(Get(splineGrid, "m_gapsRatio"), Is.Zero);
                AssertCapacityOne(splineGrid, "m_splines", "m_meshTypes");
                Assert.That(Get(splineGrid, "m_splineInfo"), Is.Null);

                var ribbonGrid = host.AddComponent<ProceduralMesh_PlacementOnRibbonGrid>();
                Assert.That(Get(ribbonGrid, "m_endKnot"), Is.EqualTo(-1));
                Assert.That(Get(ribbonGrid, "m_strideZ"), Is.EqualTo(1f));
                Assert.That(Get(ribbonGrid, "m_strideX"), Is.EqualTo(1f));
                Assert.That(Get(ribbonGrid, "m_biasMin"), Is.EqualTo(-1f));
                Assert.That(Get(ribbonGrid, "m_biasMax"), Is.EqualTo(1f));
                Assert.That(Get(ribbonGrid, "m_biasType"), Is.EqualTo(LocationGenerator.BiasType.WidthRatio));
                AssertCapacityOne(ribbonGrid, "m_ribbons", "m_meshTypes");

                var ribbonRandom = host.AddComponent<ProceduralMesh_PlacementOnRibbonRandom>();
                Assert.That(Get(ribbonRandom, "m_numMeshesType"), Is.EqualTo(NumMeshesType.PerKnot));
                Assert.That(Get(ribbonRandom, "m_numMeshes"), Is.Zero);
                AssertCapacityOne(ribbonRandom, "m_ribbons", "m_meshTypes");

                var splineRandom = host.AddComponent<ProceduralMesh_PlacementOnSplineRandom>();
                Assert.That(Get(splineRandom, "m_numMeshes"), Is.Zero);
                AssertCapacityOne(splineRandom, "m_splines", "m_meshTypes");

                var meshRandom = host.AddComponent<ProceduralMesh_PlacementOnMeshRandom>();
                Assert.That(Get(meshRandom, "m_numMeshes"), Is.Zero);
                Assert.That(Get(meshRandom, "m_scaleNumMeshes"), Is.False);
                AssertCapacityOne(meshRandom, "m_meshColliders", "m_meshTypes");
                Assert.That(Get(meshRandom, "m_storageInfo"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // Reset bodies: the surface base adds a same-object Spline (0x1A5EE40)
        // or MeshCollider (0x1A5ADD0) only when present; each leaf then appends
        // one new mesh type and sets its seed to -1: the Reset sites store
        // 0x3f800000ffffffff over seed and ratio (0x1A5D9D6, 0x1A60386,
        // 0x1A60896, 0x1A5E06B), where the constructors alone store
        // 0x3f80000000000000, seed 0 (TypeGrid 0x1A5DA50, TypeRandom 0x1A60910,
        // TypeRibbonRandom 0x1A5E0E0). Ribbon-random types keep their
        // constructor bias {-1, 1} with WidthRatio/Even (0x1A5E106). Fails if a
        // missing component is added, a component is skipped, the seed is left
        // at the constructor's value, or a type or default differs.
        [Test]
        public void ResetAddsPresentSurfacesAndOneDefaultMeshType()
        {
            var host = new GameObject(HostName);
            try
            {
                var splineGrid = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                var splines = (List<Spline>)Get(splineGrid, "m_splines");
                splines.Clear();
                var gridTypes = (List<ProceduralMesh_TypeGrid>)Get(splineGrid, "m_meshTypes");
                gridTypes.Clear();
                Invoke(splineGrid, "Reset");
                Assert.That(splines, Is.Empty, "No Spline component is present yet.");
                Assert.That(gridTypes.Count, Is.EqualTo(1));
                AssertDefaultMeshType(gridTypes[0], typeof(ProceduralMesh_TypeGrid));

                Spline spline = host.AddComponent<Spline>();
                Invoke(splineGrid, "Reset");
                Assert.That(splines, Is.EqualTo(new[] { spline }));
                Assert.That(gridTypes.Count, Is.EqualTo(2));

                var splineRandom = host.AddComponent<ProceduralMesh_PlacementOnSplineRandom>();
                var randomTypes = (List<ProceduralMesh_TypeRandom>)Get(splineRandom, "m_meshTypes");
                randomTypes.Clear();
                Invoke(splineRandom, "Reset");
                Assert.That(randomTypes.Count, Is.EqualTo(1));
                AssertDefaultMeshType(randomTypes[0], typeof(ProceduralMesh_TypeRandom));

                var ribbonRandom = host.AddComponent<ProceduralMesh_PlacementOnRibbonRandom>();
                var ribbons = (List<Ribbon>)Get(ribbonRandom, "m_ribbons");
                ribbons.Clear();
                var ribbonTypes = (List<ProceduralMesh_TypeRibbonRandom>)Get(ribbonRandom, "m_meshTypes");
                ribbonTypes.Clear();
                Invoke(ribbonRandom, "Reset");
                Assert.That(ribbons, Is.Empty);
                Assert.That(ribbonTypes.Count, Is.EqualTo(1));
                ProceduralMesh_TypeRibbonRandom ribbonType = ribbonTypes[0];
                AssertDefaultMeshType(ribbonType, typeof(ProceduralMesh_TypeRibbonRandom));
                Assert.That(ribbonType.BiasMin, Is.EqualTo(-1f));
                Assert.That(ribbonType.BiasMax, Is.EqualTo(1f));
                Assert.That(ribbonType.BiasType, Is.EqualTo(LocationGenerator.BiasType.WidthRatio));
                Assert.That(ribbonType.BiasDensity, Is.EqualTo(LocationGenerator.BiasDensity.Even));
                Assert.That(ribbonType.InverseBias, Is.False);

                var meshRandom = host.AddComponent<ProceduralMesh_PlacementOnMeshRandom>();
                var colliders = (List<MeshCollider>)Get(meshRandom, "m_meshColliders");
                colliders.Clear();
                Invoke(meshRandom, "Reset");
                Assert.That(colliders, Is.Empty);
                MeshCollider meshCollider = host.AddComponent<MeshCollider>();
                Invoke(meshRandom, "Reset");
                Assert.That(colliders, Is.EqualTo(new[] { meshCollider }));
                Assert.That(((IList)Get(meshRandom, "m_meshTypes")).Count, Is.Zero, "The mesh placement adds no mesh type.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // GetErrorMessages for splines 0x060000e1 (0x1A5EF30) then grid
        // 0x060000ed (0x1A60270): missing or null splines, a ratio failing
        // `MaxRatio > 0` (ucomiss/jbe, so NaN fails) and a stride passing
        // `strideZ <= float.Epsilon` (0x29A6BF0, so NaN passes) add messages
        // in that order; nothing to report returns null. Truth tables separate
        // `<= 0` and `< Epsilon` alternatives.
        [Test]
        public void SplineGridErrorsFollowTheNativeOrderAndPredicates()
        {
            var host = new GameObject(HostName);
            try
            {
                var grid = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                var splines = (List<Spline>)Get(grid, "m_splines");
                splines.Clear();
                ((IList)Get(grid, "m_meshTypes")).Clear();

                Configure(grid, 0f);
                Set(grid, "m_strideZ", 0f);
                Assert.That(Errors(grid), Is.EqualTo(new[]
                {
                    "'" + HostName + "' has no spline assigned",
                    "'" + HostName + RatioMessage,
                    "'" + HostName + "' has invalid strideZ 0",
                }));

                Spline spline = host.AddComponent<Spline>();
                splines.Add(spline);
                Configure(grid, 1f);
                Set(grid, "m_strideZ", 1f);
                Assert.That(Errors(grid), Is.Null, "A clean setup allocates no list.");

                splines.Add(null);
                Assert.That(Errors(grid), Is.EqualTo(new[] { "'" + HostName + "' has no spline assigned" }), "Any null entry fails.");
                splines.RemoveAt(1);

                foreach (KeyValuePair<float, bool> ratio in new Dictionary<float, bool>
                {
                    { 1f, false }, { float.Epsilon, false }, { float.PositiveInfinity, false },
                    { 0f, true }, { -1f, true }, { float.NaN, true },
                })
                {
                    Configure(grid, ratio.Key);
                    Assert.That(Errors(grid) != null, Is.EqualTo(ratio.Value), "MaxRatio " + ratio.Key);
                }

                Configure(grid, 1f);
                foreach (KeyValuePair<float, bool> stride in new Dictionary<float, bool>
                {
                    { 1f, false }, { float.Epsilon * 2f, false }, { float.NaN, false },
                    { float.Epsilon, true }, { 0f, true }, { -1f, true },
                })
                {
                    Set(grid, "m_strideZ", stride.Key);
                    List<string> errors = Errors(grid);
                    Assert.That(errors != null, Is.EqualTo(stride.Value), "strideZ " + stride.Key);
                    if (errors != null)
                        Assert.That(errors, Is.EqualTo(new[] { string.Format("'{0}' has invalid strideZ {1}", HostName, stride.Key) }));
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // GetErrorMessages for ribbons 0x060000be (0x1A5C2F0) then random
        // 0x060000d9 (0x1A5DF50): a null ribbon entry is reported, and Num
        // Meshes is rejected only when `0 > numMeshes` (ucomiss/jbe, so NaN
        // and zero pass). Fails if zero, NaN or a positive count is rejected,
        // or a negative one passes.
        [Test]
        public void RibbonRandomErrorsRejectOnlyNegativeMeshCounts()
        {
            var host = new GameObject(HostName);
            try
            {
                var random = host.AddComponent<ProceduralMesh_PlacementOnRibbonRandom>();
                var ribbons = (List<Ribbon>)Get(random, "m_ribbons");
                ribbons.Clear();
                ribbons.Add(null);
                ((IList)Get(random, "m_meshTypes")).Clear();
                Configure(random, 1f);
                string ribbonMessage = "'" + HostName + "' has no ribbon assigned";

                foreach (KeyValuePair<float, bool> count in new Dictionary<float, bool>
                {
                    { 5f, false }, { 0f, false }, { float.NaN, false },
                    { -1f, true }, { -float.Epsilon, true },
                })
                {
                    Set(random, "m_numMeshes", count.Key);
                    var expected = new List<string> { ribbonMessage };
                    if (count.Value)
                        expected.Add(string.Format("'{0}' has invalid Num Meshes {1}", HostName, count.Key));
                    Assert.That(Errors(random), Is.EqualTo(expected), "numMeshes " + count.Key);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // GetErrorMessages for meshes 0x060000b3 (0x1A5AEC0): an empty or
        // null collider list is reported with the mesh wording, and a present
        // collider with a positive ratio reports nothing.
        [Test]
        public void MeshRandomErrorsReportOnlyMissingMeshesAndRatios()
        {
            var host = new GameObject(HostName);
            try
            {
                var placement = host.AddComponent<ProceduralMesh_PlacementOnMeshRandom>();
                var colliders = (List<MeshCollider>)Get(placement, "m_meshColliders");
                colliders.Clear();
                ((IList)Get(placement, "m_meshTypes")).Clear();
                Configure(placement, 0f);
                Assert.That(Errors(placement), Is.EqualTo(new[] { "'" + HostName + "' has no mesh assigned", "'" + HostName + RatioMessage }));
                colliders.Add(host.AddComponent<MeshCollider>());
                Configure(placement, 0.5f);
                Assert.That(Errors(placement), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // HasValidSetup 0x06000090 (0x1A592A0): ValidSetup becomes whether the
        // error list is null or empty, recomputed on every call, and is stored
        // (0x1A592F8) only after OutputErrors 0x060000a0 (call 0x1A592E7).
        // OutputErrors returns while the old ValidSetup is false (0x1A5936C), so
        // only the first failure after a valid state is reported: one LogError
        // (code 0, 0x1A59561) of "'" + path + "' game object has invalid
        // procedural mesh setup:" (0x1A593F3/0x1A593FD) and "\n- " (0x1A5941B)
        // before each message. Fails if OutputErrors is dropped, runs after the
        // store, reports every failure, or a failure sticks.
        [Test]
        public void HasValidSetupRecomputesValidityFromTheErrorList()
        {
            using var log = new OwnedCoreErrorLog();
            var host = new GameObject(HostName);
            try
            {
                var grid = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                var splines = (List<Spline>)Get(grid, "m_splines");
                splines.Clear();
                ((IList)Get(grid, "m_meshTypes")).Clear();
                Configure(grid, 0f);
                Set(grid, "m_strideZ", 0f);
                SetProperty(grid, typeof(ProceduralMesh_Placement), "ValidSetup", true);
                Assert.That(grid.HasValidSetup(), Is.False);
                Assert.That(log.Errors, Is.EqualTo(new[]
                {
                    "'/" + HostName + "' game object has invalid procedural mesh setup:" +
                    "\n- '" + HostName + "' has no spline assigned" +
                    "\n- '" + HostName + RatioMessage +
                    "\n- '" + HostName + "' has invalid strideZ 0:0",
                }));
                log.Errors.Clear();
                Assert.That(grid.HasValidSetup(), Is.False);
                Assert.That(log.Errors, Is.Empty, "A setup that was already invalid reports nothing.");

                splines.Add(host.AddComponent<Spline>());
                Configure(grid, 1f);
                Set(grid, "m_strideZ", 1f);
                Assert.That(grid.HasValidSetup(), Is.True);
                Assert.That(ValidSetup(grid), Is.True);
                Set(grid, "m_strideZ", 0f);
                Assert.That(grid.HasValidSetup(), Is.False);
                Assert.That(ValidSetup(grid), Is.False);
                Assert.That(log.Errors, Is.EqualTo(new[]
                {
                    "'/" + HostName + "' game object has invalid procedural mesh setup:\n- '" + HostName + "' has invalid strideZ 0:0",
                }));
                Set(grid, "m_strideZ", 1f);
                Assert.That(grid.HasValidSetup(), Is.True, "Validity is recomputed, not latched.");
                Assert.That(log.Errors.Count, Is.EqualTo(1), "A valid setup reports nothing.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // DoUpdate 0x0600009c (0x1A5A340): an invalid setup calls
        // DespawnAllMeshes(false, true, false) (0x1A5A416); each surface
        // override 0x060000b5/0x060000c0/0x060000e3 returns at once for a zero
        // count or null storage, else zeroes the count and sums its storages.
        // Fails if DoUpdate skips the despawn, or a null storage still resets.
        [Test]
        public void InvalidDoUpdateDespawnsOnlyWhenStorageExists()
        {
            var host = new GameObject(HostName);
            try
            {
                foreach (ProceduralMesh_Placement placement in Placements(host))
                {
                    string storage = StorageField(placement);
                    Set(placement, "m_populatedCount", 3);
                    placement.DoUpdate();
                    Assert.That(Get(placement, "m_populatedCount"), Is.EqualTo(3), placement.GetType().Name + " without storage keeps its count.");
                    Set(placement, storage, EmptyStorage(placement));
                    placement.DoUpdate();
                    Assert.That(Get(placement, "m_populatedCount"), Is.Zero, placement.GetType().Name + " despawns its empty storage.");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // CheckForSpawnAndDespawn 0x060000b7/0x060000c1/0x060000e4: null
        // storage clears ValidSetup and logs once with code 0; empty storage
        // zeroes the count, keeps the setup valid and logs nothing. The texts
        // are original, quirks included: ribbon and mesh log "'" + path +
        // " has invalid procedural mesh setup'" (literal 0x32812a8, used at
        // 0x1A5CBA0 and 0x1A5BB40), the spline "'" + path + "' has invalid
        // procedural mesh setup -  " with two trailing spaces (0x3281bc8,
        // used at 0x1A5F770). CalculateTotalWorldSpaceBounds 0x060000b8/
        // 0x060000c2/0x060000e6 return the zeroed bounds (0x1A5CF7F) for no storage.
        [Test]
        public void MissingStorageInvalidatesSpawnChecksAndEmptyStorageHasZeroBounds()
        {
            using var log = new OwnedCoreErrorLog();
            var host = new GameObject(HostName);
            try
            {
                foreach (ProceduralMesh_Placement placement in Placements(host))
                {
                    string name = placement.GetType().Name;
                    string expected = placement is ProceduralMesh_PlacementOnSpline
                        ? "'/" + HostName + "' has invalid procedural mesh setup -  :0"
                        : "'/" + HostName + " has invalid procedural mesh setup':0";
                    SetProperty(placement, typeof(ProceduralMesh_Placement), "ValidSetup", true);
                    Invoke(placement, "CheckForSpawnAndDespawn", null, false);
                    Assert.That(ValidSetup(placement), Is.False, name);
                    Assert.That(log.Errors, Is.EqualTo(new[] { expected }), name);
                    log.Errors.Clear();

                    Set(placement, StorageField(placement), EmptyStorage(placement));
                    SetProperty(placement, typeof(ProceduralMesh_Placement), "ValidSetup", true);
                    Set(placement, "m_populatedCount", 7);
                    Invoke(placement, "CheckForSpawnAndDespawn", null, true);
                    Assert.That(ValidSetup(placement), Is.True, name);
                    Assert.That(Get(placement, "m_populatedCount"), Is.Zero, name);
                    Assert.That(log.Errors, Is.Empty, name);
                    var bounds = (Bounds)Invoke(placement, "CalculateTotalWorldSpaceBounds");
                    Assert.That(bounds.center, Is.EqualTo(Vector3.zero), name);
                    Assert.That(bounds.extents, Is.EqualTo(Vector3.zero), name);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // DoUpdate 0x0600009c (0x1A5A340) with a valid setup and a camera:
        // `ucomiss sqr, DespawnDistanceSquare; jbe` (0x1A5A3E2/0x1A5A3E6)
        // despawns only for an ordered sqr > despawn, so equality and NaN both
        // reach CheckForSpawnAndDespawn (0x1A5A454). A spline grid with null
        // storage shows the branch: the spawn check clears ValidSetup and logs,
        // while DespawnAllMeshes returns at once and keeps the count. Fails for
        // `>=` (the equal case) or `!(sqr <= despawn)` (the NaN case).
        // Not observable without storage bodies or a recording subclass: the
        // `m_populatedCount > 0` gate (0x1A5A3EC) and canFade = FadeOut > sqr
        // (0x1A5A3F5/0x1A5A3F8), since the override itself returns for a zero count.
        [Test]
        public void DoUpdateDespawnsOnlyStrictlyBeyondTheDespawnDistance()
        {
            FieldInfo frameField = typeof(ProceduralMesh_Placement).GetField("m_frameCount", Own);
            FieldInfo cameraField = typeof(ProceduralMesh_Placement).GetField("m_camera", Own);
            FieldInfo planesField = typeof(ProceduralMesh_Placement).GetField("m_cameraPlanes", Own);
            object priorFrame = frameField.GetValue(null), priorCamera = cameraField.GetValue(null), priorPlanes = planesField.GetValue(null);
            using var log = new OwnedCoreErrorLog();
            var host = new GameObject(HostName);
            var config = ScriptableObject.CreateInstance<ProceduralMesh_SystemConfig>();
            GameObject ownedCamera = null;
            try
            {
                // Borrow an existing main camera read-only; otherwise own one.
                Camera main = Camera.main;
                if (main == null)
                {
                    ownedCamera = new GameObject("Owned placement main camera");
                    ownedCamera.tag = "MainCamera";
                    main = ownedCamera.AddComponent<Camera>();
                }
                Assert.That(Camera.main, Is.SameAs(main));

                var grid = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                SetProperty(grid, typeof(ProceduralMesh_Placement), "SystemConfig", config);
                Vector3 eye = main.transform.position;
                var total = new Bounds(eye - new Vector3(10f, 0f, 0f), Vector3.zero);
                SetProperty(grid, typeof(ProceduralMesh_Placement), "TotalBounds", total);
                float sqrDistance = total.SqrDistance(eye);
                string spawnCheckError = "'/" + HostName + "' has invalid procedural mesh setup -  :0";

                foreach ((float despawn, int populated, bool spawnCheck) in new[]
                {
                    (sqrDistance, 3, true),
                    (sqrDistance - 1f, 3, false),
                    (float.NaN, 3, true),
                    (sqrDistance - 1f, 0, false),
                })
                {
                    string label = "despawn " + despawn + " populated " + populated;
                    frameField.SetValue(null, -1);
                    SetProperty(grid, typeof(ProceduralMesh_Placement), "ValidSetup", true);
                    SetProperty(grid, typeof(ProceduralMesh_Placement), "DespawnDistanceSquare", despawn);
                    Set(grid, "m_populatedCount", populated);
                    log.Errors.Clear();
                    grid.DoUpdate();
                    Assert.That(cameraField.GetValue(null), Is.SameAs(main), label);
                    Assert.That(ValidSetup(grid), Is.EqualTo(!spawnCheck), label);
                    Assert.That(log.Errors, Is.EqualTo(spawnCheck ? new[] { spawnCheckError } : new string[0]), label);
                    Assert.That(Get(grid, "m_populatedCount"), Is.EqualTo(populated), label);
                }
            }
            finally
            {
                frameField.SetValue(null, priorFrame);
                cameraField.SetValue(null, priorCamera);
                planesField.SetValue(null, priorPlanes);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(config);
                if (ownedCamera != null) Object.DestroyImmediate(ownedCamera);
            }
        }

        // InitialiseSetup 0x06000097 (0x1A596B0): stores ValidSetup true
        // (0x1A59734), takes the configuration from SystemConfiguration
        // (0x1A59742), resets MaxRatio (0x1A59757), adds one SetupUpdated
        // handler to OnUpdated (0x1A5996E), then squares through Math.Pow the
        // GetSetup distances Spawn (0x1A599F6), Spawn + Extra (0x1A59A1E),
        // Spawn - Fade (0x1A59A46) and Spawn + Extra + Fade (0x1A59A74). The
        // spline override 0x060000e2 (0x1A5F2D0) then allocates List(splines)
        // (0x1A5F371) and stops at the failing HasValidSetup, whose report
        // shows the ValidSetup store came first. Profile {10, 2, 3} gives 100,
        // 144, 49 and 225; a swapped term such as (Spawn + Fade)^2 gives 169.
        // SetupUpdated 0x06000098 (0x1A59E20) only removes the handler.
        [Test]
        public void InitialiseSetupSquaresTheProfileDistancesAndSubscribesOnce()
        {
            using var log = new OwnedCoreErrorLog();
            using var systemConfig = new OwnedSystemConfig();
            using var owned = new OwnedSetup();
            var host = new GameObject(HostName);
            try
            {
                ProceduralMesh_PlacementSetup.PerformanceSetup profile = Profile(PerformanceProfile.QualityLevel.Medium, 10f);
                Set(profile, "m_despawnExtraDistance", 2f);
                Set(profile, "m_fadeDistance", 3f);
                owned.Profiles.Add(profile);
                ProceduralMesh_PlacementOnSplineGrid grid = SetUpForInitialise(host, owned.Setup);

                Invoke(grid, "InitialiseSetup");
                AssertDistanceSquares(grid, 100f, 144f, 49f, 225f);
                Assert.That(grid.MaxRatio, Is.Zero, "MaxRatio is reset before the empty mesh type sum.");
                Assert.That(grid.SystemConfig, Is.SameAs(systemConfig.Config));
                Assert.That(Get(grid, "m_splineInfo"), Is.Empty);
                Assert.That(Get(owned.Setup, "m_selectedProfile"), Is.SameAs(profile));
                Assert.That(UpdatedHandlers(owned.Setup), Is.EqualTo(new[] { SetupUpdatedHandler(grid) }));
                Assert.That(ValidSetup(grid), Is.False);
                Assert.That(log.Errors, Is.EqualTo(new[]
                {
                    "'/" + HostName + "' game object has invalid procedural mesh setup:" +
                    "\n- '" + HostName + "' has no spline assigned" +
                    "\n- '" + HostName + RatioMessage + ":0",
                }));

                Invoke(grid, "SetupUpdated", owned.Setup);
                Assert.That(UpdatedHandlers(owned.Setup), Is.Empty);
                AssertDistanceSquares(grid, 100f, 144f, 49f, 225f);
                Assert.That(Get(owned.Setup, "m_selectedProfile"), Is.SameAs(profile), "SetupUpdated leaves the selection.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // InitialiseSetup without an assigned setup creates one
        // (CreateInstance 0x1A598E5, after the op_Equality test 0x1A598D2),
        // whose empty profile list yields the native default {40, 1, 5}: 1600,
        // 1681, 1225 and 2116.
        [Test]
        public void InitialiseSetupCreatesADefaultSetupWhenNoneIsAssigned()
        {
            using var log = new OwnedCoreErrorLog();
            using var systemConfig = new OwnedSystemConfig();
            using var owned = new OwnedSetup(create: false);
            var host = new GameObject(HostName);
            try
            {
                ProceduralMesh_PlacementOnSplineGrid grid = SetUpForInitialise(host, null);
                try
                {
                    Invoke(grid, "InitialiseSetup");
                }
                finally
                {
                    var created = (ProceduralMesh_PlacementSetup)Get(grid, "m_setup");
                    if (created != null) owned.Adopt(created);
                }
                Assert.That(owned.Setup, Is.Not.Null);
                Assert.That(owned.Profiles, Is.Empty);
                AssertDefaultProfile(owned.Setup.GetSetup());
                AssertDistanceSquares(grid, 1600f, 1681f, 1225f, 2116f);
                Assert.That(UpdatedHandlers(owned.Setup), Is.EqualTo(new[] { SetupUpdatedHandler(grid) }));
                Assert.That(log.Errors.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // GetSetup 0x06000102 with a live ScalablePerformance: from the lowest
        // sorted profile, each profile of the sorted copy (r15, 0x1A59C46) is
        // taken when `level > selectedLevel` (jle 0x1A59D2A) and `level <=
        // attribute.Level` (jg 0x1A59D30), and selectedLevel follows it
        // (0x1A59D07). The attribute is the active profile's
        // "ProceduralMeshPlacement" (0x1A59CBA). Authored {High, VeryLow,
        // VeryHigh, Medium}: Medium rejects `<`, High rejects the first match
        // (Medium), VeryHigh the top; no attribute keeps VeryLow.
        // OnScalablePerformanceStartup 0x06000107 (0x1A610C0) subscribes,
        // clears the cache and raises OnUpdated once; OnDestroy 0x06000108
        // (0x1A611F0) removes the subscription.
        [Test]
        public void GetSetupSelectsTheHighestProfileNotAboveTheLiveAttributeLevel()
        {
            using (var owned = new OwnedSetup())
            using (var performance = new OwnedPerformance())
            {
                var high = Profile(PerformanceProfile.QualityLevel.High, 1f);
                var veryLow = Profile(PerformanceProfile.QualityLevel.VeryLow, 2f);
                var veryHigh = Profile(PerformanceProfile.QualityLevel.VeryHigh, 3f);
                var medium = Profile(PerformanceProfile.QualityLevel.Medium, 4f);
                owned.Profiles.AddRange(new[] { high, veryLow, veryHigh, medium });
                Delegate profileHandler = Delegate.CreateDelegate(typeof(Action<PerformanceProfile>), owned.Setup,
                    typeof(ProceduralMesh_PlacementSetup).GetMethod("OnPerformanceProfileUpdated", Own));

                performance.Register();
                Assert.That(owned.Reference.IsValid(), Is.True);
                Assert.That(performance.ProfileHandlers(), Is.EqualTo(new[] { profileHandler }), "Registration runs the startup handler.");

                foreach ((PerformanceProfile.QualityLevel level, ProceduralMesh_PlacementSetup.PerformanceSetup expected) in new[]
                {
                    (PerformanceProfile.QualityLevel.Medium, medium),
                    (PerformanceProfile.QualityLevel.High, high),
                    (PerformanceProfile.QualityLevel.VeryHigh, veryHigh),
                    (PerformanceProfile.QualityLevel.Low, veryLow),
                    (PerformanceProfile.QualityLevel.VeryLow, veryLow),
                })
                {
                    performance.Attribute.Level = level;
                    Set(owned.Setup, "m_selectedProfile", null);
                    Assert.That(owned.Setup.GetSetup(), Is.SameAs(expected), "Attribute level " + level);
                }
                performance.Attributes.Clear();
                Set(owned.Setup, "m_selectedProfile", null);
                Assert.That(owned.Setup.GetSetup(), Is.SameAs(veryLow), "No attribute keeps the lowest profile.");

                Invoke(owned.Setup, "OnDestroy");
                Assert.That(performance.ProfileHandlers(), Is.Empty);

                var senders = new List<ProceduralMesh_PlacementSetup>();
                Action<ProceduralMesh_PlacementSetup> record = senders.Add;
                owned.Setup.OnUpdated += record;
                try
                {
                    Invoke(owned.Setup, "OnScalablePerformanceStartup", performance.Manager);
                }
                finally
                {
                    owned.Setup.OnUpdated -= record;
                }
                Assert.That(senders, Is.EqualTo(new[] { owned.Setup }));
                Assert.That(Get(owned.Setup, "m_selectedProfile"), Is.Null);
                Assert.That(performance.ProfileHandlers(), Is.EqualTo(new[] { profileHandler }));
                Invoke(owned.Setup, "OnDestroy");
                Assert.That(performance.ProfileHandlers(), Is.Empty);
            }
        }

        // Start 0x06000096 (0x1A59590) runs InitialiseSetup (vcall 0x1A595CE)
        // then GetSystem<ProceduralMesh_PlacementManager>().RegisterInstance.
        // OnDestroy 0x06000099 (0x1A59FD0) removes SetupUpdated only for a
        // live setup (op_Inequality 0x1A5A054), calls DespawnAllMeshes(true,
        // true, false) (0x1A5A126..0x1A5A132) and unregisters through
        // GetSystemSafe (0x1A5A161), skipping a missing manager (0x1A5A169).
        // Fails if registration, the despawn, the handler removal or the
        // unregister is dropped. The despawn's flag values reach only storage
        // bodies, so they are not observable here.
        [Test]
        public void StartRegistersAndOnDestroyUnregistersTheDespawnedPlacement()
        {
            using var log = new OwnedCoreErrorLog();
            using var systemConfig = new OwnedSystemConfig();
            using var owned = new OwnedSetup();
            var host = new GameObject(HostName);
            var managerRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<ProceduralMesh_PlacementManager>());
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Update);
            ProceduralMesh_PlacementManager manager = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                manager = new ProceduralMesh_PlacementManager();
                managerRow.Register(manager);
                ProceduralMesh_PlacementOnSplineGrid grid = SetUpForInitialise(host, owned.Setup);

                Invoke(grid, "Start");
                Assert.That(manager.Placements, Is.EqualTo(new ProceduralMesh_Placement[] { grid }));
                Assert.That(Get(grid, "m_splineInfo"), Is.Empty);
                Assert.That(UpdatedHandlers(owned.Setup), Is.EqualTo(new[] { SetupUpdatedHandler(grid) }));

                Set(grid, "m_populatedCount", 2);
                Invoke(grid, "OnDestroy");
                Assert.That(Get(grid, "m_populatedCount"), Is.Zero);
                Assert.That(manager.Placements, Is.Empty);
                Assert.That(UpdatedHandlers(owned.Setup), Is.Empty);
            },
            () => { if (manager != null) ProcessManager.UnsubscribeFromAllActions(manager); },
            managerRow.Dispose,
            () => Object.DestroyImmediate(host),
            actionRows.Dispose);
        }

        // OnDestroy without a registered manager: GetSystemSafe returns null and
        // nothing is unregistered; a null setup skips the handler removal.
        [Test]
        public void OnDestroyToleratesAMissingManagerAndSetup()
        {
            using var managerRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<ProceduralMesh_PlacementManager>());
            using var owned = new OwnedSetup();
            var host = new GameObject(HostName);
            try
            {
                var withSetup = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                Set(withSetup, "m_setup", owned.Setup);
                Set(withSetup, "m_splineInfo", new List<ProceduralMesh_StorageForSpline>());
                Set(withSetup, "m_populatedCount", 2);
                owned.Setup.OnUpdated += (Action<ProceduralMesh_PlacementSetup>)SetupUpdatedHandler(withSetup);
                Assert.DoesNotThrow(() => Invoke(withSetup, "OnDestroy"));
                Assert.That(UpdatedHandlers(owned.Setup), Is.Empty);
                Assert.That(Get(withSetup, "m_populatedCount"), Is.Zero);

                var withoutSetup = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                Assert.That(Get(withoutSetup, "m_setup"), Is.Null);
                Assert.DoesNotThrow(() => Invoke(withoutSetup, "OnDestroy"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // Ribbon CalculateBounds 0x060000c3 (0x1A5D1D0): starts from the start
        // knot's centre with zero size, resolves endKnot -1 to KnotCount - 1,
        // then encapsulates only the Left and Right knots (GetKnot alignment
        // -1 and 1) of every knot in range; an end before the start returns
        // the start point. Knot 1's centre at y = 100 is a negative control.
        [Test]
        public void RibbonBoundsEncapsulateTheStartCentreAndEveryEdgeKnot()
        {
            var host = new GameObject(HostName);
            try
            {
                var placement = host.AddComponent<ProceduralMesh_PlacementOnRibbonGrid>();
                var ribbon = new FakeRibbon(
                    (new Vector3(0f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f)),
                    (new Vector3(0f, 100f, 5f), new Vector3(-2f, 0f, 5f), new Vector3(2f, 0f, 5f)),
                    (new Vector3(0f, 0f, 10f), new Vector3(-3f, 1f, 10f), new Vector3(3f, -1f, 10f)));

                Bounds all = placement.CalculateBounds(ribbon, 0, -1);
                AssertVector(all.min, new Vector3(-3f, -1f, 0f));
                AssertVector(all.max, new Vector3(3f, 1f, 10f));
                Assert.That(ribbon.Calls, Is.EqualTo(new[]
                {
                    "0:Center", "count", "0:Left", "0:Right", "1:Left", "1:Right", "2:Left", "2:Right",
                }));

                ribbon.Calls.Clear();
                Bounds single = placement.CalculateBounds(ribbon, 1, 1);
                AssertVector(single.min, new Vector3(-2f, 0f, 5f));
                AssertVector(single.max, new Vector3(2f, 100f, 5f));
                Assert.That(ribbon.Calls, Is.EqualTo(new[] { "1:Center", "1:Left", "1:Right" }));

                ribbon.Calls.Clear();
                Bounds reversed = placement.CalculateBounds(ribbon, 2, 1);
                AssertVector(reversed.center, new Vector3(0f, 0f, 10f));
                AssertVector(reversed.extents, Vector3.zero);
                Assert.That(ribbon.Calls, Is.EqualTo(new[] { "2:Center" }));
                Assert.Throws<InvalidCastException>(() => placement.CalculateBounds(new FakeSpline(), 0, 0));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // Spline CalculateBounds 0x060000e5 (0x1A5F9D0): the start knot with zero
        // size, endKnot -1 resolved through KnotCount, then every knot of the
        // range. Fails if the end knot is excluded or knots past it included.
        [Test]
        public void SplineBoundsEncapsulateEveryKnotOfTheRange()
        {
            var host = new GameObject(HostName);
            try
            {
                var placement = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                var spline = new FakeSpline(new Vector3(0f, 0f, 0f), new Vector3(4f, -2f, 1f), new Vector3(-6f, 8f, 3f));
                Bounds all = placement.CalculateBounds(spline, 0, -1);
                AssertVector(all.min, new Vector3(-6f, -2f, 0f));
                AssertVector(all.max, new Vector3(4f, 8f, 3f));
                Assert.That(spline.Calls, Is.EqualTo(new[] { "0", "count", "0", "1", "2" }));

                Bounds firstTwo = placement.CalculateBounds(spline, 0, 1);
                AssertVector(firstTwo.min, new Vector3(0f, -2f, 0f));
                AssertVector(firstTwo.max, new Vector3(4f, 0f, 1f));

                Bounds reversed = placement.CalculateBounds(spline, 2, 0);
                AssertVector(reversed.center, new Vector3(-6f, 8f, 3f));
                AssertVector(reversed.extents, Vector3.zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // CalcSplineArea 0x060000fa (0x1A60940): sums GetKnot(i).Length for
        // StartSubSurface <= i < EndSubSurface. Lengths {1, 2, 4, 8} over
        // [1, 3) give 6; an inclusive end would give 14.
        [Test]
        public void SplineAreaSumsKnotLengthsWithAnExclusiveEnd()
        {
            var spline = new FakeSpline(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero);
            float[] lengths = { 1f, 2f, 4f, 8f };
            for (int i = 0; i < lengths.Length; ++i)
                spline.Knots[i].Length = lengths[i];
            Assert.That(SplineArea(spline, 1, 3), Is.EqualTo(6f));
            Assert.That(spline.Calls, Is.EqualTo(new[] { "1", "2" }));
            Assert.That(SplineArea(spline, 2, 2), Is.Zero);
            Assert.That(SplineArea(spline, 3, 1), Is.Zero);
        }

        // CalculateRibbonArea 0x060000db (0x1A5E120): per span i the mean of
        // the centre-to-left distances of knots i and i + 1, plus the mean of
        // the centre-to-right ones, each times knot i's Length (IRibbonKnot
        // slot 8). Widths left {1, 3, 5}, right {2, 2, 6}, lengths {10, 20, 40}:
        // [0, 2) = 40 + 160 = 200. Using knot i + 1's length gives 400; unhalved
        // full widths give 400 too.
        [Test]
        public void RibbonAreaIsTheTrapezoidSumOfEdgeWidthsTimesSpanLengths()
        {
            var ribbon = new FakeRibbon(
                (new Vector3(0f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(2f, 0f, 0f)),
                (new Vector3(0f, 0f, 10f), new Vector3(-3f, 0f, 10f), new Vector3(2f, 0f, 10f)),
                (new Vector3(0f, 0f, 20f), new Vector3(-5f, 0f, 20f), new Vector3(6f, 0f, 20f)));
            float[] lengths = { 10f, 20f, 40f };
            for (int i = 0; i < lengths.Length; ++i)
                ribbon.Centres[i].Length = lengths[i];

            Assert.That(RibbonArea(ribbon, 0, 2), Is.EqualTo(200f));
            Assert.That(ribbon.Calls, Is.EqualTo(new[]
            {
                "0:Center", "0:Left", "0:Right", "1:Center", "1:Left", "1:Right", "2:Center", "2:Left", "2:Right",
            }));
            Assert.That(RibbonArea(ribbon, 1, 2), Is.EqualTo(160f));
            Assert.That(RibbonArea(ribbon, 2, 2), Is.Zero);
        }

        // ProceduralMesh_PlacementManager..ctor 0x060000a6 (0x1A5A900) subscribes
        // OnUpdate to SystemAction.Update (5); RegisterInstance appends one
        // (0x1A59620) or a range (0x1A5AA20); UnregisterInstance removes the
        // first match (0x1A5A1D0); OnUpdate 0x060000ab runs DoUpdate on every
        // placement in order. Fails if the subscription, append order, first-
        // match removal or update dispatch differs.
        [Test]
        public void ManagerSubscribesToUpdateAndDispatchesDoUpdateInOrder()
        {
            var host = new GameObject(HostName);
            ProceduralMesh_PlacementManager manager = null;
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Update);
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                manager = new ProceduralMesh_PlacementManager();
                var lookup = (IDictionary)typeof(ProcessManager).GetField("s_systemActionLookup", Own).GetValue(null);
                var updateRow = (Dictionary<ISystem, Action<object>>)lookup[SystemAction.Update];
                Assert.That(updateRow.TryGetValue(manager, out Action<object> callback), Is.True);
                Assert.That(callback.Target, Is.SameAs(manager));
                Assert.That(callback.Method.Name, Is.EqualTo("OnUpdate"));
                Assert.That(manager.Placements, Is.Empty);

                var first = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                var second = host.AddComponent<ProceduralMesh_PlacementOnMeshRandom>();
                manager.RegisterInstance(first);
                manager.RegisterInstance(new List<ProceduralMesh_Placement> { second, first });
                Assert.That(manager.Placements, Is.EqualTo(new ProceduralMesh_Placement[] { first, second, first }));
                manager.UnregisterInstance(first);
                Assert.That(manager.Placements, Is.EqualTo(new ProceduralMesh_Placement[] { second, first }));
                manager.UnregisterInstance(host.AddComponent<ProceduralMesh_PlacementOnRibbonGrid>());
                Assert.That(manager.Placements, Is.EqualTo(new ProceduralMesh_Placement[] { second, first }));

                foreach (ProceduralMesh_Placement placement in new ProceduralMesh_Placement[] { first, second })
                {
                    Set(placement, StorageField(placement), EmptyStorage(placement));
                    Set(placement, "m_populatedCount", 2);
                }
                callback(null);
                Assert.That(Get(first, "m_populatedCount"), Is.Zero);
                Assert.That(Get(second, "m_populatedCount"), Is.Zero);
            },
            () => { if (manager != null) ProcessManager.UnsubscribeFromAllActions(manager); },
            () => Object.DestroyImmediate(host),
            actionRows.Dispose);
        }

        // An owned placement setup admitted against an absent or idle
        // ScalablePerformance row; release removes this setup's handlers. With
        // create false, a setup the proof creates under the lease is adopted.
        private sealed class OwnedSetup : IDisposable
        {
            private readonly VerificationFixtureOwnership.LifecycleRow m_lease;

            public OwnedSetup(bool create = true)
            {
                m_lease = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<ScalablePerformance>());
                if (create) Adopt(ScriptableObject.CreateInstance<ProceduralMesh_PlacementSetup>());
            }

            public ProceduralMesh_PlacementSetup Setup { get; private set; }
            public Delegate Startup { get; private set; }
            public Delegate Shutdown { get; private set; }

            public void Adopt(ProceduralMesh_PlacementSetup setup)
            {
                Setup = setup;
                Startup = Bound(setup, "OnScalablePerformanceStartup");
                Shutdown = Bound(setup, "OnScalablePerformanceShutdown");
            }

            public SystemRef<ScalablePerformance> Reference => (SystemRef<ScalablePerformance>)Get(Setup, "m_scalablePerformanceRef");

            public List<ProceduralMesh_PlacementSetup.PerformanceSetup> Profiles =>
                (List<ProceduralMesh_PlacementSetup.PerformanceSetup>)Get(Setup, "m_profiles");

            public Delegate[] Handlers(string eventField)
            {
                var value = (Delegate)typeof(SystemRef<ScalablePerformance>).GetField(eventField, Own).GetValue(Reference);
                return value == null ? new Delegate[0] : value.GetInvocationList();
            }

            private static Delegate Bound(ProceduralMesh_PlacementSetup setup, string method) =>
                Delegate.CreateDelegate(typeof(Action<ScalablePerformance>), setup, typeof(ProceduralMesh_PlacementSetup).GetMethod(method, Own));

            public void Dispose()
            {
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    if (Setup == null) return;
                    SystemRef<ScalablePerformance> reference = Reference;
                    if (reference == null) return;
                    reference.OnSystemStartup -= (Action<ScalablePerformance>)Startup;
                    reference.OnSystemShutdown -= (Action<ScalablePerformance>)Shutdown;
                },
                () => { if (Setup != null) Object.DestroyImmediate(Setup); },
                m_lease.Dispose);
            }
        }

        // An inactive original ScalablePerformance whose active profile holds a
        // "ProceduralMeshPlacement" attribute. Register publishes it in the
        // ScalablePerformance row an OwnedSetup leases; release unregisters it
        // first, which runs the setups' own shutdown handlers.
        private sealed class OwnedPerformance : IDisposable
        {
            public readonly GameObject Host;
            public readonly ScalablePerformance Manager;
            public readonly PerformanceProfile Profile;
            public readonly ScalableFeature Feature;
            public readonly PerformanceAttribute Attribute;
            public readonly List<PerformanceAttribute> Attributes;
            private bool m_registered;

            public OwnedPerformance()
            {
                try
                {
                    Host = new GameObject("Owned placement performance manager");
                    Host.SetActive(false);
                    Manager = Host.AddComponent<ScalablePerformance>();
                    Profile = ScriptableObject.CreateInstance<PerformanceProfile>();
                    Feature = ScriptableObject.CreateInstance<ScalableFeature>();
                    Feature.ID = "ProceduralMeshPlacement";
                    Attribute = new PerformanceAttribute { Feature = Feature, Level = PerformanceProfile.QualityLevel.Medium };
                    Attributes = (List<PerformanceAttribute>)Get(Profile, "m_attributes");
                    Attributes.Add(Attribute);
                    Set(Manager, "m_activeProfile", Profile);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Register()
            {
                m_registered = true;
                ProcessManager.RegisterSystem(Manager);
            }

            public Delegate[] ProfileHandlers()
            {
                var value = (Delegate)Get(Manager, "OnProfileUpdated");
                return value == null ? new Delegate[0] : value.GetInvocationList();
            }

            public void Dispose()
            {
                VerificationFixtureOwnership.RunThenCleanUp(() => { if (m_registered) ProcessManager.UnregisterSystem(Manager); },
                    () => { if (Host != null) Object.DestroyImmediate(Host); },
                    () => { if (Feature != null) Object.DestroyImmediate(Feature); },
                    () => { if (Profile != null) Object.DestroyImmediate(Profile); });
            }
        }

        // Leases the default SystemConfiguration row (absent or idle) and
        // publishes a fresh original system whose stack holds one owned
        // ProceduralMesh_SystemConfig; the base-typed AddConfig matches the
        // container GetConfig reads. Release unregisters before the lease.
        private sealed class OwnedSystemConfig : IDisposable
        {
            private readonly VerificationFixtureOwnership.LifecycleRow m_lease;
            private readonly SystemConfiguration m_system = new SystemConfiguration();
            public readonly ProceduralMesh_SystemConfig Config;

            public OwnedSystemConfig()
            {
                m_lease = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<SystemConfiguration>());
                try
                {
                    ProcessManager.RegisterSystem(m_system);
                    Config = ScriptableObject.CreateInstance<ProceduralMesh_SystemConfig>();
                    SystemConfiguration.AddConfig<SystemConfigurationAsset>(Config);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                VerificationFixtureOwnership.RunThenCleanUp(() => ProcessManager.UnregisterSystem(m_system),
                    () => { if (Config != null) Object.DestroyImmediate(Config); },
                    m_lease.Dispose);
            }
        }

        private sealed class FakeKnot : IRibbonKnot, ISplineKnot
        {
            public FakeKnot(Vector3 location) { Transform = new LightweightTransform(location, Quaternion.identity); }
            public LightweightTransform Transform { get; }
            public float Length { get; set; }
            public MetadataGroups Metadata => throw new NotSupportedException();
            public Vector3 LocalControlPointNext => throw new NotSupportedException();
            public Vector3 WorldControlPointNext => throw new NotSupportedException();
            public Vector3 LocalControlPointPrev => throw new NotSupportedException();
            public Vector3 WorldControlPointPrev => throw new NotSupportedException();
            public Vector3 TangentNext => throw new NotSupportedException();
            public Vector3 TangentPrev => throw new NotSupportedException();
            public float LengthInverse => throw new NotSupportedException();
        }

        private abstract class FakeSurface : ISurface
        {
            public readonly List<string> Calls = new List<string>();
            public DateTime GetTimestamp() => throw new NotSupportedException();
            public MetadataGroups GetSurfaceMetadata() => throw new NotSupportedException();
            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => throw new NotSupportedException();
            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => throw new NotSupportedException();
            public float Length => throw new NotSupportedException();
            public float LengthInverse => throw new NotSupportedException();
            public Bounds GetBoundingBox(bool inWorldSpace = false) => throw new NotSupportedException();
            public Vector3 TransformPoint(Vector3 point) => throw new NotSupportedException();
            public Vector3 InverseTransformPoint(Vector3 point) => throw new NotSupportedException();
            public bool IsActive() => throw new NotSupportedException();
        }

        // Records GetKnot(index, alignment) and KnotCount reads in order.
        private sealed class FakeRibbon : FakeSurface, IRibbon
        {
            public readonly List<FakeKnot> Centres = new List<FakeKnot>();
            private readonly List<FakeKnot> m_lefts = new List<FakeKnot>();
            private readonly List<FakeKnot> m_rights = new List<FakeKnot>();

            public FakeRibbon(params (Vector3 centre, Vector3 left, Vector3 right)[] knots)
            {
                foreach ((Vector3 centre, Vector3 left, Vector3 right) in knots)
                {
                    Centres.Add(new FakeKnot(centre));
                    m_lefts.Add(new FakeKnot(left));
                    m_rights.Add(new FakeKnot(right));
                }
            }

            public int KnotCount
            {
                get
                {
                    Calls.Add("count");
                    return Centres.Count;
                }
            }

            public IRibbonKnot GetKnot(int knotIndex, RibbonAlignment alignment)
            {
                Calls.Add(knotIndex + ":" + alignment);
                switch (alignment)
                {
                    case RibbonAlignment.Left: return m_lefts[knotIndex];
                    case RibbonAlignment.Right: return m_rights[knotIndex];
                    default: return Centres[knotIndex];
                }
            }

            public LightweightTransform Transform => throw new NotSupportedException();
            public IRibbonKnot GetBackKnot(RibbonAlignment alignment) => throw new NotSupportedException();
            public IRibbonKnot GetHeadKnot(RibbonAlignment alignment) => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => throw new NotSupportedException();
            public RibbonLocation FindNearestRibbonLocation(Vector3 worldPosition, RibbonAlignment alignment) => throw new NotSupportedException();
            public RibbonLocation FindAdjacentRibbonLocation(Vector3 worldPosition, Vector3 forward, RibbonAlignment alignment) => throw new NotSupportedException();
            public RibbonLocation GetRibbonLocationFromDistance(float distance, RibbonAlignment alignment) => throw new NotSupportedException();
            public LinearRatio GetLinearRatioFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw new NotSupportedException();
            public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw new NotSupportedException();
            public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw new NotSupportedException();
            public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw new NotSupportedException();
            public Vector3 GetLocalPosFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw new NotSupportedException();
            public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw new NotSupportedException();
            public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw new NotSupportedException();
            public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw new NotSupportedException();
            public LightweightTransform GetWorldTransformFromDistance(float distance) => throw new NotSupportedException();
            public LightweightTransform GetWorldTransformFromRibbonT(float ribbonT) => throw new NotSupportedException();
            public void KnotsUpdated() => throw new NotSupportedException();
            public void UpdateRibbonType(IRibbon newRibbon) => throw new NotSupportedException();
            public bool TryGetRaycastHit(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public event Action OnRibbonKnotsUpdated { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
            public event Action<IRibbon> OnRibbonTypeChanged { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        }

        // Records GetKnot(index) and KnotCount reads in order.
        private sealed class FakeSpline : FakeSurface, ISpline
        {
            public readonly List<FakeKnot> Knots = new List<FakeKnot>();

            public FakeSpline(params Vector3[] locations)
            {
                foreach (Vector3 location in locations)
                    Knots.Add(new FakeKnot(location));
            }

            public int KnotCount
            {
                get
                {
                    Calls.Add("count");
                    return Knots.Count;
                }
            }

            public ISplineKnot GetKnot(int knotIndex)
            {
                Calls.Add(knotIndex.ToString());
                return Knots[knotIndex];
            }

            public LightweightTransform Transform => throw new NotSupportedException();
            public SplineType SplineType => throw new NotSupportedException();
            public ISplineKnot GetBackKnot() => throw new NotSupportedException();
            public ISplineKnot GetHeadKnot() => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => throw new NotSupportedException();
            public SplineLocation FindNearestSplineLocation(Vector3 worldPosition) => throw new NotSupportedException();
            public SplineLocation FindAdjacentSplineLocation(Vector3 worldPosition, Vector3 forward) => throw new NotSupportedException();
            public SplineLocation GetSplineLocationFromDistance(float distance) => throw new NotSupportedException();
            public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) => throw new NotSupportedException();
            public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) => throw new NotSupportedException();
            public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT) => throw new NotSupportedException();
            public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT) => throw new NotSupportedException();
            public Vector3 GetLocalPosFromKnotT(KnotT knotT) => throw new NotSupportedException();
            public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio) => throw new NotSupportedException();
            public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio) => throw new NotSupportedException();
            public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio) => throw new NotSupportedException();
            public LightweightTransform GetWorldTransformFromDistance(float distance) => throw new NotSupportedException();
            public LightweightTransform GetWorldTransformFromSplineT(float splineT) => throw new NotSupportedException();
            public void KnotsUpdated() => throw new NotSupportedException();
            public void UpdateSplineType(ISpline newSpline) => throw new NotSupportedException();
            public event Action OnSplineKnotsUpdated { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
            public event Action<ISpline> OnSplineTypeChanged { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        }

        // A spline grid with no splines or mesh types, so InitialiseSetup
        // stops at its failing setup check before any storage body. MaxRatio
        // and ValidSetup start opposite to what InitialiseSetup stores.
        private static ProceduralMesh_PlacementOnSplineGrid SetUpForInitialise(GameObject host, ProceduralMesh_PlacementSetup setup)
        {
            var grid = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
            ((List<Spline>)Get(grid, "m_splines")).Clear();
            ((IList)Get(grid, "m_meshTypes")).Clear();
            Set(grid, "m_setup", setup);
            Configure(grid, 5f);
            SetProperty(grid, typeof(ProceduralMesh_Placement), "ValidSetup", false);
            return grid;
        }

        private static void AssertDistanceSquares(ProceduralMesh_Placement placement, float spawn, float despawn, float fadeIn, float fadeOut)
        {
            Assert.That(GetProperty(placement, "SpawnDistanceSquare"), Is.EqualTo(spawn), "SpawnDistanceSquare");
            Assert.That(GetProperty(placement, "DespawnDistanceSquare"), Is.EqualTo(despawn), "DespawnDistanceSquare");
            Assert.That(GetProperty(placement, "FadeInDistanceSquare"), Is.EqualTo(fadeIn), "FadeInDistanceSquare");
            Assert.That(GetProperty(placement, "FadeOutDistanceSquare"), Is.EqualTo(fadeOut), "FadeOutDistanceSquare");
        }

        private static Delegate[] UpdatedHandlers(ProceduralMesh_PlacementSetup setup)
        {
            var value = (Delegate)Get(setup, "OnUpdated");
            return value == null ? new Delegate[0] : value.GetInvocationList();
        }

        private static Delegate SetupUpdatedHandler(ProceduralMesh_Placement placement) =>
            Delegate.CreateDelegate(typeof(Action<ProceduralMesh_PlacementSetup>), placement,
                typeof(ProceduralMesh_Placement).GetMethod("SetupUpdated", Own));

        private static IEnumerable<ProceduralMesh_Placement> Placements(GameObject host)
        {
            yield return host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
            yield return host.AddComponent<ProceduralMesh_PlacementOnRibbonRandom>();
            yield return host.AddComponent<ProceduralMesh_PlacementOnMeshRandom>();
        }

        private static string StorageField(ProceduralMesh_Placement placement)
        {
            if (placement is ProceduralMesh_PlacementOnSpline) return "m_splineInfo";
            if (placement is ProceduralMesh_PlacementOnRibbon) return "m_ribbonInfo";
            return "m_storageInfo";
        }

        private static object EmptyStorage(ProceduralMesh_Placement placement)
        {
            if (placement is ProceduralMesh_PlacementOnSpline) return new List<ProceduralMesh_StorageForSpline>();
            if (placement is ProceduralMesh_PlacementOnRibbon) return new List<ProceduralMesh_StorageForRibbon>();
            return new List<ProceduralMesh_StorageForMesh>();
        }

        private static ProceduralMesh_PlacementSetup.PerformanceSetup Profile(PerformanceProfile.QualityLevel level, float spawnDistance)
        {
            var profile = new ProceduralMesh_PlacementSetup.PerformanceSetup();
            Set(profile, "m_qualityLevel", level);
            Set(profile, "m_spawnDistance", spawnDistance);
            return profile;
        }

        private static void AssertDefaultProfile(ProceduralMesh_PlacementSetup.PerformanceSetup profile)
        {
            Assert.That(profile.QualityLevel, Is.EqualTo(PerformanceProfile.QualityLevel.Medium));
            Assert.That(profile.SpawnDistance, Is.EqualTo(40f));
            Assert.That(profile.DespawnExtraDistance, Is.EqualTo(1f));
            Assert.That(profile.FadeDistance, Is.EqualTo(5f));
            Assert.That(profile.PopulatePerKnot, Is.True);
            Assert.That(profile.NumMeshesMultiplier, Is.EqualTo(1f));
            Assert.That(profile.UseRuntimeCallback, Is.False);
        }

        private static void AssertDefaultMeshType(ProceduralMesh_Type meshType, Type exactType)
        {
            Assert.That(meshType.GetType(), Is.EqualTo(exactType));
            Assert.That(meshType.Seed, Is.EqualTo(-1), "Reset sets the seed.");
            Assert.That(meshType.Ratio, Is.EqualTo(1f));
            var constructed = (ProceduralMesh_Type)Activator.CreateInstance(exactType);
            Assert.That(constructed.Seed, Is.Zero, "The constructor alone leaves seed 0.");
        }

        private static void AssertCapacityOne(object owner, params string[] fields)
        {
            foreach (string field in fields)
            {
                var list = Get(owner, field);
                Assert.That(list.GetType().GetProperty("Capacity").GetValue(list), Is.EqualTo(1), field);
            }
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), actual + " != " + expected);
        }

        private static void Configure(ProceduralMesh_Placement placement, float maxRatio) =>
            SetProperty(placement, typeof(ProceduralMesh_Placement), "MaxRatio", maxRatio);

        private static List<string> Errors(ProceduralMesh_Placement placement) => (List<string>)Invoke(placement, "GetErrorMessages");

        private static bool ValidSetup(ProceduralMesh_Placement placement) =>
            (bool)typeof(ProceduralMesh_Placement).GetProperty("ValidSetup", Own).GetValue(placement);

        private static float SplineArea(FakeSpline spline, int start, int end) =>
            (float)InvokeStatic(typeof(ProceduralMesh_PlacementOnSplineRandom), "CalcSplineArea", Storage(start, end), spline);

        private static float RibbonArea(FakeRibbon ribbon, int start, int end)
        {
            ribbon.Calls.Clear();
            return (float)InvokeStatic(typeof(ProceduralMesh_PlacementOnRibbonRandom), "CalculateRibbonArea", Storage(start, end), ribbon);
        }

        private static ProceduralMesh_StorageForSubSurface Storage(int start, int end) =>
            new ProceduralMesh_StorageForSubSurface { StartSubSurface = start, EndSubSurface = end };

        private static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Own | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }

        private static object Get(object owner, string field) => FindField(owner.GetType(), field).GetValue(owner);

        private static void Set(object owner, string field, object value) => FindField(owner.GetType(), field).SetValue(owner, value);

        private static object GetProperty(ProceduralMesh_Placement placement, string property) =>
            typeof(ProceduralMesh_Placement).GetProperty(property, Own).GetValue(placement);

        private static void SetProperty(object owner, Type declaring, string property, object value) =>
            declaring.GetProperty(property, Own).SetValue(owner, value);

        private static object Invoke(object owner, string method, params object[] arguments)
        {
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo info = type.GetMethod(method, Own | BindingFlags.DeclaredOnly);
                if (info != null) return Call(info, owner, arguments);
            }
            throw new MissingMethodException(method);
        }

        private static object InvokeStatic(Type type, string method, params object[] arguments) =>
            Call(type.GetMethod(method, Own), null, arguments);

        private static object Call(MethodInfo method, object owner, object[] arguments)
        {
            try
            {
                return method.Invoke(owner, arguments);
            }
            catch (TargetInvocationException failure)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }
    }
}
