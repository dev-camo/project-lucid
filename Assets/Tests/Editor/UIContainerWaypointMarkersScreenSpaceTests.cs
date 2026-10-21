using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerWaypointMarkersScreenSpace (Game.Runtime 0x0200094f, x86-64 0x683110..0x683AF0) as a real component over the scene of
    // WaypointMarkerContainerScene, an owned ellipse centre and real markers set up for real targets. The ellipse radius is a fraction of
    // the screen size (0x683994 multiplies it by Screen.width and Screen.height), so the cases give a radius of (100 / width, 50 / height)
    // and expect an ellipse of 100 by 50 pixels whatever the window size. The camera places the target of world (x, y, 0) at screen
    // (330 + 48x, 260 + 48y); the centre stands at (330, 260, 5). OnEnable and OnDisable run against a real UIRuntimeConfiguration
    // whose bridge answers the case's last input type, with the static input type action taken over and put back. The serialized
    // ellipse settings are authored data. Not covered: the marker update that precedes each clamp (UIWidgetSDTWaypointMarker.OnUpdate has
    // its own fixture).
    public sealed class UIContainerWaypointMarkersScreenSpaceTests
    {
        private const float Tolerance = 1e-2f;

        private sealed class Rig
        {
            public WaypointMarkerContainerScene Scene;
            public UIContainerWaypointMarkersScreenSpace Container;
            public RectTransform Centre;
        }

        // The ellipse is 100 by 50 pixels when the last input type is not listed (Mouse) and 300 by 150 when it is (Touch), with the
        // centre's bottom offset 40 and 120.
        private static void Run(Action<Rig> body, bool withCamera = false, Action<WaypointSettings> configure = null) =>
            WaypointMarkerContainerScene.Run(WaypointContainerType.ScreenSpace, scene =>
            {
                Assume.That(Screen.width > 0 && Screen.height > 0, "the ellipse radius is a fraction of the screen size");
                var rig = new Rig { Scene = scene, Container = scene.NewContainer<UIContainerWaypointMarkersScreenSpace>() };
                rig.Centre = scene.Owned.Track(new GameObject("Lucid ellipse centre", typeof(RectTransform))).GetComponent<RectTransform>();
                rig.Centre.position = new Vector3(330f, 260f, 5f);
                rig.Centre.offsetMin = new Vector2(5f, 7f);
                Set(rig.Container, "m_ellipseCentre", rig.Centre);
                Set(rig.Container, "m_ellipseHeightForUIOn", 120f);
                Set(rig.Container, "m_ellipseRadiusForUIOn", new Vector2(300f / Screen.width, 150f / Screen.height));
                Set(rig.Container, "m_ellipseHeightForUIOff", 40f);
                Set(rig.Container, "m_ellipseRadiusForUIOff", new Vector2(100f / Screen.width, 50f / Screen.height));
                Set(rig.Container, "m_uiInputTypesWhichToggleOn", new List<UIInputType> { UIInputType.Touch });
                body(rig);
            }, withCamera, configure);

        private static void Choose(Rig rig, UIInputType inputType) => Call(rig.Container, "OnInputTypeChanged", inputType);

        private static Vector3 ClosestPoint(Rig rig, Vector3 point) => (Vector3)Call(rig.Container, "GetClosestPointOnEllipseEdge", point);

        private static UIWidgetSDTWaypointMarker MarkerAtScreen(Rig rig, Vector3 position, bool behindCamera = false)
        {
            UIWidgetSDTWaypointMarker marker = rig.Scene.NewMarker(Vector3.zero);
            marker.RectTransform.position = position;
            marker.IsBehindCamera.Value = behindCamera;
            marker.ShowDistanceIndicator.Value = true;
            marker.ShowArrowIndicator.Value = false;
            return marker;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), label + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), label + " y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance), label + " z");
        }

        // dump.cs TypeDefIndex 2382 and the DummyDll rows (fields 0x04002665..0x0400266b, methods 0x06003584..0x0600358a).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerWaypointMarkersScreenSpace), 0x00100001,
                new (string, int)[]
                {
                    ("m_ellipseCentre", 0x0001), ("m_ellipseHeightForUIOn", 0x0001), ("m_ellipseRadiusForUIOn", 0x0001),
                    ("m_ellipseHeightForUIOff", 0x0001), ("m_ellipseRadiusForUIOff", 0x0001), ("m_uiInputTypesWhichToggleOn", 0x0001),
                    ("m_ellipseRadius", 0x0001),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("OnInputTypeChanged", 0x0081), ("DoUpdate", 0x00c4),
                    ("ClampMarkerToEllipse", 0x0081), ("GetClosestPointOnEllipseEdge", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerWaypointMarkersScreenSpace), 1, 2);
            Assert.That(typeof(UIContainerWaypointMarkersScreenSpace).BaseType, Is.EqualTo(typeof(UIContainerWaypointMarkersBase)));
            foreach (string field in new[] { "m_ellipseCentre", "m_ellipseHeightForUIOn", "m_ellipseRadiusForUIOn", "m_ellipseHeightForUIOff", "m_ellipseRadiusForUIOff", "m_uiInputTypesWhichToggleOn" })
                Assert.That(AttributeTypes(typeof(UIContainerWaypointMarkersScreenSpace).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
            Assert.That(AttributeTypes(typeof(UIContainerWaypointMarkersScreenSpace).GetField("m_ellipseRadius", Declared)), Is.Empty);
        }

        // The constructor 0x683A70 builds the list of input types before the base constructor.
        [Test]
        public void ConstructionStartsWithNoListedInputTypes()
        {
            WaypointMarkerContainerScene.Run(WaypointContainerType.ScreenSpace, scene =>
                Assert.That(Get<List<UIInputType>>(scene.NewContainer<UIContainerWaypointMarkersScreenSpace>(), "m_uiInputTypesWhichToggleOn"), Is.Empty));
        }

        // OnInputTypeChanged 0x6832C0: a listed input type (List<UIInputType>.Contains, 0x6832FD) selects the "UI on" radius and
        // bottom offset (cmovne at 0x683320, cmove at 0x683335), any other the "UI off" ones; only the offset's y changes.
        [Test]
        public void TheEllipseFollowsWhetherTheInputTypeIsListed()
        {
            Run(rig =>
            {
                Choose(rig, UIInputType.Touch);
                Assert.That(Get<Vector2>(rig.Container, "m_ellipseRadius"), Is.EqualTo(new Vector2(300f / Screen.width, 150f / Screen.height)));
                Assert.That(rig.Centre.offsetMin, Is.EqualTo(new Vector2(5f, 120f)));

                Choose(rig, UIInputType.Mouse);
                Assert.That(Get<Vector2>(rig.Container, "m_ellipseRadius"), Is.EqualTo(new Vector2(100f / Screen.width, 50f / Screen.height)));
                Assert.That(rig.Centre.offsetMin, Is.EqualTo(new Vector2(5f, 40f)));
            });
        }

        // OnEnable 0x683110 asks the configuration's bridge for the last input type, applies it and listens for changes; OnDisable
        // 0x683360 stops listening.
        [Test]
        public void EnablingAppliesTheLastInputTypeAndFollowsLaterChangesUntilDisabled()
        {
            FastAction<UIInputType> prior = InputBridge.OnUpdateLastInputType;
            InputBridge.OnUpdateLastInputType = null;
            try
            {
                Run(rig =>
                {
                    var bridge = rig.Scene.Owned.Track(ScriptableObject.CreateInstance<OnScreenControlInputBridgeProbe>());
                    var configuration = rig.Scene.Owned.Host("Lucid ui configuration", false).AddComponent<UIRuntimeConfiguration>();
                    Set(configuration, "m_inputBridge", bridge); // Reflection: serialized in the original
                    rig.Scene.Owned.Register(configuration);
                    bridge.LastInputType = UIInputType.Touch;

                    Call(rig.Container, "OnEnable");

                    Assert.That(rig.Centre.offsetMin, Is.EqualTo(new Vector2(5f, 120f)), "Touch is listed");
                    Assert.That(InputBridge.OnUpdateLastInputType.GetInvocationList().Select(action => action.Target).ToArray(), Is.EqualTo(new object[] { rig.Container }));

                    FastAction<UIInputType>.Invoke(InputBridge.OnUpdateLastInputType, UIInputType.Keyboard);
                    Assert.That(rig.Centre.offsetMin, Is.EqualTo(new Vector2(5f, 40f)), "Keyboard is not");

                    Call(rig.Container, "OnDisable");
                    Assert.That(InputBridge.OnUpdateLastInputType == null || InputBridge.OnUpdateLastInputType.GetInvocationListCount() == 0, Is.True);
                    FastAction<UIInputType>.Invoke(InputBridge.OnUpdateLastInputType, UIInputType.Touch);
                    Assert.That(rig.Centre.offsetMin, Is.EqualTo(new Vector2(5f, 40f)), "no longer listening");
                });
            }
            finally
            {
                InputBridge.OnUpdateLastInputType = prior;
            }
        }

        // GetClosestPointOnEllipseEdge 0x683940: the point's x and y are divided by the ellipse's pixel radii (divps at 0x6839C0), the
        // vector (z untouched) is normalised (length > 1e-5, else zero) and x and y are scaled back by the radii. The ellipse is 100
        // by 50 pixels. A point at zero has length 0 and maps to zero.
        [TestCase(200f, 0f, 0f, 100f, 0f, 0f, TestName = "APointOnTheXAxisMapsToTheEllipseEnd")]
        [TestCase(-300f, 0f, 0f, -100f, 0f, 0f, TestName = "APointOnTheNegativeXAxisMapsToTheOtherEnd")]
        [TestCase(0f, 100f, 0f, 0f, 50f, 0f, TestName = "APointOnTheYAxisMapsToTheEllipseTop")]
        [TestCase(100f, 50f, 0f, 70.7107f, 35.3553f, 0f, TestName = "ADiagonalPointMapsToTheUnitCircleDiagonalScaledBack")]
        [TestCase(0f, 0f, 0f, 0f, 0f, 0f, TestName = "ThePointAtZeroMapsToZero")]
        [TestCase(200f, 0f, 40f, 4.9938f, 0f, 0.99875f, TestName = "TheDepthJoinsTheLengthAndIsDividedByIt")]
        public void TheClosestPointOnTheEllipseEdge(float x, float y, float z, float expectedX, float expectedY, float expectedZ)
        {
            Run(rig =>
            {
                Choose(rig, UIInputType.Mouse);

                AssertNear(ClosestPoint(rig, new Vector3(x, y, z)), new Vector3(expectedX, expectedY, expectedZ), "edge point");
            });
        }

        // ClampMarkerToEllipse 0x6835D0 outside the ellipse: the marker's offset from the centre is cut to the edge point in its
        // direction (TryClamp of x and y to the edge point's magnitudes, 0x68388D and 0x6838AA), the marker is placed at centre plus
        // offset with depth 0, the arrow shows and the distance indicator hides.
        [TestCase(570f, 260f, 430f, 260f, TestName = "AMarkerFarToTheRightIsCutToTheRightEnd")]
        [TestCase(330f, 600f, 330f, 310f, TestName = "AMarkerFarAboveIsCutToTheTop")]
        [TestCase(410f, 300f, 400.7107f, 295.3553f, TestName = "AMarkerOutsideTheEllipseButInsideItsBoxIsCutToTheEdgeInItsDirection")]
        public void AMarkerOutsideTheEllipseIsPutOnItsEdge(float x, float y, float expectedX, float expectedY)
        {
            Run(rig =>
            {
                Choose(rig, UIInputType.Mouse);
                UIWidgetSDTWaypointMarker marker = MarkerAtScreen(rig, new Vector3(x, y, 9f));

                Call(rig.Container, "ClampMarkerToEllipse", marker);

                AssertNear(marker.RectTransform.position, new Vector3(expectedX, expectedY, 0f), "marker position");
                Assert.That(marker.ShowArrowIndicator.Value, Is.True);
                Assert.That(marker.ShowDistanceIndicator.Value, Is.False);
            });
        }

        // Inside the ellipse nothing is cut (TryClamp answers false for both axes): the marker keeps its x and y but loses its depth,
        // the arrow hides and the distance indicator shows unless the marker's settings disable it (DistanceIndicatorDisabled 0x6838EF).
        [TestCase(378f, 260f, TestName = "AMarkerNearTheCentreOnTheXAxisStaysPut")]
        [TestCase(370f, 270f, TestName = "AMarkerInsideTheEllipseOffTheAxesStaysPut")]
        [TestCase(429f, 260f, TestName = "AMarkerJustInsideTheEdgeIsNotCut")]
        public void AMarkerInsideTheEllipseKeepsItsPositionAndShowsTheDistance(float x, float y)
        {
            Run(rig =>
            {
                Choose(rig, UIInputType.Mouse);
                UIWidgetSDTWaypointMarker marker = MarkerAtScreen(rig, new Vector3(x, y, 9f));
                marker.ShowDistanceIndicator.Value = false;
                marker.ShowArrowIndicator.Value = true;

                Call(rig.Container, "ClampMarkerToEllipse", marker);

                AssertNear(marker.RectTransform.position, new Vector3(x, y, 0f), "marker position");
                Assert.That(marker.ShowArrowIndicator.Value, Is.False);
                Assert.That(marker.ShowDistanceIndicator.Value, Is.True);
            });
        }

        [Test]
        public void AMarkerWhoseSettingsHideTheDistanceKeepsItHiddenInsideTheEllipse()
        {
            Run(rig =>
            {
                Choose(rig, UIInputType.Mouse);
                UIWidgetSDTWaypointMarker marker = MarkerAtScreen(rig, new Vector3(378f, 260f, 9f));

                Call(rig.Container, "ClampMarkerToEllipse", marker);

                Assert.That(marker.ShowDistanceIndicator.Value, Is.False);
                Assert.That(marker.ShowArrowIndicator.Value, Is.False);
            }, configure: settings => Set(settings, "m_showDistanceIndicator", false));
        }

        // A marker behind the camera is mirrored through the centre (the offset's sign is flipped, 0x68363A) and put on the edge in that
        // direction, whatever its distance from the centre; only the arrow shows.
        [TestCase(570f, 260f, 230f, 260f, TestName = "ABehindMarkerToTheRightGoesToTheLeftEnd")]
        [TestCase(330f, 300f, 330f, 210f, TestName = "ABehindMarkerAboveGoesToTheBottom")]
        [TestCase(340f, 262f, 237.1523f, 241.4305f, TestName = "ABehindMarkerNearTheCentreIsStillPutOnTheEdge")]
        [TestCase(330f, 260f, 330f, 260f, TestName = "ABehindMarkerExactlyAtTheCentreStaysAtTheCentre")]
        public void AMarkerBehindTheCameraIsMirroredOntoTheEdge(float x, float y, float expectedX, float expectedY)
        {
            Run(rig =>
            {
                Choose(rig, UIInputType.Mouse);
                UIWidgetSDTWaypointMarker marker = MarkerAtScreen(rig, new Vector3(x, y, 9f), behindCamera: true);

                Call(rig.Container, "ClampMarkerToEllipse", marker);

                AssertNear(marker.RectTransform.position, new Vector3(expectedX, expectedY, 0f), "marker position");
                Assert.That(marker.ShowArrowIndicator.Value, Is.True);
                Assert.That(marker.ShowDistanceIndicator.Value, Is.False);
            });
        }

        // DoUpdate 0x683430 updates every active marker (OnUpdate with the container's canvas and smooth time, without locking the
        // y axis), and clamps those that are visible AFTER their update (IsVisible read at 0x68351A). A marker beyond the maximum draw
        // distance (100) is not visible and keeps the position of its update; an inactive marker is not touched at all.
        [Test]
        public void EveryActiveVisibleMarkerIsClampedInTheSamePass()
        {
            Run(rig =>
            {
                Choose(rig, UIInputType.Mouse);
                UIWidgetSDTWaypointMarker outside = rig.Scene.NewMarker(new Vector3(5f, 0f, 0f));
                UIWidgetSDTWaypointMarker inside = rig.Scene.NewMarker(new Vector3(1f, 0f, 0f));
                UIWidgetSDTWaypointMarker distant = rig.Scene.NewMarker(new Vector3(500f, 0f, 0f));
                UIWidgetSDTWaypointMarker inactive = rig.Scene.NewMarker(new Vector3(5f, 0f, 0f), active: false);
                inactive.RectTransform.position = new Vector3(11f, 22f, 0f);
                rig.Scene.Markers(rig.Container).AddRange(new[] { outside, inside, distant, inactive });

                Call(rig.Container, "DoUpdate", rig.Scene.Character, rig.Scene.Camera);

                AssertNear(outside.RectTransform.position, new Vector3(430f, 260f, 0f), "outside");
                Assert.That(outside.ShowArrowIndicator.Value, Is.True);
                Assert.That(outside.ShowDistanceIndicator.Value, Is.False);
                AssertNear(inside.RectTransform.position, new Vector3(378f, 260f, 0f), "inside");
                Assert.That(inside.ShowArrowIndicator.Value, Is.False);
                Assert.That(inside.ShowDistanceIndicator.Value, Is.True);
                Assert.That(distant.IsVisible.Value, Is.False, "set-up: beyond the maximum draw distance");
                AssertNear(distant.RectTransform.position, new Vector3(24330f, 260f, 0f), "distant");
                Assert.That(distant.ShowArrowIndicator.Value, Is.False, "not clamped");
                Assert.That(inactive.RectTransform.position, Is.EqualTo(new Vector3(11f, 22f, 0f)));
            }, withCamera: true, configure: settings => Set(settings, "m_maxDrawDistance", 100f));
        }

        // The smooth time is handed to every marker (0x6834FF), inside the ellipse too: starting away from its target a smoothed marker
        // has not arrived, where an unsmoothed one snaps to the target.
        [TestCase(0f, TestName = "WithoutASmoothTimeAMarkerSnapsToItsTarget")]
        [TestCase(0.5f, TestName = "WithASmoothTimeAMarkerHasNotArrived")]
        public void TheSmoothTimeIsHandedToTheMarkers(float smoothTime)
        {
            Run(rig =>
            {
                Choose(rig, UIInputType.Mouse);
                Set(rig.Container, "m_smoothTimeSeconds", smoothTime); // Reflection: serialized smooth time
                UIWidgetSDTWaypointMarker marker = rig.Scene.NewMarker(new Vector3(1f, 0f, 0f));
                marker.RectTransform.position = new Vector3(330f, 260f, 0f);
                rig.Scene.Markers(rig.Container).Add(marker);

                Call(rig.Container, "DoUpdate", rig.Scene.Character, rig.Scene.Camera);

                if (smoothTime == 0f)
                    Assert.That(marker.RectTransform.position.x, Is.EqualTo(378f).Within(Tolerance));
                else
                    Assert.That(marker.RectTransform.position.x, Is.LessThan(377f));
            }, withCamera: true);
        }
    }
}
