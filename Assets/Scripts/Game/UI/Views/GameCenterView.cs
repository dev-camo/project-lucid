using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000964, all four methods and two fields. Exposes whether the
    // player is logged in to Game Center.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenterView : ViewBase
    {
        public readonly Bindable<bool> GameCenterAvailable = new Bindable<bool>();
        private GameCenter m_gameCenter;

        // 0x060035fd: the system may be absent (non-throwing lookup); availability then reads false.
        private void Awake()
        {
            m_gameCenter = ProcessManager.GetSystemSafe<GameCenter>(null, true);
            GameCenterAvailable.Value = m_gameCenter != null && m_gameCenter.IsLoggedIn();
        }

        // 0x060035fe: only refreshes while the system exists.
        private void Update()
        {
            if (m_gameCenter != null)
                GameCenterAvailable.Value = m_gameCenter.IsLoggedIn();
        }

        // 0x060035ff: authentic empty body.
        protected override void OnSetup(ViewParameters viewParams) { }

        // 0x06003600: field initialiser, then the ViewBase constructor.
        public GameCenterView() { }
    }
}
