using Hardlight;
using HardlightProject;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only UserEditableUIFlipper that exposes the protected edit entry points and the protected
    // state of the base class, leaving every body as shipped.
    public sealed class UserEditableUIFlipperProbe : UserEditableUIFlipper
    {
        public bool Flipped => m_isFlipped;
        public bool? FlippedSettingAtExit;
        public bool? SettingsPublishedAtExit;

        public void RunOnSaveManagerValid(SaveManager saveManager) => OnSaveManagerValid(saveManager);
        public void RunDoInitialise() => DoInitialise();

        public void RunEditFlip() { UIModernEvent none = null; OnEditFlip(in none); }
        public void RunEditReset() { UIModernEvent none = null; OnEditReset(in none); }
        // The base OnEditConfirm calls ExitEditMode after publishing the settings; this records what it sees then.
        protected override void ExitEditMode()
        {
            FlippedSettingAtExit = m_saveDataSettings.OnScreenControlsFlipped;
            SettingsPublishedAtExit = ProcessManager.GetSystem<App>().Storage.TryGetValue(AppFSMKeys.GameSettings, out SaveDataSettings _);
            base.ExitEditMode();
        }

        public void RunEditConfirm() { UIModernEvent none = null; OnEditConfirm(in none); }
    }
}
