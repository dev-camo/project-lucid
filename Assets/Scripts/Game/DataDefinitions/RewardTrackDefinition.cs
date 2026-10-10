using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    // Game.Runtime.dll 0x020005c2. The complete original reward-track definition,
    // including its authored inspector callbacks and lazy GUID cache.
    [CreateAssetMenu(fileName = "RewardTrackDefinition",
        menuName = "HardlightProject/DefinitionData/Definitions/RewardTrackDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RewardTrackDefinition : ScriptableObjectWithGuid, IButtonReceiver
    {
        [Button(false, "Sort rewards by XP threshold")]
        [SerializeField] private RewardTrackType m_type;
        [SerializeField] private List<ChallengeReward> m_rewards = new List<ChallengeReward>();
        [SerializeField] private Color m_rewardGlowColour;
        [SerializeField, HashEnum((System.Type)null)] private Strings m_trackName;
        [SerializeField, HashEnum((System.Type)null)] private Strings m_trackDescription;
        [SerializeField, HashEnum((System.Type)null)] private Strings m_trackOverview;
        [SerializeField] private Color m_backgroundGradientStart;
        [SerializeField] private Color m_backgroundGradientEnd;
        [SerializeField] private Color m_shineColour;
        [SerializeField] private AssetReferenceAtlasedSprite m_icon;
        [SerializeField] private AssetReferenceAtlasedSprite m_thumbnail;
        [SerializeField] private UIContainerIdentifier m_uiContainerIdentifier;
        [SerializeField] private ChallengeManager.XPSink m_xpSinkTypeForAnalytics;

        // Original accessors 0x06001f30 through 0x06001f3e, including the two
        // private auto-property setters and their original backing-field names.
        public RewardTrackType Type => m_type;
        public List<ChallengeReward> Rewards => m_rewards;
        public Color RewardGlowColour => m_rewardGlowColour;
        public Strings TrackName => m_trackName;
        public Strings TrackDescription => m_trackDescription;
        public Strings TrackOverview => m_trackOverview;
        public Color BackgroundGradientStart => m_backgroundGradientStart;
        public Color BackgroundGradientEnd => m_backgroundGradientEnd;
        public Color ShineColour => m_shineColour;
        public ManagedAddressableAsset<Sprite> IconAsset { get; private set; }
        public ManagedAddressableAsset<Sprite> ThumbnailAsset { get; private set; }
        public UIContainerIdentifier UIContainerIdentifier => m_uiContainerIdentifier;
        public ChallengeManager.XPSink XPSinkTypeForAnalytics => m_xpSinkTypeForAnalytics;

        private readonly HashSet<string> m_rewardGuids = new HashSet<string>();

        // 0x06001f3f: publish the icon wrapper before constructing the thumbnail.
        // Neither wrapper loads content here; repeated enables replace both.
        protected void OnEnable()
        {
            IconAsset = new ManagedAddressableAsset<Sprite>(m_icon);
            ThumbnailAsset = new ManagedAddressableAsset<Sprite>(m_thumbnail);
        }

        // 0x06001f40: preserve authored enumeration, virtual GetRewardGUID,
        // exclusion of null/empty GUID strings, and cumulative cache population.
        private void CacheRewardGUIDs()
        {
            foreach (ChallengeReward reward in m_rewards)
            {
                string guid = reward.GetRewardGUID();
                if (!string.IsNullOrEmpty(guid)) m_rewardGuids.Add(guid);
            }
        }

        // 0x06001f41: the shipping method allocates this local GUID list even
        // though it never reads it. Add the base object's GUID before Validate;
        // clear the lazy cache only after every authored callback completes.
        protected override void OnValidate()
        {
            base.OnValidate();
            var rewardGuids = new List<string>(m_rewards.Count);
            foreach (ChallengeReward reward in m_rewards)
            {
                rewardGuids.Add(reward.GetGUID());
                reward.Validate(this);
            }
            m_rewardGuids.Clear();
        }

        // 0x06001f42: the inspector button is attached to the original m_type.
        public void OnButtonClick(string fieldName)
        {
            if (fieldName == "m_type") SortChallengeRewards();
        }

        // 0x06001f43 and original natural comparison 0x06001f48. Int32.CompareTo
        // avoids subtraction overflow. Equal thresholds retain List.Sort's own
        // behavior; there is no extra tie rule or null fallback in the original.
        private void SortChallengeRewards()
        {
            m_rewards.Sort((lhs, rhs) => lhs.XPThreshold.CompareTo(rhs.XPThreshold));
        }

        // 0x06001f44: an empty cache is populated again on every lookup. A
        // nonempty cache remains stale until OnValidate clears it.
        public bool ContainsRewardGUID(string guid)
        {
            if (m_rewardGuids.Count == 0) CacheRewardGUIDs();
            return m_rewardGuids.Contains(guid);
        }

        // 0x06001f45: the list and GUID set initializers execute, in that order,
        // before the original ScriptableObjectWithGuid base constructor.
        public RewardTrackDefinition() { }
    }
}
