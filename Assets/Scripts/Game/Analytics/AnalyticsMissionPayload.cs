using System.Collections.Generic;
using Hardlight;

namespace HardlightProject
{
    // Original02000096: the whole readonly value owner, two APIs and four fields.
    public readonly struct AnalyticsMissionPayload
    {
        public readonly IMissionContext MissionContext;
        public readonly MissionState MissionState;
        public readonly Character Character;
        private readonly IReadOnlyDictionary<int, DreamPowerDefinition> m_dreamPowerLoadout;

        // Original0600050c: reference stores retain the original field order.
        public AnalyticsMissionPayload(IMissionContext missionContext, MissionState missionState, Character character,
            IReadOnlyDictionary<int, DreamPowerDefinition> dreamPowerLoadout)
        {
            MissionContext = missionContext;
            MissionState = missionState;
            Character = character;
            m_dreamPowerLoadout = dreamPowerLoadout;
        }

        // Original0600050d: dictionary iteration order survives; no Sort is present on this path.
        public string GetDreamPowerSlotsAnalytics()
        {
            List<string> powers = new List<string>();
            foreach (var entry in m_dreamPowerLoadout)
            {
                entry.Deconstruct(out int slot, out DreamPowerDefinition definition);
                powers.Add(definition.GetStrippedDefinitionName());
            }
            return powers.Join();
        }
    }
}
