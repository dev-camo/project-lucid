using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HardlightProject
{
    // Original Game.Runtime 02000430; bodies translated from the 1.10.1 x86-64 binary.
    [TrackClipType(typeof(ComicPanelClip))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ComicPanelTrack : TrackAsset
    {
        // 0x0600189f (0x79EA50): the track's own description names its mixer, so every
        // track keeps a distinct panel dictionary in the comic cutscene.
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject _, int inputCount)
        {
            ScriptPlayable<ComicPanelMixer> playable = ScriptPlayable<ComicPanelMixer>.Create(graph, inputCount);
            playable.GetBehaviour().SetUniqueTrackHandle(ToString());
            return playable;
        }
    }
}
