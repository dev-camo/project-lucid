using System;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTWaypointMarker (Game.Runtime 0x020009cc, x86-64 0x6A2820..0x6A30A0) as a real component on an inactive owned RectTransform
    // host, over an owned orthographic camera (identity pose at the origin, a 1024 x 1024 render texture and pixel rect (10, 20, 640, 480):
    // half extents 20/3 by 5, so the screen point of a world point is (330 + 48x, 260 + 48y) and its screen z is the world z), an owned
    // canvas, a real WaypointDefinition holding one WaypointSettings, a real WaypointTarget and a real Character with a supplied gravity
    // direction. Awake and OnUpdate are called directly. The settings and the screen space event are serialized authored data.
    public sealed class UIWidgetSdtWaypointMarkerTests
    {
        private const float Tolerance = 1e-3f;

        private sealed class Scene
        {
            public UIWidgetSDTWaypointMarker Marker;
            public WaypointSettings Settings;
            public WaypointDefinition Definition;
            public WaypointTarget Target;
            public Camera Camera;
            public Canvas Canvas;
            public CharacterGravityProbe Character;
            public Sprite Icon;
            public readonly System.Collections.Generic.List<Vector3> Published = new System.Collections.Generic.List<Vector3>();

            public Vector3 Position => Marker.RectTransform.position;

            public void Update(float smoothTimeSeconds = 0f, bool lockYAxis = false) =>
                Marker.OnUpdate(Character, Camera, Canvas, smoothTimeSeconds, lockYAxis);
        }

        // The target stands at the given world position. Rows the components' field initialisers cache references from are leased.
        private static void Run(Vector3 targetPosition, Action<Owned, Scene> body, Action<Scene> configure = null) => Owned.Run(owned =>
        {
            owned.LeaseRow<WaypointManager>();
            owned.LeaseRow<GameplayIslandManager>();
            var scene = new Scene();
            scene.Icon = NewSprite(owned);
            scene.Settings = new WaypointSettings();
            Set(scene.Settings, "m_icon", scene.Icon);
            configure?.Invoke(scene);
            scene.Definition = Create<WaypointDefinition>(owned);
            Get<SerializableDictionary<WaypointContainerType, WaypointSettings>>(scene.Definition, "m_settingsByContainerType")
                [WaypointContainerType.ScreenSpace] = scene.Settings;
            scene.Target = owned.Host("Lucid waypoint target", false).AddComponent<WaypointTarget>();
            scene.Target.transform.position = targetPosition;
            RenderTexture texture = owned.Track(new RenderTexture(1024, 1024, 0));
            scene.Camera = owned.Host("Lucid waypoint camera", false).AddComponent<Camera>();
            scene.Camera.enabled = false;
            scene.Camera.targetTexture = texture;
            scene.Camera.orthographic = true;
            scene.Camera.orthographicSize = 5f;
            scene.Camera.aspect = 4f / 3f;
            scene.Camera.pixelRect = new Rect(10f, 20f, 640f, 480f);
            scene.Canvas = owned.Host("Lucid waypoint canvas", false).AddComponent<Canvas>();
            scene.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scene.Character = NewGravityCharacter(owned, Vector3.down);
            GameObject markerHost = owned.Track(new GameObject("Lucid waypoint marker", typeof(RectTransform)));
            markerHost.SetActive(false);
            scene.Marker = markerHost.AddComponent<UIWidgetSDTWaypointMarker>();
            var screenSpaceEvent = new UnityEvent<Vector3>();
            screenSpaceEvent.AddListener(scene.Published.Add);
            Set(scene.Marker, "m_screenSpacePositionUpdated", screenSpaceEvent);
            Call(scene.Marker, "Awake");
            scene.Marker.Setup(new UIWidgetSDTWaypointMarkerParameters(scene.Target, scene.Definition, WaypointContainerType.ScreenSpace));
            body(owned, scene);
        });

        private static void AssertNear(Vector3 actual, Vector3 expected, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), label + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), label + " y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance), label + " z");
        }

        // dump.cs TypeDefIndex 2507 and the DummyDll rows (fields 0x0400291c..0x0400292a).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetSDTWaypointMarker), 0x00100101,
                new (string, int)[]
                {
                    ("Icon", 0x0026), ("DistanceToTarget", 0x0026), ("ShowHeightIndicator", 0x0026), ("HeightIndicatorIsAbove", 0x0026),
                    ("IsVisible", 0x0026), ("ShowDistanceIndicator", 0x0026), ("ShowArrowIndicator", 0x0026), ("IsBehindCamera", 0x0026),
                    ("m_screenSpacePositionUpdated", 0x0001), ("<RectTransform>k__BackingField", 0x0001), ("m_target", 0x0001),
                    ("m_targetAssigned", 0x0001), ("m_waypointDefinition", 0x0001), ("m_positionDampVelocity", 0x0001), ("m_settings", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_RectTransform", 0x0886), ("set_RectTransform", 0x0881), ("Awake", 0x0081), ("Setup", 0x00c6), ("Close", 0x0086),
                    ("GetTargetPosition", 0x0081), ("OnUpdate", 0x0086), ("DistanceIndicatorDisabled", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetSDTWaypointMarker), 1, 2);
            Assert.That(typeof(UIWidgetSDTWaypointMarker).BaseType, Is.EqualTo(typeof(UIWidget)));
            Assert.That(AttributeTypes(typeof(UIWidgetSDTWaypointMarker)).Select(type => type.Name).ToArray(),
                Is.EqualTo(new[] { "Il2CppSetOptionAttribute", "RequireComponent", "Il2CppSetOptionAttribute" }));
            Assert.That(AttributeTypes(typeof(UIWidgetSDTWaypointMarker).GetField("m_screenSpacePositionUpdated", Declared)),
                Is.EqualTo(new[] { typeof(SerializeField) }));
        }

        // The constructor 0x6A2EC0 builds the eight bindables in field order before the UIWidget constructor.
        [Test]
        public void ConstructorBuildsEightIndependentBindables()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Track(new GameObject("Lucid waypoint marker", typeof(RectTransform)));
                host.SetActive(false);
                var marker = host.AddComponent<UIWidgetSDTWaypointMarker>();
                var bindables = new object[]
                {
                    marker.Icon, marker.DistanceToTarget, marker.ShowHeightIndicator, marker.HeightIndicatorIsAbove,
                    marker.IsVisible, marker.ShowDistanceIndicator, marker.ShowArrowIndicator, marker.IsBehindCamera,
                };

                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(bindables.Distinct().Count(), Is.EqualTo(8));
                Assert.That(marker.DistanceToTarget.Value, Is.EqualTo(0f));
                Assert.That(marker.IsVisible.Value, Is.False);
            });
        }

        // Awake 0x6A2850 takes the transform cast to a RectTransform (the property's private setter).
        [Test]
        public void AwakeTakesTheRectTransform()
        {
            Run(Vector3.zero, (owned, scene) =>
                Assert.That(scene.Marker.RectTransform, Is.SameAs((RectTransform)scene.Marker.transform)));
        }

        // Setup 0x6A28C0 after the UIWidget setup: the definition and the target are kept, the settings for the container type are
        // fetched (TryGetSettingsByContainerType, 0x6A2949) and the icon and distance flag are published from them.
        [TestCase(true, TestName = "SetupPublishesTheIconAndAVisibleDistanceIndicator")]
        [TestCase(false, TestName = "SetupPublishesTheIconAndAHiddenDistanceIndicator")]
        public void SetupTakesTheTargetAndItsSettings(bool showDistance)
        {
            Run(new Vector3(1f, 2f, 3f), (owned, scene) =>
            {
                Assert.That(Get<WaypointDefinition>(scene.Marker, "m_waypointDefinition"), Is.SameAs(scene.Definition));
                Assert.That(Get<WaypointSettings>(scene.Marker, "m_settings"), Is.SameAs(scene.Settings));
                Assert.That(Get<Transform>(scene.Marker, "m_target"), Is.SameAs(scene.Target.transform));
                Assert.That(Get<bool>(scene.Marker, "m_targetAssigned"), Is.True);
                Assert.That(scene.Marker.Icon.Value, Is.SameAs(scene.Icon));
                Assert.That(scene.Marker.ShowDistanceIndicator.Value, Is.EqualTo(showDistance));
                Assert.That(scene.Marker.DistanceIndicatorDisabled(), Is.EqualTo(!showDistance));
            }, scene => Set(scene.Settings, "m_showDistanceIndicator", showDistance));
        }

        // GetTargetPosition 0x6A2AB0 projects the target (WorldToScreenPoint) and, on an overlay canvas, uses the screen point as it is:
        // (1, 2, 2) is at screen (378, 356, 2). The marker is then moved there and, because the target is in front, its anchored depth is
        // flattened to zero (0x6A2CA7..0x6A2CB5). The screen space event carries the screen point as it was before the depth reset.
        [Test]
        public void AFrontTargetOnAnOverlayCanvasMovesTheMarkerToItsScreenPoint()
        {
            Run(new Vector3(1f, 2f, 2f), (owned, scene) =>
            {
                scene.Update();

                AssertNear(scene.Position, new Vector3(378f, 356f, 0f), "marker position");
                Assert.That(scene.Marker.IsBehindCamera.Value, Is.False);
                Assert.That(scene.Published.Count, Is.EqualTo(1));
                AssertNear(scene.Published[0], new Vector3(378f, 356f, 2f), "published screen point");
            });
        }

        // On a screen space camera canvas the screen point is converted back through the canvas camera (ScreenToWorldPoint, 0x6A2B52):
        // for this camera that is the target's own world position.
        [Test]
        public void AFrontTargetOnACameraCanvasMovesTheMarkerToItsWorldPoint()
        {
            Run(new Vector3(1f, 2f, 2f), (owned, scene) =>
            {
                scene.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                scene.Canvas.worldCamera = scene.Camera;

                scene.Update();

                AssertNear(scene.Position, new Vector3(1f, 2f, 0f), "marker position");
                Assert.That(scene.Published.Count, Is.EqualTo(1));
                AssertNear(scene.Published[0], new Vector3(1f, 2f, 2f), "published world point");
            });
        }

        // A target behind the camera has a negative screen depth (IsBehindCamera = 0 > z, 0x6A2B14), keeps that depth on the marker (the
        // anchored depth is only flattened while the target is in front).
        [Test]
        public void ABehindTargetKeepsItsNegativeDepth()
        {
            Run(new Vector3(-1f, 1f, -2f), (owned, scene) =>
            {
                scene.Update();

                Assert.That(scene.Marker.IsBehindCamera.Value, Is.True);
                AssertNear(scene.Position, new Vector3(282f, 308f, -2f), "marker position");
            });
        }

        // 0x6A2BB6..0x6A2BD3: with lockYAxis the published position is the unlocked screen point, but the marker keeps its current height.
        [Test]
        public void LockingTheYAxisKeepsTheMarkersHeightButPublishesTheScreenPoint()
        {
            Run(new Vector3(1f, 2f, 2f), (owned, scene) =>
            {
                scene.Marker.RectTransform.position = new Vector3(0f, 77f, 0f);

                scene.Update(lockYAxis: true);

                AssertNear(scene.Position, new Vector3(378f, 77f, 0f), "marker position");
                Assert.That(scene.Published.Count, Is.EqualTo(1));
                AssertNear(scene.Published[0], new Vector3(378f, 356f, 2f), "published screen point");
            });
        }

        // 0x6A2C20..0x6A2C27: SmoothDamp only runs for a smooth time greater than zero (ucomiss / jbe: zero, negative and NaN all snap).
        // The marker starts at the origin, so a smoothed move stays short of the screen point 378 while a snap reaches it.
        [TestCase(0.5f, false, TestName = "APositiveSmoothTimeSmoothsTheMove")]
        [TestCase(0f, true, TestName = "AZeroSmoothTimeSnaps")]
        [TestCase(-1f, true, TestName = "ANegativeSmoothTimeSnaps")]
        [TestCase(float.NaN, true, TestName = "ANaNSmoothTimeSnaps")]
        public void OnlyAPositiveSmoothTimeSmoothsTheMove(float smoothTime, bool snaps)
        {
            Run(new Vector3(1f, 2f, 2f), (owned, scene) =>
            {
                scene.Marker.RectTransform.position = Vector3.zero;

                scene.Update(smoothTime);

                if (snaps)
                    Assert.That(scene.Position.x, Is.EqualTo(378f).Within(Tolerance));
                else
                    Assert.That(scene.Position.x, Is.LessThan(378f - 1f));
            });
        }

        // 0x6A2D0E..0x6A2D5B: the distance is the length of (character world position - target position); (0, 6, 8) from the origin is 10.
        [Test]
        public void TheDistanceIsTheLengthFromTheCharacterToTheTarget()
        {
            Run(new Vector3(0f, 6f, 8f), (owned, scene) =>
            {
                scene.Update();

                Assert.That(scene.Marker.DistanceToTarget.Value, Is.EqualTo(10f).Within(Tolerance));
            });
        }

        // 0x6A2D86..0x6A2DDD: visible when distance >= the minimum and, only for a maximum greater than zero, distance <= the maximum.
        // Distance 10. Rows (min, max, visible): the bounds are inclusive; a maximum of zero or below means no upper bound; a NaN
        // maximum is not greater than zero so it too means none; a NaN minimum fails the ordered compare.
        [TestCase(10f, 0f, true, TestName = "VisibleExactlyAtTheMinimum")]
        [TestCase(10.01f, 0f, false, TestName = "HiddenJustInsideTheMinimum")]
        [TestCase(0f, 10f, true, TestName = "VisibleExactlyAtTheMaximum")]
        [TestCase(0f, 9.99f, false, TestName = "HiddenJustBeyondTheMaximum")]
        [TestCase(0f, -1f, true, TestName = "ANegativeMaximumMeansNoLimit")]
        [TestCase(0f, 0f, true, TestName = "AZeroMaximumMeansNoLimit")]
        [TestCase(0f, float.NaN, true, TestName = "ANaNMaximumMeansNoLimit")]
        [TestCase(float.NaN, 0f, false, TestName = "ANaNMinimumHidesTheMarker")]
        [TestCase(5f, 15f, true, TestName = "VisibleInsideTheWindow")]
        public void TheDrawWindowDecidesVisibility(float min, float max, bool visible)
        {
            Run(new Vector3(0f, 6f, 8f), (owned, scene) =>
            {
                scene.Marker.IsVisible.Value = !visible;

                scene.Update();

                Assert.That(scene.Marker.IsVisible.Value, Is.EqualTo(visible));
            }, scene =>
            {
                Set(scene.Settings, "m_minDrawDistance", min);
                Set(scene.Settings, "m_maxDrawDistance", max);
            });
        }

        // 0x6A2DF0..0x6A2E8D: the height indicator is the target's offset along gravity: shown when its magnitude reaches the threshold
        // (inclusive), pointing "above" when positive. Target at (0, 6, 8) is 6 above the character with gravity down. Rows
        // (threshold, gravity, shown, above): the inclusive threshold, a higher threshold, and a gravity turned over.
        [TestCase(5f, -1f, true, true, TestName = "ATargetAboveShowsTheIndicatorPointingUp")]
        [TestCase(6f, -1f, true, true, TestName = "TheThresholdIsInclusive")]
        [TestCase(6.01f, -1f, false, false, TestName = "AHigherThresholdHidesTheIndicator")]
        [TestCase(5f, 1f, true, false, TestName = "ATargetBelowShowsTheIndicatorPointingDown")]
        public void TheHeightIndicatorFollowsTheOffsetAlongGravity(float threshold, float gravityY, bool shown, bool above)
        {
            Run(new Vector3(0f, 6f, 8f), (owned, scene) =>
            {
                scene.Character.Down = new Vector3(0f, gravityY, 0f);

                scene.Update();

                Assert.That(scene.Marker.ShowHeightIndicator.Value, Is.EqualTo(shown));
                Assert.That(scene.Marker.HeightIndicatorIsAbove.Value, Is.EqualTo(above));
            }, scene => Set(scene.Settings, "m_showHeightIndicatorThreshold", threshold));
        }

        // The "above" flag is only written while the indicator is shown: an update that hides it leaves the last direction.
        [Test]
        public void AHiddenHeightIndicatorLeavesItsLastDirection()
        {
            Run(new Vector3(0f, 6f, 8f), (owned, scene) =>
            {
                scene.Update();
                Assert.That(scene.Marker.HeightIndicatorIsAbove.Value, Is.True);

                Set(scene.Settings, "m_showHeightIndicatorThreshold", 100f);
                scene.Character.Down = Vector3.up;
                scene.Update();

                Assert.That(scene.Marker.ShowHeightIndicator.Value, Is.False);
                Assert.That(scene.Marker.HeightIndicatorIsAbove.Value, Is.True, "not rewritten while hidden, though the target is now below");
            }, scene => Set(scene.Settings, "m_showHeightIndicatorThreshold", 1f));
        }

        // Close 0x6A29E0 clears the icon, the target and the published state, and zeroes the smoothing velocity.
        [Test]
        public void CloseForgetsTheTargetAndResetsThePublishedState()
        {
            Run(new Vector3(0f, 6f, 8f), (owned, scene) =>
            {
                scene.Update(0.5f);
                scene.Marker.ShowHeightIndicator.Value = true;
                Assert.That(scene.Marker.Icon.Value, Is.Not.Null);
                Assert.That(scene.Marker.DistanceToTarget.Value, Is.EqualTo(10f).Within(Tolerance));

                scene.Marker.Close();

                Assert.That(scene.Marker.Icon.Value, Is.Null);
                Assert.That(Get<Transform>(scene.Marker, "m_target"), Is.Null);
                Assert.That(Get<bool>(scene.Marker, "m_targetAssigned"), Is.False);
                Assert.That(scene.Marker.IsVisible.Value, Is.False);
                Assert.That(scene.Marker.ShowHeightIndicator.Value, Is.False);
                Assert.That(scene.Marker.DistanceToTarget.Value, Is.EqualTo(0f));
                Assert.That(Get<Vector3>(scene.Marker, "m_positionDampVelocity"), Is.EqualTo(Vector3.zero));
            });
        }
    }
}
