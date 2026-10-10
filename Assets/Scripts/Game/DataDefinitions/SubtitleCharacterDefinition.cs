using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "SubtitleCharacterDefinition", menuName = "HardlightProject/DefinitionData/Definitions/SubtitleCharacterDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SubtitleCharacterDefinition : ScriptableObject
    {
        [SerializeField]
        private string m_characterNameInStringID;
        [SerializeField]
        [HashEnum(null)]
        private Strings m_characterLocalisedStringID;
        [SerializeField]
        private Color m_characterColour;
        [SerializeField]
        private GameplayLevelDefinition m_requiredLevelComplete;

        // Game.Runtime 0x06001f4f/0x06001f50/0x06001f51/0x06001f52.
        public string CharacterName => m_characterNameInStringID;
        public string LocalisedName => StringTable.GetString(m_characterLocalisedStringID);
        public Color CharacterColour => m_characterColour;
        public GameplayLevelDefinition RequiredLevelComplete => m_requiredLevelComplete;
        // Game.Runtime 0x06001f53: genuine ScriptableObject base only, no field defaults.
        public SubtitleCharacterDefinition() { }
    }
}
