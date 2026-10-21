using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200094c; bodies translated from the 1.10.1 x86-64 binary.
    // Keeps one marker widget per waypoint target and lets the derived container position them every frame.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class UIContainerWaypointMarkersBase : UIContainer
    {
        [SerializeField] private UIWidgetSDTWaypointMarker m_waypointMarkerPrefab;
        [SerializeField] private Transform m_waypointMarkerParent;
        [SerializeField] private WaypointContainerType m_containerType;
        [SerializeField] [Tooltip("Smooth damp time to move towards waypoint target.")] protected float m_smoothTimeSeconds;
        private readonly SystemRef<WaypointManager> m_waypointManagerRef = ProcessManager.GetSystemRef<WaypointManager>(null, true);
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        protected readonly List<UIWidgetSDTWaypointMarker> m_waypointMarkers = new List<UIWidgetSDTWaypointMarker>();
        protected RectTransform m_rectTransform;

        // 0x06003579 (0x6822B0)
        private void Awake()
        {
            m_waypointManagerRef.InvokeOnValid(OnWaypointManagerValid);
            m_rectTransform = (RectTransform)transform;
        }

        // 0x0600357a (0x682390)
        private void OnWaypointManagerValid(WaypointManager waypointManager)
        {
            waypointManager.WaypointsUpdated += Refresh;
            Refresh();
        }

        // 0x0600357b (0x682870)
        private void OnDestroy()
        {
            if (m_waypointManagerRef.IsNull())
                return;
            m_waypointManagerRef.Get().WaypointsUpdated -= Refresh;
        }

        // 0x0600357c (0x682460): target i is shown by marker i, created on demand; targets without a definition leave their
        // marker untouched. Markers beyond the target count are closed and hidden.
        public void Refresh()
        {
            DataManager dataManager = m_appRef.Get().DataManager;
            IReadOnlyList<WaypointTarget> targets = m_waypointManagerRef.Get().GetTargets();
            for (int i = 0; i < targets.Count; i++)
            {
                WaypointTarget target = targets[i];
                if (!dataManager.WaypointDefinitions.TryGetValue(target.Type, out WaypointDefinition definition))
                    continue;
                UIWidgetSDTWaypointMarker marker;
                if (i < m_waypointMarkers.Count)
                {
                    marker = m_waypointMarkers[i];
                }
                else
                {
                    marker = Instantiate(m_waypointMarkerPrefab, m_waypointMarkerParent);
                    m_waypointMarkers.Add(marker);
                }
                marker.gameObject.SetActive(true);
                marker.Setup(new UIWidgetSDTWaypointMarkerParameters(target, definition, m_containerType));
            }
            for (int i = targets.Count; i < m_waypointMarkers.Count; i++)
            {
                UIWidgetSDTWaypointMarker marker = m_waypointMarkers[i];
                marker.Close();
                marker.gameObject.SetActive(false);
            }
        }

        // 0x0600357d (0x682980)
        private void Update()
        {
            if (m_cameraManagerRef.IsNull() || m_characterManagerRef.IsNull())
                return;
            if (m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
                DoUpdate(character, m_cameraManagerRef.Get().MainCamera);
        }

        // 0x0600357e
        protected abstract void DoUpdate(Character character, Camera mainCamera);

        // 0x0600357f (0x682A70)
        protected UIContainerWaypointMarkersBase() { }
    }
}
