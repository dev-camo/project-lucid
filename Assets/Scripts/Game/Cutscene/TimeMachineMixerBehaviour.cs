using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HardlightProject
{
    // Original Game.Runtime 02000809; native 0x5FC620.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeMachineMixerBehaviour : PlayableBehaviour
    {
        private Dictionary<string, TimelineClip> m_markerClips;

        // Originals 06002e81/06002e82.
        public Dictionary<string, TimelineClip> MarkerClips
        {
            get => m_markerClips;
            set => m_markerClips = value;
        }

        // Original06002e83: only while playing. Every not-yet-executed clip with positive weight whose condition is met
        // (and that has a director) executes once, in input order; later clips in the same frame override earlier time changes.
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying) return;
            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                ScriptPlayable<TimeMachineBehaviour> input = (ScriptPlayable<TimeMachineBehaviour>)playable.GetInput(i);
                TimeMachineBehaviour behaviour = input.GetBehaviour();
                if (inputWeight > 0f && !behaviour.ClipExecuted)
                {
                    if (!behaviour.ConditionMet()) continue;
                    if (!(playable.GetGraph().GetResolver() is PlayableDirector director)) continue;
                    behaviour.ClipExecuted = true;
                    switch (behaviour.Action)
                    {
                        case TimeMachineBehaviour.TimeMachineAction.JumpToTime:
                            director.time = behaviour.TimeToJumpTo;
                            break;
                        case TimeMachineBehaviour.TimeMachineAction.JumpToMarker:
                            director.time = MarkerClips[behaviour.MarkerToJumpTo].start;
                            break;
                        case TimeMachineBehaviour.TimeMachineAction.JumpToEnd:
                            director.time = MarkerClips[behaviour.MarkerLabel].end - behaviour.EndSoftenTime;
                            break;
                        case TimeMachineBehaviour.TimeMachineAction.Pause:
                            director.Pause();
                            break;
                    }
                }
            }
        }

        // Implicit original06002e84.
    }
}
