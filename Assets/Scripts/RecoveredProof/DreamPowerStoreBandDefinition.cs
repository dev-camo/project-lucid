using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "DreamPowerStoreBandDefinition", menuName = "HardlightProject/DefinitionData/Definitions/DreamPowerStoreBandDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class DreamPowerStoreBandDefinition : Hardlight.ScriptableObjectWithGuid
    {
        [UnityEngine.SerializeField]
        private HardlightProject.DreamPowerStoreBandType m_type;

        [UnityEngine.SerializeField]
        private UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite m_image;

        [UnityEngine.SerializeField]
        private HardlightProject.UIWidgetProgression m_progressionUnlockWidget;

        [Hardlight.Utils.HashEnum(null)]
        [UnityEngine.SerializeField]
        private Hardlight.Enums.Strings m_name;

        [UnityEngine.SerializeField]
        private UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite m_slotLockedBackground;

        // Original Game.Runtime 0x06001c5c, ARM 0x5223e0.
        public HardlightProject.DreamPowerStoreBandType Type => m_type;

        // Original Game.Runtime 0x06001c5d, ARM 0x5223e8.
        public HardlightProject.UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;

        // Original Game.Runtime 0x06001c5e, ARM 0x5223f0.
        // Original Game.Runtime 0x06001c5f, ARM 0x5223f8.
        public ManagedAddressableAsset<Sprite> ImageAsset { get; private set; }

        // Original Game.Runtime 0x06001c60, ARM 0x522400.
        // Original Game.Runtime 0x06001c61, ARM 0x522408.
        public ManagedAddressableAsset<Sprite> SlotLockedBackground { get; private set; }

        // Original Game.Runtime 0x06001c62, ARM 0x522410.
        public Hardlight.Enums.Strings Name => m_name;

        // Original Game.Runtime 0x06001c63, ARM 0x522418.
        // Preserve image construction/publication before the background reference is read.
        // Each enable replaces both wrappers; native code performs no release/load here.
        protected void OnEnable()
        {
            ImageAsset = new ManagedAddressableAsset<Sprite>(m_image);
            SlotLockedBackground = new ManagedAddressableAsset<Sprite>(m_slotLockedBackground);
        }
        // Original Game.Runtime 0x06001c64, ARM 0x5224d4.
        // Natural original constructor; documented field initializers precede the genuine base call.
    }
}
