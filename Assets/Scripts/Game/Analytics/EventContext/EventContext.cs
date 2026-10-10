using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime02000025:36 own APIs (34 concrete, two abstract).
    // All original primary/generic physical bodies are retained; source intent remains unaccepted.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class EventContext<T> : AnalyticsEvent<T>, IEventContext
        where T : EventContext<T>, new()
    {
        private long m_evtm;
        private string m_gid;
        private string m_clientVersion;
        private string m_sessionID;
        private int m_eventIDX;
        private int m_orbTotal;
        private int? m_achievementStatueTotal;
        private int? m_moonTotal;
        private int? m_zonesTotal;
        private int? m_dreamPowerTier;
        private string m_blueCoinsWalletTotalRange;
        private string m_xpAmountTotalRange;
        private bool? m_challengesState;

        // Original060000c4 and060000e5:actual abstract extension points.
        protected abstract string EventName { get; }

        // Original060000c5:Create, virtual fill, return; no cleanup is added on a fault.
        private JSONHashtable EventDetail
        {
            get
            {
                JSONHashtable data = JSONHashtable.Create();
                FillEventDetail(data);
                return data;
            }
        }

        // Original060000c6:Create, FillSession, return.
        private JSONHashtable Session
        {
            get
            {
                JSONHashtable data = JSONHashtable.Create();
                FillSession(data);
                return data;
            }
        }

        long IEventContext.EVTM { get => m_evtm; set => m_evtm = value; }
        string IEventContext.GID { get => m_gid; set => m_gid = value; }
        string IEventContext.ClientVersion { get => m_clientVersion; set => m_clientVersion = value; }
        string IEventContext.SessionID { get => m_sessionID; set => m_sessionID = value; }
        int IEventContext.EventIDX { get => m_eventIDX; set => m_eventIDX = value; }
        int IEventContext.OrbTotal { get => m_orbTotal; set => m_orbTotal = value; }
        int? IEventContext.AchievementStatueTotal { get => m_achievementStatueTotal; set => m_achievementStatueTotal = value; }
        int? IEventContext.MoonTotal { get => m_moonTotal; set => m_moonTotal = value; }
        int? IEventContext.ZonesTotal { get => m_zonesTotal; set => m_zonesTotal = value; }
        int? IEventContext.DreamPowerTier { get => m_dreamPowerTier; set => m_dreamPowerTier = value; }
        string IEventContext.BlueCoinsWalletTotalRange { get => m_blueCoinsWalletTotalRange; set => m_blueCoinsWalletTotalRange = value; }
        string IEventContext.XpAmountTotalRange { get => m_xpAmountTotalRange; set => m_xpAmountTotalRange = value; }
        bool? IEventContext.ChallengesState { get => m_challengesState; set => m_challengesState = value; }

        // Original060000e1:base first, then the thirteen snapshot reads in original field order.
        protected override void Initialise()
        {
            base.Initialise();
            m_evtm = EventContextDataProvider.EVTM;
            m_gid = EventContextDataProvider.GID;
            m_clientVersion = EventContextDataProvider.ClientVersion;
            m_sessionID = EventContextDataProvider.SessionID;
            m_eventIDX = EventContextDataProvider.EventIDX;
            m_orbTotal = EventContextDataProvider.OrbTotal;
            m_achievementStatueTotal = EventContextDataProvider.AchievementStatueTotal;
            m_moonTotal = EventContextDataProvider.MoonTotal;
            m_zonesTotal = EventContextDataProvider.ZonesTotal;
            m_dreamPowerTier = EventContextDataProvider.DreamPowerTier;
            m_blueCoinsWalletTotalRange = EventContextDataProvider.BlueCoinsWalletTotalRange;
            m_xpAmountTotalRange = EventContextDataProvider.XpAmountTotalRange;
            m_challengesState = EventContextDataProvider.ChallengesState;
        }

        // Original060000e2:short circuit preserves the original base callback evaluation first.
        protected sealed override bool IsLateDataReady() =>
            base.IsLateDataReady() && EventContextDataProvider.IsEventContextLateDataReady();

        // Original060000e3:the base late-data callback runs before context refresh.
        protected sealed override void SetLateData()
        {
            base.SetLateData();
            EventContextDataProvider.SetEventContextLateData(this);
        }

        // Original060000e4:three ordered Adds; detail/session creation is interleaved with each Add.
        protected sealed override void FillData(JSONHashtable data)
        {
            data.Add("eventName", EventName);
            data.Add("eventDetail", EventDetail);
            data.Add("session", Session);
        }

        protected abstract void FillEventDetail(JSONHashtable eventDetail);

        // Original060000e6:fixed session-key order and original nullable omissions.
        private void FillSession(JSONHashtable data)
        {
            data.Add("evtm", m_evtm);
            data.Add("gid", m_gid);
            data.Add("client_version", m_clientVersion);
            data.Add("session_id", m_sessionID);
            data.Add("event_idx", m_eventIDX);
            data.Add("orb_total", m_orbTotal);
            if (m_achievementStatueTotal.HasValue)
                data.Add("achievement_statue_total", m_achievementStatueTotal.Value);
            if (m_moonTotal.HasValue)
                data.Add("moon_total", m_moonTotal.Value);
            if (m_zonesTotal.HasValue)
                data.Add("zones_total", m_zonesTotal.Value);
            if (m_dreamPowerTier.HasValue)
                data.Add("dream_power_tier", m_dreamPowerTier.Value);
            data.Add("blue_coins_wallet_total_range", m_blueCoinsWalletTotalRange);
            data.Add("xp_amount_total_range", m_xpAmountTotalRange);
            if (m_challengesState.HasValue)
                data.Add("challenges_state", m_challengesState.Value);
        }

        // Original060000e7:base-only constructor, no own initializers.
        protected EventContext() { }
    }
}
