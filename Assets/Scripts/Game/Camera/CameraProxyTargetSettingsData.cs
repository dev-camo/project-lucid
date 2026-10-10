using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000260. All original fields and declarations retained.
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "CameraProxyTargetSettingsData", menuName = "HardlightProject/DefinitionData/Definitions/CameraProxyTargetSettingsData", order = 1)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CameraProxyTargetSettingsData : ScriptableObjectWithGuid
    {
        [SerializeField] private CameraProxyTargetSettings m_targetSettings;

        // 06000d09 / ARM 6f05f0: return the complete original settings struct.
        public CameraProxyTargetSettings TargetSettings => m_targetSettings;
        // 06000d0a / ARM 6f0604, genuine ObjectUtils generic context 032859c8.
        public static CameraProxyTargetSettingsData FindByName(string name)
        {
            return ObjectUtils.FindByName<CameraProxyTargetSettingsData>(name);
        }
        // 06000d0b / ARM 6f0658: preserve default settings and genuine GUID base.
        public CameraProxyTargetSettingsData() { }
    }
}
