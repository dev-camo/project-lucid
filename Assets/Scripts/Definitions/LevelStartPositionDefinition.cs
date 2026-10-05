using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "LevelStartPositionDefinition", menuName = "HardlightProject/DefinitionData/Definitions/LevelStartPositionDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LevelStartPositionDefinition : ScriptableObjectWithGuid
    {
        // Game.Runtime.dll 0x06001d3f: the fieldless original class tailcalls
        // the actual ScriptableObjectWithGuid constructor.
        public LevelStartPositionDefinition() { }
    }
}
