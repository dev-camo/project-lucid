using UnityEngine;

namespace Hardlight
{
    // HLUnityCore.Runtime 0x02000223; original Resource wrapper and serialized field identity.
    public class ActiveRuntimeConfiguration : SingleScriptableObject, IActiveConfiguration
    {
        public const string DefaultLocation = "Assets/Configuration/Resources/";
        public const string FileName = "ActiveRuntimeConfiguration";
        public const string ActiveConfigFieldName = "ActiveConfig";
        [SerializeField] public RuntimeConfiguration ActiveConfig;
        // 0x06000de1..0x06000de4; ARM64 0x1b1855c/0x1b18564/0x1b185a8/0x1b185ec.
        public ISystemConfigurationAssetCollection Config => ActiveConfig;
        public string AssetLocation() { return DefaultLocation; }
        public string GetFileName() { return FileName; }
        public ActiveRuntimeConfiguration() { }
    }
}
