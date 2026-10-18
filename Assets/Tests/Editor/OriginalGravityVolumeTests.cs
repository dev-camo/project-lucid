using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // Game.Runtime gravity volumes 02000a6b..02000a7c (x86-64 0x6C5A80..0x6C9750, shared generic
    // bodies 0x1391250..0x1391450). Each volume is a real component on an owned GameObject whose
    // description is a real GravityDescription set to gravity -2 between 0 and an outer falloff of
    // 15. Its reversed outer-factor comparison makes that a constant -2 for every distance in
    // [-0.01, 15.01] and 0 outside, so the expected vectors below are the native directions times -2.
    public sealed class OriginalGravityVolumeTests
    {
        private const float Gravity = -2f;
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static GravityDescription Description()
        {
            var description = new GravityDescription();
            description.Set(Gravity, 0f, 0f, 10f, 15f);
            return description;
        }

        // The concrete collider goes first, so the abstract RequireComponent(Collider) is already met.
        private static T Volume<T>(OwnedWorldObjects objects, Type colliderType, Vector3 position, Quaternion rotation)
            where T : GravitySource
        {
            GameObject owner = objects.Create(typeof(T).Name, position, rotation);
            if (colliderType != null) owner.AddComponent(colliderType);
            return owner.AddComponent<T>();
        }

        // Publishes the authored description through the original CalculateCachedValues chain.
        private static T Calculated<T>(OwnedWorldObjects objects, Type colliderType, Vector3 position, Quaternion rotation)
            where T : GravitySource
        {
            T volume = Volume<T>(objects, colliderType, position, rotation);
            Set(volume, "m_description", Description());
            Call(volume, "CalculateCachedValues");
            return volume;
        }

        private static float RecordedDistance(GravitySource volume) => ((GravityDescription)Get(volume, "Description")).Distance;

        // DummyDll TypeDef rows 0x02000a6b..0x02000a7c: flags, fields with their flags and
        // attributes, and every method in metadata order.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsMembersAndAttributes()
        {
            Assert.That((int)typeof(GravityTrigger<>).Attributes, Is.EqualTo(0x00100081));
            Assert.That(typeof(GravityTrigger<>).BaseType, Is.EqualTo(typeof(GravityTrigger)));
            Assert.That(typeof(GravityTrigger<>).GetGenericArguments()[0].GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(Collider) }));
            Assert.That(Fields(typeof(GravityTrigger<>)), Is.EqualTo(new[] { "m_description:GravityDescription:0001", "<m_collider>k__BackingField:T:0001" }));
            Assert.That(Methods(typeof(GravityTrigger<>)), Is.EqualTo(new[] { "get_m_collider:0884", "set_m_collider:0881", "CalculateCachedValues:00c4", ".ctor:1884" }));
            Assert.That(AttributeRow(typeof(GravityTrigger<>)), Is.EqualTo(
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) RequireComponent(Collider)"));

            Assert.That((int)typeof(GravityAxis<>).Attributes, Is.EqualTo(0x00100081));
            Assert.That(typeof(GravityAxis<>).GetGenericArguments()[0].GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(Collider) }));
            Assert.That(Fields(typeof(GravityAxis<>)), Is.EqualTo(new[] { "m_height:Single:0004" }));
            Assert.That(AttributeRow(typeof(GravityAxis<>).GetField("m_height", All)),
                Is.EqualTo("TooltipAttribute(Sets the gravity collider height away from the central axis.) SerializeField()"));
            Assert.That(Methods(typeof(GravityAxis<>)), Is.EqualTo(new[] { "GetGravity:00c6", ".ctor:1884" }));

            Assert.That(typeof(GravityBox).BaseType, Is.EqualTo(typeof(GravityTrigger<BoxCollider>)));
            Assert.That(Fields(typeof(GravityBox)), Is.EqualTo(new[] { "m_length:Single:0001", "m_applyAtEnds:Boolean:0001" }));
            Assert.That(Methods(typeof(GravityBox)), Is.EqualTo(new[] { "GetGravity:00c6", "SetPlaneIfCloser:0081", "CalculateDistance:0081", "CalculateCachedValues:00c4", "DrawGizmo:00c4", ".ctor:1886" }));
            Assert.That(AttributeRow(typeof(GravityBox)), Is.EqualTo(
                "Il2CppSetOptionAttribute(2,False) RequireComponent(BoxCollider) Il2CppSetOptionAttribute(1,False)"));

            Assert.That(typeof(GravityCapsule).BaseType, Is.EqualTo(typeof(GravityAxis<CapsuleCollider>)));
            Assert.That(Fields(typeof(GravityCapsule)), Is.Empty);
            Assert.That(Methods(typeof(GravityCapsule)), Is.EqualTo(new[] { "CalculateCachedValues:00c4", "DrawGizmo:00c4", ".ctor:1886" }));
            Type axisDirection = typeof(GravityCapsule).GetNestedType("AxisDirection", BindingFlags.NonPublic);
            Assert.That((int)axisDirection.Attributes, Is.EqualTo(0x00000103));
            Assert.That(Enum.GetNames(axisDirection), Is.EqualTo(new[] { "AxisX", "AxisY", "AxisZ" }));
            Assert.That(Enum.GetValues(axisDirection).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));

            Assert.That(typeof(GravityCapsuleArc).BaseType, Is.EqualTo(typeof(GravityCapsule)));
            Assert.That(Fields(typeof(GravityCapsuleArc)), Is.EqualTo(new[] { "GizmoDivisions:Int32:8051", "m_arcAngleStart:Single:0001", "m_arcAngleEnd:Single:0001" }));
            Assert.That(typeof(GravityCapsuleArc).GetField("GizmoDivisions", All).GetRawConstantValue(), Is.EqualTo(10));
            Assert.That(AttributeRow(typeof(GravityCapsuleArc).GetField("m_arcAngleEnd", All)),
                Is.EqualTo("SerializeField() RangeAttribute(0,360) TooltipAttribute(Gravity cylinder arc's end angle.)"));
            Assert.That(Methods(typeof(GravityCapsuleArc)), Is.EqualTo(new[] { "GetGravity:00c6", "DrawGizmo:00c4", "GetCapPosition:0081", ".ctor:1886" }));

            Assert.That(typeof(GravityCylinder).BaseType, Is.EqualTo(typeof(GravityAxis<BoxCollider>)));
            Assert.That(Methods(typeof(GravityCylinder)), Is.EqualTo(new[] { "CalculateCachedValues:00c4", "DrawGizmo:00c4", ".ctor:1886" }));
            Assert.That(typeof(GravitySphere).BaseType, Is.EqualTo(typeof(GravityTrigger<SphereCollider>)));
            Assert.That(Methods(typeof(GravitySphere)), Is.EqualTo(new[] { "GetGravity:00c6", "CalculateCachedValues:00c4", "DrawGizmo:00c4", ".ctor:1886" }));
            Assert.That(typeof(GravityHemisphere).BaseType, Is.EqualTo(typeof(GravitySphere)));
            Assert.That(Methods(typeof(GravityHemisphere)), Is.EqualTo(new[] { "GetGravity:00c6", "DrawGizmo:00c4", ".ctor:1886" }));

            Assert.That(Fields(typeof(GravityPlane)), Is.EqualTo(new[] { "m_planeSize:Vector2:0001", "m_decoupleColliderSize:Boolean:0001" }));
            Assert.That(Methods(typeof(GravityPlane)), Is.EqualTo(new[] { "GetGravity:00c6", "CalculateCachedValues:00c4", "DrawGizmo:00c4", ".ctor:1886" }));
            Assert.That(AttributeRow(typeof(GravityPlane)), Is.EqualTo(
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) RequireComponent(BoxCollider)"));

            Assert.That(typeof(GravityRender).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(Fields(typeof(GravityRender)), Is.EqualTo(new[] { "m_gravitySource:GravitySource:0001" }));
            Assert.That(Methods(typeof(GravityRender)), Is.EqualTo(new[] { "OnDrawGizmos:0081", ".ctor:1886" }));

            Assert.That(typeof(GravitySurfacePlane).BaseType, Is.EqualTo(typeof(GravitySurface)));
            Assert.That(Fields(typeof(GravitySurfacePlane)), Is.EqualTo(new[] { "m_distanceAlongNormal:Single:0001" }));
            Assert.That(Methods(typeof(GravitySurfacePlane)), Is.EqualTo(new[] { "GetGravity:00c6", "ValidateTrackerLocation:00c4", "CalculateCachedValues:00c4", "DrawGizmo:00c4", ".ctor:1886" }));

            Assert.That(Fields(typeof(GravityTorus)), Is.EqualTo(new[] { "m_size:Vector3:0001", "m_tubeRadius:Single:0001" }));
            Assert.That(Methods(typeof(GravityTorus)), Is.EqualTo(new[] { "GetGravity:00c6", "CalculateCachedValues:00c4", "ClosestPointOnTorus:0091", "DrawGizmo:00c4", ".ctor:1886" }));
            Assert.That(AttributeRow(typeof(GravityTorus)), Is.EqualTo(
                "Il2CppSetOptionAttribute(1,False) RequireComponent(BoxCollider) Il2CppSetOptionAttribute(2,False)"));
        }

        // Constructors 0x13913A0 (m_height 1), 0x6C66F0 (m_length 1), 0x6C6BD0 (arc end 180),
        // 0x6C7680 (plane 10 x 10) and 0x6C96D0 (size one, tube 1). The shared trigger
        // constructor 0x1391450 allocates no description.
        [Test]
        public void ConstructorsStoreTheNativeDefaults()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-defaults"))
            {
                Assert.That(Get(Volume<GravityCylinder>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity), "m_height"), Is.EqualTo(1f));
                GravityBox box = Volume<GravityBox>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                Assert.That(Get(box, "m_length"), Is.EqualTo(1f));
                Assert.That(Get(box, "m_applyAtEnds"), Is.False);
                GravityCapsuleArc arc = Volume<GravityCapsuleArc>(objects, typeof(CapsuleCollider), Vector3.zero, Quaternion.identity);
                Assert.That(Get(arc, "m_arcAngleStart"), Is.EqualTo(0f));
                Assert.That(Get(arc, "m_arcAngleEnd"), Is.EqualTo(180f));
                Assert.That(Get(arc, "m_height"), Is.EqualTo(1f));
                Assert.That(Get(Volume<GravityPlane>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity), "m_planeSize"), Is.EqualTo(new Vector2(10f, 10f)));
                GravityTorus torus = Volume<GravityTorus>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                Assert.That(Get(torus, "m_size"), Is.EqualTo(Vector3.one));
                Assert.That(Get(torus, "m_tubeRadius"), Is.EqualTo(1f));
            }
        }

        // 0x13913F0 publishes m_description through SetDescription, then stores GetComponent<T>
        // and forces isTrigger. GravitySphere 0x6C8260 then centres the collider at zero with the
        // outer falloff (MaxDistance, +0x20) as its radius.
        [Test]
        public void TriggerCachedValuesPublishTheDescriptionAndATriggerCollider()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-trigger"))
            {
                GravitySphere sphere = Volume<GravitySphere>(objects, typeof(SphereCollider), Vector3.zero, Quaternion.identity);
                var collider = sphere.GetComponent<SphereCollider>();
                collider.isTrigger = false;
                collider.center = new Vector3(1f, 2f, 3f);
                GravityDescription description = Description();
                Set(sphere, "m_description", description);
                Call(sphere, "CalculateCachedValues");
                Assert.That(Get(sphere, "Description"), Is.SameAs(description));
                Assert.That(Get(sphere, "<m_collider>k__BackingField"), Is.SameAs(collider));
                Assert.That(collider.isTrigger, Is.True);
                Assert.That(collider.center, Is.EqualTo(Vector3.zero));
                Assert.That(collider.radius, Is.EqualTo(15f));
                Assert.That(sphere.MaxDistance, Is.EqualTo(15f));
            }
        }

        // GravitySphere.GetGravity 0x6C7140: offset / |offset| * CalculateGravity(|offset|), with
        // no zero guard, so the centre gives 0/0.
        [Test]
        public void SphereGravityFollowsTheOffsetFromTheCentre()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-sphere"))
            {
                GravitySphere sphere = Calculated<GravitySphere>(objects, typeof(SphereCollider), new Vector3(1f, 1f, 1f), Quaternion.identity);
                AreClose(new Vector3(-1.2f, -1.6f, 0f), sphere.GetGravity(new Vector3(4f, 5f, 1f)), "offset (3, 4, 0)");
                Assert.That(RecordedDistance(sphere), Is.EqualTo(5f).Within(1e-5f));
                AreClose(Vector3.zero, sphere.GetGravity(new Vector3(1f, 17f, 1f)), "beyond the outer falloff");
                IsNaN(sphere.GetGravity(new Vector3(1f, 1f, 1f)), "the centre");
            }
        }

        // GravityHemisphere.GetGravity 0x6C7070: zero only when 0 > Dot(offset, up) (ordered
        // ucomiss/jbe), so the plane itself keeps the sphere gravity. The transform's up is used.
        [Test]
        public void HemisphereRejectsOnlyPointsStrictlyBelowItsPlane()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-hemisphere"))
            {
                GravityHemisphere upright = Calculated<GravityHemisphere>(objects, typeof(SphereCollider), Vector3.zero, Quaternion.identity);
                AreClose(Vector3.zero, upright.GetGravity(new Vector3(3f, -4f, 0f)), "below, inside the radius");
                AreClose(new Vector3(-1.2f, 0f, -1.6f), upright.GetGravity(new Vector3(3f, 0f, 4f)), "on the plane");
                AreClose(new Vector3(0f, -2f, 0f), upright.GetGravity(new Vector3(0f, 5f, 0f)), "above");

                GravityHemisphere flipped = Calculated<GravityHemisphere>(objects, typeof(SphereCollider), Vector3.zero, Quaternion.Euler(0f, 0f, 180f));
                AreClose(Vector3.zero, flipped.GetGravity(new Vector3(0f, 5f, 0f)), "above the world, below the flipped plane");
                AreClose(new Vector3(0f, 2f, 0f), flipped.GetGravity(new Vector3(0f, -5f, 0f)), "below the world, above the flipped plane");
            }
        }

        // GravityAxis.GetGravity 0x1391250: the offset from GetClosestPointOnLine(position, forward)
        // divided by its own length, unguarded on the axis.
        [Test]
        public void AxisGravityPointsAwayFromTheForwardAxis()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-axis"))
            {
                GravityCylinder cylinder = Calculated<GravityCylinder>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                AreClose(new Vector3(-1.2f, -1.6f, 0f), cylinder.GetGravity(new Vector3(3f, 4f, 7f)), "forward along z");
                Assert.That(RecordedDistance(cylinder), Is.EqualTo(5f).Within(1e-5f));
                IsNaN(cylinder.GetGravity(new Vector3(0f, 0f, 3f)), "on the axis");

                GravityCylinder turned = Calculated<GravityCylinder>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
                AreClose(new Vector3(0f, -1.2f, -1.6f), turned.GetGravity(new Vector3(7f, 3f, 4f)), "forward along x");
            }
        }

        // GravityCylinder 0x6C6C20: centre zero, size (2 MaxDistance, 2 MaxDistance, m_height).
        // GravityCapsule 0x6C6740: centre zero, direction 2, height m_height, radius MaxDistance.
        [Test]
        public void AxisCachedValuesLayOutTheColliderAlongForward()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-axis-layout"))
            {
                GravityCylinder cylinder = Volume<GravityCylinder>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                Set(cylinder, "m_height", 4f);
                Set(cylinder, "m_description", Description());
                var box = cylinder.GetComponent<BoxCollider>();
                box.center = Vector3.one;
                Call(cylinder, "CalculateCachedValues");
                Assert.That(box.center, Is.EqualTo(Vector3.zero));
                Assert.That(box.size, Is.EqualTo(new Vector3(30f, 30f, 4f)));
                Assert.That(box.isTrigger, Is.True);

                GravityCapsule capsule = Volume<GravityCapsule>(objects, typeof(CapsuleCollider), Vector3.zero, Quaternion.identity);
                Set(capsule, "m_height", 3f);
                Set(capsule, "m_description", Description());
                var capsuleCollider = capsule.GetComponent<CapsuleCollider>();
                capsuleCollider.center = Vector3.one;
                capsuleCollider.direction = 0;
                Call(capsule, "CalculateCachedValues");
                Assert.That(capsuleCollider.center, Is.EqualTo(Vector3.zero));
                Assert.That(capsuleCollider.direction, Is.EqualTo(2));
                Assert.That(capsuleCollider.height, Is.EqualTo(3f));
                Assert.That(capsuleCollider.radius, Is.EqualTo(15f));
                Assert.That(capsuleCollider.isTrigger, Is.True);
            }
        }

        // GravityCapsuleArc.GetGravity 0x6C6860: SignedAngle(up, direction, forward), +360 when
        // negative, zero when start > angle or angle > end (both ordered), else the axis gravity.
        // A point on the right is -90 degrees, so it only enters an arc that covers 270.
        [Test]
        public void CapsuleArcAppliesOnlyInsideItsInclusiveArc()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-arc"))
            {
                GravityCapsuleArc arc = Calculated<GravityCapsuleArc>(objects, typeof(CapsuleCollider), Vector3.zero, Quaternion.identity);
                AreClose(new Vector3(2f, 0f, 0f), arc.GetGravity(new Vector3(-3f, 0f, 0f)), "left is +90, inside [0, 180]");
                AreClose(Vector3.zero, arc.GetGravity(new Vector3(3f, 0f, 0f)), "right is 270, outside [0, 180]");

                Set(arc, "m_arcAngleStart", 200f);
                Set(arc, "m_arcAngleEnd", 300f);
                AreClose(new Vector3(-2f, 0f, 0f), arc.GetGravity(new Vector3(3f, 0f, 0f)), "right is 270, inside [200, 300]");
                AreClose(Vector3.zero, arc.GetGravity(new Vector3(-3f, 0f, 0f)), "left is 90, outside [200, 300]");

                Set(arc, "m_arcAngleStart", 0f);
                Set(arc, "m_arcAngleEnd", 0f);
                AreClose(new Vector3(0f, -2f, 0f), arc.GetGravity(new Vector3(0f, 3f, 0f)), "up is exactly 0, inside [0, 0]");
                Set(arc, "m_arcAngleStart", 0.5f);
                Set(arc, "m_arcAngleEnd", 1f);
                AreClose(Vector3.zero, arc.GetGravity(new Vector3(0f, 3f, 0f)), "up is below a start of 0.5");
            }
        }

        // GetCapPosition 0x6C6A70: AngleAxis(90, up) * rotation * (0, cos r, sin r) * distance for
        // r = -angle in radians; no position is added.
        [Test]
        public void CapPositionNegatesTheAngleAndAddsNoPosition()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-cap"))
            {
                GravityCapsuleArc arc = Volume<GravityCapsuleArc>(objects, typeof(CapsuleCollider), new Vector3(5f, 5f, 5f), Quaternion.identity);
                AreClose(new Vector3(0f, 2f, 0f), (Vector3)Call(arc, "GetCapPosition", 2f, 0f), "angle 0");
                AreClose(new Vector3(-2f, 0f, 0f), (Vector3)Call(arc, "GetCapPosition", 2f, 90f), "angle 90");
            }
        }

        // GravityBox 0x6C6490: centre up * MaxDistance / 2, size (MaxDistance, MaxDistance, m_length).
        [Test]
        public void BoxCachedValuesRaiseTheColliderAboveTheTransform()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-box-layout"))
            {
                GravityBox box = Volume<GravityBox>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                Set(box, "m_length", 4f);
                Set(box, "m_description", Description());
                Call(box, "CalculateCachedValues");
                var collider = box.GetComponent<BoxCollider>();
                Assert.That(collider.center, Is.EqualTo(new Vector3(0f, 7.5f, 0f)));
                Assert.That(collider.size, Is.EqualTo(new Vector3(15f, 15f, 4f)));
                Assert.That(collider.isTrigger, Is.True);
            }
        }

        // GravityBox.GetGravity 0x6C5A80 with SetPlaneIfCloser 0x6C6220: faces up, -up, right,
        // -right and then, when m_applyAtEnds, forward and -forward (at m_length). Each distance is
        // Dot(position - (centre - normal * extent / 2), normal) and replaces the best unless it is
        // strictly greater (ucomiss/ja), so the last of equal faces wins.
        [Test]
        public void BoxGravityTakesTheNearestFaceAndTheLastOfEqualFaces()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-box"))
            {
                GravityBox box = Calculated<GravityBox>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                AreClose(new Vector3(0f, -2f, 0f), box.GetGravity(new Vector3(1f, 2f, 0f)), "floor at 2");
                Assert.That(RecordedDistance(box), Is.EqualTo(2f).Within(1e-5f));
                AreClose(new Vector3(2f, 0f, 0f), box.GetGravity(new Vector3(6f, 10f, 0f)), "-right face at 1.5");
                Assert.That(RecordedDistance(box), Is.EqualTo(1.5f).Within(1e-5f));
                AreClose(new Vector3(2f, 0f, 0f), box.GetGravity(new Vector3(0f, 7.5f, 0f)), "four faces at 7.5: the last, -right, wins");
                AreClose(Vector3.zero, box.GetGravity(new Vector3(0f, -1f, 0f)), "below the floor");
                Assert.That(RecordedDistance(box), Is.EqualTo(-1f).Within(1e-5f));

                Set(box, "m_length", 4f);
                AreClose(new Vector3(2f, 0f, 0f), box.GetGravity(new Vector3(0f, 7.5f, 1.5f)), "ends ignored");
                Set(box, "m_applyAtEnds", true);
                AreClose(new Vector3(0f, 0f, 2f), box.GetGravity(new Vector3(0f, 7.5f, 1.5f)), "-forward end at 0.5");
                Assert.That(RecordedDistance(box), Is.EqualTo(0.5f).Within(1e-5f));
            }
        }

        // GravityPlane.GetGravity 0x6C7320: up * CalculateGravity(Dot(position - transform, up)),
        // unbounded across the plane.
        [Test]
        public void PlaneGravityUsesOnlyTheHeightAlongUp()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-plane"))
            {
                GravityPlane plane = Calculated<GravityPlane>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
                AreClose(new Vector3(0f, 0f, -2f), plane.GetGravity(new Vector3(500f, -300f, 3f)), "up is world forward");
                Assert.That(RecordedDistance(plane), Is.EqualTo(3f).Within(1e-4f));
                AreClose(Vector3.zero, plane.GetGravity(new Vector3(0f, 0f, -1f)), "below the plane");
            }
        }

        // GravityPlane 0x6C7420: the base runs first; m_decoupleColliderSize then returns before
        // the layout of centre up * MaxDistance / 2 and size (planeSize.x, MaxDistance, planeSize.y).
        [Test]
        public void PlaneCachedValuesLeaveADecoupledColliderLayout()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-plane-layout"))
            {
                GravityPlane plane = Volume<GravityPlane>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                Set(plane, "m_description", Description());
                Set(plane, "m_planeSize", new Vector2(6f, 8f));
                var collider = plane.GetComponent<BoxCollider>();
                Call(plane, "CalculateCachedValues");
                Assert.That(collider.center, Is.EqualTo(new Vector3(0f, 7.5f, 0f)));
                Assert.That(collider.size, Is.EqualTo(new Vector3(6f, 15f, 8f)));

                Set(plane, "m_decoupleColliderSize", true);
                collider.center = new Vector3(1f, 1f, 1f);
                collider.size = new Vector3(2f, 2f, 2f);
                collider.isTrigger = false;
                Call(plane, "CalculateCachedValues");
                Assert.That(collider.isTrigger, Is.True);
                Assert.That(collider.center, Is.EqualTo(new Vector3(1f, 1f, 1f)));
                Assert.That(collider.size, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            }
        }

        // ClosestPointOnTorus 0x6C9230 for segment radius (4, 1, 4) and tube 1. Outside the core
        // ellipse the tube angle is asin(y / 1); inside it is mirrored to 180 - angle. The ring is
        // ClosestPointOnEllipse with the X and Z radii at that angle, the height is clamped to the
        // top (0, 90) position's y, and the normal points away from the core ellipse point.
        [Test]
        public void ClosestPointOnTorusMirrorsInsideAndClampsTheHeight()
        {
            var segment = new Vector3(4f, 1f, 4f);
            object[] outside = { new Vector3(6f, 0f, 0f), 1f, segment, null };
            AreClose(new Vector3(5f, 0f, 0f), (Vector3)CallStatic(typeof(GravityTorus), "ClosestPointOnTorus", outside), "outside");
            AreClose(new Vector3(1f, 0f, 0f), (Vector3)outside[3], "outside normal");

            object[] inside = { new Vector3(2f, 0f, 0f), 1f, segment, null };
            AreClose(new Vector3(3f, 0f, 0f), (Vector3)CallStatic(typeof(GravityTorus), "ClosestPointOnTorus", inside), "inside");
            AreClose(new Vector3(-1f, 0f, 0f), (Vector3)inside[3], "inside normal");

            object[] above = { new Vector3(5f, 3f, 0f), 1f, segment, null };
            AreClose(new Vector3(4f, 1f, 0f), (Vector3)CallStatic(typeof(GravityTorus), "ClosestPointOnTorus", above), "above");
            AreClose(new Vector3(1f, 3f, 0f) / Mathf.Sqrt(10f), (Vector3)above[3], "above normal");
        }

        // GravityTorus.GetGravity 0x6C9000: segment radius (size / 2 - tube, size.y / 2, size.z / 2
        // - tube) in local space; the distance is to the closest point, the direction the normal.
        [Test]
        public void TorusGravityUsesTheNormalAndTheClosestPointDistance()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-torus"))
            {
                GravityTorus torus = Calculated<GravityTorus>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                Set(torus, "m_size", new Vector3(10f, 2f, 10f));
                AreClose(new Vector3(-2f, 0f, 0f), torus.GetGravity(new Vector3(6f, 0f, 0f)), "outside the ring");
                Assert.That(RecordedDistance(torus), Is.EqualTo(1f).Within(1e-4f));
                AreClose(new Vector3(2f, 0f, 0f), torus.GetGravity(new Vector3(2f, 0f, 0f)), "inside the ring");
                AreClose(new Vector3(-2f, -6f, 0f) / Mathf.Sqrt(10f), torus.GetGravity(new Vector3(5f, 3f, 0f)), "above the tube");
                Assert.That(RecordedDistance(torus), Is.EqualTo(Mathf.Sqrt(5f)).Within(1e-4f));
            }
        }

        // GravityTorus 0x6C94D0: size = m_size + one * MaxDistance, tube = m_tubeRadius +
        // MaxDistance / 2; the collider spans twice the (90, 0) X and (0, 0) Z ring radii.
        [Test]
        public void TorusCachedValuesSpanTheGrownRing()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-torus-layout"))
            {
                GravityTorus torus = Volume<GravityTorus>(objects, typeof(BoxCollider), Vector3.zero, Quaternion.identity);
                Set(torus, "m_size", new Vector3(10f, 2f, 6f));
                Set(torus, "m_description", Description());
                var collider = torus.GetComponent<BoxCollider>();
                collider.center = Vector3.one;
                Call(torus, "CalculateCachedValues");
                Assert.That(collider.center, Is.EqualTo(Vector3.zero));
                AreClose(new Vector3(25f, 17f, 21f), collider.size, "size");
            }
        }

        private static GravitySurfacePlane SurfacePlane(OwnedWorldObjects objects, Vector3 origin, Quaternion rotation,
            PositionBoundsInfo bounds)
        {
            GravitySurfacePlane plane = Volume<GravitySurfacePlane>(objects, null, Vector3.zero, Quaternion.identity);
            Set(plane, "Description", Description());
            var location = new SurfaceLocation { m_worldPosition = origin, m_worldRotation = rotation, m_positionBoundsInfo = bounds };
            Set(plane, "<Location>k__BackingField", location);
            return plane;
        }

        // GravitySurfacePlane.GetGravity 0x6C8BC0: the height along rotation * up from the
        // location's world position is stored at +0xF8 before the Active (+0x40) gate.
        [Test]
        public void SurfacePlaneRecordsTheHeightBeforeTheActiveGate()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-surface"))
            {
                GravitySurfacePlane plane = SurfacePlane(objects, new Vector3(0f, 1f, 0f), Quaternion.Euler(90f, 0f, 0f), default);
                AreClose(Vector3.zero, plane.GetGravity(new Vector3(7f, 1f, 4f)), "inactive");
                Assert.That(Get(plane, "m_distanceAlongNormal"), Is.EqualTo(4f).Within(1e-5f));
                typeof(GravitySource).GetProperty("Active").GetSetMethod(true).Invoke(plane, new object[] { true });
                AreClose(new Vector3(0f, 0f, -2f), plane.GetGravity(new Vector3(7f, 1f, 5f)), "active");
                Assert.That(Get(plane, "m_distanceAlongNormal"), Is.EqualTo(5f).Within(1e-5f));
            }
        }

        // ValidateTrackerLocation 0x6C8D50: false at an end edge, when -0.25 > height (ordered),
        // when height > MaxDistance (ordered), or when either lateral bound is strictly below
        // -0.25; a NaN height passes both limits.
        [Test]
        public void SurfacePlaneValidationTruthTable()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-surface-validate"))
            {
                bool Validate(float height, PositionBoundsInfo bounds)
                {
                    GravitySurfacePlane plane = SurfacePlane(objects, Vector3.zero, Quaternion.identity, bounds);
                    Set(plane, "m_distanceAlongNormal", height);
                    return (bool)Call(plane, "ValidateTrackerLocation");
                }
                var open = new PositionBoundsInfo(1f, 1f, false, Vector3.zero, Vector3.zero);
                Assert.That(Validate(4f, open), Is.True, "inside");
                Assert.That(Validate(4f, new PositionBoundsInfo(1f, 1f, true, Vector3.zero, Vector3.zero)), Is.False, "end edge");
                Assert.That(Validate(-0.3f, open), Is.False, "below the tolerance");
                Assert.That(Validate(-0.25f, open), Is.True, "at the tolerance");
                Assert.That(Validate(15.5f, open), Is.False, "above MaxDistance");
                Assert.That(Validate(15f, open), Is.True, "at MaxDistance");
                Assert.That(Validate(float.NaN, open), Is.True, "NaN height");
                Assert.That(Validate(4f, new PositionBoundsInfo(-1f, 1f, false, Vector3.zero, Vector3.zero)), Is.False, "left bound below -0.25");
                Assert.That(Validate(4f, new PositionBoundsInfo(1f, -1f, false, Vector3.zero, Vector3.zero)), Is.False, "right bound below -0.25");
                Assert.That(Validate(4f, new PositionBoundsInfo(-0.25f, -0.25f, false, Vector3.zero, Vector3.zero)), Is.True, "bounds at -0.25");
            }
        }

        // GravityRender.OnDrawGizmos 0x6C79B0 reads m_gravitySource and calls its DrawTargetGizmo
        // slot with no null check; GravitySource's own slot body is empty.
        [Test]
        public void RenderForwardsToTheSourceWithoutANullCheck()
        {
            using (var objects = new OwnedWorldObjects("lucid-gravity-render"))
            {
                GravityRender render = objects.Create("render").AddComponent<GravityRender>();
                Assert.Throws<NullReferenceException>(() => Call(render, "OnDrawGizmos"));
                Set(render, "m_gravitySource", Volume<GravitySphere>(objects, typeof(SphereCollider), Vector3.zero, Quaternion.identity));
                Assert.DoesNotThrow(() => Call(render, "OnDrawGizmos"));
            }
        }
    }
}
