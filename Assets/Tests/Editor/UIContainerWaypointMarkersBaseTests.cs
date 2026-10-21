using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerWaypointMarkersBase (Game.Runtime 0x0200094c, x86-64 0x6822B0..0x682BC0) through UIContainerWaypointMarkersCompass, the
    // concrete container whose per-frame work does not touch what these cases observe, as a real component over the scene of
    // WaypointMarkerContainerScene: a registered WaypointManager, App and DataManager, a marker prefab and real waypoint targets. Awake
    // is called directly. Not covered: Update (0x682980), whose dispatch to DoUpdate needs the CharacterManager's current character
    // and the camera manager; the derived containers' DoUpdate bodies are covered by their own fixtures.
    public sealed class UIContainerWaypointMarkersBaseTests
    {
        // dump.cs TypeDefIndex 2379 and the DummyDll rows (fields 0x04002653..0x0400265c, methods 0x06003579..0x0600357f).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerWaypointMarkersBase), 0x00100081,
                new (string, int)[]
                {
                    ("m_waypointMarkerPrefab", 0x0001), ("m_waypointMarkerParent", 0x0001), ("m_containerType", 0x0001),
                    ("m_smoothTimeSeconds", 0x0004), ("m_waypointManagerRef", 0x0021), ("m_cameraManagerRef", 0x0021),
                    ("m_characterManagerRef", 0x0021), ("m_appRef", 0x0021), ("m_waypointMarkers", 0x0024), ("m_rectTransform", 0x0004),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081), ("OnWaypointManagerValid", 0x0081), ("OnDestroy", 0x0081), ("Refresh", 0x0086), ("Update", 0x0081),
                    ("DoUpdate", 0x05c4), (".ctor", 0x1884),
                });
            AssertIl2CppOptions(typeof(UIContainerWaypointMarkersBase), 1, 2);
            Assert.That(typeof(UIContainerWaypointMarkersBase).BaseType, Is.EqualTo(typeof(UIContainer)));
            foreach (string field in new[] { "m_waypointMarkerPrefab", "m_waypointMarkerParent", "m_containerType" })
                Assert.That(AttributeTypes(typeof(UIContainerWaypointMarkersBase).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
            var smooth = typeof(UIContainerWaypointMarkersBase).GetField("m_smoothTimeSeconds", Declared);
            Assert.That(AttributeTypes(smooth), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(smooth), Is.EqualTo("Smooth damp time to move towards waypoint target."));
        }

        // The constructor 0x682A70 takes the four system references in field order and builds the marker list.
        [Test]
        public void ConstructionTakesTheSystemReferencesAndAnEmptyMarkerList()
        {
            WaypointMarkerContainerScene.Run(WaypointContainerType.Compass, scene =>
            {
                var container = scene.NewContainer<UIContainerWaypointMarkersCompass>();

                Assert.That(scene.Markers(container), Is.Empty);
                Assert.That(Get<SystemRef<WaypointManager>>(container, "m_waypointManagerRef").GetSafe(), Is.SameAs(scene.Manager));
                Assert.That(Get<SystemRef<CinemachineCameraManager>>(container, "m_cameraManagerRef"), Is.Not.Null);
                Assert.That(Get<SystemRef<CharacterManager>>(container, "m_characterManagerRef"), Is.Not.Null);
                Assert.That(Get<SystemRef<App>>(container, "m_appRef").Get(), Is.Not.Null);
            });
        }

        // Awake 0x6822B0 asks the manager reference for OnWaypointManagerValid (run at once because the manager is registered),
        // which subscribes Refresh to the manager's WaypointsUpdated action (0x682406) and refreshes at once; Awake then takes the
        // container's own object as a RectTransform.
        [Test]
        public void AwakeSubscribesToTheManagerRefreshesAndTakesTheRectTransform()
        {
            WaypointMarkerContainerScene.Run(WaypointContainerType.Compass, scene =>
            {
                var container = scene.NewContainer<UIContainerWaypointMarkersCompass>();
                scene.Manager.RegisterWaypoint(scene.NewTarget(new Vector3(1f, 2f, 3f)));

                Call(container, "Awake");

                var handler = scene.Manager.WaypointsUpdated.GetInvocationList().Single();
                Assert.That(handler.Target, Is.SameAs(container));
                Assert.That(handler.Method.Name, Is.EqualTo("Refresh"));
                Assert.That(Get<RectTransform>(container, "m_rectTransform"), Is.SameAs(container.GetComponent<RectTransform>()));
                Assert.That(scene.Markers(container), Has.Count.EqualTo(1), "the target registered before Awake already has its marker");
            });
        }

        // Refresh 0x682460 instantiates one marker per target that has a definition (Object.Instantiate of the prefab under the
        // parent, 0x68272F), activates it and sets it up with the target, the definition for its type and the container type.
        [Test]
        public void EachTargetWithADefinitionGetsAnActiveMarkerSetUpForIt()
        {
            WaypointMarkerContainerScene.Run(WaypointContainerType.Compass, scene =>
            {
                var container = scene.NewContainer<UIContainerWaypointMarkersCompass>();
                WaypointTarget first = scene.NewTarget(new Vector3(1f, 0f, 0f));
                WaypointTarget second = scene.NewTarget(new Vector3(2f, 0f, 0f));
                scene.Manager.RegisterWaypoint(first);
                scene.Manager.RegisterWaypoint(second);

                Call(container, "Awake");

                var markers = scene.Markers(container);
                Assert.That(markers, Has.Count.EqualTo(2));
                Assert.That(markers.Select(marker => marker.transform.parent), Is.All.SameAs(scene.MarkerParent.transform));
                Assert.That(markers.Select(marker => marker.gameObject.activeSelf), Is.All.True);
                Assert.That(markers.Select(marker => marker.Icon.Value), Is.All.SameAs(scene.Icon), "set up from the definition's settings");
                // Reflection: the marker keeps its target in a private field and shows no other sign of which target it follows.
                Assert.That(markers.Select(marker => Get<Transform>(marker, "m_target")).ToArray(), Is.EqualTo(new[] { first.transform, second.transform }));
                Assert.That(markers.Select(marker => marker.RectTransform == null), Is.All.False, "the clones' Awake ran when they were activated");
            });
        }

        // TryGetValue on the data manager's definitions (0x6826EA): a target whose type has no definition gets no marker, and the
        // markers are indexed by the target's position in the list, so the next defined target takes the first marker.
        [Test]
        public void ATargetWithoutADefinitionGetsNoMarker()
        {
            WaypointMarkerContainerScene.Run(WaypointContainerType.Compass, scene =>
            {
                var container = scene.NewContainer<UIContainerWaypointMarkersCompass>();
                WaypointTarget unknown = scene.NewTarget(new Vector3(1f, 0f, 0f), WaypointType.Enemy);
                WaypointTarget known = scene.NewTarget(new Vector3(2f, 0f, 0f));
                scene.Manager.RegisterWaypoint(unknown);

                Call(container, "Awake");
                Assert.That(scene.Markers(container), Is.Empty);

                scene.Manager.RegisterWaypoint(known);
                var markers = scene.Markers(container);
                Assert.That(markers, Has.Count.EqualTo(1));
                Assert.That(Get<Transform>(markers[0], "m_target"), Is.SameAs(known.transform), "reflection: the marker's private target");
            });
        }

        // The undefined target still uses up index 0, so the defined one at index 1 is past the marker list (jl at 0x6826FB not taken)
        // on every refresh and takes a fresh clone (0x682735); the closing loop starts at the target count (0x682806) and closes none.
        [Test]
        public void AnUndefinedTargetKeepsItsIndexSoEachRefreshAddsAnotherMarker()
        {
            WaypointMarkerContainerScene.Run(WaypointContainerType.Compass, scene =>
            {
                var container = scene.NewContainer<UIContainerWaypointMarkersCompass>();
                WaypointTarget unknown = scene.NewTarget(new Vector3(1f, 0f, 0f), WaypointType.Enemy);
                WaypointTarget known = scene.NewTarget(new Vector3(2f, 0f, 0f));
                scene.Manager.RegisterWaypoint(unknown);
                scene.Manager.RegisterWaypoint(known);
                Call(container, "Awake");
                Assert.That(scene.Markers(container), Has.Count.EqualTo(1));

                container.Refresh();

                var markers = scene.Markers(container);
                Assert.That(markers, Has.Count.EqualTo(2));
                Assert.That(markers.Select(marker => marker.gameObject.activeSelf), Is.All.True);
                Assert.That(markers.Select(marker => Get<Transform>(marker, "m_target")), Is.All.SameAs(known.transform), "reflection: the marker's private target");
            });
        }

        // The manager's WaypointsUpdated action calls Refresh: a target registered later gets its marker at once, and a removed
        // one leaves its marker closed (UIWidgetSDTWaypointMarker.Close 0x6A29E0) and inactive; the rest are set up again in order.
        [Test]
        public void RegisteringAndRemovingTargetsKeepsTheMarkersInStep()
        {
            WaypointMarkerContainerScene.Run(WaypointContainerType.Compass, scene =>
            {
                var container = scene.NewContainer<UIContainerWaypointMarkersCompass>();
                WaypointTarget first = scene.NewTarget(new Vector3(1f, 0f, 0f));
                WaypointTarget second = scene.NewTarget(new Vector3(2f, 0f, 0f));
                WaypointTarget third = scene.NewTarget(new Vector3(3f, 0f, 0f));
                scene.Manager.RegisterWaypoint(first);
                Call(container, "Awake");
                Assert.That(scene.Markers(container), Has.Count.EqualTo(1));

                scene.Manager.RegisterWaypoint(second);
                scene.Manager.RegisterWaypoint(third);
                var markers = scene.Markers(container);
                Assert.That(markers, Has.Count.EqualTo(3));

                scene.Manager.RemoveWaypoint(second);

                Assert.That(markers, Has.Count.EqualTo(3), "markers are kept for reuse");
                Assert.That(Get<Transform>(markers[0], "m_target"), Is.SameAs(first.transform));
                Assert.That(Get<Transform>(markers[1], "m_target"), Is.SameAs(third.transform), "set up again for the target that moved up");
                Assert.That(markers[2].gameObject.activeSelf, Is.False);
                Assert.That(markers[2].Icon.Value == null, Is.True, "Close cleared the icon");
                Assert.That(markers[1].gameObject.activeSelf, Is.True);
            });
        }

        // OnDestroy 0x682870 unsubscribes Refresh from the manager.
        [Test]
        public void DestroyingTheContainerStopsTheRefreshes()
        {
            WaypointMarkerContainerScene.Run(WaypointContainerType.Compass, scene =>
            {
                var container = scene.NewContainer<UIContainerWaypointMarkersCompass>();
                Call(container, "Awake");

                Call(container, "OnDestroy");

                Assert.That(scene.Manager.WaypointsUpdated, Is.Null);
                scene.Manager.RegisterWaypoint(scene.NewTarget(new Vector3(1f, 0f, 0f)));
                Assert.That(scene.Markers(container), Is.Empty);
            });
        }
    }
}
