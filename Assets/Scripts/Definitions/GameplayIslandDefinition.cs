using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "GameplayIslandDefinition", menuName = "HardlightProject/DefinitionData/Definitions/GameplayIslandDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameplayIslandDefinition : ScriptableObjectWithGuid
    {
        [HashEnum(typeof(Strings))]
        [SerializeField]
        private Strings m_localisedId = Strings.NONE;

        [SerializeField]
        private List<GameplayIslandDefinition> m_visibleIslands;

        // Game.Runtime 0x06001d02, native 0x524130: return the original nullable list, without copying.
        public List<GameplayIslandDefinition> VisibleIslands => m_visibleIslands;
        // Game.Runtime 0x06001d03, native 0x524138: return the original localisation key.
        public Strings LocalisedId => m_localisedId;
        // Game.Runtime 0x06001d04, native 0x524140: Strings.NONE assignment precedes the genuine GUID base constructor.
        public GameplayIslandDefinition() { }
    }
}
