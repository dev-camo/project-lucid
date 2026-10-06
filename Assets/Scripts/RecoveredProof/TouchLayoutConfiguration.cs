using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
using DeviceGeneration = HardlightProject.DevicePerformanceMatch_iOS.DeviceGeneration;
namespace HardlightProject
{
    [CreateAssetMenu(fileName = "TouchLayoutConfiguration", menuName = "HardlightProject/DefinitionData/Definitions/Touch/TouchLayoutConfiguration", order = 0)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TouchLayoutConfiguration : ScriptableObject
    {
        [SerializeField] private DeviceGeneration[] m_devices = Array.Empty<DeviceGeneration>();
        [SerializeField] private TouchControlDefault[] m_controlDefaults = Array.Empty<TouchControlDefault>();
        // Original06001f76 exposes the retained array directly.
        public DeviceGeneration[] Devices { get { return m_devices; } }
        // Original06001f77 resets out first, captures the array once, and returns
        // the first ordinal static String.Equals match, including two null IDs.
        public bool TryGetTouchControlDefault(string saveId, out TouchControlDefault defaultSetting)
        {
            defaultSetting = null;
            foreach (TouchControlDefault setting in m_controlDefaults)
                if (string.Equals(setting.SaveId, saveId))
                {
                    defaultSetting = setting;
                    return true;
                }
            return false;
        }
        // Original06001f78 initializes both shared Empty arrays before SO base.
        [Serializable]
        public class TouchControlDefault
        {
            [SerializeField] private string m_saveId;
            [SerializeField] private Vector2 m_position;
            [SerializeField] private float m_scale = 1f;
            // Original06001f79..7b, direct original serialized-field returns.
            public string SaveId { get { return m_saveId; } }
            public Vector2 Position { get { return m_position; } }
            public float Scale { get { return m_scale; } }
            // Original06001f7c sets scale1 before the natural Object base.
        }
    }
}
