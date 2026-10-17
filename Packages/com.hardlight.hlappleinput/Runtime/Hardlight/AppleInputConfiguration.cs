using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000011. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
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

        public string FrameworkGUID
        {
            get { return m_frameworkGUID; }
        }

        public IEnumerable<string> SupportedControllers
        {
            get { return m_supportedControllers; }
        }

        public override void Validate()
        {
        }

        public AppleInputConfiguration()
        {
        }
    }
}
