using System.Collections.Generic;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "GameTipsDefinition", menuName = "HardlightProject/DefinitionData/Definitions/GameTipsDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameTipsDefinition : ScriptableObject
    {
        [SerializeField]
        [HashEnum(null)]
        private List<Strings> m_tipIds;

        // Game.Runtime 0x06001d08, native 0x5241bc: return the same nullable list.
        public List<Strings> TipIds => m_tipIds;
        // Game.Runtime 0x06001d09, native 0x5241c4: only the genuine ScriptableObject base constructor.
        public GameTipsDefinition() { }
    }
}
