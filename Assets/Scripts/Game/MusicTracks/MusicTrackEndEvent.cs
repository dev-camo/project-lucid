using Hardlight;
using Hardlight.Enums;

namespace HardlightProject
{
    // Original02000726: the whole mutable value owner, two APIs and five fields.
    public struct MusicTrackEndEvent
    {
        public readonly MusicStoppedReason Reason;
        public readonly int TrackLengthSeconds;
        public readonly int TrackListenedSeconds;
        public readonly bool WasIdleStart;
        public UIContainerCollectionsMusic.MusicTrackCache TrackCache;

        // Original0600283c retains ordinary unchecked Single-to-Int32 truncation.
        // Shipping ARM/x86 exceptional-value conversion instructions differ; no parity is claimed for them.
        public MusicTrackEndEvent(MusicStoppedReason reason, float trackLengthSeconds,
            float trackListenedSeconds, bool wasIdleStart)
        {
            Reason = reason;
            TrackLengthSeconds = unchecked((int)trackLengthSeconds);
            TrackListenedSeconds = unchecked((int)trackListenedSeconds);
            WasIdleStart = wasIdleStart;
            TrackCache = default;
        }

        // Original0600283d: original Unity null equality precedes the enum-name/string operations.
        // The real nested cache/enclosing UI owner is a remaining source provider, never a shell.
        public string GetGameLocation()
        {
            GameplayLevelDefinition level = TrackCache.LevelDefinition;
            if (level == null)
                return string.Empty;
            string zone = level.DisplayName.GetString().TrimEndString("_NAME");
            return string.Concat(zone, "-", level.ActName.GetString());
        }
    }
}
