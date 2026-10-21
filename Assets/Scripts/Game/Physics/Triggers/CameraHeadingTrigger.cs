using System;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000731. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CameraHeadingTrigger : CharacterTrigger
    {
        private enum SelectionMode
        {
            BestAlignedToIncomingDirection = 0,
            BestAlignedToCharacterForward = 1,
            BestAlignedToCameraForward = 2,
            ClosestDistance = 3,
            BestAlignedToCharacterVelocity = 4,
        }

        [SerializeField] private CameraHeadingOverride[] m_headingOverrides = Array.Empty<CameraHeadingOverride>();
        [SerializeField] private SelectionMode m_headingSelectionMode;
        [Tooltip("If enabled, will remove the camera heading override after number of defined seconds."), SerializeField]
        private bool m_useReleaseTimer;
        [ShowIf("m_useReleaseTimer", null), SerializeField]
        private float m_releaseAfterSeconds;
        [SerializeField, Tooltip("On exit, this will not trigger again until scene reload.")]
        private bool m_onlyTriggerOnce;
        [Tooltip("If enabled, will continuously update what is the best heading override to use."), SerializeField]
        private bool m_continuouslyUpdate;
        [ShowIf("m_continuouslyUpdate", null), Tooltip("Will wait this number of seconds before switching to new heading override when it is different to the currently selected one."), SerializeField]
        private float m_continuousUpdateSwitchDelaySeconds;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);
        private StackableDataHandle m_settingHandle;
        private Coroutine m_releaseTimerRoutine;
        private bool m_canTrigger = true;
        private CameraHeadingOverride m_currentHeadingOverride;
        private float m_continuousUpdateSwitchTimerSeconds;

        // 0x06002889
        protected override void Awake()
        {
            base.Awake();
            UpdateCachedValues();
        }

        // 0x0600288a
        protected override void DoTriggerEnter(Character character)
        {
            if (!m_canTrigger) return;
            base.DoTriggerEnter(character);
            ApplyCameraSettingOverride(character);
        }

        // 0x0600288b: with continuous updates, a different best heading only replaces the live one after the switch delay.
        protected override void DoTriggerStay(Character character)
        {
            base.DoTriggerStay(character);
            if (!m_continuouslyUpdate || m_settingHandle == null || m_cameraManagerRef.IsNull()) return;

            CameraHeadingOverride headingOverride = GetHeadingOverride(character);
            if (m_currentHeadingOverride != headingOverride)
            {
                m_continuousUpdateSwitchTimerSeconds += m_timeManagerRef.Get().GetDeltaTime(TimeCategory.Camera);
                // jb after ucomiss: an unordered (NaN) delay keeps the current override as well.
                if (!(m_continuousUpdateSwitchTimerSeconds >= m_continuousUpdateSwitchDelaySeconds)) return;

                m_currentHeadingOverride = headingOverride;
                m_cameraManagerRef.Get().ModifyCameraSettingOverride(CameraSettingType.HeadingOverride,
                    m_currentHeadingOverride, m_settingHandle);
            }
            m_continuousUpdateSwitchTimerSeconds = 0f;
        }

        // 0x0600288c
        protected override void DoTriggerExit(Character character)
        {
            if (!m_canTrigger) return;
            base.DoTriggerExit(character);
            RemoveCameraSettingOverride();
        }

        // 0x0600288d
        private void OnDisable() { RemoveCameraSettingOverride(); }

        // 0x0600288e
        public void Action_ApplySettingOverride()
        {
            if (TryGetCharacter(out Character character)) ApplyCameraSettingOverride(character);
        }

        // 0x0600288f
        public void Action_RemoveSettingOverride() { RemoveCameraSettingOverride(); }

        // 0x06002890: replaces any live override, then optionally schedules its release.
        private void ApplyCameraSettingOverride(Character character)
        {
            if (m_cameraManagerRef.IsNull()) return;
            if (m_settingHandle != null) RemoveCameraSettingOverride();

            m_currentHeadingOverride = GetHeadingOverride(character);
            m_settingHandle = m_cameraManagerRef.Get().ApplyCameraSettingOverride(CameraSettingType.HeadingOverride,
                m_currentHeadingOverride);
            if (!m_useReleaseTimer) return;

            if (m_releaseTimerRoutine != null) CoroutineUtils.StopUtilCoroutine(ref m_releaseTimerRoutine);
            m_releaseTimerRoutine = CoroutineUtils.Delay(RemoveCameraSettingOverride, m_releaseAfterSeconds);
        }

        // 0x06002891: a trigger flagged only-once stays disabled after its override has been released.
        private void RemoveCameraSettingOverride()
        {
            if (m_releaseTimerRoutine != null) CoroutineUtils.StopUtilCoroutine(ref m_releaseTimerRoutine);
            m_releaseTimerRoutine = null;
            if (m_settingHandle == null || m_cameraManagerRef.IsNull()) return;

            m_cameraManagerRef.Get().RemoveCameraSettingOverride(m_settingHandle);
            m_settingHandle = null;
            m_currentHeadingOverride = null;
            m_continuousUpdateSwitchTimerSeconds = 0f;
            if (m_onlyTriggerOnce) m_canTrigger = false;
        }

        // 0x06002892
        private CameraHeadingOverride GetHeadingOverride(Character character)
        {
            switch (m_headingSelectionMode)
            {
                case SelectionMode.BestAlignedToIncomingDirection: return SelectBestAlignedIncomingAngle(character);
                case SelectionMode.BestAlignedToCharacterForward: return SelectBestAlignedCharacterForward(character);
                case SelectionMode.BestAlignedToCameraForward: return SelectBestAlignedCameraForward();
                case SelectionMode.ClosestDistance: return SelectClosest(character);
                case SelectionMode.BestAlignedToCharacterVelocity: return SelectBestAlignedCharacterVelocity(character);
                default: return null;
            }
        }

        // 0x06002893: the nearest heading whose minimum-angle rule accepts the character's facing.
        private CameraHeadingOverride SelectClosest(Character character)
        {
            CameraHeadingOverride closest = null;
            float closestDistance = float.PositiveInfinity;
            foreach (CameraHeadingOverride headingOverride in m_headingOverrides)
            {
                float cosine = Vector3.Dot(headingOverride.Target.forward, character.ForwardDirection);
                if (!IsHeadingValidSelection(headingOverride, cosine)) continue;
                float distance = Vector3.Distance(headingOverride.Target.position, character.WorldPosition);
                if (closestDistance > distance)
                {
                    closest = headingOverride;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        // 0x06002894: the direction is taken from the character towards each heading target.
        private CameraHeadingOverride SelectBestAlignedIncomingAngle(Character character)
        {
            CameraHeadingOverride best = null;
            float bestCosine = float.NegativeInfinity;
            foreach (CameraHeadingOverride headingOverride in m_headingOverrides)
            {
                Vector3 incoming = (headingOverride.Target.position - character.WorldPosition).normalized;
                float cosine = Vector3.Dot(headingOverride.Target.forward, incoming);
                if (IsHeadingValidSelection(headingOverride, cosine) && cosine > bestCosine)
                {
                    best = headingOverride;
                    bestCosine = cosine;
                }
            }
            return best;
        }

        // 0x06002895
        private CameraHeadingOverride SelectBestAlignedCharacterForward(Character character)
        {
            return SelectBestAlignedCharacterDirection(character.ForwardDirection);
        }

        // 0x06002896
        private CameraHeadingOverride SelectBestAlignedCharacterVelocity(Character character)
        {
            return SelectBestAlignedCharacterDirection(character.WorldVelocityNormalised);
        }

        // 0x06002897
        private CameraHeadingOverride SelectBestAlignedCharacterDirection(Vector3 direction)
        {
            CameraHeadingOverride best = null;
            float bestCosine = float.NegativeInfinity;
            foreach (CameraHeadingOverride headingOverride in m_headingOverrides)
            {
                float cosine = Vector3.Dot(headingOverride.Target.forward, direction);
                if (IsHeadingValidSelection(headingOverride, cosine) && cosine > bestCosine)
                {
                    best = headingOverride;
                    bestCosine = cosine;
                }
            }
            return best;
        }

        // 0x06002898
        private CameraHeadingOverride SelectBestAlignedCameraForward()
        {
            Vector3 cameraForward = m_cameraManagerRef.Get().CinemachineBrain.transform.forward;
            CameraHeadingOverride best = null;
            float bestCosine = float.NegativeInfinity;
            foreach (CameraHeadingOverride headingOverride in m_headingOverrides)
            {
                float cosine = Vector3.Dot(headingOverride.Target.forward, cameraForward);
                if (IsHeadingValidSelection(headingOverride, cosine) && cosine > bestCosine)
                {
                    best = headingOverride;
                    bestCosine = cosine;
                }
            }
            return best;
        }

        // 0x06002899: a heading with a minimum angle rejects directions whose cosine is below its limit.
        private bool IsHeadingValidSelection(CameraHeadingOverride headingOverride, float directionAngleCosine)
        {
            CameraHeadingOverride.CameraHeadingOverrideSettings settings = headingOverride.Settings;
            return !settings.RequireMinimumAngle || !(settings.MaximumAngleThresholdCosine > directionAngleCosine);
        }

        // 0x0600289a
        private void UpdateCachedValues()
        {
            foreach (CameraHeadingOverride headingOverride in m_headingOverrides) headingOverride.UpdateCachedValues();
        }
    }
}
