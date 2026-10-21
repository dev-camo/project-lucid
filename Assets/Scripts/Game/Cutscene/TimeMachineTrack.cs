using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HardlightProject
{
    // Original Game.Runtime 0200080c; native 0x5FCD50.
    [TrackClipType(typeof(TimeMachineClip))]
    [TrackColor(0.7366781f, 0.3261246f, 0.8529412f)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TimeMachineTrack : TrackAsset
    {
        // Original06002e8b: labels every clip for the timeline editor and gives the mixer its marker lookup. Marker
        // and jump-to-end clips are registered by their label (the first clip with a label wins).
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject gameObject, int inputCount)
        {
            ScriptPlayable<TimeMachineMixerBehaviour> playable = ScriptPlayable<TimeMachineMixerBehaviour>.Create(graph, inputCount);
            TimeMachineMixerBehaviour mixer = playable.GetBehaviour();
            mixer.MarkerClips = new Dictionary<string, TimelineClip>();
            foreach (TimelineClip clip in GetClips())
            {
                TimeMachineBehaviour behaviour = ((TimeMachineClip)clip.asset).Behaviour;
                string displayName = clip.displayName;
                switch (behaviour.Action)
                {
                    case TimeMachineBehaviour.TimeMachineAction.Marker:
                        displayName = "● " + (string.IsNullOrEmpty(behaviour.MarkerLabel) ? "<add label>" : behaviour.MarkerLabel);
                        if (behaviour.MarkerLabel != null && !mixer.MarkerClips.ContainsKey(behaviour.MarkerLabel))
                            mixer.MarkerClips.Add(behaviour.MarkerLabel, clip);
                        break;
                    case TimeMachineBehaviour.TimeMachineAction.JumpToTime:
                        displayName = "→ (t) " + behaviour.TimeToJumpTo.ToString();
                        break;
                    case TimeMachineBehaviour.TimeMachineAction.JumpToMarker:
                        displayName = "→ ● " + (string.IsNullOrEmpty(behaviour.MarkerToJumpTo) ? "<add label>" : behaviour.MarkerToJumpTo);
                        break;
                    case TimeMachineBehaviour.TimeMachineAction.JumpToEnd:
                        displayName = "⇥ " + (string.IsNullOrEmpty(behaviour.MarkerLabel) ? "<add label>" : behaviour.MarkerLabel);
                        if (behaviour.MarkerLabel != null && !mixer.MarkerClips.ContainsKey(behaviour.MarkerLabel))
                            mixer.MarkerClips.Add(behaviour.MarkerLabel, clip);
                        break;
                    case TimeMachineBehaviour.TimeMachineAction.Pause:
                        displayName = "||";
                        break;
                    case TimeMachineBehaviour.TimeMachineAction.DoNothing:
                        displayName = "-";
                        break;
                }
                clip.displayName = displayName;
            }
            return playable;
        }

        // Implicit original06002e8c.
    }
}
