using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000942; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerSkipMissionIntro : UIContainer
    {
        private LevelManager m_levelManager;

        // 0x06003529 (0x67F210)
        private void Start()
        {
            m_levelManager = ProcessManager.GetSystem<LevelManager>(null, true);
        }

        // 0x0600352a (0x67F290): skips the current level's intro sequences, if a level is current.
        public void Action_OnFullSkip()
        {
            if (m_levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                level.Data.SkipIntroSequence();
        }

        // 0x0600352b
        public UIContainerSkipMissionIntro() { }
    }
}
