using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime 0200080b; native 0x5FCBC0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TimeMachineSkipClip : TimeMachineClip
    {
        [SerializeField] private bool m_skipEnabledState = true;

        // Original06002e89: the authored behaviour is copied into a skip behaviour carrying this clip's skip state.
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            TimeMachineSkipBehaviour behaviour = new TimeMachineSkipBehaviour(Behaviour, m_skipEnabledState);
            return ScriptPlayable<TimeMachineSkipBehaviour>.Create(graph, behaviour, 0);
        }

        // Implicit original06002e8a.
    }
}
