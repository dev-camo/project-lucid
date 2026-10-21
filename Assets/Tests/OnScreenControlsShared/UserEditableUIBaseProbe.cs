using HardlightProject;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only concrete UserEditableUIBase. It adds nothing to the edit behaviour: the two hooks only
    // count their calls, and the protected state is exposed for reading.
    public sealed class UserEditableUIBaseProbe : UserEditableUIBase
    {
        public int InitialiseCount;
        public int UpdateCount;

        public bool Initialised => m_initialised;
        public bool EditMode => m_editMode;
        public bool Flipped => m_isFlipped;
        public SaveDataSettings Settings => m_saveDataSettings;

        protected override void DoInitialise() => InitialiseCount++;

        protected override void DoUpdate() => UpdateCount++;

        public void RunOnSaveManagerValid(SaveManager saveManager) => OnSaveManagerValid(saveManager);
    }
}
