using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "ZoneThemeOverride", menuName = "HardlightProject/DefinitionData/Definitions/ZoneThemeOverride")]
    public class ZoneThemeOverride : ScriptableObject
    {
        [SerializeField] private AssetReferenceAtlasedSprite m_zoneAccentOverride;
        [SerializeField] private bool m_zoneGradientOverride;
        [ShowIf("m_zoneGradientOverride", null)]
        [SerializeField] private Color m_backgroundStartColour = Color.white;
        [ShowIf("m_zoneGradientOverride", null)]
        [SerializeField] private Color m_backgroundEndColour = Color.white;

        // Game.Runtime.dll 0x06001864/0x06001865; ARM64 0x7748ac/0x7748b4.
        public ManagedAddressableAsset<Sprite> ZoneAccentOverride { get; private set; }

        // 0x06001866; ARM64 0x7748bc. A fresh wrapper on every enable, even null.
        private void OnEnable() => ZoneAccentOverride = new ManagedAddressableAsset<Sprite>(m_zoneAccentOverride);

        // 0x06001867; ARM64 0x774944. Sprite reference always assigned first;
        // it may clear the supplied reference even when gradient override is false.
        public void Apply(ref AssetReferenceAtlasedSprite zoneAccent, ref Color backgroundStart, ref Color backgroundEnd)
        {
            zoneAccent = m_zoneAccentOverride;
            if (m_zoneGradientOverride)
            {
                backgroundStart = m_backgroundStartColour;
                backgroundEnd = m_backgroundEndColour;
            }
        }
        // 0x06001868; ARM64 0x77499c. Both colour refs remain untouched when false.
        public void Apply(ref Color backgroundStart, ref Color backgroundEnd)
        {
            if (m_zoneGradientOverride)
            {
                backgroundStart = m_backgroundStartColour;
                backgroundEnd = m_backgroundEndColour;
            }
        }
        // 0x06001869; ARM64 0x7749b8. Raw managed reference check, no RuntimeKey,
        // wrapper validity or load invocation. Supplied out reference may stay stale.
        public void GetAccentImage(ref ManagedAddressableAsset<Sprite> zoneAccentImage)
        {
            if (m_zoneAccentOverride != null) zoneAccentImage = ZoneAccentOverride;
        }
        // 0x0600186a; ARM64 0x7749d8. Two Color.white field initializers precede
        // ScriptableObject construction; other fields retain their zero/null default.
    }
}
