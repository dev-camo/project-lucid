using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008c5; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerLevelDefinition : UIContainer
    {
        public readonly Bindable<Sprite> LoadingImage = new Bindable<Sprite>();
        private GameplayLevelDefinition m_levelDefinition;
        private MissionDefinition m_missionDefinition;
        private Sprite m_levelSprite;

        // 060032a8 (1.10.1 RVA 0x636760): a new level releases the old level's image first;
        // the same level is left alone.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerLevelViewParameters levelViewParameters = parameters.GetAs<UIContainerLevelViewParameters>();
            if (m_levelDefinition == levelViewParameters.LevelDefinition)
                return;
            if (m_levelDefinition != null)
                m_levelDefinition.UnloadLevelImage();
            m_levelDefinition = levelViewParameters.LevelDefinition;
            m_levelDefinition.LoadLevelImage(image =>
            {
                m_levelSprite = image;
                LoadingImage.Value = image;
            });
        }

        // 060032a9 (1.10.1 RVA 0x636860): a mission's loading image override replaces the level
        // image; the previous mission's override is released.
        public void SetMissionDefinition(MissionDefinition missionDefinition)
        {
            Sprite loadingImageOverride;
            LoadingImage.Value = missionDefinition.TryGetLoadingImageOverride(out loadingImageOverride) ? loadingImageOverride : m_levelSprite;
            if (m_missionDefinition != null)
                m_missionDefinition.UnloadImageOverride();
            m_missionDefinition = missionDefinition;
        }

        // 060032aa (1.10.1 RVA 0x6368F0)
        private void OnDestroy()
        {
            if (m_missionDefinition != null)
                m_missionDefinition.UnloadImageOverride();
            if (m_levelDefinition == null)
                return;
            m_levelDefinition.UnloadLevelImage();
        }

        // 060032ab (1.10.1 RVA 0x636940)
        public UIContainerLevelDefinition()
        {
        }
    }
}
