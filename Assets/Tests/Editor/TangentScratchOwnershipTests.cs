using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class TangentScratchOwnershipTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly FieldInfo Tan1 = typeof(MeshUtility).GetField("tan1", Static);
        private static readonly FieldInfo Tan2 = typeof(MeshUtility).GetField("tan2", Static);
        private static readonly FieldInfo Uvs = typeof(MeshGenerator).GetField("__uvs", Static);
        private static Vector3[] Read(FieldInfo field) => (Vector3[])field.GetValue(null);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static int[] Bits(Vector3[] values) => values.SelectMany(v => new[] { Bits(v.x), Bits(v.y), Bits(v.z) }).ToArray();
        private static float Float(int bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);

        private sealed class OuterScratch : IDisposable
        {
            private readonly Vector3[] first = Read(Tan1), second = Read(Tan2);
            private readonly Vector2 uvs = (Vector2)Uvs.GetValue(null);
            internal readonly Vector3[] Sentinel1, Sentinel2;
            private readonly int[] firstBits, secondBits;
            internal OuterScratch(int count)
            {
                Sentinel1 = Enumerable.Range(0, count).Select(i => new Vector3(i + 1, Float(unchecked((int)0x80000000)), i + 3)).ToArray();
                Sentinel2 = Enumerable.Range(0, count).Select(i => new Vector3(i + 4, i + 5, i + 6)).ToArray();
                firstBits = Bits(Sentinel1); secondBits = Bits(Sentinel2);
                Tan1.SetValue(null, Sentinel1); Tan2.SetValue(null, Sentinel2);
            }
            internal void Check()
            {
                TestContext.WriteLine("Restored scratch references: " + ReferenceEquals(Read(Tan1), Sentinel1) + ", " + ReferenceEquals(Read(Tan2), Sentinel2));
                Assert.That(Read(Tan1), Is.SameAs(Sentinel1));
                Assert.That(Read(Tan2), Is.SameAs(Sentinel2));
                Assert.That(Bits(Sentinel1), Is.EqualTo(firstBits));
                Assert.That(Bits(Sentinel2), Is.EqualTo(secondBits));
                Vector2 now = (Vector2)Uvs.GetValue(null);
                Assert.That(new[] { Bits(now.x), Bits(now.y) }, Is.EqualTo(new[] { Bits(uvs.x), Bits(uvs.y) }));
            }
            public void Dispose() { Tan1.SetValue(null, first); Tan2.SetValue(null, second); Uvs.SetValue(null, uvs); }
        }

        private static TS_Mesh Triangle(int count = 3, bool flipped = false)
        {
            var mesh = new TS_Mesh();
            mesh.vertices = count == 3 ? new[] { Vector3.zero, Vector3.right, Vector3.up } : new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward };
            mesh.normals = Enumerable.Repeat(Vector3.forward, count).ToArray();
            mesh.uv = count == 3 ? new[] { Vector2.zero, Vector2.right, new Vector2(0, flipped ? -1 : 1) } : new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 1, 2 };
            return mesh;
        }

        private static void ExerciseAccumulationAndReplacement()
        {
            // Original 06000029 @ 4FA4E0 keeps same-length scratch and adds each triangle's directions.
            MeshUtility.CalculateTangents(Triangle());
            Vector3[] first = Read(Tan1), second = Read(Tan2);
            float priorX = first[0].x, priorY = second[0].y;
            MeshUtility.CalculateTangents(Triangle());
            Assert.That(Read(Tan1), Is.SameAs(first)); Assert.That(Read(Tan2), Is.SameAs(second));
            Assert.That(first[0].x, Is.EqualTo(priorX + 1)); Assert.That(second[0].y, Is.EqualTo(priorY + 1));
            MeshUtility.CalculateTangents(Triangle(4));
            Assert.That(Read(Tan1), Is.Not.SameAs(first)); Assert.That(Read(Tan2), Is.Not.SameAs(second));
            Assert.That(Read(Tan1).Length, Is.EqualTo(4)); Assert.That(Read(Tan2).Length, Is.EqualTo(4));
        }

        private static void Lifecycle(object fixture, Type attribute)
        {
            foreach (MethodInfo method in fixture.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.IsDefined(attribute, true)))
                Invoke(() => method.Invoke(fixture, null));
        }
        private static void Invoke(Action body)
        {
            try { body(); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static void Capture(List<Exception> errors, Action body)
        {
            try { body(); }
            catch (AggregateException error) { errors.AddRange(error.Flatten().InnerExceptions); }
            catch (Exception error) { errors.Add(error); }
        }
        private static void Remember(object fixture, List<Object> objects)
        {
            foreach (string field in new[] { "_owned", "_meshes", "_ownedAssets" })
            {
                FieldInfo info = fixture.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (info == null) continue;
                foreach (Object item in (IEnumerable)info.GetValue(fixture))
                    if (!ReferenceEquals(item, null) && !objects.Any(x => ReferenceEquals(x, item))) objects.Add(item);
            }
        }

        private static void RunFixture(string route, string fault, Action duringBody, Action verifyBoundary = null, Action beforeLifecycle = null)
        {
            object fixture = route == "generator" ? (object)new DreamteckGeneratorFixture() : new OriginalDreamteckObjectBenderTests();
            var objects = new List<Object>();
            var errors = new List<Exception>();
            var sentinel = new InvalidOperationException("owned fixture fault");
            FieldInfo hook = fixture.GetType().GetField("OwnershipFault");
            var prior = (Action<string>)hook.GetValue(fixture);
            bool fired = false;
            int destroyed = 0;
            bool beforeObserved = false;
            hook.SetValue(fixture, (Action<string>)(stage =>
            {
                Remember(fixture, objects);
                if (stage == "before-component" && !beforeObserved)
                {
                    beforeObserved = true;
                    beforeLifecycle?.Invoke();
                }
                if (stage == "after-destroy") destroyed++;
                if (!fired && ((fault == "setup" && stage == "after-acquire") || (fault == "cleanup" && stage == "after-destroy")))
                {
                    if (fault == "setup") duringBody();
                    fired = true; throw sentinel;
                }
            }));
            try
            {
                try
                {
                    Lifecycle(fixture, typeof(SetUpAttribute));
                    if (fixture is DreamteckGeneratorFixture generator)
                    {
                        generator.NewObject("extra owned cleanup target");
                        PathGenerator path = generator.NewGenerator<PathGenerator>(generator.NewLine());
                        path.calculateTangents = true;
                        path.RebuildImmediate();
                    }
                    else
                    {
                        var bender = (OriginalDreamteckObjectBenderTests)fixture;
                        if (fault == "setup") bender.UpdateReferencesCreatesOnePropertyPerTransformDepthFirst();
                        else bender.AColliderWithItsOwnMeshTakesABentCopyOfThatMesh();
                    }
                    duringBody();
                    if (fault == "body") { fired = true; throw sentinel; }
                }
                catch (Exception error) { errors.Add(error); }
                finally
                {
                    Remember(fixture, objects);
                    if (fixture is IDisposable disposable) Capture(errors, disposable.Dispose);
                    else Capture(errors, () => Lifecycle(fixture, typeof(TearDownAttribute)));
                }
                verifyBoundary?.Invoke();
                Assert.That(fired, Is.EqualTo(fault != "none"));
                Assert.That(errors.Count, Is.EqualTo(fault == "none" ? 0 : 1), string.Join("\n", errors.Select(e => e.ToString())));
                if (fault != "none") Assert.That(errors[0], Is.SameAs(sentinel));
                if (fault == "cleanup") Assert.That(destroyed, Is.GreaterThan(1), "later owned destruction must still be attempted");
                Assert.That(objects.All(o => o == null), Is.True, "every acquired object and asset was destroyed before outer recovery");
            }
            finally
            {
                hook.SetValue(fixture, prior);
                // Recovery belongs to this regression, and follows (never satisfies) the boundary assertions.
                var recovery = new List<Exception>();
                foreach (Object item in objects) if (item != null) Capture(recovery, () => Object.DestroyImmediate(item));
                if (recovery.Count != 0) throw new AggregateException("Owned regression recovery", recovery);
            }
        }

        [TestCase("generator", 3)]
        [TestCase("generator", 4)]
        [TestCase("bender", 3)]
        [TestCase("bender", 4)]
        public void FixtureDetachesScratchBeforeItsFirstComponent(string route, int count)
        {
            using (var outer = new OuterScratch(count))
            {
                bool observed = false;
                RunFixture(route, "none", () => { }, outer.Check, () =>
                {
                    observed = true;
                    Assert.That(Read(Tan1), Is.Not.SameAs(outer.Sentinel1));
                    Assert.That(Read(Tan2), Is.Not.SameAs(outer.Sentinel2));
                    MeshUtility.CalculateTangents(Triangle(count));
                });
                Assert.That(observed, Is.True, "the before-component boundary was exercised");
            }
        }

        [TestCase("generator", "none", 3)]
        [TestCase("generator", "body", 3)]
        [TestCase("generator", "setup", 3)]
        [TestCase("generator", "cleanup", 3)]
        [TestCase("generator", "none", 4)]
        [TestCase("bender", "none", 3)]
        [TestCase("bender", "body", 3)]
        [TestCase("bender", "setup", 3)]
        [TestCase("bender", "cleanup", 3)]
        [TestCase("bender", "none", 4)]
        public void FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource(string route, string fault, int count)
        {
            using (var outer = new OuterScratch(count))
            {
                RunFixture(route, fault, ExerciseAccumulationAndReplacement, outer.Check);
            }
        }

        [TestCase("generator")]
        [TestCase("bender")]
        public void FollowingTriangleMatchesUninterruptedOriginalAccumulation(string route)
        {
            using (var outer = new OuterScratch(4))
            {
                MeshUtility.CalculateTangents(Triangle());
                TS_Mesh control = Triangle(flipped: true); MeshUtility.CalculateTangents(control);
                // Reallocate by a different vertex count, then repeat the same original first call.
                MeshUtility.CalculateTangents(Triangle(4)); MeshUtility.CalculateTangents(Triangle());
                RunFixture(route, "none", () => { });
                TS_Mesh after = Triangle(flipped: true); MeshUtility.CalculateTangents(after);
                Vector4[] expected = Enumerable.Repeat(new Vector4(1, 0, 0, 1), 3).ToArray();
                Assert.That(control.tangents, Is.EqualTo(expected));
                Assert.That(after.tangents, Is.EqualTo(expected));
                Assert.That(after.tangents, Is.EqualTo(control.tangents));
            }
        }
    }
}
