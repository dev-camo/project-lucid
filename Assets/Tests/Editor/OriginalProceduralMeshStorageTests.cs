using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace ProjectLucid.Tests
{
    // HLSplines.Runtime LocationGenerator, ProceduralMesh_Type* and the
    // ProceduralMesh_StorageFor* family. Expected values are derived from the
    // 1.10.1 x86-64 bodies cited per test; fixtures are in-memory fakes, owned
    // GameObjects and meshes that each test destroys. ProcessManager is never
    // reached (every pool-type path tested exits before its lookup).
    public sealed class OriginalProceduralMeshStorageTests
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const float Tolerance = 1e-5f;

        // ---------------------------------------------------------------- LocationGenerator

        // 0x1A55D20: bias < 0 -> left > 0 ? maxss(-1, bias/left) : 0, else
        // right > 0 ? minss(1, bias/right) : 0. NaN bias takes the right branch
        // and keeps NaN (minss(1, NaN) returns the second operand).
        [Test]
        public void ConvertBiasToRatioClampsPerHalfAndKeepsTheNativeNaNOperandOrder()
        {
            var rows = new (float bias, float left, float right, float expected)[]
            {
                (-0.5f, 2f, 4f, -0.25f),
                (-3f, 2f, 4f, -1f),
                (-0.5f, 0f, 4f, 0f),
                (-0.5f, float.NaN, 4f, 0f),
                (0.5f, 2f, 4f, 0.125f),
                (5f, 2f, 4f, 1f),
                (0.5f, 2f, 0f, 0f),
                (0f, 2f, 4f, 0f),
            };
            foreach (var row in rows)
                Assert.That(ConvertBias(row.bias, row.left, row.right), Is.EqualTo(row.expected), row.ToString());
            Assert.That(float.IsNaN(ConvertBias(float.NaN, 2f, 4f)), Is.True, "Mathf.Min(1, NaN) keeps NaN");
        }

        // 0x1A55D60: worldUnitBias = ratio * (bias < 0 ? left : right).
        [Test]
        public void ConvertBiasToRatioWorldUnitScalesTheRatioByTheChosenHalfWidth()
        {
            var rows = new (float bias, float left, float right, float ratio, float world)[]
            {
                (-3f, 2f, 4f, -1f, -2f),
                (0.5f, 2f, 4f, 0.125f, 0.5f),
                (-0.5f, 0f, 4f, 0f, 0f),
                (6f, 2f, 4f, 1f, 4f),
            };
            MethodInfo method = typeof(LocationGenerator).GetMethod("ConvertBiasToRatio", Any, null,
                new[] { typeof(float), typeof(float), typeof(float), typeof(float).MakeByRefType() }, null);
            foreach (var row in rows)
            {
                object[] args = { row.bias, row.left, row.right, 0f };
                Assert.That((float)method.Invoke(null, args), Is.EqualTo(row.ratio), row.ToString());
                Assert.That((float)args[3], Is.EqualTo(row.world), row.ToString());
            }
        }

        // .cctor 0x1A58CD0 copies four <PrivateImplementationDetails> blobs
        // (global-metadata offsets 0x731B00, 0x731BB8, 0x736250, 0x7361A8)
        // with InitializeArray (0x1A58D58..0x1A58E1E: lengths 0x2D, 0x2D, 0x28,
        // 0x28, stored at static offsets 0x0..0x18). Each blob's field name is
        // the SHA-256 of its bytes, so the hash ties the tables to the binary
        // independently of the hand-written zone counts.
        [Test]
        public void ZoneTablesMatchTheOriginalFieldRvaBlobs()
        {
            AssertZones("LeftBias", "16A6AACB485681C3A38D6336B51DC09E6925E2E9705180B4CEE2D0FC72E052B3", 15, 11, 7, 5, 3, 2, 1, 1);
            AssertZones("RightBias", "55873ADB5C216089CCBCC4B1C378CF5ED26E8F8A80934F3FD0A2F8D85C374084", 1, 1, 2, 3, 5, 7, 11, 15);
            AssertZones("EdgeBias", "DD4679D760FB5D4FABC866DD68C8F1A396BE647F7EC2A11EEA1293CFD0BF83E4", 12, 5, 2, 1, 1, 2, 5, 12);
            AssertZones("CenterBias", "9CAC7C5A0C053FBBCBAB580A39D17F949473514E69B515EEA25E936D56E892E4", 1, 2, 5, 12, 12, 5, 2, 1);
        }

        // 0x1A55E40: Even = biasMin + t*range; weighted = biasMin + zone*range
        // + range*0.125*frac(t*length); jump table 0x1A56044 orders the cases.
        [Test]
        public void GetTBiasPicksAWeightedZoneThenSpreadsInsideIt()
        {
            Assert.That(TBias(0.25, LocationGenerator.BiasDensity.Even), Is.EqualTo(-0.5f));
            Assert.That(TBias(0.5, LocationGenerator.BiasDensity.Left), Is.EqualTo(-0.625f));
            Assert.That(TBias(0.5, LocationGenerator.BiasDensity.Right), Is.EqualTo(0.625f));
            Assert.That(TBias(0.5, LocationGenerator.BiasDensity.Edge), Is.EqualTo(0f));
            Assert.That(TBias(0.1875, LocationGenerator.BiasDensity.Center), Is.EqualTo(-0.375f));
            var thrown = Assert.Throws<TargetInvocationException>(() => TBias(0.5, (LocationGenerator.BiasDensity)5));
            Assert.That(thrown.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(((ArgumentOutOfRangeException)thrown.InnerException).ParamName, Is.EqualTo("biasDensity"));
        }

        // 0x1A58710: |cross(v[3i+1]-v[3i], v[3i+2]-v[3i])| * 0.5; a trailing partial triangle is ignored.
        [Test]
        public void TriangleSizesAreHalfTheCrossMagnitude()
        {
            var verts = new[] { Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(2f, 2f, 0f), new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 3f) };
            var tris = new[] { 0, 1, 2, 0, 1, 4, 3 };
            var sizes = (float[])typeof(LocationGenerator).GetMethod("GetTriSizes", Any).Invoke(null, new object[] { tris, verts });
            Assert.That(sizes, Is.EqualTo(new[] { 2f, 3f }));
        }

        // 0x1A56DA0: knot = floor(i * 1/(n/(end-start))) + start, T = (float)NextDouble().
        [Test]
        public void SplineRandomPositionsSpreadSamplesOverKnotsAndTakeTFromTheRandom()
        {
            var spline = new FakeSpline(5);
            var random = new SequenceRandom(0.5, 0.25, 0.75, 0.125, 0.5, 0.25, 0.75, 0.125);
            List<LightweightTransform> positions = LocationGenerator.RandomPositions(random, spline, 8);
            Assert.That(positions.Count, Is.EqualTo(8));
            int[] knots = { 0, 0, 1, 1, 2, 2, 3, 3 };
            float[] ts = { 0.5f, 0.25f, 0.75f, 0.125f, 0.5f, 0.25f, 0.75f, 0.125f };
            for (int i = 0; i < 8; i++)
                Assert.That(positions[i].Location, Is.EqualTo(FakeSpline.At(knots[i], ts[i])), "sample " + i);

            random = new SequenceRandom(0.5, 0.5, 0.5, 0.5);
            positions = LocationGenerator.RandomPositions(random, spline, 4, 1, 3);
            Assert.That(positions.ConvertAll(p => p.Location.x), Is.EqualTo(new[] { 1f, 1f, 2f, 2f }));
        }

        // 0x1A57C40: rows every strideZ from strideZ/2, carrying (t-1)*length;
        // a skipped knot advances t by ceil((1-t)/stride)*stride (roundss 0xa).
        [Test]
        public void SplineGridCarriesTheOvershootAndSkipsLeadingKnotsWithCeil()
        {
            var spline = new FakeSpline(3, 8f, 4f, 99f);
            List<LightweightTransform> all = LocationGenerator.GridPositions(spline, 4f);
            Assert.That(all.ConvertAll(p => p.Location), Is.EqualTo(new[] { FakeSpline.At(0, 0.25f), FakeSpline.At(0, 0.75f), FakeSpline.At(1, 0.5f) }));
            // A floor-based skip would restart knot 1 at t = -0.5.
            List<LightweightTransform> skipped = LocationGenerator.GridPositions(spline, 4f, 1);
            Assert.That(skipped.ConvertAll(p => p.Location), Is.EqualTo(new[] { FakeSpline.At(1, 0.5f) }));
        }

        // 0x1A57090: centre when biasMin <= 0 <= biasMax; right then left
        // columns for x = step..max while x >= min; WorldUnit converts the
        // limits per row; no strideX and no centre returns before any knot.
        [Test]
        public void RibbonGridEmitsCentreThenRightThenLeftColumns()
        {
            var ribbon = new FakeRibbon(2, 8f);
            List<LightweightTransform> full = LocationGenerator.GridPositions(ribbon, 4f, 1f);
            Assert.That(full.ConvertAll(p => p.Location.x), Is.EqualTo(new[] { 0f, 1f, 2f, -1f, -2f, 0f, 1f, 2f, -1f, -2f }));
            Assert.That(full[0].Location.z, Is.EqualTo(0.25f));
            Assert.That(full[5].Location.z, Is.EqualTo(0.75f));
            AssertRotation(full[2].Orientation, Quaternion.Slerp(Quaternion.identity, FakeRibbon.RightRotation, 1f));
            AssertRotation(full[3].Orientation, Quaternion.Slerp(Quaternion.identity, FakeRibbon.LeftRotation, 0.5f));

            List<LightweightTransform> rightOnly = LocationGenerator.GridPositions(ribbon, 4f, 1f, 0.25f, 1f);
            Assert.That(rightOnly.ConvertAll(p => p.Location.x), Is.EqualTo(new[] { 1f, 2f, 1f, 2f }));

            // Left loop 0x1A5790B-0x1A579C5: x = step..-minRatio, emitted only when
            // x >= -maxRatio (xmm2 negated at 0x1A57931). With -1..-0.75 there is no
            // centre and no right column; x = 0.5 is rejected and x = 1 kept. An
            // x >= minRatio filter would also emit x = 0.5 (-1 on this ribbon).
            List<LightweightTransform> leftOnly = LocationGenerator.GridPositions(ribbon, 4f, 1f, -1f, -0.75f);
            Assert.That(leftOnly.ConvertAll(p => p.Location.x), Is.EqualTo(new[] { -2f, -2f }));
            Assert.That(leftOnly.ConvertAll(p => p.Location.z), Is.EqualTo(new[] { 0.25f, 0.75f }));

            List<LightweightTransform> worldUnit = LocationGenerator.GridPositions(ribbon, 4f, 1f, -1f, 1f, LocationGenerator.BiasType.WorldUnit);
            Assert.That(worldUnit.ConvertAll(p => p.Location.x), Is.EqualTo(new[] { 0f, 1f, -1f, 0f, 1f, -1f }));

            int knotReads = ribbon.KnotReads;
            Assert.That(LocationGenerator.GridPositions(ribbon, 4f, 0f, 0.25f, 1f), Is.Empty);
            Assert.That(ribbon.KnotReads, Is.EqualTo(knotReads));
        }

        // 0x1A56060 WidthRatio: t = GetTBias(...), side = Math.Sign(t); inverse
        // samples width 2 - range from -1 and shifts t > biasMin past the band.
        [Test]
        public void RibbonRandomWidthRatioLerpsTowardsTheSignedSide()
        {
            var ribbon = new FakeRibbon(2, 8f);
            var random = new SequenceRandom(0.5, 0.25, 0.75, 0.875);
            List<LightweightTransform> positions = LocationGenerator.RandomPositions(random, ribbon, 2);
            Assert.That(positions[0].Location, Is.EqualTo(new Vector3(-1f, 0f, 0.5f)));
            Assert.That(positions[1].Location, Is.EqualTo(new Vector3(1.5f, 0f, 0.75f)));

            random = new SequenceRandom(0.5, 0.25, 0.75, 0.75);
            positions = LocationGenerator.RandomPositions(random, ribbon, 2, -0.5f, 0.5f, inverseBias: true);
            Assert.That(positions[0].Location, Is.EqualTo(new Vector3(-1.5f, 0f, 0.5f)));
            Assert.That(positions[1].Location, Is.EqualTo(new Vector3(1.5f, 0f, 0.75f)));
        }

        // 0x1A56060 WorldUnit: a row is skipped when minWorld > biasMin and
        // maxWorld > biasMax (0x1A566D2-0x1A566E4; only T was drawn), where each
        // world value is the converted ratio times the chosen half-width
        // (divss, minss/maxss, mulss in single precision); otherwise t uses the
        // converted ratios. Limits past both edges or past the right edge still
        // place a mesh, but a right limit inside the ribbon can come back one
        // ulp high: 0.6576321721f / 2.4906175137f * 2.4906175137f rounds to
        // 0.6576322317f, so with the left limit past the edge that row is
        // skipped too. A test of the limits against the half-widths keeps it.
        [Test]
        public void RibbonRandomWorldUnitSkipsRowsWhenBothScaledLimitsExceedTheRequestedOnes()
        {
            var ribbon = new FakeRibbon(2, 8f);
            var random = new SequenceRandom(0.5, 0.5);
            Assert.That(LocationGenerator.RandomPositions(random, ribbon, 2, -3f, -3f, LocationGenerator.BiasType.WorldUnit), Is.Empty);
            Assert.That(random.Draws, Is.EqualTo(2));

            random = new SequenceRandom(0.5, 0.25);
            List<LightweightTransform> positions = LocationGenerator.RandomPositions(random, ribbon, 1, -1f, 1f, LocationGenerator.BiasType.WorldUnit);
            Assert.That(positions[0].Location, Is.EqualTo(new Vector3(-0.5f, 0f, 0.5f)));

            // Half-widths are 2: -5..5 and 3..3 lie outside the ribbon but are not skipped.
            random = new SequenceRandom(0.5, 0.5);
            Assert.That(LocationGenerator.RandomPositions(random, ribbon, 1, -5f, 5f, LocationGenerator.BiasType.WorldUnit), Has.Count.EqualTo(1));
            Assert.That(random.Draws, Is.EqualTo(2));
            random = new SequenceRandom(0.5, 0.5);
            Assert.That(LocationGenerator.RandomPositions(random, ribbon, 1, 3f, 3f, LocationGenerator.BiasType.WorldUnit), Has.Count.EqualTo(1));

            // sqrtss(w * w) returns w exactly for this half-width, so the overshoot
            // comes from the ratio round trip alone.
            var rounding = new FakeRibbon(2, 8f, 2.490617513656616f);
            random = new SequenceRandom(0.5, 0.5);
            Assert.That(LocationGenerator.RandomPositions(random, rounding, 1, -3f, 0.6576321721076965f, LocationGenerator.BiasType.WorldUnit), Is.Empty);
            Assert.That(random.Draws, Is.EqualTo(1));
        }

        // Inverse WorldUnit 0x1A566EA-0x1A5675F: GetTBias(-1, 2 - (maxRatio -
        // minRatio)), then t > minRatio shifts by -minRatio + maxRatio, all on
        // the converted ratios (-0.5, 0.5 for limits -1, 1 on half-widths 2):
        // t = -1 + 0.75 = -0.25 -> 0.75 -> (1.5, 0, 0.5). The world limits would
        // give GetTBias(-1, 0) = -1 and (-2, 0, 0.5); shifting by them gives (3.5, 0, 0.5).
        [Test]
        public void RibbonRandomWorldUnitInverseBiasShiftsByTheConvertedRatios()
        {
            var ribbon = new FakeRibbon(2, 8f);
            var random = new SequenceRandom(0.5, 0.75);
            List<LightweightTransform> positions = LocationGenerator.RandomPositions(random, ribbon, 1, -1f, 1f,
                LocationGenerator.BiasType.WorldUnit, LocationGenerator.BiasDensity.Even, inverseBias: true);
            Assert.That(positions, Has.Count.EqualTo(1));
            Assert.That(positions[0].Location, Is.EqualTo(new Vector3(1.5f, 0f, 0.5f)));
            AssertRotation(positions[0].Orientation, Quaternion.Slerp(Quaternion.identity, FakeRibbon.RightRotation, 0.75f));
            Assert.That(random.Draws, Is.EqualTo(2));
        }

        // 0x1A58050: target = (float)(NextDouble * total area); binary search
        // then +1 when target > cumulative[tri]; u+v >= 1 folds both; facing
        // FromToRotation(up, normalized cross(b-a, c-a)).
        [Test]
        public void MeshRandomPositionsAreAreaWeightedAndFoldIntoTheTriangle()
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(10f, 0f, 0f), new Vector3(12f, 0f, 0f), new Vector3(10f, 0f, 2f) },
                triangles = new[] { 0, 1, 2, 3, 4, 5 }
            };
            try
            {
                var random = new SequenceRandom(0.5, 0.25, 0.5, 0.1, 0.75, 0.5);
                List<LightweightTransform> positions = LocationGenerator.RandomPositions(random, mesh, 2);
                Assert.That(positions[0].Location, Is.EqualTo(new Vector3(10.5f, 0f, 1f)));
                AssertRotation(positions[0].Orientation, Quaternion.FromToRotation(Vector3.up, Vector3.down));
                Assert.That(positions[1].Location, Is.EqualTo(new Vector3(0.25f, 0.5f, 0f)));
                AssertRotation(positions[1].Orientation, Quaternion.FromToRotation(Vector3.up, Vector3.forward));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // ---------------------------------------------------------------- ProceduralMesh_Type*

        // .ctors 0x1A63A20 / 0x1A5E0E0 (biasMin/max from 0x29A81A0 = -1, 1; type/density zeroed).
        [Test]
        public void MeshTypesStartWithTheOriginalSerializedDefaults()
        {
            var type = new ProceduralMesh_TypeGrid();
            Assert.That(type.Seed, Is.Zero);
            Assert.That(type.Ratio, Is.EqualTo(1f));
            Assert.That(type.MeshPoolType, Is.Null);
            Assert.That(type.MeshPool, Is.Null);
            Assert.That(type.Setup, Is.Null);
            type.Seed = 7;
            Assert.That(type.Seed, Is.EqualTo(7));

            var ribbon = new ProceduralMesh_TypeRibbonRandom();
            Assert.That(ribbon.Ratio, Is.EqualTo(1f));
            Assert.That(ribbon.BiasMin, Is.EqualTo(-1f));
            Assert.That(ribbon.BiasMax, Is.EqualTo(1f));
            Assert.That(ribbon.BiasType, Is.EqualTo(LocationGenerator.BiasType.WidthRatio));
            Assert.That(ribbon.BiasDensity, Is.EqualTo(LocationGenerator.BiasDensity.Even));
            Assert.That(ribbon.InverseBias, Is.False);
            Assert.That(ribbon.Setup, Is.Null);
        }

        // 0x1A63A50 / 0x1A63B50: a missing pool type fails first, so the
        // manager reference is never resolved.
        [Test]
        public void MissingPoolTypeFailsBeforeAnyManagerLookup()
        {
            var type = new ProceduralMesh_TypeRandom();
            Assert.That(type.HasValidSetup(), Is.False);
            Assert.That(type.GetErrorMessage(), Is.EqualTo("No mesh pool type assigned"));
            Assert.That(typeof(ProceduralMesh_Type).GetField("m_manager", Any).GetValue(type), Is.Null);
        }

        // ---------------------------------------------------------------- StorageForSubSurface

        // .ctor 0x1A62550 and MeshLocation .ctor 0x1A63610 (m_active = 1).
        [Test]
        public void NewStorageIsUnpopulatedAndNewLocationsStartActive()
        {
            var storage = new ProceduralMesh_StorageForSubSurface();
            Assert.That(storage.State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated));
            Assert.That(Field(storage, "m_boundsCalculated"), Is.False);
            Assert.That(Field(storage, "m_meshTypeStorage"), Is.Null);

            Type locationType = typeof(ProceduralMesh_StorageForSubSurface).GetNestedType("MeshLocation", Any);
            object location = Activator.CreateInstance(locationType, new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 30f, 0f), 2.5f);
            Assert.That(locationType.GetField("m_position").GetValue(location), Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(locationType.GetField("m_scale").GetValue(location), Is.EqualTo(2.5f));
            Assert.That(locationType.GetField("m_active").GetValue(location), Is.True);
        }

        // 0x1A632E0: three draws, then quat = quat * Euler(min + r*dev) (lhs is the input).
        [Test]
        public void UpdateRotationPostMultipliesTheRandomEulerDeviation()
        {
            Quaternion start = Quaternion.Euler(0f, 90f, 0f);
            object[] args = { start, new Vector3(10f, 0f, 0f), new Vector3(80f, 40f, 0f), new SequenceRandom(0.5, 0.25, 0.75) };
            typeof(ProceduralMesh_StorageForSubSurface).GetMethod("UpdateRotation", Any).Invoke(null, args);
            Quaternion deviation = Quaternion.Euler(50f, 10f, 0f);
            AssertRotation((Quaternion)args[0], start * deviation);
            Assert.That(Quaternion.Angle((Quaternion)args[0], deviation * start), Is.GreaterThan(1f), "operand order matters");
            Assert.That(((SequenceRandom)args[3]).Draws, Is.EqualTo(3));
        }

        // 0x1A63450: pos += quat * (min + r*dev).
        [Test]
        public void UpdatePositionAddsTheRotatedRandomDeviation()
        {
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
            object[] args = { new Vector3(1f, 2f, 3f), new Vector3(0f, 1f, 0f), new Vector3(2f, 0f, 4f), new SequenceRandom(0.5, 0.9, 0.25), rotation };
            typeof(ProceduralMesh_StorageForSubSurface).GetMethod("UpdatePosition", Any).Invoke(null, args);
            Vector3 expected = new Vector3(1f, 2f, 3f) + rotation * new Vector3(1f, 1f, 1f);
            Assert.That(Vector3.Distance((Vector3)args[0], expected), Is.LessThan(Tolerance));
        }

        // 0x1A62700: the reset happens whatever the state; despawning an
        // unpopulated storage keeps its generated locations.
        [Test]
        public void DespawnMeshesDropsLocationsOnlyWhenResetIsRequested()
        {
            var storage = new ProceduralMesh_StorageForSubSurface();
            IList list = GiveEmptyTypeStorage(storage);
            storage.DespawnMeshes();
            Assert.That(Field(storage, "m_meshTypeStorage"), Is.SameAs(list));
            storage.DespawnMeshes(false, true);
            Assert.That(Field(storage, "m_meshTypeStorage"), Is.Null);
        }

        // 0x1A63280 / 0x1A63230 / 0x1A63200 with no mesh types: HandleFade
        // finishes at once. FadeOut passes resetStorage as DespawnMeshes'
        // onDestroy (esi), so it never drops the locations.
        [Test]
        public void FadesFinishAtOnceWithoutMeshTypesAndFadeOutKeepsLocations()
        {
            var storage = new ProceduralMesh_StorageForSubSurface();
            IList list = GiveEmptyTypeStorage(storage);
            storage.FadeIn();
            Assert.That(storage.State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Populated));
            Assert.That(storage.StartFadeOut(true), Is.True);
            Assert.That(storage.State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated));
            Assert.That(Field(storage, "m_fadeTimestamp"), Is.EqualTo(Time.time));
            Assert.That(Field(storage, "m_meshTypeStorage"), Is.SameAs(list));
        }

        // Scale predicate, GenerateRandomLocations 0x98E37B-0x98E528 and
        // GenerateGridLocations 0x98DC87-0x98DCD5 (shared generics): with
        // x = |parentScale| + -1, minScale is kept without a draw when
        // !(x < -float.Epsilon) (ucomiss/setbe against 0x29A7E60) && !(x > eps)
        // && !(scaleRange > eps) (cmpnltps against 0x29A6BF0); unordered
        // compares keep minScale. Otherwise scale = (r * range + min) * parentScale.
        // Rows: unit scale of either sign, one ulp either side of 1 (a one-sided
        // or unsigned test differs), range eps (a >= test draws), 2 eps, NaN
        // (a !(range <= eps) test draws) and a plain range. A NaN parent scale
        // would need a NaN Transform scale, which Unity rejects, so it is not a row.
        private static readonly (float parent, float min, float max, bool draws)[] ScaleCases =
        {
            (1f, 2f, 2f, false),
            (-1f, 2f, 2f, false),
            (1.00000012f, 2f, 2f, true),
            (0.99999994f, 2f, 2f, true),
            (1f, 0f, float.Epsilon, false),
            (1f, 0f, 2f * float.Epsilon, true),
            (1f, 0f, float.NaN, false),
            (1f, 2f, 3f, true),
        };

        private const int ScaleSeed = 11;

        [Test]
        public void RandomLocationsDrawAScaleOnlyOffAUnitParentScaleOrWithARange()
        {
            var host = new GameObject("OriginalProceduralMeshStorageTests.RandomScale");
            var setup = ScriptableObject.CreateInstance<ProceduralMesh_SetupTypeRandom>();
            try
            {
                var owner = host.AddComponent<ProceduralMesh_PlacementOnSplineRandom>();
                typeof(ProceduralMesh_Placement).GetProperty("MaxRatio").SetValue(owner, 1f);
                var meshType = new ProceduralMesh_TypeRandom { Seed = ScaleSeed };
                typeof(ProceduralMesh_TypeRandom).GetField("m_setup", Any).SetValue(meshType, setup);
                foreach (var row in ScaleCases)
                {
                    string label = "parent " + row.parent.ToString("R") + ", scale " + row.min + ".." + row.max.ToString("R");
                    host.transform.localScale = new Vector3(row.parent, 1f, row.parent);
                    SetScaleSetup(setup, row.min, row.max);
                    Random passed = null;
                    var storage = new ProceduralMesh_StorageForSubSurface();
                    storage.GenerateRandomLocations(host.transform, new List<ProceduralMesh_TypeRandom> { meshType }, NumMeshesType.PerMetreSquare, 1f, false, owner,
                        (random, count, start, end, type) =>
                        {
                            passed = random;
                            return TwoIdentityLocations();
                        });

                    // Seed + type index 0 + StartSubSurface 0; the getter draws nothing itself.
                    var reference = new Random(ScaleSeed);
                    float[] expected = ExpectedScales(reference, row);
                    Assert.That(StoredScales(storage), Is.EqualTo(expected), label);
                    Assert.That(passed.NextDouble(), Is.EqualTo(reference.NextDouble()), label + ": draw count");
                }
            }
            finally
            {
                Object.DestroyImmediate(setup);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void GridLocationsDrawAScaleOnlyOffAUnitParentScaleOrWithARange()
        {
            var host = new GameObject("OriginalProceduralMeshStorageTests.GridScale");
            var setup = ScriptableObject.CreateInstance<ProceduralMesh_SetupTypeGrid>();
            try
            {
                var owner = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                typeof(ProceduralMesh_Placement).GetProperty("MaxRatio").SetValue(owner, 1f);
                var meshType = new ProceduralMesh_TypeGrid { Seed = ScaleSeed };
                typeof(ProceduralMesh_TypeGrid).GetField("m_setup", Any).SetValue(meshType, setup);
                foreach (var row in ScaleCases)
                {
                    string label = "parent " + row.parent.ToString("R") + ", scale " + row.min + ".." + row.max.ToString("R");
                    host.transform.localScale = new Vector3(row.parent, 1f, row.parent);
                    SetScaleSetup(setup, row.min, row.max);
                    var storage = new ProceduralMesh_StorageForSubSurface();
                    // One type and no gaps: no shuffle, so the scale draws are the first ones.
                    storage.GenerateGridLocations(host.transform, new List<ProceduralMesh_TypeGrid> { meshType }, 0f, owner, (start, end) => TwoIdentityLocations());
                    Assert.That(StoredScales(storage), Is.EqualTo(ExpectedScales(new Random(ScaleSeed), row)), label);
                }
            }
            finally
            {
                Object.DestroyImmediate(setup);
                Object.DestroyImmediate(host);
            }
        }

        // GenerateRandomLocations (shared generic 0x98E090): jump table 0x98E9D8
        // over count = End - Start: PerKnot *count (0x98E176), PerRibbon
        // count*n/SubSurfaceRange [+0x44] (0x98E18D), PerSection
        // count*n/SubSurfaceSectionRange [+0x48] (0x98E19C), PerMetreSquare as is;
        // anything else throws ArgumentOutOfRangeException("numMeshesType")
        // (0x98E9AE). The count is ratio/maxRatio*n*area*multiplier rounded half to
        // even (modf + parity test at 0x98E440), and each type's Random is seeded
        // with (Seed, or owner.GetInstanceID() when 0) + type index + Start
        // (0x98E450-0x98E487). PerRibbon 5 gives 2.5, which rounds to 2 (a +0.5
        // rounding gives 3); swapping the two ranges gives 5 and 2 for the first rows.
        [Test]
        public void RandomLocationCountsFollowTheRangeKindAndRoundHalfToEven()
        {
            var host = new GameObject("OriginalProceduralMeshStorageTests.RandomCount");
            var setup = ScriptableObject.CreateInstance<ProceduralMesh_SetupTypeRandom>();
            try
            {
                var owner = host.AddComponent<ProceduralMesh_PlacementOnSplineRandom>();
                typeof(ProceduralMesh_Placement).GetProperty("MaxRatio").SetValue(owner, 1f);
                var rows = new (NumMeshesType kind, float numMeshes, int expected)[]
                {
                    (NumMeshesType.PerRibbon, 5f, 2),
                    (NumMeshesType.PerSection, 3f, 3),
                    (NumMeshesType.PerKnot, 1.25f, 2),
                    (NumMeshesType.PerMetreSquare, 2.5f, 2),
                    (NumMeshesType.PerMetreSquare, 3.5f, 4),
                };
                foreach (var row in rows)
                {
                    var meshType = new ProceduralMesh_TypeRandom { Seed = 7 };
                    typeof(ProceduralMesh_TypeRandom).GetField("m_setup", Any).SetValue(meshType, setup);
                    var counts = new List<int>();
                    CountingStorage().GenerateRandomLocations(host.transform, new List<ProceduralMesh_TypeRandom> { meshType }, row.kind, row.numMeshes, false, owner,
                        (random, count, start, end, type) =>
                        {
                            Assert.That((start, end), Is.EqualTo((1, 3)));
                            counts.Add(count);
                            return new List<LightweightTransform>();
                        });
                    Assert.That(counts, Is.EqualTo(new[] { row.expected }), row.kind + " " + row.numMeshes);
                }

                var unseeded = new ProceduralMesh_TypeRandom();
                var seeded = new ProceduralMesh_TypeRandom { Seed = 7 };
                typeof(ProceduralMesh_TypeRandom).GetField("m_setup", Any).SetValue(unseeded, setup);
                typeof(ProceduralMesh_TypeRandom).GetField("m_setup", Any).SetValue(seeded, setup);
                var passed = new List<Random>();
                CountingStorage().GenerateRandomLocations(host.transform, new List<ProceduralMesh_TypeRandom> { unseeded, seeded }, NumMeshesType.PerMetreSquare, 1f, false, owner,
                    (random, count, start, end, type) =>
                    {
                        passed.Add(random);
                        return new List<LightweightTransform>();
                    });
                Assert.That(passed.Count, Is.EqualTo(2));
                Assert.That(passed[0].NextDouble(), Is.EqualTo(new Random(owner.GetInstanceID() + 0 + 1).NextDouble()), "Seed 0 falls back to the owner's instance id");
                Assert.That(passed[1].NextDouble(), Is.EqualTo(new Random(7 + 1 + 1).NextDouble()), "seed + type index + StartSubSurface");

                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => CountingStorage().GenerateRandomLocations(host.transform,
                    new List<ProceduralMesh_TypeRandom> { seeded }, (NumMeshesType)4, 1f, false, owner, (random, count, start, end, type) => new List<LightweightTransform>()));
                Assert.That(thrown.ParamName, Is.EqualTo("numMeshesType"));
            }
            finally
            {
                Object.DestroyImmediate(setup);
                Object.DestroyImmediate(host);
            }
        }

        // GenerateGridLocations (shared generic 0x98D7E0): shuffle when gapsRatio
        // > 0 or there are two or more types (0x98D930-0x98D93F), seeded with
        // meshTypes[0].Seed (or GetInstanceID) + StartSubSurface and no type index
        // (0x98D95D-0x98D9D1); keep floor((1 - min(gaps, 1)) * count) and
        // RemoveRange the rest (0x98DA04-0x98DA4A); each type takes
        // floor(count * ratio/maxRatio) (roundss 9 at 0x98DB04) continuing from the
        // previous type's last location. Seeds 7 and 9 with Start 1 shuffle with
        // seed 8: seed 7 (no Start) or 10 (the last type's seed) order the first
        // two differently, which the fixture checks.
        [Test]
        public void GridLocationsShuffleTrimAndShareTheListAcrossTypes()
        {
            var host = new GameObject("OriginalProceduralMeshStorageTests.GridShare");
            var setup = ScriptableObject.CreateInstance<ProceduralMesh_SetupTypeGrid>();
            try
            {
                var owner = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                typeof(ProceduralMesh_Placement).GetProperty("MaxRatio").SetValue(owner, 2f);
                var first = new ProceduralMesh_TypeGrid { Seed = 7 };
                var second = new ProceduralMesh_TypeGrid { Seed = 9 };
                typeof(ProceduralMesh_TypeGrid).GetField("m_setup", Any).SetValue(first, setup);
                typeof(ProceduralMesh_TypeGrid).GetField("m_setup", Any).SetValue(second, setup);

                List<LightweightTransform> expected = FiveGridLocations();
                expected.Shuffle(new Random(7 + 1));
                List<LightweightTransform> withoutStart = FiveGridLocations();
                withoutStart.Shuffle(new Random(7));
                List<LightweightTransform> lastSeed = FiveGridLocations();
                lastSeed.Shuffle(new Random(9 + 1));
                Assert.That(withoutStart[0].Location, Is.Not.EqualTo(expected[0].Location), "fixture: Start changes the first location");
                Assert.That(lastSeed[1].Location, Is.Not.EqualTo(expected[1].Location), "fixture: the last type's seed changes the second location");

                var storage = new ProceduralMesh_StorageForSubSurface { StartSubSurface = 1, EndSubSurface = 2 };
                storage.GenerateGridLocations(host.transform, new List<ProceduralMesh_TypeGrid> { first, second }, 0.5f, owner, (start, end) => FiveGridLocations());
                Assert.That(StoredPositions(storage), Is.EqualTo(new[] { new[] { expected[0].Location }, new[] { expected[1].Location } }));

                storage = new ProceduralMesh_StorageForSubSurface { StartSubSurface = 1, EndSubSurface = 2 };
                storage.GenerateGridLocations(host.transform, new List<ProceduralMesh_TypeGrid> { first, second }, 1.5f, owner, (start, end) => FiveGridLocations());
                Assert.That(StoredPositions(storage), Is.EqualTo(new[] { new Vector3[0], new Vector3[0] }), "gapsRatio is clamped to 1");

                typeof(ProceduralMesh_Placement).GetProperty("MaxRatio").SetValue(owner, 1f);
                storage = new ProceduralMesh_StorageForSubSurface { StartSubSurface = 1, EndSubSurface = 2 };
                storage.GenerateGridLocations(host.transform, new List<ProceduralMesh_TypeGrid> { first }, 0f, owner, (start, end) => FiveGridLocations());
                Assert.That(StoredPositions(storage), Is.EqualTo(new[] { FiveGridLocations().ConvertAll(l => l.Location).ToArray() }), "one type without gaps keeps the order");
            }
            finally
            {
                Object.DestroyImmediate(setup);
                Object.DestroyImmediate(host);
            }
        }

        // SpawnFromLocations 0x1A62AB0: initial scale 0 only when fadeIn and the
        // pool's FadeTimeSeconds [+0x90] > 0 (0x1A62C42-0x1A62C6B); State =
        // 2 - anyFading (0x1A62FC0). Both loops check HasFreeInstance first and
        // MarkOverflow + break on failure; only the UseRuntimeCallback loop skips
        // inactive locations (0x1A62D63). Spawned meshes take FinalScale = (s, s, s).
        [Test]
        public void SpawnFromLocationsStopsAtAnExhaustedPoolAndSkipsInactiveOnlyWithTheCallback()
        {
            using (var pool = new PoolFixture(2, 0f))
            {
                var storage = new ProceduralMesh_StorageForSubSurface();
                GiveTypeStorage(storage, pool.Pool, (new Vector3(1f, 0f, 0f), 2f, false), (new Vector3(2f, 0f, 0f), 2f, true), (new Vector3(3f, 0f, 0f), 2f, true));
                Spawn(storage, true);
                Assert.That(SpawnedPositions(storage), Is.EqualTo(new[] { new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f) }));
                Assert.That(pool.Pool.HasOverflowed, Is.True);
                Assert.That(storage.State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Populated), "no fade time");
                PooledPrefab<ProceduralMesh> mesh = SpawnedMeshes(storage)[0];
                Assert.That(mesh.Instance.FinalScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
                Assert.That(mesh.Instance.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));

                // HandleFade 0x1A63123: a pool with no fade time is skipped, so the
                // fade finishes without touching the scale.
                Assert.That(HandleFade(storage, false), Is.True);
                Assert.That(mesh.Instance.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            }

            using (var pool = new PoolFixture(2, 0f))
            {
                var storage = new ProceduralMesh_StorageForSubSurface { UseRuntimeCallback = true };
                GiveTypeStorage(storage, pool.Pool, (new Vector3(1f, 0f, 0f), 1f, false), (new Vector3(2f, 0f, 0f), 1f, true), (new Vector3(3f, 0f, 0f), 1f, true));
                Spawn(storage, false);
                Assert.That(SpawnedPositions(storage), Is.EqualTo(new[] { new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f) }));
                Assert.That(pool.Pool.HasOverflowed, Is.False);
            }
        }

        // HandleFade 0x1A63020: ratio = elapsed / fadeTime (1 - that when fading
        // out); once elapsed > fadeTime the target (1 or 0) is used and the type
        // no longer counts as fading; returns true when nothing is still fading.
        [Test]
        public void FadeScalesTowardsTheTargetAndSnapsOnceTheFadeTimeHasPassed()
        {
            using (var pool = new PoolFixture(2, 1f))
            {
                var storage = new ProceduralMesh_StorageForSubSurface();
                GiveTypeStorage(storage, pool.Pool, (new Vector3(1f, 0f, 0f), 2f, true));
                Spawn(storage, true);
                Assert.That(storage.State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.FadeIn));
                Transform spawned = SpawnedMeshes(storage)[0].Instance.transform;
                Assert.That(spawned.localScale, Is.EqualTo(Vector3.zero));

                FieldInfo stamp = typeof(ProceduralMesh_StorageForSubSurface).GetField("m_fadeTimestamp", Any);
                stamp.SetValue(storage, Time.time - 0.25f);
                float elapsed = Time.time - (float)stamp.GetValue(storage);
                Assert.That(HandleFade(storage, true), Is.False);
                Assert.That(Vector3.Distance(spawned.localScale, new Vector3(2f, 2f, 2f) * elapsed), Is.LessThan(Tolerance));
                Assert.That(HandleFade(storage, false), Is.False);
                Assert.That(Vector3.Distance(spawned.localScale, new Vector3(2f, 2f, 2f) * (1f - elapsed)), Is.LessThan(Tolerance));

                stamp.SetValue(storage, Time.time - 2f);
                Assert.That(HandleFade(storage, true), Is.True);
                Assert.That(spawned.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
                Assert.That(HandleFade(storage, false), Is.True);
                Assert.That(spawned.localScale, Is.EqualTo(Vector3.zero));
            }

            using (var pool = new PoolFixture(1, 1f))
            {
                var storage = new ProceduralMesh_StorageForSubSurface();
                GiveTypeStorage(storage, pool.Pool, (Vector3.zero, 3f, true));
                Spawn(storage, false);
                Assert.That(storage.State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Populated), "no fade without fadeIn");
                Assert.That(SpawnedMeshes(storage)[0].Instance.transform.localScale, Is.EqualTo(new Vector3(3f, 3f, 3f)));
            }
        }

        // ---------------------------------------------------------------- StorageForSurface

        // 0x1A616B0: start = max(start, 0); end = (end == -1) ? count-1 : min(end, count-1).
        [Test]
        public void CreateMeshStorageSplitsPerSubSurfaceOrSpansTheClampedRange()
        {
            var surface = new TestSurface();
            surface.Create(-3, -1, 5, true, 1.5f, true);
            Assert.That(surface.Storage.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
                AssertStorage(surface.Storage[i], i, i + 1, 4, 4, 1.5f, true);

            surface.Create(1, 10, 5, false, 2f, false);
            Assert.That(surface.Storage.Count, Is.EqualTo(1));
            AssertStorage(surface.Storage[0], 1, 4, 4, 3, 2f, false);

            surface.Create(3, 2, 5, true, 1f, false);
            Assert.That(surface.Storage, Is.Empty);
        }

        // 0x1A5B780: zero count without force returns 0 untouched; force
        // despawns unpopulated storage without counting it; populated storage
        // is despawned and counted; canFade fades through StartFadeOut.
        [Test]
        public void DespawnSurfaceMeshesCountsOnlyPopulatedStorage()
        {
            var surface = new TestSurface();
            surface.Create(0, -1, 3, true, 1f, false);
            ProceduralMesh_StorageForSubSurface first = surface.Storage[0];
            ProceduralMesh_StorageForSubSurface second = surface.Storage[1];
            GiveEmptyTypeStorage(first);
            GiveEmptyTypeStorage(second);

            Assert.That(surface.DespawnSurfaceMeshes(false, true), Is.Zero);
            Assert.That(Field(first, "m_meshTypeStorage"), Is.Not.Null);

            surface.PopulatedCount = 2;
            Assert.That(surface.DespawnSurfaceMeshes(false, true), Is.EqualTo(2));
            Assert.That(Field(first, "m_meshTypeStorage"), Is.Not.Null);

            second.FadeIn();
            Assert.That(surface.DespawnSurfaceMeshes(false, true, true), Is.EqualTo(1));
            Assert.That(second.State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated));
            Assert.That(Field(second, "m_meshTypeStorage"), Is.Not.Null, "StartFadeOut keeps the locations");

            second.FadeIn();
            Assert.That(surface.DespawnSurfaceMeshes(false, true), Is.Zero);
            Assert.That(Field(second, "m_meshTypeStorage"), Is.Null);
            Assert.That(Field(first, "m_meshTypeStorage"), Is.Not.Null);

            surface.Movement();
            Assert.That(Field(first, "m_meshTypeStorage"), Is.Null);
            Assert.That(surface.PopulatedCount, Is.Zero);
        }

        // 0x1A63820: the camera is moved into surface space with
        // Inverse(rotation) * (camera - position); beyond despawn it despawns,
        // fading (StartFadeOut, locations kept) only inside fade-out range.
        [Test]
        public void IsTooFarAwayMeasuresInSurfaceSpaceAndFadesInsideFadeOutRange()
        {
            var go = new GameObject("ProceduralMeshStorageSurface");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                var bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
                var cameraPos = new Vector3(10f, 0f, 5f);
                Vector3 local = Quaternion.Inverse(go.transform.rotation) * (cameraPos - go.transform.position);

                var surface = new TestSurface();
                surface.Create(0, -1, 2, false, 1f, false);
                ProceduralMesh_StorageForSubSurface storage = surface.Storage[0];
                GiveEmptyTypeStorage(storage);
                storage.FadeIn();
                surface.PopulatedCount = 1;

                Assert.That(surface.TooFar(go.transform, bounds, cameraPos, 20f, 100f, true, out Vector3 output), Is.False);
                Assert.That(Vector3.Distance(output, local), Is.LessThan(Tolerance));
                Assert.That(surface.PopulatedCount, Is.EqualTo(1));

                Assert.That(surface.TooFar(go.transform, bounds, cameraPos, 10f, 100f, true, out _), Is.True);
                Assert.That(surface.PopulatedCount, Is.Zero);
                Assert.That(Field(storage, "m_meshTypeStorage"), Is.Not.Null, "fading despawn keeps locations");

                storage.FadeIn();
                surface.PopulatedCount = 1;
                Assert.That(surface.TooFar(go.transform, bounds, cameraPos, 10f, 1f, true, out _), Is.True);
                Assert.That(Field(storage, "m_meshTypeStorage"), Is.Null, "direct despawn resets locations");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // CheckForSpawnAndDespawn 0x1A619B0, with the camera inside the surface
        // bounds (no IsTooFarAway despawn) and each storage's bounds preset so
        // SquareDistanceToPoint reads them (+0x28 set). FadeIn/Populated past
        // despawn: SystemConfig +0x2D (LoadBalanceUnpopulate) clears canDespawn;
        // past fadeOut it despawns directly (0x1A61C05, DespawnMeshes(false,
        // reset)), otherwise StartFadeOut(reset) (0x1A61F0F), whose finished
        // DespawnMeshes takes reset as onDestroy and keeps the locations. Inside
        // despawn the frustum test (+0x2B, m_cameraPlanes) runs only while
        // canDespawn holds (0x1A61E90); a failure despawns directly. Unpopulated
        // inside spawn: +0x2C (LoadBalancePopulate) clears canPopulate. FadeOut
        // goes through FadeOut(reset); any other state throws
        // ArgumentOutOfRangeException() (0x1A62186).
        [Test]
        public void CheckForSpawnAndDespawnLoadBalancesAndFadesOnlyInsideFadeOutRange()
        {
            FieldInfo planesField = typeof(ProceduralMesh_Placement).GetField("m_cameraPlanes", Any);
            object priorPlanes = planesField.GetValue(null);
            var host = new GameObject("OriginalProceduralMeshStorageTests.CheckForSpawnAndDespawn");
            var cameraHost = new GameObject("OriginalProceduralMeshStorageTests.Camera");
            var config = ScriptableObject.CreateInstance<ProceduralMesh_SystemConfig>();
            try
            {
                var owner = host.AddComponent<ProceduralMesh_PlacementOnSplineGrid>();
                typeof(ProceduralMesh_Placement).GetProperty("SystemConfig").SetValue(owner, config);
                Camera camera = cameraHost.AddComponent<Camera>();
                var surfaceShape = new BoundsSurface(new Bounds(Vector3.zero, new Vector3(10f, 10f, 10f)));
                // Squared distances from the camera at the origin: far 81, near 4.
                var far = new Bounds(new Vector3(0f, 0f, 10f), new Vector3(2f, 2f, 2f));
                var near = new Bounds(new Vector3(0f, 0f, 3f), new Vector3(2f, 2f, 2f));
                const float spawn = 10f, despawn = 50f, fadeIn = 5f, fadeOut = 100f, fadeOutClose = 60f;

                int Check(TestSurface target, float fadeOutDistance) =>
                    target.Check(surfaceShape, host.transform, camera, spawn, despawn, fadeIn, fadeOutDistance, true, owner);

                // Fade-out path, one despawn per call under load balancing.
                SetConfig(config, "m_loadBalanceUnpopulate", true);
                TestSurface surface = SurfaceWith(true, far, far);
                Assert.That(Check(surface, fadeOut), Is.EqualTo(1));
                Assert.That(surface.Storage[0].State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated));
                Assert.That(Field(surface.Storage[0], "m_meshTypeStorage"), Is.Not.Null, "the fade path keeps the locations");
                Assert.That(surface.Storage[1].State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Populated));
                Assert.That(Check(surface, fadeOut), Is.Zero);
                Assert.That(surface.Storage[1].State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated));

                // Past fade-out: direct despawn drops the locations with reset.
                surface = SurfaceWith(true, far, far);
                Assert.That(Check(surface, fadeOutClose), Is.EqualTo(1));
                Assert.That(Field(surface.Storage[0], "m_meshTypeStorage"), Is.Null, "direct despawn resets the locations");
                Assert.That(Field(surface.Storage[1], "m_meshTypeStorage"), Is.Not.Null);

                // The frustum test is skipped once canDespawn is cleared.
                SetConfig(config, "m_useCameraFrustum", true);
                planesField.SetValue(null, new[] { new Plane(Vector3.up, -1000f) });
                surface = SurfaceWith(true, far, near);
                Assert.That(Check(surface, fadeOut), Is.EqualTo(1));
                Assert.That(surface.Storage[1].State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Populated));

                SetConfig(config, "m_loadBalanceUnpopulate", false);
                surface = SurfaceWith(true, far, near);
                Assert.That(Check(surface, fadeOut), Is.Zero, "without load balancing both despawn");
                Assert.That(Field(surface.Storage[1], "m_meshTypeStorage"), Is.Null, "a frustum failure despawns directly");
                SetConfig(config, "m_useCameraFrustum", false);

                // A FadeIn storage inside range keeps fading (no mesh types: it finishes).
                surface = SurfaceWith(true, near);
                SetState(surface.Storage[0], ProceduralMesh_StorageForSubSurface.StorageState.FadeIn);
                Assert.That(Check(surface, fadeOut), Is.EqualTo(1));
                Assert.That(surface.Storage[0].State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Populated));

                // FadeOut storage finishes through FadeOut(reset), which keeps the locations.
                surface = SurfaceWith(true, near);
                SetState(surface.Storage[0], ProceduralMesh_StorageForSubSurface.StorageState.FadeOut);
                Assert.That(Check(surface, fadeOut), Is.Zero);
                Assert.That(surface.Storage[0].State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated));
                Assert.That(Field(surface.Storage[0], "m_meshTypeStorage"), Is.Not.Null);

                // Spawning: one per call under LoadBalancePopulate.
                SetConfig(config, "m_loadBalancePopulate", true);
                surface = SurfaceWith(false, near, near);
                Assert.That(Check(surface, fadeOut), Is.EqualTo(1));
                Assert.That(surface.Storage[0].State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Populated));
                Assert.That(surface.Storage[1].State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated));
                SetConfig(config, "m_loadBalancePopulate", false);
                surface = SurfaceWith(false, near, near);
                Assert.That(Check(surface, fadeOut), Is.EqualTo(2));

                surface = SurfaceWith(true, near);
                SetState(surface.Storage[0], (ProceduralMesh_StorageForSubSurface.StorageState)4);
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => Check(surface, fadeOut));
                Assert.That(thrown.ParamName, Is.Null);
            }
            finally
            {
                planesField.SetValue(null, priorPlanes);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(cameraHost);
                Object.DestroyImmediate(host);
            }
        }

        // .ctor 0x1A5B540 + CreateMeshStorage 0x1A61680: (0, -1, count 2) gives one 0..1 storage.
        [Test]
        public void MeshStorageWrapsTheColliderInOneTwoPointStorage()
        {
            var go = new GameObject("ProceduralMeshStorageMesh");
            try
            {
                var collider = go.AddComponent<MeshCollider>();
                var storage = new ProceduralMesh_StorageForMesh(collider, 1.25f, true);
                var list = (List<ProceduralMesh_StorageForSubSurface>)typeof(ProceduralMesh_StorageForSurface).GetField("m_meshStorage", Any).GetValue(storage);
                Assert.That(list.Count, Is.EqualTo(1));
                AssertStorage(list[0], 0, 1, 1, 1, 1.25f, true);
                var meshSurface = (MeshSurface)typeof(ProceduralMesh_StorageForMesh).GetField("m_meshSurface", Any).GetValue(storage);
                Assert.That(meshSurface.Mesh, Is.SameAs(collider));
                Assert.That(storage.CalculateTotalWorldSpaceBounds(), Is.EqualTo(collider.bounds));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static float ConvertBias(float bias, float left, float right)
        {
            MethodInfo method = typeof(LocationGenerator).GetMethod("ConvertBiasToRatio", Any, null,
                new[] { typeof(float), typeof(float), typeof(float) }, null);
            return (float)method.Invoke(null, new object[] { bias, left, right });
        }

        private static float TBias(double draw, LocationGenerator.BiasDensity density)
        {
            return (float)typeof(LocationGenerator).GetMethod("GetTBias", Any)
                .Invoke(null, new object[] { new SequenceRandom(draw), -1f, 2f, density });
        }

        private static void AssertZones(string field, string blobName, params int[] counts)
        {
            var zones = (float[])typeof(LocationGenerator).GetField(field, Any).GetValue(null);
            var expected = new List<float>();
            for (int zone = 0; zone < counts.Length; zone++)
                for (int i = 0; i < counts[zone]; i++)
                    expected.Add(zone * 0.125f);
            Assert.That(zones, Is.EqualTo(expected), field);

            Assert.That(BitConverter.IsLittleEndian, Is.True, "the blob hash covers little-endian float bytes");
            var bytes = new byte[zones.Length * sizeof(float)];
            Buffer.BlockCopy(zones, 0, bytes, 0, bytes.Length);
            using (var sha = SHA256.Create())
                Assert.That(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""), Is.EqualTo(blobName), field + " blob hash");
        }

        private static void AssertRotation(Quaternion actual, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.01f), actual + " vs " + expected);
        }

        private static void AssertStorage(ProceduralMesh_StorageForSubSurface storage, int start, int end, int range, int sectionRange, float multiplier, bool runtime)
        {
            Assert.That(storage.StartSubSurface, Is.EqualTo(start));
            Assert.That(storage.EndSubSurface, Is.EqualTo(end));
            Assert.That(storage.SubSurfaceRange, Is.EqualTo(range));
            Assert.That(storage.SubSurfaceSectionRange, Is.EqualTo(sectionRange));
            Assert.That(storage.NumMeshesMultiplier, Is.EqualTo(multiplier));
            Assert.That(storage.UseRuntimeCallback, Is.EqualTo(runtime));
            Assert.That(storage.State, Is.EqualTo(ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated));
        }

        private static void SetScaleSetup(ScriptableObject setup, float min, float max)
        {
            setup.GetType().GetField("m_minScale", Any).SetValue(setup, min);
            setup.GetType().GetField("m_maxScale", Any).SetValue(setup, max);
            setup.GetType().GetField("m_useParentScale", Any).SetValue(setup, true);
        }

        private static List<LightweightTransform> TwoIdentityLocations() => new List<LightweightTransform>
        {
            new LightweightTransform(Vector3.zero, Quaternion.identity),
            new LightweightTransform(Vector3.forward, Quaternion.identity),
        };

        // Two locations; each draws once on the drawing path and keeps minScale otherwise.
        private static float[] ExpectedScales(Random reference, (float parent, float min, float max, bool draws) row)
        {
            var scales = new float[2];
            for (int i = 0; i < scales.Length; i++)
                scales[i] = row.draws ? ((float)reference.NextDouble() * (row.max - row.min) + row.min) * row.parent : row.min;
            return scales;
        }

        private static float[] StoredScales(ProceduralMesh_StorageForSubSurface storage)
        {
            var typeStorage = (IList)Field(storage, "m_meshTypeStorage");
            Assert.That(typeStorage.Count, Is.EqualTo(1));
            var locations = (IList)Field(typeStorage[0], "m_meshLocations");
            var scales = new float[locations.Count];
            for (int i = 0; i < scales.Length; i++)
                scales[i] = (float)Field(locations[i], "m_scale");
            return scales;
        }

        private static object Field(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);

        // An empty MeshTypeStorage list: the fade and despawn loops run over no mesh types.
        private static IList GiveEmptyTypeStorage(ProceduralMesh_StorageForSubSurface storage)
        {
            Type element = typeof(ProceduralMesh_StorageForSubSurface).GetNestedType("MeshTypeStorage", Any);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element));
            typeof(ProceduralMesh_StorageForSubSurface).GetField("m_meshTypeStorage", Any).SetValue(storage, list);
            return list;
        }

        private static ProceduralMesh_StorageForSubSurface CountingStorage() => new ProceduralMesh_StorageForSubSurface
        {
            StartSubSurface = 1,
            EndSubSurface = 3,
            SubSurfaceRange = 4,
            SubSurfaceSectionRange = 2,
            NumMeshesMultiplier = 1f,
        };

        private static List<LightweightTransform> FiveGridLocations()
        {
            var locations = new List<LightweightTransform>();
            for (int i = 0; i < 5; i++)
                locations.Add(new LightweightTransform(new Vector3(i, 0f, 0f), Quaternion.identity));
            return locations;
        }

        // Stored world positions, one array per mesh type.
        private static Vector3[][] StoredPositions(ProceduralMesh_StorageForSubSurface storage)
        {
            var typeStorage = (IList)Field(storage, "m_meshTypeStorage");
            var result = new Vector3[typeStorage.Count][];
            for (int i = 0; i < result.Length; i++)
            {
                var locations = (IList)Field(typeStorage[i], "m_meshLocations");
                result[i] = new Vector3[locations.Count];
                for (int j = 0; j < locations.Count; j++)
                    result[i][j] = (Vector3)Field(locations[j], "m_position");
            }
            return result;
        }

        private static void SetConfig(ProceduralMesh_SystemConfig config, string field, bool value) =>
            typeof(ProceduralMesh_SystemConfig).GetField(field, Any).SetValue(config, value);

        private static void SetState(ProceduralMesh_StorageForSubSurface storage, ProceduralMesh_StorageForSubSurface.StorageState state) =>
            typeof(ProceduralMesh_StorageForSubSurface).GetProperty("State").SetValue(storage, state);

        // One storage per bounds, each with empty type storage and its bounds
        // already calculated; populated storage is counted in m_populatedCount.
        private static TestSurface SurfaceWith(bool populated, params Bounds[] bounds)
        {
            var surface = new TestSurface();
            surface.Create(0, -1, bounds.Length + 1, true, 1f, false);
            for (int i = 0; i < bounds.Length; i++)
            {
                ProceduralMesh_StorageForSubSurface storage = surface.Storage[i];
                GiveEmptyTypeStorage(storage);
                if (populated)
                    storage.FadeIn();
                typeof(ProceduralMesh_StorageForSubSurface).GetField("m_bounds", Any).SetValue(storage, bounds[i]);
                typeof(ProceduralMesh_StorageForSubSurface).GetField("m_boundsCalculated", Any).SetValue(storage, true);
            }
            surface.PopulatedCount = populated ? bounds.Length : 0;
            return surface;
        }

        // One MeshTypeStorage on the given pool with (position, scale, active) locations.
        private static void GiveTypeStorage(ProceduralMesh_StorageForSubSurface storage, ProceduralMesh_Pool pool, params (Vector3 position, float scale, bool active)[] locations)
        {
            Type owner = typeof(ProceduralMesh_StorageForSubSurface);
            Type storageType = owner.GetNestedType("MeshTypeStorage", Any);
            Type locationType = owner.GetNestedType("MeshLocation", Any);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(storageType));
            object typeStorage = Activator.CreateInstance(storageType);
            var meshLocations = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(locationType));
            foreach (var location in locations)
            {
                object boxed = Activator.CreateInstance(locationType, location.position, Quaternion.identity, location.scale);
                locationType.GetField("m_active").SetValue(boxed, location.active);
                meshLocations.Add(boxed);
            }
            storageType.GetField("m_meshPool").SetValue(typeStorage, pool);
            storageType.GetField("m_meshes").SetValue(typeStorage, new List<PooledPrefab<ProceduralMesh>>());
            storageType.GetField("m_meshLocations").SetValue(typeStorage, meshLocations);
            list.Add(typeStorage);
            owner.GetField("m_meshTypeStorage", Any).SetValue(storage, list);
        }

        private static void Spawn(ProceduralMesh_StorageForSubSurface storage, bool fadeIn) =>
            typeof(ProceduralMesh_StorageForSubSurface).GetMethod("SpawnFromLocations", Any).Invoke(storage, new object[] { fadeIn });

        private static bool HandleFade(ProceduralMesh_StorageForSubSurface storage, bool fadeIn) =>
            (bool)typeof(ProceduralMesh_StorageForSubSurface).GetMethod("HandleFade", Any).Invoke(storage, new object[] { fadeIn });

        private static List<PooledPrefab<ProceduralMesh>> SpawnedMeshes(ProceduralMesh_StorageForSubSurface storage) =>
            (List<PooledPrefab<ProceduralMesh>>)Field(((IList)Field(storage, "m_meshTypeStorage"))[0], "m_meshes");

        private static Vector3[] SpawnedPositions(ProceduralMesh_StorageForSubSurface storage) =>
            SpawnedMeshes(storage).ConvertAll(mesh => mesh.Instance.transform.position).ToArray();

        // A pool whose free stack holds `capacity` instances and one active
        // bucket. Its GameObject stays inactive, so Awake (and with it the
        // PrefabPoolManager lookup in Initialise) never runs.
        private sealed class PoolFixture : IDisposable
        {
            private const BindingFlags Own = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly List<GameObject> owned = new List<GameObject>();

            public ProceduralMesh_Pool Pool { get; private set; }

            public PoolFixture(int capacity, float fadeTimeSeconds)
            {
                try
                {
                    Build(capacity, fadeTimeSeconds);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private void Build(int capacity, float fadeTimeSeconds)
            {
                var poolHost = new GameObject("OriginalProceduralMeshStorageTests.Pool");
                poolHost.SetActive(false);
                owned.Add(poolHost);
                Pool = poolHost.AddComponent<ProceduralMesh_Pool>();
                typeof(ProceduralMesh_Pool).GetField("m_fadeTimeSeconds", Own).SetValue(Pool, fadeTimeSeconds);

                var bucket = new GameObject("OriginalProceduralMeshStorageTests.PoolActiveBucket");
                owned.Add(bucket);
                var free = new Stack<PooledPrefab<ProceduralMesh>>();
                var objects = new List<ProceduralMesh>();
                for (int i = 0; i < capacity; i++)
                {
                    var instance = new GameObject("OriginalProceduralMeshStorageTests.PoolInstance" + i);
                    owned.Insert(0, instance);
                    ProceduralMesh mesh = instance.AddComponent<ProceduralMesh>();
                    objects.Add(mesh);
                    free.Push(new PooledPrefab<ProceduralMesh>(mesh, i));
                }
                Type pool = typeof(PrefabPool<ProceduralMesh>);
                pool.GetField("m_freeInactiveObjects", Own).SetValue(Pool, free);
                pool.GetField("m_freeActiveObjects", Own).SetValue(Pool, new Stack<PooledPrefab<ProceduralMesh>>());
                pool.GetField("m_objects", Own).SetValue(Pool, objects);
                pool.GetField("m_activeBuckets", Own).SetValue(Pool, new List<GameObject> { bucket });
            }

            // Instances first: spawning parents them under the bucket.
            public void Dispose()
            {
                foreach (GameObject gameObject in owned)
                    if (gameObject != null)
                        Object.DestroyImmediate(gameObject);
            }
        }

        // Only GetBoundingBox is reached by CheckForSpawnAndDespawn.
        private sealed class BoundsSurface : ISurface
        {
            private readonly Bounds bounds;

            public BoundsSurface(Bounds bounds) => this.bounds = bounds;

            public Bounds GetBoundingBox(bool inWorldSpace = false) => bounds;
            public DateTime GetTimestamp() => throw Unused();
            public MetadataGroups GetSurfaceMetadata() => throw Unused();
            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => throw Unused();
            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => throw Unused();
            public float Length => throw Unused();
            public float LengthInverse => throw Unused();
            public Vector3 TransformPoint(Vector3 point) => throw Unused();
            public Vector3 InverseTransformPoint(Vector3 point) => throw Unused();
            public bool IsActive() => throw Unused();
        }

        private sealed class TestSurface : ProceduralMesh_StorageForSurface
        {
            public TestSurface() : base(0, 0, false, 0f, false) { }

            public List<ProceduralMesh_StorageForSubSurface> Storage => m_meshStorage;

            public int PopulatedCount
            {
                get => (int)typeof(ProceduralMesh_StorageForSurface).GetField("m_populatedCount", Any).GetValue(this);
                set => typeof(ProceduralMesh_StorageForSurface).GetField("m_populatedCount", Any).SetValue(this, value);
            }

            public void Create(int start, int end, int count, bool perSubSurface, float multiplier, bool runtime) =>
                CreateMeshStorage(start, end, count, perSubSurface, multiplier, runtime);

            public void Movement() => MovementDetected();

            public int Check(ISurface surface, Transform transform, Camera camera, float spawn, float despawn, float fadeIn, float fadeOut, bool reset, ProceduralMesh_Placement owner) =>
                CheckForSpawnAndDespawn(surface, transform, m_meshStorage, camera, spawn, despawn, fadeIn, fadeOut, reset, owner);

            public bool TooFar(Transform transform, Bounds bounds, Vector3 cameraPos, float despawn, float fadeOut, bool reset, out Vector3 local)
            {
                object[] args = { transform, bounds, cameraPos, despawn, fadeOut, reset, null };
                bool result = (bool)typeof(ProceduralMesh_StorageForSurface).GetMethod("IsTooFarAway", Any).Invoke(this, args);
                local = (Vector3)args[6];
                return result;
            }
        }

        private sealed class SequenceRandom : Random
        {
            private readonly Queue<double> draws;
            public int Draws { get; private set; }

            public SequenceRandom(params double[] values) : base(0) => draws = new Queue<double>(values);

            public override double NextDouble()
            {
                Draws++;
                return draws.Dequeue();
            }
        }

        private static Exception Unused() => new NotSupportedException("Unused procedural fixture member");

        private sealed class FakeKnot : IRibbonKnot, ISplineKnot
        {
            public FakeKnot(float length) => Length = length;
            public float Length { get; }
            public LightweightTransform Transform => throw Unused();
            public MetadataGroups Metadata => throw Unused();
            public Vector3 LocalControlPointNext => throw Unused();
            public Vector3 WorldControlPointNext => throw Unused();
            public Vector3 LocalControlPointPrev => throw Unused();
            public Vector3 WorldControlPointPrev => throw Unused();
            public Vector3 TangentNext => throw Unused();
            public Vector3 TangentPrev => throw Unused();
            public float LengthInverse => throw Unused();
        }

        // Location (knot, t, 0), rotation Euler(0, 10*knot, 0).
        private sealed class FakeSpline : ISpline
        {
            private readonly float[] lengths;

            public FakeSpline(int knotCount, params float[] lengths)
            {
                KnotCount = knotCount;
                this.lengths = lengths;
            }

            public static Vector3 At(int knot, float t) => new Vector3(knot, t, 0f);

            public int KnotCount { get; }
            public ISplineKnot GetKnot(int knotIndex) => new FakeKnot(lengths[knotIndex]);
            public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio) =>
                new LightweightTransform(At(linearRatio.KnotIndex, linearRatio.T), Quaternion.Euler(0f, 10f * linearRatio.KnotIndex, 0f));

            public LightweightTransform Transform => throw Unused();
            public SplineType SplineType => throw Unused();
            public ISplineKnot GetBackKnot() => throw Unused();
            public ISplineKnot GetHeadKnot() => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public SplineLocation FindNearestSplineLocation(Vector3 worldPosition) => throw Unused();
            public SplineLocation FindAdjacentSplineLocation(Vector3 worldPosition, Vector3 forward) => throw Unused();
            public SplineLocation GetSplineLocationFromDistance(float distance) => throw Unused();
            public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) => throw Unused();
            public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT) => throw Unused();
            public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT) => throw Unused();
            public Vector3 GetLocalPosFromKnotT(KnotT knotT) => throw Unused();
            public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public LightweightTransform GetWorldTransformFromDistance(float distance) => throw Unused();
            public LightweightTransform GetWorldTransformFromSplineT(float splineT) => throw Unused();
            public void KnotsUpdated() => throw Unused();
            public void UpdateSplineType(ISpline newSpline) => throw Unused();
            public event Action OnSplineKnotsUpdated { add => throw Unused(); remove => throw Unused(); }
            public event Action<ISpline> OnSplineTypeChanged { add => throw Unused(); remove => throw Unused(); }
            public DateTime GetTimestamp() => throw Unused();
            public MetadataGroups GetSurfaceMetadata() => throw Unused();
            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => throw Unused();
            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => throw Unused();
            public float Length => throw Unused();
            public float LengthInverse => throw Unused();
            public Bounds GetBoundingBox(bool inWorldSpace = false) => throw Unused();
            public Vector3 TransformPoint(Vector3 point) => throw Unused();
            public Vector3 InverseTransformPoint(Vector3 point) => throw Unused();
            public bool IsActive() => throw Unused();
        }

        // Every knot has the same length. Centre (0, 0, knot + t) with identity
        // rotation; left (-2, 0, ...) and right (rightHalfWidth, 0, ...) with
        // distinct rotations.
        private sealed class FakeRibbon : IRibbon
        {
            public static readonly Quaternion LeftRotation = Quaternion.Euler(0f, -90f, 0f);
            public static readonly Quaternion RightRotation = Quaternion.Euler(0f, 90f, 0f);
            private readonly float knotLength;
            private readonly float rightHalfWidth;

            public FakeRibbon(int knotCount, float knotLength, float rightHalfWidth = 2f)
            {
                KnotCount = knotCount;
                this.knotLength = knotLength;
                this.rightHalfWidth = rightHalfWidth;
            }

            public int KnotReads { get; private set; }
            public int KnotCount { get; }

            public IRibbonKnot GetKnot(int knotIndex, RibbonAlignment alignment)
            {
                Assert.That(alignment, Is.EqualTo(RibbonAlignment.Center));
                KnotReads++;
                return new FakeKnot(knotLength);
            }

            public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment)
            {
                float z = linearRatio.KnotIndex + linearRatio.T;
                switch (alignment)
                {
                    case RibbonAlignment.Left: return new LightweightTransform(new Vector3(-2f, 0f, z), LeftRotation);
                    case RibbonAlignment.Right: return new LightweightTransform(new Vector3(rightHalfWidth, 0f, z), RightRotation);
                    default: return new LightweightTransform(new Vector3(0f, 0f, z), Quaternion.identity);
                }
            }

            public LightweightTransform Transform => throw Unused();
            public IRibbonKnot GetBackKnot(RibbonAlignment alignment) => throw Unused();
            public IRibbonKnot GetHeadKnot(RibbonAlignment alignment) => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public RibbonLocation FindNearestRibbonLocation(Vector3 worldPosition, RibbonAlignment alignment) => throw Unused();
            public RibbonLocation FindAdjacentRibbonLocation(Vector3 worldPosition, Vector3 forward, RibbonAlignment alignment) => throw Unused();
            public RibbonLocation GetRibbonLocationFromDistance(float distance, RibbonAlignment alignment) => throw Unused();
            public LinearRatio GetLinearRatioFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw Unused();
            public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw Unused();
            public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw Unused();
            public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw Unused();
            public Vector3 GetLocalPosFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw Unused();
            public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw Unused();
            public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw Unused();
            public LightweightTransform GetWorldTransformFromDistance(float distance) => throw Unused();
            public LightweightTransform GetWorldTransformFromRibbonT(float ribbonT) => throw Unused();
            public void KnotsUpdated() => throw Unused();
            public void UpdateRibbonType(IRibbon newRibbon) => throw Unused();
            public bool TryGetRaycastHit(Ray ray, float distance, out RaycastHit raycastHit) => throw Unused();
            public event Action OnRibbonKnotsUpdated { add => throw Unused(); remove => throw Unused(); }
            public event Action<IRibbon> OnRibbonTypeChanged { add => throw Unused(); remove => throw Unused(); }
            public DateTime GetTimestamp() => throw Unused();
            public MetadataGroups GetSurfaceMetadata() => throw Unused();
            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => throw Unused();
            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => throw Unused();
            public float Length => throw Unused();
            public float LengthInverse => throw Unused();
            public Bounds GetBoundingBox(bool inWorldSpace = false) => throw Unused();
            public Vector3 TransformPoint(Vector3 point) => throw Unused();
            public Vector3 InverseTransformPoint(Vector3 point) => throw Unused();
            public bool IsActive() => throw Unused();
        }
    }
}
