using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerWaypointMarkersCompass (Game.Runtime 0x0200094d, x86-64 0x682BD0..0x683110) as a real component over the scene of
    // WaypointMarkerContainerScene, with real markers set up for real targets. The strip is the container's own RectTransform: Awake
    // takes it, and the cases give it a size. DoUpdate and the private geometry methods are called directly; the serialized clamp switch
    // and padding are authored data. The native DoUpdate inlines the direction test and the clamp, so their own cases and the DoUpdate
    // cases cover the same predicates from both sides. Direction values are the private ScreenDirection enum (None 0, Right 1, Left 2).
    public sealed class UIContainerWaypointMarkersCompassTests
    {
        private const float Tolerance = 1e-3f;
        private static readonly Type Direction = typeof(UIContainerWaypointMarkersCompass).GetNestedType("ScreenDirection", BindingFlags.NonPublic);

        private static void Run(Action<WaypointMarkerContainerScene, UIContainerWaypointMarkersCompass> body, float stripWidth = 200f,
            bool withCamera = false) => WaypointMarkerContainerScene.Run(WaypointContainerType.Compass, scene =>
        {
            var container = scene.NewContainer<UIContainerWaypointMarkersCompass>();
            container.GetComponent<RectTransform>().sizeDelta = new Vector2(stripWidth, 40f);
            Call(container, "Awake");
            body(scene, container);
        }, withCamera);

        private static UIWidgetSDTWaypointMarker MarkerAt(WaypointMarkerContainerScene scene, UIContainerWaypointMarkersCompass container, float anchoredX,
            bool behindCamera = false)
        {
            UIWidgetSDTWaypointMarker marker = scene.NewMarker(Vector3.zero);
            marker.RectTransform.anchoredPosition = new Vector2(anchoredX, 0f);
            marker.IsBehindCamera.Value = behindCamera;
            return marker;
        }

        private static int DirectionOf(UIContainerWaypointMarkersCompass container, UIWidgetSDTWaypointMarker marker) =>
            Convert.ToInt32(Call(container, "DetermineMarkerOutOfBoundsDirection", marker));

        private static void DoUpdate(WaypointMarkerContainerScene scene, UIContainerWaypointMarkersCompass container) =>
            Call(container, "DoUpdate", scene.Character, scene.Camera);

        // dump.cs TypeDefIndex 2381/2380 and the DummyDll rows (fields 0x0400265d..0x04002664, methods 0x06003580..0x06003583).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerWaypointMarkersCompass), 0x00100001,
                new (string, int)[]
                {
                    ("m_clampMarkersToSides", 0x0001), ("m_clampToSidesPadding", 0x0001), ("ShowLeftMarkerArrow", 0x0026), ("ShowRightMarkerArrow", 0x0026),
                },
                new (string, int)[] { ("DoUpdate", 0x00c4), ("DetermineMarkerOutOfBoundsDirection", 0x0081), ("ClampMarkerToSide", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerWaypointMarkersCompass), 1, 2);
            Assert.That(typeof(UIContainerWaypointMarkersCompass).BaseType, Is.EqualTo(typeof(UIContainerWaypointMarkersBase)));
            Assert.That(AttributeTypes(typeof(UIContainerWaypointMarkersCompass).GetField("m_clampMarkersToSides", Declared)), Is.EqualTo(new[] { typeof(SerializeField) }));
            FieldInfo padding = typeof(UIContainerWaypointMarkersCompass).GetField("m_clampToSidesPadding", Declared);
            Assert.That(AttributeTypes(padding), Is.EqualTo(new[] { typeof(ShowIfAttribute), typeof(SerializeField) }));
            Assert.That(padding.GetCustomAttributesData().First().ConstructorArguments[0].Value, Is.EqualTo("m_clampMarkersToSides"));

            Assert.That(Direction, Is.Not.Null);
            Assert.That((int)Direction.Attributes, Is.EqualTo(0x00000103));
            Assert.That(Enum.GetUnderlyingType(Direction), Is.EqualTo(typeof(int)));
            Assert.That(Enum.GetNames(Direction), Is.EqualTo(new[] { "None", "Right", "Left" }));
            Assert.That(Enum.GetValues(Direction).Cast<object>().Select(Convert.ToInt32).ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        // The constructor 0x683060 builds the two arrow bindables, left then right.
        [Test]
        public void ConstructionBuildsTwoIndependentArrowBindables()
        {
            Run((scene, container) =>
            {
                Assert.That(container.ShowLeftMarkerArrow, Is.Not.Null);
                Assert.That(container.ShowRightMarkerArrow, Is.Not.Null);
                Assert.That(container.ShowLeftMarkerArrow, Is.Not.SameAs(container.ShowRightMarkerArrow));
                Assert.That(container.ShowLeftMarkerArrow.Value, Is.False);
                Assert.That(container.ShowRightMarkerArrow.Value, Is.False);
            });
        }

        // DetermineMarkerOutOfBoundsDirection 0x682F20 on a strip 200 wide (rect x from -100 to 100, centre 0). In front of the camera:
        // left of the strip (ucomiss rect.x, position.x; ja, 0x682FA7) is Left, right of it (position.x > rect.x + width, 0x682FAF) is
        // Right, the edges themselves are inside. Behind the camera the side is mirrored about the centre: right of the centre
        // (0 > centre - position.x, 0x682F8C) is Left, the centre and left of it are Right.
        [TestCase(false, -100.5f, 2, TestName = "InFrontJustLeftOfTheStripIsLeft")]
        [TestCase(false, -100f, 0, TestName = "InFrontOnTheLeftEdgeIsInside")]
        [TestCase(false, 0f, 0, TestName = "InFrontAtTheCentreIsInside")]
        [TestCase(false, 100f, 0, TestName = "InFrontOnTheRightEdgeIsInside")]
        [TestCase(false, 100.5f, 1, TestName = "InFrontJustRightOfTheStripIsRight")]
        [TestCase(true, 0.5f, 2, TestName = "BehindJustRightOfTheCentreIsLeft")]
        [TestCase(true, 0f, 1, TestName = "BehindAtTheCentreIsRight")]
        [TestCase(true, -0.5f, 1, TestName = "BehindJustLeftOfTheCentreIsRight")]
        [TestCase(true, 5000f, 2, TestName = "BehindFarRightIsLeft")]
        [TestCase(true, -5000f, 1, TestName = "BehindFarLeftIsRight")]
        public void TheDirectionOfAMarkerOutsideTheStrip(bool behindCamera, float anchoredX, int expected)
        {
            Run((scene, container) =>
            {
                UIWidgetSDTWaypointMarker marker = MarkerAt(scene, container, anchoredX, behindCamera);

                Assert.That(DirectionOf(container, marker), Is.EqualTo(expected));
            });
        }

        // ClampMarkerToSide 0x682FC0: a marker on the left goes to the strip's left edge plus the padding, one on the right to the right
        // edge minus the padding, and any other direction leaves it untouched. A clamped marker's z is dropped to 0 (xorps xmm1 at 0x683034, and 0x682E3C in DoUpdate); the None case keeps z.
        [TestCase(2, -90f, 0f, TestName = "ALeftMarkerGoesToTheLeftEdgeInsetByThePadding")]
        [TestCase(1, 90f, 0f, TestName = "ARightMarkerGoesToTheRightEdgeInsetByThePadding")]
        [TestCase(0, 500f, 3f, TestName = "AMarkerWithNoDirectionIsLeftWhereItIs")]
        public void AMarkerIsClampedToTheSideItIsOn(int direction, float expectedX, float expectedZ)
        {
            Run((scene, container) =>
            {
                UIWidgetSDTWaypointMarker marker = MarkerAt(scene, container, 500f);
                marker.RectTransform.anchoredPosition3D = new Vector3(500f, 7f, 3f);

                Call(container, "ClampMarkerToSide", marker, Enum.ToObject(Direction, direction), 10f);

                Assert.That(marker.RectTransform.anchoredPosition3D.x, Is.EqualTo(expectedX).Within(Tolerance));
                Assert.That(marker.RectTransform.anchoredPosition3D.y, Is.EqualTo(7f).Within(Tolerance));
                Assert.That(marker.RectTransform.anchoredPosition3D.z, Is.EqualTo(expectedZ).Within(Tolerance));
            });
        }

        // DoUpdate 0x682BD0 decides each active marker's side from the visibility and position it had BEFORE its own update (the
        // IsVisible read at 0x682CD0 precedes OnUpdate 0x682D95), so the first pass only publishes the visibility and the arrows
        // follow on the second. Strip 400 wide (x from -200 to 200); targets at world x -20, -10, 0 and 2 stand at strip x -630, -150,
        // 330 and 426.
        [Test]
        public void TheArrowsFollowTheMarkersOutsideTheStripOnTheSecondPass()
        {
            Run((scene, container) =>
            {
                foreach (float x in new[] { -20f, -10f, 0f, 2f })
                    scene.Markers(container).Add(scene.NewMarker(new Vector3(x, 0f, 0f)));

                DoUpdate(scene, container);
                Assert.That(container.ShowLeftMarkerArrow.Value, Is.False, "no marker was visible when the first pass looked");
                Assert.That(container.ShowRightMarkerArrow.Value, Is.False);

                DoUpdate(scene, container);
                Assert.That(container.ShowLeftMarkerArrow.Value, Is.True, "the marker at -630");
                Assert.That(container.ShowRightMarkerArrow.Value, Is.True, "the markers at 330 and 426");
                Assert.That(scene.Markers(container).Select(marker => marker.RectTransform.position.x).ToArray(),
                    Is.EqualTo(new[] { -630f, -150f, 330f, 426f }).Within(Tolerance), "no clamping unless switched on");
            }, stripWidth: 400f, withCamera: true);
        }

        [Test]
        public void TheArrowsGoOutAgainWhenNoMarkerIsOutsideTheStrip()
        {
            Run((scene, container) =>
            {
                UIWidgetSDTWaypointMarker outside = scene.NewMarker(new Vector3(2f, 0f, 0f));
                scene.Markers(container).Add(outside);
                scene.Markers(container).Add(scene.NewMarker(new Vector3(-10f, 0f, 0f)));
                DoUpdate(scene, container);
                DoUpdate(scene, container);
                Assert.That(container.ShowRightMarkerArrow.Value, Is.True, "set-up: the marker at 426 lights the right arrow");

                scene.Markers(container).Remove(outside);
                DoUpdate(scene, container);

                Assert.That(container.ShowRightMarkerArrow.Value, Is.False, "both arrows are rewritten every pass");
                Assert.That(container.ShowLeftMarkerArrow.Value, Is.False);
            }, stripWidth: 400f, withCamera: true);
        }

        // A marker behind the camera takes the mirrored side: one that stands at strip x 330 (right of the centre) lights the LEFT arrow.
        [Test]
        public void AMarkerBehindTheCameraLightsTheMirroredArrow()
        {
            Run((scene, container) =>
            {
                scene.Markers(container).Add(scene.NewMarker(new Vector3(0f, 0f, -5f)));

                DoUpdate(scene, container);
                DoUpdate(scene, container);

                Assert.That(scene.Markers(container)[0].IsBehindCamera.Value, Is.True, "set-up");
                Assert.That(container.ShowLeftMarkerArrow.Value, Is.True);
                Assert.That(container.ShowRightMarkerArrow.Value, Is.False);
            }, stripWidth: 400f, withCamera: true);
        }

        // With the clamp switched on, a marker outside the strip is moved onto its edge inset by the padding (0x682DCD..0x682E44);
        // one inside is left where its update put it.
        [Test]
        public void WithClampingOnMarkersOutsideTheStripAreMovedOntoItsEdges()
        {
            Run((scene, container) =>
            {
                Set(container, "m_clampMarkersToSides", true); // Reflection: serialized switch
                Set(container, "m_clampToSidesPadding", 10f); // Reflection: serialized padding
                foreach (float x in new[] { -20f, -10f, 0f, 2f })
                    scene.Markers(container).Add(scene.NewMarker(new Vector3(x, 0f, 0f)));

                DoUpdate(scene, container);
                DoUpdate(scene, container);

                Assert.That(scene.Markers(container).Select(marker => marker.RectTransform.anchoredPosition3D.x).ToArray(),
                    Is.EqualTo(new[] { -190f, -150f, 190f, 190f }).Within(Tolerance));
            }, stripWidth: 400f, withCamera: true);
        }

        // The smooth damp time is only handed to a marker outside the strip (0x682D72); one inside snaps. Both start away from their
        // targets on the second pass; the strip marker reaches its target (-150) exactly, the outside one (target 330) is still short.
        [Test]
        public void OnlyAMarkerOutsideTheStripIsSmoothed()
        {
            Run((scene, container) =>
            {
                Set(container, "m_smoothTimeSeconds", 0.5f); // Reflection: serialized smooth time
                UIWidgetSDTWaypointMarker inside = scene.NewMarker(new Vector3(-10f, 0f, 0f));
                UIWidgetSDTWaypointMarker outside = scene.NewMarker(new Vector3(0f, 0f, 0f));
                scene.Markers(container).Add(inside);
                scene.Markers(container).Add(outside);
                DoUpdate(scene, container);
                inside.RectTransform.position = new Vector3(0f, 0f, 0f);
                outside.RectTransform.position = new Vector3(250f, 0f, 0f);

                DoUpdate(scene, container);

                Assert.That(inside.RectTransform.position.x, Is.EqualTo(-150f).Within(Tolerance), "inside the strip the move snaps");
                Assert.That(outside.RectTransform.position.x, Is.LessThan(329f), "outside the strip the move is smoothed");
            }, stripWidth: 400f, withCamera: true);
        }

        // A marker whose object is not active in the hierarchy is skipped altogether (get_activeInHierarchy, 0x682CB9).
        [Test]
        public void AnInactiveMarkerIsNotUpdated()
        {
            Run((scene, container) =>
            {
                UIWidgetSDTWaypointMarker inactive = scene.NewMarker(new Vector3(2f, 0f, 0f), active: false);
                inactive.RectTransform.position = new Vector3(11f, 22f, 0f);
                scene.Markers(container).Add(inactive);

                DoUpdate(scene, container);
                DoUpdate(scene, container);

                Assert.That(inactive.RectTransform.position, Is.EqualTo(new Vector3(11f, 22f, 0f)));
                Assert.That(container.ShowRightMarkerArrow.Value, Is.False);
            }, stripWidth: 400f, withCamera: true);
        }
    }
}
