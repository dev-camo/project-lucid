using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenu(fileName = "ChallengeZoneMaterialDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ChallengeZoneMaterialDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class ChallengeZoneMaterialDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField]
        private HardlightProject.ChallengeZoneIdentifier m_identifier;

        [UnityEngine.SerializeField]
        private HardlightProject.ChallengeZoneMaterialDefinition.ChallengeZoneMaterial[] m_materials = Array.Empty<ChallengeZoneMaterial>();

        // Original Game.Runtime 0x06001aa1, ARM 0x5172f0.
        public HardlightProject.ChallengeZoneIdentifier Identifier => m_identifier;

        // Original Game.Runtime 0x06001aa2, ARM 0x5172f8.
        // Native always clears the out enum and returns false without reading fields.
        public bool TryGetTypeMatch(Material material, out ChallengeZoneMaterialType matchingType)
        {
            matchingType = default;
            return false;
        }

        // Original Game.Runtime 0x06001aa3, ARM 0x51730c.
        // Native clears material before reading the array and returns the first Type match.
        // ApplyMaterial is genuinely empty in the supplied player; no asset load is inferred.
        public bool TryGetMaterial(ChallengeZoneMaterialType type, out Material material)
        {
            material = null;
            foreach (ChallengeZoneMaterial entry in m_materials)
            {
                if (entry.Type == type)
                {
                    entry.ApplyMaterial(ref material);
                    return true;
                }
            }
            return false;
        }

        [Serializable]
        public class ChallengeZoneMaterial
        {
            [UnityEngine.SerializeField]
            private HardlightProject.ChallengeZoneMaterialType m_type;

            [UnityEngine.SerializeField]
            private UnityEngine.AddressableAssets.AssetReferenceT<UnityEngine.Material> m_materialAsset;

            // Original Game.Runtime 0x06001aa5, ARM 0x5174b4.
            public HardlightProject.ChallengeZoneMaterialType Type => m_type;

            // Original Game.Runtime 0x06001aa6, ARM 0x517304.
            // Genuine native constant-false body, even when an authored reference is present.
            public bool IsMatch(Material mat) => false;

            // Original Game.Runtime 0x06001aa7, ARM 0x5173bc.
            // Genuine native RET preserves the caller's ref value; no local replacement.
            public void ApplyMaterial(ref Material mat) { }

            // Original Game.Runtime 0x06001aa8, ARM 0x5174bc.
            // Natural Object-base-only constructor.
        }

        // Original Game.Runtime 0x06001aa4, ARM 0x5173c0.
        // Natural original constructor; field initializers precede the genuine base.
    }
}
