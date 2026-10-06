using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
using DeviceGeneration = HardlightProject.DevicePerformanceMatch_iOS.DeviceGeneration;
namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "TouchLayoutConfigurationGroup", menuName = "HardlightProject/DefinitionData/Groups/TouchLayoutConfigurationGroup")]
    public class TouchLayoutConfigurationGroup : DefinitionDataType<string, TouchLayoutConfiguration>
    {
        [SerializeField] private TouchLayoutConfiguration m_iPhoneDefault;
        [SerializeField] private TouchLayoutConfiguration m_iPadDefault;
        [SerializeField] private DeviceGeneration m_simulatedDeviceInEditor = DeviceGeneration.iPhone12;
        // Original06001f7d..7f, stored enum, real engine object name, null comparer.
        public DeviceGeneration SimulatedDevice { get { return m_simulatedDeviceInEditor; } }
        protected override string GetElementKey(TouchLayoutConfiguration data) { return data.name; }
        protected override IEqualityComparer<string> GetKeyComparer() { return null; }
        // Original06001f80. Keep row-first selection, exact original enum-name
        // substring and Unity live-object tests. Each default field is reloaded
        // after its comparison; absent defaults create a genuine concrete asset.
        public TouchLayoutConfiguration GetDefaultDeviceLayout(DeviceGeneration deviceGeneration)
        {
            foreach (var element in m_elements)
            {
                TouchLayoutConfiguration data = element.Data;
                if (data.Devices.Contains(deviceGeneration)) return data;
            }
            if (deviceGeneration.ToString().Contains("iPad"))
            {
                if (m_iPadDefault != null) return m_iPadDefault;
            }
            else
            {
                if (m_iPhoneDefault != null) return m_iPhoneDefault;
            }
            return ScriptableObject.CreateInstance<TouchLayoutConfiguration>();
        }
        // Original06001f81 stores the simulated enum57 before actual definition base.
    }
}
