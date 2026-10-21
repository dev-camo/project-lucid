using HardlightProject;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only concrete UserEditableUIBase. It adds nothing to the edit behaviour: the two hooks only
    // count their calls (DoInitialise also records the edit state it sees, and can leave edit mode itself),
    // and the protected state is exposed for reading.
    public sealed class UserEditableUIBaseProbe : UserEditableUIBase
    {
        public int InitialiseCount;
        public int UpdateCount;
        public bool ExitEditModeInInitialise;
        public bool EditModeSeenByInitialise;
        public bool EditModeActiveSeenByInitialise;

        public bool Initialised => m_initialised;
        public bool EditMode => m_editMode;
        public bool Flipped => m_isFlipped;
        public SaveDataSettings Settings => m_saveDataSettings;

        protected override void DoInitialise()
        {
            InitialiseCount++;
            EditModeSeenByInitialise = m_editMode;
            EditModeActiveSeenByInitialise = EditModeActive.Value;
            if (ExitEditModeInInitialise) ExitEditMode();
        }

        protected override void DoUpdate() => UpdateCount++;

        public void RunOnSaveManagerValid(SaveManager saveManager) => OnSaveManagerValid(saveManager);
    }
}
