using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200083a: platform flags exposed to UI bindings.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PlatformBindings : MonoBehaviour
    {
        [SerializeField] private bool m_debugEnablePlatforms;
        [ShowIf("m_debugEnablePlatforms")] [SerializeField] private bool m_debugIOS;
        [SerializeField] [ShowIf("m_debugEnablePlatforms")] private bool m_debugTouchDevice;
        [ShowIf("m_debugEnablePlatforms")] [SerializeField] private bool m_debugTvOS;
        [ShowIf("m_debugEnablePlatforms")] [SerializeField] private bool m_debugMacOs;
        public readonly Bindable<bool> IsIOS = new Bindable<bool>();
        public readonly Bindable<bool> IsTouchDevice = new Bindable<bool>();
        public readonly Bindable<bool> IsTvOS = new Bindable<bool>();
        public readonly Bindable<bool> IsMacOS = new Bindable<bool>();

        // 06002fb5: this is the macOS player's body. It sets only IsMacOS and never reads
        // the debug overrides. Only the macOS binary is available, so Windows and Linux
        // players built from this source also report IsMacOS until their bodies are known.
        private void Awake()
        {
            IsMacOS.Value = true;
        }
    }
}
