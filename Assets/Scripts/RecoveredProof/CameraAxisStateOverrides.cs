using Hardlight;
using Cinemachine;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption((Option)1, false)]
    [CreateAssetMenu(fileName = "CameraAxisStateOverrides", menuName = "HardlightProject/DefinitionData/Definitions/CameraAxisStateOverrides")]
    [Il2CppSetOption((Option)2, false)]
    public class CameraAxisStateOverrides : ScriptableObject
    {
        [Tooltip("Lookup by type to camera axis state definitions.")]
        [SerializeField]
        private SerializableDictionary<CameraSensitivityType, CameraAxisStateDefinition> m_overrides = new SerializableDictionary<CameraSensitivityType, CameraAxisStateDefinition>(HardlightEnumComparers.CameraSensitivityTypeComparer);

        // Original06001a6b returns the original lookup boolean and applies a found
        // definition without a Unity/null guard. A missing key leaves the axis untouched.
        public bool SetOverride(CameraSensitivityType key, ref AxisState axisState)
        {
            bool found = m_overrides.TryGetValue(key, out CameraAxisStateDefinition definition);
            if (found) definition.Set(ref axisState);
            return found;
        }

        // Original06001a6c creates/publishes the concrete-comparer dictionary before base.
        public CameraAxisStateOverrides() { }
    }
}
