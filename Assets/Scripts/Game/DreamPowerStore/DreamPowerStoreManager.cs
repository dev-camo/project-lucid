using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class DreamPowerStoreManager : MonoBehaviour, ISystem
    {
        [SerializeField] private DreamPowerStoreDefinition m_storeDefinition;
        [SerializeField] private RequirementGameplayLevelBase[] m_unlockRequirements;

        // Original 06002068/69; setter remains private.
        public int DebugExtraBlueCoins { get; private set; }
        private const string DebugMenuRoot = "Dream Power Store";
        private readonly SystemRef<ChallengeManager> m_challengeManagerRef =
            ProcessManager.GetSystemRef<ChallengeManager>();
        private readonly Dictionary<CharacterArchetype, HashSet<DreamPowerDefinition>> m_powersByArchetype =
            new Dictionary<CharacterArchetype, HashSet<DreamPowerDefinition>>();
        private readonly Dictionary<DreamPowerDefinition, HashSet<CharacterArchetype>> m_archetypesByPower =
            new Dictionary<DreamPowerDefinition, HashSet<CharacterArchetype>>();
        private DataManager m_dataManager;
        private SaveManager m_saveManager;

        // Original 0600206b/6c; both accessors are public.
        public DreamPowerDefinition LastSelectedDefinition { get; set; }
        // Original 0600206a.
        public DreamPowerStoreDefinition StoreDefinition => m_storeDefinition;

        // Original 0600206d/6e: wait for Data first, then Save. Only the second
        // completion publishes the store to ProcessManager, after collation.
        private void Awake() => ProcessManager.GetSystemRef<DataManager>().InvokeOnValid(OnDataManagerValid);
        private void OnDataManagerValid(DataManager dataManager)
        {
            m_dataManager = dataManager;
            ProcessManager.GetSystemRef<SaveManager>().InvokeOnValid(OnSaveManager);
        }
        private void OnSaveManager(SaveManager saveManager)
        {
            m_saveManager = saveManager;
            CollateDreamPowersByArchetype();
            ProcessManager.RegisterSystem(this);
        }

        // Original 06002070 does not remove pending startup callbacks.
        public void OnDestroy() => ProcessManager.UnregisterSystem(this);

        // Original 06002071 checks debug and authored requirements before the
        // null-conditional save chain. An already bought power also opens it.
        public bool IsAvailable()
        {
            if (IsDebugUnlockAll() || Requirements.AreMet(m_unlockRequirements))
                return true;
            List<SaveDataDreamPowerStoreItem> powers = m_saveManager?.CurrentSave?.DreamPowerStore?.DreamPowers;
            if (powers != null)
                foreach (SaveDataDreamPowerStoreItem power in powers)
                    if (power.Bought)
                        return true;
            return false;
        }

        // Original 06002072 creates the item first, then applies the debug
        // bought flag. It does not request a save itself.
        public SaveDataDreamPowerStoreItem GetDreamPowerStoreItemSaveData(DreamPowerDefinition definition)
        {
            SaveDataDreamPowerStoreItem item = m_saveManager.CurrentSave.DreamPowerStore.GetOrCreateDreamPowerData(definition.GetGUID());
            if (IsDebugUnlockAll())
                item.Bought = true;
            return item;
        }

        // Original 06002073 uses authored starting coins plus the debug amount.
        public int GetBlueCoinCount() => unchecked(m_storeDefinition.StartingBlueCoinCount + DebugExtraBlueCoins);

        // Original 06002074 trusts the caller's purchase checks. Mark bought
        // before updating spent currency; even a repeated call charges again.
        public void Purchase(DreamPowerDefinition definition)
        {
            SaveDataGame save = m_saveManager.CurrentSave;
            save.DreamPowerStore.GetOrCreateDreamPowerData(definition.GetGUID()).Bought = true;
            save.BlueCoins.Spent = unchecked(save.BlueCoins.Spent + definition.PurchaseCost);
            m_saveManager.RequestSave();
        }

        // Original 06002075 checks compatibility before creating the item, and
        // tests its persisted Bought field without applying the debug helper.
        public bool TryEquip(CharacterArchetype characterArchetype, int slotIndex, DreamPowerDefinition definition)
        {
            if (!ArchetypeAllowsPower(characterArchetype, definition))
                return false;
            if (!m_saveManager.CurrentSave.DreamPowerStore.GetOrCreateDreamPowerData(definition.GetGUID()).Bought)
                return false;
            m_saveManager.CurrentSave.GetOrCreateDreamPowerLoadout(characterArchetype).Set(slotIndex, definition.GetGUID());
            m_saveManager.RequestSave();
            return true;
        }

        // Original 06002076 requests a save even if Remove finds no slot.
        public void Unequip(CharacterArchetype characterArchetype, int slotIndex)
        {
            m_saveManager.CurrentSave.GetOrCreateDreamPowerLoadout(characterArchetype).Remove(slotIndex);
            m_saveManager.RequestSave();
        }

        // Original 06002077 treats None as unrestricted before reading maps.
        public bool ArchetypeAllowsPower(CharacterArchetype archetype, DreamPowerDefinition definition)
        {
            if (archetype == CharacterArchetype.None)
                return true;
            return m_powersByArchetype.TryGetValue(archetype, out HashSet<DreamPowerDefinition> powers) && powers.Contains(definition);
        }

        // Original 06002078 exposes the actual shared set only when fewer
        // archetypes can use the power than appear in the inverse map.
        public bool TryGetRequiredArchetypes(DreamPowerDefinition definition, out IReadOnlyCollection<CharacterArchetype> requiredArchetypes)
        {
            int count = m_powersByArchetype.Count;
            if (m_archetypesByPower.TryGetValue(definition, out HashSet<CharacterArchetype> archetypes) && archetypes.Count < count)
            {
                requiredArchetypes = archetypes;
                return true;
            }
            requiredArchetypes = null;
            return false;
        }

        // Original 06002079 falls back to None. The indexing method below has
        // a different native fallback, Band1 (serialized value 0x87891662).
        public DreamPowerStoreBandDefinition GetBandDefinition(DreamPowerDefinition dreamPowerDefinition)
        {
            if (m_dataManager.DreamPowerStoreBandDefinitions.TryGetValue(dreamPowerDefinition.Band, out DreamPowerStoreBandDefinition definition))
                return definition;
            return m_dataManager.DreamPowerStoreBandDefinitions[DreamPowerStoreBandType.None];
        }

        // Original 0600207a retains a required dictionary lookup on fallback.
        public int GetBandIndex(DreamPowerDefinition dreamPowerDefinition)
        {
            if (!m_dataManager.DreamPowerStoreBandDefinitions.TryGetValue(dreamPowerDefinition.Band, out DreamPowerStoreBandDefinition definition))
                definition = m_dataManager.DreamPowerStoreBandDefinitions[DreamPowerStoreBandType.Band1];
            return m_dataManager.GetGroup<DreamPowerStoreBandDefinitionGroup>().GetIndex(definition);
        }

        // Original 0600207b returns a fresh map, enumerates the saved readonly
        // interface and uses throwing definition lookup and Add, not fallbacks.
        public IReadOnlyDictionary<int, DreamPowerDefinition> GetPowersLoadoutAsDefinitions(CharacterArchetype characterArchetype)
        {
            var result = new Dictionary<int, DreamPowerDefinition>();
            foreach (var (index, guid) in m_saveManager.CurrentSave.GetOrCreateDreamPowerLoadout(characterArchetype).SlotsByIndex)
                result.Add(index, m_dataManager.DreamPowerDefinitions[guid]);
            return result;
        }

        // Original 0600207c: derive compatibility from all authored ability
        // tiers. Unity fake-null abilities are skipped only in this first pass.
        // HashSets are deliberately shared, and m_archetypesByPower is retained
        // between calls; cloning sets or clearing both maps changes behavior.
        private void CollateDreamPowersByArchetype()
        {
            var archetypesByAbility = new Dictionary<AbilityDefinition, HashSet<CharacterArchetype>>();
            foreach (var (_, character) in m_dataManager.Characters)
                foreach (AbilityTiersDefinition tiers in character.Traits.AbilityTierDefinitions)
                    foreach (AbilityDefinition ability in tiers.AbilityTiers)
                    {
                        if (ability == null)
                            continue;
                        if (archetypesByAbility.TryGetValue(ability, out HashSet<CharacterArchetype> archetypes))
                            archetypes.Add(character.Archetype);
                        else
                        {
                            archetypes = new HashSet<CharacterArchetype>();
                            archetypes.Add(character.Archetype);
                            archetypesByAbility[ability] = archetypes;
                        }
                    }

            m_powersByArchetype.Clear();
            foreach (var (_, power) in m_dataManager.DreamPowerDefinitions)
                foreach (AbilityDefinition ability in power.AbilityDefinitions)
                {
                    if (!archetypesByAbility.TryGetValue(ability, out HashSet<CharacterArchetype> archetypes))
                        continue;
                    if (m_archetypesByPower.TryGetValue(power, out HashSet<CharacterArchetype> existing))
                        existing.AddRange(archetypes);
                    else
                        m_archetypesByPower[power] = archetypes;

                    foreach (CharacterArchetype archetype in archetypes)
                        if (m_powersByArchetype.TryGetValue(archetype, out HashSet<DreamPowerDefinition> powers))
                            powers.Add(power);
                        else
                        {
                            powers = new HashSet<DreamPowerDefinition>();
                            powers.Add(power);
                            m_powersByArchetype[archetype] = powers;
                        }
                }
        }

        // Original 0600207d/7e are genuine empty shipped bodies on both CPUs.
        [Conditional("BUILD_DEVELOPMENT")]
        private void AddDebugButtons() { }
        [Conditional("BUILD_DEVELOPMENT")]
        private static void RemoveDebugButtons() { }

        // Original 0600207f uses the settings method, not the current save.
        public bool IsDebugUnlockAll() => m_saveManager.GetSaveDataSettings().Debug.UnlockDreamPowers;

        // Original 06002080 visits existing serialized items only, disposes the
        // enumerator before setting debug state, then requests one save.
        public void BuyAllPowers()
        {
            foreach (SaveDataDreamPowerStoreItem power in m_saveManager.CurrentSave.DreamPowerStore.DreamPowers)
                power.Bought = true;
            m_saveManager.GetSaveDataSettings().Debug.UnlockDreamPowers = true;
            m_saveManager.RequestSave();
        }

        // Original 06002081 resets spent first. Existing loadouts are retained.
        public void ResetBoughtPowers()
        {
            SaveDataGame save = m_saveManager.CurrentSave;
            save.BlueCoins.Spent = 0;
            foreach (SaveDataDreamPowerStoreItem power in save.DreamPowerStore.DreamPowers)
                power.Bought = false;
            m_saveManager.GetSaveDataSettings().Debug.UnlockDreamPowers = false;
            m_saveManager.RequestSave();
        }

        // Original 06002082 changes the debug amount before querying the real
        // progression system; cache failure leaves that amount changed.
        private void AdjustDebugBlueCoins(int amount)
        {
            DebugExtraBlueCoins = unchecked(DebugExtraBlueCoins + amount);
            ProcessManager.GetSystem<ProgressionManager>().CacheChallengeProgress();
        }

        // Original 06002083 initializes the system reference, forward map and
        // reverse map in that order before the genuine MonoBehaviour ctor.
        public DreamPowerStoreManager() { }
    }
}
