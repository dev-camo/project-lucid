using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000011. Every method body follows the complete ARM64 and x86-64 originals.
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "AppleInputConfiguration",
        menuName = "Hardlight/HLAppleInput/AppleInputConfiguration", order = 0)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AppleInputConfiguration : SystemConfigurationAsset
    {
        [SerializeField]
        private string m_frameworkGUID;
        [SerializeField]
        private string[] m_supportedControllers = new string[] { "ExtendedGamepad" };

        // Original 0x0600002f
        public string FrameworkGUID
        {
            get { return m_frameworkGUID; }
        }

        // Original 0x06000030
        public IEnumerable<string> SupportedControllers
        {
            get { return m_supportedControllers; }
        }

        // Original 0x06000031
        public override void Validate()
        {
        }

        // Original 0x06000032
        public AppleInputConfiguration()
        {
        }
    }
}
