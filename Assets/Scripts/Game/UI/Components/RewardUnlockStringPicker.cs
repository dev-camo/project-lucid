using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original Game.Runtime 0200083b and nested 0200083c: eight methods.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RewardUnlockStringPicker : MonoBehaviour
    {
        [SerializeField] private List<RewardUnlockStringPickerElement> m_rewardTrackStrings;
        [HashEnum, SerializeField] private Strings m_defaultString = Strings.NONE;
        public readonly Bindable<Strings> StringID = new Bindable<Strings>();
        private static readonly SystemRef<ChallengeManager> s_challengeManagerRef =
            ProcessManager.GetSystemRef<ChallengeManager>(null, true);
        private static readonly SystemRef<DataManager> s_dataManagerRef =
            ProcessManager.GetSystemRef<DataManager>(null, true);
        private RewardTrackType m_currentTrackType;

        // Original 06002fb7. Zero has no named literal in the shipping enum.
        private void Awake()
        {
            if (m_currentTrackType == (RewardTrackType)0)
                StringID.Value = m_defaultString;
        }

        // Original 06002fb8: publish the matched track before invoking its binder.
        // Foreach disposal occurs before the fallback writes and on callback faults.
        public void UpdateStringFromRewardGUID(string rewardGuid)
        {
            if (s_challengeManagerRef.Get().TryGetRewardTrackForReward(rewardGuid, out RewardTrackType trackType))
            {
                foreach (RewardUnlockStringPickerElement element in m_rewardTrackStrings)
                {
                    if (element.RewardTrackType == trackType)
                    {
                        m_currentTrackType = element.RewardTrackType;
                        StringID.Value = element.StringID;
                        return;
                    }
                }
            }
            m_currentTrackType = (RewardTrackType)0;
            StringID.Value = m_defaultString;
        }

        // Original 06002fb9: capture the band before the mandatory manager lookup.
        // The successful dictionary value supplies the genuine inherited GUID API.
        public void UpdateStringFromDreamPower(DreamPowerDefinition dreamPowerDefinition)
        {
            DreamPowerStoreBandType band = dreamPowerDefinition.Band;
            if (s_dataManagerRef.Get().DreamPowerStoreBandDefinitions.TryGetValue(band,
                out DreamPowerStoreBandDefinition bandDefinition))
            {
                UpdateStringFromRewardGUID(bandDefinition.GetGUID());
                return;
            }
            m_currentTrackType = (RewardTrackType)0;
            StringID.Value = m_defaultString;
        }

        // Original 06002fba: default string, binder, then MonoBehaviour base.
        // Original 06002fbb: challenge reference before data reference; implicit
        // static initializer preserves the original BeforeFieldInit type flag.

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class RewardUnlockStringPickerElement
        {
            [SerializeField] private RewardTrackType m_rewardTrackType;
            [SerializeField, HashEnum] private Strings m_stringId;

            // Original 06002fbc and 06002fbd.
            public RewardTrackType RewardTrackType => m_rewardTrackType;
            public Strings StringID => m_stringId;
            // Original 06002fbe: Object base only, no field initializer.
        }
    }
}
