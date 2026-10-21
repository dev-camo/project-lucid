using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008d4; bodies translated from the 1.10.1 x86-64 binary.
    // The loading screen: the level's loading image (or the mission's override) and the mission's tips.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerLoading : UIContainer
    {
        [SerializeField] private Sprite m_defaultImage;
        [SerializeField] private UIContainerGameTips m_uiContainerGameTips;
        public readonly Bindable<Sprite> Image = new Bindable<Sprite>();
        private GameplayLevelDefinition m_levelDefinition;
        private MissionDefinition m_missionDefinition;

        // 0x060032f5 (0x63B120)
        private void OnDisable()
        {
            if (m_levelDefinition != null)
                m_levelDefinition.UnloadLevelImage();
            if (m_missionDefinition != null)
                m_missionDefinition.UnloadImageOverride();
        }

        // 0x060032f6 (0x63B170): without a level the default image is shown and the tips are left alone. A mission with a
        // loading image override takes the image from the override; otherwise the level image is loaded. The native body
        // calls the private ShuffleTips directly (0x636080); SetTipOverrides, its public one-call wrapper, is taken to be inlined.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerLoadingParameters data = parameters.GetAs<UIContainerLoadingParameters>();
            if (data == null || data.LevelDefinition == null)
            {
                Image.Value = m_defaultImage;
                return;
            }
            m_levelDefinition = data.LevelDefinition;
            MissionDefinition missionDefinition = data.MissionDefinition;
            if (missionDefinition != null && missionDefinition.TryGetLoadingImageOverride(out Sprite loadingImageOverride))
            {
                m_missionDefinition = missionDefinition;
                Image.Value = loadingImageOverride;
            }
            else
            {
                data.LevelDefinition.LoadLevelImage(image => Image.Value = image);
            }
            if (m_uiContainerGameTips == null || missionDefinition == null || !missionDefinition.TryGetTips(out IReadOnlyList<Strings> tips))
                return;
            m_uiContainerGameTips.SetTipOverrides(tips);
        }

        // 0x060032f7 (0x63B340)
        public UIContainerLoading() { }
    }
}
