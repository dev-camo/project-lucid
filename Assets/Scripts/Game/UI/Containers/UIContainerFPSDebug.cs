using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020008ba; bodies 0x631730..0x6317DF (1.10.1 x86-64).
    // Debug frame counter text, republished every frame.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerFPSDebug : UIContainer
    {
        public readonly Bindable<string> FPSCount = new Bindable<string>();

        // 06003259
        private void Update()
        {
            FPSCount.Value = VisualQualityManager_SDT.FramesPerSecond;
        }

        // 0600325a
        public UIContainerFPSDebug() { }
    }
}
