using System.Globalization;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200086f; bodies translated from the 1.10.1 x86-64 binary.
    // The blue coin price of a dream power; shown only while the power can still be bought.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerDreamPowerStoreBlueCoinCost : UIContainer
    {
        public readonly Bindable<bool> ShowCost = new Bindable<bool>();
        public readonly Bindable<string> Cost = new Bindable<string>();

        // 0x060030d3 (0x6172C0): the cost is shown for every state except the locked ones, Bought and
        // Equipped; the native folds the inequalities into an unsigned compare, so negative values show it too.
        // The price is formatted with the invariant culture.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerDreamPowerStoreBlueCoinCostParameters data = parameters.GetAs<UIContainerDreamPowerStoreBlueCoinCostParameters>();
            UIDreamPowerStoreItemState itemState = data.ItemState;
            ShowCost.Value = itemState != UIDreamPowerStoreItemState.LockedByProgression
                && itemState != UIDreamPowerStoreItemState.LockedByArchetype
                && itemState != UIDreamPowerStoreItemState.Bought
                && itemState != UIDreamPowerStoreItemState.Equipped;
            Cost.Value = data.DreamPowerDefinition.PurchaseCost.ToString(CultureInfo.InvariantCulture);
        }

        // 0x060030d4 (0x6173D0)
        public UIContainerDreamPowerStoreBlueCoinCost() { }
    }
}
