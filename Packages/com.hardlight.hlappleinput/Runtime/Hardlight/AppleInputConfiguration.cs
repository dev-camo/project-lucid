using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000011. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "AppleInputConfiguration", menuName = "Hardlight/HLAppleInput/AppleInputConfiguration", order = 0)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class AppleInputConfiguration : Hardlight.SystemConfigurationAsset
    {
        [UnityEngine.SerializeField]
        private System.String m_frameworkGUID;
        [UnityEngine.SerializeField]
        private System.String[] m_supportedControllers = new string[] { "ExtendedGamepad" };

        // Original 0x0600002f; complete ARM64 and x86-64 bodies retained.
        public System.String FrameworkGUID
        {
            get { return m_frameworkGUID; }
        }

        // Original 0x06000030; complete ARM64 and x86-64 bodies retained.
        public System.Collections.Generic.IEnumerable<System.String> SupportedControllers
        {
            get { return m_supportedControllers; }
        }

        // Original 0x06000031; complete ARM64 and x86-64 bodies retained.
        public override void Validate()
        {
        }

        // Original 0x06000032; complete ARM64 and x86-64 bodies retained.
        public AppleInputConfiguration()
        {
        }

    }
}
