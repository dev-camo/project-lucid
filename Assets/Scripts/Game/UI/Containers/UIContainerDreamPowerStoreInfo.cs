using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000871; bodies translated from the 1.10.1 x86-64 binary.
    // The dream power store information popup.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerDreamPowerStoreInfo : UIContainer
    {
        [SerializeField] private Animation m_animation;
        [SerializeField] private AnimationClip m_closeClip;
        [SerializeField] private UIModernEvent m_closeEvent;
        private App m_app;
        private SaveManager m_saveManager;

        // 0x060030d6 (0x617490): opening the popup marks the store as seen, saving only on the change.
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>();
            m_saveManager = ProcessManager.GetSystem<SaveManager>();
            SaveDataDreamPowerStore dreamPowerStore = m_saveManager.CurrentSave.DreamPowerStore;
            if (dreamPowerStore.IsNewState == DreamPowerStoreIsNewState.IsNew)
            {
                dreamPowerStore.IsNewState = DreamPowerStoreIsNewState.Seen;
                m_saveManager.RequestSave();
            }
        }

        // 0x060030d7 (0x617570)
        public void Action_Close()
        {
            m_animation.Play(m_closeClip.name);
        }

        // 0x060030d8 (0x6175A0)
        public void Action_AnimationCloseFinish()
        {
            m_app.AddUniqueUIModernEvent(m_closeEvent);
        }

        // 0x060030d9 (0x6175C0)
        public UIContainerDreamPowerStoreInfo() { }
    }
}
