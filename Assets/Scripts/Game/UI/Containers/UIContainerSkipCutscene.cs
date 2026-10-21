using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000941; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerSkipCutscene : UIContainer
    {
        [SerializeField]
        private HoldToSkipInput m_holdToSkipInput;
        [HashEnum(null)]
        [SerializeField]
        private Strings m_theatreSkipString;
        private readonly SystemRef<UIManager> m_uiManagerRef = ProcessManager.GetSystemRef<UIManager>(null, true);
        private App m_app;
        private CutsceneManager m_cutsceneManager;

        // 06003525 (1.10.1 RVA 0x67EFD0): a theatre cutscene in progress gets its own skip label.
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>(null, true);
            m_cutsceneManager = ProcessManager.GetSystem<CutsceneManager>(null, true);
            if (m_app.Storage.HasValue<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre))
                m_holdToSkipInput.SetLabelString(m_theatreSkipString);
        }

        // 06003526 (1.10.1 RVA 0x67F100): flags a held skip, then closes this container.
        public void Action_OnFullSkip()
        {
            m_cutsceneManager.SkipHoldOccurred = true;
            if (m_uiManagerRef.TryGet(out UIManager uiManager))
                uiManager.Close(m_identifier);
        }

        // 06003527 (1.10.1 RVA 0x67F170)
        public void Action_OnSkipOnce()
        {
            m_cutsceneManager.SkipTapOccurred = true;
        }

        // 06003528 (1.10.1 RVA 0x67F190)
        public UIContainerSkipCutscene()
        {
        }
    }
}
