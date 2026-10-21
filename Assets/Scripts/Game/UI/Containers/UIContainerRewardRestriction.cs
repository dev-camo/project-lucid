using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200091e; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerRewardRestriction : UIContainer
    {
        public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();
        public readonly Bindable<string> Text = new Bindable<string>();
        public readonly Bindable<int> Count = new Bindable<int>();
        [SerializeField]
        [HashEnum(null)]
        private Strings m_remainingRewardCounterText;
        private CollectableDefinition m_collectableDefinition;

        // 06003495 (1.10.1 RVA 0x678B30)
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerRewardRestrictionParameters restrictionParameters = parameters.GetAs<UIContainerRewardRestrictionParameters>();
            m_collectableDefinition = ProcessManager.GetSystem<DataManager>(null, true).CollectableDefinitions[restrictionParameters.Type];
            Icon.Value = m_collectableDefinition.LoadIcon();
            Text.Value = StringTable.GetString(m_remainingRewardCounterText);
            Count.Value = restrictionParameters.Count;
        }

        // 06003496 (1.10.1 RVA 0x678C90)
        private void OnDisable()
        {
            if (m_collectableDefinition == null)
                return;
            m_collectableDefinition.UnloadIcon();
        }

        // 06003497 (1.10.1 RVA 0x678CD0)
        public UIContainerRewardRestriction()
        {
        }
    }
}
