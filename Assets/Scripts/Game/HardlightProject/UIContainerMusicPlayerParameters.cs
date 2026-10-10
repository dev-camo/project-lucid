using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020008ef, complete seven readonly fields and one constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerMusicPlayerParameters : IUIContainerParameters
    {
        public readonly MusicTrackDefinition MusicTrackDefinition;
        public readonly GameplayLevelDefinition LevelDefinition;
        public readonly int Number;
        public readonly bool Locked;
        public readonly Action PlayNext;
        public readonly Action PlayPrevious;
        public readonly Action Shuffle;

        // Original 060033ad: base constructor, then the original field order;
        // no optional defaults, validation or callback fallback.
        public UIContainerMusicPlayerParameters(MusicTrackDefinition musicTrackDefinition,
            GameplayLevelDefinition levelDefinition, int number, bool locked,
            Action playNext, Action playPrevious, Action shuffle)
        {
            MusicTrackDefinition = musicTrackDefinition;
            LevelDefinition = levelDefinition;
            Number = number;
            Locked = locked;
            PlayNext = playNext;
            PlayPrevious = playPrevious;
            Shuffle = shuffle;
        }
    }
}
