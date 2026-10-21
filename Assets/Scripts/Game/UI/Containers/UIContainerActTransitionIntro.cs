using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000877; bodies translated from the 1.10.1 x86-64 binary.
    // The act transition intro card: the current level's act title and subtitle plus its zone icon.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerActTransitionIntro : UIContainerWithCloseEvent
    {
        public readonly Bindable<AssetReferenceT<Sprite>> ZoneIconAsset = new Bindable<AssetReferenceT<Sprite>>();
        public readonly Bindable<Strings> LevelTitle = new Bindable<Strings>();
        public readonly Bindable<Strings> LevelSubtitle = new Bindable<Strings>();

        // 0x060030f8
        private void Start()
        {
            ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(OnLevelManager);
        }

        // 0x060030f9: the title and subtitle are the level's display and act names. The zone icon is
        // only published when the level's zone has an icon whose operation handle and key are valid.
        private void OnLevelManager(LevelManager levelManager)
        {
            if (!levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                return;
            LevelTitle.Value = level.LevelDefinition.DisplayName;
            LevelSubtitle.Value = level.LevelDefinition.ActName;
            if (!levelManager.GameLevels.TryGetZoneForLevel(level.LevelDefinition, out GameplayLevelCategory zone))
                return;
            if (zone.IconAsset.IsValid() && zone.IconAsset.RuntimeKeyIsValid())
                ZoneIconAsset.Value = zone.IconAsset;
        }

        // 0x060030fa: the close event first, then the container itself is closed.
        public override void Action_Close()
        {
            base.Action_Close();
            ProcessManager.GetSystem<UIManager>(null, true).Close(m_identifier);
        }

        // 0x060030fb
        public UIContainerActTransitionIntro() { }
    }
}
