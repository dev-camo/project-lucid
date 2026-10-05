using System;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "RuntimeConfiguration", menuName = "Hardlight/Runtime Configuration")]
    public class RuntimeConfiguration : ScriptableObject, ISystemConfigurationAssetCollection
    {
        [SerializeField] private SystemConfigurationAsset[] m_configurationAssets;
        // HLUnityCore.Runtime 0x06000e5b; ARM64 0x1b1a4e4. Null arrays throw; each call wraps the same array anew.
        public ReadOnlyCollection<SystemConfigurationAsset> Assets => Array.AsReadOnly(m_configurationAssets);
        // 0x06000e5c; ARM64 0x1b1a538. No array initializer exists in the supplied native constructor.
        public RuntimeConfiguration() { }
    }
}
