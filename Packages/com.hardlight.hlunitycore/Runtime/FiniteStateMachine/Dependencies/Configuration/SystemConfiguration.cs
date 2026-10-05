using UnityEngine;

namespace Hardlight
{
    // HLUnityCore.Runtime 0x02000240. Configs retain the original runtime Resource and typed stack boundaries.
    public class SystemConfiguration : ISystem
    {
        // 0x06000e67/0x06000e6c; ARM64 0x1b1a6c0/0x1b1a960.
        public StackableData StackableData { get; } = new StackableData();
        public SystemConfiguration() { }

        // 0x06000e68; ARM64 0x1b1a6c8. Missing/destroyed root assets are ignored; null entries diagnose in original order.
        private void InitialiseRuntimeConfiguration()
        {
            ActiveRuntimeConfiguration active = Resources.Load<ActiveRuntimeConfiguration>("ActiveRuntimeConfiguration");
            if (active == null || active.ActiveConfig == null) return;
            var assets = active.ActiveConfig.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                SystemConfigurationAsset asset = assets[i];
                if (asset == null) HLOutput.LogError("Null config asset in ActiveRuntimeConfiguration, please update or remove this entry.");
                else AddConfig(asset);
            }
        }

        // 0x06000e69; shared reference ARM64 0x9bc070. The newly created asset reuses native out-slot sp+8 before AddConfig.
        private T RetrieveFromConfigurableSettings<T>() where T : SystemConfigurationAsset
        {
            int crc = HLCRC32.GenerateInt(typeof(T).Name);
            if (!StackableData.TryGet(crc, out SystemConfigurationAsset configuration))
            {
                configuration = ScriptableObject.CreateInstance<T>();
                AddConfig(configuration, crc);
            }
            return (T)configuration;
        }

        // 0x06000e6a; shared reference ARM64 0x9bbcc4. GetType().Name is read even when the caller supplies a CRC.
        public static StackableDataHandle AddConfig<T>(T configuration, int? crc = null) where T : SystemConfigurationAsset
        {
            if (ProcessManager.IsSystemNull<SystemConfiguration>())
                ProcessManager.RegisterSystem<SystemConfiguration>().Get<SystemConfiguration>().InitialiseRuntimeConfiguration();
            SystemConfiguration system = ProcessManager.GetSystem<SystemConfiguration>();
            string name = configuration.GetType().Name;
            if (!crc.HasValue) crc = HLCRC32.GenerateInt(name);
            return system.StackableData.AddOverride(crc.Value, configuration);
        }

        // 0x06000e6b; shared reference ARM64 0x9bbf10.
        public static T GetConfig<T>() where T : SystemConfigurationAsset
        {
            if (ProcessManager.IsSystemNull<SystemConfiguration>())
                ProcessManager.RegisterSystem<SystemConfiguration>().Get<SystemConfiguration>().InitialiseRuntimeConfiguration();
            return ProcessManager.GetSystem<SystemConfiguration>().RetrieveFromConfigurableSettings<T>();
        }
    }
}
