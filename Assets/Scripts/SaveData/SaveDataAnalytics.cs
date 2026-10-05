using System;
using System.Collections.Generic;
using Hardlight.Analytics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SaveDataAnalytics : SaveDataResolvableItem<SaveDataAnalytics>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_lastKnownSessionID;
        [SerializeField]
        private int m_lastKnownSessionNumber;
        [SerializeField]
        private long m_sessionStartTimestamp;
        [SerializeField]
        private long m_sessionSuspendedTimestamp;
        [SerializeField]
        private float m_currentSessionLengthSeconds;
        [SerializeField]
        private string m_installDate;
        [SerializeField]
        private List<SaveDataAnalyticsConsentState> m_consentStatesList = new List<SaveDataAnalyticsConsentState>();
        private Dictionary<string, SaveDataAnalyticsConsentState> m_consentStates = new Dictionary<string, SaveDataAnalyticsConsentState>();
        // Game.Runtime.dll 0x06002b36.
        public SaveDataAnalytics()
        {
        // 0x06002b36 has no additional native work.
        }

        // Game.Runtime.dll 0x06002b21.
        public IReadOnlyList<SaveDataAnalyticsConsentState> ConsentStates
        {
            get
            {
                return m_consentStatesList;
            }
        }

        // Game.Runtime.dll 0x06002b22.
        public int SessionNumber
        {
            get
            {
                return m_lastKnownSessionNumber;
            }

            // 0x06002b23
            set
            {
                if (m_lastKnownSessionNumber == value)
                    return;
                m_lastKnownSessionNumber = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b24.
        public string SessionID
        {
            get
            {
                return m_lastKnownSessionID;
            }

            // 0x06002b25
            set
            {
                if (m_lastKnownSessionID == value)
                    return;
                m_lastKnownSessionID = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b26.
        public long SessionStartTimestamp
        {
            get
            {
                return m_sessionStartTimestamp;
            }

            // 0x06002b27
            set
            {
                if (m_sessionStartTimestamp == value)
                    return;
                m_sessionStartTimestamp = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b28.
        public long SessionSuspendedTimestamp
        {
            get
            {
                return m_sessionSuspendedTimestamp;
            }

            // 0x06002b29
            set
            {
                if (m_sessionSuspendedTimestamp == value)
                    return;
                m_sessionSuspendedTimestamp = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b2a.
        public float CurrentSessionLengthSeconds
        {
            get
            {
                return m_currentSessionLengthSeconds;
            }

            // 0x06002b2b
            set
            {
                if (m_currentSessionLengthSeconds == value)
                    return;
                m_currentSessionLengthSeconds = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b2c.
        public string InstallDate
        {
            get
            {
                return m_installDate;
            }

            // 0x06002b2d
            set
            {
                if (m_installDate == value)
                    return;
                m_installDate = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b2e.
        public override void Initialise()
        {
            if (m_consentStates == null)
                m_consentStates = new Dictionary<string, SaveDataAnalyticsConsentState>();
            base.Initialise();
        }

        // Game.Runtime.dll 0x06002b2f.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
        // 0x06002b2f has no additional native work.
        }

        // Game.Runtime.dll 0x06002b30.
        public void OnBeforeSerialize()
        {
            DictionaryToList(m_consentStates, ref m_consentStatesList);
        }

        // Game.Runtime.dll 0x06002b31.
        public void OnAfterDeserialize()
        {
            Initialise();
            ListToDictionary(m_consentStatesList, m_consentStates, entry => entry.GameCenterAccountID);
        }

        // Game.Runtime.dll 0x06002b32.
        public override void ResolveNewData(SaveDataAnalytics newSaveDataAnalytics)
        {
            // 0x06002b32 merges consent entries, preserving session fields and the current dirty flag.
            ResolveConsentDataConflicts(newSaveDataAnalytics);
        }

        // Game.Runtime.dll 0x06002b33.
        private void ResolveConsentDataConflicts(SaveDataAnalytics newSaveDataAnalytics)
        {
            foreach (KeyValuePair<string, SaveDataAnalyticsConsentState> pair in newSaveDataAnalytics.m_consentStates)
            {
                if (m_consentStates.TryGetValue(pair.Key, out SaveDataAnalyticsConsentState current))
                    current.ResolveNewData(pair.Value);
                else
                    m_consentStates[pair.Key] = pair.Value;
            }
        }

        // Game.Runtime.dll 0x06002b34.
        public SaveDataAnalyticsConsentState GetOrCreateConsentData(string gameCenterAccountID)
        {
            if (!m_consentStates.TryGetValue(gameCenterAccountID, out SaveDataAnalyticsConsentState data))
            {
                data = new SaveDataAnalyticsConsentState(gameCenterAccountID);
                m_consentStates[gameCenterAccountID] = data;
                MarkDirty();
            }

            return data;
        }

        // Game.Runtime.dll 0x06002b35.
        public void SetConsentData(string gameCenterAccountID, AnalyticsConsentState consentState, long updatedTimestamp = 0L)
        {
            if (m_consentStates.TryGetValue(gameCenterAccountID, out SaveDataAnalyticsConsentState data))
            {
                data.ConsentState = consentState;
                data.ConsentUpdatedTimestamp = updatedTimestamp > 0L ? updatedTimestamp : Hardlight.TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
            }
            else
                m_consentStates[gameCenterAccountID] = new SaveDataAnalyticsConsentState(gameCenterAccountID, consentState);
            MarkDirty();
        }
    }
}
