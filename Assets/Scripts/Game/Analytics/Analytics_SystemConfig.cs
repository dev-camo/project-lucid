using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000024. The original configuration and endpoint remain preserved.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "AnalyticsSystemConfig", menuName = "Hardlight/Analytics/Project Config", order = 0)]
    public class Analytics_SystemConfig : SystemConfigurationAsset
    {
        [SerializeField] public string m_gameID = "sdt-dev";
        [SerializeField] public string m_environment = "dev";
        [SerializeField] public string m_serverBaseURL = "https://analytics-receiver.hardlightdev.com/receiver";
        [SerializeField] public int m_maxQueueLength = 1;
        [SerializeField] public int m_maxSendAttempts = 3;
        [SerializeField] public bool m_persistentEventIndex = true;

        // Original 0x060000bc..c1, in original property order.
        public string GameID => m_gameID;
        public string Environment => m_environment;
        public string ServerBaseURL => m_serverBaseURL;
        public int MaxQueueLength => m_maxQueueLength;
        public int MaxSendAttempts => m_maxSendAttempts;
        public bool PersistentEventIndex => m_persistentEventIndex;

        // Original 0x060000c2 is empty, including no base.Validate call.
        public override void Validate() { }

        // Original 0x060000c3 applies the field initializers before the genuine base constructor.
        public Analytics_SystemConfig() { }
    }
}
