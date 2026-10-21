using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000733. Pushes the authored proxy settings onto the camera
    // manager while the character is inside, and clears the override on exit or disable.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CameraProxySettingsOverrideTrigger : CharacterTrigger
    {
        [Header("Make sure to set priority higher than zero to override abilities.")]
        [SerializeField] private CameraProxyTargetSettings m_overrideSettings;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private StackableDataHandle m_cameraOverrideHandle;

        // 0x0600289c; x86_64 0x5C97E0.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            if (!m_cameraManagerRef.IsNull())
                m_cameraManagerRef.Get().SetCameraProxySettings(m_overrideSettings, ref m_cameraOverrideHandle);
        }

        // 0x0600289d; x86_64 0x5C98A0.
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            if (m_cameraManagerRef.IsNull()) return;
            m_cameraManagerRef.Get().RemoveCameraProxySettingOverride(m_cameraOverrideHandle);
            m_cameraOverrideHandle = null;
        }

        // 0x0600289e; x86_64 0x5C99E0. Only an installed override is removed.
        private void OnDisable()
        {
            if (m_cameraOverrideHandle == null || m_cameraManagerRef.IsNull()) return;
            m_cameraManagerRef.Get().RemoveCameraProxySettingOverride(m_cameraOverrideHandle);
            m_cameraOverrideHandle = null;
        }

        // 0x0600289f; x86_64 0x5C9940. Unlike OnDisable, the handle is passed on even when null.
        public void ClearOverride()
        {
            if (m_cameraManagerRef.IsNull()) return;
            m_cameraManagerRef.Get().RemoveCameraProxySettingOverride(m_cameraOverrideHandle);
            m_cameraOverrideHandle = null;
        }

        // 0x060028a0; the manager reference is resolved before the base constructor.
        public CameraProxySettingsOverrideTrigger() { }
    }
}
