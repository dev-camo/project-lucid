using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "HardlightProject/DefinitionData/Definitions/LevelDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LevelDefinition : ScriptableObjectWithGuid, ILevelDefinition
    {
        [SerializeField] private string m_sceneName;

        // Game.Runtime 0x0600185e; ARM64 0x774760 reads original field at0x20.
        public string GetName() => m_sceneName;

        // 0x0600185f; ARM64 0x774768 is RET in the supplied player. Retain the
        // runtime contract: authoring updates are not implemented by this body.
        public void SetData(string sceneName) { }

        // 0x06001860; ARM64 0x77476c tailcalls the original GUID base constructor.
        public LevelDefinition() { }
    }
}
