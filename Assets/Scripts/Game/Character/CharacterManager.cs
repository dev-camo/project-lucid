using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    // Original Game.Runtime type 02000336. These bodies preserve the real manager;
    // all outer/cache bodies have native-derived candidates. The genuine dependency
    // graph remains incomplete, and natural compiler identities are not yet verified.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterManager : ISystem
    {
        public event Action OnCharacterPreClose; // 06001417/18; native CAS event accessors.
        public event Action OnCharacterRespawn; // 06001419/1a.
        public event Action OnCharacterTakeDamage; // 0600141b/1c.
        public bool CharacterValid { get; private set; } // 0600141d/1e; backing +0x28.
        private Dictionary<CharacterId, CharacterLoadedCache> m_characterLookup;
        private Character m_currentCharacter;
        private Transform m_lastRespawnPoint;
        private FSMStateLoader m_fsmStateLoader;
        private bool m_characterDebugEnabled;
        private Action<Character> m_onCharacterChange;
        private readonly App m_application;
        private SaveManager m_saveManager;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();
        private readonly SystemRef<DreamPowerStoreManager> m_dreamPowerStoreManagerRef = ProcessManager.GetSystemRef<DreamPowerStoreManager>();
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
        private readonly SystemRef<AddressableManager> m_addressableManagerRef = ProcessManager.GetSystemRef<AddressableManager>();
        private Coroutine m_swapCharacterCoroutine;
        private Coroutine m_delayedSwapGapParticleDisableCoroutine;
        private Action<Character> m_pendingCharacterSwapCallback;
        private readonly YieldInstruction m_delayedSwapGapParticleEnableWait = new WaitForSeconds(0.2f);
        private readonly YieldInstruction m_delayedSwapGapParticleDisableWait = new WaitForSeconds(0.75f);
        // The supplied constructor initializes both callbacks to genuine no-op lambdas.
        // Native contexts 06001443/44 each contain only RET; no callback is fabricated.
        public Action OnCharacterStoppedBoosting = () => { };
        public Action OnCharacterAirTimeReported = () => { };

        // 0600141f; ARM742230. Field initializers precede Object base construction.
        public CharacterManager(Action onInitialised)
        {
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
            m_application = ProcessManager.GetSystem<App>();
            ProcessManager.GetSystemRef<SaveManager>().InvokeOnValid(OnSaveManagerValid);
            PreloadCharacterFSMs(onInitialised);
        }

        // 06001420; ARM7427d0. Assignment precedes immediate save validation.
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            m_saveManager = saveManager;
            ValidateCharacterArchetypes();
        }

        // 06001421; ARM7427f8. Zero is the original None archetype, not a character ID.
        // RequestSave runs after disposing enumeration even if no configured unlock changed.
        private void ValidateCharacterArchetypes()
        {
            SaveDataGame currentSave = m_saveManager.CurrentSave;
            currentSave.UnlockCharacterArchetype(CharacterArchetype.None);
            foreach (var (archetype, definition) in m_application.DataManager.CharacterArchetypes)
                if (definition.UnlockedByDefault)
                    currentSave.UnlockCharacterArchetype(archetype);
            m_saveManager.RequestSave();
        }

        // 06001422; ARM7429ac. This intentionally bypasses the validity flag.
        public Character GetCurrentCharacterUnsafe() => m_currentCharacter;

        // 06001423; ARM72ff8c. The flag decides both results; no Unity-null predicate.
        public bool TryGetCurrentCharacter(out Character character)
        {
            bool characterValid = CharacterValid;
            character = characterValid ? m_currentCharacter : null;
            return characterValid;
        }
        // 06001424; ARM738f08.
        public bool IsCurrentCharacter(Character character) => CharacterValid && m_currentCharacter == character;
        // 06001425; ARM7429b4. Use original Actor/Unity collider equality.
        public bool IsCurrentCharacterColliderCollision(Collider collider) =>
            CharacterValid && m_currentCharacter.ColliderCollision == collider;
        // 06001426; ARM742a44.
        public bool IsCurrentCharacterColliderTrigger(Collider collider) =>
            CharacterValid && m_currentCharacter.ColliderTrigger == collider;
        // 06001427; ARM742ad4.
        public bool IsCurrentCharacterTransform(Transform transform) =>
            CharacterValid && m_currentCharacter.transform == transform;

        // 06001428; ARM742b6c. App metagame query precedes typed-reference registration.
        public static bool IsCharacterGameplayActive()
        {
            if (ProcessManager.GetSystem<App>().IsMetaGameActive()) return false;
            SystemRef<CharacterManager> reference = ProcessManager.GetSystemRef<CharacterManager>();
            return reference.IsValid() && reference.Get().CharacterValid;
        }

        // 06001429; wrapper742c8c, original d__43 MoveNext7451a8.
        // Cache each request before starting it, and count completion even for a null result.
        // The original exact equality wait remains; no success/error or retry policy is added.
        public IEnumerator LoadAllCharacters()
        {
            if (m_characterLookup != null) yield break;
            LevelManager levelManager = m_levelManagerRef.Get();
            int loadedCount = 0;
            int characterCount = 0;
            m_characterLookup = new Dictionary<CharacterId, CharacterLoadedCache>(HardlightEnumComparers.CharacterIdComparer);
            foreach (CharacterId characterId in EnumUtilities.GetValues<CharacterId>())
            {
                var cache = new CharacterLoadedCache();
                if (!m_application.DataManager.Characters.TryGetValue(characterId, out cache.Definition))
                    continue;
                ++characterCount;
                m_characterLookup.Add(characterId, cache);
                cache.Handle = m_addressableManagerRef.Get().LoadAssetAsyncInstance(
                    cache.Definition.Prefab, levelManager.ManagedGameObjectParent, _ => ++loadedCount);
            }
            yield return new WaitUntil(() => loadedCount == characterCount);
        }

        // 0600142a; ARM7376fc. Preserve current pose/velocity before authored close callbacks.
        // CharacterValid is read again after callbacks; the second test is not an else.
        public void SetCharacter(CharacterId characterId, bool callOnStartWhenReady = true)
        {
            if (!m_application.DataManager.Characters.ContainsKey(characterId))
            {
                HLOutput.LogError("No character with ID " + characterId.GetString());
                return;
            }
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector3 velocity = Vector3.zero;
            if (CharacterValid)
            {
                Transform lastRespawnPoint = m_currentCharacter.Storage.GetValue<Transform>(ActorFSMKeys.LastRespawnPoint);
                if (lastRespawnPoint != null) m_lastRespawnPoint = lastRespawnPoint;
                position = m_currentCharacter.WorldPosition;
                rotation = m_currentCharacter.WorldRotation;
                velocity = m_currentCharacter.WorldVelocity;
                m_currentCharacter.OnTakeDamage -= OnCharacterTakeDamage;
                OnCharacterPreClose?.Invoke();
                m_currentCharacter.Close(false);
                if (m_characterLookup.TryGetValue(m_currentCharacter.Definition.Id, out CharacterLoadedCache cache))
                {
                    GameObject proxy = cache.Proxy;
                    proxy.transform.SetParent(m_levelManagerRef.Get().ManagedGameObjectParent);
                    proxy.SetActive(false);
                }
            }
            if (!CharacterValid)
                m_currentCharacter = ProcessManager.GetSystem<LevelManager>().InstantiateParented(
                    m_characterLookup[characterId].Definition.ProxyPrefab, Vector3.zero, Quaternion.identity);
            CharacterValid = false;
            m_characterDebugEnabled = m_currentCharacter.DebugIsEnabled();
            m_application.Storage.SetValue(AppFSMKeys.CurrentCharacterId, characterId);
            InitialisePreloadedCharacter(characterId, callOnStartWhenReady, position, rotation, velocity);
        }

        // 0600142b; ARM743828. Cleanup completes before starting the original swap flow.
        public void SwapCharacter(CharacterId characterId, bool isShortcut)
        {
            if (!CharacterValid || m_currentCharacter.IdType == characterId) return;
            CleanUpSwapCharacterCoroutine();
            m_swapCharacterCoroutine = Hardlight.Utils.CoroutineUtils.RunCoroutine(SwapCharacterCoroutine(characterId, isShortcut));
        }

        // 0600142c; wrapper743a0c, original d__46 MoveNext745630.
        // Keep the immediate callback registration, nested ExitTriggerVolumes yield,
        // exact wait ordering and final cleanup, including stopping the new delayed handle.
        private IEnumerator SwapCharacterCoroutine(CharacterId characterId, bool isShortcut)
        {
            m_missionManagerRef.Get().SetTimersPaused(true);
            if (m_currentCharacter == null)
            {
                CleanUpSwapCharacterCoroutine();
                yield break;
            }
            m_currentCharacter.BeginSwapStart(isShortcut);
            PlaySwapGapEffects(characterId);
            yield return m_delayedSwapGapParticleEnableWait;
            m_currentCharacter.BeginSwapOut();
            if (m_currentCharacter.BlobShadowObject != null)
                m_currentCharacter.BlobShadowObject.SetActive(false);
            m_currentCharacter.EndSwapStart(isShortcut);
            bool onCharacterChangeCallbackCalled = false;
            void OnCharacterChangeCallback(Character _) => onCharacterChangeCallbackCalled = true;
            RegisterOnCharacterChange(OnCharacterChangeCallback);
            m_pendingCharacterSwapCallback = OnCharacterChangeCallback;
            yield return m_currentCharacter.ExitTriggerVolumes();
            SetCharacter(characterId, true);
            while (!onCharacterChangeCallbackCalled) yield return null;
            ReleaseOnCharacterChange(OnCharacterChangeCallback);
            m_pendingCharacterSwapCallback = null;
            if (m_currentCharacter == null)
            {
                CleanUpSwapCharacterCoroutine();
                yield break;
            }
            m_currentCharacter.BeginSwapIn();
            while (true)
            {
                if (m_currentCharacter == null)
                {
                    CleanUpSwapCharacterCoroutine();
                    yield break;
                }
                if (!m_currentCharacter.SwapInIsInProgress()) break;
                yield return null;
            }
            m_currentCharacter.ContinueSwapIn();
            m_currentCharacter.MovementResume(m_currentCharacter.WorldPosition, m_currentCharacter.WorldVelocity);
            yield return m_delayedSwapGapParticleDisableWait;
            m_currentCharacter.EndSwapIn();
            Hardlight.Utils.CoroutineUtils.StopUtilCoroutine(ref m_delayedSwapGapParticleDisableCoroutine);
            m_delayedSwapGapParticleDisableCoroutine = Hardlight.Utils.CoroutineUtils.RunCoroutine(DelayedSwapGapParticleDisable());
            m_currentCharacter.SwapGapVisibilityGroupOverrider.DeactivateOverrides();
            CleanUpSwapCharacterCoroutine();
        }

        // 0600142d; ARM743a9c. Unity lifetime check, effect, then visibility override.
        private void PlaySwapGapEffects(CharacterId characterId)
        {
            if (m_currentCharacter == null) return;
            m_currentCharacter.PlaySwapGapEffects(characterId);
            m_currentCharacter.SwapGapVisibilityGroupOverrider.ActivateOverrides();
        }

        // 0600142e; wrapper743b64, genuine d__48 MoveNext745010.
        // This retains the current-character lookup after the wait, and does not clear its handle.
        private IEnumerator DelayedSwapGapParticleDisable()
        {
            yield return m_delayedSwapGapParticleDisableWait;
            if (m_currentCharacter != null) m_currentCharacter.StopSwapGapEffects();
        }

        // 0600142f; ARM743900. A null swap handle returns before all other cleanup.
        private void CleanUpSwapCharacterCoroutine()
        {
            if (m_swapCharacterCoroutine == null) return;
            Hardlight.Utils.CoroutineUtils.StopUtilCoroutine(ref m_swapCharacterCoroutine);
            Hardlight.Utils.CoroutineUtils.StopUtilCoroutine(ref m_delayedSwapGapParticleDisableCoroutine);
            if (m_currentCharacter != null)
            {
                m_currentCharacter.StopSwapGapEffects();
                m_currentCharacter.SwapGapVisibilityGroupOverrider.DeactivateOverrides();
            }
            m_missionManagerRef.Get().SetTimersPaused(false);
            if (m_pendingCharacterSwapCallback != null)
            {
                ReleaseOnCharacterChange(m_pendingCharacterSwapCallback);
                m_pendingCharacterSwapCallback = null;
            }
        }
        // 06001430; ARM743c9c. No Unity lifetime test is performed on this handle.
        public bool IsCharacterSwapInProgress() => m_swapCharacterCoroutine != null;

        // 06001431; ARM742d04. Dream-power additions must belong to an existing tier.
        // Overrides replace only an added ability; duplicate Add failures remain original.
        private void InitialisePreloadedCharacter(CharacterId characterId, bool callOnStartWhenReady,
            Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!m_characterLookup.TryGetValue(characterId, out CharacterLoadedCache cache) || cache == null) return;
            var abilities = new Dictionary<ActorAbilityType, AbilityDefinition>();
            var overrides = new Dictionary<ActorAbilityType, AbilityDefinition>();
            List<AbilityTiersDefinition> tiers = cache.Definition.Traits.AbilityTierDefinitions;
            if (m_missionManagerRef.TryGet(out MissionManager missionManager) && missionManager.HasActiveMission() &&
                missionManager.ActiveMissionContext.MissionDefinition.EnableDreamPowers)
            {
                CharacterArchetype loadoutArchetype = missionManager.ActiveMissionContext.MissionDefinition.CharacterArchetype;
                SaveDataDreamPowerLoadout loadout = m_saveManager.CurrentSave.GetOrCreateDreamPowerLoadout(loadoutArchetype);
                CharacterArchetype archetype = cache.Definition.Archetype;
                foreach (var (_, powerGuid) in loadout.SlotsByIndex)
                {
                    DreamPowerDefinition dreamPower = m_application.DataManager.DreamPowerDefinitions[powerGuid];
                    if (!m_dreamPowerStoreManagerRef.Get().ArchetypeAllowsPower(archetype, dreamPower)) continue;
                    foreach (AbilityDefinition abilityDefinition in dreamPower.AbilityDefinitions)
                        if (tiers.Exists(tierDef => tierDef.HasTier(abilityDefinition)))
                            abilities.Add(abilityDefinition.AbilityType, abilityDefinition);
                    foreach (AbilityDefinition abilityOverride in dreamPower.AbilityOverrides)
                        overrides.Add(abilityOverride.AbilityType, abilityOverride);
                }
            }
            foreach (var (_, abilityOverride) in overrides)
                if (abilities.ContainsKey(abilityOverride.AbilityType))
                    abilities[abilityOverride.AbilityType] = abilityOverride;
            CharacterComponentLookup lookup = cache.Proxy.GetComponent<CharacterComponentLookup>();
            m_currentCharacter.Initialise(cache.Definition, m_lastRespawnPoint, position, rotation, velocity,
                lookup, m_characterDebugEnabled, abilities);
            if (callOnStartWhenReady) m_currentCharacter.OnStart(velocity, true);
            m_currentCharacter.OnTakeDamage += OnCharacterTakeDamage;
            CharacterValid = true;
            m_onCharacterChange?.Invoke(m_currentCharacter);
        }

        // 06001432; ARM743cac. A synchronous callback may mutate manager state before combine.
        public void RegisterOnCharacterChange(Action<Character> onCharacterChange, bool invokeImmediatelyIfValid = true)
        {
            if (invokeImmediatelyIfValid && onCharacterChange != null && CharacterValid)
                onCharacterChange(m_currentCharacter);
            m_onCharacterChange += onCharacterChange;
        }
        // 06001433; ARM743bdc.
        public void ReleaseOnCharacterChange(Action<Character> onCharacterChange) => m_onCharacterChange -= onCharacterChange;

        // 06001434; ARM743d94. Missing addressable host leaves the lookup intact.
        // Release every stored handle, clear that dictionary, then null the field.
        public void DestroyCharacters()
        {
            CleanUpSwapCharacterCoroutine();
            if (CharacterValid)
            {
                UnityEngine.Object.Destroy(m_currentCharacter.gameObject);
                m_currentCharacter = null;
                CharacterValid = false;
            }
            if (m_characterLookup == null) return;
            AddressableManager addressableManager = m_addressableManagerRef.GetSafe();
            if (addressableManager == null) return;
            foreach (var pair in m_characterLookup)
                addressableManager.ReleaseHandle(pair.Value.Handle);
            m_characterLookup.Clear();
            m_characterLookup = null;
        }

        // 06001435; ARM74402c. No invented shutdown unsubscribe or callback catch.
        private void Shutdown(object context = null)
        {
            CleanUpSwapCharacterCoroutine();
            if (m_fsmStateLoader != null)
                foreach (FiniteStateMachineScriptableObject stateMachine in m_fsmStateLoader.LoadedStateMachines)
                    stateMachine.ReleaseFSM();
            DestroyCharacters();
        }

        // 06001436; ARM742684. Copy the complete dictionary before supplying its values.
        private void PreloadCharacterFSMs(Action onInitialised)
        {
            var machines = new Dictionary<string, FiniteStateMachineScriptableObject>(
                ProcessManager.GetSystem<App>().DataManager.CharacterStateMachines);
            m_fsmStateLoader = new FSMStateLoader(machines.Values, onInitialised);
            m_fsmStateLoader.LoadStates();
        }
        // 06001437; ARM74416c.
        public void FireOnCharacterRespawned() => OnCharacterRespawn?.Invoke();

        // 06001438; ARM744188. None selects every definition; preserve AddUnique.
        public static void GetCharacterIdsForArchetype(CharacterArchetype archetype,
            ref List<CharacterId> characterIds, bool clearList = true)
        {
            if (clearList) characterIds.Clear();
            foreach (var (id, definition) in ProcessManager.GetSystem<DataManager>().Characters)
                if (archetype == CharacterArchetype.None || definition.Archetype == archetype)
                    characterIds.AddUnique(id);
        }

        // 06001439; ARM7443fc. Fast loading bypasses progression locks in this release.
        public bool IsCharacterArchetypeLocked(CharacterArchetype characterArchetype)
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return false;
            if (m_application.Storage.GetValue(AppFSMKeys.IsFastLoading, false, true)) return false;
            return !ProcessManager.GetSystem<SaveManager>().CurrentSave
                .GetOrCreateCharacterArchetypeData(characterArchetype).Unlocked;
        }
        // 0600143a; ARM7445dc. This method itself does not request a save.
        public void UnlockCharacterArchetype(MissionDefinition missionDefinition)
        {
            CharacterArchetype archetype = missionDefinition.UnlocksCharacterArchetype;
            if (archetype != CharacterArchetype.None)
                ProcessManager.GetSystem<SaveManager>().CurrentSave.UnlockCharacterArchetype(archetype);
        }

        // 0600143b; ARM744680. Queue unseen level-select unlock presentations for the
        // next level. The native +0x40 save field is LevelSelectUnlockSeen, not Complete.
        // Requirements receive the level; both enumerators retain normal disposal order.
        public void UpdateUnlocks()
        {
            if (m_levelManagerRef.IsNull() || m_missionManagerRef.IsNull()) return;
            GameplayLevelDefinition nextLevel = m_levelManagerRef.Get().GetNextLevelNotSeen();
            if (nextLevel == null) return;
            IReadOnlyDictionary<MissionDefinition, SaveDataLevelMission> missionSaveData =
                m_missionManagerRef.Get().GetMissionSaveDataForLevel(nextLevel);
            foreach (MissionGroup group in nextLevel.MissionList.Groups)
            {
                if (!group.MeetsAllRequirements(nextLevel)) continue;
                foreach (MissionDefinition mission in group.Definitions)
                {
                    if (mission.UnlocksCharacterArchetype == CharacterArchetype.None) continue;
                    if (!missionSaveData.TryGetValue(mission, out SaveDataLevelMission saveData)) continue;
                    if (saveData.LevelSelectUnlockSeen) continue;
                    CharacterArchetypeDefinition definition =
                        m_application.DataManager.CharacterArchetypes[mission.UnlocksCharacterArchetype];
                    // Read the existing list before constructing the presentation. A missing
                    // list remains an error; this method does not allocate a replacement.
                    m_application.Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks,
                        null, true).Add(new MetaGameUnlockCharacterArchetype(definition));
                }
            }
        }

        // 0600143c; ARM737348. Direct original CharacterDefinition -> Traits list.
        public IReadOnlyList<AbilityTiersDefinition> GetAbilityTiers() =>
            m_currentCharacter.Definition.Traits.AbilityTierDefinitions;
        // 0600143d/3e; ARM744e94/98. Actual supplied RET methods, not recovery defaults.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void AddDebugOverride(AbilityDefinition abilityDefinition) { }
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void ClearDebugOverrides() { }

        // Original nested type02000337, full Handle+Definition fields and two methods.
        private class CharacterLoadedCache
        {
            public AsyncOperationHandle<GameObject> Handle;
            public CharacterDefinition Definition;
            // 0600143f; ARM744e9c. Read the actual stored handle's Result each time.
            public GameObject Proxy => Handle.Result;
            // 06001440; ARM744ef0. Genuine base-only constructor.
            public CharacterLoadedCache() { }
        }
    }
}
