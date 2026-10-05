using System;
using Hardlight.Analytics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataAnalyticsConsentState : SaveDataResolvableItem<SaveDataAnalyticsConsentState>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_gameCenterAccountID;
        [SerializeField]
        private AnalyticsConsentState m_consentState;
        [SerializeField]
        private long m_consentUpdatedTimestamp;
        // Game.Runtime.dll 0x06002b3a.
        public string GameCenterAccountID
        {
            get
            {
                return m_gameCenterAccountID;
            }

            // 0x06002b3b
            set
            {
                if (m_gameCenterAccountID == value)
                    return;
                m_gameCenterAccountID = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b3c.
        public AnalyticsConsentState ConsentState
        {
            get
            {
                return m_consentState;
            }

            // 0x06002b3d
            set
            {
                if (m_consentState == value)
                    return;
                m_consentState = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b3e.
        public long ConsentUpdatedTimestamp
        {
            get
            {
                return m_consentUpdatedTimestamp;
            }

            // 0x06002b3f
            set
            {
                if (m_consentUpdatedTimestamp == value)
                    return;
                m_consentUpdatedTimestamp = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b40; timestamp is UTC milliseconds.
        public SaveDataAnalyticsConsentState(string gameCenterAccountID)
        {
            m_gameCenterAccountID = gameCenterAccountID;
            m_consentState = AnalyticsConsentState.None;
            m_consentUpdatedTimestamp = Hardlight.TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
        }

        // Game.Runtime.dll 0x06002b41; timestamp is UTC milliseconds.
        public SaveDataAnalyticsConsentState(string gameCenterAccountID, AnalyticsConsentState consentState)
        {
            m_gameCenterAccountID = gameCenterAccountID;
            m_consentState = consentState;
            m_consentUpdatedTimestamp = Hardlight.TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
        }

        // Game.Runtime.dll 0x06002b42.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
        // 0x06002b42 has no additional native work.
        }

        // Game.Runtime.dll 0x06002b43.
        public void OnBeforeSerialize()
        {
        // 0x06002b43 has no additional native work.
        }

        // Game.Runtime.dll 0x06002b44.
        public void OnAfterDeserialize()
        {
            Initialise();
        }

        // Game.Runtime.dll 0x06002b45.
        public override void ResolveNewData(SaveDataAnalyticsConsentState newSaveDataAnalyticsConsentState)
        {
            if (m_gameCenterAccountID != newSaveDataAnalyticsConsentState.m_gameCenterAccountID)
                return;
            if (m_consentUpdatedTimestamp < newSaveDataAnalyticsConsentState.m_consentUpdatedTimestamp)
            {
                m_consentState = newSaveDataAnalyticsConsentState.m_consentState;
                m_consentUpdatedTimestamp = newSaveDataAnalyticsConsentState.m_consentUpdatedTimestamp;
            }
            else if (m_consentUpdatedTimestamp == 0L && newSaveDataAnalyticsConsentState.m_consentUpdatedTimestamp == 0L && m_consentState != newSaveDataAnalyticsConsentState.m_consentState)
            {
                m_consentState = AnalyticsConsentState.None;
            }
        }
    }
}
