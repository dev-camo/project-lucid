using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.LookAtTarget (TypeDef 0x02000ac9, x86-64 0x6E7CD0..0x6E8670) on owned
    // GameObjects. Awake and Update are invoked directly because EditMode sends neither.
    public sealed class OriginalLookAtTargetTests
    {
        private const float Cos30 = 0.8660254f;

        private static LookAtTarget Looker(OwnedWorldObjects objects, Transform parent = null)
        {
            LookAtTarget looker = objects.Create("looker", Vector3.zero, Quaternion.identity, parent).AddComponent<LookAtTarget>();
            Call(looker, "Awake");
            return looker;
        }

        [Test]
        public void DeclarationsKeepTheOriginalFieldsAndMethods()
        {
            Assert.That((int)typeof(LookAtTarget).Attributes, Is.EqualTo(0x00100001));
            Assert.That(Fields(typeof(LookAtTarget)), Is.EqualTo(new[]
            {
                "m_retainVerticalAxis:Boolean:0001", "m_smoothTime:Single:0001", "m_maxSpeed:Single:0001",
                "m_defaultTarget:Transform:0001", "m_clearTargetOnDisable:Boolean:0001", "m_useConstraintAngles:Boolean:0001",
                "m_horizontalConstraintAngles:Vector2:0001", "m_verticalConstraintAngles:Vector2:0001",
                "m_gizmoConstraintDistance:Single:0001", "m_worldUp:Vector3:0001", "m_target:Transform:0001",
                "m_lookAtDefault:Boolean:0001", "m_defaultTargetPosition:Vector3:0001", "m_velocity:Vector3:0001"
            }));
            Assert.That(AttributeRow(typeof(LookAtTarget).GetField("m_gizmoConstraintDistance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)),
                Is.EqualTo("MinAttribute(0) SerializeField() ShowIfAttribute(m_useConstraintAngles,null)"));
            Assert.That(Methods(typeof(LookAtTarget)), Is.EqualTo(new[]
            {
                "Awake:0081", "SetLookAt:0086", "SetLookAt:0086", "Update:0081", "RetainVerticalAxis:0081",
                "SmoothToTarget:0081", "UpdateConstraints:0081", "UpdateTargetConstraint:0091", "EndLookAt:0086",
                "OnDisable:0081", ".ctor:1886"
            }));
        }

        // Awake 0x6E7CD0 stores transform.up and, only when a default target exists, its
        // position; .ctor 0x6E8670 sets m_clearTargetOnDisable.
        [Test]
        public void AwakeFixesTheUpAxisAndTheDefaultTargetPosition()
        {
            using (var objects = new OwnedWorldObjects("lucid-look-awake"))
            {
                LookAtTarget looker = objects.Create("looker", Vector3.zero, Quaternion.Euler(0f, 0f, 90f)).AddComponent<LookAtTarget>();
                Assert.That(Get(looker, "m_clearTargetOnDisable"), Is.True);
                Call(looker, "Awake");
                AreClose(new Vector3(-1f, 0f, 0f), (Vector3)Get(looker, "m_worldUp"), "rotated up");
                Assert.That(Get(looker, "m_defaultTargetPosition"), Is.EqualTo(Vector3.zero));

                Transform fallback = objects.Create("default", new Vector3(1f, 2f, 3f), Quaternion.identity).transform;
                Set(looker, "m_defaultTarget", fallback);
                Call(looker, "Awake");
                fallback.position = new Vector3(9f, 9f, 9f);
                Assert.That(Get(looker, "m_defaultTargetPosition"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }
        }

        // SetLookAt 0x6E7D90: m_lookAtDefault = target == null && default != null. EndLookAt and
        // OnDisable (when m_clearTargetOnDisable) pass null; the Collider overload passes its transform.
        [Test]
        public void SetLookAtChoosesTheDefaultOnlyWithoutATarget()
        {
            using (var objects = new OwnedWorldObjects("lucid-look-set"))
            {
                LookAtTarget looker = Looker(objects);
                Transform target = objects.Create("target").transform;
                looker.SetLookAt((Transform)null);
                Assert.That(Get(looker, "m_lookAtDefault"), Is.False, "no target, no default");

                Set(looker, "m_defaultTarget", objects.Create("default").transform);
                looker.SetLookAt((Transform)null);
                Assert.That(Get(looker, "m_lookAtDefault"), Is.True, "no target, a default");
                looker.SetLookAt(target);
                Assert.That(Get(looker, "m_target"), Is.SameAs(target));
                Assert.That(Get(looker, "m_lookAtDefault"), Is.False, "a target and a default");

                BoxCollider collider = objects.Create("collider").AddComponent<BoxCollider>();
                looker.SetLookAt(collider);
                Assert.That(Get(looker, "m_target"), Is.SameAs(collider.transform));

                looker.EndLookAt();
                Assert.That(Get(looker, "m_target"), Is.Null);
                Assert.That(Get(looker, "m_lookAtDefault"), Is.True);

                looker.SetLookAt(target);
                Set(looker, "m_clearTargetOnDisable", false);
                Call(looker, "OnDisable");
                Assert.That(Get(looker, "m_target"), Is.SameAs(target));
                Set(looker, "m_clearTargetOnDisable", true);
                Call(looker, "OnDisable");
                Assert.That(Get(looker, "m_target"), Is.Null);
            }
        }

        // Update 0x6E7E30 with no smoothing or constraints: LookAt(target, m_worldUp). With
        // m_retainVerticalAxis, RetainVerticalAxis 0x6E7FC0 removes the target's height along
        // m_worldUp first. Without a target or default nothing turns.
        [Test]
        public void UpdateTurnsTowardsTheTargetAndCanRetainTheVerticalAxis()
        {
            using (var objects = new OwnedWorldObjects("lucid-look-update"))
            {
                LookAtTarget looker = Looker(objects);
                Call(looker, "Update");
                Assert.That(looker.transform.rotation, Is.EqualTo(Quaternion.identity), "nothing to look at");

                looker.SetLookAt(objects.Create("target", new Vector3(10f, 5f, 0f), Quaternion.identity).transform);
                Call(looker, "Update");
                AreClose(new Vector3(10f, 5f, 0f).normalized, looker.transform.forward, "free");

                Set(looker, "m_retainVerticalAxis", true);
                Call(looker, "Update");
                AreClose(Vector3.right, looker.transform.forward, "vertical axis retained");
            }
        }

        // Update 0x6E7E30: after turning towards the default position, m_lookAtDefault becomes
        // m_velocity.sqrMagnitude > 1e-4 (seta). Without smoothing the velocity stays zero, so the
        // default is looked at once and later updates leave the transform alone.
        [Test]
        public void TheDefaultIsLookedAtOnceWithoutSmoothing()
        {
            using (var objects = new OwnedWorldObjects("lucid-look-default"))
            {
                LookAtTarget looker = objects.Create("looker").AddComponent<LookAtTarget>();
                Set(looker, "m_defaultTarget", objects.Create("default", new Vector3(10f, 0f, 0f), Quaternion.identity).transform);
                Call(looker, "Awake");
                looker.EndLookAt();
                Call(looker, "Update");
                AreClose(Vector3.right, looker.transform.forward, "turned to the default");
                Assert.That(Get(looker, "m_lookAtDefault"), Is.False);

                looker.transform.rotation = Quaternion.identity;
                Call(looker, "Update");
                Assert.That(looker.transform.rotation, Is.EqualTo(Quaternion.identity));
            }
        }

        // SmoothToTarget 0x6E8060: an exact zero smooth time returns. Otherwise the new position
        // is transform + SmoothDamp(forward, direction, ..., maxSpeed > 0 ? maxSpeed : inf) *
        // distance, so a tiny positive max speed holds the direction at the current forward.
        [Test]
        public void SmoothingKeepsTheDistanceAndHonoursAPositiveMaxSpeed()
        {
            using (var objects = new OwnedWorldObjects("lucid-look-smooth"))
            {
                LookAtTarget looker = Looker(objects);
                object[] args = { new Vector3(10f, 0f, 0f) };
                Call(looker, "SmoothToTarget", args);
                Assert.That(args[0], Is.EqualTo(new Vector3(10f, 0f, 0f)), "zero smooth time");

                Set(looker, "m_smoothTime", 1f);
                Set(looker, "m_maxSpeed", 1e-6f);
                Call(looker, "SmoothToTarget", args);
                AreClose(new Vector3(0f, 0f, 10f), (Vector3)args[0], "held at forward, at the target's distance", 1e-3f);
            }
        }

        // UpdateTargetConstraint 0x6E8590: a zero angle returns; otherwise the plane normal is
        // AngleAxis(angle, axis) * normal and the target moves back by the distance unless
        // 0 >= distance (ordered ucomiss/jae), so an unordered distance still moves it.
        [Test]
        public void TargetConstraintMovesOnlyPositiveOrUnorderedDistances()
        {
            Vector3 Constrain(Vector3 target, Vector3 origin, float angle)
            {
                object[] args = { target, origin, Vector3.up, Vector3.right, angle };
                CallStatic(typeof(LookAtTarget), "UpdateTargetConstraint", args);
                return (Vector3)args[0];
            }
            AreClose(new Vector3(2.5f, 0f, 10f * 0.5f * Cos30), Constrain(new Vector3(10f, 0f, 0f), Vector3.zero, 30f), "beyond the plane");
            Assert.That(Constrain(new Vector3(10f, 0f, 0f), Vector3.zero, 0f), Is.EqualTo(new Vector3(10f, 0f, 0f)), "zero angle");
            Assert.That(Constrain(new Vector3(0f, 0f, 10f), Vector3.zero, 30f), Is.EqualTo(new Vector3(0f, 0f, 10f)), "behind the plane");
            Vector3 unordered = Constrain(new Vector3(0f, 0f, 10f), new Vector3(float.NaN, 0f, 0f), 30f);
            Assert.That(float.IsNaN(unordered.x), Is.True, "an unordered distance is applied");
        }

        // UpdateConstraints 0x6E8220 uses the parent's position, up and right: horizontal x is
        // (up, right), horizontal y (-up, -right), vertical x (right, -up), vertical y (-right, up).
        // Each case enables one 30 degree limit and starts 10 units beyond it.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ConstraintsPairTheParentAxesInTheNativeOrder(int limit)
        {
            Vector3[] starts = { new Vector3(10f, 0f, 0f), new Vector3(-10f, 0f, 0f), new Vector3(0f, -10f, 0f), new Vector3(0f, 10f, 0f) };
            Vector3[] expected = { new Vector3(2.5f, 0f, 0f), new Vector3(-2.5f, 0f, 0f), new Vector3(0f, -2.5f, 0f), new Vector3(0f, 2.5f, 0f) };
            using (var objects = new OwnedWorldObjects("lucid-look-constraints"))
            {
                Transform parent = objects.Create("parent").transform;
                LookAtTarget looker = Looker(objects, parent);
                Set(looker, "m_horizontalConstraintAngles", new Vector2(limit == 0 ? 30f : 0f, limit == 1 ? 30f : 0f));
                Set(looker, "m_verticalConstraintAngles", new Vector2(limit == 2 ? 30f : 0f, limit == 3 ? 30f : 0f));
                object[] args = { starts[limit] };
                Call(looker, "UpdateConstraints", args);
                Assert.That(args[0], Is.EqualTo(starts[limit]), "constraints disabled");

                Set(looker, "m_useConstraintAngles", true);
                Call(looker, "UpdateConstraints", args);
                AreClose(expected[limit] + new Vector3(0f, 0f, 10f * 0.5f * Cos30), (Vector3)args[0], "limit " + limit);
            }
        }
    }
}
