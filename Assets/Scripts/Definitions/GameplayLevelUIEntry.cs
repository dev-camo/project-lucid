using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class GameplayLevelUIEntry : ScriptableObjectWithGuid, ILevelDefinition
    {
        // Game.Runtime original abstract contracts 0x06001856/0x06001857.
        public abstract string GetName();
        public abstract void SetData(string sceneName);

        // Game.Runtime 0x06001858: tail-calls the original GUID base constructor.
        protected GameplayLevelUIEntry() { }
    }
}
