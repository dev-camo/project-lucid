using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game owner02000a0b, both declarations and its true static field.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class GameObjectExtensions_DreamTeam
    {
        private static CoreGameConfiguration s_coreGameConfiguration;

        // 06003999: preserve genuine Unity equality and configuration provider.
        private static CoreGameConfiguration CoreGameConfiguration
        {
            get
            {
                if (s_coreGameConfiguration == null)
                    s_coreGameConfiguration = SystemConfiguration.GetConfig<CoreGameConfiguration>();
                return s_coreGameConfiguration;
            }
        }

        // 0600399a: shipped ARM64 is MOV w0,#1;RET, x86 is MOV al,1;RET.
        // Both parameters remain original even though this release returns true.
        public static bool CanValidate(this GameObject gameObject, bool allowIsPlaying = false)
        {
            return true;
        }
    }
}
