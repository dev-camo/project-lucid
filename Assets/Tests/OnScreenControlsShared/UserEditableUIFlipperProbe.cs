using Hardlight;
using HardlightProject;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only UserEditableUIFlipper that exposes the protected edit entry points and the protected
    // state of the base class, leaving every body as shipped.
    public sealed class UserEditableUIFlipperProbe : UserEditableUIFlipper
    {
        public bool Flipped => m_isFlipped;

        public void RunOnSaveManagerValid(SaveManager saveManager) => OnSaveManagerValid(saveManager);
        public void RunDoInitialise() => DoInitialise();

        public void RunEditFlip() { UIModernEvent none = null; OnEditFlip(in none); }
        public void RunEditReset() { UIModernEvent none = null; OnEditReset(in none); }
        public void RunEditConfirm() { UIModernEvent none = null; OnEditConfirm(in none); }
    }
}
