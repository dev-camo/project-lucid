using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000264.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CameraTypeTriggerVolume : CharacterTrigger
    {
        [SerializeField] private CameraType m_cameraType;
        [SerializeField] private Transform m_followOverride;
        [SerializeField] private Transform m_lookAtOverride;
        [Tooltip("On enter, camera type will be overriden after number of seconds."), SerializeField]
        private float m_delayTypeOverrideSeconds;
        [Tooltip("On enter, camera targets will be overriden after number of seconds."), SerializeField]
        private float m_delaySetTargetsSeconds;
        private StackableDataHandle m_handle;
        private Coroutine m_applyOverrideRoutine;
        private Coroutine m_setTargetsRoutine;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);

        // 0x06000d1e: any earlier override is released first, then each part applies now or after its delay.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            StopRoutines();
            TryRemoveOverride();

            if (m_delayTypeOverrideSeconds > 0f)
                m_applyOverrideRoutine = CoroutineUtils.Delay(ApplyOverride, m_delayTypeOverrideSeconds);
            else
                ApplyOverride();

            if (m_delaySetTargetsSeconds > 0f)
                m_setTargetsRoutine = CoroutineUtils.Delay(SetCameraTargets, m_delaySetTargetsSeconds);
            else
                SetCameraTargets();
        }

        // 0x06000d1f
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            StopRoutines();
            TryRemoveOverride();
        }

        // 0x06000d20
        private void OnDisable()
        {
            StopRoutines();
            TryRemoveOverride();
        }

        // 0x06000d21
        private void ApplyOverride()
        {
            m_handle = m_cameraManagerRef.Get().ApplyCameraTypeOverride(m_cameraType);
        }

        // 0x06000d22: the handle is kept after release, so a repeat release asks the manager again.
        private void TryRemoveOverride()
        {
            if (m_handle == null || m_cameraManagerRef.IsNull()) return;
            m_cameraManagerRef.Get().RemoveCameraSettingOverride(m_handle);
        }

        // 0x06000d23
        private void SetCameraTargets()
        {
            m_cameraManagerRef.Get().SetCameraTargetsForType(m_cameraType, m_followOverride, m_lookAtOverride);
        }

        // 0x06000d24
        private void StopRoutines()
        {
            CoroutineUtils.StopUtilCoroutine(ref m_applyOverrideRoutine);
            CoroutineUtils.StopUtilCoroutine(ref m_setTargetsRoutine);
        }
    }
}
