using Hardlight;
using HardlightProject;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only UserEditableUIComponentController that exposes the protected lifecycle and edit entry
    // points and the protected state of the base class, leaving every body as shipped.
    public sealed class UserEditableUIComponentControllerProbe : UserEditableUIComponentController
    {
        public bool EditMode => m_editMode;
        public bool Flipped => m_isFlipped;
        public SaveDataSettings Settings => m_saveDataSettings;

        public void RunOnSaveManagerValid(SaveManager saveManager) => OnSaveManagerValid(saveManager);
        public void RunDoInitialise() => DoInitialise();
        public void RunDoUpdate() => DoUpdate();
        public void RunExitEditMode() => ExitEditMode();

        public void RunEditStart() { UIModernEvent none = null; OnEditStart(in none); }
        public void RunEditCancel() { UIModernEvent none = null; OnEditCancel(in none); }
        public void RunEditFlip() { UIModernEvent none = null; OnEditFlip(in none); }
        public void RunEditReset() { UIModernEvent none = null; OnEditReset(in none); }
    }
}
