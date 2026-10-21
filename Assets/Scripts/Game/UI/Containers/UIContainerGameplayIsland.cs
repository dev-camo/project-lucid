using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020008c2; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerGameplayIsland : UIContainer
    {
        public readonly Bindable<bool> IslandNameValid = new Bindable<bool>();
        public readonly Bindable<Strings> IslandName = new Bindable<Strings>();
        public readonly Bindable<float> WeightedProximity = new Bindable<float>();
        public readonly Bindable<float> DistanceProximity = new Bindable<float>();
        public readonly Bindable<bool> CollectionInProgress = new Bindable<bool>();

        // 06003296 (1.10.1 RVA 0x635B00): the lambda is compiled as <Awake>b__5_0.
        private void Awake()
        {
            ProcessManager.GetSystemRef<GameplayIslandManager>(null, true).InvokeOnValid(islandManager =>
            {
                islandManager.OnIslandDefinitionChanged += OnGameplayIslandChanged;
                islandManager.ReenterActiveIsland();
            });
        }

        // 06003297 (1.10.1 RVA 0x635BE0)
        private void OnDestroy()
        {
            SystemRef<GameplayIslandManager> islandManagerRef = ProcessManager.GetSystemRef<GameplayIslandManager>(null, true);
            if (islandManagerRef.IsValid())
                islandManagerRef.Get().OnIslandDefinitionChanged -= OnGameplayIslandChanged;
        }

        // 06003298 (1.10.1 RVA 0x635CF0): shows the first active island's name, or none.
        private void OnGameplayIslandChanged(IReadOnlyList<GameplayIslandDefinition> islandDefinitions)
        {
            IslandName.Value = islandDefinitions.Count > 0 ? islandDefinitions[0].LocalisedId : Strings.NONE;
            IslandNameValid.Value = IslandName.Value != Strings.NONE;
        }

        // 06003299 (1.10.1 RVA 0x635E60)
        public UIContainerGameplayIsland()
        {
        }
    }
}
