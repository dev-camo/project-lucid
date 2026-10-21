using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HardlightProject
{
    // Original Game.Runtime 02000970; bodies translated from the 1.10.1 x86-64 binary.
    // Reads the touch source every frame and hands each touch to the on-screen touch zones: touches a zone
    // already follows are updated or ended there, and every new touch goes to the best zone that can take it.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OnScreenControlInputHandler : MonoBehaviour
    {
        [SerializeField] private OnScreenTouchZone[] m_controlZones;
        [SerializeField] private GameObject[] m_graphicsToToggle;
        [SerializeField] private List<UIInputType> m_uiInputTypesWhichToggleOn = new List<UIInputType>();
        // No retail body reads this flag.
        [SerializeField] private bool m_mouseShowsInEditor = true;
        private const int MaxAssumedTouchCount = 5;
        private readonly List<Touch> m_currentTouches = new List<Touch>(MaxAssumedTouchCount);
        private InputMonitor m_inputMonitor;
        private bool m_firstUpdateComplete;
        private bool m_isValidInputType;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        // 06003657 (1.10.1 RVA 0x68D000): the graphics follow the last input type, the zones start empty and the
        // current character shows its UI.
        private void OnEnable()
        {
            m_firstUpdateComplete = false;
            m_inputMonitor = ProcessManager.GetSystem<InputSystem>().InputMonitor;
            bool validInputType = m_uiInputTypesWhichToggleOn.Contains(
                ProcessManager.GetSystemSafe<UIRuntimeConfiguration>().InputBridge.GetLastInputType());
            foreach (GameObject graphic in m_graphicsToToggle) graphic.SetActive(validInputType);
            m_isValidInputType = validInputType;
            InputBridge.OnUpdateLastInputType += OnInputTypeChanged;
            foreach (OnScreenTouchZone zone in m_controlZones) zone.Initialise();
            if (m_characterManagerRef.TryGet(out CharacterManager characterManager) &&
                characterManager.TryGetCurrentCharacter(out Character character))
            {
                character.OnEnableUI();
            }
        }

        // 06003658 (RVA 0x68D310)
        private void OnDisable()
        {
            foreach (OnScreenTouchZone zone in m_controlZones) zone.Shutdown(m_isValidInputType);
            InputBridge.OnUpdateLastInputType -= OnInputTypeChanged;
        }

        // 06003659 (RVA 0x68D430)
        private void Update()
        {
            if (m_inputMonitor == null) return;
            if (m_inputMonitor.SourceProvider?.TouchSource == null) return;

            IInputTouchSource touchSource = m_inputMonitor.SourceProvider.TouchSource;
            int touchCount = touchSource.GetTouchCount();
            m_currentTouches.Clear();
            for (int i = 0; i < touchCount; i++) m_currentTouches.Add(touchSource.GetTouch(i));

            // Touches a zone already follows are consumed here, from the last touch-source index down: updated, or ended with their phase.
            foreach (OnScreenTouchZone zone in m_controlZones)
            {
                if (m_currentTouches.Count == 0 || !zone.IsActive())
                {
                    zone.ClearAllTouches();
                    continue;
                }
                if (!zone.PointerIsDown) continue;
                for (int i = m_currentTouches.Count - 1; i >= 0; i--)
                {
                    Touch touch = m_currentTouches[i];
                    if (!zone.IsTrackingTouch(touch)) continue;
                    m_currentTouches.Remove(touch);
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) zone.StopTrackingTouch(touch);
                    else zone.UpdateTouch(touch);
                }
            }

            // The remaining touches are new: only a began touch starts one, except that moving or stationary
            // touches are accepted too until the first update has completed.
            foreach (Touch touch in m_currentTouches)
            {
                bool startsTracking = touch.phase == TouchPhase.Began ||
                    (!m_firstUpdateComplete && (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary));
                if (!startsTracking) continue;
                if (EventSystem.current.IsPointerOverGameObject(touch.fingerId)) continue;

                // The lowest score wins; the search stops at the first valid zone that scores 0 or less (NaN does not stop it).
                // A found flag rather than a null test: the original has no Unity object comparison here.
                bool zoneFound = false;
                OnScreenTouchZone bestZone = null;
                float bestScore = float.MaxValue;
                foreach (OnScreenTouchZone zone in m_controlZones)
                {
                    if (!zone.CanAcceptNewTouch()) continue;
                    if (!zone.TouchPositionValid(touch.position, out float score)) continue;
                    if (!zoneFound || bestScore > score)
                    {
                        zoneFound = true;
                        bestZone = zone;
                        bestScore = score;
                    }
                    if (score <= 0f) break;
                }
                if (zoneFound) bestZone.StartTrackingTouch(touch);
            }

            foreach (OnScreenTouchZone zone in m_controlZones)
            {
                if (zone.IsActive()) zone.SupplyTouchData();
            }
            m_firstUpdateComplete = true;
        }

        // 0600365a (RVA 0x68E720)
        private int GetFingerId(Touch touch)
        {
            return touch.fingerId;
        }

        // 0600365b (RVA 0x68D270)
        private void OnInputTypeChanged(UIInputType inputType)
        {
            bool validInputType = m_uiInputTypesWhichToggleOn.Contains(inputType);
            foreach (GameObject graphic in m_graphicsToToggle) graphic.SetActive(validInputType);
            m_isValidInputType = validInputType;
        }

        // 0600365c..0600365e (RVA 0x68D300, 0x68D420, 0x68E1D0): empty in the retail binary.
        private void SetupDebugButtons()
        {
        }

        private void DestroyDebugButtons()
        {
        }

        private void AddDebugTouch()
        {
        }

        // 0600365f (RVA 0x68EF10)
        public OnScreenControlInputHandler()
        {
        }
    }
}
