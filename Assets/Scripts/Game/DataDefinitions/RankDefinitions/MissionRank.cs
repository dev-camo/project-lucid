using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    public sealed class MissionRank : BaseRank, IComparable<MissionRank>
    {
        [SerializeField] [Range(0f, 60f)] private float m_minutes;
        [Range(0f, 60f)] [SerializeField] private float m_seconds;
        [Range(0f, 99f)] [SerializeField] private float m_hundredths;
        [InspectorReadOnly] [SerializeField] private float m_timeInSeconds;
        [SerializeField] [InspectorReadOnly] private string m_formattedTime;

        // Original Game.Runtime 0x06001edc, ARM64 0x52cf90..0x52cf98.
        public float TimeInSeconds => m_timeInSeconds;

        // Original 0x06001edd, ARM64 0x52cf98..0x52d070.
        public void Validate()
        {
            TimeSpan time = GetTimeSpan();
            m_timeInSeconds = Convert.ToSingle(time.TotalSeconds);
            m_formattedTime = time.ToString();
        }

        // Original 0x06001ede, ARM64 0x52d070..0x52d150. The hundredths
        // multiplication is a float operation before conversion to double.
        // Each individual TimeSpan factory and Add preserves its own rounding.
        private TimeSpan GetTimeSpan()
        {
            TimeSpan time = new TimeSpan();
            time = time.Add(TimeSpan.FromMinutes(m_minutes));
            time = time.Add(TimeSpan.FromSeconds(m_seconds));
            time = time.Add(TimeSpan.FromMilliseconds(m_hundredths * 10f));
            return time;
        }

        // Original 0x06001edf, ARM64 0x52d150..0x52d1ac. Zero self-time
        // returns1 even for null; a nonzero self-time dereferences other.
        public int CompareTo(MissionRank other)
        {
            if (m_timeInSeconds == 0f)
                return 1;
            if (m_timeInSeconds > other.m_timeInSeconds)
                return 1;
            if (m_timeInSeconds < other.m_timeInSeconds || other.m_timeInSeconds == 0f)
                return -1;
            return 0;
        }

        // Implicit original 0x06001ee0, ARM64 0x52d1ac..0x52d244: base only.
    }
}
