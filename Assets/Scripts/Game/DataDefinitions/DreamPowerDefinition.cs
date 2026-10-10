using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "DreamPowerDefinition", menuName = "HardlightProject/DefinitionData/Definitions/DreamPowerDefinition")]
    public class DreamPowerDefinition : ScriptableObjectWithGuid
    {
        [SerializeField] private List<AbilityDefinition> m_abilityDefinitions;
        [SerializeField] private List<AbilityDefinition> m_abilityOverrides;
        [SerializeField] private int m_purchaseCost;
        [SerializeField] private AssetReferenceAtlasedSprite m_atlasedSprite;
        [SerializeField] private DreamPowerStoreBandType m_band;
        [SerializeField, HashEnum(null)] private Strings m_name;
        [SerializeField, HashEnum(null)] private Strings m_description;
        [NonSerialized] private ManagedAddressableAsset<Sprite> m_iconAsset;
        private const string DreamPowerNameDelimiter = "_";

        public List<AbilityDefinition> AbilityDefinitions => m_abilityDefinitions;
        public List<AbilityDefinition> AbilityOverrides => m_abilityOverrides;
        public int PurchaseCost => m_purchaseCost;
        public DreamPowerStoreBandType Band => m_band;
        public Strings Name => m_name;
        public Strings Description => m_description;
        public ManagedAddressableAsset<Sprite> IconAsset => m_iconAsset;

        // Original 06001c56 captures the atlas before constructing and publishing
        // a fresh wrapper. The inherited lifecycle entry is not called here.
        protected void OnEnable()
        {
            AssetReferenceAtlasedSprite atlas = m_atlasedSprite;
            ManagedAddressableAsset<Sprite> icon = new ManagedAddressableAsset<Sprite>(atlas);
            m_iconAsset = icon;
        }

        // Original 06001c57 reads Object.name twice: the first string supplies
        // Substring and its length; the second supplies the ordinal first index.
        public string GetStrippedDefinitionName()
        {
            string definitionName = name;
            int start = unchecked(name.IndexOf(DreamPowerNameDelimiter, StringComparison.Ordinal) + 1);
            return definitionName.Substring(start, unchecked(definitionName.Length - start));
        }

        public DreamPowerDefinition() { }
    }
}
