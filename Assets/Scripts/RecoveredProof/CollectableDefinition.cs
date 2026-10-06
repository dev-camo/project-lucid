using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "CollectableDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CollectableDefinition")]
    public class CollectableDefinition : Hardlight.ScriptableObjectWithGuid
    {
        [UnityEngine.SerializeField]
        private HardlightProject.CollectableType m_type;

        [UnityEngine.SerializeField]
        private UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite m_icon;

        [UnityEngine.SerializeField]
        private UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite m_iconAlt;

        [UnityEngine.SerializeField]
        private HardlightProject.UIWidgetProgression m_progressionUnlockWidget;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Should be of the format {0}{1} etc. Used for UI display.")]
        private System.String m_displayFormattedString = "{0}/{1}";

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Set to true if collectable should track total to collect in a level.")]
        private System.Boolean m_hasTotal;

        [NonSerialized]
        private UnityEngine.Sprite m_loadedSprite;

        [NonSerialized]
        private UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<UnityEngine.Sprite> m_operationHandle;

        [NonSerialized]
        private System.Int32 m_refCount;

        public HardlightProject.ManagedAddressableAsset<UnityEngine.Sprite> IconAsset;

        public HardlightProject.ManagedAddressableAsset<UnityEngine.Sprite> IconAltAsset;

        // Original Game.Runtime 0x06001c15, ARM 0x51e968.
        public HardlightProject.CollectableType Type => m_type;

        // Original Game.Runtime 0x06001c16, ARM 0x51e970.
        public UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite Icon => m_icon;

        // Original Game.Runtime 0x06001c17, ARM 0x51e978.
        public System.String DisplayFormattedString => m_displayFormattedString;

        // Original Game.Runtime 0x06001c18, ARM 0x51e980.
        public System.Boolean HasTotal => m_hasTotal;

        // Original Game.Runtime 0x06001c19, ARM 0x51e988.
        public HardlightProject.UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;

        // Original Game.Runtime 0x06001c1a, ARM 0x51e990.
        // Publish the primary wrapper before reloading the alternate reference.
        // Existing wrappers are replaced without loading or releasing their handles.
        protected void OnEnable()
        {
            IconAsset = new ManagedAddressableAsset<Sprite>(m_icon);
            IconAltAsset = new ManagedAddressableAsset<Sprite>(m_iconAlt);
        }

        // Original Game.Runtime 0x06001c1b, ARM 0x51ea4c.
        // Original captures the old sprite before increasing the unchecked count.
        // Unity's live-object predicate gates reuse; store the handle before waiting.
        public Sprite LoadIcon()
        {
            Sprite loaded = m_loadedSprite;
            m_refCount = unchecked(m_refCount + 1);
            if (loaded != null) return m_loadedSprite;
            m_operationHandle = Addressables.LoadAssetAsync<Sprite>(m_icon);
            m_loadedSprite = m_operationHandle.WaitForCompletion();
            return m_loadedSprite;
        }

        // Original Game.Runtime 0x06001c1c, ARM 0x51ebb0.
        // Original clears the sprite then clamps the count before checking the handle.
        // Release receives its current value; the original handle field is retained.
        public void UnloadIcon()
        {
            m_refCount = unchecked(m_refCount - 1);
            if (m_refCount > 0) return;
            m_loadedSprite = null;
            m_refCount = 0;
            if (m_operationHandle.IsValid()) Addressables.Release(m_operationHandle);
        }

        // Original Game.Runtime 0x06001c1d, ARM 0x51ecac.
        // Natural original constructor; genuine field initializers precede base.
    }
}
