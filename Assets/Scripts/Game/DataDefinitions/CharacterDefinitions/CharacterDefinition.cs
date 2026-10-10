using System;
using Hardlight;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CharacterDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterDefinition : ActorDefinition
    {
        [SerializeField] private CharacterId m_id;
        [SerializeField] private CharacterArchetype m_archetype;
        [SerializeField] private CharacterSkinDefinition[] m_characterSkins = Array.Empty<CharacterSkinDefinition>();
        public Color PrimaryColour = Color.white;
        public Character ProxyPrefab;
        public CharacterTraits Traits;
        public ManagedAddressableAsset<Sprite> Image;
        public ManagedAddressableAsset<Sprite> Render;
        public ManagedAddressableAsset<Sprite> RenderCompanion;
        [NonSerialized] private CharacterSkinDefinition m_currentSkin;

        public CharacterId Id => m_id;
        public CharacterArchetype Archetype => m_archetype;
        public AssetReferenceT<GameObject> Prefab => m_currentSkin.Prefab;
        public AssetReferenceT<GameObject> MeshPrefab => m_currentSkin.MeshPrefab;
        public CharacterSkinDefinition[] CharacterSkins => m_characterSkins;
        public CharacterSkinDefinition CurrentSkin => m_currentSkin;

        protected override void UpdateCachedValues()
        {
            // Original 06001bf6 resets the real base caches before applying the
            // current skin's overrides in authored order. Null arrays/elements
            // are not replaced; failures retain already-applied entries.
            base.UpdateCachedValues();
            if (m_currentSkin != null)
            {
                ActorParticleEffectLookup[] lookups = m_currentSkin.PfxOverrides;
                foreach (ActorParticleEffectLookup lookup in lookups)
                {
                    lookup.UpdateCachedValues();
                    foreach (var (trigger, effect) in lookup.Lookup)
                        PFXDefinitionsDictionary[trigger] = effect;
                }
                ActorAudioDefinition[] audioDefinitions = m_currentSkin.AudioOverrides;
                foreach (ActorAudioDefinition definition in audioDefinitions)
                    ActorAudioLookup.CompileLookup(definition.ActorAudioReferences);
            }
        }

        public bool SetCharacterSkin(string skinGUID)
        {
            // Original 06001bf7 captures the array and its first element before
            // walking every entry. The last matching GUID wins; an unknown GUID
            // falls back to the first entry. Empty arrays are not special-cased.
            CharacterSkinDefinition[] skins = m_characterSkins;
            CharacterSkinDefinition selected = skins[0];
            foreach (CharacterSkinDefinition skin in skins)
                if (skin.GetGUID() == skinGUID) selected = skin;
            return SetCharacterSkin(selected);
        }

        public bool SetCharacterSkin(CharacterSkinDefinition skin)
        {
            if (m_currentSkin == skin) return false;
            m_currentSkin = skin;
            // Original 06001bf8 publishes render, companion, then image wrappers
            // before virtual cache refresh and real SaveManager registration.
            Render = new ManagedAddressableAsset<Sprite>(skin.Render);
            RenderCompanion = new ManagedAddressableAsset<Sprite>(skin.RenderCompanion);
            Image = new ManagedAddressableAsset<Sprite>(skin.CardImage);
            UpdateCachedValues();
            ProcessManager.GetSystemRef<SaveManager>().InvokeOnValid(saveManager =>
            {
                // Original natural displayclass24_0 captures this and skin.
                // Capture CurrentSave once. Set customisation before marking
                // unlock seen, then request save; no null guards or swallowing.
                SaveDataGame save = saveManager.CurrentSave;
                save.GetOrCreateCharacterCustomisation(Id).Set(skin);
                save.GetOrCreateSkinData(skin.GetGUID()).UnlockSeen = true;
                saveManager.RequestSave();
            });
            return true;
        }

        // Original 06001bf9 initializes shared empty skins then opaque white
        // before the genuine ActorDefinition base constructor.
        public CharacterDefinition() { }
    }
}
