using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000992; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerValueProgressSetter : UIContainerValueSetter
    {
        // 0600374c (1.10.1 RVA 0x696F60)
        public void SetProgress(float progress)
        {
            UIContainer?.SetProgress(progress);
        }

        // 0600374d (1.10.1 RVA 0x697060)
        public UIContainerValueProgressSetter()
        {
        }
    }
}
