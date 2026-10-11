// Preserved Sonic Dream Team 1.10.1, Game.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// 0x06002374 0x55f44c, 0x587b20
// 0x06002375 0x55f454, 0x587b30
// 0x06002376 0x55f45c, 0x587b40
// 0x06002377 0x55f598, 0x587c60
// 0x06002378 0x55f8e8, 0x587f20
// 0x06002379 0x55f7c4, 0x587e40
// 0x0600237a 0x55fa6c, 0x588060
// 0x0600237b 0x55fb04, 0x5880e0
// 0x0600237c 0x55f9e0, 0x587ff0
// 0x0600237d 0x55f6ec, 0x587d90
// 0x0600237e 0x55f878, 0x587ec0
// 0x0600237f 0x55fb74, 0x588140
// 0x06002380 0x55fbe4, 0x5881a0
// 0x06002381 0x55fc54, 0x588200
// 0x06002382 0x55fcc4, 0x588260
// 0x06002383 0x55fd34, 0x5882c0
// 0x06002384 0x55fda4, 0x588320
// 0x06002385 0x55fe14, 0x588380
// 0x06002386 0x55fe84, 0x5883e0
// 0x06002387 0x55fef4, 0x588440
using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime owner 0x020006a0. All twenty original methods are
    // preserved; platform calls and login policy belong to the supplied game.
    // This source is preservation evidence, not an offline platform adapter.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class GameCenterAccessPointManager : ISystem
    {
        // Original get/set 0x06002374/0x06002375; backing field 0x040017a2.
        public bool IsActive { get; set; }

        // Original readonly field 0x040017a3. Both architectures construct
        // this reference before the Object constructor and action callbacks.
        private readonly SystemRef<GameCenter> m_gameCenterRef =
            ProcessManager.GetSystemRef<GameCenter>(null, true);
        private bool m_inputDisabled;

        // 0x06002376: Update subscription precedes ordinary Shutdown(2).
        // This owner does not register itself or request initialisation here.
        public GameCenterAccessPointManager()
        {
            ProcessManager.SubscribeToAction(this, SystemAction.Update, new Action<object>(OnUpdate));
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown, new Action<object>(Shutdown));
        }

        // 0x06002377. Short-circuit IsActive before availability. Inactive
        // and unavailable paths enable input without querying presentation.
        private void OnUpdate(object context = null)
        {
            if (IsActive && IsAccessPointAvailable())
                ToggleInput(!IsPresentingGameCenter());
            else
                ToggleInput(true);
        }

        // 0x06002378. An input-provider failure prevents the hide call.
        // No field reset, unsubscribe, or self-unregister is added.
        private void Shutdown(object context = null)
        {
            ToggleInput(true);
            HideAccessPoint();
        }

        // 0x06002379. The original field name is surprising: it stores the
        // argument on. Store before the provider call; repeated values do
        // nothing. Key bits 0xf13d5bcc are original DisableAllInput.
        private void ToggleInput(bool on)
        {
            if (m_inputDisabled == on) return;
            m_inputDisabled = on;
            if (on)
                ControlMapping.RemoveExclusiveInput(GameInput.DisableAllInput);
            else
                ControlMapping.AddExclusiveInput(GameInput.DisableAllInput);
        }

        // 0x0600237a. Availability guards this operation; IsActive does not.
        public void ShowAccessPoint(GameCenterAccessPoint.AccessPointLocation accessPointLocation, bool showHighlights)
        {
            if (IsAccessPointAvailable())
                GameCenterAccessPoint.ShowAccessPoint(accessPointLocation, showHighlights);
        }

        // 0x0600237b. Original query has no availability/login guard.
        public bool IsAccessPointShown() => GameCenterAccessPoint.IsAccessPointShown();

        // 0x0600237c. Preserve the original availability guard on hiding.
        public void HideAccessPoint()
        {
            if (IsAccessPointAvailable())
                GameCenterAccessPoint.HideAccessPoint();
        }

        // 0x0600237d. Keep IsValid before Get, then original login query,
        // then platform availability. The typed reference is fetched anew.
        public bool IsAccessPointAvailable() => m_gameCenterRef.IsValid()
            && m_gameCenterRef.Get().IsLoggedIn()
            && GameCenterAccessPoint.IsAccessPointAvailable();

        // 0x0600237e/0x0600237f. Neither method checks IsActive or login.
        public bool IsPresentingGameCenter() => GameCenterAccessPoint.IsPresentingGameCenter();
        public void ShowGameCenter() => GameCenterAccessPoint.ShowGameCenter();

        // 0x06002380..0x06002387. All original geometry wrappers forward
        // their native Single results; no conversion or coordinate correction.
        // The Y-screen supplier really omits "Origin" in its original name.
        public float GetOriginX() => GameCenterAccessPoint.GetAccessPointOriginX();
        public float GetOriginY() => GameCenterAccessPoint.GetAccessPointOriginY();
        public float GetWidth() => GameCenterAccessPoint.GetAccessPointSizeWidth();
        public float GetHeight() => GameCenterAccessPoint.GetAccessPointSizeHeight();
        public float GetOriginScreenCoordinateX() => GameCenterAccessPoint.GetAccessPointOriginScreenCoordinateX();
        public float GetOriginScreenCoordinateY() => GameCenterAccessPoint.GetAccessPointScreenCoordinateY();
        public float GetScreenCoordinateWidth() => GameCenterAccessPoint.GetAccessPointSizeScreenCoordinateWidth();
        public float GetScreenCoordinateHeight() => GameCenterAccessPoint.GetAccessPointSizeScreenCoordinateHeight();
    }
}
