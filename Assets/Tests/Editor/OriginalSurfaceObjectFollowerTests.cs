using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.SurfaceObjectFollower (TypeDef 0x02000a2e, x86-64 0x6B39F0..0x6B6970) with
    // SplineObjectFollower (0x02000a2d) and RibbonObjectFollower (0x02000a1f) on owned objects.
    // UpdateTransform runs against a scripted surface that records each query. The constructor
    // takes the typed LevelManager reference, so every case leases that row; Awake and the level
    // callbacks are not run because InvokeOnValid would queue work on it. As in the leave-checker
    // tests, an owned empty TimeCategoryLookup stands in the TimeScaledComponent_SDT cache that
    // the Editor's Reset/OnValidate read while the component is added.
    public sealed class OriginalSurfaceObjectFollowerTests
    {
        private sealed class ScriptedSurface : ISurface
        {
            public SurfaceLocation Location;
            public readonly List<Vector3> Queries = new List<Vector3>();

            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition)
            {
                Queries.Add(worldPosition);
                return Location;
            }

            public DateTime GetTimestamp() => throw new NotSupportedException();
            public MetadataGroups GetSurfaceMetadata() => throw new NotSupportedException();
            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => throw new NotSupportedException();
            public float Length => throw new NotSupportedException();
            public float LengthInverse => throw new NotSupportedException();
            public Bounds GetBoundingBox(bool inWorldSpace = false) => throw new NotSupportedException();
            public Vector3 TransformPoint(Vector3 point) => throw new NotSupportedException();
            public Vector3 InverseTransformPoint(Vector3 point) => throw new NotSupportedException();
            public bool IsActive() => throw new NotSupportedException();
        }

        private sealed class Fixture : IDisposable
        {
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-surface-follower");
            private readonly VerificationFixtureOwnership.LifecycleRow m_levelRow;
            private readonly object m_priorConfig;
            public readonly SplineObjectFollower Follower;
            public readonly Spline Facade;
            public readonly Transform Target;
            public readonly ScriptedSurface Surface = new ScriptedSurface();

            public Fixture()
            {
                m_levelRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<LevelManager>());
                m_priorConfig = GetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal");
                try
                {
                    SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", Objects.Own(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                    GameObject host = Objects.Create("follower");
                    host.SetActive(false);
                    Facade = host.AddComponent<Spline>();
                    // The Editor may hold an unassigned serialized reference as a fake null; the
                    // facade's CLR-null answer is what the cases below rely on.
                    Set(Facade, "m_editableSpline", null);
                    Follower = host.AddComponent<SplineObjectFollower>();
                    Set(Follower, "m_attachedSpline", Facade);
                    Set(Follower, "m_surface", Surface);
                    Target = Objects.Create("target").transform;
                    Surface.Location = new SurfaceLocation { m_worldPosition = new Vector3(5f, 0f, 0f), m_worldRotation = Quaternion.identity };
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public bool Reversed => (bool)Get(Follower, "m_rotationIsReversed");

            public void Update() => Call(Follower, "InternalUpdate", 0f);

            public void Dispose()
            {
                try { Objects.Dispose(); }
                finally
                {
                    try { SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", m_priorConfig); }
                    finally { m_levelRow.Dispose(); }
                }
            }
        }

        private static void SameRotation(Quaternion expected, Quaternion actual, string label) =>
            Assert.That(Quaternion.Angle(expected, actual), Is.LessThan(0.01f), label + ": " + actual.eulerAngles.ToString("F3"));

        private static void SetPositionTarget(SurfaceObjectFollower follower, int value) =>
            Set(follower, "m_positionTarget", Enum.ToObject(typeof(SurfaceObjectFollower).GetNestedType("PositionTarget", BindingFlags.NonPublic), value));

        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(SurfaceObjectFollower).Attributes, Is.EqualTo(0x00100081));
            Assert.That(typeof(SurfaceObjectFollower).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Type positionTarget = typeof(SurfaceObjectFollower).GetNestedType("PositionTarget", BindingFlags.NonPublic);
            Assert.That((int)positionTarget.Attributes, Is.EqualTo(0x00000103));
            Assert.That(Enum.GetNames(positionTarget), Is.EqualTo(new[] { "TargetTransform", "SurfacePosition" }));
            Assert.That(Fields(typeof(SurfaceObjectFollower)), Is.EqualTo(new[]
            {
                "m_targetTransform:Transform:0001", "m_targetOffset:Vector3:0001", "m_positionTarget:PositionTarget:0001",
                "m_canInvertRotationAngle:Boolean:0001", "m_inverseRotationAngle:Single:0001", "m_currentTargetTransform:Transform:0001",
                "m_surface:ISurface:0001", "m_rotationIsReversed:Boolean:0001", "m_hasTarget:Boolean:0001",
                "m_inverseRotation:Quaternion:0021", "m_levelManagerRef:SystemRef`1:0021"
            }));
            Assert.That(Methods(typeof(SurfaceObjectFollower)), Is.EqualTo(new[]
            {
                "GetSurface:05c4", "Awake:00c4", "OnDestroy:00c6", "OnLevelManagerValid:0081", "ResetTarget:0081", "SetTarget:0086",
                "InternalUpdate:00c4", "UpdateTransform:0081", "GetSurfaceLocation:0081", "OnDrawGizmosSelected:0081", ".ctor:1884"
            }));
            Assert.That(AttributeRow(typeof(SurfaceObjectFollower).GetField("m_inverseRotationAngle", BindingFlags.Instance | BindingFlags.NonPublic)),
                Does.StartWith("RangeAttribute(0,180) TooltipAttribute(").And.EndWith(") ShowIfAttribute(m_canInvertRotationAngle,null) SerializeField()"));

            foreach (Type follower in new[] { typeof(SplineObjectFollower), typeof(RibbonObjectFollower) })
            {
                Assert.That((int)follower.Attributes, Is.EqualTo(0x00100001), follower.Name);
                Assert.That(Methods(follower), Is.EqualTo(new[] { "GetSurface:00c4", ".ctor:1886" }), follower.Name);
            }
            Assert.That(Fields(typeof(SplineObjectFollower)), Is.EqualTo(new[] { "m_attachedSpline:Spline:0001" }));
            Assert.That(Fields(typeof(RibbonObjectFollower)), Is.EqualTo(new[] { "m_attachedRibbon:Ribbon:0001" }));
        }

        // .ctor 0x6B39F0: m_canInvertRotationAngle = 1, m_inverseRotationAngle = 0x42be0000 (95),
        // m_inverseRotation = AngleAxis(180 at 0x29a6e48, up), then GetSystemRef<LevelManager>(null, true).
        [Test]
        public void ConstructorStoresTheNativeDefaults()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(Get(fixture.Follower, "m_canInvertRotationAngle"), Is.True);
                Assert.That(Get(fixture.Follower, "m_inverseRotationAngle"), Is.EqualTo(95f));
                SameRotation(Quaternion.AngleAxis(180f, Vector3.up), (Quaternion)Get(fixture.Follower, "m_inverseRotation"), "inverse rotation");
                Assert.That(((SystemRef<LevelManager>)Get(fixture.Follower, "m_levelManagerRef")).SystemName(),
                    Is.EqualTo(ProcessManager.GetDefaultName<LevelManager>()));
                Assert.That((int)Get(fixture.Follower, "m_positionTarget"), Is.Zero);
            }
        }

        // SetTarget 0x6B6320 / UpdateTransform 0x6B63A0: the surface is queried at target + offset;
        // PositionTarget 0 copies the target position, 1 the surface position, anything else throws
        // ArgumentOutOfRangeException (0x6B67CA) after the query. A null target clears m_hasTarget.
        [Test]
        public void SetTargetPlacesTheFollowerFromTheChosenSource()
        {
            using (var fixture = new Fixture())
            {
                Set(fixture.Follower, "m_targetOffset", new Vector3(0f, 1f, 0f));
                fixture.Target.position = new Vector3(1f, 2f, 3f);
                fixture.Follower.SetTarget(fixture.Target);
                Assert.That(Get(fixture.Follower, "m_hasTarget"), Is.True);
                Assert.That(fixture.Surface.Queries, Is.EqualTo(new[] { new Vector3(1f, 3f, 3f) }));
                AreClose(new Vector3(1f, 2f, 3f), fixture.Follower.transform.position, "target transform");

                SetPositionTarget(fixture.Follower, 1);
                fixture.Update();
                AreClose(new Vector3(5f, 0f, 0f), fixture.Follower.transform.position, "surface position");

                SetPositionTarget(fixture.Follower, 2);
                fixture.Follower.transform.position = Vector3.zero;
                Assert.Throws<ArgumentOutOfRangeException>(fixture.Update);
                Assert.That(fixture.Surface.Queries, Has.Count.EqualTo(3), "the query precedes the fault");
                Assert.That(fixture.Follower.transform.position, Is.EqualTo(Vector3.zero));

                fixture.Follower.SetTarget(null);
                Assert.That(Get(fixture.Follower, "m_hasTarget"), Is.False);
                fixture.Update();
                Assert.That(fixture.Surface.Queries, Has.Count.EqualTo(3), "no target, no query");
            }
        }

        // UpdateTransform 0x6B63A0: the rotation is the surface rotation, times m_inverseRotation
        // when reversed (0x6B652B); the flip test runs after that choice and only toggles for an
        // ordered angle > m_inverseRotationAngle (ucomiss/jbe at 0x6B6793), so the new state shows on
        // the next update. InternalUpdate 0x6B6810 passes m_canInvertRotationAngle; ResetTarget
        // 0x6B62A0 restores m_targetTransform and always passes true.
        [Test]
        public void RotationFlipsOnTheFollowingUpdateOnceTheAngleExceedsTheLimit()
        {
            using (var fixture = new Fixture())
            {
                fixture.Target.rotation = Quaternion.Euler(0f, 180f, 0f);
                fixture.Follower.SetTarget(fixture.Target);
                Assert.That(fixture.Reversed, Is.True, "180 degrees exceeds 95");
                SameRotation(Quaternion.identity, fixture.Follower.transform.rotation, "this update keeps the surface rotation");

                fixture.Update();
                Assert.That(fixture.Reversed, Is.True, "the reversed forward now matches the target");
                SameRotation(Quaternion.Euler(0f, 180f, 0f), fixture.Follower.transform.rotation, "reversed rotation");

                Set(fixture.Follower, "m_canInvertRotationAngle", false);
                fixture.Target.rotation = Quaternion.identity;
                fixture.Update();
                Assert.That(fixture.Reversed, Is.True, "the update passes m_canInvertRotationAngle");

                Transform other = fixture.Objects.Create("other").transform;
                Set(fixture.Follower, "m_targetTransform", fixture.Target);
                Set(fixture.Follower, "m_currentTargetTransform", other);
                Call(fixture.Follower, "ResetTarget");
                Assert.That(Get(fixture.Follower, "m_currentTargetTransform"), Is.SameAs(fixture.Target));
                Assert.That(fixture.Reversed, Is.False, "ResetTarget always allows the flip");
                SameRotation(Quaternion.Euler(0f, 180f, 0f), fixture.Follower.transform.rotation, "flipped state shows next update");
            }
        }

        // The flip compare is ordered: a NaN limit never flips, where !(angle <= limit) would.
        [TestCase(float.NaN, false)]
        [TestCase(179f, true)]
        [TestCase(180.5f, false)]
        public void RotationFlipNeedsAnOrderedGreaterAngle(float limit, bool flips)
        {
            using (var fixture = new Fixture())
            {
                Set(fixture.Follower, "m_inverseRotationAngle", limit);
                fixture.Target.rotation = Quaternion.Euler(0f, 180f, 0f);
                fixture.Follower.SetTarget(fixture.Target);
                Assert.That(fixture.Reversed, Is.EqualTo(flips));
            }
        }

        // GetSurface: SplineObjectFollower 0x6B5FA0 returns m_attachedSpline.GetSpline() and
        // RibbonObjectFollower 0x6B39C0 m_attachedRibbon.GetRibbon(), with no checks of their own.
        // OnDrawGizmosSelected 0x6B6970 returns while the current target is null (0x6B69C7), else
        // refreshes m_surface and returns while it is null (0x6B69F7), before any gizmo call.
        [Test]
        public void SurfacesComeFromTheAttachedFacadesAndGizmosRefreshThem()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(Call(fixture.Follower, "GetSurface"), Is.Null, "the default editable variant is unassigned");
                InertSpline inert = fixture.Facade.gameObject.AddComponent<InertSpline>();
                Set(fixture.Facade, "m_inertSpline", inert);
                Set(fixture.Facade, "m_activeSplineType", SplineVariantType.Inert);
                Assert.That(Call(fixture.Follower, "GetSurface"), Is.SameAs(inert));
                Set(fixture.Facade, "m_activeSplineType", (SplineVariantType)7);
                Assert.Throws<ArgumentOutOfRangeException>(() => Call(fixture.Follower, "GetSurface"));

                Call(fixture.Follower, "OnDrawGizmosSelected");
                Assert.That(Get(fixture.Follower, "m_surface"), Is.SameAs(fixture.Surface), "no current target, no refresh");
                Set(fixture.Follower, "m_currentTargetTransform", fixture.Target);
                Set(fixture.Facade, "m_activeSplineType", SplineVariantType.Editable);
                Call(fixture.Follower, "OnDrawGizmosSelected");
                Assert.That(Get(fixture.Follower, "m_surface"), Is.Null, "refreshed from the facade, then returned");
                Assert.That(fixture.Surface.Queries, Is.Empty);

                GameObject ribbonHost = fixture.Objects.Create("ribbon");
                ribbonHost.SetActive(false);
                var ribbon = ribbonHost.AddComponent<Ribbon>();
                Set(ribbon, "m_editableRibbon", null);
                var ribbonFollower = ribbonHost.AddComponent<RibbonObjectFollower>();
                Set(ribbonFollower, "m_attachedRibbon", ribbon);
                Assert.That(Call(ribbonFollower, "GetSurface"), Is.Null);
                InertRibbon inertRibbon = ribbonHost.AddComponent<InertRibbon>();
                Set(ribbon, "m_inertRibbon", inertRibbon);
                Set(ribbon, "m_activeRibbonType", RibbonVariantType.Inert);
                Assert.That(Call(ribbonFollower, "GetSurface"), Is.SameAs(inertRibbon));
            }
        }
    }
}
