using System;
using System.Collections.Generic;
using Hardlight.Enums;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "SubtitleCharacterDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/SubtitleCharacterDefinitionGroup")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SubtitleCharacterDefinitionGroup : DefinitionDataType<string, SubtitleCharacterDefinition>
    {
        [SerializeField]
        private bool m_useColour;
        [SerializeField]
        private bool m_useNewLine;
        [SerializeField]
        private bool m_useBrackets;
        [SerializeField]
        private bool m_useDash;
        [SerializeField]
        private bool m_useBold;
        [SerializeField]
        private string m_unknownCharacterName = "???";
        [NonSerialized]
        private Dictionary<string, SubtitleCharacterDefinition> m_definitionCache;
        [NonSerialized]
        private List<string> m_characterNames = new List<string>();

        // Game.Runtime 0x06001f54/0x06001f55: original field key; null comparer.
        protected override string GetElementKey(SubtitleCharacterDefinition data) => data.CharacterName;
        protected override IEqualityComparer<string> GetKeyComparer() => null;

        // Game.Runtime 0x06001f56: publish newly built dictionary before Keys enumeration.
        // The retained names list is appended, including repeats across initializations.
        public void InitialiseCache()
        {
            m_definitionCache = GetData();
            foreach (string key in m_definitionCache.Keys)
                m_characterNames.Add(key);
        }

        // Game.Runtime 0x06001f57: search current dictionary Keys, rather than the names list.
        // The first substring match wins; an empty/whitespace first match returns null.
        private SubtitleCharacterDefinition Get(Strings localisedStringID)
        {
            if (m_definitionCache == null)
                m_definitionCache = GetData();
            string keyName = localisedStringID.GetString();
            string matchedCharacterName = "";
            foreach (string characterName in m_definitionCache.Keys)
            {
                if (keyName.Contains(characterName))
                {
                    matchedCharacterName = characterName;
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(matchedCharacterName)) return null;
            m_definitionCache.TryGetValue(matchedCharacterName, out SubtitleCharacterDefinition definition);
            return definition;
        }

        // Game.Runtime 0x06001f58: string existence/get precede the definition lookup.
        // The level condition uses its original GUID inequality and requirements gate.
        public string GetFormattedString(Strings localisedStringID)
        {
            string text = StringTable.StringExists(localisedStringID)
                ? StringTable.GetString(localisedStringID) : string.Empty;
            SubtitleCharacterDefinition definition = Get(localisedStringID);
            if (definition == null) return text;
            string characterName = definition.LocalisedName;
            if (definition.RequiredLevelComplete != null && !definition.RequiredLevelComplete.MeetsAllRequirements())
                characterName = m_unknownCharacterName;
            if (m_useBold) characterName = "<b>" + characterName + "</b>";
            if (m_useColour)
            {
                string colour = ColorUtility.ToHtmlStringRGBA(definition.CharacterColour);
                characterName = string.Concat(new[] { "<color=#", colour, ">", characterName, "</color>" });
            }
            if (m_useBrackets) characterName = "[" + characterName + "]";
            if (m_useDash) characterName += " -";
            if (m_useNewLine) text = "\n" + text;
            return characterName + " " + text;
        }

        // Game.Runtime 0x06001f59: unknown literal then list allocation before base constructor.
        public SubtitleCharacterDefinitionGroup() { }
    }
}
