using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000298. Overrides the camera Y axis target while the character is
    // inside the trigger.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineYUpOverrideTrigger : CharacterTrigger
    {
        [SerializeField]
        private CameraYAxisProxyManager.TargetData m_targetData;
        [SerializeField]
        private bool m_removeOnDisable = true;
        private StackableDataHandle m_overrideHandle;

        // 0x06000e5c; x86_64 0x7259E0. An override this trigger already holds is replaced.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            CameraYAxisProxyManager cameraYAxisProxyManager = ProcessManager.GetSystemSafe<CameraYAxisProxyManager>(null, true);
            if (cameraYAxisProxyManager == null) return;
            if (m_overrideHandle != null) RemoveCameraYUpOverride();
            m_overrideHandle = cameraYAxisProxyManager.OnAddCameraYUpOverride(m_targetData);
        }

        // 0x06000e5d; x86_64 0x725B90.
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            RemoveCameraYUpOverride();
        }

        // 0x06000e5e; x86_64 0x725BB0.
        private void OnDisable()
        {
            if (m_removeOnDisable) RemoveCameraYUpOverride();
        }

        // 0x06000e5f; x86_64 0x725AD0. The handle is kept after removal.
        private void RemoveCameraYUpOverride()
        {
            if (m_overrideHandle == null) return;
            CameraYAxisProxyManager cameraYAxisProxyManager = ProcessManager.GetSystemSafe<CameraYAxisProxyManager>(null, true);
            if (cameraYAxisProxyManager == null) return;
            cameraYAxisProxyManager.OnRemoveCameraYUpOverride(m_overrideHandle);
        }

        // 0x06000e60
        public CinemachineYUpOverrideTrigger() { }
    }
}
