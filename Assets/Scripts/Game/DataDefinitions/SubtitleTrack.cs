using TMPro;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HardlightProject
{
    // Original Game.Runtime 0200044c; native 0x7A64E0.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [TrackBindingType(typeof(TextMeshProUGUI))]
    [TrackClipType(typeof(SubtitleClip))]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SubtitleTrack : TrackAsset
    {
        // Original06001958.
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject _, int inputCount)
        {
            return ScriptPlayable<SubtitleMixer>.Create(graph, inputCount);
        }

        // Implicit original06001959: TrackAsset base constructor only.
    }
}
