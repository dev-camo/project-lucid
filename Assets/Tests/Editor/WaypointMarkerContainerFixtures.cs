using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The scene the waypoint marker containers run in: the registered WaypointManager, App and DataManager rows their
    // field initialisers and Refresh read, a marker prefab on an inactive object, an active parent for the cloned markers,
    // a real WaypointDefinition for the container type, and (on request) an owned orthographic camera, a canvas and a gravity
    // character for the markers' per-frame update. The camera has an identity pose at the origin, a 1024 x 1024 render texture and pixel
    // rect (10, 20, 640, 480): the screen point of a world point is (330 + 48x, 260 + 48y) and its depth is the world z, as in the
    // waypoint marker fixture. Serialized fields of the containers are authored data.
    internal sealed class WaypointMarkerContainerScene
    {
        public Owned Owned;
        public WaypointManager Manager;
        public DataManager Data;
        public WaypointDefinition Definition;
        public WaypointSettings Settings;
        public WaypointContainerType ContainerType;
        public Sprite Icon;
        public UIWidgetSDTWaypointMarker Prefab;
        public GameObject MarkerParent;
        public Camera Camera;
        public Canvas Canvas;
        public CharacterGravityProbe Character;

        // Every marker the container instantiated, in the order of its list.
        public List<UIWidgetSDTWaypointMarker> Markers(UIContainerWaypointMarkersBase container) =>
            Get<List<UIWidgetSDTWaypointMarker>>(container, "m_waypointMarkers");

        // The rows are registered before any container exists, because the containers' field initialisers cache typed references.
        public static void Run(WaypointContainerType containerType, Action<WaypointMarkerContainerScene> body, bool withCamera = false,
            Action<WaypointSettings> configure = null) => Owned.Run(owned =>
        {
            owned.LeaseRow<CinemachineCameraManager>();
            owned.LeaseRow<CharacterManager>();
            owned.LeaseRow<GameplayIslandManager>();
            var scene = new WaypointMarkerContainerScene { Owned = owned, ContainerType = containerType };
            scene.Manager = owned.Register(new WaypointManager());
            UiPublishingFixtures.NewApp(owned);
            scene.Data = owned.Register(owned.Host("Lucid waypoint data manager", false).AddComponent<DataManager>());
            scene.Icon = NewSprite(owned);
            scene.Settings = new WaypointSettings();
            Set(scene.Settings, "m_icon", scene.Icon);
            configure?.Invoke(scene.Settings);
            scene.Definition = Create<WaypointDefinition>(owned);
            Get<SerializableDictionary<WaypointContainerType, WaypointSettings>>(scene.Definition, "m_settingsByContainerType")[containerType] =
                scene.Settings;
            scene.Data.WaypointDefinitions[WaypointType.Checkpoint] = scene.Definition;
            GameObject prefabHost = owned.Track(new GameObject("Lucid waypoint marker prefab", typeof(RectTransform)));
            prefabHost.SetActive(false);
            scene.Prefab = prefabHost.AddComponent<UIWidgetSDTWaypointMarker>();
            Set(scene.Prefab, "m_screenSpacePositionUpdated", new UnityEvent<Vector3>());
            scene.MarkerParent = owned.Host("Lucid waypoint marker parent");
            if (withCamera) scene.AddCamera();
            body(scene);
        });

        private void AddCamera()
        {
            RenderTexture texture = Owned.Track(new RenderTexture(1024, 1024, 0));
            Camera = Owned.Host("Lucid waypoint camera", false).AddComponent<Camera>();
            Camera.enabled = false;
            Camera.targetTexture = texture;
            Camera.orthographic = true;
            Camera.orthographicSize = 5f;
            Camera.aspect = 4f / 3f;
            Camera.pixelRect = new Rect(10f, 20f, 640f, 480f);
            Canvas = Owned.Host("Lucid waypoint canvas", false).AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Character = NewGravityCharacter(Owned, Vector3.down);
        }

        // A container of the given type on an inactive object with a RectTransform, wired to the scene. Awake is not run.
        public T NewContainer<T>() where T : UIContainerWaypointMarkersBase
        {
            GameObject host = Owned.Track(new GameObject("Lucid waypoint markers", typeof(RectTransform)));
            host.SetActive(false);
            var container = host.AddComponent<T>();
            Set(container, "m_waypointMarkerPrefab", Prefab);
            Set(container, "m_waypointMarkerParent", MarkerParent.transform);
            Set(container, "m_containerType", ContainerType);
            if (Canvas != null) Set(container, "m_canvas", Canvas);
            return container;
        }

        // A target of the given type standing at a world position; registering it with the manager is the case's step.
        public WaypointTarget NewTarget(Vector3 position, WaypointType type = WaypointType.Checkpoint)
        {
            var target = Owned.Host("Lucid waypoint target", false).AddComponent<WaypointTarget>();
            Set(target, "m_type", type);
            target.transform.position = position;
            return target;
        }

        // An active marker (its Awake has run) set up for a target standing at the given world position.
        public UIWidgetSDTWaypointMarker NewMarker(Vector3 targetPosition, bool active = true)
        {
            GameObject host = Owned.Track(new GameObject("Lucid waypoint marker", typeof(RectTransform)));
            host.SetActive(false);
            var marker = host.AddComponent<UIWidgetSDTWaypointMarker>();
            Set(marker, "m_screenSpacePositionUpdated", new UnityEvent<Vector3>());
            host.SetActive(active);
            if (!active) Call(marker, "Awake");
            marker.Setup(new UIWidgetSDTWaypointMarkerParameters(NewTarget(targetPosition), Definition, ContainerType));
            return marker;
        }
    }
}
