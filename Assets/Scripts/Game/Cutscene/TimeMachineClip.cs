using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HardlightProject
{
    // Original Game.Runtime 02000808; native 0x5FC4F0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TimeMachineClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private TimeMachineBehaviour m_timeMachinePlayableAsset = new TimeMachineBehaviour();

        // Original06002e7d.
        public TimeMachineBehaviour Behaviour => m_timeMachinePlayableAsset;

        // Original06002e7e.
        public ClipCaps clipCaps => ClipCaps.None;

        // Original06002e7f.
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<TimeMachineBehaviour>.Create(graph, m_timeMachinePlayableAsset, 0);
        }

        // Implicit original06002e80: the authored behaviour exists before the base constructor runs.
    }
}
