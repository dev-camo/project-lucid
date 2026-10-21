using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000835; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InvertCameraSetting : MonoBehaviour
    {
        private SaveManager m_saveManager;

        // 0x06002fa0 (0x607E80)
        private void Awake()
        {
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(OnSaveManagerValid);
        }

        // 0x06002fa1 (0x607F60)
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            m_saveManager = saveManager;
        }

        // 0x06002fa2 (0x607F80): flips the stored flag and saves; a missing manager or settings object does nothing.
        public void Action_InvertCameraSetting()
        {
            SaveDataSettings settings = m_saveManager?.GetSaveDataSettings();
            if (settings == null)
                return;

            settings.CameraInvertedControls = !settings.CameraInvertedControls;
            m_saveManager.RequestSaveSettings();
        }
    }
}
